using UnityEngine;

[RequireComponent(typeof(AudioSource)),DisallowMultipleComponent]
public sealed class MainMenuMusic:MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioSource source;
    [SerializeField,Range(0,1)] private float volume=.22f;
    [SerializeField,Min(.1f)] private float fadeIn=1.4f,fadeOut=.3f;
    private float envelope;
    private bool leaving;
    public AudioSource Source=>source;
    public float FadeOutSeconds=>fadeOut;
    private void Awake()
    {
        if(source==null)source=GetComponent<AudioSource>();
        source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.ignoreListenerPause=true;source.dopplerLevel=0;source.priority=96;source.volume=0;source.clip=clip;
        GameAudioSettings.Route(source,GameAudioBus.Music);
    }
    private void Start(){if(clip!=null)source.Play();}
    private void Update(){envelope=Mathf.MoveTowards(envelope,leaving?0:1,Time.unscaledDeltaTime/(leaving?fadeOut:fadeIn));source.volume=volume*Mathf.SmoothStep(0,1,envelope);}
    public void BeginExit()=>leaving=true;
    private void OnDisable(){if(source!=null)source.Stop();}
}
