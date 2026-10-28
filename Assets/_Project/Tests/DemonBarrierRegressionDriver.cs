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

public sealed class DemonBarrierRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerHealth player;private CharacterController controller;private bool done,oldBackground;private int oldFps,oldVsync;
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
        yield return null;player=FindAnyObjectByType<PlayerHealth>();controller=player.GetComponent<CharacterController>();
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        var director=FindAnyObjectByType<WorldMonsterDirector>();if(director!=null)director.gameObject.SetActive(false);
        foreach(var wave in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){wave.StopAllCoroutines();wave.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        foreach(var follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))follow.enabled=false;
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerAttackController>().enabled=false;player.ConfigureUpgrades(20000,0);
        var areas=FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);var sample=areas.First(e=>e.name.StartsWith("01"));var lord=areas.First(e=>e.name.StartsWith("02"));var king=areas.First(e=>e.name.StartsWith("03"));
        var barriers=FindObjectsByType<EncounterBarrier>(FindObjectsSortMode.None);var sampleGate=barriers.Single(b=>b.Prerequisite==sample);var door=barriers.Single(b=>b.Prerequisite==lord);
        var walls=door.transform.Find("Permanent_Walls").GetComponentsInChildren<Collider>();
        Check(barriers.Length==2&&FindObjectsByType<DemonProgressionGate>(FindObjectsSortMode.None).Length==0,"Actual door and sample exit replace the broad legacy rock-wall blocker");
        var zone=GameObject.Find("Zone_Demon").transform;
        foreach(string name in new[]{"01_Sample_Area","02_DemonLord_Area","03_King_Area"})
        {
            var group=zone.Find(name);Check(group!=null&&group.GetComponentsInChildren<DemonArenaEncounter>().Length==1&&group.Find("Geometry").childCount==1,"Area grouping keeps floor, scenery and encounter together: "+name);
        }
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().All(g=>!g.name.StartsWith("SM_VKI")||g.transform.position.x<0||g.transform.position.x>105||g.transform.position.z<8||g.transform.position.z>105),"User Demon scenery no longer clutters scene roots");
        Check(walls.Length>=4&&walls.All(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==7),"All permanent user walls have solid Environment collision");
        Check(door.BlockingColliders.Length>0&&door.BlockingColliders.All(c=>c.enabled&&!c.isTrigger)&&!door.Unlocked,"Only the actual door leaf starts locked with collision");
        Check(sampleGate.MovingPart.childCount==4&&sampleGate.BlockingColliders.Length==4&&!sampleGate.Unlocked,"Four authored sample stalagmite groups start raised and collidable");
        var doorCollider=door.BlockingColliders.OfType<BoxCollider>().First();var closedPoint=doorCollider.bounds.center;closedPoint.y=door.MovingPart.position.y+.12f;
        var doorNormal=door.MovingPart.forward;doorNormal.y=0;doorNormal.Normalize();
        Warp(closedPoint-doorNormal*3);yield return Wait(.35f);FreezeOrdinary(areas);
        float closed=Cross(closedPoint,doorNormal);Check(closed<-.2f,"Normal player cannot walk through the closed door");
        player.gameObject.layer=9;closed=Cross(closedPoint,doorNormal);player.gameObject.layer=0;
        Check(closed<-.2f&&!Physics.GetIgnoreLayerCollision(9,7),"Shadow form also collides with the locked gate");
        Warp(closedPoint);yield return Capture("User_Gate_Closed");

        Warp(sample.Arena.transform.position);yield return Wait(.4f);FreezeOrdinary(areas);
        Check(sample.WaveIndex==0&&sample.LivingCount==5,"Reorganized sample arena spawns all five intended monsters on valid navigation");
        var raised=sampleGate.MovingPart.position;var first=sample.GetComponentsInChildren<EnemyHealth>().First();first.TakeDamage(first.MaxHealth+1);yield return Wait(.3f);
        Check(!sample.Completed&&sampleGate.OpeningProgress==0&&Vector3.Distance(raised,sampleGate.MovingPart.position)<.01f,"A partial kill cannot lower the sample exit");
        var spikeCenter=sampleGate.transform.position;spikeCenter.y=raised.y+.12f;var spikeNormal=Vector3.ProjectOnPlane(lord.Arena.transform.position-sample.Arena.transform.position,Vector3.up).normalized;
        Check(Cross(spikeCenter,spikeNormal,5)<-.2f,"Raised stalagmites physically block the sample exit");
        Warp(sample.Arena.transform.position);Kill(sample);yield return Until(()=>sampleGate.OpeningProgress>.05f,5);
        Check(sample.Completed&&sampleGate.OpeningProgress>0&&sampleGate.OpeningProgress<1&&sampleGate.MovingPart.position.y<raised.y,"Clearing the sample begins a smooth downward movement");
        var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.PauseMenu,true);var pausedPosition=sampleGate.MovingPart.position;yield return Wait(.25f);
        Check(Vector3.Distance(pausedPosition,sampleGate.MovingPart.position)<.001f,"Sinking exit animation pauses with the game");pause.SetPaused(GamePauseReason.PauseMenu,false);
        yield return Until(()=>sampleGate.IsOpen,4);Check(sampleGate.IsOpen&&!sampleGate.MovingPart.gameObject.activeSelf&&sampleGate.BlockingColliders.All(c=>!c.enabled),"Stalagmites finish below ground and release collision only when clear");
        Check(Get<NavMeshObstacle[]>(sampleGate,"navigationObstacles").All(o=>!o.enabled),"Sample exit also releases its carved navigation blocker");
        Check(Cross(spikeCenter,spikeNormal,5)>2,"Normal player can cross the sample exit after the stalagmites lower");yield return Capture("Sample_Exit_Open");

        var lordBoss=Get<GameObject>(lord,"boss").GetComponent<EncounterCombatant>();Warp(lord.Arena.transform.position);yield return Wait(.4f);FreezeOrdinary(areas);
        Check(lord.LivingCount==7&&!door.Unlocked,"Lord arena still starts with boss, two dragons and four monsters");
        lordBoss.Health.IsInvulnerable=false;lordBoss.Health.TakeDamage(lordBoss.Health.MaxHealth+1);yield return Wait(.3f);
        Check(!lord.Completed&&!door.Unlocked&&door.OpeningProgress==0,"Killing DemonLord alone cannot open the door while guardians survive");
        var closedRotation=door.MovingPart.rotation;Kill(lord);yield return Until(()=>door.OpeningProgress>.1f,5);
        Check(lord.Completed&&door.OpeningProgress>0&&door.OpeningProgress<1&&Quaternion.Angle(closedRotation,door.MovingPart.rotation)>1,"Defeating the whole Lord encounter swings the real door on its hinge");
        Check(walls.All(c=>c.enabled)&&door.MovingPart.gameObject.activeSelf,"Opening moves the door while every surrounding wall remains in place");
        yield return Until(()=>door.IsOpen,4);yield return Wait(.8f);
        Check(door.IsOpen&&Quaternion.Angle(closedRotation,door.MovingPart.rotation)>100&&door.BlockingColliders.All(c=>!c.enabled),"Open gate stays visible, rotated clear of the passage, with blocking collision released");
        Check(Get<NavMeshObstacle[]>(door,"navigationObstacles").All(o=>!o.enabled)&&walls.All(c=>c.enabled),"Door navigation opens without removing permanent wall collision");
        bool startNav=NavMesh.SamplePosition(closedPoint-doorNormal*1.8f,out var navStart,.8f,NavMesh.AllAreas);
        bool endNav=NavMesh.SamplePosition(closedPoint+doorNormal*1.8f,out var navEnd,.8f,NavMesh.AllAreas);
        Check(startNav&&endNav&&!NavMesh.Raycast(navStart.position,navEnd.position,out _,NavMesh.AllAreas),"Navigation has a clear route straight through the opened doorway");
        Check(Cross(closedPoint,doorNormal)>1.5f,"Actual player can pass through the opened door into the King side");yield return Capture("User_Gate_Open");
        Warp(king.Arena.transform.position);yield return Wait(.4f);FreezeOrdinary(areas);Check(king.WaveIndex==0&&king.LivingCount==8,"King encounter remains linked to Lord victory after regrouping");
        // Walk into a permanent bar wall after victory: it must still block.
        var wall=walls.OfType<BoxCollider>().First(c=>c.name.Contains("300"));var point=wall.bounds.center;point.y=door.MovingPart.position.y+.12f;var normal=wall.transform.forward;normal.y=0;normal.Normalize();
        Check(Cross(point,normal)<-.2f,"Permanent bar walls continue blocking after the door has opened");
        Check(errors.Count==0,"No runtime errors in authored gate collision, sinking sample exit, navigation and encounter links");
    }
    private void FreezeOrdinary(DemonArenaEncounter[] areas)
    {
        foreach(var enemy in areas.SelectMany(e=>e.GetComponentsInChildren<EnemyAI>())){enemy.enabled=false;if(enemy.TryGetComponent<EnemyAttack>(out var attack))attack.enabled=false;foreach(var c in enemy.GetComponentsInChildren<Collider>())c.enabled=false;}
    }
    private float Cross(Vector3 point,Vector3 normal,float halfDistance=3)
    {
        Warp(point-normal*halfDistance);
        for(int i=0;i<40;i++)controller.Move(normal*(halfDistance*2/40));
        return Vector3.Dot(player.transform.position-point,normal);
    }
    private void Warp(Vector3 point)
    {
        controller.enabled=false;player.transform.position=point;controller.enabled=true;Physics.SyncTransforms();
        var camera=Camera.main;camera.transform.position=point+new Vector3(0,18,-15);camera.transform.LookAt(point+Vector3.up*1.5f);
    }
    private static void Kill(DemonArenaEncounter encounter)
    {
        var actors=encounter.GetComponentsInChildren<EnemyHealth>().ToList();var boss=Get<GameObject>(encounter,"boss");if(boss!=null&&boss.activeSelf)actors.Add(boss.GetComponent<EnemyHealth>());
        foreach(var health in actors){health.IsInvulnerable=false;health.TakeDamage(health.MaxHealth+1);}
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/DemonBarrierTests/"+name+".png");yield return Wait(.1f);}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/DemonBarrierPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);File.WriteAllText("Logs/DemonBarrierPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Barrier test interrupted"));}
}
#endif
