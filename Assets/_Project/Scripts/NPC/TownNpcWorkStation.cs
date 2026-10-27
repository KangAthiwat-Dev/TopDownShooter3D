using UnityEngine;

/// <summary>Editable construction work/pickup/delivery stops; the root's forward is the work facing.</summary>
public sealed class TownNpcWorkStation : MonoBehaviour
{
    [SerializeField] private Transform pickup;
    [SerializeField] private Transform delivery;
    [SerializeField] private Vector2 workAreaCenter = new Vector2(65.5f, -83.5f);
    [SerializeField] private Vector2 workAreaSize = new Vector2(17f, 19f);
    [SerializeField] private Transform boundsFrame;
    public Transform Pickup => pickup;
    public Transform Delivery => delivery;
    public bool Contains(Vector3 point)
    {
        if (boundsFrame != null) point = boundsFrame.InverseTransformPoint(point);
        return Mathf.Abs(point.x - workAreaCenter.x) <= workAreaSize.x * .5f && Mathf.Abs(point.z - workAreaCenter.y) <= workAreaSize.y * .5f;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, .65f, .1f); Gizmos.DrawWireSphere(transform.position, .4f);
        Gizmos.DrawRay(transform.position, transform.forward);
        if (pickup != null && delivery != null)
        {
            Gizmos.DrawLine(transform.position, pickup.position); Gizmos.DrawLine(pickup.position, delivery.position);
            Gizmos.DrawLine(delivery.position, transform.position);
        }
    }
}
