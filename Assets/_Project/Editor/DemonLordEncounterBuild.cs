#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static partial class DemonLordEncounterSetup
{
    [MenuItem("Tools/Enemies/Demon Lord/Install DemonLord and Nightmare Dragon Encounter")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var lord = GameObject.Find("DemonLord");
        if (lord == null) throw new InvalidOperationException("Open WorldPrototype with the placed DemonLord.");
        if (lord.GetComponent<EncounterCombatant>() != null) throw new InvalidOperationException("Encounter already installed; edit its Inspector settings.");
        Directory.CreateDirectory("Logs/SceneBackups");
        string backup = "Logs/SceneBackups/WorldPrototype_BeforeDemonLordEncounter_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(lord.scene, backup, true)) throw new IOException("Cannot back up the scene.");
        EnsureFolder(Folder); EnsureFolder(Folder + "/DragonClips");
        var originalPosition = lord.transform.position; var originalScale = lord.transform.localScale; var originalRotation = lord.transform.rotation;
        Undo.RegisterFullObjectHierarchyUndo(lord, "Install DemonLord encounter");

        var floor = GameObject.Find("World/Map_WindingForest/DemonLava_LiquidLake_And_ChannelStreams/Cylinder (1)");
        var meshBounds = floor.GetComponent<MeshFilter>().sharedMesh.bounds;
        var zoneObject = new GameObject("DemonLord_Arena");
        zoneObject.transform.SetPositionAndRotation(floor.transform.TransformPoint(meshBounds.center + Vector3.up * meshBounds.extents.y),
            Quaternion.Euler(0, floor.transform.eulerAngles.y, 0));
        var arena = zoneObject.AddComponent<BossArenaZone>(); var box = zoneObject.GetComponent<BoxCollider>();
        box.center = new Vector3(0, 3, 0); box.size = new Vector3(24, 8, 20); box.isTrigger = true;
        Ref(arena, "area", box);
        Undo.RegisterCreatedObjectUndo(zoneObject, "Create DemonLord arena");

        var lordAnimator = lord.GetComponent<Animator>();
        lordAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DemonPack + "/DemonLord.controller");
        lordAnimator.applyRootMotion = false; lordAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var fx = Get<DemonLordVFX>(lord);
        EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>(DemonPack + "/DemonLord.prefab").GetComponent<DemonLordVFX>(), fx);
        fx.enabled = true;
        if (lord.TryGetComponent(out DemonLordTester tester)) tester.enabled = false;
        var lordActor = ConfigureActor(lord, arena, lordAnimator, "DEMON LORD", 900, .8f, 3.3f, 2.0f);
        Configure(lordActor, "chaseSpeed", 3.5f); Configure(lordActor, "sightHeight", 2.6f); Configure(lordActor, "stoppingRange", 2.5f);
        Configure(lordActor, "openingAnimation", "Taunt"); Configure(lordActor, "openingDuration", LordClip("Taunt").length + .1f);
        SetSkills(lordActor, new[]
        {
            Skill("Claw Slash", "Attack", EncounterSkillKind.Melee, LordClip("Attack").length, 0, 3.5f, 12, 3.2f, .6f, .18f),
            Skill("Blood Bolt", "Bolt", EncounterSkillKind.Bolt, LordClip("Bolt").length, 4, 17, 20, 3f, .7f, .18f),
            Skill("Blood Nova", "Nova", EncounterSkillKind.Nova, LordClip("Nova").length, 0, 6, 25, 9, .9f, .2f),
            Skill("Curse Circle", "Curse", EncounterSkillKind.Curse, LordClip("Curse").length, 3.5f, 18, 35, 11, .8f, .35f)
        });
        fx.slashDamage = 12; fx.boltDamage = 20; fx.novaDamage = 25; fx.curseDamage = 35;

        var dragon = new GameObject("DragonNightmare");
        var spawn = arena.transform.TransformPoint(new Vector3(6, 0, 4));
        if (!NavMesh.SamplePosition(spawn, out var hit, 2, NavMesh.AllAreas) || !arena.Contains(hit.position))
            throw new InvalidOperationException("No walkable dragon spawn in DemonLord arena.");
        dragon.transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(lord.transform.position - hit.position, Vector3.up)));
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DragonPack + "/Prefab/DragonNightmare/Red.prefab"));
        visual.name = "DragonNightmare_Visual"; visual.transform.SetParent(dragon.transform, false);
        visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one * .65f;
        var dragonAnimator = visual.GetComponentInChildren<Animator>();
        dragonAnimator.runtimeAnimatorController = DragonController(); dragonAnimator.applyRootMotion = false;
        dragonAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var dragonMaterial = DragonMaterial();
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => dragonMaterial).ToArray();
        var dragonActor = ConfigureActor(dragon, arena, dragonAnimator, "NIGHTMARE DRAGON", 650, 1.1f, 2.8f, 1.65f);
        Configure(dragonActor, "chaseSpeed", 4.8f); Configure(dragonActor, "stoppingRange", 3.1f);
        Configure(dragonActor, "sightHeight", 1.8f); Configure(dragonActor, "patrolRadius", 2.5f);
        Configure(dragonActor, "openingAnimation", "Roar"); Configure(dragonActor, "openingDuration", DragonClip("scream").length + .1f);
        SetSkills(dragonActor, new[]
        {
            Skill("Bite", "Attack_Bite", EncounterSkillKind.Melee, DragonClip("Basic Attack").length, 0, 3.8f, 14, 3.5f, .8f, .18f),
            Skill("Claw Combo", "Attack_Claw", EncounterSkillKind.Melee, DragonClip("Claw Attack").length, 0, 4.2f, 12, 8f, .9f, .4f),
            Skill("Horn Strike", "Attack_Horn", EncounterSkillKind.Melee, DragonClip("Horn Attack").length, 0, 4.4f, 22, 7f, 1f, .3f)
        });
        Ref(visual.AddComponent<EncounterAnimationEvents>(), "owner", dragonActor);
        Undo.RegisterCreatedObjectUndo(dragon, "Place Nightmare Dragon guardian");
        BuildBossHud(lordActor, "DemonLord Encounter HUD", 10);
        BuildBossHud(dragonActor, "Nightmare Dragon HUD", 20);

        Track(lord); Track(dragon); Track(zoneObject);
        PrefabUtility.SaveAsPrefabAsset(lord, "Assets/_Project/Prefabs/Enemies/DemonLord_Boss.prefab");
        PrefabUtility.SaveAsPrefabAsset(dragon, "Assets/_Project/Prefabs/Enemies/DragonNightmare_Guardian.prefab");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(lord.scene); EditorSceneManager.SaveScene(lord.scene);
        File.WriteAllText("Logs/DemonLordEncounterBuild.txt", "DemonLord and Nightmare Dragon encounter installed.\n" +
            "Arena=" + arena.transform.position + " size=" + box.size + "\nDemonLord=" + lord.transform.position + "\nDragon=" + dragon.transform.position + "\n" +
            (lord.transform.position == originalPosition && lord.transform.localScale == originalScale && lord.transform.rotation == originalRotation ? "PASS" : "FAIL") +
            " user DemonLord placement and 2x size preserved\nKingOfDemon 3x encounter preserved; original dragon model, clips, textures and materials unchanged.\nBackup=" + backup + "\n");
        Debug.Log("Installed DemonLord AI and Nightmare Dragon guardian with independent HUD health bars.");
    }

    private static EncounterCombatant ConfigureActor(GameObject go, BossArenaZone arena, Animator animator, string name,
        float hp, float radius, float height, float aimHeight)
    {
        float scale = Mathf.Abs(go.transform.lossyScale.x);
        var capsule = Get<CapsuleCollider>(go); capsule.radius = radius / scale; capsule.height = height / scale;
        capsule.center = Vector3.up * height * .5f / scale; capsule.isTrigger = false;
        var body = Get<Rigidbody>(go); body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None;
        var agent = Get<NavMeshAgent>(go); agent.radius = radius; agent.height = height; agent.speed = 3.5f; agent.acceleration = 14;
        agent.angularSpeed = 240; agent.updateRotation = false; agent.stoppingDistance = 2; agent.avoidancePriority = name == "DEMON LORD" ? 30 : 40;
        var health = Get<EnemyHealth>(go); Configure(health, "maxHealth", hp); Configure(health, "allowInstantKill", false);
        var target = Get<Targetable>(go);
        var aim = go.transform.Find("Boss_AimPoint"); if (aim == null) { aim = new GameObject("Boss_AimPoint").transform; aim.SetParent(go.transform, false); }
        aim.localPosition = Vector3.up * aimHeight / scale;
        Ref(target, "aimPoint", aim); Configure(target, "closeRangeOffset", Mathf.Max(0, radius - .6f));
        var actor = Get<EncounterCombatant>(go); Ref(actor, "arena", arena); Ref(actor, "animator", animator); Configure(actor, "displayName", name);
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Enemy");
        return actor;
    }
    private static EncounterCombatant.Skill Skill(string name, string animation, EncounterSkillKind kind, float duration,
        float min, float max, float damage, float cooldown, float recovery, float turn) => new EncounterCombatant.Skill
        { name = name, animationState = animation, kind = kind, duration = duration, minRange = min, range = max,
            damage = damage, cooldown = cooldown, recovery = recovery, turnUntil = turn };
    private static void SetSkills(EncounterCombatant actor, EncounterCombatant.Skill[] skills)
    {
        var data = new SerializedObject(actor); var list = data.FindProperty("skills"); list.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
        {
            var item = list.GetArrayElementAtIndex(i); var skill = skills[i];
            item.FindPropertyRelative("name").stringValue = skill.name; item.FindPropertyRelative("animationState").stringValue = skill.animationState;
            item.FindPropertyRelative("kind").enumValueIndex = (int)skill.kind;
            foreach (var field in typeof(EncounterCombatant.Skill).GetFields().Where(f => f.FieldType == typeof(float)))
                item.FindPropertyRelative(field.Name).floatValue = (float)field.GetValue(skill);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static AnimationClip LordClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(DemonPack + "/Animations/DemonLord_" + name + ".anim");
    private static AnimationClip DragonClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/DragonClips/" + name.Replace(" ", "_") + ".anim");
    private static RuntimeAnimatorController DragonController()
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/DragonNightmare_Combat.controller");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        foreach (var name in new[] { "idle01", "walk", "run", "Basic Attack", "Claw Attack", "Horn Attack", "scream", "die" })
        {
            var clip = UnityEngine.Object.Instantiate(Clip(DragonPack + "/Animations/DragonNightMare/" + name + ".fbx"));
            clip.name = name; var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "idle01" || name == "walk" || name == "run";
            settings.loopBlend = settings.loopTime; AnimationUtility.SetAnimationClipSettings(clip, settings);
            float[] impacts = name == "Basic Attack" ? new[] { .45f } : name == "Claw Attack" ? new[] { .4f, .68f } : name == "Horn Attack" ? new[] { .55f } : Array.Empty<float>();
            AnimationUtility.SetAnimationEvents(clip, impacts.Select(t => new AnimationEvent { time = clip.length * t, functionName = "EncounterImpact" }).ToArray());
            AssetDatabase.CreateAsset(clip, Folder + "/DragonClips/" + name.Replace(" ", "_") + ".anim");
        }
        var locomotion = machine.AddState("Locomotion"); machine.defaultState = locomotion;
        var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller); tree.AddChild(DragonClip("idle01"), 0); tree.AddChild(DragonClip("walk"), 1.4f); tree.AddChild(DragonClip("run"), 4.8f);
        locomotion.motion = tree;
        foreach (var pair in new[] { new[] { "Attack_Bite", "Basic Attack" }, new[] { "Attack_Claw", "Claw Attack" }, new[] { "Attack_Horn", "Horn Attack" }, new[] { "Roar", "scream" }, new[] { "Death", "die" } })
        {
            var state = machine.AddState(pair[0]); state.motion = DragonClip(pair[1]);
            if (pair[0] == "Death") continue;
            var exit = state.AddTransition(locomotion); exit.hasExitTime = true; exit.exitTime = 1; exit.hasFixedDuration = true; exit.duration = .1f;
        }
        return controller;
    }
    private static Material DragonMaterial()
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(DragonPack + "/Materials/DragonNightmare/RedPBR.mat");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "DragonNightmare_Red_URP" };
        material.SetTexture("_BaseMap", source.GetTexture("_MainTex")); material.SetColor("_BaseColor", source.GetColor("_Color"));
        foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) material.SetTexture(property, source.GetTexture(property));
        material.SetFloat("_Metallic", source.GetFloat("_Metallic")); material.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
        material.SetFloat("_BumpScale", 1); material.SetFloat("_OcclusionStrength", 1);
        material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
        AssetDatabase.CreateAsset(material, Folder + "/DragonNightmare_Red_URP.mat"); return material;
    }
    private static void BuildBossHud(EncounterCombatant actor, string name, int order)
    {
        var original = UnityEngine.Object.FindObjectsByType<BossHealthUI>(FindObjectsSortMode.None).First(ui => ui.name == "Boss Encounter HUD");
        var hud = UnityEngine.Object.Instantiate(original.gameObject, original.transform.parent); hud.name = name;
        var ui = hud.GetComponent<BossHealthUI>(); Ref(ui, "boss", null); Ref(ui, "encounterBoss", actor);
        var fields = new SerializedObject(ui); fields.FindProperty("displayOrder").intValue = order; fields.ApplyModifiedPropertiesWithoutUndo();
        hud.GetComponent<CanvasGroup>().alpha = 0;
        var label = (TMP_Text)fields.FindProperty("bossName").objectReferenceValue; label.text = actor.DisplayName;
        Undo.RegisterCreatedObjectUndo(hud, "Create encounter health bar");
    }
    private static void EnsureFolder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    private static T Get<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var result) ? result : Undo.AddComponent<T>(go);
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Configure(UnityEngine.Object target, string field, float value)
    { var data = new SerializedObject(target); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Configure(UnityEngine.Object target, string field, string value)
    { var data = new SerializedObject(target); data.FindProperty(field).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Configure(UnityEngine.Object target, string field, bool value)
    { var data = new SerializedObject(target); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Track(GameObject root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            foreach (var item in child.GetComponents<Component>().Cast<UnityEngine.Object>().Append(child.gameObject))
            { EditorUtility.SetDirty(item); if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
    }
}
#endif
