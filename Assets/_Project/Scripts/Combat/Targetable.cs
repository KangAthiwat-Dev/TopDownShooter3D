using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class Targetable : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;
    [SerializeField, Min(0f), Tooltip("Additional body radius for close-range checks on oversized enemies.")]
    private float closeRangeOffset;

    private EnemyHealth health;

    public Transform AimPoint =>
        aimPoint != null ? aimPoint : transform;

    public EnemyHealth Health => health;

    public float HorizontalDistanceSquared(Vector3 origin)
    {
        var offset = AimPoint.position - origin;
        offset.y = 0f;
        float distance = Mathf.Max(0f, offset.magnitude - closeRangeOffset);
        return distance * distance;
    }

    public bool IsTargetable =>
        isActiveAndEnabled &&
        health != null &&
        health.IsAlive;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }
}
