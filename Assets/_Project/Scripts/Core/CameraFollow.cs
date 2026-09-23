using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Follow Settings")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    private Vector3 followVelocity;

    private void Start()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + targetOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime
        );
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
