using UnityEngine;

// หนามเลือดผุดรอบตัว Demon Lord: ขยายจากตัวบอสออกไปถึง radius ผู้เล่นที่อยู่ในรัศมีขณะคลื่นผ่านโดนดาเมจ 1 ครั้ง
public class BloodNova : MonoBehaviour
{
    public float expandTime = 0.35f;
    float radius = 6f, damage, t; string method; bool hit, active;
    GameObject owner;

    public void Init(float r, float dmg, string damageMethod, GameObject from = null)
    { radius = r; damage = dmg; method = damageMethod; owner = from; active = true; }

    void Update()
    {
        if (!active || hit || Time.timeScale <= 0f) return;
        float previous = Mathf.Lerp(.5f, radius, t / expandTime);
        t += Time.deltaTime;
        float cur = Mathf.Lerp(0.5f, radius, t / expandTime);
        var p = GameObject.FindGameObjectWithTag("Player"); if (!p) return;
        Vector3 d = p.transform.position - transform.position;
        bool sameFloor = Mathf.Abs(d.y) <= 2.2f; d.y = 0;
        if (sameFloor && d.magnitude <= cur + .45f && d.magnitude >= previous - 1.5f &&
            BossCombatSight.Clear(owner != null ? owner.transform : null, transform.position + Vector3.up, p.transform, p.transform.position + Vector3.up))
        { hit = true; DemonLordVFX.RaisePlayerHit(p, damage, method); }
        if (t >= expandTime) active = false;
    }

    void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, radius); }
}
