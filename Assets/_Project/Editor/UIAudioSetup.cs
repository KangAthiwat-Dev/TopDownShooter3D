#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UIAudioSetup
{
    private const string Pending="UIAudio.Test";
    static UIAudioSetup()=>EditorApplication.playModeStateChanged+=Changed;
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    [MenuItem("Tools/Player Audio/Install UI Open Close and Slot Hover")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.Object.FindAnyObjectByType<UIAudioFeedback>()!=null)throw new InvalidOperationException("UI audio already installed; keep Inspector settings.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeUIAudio_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var canvas=UnityEngine.Object.FindAnyObjectByType<GamePauseController>().transform;
        var root=new GameObject("UI Audio Feedback");root.transform.SetParent(canvas,false);var feedback=root.AddComponent<UIAudioFeedback>();
        foreach(string field in new[]{"windows","hover"})
        {
            var obj=new GameObject("UI "+field+" Voice");obj.transform.SetParent(root.transform,false);var audio=obj.AddComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=0;audio.ignoreListenerPause=true;audio.priority=32;Ref(feedback,field,audio);
        }
        string[] fields={"openClip","closeClip","hoverClip"},files={"open_ui","close_ui","hover_slot"};
        for(int i=0;i<fields.Length;i++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/UI/"+files[i]+".mp3");if(clip==null)throw new InvalidOperationException("Missing UI sound "+files[i]);Ref(feedback,fields[i],clip);
        }
        int modals=0;var targets=new HashSet<GameObject>();
        foreach(var owner in canvas.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(owner==null)continue;bool modal=owner is InventoryPanelUI||owner is PlayerUpgradePanelUI||owner is GuildTradeUI||owner is PotionShopUI||owner is WeaponShopUI||owner is StoryDialogueUI||owner is StoryQuestUI;
            if(modal)
            {
                var property=new SerializedObject(owner).FindProperty(owner is StoryQuestUI?"journalWindow":"window");var window=(GameObject)property.objectReferenceValue;
                Ref(Undo.AddComponent<UIModalAudio>(window),"feedback",feedback);modals++;
            }
            if(owner is InventorySlotUI slot)targets.Add(slot.Button.gameObject);
            if(owner is InventoryHotbarUI)
            {
                var slots=new SerializedObject(owner).FindProperty("slots");
                for(int i=0;i<slots.arraySize;i++){var image=(Image)slots.GetArrayElementAtIndex(i).FindPropertyRelative("frame").objectReferenceValue;if(image!=null){image.raycastTarget=true;EditorUtility.SetDirty(image);targets.Add(image.gameObject);}}
            }
            if(owner is GuildTradeUI||owner is PotionShopUI||owner is WeaponShopUI)
            {
                var s=new SerializedObject(owner);var rows=s.FindProperty("rows");
                for(int i=0;i<rows.arraySize;i++){var button=(Button)rows.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue;if(button!=null)targets.Add(button.gameObject);}
                var slots=s.FindProperty("slotButtons");if(slots!=null)for(int i=0;i<slots.arraySize;i++){var button=(Button)slots.GetArrayElementAtIndex(i).objectReferenceValue;if(button!=null)targets.Add(button.gameObject);}
            }
        }
        foreach(var obj in targets)Ref(Undo.AddComponent<UISlotHoverAudio>(obj),"feedback",feedback);
        EditorUtility.SetDirty(feedback);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/UIAudioInstallation.txt",$"UI audio installed: {modals} modal windows; {targets.Count} slot hover targets.\nOpen/close volume=.28; hover=.18; pause-independent 2D sources; hover cooldown=.075s.\n");
    }
    [MenuItem("Tools/Player Audio/Test UI Sounds While Paused")]
    public static void Test(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.EraseBool(Pending);new GameObject("Temporary UI audio test").AddComponent<UIAudioRegressionDriver>();}}
}
#endif
