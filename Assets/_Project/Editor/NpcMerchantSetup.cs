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

public static class NpcMerchantSetup
{
    public const string Base = "Assets/_Project/Art/ThirdParty/Quaternius/Universal Base Characters";
    public const string Outfits = "Assets/_Project/Art/ThirdParty/Quaternius/Modular Character Outfits";
    public const string Folder = "Assets/_Project/NPCs/WeaponMerchant";
    public const string BodySource = Outfits + "/Exports/FBX (Unity)/Outfits/Male_Peasant.fbx";
    public const string HeadSource = Base + "/Base Characters/Unity/Superhero_Male_FullBody.fbx";
    public const string HairSource = Base + "/Hairstyles/Rigged to Head Bone/FBX (Unity)/Hair_SimpleParted.fbx";
    public const string RootName = "NPC_WeaponMerchant";
    public const string PrefabPath = Folder + "/NPC_WeaponMerchant.prefab";
    private const string CityAnchor = "Future_Weapon_Shop_NPC_Anchor";

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static string[] OriginalFiles => new[] { BodySource, BodySource + ".meta", HeadSource, HeadSource + ".meta", HairSource, HairSource + ".meta", "Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab", "Assets/_Project/Animations/Player/PlayerAnimator.controller" };

    private static GameObject ModelCopy(string source, string stem, bool humanoid)
    {
        string path = Folder + "/Sources/" + stem + ".fbx";
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        if (!AssetDatabase.CopyAsset(source, path)) throw new IOException(source);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        if (humanoid)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Material Material(string name, string texture, Color tint)
    {
        var map = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
        if (map == null) throw new IOException("Missing texture " + texture);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        material.SetTexture("_BaseMap", map);
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat");
        return material;
    }

    private static float HeadWeight(BoneWeight weight, Transform[] bones)
    {
        bool Head(int i) => bones[i].name == "Head" || bones[i].name == "neck_01";
        return (Head(weight.boneIndex0) ? weight.weight0 : 0) + (Head(weight.boneIndex1) ? weight.weight1 : 0) +
               (Head(weight.boneIndex2) ? weight.weight2 : 0) + (Head(weight.boneIndex3) ? weight.weight3 : 0);
    }

    // Bake only visible head geometry to the outfit's bind pose, then skin it to that ONE rig.
    // No second Animator, body-under-clothing overlap, runtime mesh generation or per-frame rebinding.
    public static SkinnedMeshRenderer AttachSkin(SkinnedMeshRenderer source, Transform targetModel, string name, Material material, bool headOnly, string outputFolder = null, float headCutHeight = 1.49f)
    {
        var boneMap = targetModel.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        Transform[] targetBones = source.bones.Select(b => boneMap.TryGetValue(b.name, out var target) ? target : throw new InvalidOperationException("Missing bone " + b.name)).ToArray();
        Mesh input = source.sharedMesh;
        Vector3[] vertices = input.vertices, normals = input.normals;
        Vector2[] uv = input.uv;
        BoneWeight[] weights = input.boneWeights;
        var keep = vertices.Select((v, i) => !headOnly || (source.transform.TransformPoint(v).y >= headCutHeight && HeadWeight(weights[i], source.bones) >= .5f)).ToArray();
        var oldToNew = new System.Collections.Generic.Dictionary<int, int>();
        var newVertices = new System.Collections.Generic.List<Vector3>();
        var newNormals = new System.Collections.Generic.List<Vector3>();
        var newUv = new System.Collections.Generic.List<Vector2>();
        var newWeights = new System.Collections.Generic.List<BoneWeight>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int sub = 0; sub < input.subMeshCount; sub++)
        {
            int[] indices = input.GetTriangles(sub);
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (!keep[indices[i]] || !keep[indices[i + 1]] || !keep[indices[i + 2]]) continue;
                for (int corner = 0; corner < 3; corner++)
                {
                    int old = indices[i + corner];
                    if (!oldToNew.TryGetValue(old, out int index))
                    {
                        index = oldToNew.Count; oldToNew.Add(old, index);
                        Vector3 worldPoint = source.transform.TransformPoint(vertices[old]);
                        Vector3 worldNormal = source.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[old]).normalized;
                        BoneWeight w = weights[old];
                        Vector3 point = Vector3.zero, normal = Vector3.zero;
                        void Influence(int b, float weight)
                        {
                            if (weight <= 0) return;
                            Matrix4x4 transfer = targetBones[b].localToWorldMatrix * source.bones[b].worldToLocalMatrix;
                            point += transfer.MultiplyPoint3x4(worldPoint) * weight;
                            normal += transfer.inverse.transpose.MultiplyVector(worldNormal) * weight;
                        }
                        Influence(w.boneIndex0, w.weight0); Influence(w.boneIndex1, w.weight1); Influence(w.boneIndex2, w.weight2); Influence(w.boneIndex3, w.weight3);
                        newVertices.Add(targetModel.InverseTransformPoint(point));
                        newNormals.Add(targetModel.localToWorldMatrix.transpose.MultiplyVector(normal).normalized);
                        newUv.Add(uv[old]); newWeights.Add(w);
                    }
                    triangles.Add(index);
                }
            }
        }
        if (triangles.Count < 30) throw new InvalidOperationException("Head extraction produced no usable geometry.");
        var mesh = new Mesh { name = name };
        mesh.SetVertices(newVertices); mesh.SetNormals(newNormals); mesh.SetUVs(0, newUv);
        mesh.boneWeights = newWeights.ToArray();
        mesh.bindposes = targetBones.Select(b => b.worldToLocalMatrix * targetModel.localToWorldMatrix).ToArray();
        mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        string meshPath = (outputFolder ?? Folder) + "/Meshes/" + name + ".asset";
        var existingMesh = outputFolder != null ? AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) : null;
        if (existingMesh != null) { UnityEngine.Object.DestroyImmediate(mesh); mesh = existingMesh; }
        else AssetDatabase.CreateAsset(mesh, meshPath);
        var go = new GameObject(name); go.transform.SetParent(targetModel, false);
        var skin = go.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = mesh; skin.bones = targetBones;
        skin.rootBone = source.rootBone != null && boneMap.TryGetValue(source.rootBone.name, out var root) ? root : targetBones[0];
        skin.sharedMaterial = material; skin.updateWhenOffscreen = true;
        // Conservative local bounds cover idle sway without making mesh visibility depend on a source rig.
        skin.localBounds = new Bounds(new Vector3(0, 1.55f, 0), new Vector3(1.2f, 1f, 1.2f));
        return skin;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    [Serializable] private sealed class BuildRecord
    {
        public string backup;
        public string[] originalFiles, hashes;
        public int preexistingComponents, changedComponents;
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Build and Install Prototype")]
    public static void Build()
    {
        RequireScene();
        if (GameObject.Find(RootName) != null || AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("NPC prototype already exists. Do not overwrite manual edits.");
        var anchor = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == CityAnchor);
        var originals = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null && c != anchor).ToArray();
        var snapshot = originals.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        var record = new BuildRecord { originalFiles = OriginalFiles, hashes = OriginalFiles.Select(Hash).ToArray(), preexistingComponents = originals.Length };
        Directory.CreateDirectory("Logs/SceneBackups");
        record.backup = "Logs/SceneBackups/WorldPrototype_BeforeNPC_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), record.backup, true)) throw new IOException("Cannot back up the live scene.");
        EnsureFolder(Folder + "/Sources"); EnsureFolder(Folder + "/Meshes"); EnsureFolder(Folder + "/Materials");
        var bodySource = ModelCopy(BodySource, "Merchant_Peasant_Rig", true);
        var headSource = ModelCopy(HeadSource, "Merchant_Head_Source", false);
        var hairSource = ModelCopy(HairSource, "Merchant_Hair_Source", false);
        var cloth = Material("Merchant_Cloth", Outfits + "/Textures/Peasant/T_Peasant_BaseColor.png", Color.white);
        var hands = Material("Merchant_Hands", Outfits + "/Textures/Base/T_Regular_Male_Dark_BaseColor.png", Color.white);
        var skin = Material("Merchant_Skin", Base + "/Base Characters/Textures/T_Superhero_Male_Dark.png", Color.white);
        var eyes = Material("Merchant_Eyes", Base + "/Base Characters/Textures/T_Eye_Brown.png", Color.white);
        var hair = Material("Merchant_Hair", Base + "/Hairstyles/Textures/T_Hair_1_BaseColor.png", new Color(.3f, .16f, .08f));
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Merchant_Idle.controller");
        var idle = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Player/Locomotion/Idle_Kevin.anim"));
        idle.name = "Merchant_Idle"; AnimationUtility.SetAnimationEvents(idle, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(idle); settings.loopTime = true; settings.loopBlend = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings); AssetDatabase.CreateAsset(idle, Folder + "/Merchant_Idle.anim");
        var state = controller.layers[0].stateMachine.AddState("Idle"); state.motion = idle;
        controller.layers[0].stateMachine.defaultState = state;
        var root = new GameObject(RootName);
        GameObject baseModel = null, hairModel = null;
        try
        {
            var facing = new GameObject("Facing").transform; facing.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(bodySource, facing); model.name = "Peasant_Visual";
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            baseModel = UnityEngine.Object.Instantiate(headSource); hairModel = UnityEngine.Object.Instantiate(hairSource);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name.Contains("Peasant") ? cloth : hands).ToArray();
                renderer.updateWhenOffscreen = true;
            }
            foreach (var renderer in baseModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AttachSkin(renderer, model.transform, renderer.name.Contains("Super") ? "Head_Only" : renderer.name, renderer.name.Contains("Super") ? skin : renderer.name.Contains("Eyes") ? eyes : hair, renderer.name.Contains("Super"));
            foreach (var renderer in hairModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)) AttachSkin(renderer, model.transform, "Hair_SimpleParted", hair, false);
            UnityEngine.Object.DestroyImmediate(baseModel); baseModel = null;
            UnityEngine.Object.DestroyImmediate(hairModel); hairModel = null;
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Peasant Humanoid Avatar is invalid.");
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // The source faces -Z. Keep the controller/AI pivot facing +Z.
            model.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            animator.Rebind(); animator.Update(0); animator.Play("Idle", 0, .15f); animator.Update(0);
            // Use baked geometry, not conservative culling bounds, to measure true height and floor contact.
            Bounds geometry = GeometryBounds(model);
            float scale = 1.88f / geometry.size.y; model.transform.localScale *= scale;
            geometry = GeometryBounds(model); model.transform.localPosition -= Vector3.up * geometry.min.y;
            var capsule = root.AddComponent<CapsuleCollider>(); capsule.height = 1.88f; capsule.radius = .31f; capsule.center = Vector3.up * .94f;
            var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.height = 1.88f; obstacle.radius = .36f; obstacle.center = Vector3.up * .94f; obstacle.carving = true;
            var behaviour = root.AddComponent<NpcIdleLook>(); behaviour.Configure(facing, null);
            var point = new GameObject("Future_Dialogue_Anchor").transform; point.SetParent(root.transform, false); point.localPosition = new Vector3(0, 1.4f, .65f);
            var aim = new GameObject("Nameplate_Anchor").transform; aim.SetParent(root.transform, false); aim.localPosition = Vector3.up * 2.05f;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root); root = null;
            var installed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), anchor);
            installed.transform.localPosition = Vector3.zero; installed.transform.localRotation = Quaternion.identity;
            installed.GetComponent<NpcIdleLook>().SetLookTarget(UnityEngine.Object.FindFirstObjectByType<PlayerMovement>().transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(installed.GetComponent<NpcIdleLook>());
            var differences = originals.Where(c => c == null || EditorJsonUtility.ToJson(c) != snapshot[c.GetEntityId().ToString()]).ToArray();
            record.changedComponents = differences.Length;
            File.WriteAllText("Logs/NpcMerchantScope.txt", "Compared " + originals.Length + " pre-existing components; differences=" + differences.Length + "\n" + string.Join("\n", differences.Select(c => c != null ? c.name + ":" + c.GetType().Name : "deleted")));
            if (differences.Length != 0) throw new InvalidOperationException("Unexpected edits outside NPC scope; see Logs/NpcMerchantScope.txt");
            File.WriteAllText(Folder + "/BuildRecord.json", JsonUtility.ToJson(record, true));
            AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Validate(); Capture(); Focus();
        }
        finally
        {
            if (baseModel != null) UnityEngine.Object.DestroyImmediate(baseModel);
            if (hairModel != null) UnityEngine.Object.DestroyImmediate(hairModel);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static Bounds GeometryBounds(GameObject go)
    {
        bool first = true; Bounds bounds = default;
        foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var baked = new Mesh(); skin.BakeMesh(baked);
            foreach (Vector3 vertex in baked.vertices)
            {
                Vector3 p = skin.transform.TransformPoint(vertex);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
            UnityEngine.Object.DestroyImmediate(baked);
        }
        return bounds;
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Validate Prototype")]
    public static void Validate()
    {
        RequireScene(); var npc = GameObject.Find(RootName); var log = new StringBuilder("Merchant validation " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool value, string label) { passed &= value; log.AppendLine((value ? "PASS " : "FAIL ") + label); }
        Check(npc != null, "One merchant installed");
        if (npc != null)
        {
            Check(UnityEngine.Object.FindObjectsByType<NpcIdleLook>(FindObjectsSortMode.None).Count(n => n.NpcId == "armory_merchant_01") == 1, "Only one weapon merchant identity");
            var animators = npc.GetComponentsInChildren<Animator>(true);
            Check(animators.Length == 1 && animators[0].avatar.isHuman && animators[0].avatar.isValid, "One valid Humanoid Animator for body, head, eyes and hair");
            Check(animators[0].runtimeAnimatorController != null && !animators[0].applyRootMotion, "Idle controller assigned; root motion disabled");
            var skins = npc.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(skins.Length == 8 && skins.All(s => s.sharedMesh != null && s.bones.All(b => b != null && b.IsChildOf(npc.transform))), "All eight skinned parts use the merchant rig");
            Check(skins.All(s => s.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name.Contains("Universal Render Pipeline") && m.mainTexture != null)), "All materials use textured URP shaders");
            Check(skins.Single(s => s.name == "Head_Only").sharedMesh.vertexCount < 4000, "Only head/neck retained from the Superhero full body");
            Check(npc.GetComponent<CapsuleCollider>() != null && npc.GetComponent<NavMeshObstacle>().carving, "Simple collision and stationary navigation carving enabled");
            Check(npc.GetComponentInChildren<Targetable>() == null && npc.GetComponentInChildren<EnemyAI>() == null && npc.GetComponentInChildren<EnemyHealth>() == null, "Merchant is not an enemy or combat target");
            Check(npc.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
            Check(npc.transform.parent.name == CityAnchor && Vector3.Distance(npc.transform.position, npc.transform.parent.position) < .001f, "Placed at the user's armory NPC anchor");
            Check(Physics.Raycast(npc.transform.position + Vector3.up * .15f, Vector3.down, out var hit, .3f, 1 << 7) && Mathf.Abs(hit.point.y - npc.transform.position.y) < .06f, "Feet supported by the level city floor");
            var record = JsonUtility.FromJson<BuildRecord>(File.ReadAllText(Folder + "/BuildRecord.json"));
            Check(record.originalFiles.Select(Hash).SequenceEqual(record.hashes), "Imported sources, Player visual and Player animator unchanged");
            Check(record.changedComponents == 0, "All " + record.preexistingComponents + " pre-existing scene components unchanged");
        }
        log.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/NpcMerchantValidation.txt", log.ToString());
        if (passed) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Focus Prototype")]
    public static void Focus()
    {
        var npc = GameObject.Find(RootName); if (npc == null) return;
        Selection.activeGameObject = npc;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(npc.transform.position + Vector3.up, Quaternion.Euler(12, 160, 0), 3.3f, false, true);
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Capture Prototype")]
    public static void Capture()
    {
        var npc = GameObject.Find(RootName); if (npc == null) return;
        var go = new GameObject("Temporary NPC capture"); var camera = go.AddComponent<Camera>();
        try
        {
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .16f, .2f); camera.nearClipPlane = .05f; camera.farClipPlane = 200f;
            Vector3 p = npc.transform.position;
            camera.transform.position = p + new Vector3(-2.1f, 2.1f, 4.3f); camera.transform.LookAt(p + Vector3.up * .95f); camera.fieldOfView = 35;
            SaveImage(camera, "Logs/NpcMerchant_Closeup.png", 1100, 1300);
            camera.transform.position = p + new Vector3(-8, 10, 12); camera.transform.LookAt(p + new Vector3(3, .8f, -1.8f)); camera.fieldOfView = 42;
            SaveImage(camera, "Logs/NpcMerchant_Armory.png", 1500, 1100);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void SaveImage(Camera camera, string path, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24); var previous = RenderTexture.active; var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }

    [MenuItem("Tools/NPC/Weapon Merchant/Inspect Sources")]
    public static void Inspect()
    {
        var log = new StringBuilder("NPC sources " + DateTime.Now.ToString("s") + "\n");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string path in new[] { BodySource, HeadSource, HairSource })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new IOException(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                log.AppendLine("\nSOURCE " + path + " rig=" + importer.animationType);
                foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    log.AppendLine("MESH " + renderer.name + " vertices=" + renderer.sharedMesh.vertexCount + " bounds=" + renderer.bounds + " bones=" + renderer.bones.Length);
                    foreach (Material material in renderer.sharedMaterials)
                        log.AppendLine("MATERIAL " + material.name + " texture=" + AssetDatabase.GetAssetPath(material.mainTexture));
                    foreach (Transform bone in renderer.bones.Where(b => b != null && new[] { "Head", "neck_01", "spine_03", "pelvis", "foot_l", "foot_r" }.Contains(b.name)))
                        log.AppendLine("BONE " + bone.name + " p=" + bone.position.ToString("F4") + " rotation=" + bone.eulerAngles.ToString("F2"));
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                log.AppendLine("PLAYER cc height=" + player.GetComponent<CharacterController>().height);
                var animators = player.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators) log.AppendLine("PLAYER animator=" + animator.name + " avatar=" + AssetDatabase.GetAssetPath(animator.avatar));
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/NpcMerchantSources.txt", log.ToString());
            Debug.Log("NPC source inspection saved: Logs/NpcMerchantSources.txt");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
#endif
