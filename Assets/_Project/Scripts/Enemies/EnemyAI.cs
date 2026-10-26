using UnityEngine;
using UnityEngine.AI;

public enum EnemyAwarenessState { Patrol, Chase, Investigate, Search, Return }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Sight (solid colliders block vision; triggers do not)")]
    [Min(0.1f)] [SerializeField] private float detectionRange = 12f;
    [Min(0.1f)] [SerializeField] private float loseTargetRange = 16f;
    [Range(1f, 179f)] [SerializeField] private float fieldOfView = 110f;
    [Min(0.1f)] [SerializeField] private float eyeHeight = 1.45f;
    [SerializeField] private LayerMask sightBlockers = Physics.DefaultRaycastLayers;

    [Header("Memory and search")]
    [Tooltip("Maximum time spent reaching the last seen point, before looking around.")]
    [Min(0.1f)] [SerializeField] private float investigateDuration = 4f;
    [Min(0.1f)] [SerializeField] private float searchDuration = 5f;
    [Min(0f)] [SerializeField] private float searchRadius = 2.5f;
    [Min(1f)] [SerializeField] private float searchTurnSpeed = 100f;

    [Header("Patrol around spawn")]
    [Min(1f)] [SerializeField] private float patrolRadius = 7f;
    [Min(0f)] [SerializeField] private float patrolPauseMin = 0.6f;
    [Min(0f)] [SerializeField] private float patrolPauseMax = 1.4f;

    [Header("Movement")]
    [Min(0.1f)] [SerializeField] private float patrolSpeed = 1.8f;
    [Tooltip("0 preserves each prefab's original NavMeshAgent chase speed.")]
    [Min(0f)] [SerializeField] private float chaseSpeed;
    [Min(0.1f)] [SerializeField] private float attackRange = 1.6f;
    [Min(1f)] [SerializeField] private float rotationSpeed = 720f;
    [Min(0.05f)] [SerializeField] private float repathInterval = 0.2f;

    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private NavMeshAgent agent;
    private NavMeshPath navigationPath;
    private Vector3 patrolOrigin, lastSeenPosition;
    private float originalChaseSpeed, stateStarted, lastSeenTime, nextPathTime;
    private float nextPatrolTime, destinationDeadline, nextSearchMoveTime, nextPlayerLookup;
    private bool hasDestination;

    public EnemyAwarenessState State { get; private set; } = EnemyAwarenessState.Patrol;
    public bool HasTargetMemory => State == EnemyAwarenessState.Chase ||
        State == EnemyAwarenessState.Investigate || State == EnemyAwarenessState.Search;
    public bool CanSeeTarget { get; private set; }
    public Vector3 LastSeenPosition => lastSeenPosition;
    public float TimeWithoutSight => HasTargetMemory ? Time.time - lastSeenTime : 0f;
    public float PatrolSpeed => patrolSpeed;
    public float ChaseSpeed => chaseSpeed > 0f ? chaseSpeed : originalChaseSpeed;
    public bool IsMoving => AgentReady && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;
    public bool IsInAttackRange { get; private set; }
    public Transform Target => target;
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        originalChaseSpeed = Mathf.Max(0.1f, agent.speed);
        agent.updateRotation = false;
        // NavMeshAgent owns the root Transform each frame. Rigidbody interpolation
        // otherwise rewrites it from stale physics poses, making real movement much
        // slower than agent.velocity (and causing steering jitter). Keep the body
        // kinematic for collisions, but do not interpolate a non-physics mover.
        if (TryGetComponent(out Rigidbody body) && body.isKinematic)
            body.interpolation = RigidbodyInterpolation.None;
        navigationPath = new NavMeshPath();
        patrolOrigin = transform.position;
    }

    private void Start()
    {
        FindPlayer();
        // No initial wait: newly spawned enemies immediately choose a patrol point.
        nextPatrolTime = Time.time;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || !AgentReady) return;
        if (target == null && Time.time >= nextPlayerLookup) FindPlayer();

        CanSeeTarget = HasVisualContact();
        IsInAttackRange = false;
        if (CanSeeTarget)
        {
            // The ONLY place that writes a live target position to memory.
            lastSeenPosition = target.position;
            lastSeenTime = Time.time;
            if (State != EnemyAwarenessState.Chase) EnterState(EnemyAwarenessState.Chase);
        }
        else if (State == EnemyAwarenessState.Chase)
        {
            EnterState(EnemyAwarenessState.Investigate);
            if (!MoveTo(lastSeenPosition, ChaseSpeed, 0.3f, true))
                EnterState(EnemyAwarenessState.Search);
        }

        switch (State)
        {
            case EnemyAwarenessState.Patrol: UpdatePatrol(); break;
            case EnemyAwarenessState.Chase: UpdateChase(); break;
            case EnemyAwarenessState.Investigate:
                if (ReachedDestination() || Time.time - stateStarted >= investigateDuration)
                    EnterState(EnemyAwarenessState.Search);
                else FaceMovement();
                break;
            case EnemyAwarenessState.Search: UpdateSearch(); break;
            case EnemyAwarenessState.Return: UpdateReturn(); break;
        }
    }

    private void UpdateChase()
    {
        if (TargetWithinReach())
        {
            IsInAttackRange = true;
            StopMoving();
            FaceDirection(lastSeenPosition - transform.position);
            return;
        }
        // Stop slightly inside reach, not just outside the attack threshold.
        MoveTo(lastSeenPosition, ChaseSpeed, attackRange * 0.8f);
        FaceMovement();
    }

    private void UpdatePatrol()
    {
        if (hasDestination)
        {
            if (!ReachedDestination() && Time.time < destinationDeadline) { FaceMovement(); return; }
            StopMoving();
            nextPatrolTime = Time.time + Random.Range(patrolPauseMin, Mathf.Max(patrolPauseMin, patrolPauseMax));
        }
        if (Time.time < nextPatrolTime) return;
        if (!ChooseReachablePoint(patrolOrigin, patrolRadius, patrolSpeed))
            nextPatrolTime = Time.time + 1f;
    }

    private void UpdateSearch()
    {
        if (Time.time - stateStarted >= searchDuration)
        {
            EnterState(EnemyAwarenessState.Return);
            if (!MoveTo(patrolOrigin, patrolSpeed, 0.4f, true)) EnterState(EnemyAwarenessState.Patrol);
            return;
        }
        if (hasDestination && !ReachedDestination() && Time.time < destinationDeadline) FaceMovement();
        else
        {
            StopMoving();
            // Visible uncertainty: scan left/right before checking nearby spots.
            float yaw = Mathf.Sin((Time.time - stateStarted) * 1.8f) * searchTurnSpeed;
            transform.Rotate(0f, yaw * Time.deltaTime, 0f, Space.World);
            if (Time.time >= nextSearchMoveTime)
            {
                ChooseReachablePoint(lastSeenPosition, searchRadius, patrolSpeed);
                nextSearchMoveTime = Time.time + Random.Range(1.4f, 2f);
            }
        }
    }

    private void UpdateReturn()
    {
        if (ReachedDestination() || Time.time >= destinationDeadline) EnterState(EnemyAwarenessState.Patrol);
        else FaceMovement();
    }

    private void EnterState(EnemyAwarenessState state)
    {
        StopMoving();
        State = state;
        stateStarted = Time.time;
        nextPathTime = 0f;
        if (state == EnemyAwarenessState.Search) nextSearchMoveTime = Time.time + 1f;
        if (state == EnemyAwarenessState.Patrol) nextPatrolTime = Time.time;
    }

    private bool ChooseReachablePoint(Vector3 center, float radius, float speed)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            if ((point - transform.position).sqrMagnitude < 1f) continue;
            if (MoveTo(point, speed, 0.3f, true)) return true;
        }
        return false;
    }

    private bool MoveTo(Vector3 point, float speed, float stoppingDistance, bool force = false)
    {
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        if (!force && hasDestination && Time.time < nextPathTime) return true;
        nextPathTime = Time.time + repathInterval;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, filter) ||
            !agent.CalculatePath(hit.position, navigationPath) || navigationPath.status != NavMeshPathStatus.PathComplete)
        {
            StopMoving();
            return false;
        }
        agent.isStopped = false;
        hasDestination = agent.SetPath(navigationPath);
        destinationDeadline = Time.time + Vector3.Distance(transform.position, hit.position) / Mathf.Max(speed, 0.1f) + 6f;
        return hasDestination;
    }

    private bool ReachedDestination() => !hasDestination || (!agent.pathPending &&
        (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f));

    private bool HasVisualContact()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        Vector3 toTarget = target.position - transform.position;
        float range = HasTargetMemory ? Mathf.Max(detectionRange, loseTargetRange) : detectionRange;
        if (toTarget.sqrMagnitude > range * range) return false;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, toTarget) > fieldOfView * 0.5f)
            return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        var controller = target.GetComponent<CharacterController>();
        Vector3 chest = controller != null && controller.enabled ? controller.bounds.center : target.position + Vector3.up;
        Vector3 ray = chest - eye;
        int count = Physics.RaycastNonAlloc(eye, ray.normalized, sightHits, ray.magnitude, sightBlockers, QueryTriggerInteraction.Ignore);
        // In dense scenes a full buffer must not silently omit a blocker.
        RaycastHit[] hits = count == sightHits.Length
            ? Physics.RaycastAll(eye, ray.normalized, ray.magnitude, sightBlockers, QueryTriggerInteraction.Ignore) : sightHits;
        if (hits != sightHits) count = hits.Length;
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].collider.transform;
            if (hit == target || hit.IsChildOf(target) || hit == transform || hit.IsChildOf(transform)) continue;
            // Enemies do not blind each other; solid world geometry does.
            if (hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
            return false;
        }
        return true;
    }

    private bool TargetWithinReach()
    {
        if (target == null) return false;
        Vector3 offset = target.position - transform.position;
        if (Mathf.Abs(offset.y) > agent.height) return false;
        offset.y = 0f;
        return offset.sqrMagnitude <= attackRange * attackRange;
    }

    // Recheck at the delayed damage frame: a wall/turn/dash may have intervened.
    public bool CanAttackTarget() => isActiveAndEnabled && AgentReady && State == EnemyAwarenessState.Chase &&
        TargetWithinReach() && HasVisualContact();

    private void FindPlayer()
    {
        nextPlayerLookup = Time.time + 0.5f;
        if (target != null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    private void StopMoving()
    {
        hasDestination = false;
        if (!AgentReady) return;
        agent.isStopped = true;
        if (agent.hasPath) agent.ResetPath();
    }

    private void FaceMovement()
    {
        Vector3 direction = agent.desiredVelocity;
        if (direction.sqrMagnitude > 0.01f) FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        IsInAttackRange = false;
        CanSeeTarget = false;
        StopMoving();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        Vector3 previous = eye + Quaternion.Euler(0, -fieldOfView * 0.5f, 0) * transform.forward * detectionRange;
        Gizmos.DrawLine(eye, previous);
        for (int i = 1; i <= 24; i++)
        {
            Vector3 next = eye + Quaternion.Euler(0, Mathf.Lerp(-fieldOfView * 0.5f, fieldOfView * 0.5f, i / 24f), 0) * transform.forward * detectionRange;
            Gizmos.DrawLine(previous, next); previous = next;
        }
        Gizmos.DrawLine(eye, previous);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(Application.isPlaying ? patrolOrigin : transform.position, patrolRadius);
        if (!Application.isPlaying || !HasTargetMemory) return;
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(lastSeenPosition, 0.4f);
        Gizmos.DrawLine(eye, lastSeenPosition);
    }
}
