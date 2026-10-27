using System;
using UnityEngine;
using UnityEngine.AI;

public enum DemonBossState { Dormant, Chase, Casting, Recovery, Returning, Dead }
public enum DemonBossSkill { Slash, Fireball, Shockwave }

[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(Targetable))]
public sealed class KingOfDemonAI : MonoBehaviour, IBossHealthSource
{
    [Header("Encounter")]
    [SerializeField] private BossArenaZone arena;
    [SerializeField] private Animator animator;
    [SerializeField] private BossVFXController effects;
    [SerializeField] private string displayName = "KING OF DEMON";
    [SerializeField, Min(0f)] private float openingDelay = .85f;
    [Header("Movement")]
    [SerializeField, Min(.1f)] private float chaseSpeed = 3.5f;
    [SerializeField, Min(1f)] private float turnSpeed = 240f;
    [SerializeField, Min(.1f)] private float sightHeight = 1.5f;
    [Header("Skills (world metres and seconds)")]
    [SerializeField, Min(.1f)] private float meleeRange = 3.2f;
    [SerializeField, Min(0f)] private float meleeDamage = 18f;
    [SerializeField, Range(1, 180)] private float meleeHalfAngle = 65f;
    [SerializeField, Min(.1f)] private float fireballRange = 17f;
    [SerializeField, Min(.1f)] private float shockwaveRange = 6.5f;
    [SerializeField, Min(.1f)] private float slashCooldown = 3.6f;
    [SerializeField, Min(.1f)] private float fireballCooldown = 6f;
    [SerializeField, Min(.1f)] private float shockwaveCooldown = 10f;
    [SerializeField, Min(.1f)] private float recoveryTime = .65f;
    [SerializeField, Min(.1f)] private float deathHudHold = 2f;

    private EnemyHealth health;
    private NavMeshAgent agent;
    private Targetable targetable;
    private Collider body;
    private Vector3 home;
    private Quaternion homeRotation;
    private float nextDecision, castStarted, stateUntil, nextRepath;
    private float slashReady, fireballReady, shockwaveReady, deathTime;
    private float slashLength = 2.2f, fireballLength = 2.7f, shockwaveLength = 3.317f;
    private bool subscribed;
    private static readonly int Speed = Animator.StringToHash("Speed");

