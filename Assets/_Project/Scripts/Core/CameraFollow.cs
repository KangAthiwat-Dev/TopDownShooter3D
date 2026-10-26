using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Follow Settings")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    [Header("Running Bob")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private bool enableRunningBob = true;
    [SerializeField, Min(0f)] private float bobFrequency = 2.8f;
    [SerializeField] private Vector2 bobAmplitude = new Vector2(0.035f, 0.05f);
    [SerializeField, Min(0f)] private float bobRollAngle = 0.2f;
    [SerializeField, Min(0.01f)] private float bobBlendSpeed = 8f;

    private Vector3 followVelocity;
    private CharacterController playerController;
    private Vector3 cameraRestPosition;
    private Quaternion cameraRestRotation;
    private float bobPhase;
    private float bobWeight;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>();
        }

        if (playerMovement == null && target != null)
        {
            playerMovement = target.GetComponent<PlayerMovement>();
        }

        if (playerMovement != null)
        {
            playerController = playerMovement.GetComponent<CharacterController>();
        }

        if (targetCamera != null)
        {
            cameraRestPosition = targetCamera.transform.localPosition;
            cameraRestRotation = targetCamera.transform.localRotation;
        }
    }

    private void Start()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            ResetRunningBob();
            return;
        }

        Vector3 targetPosition = target.position + targetOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime
        );

        UpdateRunningBob(Time.deltaTime);
    }

    private void UpdateRunningBob(float deltaTime)
    {
        if (targetCamera == null || deltaTime <= 0f)
        {
            return;
        }

        Vector3 velocity = playerController != null
            ? playerController.velocity
            : Vector3.zero;
        velocity.y = 0f;

        bool shouldBob = enableRunningBob &&
                         playerMovement != null &&
                         playerMovement.isActiveAndEnabled &&
                         playerMovement.IsRunning &&
                         !playerMovement.IsDashing &&
                         !playerMovement.IsShadowDashing &&
                         velocity.sqrMagnitude > 0.01f;

        bobWeight = Mathf.MoveTowards(
            bobWeight,
            shouldBob ? 1f : 0f,
            bobBlendSpeed * deltaTime
        );

        if (bobWeight <= 0f)
        {
            ResetRunningBob();
            return;
        }

        bobPhase = (bobPhase + bobFrequency * Mathf.PI * 2f * deltaTime)
                   % (Mathf.PI * 2f);

        Vector3 offset = new Vector3(
            Mathf.Sin(bobPhase) * bobAmplitude.x,
            Mathf.Sin(bobPhase * 2f) * bobAmplitude.y,
            0f
        ) * bobWeight;

        // โยกเฉพาะกล้องลูก เพื่อไม่ให้ตำแหน่ง SmoothDamp ของ rig สะสม offset
        targetCamera.transform.localPosition = cameraRestPosition + offset;
        targetCamera.transform.localRotation = cameraRestRotation * Quaternion.Euler(
            0f,
            0f,
            -Mathf.Sin(bobPhase) * bobRollAngle * bobWeight
        );
    }

    private void ResetRunningBob()
    {
        bobPhase = 0f;
        bobWeight = 0f;

        if (targetCamera != null)
        {
            targetCamera.transform.localPosition = cameraRestPosition;
            targetCamera.transform.localRotation = cameraRestRotation;
        }
    }

    private void OnDisable()
    {
        ResetRunningBob();
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position + targetOffset;
    }
}
