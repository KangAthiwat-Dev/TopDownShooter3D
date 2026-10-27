#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class KingOfDemonSizeSetup
{
    [MenuItem("Tools/Enemies/Set DemonLord 2x and KingOfDemon 3x")]
    public static void ResizeBoth()
    {
        Resize();
        ResizeDemonLord();
    }

    [MenuItem("Tools/Enemies/King Of Demon/Resize Boss to Three Times Player Height")]
    public static void Resize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var boss = UnityEngine.Object.FindAnyObjectByType<KingOfDemonAI>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerAnimationController>();
        if (boss == null || player == null) throw new InvalidOperationException("Open the scene containing the player and boss.");
        float playerHeight = BodyHeight(player.gameObject);
        float originalHeight = BodyHeight(boss.gameObject);
        const float multiplier = 3f;
        float factor = playerHeight * multiplier / originalHeight;
        if (!float.IsFinite(factor) || factor < .1f || factor > 10f)
            throw new InvalidOperationException("Invalid measured character height: player=" + playerHeight + ", boss=" + originalHeight);
        string backup = "Logs/SceneBackups/WorldPrototype_BeforeBossResize_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        Directory.CreateDirectory("Logs/SceneBackups");
        if (!EditorSceneManager.SaveScene(boss.gameObject.scene, backup, true)) throw new IOException("Cannot back up the active scene.");
        Undo.RegisterFullObjectHierarchyUndo(boss.gameObject, "Resize demon boss to three times player height");
        var position = boss.transform.position;
        var rotation = boss.transform.rotation;
        var capsule = boss.GetComponent<CapsuleCollider>();
        var agent = boss.GetComponent<NavMeshAgent>();
        var effects = boss.GetComponent<BossVFXController>();
        float oldRadius = capsule.radius * Mathf.Abs(boss.transform.lossyScale.x);
        boss.transform.localScale *= factor;
        // Capsule and aim point inherit the body scale; navigation values are world-space dimensions.
        float newRadius = capsule.radius * Mathf.Abs(boss.transform.lossyScale.x);
        agent.radius = newRadius;
        agent.height = capsule.height * Mathf.Abs(boss.transform.lossyScale.y);
        var targetFields = new SerializedObject(boss.GetComponent<Targetable>());
        targetFields.FindProperty("closeRangeOffset").floatValue = Mathf.Max(0f, newRadius - .6f);
        targetFields.ApplyModifiedPropertiesWithoutUndo();
        var fields = new SerializedObject(boss);
        fields.FindProperty("meleeRange").floatValue += newRadius - oldRadius;
        fields.FindProperty("sightHeight").floatValue = agent.height * .75f;
        fields.ApplyModifiedPropertiesWithoutUndo();
        // Enlarging the body must not silently enlarge the wave's damage area or projectile colliders.
        effects.effectScaleMultiplier /= factor;
        foreach (var component in boss.GetComponents<Component>())
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        PrefabUtility.SaveAsPrefabAsset(boss.gameObject, "Assets/_Project/Prefabs/Enemies/KingOfDemon_Boss.prefab");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        EditorSceneManager.SaveScene(boss.gameObject.scene);
        float finalHeight = BodyHeight(boss.gameObject);
        File.WriteAllText("Logs/KingOfDemonSize.txt", "Player visual height=" + playerHeight.ToString("F3") + "m\n" +
            "Boss visual height=" + finalHeight.ToString("F3") + "m; ratio=" + (finalHeight / playerHeight).ToString("F3") + "\n" +
            "Root scale=" + boss.transform.localScale.ToString("F4") + "; nav height=" + agent.height.ToString("F3") + "; nav radius=" + agent.radius.ToString("F3") + "\n" +
            "Aim height=" + (boss.transform.Find("Boss_AimPoint").position.y - boss.transform.position.y).ToString("F3") + "\n" +
            (Mathf.Abs(finalHeight / playerHeight - multiplier) < .01f ? "PASS" : "FAIL") + " measured 3:1 visible height\n" +
            (boss.transform.position == position && boss.transform.rotation == rotation ? "PASS" : "FAIL") + " placement preserved\n" +
            "Damage values, health, speed, cooldowns and world-space VFX size preserved.\nBackup=" + backup + "\n");
        Debug.Log("Demon boss resized: " + finalHeight.ToString("F2") + "m / player " + playerHeight.ToString("F2") + "m. Collider, aim and navigation follow its new size.");
    }

    [MenuItem("Tools/Enemies/Demon Lord/Resize DemonLord to Twice Player Height")]
    public static void ResizeDemonLord()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerAnimationController>();
        if (player == null) throw new InvalidOperationException("Open the player scene first.");
        float playerHeight = BodyHeight(player.gameObject);
        const string path = "Assets/Character/DemonLord/DemonLord.prefab";
        Directory.CreateDirectory("Logs/AssetBackups");
        string backup = "Logs/AssetBackups/DemonLord_BeforeResize_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";
        File.Copy(path, backup, true);
        var prefab = PrefabUtility.LoadPrefabContents(path);
        float height;
        try
        {
            FitDemonLord(prefab, playerHeight * 2f);
            height = BodyHeight(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        var scene = player.gameObject.scene;
        var instances = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == "DemonLord" && !t.GetComponentsInParent<Transform>().Skip(1).Any(p => p.name == "DemonLord"))
            .ToArray();
        foreach (var instance in instances)
        {
            Undo.RegisterFullObjectHierarchyUndo(instance.gameObject, "Resize DemonLord to twice player height");
            FitDemonLord(instance.gameObject, playerHeight * 2f);
            foreach (var component in instance.GetComponents<Component>())
            {
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        AssetDatabase.SaveAssets();
        if (instances.Length > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        File.WriteAllText("Logs/DemonLordSize.txt", "Player visual height=" + playerHeight.ToString("F3") + "m\n" +
            "DemonLord prefab height=" + height.ToString("F3") + "m; ratio=" + (height / playerHeight).ToString("F3") + "\n" +
            (Mathf.Abs(height / playerHeight - 2f) < .01f ? "PASS" : "FAIL") + " measured 2:1 visible height\n" +
            "Updated existing scene instances=" + instances.Length + "\nPrefab=" + path + "\nBackup=" + backup + "\n");
        Debug.Log("DemonLord resized to twice player height. Prefab and " + instances.Length + " existing scene instances saved.");
    }

    private static void FitDemonLord(GameObject model, float targetHeight)
    {
        float factor = targetHeight / BodyHeight(model);
        if (!float.IsFinite(factor) || factor < .01f || factor > 100f) throw new InvalidOperationException("Invalid DemonLord model height.");
        model.transform.localScale *= factor;
        if (model.TryGetComponent(out NavMeshAgent agent))
        {
            agent.radius *= factor;
            agent.height *= factor;
        }
        EditorUtility.SetDirty(model.transform);
    }

    private static float BodyHeight(GameObject model)
    {
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true).Where(r =>
            r.enabled && r.gameObject.activeInHierarchy && (r is SkinnedMeshRenderer || r is MeshRenderer) &&
            !r.GetComponentsInParent<Transform>().Any(t =>
                t.name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("Rifle", StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            // World bounds include the imported rig's unit conversion and current body scale.
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            top = Mathf.Max(top, renderer.bounds.max.y);
        }
        return top - bottom;
    }
}
#endif
