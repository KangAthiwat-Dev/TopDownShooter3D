using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController characterController;
    private PlayerInput playerInput;
    private InputAction moveAction;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions.FindAction(
            "Move",
            throwIfNotFound: true
        );
    }

    private void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 moveDirection = new Vector3(
            input.x,
            0f,
            input.y
        );

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.forward = moveDirection;
        }

        characterController.SimpleMove(
            moveDirection * moveSpeed
        );
    }
}