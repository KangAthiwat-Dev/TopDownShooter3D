using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldAudioArea { Forest, Village, OtherZone, Demon }

/// <summary>Area ambience, peaceful exploration cues and combat music with a shared priority.</summary>
[DefaultExecutionOrder(70),DisallowMultipleComponent]
public sealed class WorldAmbientAudio : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private ForestMapLayout map;
    [SerializeField] private TownMusicZone city;
    [SerializeField] private WorldMonsterSafeZone village;
    [SerializeField] private WorldMonsterSafeZone[] excludedZones;
    [SerializeField] private AudioSource forestSource,villageSource,adventureSource;
    [Header("Demon territory and combat")]
    [SerializeField] private Transform demonTerritory;
    [SerializeField] private Vector2 demonTerritorySize;
    [SerializeField] private BossArenaZone[] demonAreas;
    [SerializeField] private AudioSource demonSource,combatSource;
    [SerializeField,Range(0,1)] private float demonVolume=.18f,combatVolume=.22f;
    [SerializeField,Min(.1f)] private float combatFadeIn=.8f,combatReleaseSeconds=6;
    [SerializeField] private AudioClip[] adventureTracks;
    [SerializeField,Range(0,1)] private float forestVolume=.22f,villageVolume=.22f,adventureVolume=.16f;
    [SerializeField,Min(.1f)] private float fadeSeconds=2.5f;
    [SerializeField,Min(0)] private float quietBeforeMusic=10,walkingBeforeMusic=8;
    [SerializeField] private Vector2 firstWait=new Vector2(25,45),repeatWait=new Vector2(60,120),songDuration=new Vector2(35,60);
    private PlayerHealth health;
    private PlayerAttackController attack;
    private PlayerInputGate gate;
    private DemonCliffZone cliffs;
    private DemonArenaEncounter[] encounters;
    private readonly List<IBossHealthSource> bosses=new List<IBossHealthSource>();
    private Vector3 previousPosition;
    private float quietTime,walkTime,waitTime,songTime,lastMoved=-100,lastDamage=-100,lastCombat=-100,previousHp,nextThreatCheck;
    private bool threat,musicActive,combatActive;
    private int previousTrack=-1;
    public WorldAudioArea CurrentArea {get;private set;}
    public bool IsThreatened=>threat;
    public bool IsAdventurePlaying=>musicActive;
    public bool IsCombatPlaying=>combatActive;
    public float QuietWalkingSeconds=>walkTime;
    public event Action AdventureStarted;
    private static float Pick(Vector2 range)=>UnityEngine.Random.Range(Mathf.Max(0,Mathf.Min(range.x,range.y)),Mathf.Max(0,Mathf.Max(range.x,range.y)));
    private void Awake()
    {
        health=player.GetComponent<PlayerHealth>();attack=player.GetComponent<PlayerAttackController>();gate=player.GetComponent<PlayerInputGate>();cliffs=FindAnyObjectByType<DemonCliffZone>();
        encounters=FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);
        foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(behaviour is IBossHealthSource boss)bosses.Add(boss);
    }
    private void OnEnable()
    {
        previousPosition=player.transform.position;previousHp=health.CurrentHealth;CurrentArea=ResolveArea(previousPosition);waitTime=Pick(firstWait);quietTime=walkTime=0;lastDamage=lastCombat=-100;nextThreatCheck=0;threat=combatActive=false;
        health.HealthChanged+=OnHealth;health.Died+=StopAll;
    }
    private void OnDisable(){if(health!=null){health.HealthChanged-=OnHealth;health.Died-=StopAll;}StopAll();}
    private void OnHealth(float current,float maximum){if(current<previousHp)lastDamage=lastCombat=Time.time;previousHp=current;}
    private static bool Near(Transform point,Vector3 position,float radius)=>point!=null&&Vector3.ProjectOnPlane(point.position-position,Vector3.up).sqrMagnitude<radius*radius;
    public WorldAudioArea ResolveArea(Vector3 position)
    {
        if(city!=null&&city.Contains(position,city.IsInside))return WorldAudioArea.OtherZone;
        if(village!=null&&village.Contains(position,CurrentArea==WorldAudioArea.Village?1.5f:0))return WorldAudioArea.Village;
        if(village==null&&Near(map.village,position,24))return WorldAudioArea.Village;
        if(InDemonArea(position))return WorldAudioArea.Demon;
        if(excludedZones!=null)foreach(var zone in excludedZones)if(zone!=null&&zone.Contains(position))return WorldAudioArea.OtherZone;
        if(Near(map.sanctuary,position,24)||Near(map.castle,position,28)||Near(map.camp,position,13)||cliffs!=null&&cliffs.Contains(position))return WorldAudioArea.OtherZone;
        return WorldAudioArea.Forest;
    }
    private bool InDemonArea(Vector3 position)
    {
        float padding=CurrentArea==WorldAudioArea.Demon?1.5f:0;
        if(demonAreas!=null)foreach(var arena in demonAreas)if(arena!=null&&arena.Contains(position,padding))return true;
        if(demonTerritory!=null)
        {
            var local=demonTerritory.InverseTransformPoint(position);var scale=demonTerritory.lossyScale;
            return Mathf.Abs(local.x)<=demonTerritorySize.x*.5f+padding/Mathf.Max(.001f,Mathf.Abs(scale.x))&&
                Mathf.Abs(local.z)<=demonTerritorySize.y*.5f+padding/Mathf.Max(.001f,Mathf.Abs(scale.z));
        }
        if(Near(map.castle,position,28+padding))return true;
        if(cliffs==null)return false;
        if(cliffs.Contains(position))return true;
        // Keep a small exit margin at the polygon edge to avoid rapid audio switching.
        if(padding>0)for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;if(cliffs.Contains(position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*padding))return true;}
        return false;
    }
    private bool CombatNearby()
    {
        // Arena ownership includes newly spawned dragons and the gap between waves.
        foreach(var encounter in encounters)
            if(encounter!=null&&encounter.isActiveAndEnabled&&encounter.WaveIndex>=0&&!encounter.Completed&&encounter.Arena.PlayerInside)return true;
        foreach(var boss in bosses)
        {
            if(boss is Behaviour owner&&owner==null)continue;
            if(boss.Health!=null&&boss.Health.IsAlive&&boss.ShowHealthBar)return true;
        }
        foreach(var enemy in EnemyAI.ActiveEnemies)
        {
            if(enemy==null||!enemy.isActiveAndEnabled||!enemy.TryGetComponent<EnemyHealth>(out var hp)||!hp.IsAlive)continue;
            if(Vector3.ProjectOnPlane(enemy.transform.position-player.transform.position,Vector3.up).sqrMagnitude>900)continue;
            bool targetsPlayer=enemy.Target!=null&&(enemy.Target==player.transform||enemy.Target.IsChildOf(player.transform));
            if(targetsPlayer&&enemy.HasTargetMemory||attack!=null&&attack.IsAttackHeld)return true;
        }
        return false;
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
        foreach(var boss in bosses)if(!(boss is Behaviour owner&&owner==null)&&boss.Health!=null&&boss.Health.IsAlive&&boss.ShowHealthBar)return true;
        return false;
    }
    private void Update()
    {
        Vector3 position=player.transform.position,delta=position-previousPosition;previousPosition=position;delta.y=0;
        if(!health.IsAlive){StopAll();return;}
        if(Time.timeScale<=0||AudioListener.pause||gate!=null&&gate.BlocksGameplay)return;
        var area=ResolveArea(position);
        if(area!=CurrentArea){CurrentArea=area;musicActive=false;quietTime=walkTime=0;waitTime=Pick(firstWait);}
        if(Time.time>=nextThreatCheck){nextThreatCheck=Time.time+.25f;threat=ThreatNearby();if(CombatNearby())lastCombat=Time.time;}
        combatActive=Time.time-lastCombat<combatReleaseSeconds;
        if(city!=null)city.SetCombatOverride(combatActive);
        bool forest=area==WorldAudioArea.Forest;
        if(!forest||threat||combatActive)
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
        Fade(forestSource,forest?forestVolume*(combatActive?.4f:musicActive?.55f:1):0,fadeSeconds);
        Fade(villageSource,area==WorldAudioArea.Village?villageVolume*(combatActive?.4f:1):0,fadeSeconds);
        Fade(demonSource,area==WorldAudioArea.Demon?demonVolume*(combatActive?.25f:1):0,fadeSeconds);
        Fade(adventureSource,musicActive?adventureVolume:0,threat||combatActive?.65f:fadeSeconds);
        Fade(combatSource,combatActive?combatVolume:0,combatActive?combatFadeIn:fadeSeconds);
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
    {musicActive=combatActive=false;if(city!=null)city.SetCombatOverride(false);foreach(var source in new[]{forestSource,villageSource,adventureSource,demonSource,combatSource})if(source!=null){source.Stop();source.volume=0;}}
    private void OnDrawGizmosSelected()
    {
        if(demonTerritory==null)return;Gizmos.matrix=demonTerritory.localToWorldMatrix;Gizmos.color=new Color(.8f,.25f,.6f);
        Gizmos.DrawWireCube(Vector3.zero,new Vector3(demonTerritorySize.x,8,demonTerritorySize.y));
    }
}
