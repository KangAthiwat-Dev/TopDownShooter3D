using System;
using UnityEngine;

public enum InventoryItemCategory { Resource, Consumable, Quest }

[CreateAssetMenu(menuName = "TopDownShooter/Inventory Item")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName = "New Item";
    [SerializeField, TextArea(3, 7)] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private InventoryItemCategory category;
    [SerializeField, Range(1, 999)] private int maxStack = 20;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public InventoryItemCategory Category => category;
    public int MaxStack => Mathf.Clamp(maxStack, 1, 999);
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId)) itemId = Guid.NewGuid().ToString("N");
        maxStack = Mathf.Clamp(maxStack, 1, 999);
    }
}
