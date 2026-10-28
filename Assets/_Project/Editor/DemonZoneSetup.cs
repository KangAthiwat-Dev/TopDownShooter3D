#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static partial class DemonZoneSetup
{
    [MenuItem("Tools/Demon Zone/Inspect User Areas and Navigation")]
    public static void Inspect()
    {
        var report=new StringBuilder();var scene=SceneManager.GetActiveScene();
        foreach(var go in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>new[]{"sample_zone","King_zone","Zone_Demon","Lava","Lava_fall","DemonLord","DragonNightmare","KingOfDemon"}.Contains(t.name)))
        {
            report.AppendLine($"{go.name} world={go.position:F3} scale={go.lossyScale:F3} rotation={go.eulerAngles:F1}");
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>().Take(3))report.AppendLine(" render "+r.name+" bounds="+r.bounds);
            foreach(var c in go.GetComponents<Collider>())report.AppendLine(" collider "+c.GetType().Name+" bounds="+c.bounds);
        }
        foreach(var area in UnityEngine.Object.FindObjectsByType<BossArenaZone>(FindObjectsSortMode.None))report.AppendLine("ARENA "+area.name+" pos="+area.transform.position+" bounds="+area.GetComponent<BoxCollider>().bounds);
        foreach(var actor in UnityEngine.Object.FindObjectsByType<EncounterCombatant>(FindObjectsSortMode.None))report.AppendLine("ACTOR "+actor.name+" pos="+actor.transform.position+" arena="+actor.Arena?.name);
        report.AppendLine("NAV samples (x,z: y)");
        for(float z=20;z<=100;z+=5)for(float x=0;x<=100;x+=5)if(NavMesh.SamplePosition(new Vector3(x,12,z),out var nav,20,NavMesh.AllAreas)&&Mathf.Abs(nav.position.x-x)<1&&Mathf.Abs(nav.position.z-z)<1)report.AppendLine($"{x},{z}: {nav.position.y:F2}");
        File.WriteAllText("Logs/DemonZoneInspection.txt",report.ToString());Debug.Log("Demon area layout inspected.");
    }
}
#endif
