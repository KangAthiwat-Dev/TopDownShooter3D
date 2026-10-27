#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public sealed class PlayerProgressionRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private PlayerProgressionState state;private string before;private bool persistBefore;
    private PlayerInput input;private ReadOnlyArray<InputDevice>? inputBefore;
    private bool passed=true,done,backgroundBefore;private float scaleBefore;private int fpsBefore,vsyncBefore;private string runtimeError;
    private string testKey;private readonly List<GameObject> temporary=new List<GameObject>();
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static string Save(int level,int experience)=>"{\"version\":1,\"level\":"+level+",\"experience\":"+experience+"}";
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;scaleBefore=Time.timeScale;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Monster EXP / level / combat / save regression "+DateTime.Now.ToString("s"));
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
        var worldPopulation=FindAnyObjectByType<WorldMonsterDirector>();if(worldPopulation!=null)worldPopulation.gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyHealth>())enemy.gameObject.SetActive(false);
        foreach(var population in FindObjectsByType<TownNpcPopulation>())population.enabled=false;
        var movement=FindAnyObjectByType<PlayerMovement>();state=movement.GetComponent<PlayerProgressionState>();before=state.CaptureSave();
        persistBefore=(bool)typeof(PlayerProgressionState).GetField("persistProgress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(state);Set(state,"persistProgress",false);
        input=movement.GetComponent<PlayerInput>();if(input.actions.devices.HasValue)inputBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());input.actions.devices=Array.Empty<InputDevice>();
        state.RestoreSave(Save(1,0));var hud=FindAnyObjectByType<PlayerProgressionUI>();
        state.GainExperience(99);Check(state.Level==1&&state.Experience==99&&Mathf.Abs(hud.DisplayedProgress-.99f)<.001f,"EXP fills existing gold HUD before a level-up");
        state.GainExperience(1);Check(state.Level==2&&state.Experience==0&&hud.DisplayedLevel=="LV. 2"&&hud.DisplayedProgress==0,"Exact threshold promotes and clears the completed bar");
        state.GainExperience(147+218+10);Check(state.Level==4&&state.Experience==10,"One reward can cross several levels while carrying excess EXP");
        string unchanged=state.CaptureSave();state.GainExperience(0);state.GainExperience(-30);Check(state.CaptureSave()==unchanged,"Zero or negative rewards cannot change progression");
        Check(PlayerProgressionState.ExperienceForLevel(1)==100&&PlayerProgressionState.ExperienceForLevel(2)==147&&PlayerProgressionState.ExperienceForLevel(99)==118778,"Progression curve has expected early and late costs");
        Check(Enumerable.Range(1,98).All(l=>PlayerProgressionState.ExperienceForLevel(l+1)>PlayerProgressionState.ExperienceForLevel(l)),"Every later level needs more EXP");
        var boundaries=new[]{1,9,10,19,20,29,30,39,40,49,50,64,65,79,80,89,90,99,100};
        var ranks=new[]{"F","F","E","E","D","D","C","C","B","B","A","A","S","S","SS","SS","SSS","SSS","EX"};
        for(int i=0;i<boundaries.Length;i++){state.RestoreSave(Save(boundaries[i],0));Check(state.Rank.StartsWith(ranks[i]+" /")&&hud.DisplayedRank==state.Rank,"Rank and HUD at level "+boundaries[i]+" = "+ranks[i]);}
        state.RestoreSave(Save(99,118777));state.GainExperience(1);state.GainExperience(int.MaxValue);
        Check(state.Level==100&&state.Experience==0&&state.ExperienceRequired==0&&state.LevelProgress==1&&hud.DisplayedProgress==1,"Level 100 caps safely with a full gold bar and no overflow");
        state.RestoreSave(Save(1,0));state.GainExperience(int.MaxValue);Check(state.Level==100,"Huge EXP reward reaches the cap without integer overflow");
        unchanged=state.CaptureSave();Check(!state.RestoreSave("{\"version\":2,\"level\":20,\"experience\":0}")&&!state.RestoreSave(Save(0,0))&&!state.RestoreSave(Save(100,1))&&!state.RestoreSave("{}")&&state.CaptureSave()==unchanged,"Invalid or future saves cannot overwrite current progression");
        state.RestoreSave(Save(1,0));var target=Target(new Vector3(210,1,210),40,100);
        target.TakeDamage(1,state.gameObject);Check(state.Experience==0,"Non-lethal damage does not award EXP");target.TakeDamage(99,state.gameObject);target.TakeDamage(999,state.gameObject);
        Check(state.Experience==40,"Lethal player hit awards exactly once");target.RestoreToFullHealth();target.TakeDamage(100,state.gameObject);Check(state.Experience==80,"A restored monster life can award once again");
        var other=Target(new Vector3(212,1,210),100,100);other.TakeDamage(1,state.gameObject);other.TakeDamage(99);Check(state.Experience==80,"Unattributed lethal damage does not steal a player kill reward");
        var dummy=Target(new Vector3(214,1,210),0,10);dummy.TakeDamage(10,state.gameObject);Check(state.Experience==80,"Zero-reward training targets do not grant EXP");
        // Isolate real physics and animation tests on a temporary floor away from the map.
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(floor);floor.layer=7;floor.transform.position=new Vector3(220,0,220);floor.transform.localScale=new Vector3(20,1,20);
        var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.position=new Vector3(220,.55f,220);movement.transform.rotation=Quaternion.identity;cc.enabled=true;Physics.SyncTransforms();yield return Wait(.2f);
        state.RestoreSave(Save(1,0));var shotTarget=Target(new Vector3(220,.5f,224),35,10);
        var ranged=movement.GetComponent<PlayerRangedWeapon>();var projectilePrefab=(Projectile)typeof(PlayerRangedWeapon).GetField("projectilePrefab",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ranged);
        var projectile=Instantiate(projectilePrefab,movement.transform.position+Vector3.up+Vector3.forward,Quaternion.identity);temporary.Add(projectile.gameObject);projectile.Initialize(Vector3.forward,18,10,state.gameObject);
        yield return Until(()=>!shotTarget.IsAlive,2);Check(!shotTarget.IsAlive&&state.Experience==35,"Actual projectile collision credits its player owner");
        shotTarget.gameObject.SetActive(false);var meleeTarget=Target(movement.transform.position+Vector3.forward*1.4f,25,5);var targetable=meleeTarget.gameObject.AddComponent<Targetable>();
        var melee=movement.GetComponent<PlayerMeleeAttack>();typeof(PlayerMeleeAttack).GetMethod("HandleMeleeAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(melee,new object[]{targetable});
        Check(meleeTarget.IsAlive&&state.Experience==35,"Melee EXP waits for the actual animation hit");yield return Until(()=>!meleeTarget.IsAlive,3);
        Check(!meleeTarget.IsAlive&&state.Experience==60,"Actual melee animation hit awards the monster reward");
        // Save round trip uses a disposable key, never the real player profile.
        testKey="TopDownShooter3D.Progression.Test."+Guid.NewGuid().ToString("N");
        var saver=SaveActor(testKey);saver.GainExperience(345);saver.FlushSave();string expected=saver.CaptureSave();Destroy(saver.gameObject);yield return null;
        var loaded=SaveActor(testKey);Check(loaded.CaptureSave()==expected,"Saved level and remaining EXP load in a new player instance");
        Check(runtimeError==null,"No runtime errors in progression combat or HUD");
        FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();
    }
    private EnemyHealth Target(Vector3 position,int reward,float hp)
    {
        var go=new GameObject("Temporary EXP monster");temporary.Add(go);go.SetActive(false);go.layer=6;go.transform.position=position;
        var health=go.AddComponent<EnemyHealth>();Set(health,"experienceReward",reward);Set(health,"maxHealth",hp);
        var box=go.AddComponent<BoxCollider>();box.center=Vector3.up;box.size=new Vector3(1,2,1);go.SetActive(true);return health;
    }
    private PlayerProgressionState SaveActor(string key)
    {
        var go=new GameObject("Temporary progression save roundtrip");temporary.Add(go);go.SetActive(false);var p=go.AddComponent<PlayerProgressionState>();Set(p,"saveKey",key);go.SetActive(true);return p;
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> predicate,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool valid,string label){passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/PlayerProgressionPlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message+"\n"+trace;}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(state!=null&&before!=null){state.RestoreSave(before);Set(state,"persistProgress",persistBefore);}if(input!=null)input.actions.devices=inputBefore;
        foreach(var go in temporary)if(go!=null)Destroy(go);
        if(testKey!=null){PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();}
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=scaleBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/PlayerProgressionPlaytest.txt",report.ToString());Debug.Log("Player progression regression complete. Passed="+passed);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Progression test interrupted."));}
}
#endif
