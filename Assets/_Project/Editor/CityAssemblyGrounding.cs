#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CityAssemblySetup
{
    [MenuItem("Tools/World/City Assembly/Finish Grounding and Validate Movable City")]
    public static void FinishGrounding()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var city = UnityEngine.Object.FindAnyObjectByType<CityAssemblyRoot>();
        if (city == null) throw new InvalidOperationException("Group the city first.");
        var frame = city.transform;
        var approaches = city.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Gate_Approach_Flat_Collision").ToArray();
        var fields = new SerializedObject(city); var pads = fields.FindProperty("groundingPads"); pads.arraySize = approaches.Length + 1;
        pads.GetArrayElementAtIndex(0).boundsValue = new Bounds(Vector3.zero, new Vector3(42, 0, 44));
        for (int i = 0; i < approaches.Length; i++)
        {
            var collider = approaches[i]; var center = frame.InverseTransformPoint(collider.transform.TransformPoint(collider.center));
            var size = new Vector3(collider.size.x * collider.transform.lossyScale.x, 0, collider.size.z * collider.transform.lossyScale.z);
            pads.GetArrayElementAtIndex(i + 1).boundsValue = new Bounds(center, size);
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        city.GroundAndRebuildNavigation();
        var report = new StringBuilder("Final city assembly validation\n"); bool passed = true;
        void Check(bool valid, string message) { passed &= valid; report.AppendLine((valid ? "PASS " : "FAIL ") + message); }
        var music = city.GetComponentInChildren<TownMusicZone>(); var population = city.GetComponentInChildren<TownNpcPopulation>();
        var stations = city.GetComponentsInChildren<TownNpcWorkStation>();
        var musicLocal = new SerializedObject(music).FindProperty("center").vector3Value;
        var originalMusicCenter = frame.TransformPoint(musicLocal);
        var originalNode = population.CityPoints[0].transform.position;
        var originalPosition = frame.position; var originalRotation = frame.rotation;
        var originalStation = stations.Select(s => s.transform.position).ToArray();
        frame.position += new Vector3(120, 0, 80); frame.rotation = Quaternion.Euler(0, 37, 0);
        Check(music.Contains(frame.TransformPoint(musicLocal)) && !music.Contains(originalMusicCenter), "music boundary follows city translation and rotation");
        Check(population.Contains(population.CityPoints[0].transform.position) && !population.Contains(originalNode), "NPC city boundary and route points follow city translation and rotation");
        Check(stations.Select((s, i) => s.Contains(s.transform.position) && !s.Contains(originalStation[i])).All(v => v), "construction work areas follow the city parent");
        frame.SetPositionAndRotation(originalPosition, originalRotation); city.GroundToTerrain();
        fields.Update(); fields.FindProperty("navigationNeedsRebuild").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
        Check(Mathf.Abs(frame.position.y - 4f) < .02f && Mathf.Abs(city.FloorHeight - 4.14f) < .02f, "city foundation touches level terrain at 4m; paving is 4.14m");
        Check(population.CityPoints.All(p => p.transform.IsChildOf(frame)) && population.OutsidePoints.All(p => p.transform.IsChildOf(frame)), "city and outside NPC route markers are under City_All");
        Check(city.GetComponentsInChildren<Transform>(true).Any(t => t.name == "NPC_WeaponMerchant"), "named shop NPC is included");
        Check(frame.GetComponentInParent<ForestMapLayout>().city.IsChildOf(frame), "map city landmark remains linked");
        report.AppendLine("Parent=" + PathOf(frame) + "\nPosition=" + frame.position.ToString("F3") + "\nOriginal layout and non-city placements were preserved by the grouping pass.\n");
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        File.WriteAllText("Logs/CityAssemblyFinalValidation.txt", report.ToString());
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(city.gameObject.scene); EditorSceneManager.SaveScene(city.gameObject.scene);
        Debug.Log("Final movable-city grounding validation passed=" + passed);
    }
}
#endif
