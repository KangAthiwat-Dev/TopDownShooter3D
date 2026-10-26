#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class CitySurfaceRegressionDriver:MonoBehaviour
{
    // Populated by JsonUtility from the editor-generated alignment record.
    [Serializable] private sealed class Plan{public float west=0,east=0,south=0,north=0,top=0,westGateZ=0,northGateX=0;}
    private readonly StringBuilder report=new StringBuilder();private PlayerMovement movement;private PlayerInput input;private CharacterController controller;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? oldDevices;
    private bool passed=true,finished,oldBackground;private int oldFps,oldVsync;private string runtimeError;
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        report.AppendLine("Actual player / aligned city regression "+DateTime.Now.ToString("s"));var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0){object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;}
        Finish(null);
    }
    private IEnumerator Checks()
    {
        Plan p=JsonUtility.FromJson<Plan>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/World/CitySurfaceAlignment/AlignmentPlan.asset").text);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        movement=FindAnyObjectByType<PlayerMovement>();controller=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();keyboard=InputSystem.AddDevice<Keyboard>("CitySurfaceTestKeyboard");
        var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());input.actions.devices=new InputDevice[]{keyboard};yield return Wait(0.3f);
        Warp(new Vector3(p.west-6.5f,p.top+0.08f,p.westGateZ));yield return Wait(0.2f);Keys(Key.D);yield return Wait(2.5f);Keys();yield return Wait(0.25f);
        Check(movement.transform.position.x>p.west+3,"Real player walks through the west gate into the city");Check(Grounded(p.top),"West gate transition lands on the aligned city floor");
        Warp(new Vector3(p.northGateX,p.top+0.08f,p.north+6.5f));yield return Wait(0.2f);Keys(Key.S);yield return Wait(2.5f);Keys();yield return Wait(0.25f);
        Check(movement.transform.position.z<p.north-3,"Real player walks through the north gate into the city");Check(Grounded(p.top),"North gate transition lands on the aligned city floor");
        Warp(new Vector3(p.northGateX-8,p.top+0.08f,p.westGateZ+6));yield return Wait(0.25f);float min=movement.transform.position.y,max=min;Keys(Key.S);float until=Time.realtimeSinceStartup+1.6f;
        while(Time.realtimeSinceStartup<until){yield return null;min=Mathf.Min(min,movement.transform.position.y);max=Mathf.Max(max,movement.transform.position.y);}Keys();yield return Wait(0.2f);
        Check(movement.transform.position.z<p.westGateZ+1.5f,"Real player crosses the stone/wood seam without getting stuck");Check(max-min<0.06f&&Grounded(p.top),"No height step or falling at stone/wood seams (vertical variation "+(max-min).ToString("F3")+" m)");
        Warp(new Vector3(p.northGateX,p.top+0.08f,p.south+2));yield return Wait(0.2f);Keys(Key.S);yield return Wait(0.9f);Keys();yield return Wait(0.15f);
        Check(movement.transform.position.z>p.south+0.5f,"Solid south wall blocks the actual player");
        Warp(new Vector3(p.east-2,p.top+0.08f,p.westGateZ+6));yield return Wait(0.2f);Keys(Key.D);yield return Wait(0.9f);Keys();yield return Wait(0.15f);
        Check(movement.transform.position.x<p.east-0.5f,"Solid east wall blocks the actual player");
        var path=new NavMeshPath();bool navigation=NavMesh.SamplePosition(new Vector3(p.west-6,p.top,p.westGateZ),out NavMeshHit outside,1,NavMesh.AllAreas)&&NavMesh.SamplePosition(new Vector3(p.northGateX,p.top,p.westGateZ),out NavMeshHit inside,1,NavMesh.AllAreas)&&NavMesh.CalculatePath(outside.position,inside.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
        Check(navigation,"Complete enemy navigation path from outside through the city gateway");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();Check(runtimeError==null,"No runtime errors while walking city floors, gates and walls");
    }
    private void Warp(Vector3 p){Keys();controller.enabled=false;movement.transform.position=p;controller.enabled=true;Physics.SyncTransforms();}
    private bool Grounded(float top)=>Mathf.Abs(movement.transform.position.y-top)<0.2f&&Physics.Raycast(movement.transform.position+Vector3.up,Vector3.down,out RaycastHit hit,2,1<<7,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-top)<0.003f;
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    private void Check(bool ok,string text){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);}
    private void OnLog(string condition,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=condition;}
    private void Finish(Exception failure)
    {
        if(finished)return;finished=true;if(failure!=null){passed=false;report.AppendLine("EXCEPTION "+failure);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(input!=null)input.actions.devices=oldDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CitySurfacePlaytest.txt",report.ToString());Debug.Log(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!finished)Finish(new InvalidOperationException("City test interrupted"));}
}
#endif
