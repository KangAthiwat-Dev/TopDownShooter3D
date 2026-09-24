using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private bool deathInvoked;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f)
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
            Died?.Invoke();
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}