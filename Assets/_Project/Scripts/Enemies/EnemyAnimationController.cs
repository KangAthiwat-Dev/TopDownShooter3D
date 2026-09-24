using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float damping = 0.1f;

    private NavMeshAgent agent;
    private int speedHash;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
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
            !agent.isOnNavMesh)
        {
            return;
        }

        float normalizedSpeed = 0f;

        if (!agent.isStopped && agent.speed > 0.01f)
        {
            normalizedSpeed = Mathf.Clamp01(
                agent.velocity.magnitude / agent.speed
            );
        }

        animator.SetFloat(
            speedHash,
            normalizedSpeed,
            damping,
            Time.deltaTime
        );
    }
}