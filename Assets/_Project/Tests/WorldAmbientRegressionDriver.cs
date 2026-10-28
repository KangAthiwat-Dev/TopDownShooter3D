#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class WorldAmbientRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private WorldAmbientAudio audioControl;private PlayerMovement player;private Keyboard keyboard;private GameObject floor;private bool done;private string runtimeError;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private IEnumerator Start()
    {
        Application.logMessageReceived+=Log;var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;audioControl=FindAnyObjectByType<WorldAmbientAudio>();player=FindAnyObjectByType<PlayerMovement>();Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);foreach(var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        var input=player.GetComponent<PlayerInput>();keyboard=InputSystem.AddDevice<Keyboard>("WorldAmbientTestKeyboard");input.neverAutoSwitchControlSchemes=true;input.actions.devices=new InputDevice[]{keyboard};player.ConfigureCameraRelativeMovement(false,null);
        Set(audioControl,"fadeSeconds",.1f);Set(audioControl,"firstWait",new Vector2(.1f,.1f));Set(audioControl,"repeatWait",new Vector2(1,1));Set(audioControl,"songDuration",new Vector2(.45f,.45f));Set(audioControl,"walkingBeforeMusic",.25f);Set(audioControl,"quietBeforeMusic",.5f);
        var forest=Get<AudioSource>(audioControl,"forestSource");var village=Get<AudioSource>(audioControl,"villageSource");var song=Get<AudioSource>(audioControl,"adventureSource");
        Check(forest.clip!=null&&village.clip!=null&&song.clip!=null&&forest.clip.loadType==AudioClipLoadType.Streaming&&song.clip.length>100,"Forest, village and converted adventure clips load as streaming audio");
        floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=7;floor.transform.position=new Vector3(2000,19.9f,2000);floor.transform.localScale=new Vector3(40,.2f,40);
        Warp(new Vector3(2000,20.05f,2000));yield return Wait(.35f);
        Check(audioControl.CurrentArea==WorldAudioArea.Forest&&forest.isPlaying&&forest.volume>.1f&&!village.isPlaying,"Outside named zones uses forest ambience");
        int starts=0;audioControl.AdventureStarted+=()=>starts++;yield return Wait(.6f);Check(starts==0,"Idle alone does not start adventure music");
        Keys(Key.W);yield return Until(()=>starts>0,2);Keys();
        Check(starts==1&&audioControl.IsAdventurePlaying&&!song.loop,"Actual quiet walking starts one non-looping adventure cue");
        yield return Wait(.65f);Check(!audioControl.IsAdventurePlaying&&starts==1,"Cue ends and enters a silent interval instead of looping continuously");
        yield return Wait(.8f);Check(starts==1,"Standing still does not repeatedly restart music");Keys(Key.W);yield return Until(()=>starts==2,2);Keys();
        Check(starts==2,"Further exploration can start a later cue after cooldown");
        player.GetComponent<PlayerHealth>().TakeDamage(1);yield return Wait(.3f);Check(!audioControl.IsAdventurePlaying,"Taking combat damage interrupts the exploration cue");
        var villageZone=Get<WorldMonsterSafeZone>(audioControl,"village");Warp(villageZone!=null?villageZone.transform.TransformPoint(villageZone.center):FindAnyObjectByType<ForestMapLayout>().village.position);yield return Wait(.35f);
        Check(audioControl.CurrentArea==WorldAudioArea.Village&&village.isPlaying&&village.volume>.1f&&forest.volume==0&&!audioControl.IsAdventurePlaying,"Village switches to its own ambience and suppresses adventure music");
        var city=FindAnyObjectByType<TownMusicZone>();var cityClip=city.MusicSource.clip;var frame=Get<Transform>(city,"boundsFrame");var center=Get<Vector3>(city,"center");Warp(frame!=null?frame.TransformPoint(center):center);yield return Wait(.4f);
        Check(audioControl.CurrentArea==WorldAudioArea.OtherZone&&forest.volume==0&&village.volume==0&&city.IsInside&&city.MusicSource.isPlaying&&city.MusicSource.clip==cityClip,"City retains its existing track without overlapping forest or village beds");
        Warp(FindAnyObjectByType<PlayerSceneSpawnPoint>().Position);yield return Wait(.35f);Check(audioControl.CurrentArea==WorldAudioArea.OtherZone&&forest.volume==0,"Summoning zone is excluded from fallback forest ambience");
        Warp(new Vector3(2000,20.05f,2000));yield return Wait(.2f);var pause=FindAnyObjectByType<GamePauseController>();float before=forest.volume;pause.SetPaused(GamePauseReason.Journal,true);yield return Wait(.25f);
        Check(AudioListener.pause&&forest.volume==before,"Menu pause freezes the audio envelopes and scheduling");pause.SetPaused(GamePauseReason.Journal,false);yield return Wait(.15f);Check(forest.isPlaying,"Ambient loop resumes after menu close");
        player.GetComponent<PlayerHealth>().TakeDamage(999999);yield return Wait(.1f);Check(forest.volume==0&&village.volume==0&&song.volume==0,"Death stops the added background audio");
        Check(runtimeError==null,"No runtime errors during zone fades, scheduling, pause or death");
    }
    private void Warp(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool okay,string label){report.AppendLine((okay?"PASS ":"FAIL ")+label);if(!okay)throw new InvalidOperationException(label);}
    private void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message+"\n"+trace;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;Application.logMessageReceived-=Log;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(floor!=null)Destroy(floor);
        if(failure!=null)report.AppendLine(failure.ToString());if(runtimeError!=null)report.AppendLine(runtimeError);report.AppendLine(failure==null&&runtimeError==null?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/WorldAmbientPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("World audio test interrupted"));}
}
#endif
