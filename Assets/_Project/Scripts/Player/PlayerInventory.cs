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
        if (!CanAdd(item, amount)) return false;
        for (int pass = 0; pass < 2 && amount > 0; pass++)
        for (int i = 0; i < Capacity && amount > 0; i++)
        {
            if (pass == 0 ? items[i] != item : items[i] != null) continue;
            int count = Mathf.Min(amount, item.MaxStack - quantities[i]);
            if (count <= 0) continue;
            items[i] = item; quantities[i] += count; amount -= count;
        }
        Changed?.Invoke();
        return true;
    }
    public bool TryRemove(int slot, int amount = 1)
    {
        if (slot < 0 || slot >= Capacity || amount <= 0 || items[slot] == null || quantities[slot] < amount) return false;
        quantities[slot] -= amount;
        if (quantities[slot] == 0) items[slot] = null;
        Changed?.Invoke(); return true;
    }
}
