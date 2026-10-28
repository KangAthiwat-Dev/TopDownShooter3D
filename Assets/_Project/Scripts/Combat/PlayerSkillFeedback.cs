using UnityEngine;

/// <summary>Visual presentation only. All damage remains at the real projectile/dash/animation commits.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillFeedback : MonoBehaviour
{
    [SerializeField] private RangedBurstVfx swordSlash,criticalHit,thunderBurst,healBurst;
    private PlayerMeleeAttack melee;
    private PlayerCombatSkills skills;
    private float nextCriticalVisual;
    private void Awake(){melee=GetComponent<PlayerMeleeAttack>();skills=GetComponent<PlayerCombatSkills>();}
    private void OnEnable(){melee.SwingCommitted+=OnSwing;skills.CriticalHit+=OnCritical;skills.ThunderStarted+=OnThunder;skills.ThunderHit+=OnThunderHit;skills.LifeStolen+=OnHeal;}
    private void OnDisable()
    {
        if(melee!=null)melee.SwingCommitted-=OnSwing;
        if(skills!=null){skills.CriticalHit-=OnCritical;skills.ThunderStarted-=OnThunder;skills.ThunderHit-=OnThunderHit;skills.LifeStolen-=OnHeal;}
    }
    private void OnSwing(bool armed)
    {
        if(armed&&swordSlash!=null)Instantiate(swordSlash,transform.position+Vector3.up+transform.forward*.8f,transform.rotation*Quaternion.Euler(0,0,-20));
    }
    private void OnCritical(EnemyHealth enemy)
    {
        if(criticalHit==null||Time.time<nextCriticalVisual)return;nextCriticalVisual=Time.time+.08f;
        var fx=Instantiate(criticalHit,enemy.transform.position+Vector3.up,Quaternion.identity);fx.transform.localScale*=.55f;
    }
    private void OnThunder()
    {if(thunderBurst!=null){var fx=Instantiate(thunderBurst,transform.position+Vector3.up*.8f,transform.rotation);fx.transform.localScale*=.6f;}}
    private void OnThunderHit(EnemyHealth enemy)
    {if(thunderBurst!=null){var fx=Instantiate(thunderBurst,enemy.transform.position+Vector3.up*.8f,Quaternion.identity);fx.transform.localScale*=.5f;}}
    private void OnHeal(float amount)
    {if(healBurst!=null){var fx=Instantiate(healBurst,transform.position+Vector3.up,Quaternion.identity);fx.transform.localScale*=.45f;}}
}
