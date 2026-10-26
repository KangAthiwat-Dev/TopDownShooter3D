using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>Small, reversible example zone using the installed Fantasy Worlds URP pack.</summary>
public static class ForestZoneSetup
{
    public const string Pack = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content/";
    public const string ZoneFolder = "Assets/_Project/World/ForestDemo";
    public const string RootName = "Zone_ForestDemo";
    private static Terrain terrain;
    private static Vector3 origin;
    private static System.Random random;

    [MenuItem("Tools/World/Build Forest Demo Around Player")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GameObject.Find(RootName) != null) throw new InvalidOperationException("Forest zone already exists. Edit it directly; the builder will not overwrite your work.");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (player == null) throw new InvalidOperationException("Player not found");
        terrain = Terrain.activeTerrains.First(t => t.terrainData != null && Contains(t, player.transform.position));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/P_fwOF_Tree_M_2.prefab") == null) throw new InvalidOperationException("Import Fantasy Worlds URP first");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(ZoneFolder + "/ForestTerrain.asset") != null) throw new InvalidOperationException("ForestTerrain.asset already exists; refusing to overwrite it.");
        BackupScene("BeforeForestZone");
        EnsureFolder(ZoneFolder);
        EnsureFolder(ZoneFolder + "/Materials");
        EnsureFolder(ZoneFolder + "/Layers");
        EnsureFolder(ZoneFolder + "/Prefabs");
        random = new System.Random(20261025);
        origin = new Vector3(player.transform.position.x, 0, player.transform.position.z);

