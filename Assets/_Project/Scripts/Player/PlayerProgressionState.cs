using System;
using UnityEngine;

public enum PlayerUpgrade { Vitality, Endurance, Transformation, Firepower, FireRate }

/// <summary>Player EXP, level and derived rank, saved independently of a scene.</summary>
[DisallowMultipleComponent]
public sealed class PlayerProgressionState : MonoBehaviour
{
    public const int MaximumLevel = 100;
    public const int UpgradeCount = 5;
    public const int MaximumUpgradeRank = 20;
    [SerializeField, Range(1, MaximumLevel)] private int level = 1;
    [SerializeField, Min(0)] private int experience;
    [SerializeField] private int[] upgradeRanks = new int[UpgradeCount];
    [SerializeField] private bool persistProgress = true;
    [SerializeField] private string saveKey = "TopDownShooter3D.Progression.v1";
    private bool saveDirty;
    private float saveAt;
    [Serializable] private sealed class SaveData
    { public int version; public int level; public int experience; public int[] upgrades; }
    public int Level => level;
    public int Experience => experience;
    public bool IsMaximumLevel => level >= MaximumLevel;
    public int ExperienceRequired => IsMaximumLevel ? 0 : ExperienceForLevel(level);
    public float LevelProgress => IsMaximumLevel ? 1f : experience / (float)ExperienceRequired;
    public string Rank => RankForLevel(level);
    public event Action Changed;
    public event Action<int, int> LeveledUp;
    public int SpentUpgradePoints
    { get { int total=0; for(int i=0;i<UpgradeCount;i++)total+=UpgradeRank((PlayerUpgrade)i); return total; } }
    public int AvailableUpgradePoints => Mathf.Max(0, level - 1 - SpentUpgradePoints);
    public int UpgradeRank(PlayerUpgrade upgrade)
    {
        int index=(int)upgrade;
        return index>=0&&index<UpgradeCount&&upgradeRanks!=null&&upgradeRanks.Length==UpgradeCount ? upgradeRanks[index] : 0;
    }
    public bool TryUpgrade(PlayerUpgrade upgrade)
    {
        int index=(int)upgrade;
        if(index<0||index>=UpgradeCount||AvailableUpgradePoints<=0||UpgradeRank(upgrade)>=MaximumUpgradeRank)return false;
        var health=GetComponent<PlayerHealth>();if(health!=null&&!health.IsAlive)return false;
        if(upgradeRanks==null||upgradeRanks.Length!=UpgradeCount)upgradeRanks=new int[UpgradeCount];
        upgradeRanks[index]++;MarkDirty();Changed?.Invoke();return true;
    }
    private void MarkDirty()
    {saveDirty=true;if(saveAt<=0)saveAt=Time.unscaledTime+.75f;}

    public static int ExperienceForLevel(int currentLevel)
    {
        if (currentLevel >= MaximumLevel) return 0;
        int step = Mathf.Clamp(currentLevel, 1, MaximumLevel - 1) - 1;
        return 100 + 35 * step + 12 * step * step;
    }

    public static string RankForLevel(int currentLevel)
    {
        if (currentLevel >= 100) return "EX / LEGEND";
        if (currentLevel >= 90) return "SSS / MYTHIC";
        if (currentLevel >= 80) return "SS / CHAMPION";
        if (currentLevel >= 65) return "S / HERO";
        if (currentLevel >= 50) return "A / MASTER";
        if (currentLevel >= 40) return "B / ELITE";
        if (currentLevel >= 30) return "C / VETERAN";
        if (currentLevel >= 20) return "D / ADVENTURER";
        if (currentLevel >= 10) return "E / SCOUT";
        return "F / NOVICE";
    }

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(saveKey)) saveKey = "TopDownShooter3D.Progression.v1";
        level = Mathf.Clamp(level, 1, MaximumLevel);
        if(upgradeRanks==null||upgradeRanks.Length!=UpgradeCount)upgradeRanks=new int[UpgradeCount];
        int spent=0;for(int i=0;i<UpgradeCount;i++){upgradeRanks[i]=Mathf.Clamp(upgradeRanks[i],0,MaximumUpgradeRank);spent+=upgradeRanks[i];}
        if(spent>level-1)Array.Clear(upgradeRanks,0,UpgradeCount);
        experience = IsMaximumLevel ? 0 : Mathf.Clamp(experience, 0, ExperienceRequired - 1);
        if (persistProgress && PlayerPrefs.HasKey(saveKey)) RestoreSave(PlayerPrefs.GetString(saveKey));
    }

    public void GainExperience(int amount)
    {
        if (amount <= 0 || IsMaximumLevel) return;
        int oldLevel = level;
        // A wide accumulator protects against overflow from very large rewards.
        long remaining = (long)experience + amount;
        while (level < MaximumLevel && remaining >= ExperienceForLevel(level))
        { remaining -= ExperienceForLevel(level); level++; }
        experience = IsMaximumLevel ? 0 : (int)remaining;
        MarkDirty();
        Changed?.Invoke();
        if (oldLevel != level) LeveledUp?.Invoke(oldLevel, level);
    }

    public string CaptureSave() => JsonUtility.ToJson(new SaveData { version = 2, level = level, experience = experience, upgrades = (int[])upgradeRanks.Clone() });

    public bool RestoreSave(string json)
    {
        SaveData data;
        try { data = JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { return false; }
        if (data == null || (data.version != 1&&data.version != 2) || data.level < 1 || data.level > MaximumLevel || data.experience < 0 ||
            (data.level == MaximumLevel ? data.experience != 0 : data.experience >= ExperienceForLevel(data.level))) return false;
        int[] ranks=new int[UpgradeCount];
        if(data.version==2)
        {
            if(data.upgrades==null||data.upgrades.Length!=UpgradeCount)return false;
            int spent=0;for(int i=0;i<UpgradeCount;i++){if(data.upgrades[i]<0||data.upgrades[i]>MaximumUpgradeRank)return false;spent+=data.upgrades[i];ranks[i]=data.upgrades[i];}
            if(spent>data.level-1)return false;
        }
        level = data.level; experience = data.experience; upgradeRanks=ranks; saveDirty = false; saveAt = 0;
        Changed?.Invoke(); return true;
    }

    public void FlushSave()
    {
        if (!saveDirty || !persistProgress) return;
        PlayerPrefs.SetString(saveKey, CaptureSave()); PlayerPrefs.Save();
        saveDirty = false; saveAt = 0;
    }
    private void Update() { if (persistProgress && saveDirty && Time.unscaledTime >= saveAt) FlushSave(); }
    private void OnDisable() => FlushSave();
    private void OnApplicationPause(bool paused) { if (paused) FlushSave(); }
    private void OnApplicationQuit() => FlushSave();
}
