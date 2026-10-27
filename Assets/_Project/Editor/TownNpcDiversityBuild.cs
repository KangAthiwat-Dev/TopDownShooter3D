#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class TownNpcDiversitySetup
{
    [Serializable] private sealed class Record
    {
        public string backup;
        public string[] files, hashes;
        public int preservedComponents;
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
    private static GameObject CopyModel(string source, string name, bool human)
    {
        string path = Folder + "/Sources/" + name + ".fbx";
        if (!File.Exists(path) && !AssetDatabase.CopyAsset(source, path)) throw new IOException(source);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true; importer.importAnimation = false;
        if (human) { importer.animationType = ModelImporterAnimationType.Human; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; }
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (human)
        {
            // Female_Peasant's automatic map is invalid despite sharing the kit's 65-bone rig.
            // Map this female skeleton explicitly and calibrate its Humanoid T-pose.
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var humanBones = new List<HumanBone>();
            void Bone(HumanBodyBones slot, string bone)
            {
                if (transforms.Any(t => t.name == bone)) humanBones.Add(new HumanBone { humanName = HumanTrait.BoneName[(int)slot], boneName = bone, limit = new HumanLimit { useDefaultValues = true } });
            }
            Bone(HumanBodyBones.Hips,"pelvis"); Bone(HumanBodyBones.Spine,"spine_01"); Bone(HumanBodyBones.Chest,"spine_02"); Bone(HumanBodyBones.UpperChest,"spine_03");
            Bone(HumanBodyBones.Neck,"neck_01"); Bone(HumanBodyBones.Head,"Head");
            foreach (string side in new[] { "Left", "Right" })
            {
                string suffix = side == "Left" ? "_l" : "_r";
                void Map(string human, string bone) => Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + human), bone + suffix);
                Map("Shoulder","clavicle"); Map("UpperArm","upperarm"); Map("LowerArm","lowerarm"); Map("Hand","hand");
                Map("UpperLeg","thigh"); Map("LowerLeg","calf"); Map("Foot","foot"); Map("Toes","ball");
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    string stem = finger == "Little" ? "pinky" : finger.ToLowerInvariant();
                    Map(finger + "Proximal",stem + "_01"); Map(finger + "Intermediate",stem + "_02"); Map(finger + "Distal",stem + "_03");
                }
            }
            importer.humanDescription = new HumanDescription {
                human = humanBones.ToArray(), skeleton = TownFemaleRigRepair.ReferencePose(model,humanBones.ToArray()),
                armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f
            };
            importer.SaveAndReimport(); model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return model;
    }
    private static Material Material(string name, string texture, Color tint)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat"); if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture)); material.SetColor("_BaseColor", tint);
        material.SetFloat("_Smoothness", .08f); AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat"); return material;
    }
    private static TownNpcBrain Female(string name, GameObject bodySource, GameObject headSource, GameObject hairSource, Material cloth, Material skin, Material eyes, Material hair)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/Citizen_Female_" + name + ".prefab"); if (existing != null) return existing.GetComponent<TownNpcBrain>();
        var root = new GameObject("Citizen_Female_" + name); GameObject head = null, hairstyle = null;
        try
        {
            var facing = new GameObject("Facing").transform; facing.SetParent(root.transform, false);
            var model = UnityEngine.Object.Instantiate(bodySource, facing); model.name = "Female_Peasant_Visual";
            if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMaterial = cloth;
            head = UnityEngine.Object.Instantiate(headSource); hairstyle = UnityEngine.Object.Instantiate(hairSource);
            float cut = head.GetComponentsInChildren<Transform>().Single(t => t.name == "neck_01").position.y - .04f;
            foreach (var renderer in head.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                bool face = renderer.name.Contains("Superhero");
                NpcMerchantSetup.AttachSkin(renderer, model.transform, name + "_" + (face ? "Head_Only" : renderer.name), face ? skin : renderer.name.Contains("Eyes") ? eyes : hair, face, Folder, cut);
            }
            foreach (var renderer in hairstyle.GetComponentsInChildren<SkinnedMeshRenderer>())
                NpcMerchantSetup.AttachSkin(renderer, model.transform, name + "_Hair", hair, false, Folder);
            UnityEngine.Object.DestroyImmediate(head); head = null; UnityEngine.Object.DestroyImmediate(hairstyle); hairstyle = null;
            var animator = model.GetComponent<Animator>();
            if (animator == null || !animator.avatar.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Invalid female Humanoid avatar.");
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TownNpcSetup.Folder + "/Townsfolk.controller");
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            animator.Rebind(); animator.Update(0); animator.Play("Locomotion", 0, .15f); animator.Update(0);
            var geometry = NpcMerchantSetup.GeometryBounds(model); model.transform.localScale *= 1.78f / geometry.size.y;
            geometry = NpcMerchantSetup.GeometryBounds(model); model.transform.localPosition -= Vector3.up * geometry.min.y;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            { renderer.updateWhenOffscreen = false; renderer.localBounds = new Bounds(Vector3.up, new Vector3(2.5f, 3.4f, 2.5f)); }
            var capsule = root.AddComponent<CapsuleCollider>(); capsule.height = 1.78f; capsule.radius = .28f; capsule.center = Vector3.up * .89f;
            var agent = root.AddComponent<NavMeshAgent>(); agent.height = 1.78f; agent.radius = .28f; agent.speed = 1.65f; agent.acceleration = 12f;
            agent.stoppingDistance = .25f; agent.angularSpeed = 360f; agent.autoRepath = true; agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            root.AddComponent<CivilianHealth>(); var brain = root.AddComponent<TownNpcBrain>(); var settings = new SerializedObject(brain);
            settings.FindProperty("animator").objectReferenceValue = animator; settings.FindProperty("gender").enumValueIndex = (int)TownNpcGender.Female;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + root.name + ".prefab").GetComponent<TownNpcBrain>();
        }
        finally { if (head != null) UnityEngine.Object.DestroyImmediate(head); if (hairstyle != null) UnityEngine.Object.DestroyImmediate(hairstyle); UnityEngine.Object.DestroyImmediate(root); }
    }
    private static AnimationClip WorkerClip(string take, string name)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(Ual2).OfType<AnimationClip>().Single(c => c.name == "Armature|" + take);
        if (!source.isHumanMotion) throw new InvalidOperationException(take + " is not Humanoid");
        var clip = UnityEngine.Object.Instantiate(source); clip.name = name; AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.loopBlend = true;
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, Folder + "/Animations/" + name + ".anim"); return clip;
    }
    private static TownNpcBrain Worker()
    {
        string controllerPath = Folder + "/Builder.controller";
        AssetDatabase.CopyAsset(TownNpcSetup.Folder + "/Townsfolk.controller", controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath); var machine = controller.layers[0].stateMachine;
        machine.AddState("TreeChopping").motion = WorkerClip("TreeChopping_Loop", "Builder_TreeChopping_Loop");
        machine.AddState("WalkCarry").motion = WorkerClip("Walk_Carry_Loop", "Builder_Walk_Carry_Loop"); EditorUtility.SetDirty(controller);
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TownNpcSetup.Folder + "/Prefabs/Citizen_Russet.prefab")); root.name = "Citizen_Male_Builder";
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var cargo = new GameObject("Carried_Wood_Planks"); cargo.transform.SetParent(root.transform, false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MedievalModelOnlySetup.Package + "/Generated/Prefabs/VK/Pile/SM_VK_Pile_Planks_1.prefab");
            var wood = UnityEngine.Object.Instantiate(source, cargo.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(wood)) PrefabUtility.UnpackPrefabInstance(wood, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Bounds bounds = wood.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach (var renderer in wood.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            wood.transform.localScale = Vector3.Scale(wood.transform.localScale, new Vector3(.72f / bounds.size.x, .18f / bounds.size.y, .34f / bounds.size.z));
            bounds = wood.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach (var renderer in wood.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            wood.transform.position -= bounds.center;
            foreach (var collider in cargo.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var t in cargo.GetComponentsInChildren<Transform>()) t.gameObject.layer = 0;
            cargo.SetActive(false);
            root.GetComponentInChildren<Animator>().runtimeAnimatorController = controller;
            var job = root.AddComponent<TownConstructionJob>(); var settings = new SerializedObject(job);
            settings.FindProperty("carriedWood").objectReferenceValue = cargo; settings.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + root.name + ".prefab").GetComponent<TownNpcBrain>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/NPC/Women and Builders/Install Mixed Citizens and Construction Crew")]
    public static void Install()
    {
        RequireScene(); if (File.Exists(Folder + "/BuildRecord.json")) throw new InvalidOperationException("Already installed; preserve manual changes.");
        var population = UnityEngine.Object.FindFirstObjectByType<TownNpcPopulation>();
        var components = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null && c != population && c != population.transform).ToArray();
        var before = components.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        var files = new[] { FemaleBody, FemaleBody + ".meta", FemaleHead, FemaleHead + ".meta", HairRoot + "Hair_Long.fbx", HairRoot + "Hair_Long.fbx.meta", HairRoot + "Hair_Buns.fbx", HairRoot + "Hair_Buns.fbx.meta", Ual2, Ual2 + ".meta",
            NpcMerchantSetup.PrefabPath, TownNpcSetup.Folder + "/Townsfolk.controller", TownNpcSetup.Folder + "/Prefabs/Citizen_Russet.prefab",
            "Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab", "Assets/_Project/Animations/Player/PlayerAnimator.controller", "Assets/_Project/World/CityExpansion/CityExpansionNavigation_Final.asset" };
        var record = new Record { files = files, hashes = files.Select(Hash).ToArray() };
        Directory.CreateDirectory("Logs/SceneBackups"); record.backup = "Logs/SceneBackups/WorldPrototype_BeforeWomenBuilders_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), record.backup, true)) throw new IOException("Could not back up live scene.");
        EnsureFolder(Folder + "/Sources"); EnsureFolder(Folder + "/Meshes"); EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Animations"); EnsureFolder(Folder + "/Prefabs");
        var body = CopyModel(FemaleBody, "Female_Peasant_Rig", true); var head = CopyModel(FemaleHead, "Female_Head_Source", false);
        var longHair = CopyModel(HairRoot + "Hair_Long.fbx", "Hair_Long_Source", false); var buns = CopyModel(HairRoot + "Hair_Buns.fbx", "Hair_Buns_Source", false);
        var skin = Material("Female_Skin", NpcMerchantSetup.Base + "/Base Characters/Textures/T_Superhero_Female_Dark_BaseColor.png", Color.white);
        var eyes = AssetDatabase.LoadAssetAtPath<Material>(NpcMerchantSetup.Folder + "/Materials/Merchant_Eyes.mat");
        string clothTexture = NpcMerchantSetup.Outfits + "/Textures/Peasant/T_Peasant_2_BaseColor.png", hairTexture = NpcMerchantSetup.Base + "/Hairstyles/Textures/T_Hair_2_BaseColor.png";
        var females = new[] {
            Female("Teal_Long",body,head,longHair,Material("Female_Cloth_Teal",clothTexture,new Color(.48f,.83f,.76f)),skin,eyes,Material("Female_Hair_Dark",hairTexture,new Color(.16f,.12f,.1f))),
            Female("Amber_Buns",body,head,buns,Material("Female_Cloth_Amber",clothTexture,new Color(.95f,.74f,.43f)),skin,eyes,Material("Female_Hair_Brown",hairTexture,new Color(.4f,.21f,.1f))),
            Female("Plum_Buns",body,head,buns,Material("Female_Cloth_Plum",clothTexture,new Color(.74f,.5f,.76f)),skin,eyes,Material("Female_Hair_Auburn",hairTexture,new Color(.46f,.18f,.1f)))
        };
        var worker = Worker();
        var routeRoot = new GameObject("Construction_Work_Routes").transform; Undo.RegisterCreatedObjectUndo(routeRoot.gameObject, "Add construction work stops"); routeRoot.SetParent(population.transform, false);
        Transform Point(string name, Vector3 requested, Transform parent)
        {
            if (!NavMesh.SamplePosition(requested, out var hit, 1.2f, NavMesh.AllAreas)) throw new InvalidOperationException("No construction navigation at " + requested);
            var point = new GameObject(name).transform; point.SetParent(parent, false); point.position = hit.position; return point;
        }
        var stations = new TownNpcWorkStation[Stations.Length];
        for (int i = 0; i < Stations.Length; i++)
        {
            var station = Point("Builder_Station_" + (i + 1), Stations[i][0], routeRoot).gameObject.AddComponent<TownNpcWorkStation>(); stations[i] = station;
            station.transform.rotation = Quaternion.Euler(0, i == 0 ? 270 : 0, 0);
            var pickup = Point("Pick_Up_Wood", Stations[i][1], station.transform); var drop = Point("Deliver_Wood", Stations[i][2], station.transform);
            var settings = new SerializedObject(station); settings.FindProperty("pickup").objectReferenceValue = pickup; settings.FindProperty("delivery").objectReferenceValue = drop; settings.ApplyModifiedPropertiesWithoutUndo();
        }
        Undo.RecordObject(population, "Reserve female and builder population slots"); var config = new SerializedObject(population);
        var array = config.FindProperty("femalePrefabs"); array.arraySize = females.Length;
        for (int i = 0; i < females.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = females[i];
        config.FindProperty("femaleCount").intValue = 5; config.FindProperty("constructionWorkers").intValue = 3;
        config.FindProperty("constructionWorkerPrefab").objectReferenceValue = worker;
        array = config.FindProperty("constructionStations"); array.arraySize = stations.Length;
        for (int i = 0; i < stations.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = stations[i];
        config.ApplyModifiedProperties(); EditorUtility.SetDirty(population);
        var changed = components.Where(c => c == null || EditorJsonUtility.ToJson(c) != before[c.GetEntityId().ToString()]).ToArray();
        if (changed.Length > 0) throw new InvalidOperationException("Unexpected changes outside population scope: " + string.Join(",", changed.Select(c => c != null ? c.name : "destroyed")));
        if (!files.Select(Hash).SequenceEqual(record.hashes)) throw new InvalidOperationException("Original sources/merchant/player/navigation changed.");
        record.preservedComponents = components.Length; File.WriteAllText(Folder + "/BuildRecord.json", JsonUtility.ToJson(record, true)); AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); Validate(); Selection.activeGameObject = population.gameObject;
    }
    [MenuItem("Tools/NPC/Women and Builders/Validate Mixed Population")]
    public static void Validate()
    {
        RequireScene(); var log = new StringBuilder("Women / builders validation " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool value, string label) { passed &= value; log.AppendLine((value ? "PASS " : "FAIL ") + label); }
        var population = UnityEngine.Object.FindFirstObjectByType<TownNpcPopulation>();
        Check(population.MaxPopulation == 12 && population.ConfiguredFemales == 5 && population.ConfiguredWorkers == 3, "12 total slots: 5 women, 4 male townsfolk, 3 male builders");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder + "/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)); var brain = prefab.GetComponent<TownNpcBrain>(); var animator = prefab.GetComponentInChildren<Animator>();
            Check(animator != null && animator.avatar.isHuman && animator.avatar.isValid && !animator.applyRootMotion && prefab.GetComponentsInChildren<Animator>().Length == 1, prefab.name + ": one valid Humanoid Animator, root motion off");
            Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => s.bones.All(b => b != null && b.IsChildOf(prefab.transform)) && s.sharedMaterials.All(m => m != null && m.shader != null && m.mainTexture != null)), prefab.name + ": all skin parts bound to one rig with textured materials");
            Check(brain.IsConstructionWorker ? brain.Gender == TownNpcGender.Male : brain.Gender == TownNpcGender.Female, prefab.name + ": correct gender / worker role");
        }
        Check(population.ConstructionStations.Count == 3 && population.ConstructionStations.All(s => s.Contains(s.transform.position) && s.Contains(s.Pickup.position) && s.Contains(s.Delivery.position)), "Three local construction work/pickup/delivery circuits");
        Check(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Builder_TreeChopping_Loop.anim").isLooping && AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Builder_Walk_Carry_Loop.anim").isLooping, "Private looping TreeChopping and Walk_Carry animations");
        var worker = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/Citizen_Male_Builder.prefab");
        Check(worker.GetComponent<TownConstructionJob>().CarriedWood.GetComponentsInChildren<Collider>(true).Length == 0 && worker.GetComponentInChildren<AudioSource>(true) == null, "Carry visual has no physics; no hammer or work audio added");
        var record = JsonUtility.FromJson<Record>(File.ReadAllText(Folder + "/BuildRecord.json"));
        Check(record.files.Select(Hash).SequenceEqual(record.hashes), "Source packs, merchant, old citizens/controller, player and navigation assets preserved");
        Check(File.Exists(record.backup) && record.preservedComponents > 0, "Fresh backup; " + record.preservedComponents + " other scene components preserved");
        log.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TownDiversityValidation.txt", log.ToString());
        if (passed) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }
}
#endif
