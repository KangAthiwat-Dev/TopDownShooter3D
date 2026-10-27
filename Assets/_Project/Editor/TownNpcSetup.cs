#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class TownNpcSetup
{
    public const string Folder = "Assets/_Project/NPCs/Townsfolk";
    public const string RootName = "Town_NPC_Population";
    public const string Ual = "Assets/_Project/Art/ThirdParty/Quaternius/UAL1/UAL1_Standard.fbx";
    public static readonly Vector3[] CitySites = {
        new Vector3(75.76f,5.14f,-73), new Vector3(77.7f,5.14f,-73), new Vector3(73.8f,5.14f,-72.5f),
        new Vector3(75.76f,5.14f,-58), new Vector3(75.76f,5.14f,-65), new Vector3(75.76f,5.14f,-80),
        new Vector3(75.76f,5.14f,-86), new Vector3(60,5.14f,-70.88f), new Vector3(67,5.14f,-70.88f),
        new Vector3(84,5.14f,-70.88f), new Vector3(91,5.14f,-70.88f), new Vector3(70.7f,5.14f,-86.8f),
        new Vector3(82,5.14f,-82.88f), new Vector3(87,5.14f,-82.88f), new Vector3(83,5.14f,-87.3f),
        new Vector3(71.1f,5.14f,-78), new Vector3(78.5f,5.14f,-55), new Vector3(71,5.14f,-60),
        new Vector3(62,5.14f,-73.5f), new Vector3(80,5.14f,-74.5f), new Vector3(87,5.14f,-74.5f)
    };
    public static readonly Vector3[] OutsideSites = {
        new Vector3(75.76f,5.14f,-46), new Vector3(71,5.14f,-40), new Vector3(50,5.14f,-70.88f),
        new Vector3(44,5.14f,-68), new Vector3(75.76f,5.14f,-35)
    };
    [MenuItem("Tools/NPC/Townsfolk/Inspect Animation and Routes")]
    public static void Inspect()
    {
        var report = new StringBuilder("Town NPC source / navigation audit " + DateTime.Now.ToString("s") + "\n");
        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Ual).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__") && new[] { "Idle", "Interact", "Walk", "Sprint", "Death" }.Any(s => c.name.Contains(s))))
            report.AppendLine(clip.name + " human=" + clip.isHumanMotion + " length=" + clip.length + " loop=" + clip.isLooping);
        var path = new NavMeshPath();
        foreach (var p in CitySites.Concat(OutsideSites))
        {
            bool sampled = NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas);
            bool connected = sampled && NavMesh.CalculatePath(CitySites[0], hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            report.AppendLine(p + " sampled=" + sampled + " nav=" + hit.position + " connected=" + connected + " corners=" + path.corners.Length);
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TownNpcInspection.txt", report.ToString()); Debug.Log("Town NPC source / route audit saved.");
    }
}
#endif
