using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    private PlayerMovement playerMovement;

    public bool IsInvulnerable =>
        playerMovement != null &&
        playerMovement.IsShadowDashing;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDead => !IsAlive;
    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        CurrentHealth = maxHealth;
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
            CurrentHealth - damage,
            0f
        );

        HealthChanged?.Invoke(
            CurrentHealth,
            maxHealth
        );

        Debug.Log(
            $"Player received {damage:0.#} damage. " +
            $"Health: {CurrentHealth:0.#}/{maxHealth:0.#}"
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