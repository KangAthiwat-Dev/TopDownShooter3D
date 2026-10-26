using UnityEngine;

[RequireComponent(typeof(PlayerAttackController))]
public class PlayerCombatFacing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAttackController attackController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Facing")]
    [SerializeField, Min(0f)] private float aimHoldDuration = 0.5f;
    [SerializeField, Min(1f)] private float rotationSpeed = 1080f;

    private PlayerTargeting targeting;
    private PlayerInteractor interactor;
    private PlayerMeleeAttack meleeAttack;
    private PlayerHealth health;
    private Targetable currentTarget;
    private float aimTimer;

    public Targetable AimTarget => IsAiming ? currentTarget : null;
    public bool IsAiming => CanAim() && IsCurrentRangedTarget(currentTarget) &&
                            (attackController.IsAttackHeld || aimTimer > 0f);

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
        targeting = GetComponent<PlayerTargeting>();
        interactor = GetComponent<PlayerInteractor>();
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        health = GetComponent<PlayerHealth>();
    }

    // Called only by PlayerMovement, before Animator evaluation. There is no
    // second Update/LateUpdate writer pulling the root back towards movement.
    public bool TryGetFacingRotation(float deltaTime, out Quaternion rotation)
    {
        rotation = transform.rotation;
        if (!CanAim())
        {
            ClearAim();
            return false;
        }

        Targetable selected = targeting != null ? targeting.CurrentTarget : null;
        if (attackController.IsAttackHeld && IsCurrentRangedTarget(selected))
        {
            currentTarget = selected;
            aimTimer = aimHoldDuration;
        }
        else
        {
            aimTimer = Mathf.Max(0f, aimTimer - Mathf.Max(0f, deltaTime));
        }

        if (!IsAiming)
        {
            ClearAim();
            return false;
        }

        Vector3 direction = currentTarget.AimPoint.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return false;

        rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up), rotationSpeed * Mathf.Max(0f, deltaTime));
        return true;
    }

    private bool CanAim()
    {
        return isActiveAndEnabled && attackController != null && attackController.isActiveAndEnabled &&
               playerMovement != null && playerMovement.isActiveAndEnabled &&
               !playerMovement.IsDashing && !playerMovement.IsShadowDashing &&
               !playerMovement.IsInteractionLocked &&
               (interactor == null || !interactor.IsInteracting) &&
               (meleeAttack == null || !meleeAttack.IsAttacking) &&
               (health == null || health.IsAlive);
    }

    private bool IsCurrentRangedTarget(Targetable target)
    {
        return target != null && target.IsTargetable && targeting != null &&
               targeting.isActiveAndEnabled && !targeting.IsCloseTarget && targeting.CurrentTarget == target;
    }

    public void ClearAim()
    {
        currentTarget = null;
        aimTimer = 0f;
    }

    private void OnDisable() => ClearAim();
}
