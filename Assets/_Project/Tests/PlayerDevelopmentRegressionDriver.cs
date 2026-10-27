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

public sealed class PlayerDevelopmentRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
    private readonly List<string> errors=new List<string>();
    private readonly List<float> shotTimes=new List<float>(),shotDamage=new List<float>();
    private readonly Vector3 origin=new Vector3(500,10.54f,500);
    private PlayerMovement movement;private CharacterController cc;private PlayerProgressionState progression;private PlayerEnergy energy;private PlayerHealth health;
    private PlayerRangedWeapon weapon;private PlayerAttackController attack;private PlayerUpgradePanelUI panel;private PlayerInput input;private Animator animator;
    private Keyboard keyboard;private ReadOnlyArray<InputDevice>? devicesBefore;private bool autoSwitchBefore,backgroundBefore,persistBefore,passed=true,done;
    private string saveBefore,testKey;private int fpsBefore,vsyncBefore;private float timeBefore;
    private static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static string Legacy(int level)=>"{\"version\":1,\"level\":"+level+",\"experience\":0}";
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Player development regression "+DateTime.Now.ToString("s"));
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
        movement=FindAnyObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();progression=movement.GetComponent<PlayerProgressionState>();
        energy=movement.GetComponent<PlayerEnergy>();health=movement.GetComponent<PlayerHealth>();weapon=movement.GetComponent<PlayerRangedWeapon>();attack=movement.GetComponent<PlayerAttackController>();
        panel=FindAnyObjectByType<PlayerUpgradePanelUI>();input=movement.GetComponent<PlayerInput>();animator=movement.GetComponentInChildren<Animator>();
        saveBefore=progression.CaptureSave();persistBefore=Get<bool>(progression,"persistProgress");Set(progression,"persistProgress",false);
        if(input.actions.devices.HasValue)devicesBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        autoSwitchBefore=input.neverAutoSwitchControlSchemes;input.neverAutoSwitchControlSchemes=true;
        keyboard=InputSystem.AddDevice<Keyboard>("PlayerDevelopmentRegression");input.actions.devices=new InputDevice[]{keyboard};Keys();
        Warp(new Vector3(500,20,500));var director=FindAnyObjectByType<WorldMonsterDirector>();yield return Until(()=>director.Initialized,15);director.enabled=false;
        foreach(var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        Cube("Temporary movement floor",new Vector3(500,10,500),new Vector3(140,1,140));
        progression.RestoreSave(Legacy(1));Warp(origin);yield return Wait(.2f);
        Check(progression.AvailableUpgradePoints==0&&!progression.TryUpgrade(PlayerUpgrade.Firepower),"Level 1 starts with no upgrades or free points");
        Check(weapon.Damage==8&&attack.BurstSize==3&&Mathf.Abs(attack.RangedAttackInterval-.2f)<.001f&&Mathf.Abs(attack.BurstRecovery-.8f)<.001f,"Fresh player has light 8-damage, three-shot bursts with deliberate recovery");
        progression.GainExperience(100);Check(progression.Level==2&&progression.AvailableUpgradePoints==1,"A real EXP level-up grants one unspent point");
        Check(progression.RestoreSave(Legacy(6))&&progression.AvailableUpgradePoints==5,"Legacy saves retain their level and receive all previously earned points");
        Keys(Key.C);yield return Wait(.1f);Keys();yield return Wait(.04f);
        Check(panel.IsOpen&&Time.timeScale==0,"C opens the upgrade menu and pauses gameplay");
        Check(!FindAnyObjectByType<InventoryPanelUI>().TryOpen(),"Inventory cannot overlap the character upgrade modal");
        foreach(PlayerUpgrade upgrade in Enum.GetValues(typeof(PlayerUpgrade)))Check(panel.Spend(upgrade),"Spend one point on "+upgrade);
        Check(progression.AvailableUpgradePoints==0&&!panel.Spend(PlayerUpgrade.Vitality),"Points cannot be overspent by repeated button presses");
        Check(health.MaxHealth==115&&Mathf.Abs(health.DamageReduction-.01f)<.001f,"Vitality increases max health and damage resistance immediately");
        Check(energy.MaxEnergy==110&&energy.ShadowDrainPerSecond<30,"Endurance and transformation increase usable ability duration");
        Check(weapon.Damage>8&&attack.RangedAttackInterval<.2f&&attack.BurstRecovery<.8f,"Firepower and cadence upgrades immediately affect the gun");
        string upgraded=progression.CaptureSave();progression.RestoreSave(Legacy(1));progression.RestoreSave(upgraded);
        Check(progression.UpgradeRank(PlayerUpgrade.Endurance)==1&&progression.AvailableUpgradePoints==0&&health.MaxHealth==115&&weapon.Damage>8,"Version 2 save roundtrip reapplies all stats and spent points");
        string unchanged=progression.CaptureSave();
        Check(!progression.RestoreSave("{\"version\":2,\"level\":2,\"experience\":0,\"upgrades\":[20,0,0,0,0]}")&&!progression.RestoreSave("{\"version\":2,\"level\":99,\"experience\":0,\"upgrades\":[21,0,0,0,0]}")&&progression.CaptureSave()==unchanged,"Invalid overspent or out-of-range upgrade saves are rejected atomically");
        progression.RestoreSave(Legacy(100));for(int i=0;i<20;i++)progression.TryUpgrade(PlayerUpgrade.Vitality);
        Check(!progression.TryUpgrade(PlayerUpgrade.Vitality)&&progression.UpgradeRank(PlayerUpgrade.Vitality)==20&&progression.AvailableUpgradePoints==79,"Each branch caps at 20 while unspent level points remain valid");
        progression.RestoreSave(upgraded);
        Check(Get<TMPro.TMP_Text>(panel,"summary").text.Contains("HEALTH  115")&&Get<TMPro.TMP_Text>(panel,"summary").text.Contains("ENERGY  110"),"Upgrade menu summary refreshes after effective stats are applied");
        testKey="TopDownShooter3D.Development.Test."+Guid.NewGuid().ToString("N");
        var saveActor=new GameObject("Temporary upgrade save writer");temporary.Add(saveActor);saveActor.SetActive(false);
        var saver=saveActor.AddComponent<PlayerProgressionState>();Set(saver,"saveKey",testKey);saveActor.SetActive(true);saver.RestoreSave(Legacy(4));saver.TryUpgrade(PlayerUpgrade.Firepower);saver.FlushSave();
        string expected=saver.CaptureSave();Destroy(saveActor);yield return null;
        var loadActor=new GameObject("Temporary upgrade save reader");temporary.Add(loadActor);loadActor.SetActive(false);var reader=loadActor.AddComponent<PlayerProgressionState>();Set(reader,"saveKey",testKey);loadActor.SetActive(true);
        Check(reader.CaptureSave()==expected&&reader.AvailableUpgradePoints==2,"A new player instance loads saved upgrade ranks and unspent points");
        yield return Screenshot("PlayerDevelopment_Upgrades");panel.Close();
        float hp=health.CurrentHealth;health.TakeDamage(20);Check(Mathf.Abs(hp-health.CurrentHealth-19.8f)<.01f,"Upgraded resistance reduces real incoming damage");
        progression.RestoreSave(Legacy(1));RefillEnergy();
        yield return MovementChecks();
        yield return GunChecks();
        Check(errors.Count==0,"No runtime errors in upgrades, saves, movement, animation, sprint or shooting");
    }
    private IEnumerator MovementChecks()
    {
        Warp(origin);Keys(Key.W);yield return Wait(.15f);
        Check(cc.velocity.z>4.5f,"Movement reaches normal running speed within 150 ms");
        foreach(var pair in new[]{(Key.D,Vector3.right),(Key.S,Vector3.back),(Key.A,Vector3.left),(Key.W,Vector3.forward)})
        {
            Vector3 before=movement.transform.position;Keys(pair.Item1);yield return Wait(.075f);
            Vector3 delta=Vector3.ProjectOnPlane(movement.transform.position-before,Vector3.up);
            float magnitude=new Vector2(animator.GetFloat("MoveX"),animator.GetFloat("MoveZ")).magnitude;
            Check(delta.magnitude>.2f&&Vector3.Dot(delta.normalized,pair.Item2)>.8f,"Quick turn "+pair.Item1+" follows input without braking to a stop");
            Check(magnitude>.97f&&animator.GetFloat("MoveSpeed")>.55f,"Quick turn "+pair.Item1+" keeps a directional running gait instead of idle blending");
            yield return Wait(.1f);
        }
        Keys();yield return Wait(.15f);Check(Vector3.ProjectOnPlane(cc.velocity,Vector3.up).magnitude<.05f,"Releasing movement stops promptly");
        RefillEnergy();Warp(origin+Vector3.back*15);Keys(Key.W,Key.LeftShift);yield return Wait(.7f);
        Check(movement.IsSprinting&&cc.velocity.z>9&&energy.CurrentEnergy<95,"Sprint uses its faster gait and drains shared energy");
        Check(animator.GetFloat("LocomotionRate")>1.2f,"Sprint animation cadence scales to the authored 10 m/s travel speed");
        yield return Wait(5.3f);Check(!movement.IsSprinting&&cc.velocity.z>4.5f&&cc.velocity.z<5.5f,"Exhausted sprint falls back to responsive running");
        yield return Wait(1.1f);Check(!movement.IsSprinting,"Holding Shift after exhaustion does not repeatedly restart sprint");Keys();yield return Wait(.2f);
        progression.RestoreSave(Legacy(6));for(int i=0;i<5;i++)progression.TryUpgrade(PlayerUpgrade.Endurance);RefillEnergy();
        energy.ConsumeSprintEnergy(6);Check(energy.CurrentEnergy>40,"Five endurance ranks keep enough energy for more than six seconds of sprinting");
        progression.RestoreSave(Legacy(1));RefillEnergy();Warp(origin);Keys(Key.M);yield return Wait(.85f);
        float baseSpent=100-energy.CurrentEnergy;Check(movement.IsShadowDashing&&baseSpent>20,"Real transformation drains the baseline energy budget");Keys();yield return Wait(.7f);
        progression.RestoreSave(Legacy(6));for(int i=0;i<5;i++)progression.TryUpgrade(PlayerUpgrade.Transformation);RefillEnergy();Warp(origin);Keys(Key.M);yield return Wait(.85f);
        float improvedSpent=100-energy.CurrentEnergy;Check(movement.IsShadowDashing&&improvedSpent<baseSpent*.8f,"Transformation upgrades prolong the actual held form by reducing drain");Keys();yield return Wait(.8f);
        progression.RestoreSave(Legacy(1));RefillEnergy();
        var ramp=Cube("Temporary 20 degree ramp",new Vector3(500,11.5f,512),new Vector3(10,.5f,14));ramp.transform.rotation=Quaternion.Euler(20,0,0);Physics.SyncTransforms();
        Vector3 rampTop=Surface(new Vector3(500,0,507));Warp(rampTop+Vector3.up*.04f);Keys(Key.W);
        float maxGap=0;int groundedFrames=0,total=0;float until=Time.realtimeSinceStartup+1.65f;
        while(Time.realtimeSinceStartup<until)
        {
            yield return null;maxGap=Mathf.Max(maxGap,movement.transform.position.y-Surface(movement.transform.position).y);total++;if(cc.isGrounded)groundedFrames++;
        }
        Check(maxGap<.16f&&groundedFrames>=total*.9f,"Descending a 20-degree ramp remains grounded (max gap="+maxGap.ToString("F3")+"m)");
        Keys(Key.S);maxGap=0;until=Time.realtimeSinceStartup+1.35f;
        while(Time.realtimeSinceStartup<until){yield return null;maxGap=Mathf.Max(maxGap,movement.transform.position.y-Surface(movement.transform.position).y);}
        Check(maxGap<.16f,"Ascending the same ramp tracks the surface without hovering");Keys();yield return Wait(.2f);
        for(int i=0;i<5;i++){float h=(i+1)*.18f;Cube("Temporary stair "+i,new Vector3(500,10.5f+h*.5f,529+i*2),new Vector3(10,h,2));}
        Physics.SyncTransforms();Warp(Surface(new Vector3(500,0,537))+Vector3.up*.04f);Keys(Key.S);maxGap=0;until=Time.realtimeSinceStartup+2.25f;
        while(Time.realtimeSinceStartup<until){yield return null;maxGap=Mathf.Max(maxGap,movement.transform.position.y-Surface(movement.transform.position).y);}
        Check(maxGap<.25f&&cc.isGrounded,"Small descending steps stay attached to the ground (max gap="+maxGap.ToString("F3")+"m)");Keys();yield return Wait(.15f);
        Cube("Temporary tall ledge",new Vector3(530,12,500),new Vector3(5,3,5));Warp(new Vector3(530,13.54f,500));Keys(Key.W);yield return Wait(.7f);
        Check(movement.transform.position.y>10.8f&&movement.transform.position.y<13.4f,"Large ledges use real falling gravity rather than ground snapping through the drop");
        yield return Wait(.55f);Keys();Check(Mathf.Abs(movement.transform.position.y-10.5f)<.1f,"The player lands cleanly on the lower floor");
        Warp(origin);yield return Wait(.2f);float y=movement.transform.position.y;yield return Wait(.4f);Check(Mathf.Abs(y-movement.transform.position.y)<.01f,"Standing ground contact does not jitter vertically");
    }
    private IEnumerator GunChecks()
    {
        progression.RestoreSave(Legacy(1));Warp(origin);yield return Wait(.2f);
        var target=new GameObject("Temporary burst target");temporary.Add(target);target.SetActive(false);target.layer=6;
        var collider=target.AddComponent<CapsuleCollider>();collider.height=2;collider.radius=.5f;collider.center=Vector3.up;
        var enemyHealth=target.AddComponent<EnemyHealth>();Set(enemyHealth,"maxHealth",10000f);var targetable=target.AddComponent<Targetable>();
        var aim=new GameObject("Aim Point").transform;aim.SetParent(target.transform,false);aim.localPosition=Vector3.up*1.4f;Set(targetable,"aimPoint",aim);
        target.transform.position=origin+Vector3.forward*7;target.SetActive(true);Physics.SyncTransforms();weapon.ProjectileFired+=Shot;
        shotTimes.Clear();shotDamage.Clear();Keys(Key.J);yield return Until(()=>shotTimes.Count>=6,6);Keys();yield return Wait(.5f);
        Check(shotTimes.Count==6,"Held attack emits exactly two committed three-round bursts");
        float gap=shotTimes[3]-shotTimes[2],within=shotTimes[1]-shotTimes[0];
        Check(within>=.19f&&within<.32f&&gap>=.79f&&gap>within*3,"Baseline shot timing separates a three-shot burst from the longer recovery");
        Check(shotDamage.All(d=>Mathf.Abs(d-8)<.001f)&&Mathf.Abs(enemyHealth.CurrentHealth-9952)<.01f,"All six real projectiles hit for the weaker baseline damage");
        progression.RestoreSave(Legacy(11));for(int i=0;i<5;i++){progression.TryUpgrade(PlayerUpgrade.Firepower);progression.TryUpgrade(PlayerUpgrade.FireRate);}
        shotTimes.Clear();shotDamage.Clear();Keys(Key.J);yield return Until(()=>shotTimes.Count>=6,5);Keys();yield return Wait(.5f);
        Check(shotTimes.Count==6&&shotTimes[3]-shotTimes[2]<gap*.9f&&shotTimes[1]-shotTimes[0]<within*.95f,"Cadence upgrades shorten actual shot intervals and burst recovery");
        Check(shotDamage.All(d=>d>12)&&enemyHealth.CurrentHealth<9876,"Firepower increases the damage carried by real projectiles");
        shotTimes.Clear();Keys(Key.J);yield return Until(()=>shotTimes.Count>=1,3);Keys();yield return Wait(.12f);int stopped=shotTimes.Count;yield return Wait(.5f);
        Check(shotTimes.Count==stopped&&stopped==1,"Releasing attack cancels the unfinished burst instead of queuing bullets");
        Keys(Key.J);yield return Until(()=>shotTimes.Count>=2,3);var inventory=FindAnyObjectByType<InventoryPanelUI>();inventory.TryOpen();int pausedShots=shotTimes.Count;yield return Wait(.3f);
        Check(shotTimes.Count==pausedShots,"Opening inventory cannot fire queued burst rounds while paused");Keys();inventory.Close();yield return Wait(.4f);
        Check(shotTimes.Count==pausedShots,"Closing inventory without a fresh press does not leak a queued shot");weapon.ProjectileFired-=Shot;Destroy(target);
    }
    private void Shot(Projectile p){shotTimes.Add(Time.time);shotDamage.Add(Get<float>(p,"damage"));}
    private GameObject Cube(string name,Vector3 position,Vector3 size)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(go);go.name=name;go.layer=7;go.transform.position=position;go.transform.localScale=size;Physics.SyncTransforms();return go;}
    private Vector3 Surface(Vector3 point)
    {Physics.Raycast(new Vector3(point.x,50,point.z),Vector3.down,out var hit,100,1<<7,QueryTriggerInteraction.Ignore);return hit.point;}
    private void RefillEnergy()=>typeof(PlayerEnergy).GetMethod("SetEnergy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(energy,new object[]{energy.MaxEnergy});
    private void Warp(Vector3 point){Keys();cc.enabled=false;movement.transform.SetPositionAndRotation(point,Quaternion.identity);cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> predicate,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Screenshot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/"+name+".png");yield return Wait(.15f);}
    private void Check(bool value,string label){passed&=value;report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/PlayerDevelopmentPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}foreach(var e in errors){passed=false;report.AppendLine("RUNTIME ERROR "+e);}
        Keys();if(panel!=null)panel.Close();var inventory=FindAnyObjectByType<InventoryPanelUI>();if(inventory!=null)inventory.Close();
        if(progression!=null&&saveBefore!=null){progression.RestoreSave(saveBefore);Set(progression,"persistProgress",persistBefore);}
        if(weapon!=null)weapon.ProjectileFired-=Shot;
        if(input!=null){input.actions.devices=devicesBefore;input.neverAutoSwitchControlSchemes=autoSwitchBefore;}
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        foreach(var obj in temporary)if(obj!=null)Destroy(obj);
        if(testKey!=null){PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();}
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/PlayerDevelopmentPlaytest.txt",report.ToString());Debug.Log("Player development test complete. Passed="+passed);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Development test interrupted."));}
}
#endif
