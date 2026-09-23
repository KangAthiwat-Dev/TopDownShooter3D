using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField, Min(0.1f)]
    private float lifetime = 3f;

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

            damageable.TakeDamage(damage);
            Destroy(gameObject);

            return;
        }

        // ชนกำแพงหรือวัตถุที่ไม่ใช่ Trigger
        if (!other.isTrigger)
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}