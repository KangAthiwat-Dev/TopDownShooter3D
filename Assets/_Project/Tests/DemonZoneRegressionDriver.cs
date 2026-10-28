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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class DemonZoneRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerHealth player;private PlayerMovement movement;private Keyboard keyboard;private bool done,oldBackground;private int oldFps,oldVsync;
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    private static T Get<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
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
        yield return null;
        player=FindAnyObjectByType<PlayerHealth>();movement=player.GetComponent<PlayerMovement>();
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        var population=FindAnyObjectByType<WorldMonsterDirector>();if(population!=null)population.gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))ai.gameObject.SetActive(false);
        foreach(var follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))follow.enabled=false;
        player.GetComponent<PlayerAttackController>().enabled=false;movement.enabled=false;player.ConfigureUpgrades(20000,0);
        keyboard=InputSystem.AddDevice<Keyboard>("DemonZoneTestKeyboard");var input=player.GetComponent<PlayerInput>();input.neverAutoSwitchControlSchemes=true;input.actions.devices=new InputDevice[]{keyboard};
        var encounters=FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None);
        var sample=encounters.First(e=>e.name.StartsWith("01"));var lord=encounters.First(e=>e.name.StartsWith("02"));var king=encounters.First(e=>e.name.StartsWith("03"));
        var kingBoss=Get<GameObject>(king,"boss").GetComponent<KingOfDemonAI>();var lordBoss=Get<GameObject>(lord,"boss").GetComponent<EncounterCombatant>();var gate=FindAnyObjectByType<DemonProgressionGate>();
        Check(encounters.Length==3&&sample.Waves.Count==1&&lord.Waves.Count==1&&king.Waves.Count==3,"Three separately editable arenas and three King stages exist");
        Check(sample.Waves.SelectMany(w=>w.members).All(s=>s.prefab.GetComponent<EnemyAI>()!=null),"Sample zone contains ordinary monsters only");
        Check(lord.Waves[0].members.Count(s=>s.prefab.GetComponent<EncounterCombatant>()!=null)==2&&king.Waves[1].members.Count(s=>s.prefab.GetComponent<EncounterCombatant>()!=null)==2,"DemonLord and King stage two each have exactly two dragons");
        Check(king.Waves[0].members.All(s=>s.prefab.name.Contains("Goblin")||s.prefab.name.Contains("Skeleton")),"First King wave excludes wildlife");
        Check(!gate.IsOpen&&gate.GetComponent<Collider>().enabled&&!kingBoss.gameObject.activeSelf&&!lordBoss.gameObject.activeSelf,"Rock gate starts closed and bosses wait for their encounter stage");
        Warp(king.Arena.transform.position);yield return Wait(.35f);
        Check(king.WaveIndex==-1&&!kingBoss.gameObject.activeSelf,"Bypassing the gate cannot activate King before DemonLord victory");
        Warp(gate.transform.position-gate.transform.right*5);var controller=player.GetComponent<CharacterController>();
        for(int i=0;i<20;i++)controller.Move(gate.transform.right*.5f);
        Check(gate.transform.InverseTransformPoint(player.transform.position).x < -1.4f,"Actual player controller cannot cross the closed rock gate");
        Warp(sample.Arena.transform.position);yield return Wait(.35f);
        Check(sample.WaveIndex==0&&sample.LivingCount==5,"Entering sample spawns exactly five monsters");
        var normal=sample.GetComponentsInChildren<EnemyHealth>().First();normal.TakeDamage(40);yield return Wait(.15f);
        var bars=FindAnyObjectByType<MonsterOverheadHealthUI>();Check(bars.VisibleCount>0,"Damaged ordinary monsters show floating overhead health bars");yield return Capture("Sample_Health_Bars");
        KillMembers(sample);yield return Wait(2.9f);Check(sample.Completed,"Actual deaths complete sample encounter");

        Warp(lord.Arena.transform.position);yield return Wait(.5f);
        Check(lord.LivingCount==7&&lordBoss.gameObject.activeSelf,"DemonLord starts with two dragons and four small monsters");
        var dragons=lord.GetComponentsInChildren<EncounterCombatant>().ToArray();
        Check(dragons.Length==2&&dragons.All(d=>!d.UsesBossHud&&d.Arena==lord.Arena),"Dragons share the arena AI and use overhead bars instead of boss HUD");
        Check(lordBoss.Health.MaxHealth>=3000&&kingBoss.Health.MaxHealth>=5000&&dragons.All(d=>d.Health.MaxHealth>=1000),"Late-game boss and guardian durability is scoped to Demon encounters");
        var hud=FindObjectsByType<BossHealthUI>(FindObjectsSortMode.None);Check(hud.Length==2,"Only DemonLord and King retain screen boss HUD presenters");
        yield return Wait(.35f);Check(bars.VisibleCount>0,"Dragon and monster overhead bars render during the Lord encounter");yield return Capture("DemonLord_Guardians");
        lordBoss.Health.IsInvulnerable=false;lordBoss.Health.TakeDamage(99999);yield return Wait(.2f);
        Check(!gate.IsOpen&&!lord.Completed&&lord.LivingCount==6,"Killing only DemonLord does not skip surviving guardians");
        Warp(sample.Arena.transform.position);yield return Wait(5.4f);
        Check(lord.WaveIndex==-1&&!lordBoss.gameObject.activeSelf&&lordBoss.Health.IsAlive,"Abandoning unfinished fight resets dead boss and incomplete stage safely");
        Warp(lord.Arena.transform.position);yield return Wait(.4f);Check(lord.LivingCount==7,"Re-entry creates one clean Lord attempt without duplicate guards");
        KillMembers(lord);yield return Wait(2.9f);Check(lord.Completed&&gate.IsOpen&&!gate.GetComponent<Collider>().enabled&&!gate.GetComponent<NavMeshObstacle>().enabled,"Clearing all Lord combatants opens physical and navigation gate");
        yield return Wait(2.1f);Check(!Get<Transform>(gate,"rocks").gameObject.activeSelf,"Rock wall finishes sinking out of the path");
        Warp(gate.transform.position-gate.transform.right*5);for(int i=0;i<20;i++)controller.Move(gate.transform.right*.5f);
        Check(gate.transform.InverseTransformPoint(player.transform.position).x > 2,"Actual player controller crosses the opened gate");

        Warp(king.Arena.transform.position);yield return Wait(.35f);
        Check(king.WaveIndex==0&&king.LivingCount==8&&!kingBoss.gameObject.activeSelf,"King stage one spawns eight monsters while boss remains absent");
        // Removing a member without a death must reset rather than count as a kill.
        Destroy(king.GetComponentsInChildren<EnemyHealth>().First().gameObject);yield return null;yield return null;
        Check(king.WaveIndex<=0&&!kingBoss.gameObject.activeSelf,"A despawn cannot skip encounter stages");
        yield return Wait(.3f);KillMembers(king);yield return Wait(2.9f);
        Check(king.WaveIndex==1&&king.LivingCount==6&&!kingBoss.gameObject.activeSelf,"After first wave, two dragons arrive alongside four monsters");yield return Capture("King_Stage_Two");
        KillMembers(king);yield return Wait(2.9f);
        Check(king.WaveIndex==2&&king.LivingCount==1&&kingBoss.gameObject.activeSelf&&kingBoss.IsEngaged,"Only after stage two does King enter combat");
        var kingsHud=hud.First(u=>Get<KingOfDemonAI>(u,"boss")!=null);yield return Wait(.4f);Check(kingsHud.IsVisible,"King boss health HUD appears in final phase");

        // Exercise the real AI warning -> warp -> animation event -> committed miss.
        yield return Until(()=>kingBoss.State==DemonBossState.Chase,8);
        kingBoss.GetComponent<BossVFXController>().CancelCombatEffects();
        Set(kingBoss,"fireballReady",float.PositiveInfinity);Set(kingBoss,"shockwaveReady",float.PositiveInfinity);
        Set(kingBoss,"slashReady",0f);Set(kingBoss,"teleportReady",0f);Set(kingBoss,"nextDecision",0f);
        Warp(king.Arena.ClampToArena(kingBoss.transform.position+kingBoss.transform.forward*8,4));
        yield return Until(()=>kingBoss.State==DemonBossState.Teleporting,8);
        if(kingBoss.State!=DemonBossState.Teleporting)DumpLanding(kingBoss);
        Check(kingBoss.State==DemonBossState.Teleporting,"King signals a blink strike before teleporting; state="+kingBoss.State+", inside="+king.Arena.PlayerInside+", boss="+kingBoss.transform.position+", player="+player.transform.position+", sight="+kingBoss.HasSight(player.transform));
        var destination=kingBoss.TeleportDestination;var before=player.CurrentHealth;yield return Capture("King_Blink_Warning");
        // Dash perpendicular to the committed attack line using actual input.
        Vector3 incoming=Vector3.ProjectOnPlane(player.transform.position-destination,Vector3.up).normalized;
        Vector3 side=Vector3.Cross(Vector3.up,incoming);movement.enabled=true;
        var camera=Camera.main;camera.transform.rotation=Quaternion.LookRotation(incoming+Vector3.down*.8f,Vector3.up);
        var dodgeKey=Mathf.Abs(incoming.x)>Mathf.Abs(incoming.z)?Key.W:Key.D;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(dodgeKey,Key.Space));
        yield return Wait(.22f);Check(movement.IsDashing||Vector3.Distance(player.transform.position,destination)>4,"Actual Dash input moves player clear of the warned landing strike");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.4f);movement.enabled=false;
        yield return Until(()=>kingBoss.State==DemonBossState.Recovery,6);
        Check(player.CurrentHealth>=before-.01f&&kingBoss.State==DemonBossState.Recovery,"Committed blink swing misses a dodging player and enters recovery");
        var rotation=kingBoss.transform.rotation;yield return Wait(.7f);Check(kingBoss.State==DemonBossState.Recovery&&Quaternion.Angle(rotation,kingBoss.transform.rotation)<1,"Miss recovery provides an attack opening without tracking the player");
        yield return Wait(1.2f);Check(kingBoss.State!=DemonBossState.Dead,"King resumes fighting after recovery");

        // Repeat with a stationary target to confirm impact damage is real.
        yield return Until(()=>kingBoss.State==DemonBossState.Chase,6);
        Warp(king.Arena.ClampToArena(kingBoss.transform.position+kingBoss.transform.forward*7,4));
        Set(kingBoss,"slashReady",0f);Set(kingBoss,"teleportReady",0f);Set(kingBoss,"nextDecision",0f);
        yield return Until(()=>kingBoss.State==DemonBossState.Teleporting,8);Check(kingBoss.State==DemonBossState.Teleporting,"Blink strike becomes available again after cooldown");
        before=player.CurrentHealth;yield return Wait(1.65f);Check(player.CurrentHealth<before,"Standing in the blink swing deals actual melee damage");
        kingBoss.Health.IsInvulnerable=false;kingBoss.Health.TakeDamage(99999);yield return Wait(2.9f);Check(king.Completed,"King death completes the final encounter");

        var lava=FindObjectsByType<LavaDeathSurface>(FindObjectsSortMode.None);Check(lava.Length==2,"Both actual liquid meshes have lethal surface checks");
        Vector3 lavaPoint=default;
        foreach(var surface in lava)
        {
            var mesh=surface.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            bool found=false;Vector3 wet=default;
            for(int i=0;i<triangles.Length;i+=3)
            {
                wet=surface.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                if(surface.TrySurfaceHeight(wet,out var height)){wet.y=height;found=true;break;}
            }
            if(surface.name=="Lava")lavaPoint=wet;
            Check(found&&surface.IsLethalAt(wet+Vector3.down)&&!surface.IsLethalAt(wet+Vector3.up*3),surface.name+": below liquid is lethal while a bridge above stays safe");
        }
        player.ConfigureUpgrades(20000,.75f);player.TryPotionGuard(.75f,10);
        Set(movement,"<IsShadowDashing>k__BackingField",true);
        Warp(lavaPoint+Vector3.up*30);yield return Wait(.15f);Check(player.IsAlive,"Player above liquid remains alive during actual hazard updates");
        Warp(lavaPoint-Vector3.up);yield return Wait(.2f);
        Check(player.IsDead,"Actual liquid contact kills through upgraded armour, potion resistance and shadow form");
        Check(errors.Count==0,"No runtime errors in phases, resets, boss combat, overhead UI and lava");
    }
    private void DumpLanding(KingOfDemonAI boss)
    {
        report.AppendLine("TRACE enabled="+Get<bool>(boss,"teleportMelee")+" ready="+Get<float>(boss,"teleportReady")+" time="+Time.time+" slashReady="+Get<float>(boss,"slashReady"));
        var dir=Vector3.ProjectOnPlane(player.transform.position-boss.transform.position,Vector3.up).normalized;
        foreach(float angle in new[]{0f,45f,-45f,90f,-90f,180f})
        {
            var p=player.transform.position-Quaternion.Euler(0,angle,0)*dir*2.6f;
            bool sampled=NavMesh.SamplePosition(p,out var hit,.8f,NavMesh.AllAreas);
            report.AppendLine("TRACE landing="+p+" nav="+sampled+" hit="+hit.position+" inside="+boss.Arena.Contains(hit.position,-1.5f));
            foreach(var c in Physics.OverlapSphere(hit.position+Vector3.up*2,1.35f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))report.AppendLine("TRACE overlap "+c.name+" / "+c.GetType().Name+" / "+c.bounds+" own="+c.transform.IsChildOf(boss.transform)+" player="+(c.GetComponentInParent<PlayerHealth>()!=null));
        }
    }
    private void Warp(Vector3 point)
    {
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=point+Vector3.up*.04f;cc.enabled=true;Physics.SyncTransforms();
        var view=Camera.main;view.transform.position=point+new Vector3(0,19,-15);view.transform.LookAt(point+Vector3.up);
    }
    private static void KillMembers(DemonArenaEncounter encounter)
    {
        var actors=encounter.GetComponentsInChildren<EnemyHealth>().ToList();var boss=Get<GameObject>(encounter,"boss");
        if(boss!=null&&boss.activeSelf)actors.Add(boss.GetComponent<EnemyHealth>());
        foreach(var health in actors){health.IsInvulnerable=false;health.TakeDamage(health.MaxHealth+1);}
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/DemonZoneTests/"+name+".png");yield return Wait(.1f);}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/DemonZonePlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);
        File.WriteAllText("Logs/DemonZonePlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Test interrupted"));}
}
#endif
