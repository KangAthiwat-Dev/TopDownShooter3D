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
        if (isAttacking ||
            !enemyAI.IsInAttackRange ||
            enemyAI.Target == null)
        {
            return;
        }

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(attackHash);
        }

        // รอให้ท่าเหวี่ยงอาวุธมาถึงจังหวะโดน
        yield return new WaitForSeconds(hitDelay);

        if (enemyAI.IsInAttackRange &&
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