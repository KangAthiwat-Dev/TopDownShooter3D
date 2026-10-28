using UnityEngine;

/// <summary>Recorded walking loops selected by the actual supporting collider, independent of map zones.</summary>
[DisallowMultipleComponent,RequireComponent(typeof(PlayerMovement)),DefaultExecutionOrder(60)]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    [SerializeField] private AudioSource outside,inside;
    [SerializeField,Range(0,1)] private float volume=.24f;
    [SerializeField,Min(.01f)] private float fadeDuration=.1f;
    private PlayerMovement movement;
    private CharacterController controller;
    private PlayerHealth health;
    private PlayerInputGate gate;
    private Vector3 previousPosition;
    private readonly RaycastHit[] groundHits=new RaycastHit[32];
    public Collider SupportingCollider {get;private set;}
    public bool IsOutside=>SupportingCollider is TerrainCollider;
    public bool IsAudible=>outside!=null&&outside.isPlaying||inside!=null&&inside.isPlaying;
    private void Awake()
    {movement=GetComponent<PlayerMovement>();controller=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();gate=GetComponent<PlayerInputGate>();}
    private void OnEnable(){previousPosition=transform.position;if(health!=null)health.Died+=Stop;}
    private void OnDisable(){if(health!=null)health.Died-=Stop;Stop();}
    private void LateUpdate()
    {
        Vector3 delta=transform.position-previousPosition;previousPosition=transform.position;delta.y=0;
        if(Time.deltaTime<=0||movement==null||!movement.isActiveAndEnabled||!controller.enabled||!controller.isGrounded||
            movement.IsDashing||movement.IsShadowDashing||movement.IsInteractionLocked||health!=null&&!health.IsAlive||gate!=null&&gate.BlocksGameplay)
        {SupportingCollider=null;Stop();return;}
        float speed=delta.magnitude/Time.deltaTime;
        // Ignore teleports, dash end frames and turning in place. Wall-pushing has no travelled distance.
        bool moving=speed>.2f&&speed<=movement.SprintSpeed*1.25f;
        SupportingCollider=moving?FindSupport():null;
        float pitch=Mathf.Lerp(.9f,1.5f,Mathf.InverseLerp(movement.WalkSpeed,movement.SprintSpeed,speed));
        UpdateLoop(outside,moving&&IsOutside?volume:0,pitch);
        UpdateLoop(inside,moving&&SupportingCollider!=null&&!IsOutside?volume:0,pitch);
    }
    private Collider FindSupport()
    {
        Bounds bounds=controller.bounds;Vector3 origin=new Vector3(bounds.center.x,bounds.min.y+.3f,bounds.center.z);
        int count=Physics.SphereCastNonAlloc(origin,controller.radius*.55f,Vector3.down,groundHits,.75f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        Collider result=null;float closest=float.MaxValue;
        for(int i=0;i<count;i++)
        {
            var hit=groundHits[i];if(hit.collider==null||hit.normal.y<.5f||hit.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<IDamageable>()!=null)continue;
            if(hit.distance<closest){closest=hit.distance;result=hit.collider;}
        }
        return result;
    }
    private void UpdateLoop(AudioSource source,float target,float pitch)
    {
        if(source==null||source.clip==null)return;
        source.pitch=pitch;source.volume=Mathf.MoveTowards(source.volume,target,volume/Mathf.Max(.01f,fadeDuration)*Time.deltaTime);
        if(target>0&&!source.isPlaying)source.Play();
        else if(target<=0&&source.volume<=.001f)source.Stop();
    }
    private void Stop(){if(outside!=null){outside.Stop();outside.volume=0;}if(inside!=null){inside.Stop();inside.volume=0;}}
}
