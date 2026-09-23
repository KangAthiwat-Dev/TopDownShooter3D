using UnityEngine;

[DisallowMultipleComponent]
public class CameraDashEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("FOV Punch")]
    [SerializeField, Min(0f)] private float dashFovIncrease = 7f;
    [SerializeField, Min(0.01f)] private float expandSpeed = 18f;
    [SerializeField, Min(0.01f)] private float recoverSpeed = 8f;

    private float normalFov;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>();
        }

        if (targetCamera != null)
        {
            normalFov = targetCamera.fieldOfView;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null || playerMovement == null)
        {
            return;
        }

        bool isDashing = playerMovement.IsDashing;

        float targetFov = isDashing
            ? normalFov + dashFovIncrease
            : normalFov;

        float transitionSpeed = isDashing
            ? expandSpeed
            : recoverSpeed;

        float transitionAmount =
            1f - Mathf.Exp(
                -transitionSpeed * Time.deltaTime
            );

        targetCamera.fieldOfView = Mathf.Lerp(
            targetCamera.fieldOfView,
            targetFov,
            transitionAmount
        );
    }

    private void OnDisable()
    {
        if (targetCamera != null)
        {
            targetCamera.fieldOfView = normalFov;
        }
    }
}