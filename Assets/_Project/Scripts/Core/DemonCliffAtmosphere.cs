using UnityEngine;

/// <summary>Limits local ambience to the approach; audio respects the existing global pause owner.</summary>
public sealed class DemonCliffAtmosphere : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private ParticleSystem[] particles = System.Array.Empty<ParticleSystem>();
    [SerializeField] private AudioSource lavaAmbience;
    [SerializeField] private Vector3 center=new Vector3(60,12,65);
    [SerializeField] private float activeRadius=90f;
    [SerializeField,Range(0,1)] private float ambienceVolume=.08f;
    private bool active,started;
    private float weight;
    private void Start()
    {
        if(player==null){var actor=GameObject.FindGameObjectWithTag("Player");if(actor!=null)player=actor.transform;}
        if(lavaAmbience!=null){lavaAmbience.volume=0;lavaAmbience.playOnAwake=false;}
        foreach(var system in particles)if(system!=null)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void Update()
    {
        if(player==null||Time.timeScale<=0f||AudioListener.pause)return;
        float distance=Vector3.ProjectOnPlane(player.position-center,Vector3.up).magnitude;
        bool nearby=distance<activeRadius;
        if(nearby!=active)
        {
            active=nearby;
            foreach(var system in particles)if(system!=null){if(active)system.Play(true);else system.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        }
        weight=Mathf.MoveTowards(weight,nearby?1:0,Time.deltaTime*.5f);
        if(lavaAmbience==null)return;
        if(nearby&&!started){lavaAmbience.Play();started=true;}
        lavaAmbience.volume=weight*ambienceVolume;
        if(!nearby&&weight<=0&&started){lavaAmbience.Stop();started=false;}
    }
    private void OnDisable()
    {if(lavaAmbience!=null){lavaAmbience.Stop();lavaAmbience.volume=0;}started=active=false;weight=0;}
}
