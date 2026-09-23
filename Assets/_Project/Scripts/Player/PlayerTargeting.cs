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

    [Header("Runtime")]
    [SerializeField]
    private Targetable currentTarget;

    private readonly Collider[] overlapResults =
        new Collider[64];

    private float scanTimer;

    public Targetable CurrentTarget => currentTarget;

    public Transform CurrentAimPoint =>
        currentTarget != null
            ? currentTarget.AimPoint
            : null;

    public event Action<Targetable> TargetChanged;

    private void Update()
    {
        scanTimer -= Time.deltaTime;

        if (!IsValidTarget(currentTarget))
        {
            SetTarget(null);
        }

        if (currentTarget == null && scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            FindTarget();
        }

        if (currentTarget != null)
        {
            Debug.DrawLine(
                transform.position + Vector3.up,
                currentTarget.AimPoint.position,
                Color.cyan
            );
        }
    }

    private void FindTarget()
    {
        int targetCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            targetRange,
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

            if (!IsValidTarget(candidate))
            {
                continue;
            }

            Vector3 offset =
                candidate.AimPoint.position -
                transform.position;

            offset.y = 0f;

            float distanceSquared =
                offset.sqrMagnitude;

            bool isBetterTarget =
                IsBetterTarget(
                    candidate,
                    distanceSquared,
                    bestDistanceSquared,
                    lowestHealth
                );

            if (!isBetterTarget)
            {
                continue;
            }

            bestTarget = candidate;
            bestDistanceSquared = distanceSquared;
            lowestHealth =
                candidate.Health.CurrentHealth;
        }

        SetTarget(bestTarget);
    }

    private bool IsBetterTarget(
        Targetable candidate,
        float candidateDistanceSquared,
        float bestDistanceSquared,
        float lowestHealth
    )
    {
        if (targetPriority == TargetPriority.Nearest)
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

        bool sameHealth =
            Mathf.Approximately(
                candidateHealth,
                lowestHealth
            );

        return sameHealth &&
               candidateDistanceSquared <
               bestDistanceSquared;
    }

    private bool IsValidTarget(Targetable target)
    {
        if (target == null || !target.IsTargetable)
        {
            return false;
        }

        Vector3 offset =
            target.AimPoint.position -
            transform.position;

        offset.y = 0f;

        return offset.sqrMagnitude <=
               targetRange * targetRange;
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
        SetTarget(null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            targetRange
        );
    }
}