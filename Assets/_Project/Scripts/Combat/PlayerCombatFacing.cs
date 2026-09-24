using UnityEngine;

[RequireComponent(typeof(PlayerAttackController))]
public class PlayerCombatFacing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAttackController attackController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Facing")]
    [SerializeField, Min(0f)] private float aimHoldDuration = 0.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;
    [SerializeField] private bool snapOnShot = true;

    private Targetable currentTarget;
    private float aimTimer;

    public bool IsAiming =>
        aimTimer > 0f && IsUsableTarget(currentTarget);

    private void Awake()
    {
        if (attackController == null)
        {
            attackController = GetComponent<PlayerAttackController>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }
    }

    private void OnEnable()
    {
        attackController.RangedAttackRequested += HandleRangedAttack;
        attackController.MeleeAttackRequested += HandleMeleeAttack;
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.RangedAttackRequested -= HandleRangedAttack;
            attackController.MeleeAttackRequested -= HandleMeleeAttack;
        }

        ClearAim();
    }

    private void LateUpdate()
    {
        // ระหว่าง Dash ให้ระบบ Dash ควบคุมทิศทาง
        if (playerMovement != null && playerMovement.IsDashing)
        {
            ClearAim();
            return;
        }

        if (aimTimer <= 0f)
        {
            ClearAim();
            return;
        }

        aimTimer -= Time.deltaTime;

        if (!IsUsableTarget(currentTarget))
        {
            ClearAim();
            return;
        }

        FaceTarget(false);
    }

    private void HandleRangedAttack(Targetable target)
    {
        if (!IsUsableTarget(target))
        {
            return;
        }

        currentTarget = target;
        aimTimer = aimHoldDuration;

        // หันทันทีในเฟรมที่ยิง ป้องกันการยิงออกด้านหลัง
        FaceTarget(snapOnShot);
    }

    private void HandleMeleeAttack(Targetable target)
    {
        // PlayerMeleeAttack จะเป็นผู้ควบคุมการหันตอนต่อย
        ClearAim();
    }

    private void FaceTarget(bool instant)
    {
        Vector3 direction =
            currentTarget.AimPoint.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (instant)
        {
            transform.rotation = targetRotation;
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private bool IsUsableTarget(Targetable target)
    {
        return target != null && target.IsTargetable;
    }

    private void ClearAim()
    {
        currentTarget = null;
        aimTimer = 0f;
    }
}