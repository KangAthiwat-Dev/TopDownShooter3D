#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class CityNorthwestRegressionDriver:MonoBehaviour
{
    private PlayerMovement movement;private CharacterController cc;private PlayerInput input;private Keyboard keyboard;
    private ReadOnlyArray<InputDevice>? oldDevices;private bool done,passed=true,oldBackground;private int oldFps,oldVsync;private string error;
    private readonly StringBuilder report=new StringBuilder();
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        report.AppendLine("Actual player / northwest quarter regression "+DateTime.Now.ToString("s"));
        var iterator=Checks();
        while(true)
        {
            bool next=false;Exception failure=null;try{next=iterator.MoveNext();}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(!next)break;yield return iterator.Current;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();
        var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard=InputSystem.AddDevice<Keyboard>("NorthwestAlleyTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};yield return Wait(.3f);
        Warp(65,-69.8f);yield return Wait(.2f);Keys(Key.W);yield return Wait(3.15f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.z>-54.6f,"Actual 0.9 m-wide player traverses main alley from road to rear lane");Grounded("Main alley stays level");
        Warp(75.7f,-58.5f);yield return Wait(.2f);Keys(Key.A);yield return Wait(3.25f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.x<59.7f,"Player crosses upper side alley from main road through the quarter");Grounded("Upper cross alley stays level");
        Warp(73.1f,-64.0f);yield return Wait(.2f);Keys(Key.A);yield return Wait(2.8f);Keys();yield return Wait(.2f);
        Check(movement.transform.position.x<60,"Player traverses lower side alley between workshop and bakery");Grounded("Lower cross alley stays level");
        // Closed shells should physically block a player walking toward their fronts.
        Warp(60.3f,-69.2f);yield return Wait(.2f);Keys(Key.W);yield return Wait(1f);Keys();yield return Wait(.15f);
        Check(movement.transform.position.z<-68.0f,"Workshop frontage blocks entry into the closed building");
        Warp(69.4f,-69.2f);yield return Wait(.2f);Keys(Key.W);yield return Wait(1f);Keys();yield return Wait(.15f);
        Check(movement.transform.position.z<-68.0f,"Cobbler frontage blocks entry into the closed building");
        // A last short diagonal move exercises the actual camera/animation/player setup in the new district.
        Warp(65,-64f);yield return Wait(.2f);Keys(Key.W);yield return Wait(.45f);Keys();yield return Wait(.2f);
        Check(error==null,"No runtime errors during alley and building-collision playtest");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private void Warp(float x,float z){Keys();cc.enabled=false;movement.transform.position=new Vector3(x,5.22f,z);cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    private void Grounded(string label)=>Check(Mathf.Abs(movement.transform.position.y-5.14f)<.18f,label);
    private void Check(bool ok,string message){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+message+"; player="+movement.transform.position.ToString("F3"));}
    private void OnLog(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)error=condition;}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(failure!=null){passed=false;report.AppendLine("EXCEPTION "+failure);}if(error!=null){passed=false;report.AppendLine("RUNTIME ERROR "+error);}
        if(input!=null)input.actions.devices=oldDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CityNorthwestPlaytest.txt",report.ToString());
        if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Test interrupted"));}
}
#endif
