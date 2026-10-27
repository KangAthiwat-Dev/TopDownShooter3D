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
    [MenuItem("Tools/World/Castle Route/Finish Exposed Stair Connector")]
    public static void FinishStairConnector()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");var root=GameObject.Find("CastleRoute_WalkabilityRepair");if(root.transform.Find("Stair_Connection_Exposed_Stone")!=null)throw new InvalidOperationException("Stone connector already finished.");
        var contact=root.transform.Find("Stair_To_Island_Smooth_Contact");var mesh=contact.GetComponent<MeshCollider>().sharedMesh;var positions=mesh.vertices;var faces=TopFaces(Find("PlatformStair_001"));
        float GroundWithoutContact(Vector3 p)=>Physics.RaycastAll(p+Vector3.up*60,Vector3.down,150,1<<7,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(root.transform)).Select(h=>h.point.y).DefaultIfEmpty(p.y).Max();
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int k=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x*.28f,(p.z+p.y)*.28f));triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
        }
        for(int i=0;i<positions.Length/2-1;i++)
        {
            var a=contact.TransformPoint(positions[i*2]);var b=contact.TransformPoint(positions[i*2+1]);var c=contact.TransformPoint(positions[i*2+2]);var d=contact.TransformPoint(positions[i*2+3]);var p=(a+b)*.5f;var q=(c+d)*.5f;
            if(SurfaceHeight(faces,(p+q)*.5f,out _))continue;
            if(Mathf.Max(p.y-GroundWithoutContact(p),q.y-GroundWithoutContact(q))<.16f)continue;
            Quad(a,c,d,b);var bottomA=a;var bottomB=b;var bottomC=c;var bottomD=d;bottomA.y=GroundWithoutContact(a)-.05f;bottomB.y=GroundWithoutContact(b)-.05f;bottomC.y=GroundWithoutContact(c)-.05f;bottomD.y=GroundWithoutContact(d)-.05f;Quad(bottomA,bottomC,c,a);Quad(b,d,bottomD,bottomB);
        }
        if(vertices.Count==0)return;
        var surface=new GameObject("Stair_Connection_Exposed_Stone");surface.transform.SetParent(root.transform,false);var rendered=new Mesh{name=surface.name};rendered.SetVertices(vertices);rendered.SetUVs(0,uv);rendered.SetTriangles(triangles,0);rendered.RecalculateNormals();rendered.RecalculateTangents();rendered.RecalculateBounds();AssetDatabase.CreateAsset(rendered,Folder+"/Stair_Connection_Exposed_Stone.asset");surface.AddComponent<MeshFilter>().sharedMesh=rendered;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Stair_Terrain_Stone_Connection"};material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_BC.png"));material.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(material,Folder+"/Stair_Terrain_Stone_Connection.mat");surface.AddComponent<MeshRenderer>().sharedMaterial=material;
        Undo.RegisterCreatedObjectUndo(surface,"Finish exposed stone walking connector");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Debug.Log("Finished visible stone only where the walking connector crosses a gap above the original terrain.");
    }
    [MenuItem("Tools/World/Castle Route/Smooth Stairs to Terrain Contact")]
    public static void SmoothStairContact()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");var root=GameObject.Find("CastleRoute_WalkabilityRepair");var previous=root.transform.Find("Stair_To_Island_Smooth_Contact");
        var scene=root.scene;EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/WorldPrototype_BeforeStairContact_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
        var plan=JsonUtility.FromJson<RoutePlan>(File.ReadAllText(Folder+"/RoutePlan.json"));var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;string terrainPath=AssetDatabase.GetAssetPath(terrain.terrainData),before=Digest(terrainPath);
        var controls=new[]{plan.stairs[0],plan.stairs[1],plan.route[2]};var nodes=new List<Vector3>();var original=new List<float>();
        Physics.SyncTransforms();for(int segment=0;segment<controls.Length-1;segment++)
        {
            var start=controls[segment];var end=controls[segment+1];var direction=Vector3.ProjectOnPlane(end-start,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,direction);int count=Mathf.CeilToInt(Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude/.2f);
            for(int i=segment==0?0:1;i<=count;i++){var p=Vector3.Lerp(start,end,(float)i/count);p.y=PhysicalHeight(p)+.028f;nodes.Add(p);original.Add(p.y);}
        }
        // Least upper profile with a 0.62 rise/run limit, so the existing terrain risers stay below the walking contact.
        for(int i=1;i<nodes.Count;i++){var p=nodes[i];float step=Vector3.ProjectOnPlane(p-nodes[i-1],Vector3.up).magnitude;p.y=Mathf.Max(p.y,nodes[i-1].y-.62f*step);nodes[i]=p;}
        for(int i=nodes.Count-2;i>=0;i--){var p=nodes[i];float step=Vector3.ProjectOnPlane(p-nodes[i+1],Vector3.up).magnitude;p.y=Mathf.Max(p.y,nodes[i+1].y-.62f*step);nodes[i]=p;}
        MeshStrip("Stair_To_Island_Smooth_Contact",root.transform,nodes.ToArray(),2.8f,false,null);var nav=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/CastleRouteNavigation_Final.asset"));EditorUtility.SetDirty(nav);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/CastleStairContact.txt","Fitted contact from stairs to first island.\nMax envelope lift over sampled contact="+nodes.Select((p,i)=>p.y-original[i]).Max().ToString("F3")+" m\n"+(Digest(terrainPath)==before?"PASS":"FAIL")+" terrain data and original painted layout unchanged\nInvisible collision only; player step/slope settings unchanged.\n");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("Stair-to-terrain collision contact smoothed.");
    }
}
#endif
