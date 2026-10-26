using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerEnergy))]
[DefaultExecutionOrder(-100)]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 2f;
    // Keep serialized names so existing scene speed overrides survive.
    [SerializeField, Min(0.1f), InspectorName("Run Speed")] private float moveSpeed = 5f;
    [SerializeField, Min(0.1f), InspectorName("Sprint Speed")] private float runSpeed = 8f;
    [SerializeField] private float turnSpeed = 15f;
    [SerializeField, Min(0.1f)] private float acceleration = 18f;
    [SerializeField, Min(0.1f)] private float deceleration = 26f;

    [Header("Short Dash")]
    [SerializeField] private float dashDistance = 4f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.65f;

    [Header("Shadow Dash")]
    [SerializeField] private float shadowDashSpeed = 11f;

    private CharacterController characterController;
    private PlayerInput playerInput;
    private PlayerEnergy playerEnergy;
    private PlayerCombatFacing combatFacing;
    private PlayerMeleeAttack meleeAttack;
    private PlayerInputGate inputGate;

    private InputAction moveAction;
    private InputAction runAction;
    private InputAction dashAction;
    private InputAction shadowDashAction;

    private Vector3 dashDirection;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;

    private bool shadowNeedsRelease;
    private Vector3 locomotionVelocity;

    public bool IsDashing => dashTimeRemaining > 0f;
    public bool IsShadowDashing { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsSprinting => IsRunning;
    public float WalkSpeed => Mathf.Min(walkSpeed, RunSpeed - 0.01f);
    public float RunSpeed => Mathf.Max(moveSpeed, 0.02f);
    public float SprintSpeed => Mathf.Max(runSpeed, RunSpeed + 0.01f);
    public bool IsInteractionLocked { get; private set; }

    public void SetInteractionLocked(bool locked)
    {
        IsInteractionLocked = locked;
        if (locked)
        {
            IsRunning = false;
            locomotionVelocity = Vector3.zero;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        // Acceleration starts with sub-millimetre steps at high frame rates.
        // A non-zero minimum can discard every startup step indefinitely.
        characterController.minMoveDistance = 0f;
        playerInput = GetComponent<PlayerInput>();
        playerEnergy = GetComponent<PlayerEnergy>();
        combatFacing = GetComponent<PlayerCombatFacing>();
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        inputGate = GetComponent<PlayerInputGate>();

        moveAction = playerInput.actions.FindAction(
            "Move",
            throwIfNotFound: true
        );

        runAction = playerInput.actions.FindAction(
            "Run",
            throwIfNotFound: true
        );

        dashAction = playerInput.actions.FindAction(
            "Dash",
            throwIfNotFound: true
        );

        shadowDashAction = playerInput.actions.FindAction(
            "ShadowDash",
            throwIfNotFound: true
        );
    }

    private void Update()
    {
        if (inputGate != null && inputGate.BlocksGameplay) return;
        dashCooldownRemaining = Mathf.Max(
            0f,
            dashCooldownRemaining - Time.deltaTime
        );

        if (IsInteractionLocked)
        {
            if (combatFacing != null) combatFacing.ClearAim();
            IsRunning = false;
            locomotionVelocity = Vector3.zero;
            characterController.SimpleMove(Vector3.zero);
            return;
        }

        Vector3 moveDirection = ReadMoveDirection();
        bool shadowButtonHeld = inputGate != null ? inputGate.IsHeld(shadowDashAction) : shadowDashAction.IsPressed();

        if (!shadowButtonHeld)
        {
            StopShadowDash();
            shadowNeedsRelease = false;
        }

        if (
            (inputGate != null ? inputGate.WasPressed(dashAction) : dashAction.WasPressedThisFrame()) &&
            !IsShadowDashing &&
            dashCooldownRemaining <= 0f
        )
        {
            StartDash(moveDirection);
        }

        Vector3 currentVelocity;

        bool normalMovement = false;
        if (IsDashing)
        {
            if (combatFacing != null) combatFacing.ClearAim();
            IsRunning = false;
            currentVelocity = UpdateShortDash();
        }
        else if (shadowButtonHeld && !shadowNeedsRelease)
        {
            currentVelocity = UpdateShadowDash(moveDirection);
        }
        else
        {
            currentVelocity = UpdateNormalMovement(moveDirection);
            normalMovement = true;
        }

        characterController.SimpleMove(currentVelocity);
        if (normalMovement && (characterController.collisionFlags & CollisionFlags.Sides) != 0)
        {
            // Do not accumulate hidden momentum while pressed against a wall.
            locomotionVelocity = characterController.velocity;
            locomotionVelocity.y = 0f;
        }
    }

    private Vector3 ReadMoveDirection()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 direction = new Vector3(
            input.x,
            0f,
            input.y
        );

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    private Vector3 UpdateNormalMovement(Vector3 moveDirection)
    {
        // Melee owns its full-body action; otherwise aiming takes priority over
        // movement. No second rotation writer pulls the root back towards walking.
        if (meleeAttack == null || !meleeAttack.IsAttacking)
        {
            if (combatFacing != null && combatFacing.TryGetFacingRotation(Time.deltaTime, out Quaternion facing))
                transform.rotation = facing;
            else
                RotateTowards(moveDirection);
        }

        IsRunning =
            runAction.IsPressed() &&
            moveDirection.sqrMagnitude > 0.01f;

        return UpdateLocomotionVelocity(moveDirection, IsRunning, Time.deltaTime);
    }

    private Vector3 UpdateLocomotionVelocity(Vector3 direction, bool sprinting, float deltaTime)
    {
        Vector3 targetVelocity = Vector3.ClampMagnitude(direction, 1f) * (sprinting ? SprintSpeed : RunSpeed);
        float rate = targetVelocity.sqrMagnitude < locomotionVelocity.sqrMagnitude ||
                     Vector3.Dot(locomotionVelocity, targetVelocity) < 0f ? deceleration : acceleration;
        locomotionVelocity = Vector3.MoveTowards(locomotionVelocity, targetVelocity, rate * Mathf.Max(0f, deltaTime));
        return locomotionVelocity;
    }

    private void StartDash(Vector3 requestedDirection)
    {
        StopShadowDash();
        IsRunning = false;
        locomotionVelocity = Vector3.zero;

        if (requestedDirection.sqrMagnitude > 0.01f)
        {
            dashDirection = requestedDirection.normalized;
        }
        else
        {
            dashDirection = transform.forward;
        }

        transform.forward = dashDirection;

        dashTimeRemaining = dashDuration;
        dashCooldownRemaining = dashCooldown;
    }

    private Vector3 UpdateShortDash()
    {
        dashTimeRemaining = Mathf.Max(
            0f,
            dashTimeRemaining - Time.deltaTime
        );

        float dashSpeed = dashDistance / dashDuration;

        return dashDirection * dashSpeed;
    }

    private Vector3 UpdateShadowDash(Vector3 moveDirection)
    {
        if (!IsShadowDashing)
        {
            if (!playerEnergy.CanStartShadowDash)
            {
                return UpdateNormalMovement(moveDirection);
            }

            IsShadowDashing = true;
            if (combatFacing != null) combatFacing.ClearAim();
        }

        IsRunning = false;
        locomotionVelocity = Vector3.zero;

        bool hasEnergy =
            playerEnergy.ConsumeShadowDashEnergy(Time.deltaTime);

        if (!hasEnergy)
        {
            StopShadowDash();
            shadowNeedsRelease = true;

            return UpdateNormalMovement(moveDirection);
        }

        Vector3 shadowDirection = moveDirection.sqrMagnitude > 0.01f
            ? moveDirection.normalized
            : transform.forward;

        RotateTowards(shadowDirection);

        return shadowDirection * shadowDashSpeed;
    }

    private void StopShadowDash()
    {
        IsShadowDashing = false;
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-turnSpeed * Time.deltaTime)
        );
    }

    private void OnDisable()
    {
        if (combatFacing != null) combatFacing.ClearAim();
        locomotionVelocity = Vector3.zero;
        IsRunning = false;
    }
}
