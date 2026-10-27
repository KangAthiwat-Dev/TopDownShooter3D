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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public sealed class EnemyBalanceDeathRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<GameObject> temporary=new List<GameObject>();
    private readonly List<string> errors=new List<string>();
    private PlayerMovement movement;private PlayerHealth playerHealth;private PlayerProgressionState progression;private PlayerInput input;
    private Keyboard keyboard;private ReadOnlyArray<InputDevice>? devicesBefore;private bool autoSwitchBefore,persistBefore,backgroundBefore,done,passed=true;
    private int fpsBefore,vsyncBefore;private float timeBefore;private string saveBefore;
    private static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/"+name+".prefab");
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Enemy balance / player death regression "+DateTime.Now.ToString("s"));
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
        movement=FindAnyObjectByType<PlayerMovement>();playerHealth=movement.GetComponent<PlayerHealth>();progression=movement.GetComponent<PlayerProgressionState>();input=movement.GetComponent<PlayerInput>();
        saveBefore=progression.CaptureSave();persistBefore=Get<bool>(progression,"persistProgress");Set(progression,"persistProgress",false);
        progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        if(input.actions.devices.HasValue)devicesBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());autoSwitchBefore=input.neverAutoSwitchControlSchemes;input.neverAutoSwitchControlSchemes=true;
        keyboard=InputSystem.AddDevice<Keyboard>("BalanceDeathTest");input.actions.devices=new InputDevice[]{keyboard};Keys();
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);
        foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        movement.enabled=false;
        Check(NavMesh.SamplePosition(new Vector3(-20,4,-65),out var anchor,2,NavMesh.AllAreas),"A valid map clearing is available for real AI tests");
        Warp(anchor.position+Vector3.back*8);yield return Wait(.2f);
        var world=FindAnyObjectByType<MonsterLootWorld>();
        int Units()=>world.Pickups.Where(p=>p!=null&&!p.IsCollected).Sum(p=>p.Amount)+Enumerable.Range(0,PlayerInventory.Capacity).Sum(world.Inventory.GetQuantity);
        foreach(string name in new[]{"Enemy_Spider","Enemy_Imp_Green","Enemy_Imp_Blue","Enemy_Imp_Red","Enemy_Puglin","Enemy_Bear","Enemy_Skeleton","Enemy_GoblinDemon","Enemy_GoblinChief"})
        {
            var prefab=Prefab(name);var go=Instantiate(prefab,anchor.position,Quaternion.identity);temporary.Add(go);Freeze(go);
            var ai=go.GetComponent<EnemyAI>();var hp=go.GetComponent<EnemyHealth>();var attack=go.GetComponent<EnemyAttack>();
            Check(ai.ChaseSpeed>ai.PatrolSpeed&&ai.ChaseSpeed<movement.RunSpeed&&hp.MaxHealth>=32&&hp.MaxHealth<=120,name+" has fair chase speed and normal-monster health");
            Check(Get<int>(attack,"damage")<=14&&Get<float>(attack,"attackCooldown")>=1.5f&&go.GetComponent<EnemyLootDropper>().Table!=null,name+" keeps readable attack timing and its loot table");
            int hits=0;while(hp.IsAlive&&hits<20){hp.TakeDamage(8,progression.gameObject);hits++;}
            Check(!hp.IsAlive&&hits==Mathf.CeilToInt(hp.MaxHealth/8)&&hits>=4&&hits<=15,name+" dies to the expected number of baseline bullets: "+hits);
            string xp=progression.CaptureSave();int units=Units();hp.TakeDamage(999,progression.gameObject);
            Check(progression.CaptureSave()==xp&&Units()==units,name+" does not award EXP or loot twice after death");
            Destroy(go);yield return null;
        }
        Check(Get<float>(Prefab("Enemy_Imp_Blue").GetComponent<EnemyAI>(),"chaseSpeed")==3.8f&&Get<float>(Prefab("Enemy_Puglin").GetComponent<EnemyAI>(),"chaseSpeed")==2.8f,"Former 20 m/s Blue Imp and 8 m/s Puglin overrides are replaced explicitly");
        Check(Prefab("KingOfDemon_Boss").GetComponent<EnemyHealth>().MaxHealth==1200&&Prefab("DemonLord_Boss").GetComponent<EnemyHealth>().MaxHealth==900&&Prefab("DragonNightmare_Guardian").GetComponent<EnemyHealth>().MaxHealth==650,"Boss health remains at its authored encounter values");
        var blue=Instantiate(Prefab("Enemy_Imp_Blue"),anchor.position,Quaternion.identity);temporary.Add(blue);Set(blue.GetComponent<EnemyAI>(),"huntCivilians",false);
        Warp(anchor.position+Vector3.forward*9);yield return Wait(.7f);Vector3 before=blue.transform.position;float speed=blue.GetComponent<NavMeshAgent>().velocity.magnitude;
        yield return Wait(.45f);float travelled=Vector3.Distance(before,blue.transform.position)/.45f;
        Check(blue.GetComponent<EnemyAI>().State==EnemyAwarenessState.Chase&&speed>3&&speed<=3.9f&&travelled>2.8f&&travelled<4.1f,"Blue Imp actually chases at its balanced physical speed");Destroy(blue);yield return null;
        progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        var skeleton=Instantiate(Prefab("Enemy_Skeleton"),anchor.position,Quaternion.identity);temporary.Add(skeleton);Set(skeleton.GetComponent<EnemyAI>(),"huntCivilians",false);
        Warp(anchor.position+Vector3.forward*1.3f);float hpBefore=playerHealth.CurrentHealth;
        yield return Until(()=>playerHealth.CurrentHealth<hpBefore,3);
        Check(Mathf.Abs(hpBefore-playerHealth.CurrentHealth-8)<.01f,"Balanced Skeleton deals its actual reduced melee damage");Destroy(skeleton);yield return null;
        foreach(var drop in world.Pickups.ToArray())if(drop!=null)Destroy(drop.gameObject);yield return null;
        movement.enabled=true;Warp(anchor.position);
        // Put the real character into a sprint/rifle pose, then test death while a menu owns pause.
        var dummy=new GameObject("Temporary death rifle target");temporary.Add(dummy);dummy.SetActive(false);dummy.layer=6;
        var collider=dummy.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=2;dummy.AddComponent<EnemyHealth>();var target=dummy.AddComponent<Targetable>();
        var aim=new GameObject("Aim").transform;aim.SetParent(dummy.transform,false);aim.localPosition=Vector3.up*1.4f;Set(target,"aimPoint",aim);
        dummy.transform.position=anchor.position+Vector3.forward*9;dummy.SetActive(true);Physics.SyncTransforms();
        Keys(Key.W,Key.LeftShift,Key.J);yield return Wait(.25f);
        var inventory=FindAnyObjectByType<InventoryPanelUI>();Check(inventory.TryOpen(),"The inventory can be open before a lethal event");
        var animator=movement.GetComponentInChildren<Animator>();var hips=animator.GetBoneTransform(HumanBodyBones.Hips);float standingHip=hips.position.y;
        int deaths=0;playerHealth.Died+=()=>deaths++;
        var death=movement.GetComponent<PlayerDeathPresentation>();var gameOver=FindAnyObjectByType<GameOverUI>();var panel=Get<GameObject>(gameOver,"gameOverPanel");
        Vector3 root=movement.transform.position;float clock=Time.time;playerHealth.TakeDamage(10000);playerHealth.TakeDamage(10000);
        Check(deaths==1&&!playerHealth.IsAlive&&death.Started&&!movement.enabled&&!movement.GetComponent<PlayerAttackController>().enabled,"Lethal damage starts one death presentation and disables movement/combat immediately");
        Check(Time.timeScale==0&&!inventory.IsOpen&&!panel.activeSelf,"The world pauses and the bag closes while Game Over waits for the fall");
        Check(animator.updateMode==AnimatorUpdateMode.UnscaledTime&&!movement.GetComponent<PlayerRangedWeapon>().enabled,"Only the dying animator advances in real time; pending shooting is disabled");
        yield return Wait(.7f);
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death")&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime>.15f,"The real player reaches and advances the death state despite timeScale zero");
        Check(hips.position.y<standingHip-.15f&&Time.time==clock&&Vector3.Distance(root,movement.transform.position)<.01f,"The body visibly collapses while the player root and game clock remain frozen");
        Check(!panel.activeSelf&&!movement.GetComponent<PlayerAnimationController>().TryPlayPunch(),"No early Game Over overlay or attack can replace the death pose");
        Capture("Player_Death_Falling",movement.transform.position+Vector3.up*.8f);
        yield return Until(()=>panel.activeSelf,death.GameOverDelay+1);
        Check(panel.activeSelf&&Time.timeScale==0&&animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Game Over appears after the non-looping fall and the final pose remains");
        var final=Geometry(animator.gameObject);
        Check(final.max.y<root.y+1.2f&&final.min.y>root.y-.25f,"The final dead body rests near the ground instead of returning upright or sinking");
        Check(EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(panel.transform),"Restart button receives UI focus after the death animation");
        Capture("Player_Death_Final",movement.transform.position+Vector3.up*.5f);
        // Preserve the real profile before exercising the actual restart path.
        progression.RestoreSave(saveBefore);Set(progression,"persistProgress",persistBefore);Keys();input.actions.devices=devicesBefore;input.neverAutoSwitchControlSchemes=autoSwitchBefore;yield return null;
        var scene=SceneManager.GetActiveScene().handle;gameOver.RestartGame();
        yield return Until(()=>SceneManager.GetActiveScene().handle!=scene&&FindAnyObjectByType<PlayerHealth>()!=null,12);yield return Wait(.25f);
        movement=FindAnyObjectByType<PlayerMovement>();playerHealth=movement.GetComponent<PlayerHealth>();progression=movement.GetComponent<PlayerProgressionState>();input=movement.GetComponent<PlayerInput>();
        var freshAnimator=movement.GetComponentInChildren<Animator>();var freshGameOver=FindAnyObjectByType<GameOverUI>();
        Check(playerHealth.IsAlive&&movement.enabled&&movement.GetComponent<PlayerRangedWeapon>().enabled&&movement.GetComponent<PlayerDashVisual>().enabled&&
            !movement.GetComponent<PlayerDeathPresentation>().Started&&freshAnimator.updateMode==AnimatorUpdateMode.Normal&&!Get<GameObject>(freshGameOver,"gameOverPanel").activeSelf&&Time.timeScale==1,"Actual restart restores a live, normal-time player with no stale death, dash or UI flags");
        Check(progression.CaptureSave()==saveBefore,"Restart retains the user's original level and upgrades");
        Check(errors.Count==0,"No runtime errors in balanced combat, loot, death, pause or restart");
    }
    private static void Freeze(GameObject actor)
    {actor.GetComponent<EnemyAI>().enabled=false;actor.GetComponent<EnemyAttack>().enabled=false;var agent=actor.GetComponent<NavMeshAgent>();if(agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}}
    private void Warp(Vector3 position)
    {var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.SetPositionAndRotation(position,Quaternion.identity);cc.enabled=true;Physics.SyncTransforms();}
    private static Bounds Geometry(GameObject model)
    {
        bool first=true;Bounds result=default;
        foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy))
        {
            var mesh=new Mesh();r.BakeMesh(mesh);var b=mesh.bounds;
            for(int i=0;i<8;i++){var p=r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}Destroy(mesh);
        }
        return result;
    }
    private void Capture(string name,Vector3 target)
    {
        var go=new GameObject("Temporary death photo");temporary.Add(go);var camera=go.AddComponent<Camera>();camera.transform.position=target+new Vector3(3,2,5);camera.transform.LookAt(target);
        camera.orthographic=true;camera.orthographicSize=2.1f;camera.nearClipPlane=.1f;camera.farClipPlane=50;
        var rt=RenderTexture.GetTemporary(1200,900,24);var old=RenderTexture.active;var image=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(image);Destroy(go);}
    }
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> condition,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool valid,string label){passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/EnemyBalanceDeathPlaytest.txt",report.ToString());if(!valid)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}foreach(var e in errors){passed=false;report.AppendLine("RUNTIME ERROR "+e);}
        Keys();if(progression!=null&&saveBefore!=null){progression.RestoreSave(saveBefore);Set(progression,"persistProgress",persistBefore);}
        if(input!=null){input.actions.devices=devicesBefore;input.neverAutoSwitchControlSchemes=autoSwitchBefore;}if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        foreach(var go in temporary)if(go!=null)Destroy(go);
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/EnemyBalanceDeathPlaytest.txt",report.ToString());Debug.Log("Enemy balance and player death test complete. Passed="+passed);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Balance/death test interrupted."));}
}
#endif
