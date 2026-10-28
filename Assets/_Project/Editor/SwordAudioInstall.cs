#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public static partial class SwordAudioSetup
{
    private static AudioClip Clip(string name)
    {
        string path="Assets/_Project/Audio/sword/"+name+".mp3";var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(importer==null)throw new InvalidOperationException("Missing sword sound: "+path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.9f;settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Tools/Player Audio/Install Normal and Final Sword Sounds")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!=GameMenuUI.WorldScene)throw new InvalidOperationException("Open WorldPrototype first.");
        var melee=UnityEngine.Object.FindAnyObjectByType<PlayerMeleeAttack>();if(melee.GetComponent<PlayerSwordAudio>()!=null)throw new InvalidOperationException("Sword sounds already installed; keep Inspector tuning.");
        var normal=Clip("musicholder-sword-sound-260274");var final=Clip("dragon-studio-violent-sword-slice-393848");
        var group=Resources.Load<AudioMixer>(GameAudioSettings.MixerResource).FindMatchingGroups("").Single(g=>g.name=="Effects");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeSwordAudio_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var go=new GameObject("Sword Swing Audio");Undo.RegisterCreatedObjectUndo(go,"Add player sword audio");go.transform.SetParent(melee.transform,false);
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;source.outputAudioMixerGroup=group;
        var owner=Undo.AddComponent<PlayerSwordAudio>(melee.gameObject);var so=new SerializedObject(owner);so.FindProperty("source").objectReferenceValue=source;so.FindProperty("normalSwing").objectReferenceValue=normal;so.FindProperty("finalSwing").objectReferenceValue=final;so.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/SwordAudioInstallation.txt","Player sword audio: normal, normal, final; resets after 2 seconds without a swing. Animation cue=0.22 normalized. Original Sword_Attack state remains at speed 1.75. Normal file starts at .17s to skip silence; final at .03s. Effects bus; volumes=.38/.42. No damage, hit count, VFX or animation changes.\n");
        Debug.Log("Normal and final sword swing sounds installed on the player.");
    }
}
#endif
