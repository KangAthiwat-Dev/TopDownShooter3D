using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)),RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    [SerializeField,Min(.1f)] private float lifetime=3;
    [SerializeField] private RangedBurstVfx impactVfx;
    [SerializeField] private RangedBurstVfx explosionVfx;
    private Rigidbody body;
    private SphereCollider sphere;
    private GameObject owner;
    private float damage,expiresAt,blastRadius,blastRatio;
    private int hitsRemaining=1;
    private bool hasHit,initialized;
    private Vector3 previousPosition;
    private PlayerCombatSkills skills;
    private CombatHitSettings hitSettings;
    private readonly HashSet<EnemyHealth> damaged=new HashSet<EnemyHealth>();
    private readonly RaycastHit[] sweepHits=new RaycastHit[64];
    private readonly Collider[] areaHits=new Collider[128];
    public GameObject Owner=>owner;
    public float Damage=>damage;
    public float BlastRadius=>blastRadius;
    public int HitsRemaining=>hitsRemaining;
    private void Awake(){body=GetComponent<Rigidbody>();sphere=GetComponent<SphereCollider>();}
    public void Initialize(Vector3 direction,float speed,float damageAmount,GameObject projectileOwner)
    {
        owner=projectileOwner;damage=damageAmount;hasHit=false;initialized=true;damaged.Clear();hitsRemaining=1;blastRadius=0;skills=null;
        previousPosition=transform.position;expiresAt=Time.time+lifetime;body.linearVelocity=direction.normalized*speed;
    }
    public void ConfigureSkills(PlayerCombatSkills caster,int extraTargets,float radius,float ratio,CombatHitSettings settings)
    {skills=caster;hitsRemaining=1+Mathf.Clamp(extraTargets,0,3);blastRadius=Mathf.Max(0,radius);blastRatio=Mathf.Clamp01(ratio);hitSettings=settings;}
    private void Update(){if(initialized&&!hasHit&&Time.time>=expiresAt)Finish(transform.position,-transform.forward);}
    private void FixedUpdate()
    {SweepToCurrent();}
    private void SweepToCurrent()
    {
        if(!initialized||hasHit)return;
        Vector3 current=body.position,delta=current-previousPosition;
        if(delta.sqrMagnitude>.000001f)
        {
            float radius=sphere.radius*Mathf.Max(transform.lossyScale.x,transform.lossyScale.y,transform.lossyScale.z);
            int count=Physics.SphereCastNonAlloc(previousPosition,Mathf.Max(.015f,radius),delta.normalized,sweepHits,delta.magnitude,Physics.AllLayers,QueryTriggerInteraction.Collide);
            // A wall before an enemy must win, even within one fast physics step.
            for(int i=1;i<count;i++){var item=sweepHits[i];int j=i-1;while(j>=0&&sweepHits[j].distance>item.distance){sweepHits[j+1]=sweepHits[j];j--;}sweepHits[j+1]=item;}
            for(int i=0;i<count&&!hasHit;i++)Contact(sweepHits[i].collider,sweepHits[i].point,sweepHits[i].normal);
        }
        previousPosition=current;
    }
    private void OnTriggerEnter(Collider other)
    {
        if(!initialized||hasHit)return;
        SweepToCurrent();
        if(!hasHit)Contact(other,other.ClosestPoint(transform.position),body.linearVelocity.sqrMagnitude>.0001f?-body.linearVelocity.normalized:-transform.forward);
    }
    private void Contact(Collider other,Vector3 point,Vector3 normal)
    {
        if(other==null||hasHit||other.GetComponentInParent<Projectile>()==this||owner!=null&&other.transform.IsChildOf(owner.transform))return;
        var enemy=other.GetComponentInParent<EnemyHealth>();
        // A lethal pierced hit may leave other queued hurtbox callbacks this
        // physics step. That newly dead body must not become a wall to this bullet.
        if(enemy!=null&&damaged.Contains(enemy))return;
        if(enemy!=null&&enemy.IsAlive)
        {
            if(!damaged.Add(enemy))return;
            if(skills!=null)skills.DealDamage(enemy,damage,hitSettings);else enemy.TakeDamage(damage,owner);
            hitsRemaining--;
            if(hitsRemaining<=0)Finish(point,normal);else ShowImpact(point,normal);
            return;
        }
        var damageable=other.GetComponentInParent<IDamageable>();
        if(damageable!=null&&damageable.IsAlive)
        {damageable.TakeDamage(damage);Finish(point,normal);return;}
        if(!other.isTrigger)Finish(point,normal);
    }
    private void Finish(Vector3 point,Vector3 normal)
    {
        if(hasHit)return;hasHit=true;
        if(blastRadius>0)
        {
            Vector3 center=point+normal*.06f;
            int count=Physics.OverlapSphereNonAlloc(center,blastRadius,areaHits,Physics.AllLayers,QueryTriggerInteraction.Collide);
            Collider[] hits=count==areaHits.Length?Physics.OverlapSphere(center,blastRadius,Physics.AllLayers,QueryTriggerInteraction.Collide):areaHits;
            if(hits!=areaHits)count=hits.Length;
            for(int i=0;i<count;i++)
            {
                var enemy=hits[i].GetComponentInParent<EnemyHealth>();
                if(enemy==null||!enemy.IsAlive||damaged.Contains(enemy)||!CombatLineOfSight.Clear(center,enemy,owner))continue;
                damaged.Add(enemy);if(skills!=null)skills.DealDamage(enemy,damage*blastRatio,hitSettings);else enemy.TakeDamage(damage*blastRatio,owner);
            }
            if(explosionVfx!=null){var burst=Instantiate(explosionVfx,point,Quaternion.identity);burst.transform.localScale*=blastRadius/1.9f;}
        }
        else ShowImpact(point,normal);
        Destroy(gameObject);
    }
    private void ShowImpact(Vector3 point,Vector3 normal)
    {if(impactVfx!=null)Instantiate(impactVfx,point,Quaternion.LookRotation(normal.sqrMagnitude>.0001f?normal:Vector3.up));}
}
