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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class VillageFarmRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private PlayerMovement movement;private CharacterController cc;private PlayerInput input;
    private ReadOnlyArray<InputDevice>? oldDevices;private Gamepad pad;private Keyboard keyboard;
    private bool passed=true,finished,oldBackground;private int oldFps,oldVsync;private float oldScale;private string runtimeError;
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;oldScale=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;
        Application.logMessageReceived+=OnLog;report.AppendLine("Yellow village player and population test "+DateTime.Now.ToString("s"));
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
        var root=GameObject.Find("Village_YellowRoof_Farm");var population=root.GetComponentInChildren<TownNpcPopulation>();
        var city=FindObjectsByType<TownNpcPopulation>().Single(p=>p!=population);
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();input=movement.GetComponent<PlayerInput>();
        if(input.actions.devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        pad=InputSystem.AddDevice<Gamepad>("VillageTestPad");keyboard=InputSystem.AddDevice<Keyboard>("VillageTestKeyboard");input.actions.devices=new InputDevice[]{pad,keyboard};
        FindAnyObjectByType<CameraViewSettings>().ApplyView(CameraViewMode.TopDown,false);
        yield return Until(()=>population.AliveCount==10&&city.AliveCount==12,8);
        Check(population.AliveCount==10&&city.AliveCount==12,"Village fills ten residents while city retains twelve");
        var people=root.GetComponentsInChildren<TownNpcBrain>();
        Check(people.Count(p=>p.Gender==TownNpcGender.Female)==5&&people.Count(p=>p.Gender==TownNpcGender.Male)==5,"Five women and five men spawn in the village");
        Check(people.All(p=>p.Agent.isOnNavMesh&&population.Contains(p.transform.position)),"All villagers start on grounded navigation within their own boundary");
        Check(people.All(p=>p.GetComponent<Targetable>()==null&&p.GetComponent<EnemyHealth>()==null),"Villagers remain neutral civilians");
        Check(people.SelectMany(p=>p.GetComponentsInChildren<SkinnedMeshRenderer>()).SelectMany(r=>r.sharedMaterials).Where(m=>m.name.Contains("Cloth")).All(m=>m.name.StartsWith("Villager_Cloth_")),"Every villager uses a separate muted rural cloth material");
        var start=people.ToDictionary(p=>p,p=>p.transform.position);yield return Wait(5);
        Check(people.Count(p=>Vector3.Distance(start[p],p.transform.position)>.7f)>=5,"Residents walk around the village using their routine animations");
        Check(people.All(p=>population.Contains(p.transform.position)),"Residents stay on village side of the stream");
        Check(people.All(p=>!(p.transform.position.x>-82.75f&&p.transform.position.x<-68.25f&&p.transform.position.z>53.75f&&p.transform.position.z<64.75f)),"Villager routes avoid the planted vegetable beds");
        WarpPlayer(new Vector3(-59,4,37.5f));yield return Wait(.3f);
        // Keep the player's stop beside the resident activity marker, clear of waiting villagers.
        yield return Travel(new Vector3(-63.5f,4,50),"Player walks in from the front of the village");
        yield return Travel(new Vector3(-62,4,57),"Player reaches well plaza");
        Capture("YellowVillage_Gameplay",new Vector3(-60,6,57),new Vector3(13,13,-18),12);
        yield return Travel(new Vector3(-61,4,66.9f),"Player walks past gardens without hitting a cottage");
        yield return Travel(new Vector3(-78,4,67.5f),"Player walks the lane between vegetable garden and animal enclosure");
        Check(Mathf.Abs(movement.transform.position.y-4)<.25f,"Player remains on existing terrain instead of floating");
        var a=people[0];var b=people[1];
        void Idle(TownNpcBrain p){typeof(TownNpcBrain).GetMethod("Idle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,null);typeof(TownNpcBrain).GetField("socialCooldown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,0f);}
        Idle(a);Idle(b);a.Agent.Warp(new Vector3(-62,4.08f,56));b.Agent.Warp(new Vector3(-64,4.08f,56));
        Check(a.TryTalkWith(b),"Village neighbours can start a conversation");yield return Wait(.5f);
        Check(a.State==TownNpcState.Talking&&b.State==TownNpcState.Talking&&a.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Talking"),"Both villagers play Idle_Talking_Loop");
        Capture("YellowVillage_Villagers",new Vector3(-63,5.15f,56),new Vector3(4,2.6f,-5),3.1f);
        var activity=population.CityPoints.First(p=>p.Activity==TownNpcActivityKind.Interact);
        a.Agent.Warp(activity.transform.position+Vector3.back*1.1f);Idle(a);Check(a.TryVisit(activity),"Villager can visit farm interaction point");
        yield return Until(()=>a.State==TownNpcState.Interacting,5);yield return Wait(.2f);
        Check(a.State==TownNpcState.Interacting&&a.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Interact"),"Actual Interact animation plays at farm activity");
        int spawned=population.SpawnedTotal;var health=b.GetComponent<CivilianHealth>();health.TakeDamage(health.CurrentHealth+1);
        Check(population.AliveCount==9,"A villager death opens exactly one local population slot");
        yield return Until(()=>population.AliveCount==10,17);
        Check(population.AliveCount==10&&population.SpawnedTotal==spawned+1&&city.AliveCount==12,"One replacement respawns without changing city population");
        Check(population.AwayCount==0,"Village routine does not send residents beyond its boundary");
        Check(runtimeError==null,"No runtime errors during village traversal or NPC routines");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> done,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Travel(Vector3 target,string label)
    {
        float until=Time.realtimeSinceStartup+Vector3.Distance(movement.transform.position,target)/3f+5;
        while(Time.realtimeSinceStartup<until&&Vector3.ProjectOnPlane(target-movement.transform.position,Vector3.up).magnitude>.25f){Move(target-movement.transform.position);yield return null;}
        Move(Vector3.zero);yield return Wait(.15f);Check(Vector3.ProjectOnPlane(target-movement.transform.position,Vector3.up).magnitude<.7f,label+" actual="+movement.transform.position.ToString("F2"));
    }
    private void Move(Vector3 dir)
    {if(pad!=null)InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(dir.x,dir.z).normalized});if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
    private void WarpPlayer(Vector3 p){cc.enabled=false;movement.transform.position=p+Vector3.up*.08f;cc.enabled=true;Physics.SyncTransforms();}
    private void Capture(string name,Vector3 target,Vector3 offset,float size)
    {
        var go=new GameObject("Temporary village test camera");var camera=go.AddComponent<Camera>();camera.transform.position=target+offset;camera.transform.LookAt(target);camera.orthographic=true;camera.orthographicSize=size;camera.nearClipPlane=.1f;camera.farClipPlane=250;
        var rt=RenderTexture.GetTemporary(1400,1050,24);var previous=RenderTexture.active;var image=new Texture2D(1400,1050,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1050),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(image);Destroy(go);}
    }
    private void Check(bool ok,string label){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/VillagePlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message+"\n"+trace;}
    private void Finish(Exception error)
    {
        if(finished)return;finished=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        Move(Vector3.zero);if(input!=null)input.actions.devices=oldDevices;if(pad!=null)InputSystem.RemoveDevice(pad);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;Time.timeScale=oldScale;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/VillagePlaytest.txt",report.ToString());Debug.Log("Village player and population test completed. Passed="+passed);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!finished)Finish(new InvalidOperationException("Village test interrupted."));}
}
#endif
