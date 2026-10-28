#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed class PlayerSkillsRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerMovement player;private PlayerProgressionState state;private PlayerCombatSkills skills;private PlayerHealth health;
    private GameObject fixture;private readonly Vector3 origin=new Vector3(2000,20,2000);private Keyboard keyboard;private PlayerInput input;
    private int fps,vsync;private bool background,done;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private static Projectile Prefab(string kind)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/CombatSkills/"+kind+".prefab").GetComponent<Projectile>();
    private IEnumerator Start()
    {
        fps=Application.targetFrameRate;vsync=QualitySettings.vSyncCount;background=Application.runInBackground;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.runInBackground=true;
        Application.logMessageReceived+=OnLog;report.AppendLine("Thai skills, physics, combat, VFX and save regression "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private void Profile(params (PlayerUpgrade kind,int rank)[] upgrades)
    {
        var ranks=new int[PlayerProgressionState.UpgradeCount];foreach(var u in upgrades)ranks[(int)u.kind]=u.rank;
        string json="{\"version\":3,\"level\":100,\"experience\":0,\"upgrades\":["+string.Join(",",ranks)+"]}";
        if(!state.RestoreSave(json))throw new InvalidOperationException("Test profile rejected");
    }
    private IEnumerator Checks()
    {
        yield return null;
        player=FindAnyObjectByType<PlayerMovement>();state=player.GetComponent<PlayerProgressionState>();skills=player.GetComponent<PlayerCombatSkills>();health=player.GetComponent<PlayerHealth>();
        Set(state,"persistProgress",false);Set(player.GetComponent<PlayerGoldWallet>(),"persistGold",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        input=player.GetComponent<PlayerInput>();input.neverAutoSwitchControlSchemes=true;keyboard=InputSystem.AddDevice<Keyboard>("CombatSkillsTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};Keys();
        Check(state.RestoreSave("{\"version\":2,\"level\":30,\"experience\":7,\"upgrades\":[2,3,4,5,1]}"),"Original v2 five-upgrade profile migrates without rejection");
        Check(state.Level==30&&state.Experience==7&&state.SpentUpgradePoints==15&&state.AvailableUpgradePoints==14&&state.UpgradeRank(PlayerUpgrade.FireRate)==1&&state.UpgradeRank(PlayerUpgrade.ArcaneBarrage)==0,"Migration preserves level, EXP, five ranks and available points");
        Check(state.CaptureSave().Contains("\"version\":3")&&state.RestoreSave(state.CaptureSave()),"Expanded eleven-rank v3 save roundtrips");
        string valid=state.CaptureSave();Check(!state.RestoreSave("{\"version\":4,\"level\":30,\"experience\":0}")&&state.CaptureSave()==valid,"Unknown save versions leave the valid profile untouched");
        state.RestoreSave("{\"version\":1,\"level\":2,\"experience\":0}");
        Check(state.TryUpgrade(PlayerUpgrade.ExplosiveShot)&&!state.TryUpgrade(PlayerUpgrade.PiercingShot)&&state.AvailableUpgradePoints==0,"Each new skill spends one existing level point and cannot overspend");
        Profile();foreach(PlayerUpgrade kind in new[]{PlayerUpgrade.ArcaneBarrage,PlayerUpgrade.PiercingShot,PlayerUpgrade.ExplosiveShot,PlayerUpgrade.CriticalStrike,PlayerUpgrade.LifeSteal,PlayerUpgrade.ThunderDash})
        {
            for(int i=0;i<PlayerProgressionState.MaximumRank(kind);i++)Check(state.TryUpgrade(kind),"Upgrade "+kind+" rank "+(i+1));
            Check(!state.TryUpgrade(kind),kind+" cap is enforced");
        }
        Check(skills.ProjectilesPerVolley==7&&skills.ExtraPierceTargets==3&&Mathf.Approximately(skills.BlastRadius,2.7f)&&Mathf.Approximately(skills.HitSettings.criticalChance,.25f),"Max new skill ranks produce bounded spread, pierce, blast and critical values");
        Profile((PlayerUpgrade.FireRate,2),(PlayerUpgrade.Vitality,2));
        Check(player.GetComponent<PlayerAttackController>().FireRateMultiplier>1&&health.DamageReduction>0&&health.MaxHealth==130,"Rapid Fire and Arcane Shield reuse the existing cadence and vitality upgrades");
        var ui=FindAnyObjectByType<PlayerUpgradePanelUI>();
        Keys(Key.C);yield return Wait(.15f);Keys();yield return Wait(.1f);
        Check(ui.IsOpen&&Time.timeScale==0&&ui.DisplayedPoints.Contains("แต้มคงเหลือ"),"Real C opens the Thai upgrade UI and pauses gameplay");
        Check(Get<GameObject[]>(ui,"cards").Count(c=>c.activeSelf)==8,"Combat page shows eight requested skills without duplicating existing upgrades");
        yield return Capture("Thai_Combat_Skills");ui.ShowPage(false);yield return Capture("Thai_Base_Upgrades");
        Check(Get<GameObject[]>(ui,"cards").Count(c=>c.activeSelf)==3,"Base page keeps energy, transformation and firepower available");
        ui.ShowPage(true);int available=state.AvailableUpgradePoints;var button=Get<Button[]>(ui,"upgradeButtons")[(int)PlayerUpgrade.ArcaneBarrage];button.onClick.Invoke();
        Check(state.UpgradeRank(PlayerUpgrade.ArcaneBarrage)==1&&state.AvailableUpgradePoints==available-1,"The actual skill-card button applies the upgrade and refreshes points");
        ui.Close();var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.Journal,true);
        Check(!ui.TryOpen(),"Skill window cannot open behind a quest journal or another modal");pause.SetPaused(GamePauseReason.Journal,false);
        foreach(string name in new[]{"ArcaneExplosion","ThunderImpact","SwordSlash","CriticalImpact","LifeSteal"})
        {
            var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/CombatSkills/"+name+".prefab");
            Check(fx!=null&&fx.GetComponent<RangedBurstVfx>()!=null&&fx.GetComponentsInChildren<AudioSource>(true).Length==0&&fx.GetComponentsInChildren<Collider>(true).Length==0&&fx.GetComponentsInChildren<ParticleSystem>(true).All(p=>!p.main.loop),name+" is a silent, bounded visual copy without gameplay colliders");
        }
        fixture=new GameObject("Temporary skills combat fixture");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Fixture floor";floor.layer=7;floor.transform.SetParent(fixture.transform);floor.transform.position=origin+Vector3.down*.1f;floor.transform.localScale=new Vector3(50,.2f,50);
        Warp(origin);player.transform.rotation=Quaternion.identity;yield return Wait(.15f);Profile();
        var camera=Camera.main;var follow=camera.GetComponentInParent<CameraFollow>();if(follow!=null)follow.enabled=false;
        camera.transform.position=origin+new Vector3(5,8,-8);camera.transform.LookAt(origin+Vector3.forward*3+Vector3.up);
        var target=Enemy(new Vector3(0,0,3),100);int crits=0;skills.CriticalHit+=e=>crits++;
        Check(skills.DealDamage(target,20,new CombatHitSettings(1,2,0))&&target.CurrentHealth==60&&crits==1,"Critical Strike applies exactly double damage at an actual committed hit");
        target.RestoreToFullHealth();skills.DealDamage(target,20,new CombatHitSettings(0,2,0));
        Check(target.CurrentHealth==80&&crits==1,"A non-critical hit keeps the original damage");
        health.TakeDamage(30);float before=health.CurrentHealth;target.RestoreToFullHealth();skills.DealDamage(target,999,new CombatHitSettings(0,2,4));skills.DealDamage(target,999,new CombatHitSettings(0,2,4));
        Check(Mathf.Approximately(health.CurrentHealth,before+4),"Life Steal heals once per actual lethal monster hit");
        var dummy=Enemy(new Vector3(2,0,3),10,0);before=health.CurrentHealth;skills.DealDamage(dummy,20,new CombatHitSettings(0,2,4));
        Check(health.CurrentHealth==before,"Training targets cannot be farmed for Life Steal healing");
        yield return ClearEnemies();Warp(origin+Vector3.back*5);
        EnemyHealth a=Enemy(new Vector3(0,0,2)),b=Enemy(new Vector3(0,0,4));Fire(0,0);yield return Wait(.4f);
        Check(a.CurrentHealth==90&&b.CurrentHealth==100,"With no new skills, a bullet damages one enemy and stops as before");
        yield return ClearEnemies();
        a=Enemy(new Vector3(0,0,2));AddCollider(a);b=Enemy(new Vector3(0,0,4));var c=Enemy(new Vector3(0,0,6));var d=Enemy(new Vector3(0,0,8));
        Fire(2,0);yield return Wait(.5f);
        Check(a.CurrentHealth==90&&b.CurrentHealth==90&&c.CurrentHealth==90&&d.CurrentHealth==100,"Piercing hits the configured three enemies, with one hit per enemy even across several colliders");
        yield return ClearEnemies();
        a=Enemy(new Vector3(0,0,2),5);AddCollider(a);b=Enemy(new Vector3(0,0,4),5);AddCollider(b);c=Enemy(new Vector3(0,0,6),5);d=Enemy(new Vector3(0,0,8),5);
        before=health.CurrentHealth;Fire(2,0,4);yield return Wait(.5f);
        Check(!a.IsAlive&&!b.IsAlive&&!c.IsAlive&&d.IsAlive,"A lethal piercing hit still travels past the dead target's other colliders");
        Check(Mathf.Approximately(health.CurrentHealth,Mathf.Min(health.MaxHealth,before+12)),"Actual lethal piercing projectile heals once for each of its three monster kills");
        yield return ClearEnemies();
        a=Enemy(new Vector3(0,0,2));b=Enemy(new Vector3(0,0,4));var wall=Wall(new Vector3(0,1,3),new Vector3(3,2,.15f));Fire(3,0);yield return Wait(.4f);
        Check(a.CurrentHealth==90&&b.CurrentHealth==100,"Piercing bullets stop at a solid wall before a monster behind it");Destroy(wall);yield return ClearEnemies();
        a=Enemy(new Vector3(0,0,2));AddCollider(a);b=Enemy(new Vector3(.9f,0,2));AddCollider(b);c=Enemy(new Vector3(3,0,2));d=Enemy(new Vector3(-1.4f,0,2));
        wall=Wall(new Vector3(-.7f,1,2),new Vector3(.15f,2,3));Fire(0,1.9f);yield return Wait(.25f);
        Check(a.CurrentHealth==90&&Mathf.Approximately(b.CurrentHealth,93.5f),"Explosive Shot deals direct damage once and 65% splash once to a nearby enemy");
        Check(c.CurrentHealth==100&&d.CurrentHealth==100,"Blast radius excludes distant enemies and solid scenery shields nearby enemies");
        Check(FindObjectsByType<RangedBurstVfx>(FindObjectsSortMode.None).Any(v=>v.name.StartsWith("ArcaneExplosion")&&v.GetComponentsInChildren<ParticleSystem>().Any(p=>p.particleCount>0)),"Explosive impact plays the selected cartoon explosion asset");
        yield return Capture("Explosive_Impact");Destroy(wall);yield return ClearEnemies();
        a=Enemy(new Vector3(0,0,2));b=Enemy(new Vector3(0,0,4));c=Enemy(new Vector3(1,0,4));Fire(1,2.7f);yield return Wait(.4f);
        Check(a.CurrentHealth==90&&b.CurrentHealth==90&&Mathf.Approximately(c.CurrentHealth,93.5f),"Piercing plus explosion detonates at the final target, without double-hitting earlier direct targets");
        yield return ClearEnemies();yield return Volley();yield return ClearEnemies();
        Profile((PlayerUpgrade.LifeSteal,1));Warp(origin);player.transform.rotation=Quaternion.identity;
        var equipment=player.GetComponent<PlayerMeleeEquipment>();
        if(!equipment.HasWeapon)
        {
            var sword=AssetDatabase.FindAssets("t:MeleeWeaponDefinition",new[]{"Assets/_Project"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>).First(w=>w.HeldPrefab!=null);
            equipment.RestoreLoadout(new[]{sword,(MeleeWeaponDefinition)null},0);
        }
        a=Enemy(new Vector3(0,0,1.4f),5);yield return Wait(.2f);var melee=player.GetComponent<PlayerMeleeAttack>();int swings=0;melee.SwingCommitted+=armed=>{if(armed)swings++;};
        typeof(PlayerMeleeAttack).GetMethod("HandleMeleeAttack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(melee,new object[]{a.GetComponent<Targetable>()});
        Check(melee.IsAttacking&&a.IsAlive&&swings==0,"Sword damage and slash effect both wait for the animation hit frame");
        yield return Until(()=>swings>0,3);yield return Wait(.05f);
        Check(swings==1&&!a.IsAlive&&FindObjectsByType<RangedBurstVfx>(FindObjectsSortMode.None).Any(v=>v.name.StartsWith("SwordSlash")),"Actual sword animation commits one slash asset and a correctly attributed lethal hit");
        yield return Capture("Sword_Slash");yield return Wait(1.5f);yield return ClearEnemies();
        Profile((PlayerUpgrade.ThunderDash,1));Warp(origin);player.transform.rotation=Quaternion.identity;
        a=Enemy(new Vector3(0,0,1.2f));AddCollider(a);yield return Wait(.1f);Keys(Key.Space);yield return Wait(.04f);Keys();yield return Wait(.35f);
        Check(a.CurrentHealth==88&&player.transform.position.z>a.transform.position.z+.3f,"Real Space Thunder Dash passes the monster and deals exactly one hit across its colliders");
        yield return Capture("Thunder_Dash");yield return Wait(.2f);
        Check(!Physics.GetIgnoreCollision(player.GetComponent<CharacterController>(),a.GetComponent<Collider>()),"Dash restores enemy collisions after the player has passed through");
        yield return ClearEnemies();Warp(origin);player.transform.rotation=Quaternion.identity;yield return Wait(1.2f);
        a=Enemy(new Vector3(0,0,1.6f));wall=Wall(new Vector3(0,1,.7f),new Vector3(3,2,.15f));Keys(Key.Space);yield return Wait(.05f);Keys();yield return Wait(.3f);
        Check(a.CurrentHealth==100&&player.transform.position.z<origin.z+.8f,"Thunder Dash still respects walls and cannot damage a monster through scenery");Destroy(wall);
        yield return Wait(1.6f);
        Check(!FindObjectsByType<RangedBurstVfx>(FindObjectsSortMode.None).Any(),"New slash, explosion and skill effects clean up after their bounded lifetimes");
        Check(errors.Count==0,"No runtime errors during skill upgrades, damage, physics, UI or VFX");
    }
    private IEnumerator Volley()
    {
        Profile((PlayerUpgrade.ArcaneBarrage,3),(PlayerUpgrade.PiercingShot,1),(PlayerUpgrade.ExplosiveShot,1));Warp(origin);player.transform.rotation=Quaternion.identity;
        var enemy=Enemy(new Vector3(0,0,8),1000);enemy.IsInvulnerable=true;yield return Wait(.3f);
        var weapon=player.GetComponent<PlayerRangedWeapon>();int spawned=0,volleys=0;bool aligned=true,correctType=true;var times=new List<float>();
        Action<Projectile> launched=p=>{spawned++;correctType&=p.BlastRadius>0&&p.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Glowing Arcane Core");};
        Action<Projectile> fired=p=>{volleys++;times.Add(Time.time);aligned&=Vector3.Angle(p.GetComponent<Rigidbody>().linearVelocity,weapon.MuzzlePoint.forward)<.05f;};
        weapon.ProjectileSpawned+=launched;weapon.ProjectileFired+=fired;Keys(Key.J);yield return Until(()=>volleys>=4,5);Keys();weapon.ProjectileSpawned-=launched;weapon.ProjectileFired-=fired;
        Check(volleys>=4&&spawned==volleys*7,"Actual J input emits seven spread bullets per volley, with only one cadence/muzzle event");
        Check(aligned&&correctType,"Combined skill volleys use explosive orbs, and their central shot follows the real final muzzle forward");
        Check(times[3]-times[2]>times[1]-times[0]+.3f,"Barrage preserves the existing three-round burst and recovery rhythm");
        yield return Capture("Explosive_Spread_Volley");yield return Wait(.4f);
    }
    private EnemyHealth Enemy(Vector3 offset,float hp=100,int exp=1)
    {
        var obj=new GameObject("Skill test enemy");obj.SetActive(false);obj.layer=6;obj.transform.SetParent(fixture.transform);obj.transform.position=origin+offset;
        var health=obj.AddComponent<EnemyHealth>();Set(health,"maxHealth",hp);Set(health,"experienceReward",exp);
        var box=obj.AddComponent<BoxCollider>();box.center=Vector3.up;box.size=new Vector3(.5f,1.8f,.5f);
        var aim=new GameObject("Aim").transform;aim.SetParent(obj.transform,false);aim.localPosition=Vector3.up;
        var t=obj.AddComponent<Targetable>();Set(t,"aimPoint",aim);
        var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);visual.transform.SetParent(obj.transform,false);visual.transform.localPosition=Vector3.up;Destroy(visual.GetComponent<Collider>());
        obj.SetActive(true);Physics.SyncTransforms();return health;
    }
    private void AddCollider(EnemyHealth enemy)
    {var child=new GameObject("Compound hurtbox"){layer=6};child.transform.SetParent(enemy.transform,false);var box=child.AddComponent<BoxCollider>();box.center=Vector3.up;box.size=new Vector3(.55f,1.7f,.45f);}
    private GameObject Wall(Vector3 offset,Vector3 size)
    {var w=GameObject.CreatePrimitive(PrimitiveType.Cube);w.name="Skill test wall";w.layer=7;w.transform.SetParent(fixture.transform);w.transform.position=origin+offset;w.transform.localScale=size;Physics.SyncTransforms();return w;}
    private Projectile Fire(int pierce,float radius,float healing=0)
    {
        var source=radius>0?Prefab("ExplosiveOrb"):pierce>0?Prefab("PiercingTracer"):AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectiles/PlayerEnergyTracer.prefab").GetComponent<Projectile>();
        var p=Instantiate(source,origin+Vector3.up,Quaternion.identity,fixture.transform);p.Initialize(Vector3.forward,28,10,player.gameObject);p.ConfigureSkills(skills,pierce,radius,.65f,new CombatHitSettings(0,2,healing));return p;
    }
    private IEnumerator ClearEnemies()
    {
        foreach(var e in fixture.GetComponentsInChildren<EnemyHealth>())Destroy(e.gameObject);foreach(var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))Destroy(p.gameObject);yield return null;yield return null;
    }
    private void Warp(Vector3 point){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.position=point;c.enabled=true;Physics.SyncTransforms();}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/SkillTests/"+name+".png");yield return Wait(.1f);}
    private static IEnumerator Until(Func<bool> predicate,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool okay,string label){report.AppendLine((okay?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/PlayerSkillsPlaytest.txt",report.ToString());if(!okay)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(fixture!=null)Destroy(fixture);
        Application.logMessageReceived-=OnLog;Application.targetFrameRate=fps;QualitySettings.vSyncCount=vsync;Application.runInBackground=background;
        if(failure!=null)report.AppendLine("EXCEPTION "+failure);foreach(var error in errors)report.AppendLine("RUNTIME ERROR "+error);
        report.AppendLine(failure==null&&errors.Count==0?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/PlayerSkillsPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Skills regression interrupted"));}
}
#endif
