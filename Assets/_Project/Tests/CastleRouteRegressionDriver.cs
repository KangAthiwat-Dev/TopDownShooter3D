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

public sealed class CastleRouteRegressionDriver:MonoBehaviour
{
    [Serializable] private sealed class Plan{public Vector3[] bridge,stairs,route,ascent;public float width;}
    private Plan plan;private PlayerMovement movement;private CharacterController cc;private PlayerInput input;private Gamepad pad;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? oldDevices;
    private bool done,passed=true,oldBackground;private int oldFps,oldVsync;private float oldTimeScale;private string runtimeError;private readonly StringBuilder report=new StringBuilder();
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;oldTimeScale=Time.timeScale;Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;report.AppendLine("Actual player / authored castle route "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());while(stack.Count>0){object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;}Finish(null);
    }
    private IEnumerator Checks()
    {
        plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Assets/_Project/World/CastleRouteRepair/RoutePlan.json"));foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());pad=InputSystem.AddDevice<Gamepad>("CastleRouteTestPad");keyboard=InputSystem.AddDevice<Keyboard>("CastleRouteTestKeyboard");input.actions.devices=new InputDevice[]{pad,keyboard};FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown,false);yield return Wait(1.5f);
        if(plan.ascent!=null&&plan.ascent.Length>0){Warp(plan.ascent[0]);yield return Wait(.4f);for(int i=1;i<plan.ascent.Length;i++)yield return Travel(plan.ascent[i],false,false,"walk stairs and existing ridge passage "+i);}
        else{Warp(plan.stairs[0]);yield return Wait(.4f);yield return Travel(plan.stairs[1],false,false,"walk up authored stairs and upper terrain connection");if(Vector2.Distance(XZ(movement.transform.position),XZ(plan.stairs[1]))<.8f)yield return Travel(plan.route[2],false,false,"reach first flat island from stair approach");}
        Warp(plan.route[2]);yield return Wait(.3f);yield return Travel(plan.bridge[0],false,false,"walk from first island to bridge landing");
        for(int i=1;i<plan.bridge.Length;i++)yield return Travel(plan.bridge[i],false,false,"walk forward across bridge segment "+i);
        yield return Wait(.3f);Capture("Logs/CastleRoute_Bridge_Gameplay.png");
        for(int i=plan.bridge.Length-2;i>=0;i--)yield return Travel(plan.bridge[i],false,false,"walk back across bridge segment "+i);
        Warp(plan.bridge[0]);yield return Wait(.3f);yield return Travel(plan.bridge.Last(),true,false,"sprint continuously across full bridge");
        Warp(plan.bridge[0]);yield return Wait(.4f);var direction=plan.bridge[2]-movement.transform.position;Move(direction,Key.Space);yield return Wait(.06f);Check(movement.IsDashing,"normal dash actually starts on the bridge approach");yield return Travel(plan.bridge.Last(),false,false,"normal dash then traverse full bridge");
        Warp(plan.bridge[0]);yield return Wait(.5f);Move(plan.bridge[2]-movement.transform.position,Key.M);yield return Wait(.25f);Check(movement.IsShadowDashing,"shadow dash actually starts on bridge approach");yield return Travel(plan.bridge.Last(),false,true,"shadow dash across full bridge");Move(Vector3.zero);yield return Wait(.5f);
        yield return Travel(plan.route[5],false,false,"reach second island center after bridge");yield return Travel(plan.route[6],false,false,"walk onto third connected platform");
        foreach(var pair in new[]{new[]{plan.stairs[0],plan.stairs[1]},new[]{plan.bridge[0],plan.bridge.Last()},new[]{plan.route[2],plan.route[6]}})
        {var path=new NavMeshPath();Check(NavMesh.CalculatePath(pair[0],pair[1],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"navigation connects "+pair[0].ToString("F1")+" -> "+pair[1].ToString("F1"));}
        Check(Mathf.Abs(cc.stepOffset-.3f)<.001f&&Mathf.Abs(cc.slopeLimit-45)<.001f,"player step and slope settings preserved");Check(runtimeError==null,"no runtime errors through route movement and both dash modes");FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private IEnumerator Travel(Vector3 target,bool sprint,bool shadow,string label)
    {
        var originPosition=movement.transform.position;var travelDirection=(XZ(target)-XZ(originPosition)).normalized;float goalDistance=Vector2.Distance(XZ(target),XZ(originPosition));float began=Time.realtimeSinceStartup;float deadline=began+Vector3.Distance(movement.transform.position,target)/(sprint?5:3)+5;float low=movement.transform.position.y;
        while(Time.realtimeSinceStartup<deadline&&Vector2.Distance(XZ(movement.transform.position),XZ(target))>.25f)
        {Move(target-movement.transform.position,shadow?new[]{Key.M}:sprint?new[]{Key.LeftShift}:Array.Empty<Key>());low=Mathf.Min(low,movement.transform.position.y);yield return null;}
        Move(Vector3.zero);yield return Wait(.15f);var travel=XZ(movement.transform.position-originPosition);float progress=Vector2.Dot(travel,travelDirection);float lateral=Mathf.Abs(travel.x*travelDirection.y-travel.y*travelDirection.x);
        bool arrived=(Vector2.Distance(XZ(movement.transform.position),XZ(target))<.6f||progress>=goalDistance-.25f&&lateral<.55f)&&Mathf.Abs(movement.transform.position.y-target.y)<.8f;
        Check(arrived,label+" actual="+movement.transform.position.ToString("F3")+" expected="+target.ToString("F3")+" minY="+low.ToString("F2"));
        if(!arrived)
        {
            var origin=movement.transform.position+Vector3.up*.65f;var delta=target-origin;delta.y=0;if(Physics.SphereCast(origin,.4f,delta.normalized,out var hit,2,~0,QueryTriggerInteraction.Ignore))report.AppendLine("  BLOCKED BY "+hit.transform.name+" collider="+hit.collider.GetType().Name+" hit="+hit.point.ToString("F3"));
            File.WriteAllText("Logs/CastleRoutePlaytest.txt",report.ToString());
        }
    }
    private void Warp(Vector3 p){Move(Vector3.zero);cc.enabled=false;movement.transform.position=p+Vector3.up*.06f;cc.enabled=true;Physics.SyncTransforms();}
    private static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    private void Move(Vector3 direction,params Key[] keys)
    {direction.y=0;if(pad!=null)InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(direction.x,direction.z).normalized});if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float duration){float until=Time.realtimeSinceStartup+duration;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Capture(string path)
    {
        var camera=FindAnyObjectByType<CameraFollow>().FollowCamera;var rt=RenderTexture.GetTemporary(1400,1000,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
    }
    private void Check(bool ok,string label){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/CastleRoutePlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(failure!=null){passed=false;report.AppendLine("EXCEPTION "+failure);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}Move(Vector3.zero);if(input!=null)input.actions.devices=oldDevices;if(pad!=null)InputSystem.RemoveDevice(pad);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;Time.timeScale=oldTimeScale;report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/CastleRoutePlaytest.txt",report.ToString());Debug.Log("Actual-player castle route test finished. Passed="+passed);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Test interrupted"));}
}
#endif
