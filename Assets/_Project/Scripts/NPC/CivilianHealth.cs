using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Neutral, damageable townsfolk; deliberately NOT an EnemyHealth/Targetable.</summary>
[DisallowMultipleComponent]
public sealed class CivilianHealth : MonoBehaviour, IDamageable
{
    private static readonly List<CivilianHealth> living = new List<CivilianHealth>();
    public static IReadOnlyList<CivilianHealth> Living => living;
    [SerializeField, Min(1f)] private float maxHealth = 50f;
    [SerializeField, Min(.1f)] private float corpseSeconds = 4.5f;
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0f;
    public event Action<CivilianHealth> Died;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => living.Clear();
    private void Awake() => CurrentHealth = maxHealth;
    private void OnEnable() { if (IsAlive && !living.Contains(this)) living.Add(this); }
    private void OnDisable() => living.Remove(this);
    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (IsAlive) return;
        living.Remove(this);
        GetComponent<TownNpcBrain>()?.HandleDeath();
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        Died?.Invoke(this);
        Destroy(gameObject, corpseSeconds);
    }
}
