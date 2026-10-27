#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KingOfDemonEncounterSetup
{
    private const string Pack = "Assets/Character/KingOfDemon";
    private static readonly Color Gold = new Color(.94f, .76f, .39f);

    [MenuItem("Tools/Enemies/King Of Demon/Install Boss Encounter and HUD")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype first.");
        var boss = GameObject.Find("KingOfDemon");
        var hud = UnityEngine.Object.FindAnyObjectByType<AdventureHudUI>();
        if (boss == null || hud == null) throw new InvalidOperationException("Existing KingOfDemon and player HUD are required.");
        Directory.CreateDirectory("Logs/SceneBackups");
        string backup = "Logs/SceneBackups/WorldPrototype_BeforeDemonBoss_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Cannot back up the active scene.");
        var position = boss.transform.position;
        var rotation = boss.transform.rotation;
        var scale = boss.transform.localScale;
        Undo.RegisterFullObjectHierarchyUndo(boss, "Install demon boss encounter");

        var animator = boss.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Pack + "/KingOfDemon.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (boss.TryGetComponent(out BossSkillTester tester)) tester.enabled = false;
        var effects = Get<BossVFXController>(boss);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "/KingOfDemon.prefab").GetComponent<BossVFXController>();
        EditorUtility.CopySerialized(source, effects);
        effects.enabled = true;

        float uniformScale = Mathf.Abs(boss.transform.lossyScale.y);
        var renderers = boss.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float height = Mathf.Clamp(bounds.size.y * .9f, 2.1f, 4f);
        var collider = Get<CapsuleCollider>(boss);
        collider.height = height / uniformScale;
        collider.radius = .6f / uniformScale;
        collider.center = Vector3.up * height * .5f / uniformScale;
        collider.isTrigger = false;
        var body = Get<Rigidbody>(boss);
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        var agent = Get<NavMeshAgent>(boss);
        agent.agentTypeID = 0;
        agent.radius = .6f;
        agent.height = height;
        agent.speed = 3.5f;
        agent.acceleration = 14f;
        agent.angularSpeed = 240f;
        agent.stoppingDistance = 2.2f;
        agent.updateRotation = false;
        agent.avoidancePriority = 25;
        var health = Get<EnemyHealth>(boss);
        Set(health, "maxHealth", 1200f);
        Set(health, "allowInstantKill", false);
        var targetable = Get<Targetable>(boss);
        var aim = boss.transform.Find("Boss_AimPoint");
        if (aim == null) { aim = new GameObject("Boss_AimPoint").transform; aim.SetParent(boss.transform, false); }
        aim.localPosition = Vector3.up * Mathf.Min(height * .6f, 1.55f) / uniformScale;
        Ref(targetable, "aimPoint", aim);
        foreach (var child in boss.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("Enemy");

        var zone = UnityEngine.Object.FindAnyObjectByType<BossArenaZone>();
        if (zone == null)
        {
            var arena = new GameObject("KingOfDemon_Arena");
            Undo.RegisterCreatedObjectUndo(arena, "Create boss encounter bounds");
            zone = arena.AddComponent<BossArenaZone>();
        }
        var floor = GameObject.Find("World/Map_WindingForest/DemonLava_LiquidLake_And_ChannelStreams/Cylinder (2)");
        var box = zone.GetComponent<BoxCollider>();
        if (floor != null)
        {
            var mesh = floor.GetComponent<MeshFilter>().sharedMesh.bounds;
            var top = floor.transform.TransformPoint(mesh.center + Vector3.up * mesh.extents.y);
            zone.transform.SetPositionAndRotation(top, Quaternion.Euler(0, floor.transform.eulerAngles.y, 0));
            box.size = new Vector3(Mathf.Abs(mesh.size.x * floor.transform.lossyScale.x) - 5f, 8f,
                Mathf.Abs(mesh.size.z * floor.transform.lossyScale.z) - 5f);
        }
        else
        {
            zone.transform.SetPositionAndRotation(position, Quaternion.identity);
            box.size = new Vector3(26, 8, 26);
        }
        box.center = new Vector3(0, 3, 0);
        box.isTrigger = true;
        var ai = Get<KingOfDemonAI>(boss);
        Ref(ai, "arena", zone);
        Ref(ai, "animator", animator);
        Ref(ai, "effects", effects);
        Ref(zone, "area", box);
        Ref(zone, "boss", ai);
        if (!zone.Contains(position)) throw new InvalidOperationException("Boss must be inside the authored arena bounds.");

        BuildHud(hud.transform, ai);
        foreach (var component in boss.GetComponents<Component>())
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        EditorUtility.SetDirty(zone);
        EditorUtility.SetDirty(box);
        PrefabUtility.SaveAsPrefabAsset(boss, "Assets/_Project/Prefabs/Enemies/KingOfDemon_Boss.prefab");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/KingOfDemonEncounterSetup.txt", "Installed boss AI, 1200 HP, three skills, arena and top-centre HUD.\n" +
            "Backup=" + backup + "\nBoss position=" + position.ToString("F3") + "; scale=" + scale + "\nArena=" + zone.transform.position + "; size=" + box.size + "\n" +
            (boss.transform.position == position && boss.transform.rotation == rotation && boss.transform.localScale == scale ? "PASS" : "FAIL") +
            " user boss transform preserved\nOriginal model, animation clips, controller, textures and terrain unchanged.\n");
        Debug.Log("KingOfDemon encounter and HUD installed. Arena bounds are editable on KingOfDemon_Arena.");
    }

    private static T Get<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var value) ? value : Undo.AddComponent<T>(go);
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, float value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, bool value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    private static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
    {
        var image = Rect(name, parent, new Vector2(.5f, 1), position, size).gameObject.AddComponent<Image>();
        image.color = color; image.sprite = sprite; image.raycastTarget = false;
        return image;
    }
    private static void BuildHud(Transform hud, KingOfDemonAI boss)
    {
        var old = hud.Find("Boss Encounter HUD");
        if (old != null) { Ref(old.GetComponent<BossHealthUI>(), "boss", boss); return; }
        var root = Rect("Boss Encounter HUD", hud, new Vector2(.5f, 1), new Vector2(0, -132), new Vector2(680, 86));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
        var title = Rect("Boss Name", root, new Vector2(.5f, 1), Vector2.zero, new Vector2(660, 29)).gameObject.AddComponent<TextMeshProUGUI>();
        title.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        title.text = boss.DisplayName; title.fontSize = 23; title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4; title.alignment = TextAlignmentOptions.Center; title.color = Gold; title.raycastTarget = false;
        title.outlineColor = new Color(.06f, .025f, .02f); title.outlineWidth = .16f;
        var frame = hud.Find("Vitals/Health/Frame").GetComponent<Image>().sprite;
        var fillSprite = hud.Find("Vitals/Health/Fill").GetComponent<Image>().sprite;
        var bar = Rect("Boss Health Bar", root, new Vector2(.5f, 1), new Vector2(0, -29), new Vector2(680, 52));
        Picture("Empty", bar, new Vector2(0, -15), new Vector2(557, 22), new Color(.07f, .025f, .03f, .97f));
        var trail = Picture("Damage Trail", bar, new Vector2(0, -15), new Vector2(557, 22), new Color(1f, .58f, .15f));
        var fill = Picture("Health Fill", bar, new Vector2(0, -15), new Vector2(557, 22), new Color(.9f, .3f, .26f), fillSprite);
        foreach (var image in new[] { trail, fill }) { image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; }
        Picture("Frame", bar, Vector2.zero, new Vector2(680, 52), Color.white, frame);
        var ui = root.gameObject.AddComponent<BossHealthUI>();
        Ref(ui, "boss", boss); Ref(ui, "healthFill", fill); Ref(ui, "damageTrail", trail); Ref(ui, "bossName", title);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create boss health HUD");
    }
}
#endif
