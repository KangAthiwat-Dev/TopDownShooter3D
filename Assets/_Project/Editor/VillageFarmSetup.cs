#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

public static partial class VillageFarmSetup
{
    public const string Folder = "Assets/_Project/World/YellowRoofVillage";
    public const string Kit = MedievalModelOnlySetup.Package;
    public const string RootName = "Village_YellowRoof_Farm";
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    private static void Render(string name, Vector3 target, Vector3 offset, float size)
    {
        var go = new GameObject("Temporary village inspection camera"); var camera = go.AddComponent<Camera>();
        var rt = RenderTexture.GetTemporary(1600, 1200, 24); var previous = RenderTexture.active;
        var texture = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = target + offset; camera.transform.LookAt(target);
            camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .1f; camera.farClipPlane = 600;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
    [MenuItem("Tools/World/Yellow Roof Village/Inspect Site and Available Pieces")]
    public static void Inspect()
    {
        RequireScene(); var report = new StringBuilder("Village site and model inspection " + DateTime.Now.ToString("s") + "\n");
        var map = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>(); var terrain = map.terrain;
        report.AppendLine("Terrain=" + AssetDatabase.GetAssetPath(terrain.terrainData) + " pos=" + terrain.transform.position + " size=" + terrain.terrainData.size);
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) report.AppendLine("ROOT " + PathOf(root.transform) + " bounds=" + BoundsOf(root));
        var zone = GameObject.Find("Zone_Villager");
        if (zone != null) foreach (Transform t in zone.transform)
            report.AppendLine("SITE " + PathOf(t) + " active=" + t.gameObject.activeSelf + " pos=" + t.position.ToString("F2") + " bounds=" + BoundsOf(t.gameObject) + " source=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject));
        string[] pieces = { "Roof/Roof_Single", "RoofS/RoofS_Single", "Roof/Roof_Gable", "Roof/Roof_Mid", "Wall/Wall_Wattle", "Wall/Wall_Wattle_Door", "Corner/Corner_Wattle", "Prop/Prop_Fence", "Prop/Prop_FenceGate", "Prop/Prop_GardenBed", "Crop/Crop_Carrot", "Crop/Crop_Cabbage", "Crop/Crop_Wheat", "Crop/Crop_Pumpkin", "Prop/Prop_ChickenCoop", "Prop/Prop_HayRack", "Prop/Prop_Trough", "Animal/Animal_Sheep", "Animal/Animal_Cow", "Animal/Animal_Chickens" };
        foreach (string piece in pieces)
        {
            var parts = piece.Split('/'); var model = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "/Generated/Prefabs/VK/" + parts[0] + "/SM_VK_" + parts[1] + ".prefab");
            var go = UnityEngine.Object.Instantiate(model); report.AppendLine("ASSET " + piece + " bounds=" + BoundsOf(go) + " mats=" + string.Join(",", go.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())); UnityEngine.Object.DestroyImmediate(go);
        }
        report.AppendLine("HEIGHT GRID z rows=30..110 x columns=-110..-20 step=2");
        for (int z = 30; z <= 110; z += 2)
        { report.Append(z + ":"); for (int x = -110; x <= -20; x += 2) report.Append(" " + (terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y).ToString("F2")); report.AppendLine(); }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/VillageSiteInspection.txt", report.ToString());
        Render("VillageSite_Before_Top", new Vector3(-65, 4, 70), new Vector3(0, 110, 0.001f), 48);
        Render("VillageSite_Before_Angle", new Vector3(-65, 4, 70), new Vector3(45, 68, -78), 43);
        Debug.Log("Village site inspection and views saved.");
    }
}
#endif
