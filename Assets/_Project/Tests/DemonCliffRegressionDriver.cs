#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class DemonCliffRegressionDriver:MonoBehaviour
{
    private PlayerMovement movement;private CharacterController cc;private PlayerInput input;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? oldDevices;
    private DemonCliffZone zone;private Camera view;private GameObject bear;private bool done,passed=true,oldBackground;private int oldFps,oldVsync;private string runtimeError;private readonly StringBuilder report=new StringBuilder();
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        report.AppendLine("Actual player / three-level lava approach "+DateTime.Now.ToString("s"));var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0){object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception ex){failure=ex;}if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;}
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();zone=FindAnyObjectByType<DemonCliffZone>();view=FindAnyObjectByType<CameraFollow>().FollowCamera;
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown,false);
        var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());keyboard=InputSystem.AddDevice<Keyboard>("DemonApproachTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};yield return Wait(.6f);
        Warp(zone.entrance.position);yield return Wait(.4f);
        foreach(var node in zone.travelRoute.Skip(1))
        {
            var nav=new NavMeshPath();bool route=NavMesh.CalculatePath(movement.transform.position,node.position,NavMesh.AllAreas,nav);if(!route||nav.status!=NavMeshPathStatus.PathComplete){Check(false,"actual player route unavailable "+node.name);continue;}
            foreach(var corner in nav.corners.Skip(1))yield return WalkTo(corner);
            Check(Vector2.Distance(XZ(movement.transform.position),XZ(node.position))<1.2f&&Mathf.Abs(movement.transform.position.y-node.position.y)<.65f,"player traversed "+node.name+" at "+movement.transform.position.ToString("F2"));
            if(node==zone.travelRoute[2]||node==zone.travelRoute[19]||node==zone.travelRoute[23]){yield return Wait(.7f);Capture("Logs/DemonCliffs_Stage"+(node==zone.travelRoute[2]?1:node==zone.travelRoute[19]?2:3)+"_Gameplay.png");}
        }
        Keys();yield return Wait(.5f);var ambiance=zone.GetComponentsInChildren<AudioSource>().First(a=>a.name=="Lava_Rumble_Local3D");Check(ambiance.isPlaying&&ambiance.volume>0&&ambiance.volume<=.081f,"local lava ambience fades up at capped volume");
        var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.Inventory,true);var sample=ambiance.timeSamples;var position=movement.transform.position;yield return Wait(.35f);Check(Vector3.Distance(position,movement.transform.position)<.001f&&Mathf.Abs(ambiance.timeSamples-sample)<2048,"inventory pause freezes player and lava audio");pause.SetPaused(GamePauseReason.Inventory,false);
        Warp(new Vector3(84,10.05f,34.6f));yield return Wait(.3f);Keys(Key.W,Key.Space);yield return Wait(.09f);Check(movement.IsDashing,"normal dash starts on lower-right ascent");Keys(Key.W,Key.LeftShift);yield return Wait(1.6f);Keys();yield return Wait(.25f);Check(movement.transform.position.z>47&&Mathf.Abs(movement.transform.position.y-14.8f)<.3f,"sprint and dash ascend onto second stage without snagging");
        Warp(new Vector3(60,15.4f,62));yield return Wait(.35f);Keys(Key.W,Key.M);yield return Wait(.4f);Check(movement.IsShadowDashing,"shadow dash starts on upper ascent");yield return Wait(.9f);Keys();yield return Wait(.5f);Check(movement.transform.position.z>73&&movement.transform.position.y>19.9f,"shadow dash climbs onto mini-boss forecourt and lands supported");
        foreach(bool shadow in new[]{false,true})
        {
            Warp(new Vector3(31,9.85f,59));yield return Wait(.5f);Keys(Key.W,shadow?Key.M:Key.Space);yield return Wait(1.2f);Keys();yield return Wait(.3f);Check(movement.transform.position.z<63&&movement.transform.position.y>9.5f,(shadow?"shadow":"normal")+" dash is contained by cliff-edge collision");
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Bear.prefab");bear=Instantiate(prefab,zone.stages[0].center.position,Quaternion.identity);bear.GetComponent<EnemyAI>().enabled=false;bear.GetComponent<EnemyAttack>().enabled=false;bear.GetComponent<EnemyVoice>().enabled=false;
        var agent=bear.GetComponent<NavMeshAgent>();agent.isStopped=false;agent.speed=8;agent.acceleration=24;agent.stoppingDistance=.25f;agent.SetDestination(zone.stages[2].center.position);float end=Time.realtimeSinceStartup+35;float maxHeight=0;
        while(Time.realtimeSinceStartup<end&&(agent.pathPending||agent.remainingDistance>.5f)){maxHeight=Mathf.Max(maxHeight,bear.transform.position.y);yield return null;}
        Check(agent.isOnNavMesh&&Vector3.Distance(bear.transform.position,zone.stages[2].center.position)<1&&maxHeight>19.8f,"actual Bear agent traverses bridge and both upper ramps to forecourt");Destroy(bear);bear=null;
        Check(runtimeError==null,"no runtime errors through movement, dash, local VFX/audio, pause and creature navigation");FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private IEnumerator WalkTo(Vector3 target)
    {
        float until=Time.realtimeSinceStartup+Mathf.Max(5,Vector3.Distance(movement.transform.position,target)/3+3);
        while(Time.realtimeSinceStartup<until)
        {
            var delta=target-movement.transform.position;if(XZ(delta).magnitude<.5f)break;var keys=new List<Key>();if(Mathf.Abs(delta.x)>Mathf.Abs(delta.z)*.42f)keys.Add(delta.x>0?Key.D:Key.A);if(Mathf.Abs(delta.z)>Mathf.Abs(delta.x)*.42f)keys.Add(delta.z>0?Key.W:Key.S);Keys(keys.ToArray());yield return null;
        }
        Keys();yield return Wait(.12f);
    }
    private static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    private void Warp(Vector3 p){Keys();cc.enabled=false;movement.transform.position=p+Vector3.up*.04f;cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Capture(string path)
    {
        var rt=RenderTexture.GetTemporary(1500,1000,24);var old=view.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1500,1000,TextureFormat.RGB24,false);
        try{view.targetTexture=rt;view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{view.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
    }
    private void Check(bool ok,string text){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);File.WriteAllText("Logs/DemonCliffsPlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(failure!=null){passed=false;report.AppendLine("EXCEPTION "+failure);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}Keys();if(input!=null)input.actions.devices=oldDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(bear!=null)Destroy(bear);
        var pause=FindAnyObjectByType<GamePauseController>();if(pause!=null)pause.SetPaused(GamePauseReason.Inventory,false);Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/DemonCliffsPlaytest.txt",report.ToString());Debug.Log("Demon cliff playtest complete. Passed="+passed);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Test interrupted"));}
}
#endif
