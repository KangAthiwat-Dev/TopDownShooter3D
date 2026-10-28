#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DemonBarrierSetup
{
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<MeshRenderer>(true);
        if(renderers.Length==0)return new Bounds(go.transform.position,Vector3.zero);
        var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    [MenuItem("Tools/Demon Barriers/Inspect User Walls Gate and Stalagmites")]
    public static void Inspect()
    {
        var report=new StringBuilder();var scene=SceneManager.GetActiveScene();
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(var t in all.Where(t=>t.name=="Zone_Demon"||t.name=="sample_zone"||t.name=="Lord_zone"||t.name=="King_zone"||t.GetComponent<DemonArenaEncounter>()!=null))
            report.AppendLine("AREA "+PathOf(t)+" pos="+t.position+" rot="+t.eulerAngles+" bounds="+BoundsOf(t.gameObject));
        foreach(var t in all.Where(t=>t.parent!=null&&t.parent.name=="Zone_Demon"))
            report.AppendLine("CHILD "+PathOf(t)+" pos="+t.position+" bounds="+BoundsOf(t.gameObject));
        var candidates=all.Where(t=>
        {
            var p=t.position;return p.x>-10&&p.x<110&&p.z>5&&p.z<110&&
                (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)||t.parent==null||t.name.IndexOf("gate",StringComparison.OrdinalIgnoreCase)>=0);
        }).OrderBy(t=>t.position.z).ThenBy(t=>t.position.x);
        foreach(var t in candidates)
        {
            string asset=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if(t.parent!=null&&!new[]{"castle","wall","gate","door","stalag","rock","spike","stone","post"}.Any(s=>(asset+" "+t.name).IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0))continue;
            report.AppendLine("OBJECT "+PathOf(t)+" pos="+t.position+" scale="+t.lossyScale+" rot="+t.eulerAngles+" active="+t.gameObject.activeInHierarchy+" bounds="+BoundsOf(t.gameObject)+" asset="+asset);
            foreach(var c in t.GetComponentsInChildren<Collider>(true))report.AppendLine("  COLLIDER "+PathOf(c.transform)+" "+c.GetType().Name+" enabled="+c.enabled+" trigger="+c.isTrigger+" bounds="+c.bounds);
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DemonBarrierInspection.txt",report.ToString());Debug.Log("User Demon barriers inspected.");
    }
}
#endif
