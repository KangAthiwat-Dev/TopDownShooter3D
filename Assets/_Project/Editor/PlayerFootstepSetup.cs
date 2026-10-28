#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerFootstepSetup
{
    private const string Pending="Footstep.Test",Backup="Footstep.StoryBackup",Existed="Footstep.StoryExisted";
    static PlayerFootstepSetup()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Player Audio/Install Terrain and Object Footsteps")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if(player.GetComponent<PlayerFootstepAudio>()!=null)throw new InvalidOperationException("Footsteps already installed; keep Inspector settings.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeFootsteps_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var footsteps=Undo.AddComponent<PlayerFootstepAudio>(player.gameObject);var settings=new SerializedObject(footsteps);
        foreach(string kind in new[]{"outside","inside"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Interaction/Movement/walk_sound_"+kind+".mp3");
            if(clip==null)throw new InvalidOperationException("Missing walking sound "+kind);
            var obj=new GameObject("Footsteps "+kind);obj.transform.SetParent(player.transform,false);obj.transform.localPosition=Vector3.up*.2f;
            var source=obj.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.loop=true;source.volume=0;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=5;source.maxDistance=35;source.dopplerLevel=0;
            settings.FindProperty(kind).objectReferenceValue=source;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(footsteps);if(PrefabUtility.IsPartOfPrefabInstance(footsteps))PrefabUtility.RecordPrefabInstancePropertyModifications(footsteps);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("Player footsteps installed: Terrain=outside; solid objects=inside; movement-only loops with surface crossfade.");
    }
    [MenuItem("Tools/Player Audio/Test Footstep Surfaces and Stops")]
    public static void Test()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Existed,PlayerPrefs.HasKey(PlayerStoryProgress.DefaultSaveKey));SessionState.SetString(Backup,PlayerPrefs.GetString(PlayerStoryProgress.DefaultSaveKey));
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))
        {SessionState.EraseBool(Pending);new GameObject("Temporary footstep verification").AddComponent<PlayerFootstepRegressionDriver>();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetString(Backup,"__none")!="__none")
        {
            if(SessionState.GetBool(Existed,false))PlayerPrefs.SetString(PlayerStoryProgress.DefaultSaveKey,SessionState.GetString(Backup,""));else PlayerPrefs.DeleteKey(PlayerStoryProgress.DefaultSaveKey);
            PlayerPrefs.Save();SessionState.EraseString(Backup);
        }
    }
}
#endif
