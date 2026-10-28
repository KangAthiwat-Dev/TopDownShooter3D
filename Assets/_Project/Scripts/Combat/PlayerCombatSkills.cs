using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatHitSettings
{
    public readonly float criticalChance, criticalMultiplier, killHealing;
    public CombatHitSettings(float chance,float multiplier,float heal)
    {criticalChance=Mathf.Clamp01(chance);criticalMultiplier=Mathf.Max(1,multiplier);killHealing=Mathf.Max(0,heal);}
}

/// <summary>Effects of the six new upgrades; existing vitality, energy, damage and cadence remain in PlayerUpgradeStats.</summary>
[DefaultExecutionOrder(40),DisallowMultipleComponent,RequireComponent(typeof(PlayerProgressionState))]
public sealed class PlayerCombatSkills : MonoBehaviour
{
    [SerializeField,Range(1,15)] private float spreadDegrees=6;
    [SerializeField,Range(.1f,1)] private float sideBulletDamage=.5f;
    [SerializeField,Range(.1f,1)] private float blastDamageRatio=.65f;
    [SerializeField,Min(.1f)] private float dashRadius=1;
    private PlayerProgressionState state;
    private PlayerHealth health;
    private PlayerMovement movement;
    private PlayerInputGate gate;
    private readonly HashSet<EnemyHealth> dashedEnemies=new HashSet<EnemyHealth>();
    private readonly Collider[] nearby=new Collider[64];
    private Vector3 lastDashPosition;
    private bool trackingDash;
    private CombatHitSettings dashSettings;
    private float activeDashDamage;
    private CharacterController controller;
    private readonly List<Collider> dashPassThrough=new List<Collider>();
    public int Rank(PlayerUpgrade kind)=>state.UpgradeRank(kind);
    public int ProjectilesPerVolley=>1+Rank(PlayerUpgrade.ArcaneBarrage)*2;
    public int ExtraPierceTargets=>Rank(PlayerUpgrade.PiercingShot);
    public float BlastRadius=>Rank(PlayerUpgrade.ExplosiveShot)>0?1.5f+.4f*Rank(PlayerUpgrade.ExplosiveShot):0;
    public float SpreadDegrees=>spreadDegrees;
    public float SideBulletDamage=>sideBulletDamage;
    public float BlastDamageRatio=>blastDamageRatio;
    public float ThunderDamage=>Rank(PlayerUpgrade.ThunderDash)>0?12+6*(Rank(PlayerUpgrade.ThunderDash)-1):0;
    public CombatHitSettings HitSettings=>new CombatHitSettings(Rank(PlayerUpgrade.CriticalStrike)*.05f,2,Rank(PlayerUpgrade.LifeSteal)*2);
    public event Action<EnemyHealth> CriticalHit;
    public event Action<float> LifeStolen;
    public event Action ThunderStarted;
    public event Action<EnemyHealth> ThunderHit;
    private void Awake()
    {state=GetComponent<PlayerProgressionState>();health=GetComponent<PlayerHealth>();movement=GetComponent<PlayerMovement>();gate=GetComponent<PlayerInputGate>();controller=GetComponent<CharacterController>();}
    private void OnEnable(){if(movement!=null)movement.DashStarted+=OnDash;}
    private void OnDisable(){if(movement!=null)movement.DashStarted-=OnDash;trackingDash=false;dashedEnemies.Clear();RestoreCollisions(true);}
    private void OnDash()
    {
        dashedEnemies.Clear();lastDashPosition=transform.position;activeDashDamage=ThunderDamage;dashSettings=HitSettings;
        trackingDash=activeDashDamage>0;
        if(trackingDash)
        {
            // Thunder Dash may pass through monster bodies; scenery still blocks it.
            if(controller!=null)foreach(var collider in Physics.OverlapSphere(transform.position,6,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                if(collider.GetComponentInParent<EnemyHealth>()!=null&&!Physics.GetIgnoreCollision(controller,collider))
                {Physics.IgnoreCollision(controller,collider,true);dashPassThrough.Add(collider);}
            ThunderStarted?.Invoke();
        }
    }
    private void LateUpdate()
    {
        RestoreCollisions(health!=null&&!health.IsAlive);
        if(!trackingDash)return;
        if(health==null||!health.IsAlive||!movement.isActiveAndEnabled){trackingDash=false;return;}
        if(Time.timeScale<=0||(gate!=null&&gate.BlocksGameplay))return;
        Vector3 previous=lastDashPosition,current=transform.position;
        // Sweep the actual travelled segment, including the final frame. Blocked
        // motion cannot damage a target through the wall behind the player.
        int count=Physics.OverlapCapsuleNonAlloc(previous+Vector3.up,current+Vector3.up,dashRadius,nearby,Physics.AllLayers,QueryTriggerInteraction.Collide);
        Collider[] hits=count==nearby.Length?Physics.OverlapCapsule(previous+Vector3.up,current+Vector3.up,dashRadius,Physics.AllLayers,QueryTriggerInteraction.Collide):nearby;
        if(hits!=nearby)count=hits.Length;
        for(int i=0;i<count;i++)
        {
            var enemy=hits[i].GetComponentInParent<EnemyHealth>();
            if(enemy==null||!enemy.IsAlive||dashedEnemies.Contains(enemy)||!CombatLineOfSight.Clear(current+Vector3.up,enemy,gameObject))continue;
            dashedEnemies.Add(enemy);if(DealDamage(enemy,activeDashDamage,dashSettings))ThunderHit?.Invoke(enemy);
        }
        lastDashPosition=current;
        if(!movement.IsDashing)trackingDash=false;
    }
    private void RestoreCollisions(bool force)
    {
        if(controller==null)return;
        for(int i=dashPassThrough.Count-1;i>=0;i--)
        {
            var collider=dashPassThrough[i];
            if(collider==null){dashPassThrough.RemoveAt(i);continue;}
            // If a dash ends inside a large body, restore only after separating,
            // avoiding a one-frame physics shove. Existing ignore rules are preserved.
            if(force||!movement.IsDashing&&!controller.bounds.Intersects(collider.bounds))
            {Physics.IgnoreCollision(controller,collider,false);dashPassThrough.RemoveAt(i);}
        }
    }
    public bool DealDamage(EnemyHealth enemy,float amount,CombatHitSettings settings,bool instantKill=false)
    {
        if(enemy==null||!enemy.IsAlive||enemy.IsInvulnerable||amount<=0)return false;
        bool critical=UnityEngine.Random.value<settings.criticalChance;
        float before=enemy.CurrentHealth;
        float damage=instantKill&&enemy.AllowInstantKill?before:amount*(critical?settings.criticalMultiplier:1);
        enemy.TakeDamage(damage,gameObject);
        if(enemy.CurrentHealth>=before)return false;
        if(critical)CriticalHit?.Invoke(enemy);
        // One heal per actual monster life, never per collider, hit or zero-EXP practice target.
        if(!enemy.IsAlive&&enemy.ExperienceReward>0&&settings.killHealing>0&&health!=null&&health.IsAlive)
        {
            float old=health.CurrentHealth;if(health.TryHeal(settings.killHealing))LifeStolen?.Invoke(health.CurrentHealth-old);
        }
        return true;
    }
    public string Preview(PlayerUpgrade kind)
    {
        int rank=Rank(kind);
        return kind switch
        {
            PlayerUpgrade.ArcaneBarrage=>$"กระสุนต่อครั้ง  {1+rank*2} → {3+rank*2} นัด\nยิงแผ่ด้านหน้า กระสุนข้างมีดาเมจ 50%",
            PlayerUpgrade.PiercingShot=>$"ทะลุศัตรูเพิ่ม  {rank} → {rank+1} ตัว\nกระสุนหยุดเมื่อชนกำแพงหรือสิ่งกีดขวาง",
            PlayerUpgrade.ExplosiveShot=>$"รัศมีระเบิด  {(rank>0?1.5f+.4f*rank:0):0.#} → {1.5f+.4f*(rank+1):0.#} ม.\nดาเมจรอบจุดกระทบ 65% ไม่ทะลุกำแพง",
            PlayerUpgrade.CriticalStrike=>$"โอกาสคริติคอล  {rank*5}% → {(rank+1)*5}%\nกระสุน หมัด ดาบ และพุ่ง ทำดาเมจ 2 เท่า",
            PlayerUpgrade.LifeSteal=>$"ฟื้นเลือดเมื่อสังหาร  {rank*2} → {(rank+1)*2} หน่วย\nใช้ได้กับกระสุน หมัด ดาบ และพุ่ง",
            PlayerUpgrade.ThunderDash=>$"ดาเมจพุ่ง  {(rank>0?12+6*(rank-1):0)} → {12+6*rank}\nSpace พุ่งผ่านศัตรู โดนหนึ่งครั้งต่อการพุ่ง",
            _=>""
        };
    }
    public static string Name(PlayerUpgrade kind)=>kind switch
    {
        PlayerUpgrade.Vitality=>"พลังกาย / เกราะเวท",PlayerUpgrade.Endurance=>"พลังงาน",PlayerUpgrade.Transformation=>"ระยะเวลาแปลงร่าง",
        PlayerUpgrade.Firepower=>"พลังยิง",PlayerUpgrade.FireRate=>"ยิงรวดเร็ว",PlayerUpgrade.ArcaneBarrage=>"กระสุนกระจาย",
        PlayerUpgrade.PiercingShot=>"กระสุนทะลุ",PlayerUpgrade.ExplosiveShot=>"กระสุนระเบิด",PlayerUpgrade.CriticalStrike=>"โจมตีคริติคอล",
        PlayerUpgrade.LifeSteal=>"ดูดพลังชีวิต",PlayerUpgrade.ThunderDash=>"พุ่งสายฟ้า",_=>""
    };
}

public static class CombatLineOfSight
{
    public static bool Clear(Vector3 origin,EnemyHealth target,GameObject owner)
    {
        Vector3 aim=target.GetComponent<Targetable>()?.AimPoint.position??target.transform.position+Vector3.up;
        Vector3 delta=aim-origin;if(delta.sqrMagnitude<.0001f)return true;
        // Only used at damage commits, not per-frame AI scans. Other combatants
        // do not shield a monster from an explosion, solid scenery does.
        foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,Physics.AllLayers,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider.GetComponentInParent<EnemyHealth>()!=null||hit.collider.GetComponentInParent<IDamageable>()!=null)continue;
            if(owner!=null&&hit.transform.IsChildOf(owner.transform))continue;
            if(hit.collider.GetComponentInParent<Projectile>()!=null)continue;
            return false;
        }
        return true;
    }
}
