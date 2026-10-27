#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CityAssemblySetup
{
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    [MenuItem("Tools/World/City Assembly/Inspect City Groups and Ground")]
    public static void Inspect()
    {
        var report = new StringBuilder("City hierarchy and ground inspection\n");
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            report.AppendLine("ROOT " + root.name + " active=" + root.activeSelf + " pos=" + root.transform.position.ToString("F3") + " bounds=" + BoundsOf(root));
        var map = UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        report.AppendLine("CITY LANDMARK " + PathOf(map.city));
        foreach (Transform child in map.city)
            report.AppendLine("LANDMARK CHILD " + PathOf(child) + " active=" + child.gameObject.activeSelf + " bounds=" + BoundsOf(child.gameObject));
        var terrain = map.terrain;
        report.AppendLine("TERRAIN " + terrain.transform.position + " data=" + AssetDatabase.GetAssetPath(terrain.terrainData));
        for (float z = -94.7745f; z <= -50.7f; z += 11)
            for (float x = 54.7626f; x <= 96.8f; x += 10.5f)
                report.AppendLine("HEIGHT " + x.ToString("F2") + "," + z.ToString("F2") + "=" + (terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y).ToString("F3"));
        foreach (var music in UnityEngine.Object.FindObjectsByType<TownMusicZone>(FindObjectsSortMode.None))
            report.AppendLine("MUSIC " + PathOf(music.transform) + " " + EditorJsonUtility.ToJson(music));
        foreach (var population in UnityEngine.Object.FindObjectsByType<TownNpcPopulation>(FindObjectsSortMode.None))
            report.AppendLine("POPULATION " + PathOf(population.transform));
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CityAssemblyInspection.txt", report.ToString());
        Debug.Log("City assembly inspection saved.");
    }
}
#endif
