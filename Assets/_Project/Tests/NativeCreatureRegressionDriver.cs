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
using UnityEngine.InputSystem.Utilities;

public sealed class NativeCreatureRegressionDriver:MonoBehaviour
{
    private const string RifleFolder="Assets/_Project/Weapons/SpaceRifle";
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private readonly Vector3 origin=new Vector3(2000,20,2000);
    private GameObject fixture;private NavMeshData navData;private NavMeshDataInstance navigation;private CreatureDamageTarget victim;
    private PlayerMovement player;private PlayerGoldWallet wallet;private string walletBefore;private Vector3 playerPosition;private Quaternion playerRotation;
    private PlayerInput input;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? previousDevices;private bool previousSwitch,movementBefore;
    private Camera captureCamera;private CameraFollow cameraFollow;private bool followBefore;private Vector3 cameraPosition;private Quaternion cameraRotation;
    private bool done,backgroundBefore;private int fpsBefore,vsyncBefore;private float timeBefore;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private static GameObject Prefab(string kind)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_"+kind+".prefab");
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Native Wolf Boss, Giant Spider, wizard and retained rifle — "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;
            try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception error){failure=error;}
            if(failure!=null){Finish(failure);yield break;}
            if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        yield return null;
        player=FindAnyObjectByType<PlayerMovement>();playerPosition=player.transform.position;playerRotation=player.transform.rotation;movementBefore=player.enabled;
        wallet=player.GetComponent<PlayerGoldWallet>();walletBefore=wallet.CaptureSave();
        Check(new[]{"Assets/Assets_Imported/alphaWolf","Assets/_Project/Enemies/StoryCreatures/AlphaWolf","Assets/Spiders","Assets/_Project/Enemies/Creatures/Spider","Assets/Assets_Imported/SciFi_Space_Soldier"}.All(p=>!Directory.Exists(p)),"Retired wolf, spider and Space Soldier source assets are removed");
        var dependencies=AssetDatabase.GetDependencies("Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab",true);
        Check(dependencies.Any(p=>p.StartsWith(RifleFolder)&&p.EndsWith(".FBX"))&&!dependencies.Any(p=>p.Contains("SciFi_Space_Soldier")),"Current human player resolves its rifle mesh and textures from the retained rifle folder");
        var wizard=FindObjectsByType<StoryActor>(FindObjectsSortMode.None).Single(a=>a.ActorId=="elder_wizard");
        Check(wizard.GetComponentInChildren<Animator>().isHuman&&wizard.GetComponentInChildren<Animator>().avatar.isValid&&wizard.GetComponent<NpcIdleLook>()!=null&&wizard.GetComponent<EnemyHealth>()==null,"Elder wizard is an animated, non-combat existing-NPC placeholder");
        Check(wizard.GetComponentsInChildren<Transform>().Any(t=>t.name=="Temporary Wizard Staff")&&wizard.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader.isSupported),"Wizard staff, white hair/beard and materials resolve");
        Check(NavMesh.SamplePosition(wizard.transform.position,out var wizardGround,1,NavMesh.AllAreas)&&Mathf.Abs(wizardGround.position.y-wizard.transform.position.y)<.2f,"Wizard stands on reachable ground near the summoning point");
        foreach(string kind in new[]{"Spider","AlphaWolf"})
        {
            var prefab=Prefab(kind);var animator=prefab.GetComponentInChildren<Animator>();
            Check(animator!=null&&animator.avatar!=null&&animator.avatar.isValid&&!animator.isHuman&&!animator.applyRootMotion,kind+" has one valid Generic rig with root motion disabled");
            var paths=animator.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,animator.transform)).ToHashSet();
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
            {
                var curves=AnimationUtility.GetCurveBindings(clip);
                Check(curves.Length>0&&curves.All(b=>paths.Contains(b.path)),kind+" animation binds to its own skeleton: "+clip.name);
            }
            Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.mainTexture!=null&&!ShaderUtil.ShaderHasError(m.shader)),kind+" uses textured project-owned URP materials");
            Check(prefab.GetComponent<EnemyLootDropper>()?.Table!=null,kind+" retains the existing loot contract");
        }
        Check(Prefab("Spider").GetComponent<EnemyHealth>().MaxHealth==32&&Get<int>(Prefab("Spider").GetComponent<EnemyAttack>(),"damage")==6,"Spider replacement preserves its balanced HP and damage");
        var voice=Prefab("Spider").GetComponent<EnemyVoice>();Check(voice!=null&&voice.AttackClip!=null&&voice.DeathClip!=null&&voice.Source.spatialBlend==1,"Spider replacement keeps the original 3D attack and death voices");
        var director=FindAnyObjectByType<WorldMonsterDirector>();var species=director.species.Single(s=>s.id=="alpha_wolf");
        Check(species.population==1&&species.habitat==WorldMonsterHabitat.Forest&&species.prefab==Prefab("AlphaWolf"),"One Alpha Wolf slot is wired into the forest population");
        float deadline=Time.realtimeSinceStartup+8;
        WorldMonsterMember[] Wolves()=>FindObjectsByType<WorldMonsterMember>(FindObjectsSortMode.None).Where(m=>m.SpeciesId=="alpha_wolf"&&m.Health.IsAlive).ToArray();
        while(Wolves().Length==0&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(Wolves().Length==1,"Real map population spawns exactly one living native Wolf Boss");
        var first=Wolves()[0];if(first.TryGetComponent<EnemyLootDropper>(out var dropper))DestroyImmediate(dropper);
        foreach(var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))ai.enabled=false;
        director.roamingRespawnDelay=.35f;int notifications=0;Action<WorldMonsterMember> defeated=m=>{if(m==first)notifications++;};director.MonsterDefeated+=defeated;
        first.Health.TakeDamage(99999);first.Health.TakeDamage(99999);deadline=Time.realtimeSinceStartup+6;
        while(!Wolves().Any(m=>m!=first)&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(Wolves().Length==1&&Wolves()[0]!=first&&notifications==1,"Wolf death notifies once and respawns within the population cap");director.MonsterDefeated-=defeated;
        director.gameObject.SetActive(false);foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        fixture=new GameObject("Temporary isolated native-creature fixture");
        var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(60,.2f,60),transform=Matrix4x4.TRS(origin+Vector3.down*.1f,Quaternion.identity,Vector3.one)}};
        navData=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,new Bounds(origin,new Vector3(64,10,64)),Vector3.zero,Quaternion.identity);navigation=NavMesh.AddNavMeshData(navData);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(fixture.transform);floor.transform.position=origin+Vector3.down*.1f;floor.transform.localScale=new Vector3(60,.2f,60);
        var proxy=new GameObject("Creature target proxy");proxy.transform.SetParent(fixture.transform);var controller=proxy.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.center=Vector3.up*.9f;victim=proxy.AddComponent<CreatureDamageTarget>();
        Check(navigation.valid,"Isolated navigation fixture leaves the real map NavMesh untouched");
        foreach(string kind in new[]{"Spider","AlphaWolf"})yield return Creature(kind);
        yield return Rifle();
        Check(wallet.CaptureSave()==walletBefore,"Verification preserves player Gold, items and weapons");Check(errors.Count==0,"No runtime errors during native animations, combat, cleanup or rifle fire");
    }
    private IEnumerator Creature(string kind)
    {
        PlaceVictim(origin+Vector3.forward*100);victim.ResetDamage();
        var actor=Instantiate(Prefab(kind),origin,Quaternion.identity,fixture.transform);if(actor.TryGetComponent<EnemyLootDropper>(out var dropper))DestroyImmediate(dropper);
        var ai=actor.GetComponent<EnemyAI>();Set(ai,"target",victim.transform);Set(ai,"huntCivilians",false);
        var agent=actor.GetComponent<NavMeshAgent>();var animator=actor.GetComponentInChildren<Animator>();var attack=actor.GetComponent<EnemyAttack>();var health=actor.GetComponent<EnemyHealth>();
        var bones=animator.GetComponentsInChildren<Transform>();var rotations=bones.Select(t=>t.localRotation).ToArray();Vector3 start=actor.transform.position;float motion=0,until=Time.realtimeSinceStartup+1.25f;
        while(Time.realtimeSinceStartup<until){yield return null;for(int i=0;i<bones.Length;i++)motion=Mathf.Max(motion,Quaternion.Angle(rotations[i],bones[i].localRotation));}
        Check(agent.isOnNavMesh&&ai.State==EnemyAwarenessState.Patrol&&Vector3.Distance(start,actor.transform.position)>.2f&&motion>2,kind+" patrol moves real geometry with its native animation");
        PlaceVictim(actor.transform.position+actor.transform.forward*10);yield return Wait(.8f);start=actor.transform.position;float began=Time.time;yield return Wait(.3f);
        float speed=Vector3.Distance(start,actor.transform.position)/(Time.time-began);
        Check(ai.CanSeeTarget&&ai.State==EnemyAwarenessState.Chase&&speed>ai.PatrolSpeed*1.5f&&Mathf.Abs(speed-ai.ChaseSpeed)<.8f&&animator.GetFloat("Speed")>.8f,kind+" runs faster than patrol with matching world movement");
        var head=bones.Single(t=>t.name==(kind=="Spider"?"head":"Bip01 Head"));
        report.AppendLine($"{kind} root={actor.transform.position:F3} head={head.position:F3} forward={actor.transform.forward:F3} headDot={Vector3.Dot(head.position-actor.transform.position,actor.transform.forward):F3}");
        Check(Vector3.Dot(head.position-actor.transform.position,actor.transform.forward)>.1f,kind+" model faces the same direction as sight and attacks");
        agent.Warp(origin);actor.transform.rotation=Quaternion.identity;victim.ResetDamage();float reach=Get<float>(ai,"attackRange"),delay=Get<float>(attack,"hitDelay");PlaceVictim(origin+Vector3.forward*reach*.75f);
        yield return Wait(.1f);Check(ai.CanAttackTarget(),kind+" starts close-range combat against a visible target");yield return Wait(delay*.5f);Check(victim.Hits==0,kind+" respects the animation wind-up");
        yield return Wait(delay*.75f+.1f);Check(victim.Hits==1&&Mathf.Approximately(victim.TotalDamage,Get<int>(attack,"damage")),kind+" commits one correctly timed damage hit");
        int deaths=0;health.Died+=()=>deaths++;health.TakeDamage(99999);health.TakeDamage(99999);yield return Wait(.2f);
        Check(deaths==1&&!ai.enabled&&!agent.enabled&&!actor.GetComponent<Targetable>().IsTargetable&&animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),kind+" death stops AI and targeting and plays the matching rig's death once");
        float cleanup=Get<float>(actor.GetComponent<EnemyDeathController>(),"destroyDelay");yield return Wait(cleanup+.2f);Check(actor==null,kind+" corpse cleans up after its death presentation");
    }
    private IEnumerator Rifle()
    {
        PlaceVictim(origin+Vector3.forward*100);
        var target=Instantiate(Prefab("AlphaWolf"),origin+Vector3.forward*8,Quaternion.identity,fixture.transform);target.GetComponent<EnemyAI>().enabled=false;target.GetComponent<EnemyAttack>().enabled=false;target.GetComponent<EnemyHealth>().IsInvulnerable=true;
        player.enabled=true;WarpPlayer(origin);player.transform.rotation=Quaternion.identity;
        captureCamera=Camera.main;cameraFollow=captureCamera.GetComponentInParent<CameraFollow>();cameraPosition=captureCamera.transform.position;cameraRotation=captureCamera.transform.rotation;
        if(cameraFollow!=null){followBefore=cameraFollow.enabled;cameraFollow.enabled=false;}
        captureCamera.transform.position=origin+new Vector3(4,4,-4.5f);captureCamera.transform.LookAt(origin+Vector3.up+Vector3.forward*2.4f);
        input=player.GetComponent<PlayerInput>();previousDevices=input.actions.devices;previousSwitch=input.neverAutoSwitchControlSchemes;input.neverAutoSwitchControlSchemes=true;
        keyboard=InputSystem.AddDevice<Keyboard>("RetainedRifleTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.3f);
        var weapon=player.GetComponent<PlayerRangedWeapon>();int shots=0;Action<Projectile> fired=p=>shots++;weapon.ProjectileFired+=fired;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J));float until=Time.realtimeSinceStartup+4;while(shots==0&&Time.realtimeSinceStartup<until)yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());weapon.ProjectileFired-=fired;
        Check(shots>0&&weapon.MuzzlePoint!=null&&weapon.MuzzlePoint.gameObject.activeInHierarchy,"Real J input still aims and fires the retained rifle after removing Space Soldier");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/RetainedRifle_Play.png",2);yield return Wait(.2f);
    }
    private void PlaceVictim(Vector3 point){var controller=victim.GetComponent<CharacterController>();controller.enabled=false;victim.transform.position=point;controller.enabled=true;Physics.SyncTransforms();}
    private void WarpPlayer(Vector3 point){var controller=player.GetComponent<CharacterController>();bool before=controller.enabled;controller.enabled=false;player.transform.position=point;controller.enabled=before;Physics.SyncTransforms();}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool okay,string label){report.AppendLine((okay?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/NativeCreaturePlaytest.txt",report.ToString());if(!okay)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;
        if(input!=null){input.actions.devices=previousDevices;input.neverAutoSwitchControlSchemes=previousSwitch;}if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        if(player!=null){WarpPlayer(playerPosition);player.transform.rotation=playerRotation;player.enabled=movementBefore;}
        if(captureCamera!=null)captureCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);if(cameraFollow!=null)cameraFollow.enabled=followBefore;
        if(fixture!=null)DestroyImmediate(fixture);if(navigation.valid)navigation.Remove();if(navData!=null)DestroyImmediate(navData);
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        if(failure!=null)report.AppendLine("EXCEPTION "+failure);foreach(var error in errors)report.AppendLine("RUNTIME ERROR "+error);
        bool passed=failure==null&&errors.Count==0;report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/NativeCreaturePlaytest.txt",report.ToString());Debug.Log("Native creatures and retained rifle test complete. Passed="+passed);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Native creature regression interrupted."));}
}
#endif
