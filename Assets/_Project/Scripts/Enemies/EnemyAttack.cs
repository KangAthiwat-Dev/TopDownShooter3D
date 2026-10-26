using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float hitDelay = 0.35f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private EnemyAI enemyAI;
    private bool isAttacking;
    private int attackHash;

    public event System.Action AttackStarted;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        attackHash = Animator.StringToHash("Attack");

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || isAttacking ||
            !enemyAI.IsInAttackRange || !enemyAI.CanAttackTarget() ||
            enemyAI.Target == null)
        {
            return;
        }

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        AttackStarted?.Invoke();

        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(attackHash);
        }

        // รอให้ท่าเหวี่ยงอาวุธมาถึงจังหวะโดน
        yield return new WaitForSeconds(hitDelay);
        // A menu can open in the same frame that this delay becomes ready.
        while (Time.timeScale <= 0f) yield return null;

        if (enemyAI.CanAttackTarget() &&
            enemyAI.Target != null)
        {
            IDamageable damageable =
                enemyAI.Target.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        float remainingCooldown =
            Mathf.Max(attackCooldown - hitDelay, 0f);

        yield return new WaitForSeconds(remainingCooldown);

        isAttacking = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isAttacking = false;
    }
}
