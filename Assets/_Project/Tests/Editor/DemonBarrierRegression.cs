#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DemonBarrierRegression
{
    private const string Pending="DemonBarrier.Test",Backup="DemonBarrier.Profile";
    [Serializable] private sealed class Entry{public string key,value;public bool exists;}
    [Serializable] private sealed class Profile{public Entry[] entries;}
    static DemonBarrierRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Demon Barriers/Test User Door Sample Stalagmites and Collision")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        string[] keys={PlayerStoryProgress.DefaultSaveKey,"TopDownShooter3D.Gold.v1","TopDownShooter3D.Progression.v1"};
        var entries=new Entry[keys.Length];for(int i=0;i<keys.Length;i++)entries[i]=new Entry{key=keys[i],value=PlayerPrefs.GetString(keys[i]),exists=PlayerPrefs.HasKey(keys[i])};
        string json=JsonUtility.ToJson(new Profile{entries=entries},true);SessionState.SetString(Backup,json);Directory.CreateDirectory("Logs/DemonBarrierTests");File.WriteAllText("Logs/DemonBarrierTests/ProfileBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json",json);
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.EraseBool(Pending);new GameObject("Temporary user barrier regression").AddComponent<DemonBarrierRegressionDriver>();}
        if(state!=PlayModeStateChange.EnteredEditMode)return;string json=SessionState.GetString(Backup,"");if(string.IsNullOrEmpty(json))return;
        foreach(var e in JsonUtility.FromJson<Profile>(json).entries){if(e.exists)PlayerPrefs.SetString(e.key,e.value);else PlayerPrefs.DeleteKey(e.key);}PlayerPrefs.Save();SessionState.EraseString(Backup);
        File.AppendAllText("Logs/DemonBarrierPlaytest.txt","PASS Original story, Gold/loadout and progression PlayerPrefs restored\n");
    }
}
#endif
