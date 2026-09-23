using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool disableOnDeath = true;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0f;

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(
            0f,
            CurrentHealth - amount
        );

        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        Debug.Log(
            $"{name} received {amount} damage. " +
            $"Health: {CurrentHealth}/{maxHealth}"
        );

        if (!IsAlive)
        {
            Die();
        }
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        gameObject.SetActive(true);

        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        Died?.Invoke();

        Debug.Log($"{name} died.");

        if (disableOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    [ContextMenu("Test: Take 25 Damage")]
    private void TakeTestDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before testing damage."
            );

            return;
        }

        TakeDamage(25f);
    }
}