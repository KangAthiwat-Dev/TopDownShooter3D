using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Owns drops independently of corpses/camps and the player's existing supply inventory.</summary>
public sealed class MonsterLootWorld : MonoBehaviour
{
    [SerializeField] private LootPickup pickupPrefab;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Camera view;
    [SerializeField] private Terrain terrain;
    [SerializeField] private LayerMask groundAndWalls = 1 << 7;
    [Header("Automatic collection")]
    [SerializeField, Min(1)] private float magnetRadius = 8;
    [SerializeField, Min(1)] private float pullSpeed = 22;
    [SerializeField, Min(1)] private float pullAcceleration = 32;
    [SerializeField, Min(.1f)] private float mergeRadius = 1.25f;
    private readonly List<LootPickup> pickups = new List<LootPickup>();
    private readonly Dictionary<ItemDefinition, int> rolled = new Dictionary<ItemDefinition, int>();
    private System.Random random;

    public PlayerInventory Inventory => inventory;
    public Camera View => view;
    public float MagnetRadius => magnetRadius;
    public float PullSpeed => pullSpeed;
    public float PullAcceleration => pullAcceleration;
    public bool CanCollect => inventory != null && inventory.isActiveAndEnabled && playerHealth != null && playerHealth.IsAlive;
    public Vector3 CollectionPoint => inventory.transform.position + Vector3.up * .9f;
    public IReadOnlyList<LootPickup> Pickups => pickups;
    public int SpawnedStacks { get; private set; }
    public int CollectedUnits { get; private set; }
    public event Action<ItemDefinition, int> Collected;

    private void Awake()
    {
        random = new System.Random();
        if (view == null) view = Camera.main;
    }

    public void SpawnDrops(MonsterDropTable table, Vector3 position)
    {
        if (table == null || pickupPrefab == null) return;
        rolled.Clear();
        table.Roll(random, (item, amount) =>
        {
            rolled.TryGetValue(item, out int count);
            rolled[item] = count + amount;
        });
        foreach (var pair in rolled) SpawnItem(pair.Key, pair.Value, position);
    }

    public void SpawnItem(ItemDefinition item, int amount, Vector3 position)
    {
        if (item == null || amount <= 0 || pickupPrefab == null) return;
        // Merge nearby resting piles, without making a pile larger than one inventory stack.
        foreach (var pickup in pickups)
        {
            if (!pickup.CanMerge || pickup.Item != item || Vector3.Distance(pickup.GroundPoint, position) > mergeRadius) continue;
            int moved = Mathf.Min(amount, item.MaxStack - pickup.Amount);
            if (moved <= 0) continue;
            pickup.AddAmount(moved); amount -= moved;
            if (amount == 0) return;
        }
        while (amount > 0)
        {
            int count = Mathf.Min(amount, item.MaxStack);
            float angle = (float)random.NextDouble() * Mathf.PI * 2;
            float distance = .45f + (float)random.NextDouble() * .65f;
            Vector3 destination = FindGround(position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance);
            var pickup = Instantiate(pickupPrefab, position + Vector3.up * .8f, Quaternion.identity, transform);
            pickup.Initialize(this, item, count, destination);
            pickups.Add(pickup); SpawnedStacks++;
            amount -= count;
        }
    }

    public Vector3 FindGround(Vector3 position)
    {
        // Navigation keeps scattered loot out of solid props and on the correct platform.
        if (NavMesh.SamplePosition(position, out var nav, 2, NavMesh.AllAreas)) position = nav.position;
        if (Physics.Raycast(position + Vector3.up * .6f, Vector3.down, out var floor, 2, groundAndWalls, QueryTriggerInteraction.Ignore)
            && floor.normal.y > .5f) return floor.point;
        if (terrain != null && position.y < terrain.SampleHeight(position) + terrain.transform.position.y + .3f)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    public bool HasClearPath(Vector3 position) => !Physics.Linecast(position, CollectionPoint, groundAndWalls, QueryTriggerInteraction.Ignore);
    public int SpaceFor(ItemDefinition item) => CanCollect ? inventory.SpaceFor(item) : 0;

    internal int TryCollect(ItemDefinition item, int amount)
    {
        if (Time.timeScale <= 0 || !CanCollect) return 0;
        int accepted = Mathf.Min(amount, inventory.SpaceFor(item));
        if (accepted <= 0 || !inventory.TryAdd(item, accepted)) return 0;
        CollectedUnits += accepted;
        Collected?.Invoke(item, accepted);
        return accepted;
    }

    internal void Forget(LootPickup pickup) => pickups.Remove(pickup);
}
