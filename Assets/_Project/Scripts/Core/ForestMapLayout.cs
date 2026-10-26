using System;
using UnityEngine;

/// <summary>Editable map landmarks only. No gameplay, spawning or movement logic.</summary>
public sealed class ForestMapLayout : MonoBehaviour
{
    [Serializable]
    public sealed class Route
    {
        public string name;
        public float width = 6;
        public Transform[] points;
    }

    public Terrain terrain;
    public Transform sanctuary, village, city, castle, camp;
    public Transform[] arrivals;
    public Route[] routes;
    public Vector2 minimum, maximum;

    private void OnDrawGizmosSelected()
    {
        if (routes == null) return;
        foreach (Route route in routes)
        {
            Gizmos.color = route.width < 5 ? new Color(0.9f, 0.75f, 0.2f) : new Color(0.2f, 0.8f, 1f);
            if (route.points == null) continue;
            for (int i = 0; i < route.points.Length; i++)
            {
                if (route.points[i] == null) continue;
                Gizmos.DrawWireSphere(route.points[i].position + Vector3.up * 0.1f, route.width * 0.5f);
                if (i > 0 && route.points[i - 1] != null) Gizmos.DrawLine(route.points[i - 1].position, route.points[i].position);
            }
        }
    }
}
