using UnityEngine;

// ลูกไฟของ King of Demon: พุ่งตรงไปข้างหน้า ชนแล้วระเบิด
[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : MonoBehaviour
{
    public GameObject impactPrefab;
    public float lifetime = 4f;

    Vector3 dir; float speed, damage; string method; GameObject owner; bool done;
    SphereCollider sphere;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        sphere = GetComponentInChildren<SphereCollider>();
    }

    public void Launch(Vector3 direction, float spd, float dmg, string damageMethod, GameObject from)
    {
        dir = direction.normalized; speed = spd; damage = dmg; method = damageMethod; owner = from;
        Invoke(nameof(Explode), lifetime);
    }

    void FixedUpdate()
    {
        if (done || speed <= 0f) return;
        Vector3 step = dir * speed * Time.fixedDeltaTime;
        float radius = sphere != null ? sphere.radius * Mathf.Max(sphere.transform.lossyScale.x,
            sphere.transform.lossyScale.y, sphere.transform.lossyScale.z) : .18f;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, radius, dir, step.magnitude,
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
        else transform.position += step;
    }

    void OnTriggerEnter(Collider c)
    {
        if (!Ignored(c)) Hit(c);
    }

    bool Ignored(Collider c) => c.isTrigger || c.transform.IsChildOf(transform) ||
        owner != null && (c.transform == owner.transform || c.transform.IsChildOf(owner.transform));

    void Hit(Collider c)
    {
        if (done) return;
        var player = c.GetComponentInParent<PlayerHealth>();
        if (player != null) BossVFXController.RaisePlayerHit(player.gameObject, damage, method);
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
        // หยุดปล่อยอนุภาค ให้หางไฟค่อยๆ จางแล้วค่อยลบ
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        Destroy(gameObject, 1.2f);
    }
}
