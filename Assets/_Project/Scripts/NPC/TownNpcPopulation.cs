using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Fixed living slots: death schedules one delayed replacement, not an endless spawn stream.</summary>
[DisallowMultipleComponent]
public sealed class TownNpcPopulation : MonoBehaviour
{
    [Header("Ordinary citizens only — the named merchant is separate")]
    [SerializeField] private TownNpcBrain[] citizenPrefabs;
    [Header("Reserved slots — included in Max Population, stable across replacement")]
    [SerializeField] private TownNpcBrain[] femalePrefabs;
    [SerializeField, Min(0)] private int femaleCount = 5;
    [SerializeField] private TownNpcBrain constructionWorkerPrefab;
    [SerializeField, Min(0)] private int constructionWorkers = 3;
    [SerializeField] private TownNpcWorkStation[] constructionStations;
    [SerializeField, Min(1)] private int maxPopulation = 12;
    [SerializeField, Min(0)] private int maxOutside = 2;
    [SerializeField, Range(0f, 1f)] private float roamingCitizenChance = .3f;
    [SerializeField, Min(.1f)] private float respawnDelay = 12f;
    [SerializeField, Min(.1f)] private float spawnRetrySeconds = 2f;
    [Header("Grounded city bounds (XZ), with a margin inside the walls")]
    [SerializeField] private Vector3 cityCenter = new Vector3(75.7626f, 5.14f, -72.7745f);
    [SerializeField] private Vector2 citySize = new Vector2(40f, 42f);
    [SerializeField] private Transform cityFrame;
    [SerializeField] private TownNpcActivityPoint[] cityPoints;
    [SerializeField] private TownNpcActivityPoint[] outsidePoints;
    [SerializeField] private LayerMask solidScenery = 1 << 7;
    [Tooltip("Runtime-only carving for grounded solid props that were added/moved after the last navigation bake. Original objects and NavMesh assets are not edited.")]
    [SerializeField] private bool avoidUnbakedCityProps = true;
    private enum SlotRole { Female, Male, Builder }
    private sealed class Slot { public CivilianHealth health; public float readyAt; public int generation; public SlotRole role; public int station; }
    private readonly List<Slot> slots = new List<Slot>();
    private readonly HashSet<TownNpcBrain> away = new HashSet<TownNpcBrain>();
    private Transform player;
    private float startupReadyAt;
    public int RuntimePropObstacles { get; private set; }
    public int MaxPopulation => maxPopulation;
    public int ConfiguredWorkers => constructionWorkerPrefab != null && constructionStations != null && constructionStations.Length > 0 ? Mathf.Clamp(constructionWorkers, 0, maxPopulation) : 0;
    public int ConfiguredFemales => femalePrefabs != null && femalePrefabs.Length > 0 ? Mathf.Clamp(femaleCount, 0, maxPopulation - ConfiguredWorkers) : 0;
    public IReadOnlyList<TownNpcWorkStation> ConstructionStations => constructionStations;
    public int MaxOutside => Mathf.Min(maxOutside, Mathf.Max(0, (maxPopulation - 1) / 2));
    public int AliveCount { get { int count = 0; foreach (var slot in slots) if (slot.health != null && slot.health.IsAlive) count++; return count; } }
    public int AwayCount => away.Count;
    public int SpawnedTotal { get; private set; }
    public IReadOnlyList<TownNpcActivityPoint> CityPoints => cityPoints;
    public IReadOnlyList<TownNpcActivityPoint> OutsidePoints => outsidePoints;
    private Vector3 WorldCityCenter => cityFrame != null ? cityFrame.TransformPoint(cityCenter) : cityCenter;
    public bool Contains(Vector3 point)
    {
        if (cityFrame != null) point = cityFrame.InverseTransformPoint(point);
        return Mathf.Abs(point.x - cityCenter.x) <= citySize.x * .5f && Mathf.Abs(point.z - cityCenter.z) <= citySize.y * .5f;
    }

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player"); if (playerObject != null) player = playerObject.transform;
        for (int i = 0; i < maxPopulation; i++)
        {
            bool worker = i >= maxPopulation - ConfiguredWorkers;
            slots.Add(new Slot { role = worker ? SlotRole.Builder : i < ConfiguredFemales ? SlotRole.Female : SlotRole.Male,
                station = worker ? (i - (maxPopulation - ConfiguredWorkers)) % constructionStations.Length : -1 });
        }
        if (avoidUnbakedCityProps) CarveUnbakedProps();
        // Allow the navigation system to apply the newly registered carving shapes first.
        startupReadyAt = Time.time + .3f;
    }
    private void Update() { if (Time.timeScale > 0f && Time.time >= startupReadyAt) FillVacancies(); }
    private void CarveUnbakedProps()
    {
        Transform helpers = null;
        var registered = new List<Bounds>();
        void Add(Bounds bounds, string label, bool requireSolidCollider)
        {
            if (!Contains(bounds.center) || bounds.size.y < .65f || bounds.min.y > WorldCityCenter.y + 1.5f || bounds.max.y < WorldCityCenter.y + .65f) return;
            foreach (var previous in registered)
                if (previous.min.x <= bounds.min.x && previous.max.x >= bounds.max.x && previous.min.z <= bounds.min.z && previous.max.z >= bounds.max.z) return;
            Vector3 ground = new Vector3(bounds.center.x, WorldCityCenter.y, bounds.center.z);
            // Already baked blockers have no walkable polygon at their centre; leave them alone.
            if (!NavMesh.SamplePosition(ground, out var nav, .25f, NavMesh.AllAreas)) return;
            if (requireSolidCollider && !Physics.CheckCapsule(nav.position + Vector3.up * .45f, nav.position + Vector3.up * 1.4f, .25f, solidScenery, QueryTriggerInteraction.Ignore)) return;
            if (helpers == null) { helpers = new GameObject("Runtime_Unbaked_Prop_Navigation").transform; helpers.SetParent(transform, false); }
            float height = Mathf.Max(2f, bounds.max.y - WorldCityCenter.y);
            var obstacle = new GameObject("Avoid_" + label).AddComponent<NavMeshObstacle>(); obstacle.transform.SetParent(helpers, false);
            obstacle.transform.position = new Vector3(bounds.center.x, WorldCityCenter.y + height * .5f, bounds.center.z);
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(bounds.size.x + .12f, height, bounds.size.z + .12f);
            obstacle.carving = true; obstacle.carveOnlyStationary = true; obstacle.carvingTimeToStationary = .1f;
            registered.Add(bounds); RuntimePropObstacles++;
        }
        foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!collider.enabled || collider.isTrigger || collider is TerrainCollider || (solidScenery.value & (1 << collider.gameObject.layer)) == 0) continue;
            Add(collider.bounds, collider.name, true);
        }
        // Model-only imported props can have NO Collider at all (the fountain is one).
        // Restrict the conservative bounds fallback to grounded urban Kit/URP meshes,
        // not foliage, particles, characters or the terrain.
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || (solidScenery.value & (1 << renderer.gameObject.layer)) == 0 || renderer.GetComponentInParent<Collider>() != null) continue;
            bool urban = false;
            foreach (var material in renderer.sharedMaterials)
                if (material != null && (material.shader.name == "MedievalKit/KitLit" || material.shader.name == "Universal Render Pipeline/Lit")) { urban = true; break; }
            if (urban) Add(renderer.bounds, renderer.name, false);
        }
    }
    private void FillVacancies()
    {
        if (citizenPrefabs == null || citizenPrefabs.Length == 0 || cityPoints == null || cityPoints.Length == 0) return;
        foreach (var slot in slots)
        {
            if (slot.health != null && slot.health.IsAlive || Time.time < slot.readyAt) continue;
            slot.readyAt = Time.time + spawnRetrySeconds;
            var station = slot.role == SlotRole.Builder ? constructionStations[slot.station] : null;
            if (!TrySpawnPoint(out var point, station)) continue;
            var prefabs = slot.role == SlotRole.Female ? femalePrefabs : citizenPrefabs;
            int variant = slot.role == SlotRole.Female ? (slots.IndexOf(slot) + slot.generation) % prefabs.Length : Random.Range(0, prefabs.Length);
            var prefab = slot.role == SlotRole.Builder ? constructionWorkerPrefab : prefabs[variant]; if (prefab == null) continue;
            var citizen = Instantiate(prefab, point, Quaternion.Euler(0, Random.Range(0, 360f), 0), transform);
            citizen.name = (slot.role == SlotRole.Builder ? "Builder_" : "Citizen_" + slot.role + "_") + slots.IndexOf(slot).ToString("D2") + "_" + (++slot.generation);
            if (station != null) citizen.GetComponent<TownConstructionJob>().Assign(station);
            bool roamer = slot.role != SlotRole.Builder && (slots.IndexOf(slot) == 0 || Random.value < roamingCitizenChance);
            citizen.Configure(this, roamer);
            slot.health = citizen.GetComponent<CivilianHealth>(); slot.health.Died += CitizenDied;
            SpawnedTotal++;
        }
    }
    private void CitizenDied(CivilianHealth health)
    {
        health.Died -= CitizenDied;
        away.Remove(health.GetComponent<TownNpcBrain>());
        foreach (var slot in slots) if (slot.health == health) { slot.readyAt = Time.time + respawnDelay; break; }
    }
    private bool TrySpawnPoint(out Vector3 result, TownNpcWorkStation station = null)
    {
        for (int attempt = 0; attempt < 36; attempt++)
        {
            var node = cityPoints[Random.Range(0, cityPoints.Length)]; if (node == null && station == null) continue;
            Vector2 jitter = Random.insideUnitCircle * 1.2f;
            Vector3 requested = (station != null ? station.transform.position : node.transform.position) + new Vector3(jitter.x, 0, jitter.y);
            if (!NavMesh.SamplePosition(requested, out var hit, 1.2f, NavMesh.AllAreas) || !Contains(hit.position) || Mathf.Abs(hit.position.y - WorldCityCenter.y) > .5f) continue;
            if (station != null && !station.Contains(hit.position)) continue;
            if (player != null && Vector3.Distance(hit.position, player.position) < 2f) continue;
            bool busy = false;
            foreach (var other in CivilianHealth.Living) if (other != null && (other.transform.position - hit.position).sqrMagnitude < 1.5f * 1.5f) { busy = true; break; }
            foreach (var enemy in EnemyAI.ActiveEnemies) if (enemy != null && (enemy.transform.position - hit.position).sqrMagnitude < 8f * 8f) { busy = true; break; }
            if (busy || Physics.CheckCapsule(hit.position + Vector3.up * .45f, hit.position + Vector3.up * 1.5f, .29f, solidScenery, QueryTriggerInteraction.Ignore)) continue;
            result = hit.position; return true;
        }
        result = default; return false;
    }
    public bool TryDestination(TownNpcBrain citizen, bool outside, out TownNpcActivityPoint result)
    {
        result = null; var points = outside ? outsidePoints : cityPoints;
        if (points == null || points.Length == 0) return false;
        int start = Random.Range(0, points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[(i + start) % points.Length];
            if (point == null || (point.transform.position - citizen.transform.position).sqrMagnitude < 3f * 3f) continue;
            if (citizen.CanReach(point.transform.position, outside || away.Contains(citizen))) { result = point; return true; }
        }
        return false;
    }
    public bool TryReserveOuting(TownNpcBrain citizen)
    {
        if (citizen == null || !citizen.CanLeaveTown || away.Count >= MaxOutside) return false;
        return away.Add(citizen);
    }
    public bool HasOutingPermit(TownNpcBrain citizen) => away.Contains(citizen);
    public void ReleaseOuting(TownNpcBrain citizen) => away.Remove(citizen);
    private void OnDestroy()
    {
        foreach (var slot in slots) if (slot.health != null) slot.health.Died -= CitizenDied;
        away.Clear();
    }
}
