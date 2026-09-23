using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerAttackController))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAttackController attackController;

    [Header("Unarmed Attack")]
    [SerializeField, Min(0f)] private float unarmedDamage = 15f;
    [SerializeField, Min(0.1f)] private float hitRange = 2.2f;
    [SerializeField, Min(0f)] private float windUpTime = 0.1f;
    [SerializeField, Min(0f)] private float recoveryTime = 0.12f;

    [Header("Runtime")]
    [SerializeField] private bool attackInProgress;

    private Targetable pendingTarget;
    private Coroutine attackRoutine;

    public bool IsAttacking => attackInProgress;

    // เก็บไว้ใช้ต่อกับเสียง, VFX และ Animation ในอนาคต
    public event Action<Targetable> MeleeHitConfirmed;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController = GetComponent<PlayerAttackController>();
        }
    }

    private void OnEnable()
    {
        attackController.MeleeAttackRequested += HandleMeleeAttack;
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.MeleeAttackRequested -= HandleMeleeAttack;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        attackRoutine = null;
        pendingTarget = null;
        attackInProgress = false;
    }

    private void LateUpdate()
    {
        // ระหว่างออกหมัด ให้ตัวละครหันหาเป้าหมาย
        if (attackInProgress && IsUsableTarget(pendingTarget))
        {
            FaceTarget(pendingTarget);
        }
    }

    private void HandleMeleeAttack(Targetable target)
    {
        if (attackInProgress || !IsUsableTarget(target))
        {
            return;
        }

        attackRoutine = StartCoroutine(PerformAttack(target));
    }

    private IEnumerator PerformAttack(Targetable target)
    {
        attackInProgress = true;
        pendingTarget = target;

        FaceTarget(target);

        // ช่วงง้างหมัด
        if (windUpTime > 0f)
        {
            yield return new WaitForSeconds(windUpTime);
        }

        // ตรวจอีกครั้งตอนหมัดโดนจริง
        if (IsTargetInHitRange(target))
        {
            target.Health.TakeDamage(unarmedDamage);
            MeleeHitConfirmed?.Invoke(target);

            Debug.Log($"Melee hit: {target.name}");
        }
        else
        {
            Debug.Log("Melee missed: target left the hit range");
        }

        // ช่วงพักหลังโจมตี
        if (recoveryTime > 0f)
        {
            yield return new WaitForSeconds(recoveryTime);
        }

        pendingTarget = null;
        attackInProgress = false;
        attackRoutine = null;
    }

    private bool IsUsableTarget(Targetable target)
    {
        return target != null && target.IsTargetable;
    }

    private bool IsTargetInHitRange(Targetable target)
    {
        if (!IsUsableTarget(target))
        {
            return false;
        }

        Vector3 offset = target.AimPoint.position - transform.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= hitRange * hitRange;
    }

    private void FaceTarget(Targetable target)
    {
        if (!IsUsableTarget(target))
        {
            return;
        }

        Vector3 direction = target.AimPoint.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}