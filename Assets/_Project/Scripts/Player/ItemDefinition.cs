using System;
using UnityEngine;

public enum InventoryItemCategory { Resource, Consumable, Quest }
public enum PotionEffect { None, Heal, Energy, Guard }

[CreateAssetMenu(menuName = "TopDownShooter/Inventory Item")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName = "New Item";
    [SerializeField, TextArea(3, 7)] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private InventoryItemCategory category;
    [SerializeField, Range(1, 999)] private int maxStack = 20;
    [SerializeField, Min(0)] private int sellPrice;
    [Header("Potion (Consumables only)")]
    [SerializeField] private PotionEffect potionEffect;
    [SerializeField, Range(0, 1)] private float potionStrength;
    [SerializeField, Min(0)] private float potionDuration;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public InventoryItemCategory Category => category;
    public int MaxStack => Mathf.Clamp(maxStack, 1, 999);
    public int SellPrice => Mathf.Max(0, sellPrice);
    public bool CanSell => category == InventoryItemCategory.Resource && SellPrice > 0;
    public PotionEffect Effect => category == InventoryItemCategory.Consumable ? potionEffect : PotionEffect.None;
    public float PotionStrength => Mathf.Clamp01(potionStrength);
    public float PotionDuration => Mathf.Max(0, potionDuration);
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId)) itemId = Guid.NewGuid().ToString("N");
        maxStack = Mathf.Clamp(maxStack, 1, 999);
        sellPrice = Mathf.Max(0, sellPrice);
    }
}
