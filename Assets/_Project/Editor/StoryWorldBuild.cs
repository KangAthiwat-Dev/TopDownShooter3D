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

public static partial class StoryWorldSetup
{
    private static void Ref(UnityEngine.Object owner, string field, UnityEngine.Object value)
    { var s = new SerializedObject(owner); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void String(UnityEngine.Object owner, string field, string value)
    { var s = new SerializedObject(owner); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Identity(GameObject go, string id, string title, string role)
    {
        var actor = go.GetComponent<StoryActor>() ?? Undo.AddComponent<StoryActor>(go);
        String(actor, "actorId", id); String(actor, "displayName", title); String(actor, "storyRole", role);
        EditorUtility.SetDirty(actor); if (PrefabUtility.IsPartOfPrefabInstance(actor)) PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
    }
    [MenuItem("Tools/Story/Prepare Story NPCs and Kingdom Minimap")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
        if (UnityEngine.Object.FindAnyObjectByType<KingdomMinimapUI>() != null) throw new InvalidOperationException("Already installed; preserve custom NPC and UI edits.");
        Directory.CreateDirectory("Logs/SceneBackups"); EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforeStoryPreparation_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        foreach (string sub in new[] { "/NPCs", "/Materials", "/Map" }) Directory.CreateDirectory(Folder + sub); AssetDatabase.Refresh();
        foreach (var contact in UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None))
        {
            if (contact.Role == CityServiceRole.PotionStore) Identity(contact.gameObject, "silverwell_alchemist", "Alchemist", "Potion shop / Story Scene 04");
            else if (contact.Role == CityServiceRole.GuildBuyer) Identity(contact.gameObject, "silverwell_guild_receptionist", "Guild Receptionist", "Material buyer / Story Scene 06");
        }
        var blacksmith = UnityEngine.Object.FindObjectsByType<NpcIdleLook>(FindObjectsSortMode.None).Single(n => n.NpcId == "armory_merchant_01");
        Identity(blacksmith.gameObject, "silverwell_blacksmith", "Blacksmith", "Sword shop / Story Scene 05");
        var boss = UnityEngine.Object.FindAnyObjectByType<KingOfDemonAI>(); Identity(boss.gameObject, "malachar", "Malachar", "Final boss / Story Scenes 09-12");
        String(boss, "displayName", "MALACHAR"); EditorUtility.SetDirty(boss); if (PrefabUtility.IsPartOfPrefabInstance(boss)) PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
        var layout = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        BuildGuard(layout);
        var music = UnityEngine.Object.FindAnyObjectByType<TownMusicZone>(); String(music, "areaName", "SILVERWELL TOWN");
        var cityFields = new SerializedObject(music); var cityFrame = cityFields.FindProperty("boundsFrame").objectReferenceValue as Transform;
        Vector3 cityCenter = cityFields.FindProperty("center").vector3Value;
        if (cityFrame != null) cityCenter = cityFrame.TransformPoint(cityCenter);
        var markerRoot = (GameObject.Find("Everwood Story Landmarks") ?? new GameObject("Everwood Story Landmarks")).transform;
        WorldMapLandmark Mark(string id, Transform parent, Vector3 world, string title, string label, Color color)
        {
            var existing=UnityEngine.Object.FindObjectsByType<WorldMapLandmark>(FindObjectsSortMode.None).FirstOrDefault(m=>m.displayName==title);if(existing!=null)return existing;
            var go = new GameObject(id); go.transform.SetParent(parent, true); go.transform.position = world;
            var mark = go.AddComponent<WorldMapLandmark>(); mark.displayName = title; mark.mapLabel = label; mark.color = color; return mark;
        }
        var cliffs = UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();
        // The edited map may no longer contain the original cliff-layout helper.
        // Use the stationary boss arena so the castle label never follows the boss during combat.
        var arena = new SerializedObject(boss).FindProperty("arena").objectReferenceValue as BossArenaZone;
        var castleAnchor = cliffs != null && cliffs.castleFootprint != null ? cliffs.castleFootprint : arena != null ? arena.transform : null;
        if (castleAnchor == null) throw new InvalidOperationException("A stationary castle or boss-arena anchor is required.");
        layout.castle = castleAnchor;
        EditorUtility.SetDirty(layout);
        var marks = new[] {
            Mark("Silverwell",cityFrame != null ? cityFrame : markerRoot,cityCenter,"Silverwell Town","Silverwell\nTown",new Color(1,.78f,.3f)),
            Mark("Oakshade",layout.village,layout.village.position,"Oakshade Village","Oakshade\nVillage",new Color(.5f,.95f,.55f)),
            Mark("Summoning",layout.sanctuary,layout.sanctuary.position,"Summoning Ruins","Summoning\nRuins",new Color(.45f,.8f,1)),
            Mark("Malachar Castle",castleAnchor,castleAnchor.position,"Malachar's Castle","Malachar's\nCastle",new Color(1,.38f,.32f))
        };
        CreateMinimap(layout, marks);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/StoryPreparation.txt", "Prepared armored village guard; reused alchemist, blacksmith, guild receptionist and Malachar.\nInstalled north-up kingdom minimap with live player and four moving landmark anchors.\nQuest progression, dialogue gates, fountain checkpoint, Alpha Wolf, Wood Golem, opening cinematic and Victory remain separate work.\n");
        Debug.Log("Story NPC preparation and kingdom minimap installed.");
    }
    private static void BuildGuard(ForestMapLayout layout)
    {
        if(UnityEngine.Object.FindObjectsByType<StoryActor>(FindObjectsSortMode.None).Any(a=>a.ActorId=="oakshade_guard"))return;
        const string prefabPath = Folder + "/NPCs/NPC_Oakshade_Guard.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var root = new GameObject("NPC_Oakshade_Guard");
            try
            {
                var facing = new GameObject("Facing").transform; facing.SetParent(root.transform, false);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets_Imported/Blink/Art/Characters/Stylized/Humans/Prefabs_Humans/HumanMale_Character_Free.prefab");
                var model = UnityEngine.Object.Instantiate(source, facing); model.name = "Armored Guard";
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Starter" || t.name.StartsWith("Starter_")) t.gameObject.SetActive(true);
                    if (t.name == "Hairs" || t.name == "Beards") t.gameObject.SetActive(false);
                }
                var armorSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/PlayerHuman/M_PlayerHuman_StarterArmor_1.mat");
                var armor=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Guard_Armor.mat");
                if(armor==null)
                {
                    armor = new Material(armorSource) { name = "Oakshade_Guard_Armor" };
                    armor.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Assets_Imported/Blink/Art/Characters/Stylized/Humans/Armors_Humans/Textures_ArmorsHumans/StarterArmor_BaseColor2.png"));
                    armor.SetFloat("_Metallic", .5f); armor.SetFloat("_Smoothness", .32f); AssetDatabase.CreateAsset(armor, Folder + "/Materials/Guard_Armor.mat");
                }
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) { UnityEngine.Object.DestroyImmediate(skin); continue; }
                    skin.sharedMaterials = skin.sharedMaterials.Select(m => skin.name.StartsWith("Starter_") ? armor : m != null ? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/PlayerHuman/M_PlayerHuman_" + m.name + ".mat") ?? m : null).ToArray();
                    skin.updateWhenOffscreen = true;
                }
                ApplyGuardColors(model);
                var animator = model.GetComponent<Animator>();
                if (animator == null || !animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Guard Humanoid avatar is invalid.");
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TownNpcSetup.Folder + "/Townsfolk.controller"); animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Update(0); animator.SetFloat("Speed",0); animator.Play("Locomotion", 0, .3f); animator.Update(0);
                var bounds = GuardBounds(model); model.transform.localScale *= 1.88f / bounds.size.y; bounds = GuardBounds(model); model.transform.localPosition -= Vector3.up * bounds.min.y;
                var look = root.AddComponent<NpcIdleLook>(); look.Configure(facing, null); String(look,"npcId","oakshade_guard_01"); String(look,"displayName","Oakshade Guard");
                var body = root.AddComponent<CapsuleCollider>(); body.center = Vector3.up * .94f; body.height = 1.88f; body.radius = .3f;
                var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.center = body.center; obstacle.height = body.height; obstacle.radius = .34f; obstacle.carving = true;
                Identity(root,"oakshade_guard","Oakshade Guard","Village quest contact / Story Scenes 02-03; quest state not installed");
                prefab = PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                CaptureGuard(root);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var installed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PlaceGuard(installed, layout);
    }
    private static void PlaceGuard(GameObject installed, ForestMapLayout layout)
    {
        // Old city arrivals can remain after village edits. Only village-owned anchors are valid here.
        var arrival = layout.arrivals?.Where(t => t != null && t.IsChildOf(layout.village)).OrderBy(t => Vector3.Distance(t.position,layout.village.position)).FirstOrDefault();
        Vector3 desired = arrival != null ? arrival.position : layout.village.position + Vector3.back * 16;
        // Keep the guard near the village entrance, on a reachable clear patch, without altering the village population.
        bool found = false;
        foreach (float radius in new[] { 2f, 3f, 5f, 7f })
        {
            for (int i = 0; i < 8; i++)
            {
                var p = desired + Quaternion.Euler(0,i*45,0)*Vector3.forward*radius;
                if (!NavMesh.SamplePosition(p,out var nav,2,NavMesh.AllAreas) || Physics.CheckCapsule(nav.position+Vector3.up*.4f,nav.position+Vector3.up*1.5f,.34f,1<<7,QueryTriggerInteraction.Ignore)) continue;
                installed.transform.position = nav.position; found = true; break;
            }
            if (found) break;
        }
        if (!found) throw new InvalidOperationException("No clear village guard standing position.");
        installed.transform.SetParent(layout.village,true); installed.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(desired-installed.transform.position,Vector3.up));
        var lookAt = installed.GetComponent<NpcIdleLook>(); lookAt.SetLookTarget(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform); PrefabUtility.RecordPrefabInstancePropertyModifications(lookAt);
    }
    private static Bounds GuardBounds(GameObject model)
    {
        Bounds bounds=default;bool first=true;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(!skin.enabled||skin.sharedMesh==null)continue;var mesh=new Mesh();skin.BakeMesh(mesh);
            foreach(var vertex in mesh.vertices){var p=skin.transform.TransformPoint(vertex);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        if(first)throw new InvalidOperationException("Guard has no visible geometry.");return bounds;
    }
    private static void CaptureGuard(GameObject source)
    {
        var preview = new PreviewRenderUtility(); Texture2D image = null;
        try
        {
            var clone = UnityEngine.Object.Instantiate(source); foreach (var m in clone.GetComponents<MonoBehaviour>()) m.enabled = false; preview.AddSingleGO(clone);
            var a = clone.GetComponentInChildren<Animator>(); a.Rebind(); a.Update(0); a.Play("Locomotion",0,.3f); a.Update(0);
            var camera = preview.camera; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f,.12f,.15f); camera.fieldOfView = 30; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.transform.position = new Vector3(3,1.8f,5); camera.transform.LookAt(Vector3.up*.95f);
            preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(40,35,0); preview.lights[1].intensity = .7f; preview.lights[1].transform.rotation = Quaternion.Euler(20,210,0); preview.ambientColor = Color.gray*.7f;
            preview.BeginStaticPreview(new Rect(0,0,700,800)); preview.Render(true); image = preview.EndStaticPreview(); File.WriteAllBytes("Logs/OakshadeGuard.png",image.EncodeToPNG());
        }
        finally { if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup(); }
    }
    private static void ApplyGuardColors(GameObject model)
    {
        foreach (var part in model.GetComponentsInChildren<Transform>(true))
        {
            if (part.name == "Hairs") part.gameObject.SetActive(true);
            if (part.parent != null && part.parent.name == "Hairs") part.gameObject.SetActive(part.name == "Hair2");
        }
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool body = skin.name == "Face1" || skin.name == "Neck" || skin.name == "Arms" || skin.name == "Hands";
            bool hair = skin.name == "Hair2" || skin.name == "Eyebrow1";
            if (!body && !hair) continue;
            string sourceName = body ? skin.name == "Face1" ? "Face1" : "Body" : skin.name == "Hair2" ? "Hair2" : "Eyebrows";
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/PlayerHuman/M_PlayerHuman_" + sourceName + ".mat");
            string path = Folder + "/Materials/Guard_" + sourceName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(original) { name = "Guard_" + skin.name };
                material.SetColor("_BaseColor", body ? new Color(1,.72f,.55f) : new Color(.22f,.13f,.07f));
                material.SetFloat("_Smoothness", .15f);
                AssetDatabase.CreateAsset(material, path);
            }
            skin.sharedMaterial = material;
        }
    }
}
#endif
