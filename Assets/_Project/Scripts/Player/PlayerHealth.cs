using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    private PlayerMovement playerMovement;
    private float bonusHealth, damageReduction;

    public bool IsInvulnerable =>
        playerMovement != null &&
        playerMovement.IsShadowDashing;

    public float MaxHealth => maxHealth + bonusHealth;
    public float DamageReduction => damageReduction;
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
            CurrentHealth - damage * (1f - damageReduction),
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

    private void Die()
    {
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
