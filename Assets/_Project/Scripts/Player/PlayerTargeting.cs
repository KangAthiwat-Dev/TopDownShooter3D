using System;
using UnityEngine;

public enum TargetPriority
{
    Nearest,
    LowestHealth
}

[DisallowMultipleComponent]
public class PlayerTargeting : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField, Min(0.1f)]
    private float targetRange = 12f;

    [SerializeField, Min(0.05f)]
    private float scanInterval = 0.15f;

    [SerializeField]
    private LayerMask enemyLayer;

    [SerializeField]
    private TargetPriority targetPriority =
        TargetPriority.Nearest;

    [Header("Close Threat Override")]
    [SerializeField, Min(0.1f)]
    private float closeEnterRange = 1.8f;

    [SerializeField, Min(0.1f)]
    private float closeExitRange = 2.2f;

    [Header("Runtime")]
    [SerializeField]
    private Targetable currentTarget;

    [SerializeField]
    private bool closeOverrideActive;

    private readonly Collider[] overlapResults =
        new Collider[64];

    private float scanTimer;

    public Targetable CurrentTarget => currentTarget;

    public Transform CurrentAimPoint =>
        currentTarget != null
            ? currentTarget.AimPoint
            : null;

    public bool IsCloseTarget =>
        closeOverrideActive &&
        currentTarget != null;

    public event Action<Targetable> TargetChanged;

    private void Update()
    {
        scanTimer -= Time.deltaTime;

        if (!IsValidTarget(currentTarget, targetRange))
        {
            closeOverrideActive = false;
            SetTarget(null);
        }

        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            RefreshTarget();
        }

        if (currentTarget != null)
        {
            Debug.DrawLine(
                transform.position + Vector3.up,
                currentTarget.AimPoint.position,
                closeOverrideActive
                    ? Color.red
                    : Color.cyan
            );
        }
    }

    private void RefreshTarget()
    {
        // หากล็อกเป้าหมายประชิดอยู่
        // ให้คงไว้จนกว่าจะออกจากระยะ Exit
        if (closeOverrideActive)
        {
            if (IsValidTarget(
                currentTarget,
                closeExitRange
            ))
            {
                return;
            }

            closeOverrideActive = false;
            SetTarget(null);
        }

        // ศัตรูประชิดมีความสำคัญกว่า
        // เป้าหมายระยะไกลเสมอ
        Targetable closeTarget = FindBestTarget(
            closeEnterRange,
            TargetPriority.Nearest
        );

        if (closeTarget != null)
        {
            closeOverrideActive = true;
            SetTarget(closeTarget);
            return;
        }

        // ถ้ายังมีเป้าหมายระยะไกลที่ใช้ได้
        // ให้ล็อกตัวเดิมต่อไป
        if (currentTarget != null)
        {
            return;
        }

        SetTarget(
            FindBestTarget(
                targetRange,
                targetPriority
            )
        );
    }

    private Targetable FindBestTarget(
        float searchRange,
        TargetPriority priority
    )
    {
        int targetCount =
            Physics.OverlapSphereNonAlloc(
                transform.position,
                searchRange,
                overlapResults,
                enemyLayer,
                QueryTriggerInteraction.Ignore
            );

        Targetable bestTarget = null;
        float bestDistanceSquared = float.MaxValue;
        float lowestHealth = float.MaxValue;

        for (int i = 0; i < targetCount; i++)
        {
            Targetable candidate =
                overlapResults[i]
                    .GetComponentInParent<Targetable>();

            if (!IsValidTarget(
                candidate,
                searchRange
            ))
            {
                continue;
            }

            float distanceSquared =
                GetHorizontalDistanceSquared(
                    candidate
                );

            bool isBetter =
                IsBetterTarget(
                    candidate,
                    priority,
                    distanceSquared,
                    bestDistanceSquared,
                    lowestHealth
                );

            if (!isBetter)
            {
                continue;
            }

            bestTarget = candidate;
            bestDistanceSquared = distanceSquared;
            lowestHealth =
                candidate.Health.CurrentHealth;
        }

        return bestTarget;
    }

    private bool IsBetterTarget(
        Targetable candidate,
        TargetPriority priority,
        float candidateDistanceSquared,
        float bestDistanceSquared,
        float lowestHealth
    )
    {
        if (priority == TargetPriority.Nearest)
        {
            return candidateDistanceSquared <
                   bestDistanceSquared;
        }

        float candidateHealth =
            candidate.Health.CurrentHealth;

        if (candidateHealth < lowestHealth)
        {
            return true;
        }

        bool hasSameHealth =
            Mathf.Approximately(
                candidateHealth,
                lowestHealth
            );

        return hasSameHealth &&
               candidateDistanceSquared <
               bestDistanceSquared;
    }

    private bool IsValidTarget(
        Targetable target,
        float allowedRange
    )
    {
        if (target == null || !target.IsTargetable)
        {
            return false;
        }

        return GetHorizontalDistanceSquared(target) <=
               allowedRange * allowedRange;
    }

    private float GetHorizontalDistanceSquared(
        Targetable target
    )
    {
        Vector3 offset =
            target.AimPoint.position -
            transform.position;

        offset.y = 0f;

        return offset.sqrMagnitude;
    }

    private void SetTarget(Targetable newTarget)
    {
        if (currentTarget == newTarget)
        {
            return;
        }

        currentTarget = newTarget;
        TargetChanged?.Invoke(currentTarget);
    }

    private void OnDisable()
    {
        closeOverrideActive = false;
        SetTarget(null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            targetRange
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            closeEnterRange
        );
    }
}