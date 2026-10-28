using System;
using UnityEngine;
using UnityEngine.AI;

public interface IBossHealthSource
{
    EnemyHealth Health { get; }
    string DisplayName { get; }
    bool ShowHealthBar { get; }
}

public enum EncounterState { Dormant, Chase, Casting, Recovery, Returning, Dead }
public enum EncounterSkillKind { Melee, Bolt, Nova, Curse, Roar }

/// <summary>Grounded arena combat shared by DemonLord and its dragon guardian.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(Targetable))]
public sealed class EncounterCombatant : MonoBehaviour, IBossHealthSource
{
    [Serializable]
    public sealed class Skill
    {
        public string name, animationState;
        public EncounterSkillKind kind;
        [Min(.1f)] public float duration = 1f;
        [Min(0)] public float minRange;
        [Min(.1f)] public float range = 3.5f;
        [Min(0)] public float damage = 12f;
        [Min(.1f)] public float cooldown = 4f;
        [Min(0)] public float recovery = .65f;
        [Min(0)] public float turnUntil = .2f;
    }

    [SerializeField] private string displayName;
    [SerializeField] private bool showBossHud = true;
    public bool UsesBossHud => showBossHud;
    public void ConfigureArena(BossArenaZone value, bool bossHud) { arena = value; showBossHud = bossHud; }
    [SerializeField] private BossArenaZone arena;
    [SerializeField] private Animator animator;
    [SerializeField] private string openingAnimation;
    [SerializeField] private float openingDuration = 1f;
    [SerializeField] private Skill[] skills;
    [Header("Movement")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float walkSpeed = 1.4f;
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float stoppingRange = 2.5f;
    [SerializeField] private float patrolRadius = 3f;
    [SerializeField] private float sightHeight = 1.8f;
    [SerializeField] private float maxHitHeightDifference = 2.2f;
    [SerializeField] private float deathHudHold = 2f;

    private EnemyHealth health;
    private Targetable targetable;
    private NavMeshAgent agent;
    private Collider body;
    private DemonLordVFX effects;
    private Vector3 home;
    private Quaternion homeRotation;
    private float[] ready;
    private int current = -1, nextSkill;
    private float stateUntil, castStarted, nextPath, nextPatrol, deathTime;
    private bool patrolMoving;
    private NavMeshPath path;
    private static readonly int Speed = Animator.StringToHash("Speed");

    public EncounterState State { get; private set; }
    public Skill CurrentSkill => current >= 0 && current < skills.Length ? skills[current] : null;
    public EnemyHealth Health => health;
    public string DisplayName => displayName;
    public BossArenaZone Arena => arena;
    public Vector3 HomePosition => home;
    public bool IsEngaged => State == EncounterState.Chase || State == EncounterState.Casting || State == EncounterState.Recovery;
    public bool CanEmitSkillEffects => isActiveAndEnabled && State == EncounterState.Casting && health.IsAlive &&
        arena != null && arena.PlayerInside && arena.Player != null && arena.Player.IsAlive;
    public bool ShowHealthBar => showBossHud && isActiveAndEnabled && arena != null && arena.PlayerInside &&
        (IsEngaged || State == EncounterState.Dead && Time.time - deathTime < deathHudHold);
    public event Action<Skill> SkillStarted;
    public event Action MeleeHit;
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>(); targetable = GetComponent<Targetable>();
        agent = GetComponent<NavMeshAgent>(); body = GetComponent<Collider>(); effects = GetComponent<DemonLordVFX>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        home = transform.position; homeRotation = transform.rotation;
        ready = new float[skills.Length];
        path = new NavMeshPath();
        agent.updateRotation = false; animator.applyRootMotion = false;
        if (TryGetComponent(out Rigidbody rigidbody)) rigidbody.interpolation = RigidbodyInterpolation.None;
        if (TryGetComponent(out DemonLordTester tester)) tester.enabled = false;
        SetTargetable(false);
    }
    private void OnEnable() { if (health != null) health.Died += Die; }
    private void Start()
    {
        if (!AgentReady && NavMesh.SamplePosition(home, out var hit, 2f, agent.areaMask))
        {
            // Register authored scene actors only after the surface is available.
            if (!agent.enabled) { transform.position = hit.position; agent.enabled = true; }
            agent.Warp(hit.position);
        }
        StopMoving();
        nextPatrol = Time.time + UnityEngine.Random.Range(.3f, 1.2f);
    }

