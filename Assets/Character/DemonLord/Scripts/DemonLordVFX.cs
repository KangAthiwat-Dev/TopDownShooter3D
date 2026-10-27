using UnityEngine;
using System.Collections.Generic;

// ใส่ไว้ที่ตัว Demon Lord (ตัวเดียวกับ Animator)
// Animation Event ในแต่ละท่าจะเรียกฟังก์ชัน VFX_... ด้านล่าง เพื่อสร้างเอฟเฟกต์ "แยก" ออกมาจากตัวละคร
// เอฟเฟกต์ทุกอันเป็น Prefab ต่างหาก ถูกสร้างตอนใช้ท่า แล้วลบตัวเองเมื่อจบ (ไม่เป็นลูกของตัวละคร)
public class DemonLordVFX : MonoBehaviour
{
    [Header("Effect Prefabs")]
    public GameObject chargePrefab;     // พลังเลือดวนที่มือตอนร่าย
    public GameObject slashPrefab;      // กรงเล็บเลือด (Attack)
    public GameObject boltPrefab;       // ลูกเลือดพุ่งใส่ผู้เล่น (Bolt)
    public GameObject novaPrefab;       // หนามเลือดผุดรอบตัว (Nova)
    public GameObject cursePrefab;      // วงเวทใต้เท้าผู้เล่น ระเบิดหลังดีเลย์ (Curse)
    public GameObject tauntPrefab;      // ออร่าระเบิดตอนคำราม (Taunt)
    public GameObject deathPrefab;      // หมอกเลือดตอนตาย (Death)
    public GameObject idleAuraPrefab;   // ประกายเลือดลอยรอบตัวตลอดเวลา (เว้นว่างหรือปิด idleAura ได้)
    public bool idleAura = true;

    [Header("Skill Settings")]
    public float boltSpeed = 12f;
    public float boltDamage = 20f;
    [Tooltip("รัศมีหนามเลือดรอบตัว (เมตร)")] public float novaRadius = 6f;
    public float novaDamage = 25f;
    [Tooltip("รัศมีวงเวทสาป (เมตร)")] public float curseRadius = 3f;
    [Tooltip("เวลาก่อนวงเวทระเบิด (วินาที) ให้ผู้เล่นมีเวลาหลบ")] public float curseDelay = 1.2f;
    public float curseDamage = 35f;
    [Tooltip("ดาเมจต่อการฟาด 1 ครั้ง (ท่า Attack ฟาด 2 ครั้ง)")] public float slashDamage = 10f;
    public float slashRange = 3.5f;
    [Tooltip("ชื่อฟังก์ชันรับดาเมจบน Player ที่จะเรียกผ่าน SendMessage(float)")]
    public string damageMethod = "TakeDamage";

    // ฝั่งโปรแกรมใช้ event นี้แทน SendMessage ได้: (ตัวที่โดน, ดาเมจ)
    public static event System.Action<GameObject, float> PlayerHit;
    internal static void RaisePlayerHit(GameObject target, float dmg, string method)
    {
        if (!target) return;
        if (PlayerHit != null) PlayerHit(target, dmg);
        else if (!string.IsNullOrEmpty(method)) target.SendMessage(method, dmg, SendMessageOptions.DontRequireReceiver);
    }

    Animator anim; Transform rHand, lHand, chest, player;
    GameObject chargeR, chargeL, aura;
    EncounterCombatant combat;
    readonly List<GameObject> activeEffects = new List<GameObject>();
    public event System.Action<string> EffectReleased;
    bool CanEmit => combat == null || combat.CanEmitSkillEffects;
    bool slashFlip;
    float S = 1f;   // ขนาดเอฟเฟกต์ที่ติดตัว เทียบกับคนสูง ~1.8 ม.

