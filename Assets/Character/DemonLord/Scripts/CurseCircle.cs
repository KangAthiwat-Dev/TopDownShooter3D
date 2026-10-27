using UnityEngine;

// วงเวทสาปใต้เท้าผู้เล่น: ขึ้นเตือนก่อน แล้วระเบิดหลัง delay วินาที ถ้าผู้เล่นยังอยู่ในวงจะโดนดาเมจ
public class CurseCircle : MonoBehaviour
{
    public GameObject explodePrefab;
    float radius = 3f, delay = 1.2f, damage, t; string method; bool armed, done;
    GameObject owner;

    public void Init(float r, float d, float dmg, string damageMethod, GameObject from = null)
    { radius = r; delay = d; damage = dmg; method = damageMethod; owner = from; armed = true; }

    void Update()
    {
        if (!armed || done || Time.timeScale <= 0f) return;
        t += Time.deltaTime;
        if (t < delay) return;
        done = true;
        if (explodePrefab)
        {
            var fx = Instantiate(explodePrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = explodePrefab.transform.localScale * radius;
        }
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p)
        {
            Vector3 d = p.transform.position - transform.position;
            bool sameFloor = Mathf.Abs(d.y) <= 2.2f; d.y = 0;
            if (sameFloor && d.magnitude <= radius && BossCombatSight.Clear(owner != null ? owner.transform : null,
                transform.position + Vector3.up, p.transform, p.transform.position + Vector3.up))
                DemonLordVFX.RaisePlayerHit(p, damage, method);
        }
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(gameObject, 0.1f);
    }

    void OnDrawGizmosSelected() { Gizmos.color = new Color(0.8f, 0, 0.3f); Gizmos.DrawWireSphere(transform.position, radius); }
}