    private void Update()
    {
        if (health == null || State == EncounterState.Dead || Time.timeScale <= 0f) return;
        if (arena == null) { StopMoving(); SetSpeed(0); return; }
        bool present = arena != null && arena.isActiveAndEnabled && arena.PlayerInside;
        if (!present && IsEngaged) ReturnHome();
        if (State == EncounterState.Returning) { UpdateReturn(); return; }
        if (!present) { UpdatePatrol(); return; }
        if (State == EncounterState.Dormant)
        {
            if (!AgentReady) return;
            StopMoving(); SetTargetable(true); nextSkill = 0;
            Array.Clear(ready, 0, ready.Length);
            if (!string.IsNullOrEmpty(openingAnimation))
            {
                current = -1; State = EncounterState.Casting; castStarted = Time.time;
                stateUntil = Time.time + openingDuration;
                animator.CrossFadeInFixedTime(openingAnimation, .1f, 0, 0);
            }
            else { State = EncounterState.Recovery; stateUntil = Time.time + .75f; }
        }
        if (!AgentReady) return;
        Vector3 offset = arena.Player.transform.position - transform.position;
        float distance = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
        if (State == EncounterState.Casting)
        {
            if (Time.time - castStarted < (CurrentSkill != null ? CurrentSkill.turnUntil : .3f)) Face(offset);
            SetSpeed(0);
            if (Time.time >= stateUntil)
            {
                State = EncounterState.Recovery;
                stateUntil = Time.time + (CurrentSkill != null ? CurrentSkill.recovery : .5f);
            }
            return;
        }
        if (State == EncounterState.Recovery)
        {
            SetSpeed(0); if (Time.time >= stateUntil) State = EncounterState.Chase;
            return;
        }
        if (HasSight(arena.Player.transform))
            for (int n = 0; n < skills.Length; n++)
            {
                int index = (nextSkill + n) % skills.Length;
                var skill = skills[index];
                if (Time.time < ready[index] || distance < skill.minRange || distance > skill.range) continue;
                Cast(index); return;
            }
        if (distance > stoppingRange)
        {
            MoveTo(arena.ClampToArena(arena.Player.transform.position, agent.radius + .4f), chaseSpeed, stoppingRange * .8f);
            Face(agent.desiredVelocity.sqrMagnitude > .01f ? agent.desiredVelocity : offset);
        }
        else { StopMoving(); Face(offset); }
        SetSpeed(agent.velocity.magnitude);
    }

    private void Cast(int index)
    {
        StopMoving(); Face(arena.Player.transform.position - transform.position);
        current = index; nextSkill = (index + 1) % skills.Length;
        var skill = skills[index]; State = EncounterState.Casting; castStarted = Time.time;
        stateUntil = Time.time + skill.duration + .08f; ready[index] = stateUntil + skill.cooldown;
        animator.CrossFadeInFixedTime(skill.animationState, .08f, 0, 0);
        SetSpeed(0); SkillStarted?.Invoke(skill);
    }

    public void ResolveMeleeImpact()
    {
        if (!CanEmitSkillEffects || CurrentSkill == null || CurrentSkill.kind != EncounterSkillKind.Melee) return;
        var victim = arena.Player;
        var delta = victim.transform.position - transform.position;
        var planar = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (Mathf.Abs(delta.y) > maxHitHeightDifference || planar.magnitude > CurrentSkill.range ||
            Vector3.Angle(transform.forward, planar) > 70f || !HasSight(victim.transform)) return;
        victim.TakeDamage(CurrentSkill.damage); MeleeHit?.Invoke();
    }

