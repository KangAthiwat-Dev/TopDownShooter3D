using System;
using UnityEngine;

/// <summary>Editable outdoor approach layout. Encounter locks and scene travel are reserved, not active.</summary>
[DisallowMultipleComponent]
public sealed class DemonCliffZone : MonoBehaviour
{
    [Serializable] public sealed class Stage
    {
        public string name;
        public Transform center;
        public float arenaRadius=8f;
        public Transform[] monsterSpawns;
        public Transform futureGate;
    }
    public Stage[] stages;
    public Transform entrance,castleEntrance,castleFootprint;
    public Vector2 castleFootprintSize=new Vector2(28,12);
    public Transform[] travelRoute;
    public Vector2[] outline;
    public float lavaHeight=4.25f;
    public string displayName="DEMON CLIFFS";
    public bool Contains(Vector3 position)
    {
        if(outline==null||outline.Length<3)return false;
        bool inside=false;
        for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
        {
            var a=outline[i];var b=outline[j];
            if((a.y>position.z)!=(b.y>position.z)&&position.x<(b.x-a.x)*(position.z-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
    private void OnDrawGizmosSelected()
    {
        if(stages!=null)foreach(var stage in stages)
        {
            if(stage.center==null)continue;
            Gizmos.color=new Color(1,.45f,.1f);Gizmos.DrawWireSphere(stage.center.position+Vector3.up,stage.arenaRadius);
            if(stage.futureGate!=null){Gizmos.color=Color.red;Gizmos.DrawWireCube(stage.futureGate.position+Vector3.up*1.5f,new Vector3(6,3,.3f));}
            if(stage.monsterSpawns!=null)foreach(var spawn in stage.monsterSpawns)if(spawn!=null)Gizmos.DrawWireSphere(spawn.position,.5f);
        }
        if(travelRoute!=null){Gizmos.color=Color.cyan;for(int i=1;i<travelRoute.Length;i++)if(travelRoute[i-1]!=null&&travelRoute[i]!=null)Gizmos.DrawLine(travelRoute[i-1].position+Vector3.up*.3f,travelRoute[i].position+Vector3.up*.3f);}
        if(castleFootprint!=null){Gizmos.color=new Color(.8f,.4f,1);Gizmos.DrawWireCube(castleFootprint.position+Vector3.up*4,new Vector3(castleFootprintSize.x,8,castleFootprintSize.y));}
        if(castleEntrance!=null)Gizmos.DrawRay(castleEntrance.position,castleEntrance.forward*3);
    }
}
