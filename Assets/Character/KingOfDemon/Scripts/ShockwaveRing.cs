using UnityEngine;

// คลื่นกระแทกวงแหวนรอบตัวบอส: ขยายออกไปถึง radius ผู้เล่นที่อยู่ในแนวคลื่นจะโดนดาเมจ 1 ครั้ง
public class ShockwaveRing : MonoBehaviour
{
    public float expandTime = 0.6f;
    public float ringWidth = 1.5f;
    public float verticalTolerance = 2.2f;

    float radius = 7f, damage, t; string method; GameObject owner; bool hit, active;

    public void Init(float r, float dmg, string damageMethod, GameObject from)
    {
        radius = r; damage = dmg; method = damageMethod; owner = from; active = true;
    }

    void Update()
    {
        if (!active || hit || Time.timeScale <= 0f) return;
        float previous = Mathf.Lerp(0.3f, radius, t / expandTime);
        t += Time.deltaTime;
        float cur = Mathf.Lerp(0.3f, radius, t / expandTime);
        var p = GameObject.FindGameObjectWithTag("Player");
        if (!p) return;
        Vector3 d = p.transform.position - transform.position;
        bool sameFloor = Mathf.Abs(d.y) <= verticalTolerance;
        d.y = 0;
        float m = d.magnitude;
        if (sameFloor && m <= cur + .45f && m >= previous - ringWidth - .45f &&
            BossCombatSight.Clear(owner != null ? owner.transform : null, transform.position + Vector3.up,
                p.transform, p.transform.position + Vector3.up))
        {
            hit = true;
            BossVFXController.RaisePlayerHit(p, damage, method);
        }
        if (t >= expandTime) active = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, radius);
    }
}
