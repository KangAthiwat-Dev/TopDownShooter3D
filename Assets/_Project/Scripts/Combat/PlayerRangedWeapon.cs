using System;
using UnityEngine;

// Fire after Animator, spine correction (150) and rifle hand/aim IK (175).
[RequireComponent(typeof(PlayerAttackController))]
[DefaultExecutionOrder(200)]
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

    [Header("Aim Readiness")]
    [SerializeField, Range(0.1f, 15f)] private float maxBodyAimError = 5f;
    [SerializeField, Range(0.1f, 10f)] private float maxMuzzleAimError = 2f;

    private PlayerCombatFacing combatFacing;
    private PlayerAnimationController animationController;
    private Targetable pendingTarget;

    public Transform MuzzlePoint => muzzlePoint;
    public Targetable PendingTarget => pendingTarget;
    public event Action<Projectile> ProjectileFired;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController =
                GetComponent<PlayerAttackController>();
        }
        combatFacing = GetComponent<PlayerCombatFacing>();
        animationController = GetComponent<PlayerAnimationController>();
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
        CancelPendingShot();
    }

    private void HandleRangedAttack(
        Targetable target
    )
    {
        pendingTarget = target;
    }

    public void CancelPendingShot() => pendingTarget = null;

    private void LateUpdate()
    {
        if (pendingTarget == null) return;
        if (attackController == null || !attackController.IsAttackHeld ||
            attackController.CurrentMode != AttackMode.Ranged ||
            attackController.CurrentAttackTarget != pendingTarget || !pendingTarget.IsTargetable ||
            combatFacing == null || combatFacing.AimTarget != pendingTarget)
        {
            CancelPendingShot();
            return;
        }

        if (projectilePrefab == null || muzzlePoint == null || !muzzlePoint.gameObject.activeInHierarchy ||
            animationController == null || !animationController.IsRangedPoseReady) return;

        Vector3 bodyDirection = pendingTarget.AimPoint.position - transform.position;
        bodyDirection.y = 0f;
        Vector3 muzzleDirection = pendingTarget.AimPoint.position - muzzlePoint.position;
        if (bodyDirection.sqrMagnitude < 0.0001f || muzzleDirection.sqrMagnitude < 0.0001f ||
            Vector3.Angle(transform.forward, bodyDirection) > maxBodyAimError ||
            Vector3.Angle(muzzlePoint.forward, muzzleDirection) > maxMuzzleAimError) return;

        Targetable shotTarget = pendingTarget;
        pendingTarget = null;
        // Capture the FINAL barrel pose. Never steer this projectile towards a
        // target or overwrite its direction with a separate auto-aim vector.
        Vector3 direction = muzzlePoint.forward;
        Projectile projectile = Instantiate(projectilePrefab, muzzlePoint.position,
            Quaternion.LookRotation(direction, muzzlePoint.up));
        projectile.Initialize(direction, projectileSpeed, damage, gameObject);
        attackController.ConfirmRangedShot(shotTarget);
        ProjectileFired?.Invoke(projectile);
    }
}
