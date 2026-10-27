#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

public static partial class DemonLavaSurfaceSetup
{
    // Lava UV V decreases with world height. Negative V scrolling moves features downhill.
    private const float PoolHeight=.65f;
    private sealed class MeshData
    {
        public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        public readonly List<Vector2> uv=new List<Vector2>();public readonly List<Color> colors=new List<Color>();
        public readonly List<int> triangles=new List<int>();public readonly Dictionary<int,int> indices=new Dictionary<int,int>();
    }
    private static string Digest(string file)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","");}
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static Material LavaMaterial(string name,string template,Shader shader,bool falls)
    {
        var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(Source+template)){name=name,shader=shader};
        mat.SetVector("_LavaScale",Vector4.one);mat.SetVector("_LavaScaleNormal",Vector4.one);
        mat.SetVector("_LavaSpeed",falls?new Vector4(0,-.19f,0,0):new Vector4(.035f,.016f,0,0));
        mat.SetVector("_LavaNormalSpeed",falls?new Vector4(0,-.14f,0,0):new Vector4(.019f,.008f,0,0));
        mat.SetVector("_VoronoiSpeed",falls?new Vector4(0,-.09f,0,0):new Vector4(.01f,.004f,0,0));
        mat.SetVector("_NoiseSpeed",falls?new Vector4(0,-.05f,0,0):new Vector4(.008f,.006f,0,0));
        mat.SetFloat("_NoiseAmount",0);mat.SetFloat("_VoronoiAmount",0);mat.SetFloat("_NoiseScale",6);mat.SetFloat("_VoronoiScale",5);
        mat.SetFloat("_Normal_Strength",2);mat.SetFloat("_BottomPower",0);mat.SetFloat("_Frenselpower",2.5f);
        mat.SetColor("_LavaColor",falls?new Color(2.8f,.58f,.025f,1):new Color(2.4f,.48f,.012f,1));mat.SetColor("_FresnelColor",new Color(.35f,.065f,.005f,1));
        AssetDatabase.CreateAsset(mat,Folder+"/Materials/"+name+".mat");return mat;
    }
    [MenuItem("Tools/World/Lava Surfaces/Apply Animated Pools and Downhill Flows")]
    public static void Apply()
    {
        var terrain=CurrentTerrain();if(GameObject.Find(RootName)!=null||AssetDatabase.IsValidFolder(Folder+"/Meshes"))throw new InvalidOperationException("Lava surfaces already exist; their materials and transforms remain editable. Do not overwrite later edits.");
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/Shaders/TerrainPaintedLava.shadergraph");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Private new lava shader must compile first.");
        var td=terrain.terrainData;var lavaLayers=Enumerable.Range(0,td.terrainLayers.Length).Where(i=>(AssetDatabase.GetAssetPath(td.terrainLayers[i])+AssetDatabase.GetAssetPath(td.terrainLayers[i].diffuseTexture)).ToLowerInvariant().Contains("magma")).ToArray();
        if(lavaLayers.Length==0)throw new InvalidOperationException("No painted Magma terrain layer. Refusing to guess the intended area.");
        Directory.CreateDirectory("Logs/SceneBackups");string backup="Logs/SceneBackups/WorldPrototype_BeforeNewLava_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";var scene=terrain.gameObject.scene;if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up live scene.");
        AssetDatabase.SaveAssets();var oldComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var preserved=new[]{AssetDatabase.GetAssetPath(td),Source+"Lava.shadergraph",Source+"Lava.mat",Source+"Lavafall.mat",Source+"LavaRiver.prefab"}.Concat(UnityEngine.Object.FindObjectsByType<NavMeshSurface>().Select(s=>AssetDatabase.GetAssetPath(s.navMeshData))).Where(File.Exists).Distinct().ToDictionary(p=>p,Digest);
        EnsureFolder(Folder+"/Meshes");EnsureFolder(Folder+"/Materials");var poolMaterial=LavaMaterial("Lava_Pools_NewAsset","Lava.mat",shader,false);var flowMaterial=LavaMaterial("Lava_Mountain_Downhill_NewAsset","Lavafall.mat",shader,true);
        var root=new GameObject(RootName);var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();root.transform.SetParent(map!=null?map.transform:terrain.transform.parent,false);root.transform.position=terrain.transform.position;Undo.RegisterCreatedObjectUndo(root,"Animated lava surfaces");
        var alpha=td.GetAlphamaps(0,0,td.alphamapWidth,td.alphamapHeight);int n=td.heightmapResolution-1;var pool=new MeshData();var flow=new MeshData();var mask=new float[n+1,n+1];
        float Mask(float u,float v)
        {
            float ax=u*(td.alphamapWidth-1),az=v*(td.alphamapHeight-1);int x=Mathf.FloorToInt(ax),z=Mathf.FloorToInt(az),nx=Mathf.Min(x+1,td.alphamapWidth-1),nz=Mathf.Min(z+1,td.alphamapHeight-1);float m=0;
            foreach(int layer in lavaLayers)m+=Mathf.Lerp(Mathf.Lerp(alpha[z,x,layer],alpha[z,nx,layer],ax-x),Mathf.Lerp(alpha[nz,x,layer],alpha[nz,nx,layer],ax-x),az-z);return m;
        }
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)mask[z,x]=Mask((float)x/n,(float)z/n);
        float Height(int x,int z)=>td.GetHeight(x,z)+terrain.transform.position.y;
        int Vertex(MeshData mesh,int x,int z,bool falls)
        {
            int id=z*(n+1)+x;if(mesh.indices.TryGetValue(id,out int index))return index;
            float u=(float)x/n,v=(float)z/n,h=Height(x,z);var world=terrain.transform.position+new Vector3(u*td.size.x,0,v*td.size.z);world.y=Mathf.Max(h+.04f,PoolHeight);float opacity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.72f,mask[z,x]));
            index=mesh.vertices.Count;mesh.indices.Add(id,index);mesh.vertices.Add(root.transform.InverseTransformPoint(world));mesh.normals.Add(h<PoolHeight-.04f?Vector3.up:td.GetInterpolatedNormal(u,v));
            // The source graph raises raw UV.y to BottomPower; keep it positive to avoid pow(negative, 0) producing NaN on Metal.
            mesh.uv.Add(falls?new Vector2((world.x+world.z*.37f)/4,1+(td.size.y+terrain.transform.position.y-h)/4):new Vector2(world.x/3,world.z/3));mesh.colors.Add(new Color(opacity,opacity,opacity,opacity));return index;
        }
        void Triangle(int ax,int az,int bx,int bz,int cx,int cz)
        {
            if(Mathf.Max(mask[az,ax],Mathf.Max(mask[bz,bx],mask[cz,cx]))<.15f)return;
            bool falls=(Height(ax,az)+Height(bx,bz)+Height(cx,cz))/3>.85f;var mesh=falls?flow:pool;
            mesh.triangles.Add(Vertex(mesh,ax,az,falls));mesh.triangles.Add(Vertex(mesh,bx,bz,falls));mesh.triangles.Add(Vertex(mesh,cx,cz,falls));
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){Triangle(x,z,x,z+1,x+1,z+1);Triangle(x,z,x+1,z+1,x+1,z);}
        MakeSurface("01_Animated_Lava_Pools",root.transform,pool,poolMaterial);MakeSurface("02_Lava_Flowing_Down_Mountains",root.transform,flow,flowMaterial);
        var log=new StringBuilder("New lava visual integration "+DateTime.Now.ToString("s")+"\nBACKUP "+backup+"\nSOURCE "+Source+"\nTERRAIN "+AssetDatabase.GetAssetPath(td)+"\nPOOL Y="+PoolHeight+"\n");
        log.AppendLine("Pool vertices="+pool.vertices.Count+" triangles="+pool.triangles.Count/3+"; mountain flow vertices="+flow.vertices.Count+" triangles="+flow.triangles.Count/3);
        int changed=0;foreach(var entry in oldComponents)if(entry.Key!=null&&entry.Key!=root.transform.parent&&EditorJsonUtility.ToJson(entry.Key)!=entry.Value){changed++;log.AppendLine("FAIL existing component modified "+entry.Key.name+" "+entry.Key.GetType().Name);}
        log.AppendLine((changed==0?"PASS":"FAIL")+" existing components preserved; modified="+changed);
        AssetDatabase.SaveAssets();foreach(var entry in preserved)log.AppendLine((Digest(entry.Key)==entry.Value?"PASS":"FAIL")+" source/terrain/navigation bytes preserved "+entry.Key);
        File.WriteAllText("Logs/DemonLavaBuild.txt",log.ToString());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Capture();Selection.activeGameObject=root;Debug.Log("Applied new animated lava only. Logs/DemonLavaBuild.txt");
    }
    private static void MakeSurface(string name,Transform parent,MeshData data,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=7;var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(data.vertices);mesh.SetNormals(data.normals);mesh.SetUVs(0,data.uv);mesh.SetColors(data.colors);mesh.SetTriangles(data.triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
    }
    [MenuItem("Tools/World/Lava Surfaces/Normalize Mountain Flow UV")]
    public static void NormalizeFlowUV()
    {
        var terrain=CurrentTerrain();var root=GameObject.Find(RootName);var filter=root.transform.Find("02_Lava_Flowing_Down_Mountains").GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var uv=mesh.uv;var vertices=mesh.vertices;
        for(int i=0;i<uv.Length;i++){var world=filter.transform.TransformPoint(vertices[i]);float h=terrain.SampleHeight(world)+terrain.transform.position.y;uv[i].y=1+(terrain.terrainData.size.y+terrain.transform.position.y-h)/4;}
        mesh.uv=uv;mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();Capture();
    }
    [MenuItem("Tools/World/Lava Surfaces/Capture Current Lava")]
    public static void Capture()
    {
        var terrain=CurrentTerrain();var go=new GameObject("Temporary lava preview camera");var camera=go.AddComponent<Camera>();camera.enabled=false;var rt=RenderTexture.GetTemporary(1500,1200,24);var image=new Texture2D(1500,1200,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try
        {
            camera.transform.position=new Vector3(42,97,-10);camera.transform.LookAt(new Vector3(42,5,59));camera.orthographic=true;camera.orthographicSize=50;camera.farClipPlane=250;camera.cullingMask=~(1<<5);camera.clearFlags=CameraClearFlags.Skybox;camera.targetTexture=rt;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,1200),0,0);image.Apply();File.WriteAllBytes("Logs/DemonLava_Overview.png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
#endif
