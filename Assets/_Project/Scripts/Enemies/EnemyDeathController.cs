using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyDeathController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject targetHighlight;
    [SerializeField] private float destroyDelay = 3f;

    private EnemyHealth health;
    private EnemyAI enemyAI;
    private EnemyAttack enemyAttack;
    private EnemyAnimationController animationController;
    private NavMeshAgent agent;
    private Targetable targetable;
    private Collider[] colliders;

    private bool hasDied;
    private int dieHash;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        enemyAI = GetComponent<EnemyAI>();
        enemyAttack = GetComponent<EnemyAttack>();

        animationController =
            GetComponent<EnemyAnimationController>();

        agent = GetComponent<NavMeshAgent>();
        targetable = GetComponent<Targetable>();
        colliders = GetComponentsInChildren<Collider>();

        dieHash = Animator.StringToHash("Die");

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (hasDied)
        {
            return;
        }

        hasDied = true;

        // หยุดโจมตีและหยุด AI
        if (enemyAttack != null)
        {
            enemyAttack.enabled = false;
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        if (animationController != null)
        {
            animationController.enabled = false;
        }

        // หยุด NavMeshAgent
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        // เลิกเป็นเป้าหมายของ Player
        if (targetable != null)
        {
            targetable.enabled = false;
        }

        if (targetHighlight != null)
        {
            targetHighlight.SetActive(false);
        }

        // ปิด Collider เพื่อไม่ให้ยิงศพซ้ำ
        foreach (Collider enemyCollider in colliders)
        {
            enemyCollider.enabled = false;
        }

        // เล่นท่าตาย
        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger(dieHash);
        }

        Destroy(gameObject, destroyDelay);
    }
}