    bool inited;
    void Awake() { Init(); }
    void Init()
    {
        if (inited) return; inited = true;
        combat = GetComponent<EncounterCombatant>();
        anim = GetComponent<Animator>();
        if (anim && anim.isHuman)
        {
            rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head) S = Mathf.Max(0.3f, (head.position.y - transform.position.y) / 1.6f);
        }
    }

    void OnEnable() { Init(); if (idleAura && idleAuraPrefab && !aura) aura = Spawn(idleAuraPrefab, transform.position, Quaternion.identity, transform); }
    void OnDisable() { CancelCombatEffects(true); }

    Transform Player()
    {
        if (!player) { var p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; }
        return player;
    }

    // ---------- Animation Events ----------
    public void VFX_ChargeStart()
    {
        if (!CanEmit) return;
        Init(); slashFlip = false;
        ClearCharge();
        if (!chargePrefab) return;
        if (rHand) chargeR = Spawn(chargePrefab, rHand.position, Quaternion.identity, rHand);
        if (lHand) chargeL = Spawn(chargePrefab, lHand.position, Quaternion.identity, lHand);
    }

    public void VFX_Slash()
    {
        if (!CanEmit) return;
        Init();
        Vector3 pos = transform.position + transform.forward * 0.9f * S + Vector3.up * 1.0f * S;
        // ฟาดครั้งที่ 1 / 2 สลับทิศรอยกรงเล็บ (ซ้าย-ขวา) ให้ตรงกับมือที่ฟาด
        var rot = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0, 0, slashFlip ? 180f : 0f);
        slashFlip = !slashFlip;
        if (slashPrefab) Spawn(slashPrefab, pos, rot, null);
        var p = Player();
        if (combat != null) combat.ResolveMeleeImpact();
        else if (p)
        {
            Vector3 d = p.position - transform.position; d.y = 0;
            if (d.magnitude <= slashRange && Vector3.Angle(transform.forward, d) < 70f) RaisePlayerHit(p.gameObject, slashDamage, damageMethod);
        }
        EffectReleased?.Invoke("Slash");
    }

    public void VFX_Bolt()
    {
        Init();
        ClearCharge();
        if (!CanEmit) return;
        if (!boltPrefab) return;
        // ยิงจากมือที่ยื่นไปข้างหน้ามากกว่า (มือที่ร่ายจริงในท่า)
        Transform hand = rHand;
        if (rHand && lHand && Vector3.Dot(lHand.position - rHand.position, transform.forward) > 0) hand = lHand;
        Vector3 origin = hand ? hand.position : transform.position + Vector3.up * 1.4f * S + transform.forward * 0.6f * S;
        Vector3 dir = transform.forward;
        var p = Player();
        if (p) dir = (p.position + Vector3.up * 1.0f) - origin;
        dir.y = Mathf.Clamp(dir.y, -0.3f * dir.magnitude, 0.3f * dir.magnitude);
        dir.Normalize();
        var go = Spawn(boltPrefab, origin + dir * 0.3f * S, Quaternion.LookRotation(dir), null);
        var b = go.GetComponent<BloodProjectile>();
        if (b) b.Launch(dir, boltSpeed, boltDamage, damageMethod, gameObject, p);
        EffectReleased?.Invoke("Bolt");
    }

    public void VFX_Nova()
    {
        Init();
        ClearCharge();
        if (!CanEmit) return;
        if (!novaPrefab) return;
        var go = Instantiate(novaPrefab, transform.position + Vector3.up * 0.05f, Quaternion.identity);
        go.transform.localScale = Vector3.one * novaRadius;   // prefab ออกแบบที่รัศมี 1
        Track(go);
        var n = go.GetComponent<BloodNova>(); if (n) n.Init(novaRadius, novaDamage, damageMethod, gameObject);
        EffectReleased?.Invoke("Nova");
    }

    public void VFX_Curse()
    {
        Init();
        ClearCharge();
        if (!CanEmit) return;
        if (!cursePrefab) return;
        var p = Player();
        Vector3 pos = p ? p.position : transform.position + transform.forward * 5f;
        pos.y = (p ? p.position.y : transform.position.y) + 0.05f;
        var go = Instantiate(cursePrefab, pos, Quaternion.identity);
        go.transform.localScale = Vector3.one * curseRadius;   // prefab ออกแบบที่รัศมี 1
        Track(go);
        var c = go.GetComponent<CurseCircle>(); if (c) c.Init(curseRadius, curseDelay, curseDamage, damageMethod, gameObject);
        EffectReleased?.Invoke("Curse");
    }

    public void VFX_Taunt()
    {
        if (!CanEmit) return;
        Init();
        if (!tauntPrefab) return;
        Spawn(tauntPrefab, transform.position, Quaternion.identity, transform);
        EffectReleased?.Invoke("Taunt");
    }

    public void VFX_DeathMist()
    {
        Init();
        ClearCharge(); if (aura) Destroy(aura);
        if (!deathPrefab) return;
        Spawn(deathPrefab, chest ? chest.position : transform.position + Vector3.up * S, Quaternion.identity, null);
        EffectReleased?.Invoke("Death");
    }

    // ---------- helpers ----------
    GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform follow)
    {
        var go = Instantiate(prefab, pos, rot);       // ไม่ใส่เป็นลูกของตัวละคร: เอฟเฟกต์แยกจากตัวละครเสมอ
        go.transform.localScale = prefab.transform.localScale * S;
        if (follow) { var f = go.GetComponent<DLVFXAutoDestroy>(); if (f) f.follow = follow; }
        Track(go);
        return go;
    }

    void Track(GameObject effect)
    {
        activeEffects.RemoveAll(go => go == null);
        activeEffects.Add(effect);
    }

    public void CancelCombatEffects(bool includeAura)
    {
        ClearCharge();
        foreach (var effect in activeEffects)
            if (effect != null && (includeAura || effect != aura))
            { effect.SetActive(false); Destroy(effect); }
        activeEffects.RemoveAll(go => go == null || includeAura || go != aura);
        if (includeAura) aura = null;
    }

    void ClearCharge()
    {
        if (chargeR) Destroy(chargeR);
        if (chargeL) Destroy(chargeL);
    }
}
