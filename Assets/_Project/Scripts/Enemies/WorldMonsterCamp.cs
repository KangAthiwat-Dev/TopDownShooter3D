using UnityEngine;

/// <summary>A whole authored camp is selected, rotated and grounded as one encounter.</summary>
public sealed class WorldMonsterCamp : MonoBehaviour
{
    public Transform[] groundedProps;
    public Transform[] memberPoints;
    [Min(1)] public float radius = 10;
    public int CampId { get; internal set; }

    public void GroundProps(Terrain terrain)
    {
        foreach (Transform prop in groundedProps)
        {
            if (prop == null) continue;
            Vector3 point = prop.position;
            // Authored local Y already compensates for the model pivot below its feet.
            point.y += terrain.SampleHeight(point) + terrain.transform.position.y - transform.position.y;
            prop.position = point;
        }
    }
}
