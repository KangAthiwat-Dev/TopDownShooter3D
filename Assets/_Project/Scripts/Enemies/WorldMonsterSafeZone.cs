using UnityEngine;

/// <summary>Spawn exclusion that follows a settlement or encounter when it is moved.</summary>
public sealed class WorldMonsterSafeZone : MonoBehaviour
{
    public Vector3 center;
    public Vector2 size = new Vector2(40, 40);

    public bool Contains(Vector3 position, float padding = 0)
    {
        Vector3 local = transform.InverseTransformPoint(position) - center;
        Vector3 scale = transform.lossyScale;
        return Mathf.Abs(local.x) <= size.x * .5f + padding / Mathf.Abs(scale.x) &&
               Mathf.Abs(local.z) <= size.y * .5f + padding / Mathf.Abs(scale.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(.2f, .9f, .7f, .6f);
        Gizmos.DrawWireCube(center, new Vector3(size.x, 4, size.y));
    }
}