        // Copy, never sculpt or modify the user's source TerrainData.
        string originalTerrain = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (!AssetDatabase.CopyAsset(originalTerrain, ZoneFolder + "/ForestTerrain.asset")) throw new IOException("Terrain copy failed");
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ZoneFolder + "/ForestTerrain.asset");
        Undo.RecordObject(terrain, "Forest terrain data");
        terrain.terrainData = data;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        Undo.RecordObject(terrainCollider, "Forest terrain collider"); terrainCollider.terrainData = data;
        Material terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_ForestTerrain" };
        // The pack's mask maps use metallic/AO/height/smoothness packing.
        // This zone is matte, with a lifted AO floor so the grass stays readable in shade.
        terrainMat.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
        AssetDatabase.CreateAsset(terrainMat, ZoneFolder + "/Materials/M_ForestTerrain.mat");
        terrain.materialTemplate = terrainMat; terrain.drawInstanced = true;
        PaintGround(data);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create forest demo");
        root.transform.position = Ground(Vector2.zero);
        Transform world = GameObject.Find("World")?.transform;
        if (world != null) root.transform.SetParent(world, true);
        Transform trees = Group("01_Trees", root.transform), rocks = Group("02_Rocks", root.transform), plants = Group("03_Undergrowth", root.transform);
        // Leave a ~28m-wide combat clearing. Break the ring with two trail exits.
        for (int i = 0; i < 15; i++)
        {
            float angle = i * Mathf.PI * 2f / 15f + Range(-0.08f, 0.08f);
            float radius = Range(20f, 27f);
            Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (PathDistance(p) < 3.8f) p.x += p.x < 0 ? -4 : 4;
            GameObject tree = Place("P_fwOF_Tree_M_2", trees, p, Range(0.64f, 0.87f), Range(0, 360));
            // The art prefab has 28 branch capsules. A trunk-only gameplay collider
            // avoids invisible branches blocking bullets, dashes and navigation.
            foreach (Collider c in tree.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var trunk = tree.AddComponent<CapsuleCollider>(); trunk.radius = 1.45f; trunk.height = 5f; trunk.center = new Vector3(0, 2.2f, 0);
            foreach (LODGroup lod in tree.GetComponentsInChildren<LODGroup>(true)) lod.fadeMode = LODFadeMode.CrossFade;
        }
        for (int i = 0; i < 32; i++)
        {
            Vector2 p = RandomRing(12.5f, 30f);
            if (PathDistance(p) < 3f) continue;
            Place("P_fwOF_Rock_01", rocks, p, Range(0.9f, 2.6f), Range(0, 360));
            for (int j = 0; j < 2; j++) Place("P_fwOF_Stone_01", rocks, p + RandomRing(0.7f, 1.8f), Range(0.6f, 1.6f), Range(0, 360));
        }
        for (int i = 0; i < 70; i++)
        {
            Vector2 p = RandomRing(8f, 30f);
            if (PathDistance(p) < 2.7f) continue;
            Place("P_fwOF_TreeSapling_02B", plants, p, Range(0.8f, 1.8f), Range(0, 360));
        }
        for (int i = 0; i < 160; i++)
        {
            Vector2 p = RandomRing(3.5f, 30f);
            if (PathDistance(p) < 2f) continue;
            Place("P_fwOF_ForestPlant_B_02", plants, p, Range(1f, 2.8f), Range(0, 360));
        }
        AddGrass(data);
        var windPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TriForge Assets/Fantasy Worlds - DEMO Common Files/TriForge Wind Controller.prefab");
        if (UnityEngine.Object.FindAnyObjectByType<TriForge.TF_FW_WindController>() == null && windPrefab != null)
        {
            var wind = (GameObject)PrefabUtility.InstantiatePrefab(windPrefab, root.transform);
            wind.name = "04_GentleForestWind"; wind.transform.rotation = Quaternion.Euler(0, 35, 0);
            var settings = wind.GetComponent<TriForge.TF_FW_WindController>();
            settings.WindStrength = 0.15f; settings.GrassWindStrength = 0.22f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        }
        SetLighting();
        RefineLook(root);
        Undo.RecordObject(player.transform, "Place player on raised ground");
        player.transform.position = Ground(Vector2.zero) + Vector3.up * 0.08f;
        var follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        if (follow != null)
        {
            var settings = new SerializedObject(follow);
            Undo.RecordObject(follow.transform, "Frame player on forest plateau");
            follow.transform.position = player.transform.position + settings.FindProperty("targetOffset").vector3Value;
        }
        // Reuse existing waves, only moving their six markers onto the playable clearing.
        var spawner = UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>();
        if (spawner != null)
        {
            var points = new SerializedObject(spawner).FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (point == null) continue;
                float angle = (i * 360f / points.arraySize + 25f) * Mathf.Deg2Rad;
                Undo.RecordObject(point, "Place forest encounter marker");
                point.position = Ground(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 10.5f) + Vector3.up * 0.1f;
            }
        }
        Physics.SyncTransforms();
        RebuildNavigation();
        EditorUtility.SetDirty(data); EditorUtility.SetDirty(terrain); EditorUtility.SetDirty(terrainCollider);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Logs/ForestZoneBuild.txt", $"Built {DateTime.Now:s}\nCenter: {origin}\nOriginal terrain retained: {originalTerrain}\nWorking terrain: {ZoneFolder}/ForestTerrain.asset\nHeightmap was not modified. Paint/foliage is limited to 34m from player.\nExisting wave configuration and gameplay tuning preserved.\n");
        Focus(); Validate(); CaptureOverview();
        Debug.Log("Forest demo built around Player. Source terrain/pack/old NavMesh retained. See Logs/ForestZoneValidation.txt.");
    }

    private static bool Contains(Terrain t, Vector3 p) => p.x >= t.transform.position.x && p.z >= t.transform.position.z && p.x <= t.transform.position.x + t.terrainData.size.x && p.z <= t.transform.position.z + t.terrainData.size.z;
    private static float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    private static Vector2 RandomRing(float min, float max) { float a = Range(0, Mathf.PI * 2); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sqrt(Range(min * min, max * max)); }
    private static Vector3 Ground(Vector2 offset) { Vector3 p = origin + new Vector3(offset.x, 0, offset.y); p.y = terrain.SampleHeight(p) + terrain.transform.position.y; return p; }
    private static float PathDistance(Vector2 p) => Mathf.Abs(p.x - (Mathf.Sin(p.y * 0.12f) * 3f + p.y * 0.14f));
    private static Transform Group(string name, Transform parent) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    private static void EnsureFolder(string path) { if (!AssetDatabase.IsValidFolder(path)) { EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); } }
    private static GameObject Place(string prefabName, Transform parent, Vector2 p, float scale, float yaw)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + prefabName + ".prefab"), parent);
        go.transform.SetPositionAndRotation(Ground(p), Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = terrain.gameObject.layer;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.lightProbeUsage = LightProbeUsage.Off;
        return go;
    }
    private static void PaintGround(TerrainData data)
    {
        TerrainLayer[] old = data.terrainLayers;
        float[,,] original = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        string[] source = { "TL_fwOF_Grass_01 1", "TL_fwOF_Soil_01", "TL_fwOF_TerrainMoss_01", "TL_fwOF_RockSurf_02" };
        var layers = old.ToList();
        foreach (string name in source)
        {
            TerrainLayer layer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainLayer>(Pack + "Textures/Terrain/" + name + ".terrainlayer"));
            layer.name = "Forest_" + name; layer.tileSize = Vector2.one * (name.Contains("Rock") ? 5f : 4f);
            layer.metallic = 0; layer.smoothness = 0; layer.normalScale = 0.2f;
            layer.maskMapRemapMin = new Vector4(0, 0.7f, 0, 0); layer.maskMapRemapMax = new Vector4(0, 1, 1, 0.12f);
            if (name.Contains("Grass")) layer.diffuseRemapMax = new Vector4(1.15f, 1.15f, 1.02f, 1);
            AssetDatabase.CreateAsset(layer, ZoneFolder + "/Layers/" + layer.name + ".terrainlayer"); layers.Add(layer);
        }
        data.terrainLayers = layers.ToArray();
        int w = data.alphamapWidth, h = data.alphamapHeight, start = old.Length;
        float[,,] result = new float[h, w, layers.Count];
        for (int z = 0; z < h; z++) for (int x = 0; x < w; x++)
        {
            Vector3 world = terrain.transform.position + new Vector3((float)x / (w - 1) * data.size.x, 0, (float)z / (h - 1) * data.size.z);
            Vector2 p = new Vector2(world.x - origin.x, world.z - origin.z);
            float blend = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(28, 34, p.magnitude));
            for (int l = 0; l < old.Length; l++) result[z, x, l] = original[z, x, l] * (1 - blend);
            if (blend <= 0) continue;
            float path = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.05f, 2.8f, PathDistance(p)))) * 0.65f;
            float slope = data.GetSteepness((float)x / (w - 1), (float)z / (h - 1));
            float rock = Mathf.InverseLerp(24, 48, slope) * 0.6f;
            float moss = Mathf.PerlinNoise(p.x * 0.1f + 30, p.y * 0.1f + 20) * 0.16f;
            float grass = Mathf.Max(0, 1 - path - rock - moss);
            float sum = grass + path + moss + rock;
            result[z, x, start] = blend * grass / sum; result[z, x, start + 1] = blend * path / sum;
            result[z, x, start + 2] = blend * moss / sum; result[z, x, start + 3] = blend * rock / sum;
        }
        data.SetAlphamaps(0, 0, result);
    }
    private static void AddGrass(TerrainData data)
    {
        string grassPath = ZoneFolder + "/Prefabs/ForestGrass.prefab";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/P_fwOF_Grass_M_1.prefab");
        Material material = new Material(source.GetComponent<MeshRenderer>().sharedMaterial) { name = "M_ForestGrass", enableInstancing = true };
        material.SetColor("_Color", new Color(0.87f, 0.96f, 0.65f, 1));
        material.SetColor("_RootColor", new Color(0.3f, 0.38f, 0.12f, 1));
        material.SetFloat("_FadeDistance", 65); material.SetFloat("_FadeFalloff", 4);
        AssetDatabase.CreateAsset(material, ZoneFolder + "/Materials/M_ForestGrass.mat");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            go.name = "ForestGrass"; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(go, grassPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var prototypes = data.detailPrototypes.ToList(); int index = prototypes.Count;
        prototypes.Add(new DetailPrototype {
            prototype = AssetDatabase.LoadAssetAtPath<GameObject>(grassPath), usePrototypeMesh = true,
            useInstancing = true, renderMode = DetailRenderMode.VertexLit,
            minWidth = 1.1f, maxWidth = 1.75f, minHeight = 1.1f, maxHeight = 1.65f,
            healthyColor = Color.white, dryColor = Color.white, noiseSpread = 0.23f,
            density = 1f, targetCoverage = 1f, useDensityScaling = true, positionJitter = 1f
        });
        if (index == 0) data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        data.detailPrototypes = prototypes.ToArray();
        PaintGrass(data, index);
        terrain.detailObjectDistance = 65; terrain.detailObjectDensity = 1;
    }
    private static void PaintGrass(TerrainData data, int index)
    {
        int res = data.detailResolution; int[,] grass = new int[res, res];
        float cellArea = data.size.x * data.size.z / (res * res);
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
        {
            Vector2 p = new Vector2(terrain.transform.position.x + (x + 0.5f) / res * data.size.x - origin.x, terrain.transform.position.z + (z + 0.5f) / res * data.size.z - origin.z);
            float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(27, 32, p.magnitude));
            float path = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, 3.2f, PathDistance(p)));
            float chance = cellArea * 5.2f * edge * Mathf.Lerp(0.05f, 1, path);
            if (random.NextDouble() < chance) grass[z, x] = data.detailScatterMode == DetailScatterMode.InstanceCountMode ? 1 : data.maxDetailScatterPerRes;
        }
        data.SetDetailLayer(0, 0, index, grass);
    }
    private static void SetLighting()
    {
        Material skySource = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/M_fwOF_SkyBox_01.mat");
        Material sky = new Material(skySource) { name = "M_ForestSky" }; sky.SetFloat("_Exposure", 1f); sky.SetColor("_Tint", new Color(0.7f, 0.75f, 0.8f, 1));
        AssetDatabase.CreateAsset(sky, ZoneFolder + "/Materials/M_ForestSky.mat"); RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.526f, 0.687f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.96f, 0.78f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.25f, 0.09f);
        Light sun = RenderSettings.sun ?? UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null) { Undo.RecordObject(sun, "Warm forest sunlight"); sun.color = new Color(1, 0.95f, 0.8f); sun.intensity = 3.5f; sun.shadowStrength = 0.82f; RenderSettings.sun = sun; }
    }

    private static void RefineLook(GameObject root)
    {
        Material grass = AssetDatabase.LoadAssetAtPath<Material>(ZoneFolder + "/Materials/M_ForestGrass.mat");
        grass.SetColor("_Color", new Color(0.75f, 0.90f, 0.57f, 1));
        grass.SetColor("_RootColor", new Color(0.23f, 0.32f, 0.09f, 1));
        grass.SetFloat("_RootMaskStrength", -0.2f); grass.SetFloat("_NormalScale", 0.25f);
        grass.SetFloat("_TransStrength", 0.7f); grass.SetFloat("_TransDirect", 0.85f);
        EditorUtility.SetDirty(grass);
        // A few nearer silhouettes make the forest visible from the actual
        // top-down camera, not only from an editor overview.
        if (root.transform.Find("06_ClearingLandmarks") == null)
        {
            Transform landmarks = Group("06_ClearingLandmarks", root.transform);
            foreach (Vector2 p in new[] { new Vector2(-8, 10), new Vector2(10, 7.5f), new Vector2(-13, -4) })
            {
                var tree = Place("P_fwOF_Tree_M_2", landmarks, p, 0.68f, Range(0, 360));
                foreach (Collider c in tree.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                var trunk = tree.AddComponent<CapsuleCollider>(); trunk.radius = 1.45f; trunk.height = 5; trunk.center = new Vector3(0, 2.2f, 0);
            }
            for (int i = 0; i < 24; i++)
            {
                Vector2 p = RandomRing(4.5f, 9);
                if (PathDistance(p) > 3) Place("P_fwOF_ForestPlant_B_02", landmarks, p, Range(1.2f, 2.5f), Range(0, 360));
            }
        }
        Transform existing = root.transform.Find("05_ForestLook");
        if (existing == null)
        {
            var look = new GameObject("05_ForestLook"); look.transform.SetParent(root.transform, false);
            var box = look.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(70, 70, 70);
            var volume = look.AddComponent<Volume>(); volume.isGlobal = false; volume.priority = 10; volume.blendDistance = 8;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "ForestLook";
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.value = 0.45f; color.contrast.value = 12; color.saturation.value = 6;
            AssetDatabase.CreateAsset(profile, ZoneFolder + "/ForestLook.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            volume.sharedProfile = profile;
        }
        var mainCameraData = Camera.main.GetUniversalAdditionalCameraData();
        Undo.RecordObject(mainCameraData, "Anti-alias forest foliage");
        mainCameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        mainCameraData.antialiasingQuality = AntialiasingQuality.High;
    }

    [MenuItem("Tools/World/Rebake Forest Navigation")]
    public static void RebuildNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity" || GameObject.Find(RootName) == null)
            throw new InvalidOperationException("Open WorldPrototype with Forest Demo before rebaking its navigation.");
        NavMeshSurface surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null) throw new InvalidOperationException("Navigation surface missing");
        // Build a new asset; the previous baked NavMesh is retained for recovery.
        Undo.RecordObject(surface, "Update navigation for raised forest terrain");
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh build failed");
        string path = AssetDatabase.GenerateUniqueAssetPath(ZoneFolder + "/ForestNavigation.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, path); EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/World/Focus Forest Demo")]
    public static void Focus()
    {
        GameObject root = GameObject.Find(RootName); if (root == null) return;
        Selection.activeGameObject = root;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) view.LookAt(root.transform.position + Vector3.up * 2f, Quaternion.Euler(52, 0, 0), 35f, false, true);
    }

    [MenuItem("Tools/World/Validate Forest Demo")]
    public static void Validate()
    {
        var report = new StringBuilder(); bool passed = true;
        Action<bool, string> check = (ok, message) => { report.AppendLine((ok ? "PASS " : "FAIL ") + message); passed &= ok; };
        Terrain t = Terrain.activeTerrains.FirstOrDefault(a => AssetDatabase.GetAssetPath(a.terrainData) == ZoneFolder + "/ForestTerrain.asset");
        GameObject root = GameObject.Find(RootName);
        check(root != null && t != null, "Forest root and working terrain exist");
        if (root != null && t != null)
        {
            TerrainData original = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/New Terrain 1.asset");
            float[,] a = original.GetHeights(0, 0, original.heightmapResolution, original.heightmapResolution), b = t.terrainData.GetHeights(0, 0, t.terrainData.heightmapResolution, t.terrainData.heightmapResolution);
            bool equal = a.GetLength(0) == b.GetLength(0) && a.GetLength(1) == b.GetLength(1);
            if (equal) for (int z = 0; z < a.GetLength(0); z++) for (int x = 0; x < a.GetLength(1); x++) equal &= a[z, x] == b[z, x];
            check(equal, "Original sculpted heightmap preserved exactly");
            check(t.GetComponent<TerrainCollider>().terrainData == t.terrainData, "Terrain and physics collider share copied terrain data");
            check(t.terrainData.detailPrototypes.Length > 0, "Grass detail layer installed");
            if (t.terrainData.detailPrototypes.Length > 0) { long count = 0; foreach (int value in t.terrainData.GetDetailLayer(0, 0, t.terrainData.detailWidth, t.terrainData.detailHeight, t.terrainData.detailPrototypes.Length - 1)) count += value; check(count > 1000, "Grass actually painted: " + count + " detail instances"); }
            check(root.GetComponentsInChildren<Transform>(true).All(o => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(o.gameObject) == 0), "No missing scripts on forest objects");
            foreach (Material m in root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                check(m.shader != null && m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader), "Shader valid: " + m.name);
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            Vector3 target = player.transform.position;
            check(Physics.Raycast(target + Vector3.up, Vector3.down, out RaycastHit support, 3f, 1 << t.gameObject.layer, QueryTriggerInteraction.Ignore) && Mathf.Abs(target.y - support.point.y) < 0.2f, "Player starts on walkable ground or summoning platform");
            check(NavMesh.SamplePosition(target, out NavMeshHit playerNav, 1f, NavMesh.AllAreas), "Player is on rebuilt NavMesh");
            var spawner = UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>();
            if (spawner != null)
            {
                var points = new SerializedObject(spawner).FindProperty("spawnPoints");
                for (int i = 0; i < points.arraySize; i++)
                {
                    var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    var path = new NavMeshPath();
                    bool reachable = point != null && NavMesh.SamplePosition(point.position, out NavMeshHit spawn, 1f, NavMesh.AllAreas) && NavMesh.CalculatePath(spawn.position, playerNav.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                    check(reachable, "Spawn " + (i + 1) + " has complete path to player");
                }
            }
        }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ForestZoneValidation.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/World/Capture Forest Overview")]
    public static void CaptureOverview()
    {
        GameObject root = GameObject.Find(RootName); if (root == null) return;
        GameObject go = new GameObject("Forest preview camera (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture rt = null; Texture2D image = null;
        try
        {
            Camera camera = go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
            var urp = camera.GetUniversalAdditionalCameraData(); urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera.clearFlags = CameraClearFlags.Skybox; camera.fieldOfView = 55;
            Vector3 center = root.transform.position;
            camera.transform.position = center + new Vector3(27, 23, -31); camera.transform.LookAt(center + Vector3.up * 1.5f);
            rt = new RenderTexture(1280, 800, 24); camera.targetTexture = rt;
            image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            for (int i = 0; i < 2; i++)
            {
                if (i == 1) { camera.fieldOfView = 68; camera.transform.position = center + new Vector3(-7, 1.7f, -8); camera.transform.LookAt(center + new Vector3(1, 0.6f, 12)); }
                camera.Render();
                RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); RenderTexture.active = previous;
                Directory.CreateDirectory("Logs"); File.WriteAllBytes(i == 0 ? "Logs/ForestDemo_Overview.png" : "Logs/ForestDemo_CloseUp.png", image.EncodeToPNG());
            }
        }
        finally { if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } if (image != null) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/World/Inspect Forest Zone")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine(DateTime.Now.ToString("s"));
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<Terrain>())
        {
            TerrainData data = terrain.terrainData;
            report.AppendLine($"TERRAIN {terrain.name}: position={terrain.transform.position}, layer={terrain.gameObject.layer}, data={AssetDatabase.GetAssetPath(data)}, size={data.size}, heights={data.heightmapResolution}, alpha={data.alphamapResolution}, details={data.detailResolution}, trees={data.treeInstanceCount}, material={terrain.materialTemplate?.name}, shader={terrain.materialTemplate?.shader?.name}");
            foreach (TerrainLayer layer in data.terrainLayers)
                report.AppendLine($" LAYER {AssetDatabase.GetAssetPath(layer)} diffuse={layer?.diffuseTexture?.name} normal={layer?.normalMapTexture?.name} mask={layer?.maskMapTexture?.name} remapMin={layer?.maskMapRemapMin} remapMax={layer?.maskMapRemapMax}");
            report.AppendLine("Detail mode: " + data.detailScatterMode + ", drawn=" + terrain.drawTreesAndFoliage);
            foreach (DetailPrototype prototype in data.detailPrototypes) report.AppendLine($" DETAIL {prototype.prototype?.name}: instancing={prototype.useInstancing}, density={prototype.density}, scale={prototype.minWidth}-{prototype.maxWidth}, height={prototype.minHeight}-{prototype.maxHeight}");
            for (int i = 0; i < data.detailPrototypes.Length; i++) { var values = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i); long count = 0; foreach (int value in values) count += value; report.AppendLine(" DETAIL COUNT " + i + "=" + count); }
        }
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerMovement>())
        {
            Vector3 p = player.transform.position;
            report.AppendLine($"PLAYER {p}, layer={player.gameObject.layer}");
            foreach (Terrain t in Terrain.activeTerrains)
            {
                report.AppendLine($" Ground under player={t.SampleHeight(p) + t.transform.position.y:F3}");
                for (int z = -20; z <= 20; z += 10)
                {
                    for (int x = -20; x <= 20; x += 10) report.Append($"{t.SampleHeight(p + new Vector3(x,0,z)):F1} ");
                    report.AppendLine();
                }
            }
        }
        report.AppendLine($"Lighting: ambientMode={RenderSettings.ambientMode} intensity={RenderSettings.ambientIntensity} sky={RenderSettings.ambientSkyColor} equator={RenderSettings.ambientEquatorColor} ground={RenderSettings.ambientGroundColor}, skybox={AssetDatabase.GetAssetPath(RenderSettings.skybox)}, fog={RenderSettings.fog}");
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>())
            report.AppendLine($"LIGHT {light.name}: {light.type} intensity={light.intensity} color={light.color} rotation={light.transform.eulerAngles} shadows={light.shadows}");
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>())
            report.AppendLine($"VOLUME {volume.name}: profile={AssetDatabase.GetAssetPath(volume.sharedProfile)} global={volume.isGlobal}");
        foreach (NavMeshSurface surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>())
            report.AppendLine($"NAV {surface.name}: collect={surface.collectObjects} geometry={surface.useGeometry} mask={surface.layerMask.value} center={surface.center} size={surface.size} data={AssetDatabase.GetAssetPath(surface.navMeshData)}");
        foreach (Transform t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            if (t.name.Contains("Spawn") || t.name.Contains("Club") || t.name.Contains("Stand")) report.AppendLine($"OBJECT {t.name} {t.position}");
        foreach (string path in AssetDatabase.FindAssets("t:GameObject", new[] { Pack }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab")))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            report.AppendLine($"PREFAB {path} bounds={bounds} colliders={prefab.GetComponentsInChildren<Collider>(true).Length}");
            foreach (Material material in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                report.AppendLine($" MAT {material.name}: {material.shader?.name} supported={material.shader?.isSupported} shaderError={ShaderUtil.ShaderHasError(material.shader)}");
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ForestZoneInspection.txt", report.ToString());
        Debug.Log("Forest inspection: Logs/ForestZoneInspection.txt");
    }

    [MenuItem("Tools/World/Import Fantasy Worlds URP")]
    public static void ImportPack()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/P_fwOF_Tree_M_2.prefab") != null)
        { Debug.Log("Fantasy Worlds URP is already imported; original assets were not overwritten."); return; }
        BackupScene("BeforeForestImport");
        AssetDatabase.ImportPackage("Assets/TriForge Assets/_URP Content - Fantasy Worlds - Old Forest DEMO.unitypackage", false);
    }

    private static void BackupScene(string label)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype before setting up this zone.");
        Directory.CreateDirectory("Logs/SceneBackups");
        string path = $"Logs/SceneBackups/{scene.name}_{label}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.unity";
        if (!EditorSceneManager.SaveScene(scene, path, true)) throw new IOException("Scene backup failed");
        Debug.Log("Forest scene backup: " + path);
    }
}
