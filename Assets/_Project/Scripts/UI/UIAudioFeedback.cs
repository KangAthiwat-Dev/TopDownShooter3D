using System;
using UnityEngine;

public enum UIAudioCue { Open, Close, Hover }

/// <summary>Dedicated pause-independent 2D UI voices. Hover never interrupts a menu open/close.</summary>
[DisallowMultipleComponent]
public sealed class UIAudioFeedback : MonoBehaviour
{
    [SerializeField] private AudioSource windows,hover;
    [SerializeField] private AudioClip openClip,closeClip,hoverClip;
    [SerializeField,Range(0,1)] private float windowVolume=.28f,hoverVolume=.18f;
    [SerializeField,Min(.01f)] private float hoverCooldown=.075f;
    private float nextHover;
    private bool ready,started,quitting;
    public event Action<UIAudioCue> Played;
    private void Awake()
    {
        foreach(var source in new[]{windows,hover})if(source!=null)
        {source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.ignoreListenerPause=true;source.dopplerLevel=0;source.Stop();}
    }
    private void Start(){started=true;ready=true;}
    private void OnApplicationQuit()=>quitting=true;
    private void OnDisable(){ready=false;if(windows!=null)windows.Stop();if(hover!=null)hover.Stop();}
    private void OnEnable()=>ready=started;
    public bool Play(UIAudioCue cue)
    {
        if(!Application.isPlaying||!ready||quitting||!isActiveAndEnabled)return false;
        bool isHover=cue==UIAudioCue.Hover;
        if(isHover&&Time.unscaledTime<nextHover)return false;
        var source=isHover?hover:windows;var clip=isHover?hoverClip:cue==UIAudioCue.Open?openClip:closeClip;
        if(source==null||clip==null)return false;
        nextHover=Time.unscaledTime+(isHover?hoverCooldown:.12f);
        source.clip=clip;source.volume=isHover?hoverVolume:windowVolume;source.Play();Played?.Invoke(cue);return true;
    }
}
