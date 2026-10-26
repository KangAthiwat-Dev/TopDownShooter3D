using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image frame, background, icon;
    [SerializeField] private TMP_Text glyph, itemName, quantity, status;
    private Action<int> select;
    private int slotIndex;
    public Button Button => button;
    public string DisplayedName => itemName != null ? itemName.text : string.Empty;
    public void Bind(int index, Action<int> callback)
    {
        slotIndex = index; select = callback;
        button.onClick.AddListener(Clicked);
    }
    private void Clicked() => select?.Invoke(slotIndex);
    public void OnSelect(BaseEventData eventData) => select?.Invoke(slotIndex);
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Clicked); }
    public void Show(string title, Sprite sprite, string mark, string count, string state, bool selected, bool melee)
    {
        Color accent = melee ? new Color(1f, 0.76f, 0.32f) : new Color(0.35f, 0.8f, 0.85f);
        frame.color = selected ? accent : new Color(0.23f, 0.29f, 0.35f);
        background.color = selected ? new Color(0.10f, 0.17f, 0.21f) : new Color(0.052f, 0.074f, 0.1f);
        icon.sprite = sprite; icon.gameObject.SetActive(sprite != null);
        glyph.text = mark; glyph.color = accent; glyph.gameObject.SetActive(sprite == null);
        itemName.text = title; quantity.text = count; status.text = state; status.color = accent;
    }
}
