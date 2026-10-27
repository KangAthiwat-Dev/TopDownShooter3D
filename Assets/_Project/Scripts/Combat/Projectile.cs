using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField, Min(0.1f)]
    private float lifetime = 3f;
    [SerializeField] private RangedBurstVfx impactVfx;

    private Rigidbody body;
    private GameObject owner;
    private float damage;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Initialize(
        Vector3 direction,
        float speed,
        float damageAmount,
        GameObject projectileOwner
    )
    {
        owner = projectileOwner;
        damage = damageAmount;
        hasHit = false;

        body.linearVelocity =
            direction.normalized * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
        {
            return;
        }

        if (owner != null &&
            other.transform.IsChildOf(
                owner.transform
            ))
        {
            return;
        }

        IDamageable damageable =
            other.GetComponentInParent
                <IDamageable>();

        if (damageable != null &&
            damageable.IsAlive)
        {
            hasHit = true;
            ShowImpact(other);
            if (damageable is EnemyHealth enemy) enemy.TakeDamage(damage, owner);
            else damageable.TakeDamage(damage);
            Destroy(gameObject);

            return;
        }

        // ชนกำแพงหรือวัตถุที่ไม่ใช่ Trigger
        if (!other.isTrigger)
        {
            hasHit = true;
            ShowImpact(other);
            Destroy(gameObject);
        }
    }
    private void ShowImpact(Collider other)
    {
        if (impactVfx == null) return;
        Vector3 normal = body.linearVelocity.sqrMagnitude > 0.0001f ? -body.linearVelocity.normalized : -transform.forward;
        Instantiate(impactVfx, other.ClosestPoint(transform.position), Quaternion.LookRotation(normal));
    }
}
