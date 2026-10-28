#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public sealed class DemonCombatAudioRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private WorldAmbientAudio audioControl;private PlayerHealth player;private CharacterController controller;
    private bool done,oldBackground;private int oldFps,oldVsync;private GameObject subject;
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=Log;
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;audioControl=FindAnyObjectByType<WorldAmbientAudio>();player=FindAnyObjectByType<PlayerHealth>();controller=player.GetComponent<CharacterController>();
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerAttackController>().enabled=false;player.GetComponent<PlayerMeleeAttack>().enabled=false;player.GetComponent<PlayerRangedWeapon>().enabled=false;player.ConfigureUpgrades(20000,0);
        var director=FindAnyObjectByType<WorldMonsterDirector>();director.gameObject.SetActive(false);
        foreach(var wave in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){wave.StopAllCoroutines();wave.enabled=false;}
        var encounters=FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);foreach(var e in encounters)e.enabled=false;
        foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        Set(audioControl,"fadeSeconds",.25f);Set(audioControl,"combatFadeIn",.2f);Set(audioControl,"combatReleaseSeconds",.7f);
        var demon=Get<AudioSource>(audioControl,"demonSource");var combat=Get<AudioSource>(audioControl,"combatSource");var forest=Get<AudioSource>(audioControl,"forestSource");var village=Get<AudioSource>(audioControl,"villageSource");var adventure=Get<AudioSource>(audioControl,"adventureSource");
        Check(demon.clip.name=="horror-ambience-2482"&&combat.clip.name=="torone-music-loops-pack-01-dark-fantasy-combat","Requested Demon and fighting clips are connected");
        Check(demon.clip.loadType==AudioClipLoadType.Streaming&&combat.clip.loadType==AudioClipLoadType.Streaming&&demon.loop&&combat.loop&&!demon.playOnAwake&&!combat.playOnAwake,"Both long tracks stream and loop only when requested");
        Check(demon.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Ambience)&&combat.outputAudioMixerGroup==GameAudioSettings.Group(GameAudioBus.Music),"Existing Ambience and Music options control the new tracks");
        var areas=Get<BossArenaZone[]>(audioControl,"demonAreas");
        foreach(var area in areas)
        {
            Warp(area.GetComponent<BoxCollider>().bounds.center);yield return Wait(.6f);
            Check(audioControl.CurrentArea==WorldAudioArea.Demon&&demon.isPlaying&&demon.volume>.15f&&forest.volume==0&&village.volume==0&&!audioControl.IsCombatPlaying,"Demon ambience covers "+area.name+" without forest/village overlap");
        }
        var boundary=Get<Transform>(audioControl,"demonTerritory");var size=Get<Vector2>(audioControl,"demonTerritorySize");
        Check(boundary!=null&&boundary.IsChildOf(GameObject.Find("Zone_Demon").transform),"Editable audio boundary follows the Demon zone parent");
        Warp(boundary.position);yield return Wait(.35f);float before=demon.time;Warp(boundary.position+Vector3.right);yield return Wait(.3f);
        Check(demon.isPlaying&&demon.time>before,"Moving within the territory does not restart the Demon loop");
        var edge=boundary.TransformPoint(new Vector3(-size.x*.5f-.5f,0,0));
        Check(audioControl.ResolveArea(edge)==WorldAudioArea.Demon,"Demon boundary includes exit hysteresis to prevent rapid switching");
        // Start away from all configured zones and threats.
        var point=new Vector3(-150,4,-150);Warp(point);yield return Wait(.6f);
        Check(audioControl.CurrentArea==WorldAudioArea.Forest&&forest.isPlaying&&demon.volume==0&&!combat.isPlaying,"Leaving Demon territory restores forest ambience");
        // Find two reachable positions in the ordinary forest, away from authored camps.
        Vector3 spawn=default,target=default;bool found=false;
        foreach(var site in director.roamingSites)
        {
            if(site.point==null||audioControl.ResolveArea(site.point.position)!=WorldAudioArea.Forest)continue;
            if(!director.TryGround(site.point.position,1,out spawn)||!director.TryGround(spawn+Vector3.forward*4,1,out target))continue;
            if(NavMesh.Raycast(spawn,target,out _,NavMesh.AllAreas)||Physics.Linecast(spawn+Vector3.up*1.45f,target+Vector3.up*1.2f,~0,QueryTriggerInteraction.Ignore))continue;
            found=true;break;
        }
        Check(found,"Forest combat check uses two unobstructed NavMesh positions");Warp(target);
        subject=Instantiate(director.goblinPrefab,spawn,Quaternion.LookRotation(spawn-target));var ai=subject.GetComponent<EnemyAI>();
        Set(ai,"huntCivilians",false);Set(ai,"nextPatrolTime",Time.time+100);Set(ai,"patrolSpeed",.01f);Set(ai,"rotationSpeed",0f);subject.GetComponent<EnemyAttack>().enabled=false;
        yield return null;Set(ai,"nextPatrolTime",Time.time+100);yield return Wait(.5f);
        Check(!ai.HasTargetMemory&&!audioControl.IsCombatPlaying,"A nearby unaware monster does not start combat music");
        subject.transform.rotation=Quaternion.LookRotation(target-spawn);yield return Until(()=>ai.HasTargetMemory&&audioControl.IsCombatPlaying,4);yield return Wait(.3f);
        Check(ai.Target==player.transform&&ai.HasTargetMemory&&audioControl.IsCombatPlaying&&combat.isPlaying&&combat.volume>.18f&&!audioControl.IsAdventurePlaying,"Real enemy sight and player pursuit start combat music and suppress exploration music");
        var samples=new float[1024];float energy=0;for(int j=0;j<8;j++){combat.GetOutputData(samples,0);energy=Mathf.Max(energy,Mathf.Sqrt(samples.Sum(s=>s*s)/samples.Length));yield return Wait(.1f);}
        report.AppendLine("Combat source RMS="+energy.ToString("F6"));Check(energy>.00001f,"The imported combat track produces real audio samples");
        var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.PauseMenu,true);float volume=combat.volume;float time=combat.time;yield return Wait(.3f);
        Check(AudioListener.pause&&combat.volume==volume&&Mathf.Abs(combat.time-time)<.1f,"Pause freezes combat audio playback and fades");pause.SetPaused(GamePauseReason.PauseMenu,false);
        Destroy(subject);yield return Wait(1.3f);Check(!audioControl.IsCombatPlaying&&combat.volume==0,"Combat music fades out after the threat and release interval end");
        var city=FindAnyObjectByType<TownMusicZone>();Set(city,"fadeInSeconds",.25f);Set(city,"fadeOutSeconds",.25f);var frame=Get<Transform>(city,"boundsFrame");var center=Get<Vector3>(city,"center");Warp(frame!=null?frame.TransformPoint(center):center);yield return Wait(.6f);
        Check(city.IsInside&&city.MusicSource.volume>.15f&&!combat.isPlaying,"The city retains its original peaceful music");
        player.TakeDamage(1);yield return Wait(.45f);Check(audioControl.IsCombatPlaying&&combat.isPlaying,"Taking real damage also starts the fighting track");
        // Refresh damage until the city's smooth duck envelope has fully closed.
        player.TakeDamage(1);yield return Wait(.45f);player.TakeDamage(1);yield return Wait(.45f);
        Check(city.MusicSource.volume<.001f,"Combat smoothly suppresses the city song instead of stacking two songs");
        yield return Wait(1.8f);Check(!audioControl.IsCombatPlaying&&city.MusicSource.volume>.15f,"Peaceful city music returns after combat");
        var sample=encounters.Single(e=>e.name.StartsWith("01"));Set(sample,"waveDelay",1.3f);sample.enabled=true;Warp(sample.Arena.GetComponent<BoxCollider>().bounds.center);yield return Until(()=>sample.LivingCount==5,4);yield return Wait(.3f);
        foreach(var enemy in sample.GetComponentsInChildren<EnemyAI>()){enemy.enabled=false;enemy.GetComponent<EnemyAttack>().enabled=false;}
        Check(sample.LivingCount==5&&audioControl.IsCombatPlaying,"The actual sample arena starts its five-monster encounter and combat track");
        foreach(var hp in sample.GetComponentsInChildren<EnemyHealth>())hp.TakeDamage(99999);
        yield return Wait(.9f);Check(sample.LivingCount==0&&!sample.Completed&&audioControl.IsCombatPlaying,"Fighting music stays continuous during the encounter's wave delay");
        yield return Until(()=>sample.Completed&&!audioControl.IsCombatPlaying&&combat.volume==0,4);yield return Wait(.3f);
        Check(sample.Completed&&!audioControl.IsCombatPlaying&&demon.volume>.15f,"Clearing the arena returns smoothly to Demon ambience");
        player.TakeDamage(1);yield return Wait(.25f);player.TakeDamage(999999);yield return Wait(.1f);
        Check(!audioControl.IsCombatPlaying&&new[]{demon,combat,forest,village,adventure}.All(s=>s.volume==0&&!s.isPlaying),"Player death stops every world ambience and combat track");
        Check(errors.Count==0,"No runtime errors in area selection, combat priority, streaming, pause, waves or death");
    }
    private void Warp(Vector3 point){controller.enabled=false;player.transform.position=point;controller.enabled=true;Physics.SyncTransforms();}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/DemonCombatAudioPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(subject!=null)Destroy(subject);if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);File.WriteAllText("Logs/DemonCombatAudioPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Demon combat audio test interrupted"));}
}
#endif