    public bool HasSight(Transform target) => BossCombatSight.Clear(transform,
        transform.position + Vector3.up * sightHeight, target, target.position + Vector3.up);

    private void UpdatePatrol()
    {
        if (!AgentReady) return;
        if (patrolMoving && !agent.pathPending && (!agent.hasPath || agent.remainingDistance < .4f))
        { StopMoving(); nextPatrol = Time.time + UnityEngine.Random.Range(1.5f, 3f); }
        if (!patrolMoving && Time.time >= nextPatrol)
        {
            var offset = UnityEngine.Random.insideUnitCircle * patrolRadius;
            patrolMoving = MoveTo(arena.ClampToArena(home + new Vector3(offset.x, 0, offset.y), agent.radius + .5f), walkSpeed, .25f);
            nextPatrol = Time.time + 2f;
        }
        Face(agent.desiredVelocity); SetSpeed(agent.velocity.magnitude);
    }

    private void ReturnHome()
    {
        State = EncounterState.Returning; current = -1; SetTargetable(false); StopMoving(); nextPath = 0;
        effects?.CancelCombatEffects(false);
        animator.CrossFadeInFixedTime("Locomotion", .1f, 0, 0);
    }
    public void ResetEncounter()
    {
        if (health == null) return;
        StopMoving(); effects?.CancelCombatEffects(true);
        if (body != null) body.enabled = true;
        if (AgentReady) agent.Warp(home); else transform.position = home;
        transform.rotation = homeRotation; health.RestoreToFullHealth();
        current = -1; nextSkill = 0; State = EncounterState.Dormant;
        Array.Clear(ready, 0, ready.Length); SetTargetable(false);
        animator.Rebind(); animator.Update(0); animator.Play("Locomotion", 0, 0);
    }
    private void UpdateReturn()
    {
        if (Vector3.ProjectOnPlane(transform.position - home, Vector3.up).sqrMagnitude < .25f)
        {
            StopMoving(); transform.rotation = homeRotation; health.RestoreToFullHealth();
            State = EncounterState.Dormant; nextPatrol = Time.time + 2f; SetSpeed(0); return;
        }
        MoveTo(home, chaseSpeed, .15f); Face(agent != null ? agent.desiredVelocity : Vector3.zero);
        SetSpeed(AgentReady ? agent.velocity.magnitude : 0f);
    }
    private bool MoveTo(Vector3 point, float speed, float stoppingDistance)
    {
        if (!AgentReady) return false;
        agent.speed = speed; agent.stoppingDistance = stoppingDistance;
        if (Time.time < nextPath) return agent.hasPath;
        nextPath = Time.time + .25f;
        if (!NavMesh.SamplePosition(point, out var hit, 1.5f, agent.areaMask) || !arena.Contains(hit.position) ||
            !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
        { StopMoving(); return false; }
        agent.isStopped = false; return agent.SetPath(path);
    }
    private void StopMoving()
    { patrolMoving = false; if (AgentReady) { agent.isStopped = true; agent.ResetPath(); } }
    private void Face(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
    private void SetSpeed(float value) { if (animator != null) animator.SetFloat(Speed, value, .15f, Time.deltaTime); }
    private void SetTargetable(bool value) { health.IsInvulnerable = !value; targetable.enabled = value; }
    private void Die()
    {
        if (State == EncounterState.Dead) return;
        State = EncounterState.Dead; deathTime = Time.time; current = -1;
        StopMoving(); SetTargetable(false); effects?.CancelCombatEffects(true);
        if (body != null) body.enabled = false;
        animator.SetFloat(Speed, 0); animator.CrossFadeInFixedTime("Death", .1f, 0, 0);
    }
    private void OnDisable()
    {
        if (health != null) { health.Died -= Die; SetTargetable(false); }
        StopMoving(); effects?.CancelCombatEffects(true);
    }
}
