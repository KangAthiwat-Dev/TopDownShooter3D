using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private static readonly System.Collections.Generic.List<EnemyHealth> living = new System.Collections.Generic.List<EnemyHealth>();
    public static System.Collections.Generic.IReadOnlyList<EnemyHealth> Active => living;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => living.Clear();
    private void OnEnable() { if (!living.Contains(this)) living.Add(this); }
    private void OnDisable() => living.Remove(this);
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool allowInstantKill = true;
    [SerializeField, Min(0)] private int experienceReward = 35;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool AllowInstantKill => allowInstantKill;
    public int ExperienceReward => experienceReward;
    public bool IsInvulnerable { get; set; }
    public GameObject LastDamageSource { get; private set; }

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
        LastDamageSource = source;

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
        LastDamageSource = null;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void ConfigureEncounter(float health, int experience)
    {
        maxHealth = Mathf.Max(1, health); experienceReward = Mathf.Max(0, experience);
        RestoreToFullHealth();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
