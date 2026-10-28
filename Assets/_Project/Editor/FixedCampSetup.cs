#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class FixedCampSetup
{
    private struct PaintPatch { public int layer,cells;public Vector3 center;public Vector2 size; }
    private static List<PaintPatch> Patches(Terrain terrain)
    {
        var data=terrain.terrainData;int w=data.alphamapWidth,h=data.alphamapHeight,n=data.alphamapLayers;
        var map=data.GetAlphamaps(0,0,w,h);var result=new List<PaintPatch>();var visited=new bool[w*h];var queue=new Queue<int>();
        var steps=new[]{new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(0,-1),new Vector2Int(0,1)};
        for(int layer=0;layer<n;layer++)
        {
            System.Array.Clear(visited,0,visited.Length);
            for(int z=0;z<h;z++)for(int x=0;x<w;x++)
            {
                int id=z*w+x;if(visited[id]||map[z,x,layer]<.5f)continue;
                queue.Enqueue(id);visited[id]=true;int count=0,minX=x,maxX=x,minZ=z,maxZ=z;double sx=0,sz=0;
                while(queue.Count>0)
                {
                    int cell=queue.Dequeue(),cx=cell%w,cz=cell/w;count++;sx+=cx;sz+=cz;minX=Mathf.Min(minX,cx);maxX=Mathf.Max(maxX,cx);minZ=Mathf.Min(minZ,cz);maxZ=Mathf.Max(maxZ,cz);
                    foreach(var step in steps)
                    {
                        int nx=cx+step.x,nz=cz+step.y;if(nx<0||nx>=w||nz<0||nz>=h)continue;int ni=nz*w+nx;
                        if(!visited[ni]&&map[nz,nx,layer]>=.5f){visited[ni]=true;queue.Enqueue(ni);}
                    }
                }
                var size=new Vector2((maxX-minX+1)*data.size.x/(w-1),(maxZ-minZ+1)*data.size.z/(h-1));
                if(size.x*size.y<4)continue;
                var point=terrain.transform.position+new Vector3((float)(sx/count)*data.size.x/(w-1),0,(float)(sz/count)*data.size.z/(h-1));point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
                result.Add(new PaintPatch{layer=layer,cells=count,center=point,size=size});
            }
        }
        return result;
    }
    [MenuItem("Tools/Fixed Camps/Inspect Painted Clearings and Camp Sources")]
    public static void Inspect()
    {
        var report=new StringBuilder();
        foreach(var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            report.AppendLine("TERRAIN "+terrain.name+" pos="+terrain.transform.position+" size="+terrain.terrainData.size+" data="+AssetDatabase.GetAssetPath(terrain.terrainData));
            var layers=terrain.terrainData.terrainLayers;
            for(int i=0;i<layers.Length;i++)report.AppendLine(" LAYER "+i+" "+AssetDatabase.GetAssetPath(layers[i])+" texture="+layers[i]?.diffuseTexture?.name);
            foreach(var p in Patches(terrain))report.AppendLine(" PATCH layer="+p.layer+" cells="+p.cells+" center="+p.center.ToString("F2")+" size="+p.size.ToString("F2"));
        }
        var director=UnityEngine.Object.FindAnyObjectByType<WorldMonsterDirector>();
        report.AppendLine("DIRECTOR terrain="+director.terrain.name+" camps="+director.campPopulation+" respawn="+director.campRespawnDelay+" candidates="+director.campSites.Length);
        foreach(var camp in director.campTemplates)report.AppendLine(" TEMPLATE "+AssetDatabase.GetAssetPath(camp)+" radius="+camp.radius+" members="+camp.memberPoints.Length);
        foreach(var t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.IndexOf("camp",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("clear",StringComparison.OrdinalIgnoreCase)>=0))
            report.AppendLine(" MARKER "+t.name+" pos="+t.position+" parent="+t.parent?.name);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FixedCampInspection.txt",report.ToString());Debug.Log("Painted clearings and camp source inspection complete.");
    }
}
#endif
