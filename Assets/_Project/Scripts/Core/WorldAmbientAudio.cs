using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldAudioArea { Forest, Village, OtherZone }

/// <summary>Forest/village beds and occasional peaceful exploration music; city music keeps its existing owner.</summary>
[DefaultExecutionOrder(70),DisallowMultipleComponent]
public sealed class WorldAmbientAudio : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private ForestMapLayout map;
    [SerializeField] private TownMusicZone city;
    [SerializeField] private WorldMonsterSafeZone village;
    [SerializeField] private WorldMonsterSafeZone[] excludedZones;
    [SerializeField] private AudioSource forestSource,villageSource,adventureSource;
    [SerializeField] private AudioClip[] adventureTracks;
    [SerializeField,Range(0,1)] private float forestVolume=.22f,villageVolume=.22f,adventureVolume=.16f;
    [SerializeField,Min(.1f)] private float fadeSeconds=2.5f;
    [SerializeField,Min(0)] private float quietBeforeMusic=10,walkingBeforeMusic=8;
    [SerializeField] private Vector2 firstWait=new Vector2(25,45),repeatWait=new Vector2(60,120),songDuration=new Vector2(35,60);
    private PlayerHealth health;
    private PlayerAttackController attack;
    private PlayerInputGate gate;
    private DemonCliffZone cliffs;
    private readonly List<IBossHealthSource> bosses=new List<IBossHealthSource>();
    private Vector3 previousPosition;
    private float quietTime,walkTime,waitTime,songTime,lastMoved=-100,lastDamage=-100,previousHp,nextThreatCheck;
    private bool threat,musicActive;
    private int previousTrack=-1;
    public WorldAudioArea CurrentArea {get;private set;}
    public bool IsThreatened=>threat;
    public bool IsAdventurePlaying=>musicActive;
    public float QuietWalkingSeconds=>walkTime;
    public event Action AdventureStarted;
    private static float Pick(Vector2 range)=>UnityEngine.Random.Range(Mathf.Max(0,Mathf.Min(range.x,range.y)),Mathf.Max(0,Mathf.Max(range.x,range.y)));
    private void Awake()
    {
        health=player.GetComponent<PlayerHealth>();attack=player.GetComponent<PlayerAttackController>();gate=player.GetComponent<PlayerInputGate>();cliffs=FindAnyObjectByType<DemonCliffZone>();
        foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(behaviour is IBossHealthSource boss)bosses.Add(boss);
    }
    private void OnEnable()
    {
        previousPosition=player.transform.position;previousHp=health.CurrentHealth;CurrentArea=ResolveArea(previousPosition);waitTime=Pick(firstWait);quietTime=walkTime=0;
        health.HealthChanged+=OnHealth;health.Died+=StopAll;
    }
    private void OnDisable(){if(health!=null){health.HealthChanged-=OnHealth;health.Died-=StopAll;}StopAll();}
    private void OnHealth(float current,float maximum){if(current<previousHp)lastDamage=Time.time;previousHp=current;}
    private static bool Near(Transform point,Vector3 position,float radius)=>point!=null&&Vector3.ProjectOnPlane(point.position-position,Vector3.up).sqrMagnitude<radius*radius;
    public WorldAudioArea ResolveArea(Vector3 position)
    {
        if(city!=null&&city.Contains(position,city.IsInside))return WorldAudioArea.OtherZone;
        if(village!=null&&village.Contains(position,CurrentArea==WorldAudioArea.Village?1.5f:0))return WorldAudioArea.Village;
        if(village==null&&Near(map.village,position,24))return WorldAudioArea.Village;
        if(excludedZones!=null)foreach(var zone in excludedZones)if(zone!=null&&zone.Contains(position))return WorldAudioArea.OtherZone;
        if(Near(map.sanctuary,position,24)||Near(map.castle,position,28)||Near(map.camp,position,13)||cliffs!=null&&cliffs.Contains(position))return WorldAudioArea.OtherZone;
        return WorldAudioArea.Forest;
    }
    private bool ThreatNearby()
    {
        if(Time.time-lastDamage<quietBeforeMusic||attack!=null&&attack.IsAttackHeld)return true;
        foreach(var enemy in EnemyAI.ActiveEnemies)
        {
            if(enemy==null||!enemy.isActiveAndEnabled||!enemy.TryGetComponent<EnemyHealth>(out var enemyHealth)||!enemyHealth.IsAlive)continue;
            float distance=Vector3.ProjectOnPlane(enemy.transform.position-player.transform.position,Vector3.up).sqrMagnitude;
            if(distance<64||distance<400&&enemy.HasTargetMemory)return true;
        }
        foreach(var boss in bosses)if(boss.Health!=null&&boss.Health.IsAlive&&boss.ShowHealthBar)return true;
        return false;
    }
    private void Update()
    {
        Vector3 position=player.transform.position,delta=position-previousPosition;previousPosition=position;delta.y=0;
        if(!health.IsAlive){StopAll();return;}
        if(Time.timeScale<=0||AudioListener.pause||gate!=null&&gate.BlocksGameplay)return;
        var area=ResolveArea(position);
        if(area!=CurrentArea){CurrentArea=area;musicActive=false;quietTime=walkTime=0;waitTime=Pick(firstWait);}
        if(Time.time>=nextThreatCheck){nextThreatCheck=Time.time+.25f;threat=ThreatNearby();}
        bool forest=area==WorldAudioArea.Forest;
        if(!forest||threat)
        {
            if(musicActive){musicActive=false;waitTime=Pick(repeatWait);}
            quietTime=walkTime=0;
        }
        else
        {
            quietTime+=Time.deltaTime;waitTime-=Time.deltaTime;
            float speed=delta.magnitude/Mathf.Max(.0001f,Time.deltaTime);
            if(player.isActiveAndEnabled&&player.IsGrounded&&!player.IsDashing&&!player.IsShadowDashing&&speed>.25f&&speed<=player.SprintSpeed*1.25f)
            {walkTime+=Time.deltaTime;lastMoved=Time.time;}
            if(musicActive)
            {
                songTime-=Time.deltaTime;
                if(songTime<=0||!adventureSource.isPlaying){musicActive=false;waitTime=Pick(repeatWait);walkTime=0;}
            }
            else if(waitTime<=0&&quietTime>=quietBeforeMusic&&walkTime>=walkingBeforeMusic&&Time.time-lastMoved<5&&adventureSource.volume<=.001f)
                StartAdventure();
        }
        Fade(forestSource,forest?forestVolume*(musicActive?.55f:1):0,fadeSeconds);
        Fade(villageSource,area==WorldAudioArea.Village?villageVolume:0,fadeSeconds);
        Fade(adventureSource,musicActive?adventureVolume:0,threat?.65f:fadeSeconds);
    }
    private void StartAdventure()
    {
        if(adventureTracks==null||adventureTracks.Length==0)return;
        int index=UnityEngine.Random.Range(0,adventureTracks.Length);
        if(adventureTracks.Length>1&&index==previousTrack)index=(index+1)%adventureTracks.Length;
        var clip=adventureTracks[index];if(clip==null){waitTime=Pick(repeatWait);return;}
        previousTrack=index;adventureSource.clip=clip;adventureSource.loop=false;adventureSource.Play();musicActive=true;
        songTime=Mathf.Min(Pick(songDuration),Mathf.Max(1,clip.length-.2f));AdventureStarted?.Invoke();
    }
    private static void Fade(AudioSource source,float target,float seconds)
    {
        if(source==null||source.clip==null)return;
        if(target>0&&!source.isPlaying)source.Play();
        source.volume=Mathf.MoveTowards(source.volume,target,Time.deltaTime*.3f/Mathf.Max(.1f,seconds));
        if(target<=0&&source.volume<=.001f){source.volume=0;source.Stop();}
    }
    private void StopAll()
    {musicActive=false;foreach(var source in new[]{forestSource,villageSource,adventureSource})if(source!=null){source.Stop();source.volume=0;}}
}
