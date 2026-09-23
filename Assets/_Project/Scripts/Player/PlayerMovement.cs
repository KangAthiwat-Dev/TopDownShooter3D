using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 15f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 4f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.65f;

    private CharacterController characterController;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction dashAction;

    private Vector3 dashDirection;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;

    public bool IsDashing => dashTimeRemaining > 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions.FindAction(
            "Move",
            throwIfNotFound: true
        );

        dashAction = playerInput.actions.FindAction(
            "Dash",
            throwIfNotFound: true
        );
    }

    private void Update()
    {
        dashCooldownRemaining = Mathf.Max(
            0f,
            dashCooldownRemaining - Time.deltaTime
        );

        Vector3 moveDirection = ReadMoveDirection();

        if (dashAction.WasPressedThisFrame() && dashCooldownRemaining <= 0f)
        {
            StartDash(moveDirection);
        }

        Vector3 currentVelocity;

        if (IsDashing)
        {
            currentVelocity = UpdateDash();
        }
        else
        {
            currentVelocity = UpdateNormalMovement(moveDirection);
        }

        characterController.SimpleMove(currentVelocity);
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
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        return moveDirection * moveSpeed;
    }

    private void StartDash(Vector3 requestedDirection)
    {
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

    private Vector3 UpdateDash()
    {
        dashTimeRemaining = Mathf.Max(
            0f,
            dashTimeRemaining - Time.deltaTime
        );

        float dashSpeed = dashDistance / dashDuration;

        return dashDirection * dashSpeed;
    }
}