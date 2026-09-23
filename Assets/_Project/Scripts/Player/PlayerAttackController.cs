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

    [Header("Runtime")]
    [SerializeField]
    private AttackMode currentMode;

    [SerializeField]
    private Targetable currentAttackTarget;

    private PlayerInput playerInput;
    private PlayerTargeting playerTargeting;
    private PlayerMovement playerMovement;

    private InputAction attackAction;
    private float attackCooldownRemaining;

    public AttackMode CurrentMode => currentMode;

    public Targetable CurrentAttackTarget =>
        currentAttackTarget;

    public event Action<Targetable>
        RangedAttackRequested;

    public event Action<Targetable>
        MeleeAttackRequested;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerTargeting =
            GetComponent<PlayerTargeting>();
        playerMovement =
            GetComponent<PlayerMovement>();

        attackAction =
            playerInput.actions.FindAction(
                "Attack",
                throwIfNotFound: true
            );
    }

    private void Update()
    {
        attackCooldownRemaining = Mathf.Max(
            0f,
            attackCooldownRemaining -
            Time.deltaTime
        );

        UpdateAttackMode();

        if (playerMovement.IsDashing)
        {
            return;
        }

        if (!attackAction.IsPressed())
        {
            return;
        }

        if (attackCooldownRemaining > 0f)
        {
            return;
        }

        if (currentAttackTarget == null)
        {
            return;
        }

        RequestAttack();

        attackCooldownRemaining =
            attackInterval;
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
            Debug.Log(
                $"Melee attack requested: " +
                $"{currentAttackTarget.name}"
            );

            MeleeAttackRequested?.Invoke(
                currentAttackTarget
            );

            return;
        }

        Debug.Log(
            $"Ranged attack requested: " +
            $"{currentAttackTarget.name}"
        );

        RangedAttackRequested?.Invoke(
            currentAttackTarget
        );
    }
}