using UnityEngine;
using UnityEngine.AI;

public enum EncounterBarrierMotion { Slide,Swing }

/// <summary>Moves only the named door/stalagmites. Permanent boundary walls keep their collision.</summary>
[DisallowMultipleComponent]
public sealed class EncounterBarrier:MonoBehaviour
{
    [SerializeField] private DemonArenaEncounter prerequisite;
    [SerializeField] private Transform movingPart;
    [SerializeField] private EncounterBarrierMotion motion;
    [SerializeField] private Vector3 slideOffset=new Vector3(0,-7,0);
    [SerializeField] private Vector3 swingEuler=new Vector3(0,105,0);
    [SerializeField,Min(.1f)] private float duration=2;
    [SerializeField,Range(0,1)] private float releaseCollisionAt=.9f;
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private NavMeshObstacle[] navigationObstacles;
    [SerializeField] private bool hideWhenOpen;
    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private bool requested;
    private float progress;
    public bool Unlocked=>requested||prerequisite!=null&&prerequisite.Completed;
    public bool IsOpen=>progress>=1;
    public float OpeningProgress=>progress;
    public Transform MovingPart=>movingPart;
    public DemonArenaEncounter Prerequisite=>prerequisite;
    public Collider[] BlockingColliders=>blockingColliders;
    private void Awake()
    {
        if(movingPart==null)movingPart=transform;
        closedPosition=movingPart.localPosition;closedRotation=movingPart.localRotation;
    }
    private void OnEnable(){if(prerequisite!=null)prerequisite.Changed+=OnEncounterChanged;}
    private void OnDisable(){if(prerequisite!=null)prerequisite.Changed-=OnEncounterChanged;}
    private void OnEncounterChanged(){if(prerequisite!=null&&prerequisite.Completed)requested=true;}
    private void Update()
    {
        if(!Unlocked||progress>=1||Time.deltaTime<=0)return;
        requested=true;progress=Mathf.MoveTowards(progress,1,Time.deltaTime/duration);
        float eased=Mathf.SmoothStep(0,1,progress);
        if(motion==EncounterBarrierMotion.Slide)movingPart.localPosition=closedPosition+slideOffset*eased;
        else movingPart.localRotation=closedRotation*Quaternion.Euler(swingEuler*eased);
        if(progress>=releaseCollisionAt)
        {
            foreach(var c in blockingColliders)if(c!=null)c.enabled=false;
            foreach(var obstacle in navigationObstacles)if(obstacle!=null)obstacle.enabled=false;
        }
        if(progress>=1&&hideWhenOpen)movingPart.gameObject.SetActive(false);
    }
    private void OnDrawGizmosSelected()
    {
        if(movingPart==null)return;Gizmos.color=new Color(.25f,1,.5f,.8f);
        if(motion==EncounterBarrierMotion.Slide)Gizmos.DrawLine(movingPart.position,movingPart.position+(movingPart.parent!=null?movingPart.parent.TransformVector(slideOffset):slideOffset));
    }
}
