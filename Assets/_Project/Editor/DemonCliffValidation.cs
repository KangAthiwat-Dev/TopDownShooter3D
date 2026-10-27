#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static partial class DemonCliffSetup
{
    [MenuItem("Tools/World/Demon Cliffs/Validate Navigation and Sources")]
    public static void Validate()
    {
        var zone=UnityEngine.Object.FindAnyObjectByType<DemonCliffZone>();if(zone==null)throw new InvalidOperationException("Build approach first.");
        var log=new StringBuilder("Demon cliff validation "+DateTime.Now.ToString("s")+"\n");int fails=0,passes=0;
        void Check(bool condition,string description){if(condition)passes++;else fails++;log.AppendLine((condition?"PASS ":"FAIL ")+description);}
        var data=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();var nav=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        log.AppendLine("NAV layers="+nav.layerMask.value+" mask="+nav.layerMask+" data="+AssetDatabase.GetAssetPath(nav.navMeshData));
        Check(data.castle==zone.castleFootprint,"map castle landmark targets reserved NE footprint");Check(zone.stages.Length==3,"three encounter stages");
        Check(zone.stages[0].center.position.y<zone.stages[1].center.position.y&&zone.stages[1].center.position.y<zone.stages[2].center.position.y,"three ascending levels");
        Check(zone.entrance.position.x<zone.stages[0].center.position.x,"entrance is west of first stage");
        Check(AssetDatabase.GetAssetPath(data.terrain.terrainData).StartsWith(Folder),"current terrain is isolated copy");Check(data.terrain.GetComponent<TerrainCollider>().terrainData==data.terrain.terrainData,"render and physics use same terrain");
        Check(!Physics.GetIgnoreLayerCollision(9,7),"shadow dash still collides with environment rims");
        var points=zone.travelRoute.Select(t=>t.position).ToArray();
        for(int i=0;i<points.Length;i++)
        {
            bool found=NavMesh.SamplePosition(points[i],out var hit,.8f,NavMesh.AllAreas);Check(found&&Mathf.Abs(hit.position.y-points[i].y)<.5f,"nav route "+i+" expected="+points[i].ToString("F2")+" found="+hit.position.ToString("F2"));
            if(i==0)continue;var path=new NavMeshPath();bool ok=NavMesh.CalculatePath(points[i-1],points[i],NavMesh.AllAreas,path);Check(ok&&path.status==NavMeshPathStatus.PathComplete,"connected route segment "+(i-1)+" -> "+i+" status="+path.status);
        }
        foreach(var stage in zone.stages)foreach(var spawn in stage.monsterSpawns){var path=new NavMeshPath();Check(NavMesh.CalculatePath(stage.center.position,spawn.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"future monster anchor reachable "+stage.name+"/"+spawn.name);}
        foreach(var pair in new[]{new[]{data.city.position,zone.entrance.position},new[]{data.village.position,zone.entrance.position},new[]{zone.entrance.position,zone.castleEntrance.position}})
        {var path=new NavMeshPath();Check(NavMesh.CalculatePath(pair[0],pair[1],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"global connection "+pair[0].ToString("F1")+" -> "+pair[1].ToString("F1")+" "+path.status);}
        foreach(var renderer in zone.GetComponentsInChildren<Renderer>())foreach(var mat in renderer.sharedMaterials)if(mat!=null)Check(mat.shader.isSupported&&!ShaderUtil.ShaderHasError(mat.shader),"shader valid "+mat.name);
        var reference=zone.transform.Find("00_UserSketch_Reference_Inactive");Check(reference!=null&&reference.childCount==8&&reference.Cast<Transform>().All(t=>!t.gameObject.activeSelf),"eight original sketch objects retained inactive");
        Check(zone.GetComponentsInChildren<NavMeshAgent>().Length==0,"no unrequested permanent enemy spawns");
        log.AppendLine("TOTAL "+passes+" PASS / "+fails+" FAIL");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DemonCliffsValidation.txt",log.ToString());Debug.Log("Demon cliffs validation: "+passes+" pass, "+fails+" fail.");
    }
}
#endif
