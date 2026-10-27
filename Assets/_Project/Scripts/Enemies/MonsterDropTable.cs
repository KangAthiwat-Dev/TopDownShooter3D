using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TopDownShooter/Monster Drop Table")]
public sealed class MonsterDropTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public ItemDefinition item;
        [Range(0, 1)] public float chance = 1;
        [Min(1)] public int minimum = 1;
        [Min(1)] public int maximum = 1;
    }

    public Entry[] entries = Array.Empty<Entry>();

    public void Roll(System.Random random, Action<ItemDefinition, int> receive)
    {
        if (entries == null || random == null || receive == null) return;
        foreach (var entry in entries)
        {
            if (entry == null || entry.item == null || entry.chance <= 0 || random.NextDouble() >= entry.chance) continue;
            int minimum = Mathf.Clamp(entry.minimum, 1, 999);
            int maximum = Mathf.Clamp(entry.maximum, minimum, 999);
            receive(entry.item, random.Next(minimum, maximum + 1));
        }
    }
}
