#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

public static partial class DemonCliffSetup
{
    private static readonly Vector2[] Basin={V(11,43),V(17,32),V(33,28),V(76,28),V(94,35),V(97,54),V(96,82),V(88,98),V(47,98),V(34,88),V(27,70),V(14,60)};
    private static readonly Vector2[][] Floors={
        new[]{V(20,41),V(26,36),V(41,36),V(49,42),V(50,52),V(45,60),V(33,63),V(22,57),V(18,51),V(18,46)},
        new[]{V(55,44),V(61,39),V(78,39),V(78,47),V(86,47),V(90,52),V(88,61),V(80,65),V(66,65),V(57,64),V(54,57)},
        new[]{V(43,72),V(49,65),V(56,65),V(56,73),V(65,73),V(65,65),V(73,69),V(83,73),V(85,85),V(79,96),V(49,96),V(42,86)}
    };
    private static readonly float[] Heights={9.8f,14.8f,20.1f};
    private static readonly Vector3[] LowBridge={P(45,9.8f,41),P(50,9.8f,34),P(64,9.8f,32),P(78,9.8f,32),P(84,9.8f,34)};
    private static readonly List<Vector3[]> walkPaths=new List<Vector3[]>();
    private const float PathWidth=6.5f;
    private static Transform buildRoot,walkRoot,guardRoot,decorRoot;
    private static Material floorMaterial,sideMaterial,rimMaterial,ashMaterial,glowMaterial;
    private static Vector2 V(float x,float z)=>new Vector2(x,z);
    private static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
    private static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    private static Transform Group(string name,Transform parent)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    private static bool Inside(Vector2 p,Vector2[] poly)
    {
        bool inside=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)inside=!inside;return inside;
    }
    private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
    {var ab=b-a;float t=ab.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude):0;return Vector2.Distance(p,a+ab*t);}
    private static float OutlineDistance(Vector2 p)
    {float distance=float.MaxValue;for(int i=0;i<Basin.Length;i++)distance=Mathf.Min(distance,SegmentDistance(p,Basin[i],Basin[(i+1)%Basin.Length]));return distance;}
    private static List<int> Triangulate(Vector2[] poly)
    {
        float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        var remaining=Enumerable.Range(0,poly.Length).ToList();var output=new List<int>();
        float area=0;for(int i=0;i<poly.Length;i++)area+=poly[i].x*poly[(i+1)%poly.Length].y-poly[(i+1)%poly.Length].x*poly[i].y;
        if(area<0)remaining.Reverse();int safety=0;
        while(remaining.Count>2&&safety++<poly.Length*poly.Length)
        {
            bool found=false;
            for(int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];if(Cross(poly[a],poly[b],poly[c])<=.00001f)continue;
                bool occupied=remaining.Any(q=>q!=a&&q!=b&&q!=c&&Cross(poly[a],poly[b],poly[q])>=-.00001f&&Cross(poly[b],poly[c],poly[q])>=-.00001f&&Cross(poly[c],poly[a],poly[q])>=-.00001f);
                if(occupied)continue;output.Add(c);output.Add(b);output.Add(a);remaining.RemoveAt(i);found=true;break;
            }
            if(!found)throw new InvalidOperationException("Cannot triangulate approach polygon.");
        }
        return output;
    }
    private static MeshRenderer MeshObject(string name,Transform parent,List<Vector3> world,List<int> triangles,Material material,bool collider)
    {
        var go=Group(name,parent).gameObject;go.layer=7;Vector3 origin=world.Aggregate(Vector3.zero,(a,p)=>a+p)/world.Count;go.transform.position=origin;
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(world.Select(p=>p-origin).ToList());mesh.SetTriangles(triangles,0);
        mesh.SetUVs(0,world.Select(p=>new Vector2(p.x*.22f,p.z*.22f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);return renderer;
    }
    private static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
    private static void Deck(int stage)
    {
        var poly=Floors[stage];float y=Heights[stage];
        MeshObject("Stage_"+(stage+1)+"_Walkable_Stone",walkRoot,poly.Select(p=>P(p.x,y,p.y)).ToList(),Triangulate(poly),floorMaterial,true);
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<poly.Length;i++)
        {
            Vector2 a=poly[i],b=poly[(i+1)%poly.Length];
            Quad(vertices,triangles,P(a.x,y,a.y),P(b.x,y,b.y),P(b.x,3.5f,b.y),P(a.x,3.5f,a.y));
        }
        var sides=MeshObject("Stage_"+(stage+1)+"_Basalt_Cliff_Faces",decorRoot,vertices,triangles,sideMaterial,true);
        sides.GetComponent<MeshCollider>().convex=false;
    }
    private static Vector3[] SmoothFlat(Vector3[] nodes)
    {
        var result=new List<Vector3>();
        for(int i=0;i<nodes.Length-1;i++)
        {
            var a=nodes[Mathf.Max(0,i-1)];var b=nodes[i];var c=nodes[i+1];var d=nodes[Mathf.Min(nodes.Length-1,i+2)];int steps=Mathf.CeilToInt(Vector3.Distance(b,c));
            for(int j=0;j<steps;j++){float t=(float)j/steps;result.Add(.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
        }
        result.Add(nodes.Last());return result.ToArray();
    }
    private static void Ribbon(string name,Vector3[] nodes,bool guards=true,bool navigation=true)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var left=new List<Vector3>();var right=new List<Vector3>();
        for(int i=0;i<nodes.Length;i++)
        {
            var tangent=Vector3.ProjectOnPlane(nodes[Mathf.Min(i+1,nodes.Length-1)]-nodes[Mathf.Max(0,i-1)],Vector3.up).normalized;
            var normal=Vector3.Cross(Vector3.up,tangent)*PathWidth*.5f;left.Add(nodes[i]-normal);right.Add(nodes[i]+normal);
        }
        for(int i=0;i<nodes.Length-1;i++)
        {
            Quad(vertices,triangles,left[i],left[i+1],right[i+1],right[i]);
            Quad(vertices,triangles,left[i]-Vector3.up*.5f,left[i+1]-Vector3.up*.5f,left[i+1],left[i]);
            Quad(vertices,triangles,right[i],right[i+1],right[i+1]-Vector3.up*.5f,right[i]-Vector3.up*.5f);
        }
        MeshObject(name,walkRoot,vertices,triangles,navigation?floorMaterial:ashMaterial,navigation);
        if(!guards)return;
        for(int i=0;i<nodes.Length-1;i++)
        {
            bool OnDeck(Vector3 p)=>Enumerable.Range(0,Floors.Length).Any(stage=>Mathf.Abs(p.y-Heights[stage])<.25f&&Inside(XZ(p),Floors[stage]));
            if(!OnDeck((left[i]+left[i+1])*.5f))Guard(name+"_L_"+i,left[i],left[i+1]);
            if(!OnDeck((right[i]+right[i+1])*.5f))Guard(name+"_R_"+i,right[i],right[i+1]);
        }
    }
    private static readonly List<Vector3> rimVertices=new List<Vector3>();
    private static readonly List<int> rimTriangles=new List<int>();
    private static void Guard(string name,Vector3 a,Vector3 b)
    {
        Vector3 direction=b-a;float length=direction.magnitude;if(length<.05f)return;
        var go=Group(name,guardRoot).gameObject;go.layer=7;go.transform.position=(a+b)*.5f+Vector3.up*.75f;
        go.transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);var box=go.AddComponent<BoxCollider>();box.size=new Vector3(.55f,1.5f,length+.08f);
        Vector3 side=Vector3.Cross(Vector3.up,direction).normalized*.36f;Vector3 lift=Vector3.up*1.4f;
        Quad(rimVertices,rimTriangles,a-side,b-side,b-side+lift,a-side+lift);Quad(rimVertices,rimTriangles,b+side,a+side,a+side+lift,b+side+lift);
        Quad(rimVertices,rimTriangles,a-side+lift,b-side+lift,b+side+lift,a+side+lift);
    }
    private static void DeckGuards(int stage)
    {
        var poly=Floors[stage];float y=Heights[stage];
        for(int i=0;i<poly.Length;i++)
        {
            Vector3 a=P(poly[i].x,y,poly[i].y),b=P(poly[(i+1)%poly.Length].x,y,poly[(i+1)%poly.Length].y);int count=Mathf.CeilToInt(Vector3.Distance(a,b)/1.4f);
            for(int j=0;j<count;j++)
            {
                Vector3 start=Vector3.Lerp(a,b,(float)j/count),end=Vector3.Lerp(a,b,(float)(j+1)/count),mid=(start+end)*.5f;bool open=false;
                foreach(var path in walkPaths)for(int k=1;k<path.Length;k++)
                {
                    var ab=path[k]-path[k-1];float t=Mathf.Clamp01(Vector2.Dot(XZ(mid-path[k-1]),XZ(ab))/Mathf.Max(.001f,XZ(ab).sqrMagnitude));
                    float pathY=Mathf.Lerp(path[k-1].y,path[k].y,t);
                    if(SegmentDistance(XZ(mid),XZ(path[k-1]),XZ(path[k]))<PathWidth*.5f+.75f&&pathY>y-.7f&&pathY<y+2f)open=true;
                }
                if(!open)Guard("Stage_"+(stage+1)+"_Ledge_"+i+"_"+j,start,end);
            }
        }
    }
}
#endif
