using UnityEngine;
using UnityEngine.AI;

public enum TownNpcState { Idle, Walking, Talking, Interacting, Outbound, Exploring, Returning, Fleeing, Dead, Working, Carrying }
public enum TownNpcGender { Male, Female }

[RequireComponent(typeof(NavMeshAgent)), RequireComponent(typeof(CivilianHealth)), DisallowMultipleComponent]
public sealed class TownNpcBrain : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private TownNpcGender gender;
    [SerializeField, Min(.1f)] private float walkSpeed = 1.65f;
    [SerializeField, Min(.1f)] private float runSpeed = 4.5f;
    [SerializeField, Min(.1f)] private float fleeSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float routineRunChance = .15f;
    [SerializeField, Min(.1f)] private float threatRadius = 10f;
    [SerializeField, Min(.1f)] private float safeAfterSeconds = 4f;
    [SerializeField] private Vector2 idleSeconds = new Vector2(2f, 5f);
    [SerializeField] private Vector2 outingCooldown = new Vector2(18f, 45f);
    [SerializeField] private Vector2 outsideStaySeconds = new Vector2(8f, 16f);
    [SerializeField] private LayerMask sightBlockers = 1 << 7;
    private TownNpcPopulation population;
    private CivilianHealth health;
    private NavMeshAgent agent;
    private NavMeshPath path;
    private TownNpcActivityPoint destination;
    private TownNpcBrain partner;
    private float stateUntil, nextOuting, nextThreatScan, lastThreatAt = -1000f, nextFleePath, pathDeadline;
    private float outsideUntil, socialCooldown;
    private EnemyAI threat;
    private string animationState;
    private TownConstructionJob constructionJob;
    public TownNpcState State { get; private set; }
    public bool CanLeaveTown { get; private set; }
    public TownNpcPopulation Population => population;
    public NavMeshAgent Agent => agent;
    public EnemyAI Threat => threat;
    public TownNpcGender Gender => gender;
    public bool IsConstructionWorker => constructionJob != null || GetComponent<TownConstructionJob>() != null;
    public bool JobMovementFinished => AgentReady && (Arrived() || Time.time >= pathDeadline);
    private bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;
    public void Configure(TownNpcPopulation owner, bool mayLeave)
    {
        population = owner; CanLeaveTown = mayLeave && !IsConstructionWorker;
        nextOuting = Time.time + Random.Range(outingCooldown.x, outingCooldown.y);
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>(); health = GetComponent<CivilianHealth>(); path = new NavMeshPath();
        constructionJob = GetComponent<TownConstructionJob>();
        agent.updateRotation = false;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }
    private void Start() { stateUntil = Time.time + Random.Range(.2f, 2f); Play("Locomotion"); }
    private void Update()
    {
        if (Time.timeScale <= 0f || State == TownNpcState.Dead || !health.IsAlive || !AgentReady) return;
        if (population == null || !population.isActiveAndEnabled) { Stop(); return; }
        if (Time.time >= nextThreatScan) { nextThreatScan = Time.time + .25f; ScanThreats(); }
        if (State == TownNpcState.Fleeing) UpdateFlee();
        else if (constructionJob != null && constructionJob.IsAssigned) constructionJob.Tick();
        else if (State == TownNpcState.Talking)
        {
            if (partner == null || !partner.health.IsAlive || partner.State != TownNpcState.Talking || Time.time >= stateUntil) Idle();
            else Face(partner.transform.position - transform.position);
        }
        else if (State == TownNpcState.Interacting)
        {
            if (destination != null) Face(destination.transform.forward);
            if (Time.time >= stateUntil) Idle();
        }
        else if (State == TownNpcState.Idle || State == TownNpcState.Exploring)
        {
            if (Time.time >= stateUntil) ChooseRoutine();
        }
        else
        {
            Face(agent.desiredVelocity);
            if (Arrived() || Time.time >= pathDeadline) Arrive();
        }
        if (animator != null) animator.SetFloat("Speed", AgentReady && !agent.isStopped ? agent.velocity.magnitude : 0f, .12f, Time.deltaTime);
    }
    private void ScanThreats()
    {
        threat = null; float nearest = threatRadius * threatRadius;
        foreach (var enemy in EnemyAI.ActiveEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            var hp = enemy.GetComponent<EnemyHealth>(); if (hp != null && !hp.IsAlive) continue;
            Vector3 delta = enemy.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 3f || delta.sqrMagnitude > nearest || !HasSight(enemy.transform.position)) continue;
            nearest = delta.sqrMagnitude; threat = enemy;
        }
        if (threat == null) return;
        lastThreatAt = Time.time;
        if (State != TownNpcState.Fleeing) { BreakConversation(); constructionJob?.Suspend(); State = TownNpcState.Fleeing; Stop(); Play("Locomotion"); nextFleePath = 0f; }
    }
    private bool HasSight(Vector3 point)
    {
        Vector3 eye = transform.position + Vector3.up * 1.4f;
        Vector3 delta = point + Vector3.up - eye;
        return !Physics.Raycast(eye, delta.normalized, delta.magnitude, sightBlockers, QueryTriggerInteraction.Ignore);
    }
    private void UpdateFlee()
    {
        if (Time.time - lastThreatAt >= safeAfterSeconds)
        {
            if (population.HasOutingPermit(this)) ReturnHome(); else Idle();
            return;
        }
        if (Time.time >= nextFleePath)
        {
            nextFleePath = Time.time + .65f;
            Vector3 danger = threat != null ? threat.transform.position : transform.position - transform.forward;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - danger, Vector3.up).normalized;
            if (away.sqrMagnitude < .001f) away = transform.forward;
            bool found = false;
            for (int i = 0; i < 12; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i == 0 ? 0 : Random.Range(-100f, 100f), 0) * away;
                if (Move(transform.position + direction * Random.Range(5f, 9f), fleeSpeed, population.HasOutingPermit(this))) { found = true; break; }
            }
            if (!found)
            {
                // A resident trapped against a city edge chooses the safest reachable INNER point.
                TownNpcActivityPoint safest = null; float best = -1;
                foreach (var point in population.CityPoints)
                {
                    if (point == null || !CanReach(point.transform.position, population.HasOutingPermit(this))) continue;
                    float score = (point.transform.position - danger).sqrMagnitude;
                    if (score > best) { best = score; safest = point; }
                }
                if (safest != null) Move(safest.transform.position, fleeSpeed, population.HasOutingPermit(this));
            }
        }
        Face(agent.desiredVelocity);
    }
    private void ChooseRoutine()
    {
        if (population.HasOutingPermit(this))
        {
            if (State != TownNpcState.Outbound && Time.time >= outsideUntil) { ReturnHome(); return; }
            if (population.TryDestination(this, true, out var outside)) { destination = outside; if (Move(outside.transform.position, walkSpeed, true)) { State = TownNpcState.Outbound; Play("Locomotion"); return; } }
            ReturnHome(); return;
        }
        if (CanLeaveTown && Time.time >= nextOuting)
        {
            nextOuting = Time.time + Random.Range(outingCooldown.x, outingCooldown.y);
            if (TryBeginOuting()) return;
        }
        if (population.TryDestination(this, false, out var point))
        {
            if (TryVisit(point, Random.value < routineRunChance)) return;
        }
        Idle();
    }
    public bool TryVisit(TownNpcActivityPoint point, bool run = false)
    {
        if (point == null || population == null || !health.IsAlive || IsConstructionWorker || State == TownNpcState.Fleeing ||
            population.HasOutingPermit(this) || !population.Contains(point.transform.position)) return false;
        if (!Move(point.transform.position, run ? runSpeed : walkSpeed, false)) return false;
        BreakConversation(); destination = point; State = TownNpcState.Walking; Play("Locomotion"); return true;
    }
    public bool TryBeginOuting()
    {
        if (!health.IsAlive || population == null || !population.TryReserveOuting(this)) return false;
        if (population.TryDestination(this, true, out var point) && Move(point.transform.position, walkSpeed, true))
        {
            BreakConversation(); destination = point; State = TownNpcState.Outbound; outsideUntil = float.PositiveInfinity; Play("Locomotion"); return true;
        }
        population.ReleaseOuting(this); return false;
    }
    private void ReturnHome()
    {
        if (population.TryDestination(this, false, out var point) && Move(point.transform.position, walkSpeed, true))
        { destination = point; State = TownNpcState.Returning; Play("Locomotion"); }
        else { Stop(); State = TownNpcState.Exploring; stateUntil = Time.time + 1f; outsideUntil = Time.time; }
    }
    private void Arrive()
    {
        Stop();
        if (State == TownNpcState.Returning)
        {
            if (population.Contains(transform.position)) { population.ReleaseOuting(this); nextOuting = Time.time + Random.Range(outingCooldown.x, outingCooldown.y); Idle(); }
            else ReturnHome();
        }
        else if (State == TownNpcState.Outbound)
        {
            if (float.IsPositiveInfinity(outsideUntil)) outsideUntil = Time.time + Random.Range(outsideStaySeconds.x, outsideStaySeconds.y);
            State = TownNpcState.Exploring; stateUntil = Time.time + Random.Range(2f, 4f); Play("Locomotion");
        }
        else if (destination != null && destination.Activity == TownNpcActivityKind.Interact)
        { State = TownNpcState.Interacting; stateUntil = Time.time + 2.1f; Play("Interact"); }
        else
        {
            if (TryFindConversation()) return;
            Idle();
        }
    }
    private bool TryFindConversation()
    {
        if (Time.time < socialCooldown) return false;
        foreach (var other in CivilianHealth.Living)
        {
            var brain = other != null ? other.GetComponent<TownNpcBrain>() : null;
            if (brain == null || brain == this || brain.population != population || (brain.transform.position - transform.position).sqrMagnitude > 3f * 3f) continue;
            if (TryTalkWith(brain)) return true;
        }
        return false;
    }
    public bool TryTalkWith(TownNpcBrain other)
    {
        if (other == null || other == this || other.population != population || (other.transform.position - transform.position).sqrMagnitude > 9f || !CanSocialise() || !other.CanSocialise() || !HasSight(other.transform.position)) return false;
        float until = Time.time + Random.Range(4f, 7f);
        partner = other; other.partner = this;
        State = other.State = TownNpcState.Talking; stateUntil = other.stateUntil = until;
        Stop(); other.Stop(); Play("Talking"); other.Play("Talking"); return true;
    }
    private bool CanSocialise() => population != null && health.IsAlive && AgentReady && !IsConstructionWorker && Time.time >= socialCooldown && !population.HasOutingPermit(this) &&
        (State == TownNpcState.Idle || State == TownNpcState.Walking);
    private void BreakConversation()
    {
        var previous = partner; partner = null; socialCooldown = Time.time + 8f;
        if (previous != null && previous.partner == this) { previous.partner = null; if (previous.State == TownNpcState.Talking) previous.Idle(); }
    }
    private void Idle()
    {
        BreakConversation(); Stop(); State = TownNpcState.Idle; stateUntil = Time.time + Random.Range(idleSeconds.x, idleSeconds.y); Play("Locomotion");
    }
    public bool CanReach(Vector3 requested, bool allowOutside)
    {
        if (!AgentReady || population == null) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(requested, out var hit, 1.5f, filter) || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        if (!allowOutside) foreach (var corner in path.corners) if (!population.Contains(corner)) return false;
        return allowOutside || population.Contains(hit.position);
    }
    private bool Move(Vector3 requested, float speed, bool allowOutside)
    {
        if (!CanReach(requested, allowOutside)) return false;
        agent.speed = speed; agent.isStopped = false; agent.SetPath(path);
        pathDeadline = Time.time + path.corners.Length * 2f + Vector3.Distance(transform.position, requested) / Mathf.Max(.1f, speed) + 8f;
        return true;
    }
    private bool Arrived() => !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .15f);
    public bool TryJobMove(Vector3 point, float speed, bool carrying)
    {
        if (constructionJob == null || constructionJob.Station == null || !health.IsAlive || State == TownNpcState.Fleeing || !constructionJob.Station.Contains(point)) return false;
        if (!CanReach(point, false)) return false;
        if (constructionJob.Station.Contains(transform.position))
            foreach (var corner in path.corners) if (!constructionJob.Station.Contains(corner)) return false;
        agent.speed = speed; agent.isStopped = false; agent.SetPath(path);
        pathDeadline = Time.time + Vector3.Distance(transform.position, point) / Mathf.Max(.1f, speed) + 15f;
        State = carrying ? TownNpcState.Carrying : TownNpcState.Walking;
        Play(carrying ? "WalkCarry" : "Locomotion"); return true;
    }
    public void SetJobStationary(string state) { Stop(); State = TownNpcState.Working; Play(state); }
    public void FaceJobDirection(Vector3 direction) => Face(direction);
    private void Stop() { if (AgentReady) { agent.isStopped = true; if (agent.hasPath) agent.ResetPath(); } }
    private void Face(Vector3 direction)
    {
        direction.y = 0; if (direction.sqrMagnitude < .001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
    }
    private void Play(string state)
    {
        if (animator == null || animator.runtimeAnimatorController == null || animationState == state) return;
        animationState = state; animator.CrossFadeInFixedTime(state, .18f);
    }
    public void HandleDeath()
    {
        BreakConversation(); constructionJob?.Suspend(); Stop(); if (population != null) population.ReleaseOuting(this);
        State = TownNpcState.Dead; if (agent != null) agent.enabled = false; Play("Death");
    }
    private void OnDisable() { BreakConversation(); constructionJob?.Suspend(); Stop(); if (population != null) population.ReleaseOuting(this); }
}
