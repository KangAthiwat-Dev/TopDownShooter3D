using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared currency and owned melee slots, saved together for guild sales and purchases.</summary>
[DisallowMultipleComponent]
public sealed class PlayerGoldWallet : MonoBehaviour
{
    public const int MaximumGold = 999999999;
    [SerializeField, Min(0)] private int gold;
    [SerializeField] private bool persistGold = true;
    [SerializeField] private string saveKey = "TopDownShooter3D.Gold.v1";
    [SerializeField] private MeleeWeaponDefinition[] weaponCatalog;
    [SerializeField] private ItemDefinition[] itemCatalog;
    [Serializable] private sealed class SaveData { public int version; public int gold; public string[] weapons; public int selected; public string[] items; public int[] counts; public string[] questReceipts; }
    private readonly HashSet<string> questReceipts = new HashSet<string>(StringComparer.Ordinal);
    public bool HasQuestReceipt(string id) => questReceipts.Contains(id);
    internal void RecordQuestReceipt(string id) => questReceipts.Add(id);
    private PlayerMeleeEquipment equipment;
    private PlayerInventory inventory;
    private SaveData pendingLoadout;
    private bool started, restoring;
    public int Gold => gold;
    public event Action Changed;

    private void Awake()
    {
        equipment = GetComponent<PlayerMeleeEquipment>();
        inventory = GetComponent<PlayerInventory>();
        gold = Mathf.Clamp(gold, 0, MaximumGold);
        if (string.IsNullOrWhiteSpace(saveKey)) saveKey = "TopDownShooter3D.Gold.v1";
        if (persistGold && PlayerPrefs.HasKey(saveKey)) RestoreSave(PlayerPrefs.GetString(saveKey));
    }
    private void Start()
    {
        started = true; ApplyPendingLoadout();
        if (equipment != null) equipment.SlotsChanged += SaveEquipment;
        if (inventory != null) inventory.Changed += SaveEquipment;
    }
    private void OnDestroy() { if (equipment != null) equipment.SlotsChanged -= SaveEquipment; if (inventory != null) inventory.Changed -= SaveEquipment; }
    private void SaveEquipment() { if (!restoring) Commit(); }
    private void ApplyPendingLoadout()
    {
        if (pendingLoadout == null) return;
        var data = pendingLoadout; pendingLoadout = null;
        restoring = true;
        try
        {
            if (equipment != null && data.weapons != null && data.weapons.Length == PlayerMeleeEquipment.Capacity)
            {
                var loadout = new MeleeWeaponDefinition[PlayerMeleeEquipment.Capacity];
                for (int i = 0; i < loadout.Length; i++)
                    if (!string.IsNullOrEmpty(data.weapons[i]) && weaponCatalog != null)
                        loadout[i] = Array.Find(weaponCatalog, w => w != null && w.WeaponId == data.weapons[i]);
                equipment.RestoreLoadout(loadout, data.selected);
            }
            if (inventory != null && data.items != null && data.items.Length == PlayerInventory.Capacity)
            {
                var restored = new ItemDefinition[PlayerInventory.Capacity];
                for (int i = 0; i < restored.Length; i++)
                    if (!string.IsNullOrEmpty(data.items[i]) && itemCatalog != null)
                        restored[i] = Array.Find(itemCatalog, item => item != null && item.ItemId == data.items[i]);
                inventory.RestoreContents(restored, data.counts);
            }
        }
        finally { restoring = false; }
    }
    public bool CanReceive(long amount) => amount > 0 && amount <= MaximumGold - (long)gold;
    public bool TryCredit(int amount)
    {
        if (!CanReceive(amount)) return false;
        gold += amount; Commit(); return true;
    }
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > gold) return false;
        gold -= amount; Commit(); return true;
    }
    // Inventory validates both sides, updates both states, then publishes either event.
    internal void CreditSale(int amount) => gold += amount;
    internal void DebitPurchase(int amount) => gold -= amount;
    internal void Commit()
    {
        if (persistGold) { PlayerPrefs.SetString(saveKey, CaptureSave()); PlayerPrefs.Save(); }
        Changed?.Invoke();
    }
    public string CaptureSave()
    {
        string[] owned = equipment != null ? new string[PlayerMeleeEquipment.Capacity] : null;
        if (owned != null) for (int i = 0; i < owned.Length; i++) owned[i] = equipment.GetWeapon(i)?.WeaponId ?? "";
        string[] items = inventory != null ? new string[PlayerInventory.Capacity] : null;
        int[] counts = inventory != null ? new int[PlayerInventory.Capacity] : null;
        if (items != null) for (int i = 0; i < items.Length; i++)
        {
            items[i] = inventory.GetItem(i)?.ItemId ?? "";
            // Runtime-only objects without a stable ID cannot be restored; never emit an invalid empty-ID stack.
            counts[i] = string.IsNullOrEmpty(items[i]) ? 0 : inventory.GetQuantity(i);
        }
        var receipts = new List<string>(questReceipts); receipts.Sort(StringComparer.Ordinal);
        return JsonUtility.ToJson(new SaveData { version = 2, gold = gold, weapons = owned, selected = equipment != null ? equipment.SelectedSlotIndex : -1, items = items, counts = counts, questReceipts = receipts.ToArray() });
    }
    public bool RestoreSave(string json)
    {
        SaveData data;
        try { data = JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { return false; }
        if (data == null || (data.version != 1 && data.version != 2) || data.gold < 0 || data.gold > MaximumGold) return false;
        // JsonUtility may encode a null array as []; both represent a legacy Gold-only record.
        if (data.weapons != null && data.weapons.Length != 0 && (data.weapons.Length != PlayerMeleeEquipment.Capacity || data.selected < -1 || data.selected >= PlayerMeleeEquipment.Capacity)) return false;
        bool hasItems = data.items != null && data.items.Length != 0;
        if (hasItems)
        {
            if (data.items.Length != PlayerInventory.Capacity || data.counts == null || data.counts.Length != PlayerInventory.Capacity) return false;
            for (int i = 0; i < data.items.Length; i++)
            {
                if (string.IsNullOrEmpty(data.items[i]) ? data.counts[i] != 0 : data.counts[i] < 1 || data.counts[i] > 999) return false;
                var item = itemCatalog != null ? Array.Find(itemCatalog, a => a != null && a.ItemId == data.items[i]) : null;
                if (item != null && data.counts[i] > item.MaxStack) return false;
            }
        }
        else if (data.counts != null && data.counts.Length != 0) return false;
        pendingLoadout = hasItems || data.weapons != null && data.weapons.Length == PlayerMeleeEquipment.Capacity ? data : null;
        gold = data.gold;
        questReceipts.Clear();
        if (data.questReceipts != null) foreach (string id in data.questReceipts) if (!string.IsNullOrWhiteSpace(id)) questReceipts.Add(id);
        if (started) ApplyPendingLoadout();
        Changed?.Invoke(); return true;
    }
}
