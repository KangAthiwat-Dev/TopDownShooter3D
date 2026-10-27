using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Follow Settings")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    [Header("Movement-Directed Orbit")]
    [Tooltip("Orbit behind actual walking/running velocity, never the player's aiming rotation. Off preserves older scenes.")]
    [SerializeField] private bool followMovementHeading;
    [SerializeField] private bool alignBehindTargetOnStart;
    [SerializeField, Min(.01f)] private float headingSmoothTime = .4f;
    [SerializeField, Min(1f)] private float maxHeadingSpeed = 110f;
    [SerializeField, Min(.01f)] private float minimumHeadingSpeed = .8f;
    [SerializeField, Min(0f)] private float headingConfirmTime = .12f;
    [SerializeField, Range(0f, 45f)] private float headingDeadZone = 7f;

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
    private bool hasRestPose;
    private Quaternion rigRestRotation;
    private float restViewYaw;
    private float orbitYaw;
    private float desiredOrbitYaw;
    private float headingVelocity;
    private float candidateYaw;
    private float candidateTime;
    private bool hasCandidate;

    public bool FollowsMovementHeading => followMovementHeading;
    public Camera FollowCamera => targetCamera;

    private void Awake()
    {
        ResolveReferences();
        CaptureViewPose();
    }

    private void ResolveReferences()
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

    }

    private void CaptureViewPose()
    {
        if (targetCamera != null)
        {
            cameraRestPosition = targetCamera.transform.localPosition;
            cameraRestRotation = targetCamera.transform.localRotation;
            hasRestPose = true;
            if (followMovementHeading && alignBehindTargetOnStart && target != null)
            {
                Vector3 viewHeading = Vector3.ProjectOnPlane(transform.position - targetCamera.transform.position, Vector3.up);
                Vector3 playerHeading = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                if (viewHeading.sqrMagnitude > .001f && playerHeading.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(viewHeading, playerHeading, Vector3.up), Vector3.up) * transform.rotation;
            }
        }

        rigRestRotation = transform.rotation;
        // Align the camera-to-pivot offset, not a particular assumed child-camera yaw.
        // Rotating the parent about world Y preserves the authored height, pitch and distance.
        Vector3 behind = targetCamera != null
            ? Vector3.ProjectOnPlane(transform.position - targetCamera.transform.position, Vector3.up)
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (behind.sqrMagnitude < .0001f) behind = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        restViewYaw = Mathf.Atan2(behind.x, behind.z) * Mathf.Rad2Deg;
        orbitYaw = desiredOrbitYaw = headingVelocity = candidateYaw = candidateTime = 0f;
        hasCandidate = false;
    }

    public void ApplyViewPose(Vector3 localPosition, Quaternion localRotation, Quaternion rigRotation,
        Vector3 offset, bool orbitWithMovement, bool initiallyBehindPlayer)
    {
        ResolveReferences();
        ResetRunningBob();
        followMovementHeading = orbitWithMovement;
        alignBehindTargetOnStart = initiallyBehindPlayer;
        targetOffset = offset;
        transform.rotation = rigRotation;
        if (targetCamera != null)
        {
            targetCamera.transform.localPosition = localPosition;
            targetCamera.transform.localRotation = localRotation;
        }
        CaptureViewPose();
        SnapToTarget();
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

        if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;

        Vector3 targetPosition = target.position + targetOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime
        );

        UpdateMovementHeading(Time.deltaTime);
        UpdateRunningBob(Time.deltaTime);
    }

    private void UpdateMovementHeading(float deltaTime)
    {
        if (!followMovementHeading) return;
        Vector3 velocity = playerController != null && playerController.enabled
            ? Vector3.ProjectOnPlane(playerController.velocity, Vector3.up) : Vector3.zero;
        bool locomotion = playerMovement != null && playerMovement.isActiveAndEnabled &&
            !playerMovement.IsDashing && !playerMovement.IsShadowDashing && !playerMovement.IsInteractionLocked &&
            velocity.sqrMagnitude >= minimumHeadingSpeed * minimumHeadingSpeed;

        if (locomotion)
        {
            float yaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg - restViewYaw;
            if (!hasCandidate || Mathf.Abs(Mathf.DeltaAngle(candidateYaw, yaw)) > headingDeadZone)
            {
                candidateYaw = yaw;
                candidateTime = 0f;
                hasCandidate = true;
            }
            else candidateTime += deltaTime;

            if (candidateTime >= headingConfirmTime && Mathf.Abs(Mathf.DeltaAngle(desiredOrbitYaw, candidateYaw)) > headingDeadZone)
                desiredOrbitYaw = candidateYaw;
        }
        else
        {
            // Tiny taps, dashes and collisions do not replace the last stable walking heading.
            hasCandidate = false;
            candidateTime = 0f;
        }

        orbitYaw = Mathf.SmoothDampAngle(orbitYaw, desiredOrbitYaw, ref headingVelocity,
            Mathf.Max(.01f, headingSmoothTime), Mathf.Max(1f, maxHeadingSpeed), deltaTime);
        transform.rotation = Quaternion.AngleAxis(orbitYaw, Vector3.up) * rigRestRotation;
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

        if (targetCamera != null && hasRestPose)
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
        followVelocity = Vector3.zero;
    }
}
