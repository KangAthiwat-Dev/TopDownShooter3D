using UnityEngine;

[RequireComponent(typeof(PlayerAttackController))]
public class PlayerRangedWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerAttackController attackController;

    [SerializeField]
    private Projectile projectilePrefab;

    [SerializeField]
    private Transform muzzlePoint;

    [Header("Weapon Stats")]
    [SerializeField, Min(0f)]
    private float damage = 20f;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 18f;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController =
                GetComponent<PlayerAttackController>();
        }
    }

    private void OnEnable()
    {
        attackController.RangedAttackRequested +=
            HandleRangedAttack;
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.RangedAttackRequested -=
                HandleRangedAttack;
        }
    }

    private void HandleRangedAttack(
        Targetable target
    )
    {
        if (target == null ||
            !target.IsTargetable ||
            projectilePrefab == null ||
            muzzlePoint == null)
        {
            return;
        }

        Vector3 shotDirection =
            target.AimPoint.position -
            muzzlePoint.position;

        if (shotDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion projectileRotation =
            Quaternion.LookRotation(
                shotDirection.normalized
            );

        Projectile projectile = Instantiate(
            projectilePrefab,
            muzzlePoint.position,
            projectileRotation
        );

        projectile.Initialize(
            shotDirection,
            projectileSpeed,
            damage,
            gameObject
        );
    }
}