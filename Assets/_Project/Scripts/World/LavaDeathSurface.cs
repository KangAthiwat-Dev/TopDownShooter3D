using System.Collections.Generic;
using UnityEngine;

/// <summary>Samples the real liquid mesh without adding walkable collision to lava.</summary>
[RequireComponent(typeof(MeshFilter))]
public sealed class LavaDeathSurface : MonoBehaviour
{
    [SerializeField, Min(0)] private float contactHeight = .12f;
    [SerializeField, Min(.25f)] private float cellSize = 2;
    private Vector3[] vertices;
    private int[] triangles;
    private readonly Dictionary<Vector2Int,List<int>> cells = new Dictionary<Vector2Int,List<int>>();
    private PlayerHealth player;
    private float nextCheck;
    private void Awake()
    {
        var mesh=GetComponent<MeshFilter>().sharedMesh;
        if(mesh==null)return;
        vertices=mesh.vertices;triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
            int minX=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/cellSize),maxX=Mathf.FloorToInt(Mathf.Max(a.x,b.x,c.x)/cellSize);
            int minZ=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/cellSize),maxZ=Mathf.FloorToInt(Mathf.Max(a.z,b.z,c.z)/cellSize);
            for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            {var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();list.Add(i);}
        }
    }
    public bool TrySurfaceHeight(Vector3 worldPoint, out float height)
    {
        height=float.NegativeInfinity;
        var point=transform.InverseTransformPoint(worldPoint);
        if(!cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x/cellSize),Mathf.FloorToInt(point.z/cellSize)),out var list))return false;
        bool found=false;
        foreach(int i in list)
        {
            var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
            float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Mathf.Abs(denominator)<.000001f)continue;
            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/denominator;
            float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/denominator;
            if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
            float y=u*a.y+v*b.y+(1-u-v)*c.y;
            height=Mathf.Max(height,transform.TransformPoint(new Vector3(point.x,y,point.z)).y);found=true;
        }
        return found;
    }
    public bool IsLethalAt(Vector3 feet) => TrySurfaceHeight(feet,out float height)&&feet.y<=height+contactHeight;
    private void Update()
    {
        if(Time.timeScale<=0||Time.time<nextCheck)return;nextCheck=Time.time+.05f;
        if(player==null){var p=GameObject.FindGameObjectWithTag("Player");if(p!=null)player=p.GetComponent<PlayerHealth>();}
        if(player==null||!player.IsAlive)return;
        var controller=player.GetComponent<CharacterController>();
        var feet=player.transform.position;
        if(controller!=null&&controller.enabled)feet.y=controller.bounds.min.y;
        if(IsLethalAt(feet))player.KillFromLava();
    }
}
