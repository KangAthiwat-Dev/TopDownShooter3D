#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StoryQuestRegression
{
    private const string Pending="StoryQuests.PlayTest",Backup="StoryQuests.ProfileBackup";
    [Serializable] private sealed class Entry { public string key,value;public bool exists; }
    [Serializable] private sealed class Profile {public Entry[] entries;}
    static StoryQuestRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Story/Run First Three Steps Play Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        BackupProfile();SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Story/Test Existing Guild Compatibility")]
    public static void GuildCompatibility()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)return;BackupProfile();GuildTradeRegression.Run();}
    [MenuItem("Tools/Story/Test Existing Potion Compatibility")]
    public static void PotionCompatibility()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)return;BackupProfile();PotionShopRegression.Run();}
    private static void BackupProfile()
    {
        var keys=new[]{PlayerStoryProgress.DefaultSaveKey,"TopDownShooter3D.Gold.v1","TopDownShooter3D.Progression.v1"};
        var entries=new Entry[keys.Length];for(int i=0;i<keys.Length;i++)entries[i]=new Entry{key=keys[i],value=PlayerPrefs.GetString(keys[i]),exists=PlayerPrefs.HasKey(keys[i])};
        string json=JsonUtility.ToJson(new Profile{entries=entries},true);SessionState.SetString(Backup,json);
        Directory.CreateDirectory("Logs/StoryTests");File.WriteAllText("Logs/StoryTests/ProfileBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json",json);
    }
    private static void Changed(PlayModeStateChange change)
    {
        if(change==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))
        {SessionState.EraseBool(Pending);new GameObject("Temporary story regression").AddComponent<StoryQuestRegressionDriver>();}
        if(change!=PlayModeStateChange.EnteredEditMode)return;
        string backup=SessionState.GetString(Backup,"");if(string.IsNullOrEmpty(backup))return;
        var profile=JsonUtility.FromJson<Profile>(backup);
        foreach(var e in profile.entries){if(e.exists)PlayerPrefs.SetString(e.key,e.value);else PlayerPrefs.DeleteKey(e.key);}
        PlayerPrefs.Save();SessionState.EraseString(Backup);
        File.AppendAllText("Logs/StoryQuestPlaytest.txt","PASS Original story, Gold/loadout and progression PlayerPrefs restored after Play Mode\n");
    }
}
#endif
