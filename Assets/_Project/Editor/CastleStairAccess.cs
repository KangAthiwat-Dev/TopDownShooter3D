#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

public static partial class CastleRouteRepair
{
    [MenuItem("Tools/World/Castle Route/Fit Stair Access Around Existing Rock Ridge")]
    public static void FitStairAccess()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");var root=GameObject.Find("CastleRoute_WalkabilityRepair");var previous=root.transform.Find("Stair_To_Island_Smooth_Contact");
        EditorSceneManager.SaveScene(root.scene,"Logs/SceneBackups/WorldPrototype_BeforeStairAccess_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);if(previous!=null)previous.gameObject.SetActive(false);Physics.SyncTransforms();
        float Ground(Vector3 p)=>Physics.RaycastAll(p+Vector3.up*60,Vector3.down,150,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(root.transform)&&!(h.collider is CharacterController)).Select(h=>h.point.y).DefaultIfEmpty(-100).Max();
        float ClearanceGround(Vector3 p)=>Mathf.Max(Ground(p),Mathf.Max(Mathf.Max(Ground(p+Vector3.right*.35f),Ground(p-Vector3.right*.35f)),Mathf.Max(Ground(p+Vector3.forward*.35f),Ground(p-Vector3.forward*.35f))));
        var plan=JsonUtility.FromJson<RoutePlan>(File.ReadAllText(Folder+"/RoutePlan.json"));var faces=TopFaces(Find("PlatformStair_001"));var stairBounds=BoundsOf(Find("PlatformStair_001"));var stairAxis=Axis(Find("PlatformStair_001"),faces,true);var head=stairBounds.center+stairAxis*5.35f;head.y=Ground(head);
        const int nx=61,nz=49;const float x0=-9,z0=45,step=.5f;var heights=new float[nx*nz];
        Vector3 Point(int id)=>new Vector3(x0+id%nx*step,heights[id],z0+id/nx*step);
        int Id(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.z-z0)/step),0,nz-1)*nx+Mathf.Clamp(Mathf.RoundToInt((p.x-x0)/step),0,nx-1);
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)heights[z*nx+x]=ClearanceGround(new Vector3(x0+x*step,0,z0+z*step));
        int start=Id(head),goal=Id(plan.route[2]);var costs=Enumerable.Repeat(float.PositiveInfinity,nx*nz).ToArray();var parent=Enumerable.Repeat(-1,nx*nz).ToArray();var closed=new bool[nx*nz];var open=new SortedSet<(float priority,int id)>();costs[start]=0;open.Add((0,start));
        while(open.Count>0)
        {
            var item=open.Min;open.Remove(item);int id=item.id;if(closed[id])continue;closed[id]=true;if(id==goal)break;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0&&dz==0)continue;int x=id%nx+dx,z=id/nx+dz;if(x<0||z<0||x>=nx||z>=nz)continue;int next=z*nx+x;float distance=step*Mathf.Sqrt(dx*dx+dz*dz),rise=Mathf.Abs(heights[next]-heights[id]);
                if(closed[next]||heights[next]<5||heights[next]>10.1f||rise>distance*.72f)continue;float cost=costs[id]+distance+rise*.4f;if(cost>=costs[next])continue;costs[next]=cost;parent[next]=id;var delta=Point(goal)-Point(next);delta.y=0;open.Add((cost+delta.magnitude,next));
            }
        }
        if(parent[goal]<0){if(previous!=null)previous.gameObject.SetActive(true);throw new InvalidOperationException("No safe stair-to-island passage found through the existing ridge.");}
        var path=new List<Vector3>();for(int id=goal;id>=0;id=parent[id]){path.Add(Point(id));if(id==start)break;}path.Reverse();
        bool Clear(Vector3 a,Vector3 b)
        {
            float length=Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude;int count=Mathf.CeilToInt(length/.2f);float last=ClearanceGround(a);for(int i=1;i<=count;i++){var p=Vector3.Lerp(a,b,(float)i/count);float h=ClearanceGround(p);if(h<5||h>10.1f||Mathf.Abs(h-last)>.72f*length/count)return false;last=h;}return true;
        }
        var corners=new List<Vector3>{plan.stairs[0],head,path[0]};int current=0;while(current<path.Count-1){int next=path.Count-1;while(next>current+1&&!Clear(path[current],path[next]))next--;corners.Add(path[next]);current=next;}corners.Add(plan.route[2]);
        var nodes=new List<Vector3>();for(int k=1;k<corners.Count;k++){int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.ProjectOnPlane(corners[k]-corners[k-1],Vector3.up).magnitude/.2f));for(int j=k==1?0:1;j<=count;j++){var p=Vector3.Lerp(corners[k-1],corners[k],(float)j/count);p.y=ClearanceGround(p)+.025f;nodes.Add(p);}}
        var ground=nodes.Select(p=>p.y).ToArray();for(int i=1;i<nodes.Count;i++){var p=nodes[i];p.y=Mathf.Max(p.y,nodes[i-1].y-.72f*Vector3.ProjectOnPlane(p-nodes[i-1],Vector3.up).magnitude);nodes[i]=p;}for(int i=nodes.Count-2;i>=0;i--){var p=nodes[i];p.y=Mathf.Max(p.y,nodes[i+1].y-.72f*Vector3.ProjectOnPlane(p-nodes[i+1],Vector3.up).magnitude);nodes[i]=p;}
        if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);MeshStrip("Stair_To_Island_Smooth_Contact",root.transform,nodes.ToArray(),2.8f,false,null);plan.stairs[1]=head;plan.route[1]=head;plan.ascent=corners.Where((p,i)=>i==0||Vector3.ProjectOnPlane(p-corners[i-1],Vector3.up).magnitude>.2f).Select(p=>{p.y=nodes.OrderBy(n=>Vector3.ProjectOnPlane(n-p,Vector3.up).sqrMagnitude).First().y;return p;}).ToArray();File.WriteAllText(Folder+"/RoutePlan.json",JsonUtility.ToJson(plan,true));AssetDatabase.ImportAsset(Folder+"/RoutePlan.json");
        var nav=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/CastleRouteNavigation_Final.asset"));EditorUtility.SetDirty(nav);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        File.WriteAllText("Logs/CastleStairContact.txt","PASS route uses existing low passage around the rock ridge\nRoute corners="+plan.ascent.Length+"; highest walk contact="+nodes.Max(p=>p.y).ToString("F3")+"; envelope lift="+nodes.Select((p,i)=>p.y-ground[i]).Max().ToString("F3")+"\nTerrain, all user transforms, textures and player settings unchanged.\n");Debug.Log("Fitted stair access through the existing low rock passage.");
    }
}
#endif
