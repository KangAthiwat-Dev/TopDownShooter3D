using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float loseTargetRange = 16f;

    [Header("Movement")]
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float rotationSpeed = 720f;

    private NavMeshAgent agent;
    private bool isAlerted;

    public bool IsMoving =>
        agent != null &&
        agent.isOnNavMesh &&
        !agent.isStopped &&
        agent.velocity.sqrMagnitude > 0.01f;

    public bool IsInAttackRange { get; private set; }
    public Transform Target => target;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // เราจะควบคุมการหันเอง
        agent.updateRotation = false;
        agent.stoppingDistance = attackRange;
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (target == null)
        {
            FindPlayer();
            StopMoving();
            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;

        float distance = directionToTarget.magnitude;

        UpdateDetection(distance);

        if (!isAlerted)
        {
            IsInAttackRange = false;
            StopMoving();
            return;
        }

        if (distance <= attackRange)
        {
            IsInAttackRange = true;
            StopMoving();
            FaceDirection(directionToTarget);
            return;
        }

        IsInAttackRange = false;

        agent.isStopped = false;
        agent.SetDestination(target.position);

        Vector3 moveDirection = agent.desiredVelocity;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            FaceDirection(moveDirection);
        }
    }

    private void UpdateDetection(float distance)
    {
        if (!isAlerted && distance <= detectionRange)
        {
            isAlerted = true;
        }
        else if (isAlerted && distance > loseTargetRange)
        {
            isAlerted = false;
        }
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            target = player.transform;
        }
    }

    private void StopMoving()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;

        if (agent.hasPath)
        {
            agent.ResetPath();
        }
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}