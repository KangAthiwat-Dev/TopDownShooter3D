using UnityEngine;

/// <summary>Shortens the third-person camera boom at solid scenery without altering materials.</summary>
[DefaultExecutionOrder(1000), DisallowMultipleComponent]
[RequireComponent(typeof(CameraFollow))]
public sealed class CameraBoomCollision : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private LayerMask obstacleLayers = 1 << 7;
    [SerializeField, Min(.01f)] private float probeRadius = .22f;
    [SerializeField, Min(0f)] private float wallPadding = .1f;
    [SerializeField, Min(.01f)] private float returnSmoothTime = .25f;

    private Vector3 restLocalPosition;
    private float currentDistance, distanceVelocity;
    private bool hasRestPose;
    public bool IsObstructed { get; private set; }
    public float CurrentDistance => currentDistance;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        ResetForView();
    }

    public void ResetForView()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null) return;
        restLocalPosition = viewCamera.transform.localPosition;
        hasRestPose = true;
        currentDistance = restLocalPosition.magnitude;
        distanceVelocity = 0f;
        IsObstructed = false;
    }

    private void LateUpdate()
    {
        if (viewCamera == null || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        // CameraFollow has already written the authored boom + this frame's subtle bob.
        Vector3 desired = viewCamera.transform.position - transform.position;
        float fullDistance = desired.magnitude;
        if (fullDistance < .001f) return;
        Vector3 direction = desired / fullDistance;
        float allowedDistance = fullDistance;
        IsObstructed = Physics.SphereCast(transform.position, probeRadius, direction,
            out RaycastHit hit, fullDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
        if (IsObstructed) allowedDistance = Mathf.Max(.02f, hit.distance - wallPadding);

        if (allowedDistance < currentDistance)
        {
            // Collision safety is immediate; only the outward recovery is damped.
            currentDistance = allowedDistance;
            distanceVelocity = 0f;
        }
        else currentDistance = Mathf.SmoothDamp(currentDistance, allowedDistance, ref distanceVelocity,
            returnSmoothTime, Mathf.Infinity, Time.deltaTime);
        viewCamera.transform.position = transform.position + direction * currentDistance;
    }

    private void OnDisable()
    {
        if (!hasRestPose) return;
        if (viewCamera != null) viewCamera.transform.localPosition = restLocalPosition;
        currentDistance = restLocalPosition.magnitude; distanceVelocity = 0f; IsObstructed = false;
    }
}
