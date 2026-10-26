#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class CityExpansionRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private PlayerMovement movement;private PlayerInput input;private CharacterController cc;private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? oldDevices;private bool done,passed=true,oldBackground;private int oldFps,oldVsync;private string runtimeError;
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        report.AppendLine("Actual player / grounded city expansion "+DateTime.Now.ToString("s"));var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception error=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){error=e;}
            if(error!=null){Finish(error);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());keyboard=InputSystem.AddDevice<Keyboard>("CityExpansionTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};yield return Wait(.3f);
        Warp(48.5f,-70.8856f);yield return Wait(.2f);Keys(Key.D);yield return Wait(2.8f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.x>61,"Actual player enters city through grounded west approach and gate");Ground("West gate has no falling or height step");
        Warp(75.7626f,-43.8f);yield return Wait(.2f);Keys(Key.S);yield return Wait(2.8f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.z<-56,"Actual player enters grounded north approach and gate");Ground("North gate has no falling or height step");
        Warp(75.7626f,-76f);yield return Wait(.2f);Keys(Key.S);yield return Wait(2.15f);Keys();yield return Wait(.15f);Keys(Key.A);yield return Wait(1.4f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.x<70&&movement.transform.position.z<-83,"Player turns through rounded southwest road corner");Ground("Rounded road corner stays flat");
        Warp(86,-82.8856f);yield return Wait(.2f);Keys(Key.S);yield return Wait(.65f);Keys();yield return Wait(.15f);
        Check(movement.transform.position.z<-85.5f,"Player steps across low visual kerb into armory forecourt");Ground("Armory forecourt is supported and level");
        Warp(86.4f,-86.6f);yield return Wait(.2f);Keys(Key.S);yield return Wait(.8f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.z>-89.4f,"Closed armory collision blocks entry through solid storefront");
        Warp(72.4f,-74.8f);yield return Wait(.2f);Keys(Key.S);yield return Wait(1.4f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.z<-81,"Player traverses clear construction-side access lane");Ground("Construction access lane has continuous ground");
        Warp(65,-73.3f);yield return Wait(.2f);Keys(Key.S);yield return Wait(.8f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.z>-75,"Construction footprint safely blocks walking through scaffolding");
        var nav=new NavMeshPath();bool route=NavMesh.SamplePosition(new Vector3(71.8f,5.14f,-79),out var a,.8f,NavMesh.AllAreas)&&NavMesh.SamplePosition(new Vector3(86.4f,5.14f,-86.8f),out var b,.8f,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,nav)&&nav.status==NavMeshPathStatus.PathComplete;
        Check(route,"Complete enemy navigation from construction access to armory forecourt");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();Check(runtimeError==null,"No runtime errors during city expansion playtest");
    }
    private void Warp(float x,float z){Keys();cc.enabled=false;movement.transform.position=new Vector3(x,5.22f,z);cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    private void Ground(string text)=>Check(Mathf.Abs(movement.transform.position.y-5.14f)<.18f,text);
    private void Check(bool ok,string text){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text+"; player="+movement.transform.position.ToString("F3"));}
    private void OnLog(string condition,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)runtimeError=condition;}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(input!=null)input.actions.devices=oldDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CityExpansionPlaytest.txt",report.ToString());if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Playtest interrupted"));}
}
#endif
