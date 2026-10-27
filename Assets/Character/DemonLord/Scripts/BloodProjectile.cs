using UnityEngine;

// ลูกเลือดของ Demon Lord: พุ่งไปหาผู้เล่น เลี้ยวตามได้เล็กน้อย ชนแล้วแตก
[RequireComponent(typeof(Rigidbody))]
public class BloodProjectile : MonoBehaviour
{
    public GameObject impactPrefab;
    public float lifetime = 4f;
    [Tooltip("องศาต่อวินาทีที่ลูกเลือดเลี้ยวตามผู้เล่นได้ (0 = พุ่งตรง)")] public float homing = 35f;

    Vector3 dir; float speed, damage; string method; GameObject owner; Transform target; bool done;
    SphereCollider sphere;

    void Awake() { var rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; sphere = GetComponentInChildren<SphereCollider>(); }

    public void Launch(Vector3 direction, float spd, float dmg, string damageMethod, GameObject from, Transform tgt)
    {
        dir = direction.normalized; speed = spd; damage = dmg; method = damageMethod; owner = from; target = tgt;
        Invoke(nameof(Explode), lifetime);
    }

    void FixedUpdate()
    {
        if (done || speed <= 0f) return;
        if (target && homing > 0)
        {
            Vector3 want = (target.position + Vector3.up) - transform.position;
            dir = Vector3.RotateTowards(dir, want.normalized, homing * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        }
        float distance = speed * Time.fixedDeltaTime;
        float radius = sphere != null ? sphere.radius * Mathf.Max(sphere.transform.lossyScale.x,
            sphere.transform.lossyScale.y, sphere.transform.lossyScale.z) : .18f;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, radius, dir, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (Ignored(hit.collider)) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            transform.position += dir * Mathf.Max(0f, nearest.Value.distance);
            Hit(nearest.Value.collider);
        }
        else transform.position += dir * distance;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    void OnTriggerEnter(Collider c)
    {
        if (!Ignored(c)) Hit(c);
    }

    bool Ignored(Collider c) => c.isTrigger || c.transform.IsChildOf(transform) ||
        c.GetComponentInParent<EnemyHealth>() != null ||
        owner != null && (c.transform == owner.transform || c.transform.IsChildOf(owner.transform));

    void Hit(Collider c)
    {
        if (done) return;
        var victim = c.GetComponentInParent<PlayerHealth>();
        if (victim != null) DemonLordVFX.RaisePlayerHit(victim.gameObject, damage, method);
        Explode();
    }

    void Explode()
    {
        if (done) return;
        done = true; CancelInvoke();
        if (impactPrefab)
        {
            var fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = impactPrefab.transform.localScale * transform.localScale.x;
        }
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        Destroy(gameObject, 1.2f);
    }
}
