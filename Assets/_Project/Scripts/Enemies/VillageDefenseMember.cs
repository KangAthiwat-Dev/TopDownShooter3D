using UnityEngine;

[DisallowMultipleComponent,RequireComponent(typeof(EnemyHealth))]
public sealed class VillageDefenseMember : MonoBehaviour
{
    private EnemyHealth health;
    public VillageDefenseEncounter Owner {get;private set;}
    public int Slot {get;private set;}=-1;
    public bool HealthDead=>health!=null&&!health.IsAlive;
    public void Initialize(VillageDefenseEncounter owner,int slot)
    {
        Owner=owner;Slot=slot;health=GetComponent<EnemyHealth>();health.Died+=OnDeath;
    }
    private void OnDeath()=>Owner.Story.RecordDefenseDeath(this);
    private void OnDestroy(){if(health!=null)health.Died-=OnDeath;}
}
