#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerSkillsRegression
{
    private const string Pending="PlayerSkills.Test",Backup="PlayerSkills.ProfileBackup";
    [Serializable] private sealed class Entry { public string key,value;public bool exists; }
    [Serializable] private sealed class Data {public Entry[] entries;}
    static PlayerSkillsRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Player Skills/Run Skills Combat Save and VFX Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        BackupProfile();SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Player Skills/Run Baseline Movement and Burst Compatibility")]
    public static void Baseline()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)return;BackupProfile();PlayerDevelopmentRegression.Run();}
    private static void BackupProfile()
    {
        string[] keys={"TopDownShooter3D.Progression.v1","TopDownShooter3D.Gold.v1",PlayerStoryProgress.DefaultSaveKey};var entries=new Entry[keys.Length];
        for(int i=0;i<keys.Length;i++)entries[i]=new Entry{key=keys[i],value=PlayerPrefs.GetString(keys[i]),exists=PlayerPrefs.HasKey(keys[i])};
        string json=JsonUtility.ToJson(new Data{entries=entries},true);SessionState.SetString(Backup,json);
        Directory.CreateDirectory("Logs/SkillTests");File.WriteAllText("Logs/SkillTests/ProfileBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json",json);
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))
        {SessionState.EraseBool(Pending);new GameObject("Temporary skill regression").AddComponent<PlayerSkillsRegressionDriver>();}
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        string backup=SessionState.GetString(Backup,"");if(string.IsNullOrEmpty(backup))return;
        foreach(var e in JsonUtility.FromJson<Data>(backup).entries){if(e.exists)PlayerPrefs.SetString(e.key,e.value);else PlayerPrefs.DeleteKey(e.key);}
        PlayerPrefs.Save();SessionState.EraseString(Backup);File.AppendAllText("Logs/PlayerSkillsPlaytest.txt","PASS Original progression, Gold/loadout and story profile restored after Play Mode\n");
    }
}
#endif
