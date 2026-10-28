using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerInventory))]
public sealed class PlayerPotionUse : MonoBehaviour
{
    [SerializeField] private GamePauseController pause;
    private PlayerInventory inventory;
    private PlayerHealth health;
    private PlayerEnergy energy;
    private PlayerMovement movement;
    private bool usingPotion;
    public string LastResult { get; private set; }
    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>(); health = GetComponent<PlayerHealth>(); energy = GetComponent<PlayerEnergy>(); movement = GetComponent<PlayerMovement>();
        if (pause == null) pause = FindAnyObjectByType<GamePauseController>();
    }
    public bool CanUse(ItemDefinition item, out string reason)
    {
        reason = "ใช้ Potion ไม่ได้ในขณะนี้";
        if (usingPotion || !isActiveAndEnabled || health == null || !health.IsAlive || item == null || item.Effect == PotionEffect.None || item.PotionStrength <= 0 ||
            movement != null && (movement.IsDashing || movement.IsShadowDashing) || pause != null && (pause.Reasons & ~GamePauseReason.Inventory) != GamePauseReason.None) return false;
        if (item.Effect == PotionEffect.Heal && health.CurrentHealth >= health.MaxHealth) { reason = "เลือดเต็มแล้ว"; return false; }
        if (item.Effect == PotionEffect.Energy && (energy == null || energy.CurrentEnergy >= energy.MaxEnergy)) { reason = "พลังเต็มแล้ว"; return false; }
        if (item.Effect == PotionEffect.Guard && (item.PotionDuration <= 0 || health.PotionGuardRemaining > 0)) { reason = "เกราะเวทยังมีผลอยู่"; return false; }
        reason = "ใช้ Potion"; return true;
    }
    public bool TryUse(int slot, ItemDefinition expected)
    {
        if (!CanUse(expected, out string reason) || inventory.GetItem(slot) != expected) { LastResult = reason; return false; }
        usingPotion = true;
        try
        {
            bool success = inventory.ConsumeOne(slot, expected, () =>
            {
                switch (expected.Effect)
                {
                    case PotionEffect.Heal: health.TryHeal(health.MaxHealth * expected.PotionStrength); break;
                    case PotionEffect.Energy: energy.TryRestore(energy.MaxEnergy * expected.PotionStrength); break;
                    case PotionEffect.Guard: health.TryPotionGuard(expected.PotionStrength, expected.PotionDuration); break;
                }
            });
            LastResult = success ? "ใช้ " + expected.DisplayName + " แล้ว" : "ไอเทมเปลี่ยน กรุณาเลือกใหม่"; return success;
        }
        finally { usingPotion = false; }
    }
}
