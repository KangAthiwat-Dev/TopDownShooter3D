using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float damping = 0.1f;

    private NavMeshAgent agent;
    private EnemyAI enemyAI;
    private int speedHash;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAI = GetComponent<EnemyAI>();
        speedHash = Animator.StringToHash("Speed");

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            agent == null ||
            !agent.isActiveAndEnabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        float normalizedSpeed = 0f;

        if (!agent.isStopped && enemyAI != null)
        {
            // Fixed physical references; agent.speed changes with the AI state.
            // Blend tree: Idle=0, Walk=0.5, Run=1.
            float speed = agent.velocity.magnitude;
            float walk = Mathf.Max(0.1f, enemyAI.PatrolSpeed);
            float run = Mathf.Max(walk + 0.1f, enemyAI.ChaseSpeed);
            normalizedSpeed = speed <= walk ? 0.5f * speed / walk :
                Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(walk, run, speed));
        }

        animator.SetFloat(
            speedHash,
            normalizedSpeed,
            damping,
            Time.deltaTime
        );
    }
}
