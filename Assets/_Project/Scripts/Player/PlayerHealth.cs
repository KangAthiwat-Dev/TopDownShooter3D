using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    private PlayerMovement playerMovement;
    private float bonusHealth, damageReduction;
    private float potionReduction, potionGuardUntil;

    public bool IsInvulnerable =>
        playerMovement != null &&
        playerMovement.IsShadowDashing;

    public float MaxHealth => maxHealth + bonusHealth;
    public float DamageReduction => 1f - (1f - damageReduction) * (1f - (PotionGuardRemaining > 0 ? potionReduction : 0));
    public float PotionGuardRemaining => Mathf.Max(0, potionGuardUntil - Time.time);
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDead => !IsAlive;
    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        CurrentHealth = MaxHealth;
    }

    public void ConfigureUpgrades(float bonus, float reduction)
    {
        float previousMax=MaxHealth;
        bonus=Mathf.Max(0,bonus);reduction=Mathf.Clamp(reduction,0,.75f);
        if(Mathf.Approximately(bonusHealth,bonus)&&Mathf.Approximately(damageReduction,reduction))return;
        bonusHealth=bonus;damageReduction=reduction;
        if(IsAlive)CurrentHealth=Mathf.Clamp(CurrentHealth+Mathf.Max(0,MaxHealth-previousMax),0,MaxHealth);
        HealthChanged?.Invoke(CurrentHealth,MaxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (
            IsDead ||
            IsInvulnerable ||
            damage <= 0f
        )
        {
            return;
        }

        CurrentHealth = Mathf.Max(
            CurrentHealth - damage * (1f - DamageReduction),
            0f
        );

        HealthChanged?.Invoke(
            CurrentHealth,
            MaxHealth
        );

        Debug.Log(
            $"Player received {damage:0.#} damage. " +
            $"Health: {CurrentHealth:0.#}/{MaxHealth:0.#}"
        );

        if (IsDead)
        {
            Die();
        }
    }

    public bool TryHeal(float amount)
    {
        if (!IsAlive || amount <= 0 || CurrentHealth >= MaxHealth) return false;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount); HealthChanged?.Invoke(CurrentHealth, MaxHealth); return true;
    }

    // Environmental death deliberately bypasses armour, potions and shadow dash.
    public void KillFromLava()
    {
        if (IsDead) return;
        CurrentHealth = 0;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        Die();
    }
    public bool TryPotionGuard(float reduction, float duration)
    {
        if (!IsAlive || reduction <= 0 || duration <= 0 || PotionGuardRemaining > 0) return false;
        potionReduction = Mathf.Clamp(reduction, 0, .75f); potionGuardUntil = Time.time + duration; return true;
    }

    private void Die()
    {
        potionGuardUntil = 0;
        Died?.Invoke();
        
        Debug.Log("Player died.");

        PlayerMovement movement = GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.enabled = false;
        }

        PlayerAttackController attack =
            GetComponent<PlayerAttackController>();

        if (attack != null)
        {
            attack.enabled = false;
        }
    }
}
