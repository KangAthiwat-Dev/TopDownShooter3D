#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static partial class CastleRouteRepair
{
    private const string Folder="Assets/_Project/World/CastleRouteRepair";
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static GameObject Find(string name)=>UnityEngine.Object.FindObjectsByType<Transform>().First(t=>t.name==name).gameObject;
    private static Bounds BoundsOf(GameObject go)
    {var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    private struct Face{public Vector3 a,b,c;}
    private static List<Face> TopFaces(GameObject go)
    {
        var output=new List<Face>();foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;var verts=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){var a=filter.transform.TransformPoint(verts[triangles[i]]);var b=filter.transform.TransformPoint(verts[triangles[i+1]]);var c=filter.transform.TransformPoint(verts[triangles[i+2]]);var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y>.65f)output.Add(new Face{a=a,b=b,c=c});}
        }return output;
    }
    private static Vector3 Axis(GameObject go,List<Face> faces,bool stairs)
    {
        var points=faces.SelectMany(f=>new[]{f.a,f.b,f.c}).ToArray();var mean=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;double xx=0,zz=0,xz=0,xy=0,zy=0;
        foreach(var p in points){var d=p-mean;xx+=d.x*d.x;zz+=d.z*d.z;xz+=d.x*d.z;xy+=d.x*d.y;zy+=d.z*d.y;}
        if(stairs){double det=xx*zz-xz*xz;return new Vector3((float)((xy*zz-zy*xz)/det),0,(float)((zy*xx-xy*xz)/det)).normalized;}
        float angle=.5f*Mathf.Atan2((float)(2*xz),(float)(xx-zz));return new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
    }
    private static bool SurfaceHeight(List<Face> faces,Vector3 p,out float y)
    {
        y=float.NegativeInfinity;bool hit=false;
        foreach(var f in faces)
        {
            float det=(f.b.z-f.c.z)*(f.a.x-f.c.x)+(f.c.x-f.b.x)*(f.a.z-f.c.z);if(Mathf.Abs(det)<.00001f)continue;
            float u=((f.b.z-f.c.z)*(p.x-f.c.x)+(f.c.x-f.b.x)*(p.z-f.c.z))/det,v=((f.c.z-f.a.z)*(p.x-f.c.x)+(f.a.x-f.c.x)*(p.z-f.c.z))/det;
            if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;y=Mathf.Max(y,u*f.a.y+v*f.b.y+(1-u-v)*f.c.y);hit=true;
        }return hit;
    }
    [MenuItem("Tools/World/Castle Route/Inspect User Stairs and Bridge")]
    public static void Inspect()
    {
        var log=new StringBuilder("User route collision survey "+DateTime.Now.ToString("s")+"\n");var cc=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().GetComponent<CharacterController>();log.AppendLine("PLAYER radius="+cc.radius+" height="+cc.height+" center="+cc.center+" step="+cc.stepOffset+" slope="+cc.slopeLimit+" layer="+cc.gameObject.layer);
        var terrain=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>().terrain;log.AppendLine("TERRAIN "+AssetDatabase.GetAssetPath(terrain.terrainData));
        foreach(string name in new[]{"PlatformStair_001","SM_bridge"})
        {
            var go=Find(name);var bounds=BoundsOf(go);var faces=TopFaces(go);var axis=Axis(go,faces,name.Contains("Stair"));var center=bounds.center;float min=faces.SelectMany(f=>new[]{f.a,f.b,f.c}).Min(p=>Vector3.Dot(p-center,axis)),max=faces.SelectMany(f=>new[]{f.a,f.b,f.c}).Max(p=>Vector3.Dot(p-center,axis));
            log.AppendLine("TARGET "+PathOf(go.transform)+" source="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)+" position="+go.transform.position.ToString("F3")+" rotation="+go.transform.eulerAngles.ToString("F2")+" scale="+go.transform.lossyScale.ToString("F3")+" bounds="+bounds+" axis="+axis.ToString("F4")+" extents="+min.ToString("F3")+".."+max.ToString("F3"));
            foreach(var r in go.GetComponentsInChildren<Renderer>())log.AppendLine("RENDERER "+r.name+" mesh="+AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh)+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name+":"+m.shader.name+":"+AssetDatabase.GetAssetPath(m))));
            foreach(var c in go.GetComponentsInChildren<Collider>())log.AppendLine("COLLIDER "+PathOf(c.transform)+" "+c.GetType().Name+" enabled="+c.enabled+" trigger="+c.isTrigger+" layer="+c.gameObject.layer+" bounds="+c.bounds);
            for(float t=min-4;t<max+4;t+=.4f)
            {
                var p=center+axis*t;bool surface=SurfaceHeight(faces,p,out float top);float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                var hits=Physics.RaycastAll(p+Vector3.up*80,Vector3.down,200,~0,QueryTriggerInteraction.Ignore).OrderByDescending(h=>h.point.y).Take(4).Select(h=>PathOf(h.transform)+"@"+h.point.y.ToString("F3")+"/slope"+Vector3.Angle(h.normal,Vector3.up).ToString("F1"));
                log.AppendLine("PROFILE t="+t.ToString("F2")+" xz="+p.x.ToString("F2")+","+p.z.ToString("F2")+" visual="+(surface?top.ToString("F3"):"none")+" terrain="+ground.ToString("F3")+" hits="+string.Join(" | ",hits));
            }
        }
        foreach(var nav in UnityEngine.Object.FindObjectsByType<NavMeshSurface>())log.AppendLine("NAV "+PathOf(nav.transform)+" asset="+AssetDatabase.GetAssetPath(nav.navMeshData)+" mask="+nav.layerMask.value+" geo="+nav.useGeometry);
        foreach(var c in UnityEngine.Object.FindObjectsByType<CapsuleCollider>())if(c.transform.position.x>-25&&c.transform.position.z>20)
        {
            log.AppendLine("CAPSULE "+PathOf(c.transform)+" position="+c.transform.position.ToString("F3")+" rotation="+c.transform.eulerAngles+" scale="+c.transform.lossyScale+" radius="+c.radius+" height="+c.height+" direction="+c.direction+" collider bounds="+c.bounds+" visual bounds="+BoundsOf(c.gameObject));
            var fs=TopFaces(c.gameObject);log.AppendLine("VISUAL TOP faces="+fs.Count+" min="+fs.Min(f=>Mathf.Min(f.a.y,Mathf.Min(f.b.y,f.c.y)))+" max="+fs.Max(f=>Mathf.Max(f.a.y,Mathf.Max(f.b.y,f.c.y))));
        }
        foreach(var box in UnityEngine.Object.FindObjectsByType<BoxCollider>())if(box.name.Contains("Boundary")||box.name.Contains("Block"))log.AppendLine("BLOCKER "+PathOf(box.transform)+" bounds="+box.bounds);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleRouteInspection.txt",log.ToString());Debug.Log("Castle route inspection: Logs/CastleRouteInspection.txt");
    }
}
#endif
