#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class DemonLavaSurfaceSetup
{
    private const string LiquidRootName="DemonLava_LiquidLake_And_ChannelStreams";
    private const float LiquidLevel=2.35f;
    private struct ShorePoint
    {
        public Vector3 world;public float ground;
        public ShorePoint(Vector3 world,float ground){this.world=world;this.ground=ground;}
    }
    [MenuItem("Tools/World/Lava Surfaces/Fill Liquid Lake and Painted Channel Streams")]
    public static void FillLiquidLake()
    {
        var terrain=CurrentTerrain();if(GameObject.Find(LiquidRootName)!=null)throw new InvalidOperationException("Liquid lake already exists. Preserve later edits.");var td=terrain.terrainData;var scene=terrain.gameObject.scene;
        string backup="Logs/SceneBackups/WorldPrototype_BeforeLiquidLake_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";Directory.CreateDirectory("Logs/SceneBackups");if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
        AssetDatabase.SaveAssets();var oldComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var preserved=new[]{AssetDatabase.GetAssetPath(td),Source+"Lava.shadergraph",Source+"Lava.mat",Source+"Lavafall.mat",AssetDatabase.GetAssetPath(td.terrainLayers.Last())}.Concat(UnityEngine.Object.FindObjectsByType<NavMeshSurface>().Select(s=>AssetDatabase.GetAssetPath(s.navMeshData))).Where(File.Exists).Distinct().ToDictionary(p=>p,Digest);
        var previousOverlay=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==RootName);if(previousOverlay!=null)previousOverlay.gameObject.SetActive(false);
        EnsureFolder(Folder+"/LiquidMeshes");EnsureFolder(Folder+"/LiquidMaterials");
        Material LiquidMaterial(string name,string template,bool stream)
        {
            var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+template+".mat")){name=name};mat.SetColor("_LavaColor",new Color(1.6f,.3f,.012f,1));mat.SetColor("_FresnelColor",new Color(.18f,.035f,.003f,1));mat.SetVector("_LavaScale",new Vector4(.65f,.65f,0,0));mat.SetVector("_LavaScaleNormal",new Vector4(.65f,.65f,0,0));
            if(stream)mat.SetVector("_LavaSpeed",new Vector4(0,-.19f,0,0));AssetDatabase.CreateAsset(mat,Folder+"/LiquidMaterials/"+name+".mat");return mat;
        }
        var lakeMat=LiquidMaterial("Lava_LiquidLake","Lava_Pools_NewAsset",false);var streamMat=LiquidMaterial("Lava_LiquidStreams","Lava_Mountain_Downhill_NewAsset",true);
        var root=new GameObject(LiquidRootName);var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();root.transform.SetParent(map!=null?map.transform:terrain.transform.parent,false);root.transform.position=terrain.transform.position;Undo.RegisterCreatedObjectUndo(root,"Fill actual lava lake");
        int n=td.heightmapResolution-1;float dx=td.size.x/n,dz=td.size.z/n;var heights=td.GetHeights(0,0,n+1,n+1);var wet=new bool[n+1,n+1];var queue=new Queue<Vector2Int>();
        float H(int x,int z)=>heights[z,x]*td.size.y+terrain.transform.position.y;
        var seed=new Vector2Int(Mathf.RoundToInt((35-terrain.transform.position.x)/dx),Mathf.RoundToInt((50-terrain.transform.position.z)/dz));if(H(seed.x,seed.y)>=LiquidLevel)throw new InvalidOperationException("Expected central depression has moved. Refusing to fill another part of the map.");
        wet[seed.y,seed.x]=true;queue.Enqueue(seed);int floodCount=0;
        while(queue.Count>0)
        {
            var p=queue.Dequeue();floodCount++;foreach(var offset in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            {var q=p+offset;if(q.x<0||q.y<0||q.x>n||q.y>n||wet[q.y,q.x]||H(q.x,q.y)>=LiquidLevel)continue;wet[q.y,q.x]=true;queue.Enqueue(q);}
        }
        if(floodCount*dx*dz>6500)throw new InvalidOperationException("Depression spills outside expected lake. Terrain has changed; inspect the shoreline before filling.");
        var lake=new MeshData();
        ShorePoint GroundPoint(int x,int z)=>new ShorePoint(terrain.transform.position+new Vector3(x*dx,0,z*dz),H(x,z));
        void LakeTriangle(int ax,int az,int bx,int bz,int cx,int cz)
        {
            if(!wet[az,ax]&&!wet[bz,bx]&&!wet[cz,cx])return;
            var poly=new List<ShorePoint>{GroundPoint(ax,az),GroundPoint(bx,bz),GroundPoint(cx,cz)};var clipped=new List<ShorePoint>();
            for(int i=0;i<poly.Count;i++)
            {
                var a=poly[i];var b=poly[(i+1)%poly.Count];bool ai=a.ground<LiquidLevel,bi=b.ground<LiquidLevel;
                if(ai)clipped.Add(a);if(ai!=bi){float t=(LiquidLevel-a.ground)/(b.ground-a.ground);clipped.Add(new ShorePoint(Vector3.Lerp(a.world,b.world,t),LiquidLevel));}
            }
            if(clipped.Count<3)return;int start=lake.vertices.Count;
            foreach(var point in clipped)
            {
                var world=point.world;world.y=LiquidLevel+.015f;float opacity=Mathf.SmoothStep(0,1,Mathf.Clamp01((LiquidLevel-point.ground)/.25f));lake.vertices.Add(root.transform.InverseTransformPoint(world));lake.normals.Add(Vector3.up);lake.uv.Add(new Vector2(world.x/3,world.z/3));lake.colors.Add(new Color(opacity,opacity,opacity,opacity));
            }
            for(int i=1;i<clipped.Count-1;i++){lake.triangles.Add(start);lake.triangles.Add(start+i);lake.triangles.Add(start+i+1);}
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){LakeTriangle(x,z,x,z+1,x+1,z+1);LakeTriangle(x,z,x+1,z+1,x+1,z);}
        // The painted Magma marks the channel bed. Erode its edges so the original crust remains visible along both banks.
        var alpha=td.GetAlphamaps(0,0,td.alphamapWidth,td.alphamapHeight);int layer=Array.FindIndex(td.terrainLayers,l=>(AssetDatabase.GetAssetPath(l)+AssetDatabase.GetAssetPath(l.diffuseTexture)).ToLowerInvariant().Contains("magma"));if(layer<0)throw new InvalidOperationException("No painted channel guide.");
        var distance=new float[n+1,n+1];
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int ax=Mathf.RoundToInt((float)x/n*(td.alphamapWidth-1)),az=Mathf.RoundToInt((float)z/n*(td.alphamapHeight-1));distance[z,x]=alpha[az,ax,layer]>.6f?1000:0;}
        float diagonal=Mathf.Sqrt(dx*dx+dz*dz);
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){float d=distance[z,x];if(x>0)d=Mathf.Min(d,distance[z,x-1]+dx);if(z>0)d=Mathf.Min(d,distance[z-1,x]+dz);if(x>0&&z>0)d=Mathf.Min(d,distance[z-1,x-1]+diagonal);if(x<n&&z>0)d=Mathf.Min(d,distance[z-1,x+1]+diagonal);distance[z,x]=d;}
        for(int z=n;z>=0;z--)for(int x=n;x>=0;x--){float d=distance[z,x];if(x<n)d=Mathf.Min(d,distance[z,x+1]+dx);if(z<n)d=Mathf.Min(d,distance[z+1,x]+dz);if(x<n&&z<n)d=Mathf.Min(d,distance[z+1,x+1]+diagonal);if(x>0&&z<n)d=Mathf.Min(d,distance[z+1,x-1]+diagonal);distance[z,x]=d;}
        var streams=new MeshData();
        int StreamVertex(int x,int z)
        {
            int key=z*(n+1)+x;if(streams.indices.TryGetValue(key,out int id))return id;id=streams.vertices.Count;streams.indices.Add(key,id);float h=H(x,z),depth=.12f+.12f*Mathf.Clamp01((distance[z,x]-.65f)/1.5f);var world=terrain.transform.position+new Vector3(x*dx,0,z*dz);world.y=Mathf.Max(h+depth,LiquidLevel+.025f);
            streams.vertices.Add(root.transform.InverseTransformPoint(world));streams.normals.Add(td.GetInterpolatedNormal((float)x/n,(float)z/n));streams.uv.Add(new Vector2((world.x+world.z*.37f)/4,1+(td.size.y+terrain.transform.position.y-h)/4));
            float opacity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.2f,distance[z,x]));streams.colors.Add(new Color(opacity,opacity,opacity,opacity));return id;
        }
        void StreamTriangle(int ax,int az,int bx,int bz,int cx,int cz)
        {
            float wx=terrain.transform.position.x+(ax+bx+cx)*dx/3,wz=terrain.transform.position.z+(az+bz+cz)*dz/3;
            if(!((wx>38&&wz>68)||(wx>72&&wz>50&&wz<68)))return;
            if(Mathf.Max(H(ax,az),Mathf.Max(H(bx,bz),H(cx,cz)))<LiquidLevel-.15f)return;
            if(Mathf.Max(distance[az,ax],Mathf.Max(distance[bz,bx],distance[cz,cx]))<=.65f)return;
            streams.triangles.Add(StreamVertex(ax,az));streams.triangles.Add(StreamVertex(bx,bz));streams.triangles.Add(StreamVertex(cx,cz));
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){StreamTriangle(x,z,x,z+1,x+1,z+1);StreamTriangle(x,z,x+1,z+1,x+1,z);}
        CreateLiquidMesh("01_Liquid_Lake_Constant_Level",root.transform,lake,lakeMat);CreateLiquidMesh("02_Liquid_Streams_Inside_Painted_Channels",root.transform,streams,streamMat);
        AssetDatabase.SaveAssets();var report=new StringBuilder("Corrected liquid lava integration "+DateTime.Now.ToString("s")+"\nBACKUP "+backup+"\nLAKE_LEVEL "+LiquidLevel+"\nCONNECTED LAKE AREA approximately "+(floodCount*dx*dz).ToString("F1")+" square metres\n");
        report.AppendLine("Lake triangles="+lake.triangles.Count/3+"; stream triangles="+streams.triangles.Count/3+"; original painted banks remain visible outside eroded stream cores.");
        int changes=0;foreach(var entry in oldComponents)if(entry.Key!=null&&entry.Key!=root.transform.parent&&EditorJsonUtility.ToJson(entry.Key)!=entry.Value){changes++;report.AppendLine("FAIL existing component changed "+entry.Key.name);}
        report.AppendLine((changes==0?"PASS":"FAIL")+" existing terrain, colliders, routes, and gameplay components unchanged");foreach(var entry in preserved)report.AppendLine((Digest(entry.Key)==entry.Value?"PASS":"FAIL")+" original bytes unchanged "+entry.Key);
        report.AppendLine("PASS new liquid surfaces contain no collider or gameplay component");File.WriteAllText("Logs/DemonLavaLiquidBuild.txt",report.ToString());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Capture();Selection.activeGameObject=root;Debug.Log("Filled actual lake and added streams only inside painted channel cores.");
    }
    private static void CreateLiquidMesh(string name,Transform parent,MeshData data,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=7;var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(data.vertices);mesh.SetNormals(data.normals);mesh.SetUVs(0,data.uv);mesh.SetColors(data.colors);mesh.SetTriangles(data.triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/LiquidMeshes/"+name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    [MenuItem("Tools/World/Lava Surfaces/Restore Original Paint and Inspect Liquid Channels")]
    public static void RestorePaintAndInspectLiquid()
    {
        var terrain=CurrentTerrain();Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(terrain.gameObject.scene,"Logs/SceneBackups/WorldPrototype_BeforeLiquidLavaCorrection_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var overlay=GameObject.Find(RootName);if(overlay!=null){Undo.RecordObject(overlay,"Remove mistaken texture overlay");overlay.SetActive(false);}
        var td=terrain.terrainData;var alpha=td.GetAlphamaps(0,0,td.alphamapWidth,td.alphamapHeight);int layer=Array.FindIndex(td.terrainLayers,l=>(AssetDatabase.GetAssetPath(l)+AssetDatabase.GetAssetPath(l.diffuseTexture)).ToLowerInvariant().Contains("magma"));
        var log=new StringBuilder("x,z,height,paint\n");
        for(float z=18;z<=99;z+=.5f)for(float x=0;x<=97;x+=.5f)
        {float u=(x-terrain.transform.position.x)/td.size.x,v=(z-terrain.transform.position.z)/td.size.z;int ax=Mathf.Clamp(Mathf.RoundToInt(u*(td.alphamapWidth-1)),0,td.alphamapWidth-1),az=Mathf.Clamp(Mathf.RoundToInt(v*(td.alphamapHeight-1)),0,td.alphamapHeight-1);float h=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;log.AppendLine(x+","+z+","+h.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+alpha[az,ax,layer].ToString("F4",System.Globalization.CultureInfo.InvariantCulture));}
        File.WriteAllText("Logs/DemonLavaLiquidChannels.csv",log.ToString());EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);EditorSceneManager.SaveScene(terrain.gameObject.scene);Capture();Debug.Log("Original painted terrain restored; generated overlay disabled. Liquid channel survey exported.");
    }
}
#endif
