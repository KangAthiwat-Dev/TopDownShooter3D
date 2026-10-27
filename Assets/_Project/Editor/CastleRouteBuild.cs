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
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

public static partial class CastleRouteRepair
{
    [Serializable] public sealed class RoutePlan{public Vector3[] bridge,stairs,route,ascent;public float width;public string backup;}
    private static string Digest(string file){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","");}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static float PhysicalHeight(Vector3 p)=>Physics.RaycastAll(p+Vector3.up*60,Vector3.down,150,1<<7,QueryTriggerInteraction.Ignore).Select(h=>h.point.y).DefaultIfEmpty(float.NaN).Max();
    private static Mesh MeshStrip(string name,Transform root,Vector3[] nodes,float width,bool visible,Material material)
    {
        var go=new GameObject(name);go.layer=7;go.transform.SetParent(root,false);go.transform.position=nodes[0];var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();float distance=0;
        for(int i=0;i<nodes.Length;i++){if(i>0)distance+=Vector3.Distance(nodes[i-1],nodes[i]);var dir=nodes[Mathf.Min(i+1,nodes.Length-1)]-nodes[Mathf.Max(0,i-1)];dir.y=0;var right=Vector3.Cross(Vector3.up,dir.normalized)*width*.5f;vertices.Add(nodes[i]-right-go.transform.position);vertices.Add(nodes[i]+right-go.transform.position);uv.Add(new Vector2(0,distance/2));uv.Add(new Vector2(1,distance/2));if(i<nodes.Length-1){int k=i*2;triangles.AddRange(new[]{k,k+2,k+3,k,k+3,k+1});}}
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+".asset"));go.AddComponent<MeshCollider>().sharedMesh=mesh;
        if(visible){go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}return mesh;
    }
    [MenuItem("Tools/World/Castle Route/Fix Island Colliders and Textured Bridge")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");if(GameObject.Find("CastleRoute_WalkabilityRepair")!=null)throw new InvalidOperationException("Route already repaired; preserve later edits.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();string backup="Logs/SceneBackups/WorldPrototype_BeforeCastleRoute_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";Directory.CreateDirectory("Logs/SceneBackups");if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
        var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;var nav=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();var oldAssets=new[]{AssetDatabase.GetAssetPath(terrain.terrainData),AssetDatabase.GetAssetPath(nav.navMeshData),"Assets/Assets_Imported/stylized-bridge/source/SM_bridge.fbx","Assets/Assets_Imported/stylized-bridge/source/SM_bridge.fbx.meta","Assets/Assets_Imported/stylized-bridge/textures/T_Bridge_BaseColor.png","Assets/Assets_Imported/stylized-bridge/textures/T_Bridge_Normal.png"}.ToDictionary(p=>p,Digest);
        var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));var allowed=new HashSet<Component>();EnsureFolder(Folder);
        var root=new GameObject("CastleRoute_WalkabilityRepair");Undo.RegisterCreatedObjectUndo(root,"Castle route physics and bridge");var log=new StringBuilder("User castle route repair "+DateTime.Now.ToString("s")+"\nBACKUP "+backup+"\n");
        var islands=UnityEngine.Object.FindObjectsByType<CapsuleCollider>().Where(c=>c.transform.parent!=null&&c.transform.parent.name=="DemonLava_LiquidLake_And_ChannelStreams"&&c.name.StartsWith("Cylinder")).OrderBy(c=>c.name).ToArray();if(islands.Length!=3)throw new InvalidOperationException("Expected the user's three island primitives.");
        foreach(var original in islands)
        {
            var mesh=original.GetComponent<MeshFilter>().sharedMesh;allowed.Add(original);Undo.RecordObject(original,"Disable inflated primitive capsule");original.enabled=false;var exact=Undo.AddComponent<MeshCollider>(original.gameObject);exact.sharedMesh=mesh;exact.convex=false;Undo.RecordObject(original.gameObject,"Island environment collision layer");original.gameObject.layer=7;
            log.AppendLine("Replaced capsule collision with actual mesh: "+PathOf(original.transform)+" visual top="+BoundsOf(original.gameObject).max.y.ToString("F3"));
        }
        var stairs=Find("PlatformStair_001");foreach(Transform t in stairs.GetComponentsInChildren<Transform>()){Undo.RecordObject(t.gameObject,"Stairs environment collision layer");t.gameObject.layer=7;PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
        var bridge=Find("SM_bridge");var faces=TopFaces(bridge);var axis=Axis(bridge,faces,false);var center=BoundsOf(bridge).center;var side=Vector3.Cross(Vector3.up,axis);var hits=new List<Vector2>();
        for(float t=-10;t<=10;t+=.08f)if(SurfaceHeight(faces,center+axis*t,out float h))hits.Add(new Vector2(t,h));float lo=hits.First().x,hi=hits.Last().x;
        // Fit the shallow sag and lift only enough to clear the individual plank seams.
        double[,] a=new double[3,4];foreach(var p in hits){double[] q={p.x*p.x,p.x,1};for(int r=0;r<3;r++){for(int c=0;c<3;c++)a[r,c]+=q[r]*q[c];a[r,3]+=q[r]*p.y;}}
        for(int r=0;r<3;r++){double divisor=a[r,r];for(int c=r;c<4;c++)a[r,c]/=divisor;for(int k=0;k<3;k++)if(k!=r){double f=a[k,r];for(int c=r;c<4;c++)a[k,c]-=f*a[r,c];}}
        float Curve(float t)=>(float)(a[0,3]*t*t+a[1,3]*t+a[2,3]);float lift=hits.Max(p=>p.y-Curve(p.x))+.018f;
        float sampleT=hits.OrderBy(p=>Mathf.Abs(p.x)).First().x;SurfaceHeight(faces,center+axis*sampleT,out float sampleY);float left=0,right=0;
        for(float s=.05f;s<3;s+=.05f){if(!SurfaceHeight(faces,center+axis*sampleT-side*s,out float y)||Mathf.Abs(y-sampleY)>.15f)break;left=s;}
        for(float s=.05f;s<3;s+=.05f){if(!SurfaceHeight(faces,center+axis*sampleT+side*s,out float y)||Mathf.Abs(y-sampleY)>.15f)break;right=s;}
        float width=Mathf.Clamp(2*Mathf.Min(left,right)-.08f,1.4f,2.4f);
        foreach(var filter in bridge.GetComponentsInChildren<MeshFilter>()){Undo.RecordObject(filter.gameObject,"Bridge environment collision layer");filter.gameObject.layer=7;PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);if(filter.GetComponent<MeshCollider>()==null)Undo.AddComponent<MeshCollider>(filter.gameObject).sharedMesh=filter.sharedMesh;}
        const string texRoot="Assets/Assets_Imported/stylized-bridge/textures/";AssetDatabase.CopyAsset(texRoot+"T_Bridge_Normal.png",Folder+"/Bridge_Normal.png");var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Bridge_Normal.png");importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;importer.SaveAndReimport();
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Bridge_Original_Texture_URP"};material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texRoot+"T_Bridge_BaseColor.png"));material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Bridge_Normal.png"));material.SetFloat("_Smoothness",.15f);BaseShaderGUI.SetMaterialKeywords(material);AssetDatabase.CreateAsset(material,Folder+"/Bridge_Original_Texture_URP.mat");
        foreach(var renderer in bridge.GetComponentsInChildren<Renderer>()){allowed.Add(renderer);Undo.RecordObject(renderer,"Apply bridge's original texture atlas");renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>material).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        Physics.SyncTransforms();var nodes=new List<Vector3>();for(float t=lo;t<hi;t+=.25f){var p=center+axis*t;p.y=Curve(t)+lift;nodes.Add(p);}var end=center+axis*hi;end.y=Curve(hi)+lift;nodes.Add(end);MeshStrip("Bridge_Continuous_Walk_Surface",root.transform,nodes.ToArray(),width,false,null);
        var approachMat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Bridge_Landing_Wood"};approachMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StylizedDarkCastle/Textures/tex_wood.png"));approachMat.SetColor("_BaseColor",new Color(.62f,.6f,.58f,1));approachMat.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(approachMat,Folder+"/Bridge_Landing_Wood.mat");
        var start=center+axis*(lo-2.2f);start.y=PhysicalHeight(start)+.018f;var finish=center+axis*(hi+2.2f);finish.y=PhysicalHeight(finish)+.018f;
        MeshStrip("Bridge_West_Landing",root.transform,new[]{start,nodes.First()},width,true,approachMat);MeshStrip("Bridge_East_Landing",root.transform,new[]{nodes.Last(),finish},width,true,approachMat);
        var sf=TopFaces(stairs);var sa=Axis(stairs,sf,true);var sc=BoundsOf(stairs).center;var startStair=sc-sa*7;startStair.y=terrain.SampleHeight(startStair)+terrain.transform.position.y;var endStair=sc+sa*12;endStair.y=PhysicalHeight(endStair);
        Vector3 IslandCenter(int i){var b=BoundsOf(islands[i].gameObject);return new Vector3(b.center.x,b.max.y,b.center.z);}
        var plan=new RoutePlan{backup=backup,width=width,bridge=new[]{start,nodes.First(),center+Vector3.up*(Curve(0)+lift-center.y),nodes.Last(),finish},stairs=new[]{startStair,endStair},route=new[]{startStair,endStair,IslandCenter(0),start,finish,IslandCenter(1),IslandCenter(2)}};
        File.WriteAllText(Folder+"/RoutePlan.json",JsonUtility.ToJson(plan,true));AssetDatabase.ImportAsset(Folder+"/RoutePlan.json");
        log.AppendLine("Bridge visible deck extent="+lo.ToString("F2")+".."+hi.ToString("F2")+" width="+width.ToString("F2")+" collider lift="+lift.ToString("F3"));log.AppendLine("Landings "+start.ToString("F3")+" -> "+finish.ToString("F3"));
        allowed.Add(nav);Physics.SyncTransforms();nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,Folder+"/CastleRouteNavigation.asset");EditorUtility.SetDirty(nav);AssetDatabase.SaveAssets();
        int changed=0;foreach(var p in components)if(p.Key!=null&&!allowed.Contains(p.Key)&&EditorJsonUtility.ToJson(p.Key)!=p.Value){changed++;log.AppendLine("FAIL unexpected component change "+p.Key.name+" "+p.Key.GetType().Name);}
        log.AppendLine((changed==0?"PASS":"FAIL")+" existing layout, player settings, terrain and other components preserved");foreach(var p in oldAssets)log.AppendLine((Digest(p.Key)==p.Value?"PASS":"FAIL")+" source bytes unchanged "+p.Key);
        File.WriteAllText("Logs/CastleRouteBuild.txt",log.ToString());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("Route collider repair and bridge textures applied. Ready for actual-player test.");
    }
}
#endif
