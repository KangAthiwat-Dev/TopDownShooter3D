#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

public static partial class VillageFarmSetup
{
    private static Terrain siteTerrain;
    private static System.Random random;
    private static float Height(float x, float z) => siteTerrain.SampleHeight(new Vector3(x, 0, z)) + siteTerrain.transform.position.y;
    private static Transform Group(string name, Transform parent)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = 7; return go.transform; }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static string HeightHash(TerrainData data)
    {
        var values = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        var bytes = new byte[values.Length * sizeof(float)]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        using (var sha = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes));
    }
    private static GameObject Piece(string family, string stem, Transform parent, Vector3 local, float yaw = 0, Vector3? scale = null)
    {
        string path = Kit + "/Generated/Prefabs/VK/" + family + "/SM_VK_" + stem + ".prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new IOException(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = scale ?? Vector3.one;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 7;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }
    private static GameObject At(string family, string stem, Transform parent, float x, float z, float yaw = 0, float scale = 1)
    {
        var go = Piece(family, stem, parent, Vector3.zero, yaw, Vector3.one * scale);
        go.transform.position = new Vector3(x, Height(x, z), z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); return go;
    }
    private static void SolidBounds(GameObject go, bool excludeNavigation = true, float inset = 0)
    {
        var b = BoundsOf(go); var colliderRoot = Group("Solid_Scenery_Collision", go.transform);
        colliderRoot.position = b.center; colliderRoot.rotation = Quaternion.identity; colliderRoot.localScale = Vector3.one;
        var box = colliderRoot.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(Mathf.Max(.1f, b.size.x - inset), b.size.y, Mathf.Max(.1f, b.size.z - inset));
        box.size = Vector3.Scale(box.size, new Vector3(1 / colliderRoot.lossyScale.x, 1 / colliderRoot.lossyScale.y, 1 / colliderRoot.lossyScale.z));
        if (excludeNavigation) { var nav = colliderRoot.gameObject.AddComponent<NavMeshModifier>(); nav.overrideArea = true; nav.area = 1; }
    }
    private static void RuralStyle(GameObject go)
    {
        var thatch = AssetDatabase.LoadAssetAtPath<Material>(Kit + "/Generated/Materials/M_VK_Roof_Thatch.mat");
        var daub = AssetDatabase.LoadAssetAtPath<Material>(Kit + "/Generated/Materials/M_VK_Plaster_Daub.mat");
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && m.name == "M_VK_RoofRed" ? thatch : m != null && m.name == "M_VK_Plaster" ? daub : m).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
    private static Transform Cottage(Transform parent, string name, float x, float z, float yaw, int bays, string wall, float scale)
    {
        var house = Group(name, parent); house.position = new Vector3(x, Height(x, z), z); house.rotation = Quaternion.Euler(0, yaw, 0); house.localScale = Vector3.one * scale;
        var shell = Group("01_Assembled_Walls", house); var roof = Group("02_Yellow_Thatch_Roof", house); var detail = Group("03_Porch_and_Household", house);
        float width = bays * 3;
        for (int i = 0; i < bays; i++)
        {
            float px = -width * .5f + 1.5f + i * 3;
            Piece("Wall", "Wall_" + wall + (i == 0 ? "_Door" : "_Window"), shell, new Vector3(px, 0, 1.5f));
            Piece("Wall", "Wall_" + wall + (i == 0 ? "_Window" : ""), shell, new Vector3(px, 0, -1.5f), 180);
            Piece("RoofThatchS", bays == 1 ? "RoofThatchS_Single" : "RoofThatchS_Gable", roof, new Vector3(px, 3, 0), i == 0 ? 0 : 180);
        }
        Piece("Wall", "Wall_" + wall + "_Window", shell, new Vector3(-width * .5f, 0, 0), 270);
        Piece("Wall", "Wall_" + wall, shell, new Vector3(width * .5f, 0, 0), 90);
        for (int i = 0; i < 4; i++) Piece("Corner", "Corner_" + wall, shell, new Vector3(i < 2 ? width * .5f : -width * .5f, 0, i == 0 || i == 3 ? 1.5f : -1.5f), -90 * i);
        Piece("Prop", "Prop_Lantern", detail, new Vector3(-width * .5f + 2.45f, 2.2f, 1.65f), 0, Vector3.one * .75f);
        Piece("Prop", "Prop_Sacks", detail, new Vector3(width * .5f + .65f, 0, -.75f), 30, Vector3.one * .6f);
        Piece("Prop", "Prop_Stool", detail, new Vector3(-width * .5f, 0, 2.05f), 10, Vector3.one * .8f);
        RuralStyle(house.gameObject);
        var physics = Group("04_Closed_House_Collision", house); var box = physics.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 2.5f, 0); box.size = new Vector3(width + .45f, 5f, 3.45f);
        var modifier = physics.gameObject.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1;
        var threshold = Group("Door_Activity_Anchor", house); threshold.localPosition = new Vector3(-width * .5f + 1.5f, 0, 3.1f); threshold.localRotation = Quaternion.Euler(0, 180, 0);
        return house;
    }
    private static void FenceLine(Transform parent, Vector2 start, Vector2 end)
    {
        float length = Vector2.Distance(start, end); int count = Mathf.Max(1, Mathf.RoundToInt(length / 2.7f));
        float yaw = -Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            var p = Vector2.Lerp(start, end, (i + .5f) / count);
            var go = At("Prop", "Prop_Fence", parent, p.x, p.y, yaw);
            go.transform.localScale = new Vector3(length / count / 3f, .84f, .84f);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, .6f, 0); col.size = new Vector3(3, 1.2f, .21f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
    }
    private static Transform Pen(Transform parent, string name, float x, float z, float width, float depth)
    {
        var pen = Group(name, parent); var fence = Group("Wood_Fence_and_Open_Gate", pen);
        float left = x - width / 2, right = x + width / 2, front = z - depth / 2, rear = z + depth / 2;
        FenceLine(fence, new Vector2(left, front), new Vector2(left, rear)); FenceLine(fence, new Vector2(left, rear), new Vector2(right, rear));
        FenceLine(fence, new Vector2(right, rear), new Vector2(right, front));
        FenceLine(fence, new Vector2(left, front), new Vector2(x - 1.4f, front)); FenceLine(fence, new Vector2(x + 1.4f, front), new Vector2(right, front));
        At("Prop", "Prop_FenceGate", fence, x + 1.1f, front + .95f, 65, .73f);
        var trough = At("Prop", "Prop_Trough", pen, left + 1.6f, z - .8f, 90, .85f); SolidBounds(trough);
        At("Prop", "Prop_HayBale", pen, right - 1.2f, rear - 1.4f, 16, .8f);
        return pen;
    }
    private static void Farms(Transform root)
    {
        var plots = Group("03_Vegetable_and_Herb_Plots", root);
        string[] crops = { "Cabbage", "Carrot", "Pumpkin", "Wheat" };
        for (int x = 0; x < 4; x++) for (int z = 0; z < 3; z++)
            At("Crop", "Crop_" + crops[x], plots, -81f + x * 3.7f, 55.5f + z * 3.65f, (float)(random.NextDouble() * 5 - 2.5), .92f);
        FenceLine(plots, new Vector2(-83.2f, 53.3f), new Vector2(-83.2f, 65.3f)); FenceLine(plots, new Vector2(-83.2f, 65.3f), new Vector2(-69.2f, 65.3f));
        At("Prop", "Prop_Scarecrow", plots, -70, 65.3f, 25, .8f);
        var wheelbarrow = At("Prop", "Prop_Wheelbarrow", plots, -67.1f, 59.6f, -20, .7f); SolidBounds(wheelbarrow);
        At("Prop", "Prop_ShopGoods_Produce", plots, -69.5f, 52.6f, 12, .68f);
        At("Crop", "Crop_Lavender", plots, -76.5f, 47.5f, 0, .68f);
        At("Prop", "Prop_GardenBed", plots, -73.4f, 47.5f, 0, .85f);
        var livestock = Group("04_Livestock_Pens_and_Shelters", root);
        var sheep = Pen(livestock, "Sheep_and_Goat_Enclosure", -77.5f, 74.8f, 12, 9);
        Cottage(sheep, "Yellow_Roof_Shepherd_Shelter", -80.1f, 77.4f, 180, 1, "Wattle", .74f);
        At("Prop", "Prop_HayRack", sheep, -73.5f, 77.7f, 0, .66f);
        At("Animal", "Animal_Sheep", sheep, -80.5f, 73, 40, .95f); At("Animal", "Animal_Sheep", sheep, -77.9f, 75.1f, -15, .88f);
        At("Animal", "Animal_Sheep", sheep, -74.2f, 73.9f, 155, 1); At("Animal", "Animal_Goat", sheep, -75.1f, 77.1f, 130, .9f);
        var cows = Pen(livestock, "Cattle_Enclosure", -53.2f, 73.8f, 9, 9);
        Cottage(cows, "Yellow_Roof_Cattle_Shelter", -54.8f, 76.3f, 180, 1, "Wattle", .72f);
        At("Animal", "Animal_Cow", cows, -53.6f, 72.3f, 40, .9f); At("Animal", "Animal_Cow", cows, -50.8f, 74.1f, -30, .83f);
        var chicken = Group("Chicken_Yard", livestock);
        var coop = At("Prop", "Prop_ChickenCoop_Wattle", chicken, -86.2f, 57.2f, 90, .8f); RuralStyle(coop); SolidBounds(coop);
        At("Animal", "Animal_Chickens", chicken, -85.7f, 54.6f, 35, .95f); At("Animal", "Animal_Chickens", chicken, -85.5f, 59.5f, -45, .8f);
        FenceLine(chicken, new Vector2(-88, 52.9f), new Vector2(-88, 60.8f));
    }
    private static void Nature(Transform root)
    {
        var nature = Group("06_Orchard_Shrubs_and_Wild_Growth", root);
        var trees = new[] { new Vector2(-87, 80), new Vector2(-85, 84), new Vector2(-76, 90), new Vector2(-61, 90), new Vector2(-44, 84), new Vector2(-40.5f, 80), new Vector2(-41.5f, 75), new Vector2(-85.2f, 39), new Vector2(-73.5f, 38.5f), new Vector2(-46.2f, 70), new Vector2(-86.5f, 62.7f), new Vector2(-56, 85) };
        for (int i = 0; i < trees.Length; i++)
        {
            var p = trees[i]; var tree = At("Tree", i % 3 == 0 ? "Tree_Apple" : "Tree_Birch_Single", nature, p.x, p.y, i * 43, i % 3 == 0 ? .63f : .5f);
            var col = tree.AddComponent<CapsuleCollider>(); col.radius = .3f; col.height = 3.2f; col.center = Vector3.up * 1.6f;
        }
        // Irregular clusters at parcel edges; keep the village lanes and door approaches clear.
        for (int i = 0; i < 65; i++)
        {
            var anchor = trees[i % trees.Length]; var p = anchor + new Vector2((float)random.NextDouble() * 3.6f - 1.8f, (float)random.NextDouble() * 3.6f - 1.8f);
            if (Height(p.x, p.y) > 4.15f || p.y < 37) continue;
            At(i % 4 == 0 ? "Bush" : "Plant", i % 4 == 0 ? "Bush_Berry" : i % 2 == 0 ? "Plant_Fern" : "Plant_TallGrass", nature, p.x, p.y, i * 29, i % 4 == 0 ? .45f : .65f);
        }
        foreach (var p in new[] { new Vector2(-78, 83), new Vector2(-63, 84), new Vector2(-48, 82), new Vector2(-50, 50), new Vector2(-66, 42), new Vector2(-82, 66), new Vector2(-57, 61) })
        {
            At("Plant", "Plant_Wildflowers", nature, p.x, p.y, 24, .55f);
            At("Bush", "Bush_Round", nature, p.x + .8f, p.y - .5f, 82, .42f);
        }
    }
    private static Material VillageCloth(Material source, string palette, Color tint)
    {
        string path = Folder + "/Materials/Villager_Cloth_" + palette + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var mat = new Material(source) { name = "Villager_Cloth_" + palette };
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/ThirdParty/Quaternius/Modular Character Outfits/Textures/Peasant/T_Peasant_2_BaseColor.png");
        mat.SetTexture("_BaseMap", texture); mat.SetTexture("_MainTex", texture); mat.SetColor("_BaseColor", tint); mat.SetFloat("_Smoothness", .03f);
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
    private static TownNpcBrain Villager(string source, string name, Color tint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source); var go = UnityEngine.Object.Instantiate(prefab); go.name = "Villager_" + name;
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Material cloth = null;
            foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                { if (m != null && m.name.Contains("Cloth")) { if (cloth == null) cloth = VillageCloth(m, name, tint); return cloth; } return m; }).ToArray();
            var so = new SerializedObject(go.GetComponent<TownNpcBrain>());
            so.FindProperty("walkSpeed").floatValue = 1.35f; so.FindProperty("routineRunChance").floatValue = .04f; so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + go.name + ".prefab").GetComponent<TownNpcBrain>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static void Population(Transform root)
    {
        EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Prefabs");
        string male = TownNpcSetup.Folder + "/Prefabs/Citizen_Russet.prefab";
        string female = TownNpcDiversitySetup.Folder + "/Prefabs/";
        var men = new[] { Villager(male, "Male_Linen", new Color(.94f, .88f, .77f)), Villager(male, "Male_Earth", new Color(.74f, .66f, .56f)), Villager(male, "Male_Ash", new Color(.75f, .77f, .74f)) };
        var women = new[] { Villager(female + "Citizen_Female_Amber_Buns.prefab", "Female_Oat", new Color(.98f, .91f, .77f)), Villager(female + "Citizen_Female_Plum_Buns.prefab", "Female_Clay", new Color(.83f, .72f, .63f)), Villager(female + "Citizen_Female_Teal_Long.prefab", "Female_Sage", new Color(.73f, .80f, .67f)) };
        var manager = Group("07_Village_NPC_Population", root).gameObject.AddComponent<TownNpcPopulation>();
        var routes = Group("Editable_Village_Activities", manager.transform);
        var sites = new[] {
            new Vector2(-59, 38), new Vector2(-61, 45), new Vector2(-62, 50), new Vector2(-62, 55), new Vector2(-63.8f, 56.8f),
            new Vector2(-62, 60.5f), new Vector2(-61, 65), new Vector2(-61, 70), new Vector2(-64, 77.5f), new Vector2(-67, 81.5f),
            new Vector2(-76, 82.5f), new Vector2(-78, 67.5f), new Vector2(-68, 64.5f), new Vector2(-66, 56.5f), new Vector2(-68, 50),
            new Vector2(-55, 60), new Vector2(-51, 65.5f), new Vector2(-49, 53), new Vector2(-56, 80), new Vector2(-73, 44.5f), new Vector2(-78, 47.5f) };
        var points = new List<TownNpcActivityPoint>();
        for (int i = 0; i < sites.Length; i++)
        {
            var p = sites[i]; var wanted = new Vector3(p.x, Height(p.x, p.y), p.y);
            if (!NavMesh.SamplePosition(wanted, out var hit, 1.1f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - wanted.y) > .4f) throw new InvalidOperationException("Village activity has no ground navigation: " + wanted);
            var point = Group("Activity_" + i.ToString("D2"), routes).gameObject.AddComponent<TownNpcActivityPoint>(); point.transform.position = hit.position;
            var s = new SerializedObject(point); s.FindProperty("activity").enumValueIndex = (int)(i == 3 || i == 4 || i == 5 ? TownNpcActivityKind.Social : i == 13 || i == 14 || i == 17 || i == 20 ? TownNpcActivityKind.Interact : TownNpcActivityKind.Wander); s.ApplyModifiedPropertiesWithoutUndo(); points.Add(point);
        }
        var settings = new SerializedObject(manager);
        void Array(string field, UnityEngine.Object[] objects) { var a = settings.FindProperty(field); a.arraySize = objects.Length; for (int i = 0; i < objects.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = objects[i]; }
        Array("citizenPrefabs", men); Array("femalePrefabs", women); Array("cityPoints", points.Cast<UnityEngine.Object>().ToArray()); Array("outsidePoints", System.Array.Empty<UnityEngine.Object>()); Array("constructionStations", System.Array.Empty<UnityEngine.Object>());
        settings.FindProperty("maxPopulation").intValue = 10; settings.FindProperty("femaleCount").intValue = 5; settings.FindProperty("constructionWorkers").intValue = 0;
        settings.FindProperty("constructionWorkerPrefab").objectReferenceValue = null; settings.FindProperty("maxOutside").intValue = 0; settings.FindProperty("roamingCitizenChance").floatValue = 0;
        settings.FindProperty("cityFrame").objectReferenceValue = root;
        settings.FindProperty("cityCenter").vector3Value = root.InverseTransformPoint(new Vector3(-65, 4, 64)); settings.FindProperty("citySize").vector2Value = new Vector2(46, 56);
        settings.FindProperty("avoidUnbakedCityProps").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("Tools/World/Yellow Roof Village/Build Village Farm and Villagers")]
    public static void Build()
    {
        RequireScene(); if (GameObject.Find(RootName) != null) throw new InvalidOperationException("Village already built; preserve later hand edits.");
        var scene = SceneManager.GetActiveScene(); var map = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>(); siteTerrain = map.terrain; random = new System.Random(271026);
        var zone = GameObject.Find("Zone_Villager")?.transform ?? throw new InvalidOperationException("Cannot find the user village sketch.");
        var sketches = zone.Cast<Transform>().ToArray();
        string[] extraNames = { "SM_VK_Banner_Pole", "SM_VK_BreadOven", "SM_VKI_Prop_Hearth_Open", "SM_VKI_Prop_Barrel", "SM_VKI_Prop_Workbench_Carpenter" };
        var extras = scene.GetRootGameObjects().Where(g => extraNames.Contains(g.name) && g.transform.position.x < -40 && g.transform.position.z > 30).Select(g => g.transform).ToArray();
        var existing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null && c != map && !(c is NavMeshSurface) && c.transform != zone && !c.transform.IsChildOf(zone) && !extras.Any(t => c.transform == t || c.transform.IsChildOf(t))).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        string heightHash = HeightHash(siteTerrain.terrainData);
        Directory.CreateDirectory("Logs/SceneBackups"); string backup = "Logs/SceneBackups/WorldPrototype_BeforeYellowVillage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Cannot back up current live scene.");
        EnsureFolder(Folder);
        var root = Group(RootName, zone); root.position = new Vector3(-65, 4, 64); Undo.RegisterCreatedObjectUndo(root.gameObject, "Assemble yellow roof village");
        var archive = Group("00_Original_Sketch_Preserved_Inactive", root);
        foreach (var sketch in sketches.Concat(extras)) Undo.SetTransformParent(sketch, archive, "Preserve village sketch"); archive.gameObject.SetActive(false);
        var homes = Group("01_Yellow_Roof_Cottages", root);
        Cottage(homes, "01_Orchard_Family_House", -80, 86, 165, 2, "Wattle", .87f);
        Cottage(homes, "02_Linen_Cottage", -65.5f, 87, 190, 2, "Log", .86f);
        Cottage(homes, "03_Eastern_Farmhouse", -49, 83, 215, 2, "Wattle", .84f);
        Cottage(homes, "04_Woodcutters_Cottage", -46.2f, 65, 270, 1, "Log", .83f);
        Cottage(homes, "05_Gardeners_Cottage", -51.5f, 48.5f, 290, 1, "Wattle", .85f);
        Cottage(homes, "06_Bread_and_Linen_House", -66.5f, 41.8f, 8, 2, "Wattle", .82f);
        Cottage(homes, "07_Shepherds_House", -86, 67.7f, 90, 1, "Log", .83f);
        var common = Group("02_Well_Lane_and_Household_Props", root);
        var well = At("Prop", "Prop_Well", common, -57.6f, 57, 10, .85f); RuralStyle(well); SolidBounds(well, true, .25f);
        var bench = At("Prop", "Prop_Bench", common, -59, 61, 25, .8f); SolidBounds(bench);
        var notice = At("Prop", "Prop_NoticeBoard", common, -56, 53.8f, 230, .7f); RuralStyle(notice); SolidBounds(notice);
        var oven = At("BreadOven", "BreadOven", common, -71.2f, 42.4f, 70, .75f); RuralStyle(oven); SolidBounds(oven);
        var cart = At("Prop", "Prop_Cart", common, -52.6f, 60.8f, -28, .75f); SolidBounds(cart);
        At("Prop", "Prop_Woodpile", common, -49.2f, 66.2f, 90, .7f);
        At("Prop", "Prop_Laundry", common, -61.3f, 84.8f, 15, .8f);
        At("Prop", "Prop_Signpost", common, -57.2f, 39.1f, 10, .7f);
        At("Prop", "Prop_HandPump", common, -65.3f, 49, 12, .7f);
        Farms(root);
        var landmarks = Group("05_Windmill_and_Farm_Details", root);
        var mill = archive.Find("tower");
        if (mill != null)
        {
            Undo.SetTransformParent(mill, landmarks, "Reuse assembled village windmill"); mill.localScale *= .58f; mill.position = new Vector3(-82, 4, 44);
            var b = BoundsOf(mill.gameObject); mill.position += Vector3.up * (Height(-82, 44) - b.min.y); RuralStyle(mill.gameObject);
            foreach (var c in mill.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var solid = Group("Windmill_Base_Collision", mill); solid.position = new Vector3(-82, 7, 44); solid.rotation = Quaternion.identity;
            var col = solid.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(3.4f / solid.lossyScale.x, 6 / solid.lossyScale.y, 3.4f / solid.lossyScale.z);
            var modifier = solid.gameObject.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1;
        }
        At("Prop", "Prop_Haystack", landmarks, -76.2f, 41.7f, 15, .65f);
        At("Prop", "Prop_Woodpile", landmarks, -82, 47.5f, 20, .6f);
        Nature(root);
        var arrival = Group("VillageArrival", root); arrival.position = new Vector3(-59, 4, 37.5f); Undo.RecordObject(map, "Link new village landmark"); map.village = root;
        Physics.SyncTransforms(); var surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>(); Undo.RecordObject(surface, "Bake village lanes and fences"); surface.BuildNavMesh();
        AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/VillageNavigation.asset"); EditorUtility.SetDirty(surface);
        Population(root);
        bool scope = existing.All(pair => pair.Key != null && EditorJsonUtility.ToJson(pair.Key) == pair.Value);
        bool terrainUnchanged = heightHash == HeightHash(siteTerrain.terrainData);
        File.WriteAllText("Logs/VillageBuildScope.txt", "Backup=" + backup + "\n" + (scope ? "PASS" : "FAIL") + " pre-existing non-village components preserved (navigation and village landmark link excluded), compared=" + existing.Count + "\n" + (terrainUnchanged ? "PASS" : "FAIL") + " terrain heights, including front shallow stream and deeper valley, unchanged\n" + "Original user sketch and five loose village props retained inactive.\n");
        if (!scope || !terrainUnchanged) throw new InvalidOperationException("Village scope check failed; inspect Logs/VillageBuildScope.txt");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Capture(); Selection.activeGameObject = root.gameObject; SceneView.lastActiveSceneView?.LookAt(new Vector3(-65, 4, 64), Quaternion.Euler(55, 0, 0), 42);
        Debug.Log("Yellow thatch village and ten rural villagers assembled; existing shallow stream preserved.");
    }
    [MenuItem("Tools/World/Yellow Roof Village/Capture Village")]
    public static void Capture()
    {
        Render("YellowVillage_Overview", new Vector3(-65, 4, 64), new Vector3(28, 53, -62), 34);
        Render("YellowVillage_Top", new Vector3(-65, 4, 64), new Vector3(0, 100, .001f), 34);
        Render("YellowVillage_Farm", new Vector3(-76, 4, 62), new Vector3(17, 24, -30), 18);
    }
}
#endif
