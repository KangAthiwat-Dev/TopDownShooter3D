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

public sealed class FixedCampRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<string> errors=new List<string>();
    private WorldMonsterDirector director;private PlayerHealth player;private CharacterController controller;
    private bool done,oldBackground;private int oldFps,oldVsync;
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
    private WorldMonsterMember[] Members(int id)=>FindObjectsByType<WorldMonsterMember>(FindObjectsSortMode.None).Where(m=>m.CampId==id&&m.Health.IsAlive).ToArray();
    private IEnumerator Checks()
    {
        yield return null;director=FindAnyObjectByType<WorldMonsterDirector>();player=FindAnyObjectByType<PlayerHealth>();controller=player.GetComponent<CharacterController>();
        Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerAttackController>().enabled=false;player.GetComponent<PlayerMeleeAttack>().enabled=false;player.GetComponent<PlayerRangedWeapon>().enabled=false;player.ConfigureUpgrades(20000,0);
        foreach(var follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))follow.enabled=false;
        Warp(new Vector3(-150,4,-150));LookAway();
        yield return Until(()=>director.Initialized,15);
        yield return Until(()=>Enumerable.Range(0,4).All(i=>director.CampLivingCount(i)==5),8);
        Check(director.Initialized&&director.useFixedCamps&&director.CampCount==4,"Four authored fixed camps initialize without random layouts");
        var layouts=director.fixedCamps.ToArray();var centers=layouts.Select(c=>c.transform.position).ToArray();
        var props=layouts.SelectMany(c=>c.groundedProps).ToArray();var propPositions=props.Select(p=>p.position).ToArray();
        var expected=new[]{new Vector3(-25.46f,4,-75.51f),new Vector3(-.03f,4,-37.75f),new Vector3(65.03f,4,-6.37f),new Vector3(-38.16f,4,9.60f)};
        Check(layouts.Where((c,i)=>Vector3.Distance(c.transform.position,expected[i])<.1f).Count()==4,"All four camp roots match the player's painted clearings");
        for(int i=0;i<4;i++)
        {
            var members=Members(i);
            if(members.Length!=5)foreach(var a in layouts[i].memberPoints)report.AppendLine("SPAWN "+i+" "+a.name+" overlaps="+string.Join(",",Physics.OverlapCapsule(a.position+Vector3.up*.6f,a.position+Vector3.up*1.7f,.45f,~0,QueryTriggerInteraction.Ignore).Select(c=>c.name)));
            Check(members.Length==5&&members.Count(m=>m.SpeciesId=="goblin_chief")==1&&members.Count(m=>m.SpeciesId=="goblin_demon")==4,"Camp "+i+" contains one chief and four guards");
            Check(members.All(m=>m.GetComponent<NavMeshAgent>().isOnNavMesh),"Camp "+i+" members stand on valid navigation");
            Check(layouts[i].transform.lossyScale==Vector3.one&&members.All(m=>Mathf.Abs(m.transform.lossyScale.x-m.transform.localScale.x)<.001f),"Camp "+i+" keeps actors at their original prefab size");
            Check(layouts[i].groundedProps.All(p=>p.GetComponent<Collider>()!=null&&p.GetComponent<NavMeshObstacle>().carving),"Camp "+i+" props retain solid collision and navigation carving");
        }
        Check(director.RoamingAlive==director.species.Sum(s=>s.population),"Existing roaming monster population is preserved");
        var initial=Members(0);var positions=initial.Select(m=>m.transform.position).ToArray();yield return Wait(2.5f);
        Check(initial.Where((m,i)=>Vector3.Distance(m.transform.position,positions[i])>.15f).Any(),"Camp monsters patrol instead of standing still");
        Freeze();
        for(int i=0;i<4;i++){LookAt(centers[i]);yield return Capture("Camp_0"+(i+1));}
        Warp(new Vector3(-150,4,-150));LookAway();director.campRespawnDelay=4.5f;
        int deaths=0;director.MonsterDefeated+=m=>{if(m.CampId==0)deaths++;};
        var first=Members(0).First(m=>m.SpeciesId=="goblin_demon");var progression=player.GetComponent<PlayerProgressionState>();progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        int reward=first.Health.ExperienceReward,expectedLevel=1,expectedExp=reward;
        while(expectedLevel<PlayerProgressionState.MaximumLevel&&expectedExp>=PlayerProgressionState.ExperienceForLevel(expectedLevel)){expectedExp-=PlayerProgressionState.ExperienceForLevel(expectedLevel);expectedLevel++;}
        first.Health.TakeDamage(99999,player.gameObject);first.Health.TakeDamage(99999,player.gameObject);
        yield return Wait(5.1f);
        Check(director.CampLivingCount(0)==4&&director.CampRespawnRemaining(0)<0,"Partial clear never starts the timer or refills defeated members");
        Check(deaths==1&&progression.Level==expectedLevel&&progression.Experience==expectedExp,"A camp death grants its original EXP and death event exactly once, including level-up rollover");
        var removed=Members(0).First();Destroy(removed.gameObject);yield return Wait(.1f);
        yield return Until(()=>director.CampLivingCount(0)==4,3);Freeze();
        Check(director.CampLivingCount(0)==4&&director.CampRespawnRemaining(0)<0&&deaths==1,"An external despawn repairs only that living slot without a kill or a full refill");
        Warp(centers[0]+Vector3.back*10);LookAt(centers[0]);
        foreach(var m in Members(0))m.Health.TakeDamage(99999,player.gameObject);
        Check(director.CampLivingCount(0)==0&&director.CampRespawnRemaining(0)>4,"Only the final actual death starts the camp cooldown");
        var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.PauseMenu,true);float remaining=director.CampRespawnRemaining(0);yield return Wait(.8f);
        Check(Mathf.Abs(remaining-director.CampRespawnRemaining(0))<.01f,"Respawn cooldown pauses with the game");pause.SetPaused(GamePauseReason.PauseMenu,false);
        yield return Wait(5.2f);
        Check(director.CampLivingCount(0)==0,"Expired cooldown cannot respawn monsters near the player");
        Warp(new Vector3(-150,4,-150));LookAt(centers[0]);yield return Wait(1.2f);
        Check(director.CampLivingCount(0)==0,"Monsters wait while the distant camp is still visible to the camera");
        LookAway();yield return Until(()=>director.CampLivingCount(0)==5,5);Freeze();
        Check(director.CampLivingCount(0)==5&&director.CampRespawnRemaining(0)<0,"A full new squad respawns after cooldown when player and camera are away");
        Check(Members(0).All(m=>!m.Defeated&&m.Health.CurrentHealth==m.Health.MaxHealth&&m.GetComponent<NavMeshAgent>().isOnNavMesh),"Respawned monsters have fresh health, death state and valid navigation");
        Check(layouts.Where((c,i)=>c==director.ActiveCamps[i]&&Vector3.Distance(c.transform.position,centers[i])<.001f).Count()==4&&props.Where((p,i)=>p!=null&&Vector3.Distance(p.position,propPositions[i])<.001f).Count()==props.Length,"All original camp layouts and props remain at exactly the same positions");
        Check(Enumerable.Range(1,3).All(i=>director.CampLivingCount(i)==5)&&deaths==5,"Other camps remain independent and respawning sends no false death events");
        Check(errors.Count==0,"No runtime errors during fixed-camp placement, patrol, clearing, pause or respawn");
    }
    private void Freeze(){foreach(var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)){enemy.enabled=false;if(enemy.TryGetComponent<EnemyAttack>(out var attack))attack.enabled=false;if(enemy.TryGetComponent<NavMeshAgent>(out var agent)&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}}}
    private void Warp(Vector3 point){controller.enabled=false;player.transform.position=point;controller.enabled=true;Physics.SyncTransforms();}
    private static void LookAway(){Camera.main.transform.SetPositionAndRotation(new Vector3(-150,25,-150),Quaternion.LookRotation(Vector3.back+Vector3.down*.3f));}
    private static void LookAt(Vector3 point){Camera.main.transform.position=point+new Vector3(10,16,-16);Camera.main.transform.LookAt(point+Vector3.up);}
    private IEnumerator Capture(string name)
    {
        var overlays=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.renderMode!=RenderMode.WorldSpace).ToArray();
        foreach(var canvas in overlays)canvas.enabled=false;
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/FixedCampTests/"+name+".png");yield return Wait(.15f);
        foreach(var canvas in overlays)if(canvas!=null)canvas.enabled=true;
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/FixedCampPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);File.WriteAllText("Logs/FixedCampPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Fixed camp test interrupted"));}
}
#endif
