using UnityEngine;
using System.Collections.Generic;

// ใส่ไว้ที่ตัว King of Demon (ตัวเดียวกับ Animator)
// Animation Event ในแต่ละท่าจะเรียกฟังก์ชัน VFX_... ด้านล่าง เพื่อสร้างเอฟเฟกต์แยกออกมาจากตัวละคร
// เอฟเฟกต์ทุกอันเป็น Prefab ต่างหาก ถูกสร้างตอนใช้ท่า แล้วลบตัวเองเมื่อจบ
public class BossVFXController : MonoBehaviour
{
    [Header("Effect Prefabs")]
    public GameObject chargePrefab;      // พลังวนที่มือตอนชาร์จ
    public GameObject fireballPrefab;    // ลูกไฟ (magic_atk)
    public GameObject shockwavePrefab;   // คลื่นกระแทกวงแหวน (magic_atk_2)
    public GameObject powerAuraPrefab;   // ออร่าปลุกพลัง (power / Phase 2)
    public GameObject slashPrefab;       // รอยฟาด (atk_3)
    public GameObject deathBurstPrefab;  // ควันตอนตาย

    [Header("Bones (หาให้อัตโนมัติถ้าเว้นว่าง)")]
    public Transform rightHand;
    public Transform leftHand;
    public Transform chest;

    [Header("Skill Settings")]
    public float fireballSpeed = 14f;
    public float fireballDamage = 25f;
    public float shockwaveRadius = 7f;
    public float shockwaveDamage = 20f;
    [Min(.01f), Tooltip("Effect size independently of the character's body scale.")]
    public float effectScaleMultiplier = 1f;
    [Tooltip("ชื่อฟังก์ชันรับดาเมจบน Player ที่จะเรียกผ่าน SendMessage(float)")]
    public string damageMethod = "TakeDamage";

    // ฝั่งโปรแกรมใช้ event นี้แทน SendMessage ได้: (ตัวที่โดน, ดาเมจ)
    public static event System.Action<GameObject, float> PlayerHit;
    internal static void RaisePlayerHit(GameObject target, float dmg, string method)
    {
        if (PlayerHit != null) PlayerHit(target, dmg);
        else if (!string.IsNullOrEmpty(method)) target.SendMessage(method, dmg, SendMessageOptions.DontRequireReceiver);
    }

    Transform player;
    GameObject chargeR, chargeL;
    KingOfDemonAI combat;
    readonly List<GameObject> combatEffects = new List<GameObject>();
    public event System.Action SlashImpact;
    public event System.Action FireballReleased;
    public event System.Action ShockwaveReleased;
    bool CanEmit => combat == null || combat.CanEmitSkillEffects;

    void Awake()
    {
        combat = GetComponent<KingOfDemonAI>();
        if (!rightHand) rightHand = FindBone("RightHand");
        if (!leftHand) leftHand = FindBone("LeftHand");
        if (!chest) chest = FindBone("Spine2");
    }

    float Scale => transform.lossyScale.x;
    float EffectScale => Scale * effectScaleMultiplier;

    Transform Player()
    {
        if (!player) { var p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; }
        return player;
    }

    // ---------- Animation Events ----------
    public void VFX_ChargeStart()
    {
        if (!CanEmit) return;
        ClearCharge();
        if (!chargePrefab) return;
        if (rightHand) chargeR = Spawn(chargePrefab, rightHand.position, Quaternion.identity, rightHand);
        if (leftHand) chargeL = Spawn(chargePrefab, leftHand.position, Quaternion.identity, leftHand);
    }

    public void VFX_Fireball()
    {
        ClearCharge();
        if (!CanEmit) return;
        if (!fireballPrefab) return;
        Vector3 origin = HandsCenter();
        Vector3 dir = transform.forward;
        var p = Player();
        if (p)
        {
            var toPlayer = p.position + Vector3.up - origin;
            dir = combat != null
                ? transform.forward * Vector3.ProjectOnPlane(toPlayer, Vector3.up).magnitude + Vector3.up * toPlayer.y
                : toPlayer;
        }
        dir.y = Mathf.Clamp(dir.y, -0.3f * dir.magnitude, 0.3f * dir.magnitude);
        dir.Normalize();
        var go = Spawn(fireballPrefab, origin + dir * 0.4f * Scale, Quaternion.LookRotation(dir), null);
        var proj = go.GetComponent<MagicProjectile>();
        if (proj) proj.Launch(dir, fireballSpeed, fireballDamage, damageMethod, gameObject);
        FireballReleased?.Invoke();
    }

    public void VFX_Shockwave()
    {
        ClearCharge();
        if (!CanEmit) return;
        if (!shockwavePrefab) return;
        var go = Spawn(shockwavePrefab, transform.position + Vector3.up * 0.1f, Quaternion.identity, null);
        var ring = go.GetComponent<ShockwaveRing>();
        if (ring) ring.Init(shockwaveRadius * EffectScale, shockwaveDamage, damageMethod, gameObject);
        ShockwaveReleased?.Invoke();
    }

    public void VFX_Power()
    {
        if (!CanEmit) return;
        if (!powerAuraPrefab) return;
        var go = Spawn(powerAuraPrefab, transform.position, Quaternion.identity, null);
        var f = go.GetComponent<VFXAutoDestroy>(); if (f) f.follow = transform;
    }

    public void VFX_Slash()
    {
        if (!CanEmit) return;
        SlashImpact?.Invoke();
        if (!slashPrefab) return;
        Vector3 pos = transform.position + transform.forward * 1.0f * Scale + Vector3.up * 1.1f * Scale;
        Spawn(slashPrefab, pos, Quaternion.LookRotation(transform.forward), null);
    }

    public void VFX_DeathBurst()
    {
        ClearCharge();
        if (!deathBurstPrefab) return;
        Vector3 pos = chest ? chest.position : transform.position + Vector3.up * Scale;
        Spawn(deathBurstPrefab, pos, Quaternion.identity, null);
    }

    // ---------- helpers ----------
    GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform follow)
    {
        var go = Instantiate(prefab, pos, rot);       // ไม่ใส่เป็นลูกของตัวละคร เอฟเฟกต์แยกจากตัวละครเสมอ
        go.transform.localScale = prefab.transform.localScale * EffectScale;
        if (follow) { var f = go.GetComponent<VFXAutoDestroy>(); if (f) f.follow = follow; }
        if (combat != null)
        {
            combatEffects.RemoveAll(effect => effect == null);
            combatEffects.Add(go);
        }
        return go;
    }

    Vector3 HandsCenter()
    {
        if (rightHand && leftHand) return (rightHand.position + leftHand.position) * 0.5f;
        if (rightHand) return rightHand.position;
        return transform.position + Vector3.up * 1.4f * Scale + transform.forward * 0.6f * Scale;
    }

    void ClearCharge()
    {
        if (chargeR) Destroy(chargeR);
        if (chargeL) Destroy(chargeL);
    }

    public void CancelCombatEffects()
    {
        ClearCharge();
        foreach (var effect in combatEffects) if (effect != null) Destroy(effect);
        combatEffects.Clear();
    }

    void OnDisable() { CancelCombatEffects(); }

    Transform FindBone(string suffix)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }
}
