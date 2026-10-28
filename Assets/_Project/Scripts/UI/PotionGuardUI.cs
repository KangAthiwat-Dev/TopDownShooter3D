using TMPro;
using UnityEngine;

public sealed class PotionGuardUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text label;
    private void Awake() => transform.SetAsFirstSibling(); // Modal inventory and shop panels render above this ambient readout.
    private void Update()
    {
        float remaining = health != null && health.IsAlive ? health.PotionGuardRemaining : 0;
        panel.SetActive(remaining > 0);
        if (remaining > 0) label.text = $"เกราะเวท  {Mathf.CeilToInt(remaining)} วินาที";
    }
}
