using UnityEngine;

/// <summary>Editable first-pass balance. Ranks and unspent points live in the EXP save.</summary>
[DefaultExecutionOrder(20), RequireComponent(typeof(PlayerProgressionState))]
public sealed class PlayerUpgradeStats : MonoBehaviour
{
    [SerializeField,Min(0)] private float healthPerRank=15;
    [SerializeField,Range(0,.03f)] private float resistancePerRank=.01f;
    [SerializeField,Min(0)] private float energyPerRank=10;
    [SerializeField,Min(0)] private float formEfficiencyPerRank=.08f;
    [SerializeField,Min(0)] private float damagePerRank=.12f;
    [SerializeField,Min(0)] private float fireRatePerRank=.06f;
    private PlayerProgressionState progression;
    private PlayerHealth health;
    private PlayerEnergy energy;
    private PlayerRangedWeapon weapon;
    private PlayerAttackController attack;
    private void Awake()
    {
        progression=GetComponent<PlayerProgressionState>();health=GetComponent<PlayerHealth>();energy=GetComponent<PlayerEnergy>();
        weapon=GetComponent<PlayerRangedWeapon>();attack=GetComponent<PlayerAttackController>();
    }
    private void OnEnable(){progression.Changed+=Apply;Apply();}
    private void OnDisable(){if(progression!=null)progression.Changed-=Apply;}
    public void Apply()
    {
        int vitality=progression.UpgradeRank(PlayerUpgrade.Vitality),endurance=progression.UpgradeRank(PlayerUpgrade.Endurance);
        if(health!=null)health.ConfigureUpgrades(vitality*healthPerRank,vitality*resistancePerRank);
        if(energy!=null)energy.ConfigureUpgrades(endurance*energyPerRank,1f/(1f+progression.UpgradeRank(PlayerUpgrade.Transformation)*formEfficiencyPerRank));
        if(weapon!=null)weapon.DamageMultiplier=1f+progression.UpgradeRank(PlayerUpgrade.Firepower)*damagePerRank;
        if(attack!=null)attack.FireRateMultiplier=1f+progression.UpgradeRank(PlayerUpgrade.FireRate)*fireRatePerRank;
    }
    public string Preview(PlayerUpgrade kind)
    {
        int rank=progression.UpgradeRank(kind);
        switch(kind)
        {
            case PlayerUpgrade.Vitality:return $"เพิ่มเลือดสูงสุด {healthPerRank:0} และลดดาเมจที่ได้รับอีก {resistancePerRank*100:0.#}% ต่อขั้น\nเพิ่มความทนทานในการต่อสู้";
            case PlayerUpgrade.Endurance:return $"เพิ่มพลังงาน {energyPerRank:0} หน่วยต่อขั้น\nใช้วิ่งและแปลงร่างได้นานขึ้น";
            case PlayerUpgrade.Transformation:return $"เวลาแปลงร่างเพิ่ม  {rank*formEfficiencyPerRank*100:0}% → {(rank+1)*formEfficiencyPerRank*100:0}%\nลดการใช้พลังงานระหว่างแปลงร่าง";
            case PlayerUpgrade.Firepower:return $"ดาเมจกระสุนนัดหลัก  {weapon.Damage:0.#} → {weapon.BaseDamage*(1+(rank+1)*damagePerRank):0.#}\nกระสุนนัดข้างใช้ดาเมจ 50% ของนัดหลัก";
            case PlayerUpgrade.FireRate:return $"ความถี่การยิงเพิ่ม  {rank*fireRatePerRank*100:0}% → {(rank+1)*fireRatePerRank*100:0}%\nลดช่วงพักระหว่างนัดและระหว่างชุด";
            default:return GetComponent<PlayerCombatSkills>()?.Preview(kind)??"";
        }
    }
}
