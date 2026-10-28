#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class PlayerFootstepRegressionDriver : MonoBehaviour
{
    private PlayerMovement player;private PlayerFootstepAudio footsteps;private Keyboard keyboard;private GameObject fixture;private TerrainData terrainData;
    private AudioSource outside,inside;private readonly StringBuilder report=new StringBuilder();private string runtimeError;private bool done;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    private static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private IEnumerator Start()
    {
        Application.logMessageReceived+=Log;var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;player=FindAnyObjectByType<PlayerMovement>();footsteps=player.GetComponent<PlayerFootstepAudio>();
        Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);
        foreach(var spawn in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawn.StopAllCoroutines();spawn.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        outside=Get<AudioSource>(footsteps,"outside");inside=Get<AudioSource>(footsteps,"inside");
        Check(outside.clip.name=="walk_sound_outside"&&inside.clip.name=="walk_sound_inside"&&outside.loop&&inside.loop,"Both requested recordings are connected as walking loops");
        var input=player.GetComponent<PlayerInput>();keyboard=InputSystem.AddDevice<Keyboard>("FootstepTestKeyboard");input.neverAutoSwitchControlSchemes=true;input.actions.devices=new InputDevice[]{keyboard};player.ConfigureCameraRelativeMovement(false,null);
        fixture=new GameObject("Temporary footstep surfaces");terrainData=new TerrainData{heightmapResolution=33,size=new Vector3(24,2,24)};
        var terrain=Terrain.CreateTerrainGameObject(terrainData);terrain.name="Test Terrain";terrain.layer=7;terrain.transform.SetParent(fixture.transform);terrain.transform.position=new Vector3(2000,20,2000);
        var platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="City floor above Terrain";platform.layer=7;platform.transform.SetParent(fixture.transform);platform.transform.position=new Vector3(2016,20.15f,2012);platform.transform.localScale=new Vector3(8,.3f,20);
        Warp(new Vector3(2005,20.05f,2006));yield return Wait(.3f);Check(!footsteps.IsAudible,"Idle is silent");
        Keys(Key.W);yield return Wait(.65f);
        Check(footsteps.IsOutside&&outside.isPlaying&&outside.volume>.1f&&!inside.isPlaying,"Actual walking on Terrain uses outside sound only");
        Keys();yield return Wait(.25f);Check(!footsteps.IsAudible,"Releasing movement fades and stops the recording");
        Warp(new Vector3(2016,20.35f,2006));yield return Wait(.2f);Keys(Key.W);yield return Wait(.6f);
        Check(!footsteps.IsOutside&&footsteps.SupportingCollider==platform.GetComponent<Collider>()&&inside.isPlaying&&!outside.isPlaying,"Object floor above Terrain wins the ground probe and uses inside sound");
        float normalPitch=inside.pitch;Keys(Key.W,Key.LeftShift);yield return Wait(.4f);Check(inside.pitch>normalPitch,"Sprinting increases footstep cadence");Keys();yield return Wait(.2f);
        Keys(Key.Space);yield return Wait(.07f);Check(!footsteps.IsAudible,"Dash is silent");Keys();yield return Wait(.3f);
        Keys(Key.W);yield return Wait(.3f);var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.Journal,true);yield return Wait(.1f);
        Check(outside.volume==0&&inside.volume==0&&!footsteps.IsAudible,"Opening a menu immediately stops both walking sounds");Keys();pause.SetPaused(GamePauseReason.Journal,false);yield return Wait(.15f);
        Warp(new Vector3(2016,24,2006));Keys(Key.W);yield return Wait(.1f);Check(!footsteps.IsAudible,"Airborne movement does not play footsteps");Keys();
        Check(runtimeError==null,"No runtime errors during surface changes, sprint, dash, pause or falling");
    }
    private void Warp(Vector3 p){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message+"\n"+trace;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;Application.logMessageReceived-=Log;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(fixture!=null)Destroy(fixture);if(terrainData!=null)Destroy(terrainData);
        if(failure!=null)report.AppendLine(failure.ToString());if(runtimeError!=null)report.AppendLine(runtimeError);
        report.AppendLine(failure==null&&runtimeError==null?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/PlayerFootstepPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Footstep test interrupted"));}
}
#endif
