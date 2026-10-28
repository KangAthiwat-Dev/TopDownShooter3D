using System;
using UnityEngine;

/// <summary>Dedicated 2D city music with a reversible envelope and boundary hysteresis.</summary>
[RequireComponent(typeof(AudioSource)), DisallowMultipleComponent]
public sealed class TownMusicZone : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioClip clip;
    [SerializeField] private string areaName = "THE CITY";
    [SerializeField] private Vector3 center = new Vector3(75.7626f,8f,-72.7745f);
    [SerializeField] private Vector3 size = new Vector3(42f,14f,44f);
    [SerializeField] private Transform boundsFrame;
    [SerializeField, Min(0f)] private float exitPadding = 1.5f;
    [SerializeField, Range(0f,1f)] private float musicVolume = .22f;
    [SerializeField, Min(.1f)] private float fadeInSeconds = 3.5f;
    [SerializeField, Min(.1f)] private float fadeOutSeconds = 2.5f;
    private float envelope;
    private float combatEnvelope=1;
    private bool combatOverride;
    public void SetCombatOverride(bool value)=>combatOverride=value;
    private bool started,pausedAtSilence;
    public bool IsInside { get; private set; }
    public float Envelope => envelope;
    public float MusicVolume => musicVolume;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
    public AudioSource MusicSource => music;
    public string AreaName => areaName;
    public event Action<bool> PresenceChanged;

    private void Awake()
    {
        if(player==null){var actor=GameObject.FindGameObjectWithTag("Player");if(actor!=null)player=actor.transform;}
        if(music==null)music=GetComponent<AudioSource>();
        music.playOnAwake=false;music.loop=true;music.spatialBlend=0f;music.dopplerLevel=0f;
        music.priority=128;music.clip=clip;music.volume=0f;music.Stop();
    }
    public bool Contains(Vector3 position,bool includeExitMargin=false)
    {
        var bounds=new Bounds(center,size);
        if(includeExitMargin)bounds.Expand(new Vector3(exitPadding*2f,0f,exitPadding*2f));
        return bounds.Contains(boundsFrame != null ? boundsFrame.InverseTransformPoint(position) : position);
    }
    private void Update()
    {
        if(player==null||music==null||clip==null||Time.timeScale<=0f||AudioListener.pause)return;
        bool inside=Contains(player.position,IsInside);
        if(inside!=IsInside){IsInside=inside;PresenceChanged?.Invoke(inside);}
        if(IsInside)
        {
            if(!started){music.Play();started=true;}
            else if(pausedAtSilence){music.UnPause();pausedAtSilence=false;}
        }
        envelope=Mathf.MoveTowards(envelope,IsInside?1f:0f,Time.deltaTime/(IsInside?fadeInSeconds:fadeOutSeconds));
        combatEnvelope=Mathf.MoveTowards(combatEnvelope,combatOverride?0:1,Time.deltaTime/1.2f);
        music.volume=musicVolume*Mathf.SmoothStep(0f,1f,envelope)*Mathf.SmoothStep(0f,1f,combatEnvelope);
        if(!IsInside&&envelope<=0f&&started&&!pausedAtSilence){music.Pause();pausedAtSilence=true;}
    }
    private void OnDisable()
    {
        if(music!=null){music.Stop();music.volume=0f;}
        started=pausedAtSilence=IsInside=false;envelope=0f;combatOverride=false;combatEnvelope=1;
    }
    private void OnDrawGizmosSelected()
    {Gizmos.color=new Color(.85f,.68f,.3f,.8f);Gizmos.matrix=boundsFrame!=null?boundsFrame.localToWorldMatrix:Matrix4x4.identity;Gizmos.DrawWireCube(center,size);}
}
