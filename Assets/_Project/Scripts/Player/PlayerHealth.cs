using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDead => !IsAlive;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);

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