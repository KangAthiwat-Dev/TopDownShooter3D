using UnityEngine;

/// <summary>Scatter arc, resting icon and accelerating magnet flight. No gameplay collision.</summary>
public sealed class LootPickup : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private Transform visual;
    [SerializeField] private GameObject halo;
    [SerializeField] private TrailRenderer trail;
    private enum Phase { Scatter, Resting, Attracting, Returning, Collected }
    private Phase phase;
    private MonsterLootWorld world;
    private Vector3 arcStart, restPoint;
    private float phaseTime, speed, nextCheck, bobOffset;
    public ItemDefinition Item { get; private set; }
    public int Amount { get; private set; }
    public Vector3 GroundPoint { get; private set; }
    public bool IsAttracting => phase == Phase.Attracting;
    public bool CanMerge => phase == Phase.Resting;
    public bool IsCollected => phase == Phase.Collected;

    public void Initialize(MonsterLootWorld owner, ItemDefinition item, int amount, Vector3 ground)
    {
        world = owner; Item = item; Amount = amount; GroundPoint = ground;
        restPoint = ground + Vector3.up * .6f;
        arcStart = transform.position;
        phase = Phase.Scatter; phaseTime = 0;
        bobOffset = Random.value * Mathf.PI * 2;
        icon.sprite = item.Icon;
        name = item.ItemId + " x" + amount;
        halo.SetActive(false); trail.emitting = false; trail.Clear();
    }

    internal void AddAmount(int amount)
    {
        Amount += amount;
        name = Item.ItemId + " x" + Amount;
    }

    private void Update()
    {
        if (world == null || Item == null || phase == Phase.Collected || Time.deltaTime <= 0) return;
        float dt = Time.deltaTime;
        phaseTime += dt;
        switch (phase)
        {
            case Phase.Scatter:
            case Phase.Returning:
                float t = Mathf.Clamp01(phaseTime / (phase == Phase.Scatter ? .4f : .35f));
                transform.position = Vector3.Lerp(arcStart, restPoint, Mathf.SmoothStep(0, 1, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .65f;
                visual.localScale = Vector3.one * Mathf.Lerp(.55f, 1, t);
                if (t >= 1) { phase = Phase.Resting; phaseTime = 0; halo.SetActive(true); }
                break;
            case Phase.Resting:
                visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.5f + bobOffset) * .07f);
                if (Time.time < nextCheck) break;
                nextCheck = Time.time + .15f;
                if (world.SpaceFor(Item) <= 0 || Vector3.Distance(transform.position, world.CollectionPoint) > world.MagnetRadius || !world.HasClearPath(transform.position)) break;
                phase = Phase.Attracting; phaseTime = 0; speed = 3;
                halo.SetActive(false); trail.Clear(); trail.emitting = true;
                break;
            case Phase.Attracting:
                if (!world.CanCollect || world.SpaceFor(Item) <= 0 || !world.HasClearPath(transform.position)) { ReturnToGround(); break; }
                speed = Mathf.MoveTowards(speed, world.PullSpeed, world.PullAcceleration * dt);
                float distance = Vector3.Distance(transform.position, world.CollectionPoint);
                visual.localPosition = Vector3.zero;
                visual.localScale = Vector3.one * Mathf.Lerp(.35f, 1, Mathf.Clamp01(distance));
                transform.position = Vector3.MoveTowards(transform.position, world.CollectionPoint, speed * dt);
                if (Vector3.Distance(transform.position, world.CollectionPoint) > .35f) break;
                int accepted = world.TryCollect(Item, Amount);
                Amount -= accepted;
                if (Amount > 0) { ReturnToGround(); break; }
                phase = Phase.Collected; trail.emitting = false;
                gameObject.SetActive(false); Destroy(gameObject);
                break;
        }
    }

    private void ReturnToGround()
    {
        // Inventory may fill while several drops are in flight. Remaining units stay in the world.
        phase = Phase.Returning; phaseTime = 0; arcStart = transform.position;
        visual.localPosition = Vector3.zero; trail.emitting = false;
        nextCheck = Time.time + .6f;
        name = Item.ItemId + " x" + Amount;
    }

    private void LateUpdate()
    {
        if (world != null && world.View != null) visual.rotation = world.View.transform.rotation;
    }
    private void OnDestroy() { if (world != null) world.Forget(this); }
}
