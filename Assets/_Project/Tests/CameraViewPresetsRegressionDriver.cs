#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class CameraViewPresetsRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private CameraViewSettings settings;
    private CameraFollow rig;
    private Camera view;
    private PlayerMovement movement;
    private PlayerInput input;
    private CharacterController cc;
    private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? previousDevices;
    private GameObject floor;
    private GamePauseController pause;
    private bool passed=true,done,previousBackground;
    private int previousFps,previousVsync;
    private float previousTimeScale;
    private string runtimeError;
    private IEnumerator Start()
    {
        previousBackground=Application.runInBackground;previousFps=Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;previousTimeScale=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        Application.logMessageReceived+=OnLog;report.AppendLine("Actual-player camera defaults / switching "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception error=null;
            try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){error=e;}
            if(error!=null){Finish(error);yield break;}
            if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        settings=FindFirstObjectByType<CameraViewSettings>();rig=settings.GetComponent<CameraFollow>();view=rig.FollowCamera;
        movement=FindFirstObjectByType<PlayerMovement>();input=movement.GetComponent<PlayerInput>();cc=movement.GetComponent<CharacterController>();
        var devices=input.actions.devices;if(devices.HasValue)previousDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard=InputSystem.AddDevice<Keyboard>("CameraPresetTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};
        yield return Wait(.3f);
        Check(settings.CurrentView==CameraViewMode.TopDown&&!PlayerPrefs.HasKey(CameraViewSettings.PreferenceKey),"Fresh startup actually selects top-down without writing a preference");
        Check(!rig.FollowsMovementHeading&&!movement.UsesCameraRelativeMovement&&!rig.GetComponent<CameraBoomCollision>().enabled,"Top-down uses fixed yaw, world-axis controls, and no third-person boom zoom");
        Check(Vector3.Distance(view.transform.localPosition,new Vector3(0,12,-10))<.01f&&Mathf.Abs(view.fieldOfView-45f)<.01f,"Original top-down framing and FOV loaded before gameplay");
        floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary camera preset arena";floor.layer=7;floor.transform.position=new Vector3(1000,9.4f,1000);floor.transform.localScale=new Vector3(140,1,140);
        cc.enabled=false;movement.transform.position=new Vector3(1000,10,1000);cc.enabled=true;Physics.SyncTransforms();yield return Wait(.8f);
        Quaternion fixedRotation=rig.transform.rotation;
        foreach(var pair in new[]{(Key.W,Vector3.forward),(Key.D,Vector3.right),(Key.S,Vector3.back),(Key.A,Vector3.left)})
        {
            Keys();yield return Wait(.35f);var start=movement.transform.position;Keys(pair.Item1);yield return Wait(.7f);
            var travel=Vector3.ProjectOnPlane(movement.transform.position-start,Vector3.up);
            Check(travel.magnitude>2f&&Vector3.Dot(travel.normalized,pair.Item2)>.995f&&Quaternion.Angle(fixedRotation,rig.transform.rotation)<.01f,pair.Item1+" moves on original world axes without rotating the camera");
        }
        Keys();yield return Wait(.5f);Keys(Key.W,Key.LeftShift);yield return Wait(.6f);
        Check(movement.IsRunning&&Vector3.Distance(view.transform.localPosition,settings.TopDownPreset.localPosition)>.001f,"Top-down sprint retains subtle bob");
        Keys();yield return Wait(.8f);
        Check(Vector3.Distance(view.transform.localPosition,settings.TopDownPreset.localPosition)<.01f,"Top-down bob returns to its own rest pose");
        Keys(Key.W,Key.Space);yield return Wait(.08f);
        Check(movement.IsDashing&&view.fieldOfView>45.5f,"Top-down dash adds FOV punch to the 45 degree baseline");
        Keys();settings.ApplyView(CameraViewMode.HighThirdPerson,false);
        Check(!PlayerPrefs.HasKey(CameraViewSettings.PreferenceKey)&&settings.CurrentView==CameraViewMode.HighThirdPerson,"Preview switches views without changing saved preference");
        Check(rig.FollowsMovementHeading&&movement.UsesCameraRelativeMovement&&rig.GetComponent<CameraBoomCollision>().enabled,"Third-person orbit, movement reference and collision are restored together");
        yield return Wait(1f);
        Check(Vector3.Distance(view.transform.localPosition,new Vector3(0,6,-8))<.01f&&Mathf.Abs(view.fieldOfView-55f)<.01f,"Switching during dash settles at third-person pose / 55 degree FOV without old-view drift");
        var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);var position=movement.transform.position;
        Keys(Key.D);yield return Wait(3.5f);
        var displacement=Vector3.ProjectOnPlane(movement.transform.position-position,Vector3.up);
        Check(displacement.magnitude>10f&&Vector3.Dot(displacement.normalized,right)>.995f,"Third-person held D stays straight in the captured view direction");
        Check(Vector3.Dot(Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized,right)>.99f,"Third-person camera still settles behind travel");
        Keys();yield return Wait(.4f);Keys(Key.W);yield return Wait(.7f);
        // W now travels along the third-person camera's new heading. Keep it held
        // across the switch to verify top-down discards that captured world direction.
        settings.ApplyView(CameraViewMode.TopDown,false);position=movement.transform.position;yield return Wait(.8f);
        displacement=Vector3.ProjectOnPlane(movement.transform.position-position,Vector3.up);
        Check(Vector3.Dot(Vector3.ProjectOnPlane(cc.velocity,Vector3.up).normalized,Vector3.forward)>.99f&&displacement.z>2f&&Quaternion.Angle(rig.transform.rotation,Quaternion.Euler(settings.TopDownPreset.rigEulerAngles))<.01f,"Switching while a key is held restores top-down controls and clears previous orbit yaw");
        Keys();yield return Wait(.8f);
        Check(Vector3.Distance(view.transform.localPosition,new Vector3(0,12,-10))<.01f&&Mathf.Abs(view.fieldOfView-45f)<.01f,"Returning to top-down restores original framing and FOV baseline");
        pause=FindFirstObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.Inventory,true);var playerPosition=movement.transform.position;
        settings.ApplyView(CameraViewMode.HighThirdPerson,false);yield return Wait(.2f);
        Check(Time.timeScale==0&&Vector3.Distance(playerPosition,movement.transform.position)<.001f&&Mathf.Abs(view.fieldOfView-55f)<.01f,"Settings can switch while paused without resuming gameplay or moving the player");
        settings.ApplyView(CameraViewMode.TopDown,false);pause.SetPaused(GamePauseReason.Inventory,false);
        settings.SelectView(1);Check(settings.CurrentView==CameraViewMode.HighThirdPerson&&settings.ReadPreferredView()==CameraViewMode.HighThirdPerson,"Settings dropdown API saves and reads third-person selection");
        settings.SelectView(99);Check(settings.CurrentView==CameraViewMode.HighThirdPerson,"Invalid dropdown index leaves the current view unchanged");
        settings.ResetToDefault();Check(settings.CurrentView==CameraViewMode.TopDown&&!PlayerPrefs.HasKey(CameraViewSettings.PreferenceKey),"Reset removes only the camera preference and restores top-down");
        var fade=rig.GetComponent<CameraOcclusionFade>();Check(fade==null||(!fade.enabled&&fade.ActiveFadeCount==0),"Neither view enables scenery transparency");
        FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();yield return Wait(1.2f);Capture("Logs/TopDownDefault_Spawn.png");
        cc.enabled=false;movement.transform.position=new Vector3(75.76f,5.22f,-75.8f);cc.enabled=true;yield return Wait(1.2f);Capture("Logs/TopDownDefault_City.png");
        Check(runtimeError==null,"No runtime errors through default view, controls, effects, switching, pause and preference APIs");
    }
    private void Capture(string path)
    {
        var rt=RenderTexture.GetTemporary(1500,1000,24);var oldTarget=view.targetTexture;var oldActive=RenderTexture.active;var image=new Texture2D(1500,1000,TextureFormat.RGB24,false);
        try{view.targetTexture=rt;view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,1000),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{view.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
    }
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    private void Check(bool value,string label){passed&=value;report.AppendLine((value?"PASS ":"FAIL ")+label);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CameraViewPresetsPlaytest.txt",report.ToString());}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        Keys();if(pause!=null)pause.SetPaused(GamePauseReason.Inventory,false);Time.timeScale=previousTimeScale;
        if(input!=null)input.actions.devices=previousDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        if(settings!=null)settings.ApplyView(CameraViewMode.TopDown,false);
        Application.logMessageReceived-=OnLog;Application.runInBackground=previousBackground;Application.targetFrameRate=previousFps;QualitySettings.vSyncCount=previousVsync;
        if(floor!=null)Destroy(floor);report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CameraViewPresetsPlaytest.txt",report.ToString());
        if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Camera preset test interrupted"));}
}
#endif
