using UnityEngine;

/// <summary>Stationary, non-combat NPC. Only the visual pivot turns; its collider stays put.</summary>
[DisallowMultipleComponent]
public sealed class NpcIdleLook : MonoBehaviour
{
    [Header("Identity — reserved for future dialogue/shop data")]
    [SerializeField] private string npcId = "armory_merchant_01";
    [SerializeField] private string displayName = "Weapon Merchant";
    [Header("Look behaviour")]
    [SerializeField] private Transform facingRoot;
    [SerializeField] private Transform lookTarget;
    [SerializeField, Min(.1f)] private float noticeRadius = 4f;
    [SerializeField, Min(.1f)] private float releaseRadius = 5f;
    [SerializeField, Min(1f)] private float turnSpeed = 105f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.55f;
    [SerializeField] private LayerMask occluders = 1 << 7;

    private Quaternion homeLocalRotation;
    private PlayerHealth targetHealth;
    private PlayerMovement targetMovement;
    private float nextTargetSearch;
    public bool IsWatching { get; private set; }
    public string NpcId => npcId;
    public string DisplayName => displayName;
    public Transform FacingRoot => facingRoot;

    public void Configure(Transform visualPivot, Transform target)
    {
        facingRoot = visualPivot;
        SetLookTarget(target);
    }

    public void SetLookTarget(Transform target)
    {
        lookTarget = target;
        targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        targetMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
        IsWatching = false;
    }

    private void Awake()
    {
        if (facingRoot != null) homeLocalRotation = facingRoot.localRotation;
        SetLookTarget(lookTarget);
    }

    private void OnValidate()
    {
        noticeRadius = Mathf.Max(.1f, noticeRadius);
        releaseRadius = Mathf.Max(noticeRadius, releaseRadius);
        turnSpeed = Mathf.Max(1f, turnSpeed);
    }

    private void Update()
    {
        if (facingRoot == null || Time.timeScale <= 0f) return;
        if (lookTarget == null && Time.time >= nextTargetSearch)
        {
            nextTargetSearch = Time.time + 1f;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) SetLookTarget(player.transform);
        }

        Vector3 delta = lookTarget != null ? lookTarget.position - transform.position : Vector3.zero;
        Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        float radius = IsWatching ? releaseRadius : noticeRadius;
        bool available = lookTarget != null && lookTarget.gameObject.activeInHierarchy &&
            (targetHealth == null || targetHealth.IsAlive) &&
            (targetMovement == null || !targetMovement.IsShadowDashing);
        IsWatching = available && Mathf.Abs(delta.y) < 2.5f && flat.sqrMagnitude <= radius * radius && HasSight();

        Quaternion home = transform.rotation * homeLocalRotation;
        Quaternion desired = IsWatching && flat.sqrMagnitude > .0001f
            ? Quaternion.LookRotation(flat, Vector3.up) : home;
        facingRoot.rotation = Quaternion.RotateTowards(facingRoot.rotation, desired, turnSpeed * Time.deltaTime);
    }

    private bool HasSight()
    {
        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = lookTarget.position + Vector3.up * Mathf.Min(eyeHeight, 1.4f);
        Vector3 delta = to - from;
        return !Physics.Raycast(from, delta.normalized, delta.magnitude, occluders, QueryTriggerInteraction.Ignore);
    }

    private void OnDisable() => IsWatching = false;
}
