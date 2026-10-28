#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WorldAmbientSetup
{
    private const string Folder="Assets/_Project/Audio/SoundsBack/",Pending="WorldAmbient.Test",Backup="WorldAmbient.Story",Existed="WorldAmbient.StoryExisted";
    static WorldAmbientSetup()=>EditorApplication.playModeStateChanged+=Changed;
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Array(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var s=new SerializedObject(owner);var a=s.FindProperty(field);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    private static AudioClip Clip(string relative)
    {
        string path=Folder+relative;var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(importer==null)throw new InvalidOperationException("Missing supported audio: "+path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;
        importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Tools/Player Audio/Install Forest Village and Adventure Audio")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.Object.FindAnyObjectByType<WorldAmbientAudio>()!=null)throw new InvalidOperationException("World audio already installed; keep Inspector adjustments.");
        AssetDatabase.Refresh();var forest=Clip("forest/soundreality-nature-forest-sound-537925.mp3");var villageClip=Clip("village/village.mp3");var adventure=Clip("adventure/PeriTune_Emerald_Hill_loop.wav");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeWorldAmbience_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var city=UnityEngine.Object.FindAnyObjectByType<TownMusicZone>();
        var root=new GameObject("World Ambience and Exploration Music");var control=root.AddComponent<WorldAmbientAudio>();Ref(control,"player",player);Ref(control,"map",map);Ref(control,"city",city);
        var zones=UnityEngine.Object.FindObjectsByType<WorldMonsterSafeZone>(FindObjectsSortMode.None);
        var village=VillageBounds(map,zones);Ref(control,"village",village);
        Array(control,"excludedZones",zones.Where(z=>z.GetComponentInParent<BossArenaZone>()!=null||z.GetComponentInParent<PlayerSceneSpawnPoint>()!=null).Cast<UnityEngine.Object>().ToArray());
        AudioSource Source(string name,AudioClip clip,bool loop)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);var source=obj.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;source.priority=160;return source;
        }
        Ref(control,"forestSource",Source("Forest Ambience",forest,true));Ref(control,"villageSource",Source("Village Ambience",villageClip,true));Ref(control,"adventureSource",Source("Occasional Adventure Music",adventure,false));Array(control,"adventureTracks",new UnityEngine.Object[]{adventure});
        EditorUtility.SetDirty(control);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/WorldAmbientInstallation.txt","Forest="+forest.length+"s; Village="+villageClip.length+"s; Adventure="+adventure.length+"s\nStreaming audio; original city music unchanged. Village bounds="+(village!=null?village.name:"24m landmark fallback")+"\nFirst quiet exploration music 25–45s; walking 8s; quiet 10s; songs 35–60s; silence 60–120s.\n");
    }
    private static WorldMonsterSafeZone VillageBounds(ForestMapLayout map,WorldMonsterSafeZone[] zones)
    {
        return zones.FirstOrDefault(z=>z.transform.IsChildOf(map.village))??zones.Where(z=>Vector3.ProjectOnPlane(z.transform.TransformPoint(z.center)-map.village.position,Vector3.up).sqrMagnitude<1600)
            .OrderBy(z=>Vector3.ProjectOnPlane(z.transform.TransformPoint(z.center)-map.village.position,Vector3.up).sqrMagnitude).FirstOrDefault();
    }
    [MenuItem("Tools/Player Audio/Use Village Footprint Bounds")]
    public static void LinkVillageBounds()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var control=UnityEngine.Object.FindAnyObjectByType<WorldAmbientAudio>();var map=UnityEngine.Object.FindAnyObjectByType<ForestMapLayout>();
        Ref(control,"village",VillageBounds(map,UnityEngine.Object.FindObjectsByType<WorldMonsterSafeZone>(FindObjectsSortMode.None)));EditorUtility.SetDirty(control);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    [MenuItem("Tools/Player Audio/Test World Ambience and Music Timing")]
    public static void Test()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetString(Backup,PlayerPrefs.GetString(PlayerStoryProgress.DefaultSaveKey));SessionState.SetBool(Existed,PlayerPrefs.HasKey(PlayerStoryProgress.DefaultSaveKey));SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.EraseBool(Pending);new GameObject("Temporary world audio verification").AddComponent<WorldAmbientRegressionDriver>();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetString(Backup,"__none")!="__none")
        {if(SessionState.GetBool(Existed,false))PlayerPrefs.SetString(PlayerStoryProgress.DefaultSaveKey,SessionState.GetString(Backup,""));else PlayerPrefs.DeleteKey(PlayerStoryProgress.DefaultSaveKey);PlayerPrefs.Save();SessionState.EraseString(Backup);}
    }
}
#endif
