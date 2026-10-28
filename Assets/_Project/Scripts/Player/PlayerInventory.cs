using System;
using UnityEngine;

// Supplies only. The two melee slots belong to PlayerMeleeEquipment, not a
// second copy here. UI and hotbar read these exact same eight stacks.
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    public const int Capacity = 8;
    private readonly ItemDefinition[] items = new ItemDefinition[Capacity];
    private readonly int[] quantities = new int[Capacity];
    private bool saleInProgress;
    public event Action Changed;
    public ItemDefinition GetItem(int slot) => slot >= 0 && slot < Capacity ? items[slot] : null;
    public int GetQuantity(int slot) => slot >= 0 && slot < Capacity ? quantities[slot] : 0;
    public int OccupiedSlots
    {
        get { int total = 0; for (int i = 0; i < Capacity; i++) if (items[i] != null) total++; return total; }
    }

    public int SpaceFor(ItemDefinition item)
    {
        if (item == null) return 0;
        long space = 0;
        for (int i = 0; i < Capacity; i++)
            if (items[i] == null) space += item.MaxStack;
            else if (items[i] == item) space += Mathf.Max(0, item.MaxStack - quantities[i]);
        return (int)Math.Min(space, int.MaxValue);
    }
    public bool CanAdd(ItemDefinition item, int amount = 1) => amount > 0 && SpaceFor(item) >= amount;
    public bool TryAdd(ItemDefinition item, int amount = 1)
    {
        // Atomic: a rejected pickup must never consume part of a world item.
        if (saleInProgress || !CanAdd(item, amount)) return false;
        AddUnchecked(item, amount);
        Changed?.Invoke();
        return true;
    }
    private void AddUnchecked(ItemDefinition item, int amount)
    {
        for (int pass = 0; pass < 2 && amount > 0; pass++)
        for (int i = 0; i < Capacity && amount > 0; i++)
        {
            if (pass == 0 ? items[i] != item : items[i] != null) continue;
            int count = Mathf.Min(amount, item.MaxStack - quantities[i]);
            if (count <= 0) continue;
            items[i] = item; quantities[i] += count; amount -= count;
        }
    }
    public bool TryRemove(int slot, int amount = 1)
    {
        if (saleInProgress || slot < 0 || slot >= Capacity || amount <= 0 || items[slot] == null || quantities[slot] < amount) return false;
        quantities[slot] -= amount;
        if (quantities[slot] == 0) items[slot] = null;
        Changed?.Invoke(); return true;
    }

    internal bool TryBuy(ItemDefinition item, int amount, int unitPrice, PlayerGoldWallet wallet)
    {
        long price = (long)amount * unitPrice;
        if (saleInProgress || item == null || amount <= 0 || unitPrice <= 0 || price > int.MaxValue ||
            wallet == null || wallet.gameObject != gameObject || wallet.Gold < price || !CanAdd(item, amount)) return false;
        saleInProgress = true;
        try
        {
            AddUnchecked(item, amount); wallet.DebitPurchase((int)price); wallet.Commit(); Changed?.Invoke(); return true;
        }
        finally { saleInProgress = false; }
    }
    // Gold, the item and its receipt are committed together before either UI event.
    // A full bag/capped wallet leaves the reward available at the quest giver.
    internal bool TryGrantQuestReward(string receipt, ItemDefinition item, int amount, int gold, PlayerGoldWallet wallet)
    {
        if (saleInProgress || wallet == null || wallet.gameObject != gameObject || string.IsNullOrEmpty(receipt)) return false;
        if (wallet.HasQuestReceipt(receipt)) return true;
        if (gold <= 0 || !wallet.CanReceive(gold) || !CanAdd(item, amount)) return false;
        saleInProgress = true;
        try
        {
            AddUnchecked(item, amount); wallet.CreditSale(gold); wallet.RecordQuestReceipt(receipt);
            wallet.Commit(); Changed?.Invoke(); return true;
        }
        finally { saleInProgress = false; }
    }
    internal bool ConsumeOne(int slot, ItemDefinition expected, Action applyEffect)
    {
        if (saleInProgress || slot < 0 || slot >= Capacity || expected == null || items[slot] != expected || quantities[slot] <= 0 || applyEffect == null) return false;
        saleInProgress = true;
        try
        {
            if (--quantities[slot] == 0) items[slot] = null;
            applyEffect(); Changed?.Invoke(); return true;
        }
        finally { saleInProgress = false; }
    }
    internal void RestoreContents(ItemDefinition[] restored, int[] counts)
    {
        if (saleInProgress || restored == null || counts == null || restored.Length != Capacity || counts.Length != Capacity) return;
        for (int i = 0; i < Capacity; i++)
        {
            items[i] = restored[i]; quantities[i] = restored[i] != null ? Mathf.Clamp(counts[i], 0, restored[i].MaxStack) : 0;
            if (quantities[i] == 0) items[i] = null;
        }
        Changed?.Invoke();
    }

    public bool TrySell(int slot, ItemDefinition expected, int amount, PlayerGoldWallet wallet)
    {
        if (saleInProgress || slot < 0 || slot >= Capacity || expected == null || !expected.CanSell ||
            items[slot] != expected || amount <= 0 || quantities[slot] < amount ||
            wallet == null || wallet.gameObject != gameObject) return false;
        long total = (long)expected.SellPrice * amount;
        if (!wallet.CanReceive(total)) return false;
        saleInProgress = true;
        try
        {
            quantities[slot] -= amount;
            if (quantities[slot] == 0) items[slot] = null;
            wallet.CreditSale((int)total);
            wallet.Commit();
            Changed?.Invoke();
        }
        finally { saleInProgress = false; }
        return true;
    }
}
