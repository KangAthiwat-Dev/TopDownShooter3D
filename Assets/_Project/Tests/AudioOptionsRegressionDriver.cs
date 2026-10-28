#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AudioOptionsRegressionDriver:MonoBehaviour
{
    [Serializable] private sealed class AudioSave {public int version;public float[] levels;}
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private GameMenuUI menu;private PlayerHealth player;private GameAudioSettings settings;
    private bool done,oldBackground;private int oldFps,oldVsync;
    private PropertyInfo editorMute;
    private bool previousMute;
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=Log;
        editorMute=typeof(EditorUtility).GetProperty("audioMasterMute",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
        if(editorMute!=null){previousMute=(bool)editorMute.GetValue(null);editorMute.SetValue(null,false);}
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private void BindWorld()
    {
        player=FindAnyObjectByType<PlayerHealth>();menu=FindAnyObjectByType<GameMenuUI>();
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        var director=FindAnyObjectByType<WorldMonsterDirector>();if(director!=null)director.gameObject.SetActive(false);
        foreach(var wave in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){wave.StopAllCoroutines();wave.enabled=false;}
        foreach(var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))ai.gameObject.SetActive(false);
        foreach(var e in FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None))e.enabled=false;
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerAttackController>().enabled=false;
    }
    private AudioOptionsUI Options()=>Get<GameObject>(menu,"optionsPage").GetComponent<AudioOptionsUI>();
    private AudioOptionsUI.Row Row(GameAudioBus bus)=>Get<AudioOptionsUI.Row[]>(Options(),"rows").First(r=>r.bus==bus);
    private bool Db(GameAudioBus bus,float value)=>settings.Mixer.GetFloat(bus+"Volume",out var db)&&Mathf.Abs(db-GameAudioSettings.Decibels(value))<.02f;
    private IEnumerator Checks()
    {
        yield return null;BindWorld();settings=GameAudioSettings.Ensure();
        Check(settings.Mixer!=null&&settings.Mixer.FindMatchingGroups("").Length==5,"One persistent mixer contains Master, Music, Ambience, Effects and UI groups");
        foreach(GameAudioBus bus in Enum.GetValues(typeof(GameAudioBus)))Check(GameAudioSettings.Group(bus)!=null&&Db(bus,1),"Saved initial level reaches mixer parameter: "+bus);
        Check(FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>s.outputAudioMixerGroup!=null),"All authored world audio sources route through the mixer");
        var ambient=FindAnyObjectByType<WorldAmbientAudio>();var city=FindAnyObjectByType<TownMusicZone>();var feedback=FindAnyObjectByType<UIAudioFeedback>();
        Check(Get<AudioSource>(ambient,"forestSource").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Ambience)&&Get<AudioSource>(ambient,"villageSource").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Ambience),"Forest and village atmosphere use Ambience volume");
        Check(Get<AudioSource>(ambient,"adventureSource").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Music)&&city.MusicSource.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Music),"Exploration tracks and city music use Music volume while retaining their original fades");
        Check(Get<AudioSource>(feedback,"windows").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.UI)&&Get<AudioSource>(feedback,"hover").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.UI),"UI open, close and hover use a separate UI group");
        Check(Get<AudioSource>(player.GetComponent<PlayerRangedFeedback>(),"shotAudio").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects)&&Get<AudioSource>(player.GetComponent<PlayerPickupFeedback>(),"source").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects)&&Get<AudioSource>(player.GetComponent<PlayerFootstepAudio>(),"outside").outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects),"Gun, pickup and walking sounds use Effects volume");
        var enemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_GoblinDemon.prefab"),new Vector3(40,9.83f,25),Quaternion.identity);
        Check(enemy.GetComponentsInChildren<AudioSource>(true).All(s=>s.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects)),"Dynamically spawned enemy voices inherit their prefab Effects route");Destroy(enemy);
        var impact=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/RifleSpikyImpact.prefab"));
        Check(impact.GetComponentsInChildren<AudioSource>(true).All(s=>s.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Effects)),"Spawned combat VFX sounds inherit Effects routing");Destroy(impact);
        Check(menu.TryPause(),"Pause menu opens normally with audio settings installed");menu.ShowOptions();yield return null;
        Check(Options()!=null&&Get<AudioOptionsUI.Row[]>(Options(),"rows").Length==5&&Time.timeScale==0&&AudioListener.pause,"Five sliders are available from pause without resuming gameplay");
        var pointer=new PointerEventData(EventSystem.current){pointerId=-1};var slider=Row(GameAudioBus.Music).slider;var corners=new Vector3[4];slider.GetComponent<RectTransform>().GetWorldCorners(corners);
        pointer.position=Vector3.Lerp(corners[0],corners[2],.25f);pointer.button=PointerEventData.InputButton.Left;ExecuteEvents.Execute(slider.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(slider.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        Check(GameAudioSettings.Volume(GameAudioBus.Music)<.4f&&GameAudioSettings.Volume(GameAudioBus.Music)>.1f,"Actual mouse pointer input changes the music slider while paused");
        Row(GameAudioBus.Master).slider.value=.65f;Row(GameAudioBus.Music).slider.value=.3f;Row(GameAudioBus.Ambience).slider.value=.4f;Row(GameAudioBus.Effects).slider.value=.5f;Row(GameAudioBus.UI).slider.value=.7f;
        Check(Db(GameAudioBus.Master,.65f)&&Db(GameAudioBus.Music,.3f)&&Db(GameAudioBus.Ambience,.4f)&&Db(GameAudioBus.Effects,.5f)&&Db(GameAudioBus.UI,.7f),"All five sliders update real mixer attenuation immediately while timeScale is zero");
        Check(Row(GameAudioBus.Master).value.text=="65%"&&Row(GameAudioBus.UI).value.text=="70%","Slider labels display the saved percentages in the active menu");
        yield return Wait(.5f);var savedAudio=JsonUtility.FromJson<AudioSave>(PlayerPrefs.GetString(GameAudioSettings.SaveKey));
        Check(savedAudio.version==1&&Mathf.Abs(savedAudio.levels[0]-.65f)<.001f,"Audio preferences save automatically using real time while paused");yield return Capture("Pause_Audio_Options");
        Row(GameAudioBus.Music).slider.value=0;Check(Db(GameAudioBus.Music,0)&&Db(GameAudioBus.Effects,.5f)&&Db(GameAudioBus.UI,.7f),"Muting music leaves effects and UI levels unchanged");Row(GameAudioBus.Music).slider.value=.3f;
        Row(GameAudioBus.Master).slider.value=0;Check(Db(GameAudioBus.Master,0),"Master zero attenuates the parent of every category to silence");Row(GameAudioBus.Master).slider.value=.65f;
        float uiCount=0;feedback.Played+=_=>uiCount++;feedback.Play(UIAudioCue.Open);yield return Wait(.1f);Check(uiCount>0&&Get<AudioSource>(feedback,"windows").ignoreListenerPause,"UI feedback can still play through its own volume group while gameplay is paused");
        menu.Back();menu.ShowOptions();Check(Mathf.Abs(Row(GameAudioBus.Music).slider.value-.3f)<.001f,"Closing and reopening Options keeps the selected levels");menu.Back();menu.AskMainMenu();Get<Button>(menu,"confirmYes").onClick.Invoke();
        yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.MainScene,12);yield return Wait(1.7f);menu=FindAnyObjectByType<GameMenuUI>();
        var music=FindAnyObjectByType<MainMenuMusic>();Check(music!=null&&music.Source.isPlaying&&music.Source.loop&&music.Source.clip.name=="PeriTune_Emerald_Hill_loop","Main menu plays the requested Emerald Hill track on loop");
        Check(music.Source.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Music)&&music.Source.volume>.2f,"Menu music fades up to its balanced base gain and uses Music volume");
        Check(FindObjectsByType<MainMenuMusic>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<GameAudioSettings>(FindObjectsSortMode.None).Length==1,"Scene transition keeps one settings owner and one menu music source");
        menu.ShowOptions();yield return null;Check(Mathf.Abs(Row(GameAudioBus.Master).slider.value-.65f)<.001f&&Mathf.Abs(Row(GameAudioBus.Music).slider.value-.3f)<.001f,"Title Options reads the same saved mix selected from pause");
        yield return Capture("Title_Audio_Options");
        Row(GameAudioBus.Music).slider.value=0;yield return Wait(.15f);Check(Db(GameAudioBus.Music,0)&&music.Source.isPlaying,"Music mute operates on the mixer without stopping or restarting the track");Row(GameAudioBus.Music).slider.value=.35f;
        var sources=FindObjectsByType<AudioSource>(FindObjectsSortMode.None);foreach(var s in sources)s.Stop();
        yield return MeasureBuses();
        music.Source.Play();
        Get<Button>(Options(),"defaults").onClick.Invoke();Check(Db(GameAudioBus.Master,1)&&Db(GameAudioBus.Music,.8f)&&Db(GameAudioBus.Ambience,.8f)&&Db(GameAudioBus.Effects,1)&&Db(GameAudioBus.UI,.8f),"Reset button restores balanced default levels across all categories");
        Row(GameAudioBus.Master).slider.value=.8f;Row(GameAudioBus.Music).slider.value=.25f;menu.Back();string saved=PlayerPrefs.GetString(GameAudioSettings.SaveKey);
        GameAudioSettings.SetVolume(GameAudioBus.Music,.9f);settings.ReloadPreferences();Check(Db(GameAudioBus.Music,.25f)&&PlayerPrefs.GetString(GameAudioSettings.SaveKey)==saved,"Reloading preferences restores the saved mix without changing other profile data");
        if(!PlayerAppearanceProfile.TryRead(out _))PlayerAppearanceProfile.Save(PlayerAppearanceData.Default);
        menu.PlayGame();yield return Wait(.12f);Check(music!=null&&music.Source.volume<.2f,"Menu music fades down before gameplay loading begins");
        yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.WorldScene,12);yield return null;yield return null;BindWorld();
        Check(FindAnyObjectByType<MainMenuMusic>()==null&&Db(GameAudioBus.Music,.25f)&&Db(GameAudioBus.Master,.8f),"Entering gameplay removes menu music and retains the selected audio mix");
        menu.TryPause();menu.ShowOptions();Check(Row(GameAudioBus.Music).value.text=="25%"&&Row(GameAudioBus.Master).value.text=="80%","World Options shows the values selected on the title screen");menu.Back();menu.Resume();
        Check(errors.Count==0,"No runtime errors during routing, mute, audio playback, sliders and scene transitions");
    }
    private IEnumerator MeasureBuses()
    {
        report.AppendLine("TRACE audio pause="+AudioListener.pause+" listener volume="+AudioListener.volume+" dsp="+AudioSettings.dspTime+" editor mute="+editorMute?.GetValue(null));
        var go=new GameObject("Temporary audio mix signal");var source=go.AddComponent<AudioSource>();source.loop=true;source.spatialBlend=0;source.ignoreListenerPause=true;source.volume=.1f;
        var clip=AudioClip.Create("Audio routing probe",48000,1,48000,false);var data=new float[48000];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*Mathf.PI*2*440/48000);clip.SetData(data,0);source.clip=clip;
        foreach(GameAudioBus bus in new[]{GameAudioBus.Music,GameAudioBus.Ambience,GameAudioBus.Effects,GameAudioBus.UI})
        {
            GameAudioSettings.SetVolume(GameAudioBus.Master,1);GameAudioSettings.SetVolume(bus,1);source.outputAudioMixerGroup=GameAudioSettings.Group(bus);source.Play();yield return Wait(.2f);
            float audible=OutputRms();GameAudioSettings.SetVolume(bus,0);yield return Wait(.2f);float muted=OutputRms();
            Check(audible>.0001f&&muted<audible*.03f,"Real audio signal is silenced independently by "+bus+" slider (RMS "+audible.ToString("F6")+" → "+muted.ToString("F6")+")");
            GameAudioSettings.SetVolume(bus,1);
        }
        GameAudioSettings.SetVolume(GameAudioBus.Master,0);yield return Wait(.2f);Check(OutputRms()<.0001f,"Master volume silences actual mixed audio output");Destroy(go);Destroy(clip);
    }
    private static float OutputRms(){var values=new float[2048];AudioListener.GetOutputData(values,0);double sum=0;foreach(var v in values)sum+=v*v;return (float)Math.Sqrt(sum/values.Length);}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/AudioOptionsTests/"+name+".png");yield return Wait(.1f);}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/AudioOptionsPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);
        File.WriteAllText("Logs/AudioOptionsPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        GameAudioSettings.FlushPreferences();Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        if(editorMute!=null)editorMute.SetValue(null,previousMute);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Audio options test interrupted"));}
}
#endif
