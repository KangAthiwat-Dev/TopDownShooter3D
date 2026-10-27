using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
#endif

/// <summary>One movable city, with an editor terrain snap and a supporting stone foundation.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class CityAssemblyRoot : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private Transform foundation;
    [SerializeField] private Vector2 footprintSize = new Vector2(42f, 44f);
    [SerializeField] private float floorLocalHeight = .14f;
    [SerializeField] private bool autoGroundInEditor = true;
    [SerializeField] private bool levelTerrainPad = true;
    [SerializeField] private Bounds[] groundingPads;
    [SerializeField, HideInInspector] private bool navigationNeedsRebuild;
    private Matrix4x4 previousPose;
    public float FloorHeight => transform.TransformPoint(Vector3.up * floorLocalHeight).y;
    public bool NavigationNeedsRebuild => navigationNeedsRebuild;
    private void OnEnable() => previousPose = transform.localToWorldMatrix;

    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || transform.localToWorldMatrix == previousPose) return;
        if (autoGroundInEditor) GroundToTerrain();
        navigationNeedsRebuild = true; previousPose = transform.localToWorldMatrix;
        EditorUtility.SetDirty(this); EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    public bool GroundToTerrain()
    {
        if (terrain == null || terrain.terrainData == null) return false;
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        var terrainPosition = terrain.transform.position; var size = terrain.terrainData.size;
        for (int z = 0; z <= 8; z++) for (int x = 0; x <= 8; x++)
        {
            var sample = transform.TransformPoint(new Vector3((x / 8f - .5f) * footprintSize.x, 0, (z / 8f - .5f) * footprintSize.y));
            if (sample.x < terrainPosition.x || sample.z < terrainPosition.z || sample.x > terrainPosition.x + size.x || sample.z > terrainPosition.z + size.z) continue;
            float height = terrain.SampleHeight(sample) + terrainPosition.y;
            low = Mathf.Min(low, height); high = Mathf.Max(high, height);
        }
        if (!float.IsFinite(high)) return false;
        var position = transform.position; position.y = high; transform.position = position;
        if (foundation != null)
        {
            float bottom = transform.InverseTransformPoint(new Vector3(position.x, low - .1f, position.z)).y;
            float top = floorLocalHeight - .005f;
            foundation.localPosition = new Vector3(0, (bottom + top) * .5f, 0);
            foundation.localScale = new Vector3(footprintSize.x, Mathf.Max(.05f, top - bottom), footprintSize.y);
        }
        Physics.SyncTransforms(); navigationNeedsRebuild = true; previousPose = transform.localToWorldMatrix;
        return true;
    }

    [ContextMenu("Ground Whole City and Rebuild Navigation")]
    public void GroundAndRebuildNavigation()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        Undo.RecordObject(transform, "Ground whole city");
        if (foundation != null) Undo.RecordObject(foundation, "Fit city foundation");
        if (levelTerrainPad) LevelCityPad();
        if (!GroundToTerrain()) { Debug.LogWarning("Place City_All over its terrain before grounding it.", this); return; }
        var surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface != null)
        {
            surface.BuildNavMesh();
            const string folder = "Assets/_Project/World/CityAssembly";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/World", "CityAssembly");
            AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath(folder + "/CityAssemblyNavigation.asset"));
            EditorUtility.SetDirty(surface);
        }
        navigationNeedsRebuild = false; previousPose = transform.localToWorldMatrix;
        EditorUtility.SetDirty(this); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

#if UNITY_EDITOR
    private void LevelCityPad()
    {
        if (terrain == null || terrain.terrainData == null) return;
        const string folder = "Assets/_Project/World/CityAssembly";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/World", "CityAssembly");
        var data = terrain.terrainData;
        if (!AssetDatabase.GetAssetPath(data).StartsWith(folder + "/"))
        {
            var copy = Instantiate(data); copy.name = "MovableCityTerrain";
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/MovableCityTerrain.asset"));
            Undo.RecordObject(terrain, "Use private city grounding terrain"); terrain.terrainData = copy;
            var collider = terrain.GetComponent<TerrainCollider>();
            Undo.RecordObject(collider, "Match city terrain collision"); collider.terrainData = copy;
            data = copy;
        }
        var samples = new System.Collections.Generic.List<float>();
        for (int z = 0; z <= 8; z++) for (int x = 0; x <= 8; x++)
        {
            var p = transform.TransformPoint(new Vector3((x / 8f - .5f) * footprintSize.x, 0, (z / 8f - .5f) * footprintSize.y));
            var tp = terrain.transform.position; var ts = data.size;
            if (p.x >= tp.x && p.z >= tp.z && p.x <= tp.x + ts.x && p.z <= tp.z + ts.z)
                samples.Add(terrain.SampleHeight(p) + tp.y);
        }
        if (samples.Count == 0) return;
        samples.Sort(); float level = samples[samples.Count / 2];
        var pads = groundingPads != null && groundingPads.Length > 0 ? groundingPads :
            new[] { new Bounds(Vector3.zero, new Vector3(footprintSize.x, 0, footprintSize.y)) };
        int n = data.heightmapResolution, changed = 0;
        var eligible = new bool[n, n];
        var heights = data.GetHeights(0, 0, n, n); var original = (float[,])heights.Clone();
        float target = (level - terrain.transform.position.y) / data.size.y;
        Undo.RegisterCompleteObjectUndo(data, "Level only city footprint and gate approaches");
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            var p = new Vector3(terrain.transform.position.x + x / (float)(n - 1) * data.size.x,
                transform.position.y, terrain.transform.position.z + z / (float)(n - 1) * data.size.z);
            var local = transform.InverseTransformPoint(p); float distance = float.PositiveInfinity;
            foreach (var pad in pads)
            {
                float dx = Mathf.Max(Mathf.Abs(local.x - pad.center.x) - pad.extents.x, 0);
                float dz = Mathf.Max(Mathf.Abs(local.z - pad.center.z) - pad.extents.z, 0);
                distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dz * dz));
            }
            if (distance >= 3f) continue;
            eligible[z, x] = true;
            heights[z, x] = Mathf.Lerp(heights[z, x], target, 1f - Mathf.SmoothStep(0, 1, distance / 3f));
            if (Mathf.Abs(heights[z, x] - original[z, x]) > 1e-7f) changed++;
        }
        data.SetHeights(0, 0, heights); terrain.Flush(); EditorUtility.SetDirty(data);
        var result = data.GetHeights(0, 0, n, n); float outsideChange = 0;
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            if (!eligible[z, x]) outsideChange = Mathf.Max(outsideChange, Mathf.Abs(result[z, x] - original[z, x]) * data.size.y);
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/CityGrounding.txt", "City pad level=" + level.ToString("F3") + "m\nChanged height samples=" + changed +
            "\n" + (outsideChange < .0001f ? "PASS" : "FAIL") + " outside city/approach 3m blend height change=" + outsideChange.ToString("F6") +
            "m\nTextures, trees and details retained in a private terrain copy.\nTerrain=" + AssetDatabase.GetAssetPath(data) + "\n");
    }
#endif
}
