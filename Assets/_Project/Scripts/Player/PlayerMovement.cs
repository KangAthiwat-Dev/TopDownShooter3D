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

    [Header("Grounding")]
    [SerializeField] private LayerMask groundLayers = 1 << 7;
    [SerializeField, Min(.1f)] private float groundSnapDistance = .6f;
    [SerializeField, Min(1)] private float gravity = 30f;
    [SerializeField, Min(1)] private float groundStickSpeed = 4f;

    [Header("Camera-Relative Controls")]
    [SerializeField] private bool cameraRelativeMovement;
    [SerializeField] private Transform movementReference;
    [Tooltip("Capture the view heading on a new input direction. Holding a key keeps a straight world path while the camera catches up.")]
    [SerializeField, Range(1f, 30f)] private float inputDirectionChangeAngle = 5f;

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
    private bool sprintNeedsRelease;
    private float verticalSpeed, lastGroundedAt = -10;
    private Vector3 locomotionVelocity;
    private bool hasMovementHeading;
    private Vector2 capturedInputDirection;
    private Vector3 capturedWorldDirection;

    public bool IsDashing => dashTimeRemaining > 0f;
    public bool IsShadowDashing { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsSprinting => IsRunning;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public float WalkSpeed => Mathf.Min(walkSpeed, RunSpeed - 0.01f);
    public float RunSpeed => Mathf.Max(moveSpeed, 0.02f);
    public float SprintSpeed => Mathf.Max(runSpeed, RunSpeed + 0.01f);
    public bool IsInteractionLocked { get; private set; }
    public bool UsesCameraRelativeMovement => cameraRelativeMovement;
    public Transform MovementReference => movementReference;
    public float DashCooldownRemaining => dashCooldownRemaining;
    public float NormalizedDashCooldown => dashCooldown > 0f ? Mathf.Clamp01(dashCooldownRemaining / dashCooldown) : 0f;

    public void ConfigureCameraRelativeMovement(bool cameraRelative, Transform reference)
    {
        cameraRelativeMovement = cameraRelative;
        movementReference = reference;
        // A held key must be recaptured in the newly selected view.
        hasMovementHeading = false;
    }

    public void SetInteractionLocked(bool locked)
    {
        IsInteractionLocked = locked;
        if (locked)
        {
            IsRunning = false;
            locomotionVelocity = Vector3.zero;
            hasMovementHeading = false;
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
        if (cameraRelativeMovement && movementReference == null && Camera.main != null)
            movementReference = Camera.main.transform;

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
            MoveWithGrounding(Vector3.zero);
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

        MoveWithGrounding(currentVelocity);
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

        if (cameraRelativeMovement && movementReference != null)
        {
            float magnitude = Mathf.Clamp01(input.magnitude);
            if (magnitude <= .05f)
            {
                hasMovementHeading = false;
                return Vector3.zero;
            }

            Vector2 inputDirection = input.normalized;
            if (!hasMovementHeading || Vector2.Angle(capturedInputDirection, inputDirection) >= inputDirectionChangeAngle)
            {
                Vector3 forward = Vector3.ProjectOnPlane(movementReference.forward, Vector3.up).normalized;
                // Avoid a degenerate reference if someone authors a vertical camera later.
                if (forward.sqrMagnitude < .001f)
                    forward = Vector3.ProjectOnPlane(movementReference.up, Vector3.up).normalized;
                if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                capturedWorldDirection = (right * inputDirection.x + forward * inputDirection.y).normalized;
                capturedInputDirection = inputDirection;
                hasMovementHeading = true;
            }

            // Never re-project a held key through an auto-rotating camera every frame:
            // that feedback loop would continually turn D/A/S into a circular path.
            return capturedWorldDirection * magnitude;
        }

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

        bool held=runAction.IsPressed();
        if(!held)sprintNeedsRelease=false;
        bool wantsSprint=held&&!sprintNeedsRelease&&moveDirection.sqrMagnitude>.01f;
        IsRunning=wantsSprint&&(IsRunning?playerEnergy.CurrentEnergy>0:playerEnergy.CanStartSprint);
        if(IsRunning&&!playerEnergy.ConsumeSprintEnergy(Time.deltaTime))
        {IsRunning=false;sprintNeedsRelease=true;}

        return UpdateLocomotionVelocity(moveDirection, IsRunning, Time.deltaTime);
    }

    private Vector3 UpdateLocomotionVelocity(Vector3 direction, bool sprinting, float deltaTime)
    {
        Vector3 targetVelocity = Vector3.ClampMagnitude(direction, 1f) * (sprinting ? SprintSpeed : RunSpeed);
        float currentSpeed=locomotionVelocity.magnitude,targetSpeed=targetVelocity.magnitude;
        float speed=Mathf.MoveTowards(currentSpeed,targetSpeed,(targetSpeed<currentSpeed?deceleration:acceleration)*Mathf.Max(0,deltaTime));
        // Change travel direction immediately; blend speed and the visual facing separately.
        // Reversing no longer brakes through a long zero-speed slide.
        Vector3 heading=targetSpeed>.001f?targetVelocity/targetSpeed:locomotionVelocity.normalized;
        locomotionVelocity=heading*speed;
        return locomotionVelocity;
    }

    private void MoveWithGrounding(Vector3 velocity)
    {
        if(!characterController.enabled||Time.deltaTime<=0)return;
        float dt=Time.deltaTime;
        bool grounded=characterController.isGrounded;
        if(grounded){lastGroundedAt=Time.time;verticalSpeed=-groundStickSpeed;}
        else verticalSpeed=Mathf.Max(verticalSpeed-gravity*dt,-45f);
        Vector3 step=new Vector3(velocity.x,0,velocity.z)*dt;
        Vector3 feet=transform.TransformPoint(characterController.center)-Vector3.up*(characterController.height*transform.lossyScale.y*.5f);
        float probeUp=characterController.stepOffset+.15f;
        Vector3 probe=feet+step+Vector3.up*probeUp;
        float vertical=verticalSpeed*dt;
        if(Physics.Raycast(probe,Vector3.down,out var ground,probeUp+groundSnapDistance,groundLayers,QueryTriggerInteraction.Ignore)&&
            ground.normal.y>=Mathf.Cos(characterController.slopeLimit*Mathf.Deg2Rad))
        {
            float gap=feet.y-ground.point.y;
            // Follow nearby stairs/slopes only. Larger drops retain real gravity.
            if((grounded||Time.time-lastGroundedAt<.1f||Mathf.Abs(gap)<.12f)&&
               gap<=groundSnapDistance&&gap>=-characterController.stepOffset-.05f)
            {vertical=-gap-.02f;verticalSpeed=-groundStickSpeed;}
        }
        // One Move per frame keeps CharacterController.velocity useful to animation and camera.
        CollisionFlags flags=characterController.Move(step+Vector3.up*vertical);
        if((flags&CollisionFlags.Below)!=0){lastGroundedAt=Time.time;verticalSpeed=-groundStickSpeed;}
        if((flags&CollisionFlags.Above)!=0)verticalSpeed=Mathf.Min(0,verticalSpeed);
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
        hasMovementHeading = false;
        verticalSpeed=0;
    }
}
