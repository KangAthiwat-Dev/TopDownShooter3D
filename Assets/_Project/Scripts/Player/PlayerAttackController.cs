using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AttackMode
{
    None,
    Ranged,
    Melee
}

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerTargeting))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAttackController : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField, Min(0.05f)]
    private float attackInterval = 0.35f;
    [SerializeField, Min(0.05f), Tooltip("Rifle cadence only; melee keeps its existing animation timing.")]
    private float rangedAttackInterval = 0.18f;

    [Header("Runtime")]
    [SerializeField]
    private AttackMode currentMode;

    [SerializeField]
    private Targetable currentAttackTarget;

    private PlayerInput playerInput;
    private PlayerTargeting playerTargeting;
    private PlayerMovement playerMovement;
    private PlayerMeleeAttack meleeAttack;
    private PlayerInteractor playerInteractor;
    private PlayerRangedWeapon rangedWeapon;
    private PlayerHealth health;
    private PlayerInputGate inputGate;

    private InputAction attackAction;
    private float attackCooldownRemaining;

    public AttackMode CurrentMode => currentMode;
    public float RangedAttackInterval => Mathf.Max(0.05f, rangedAttackInterval);
    public bool IsAttackHeld => isActiveAndEnabled && attackAction != null &&
        (inputGate != null ? inputGate.IsHeld(attackAction) : attackAction.IsPressed());

    public Targetable CurrentAttackTarget =>
        currentAttackTarget;

    public event Action<Targetable>
        RangedAttackRequested;

    // Animation, audio and VFX react to a committed shot, not an aim request.
    public event Action<Targetable> RangedShotFired;

    public event Action<Targetable>
        MeleeAttackRequested;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerTargeting =
            GetComponent<PlayerTargeting>();
        playerMovement =
            GetComponent<PlayerMovement>();
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        playerInteractor = GetComponent<PlayerInteractor>();
        rangedWeapon = GetComponent<PlayerRangedWeapon>();
        health = GetComponent<PlayerHealth>();
        inputGate = GetComponent<PlayerInputGate>();

        attackAction =
            playerInput.actions.FindAction(
                "Attack",
                throwIfNotFound: true
            );
    }

    private void Update()
    {
        if (inputGate != null && inputGate.BlocksGameplay)
        {
            currentMode = AttackMode.None; currentAttackTarget = null; CancelPendingShot(); return;
        }
        attackCooldownRemaining = Mathf.Max(
            0f,
            attackCooldownRemaining - Time.deltaTime
        );

        bool cannotAttack =
            !playerMovement.isActiveAndEnabled ||
            playerMovement.IsInteractionLocked ||
            (health != null && !health.IsAlive) ||
            playerMovement.IsDashing ||
            playerMovement.IsShadowDashing ||
            (playerInteractor != null && playerInteractor.IsInteracting);

        if (cannotAttack)
        {
            currentMode = AttackMode.None;
            currentAttackTarget = null;
            CancelPendingShot();
            return;
        }

        UpdateAttackMode();

        // ห้ามยิงหรือเริ่มหมัดใหม่แทรก ก่อนท่าต่อยเดิมจบ
        if (meleeAttack != null && meleeAttack.IsAttacking)
        {
            CancelPendingShot();
            return;
        }

        if (!IsAttackHeld)
        {
            CancelPendingShot();
            return;
        }

        if (attackCooldownRemaining > 0f)
        {
            return;
        }

        if (currentAttackTarget == null)
        {
            CancelPendingShot();
            return;
        }

        RequestAttack();

        if (currentMode == AttackMode.Melee) attackCooldownRemaining = attackInterval;
    }

    private void UpdateAttackMode()
    {
        currentAttackTarget =
            playerTargeting.CurrentTarget;

        if (currentAttackTarget == null)
        {
            currentMode = AttackMode.None;
            return;
        }

        currentMode =
            playerTargeting.IsCloseTarget
                ? AttackMode.Melee
                : AttackMode.Ranged;
    }

    private void RequestAttack()
    {
        if (currentMode == AttackMode.Melee)
        {
            CancelPendingShot();
            Debug.Log(
                $"Melee attack requested: " +
                $"{currentAttackTarget.name}"
            );

            MeleeAttackRequested?.Invoke(
                currentAttackTarget
            );

            return;
        }

        if (rangedWeapon == null || !rangedWeapon.isActiveAndEnabled ||
            rangedWeapon.PendingTarget == currentAttackTarget) return;

        RangedAttackRequested?.Invoke(
            currentAttackTarget
        );
    }

    internal void ConfirmRangedShot(Targetable target)
    {
        attackCooldownRemaining = RangedAttackInterval;
        RangedShotFired?.Invoke(target);
    }

    private void CancelPendingShot()
    {
        if (rangedWeapon != null) rangedWeapon.CancelPendingShot();
    }

    private void OnDisable()
    {
        CancelPendingShot();
        currentAttackTarget = null;
        currentMode = AttackMode.None;
    }
}
