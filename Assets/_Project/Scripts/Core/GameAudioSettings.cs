using System;
using UnityEngine;
using UnityEngine.Audio;

public enum GameAudioBus { Master,Music,Ambience,Effects,UI }

/// <summary>One saved mix shared by title and world; sources retain their own fades and balance.</summary>
[DefaultExecutionOrder(-1500),DisallowMultipleComponent]
public sealed class GameAudioSettings:MonoBehaviour
{
    public const string SaveKey="TopDownShooter3D.Audio.v1";
    public const string MixerResource="Audio/GameAudio";
    private static readonly string[] Parameters={"MasterVolume","MusicVolume","AmbienceVolume","EffectsVolume","UIVolume"};
    private static readonly float[] Defaults={1,.8f,.8f,1,.8f};
    private static GameAudioSettings instance;
    private AudioMixer mixer;
    private readonly AudioMixerGroup[] groups=new AudioMixerGroup[5];
    private float[] levels=(float[])Defaults.Clone();
    private bool ready,dirty;
    private float saveAt;
    [Serializable] private sealed class Data {public int version=1;public float[] levels;}
    public static event Action Changed;
    public AudioMixer Mixer=>mixer;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics(){instance=null;Changed=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()=>Ensure();
    public static GameAudioSettings Ensure()
    {
        if(instance!=null)return instance;
        return new GameObject("Game Audio Settings").AddComponent<GameAudioSettings>();
    }
    private void Awake()
    {
        if(instance!=null&&instance!=this){Destroy(gameObject);return;}
        instance=this;DontDestroyOnLoad(gameObject);
        mixer=Resources.Load<AudioMixer>(MixerResource);
        if(mixer==null){Debug.LogError("Game audio mixer is missing from Resources/Audio/GameAudio.");return;}
        foreach(var group in mixer.FindMatchingGroups(""))
            for(int i=0;i<groups.Length;i++)if(group.name==((GameAudioBus)i).ToString())groups[i]=group;
        ReadPreferences();
    }
    // Mixer parameters are applied after Awake, before ordinary audio owners Start.
    private void Start(){ready=true;ApplyAll();Changed?.Invoke();}
    public static float Volume(GameAudioBus bus)=>Ensure().levels[(int)bus];
    public static float Decibels(float linear)=>linear<=0?-80:Mathf.Clamp(20*Mathf.Log10(linear),-80,0);
    public static AudioMixerGroup Group(GameAudioBus bus)=>Ensure().groups[(int)bus];
    public static void Route(AudioSource source,GameAudioBus bus){if(source!=null)source.outputAudioMixerGroup=Group(bus);}
    public static void FlushPreferences(){if(instance!=null)instance.Flush();}
    public static void SetVolume(GameAudioBus bus,float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value)||(int)bus<0||(int)bus>=Defaults.Length)return;
        var owner=Ensure();value=Mathf.Clamp01(value);int i=(int)bus;
        if(Mathf.Approximately(owner.levels[i],value))return;
        owner.levels[i]=value;if(owner.ready&&owner.mixer!=null)owner.mixer.SetFloat(Parameters[i],Decibels(value));
        owner.dirty=true;owner.saveAt=Time.unscaledTime+.3f;Changed?.Invoke();
    }
    public static void ResetToDefaults()
    {
        var owner=Ensure();owner.levels=(float[])Defaults.Clone();if(owner.ready)owner.ApplyAll();
        owner.dirty=true;owner.Flush();Changed?.Invoke();
    }
    public void ReloadPreferences(){ReadPreferences();if(ready)ApplyAll();Changed?.Invoke();}
    private void ReadPreferences()
    {
        levels=(float[])Defaults.Clone();dirty=false;
        if(!PlayerPrefs.HasKey(SaveKey))return;
        try
        {
            var data=JsonUtility.FromJson<Data>(PlayerPrefs.GetString(SaveKey));
            if(data==null||data.version!=1||data.levels==null||data.levels.Length!=Defaults.Length)return;
            foreach(float value in data.levels)if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>1)return;
            levels=data.levels;
        }
        catch(ArgumentException){}
    }
    private void ApplyAll(){if(mixer!=null)for(int i=0;i<levels.Length;i++)mixer.SetFloat(Parameters[i],Decibels(levels[i]));}
    public void Flush()
    {
        if(!dirty)return;PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new Data{levels=(float[])levels.Clone()}));PlayerPrefs.Save();dirty=false;
    }
    private void Update(){if(dirty&&Time.unscaledTime>=saveAt)Flush();}
    private void OnApplicationPause(bool paused){if(paused)Flush();}
    private void OnApplicationQuit()=>Flush();
    private void OnDestroy(){if(instance==this){Flush();instance=null;}}
}
