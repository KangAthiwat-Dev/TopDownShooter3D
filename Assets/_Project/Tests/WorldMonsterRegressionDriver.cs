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
using UnityEngine.InputSystem.Utilities;

public sealed class WorldMonsterRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<GameObject> temporary=new List<GameObject>();
    private readonly List<string> errors=new List<string>();
    private bool passed=true,done,backgroundBefore;private int fpsBefore,vsyncBefore;
    private float timeBefore;private PlayerProgressionState progression;private string saveBefore;private bool persistBefore;
    private WorldMonsterDirector director;private PlayerMovement movement;
    private PlayerInput input;private ReadOnlyArray<InputDevice>? inputBefore;
    private static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private static object Get(object o,string field)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;
        Application.logMessageReceived+=OnLog;report.AppendLine("World population regression "+DateTime.Now.ToString("s"));
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
    private WorldMonsterMember[] Members()=>FindObjectsByType<WorldMonsterMember>(FindObjectsSortMode.None).Where(m=>m.Health.IsAlive).ToArray();
    private void Freeze(WorldMonsterMember member)
    {
        member.GetComponent<EnemyAI>().enabled=false;member.GetComponent<EnemyAttack>().enabled=false;
        var a=member.GetComponent<NavMeshAgent>();if(a.enabled&&a.isOnNavMesh){a.isStopped=true;a.ResetPath();}
    }
    private void MovePlayer(Vector3 position)
    {
        var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.position=position;cc.enabled=true;Physics.SyncTransforms();
    }
    private IEnumerator Checks()
    {
        director=FindAnyObjectByType<WorldMonsterDirector>();movement=FindAnyObjectByType<PlayerMovement>();
        progression=movement.GetComponent<PlayerProgressionState>();saveBefore=progression.CaptureSave();persistBefore=(bool)Get(progression,"persistProgress");Set(progression,"persistProgress",false);
        progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        input=movement.GetComponent<PlayerInput>();if(input.actions.devices.HasValue)inputBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());input.actions.devices=Array.Empty<InputDevice>();
        movement.GetComponent<PlayerAttackController>().enabled=false;movement.GetComponent<PlayerMeleeAttack>().enabled=false;
        movement.GetComponent<PlayerRangedWeapon>().enabled=false;movement.enabled=false;
        var playerHealth=movement.GetComponent<PlayerHealth>();Set(playerHealth,"maxHealth",100000f);
        typeof(PlayerHealth).GetField("<CurrentHealth>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(playerHealth,100000f);
        MovePlayer(new Vector3(-150,4,-150));
        yield return Until(()=>director.Initialized,15);
        Check(director.Initialized,"Director initializes without waves");
        yield return Wait(.5f);
        Check(FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None).All(s=>!s.enabled),"Legacy waves retained but disabled");
        Check(director.CampCount==3&&director.RoamingAlive==22&&director.TotalAlive==37,"Population is bounded at 22 roaming + 15 camp members");
        var members=Members();
        foreach(var s in director.species)Check(members.Count(m=>m.SpeciesId==s.id)==s.population,"All configured "+s.id+" spawn: "+s.population);
        Check(members.Count(m=>m.SpeciesId=="goblin_chief")==3&&members.Count(m=>m.SpeciesId=="goblin_demon")==12,"Each tribe contains one chief and four guards");
        Check(members.All(m=>m.GetComponent<NavMeshAgent>().isOnNavMesh),"Every world monster starts on the NavMesh");
        Check(members.All(m=>!director.IsProtected(m.transform.position)&&m.transform.position.y>=2.3f),"No monster starts in protected settlements, encounters or river beds");
        Check(FindObjectsByType<KingOfDemonAI>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<EncounterCombatant>(FindObjectsSortMode.None).Length==2,"The three existing boss combatants remain in the scene");
        foreach(var camp in director.ActiveCamps)
        {
            Check(!director.IsProtected(camp.transform.position,camp.radius),"Camp "+camp.CampId+" entire footprint avoids protected zones");
            Check(camp.groundedProps.All(p=>p.GetComponent<BoxCollider>()!=null&&p.GetComponent<NavMeshObstacle>().carving),"Camp "+camp.CampId+" props block movement and carve navigation");
            float maxGap=camp.groundedProps.Max(p=>{
                var rs=p.GetComponentsInChildren<MeshRenderer>();float min=rs.Min(r=>r.bounds.min.y);
                return Mathf.Abs(min-(director.terrain.SampleHeight(p.position)+director.terrain.transform.position.y));
            });
            Check(maxGap<.15f,"Camp "+camp.CampId+" props are grounded (gap="+maxGap.ToString("F2")+")");
        }
        CaptureCamp(director.ActiveCamps[0]);
        Vector3[] initialPositions=members.Select(m=>m.transform.position).ToArray();
        yield return Wait(2.5f);
        Check(members.Where((m,i)=>Vector3.Distance(m.transform.position,initialPositions[i])>.2f).Count()>=15,"Most monsters patrol instead of standing still");
        foreach(var m in members)Freeze(m);
        director.roamingRespawnDelay=4;director.campRespawnDelay=2;
        int deaths=0;director.MonsterDefeated+=m=>deaths++;
        foreach(string id in new[]{"skeleton","goblin_demon"})
        {
            var subject=members.First(m=>m.SpeciesId==id);var animator=subject.GetComponentInChildren<Animator>();
            Check(animator!=null&&animator.avatar!=null&&animator.avatar.isValid,"Valid animated rig for "+id);
            Check(subject.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(mat=>mat!=null&&mat.shader.name!="Hidden/InternalErrorShader")),"URP materials resolve for "+id);
            subject.GetComponent<EnemyAnimationController>().enabled=false;
            foreach(string state in new[]{"Locomotion","Attack"})
            {
                animator.Play(state,0,.15f);animator.Update(0);
                var bones=animator.GetComponentsInChildren<Transform>();var before=bones.Select(t=>t.localRotation).ToArray();
                animator.Play(state,0,.65f);animator.Update(0);
                Check(bones.Where((t,i)=>Quaternion.Angle(t.localRotation,before[i])>.3f).Count()>3,id+" bones animate in "+state);
            }
            foreach(float speed in new[]{.5f,1f})
            {
                animator.SetFloat("Speed",speed);animator.Play("Locomotion",0,.15f);animator.Update(0);
                var bones=animator.GetComponentsInChildren<Transform>();var before=bones.Select(t=>t.localRotation).ToArray();
                animator.Play("Locomotion",0,.65f);animator.Update(0);
                Check(bones.Where((t,i)=>Quaternion.Angle(t.localRotation,before[i])>.3f).Count()>3,id+" bones animate at locomotion speed "+speed);
            }
            animator.SetFloat("Speed",0);
            // A real AI attack must see the player in front and apply delayed melee damage.
            animator.Play("Locomotion",0,0);animator.Update(0);
            subject.transform.rotation=Quaternion.identity;
            Set(subject.GetComponent<EnemyAI>(),"huntCivilians",false);
            MovePlayer(subject.transform.position+Vector3.forward*1.3f);
            var ai=subject.GetComponent<EnemyAI>();ai.enabled=true;subject.GetComponent<EnemyAttack>().enabled=true;
            float hp=playerHealth.CurrentHealth;
            yield return Until(()=>playerHealth.CurrentHealth<hp,4);
            Check(playerHealth.CurrentHealth<hp,id+" AI acquires and damages its target");
            Freeze(subject);MovePlayer(new Vector3(-150,4,-150));
            StudioCapture(subject.gameObject,id);
        }
        var skeleton=members.First(m=>m.SpeciesId=="skeleton");
        int expectedExp=skeleton.Health.ExperienceReward;
        skeleton.Health.TakeDamage(99999,progression.gameObject);skeleton.Health.TakeDamage(99999,progression.gameObject);
        yield return Wait(.2f);
        Check(deaths==1&&progression.Experience==expectedExp,"A real Skeleton death reports once and grants its existing EXP reward once");
        Check(skeleton.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"),"Skeleton enters its death animation");
        Check(director.RoamingAlive==21,"Dead roaming slot stays empty during cooldown");
        yield return Until(()=>director.RoamingAlive==22,8);
        Check(director.RoamingAlive==22&&director.TotalAlive==37,"One roaming replacement restores the cap after cooldown");
        foreach(var m in Members())Freeze(m);
        var camp0=director.ActiveCamps[0];var campPosition=camp0.transform.position;
        var guards=Members().Where(m=>m.CampId==camp0.CampId).ToArray();
        guards[0].Health.TakeDamage(99999);yield return Wait(.8f);
        var fallen=guards[0].transform.Find("ModelRoot/WorldMonsterVisual");
        Check(fallen!=null&&Quaternion.Angle(Quaternion.identity,fallen.localRotation)>70,"Goblin demo without a death take has a visible smooth fall");
        yield return Wait(2.2f);
        Check(Members().Count(m=>m.CampId==camp0.CampId)==4&&director.ActiveCamps[0]==camp0,"One fallen camp member is not individually replaced");
        foreach(var guard in guards.Skip(1))guard.Health.TakeDamage(99999);
        MovePlayer(campPosition+Vector3.back*12);yield return Wait(3.5f);
        Check(director.ActiveCamps[0]==camp0&&Members().Count(m=>m.CampId==camp0.CampId)==0,"A cleared camp remains cleared while the player is nearby");
        MovePlayer(new Vector3(-150,4,-150));
        yield return Until(()=>director.ActiveCamps[0]!=camp0,12);
        Check(director.ActiveCamps[0]!=camp0,"Cleared camp regenerates as a whole after the player leaves");
        Check(WorldMonsterDirector.HorizontalDistance(director.ActiveCamps[0].transform.position,campPosition)>20,"Regenerated tribe chooses a different clearing");
        Check(Members().Count(m=>m.CampId==0)==5&&director.TotalAlive==37,"Regenerated camp restores exactly chief + four guards");
        Check(Members().All(m=>m.GetComponent<NavMeshAgent>().isOnNavMesh),"Replacement members also remain on the NavMesh");
        Check(deaths==6,"Death hook excludes camp cleanup and ordinary despawning");
        foreach(var m in Members())Freeze(m);
        Check(errors.Count==0,"No runtime exceptions or errors during population, AI, death and respawn");
        report.AppendLine("Seed="+director.ActiveSeed+" camps="+string.Join("; ",director.ActiveCamps.Select(c=>c.transform.position.ToString())));
    }
    private void CaptureCamp(WorldMonsterCamp camp)
    {
        Capture("WorldMonster_Camp_Play",camp.transform.position+Vector3.up*1.3f,new Vector3(15,18,-22),12,-1);
    }
    private void StudioCapture(GameObject model,string id)
    {
        // Render a separate copy so the live agent's navigation and position stay intact.
        var copy=Instantiate(model);temporary.Add(copy);copy.name="Temporary monster studio";
        var nav=copy.GetComponent<NavMeshAgent>();nav.enabled=false;
        foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
        foreach(var t in copy.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        copy.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.identity);
        var animator=copy.GetComponentInChildren<Animator>();animator.Play("Locomotion",0,.15f);animator.SetFloat("Speed",0);animator.Update(0);
        Capture("WorldMonster_Studio_"+id,new Vector3(0,101,0),new Vector3(2,1,5),1.65f,1<<31);
        Destroy(copy);
    }
    private void Capture(string name,Vector3 target,Vector3 offset,float size,int mask)
    {
        bool fog=RenderSettings.fog;RenderSettings.fog=false;
        var go=new GameObject("Temporary population test camera");temporary.Add(go);var cam=go.AddComponent<Camera>();
        cam.transform.position=target+offset;cam.transform.LookAt(target);cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=500;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.13f,.16f,.18f);cam.cullingMask=mask;
        var rt=RenderTexture.GetTemporary(1400,1050,24);var old=RenderTexture.active;var image=new Texture2D(1400,1050,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1050),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(image);Destroy(go);RenderSettings.fog=fog;}
    }
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> predicate,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){passed&=value;report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/WorldMonsterPlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}
        foreach(var e in errors){passed=false;report.AppendLine("RUNTIME ERROR "+e);}
        if(progression!=null&&saveBefore!=null){progression.RestoreSave(saveBefore);Set(progression,"persistProgress",persistBefore);}
        if(input!=null)input.actions.devices=inputBefore;
        foreach(var go in temporary)if(go!=null)Destroy(go);
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/WorldMonsterPlaytest.txt",report.ToString());
        Debug.Log("World population regression complete. Passed="+passed);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Population test interrupted."));}
}
#endif
