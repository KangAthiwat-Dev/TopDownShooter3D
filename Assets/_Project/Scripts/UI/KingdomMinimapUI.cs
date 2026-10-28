using System;
using TMPro;
using UnityEngine;

/// <summary>North-up overview baked in the editor; only markers move at runtime, with no extra world camera.</summary>
public sealed class KingdomMinimapUI : MonoBehaviour
{
    [Serializable] public sealed class LandmarkView { public WorldMapLandmark landmark; public RectTransform marker; public TMP_Text label; }
    [SerializeField] private Transform player;
    [SerializeField] private RectTransform mapArea, playerMarker;
    [SerializeField] private Vector2 worldMinimum, worldMaximum;
    [SerializeField] private LandmarkView[] landmarks;
    public Transform Player => player;
    public Vector2 WorldMinimum => worldMinimum;
    public Vector2 WorldMaximum => worldMaximum;
    public Vector2 DisplayedPlayerPosition => playerMarker.anchoredPosition;
    public int LandmarkCount => landmarks?.Length ?? 0;
    public Vector2 WorldToMap(Vector3 point)
    {
        var size = mapArea.rect.size;
        return new Vector2((Mathf.InverseLerp(worldMinimum.x, worldMaximum.x, point.x) - .5f) * size.x,
            (Mathf.InverseLerp(worldMinimum.y, worldMaximum.y, point.z) - .5f) * size.y);
    }
    private void LateUpdate()
    {
        if (player == null) return;
        var half = mapArea.rect.size * .5f; var point = WorldToMap(player.position);
        playerMarker.anchoredPosition = new Vector2(Mathf.Clamp(point.x, -half.x + 13, half.x - 13), Mathf.Clamp(point.y, -half.y + 13, half.y - 13));
        playerMarker.localRotation = Quaternion.Euler(0, 0, -player.eulerAngles.y);
        foreach (var view in landmarks)
        {
            if (view.landmark == null) { view.marker.gameObject.SetActive(false); continue; }
            view.marker.anchoredPosition = WorldToMap(view.landmark.transform.position);
            bool left = view.marker.anchoredPosition.x > 0;
            view.label.rectTransform.pivot = new Vector2(left ? 1 : 0, .5f);
            view.label.rectTransform.anchoredPosition = new Vector2(left ? -9 : 9, view.marker.anchoredPosition.y > half.y - 42 ? -18 : 10);
            view.label.alignment = left ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            view.label.text = view.landmark.mapLabel;
        }
        playerMarker.SetAsLastSibling();
    }
}
