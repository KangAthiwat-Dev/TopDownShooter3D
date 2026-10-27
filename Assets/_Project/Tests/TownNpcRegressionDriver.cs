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

[DefaultExecutionOrder(-1000)]
public sealed class TownNpcRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private TownNpcPopulation population;
    private TownNpcBrain a, b, c;
    private EnemyAI enemy;
    private GameObject wall;
    private PlayerMovement player;
    private GamePauseController pause;
    private float oldScale;
    private bool oldBackground, done, passed = true;
    private int oldFps, oldVsync;
    private string runtimeError;
    private TownNpcActivityPoint[] originalCity, originalOutside;
    private IEnumerator Start()
    {
        oldScale = Time.timeScale; oldBackground = Application.runInBackground; oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
        Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        Application.logMessageReceived += OnLog; report.AppendLine("Actual city NPC / enemy / respawn regression " + DateTime.Now.ToString("s"));
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            object next = null; Exception error = null;
            try { if (!stack.Peek().MoveNext()) stack.Pop(); else next = stack.Peek().Current; }
            catch (Exception e) { error = e; }
            if (error != null) { Finish(error); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else if (next != null || stack.Count > 0) yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach (var spawner in FindObjectsByType<EnemyWaveSpawner>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var hostile in FindObjectsByType<EnemyAI>()) hostile.gameObject.SetActive(false);
        player = FindFirstObjectByType<PlayerMovement>(); var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = new Vector3(1000, 20, 1000); cc.enabled = true;
        player.enabled = false;
        population = FindFirstObjectByType<TownNpcPopulation>();
        originalCity = population.CityPoints.ToArray(); originalOutside = population.OutsidePoints.ToArray();
        yield return Wait(1.2f);
        Check(population.AliveCount == 12 && CivilianHealth.Living.Count == 12, "Initial population fills exactly 12 living slots");
        Check(FindObjectsByType<NpcIdleLook>().Count(n => n.NpcId == "armory_merchant_01") == 1, "Existing named weapon merchant remains separate");
        var people = CivilianHealth.Living.Select(h => h.GetComponent<TownNpcBrain>()).ToArray();
        Check(people.All(p => p.Agent.isOnNavMesh && population.Contains(p.transform.position)), "All citizens start on supported city navigation");
        Check(people.All(p => p.GetComponent<Targetable>() == null && p.GetComponent<EnemyHealth>() == null && p.gameObject.layer == 0), "Citizens remain neutral and are not player auto-targets");
        Check(people.SelectMany(p => p.GetComponentsInChildren<SkinnedMeshRenderer>()).SelectMany(s => s.sharedMaterials).Where(m => m.name.Contains("_Cloth_")).Distinct().Count() >= 2, "Population uses multiple clothing palettes");
        Check(population.RuntimePropObstacles > 0, "Unbaked grounded city props get runtime-only navigation blockers");
        var fountain = GameObject.Find("SM_VK_Prop_Fountain");
        Check(fountain != null && !NavMesh.SamplePosition(new Vector3(fountain.transform.position.x,5.17f,fountain.transform.position.z),out _, .2f,NavMesh.AllAreas), "The user's colliderless fountain footprint is no longer walkable");
        int initialSpawned = population.SpawnedTotal;
        var positions = people.ToDictionary(p => p, p => p.transform.position);
        Time.timeScale = 2f; yield return Wait(4f); Time.timeScale = 1f;
        Check(people.Count(p => Vector3.Distance(positions[p], p.transform.position) > 1f) >= 6, "Citizens naturally choose routes and move while workers also have stationary work phases");
        Check(people.All(p => !Physics.CheckCapsule(p.transform.position+Vector3.up*.5f,p.transform.position+Vector3.up*1.4f,.23f,1<<7,QueryTriggerInteraction.Ignore)), "Natural routes keep citizen bodies outside solid city props");
        Check(population.SpawnedTotal == initialSpawned && population.AliveCount == 12, "Healthy population does not keep spawning more citizens");
        Check(population.AwayCount <= 2 && people.Count(p => population.Contains(p.transform.position)) >= 10, "Natural routine keeps the majority in town");

        var townsfolk = people.Where(p => !p.IsConstructionWorker).ToArray(); a = townsfolk[0]; b = townsfolk[1]; c = townsfolk[2];
        foreach (var p in people) { p.enabled = false; if (p != a && p != b && p != c) p.gameObject.SetActive(false); }
        foreach (var p in new[] { a, b, c }) { p.Configure(population, false); p.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate; p.enabled = true; Invoke(p, "Idle"); }
        Warp(a, new Vector3(75.76f, 5.17f, -78)); Warp(b, new Vector3(77.7f, 5.17f, -75)); Warp(c, new Vector3(80, 5.17f, -74.5f));
        Set(a, "socialCooldown", 0f); Set(b, "socialCooldown", 0f); Set(c, "socialCooldown", 0f);
        var north = originalCity[4];
        Check(a.TryVisit(north), "Public activity request chooses a complete resident path");
        var animator = a.GetComponentInChildren<Animator>(); var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        yield return Wait(.4f); Vector3 startFoot = a.transform.InverseTransformPoint(foot.position); Vector3 start = a.transform.position;
        yield return Wait(.7f);
        Check(a.Agent.velocity.magnitude > 1.3f && a.Agent.velocity.magnitude < 1.9f && Vector3.Distance(start, a.transform.position) > .7f, "Walking animation has genuine 1.65 m/s navigation movement");
        Check(Vector3.Distance(startFoot, a.transform.InverseTransformPoint(foot.position)) > .015f && animator.GetFloat("Speed") > 1f, "Humanoid walk actively animates the feet, root motion stays disabled");
        Check(a.TryVisit(originalCity[6], true), "Citizen can choose an occasional running route"); yield return Wait(1f);
        Check(a.Agent.velocity.magnitude > 3.5f && animator.GetFloat("Speed") > 3f, "Running travels faster than walking and reaches the run blend");

        Invoke(a, "Idle"); Invoke(b, "Idle"); Warp(a, new Vector3(75.76f,5.17f,-75)); Warp(b, new Vector3(77.7f,5.17f,-75));
        Set(a, "socialCooldown", 0f); Set(b, "socialCooldown", 0f);
        Check(a.TryTalkWith(b), "Two nearby citizens reserve a conversation together"); yield return Wait(.4f);
        Vector3 hand = a.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position); yield return Wait(.55f);
        Check(a.State == TownNpcState.Talking && b.State == TownNpcState.Talking && animator.GetCurrentAnimatorStateInfo(0).IsName("Talking"), "Both partners play Idle_Talking_Loop and stop walking");
        Check(Vector3.Distance(hand, a.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position)) > .01f && a.Agent.velocity.magnitude < .1f, "Talking gesture deforms the rig without sliding");
        Check(Vector3.Dot(a.transform.forward, (b.transform.position - a.transform.position).normalized) > .9f, "Conversation partners face one another");
        Capture("Logs/TownNpc_Talking.png", new Vector3(75.8f,7.7f,-81), new Vector3(76.7f,6.2f,-75));
        var interaction = originalCity.First(p => p.Activity == TownNpcActivityKind.Interact);
        Warp(a, interaction.transform.position + Vector3.back * 1.2f);
        Check(a.TryVisit(interaction), "Citizen can leave a conversation to visit a prop activity");
        yield return Until(() => a.State == TownNpcState.Interacting, 4f);
        Check(a.State == TownNpcState.Interacting && b.State != TownNpcState.Talking, "Arrival plays Interact and releases the conversation partner");
        yield return Wait(.3f); Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Interact"), "Actual Interact animation state is active");

        // Isolate one citizen for enemy cone / occlusion / memory checks, on the real main street.
        b.gameObject.SetActive(false); c.gameObject.SetActive(false); Invoke(a, "Idle"); a.enabled = false;
        Warp(a, new Vector3(75.76f,5.17f,-77));
        SpawnEnemy(new Vector3(75.76f,5.17f,-74), Quaternion.identity, false);
        Set(enemy, "rotationSpeed", 0f); Set(enemy, "patrolSpeed", 0f); yield return Wait(.45f);
        Check(enemy.Target != a.transform && !enemy.CanSeeTarget, "Citizen behind an unaware enemy is not detected");
        DestroyEnemy();
        SpawnEnemy(new Vector3(75.76f,5.17f,-83), Quaternion.identity, false); Set(enemy, "rotationSpeed", 0f); Set(enemy, "patrolSpeed", 0f);
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Temporary NPC vision wall"; wall.layer = 7;
        wall.transform.position = new Vector3(75.76f,6.7f,-80); wall.transform.localScale = new Vector3(5,3,.4f); Physics.SyncTransforms();
        yield return Wait(.5f); Check(enemy.Target != a.transform, "Solid wall prevents acquiring a citizen in front");
        wall.SetActive(false); yield return Wait(.5f);
        Check(enemy.Target == a.transform && enemy.State == EnemyAwarenessState.Chase && enemy.CanSeeTarget, "Visible frontal citizen becomes a real enemy chase target");
        Vector3 remembered = enemy.LastSeenPosition; Warp(a, enemy.transform.position + Vector3.back * 4f); yield return Wait(.15f);
        Check(enemy.HasTargetMemory && !enemy.CanSeeTarget && Vector3.Distance(remembered, enemy.LastSeenPosition) < .01f, "Citizen moving behind the enemy retains last-seen memory without live position leakage");
        DestroyEnemy();

        Warp(a, new Vector3(75.76f,5.17f,-77)); a.enabled = true; Invoke(a, "Idle");
        SpawnEnemy(new Vector3(75.76f,5.17f,-83), Quaternion.identity, false);
        start = a.transform.position; yield return Wait(1.1f);
        Check(a.State == TownNpcState.Fleeing && a.Threat != null && a.Agent.speed > 4.9f && Vector3.Distance(start, a.transform.position) > 2f, "Visible danger interrupts routines and citizen runs away on actual navigation");
        Check(enemy.Target == a.transform && enemy.HasTargetMemory, "Enemy follows the fleeing citizen rather than an invisible distant player");
        Capture("Logs/TownNpc_Flee.png", a.transform.position + new Vector3(-5,7,-8), a.transform.position + Vector3.up);
        DestroyEnemy(); yield return Wait(4.7f);
        Check(a.State != TownNpcState.Fleeing && population.Contains(a.transform.position), "After safety hysteresis the resident resumes town life without leaving the city");

        // Real delayed EnemyAttack strike, death animation, idempotent replacement and cap.
        Set(population, "respawnDelay", 1.4f); Invoke(a, "Idle"); a.enabled = false; Warp(a, new Vector3(75.76f,5.17f,-77));
        var hp = a.GetComponent<CivilianHealth>(); hp.TakeDamage(hp.CurrentHealth - 10f); int deaths = 0; hp.Died += _ => deaths++;
        int before = population.SpawnedTotal;
        SpawnEnemy(a.transform.position + Vector3.back * 1.2f, Quaternion.identity, true);
        yield return Wait(.16f); Check(hp.IsAlive, "Enemy melee respects its wind-up before applying damage");
        yield return Until(() => !hp.IsAlive, 2f);
        Check(!hp.IsAlive && a.State == TownNpcState.Dead && !a.Agent.enabled && !a.GetComponent<Collider>().enabled, "Timed enemy damage kills the citizen, stops navigation and disables corpse collision");
        yield return Wait(.2f); Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Humanoid death animation plays before corpse cleanup");
        Check(population.AliveCount == 11, "Death immediately frees exactly one living slot");
        hp.TakeDamage(100f); Check(deaths == 1, "Repeated damage cannot schedule a second death/replacement"); DestroyEnemy();
        yield return Wait(1.8f);
        Check(population.AliveCount == 12 && population.SpawnedTotal == before + 1, "Exactly one delayed replacement fills the dead citizen's slot");
        a = CivilianHealth.Living.Select(h => h.GetComponent<TownNpcBrain>()).First(p => !p.IsConstructionWorker);
        b.gameObject.SetActive(true); c.gameObject.SetActive(true);
        foreach (var p in new[] { a,b,c }) { p.enabled = true; Invoke(p, "Idle"); p.Configure(population, true); Set(p,"outsideStaySeconds",new Vector2(20,20)); }

        // Only the reserved minority can use the REAL gate connection to leave and return.
        Set(population, "outsidePoints", new[] { originalOutside[0] }); Set(population, "cityPoints", new[] { originalCity[3] });
        Warp(a,new Vector3(75.76f,5.17f,-53.5f)); Warp(b,new Vector3(77.3f,5.17f,-54)); Warp(c,new Vector3(73.8f,5.17f,-54));
        Check(!c.CanReach(originalOutside[0].transform.position, false), "Resident-only path validation rejects outside destinations");
        Check(a.TryBeginOuting() && b.TryBeginOuting(), "Two selected citizens obtain finite outing permits");
        Check(!c.TryBeginOuting() && population.AwayCount == 2, "A third simultaneous outing is denied"); c.Configure(population,false);
        Time.timeScale = 2f; yield return Until(() => !population.Contains(a.transform.position) && !population.Contains(b.transform.position), 10f);
        Check(!population.Contains(a.transform.position) && !population.Contains(b.transform.position) && population.AwayCount == 2, "Permitted citizens actually walk through the gate onto outside navigation");
        Invoke(a,"ReturnHome"); Invoke(b,"ReturnHome"); yield return Until(() => population.AwayCount == 0, 10f); Time.timeScale = 1f;
        Check(population.AwayCount == 0 && population.Contains(a.transform.position) && population.Contains(b.transform.position), "Both return home and release their outing permits");
        Set(population,"cityPoints",originalCity); Set(population,"outsidePoints",originalOutside);

        // Production pause freezes animation, navigation, threat timers and death replacement.
        Invoke(a,"Idle"); Invoke(b,"Idle"); Warp(a,new Vector3(75.76f,5.17f,-75)); Warp(b,new Vector3(77.7f,5.17f,-75));
        Set(a,"socialCooldown",0f); Set(b,"socialCooldown",0f); a.TryTalkWith(b); yield return Wait(.3f);
        var pauseAnimator = a.GetComponentInChildren<Animator>(); pauseAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        pause = FindFirstObjectByType<GamePauseController>(); c.GetComponent<CivilianHealth>().TakeDamage(100f); int pausedCount = population.SpawnedTotal;
        pause.SetPaused(GamePauseReason.Inventory,true); yield return null; yield return null;
        float animTime = pauseAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime; Vector3 pausePosition = a.transform.position;
        yield return Wait(.6f);
        Check(Time.timeScale == 0 && population.SpawnedTotal == pausedCount && Vector3.Distance(a.transform.position,pausePosition) < .001f && Mathf.Abs(pauseAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-animTime) < .001f, "Inventory pause freezes civilian animation/movement and delays pending respawns");
        pause.SetPaused(GamePauseReason.Inventory,false); yield return Wait(1.8f);
        Check(population.AliveCount == 12 && population.SpawnedTotal == pausedCount+1, "Replacement resumes once after inventory closes");
        foreach (var p in population.GetComponentsInChildren<TownNpcBrain>(true)) if (p.GetComponent<CivilianHealth>().IsAlive) { p.gameObject.SetActive(true); p.enabled = true; }
        Capture("Logs/TownNpc_City.png",new Vector3(75.76f,25,-102),new Vector3(75.76f,5.7f,-72));
        Check(runtimeError == null, "No runtime errors through population, social, enemy, death, outing or pause checks");
    }
    private void SpawnEnemy(Vector3 point, Quaternion rotation, bool attack)
    {
        if(!NavMesh.SamplePosition(point,out var hit,1.5f,NavMesh.AllAreas))throw new InvalidOperationException("No enemy fixture navigation at "+point);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Imp_Green.prefab");
        enemy=Instantiate(prefab,hit.position,rotation).GetComponent<EnemyAI>(); enemy.GetComponent<EnemyAttack>().enabled=attack;
    }
    private void DestroyEnemy() { if(enemy!=null) DestroyImmediate(enemy.gameObject); enemy=null; }
    private static void Warp(TownNpcBrain actor,Vector3 requested)
    {
        if(!NavMesh.SamplePosition(requested,out var hit,1.5f,NavMesh.AllAreas)||!actor.Agent.Warp(hit.position)) throw new InvalidOperationException("Cannot warp citizen to "+requested);
        actor.Agent.isStopped=true; actor.Agent.ResetPath(); actor.Agent.velocity=Vector3.zero; Physics.SyncTransforms();
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    private static IEnumerator Wait(float seconds) { float until=Time.realtimeSinceStartup+seconds; while(Time.realtimeSinceStartup<until) yield return null; }
    private static IEnumerator Until(Func<bool> condition,float seconds) { float until=Time.realtimeSinceStartup+seconds; while(!condition()&&Time.realtimeSinceStartup<until) yield return null; }
    private void Check(bool value,string label) { passed &= value; report.AppendLine((value?"PASS ":"FAIL ")+label); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TownNpcPlaytest.txt",report.ToString()); }
    private void OnLog(string message,string trace,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) runtimeError=message; }
    private void Capture(string path,Vector3 position,Vector3 target)
    {
        var go=new GameObject("Temporary townsfolk capture"); var camera=go.AddComponent<Camera>(); camera.transform.position=position; camera.transform.LookAt(target); camera.fieldOfView=45; camera.nearClipPlane=.1f;
        var rt=RenderTexture.GetTemporary(1500,1000,24); var previous=RenderTexture.active; var image=new Texture2D(1500,1000,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1500,1000),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Destroy(image); Destroy(go); }
    }
    private void Finish(Exception error)
    {
        if(done)return; done=true; if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);} if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(pause!=null)pause.SetPaused(GamePauseReason.Inventory,false); Time.timeScale=oldScale;
        if(population!=null&&originalCity!=null){Set(population,"cityPoints",originalCity);Set(population,"outsidePoints",originalOutside);}
        DestroyEnemy(); if(wall!=null)Destroy(wall); if(player!=null){player.enabled=true;FindFirstObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();}
        Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TownNpcPlaytest.txt",report.ToString());
        if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Town NPC test interrupted"));}
}
#endif
