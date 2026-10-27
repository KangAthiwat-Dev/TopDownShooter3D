using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool allowInstantKill = true;
    [SerializeField, Min(0)] private int experienceReward = 35;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool AllowInstantKill => allowInstantKill;
    public int ExperienceReward => experienceReward;
    public bool IsInvulnerable { get; set; }

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private bool deathInvoked;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, GameObject source)
    {
        if (!IsAlive || IsInvulnerable || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);

        Debug.Log(
            $"{gameObject.name} received {damage:0.#} damage. " +
            $"Health: {CurrentHealth:0.#}/{maxHealth:0.#}"
        );

        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (!IsAlive && !deathInvoked)
        {
            deathInvoked = true;
            // The player who deals the lethal hit receives one reward per monster life.
            var progression = source != null ? source.GetComponentInParent<PlayerProgressionState>() : null;
            if (progression != null) progression.GainExperience(experienceReward);
            Died?.Invoke();
        }
    }

    public void RestoreToFullHealth()
    {
        CurrentHealth = maxHealth;
        deathInvoked = false;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
