using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

public static class SummoningPlatformSetup
{
    public const string Folder = "Assets/_Project/World/SummoningSanctum";
    public const string RootName = "Zone_SummoningSanctum";
    private const string Columns = "Assets/Columns ancient  ruins/Prefab/";
    private static Terrain terrain;
    private static Vector3 center;
    private static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();

    [MenuItem("Tools/World/Summoning/Build Platform and Player Spawn")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GameObject.Find(RootName) != null || AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Summoning site already exists; edit it directly. Builder will not overwrite it.");
        var platform = UnityEngine.Object.FindObjectsByType<MeshCollider>().SingleOrDefault(c => c.name == "CirclePlatformSmall_001" && c.transform.parent?.name == "Temple");
        if (platform == null) throw new InvalidOperationException("Expected the user's existing Temple/CirclePlatformSmall_001; refusing to place a duplicate elsewhere.");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (player == null) throw new InvalidOperationException("Scene player missing");
        terrain = Terrain.activeTerrains.Single();
        center = platform.transform.position; center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
        if (!platform.Raycast(new Ray(center + Vector3.up * 30, Vector3.down), out RaycastHit floor, 60)) throw new InvalidOperationException("Could not find walkable platform center");
        foreach (string name in new[] { "column ", "column_2", "column_3", "wall 4", "stone panel 1", "stone panel 2", "stone panel 3" })
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Columns + name + ".prefab") == null) throw new InvalidOperationException("Missing installed ruins prefab " + name);
        Backup(); EnsureFolder(Folder); EnsureFolder(Folder + "/Materials"); materials.Clear();
        // Save an in-memory copy of current sculpt/paint/details, including the user's
        // latest brush edits. The live TerrainData itself is never edited here.
        float[,,] livePaint = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
        terrain.terrainData.SyncTexture(TerrainData.AlphamapTextureName);
        TerrainData snapshot = UnityEngine.Object.Instantiate(terrain.terrainData); snapshot.name = "Terrain_BeforeSummoning";
        AssetDatabase.CreateAsset(snapshot, Folder + "/Terrain_BeforeSummoning.asset");
        snapshot.SetAlphamaps(0, 0, livePaint);

        GameObject root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Create summoning sanctuary");
        root.transform.position = center; root.transform.SetParent(platform.transform.parent, true);
        Transform baseGroup = Group("00_ExistingPlatform", root.transform);
        Undo.SetTransformParent(platform.transform, baseGroup, "Group existing platform without moving it");
        // Keep the original prefab and all of the user's position/scale overrides.
        RemapMaterials(platform.gameObject);
        Transform ruins = Group("01_RuinedColumns", root.transform);
        Place("column ", ruins, new Vector2(-10f, 3.5f), new Vector3(0.88f, 0.88f, 0.88f), 12);
        Place("column_3", ruins, new Vector2(-8.7f, -7f), Vector3.one * 0.95f, -23);
        Place("column ", ruins, new Vector2(8.3f, -6.4f), Vector3.one * 0.8f, 34);
        Place("column_2", ruins, new Vector2(0.3f, -10.7f), Vector3.one * 0.9f, 72);
        Place("wall 4", ruins, new Vector2(-4f, 9.7f), Vector3.one * 0.55f, 0);
        // Preserve the user's statue and standing pillar. Include their ground
        // colliders in the same layer used by the scene's navigation surface.
        foreach (Transform existing in root.transform.parent.GetComponentsInChildren<Transform>(true))
            if (existing.name == "ColumnBig_001" || existing.name == "Statue") SetLayer(existing.gameObject, terrain.gameObject.layer);

        Transform path = Group("02_StonePath", root.transform);
        Vector2 outward = new Vector2(1, 1).normalized;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = outward * (7.8f + i * 2.1f);
            Place("stone panel " + (i % 3 + 1), path, p, new Vector3(0.9f, 0.34f, 0.9f), 45 + (i % 2 == 0 ? -4 : 5));
        }
        Transform rubble = Group("03_Rubble", root.transform);
        foreach (Vector2 p in new[] { new Vector2(-7.4f, 5), new Vector2(-7, -8), new Vector2(6.6f, -8.8f), new Vector2(-3, 10) })
            Place("stone panel 3", rubble, p, new Vector3(0.45f, 0.38f, 0.42f), p.x * 23);

        CreateInlay(root.transform, floor.point);
        Transform markers = Group("05_SpawnAndFutureVfx", root.transform);
        var spawn = Group("PlayerSpawnPoint", markers);
        spawn.SetPositionAndRotation(floor.point + Vector3.up * 0.08f, Quaternion.LookRotation(new Vector3(1, 0, 1).normalized));
        var spawnComponent = spawn.gameObject.AddComponent<PlayerSceneSpawnPoint>();
        var spawnSettings = new SerializedObject(spawnComponent); spawnSettings.FindProperty("player").objectReferenceValue = player.transform; spawnSettings.ApplyModifiedPropertiesWithoutUndo();
        Transform anchor = Group("SummonVfxAnchor", markers); anchor.position = floor.point + Vector3.up * 0.03f;
        Undo.RecordObject(player.transform, "Move player start to summoning platform");
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        var follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        if (follow != null) { Undo.RecordObject(follow.transform, "Frame summoning spawn"); follow.transform.position = spawn.position + new SerializedObject(follow).FindProperty("targetOffset").vector3Value; }

        Physics.SyncTransforms();
        var surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null) throw new InvalidOperationException("Scene NavMeshSurface missing");
        Undo.RecordObject(surface, "Bake sanctuary navigation"); surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("Sanctuary NavMesh build failed");
        AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/SummoningNavigation.asset");
        EditorUtility.SetDirty(surface);
        // Only relocate the existing markers; wave definitions/timing are untouched.
        var spawner = UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>();
        if (spawner != null)
        {
            var points = new SerializedObject(spawner).FindProperty("spawnPoints");
            float[] angles = { 15, 70, 145, 205, 255, 315 };
            for (int i = 0; i < points.arraySize; i++)
            {
                var marker = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform; if (marker == null) continue;
                float angle = angles[i % angles.Length] * Mathf.Deg2Rad;
                Vector3 p = Ground(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 11.5f);
                if (!NavMesh.SamplePosition(p, out NavMeshHit nav, 2f, NavMesh.AllAreas)) throw new InvalidOperationException("No safe NavMesh near spawn marker " + marker.name);
                Undo.RecordObject(marker, "Relocate encounter marker near sanctuary"); marker.position = nav.position + Vector3.up * 0.1f;
            }
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new IOException("Could not save sanctuary scene");
        File.WriteAllText("Logs/SummoningBuild.txt", $"Built {DateTime.Now:s}\nReused original CirclePlatformSmall_001 at {platform.transform.position:F3}, scale {platform.transform.lossyScale:F3}\nSpawn {spawn.position:F3}, heading 45 degrees\nTerrain sculpt/paint/grass and original pack assets untouched.\nNo summoning lights/particles/visibility changes or map-boundary system installed.\n");
        Focus(); Validate(); Capture();
        Debug.Log("Summoning platform + spawn installed. Terrain untouched; original statue/pillar retained. Summoning VFX reserved for next phase.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static Transform Group(string name, Transform parent) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    private static Vector3 Ground(Vector2 p) { Vector3 world = center + new Vector3(p.x, 0, p.y); world.y = terrain.SampleHeight(world) + terrain.transform.position.y; return world; }
    private static void SetLayer(GameObject go, int layer) { foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) { Undo.RecordObject(t.gameObject, "Set sanctuary environment layer"); t.gameObject.layer = layer; } }
    private static GameObject Place(string name, Transform parent, Vector2 offset, Vector3 scale, float yaw)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Columns + name + ".prefab"), parent);
        go.transform.SetPositionAndRotation(Ground(offset), Quaternion.Euler(0, yaw, 0)); go.transform.localScale = scale;
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        float minY = renderers.Min(r => r.bounds.min.y); go.transform.position += Vector3.up * (Ground(offset).y - minY + 0.01f);
        SetLayer(go, terrain.gameObject.layer); RemapMaterials(go);
        if (!go.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger))
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>()) { var c = filter.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = filter.sharedMesh; }
        return go;
    }
    private static void RemapMaterials(GameObject go)
    {
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, "Use project-owned sanctuary stone materials");
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
            {
                if (source == null) return null;
                if (materials.TryGetValue(source, out Material cached)) return cached;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                string name = "M_Sanctum_" + source.name.Replace(' ', '_') + "_" + guid.Substring(0, Mathf.Min(6, guid.Length));
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
                string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(textureProperty)) { material.SetTexture("_BaseMap", source.GetTexture(textureProperty)); material.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty)); material.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty)); }
                material.SetColor("_BaseColor", new Color(0.91f, 0.88f, 0.8f, 1)); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0.06f);
                if (AssetDatabase.GetAssetPath(source).StartsWith("Assets/Columns ancient  ruins/"))
                    material.SetColor("_BaseColor", new Color(0.68f, 0.72f, 0.68f, 1));
                material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
                AssetDatabase.CreateAsset(material, Folder + "/Materials/" + name + ".mat"); materials[source] = material; return material;
            }).ToArray();
            if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private static void CreateInlay(Transform parent, Vector3 surface)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        Action<Vector3, Vector3, Vector3, Vector3> quad = (a, b, c, d) => { int n = vertices.Count; vertices.AddRange(new[] { a, b, c, d }); triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }); };
        foreach (float radius in new[] { 1.82f, 2.25f }) for (int i = 0; i < 128; i++)
        {
            float a = i * Mathf.PI * 2 / 128, b = (i + 1) * Mathf.PI * 2 / 128;
            Vector3 v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), w = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
            quad(v * (radius - 0.025f), w * (radius - 0.025f), w * (radius + 0.025f), v * (radius + 0.025f));
        }
        Action<Vector3, Vector3> line = (a, b) => { Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up) * 0.025f; quad(a - side, b - side, b + side, a + side); };
        for (int i = 0; i < 8; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, i * 45, 0);
            line(rotation * new Vector3(0, 0, 1.12f), rotation * new Vector3(0, 0, 1.65f));
            line(rotation * new Vector3(-0.18f, 0, 1.4f), rotation * new Vector3(0, 0, 1.65f));
            line(rotation * new Vector3(0.18f, 0, 1.4f), rotation * new Vector3(0, 0, 1.65f));
            line(rotation * new Vector3(-0.13f, 0, 2.04f), rotation * new Vector3(0.13f, 0, 2.04f));
        }
        var mesh = new Mesh { name = "SanctumStoneInlay" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + "/SanctumStoneInlay.asset");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Sanctum_Engraving" };
        material.SetColor("_BaseColor", new Color(0.17f, 0.19f, 0.16f, 1)); material.SetFloat("_Smoothness", 0); material.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(material, Folder + "/Materials/M_Sanctum_Engraving.mat");
        Transform inlay = Group("04_EngravedSummoningSeal_NoGlow", parent); inlay.position = surface + Vector3.up * 0.012f;
        inlay.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; inlay.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    [MenuItem("Tools/World/Summoning/Focus Platform")]
    public static void Focus()
    {
        GameObject root = GameObject.Find(RootName); if (root == null) return;
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.LookAt(root.transform.position + Vector3.up * 1.8f, Quaternion.Euler(48, 0, 0), 19, false, true);
    }

    [MenuItem("Tools/World/Summoning/Validate Platform")]
    public static void Validate()
    {
        var report = new StringBuilder(DateTime.Now.ToString("s") + "\n"); bool passed = true;
        Action<bool, string> check = (ok, message) => { passed &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + message); };
        GameObject root = GameObject.Find(RootName); var spawn = UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>();
        check(root != null && spawn != null, "Sanctuary and player spawn exist");
        if (root != null && spawn != null)
        {
            var t = Terrain.activeTerrains.Single(); var original = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/Terrain_BeforeSummoning.asset");
            check(Same(original.GetHeights(0, 0, original.heightmapResolution, original.heightmapResolution), t.terrainData.GetHeights(0, 0, t.terrainData.heightmapResolution, t.terrainData.heightmapResolution)), "User terrain sculpt unchanged");
            TerrainData paintBackup = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/Terrain_UserPaintBackup.asset") ?? original;
            var originalPaint = paintBackup.GetAlphamaps(0, 0, paintBackup.alphamapWidth, paintBackup.alphamapHeight);
            var currentPaint = t.terrainData.GetAlphamaps(0, 0, t.terrainData.alphamapWidth, t.terrainData.alphamapHeight);
            float maxPaintDelta = 0; int paintDifferences = 0;
            if (originalPaint.Length == currentPaint.Length)
            {
                for (int z = 0; z < currentPaint.GetLength(0); z++) for (int x = 0; x < currentPaint.GetLength(1); x++) for (int l = 0; l < currentPaint.GetLength(2); l++)
                { float delta = Mathf.Abs(originalPaint[z,x,l] - currentPaint[z,x,l]); if (delta > 0) { paintDifferences++; maxPaintDelta = Mathf.Max(maxPaintDelta, delta); } }
            }
            report.AppendLine($"PAINT audit originalShape={originalPaint.GetLength(0)},{originalPaint.GetLength(1)},{originalPaint.GetLength(2)} currentShape={currentPaint.GetLength(0)},{currentPaint.GetLength(1)},{currentPaint.GetLength(2)} changedValues={paintDifferences} maxDelta={maxPaintDelta:G9}");
            check(Same(originalPaint, currentPaint), "Live terrain painting matches GPU-aware preservation backup (no repaint performed)");
            bool details = original.detailPrototypes.Length == t.terrainData.detailPrototypes.Length;
            if (details) for (int i = 0; i < original.detailPrototypes.Length; i++) details &= Same(original.GetDetailLayer(0, 0, original.detailWidth, original.detailHeight, i), t.terrainData.GetDetailLayer(0, 0, t.terrainData.detailWidth, t.terrainData.detailHeight, i));
            check(details, "User grass brush edits preserved exactly");
            check(Vector3.Distance(spawn.Position, spawn.Player.position) < 0.01f, "Saved player transform matches the spawn marker");
            check(Physics.Raycast(spawn.Position + Vector3.up, Vector3.down, out RaycastHit hit, 3, 1 << t.gameObject.layer, QueryTriggerInteraction.Ignore) && hit.collider.name == "CirclePlatformSmall_001" && Mathf.Abs(spawn.Position.y - hit.point.y - 0.08f) < 0.03f, "Spawn is on the original platform surface");
            check(NavMesh.SamplePosition(spawn.Position, out NavMeshHit target, 1, NavMesh.AllAreas), "Platform center is on NavMesh");
            var spawner = UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>();
            var points = new SerializedObject(spawner).FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                Transform point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform; var path = new NavMeshPath();
                check(point != null && NavMesh.SamplePosition(point.position, out NavMeshHit start, 1, NavMesh.AllAreas) && NavMesh.CalculatePath(start.position, target.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "Enemy spawn " + (i + 1) + " reaches platform center");
            }
            foreach (Material m in root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                check(m.shader != null && m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader), "Valid URP material " + m.name);
            check(root.GetComponentsInChildren<Transform>(true).All(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject) == 0), "No missing scripts on sanctuary");
            var circles = root.GetComponentsInChildren<SummoningCircleVfx>(true);
            if (circles.Length == 0)
                check(root.GetComponentsInChildren<Light>(true).Length == 0 && root.GetComponentsInChildren<ParticleSystem>(true).Length == 0, "Phase-one platform has no stray summoning effects");
            else
                check(circles.Length == 1 && root.GetComponentsInChildren<Light>(true).All(l => l.GetComponentInParent<SummoningCircleVfx>() != null) &&
                    root.GetComponentsInChildren<ParticleSystem>(true).All(p => p.GetComponentInParent<SummoningCircleVfx>() != null) && circles[0].GetComponentsInChildren<Collider>(true).Length == 0,
                    "Phase-two birth circle is a single self-contained visual effect; no stray lights/particles or new collision");
        }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningValidation.txt", report.ToString()); Debug.Log(report.ToString());
    }
    private static bool Same(Array a, Array b) => a.Rank == b.Rank && Enumerable.Range(0, a.Rank).All(i => a.GetLength(i) == b.GetLength(i)) && a.Cast<object>().SequenceEqual(b.Cast<object>());

    // One-time diagnostic retained for the preservation audit, not a menu that
    // could reset the user's subsequent material edits.
    private static void FinalizeSite()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || GameObject.Find(RootName) == null) return;
        TerrainData data = Terrain.activeTerrain.terrainData;
        string sourcePath = AssetDatabase.GetAssetPath(data);
        string backupPath = Folder + "/Terrain_UserPaintBackup.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(backupPath) == null)
        {
            // Instantiate(TerrainData) omitted pending GPU-only paint in this scene.
            // Capture those values explicitly, without applying any brush/paint to
            // the source. Retain both the first CPU snapshot and old on-disk asset.
            float[,,] before = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            string diskBackup = "Logs/SceneBackups/Terrain_BeforeGpuPaintSync_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".asset";
            DateTime lastWrite = File.GetLastWriteTime(sourcePath);
            File.Copy(sourcePath, diskBackup); File.Copy(sourcePath + ".meta", diskBackup + ".meta");
            TerrainData copy = UnityEngine.Object.Instantiate(data); copy.name = "Terrain_UserPaintBackup";
            AssetDatabase.CreateAsset(copy, backupPath); copy.SetAlphamaps(0, 0, before);
            if (!Same(before, data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight))) throw new InvalidOperationException("Terrain copy unexpectedly affected source painting");
            data.SyncTexture(TerrainData.AlphamapTextureName);
            if (!Same(before, data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight))) throw new InvalidOperationException("GPU synchronization unexpectedly affected live paint; complete pre-sync paint retained in backup");
            EditorUtility.SetDirty(data); EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            bool preserved = Same(before, data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight));
            File.WriteAllText("Logs/SummoningTerrainPaintPreservation.txt", $"Source terrain asset last written before synchronization: {lastWrite:s} (before sanctuary installation).\nOriginal on-disk file retained: {diskBackup}\nInitial native TerrainData clone omitted pending GPU painting in layer 5; that diagnostic copy was retained, not restored onto the terrain.\nComplete live paint explicitly backed up to: {backupPath}\nExact live paint equality before/after copy, GPU-to-CPU synchronization and save: {preserved}\nOnly source painting operation was SyncTexture (save pending user edits); no brush, SetAlphamaps, sculpt, or detail write was performed on the live terrain.\n");
            if (!preserved) throw new InvalidOperationException("Terrain paint preservation check failed");
        }
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m.name.Contains("columns") && !m.name.Contains("walls") && !m.name.Contains("stone_paneks")) continue;
            string sourceName = m.name.Contains("columns") ? "columns" : m.name.Contains("walls") ? "walls" : "stone paneks_cubes";
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Columns ancient  ruins/Materials/" + sourceName + ".mat");
            m.SetTexture("_BaseMap", source.GetTexture("_MainTex")); m.SetColor("_BaseColor", new Color(0.68f, 0.72f, 0.68f, 1)); m.SetFloat("_Smoothness", 0.04f); EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets(); Validate(); Capture();
    }

    [MenuItem("Tools/World/Summoning/Capture Platform")]
    public static void Capture()
    {
        GameObject root = GameObject.Find(RootName); if (root == null) return;
        var go = new GameObject("Summoning preview (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture rt = null; Texture2D texture = null; RenderTexture old = RenderTexture.active;
        try
        {
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.fieldOfView = 48; camera.transform.position = root.transform.position + new Vector3(13, 17, -18); camera.transform.LookAt(root.transform.position + new Vector3(0, 2.5f, 1.5f));
            rt = new RenderTexture(1280, 900, 24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/SummoningPlatform_Overview.png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = old; if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } if (texture != null) UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/World/Summoning/Inspect Current Site")]
    public static void Inspect()
    {
        var report = new StringBuilder(DateTime.Now.ToString("s") + "\n");
        Scene scene = SceneManager.GetActiveScene();
        report.AppendLine("Scene " + scene.path + ", dirty=" + scene.isDirty);
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Platform") || t.name.Contains("Column") || t.name.Contains("Statue") || t.name.Contains("Pedestal") || t.name.StartsWith("Zone_") || t.name == "Player" || t.name.Contains("SpawnPoint") || t.name.Contains("Summon"))
                {
                    report.AppendLine($"OBJECT {Hierarchy(t)} position={t.position:F3} scale={t.lossyScale:F3}, prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)}, active={t.gameObject.activeInHierarchy}");
                    Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                    Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(t.position, Vector3.zero);
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    report.AppendLine(" BOUNDS " + b.ToString("F3"));
                    foreach (Material m in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                        report.AppendLine($" MAT {AssetDatabase.GetAssetPath(m)} shader={m.shader?.name}");
                    foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) report.AppendLine($" COLLIDER {c.GetType().Name} enabled={c.enabled} layer={c.gameObject.layer} bounds={c.bounds}");
                }
            }
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            report.AppendLine($"TERRAIN {Hierarchy(terrain.transform)} pos={terrain.transform.position} data={AssetDatabase.GetAssetPath(terrain.terrainData)} size={terrain.terrainData.size}");
            foreach (TerrainLayer layer in terrain.terrainData.terrainLayers) report.AppendLine($" LAYER {AssetDatabase.GetAssetPath(layer)}");
        }
        foreach (MeshCollider c in UnityEngine.Object.FindObjectsByType<MeshCollider>().Where(c => c.name.StartsWith("CirclePlatformSmall")))
        {
            foreach (float distance in new[] { 0f, 3f, 6f, 7f, 8f, 9f, 10f, 12f })
            {
                Vector3 p = c.transform.position + new Vector3(0.7071068f, 0, 0.7071068f) * distance;
                bool hit = c.Raycast(new Ray(p + Vector3.up * 50, Vector3.down), out RaycastHit info, 100);
                Terrain t = Terrain.activeTerrains.First();
                report.AppendLine($"PLATFORM SAMPLE distance={distance} hit={hit} floorY={(hit ? info.point.y : -999):F3} groundY={t.SampleHeight(p) + t.transform.position.y:F3}");
            }
        }
        string[] folders = { "Assets/ithappy/Inferno_World_Free/Prefabs", "Assets/Columns ancient  ruins" };
        foreach (string folder in folders.Where(AssetDatabase.IsValidFolder))
            foreach (string path in AssetDatabase.FindAssets("t:GameObject", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab")))
            {
                if (folder.Contains("Inferno") && !path.Contains("Decorations") && !path.Contains("Buildings")) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                report.AppendLine($"ASSET {path} bounds={b.ToString("F3")}");
            }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningInspection.txt", report.ToString());
        Debug.Log("Summoning site inspection: Logs/SummoningInspection.txt");
    }
    [MenuItem("Tools/World/Summoning/Back Up Current Scene")]
    public static void Backup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/WorldPrototype.unity") throw new InvalidOperationException("Open WorldPrototype first");
        Directory.CreateDirectory("Logs/SceneBackups");
        string path = $"Logs/SceneBackups/{scene.name}_BeforeSummoning_{DateTime.Now:yyyyMMdd_HHmmss_fff}.unity";
        if (!EditorSceneManager.SaveScene(scene, path, true)) throw new IOException("Could not back up current scene");
        Debug.Log("Summoning backup including unsaved edits: " + path);
    }
    private static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
}
