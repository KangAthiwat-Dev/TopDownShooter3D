using UnityEngine;

/// <summary>Death only: disabling, despawning or replacing a camp never generates loot.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyLootDropper : MonoBehaviour
{
    [SerializeField] private MonsterDropTable table;
    private EnemyHealth health;
    private bool issued;
    public MonsterDropTable Table => table;

    private void Awake() => health = GetComponent<EnemyHealth>();
    private void OnEnable()
    {
        health.Died += Drop;
        health.HealthChanged += HealthChanged;
    }
    private void OnDisable()
    {
        if (health == null) return;
        health.Died -= Drop;
        health.HealthChanged -= HealthChanged;
    }
    private void HealthChanged(float current, float maximum)
    {
        if (current > 0) issued = false;
    }
    private void Drop()
    {
        if (issued || table == null) return;
        issued = true;
        var world = FindAnyObjectByType<MonsterLootWorld>();
        if (world != null) world.SpawnDrops(table, transform.position);
    }
}