    public DemonBossState State { get; private set; }
    public DemonBossSkill CurrentSkill { get; private set; }
    public EnemyHealth Health => health;
    public BossArenaZone Arena => arena;
    public string DisplayName => displayName;
    public Vector3 HomePosition => home;
    public bool CanEmitSkillEffects => isActiveAndEnabled && State == DemonBossState.Casting &&
        health != null && health.IsAlive && arena != null && arena.PlayerInside && arena.Player != null && arena.Player.IsAlive;
    public bool ShowHealthBar => isActiveAndEnabled && arena != null && arena.PlayerInside &&
        (IsEngaged || State == DemonBossState.Dead && Time.time - deathTime < deathHudHold);
    public bool IsEngaged => State == DemonBossState.Chase || State == DemonBossState.Casting || State == DemonBossState.Recovery;
    public event Action<DemonBossSkill> SkillStarted;
    public event Action MeleeHit;

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        targetable = GetComponent<Targetable>();
        body = GetComponent<Collider>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (effects == null) effects = GetComponent<BossVFXController>();
        home = transform.position;
        homeRotation = transform.rotation;
        agent.updateRotation = false;
        if (TryGetComponent(out Rigidbody rigidbody)) rigidbody.interpolation = RigidbodyInterpolation.None;
        if (TryGetComponent(out BossSkillTester tester)) tester.enabled = false;
        animator.applyRootMotion = false;
        if (animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.EndsWith("_Attack")) slashLength = clip.length;
                if (clip.name.EndsWith("_Magic")) fireballLength = clip.length;
                if (clip.name.EndsWith("_Magic2")) shockwaveLength = clip.length;
            }
        SetTargetable(false);
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.Died += Die;
        if (effects != null) effects.SlashImpact += ResolveMeleeHit;
        subscribed = true;
    }

    private void Start()
    {
        StopMoving();
        if (!AgentReady && NavMesh.SamplePosition(home, out var hit, 2f, agent.areaMask)) agent.Warp(hit.position);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || health == null || State == DemonBossState.Dead) return;
        bool present = arena != null && arena.isActiveAndEnabled && arena.PlayerInside;
        if (!present && IsEngaged) BeginReturn();
        if (State == DemonBossState.Returning) { UpdateReturn(); return; }
        if (!present) { SetSpeed(0f); return; }
        if (State == DemonBossState.Dormant)
        {
            if (!AgentReady) return;
            SetTargetable(true);
            State = DemonBossState.Chase;
            nextDecision = Time.time + openingDelay;
            slashReady = fireballReady = shockwaveReady = Time.time;
        }
        if (!AgentReady) return;
        var target = arena.Player.transform;
        var offset = target.position - transform.position;
        float distance = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;

        if (State == DemonBossState.Casting)
        {
            // Commit to the swing, and to the projectile direction at its release frame.
            float faceUntil = CurrentSkill == DemonBossSkill.Fireball ? 1.15f : .4f;
            if (Time.time - castStarted < faceUntil) Face(offset);
            if (Time.time >= stateUntil) { State = DemonBossState.Recovery; stateUntil = Time.time + recoveryTime; }
            SetSpeed(0f);
            return;
        }
        if (State == DemonBossState.Recovery)
        {
            SetSpeed(0f);
            if (Time.time >= stateUntil) State = DemonBossState.Chase;
            return;
        }

        if (Time.time >= nextDecision && HasSight(target))
        {
            if (distance <= meleeRange && Time.time >= slashReady) { Cast(DemonBossSkill.Slash); return; }
            if (distance <= shockwaveRange && Time.time >= shockwaveReady) { Cast(DemonBossSkill.Shockwave); return; }
            if (distance <= fireballRange && Time.time >= fireballReady) { Cast(DemonBossSkill.Fireball); return; }
        }
        if (distance > meleeRange * .8f)
        {
            MoveTo(arena.ClampToArena(target.position, agent.radius + .3f), chaseSpeed);
            Face(agent.desiredVelocity.sqrMagnitude > .05f ? agent.desiredVelocity : offset);
        }
        else { StopMoving(); Face(offset); }
        SetSpeed(agent.velocity.magnitude);
    }

    private void Cast(DemonBossSkill skill)
    {
        StopMoving();
        Face(arena.Player.transform.position - transform.position);
        CurrentSkill = skill;
        State = DemonBossState.Casting;
        castStarted = Time.time;
        float duration = skill == DemonBossSkill.Slash ? slashLength : skill == DemonBossSkill.Fireball ? fireballLength : shockwaveLength;
        stateUntil = Time.time + duration;
        if (skill == DemonBossSkill.Slash) slashReady = stateUntil + slashCooldown;
        if (skill == DemonBossSkill.Fireball) fireballReady = stateUntil + fireballCooldown;
        if (skill == DemonBossSkill.Shockwave) shockwaveReady = stateUntil + shockwaveCooldown;
        foreach (string trigger in new[] { "Attack", "Magic", "Magic2" }) animator.ResetTrigger(trigger);
        animator.SetTrigger(skill == DemonBossSkill.Slash ? "Attack" : skill == DemonBossSkill.Fireball ? "Magic" : "Magic2");
        SetSpeed(0f);
        SkillStarted?.Invoke(skill);
    }

    private void ResolveMeleeHit()
    {
        if (!CanEmitSkillEffects || CurrentSkill != DemonBossSkill.Slash) return;
        var player = arena.Player;
        var offset = player.transform.position - transform.position;
        var planar = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (Mathf.Abs(offset.y) > 2.2f || planar.magnitude > meleeRange ||
            Vector3.Angle(transform.forward, planar) > meleeHalfAngle || !HasSight(player.transform)) return;
        player.TakeDamage(meleeDamage);
        MeleeHit?.Invoke();
    }

    public bool HasSight(Transform target) => BossCombatSight.Clear(transform,
        transform.position + Vector3.up * sightHeight, target, target.position + Vector3.up);

    private void BeginReturn()
    {
        State = DemonBossState.Returning;
        SetTargetable(false);
        effects?.CancelCombatEffects();
        animator.Play("Locomotion", 0, 0f);
        StopMoving();
        nextRepath = 0;
    }

    private void UpdateReturn()
    {
        if (Vector3.ProjectOnPlane(transform.position - home, Vector3.up).sqrMagnitude < .2f)
        {
            StopMoving();
            transform.rotation = homeRotation;
            health.RestoreToFullHealth();
            State = DemonBossState.Dormant;
            SetSpeed(0f);
            return;
        }
        if (!AgentReady) return;
        MoveTo(home, chaseSpeed * 1.15f);
        Face(agent.desiredVelocity);
        SetSpeed(agent.velocity.magnitude);
    }

    private void MoveTo(Vector3 point, float speed)
    {
        if (!AgentReady) return;
        agent.speed = speed;
        agent.stoppingDistance = State == DemonBossState.Returning ? .15f : meleeRange * .7f;
        if (Time.time < nextRepath) return;
        nextRepath = Time.time + .2f;
        if (!NavMesh.SamplePosition(point, out var hit, 1.5f, agent.areaMask) ||
            arena != null && !arena.Contains(hit.position)) { StopMoving(); return; }
        agent.isStopped = false;
        agent.SetDestination(hit.position);
    }

    private void StopMoving()
    {
        if (AgentReady) { agent.isStopped = true; agent.ResetPath(); }
    }
    private void Face(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
    private void SetSpeed(float speed) => animator.SetFloat(Speed, speed, .15f, Time.deltaTime);
    private void SetTargetable(bool value) { health.IsInvulnerable = !value; targetable.enabled = value; }

    private void Die()
    {
        if (State == DemonBossState.Dead) return;
        State = DemonBossState.Dead;
        deathTime = Time.time;
        SetTargetable(false);
        StopMoving();
        effects?.CancelCombatEffects();
        if (body != null) body.enabled = false;
        animator.SetFloat(Speed, 0);
        animator.ResetTrigger("Attack"); animator.ResetTrigger("Magic"); animator.ResetTrigger("Magic2");
        animator.SetTrigger("Die");
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            health.Died -= Die;
            if (effects != null) effects.SlashImpact -= ResolveMeleeHit;
            subscribed = false;
        }
        StopMoving();
        effects?.CancelCombatEffects();
        if (health != null) SetTargetable(false);
    }
}

/// <summary>Solid scenery blocks attacks; the caster, victim and triggers are ignored.</summary>
public static class BossCombatSight
{
    public static bool Clear(Transform owner, Vector3 origin, Transform target, Vector3 end)
    {
        var delta = end - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var t = hit.transform;
            if (owner != null && (t == owner || t.IsChildOf(owner))) continue;
            if (target != null && (t == target || t.IsChildOf(target))) continue;
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;
            return false;
        }
        return true;
    }
}
