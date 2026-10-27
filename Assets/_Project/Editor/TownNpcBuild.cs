#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class TownNpcSetup
{
    [Serializable] private sealed class BuildRecord
    {
        public string backup;
        public string[] files, hashes;
        public int preservedComponents, cityPoints, outsidePoints;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    private static AnimationClip Clip(string sourceName, string name, bool loop)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(Ual).OfType<AnimationClip>().Single(c => c.name == "Armature|" + sourceName);
        if (!source.isHumanMotion) throw new InvalidOperationException("Clip is not Humanoid: " + sourceName);
        var clip = UnityEngine.Object.Instantiate(source); clip.name = name;
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.loopBlend = loop;
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, Folder + "/Animations/" + name + ".anim");
        return clip;
    }
    private static AnimatorController Controller()
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Townsfolk.controller");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = Clip("Idle_Loop", "Citizen_Idle", true); var walk = Clip("Walk_Loop", "Citizen_Walk", true); var run = Clip("Sprint_Loop", "Citizen_Run", true);
        var blend = new BlendTree { name = "Citizen_Locomotion", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(blend, controller);
        blend.children = new[] {
            new ChildMotion { motion=idle, threshold=0f, timeScale=1f },
            new ChildMotion { motion=walk, threshold=1.65f, timeScale=1f },
            new ChildMotion { motion=run, threshold=4.5f, timeScale=1f }
        };
        var locomotion = machine.AddState("Locomotion"); locomotion.motion = blend; machine.defaultState = locomotion;
        machine.AddState("Talking").motion = Clip("Idle_Talking_Loop", "Citizen_Idle_Talking_Loop", true);
        machine.AddState("Interact").motion = Clip("Interact", "Citizen_Interact", false);
        machine.AddState("Death").motion = Clip("Death01", "Citizen_Death", false);
        EditorUtility.SetDirty(controller); return controller;
    }
    private static TownNpcBrain BuildCitizen(string palette, Color clothTint, Color hairTint, AnimatorController controller)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(NpcMerchantSetup.PrefabPath);
        var root = UnityEngine.Object.Instantiate(source); root.name = "Citizen_" + palette;
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<NpcIdleLook>());
            UnityEngine.Object.DestroyImmediate(root.GetComponent<NavMeshObstacle>());
            var cloth = new Material(AssetDatabase.LoadAssetAtPath<Material>(NpcMerchantSetup.Folder + "/Materials/Merchant_Cloth.mat")) { name = "Citizen_Cloth_" + palette };
            cloth.SetColor("_BaseColor", clothTint); AssetDatabase.CreateAsset(cloth, Folder + "/Materials/" + cloth.name + ".mat");
            var hair = new Material(AssetDatabase.LoadAssetAtPath<Material>(NpcMerchantSetup.Folder + "/Materials/Merchant_Hair.mat")) { name = "Citizen_Hair_" + palette };
            hair.SetColor("_BaseColor", hairTint); AssetDatabase.CreateAsset(hair, Folder + "/Materials/" + hair.name + ".mat");
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.sharedMaterials = skin.sharedMaterials.Select(m => m.name == "Merchant_Cloth" ? cloth : m.name == "Merchant_Hair" ? hair : m).ToArray();
                skin.updateWhenOffscreen = false; skin.localBounds = new Bounds(new Vector3(0, 1f, 0), new Vector3(2.5f, 3.4f, 2.5f));
            }
            var animator = root.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var agent = root.AddComponent<NavMeshAgent>(); agent.radius = .3f; agent.height = 1.88f;
            agent.speed = 1.65f; agent.acceleration = 12f; agent.angularSpeed = 360f; agent.stoppingDistance = .25f;
            agent.autoBraking = true; agent.autoRepath = true; agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            root.AddComponent<CivilianHealth>(); var brain = root.AddComponent<TownNpcBrain>();
            var so = new SerializedObject(brain); so.FindProperty("animator").objectReferenceValue = animator; so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + root.name + ".prefab");
            return prefab.GetComponent<TownNpcBrain>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/NPC/Townsfolk/Build and Install Population")]
    public static void Build()
    {
        RequireScene();
        if (GameObject.Find(RootName) != null || AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Already installed; do not overwrite manual NPC/route edits.");
        var scene = SceneManager.GetActiveScene();
        var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
        var snapshot = components.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        var files = new[] { Ual, Ual + ".meta", NpcMerchantSetup.PrefabPath, NpcMerchantSetup.BodySource, NpcMerchantSetup.HeadSource, NpcMerchantSetup.HairSource,
            "Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab", "Assets/_Project/Animations/Player/PlayerAnimator.controller" };
        var record = new BuildRecord { files = files, hashes = files.Select(Hash).ToArray() };
        Directory.CreateDirectory("Logs/SceneBackups"); record.backup = "Logs/SceneBackups/WorldPrototype_BeforeTownsfolk_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, record.backup, true)) throw new IOException("Cannot back up the current live scene.");
        EnsureFolder(Folder + "/Animations"); EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Prefabs");
        var controller = Controller();
        var prefabs = new[] {
            BuildCitizen("Russet",new Color(.9f,.62f,.46f),new Color(.3f,.16f,.08f),controller),
            BuildCitizen("Sage",new Color(.5f,.78f,.6f),new Color(.12f,.09f,.07f),controller),
            BuildCitizen("Blue",new Color(.48f,.66f,.95f),new Color(.6f,.42f,.18f),controller),
            BuildCitizen("Plum",new Color(.7f,.48f,.75f),new Color(.42f,.38f,.34f),controller)
        };
        var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Install town population");
        var routes = new GameObject("Editable_Routes_and_Activities").transform; routes.SetParent(root.transform, false);
        TownNpcActivityPoint Point(Vector3 requested, TownNpcActivityKind kind, int index)
        {
            if (!NavMesh.SamplePosition(requested, out var hit, 1.5f, NavMesh.AllAreas)) throw new InvalidOperationException("No navigation at " + requested);
            var p = new GameObject(kind + "_" + index.ToString("D2")).AddComponent<TownNpcActivityPoint>(); p.transform.SetParent(routes, false);
            p.transform.position = hit.position; p.transform.rotation = Quaternion.Euler(0, kind == TownNpcActivityKind.Interact ? 90 : 0, 0);
            var settings = new SerializedObject(p); settings.FindProperty("activity").enumValueIndex = (int)kind; settings.ApplyModifiedPropertiesWithoutUndo(); return p;
        }
        var city = CitySites.Select((p, i) => Point(p, i < 3 ? TownNpcActivityKind.Social : i == 14 || i == 15 ? TownNpcActivityKind.Interact : TownNpcActivityKind.Wander, i)).ToArray();
        var outside = OutsideSites.Select((p, i) => Point(p, TownNpcActivityKind.Outside, i)).ToArray();
        var population = root.AddComponent<TownNpcPopulation>(); var settings = new SerializedObject(population);
        void Array(string name, UnityEngine.Object[] values)
        {
            var property = settings.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        Array("citizenPrefabs", prefabs); Array("cityPoints", city); Array("outsidePoints", outside); settings.ApplyModifiedPropertiesWithoutUndo();
        var changes = components.Where(c => c == null || EditorJsonUtility.ToJson(c) != snapshot[c.GetEntityId().ToString()]).ToArray();
        if (changes.Length > 0) throw new InvalidOperationException("Unexpected old-scene changes: " + string.Join(",", changes.Select(c => c != null ? c.name : "destroyed")));
        record.preservedComponents = components.Length; record.cityPoints = city.Length; record.outsidePoints = outside.Length;
        if (!files.Select(Hash).SequenceEqual(record.hashes)) throw new InvalidOperationException("Original source assets changed.");
        File.WriteAllText(Folder + "/BuildRecord.json", JsonUtility.ToJson(record, true)); AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Validate(); Selection.activeGameObject = root;
    }
    [MenuItem("Tools/NPC/Townsfolk/Validate Population")]
    public static void Validate()
    {
        RequireScene(); var log = new StringBuilder("Town population validation " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool value, string label) { passed &= value; log.AppendLine((value ? "PASS " : "FAIL ") + label); }
        var manager = UnityEngine.Object.FindFirstObjectByType<TownNpcPopulation>();
        Check(manager != null && manager.MaxPopulation == 12 && manager.MaxOutside == 2, "One population controller: 12 ordinary citizens, at most 2 outing permits");
        Check(manager.CityPoints.Count == CitySites.Length && manager.OutsidePoints.Count == OutsideSites.Length, "Editable city activities and outside destinations installed");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder + "/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)); var animator = prefab.GetComponentInChildren<Animator>();
            Check(animator != null && animator.avatar.isHuman && animator.avatar.isValid && !animator.applyRootMotion, prefab.name + ": one valid Humanoid, no root motion");
            Check(prefab.GetComponent<TownNpcBrain>() != null && prefab.GetComponent<CivilianHealth>() != null && prefab.GetComponent<NavMeshAgent>() != null && prefab.GetComponent<NavMeshObstacle>() == null && prefab.GetComponent<NpcIdleLook>() == null, prefab.name + ": mobile civilian components, no stationary carving/look behaviour");
            Check(prefab.GetComponent<Targetable>() == null && prefab.GetComponent<EnemyHealth>() == null && prefab.layer == 0, prefab.name + ": neutral, not a player auto-target");
        }
        var talking = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Citizen_Idle_Talking_Loop.anim");
        Check(talking.isLooping && talking.isHumanMotion && AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Citizen_Interact.anim").isHumanMotion, "Independent talking loop and Interact clips installed");
        var record = JsonUtility.FromJson<BuildRecord>(File.ReadAllText(Folder + "/BuildRecord.json"));
        Check(record.files.Select(Hash).SequenceEqual(record.hashes), "Original packs, merchant prefab and player visual/animator untouched");
        Check(File.Exists(record.backup) && record.preservedComponents > 0, "Fresh backup exists; " + record.preservedComponents + " existing scene components preserved");
        Check(UnityEngine.Object.FindObjectsByType<NpcIdleLook>(FindObjectsSortMode.None).Count(n => n.NpcId == "armory_merchant_01") == 1, "Named weapon merchant retained separately");
        log.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TownNpcValidation.txt", log.ToString());
        if (passed) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }
}
#endif
