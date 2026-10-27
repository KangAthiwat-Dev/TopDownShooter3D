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
public sealed class TownDiversityRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report = new StringBuilder();
    private TownNpcPopulation population;
    private EnemyAI enemy;
    private GamePauseController pause;
    private bool passed=true,done,oldBackground;
    private float oldScale;
    private int oldFps,oldVsync;
    private string runtimeError;
    private TownNpcBrain[] People => population.GetComponentsInChildren<TownNpcBrain>().Where(p=>p.GetComponent<CivilianHealth>().IsAlive).ToArray();
    private IEnumerator Start()
    {
        oldScale=Time.timeScale;oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        Application.logMessageReceived+=OnLog;report.AppendLine("Mixed women / builder work loop "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception error=null;
            try { if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current; }catch(Exception e){error=e;}
            if(error!=null){Finish(error);yield break;}
            if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var hostile in FindObjectsByType<EnemyAI>())hostile.gameObject.SetActive(false);
        population=FindFirstObjectByType<TownNpcPopulation>();yield return Wait(1.5f);
        var people=People;var women=people.Where(p=>p.Gender==TownNpcGender.Female).ToArray();
        var jobs=people.Where(p=>p.IsConstructionWorker).Select(p=>p.GetComponent<TownConstructionJob>()).ToArray();
        Check(population.AliveCount==12&&people.Length==12&&women.Length==5&&jobs.Length==3,"12 living slots contain exactly 5 women, 4 male townsfolk and 3 builders");
        Check(jobs.All(j=>j.GetComponent<TownNpcBrain>().Gender==TownNpcGender.Male&&!j.GetComponent<TownNpcBrain>().CanLeaveTown),"All builders are male residents, never random outing candidates");
        Check(jobs.Select(j=>j.Station).Distinct().Count()==3&&jobs.All(j=>j.Station.Contains(j.transform.position)),"Three builders spawn at distinct stations within the construction area");
        Check(women.All(w=>w.GetComponentsInChildren<SkinnedMeshRenderer>().Length==8&&w.GetComponentsInChildren<Animator>().Length==1&&w.GetComponentInChildren<Animator>().avatar.isValid),"Female clothing, head, eyes, brows and hair share one valid rig");
        Check(women.Any(w=>w.name.Contains("Female"))&&women.SelectMany(w=>w.GetComponentsInChildren<SkinnedMeshRenderer>()).Any(s=>s.sharedMesh.name.Contains("Hair")),"Female models are actual modular women, not resized male citizens");
        var woman=women[0];var womanAnimator=woman.GetComponentInChildren<Animator>();womanAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var geometry=GeometryBounds(woman.gameObject);
        Check(geometry.size.y>1.6f&&geometry.size.y<2.05f&&Mathf.Abs(geometry.min.y-woman.transform.position.y)<.18f,"Female scale and feet are grounded, without displaced head/hair geometry");
        womanAnimator.Play("Locomotion",0,.15f);womanAnimator.Update(0);
        Check(womanAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position.y< womanAnimator.GetBoneTransform(HumanBodyBones.Head).position.y-.3f
            && womanAnimator.GetBoneTransform(HumanBodyBones.RightHand).position.y< womanAnimator.GetBoneTransform(HumanBodyBones.Head).position.y-.3f,
            "Female Humanoid idle has relaxed arms below the head after reference-pose calibration");
        Capture("Logs/TownDiversity_Female.png",woman.transform.position+woman.transform.forward*3.6f+woman.transform.right*1.2f+Vector3.up*2.1f,woman.transform.position+Vector3.up*.95f);

        var sawWork=new HashSet<TownConstructionJob>();var sawCarry=new HashSet<TownConstructionJob>();var movingCarry=new HashSet<TownConstructionJob>();
        bool inSite=true,cargoPolicy=true,cap=true,capturedChop=false,capturedCarry=false;
        int spawned=population.SpawnedTotal;Time.timeScale=3f;float end=Time.time+85f;
        while(Time.time<end)
        {
            cap &= population.AliveCount<=12&&population.AwayCount<=2;
            foreach(var job in jobs)
            {
                var actor=job.GetComponent<TownNpcBrain>();var animator=job.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                inSite &= job.Station.Contains(job.transform.position);
                cargoPolicy &= !job.HasCargo || job.Phase==ConstructionJobPhase.Carrying || job.Phase==ConstructionJobPhase.Delivering;
                if(job.Phase==ConstructionJobPhase.Chopping&&animator.GetCurrentAnimatorStateInfo(0).IsName("TreeChopping"))
                {
                    sawWork.Add(job);
                    if(!capturedChop){capturedChop=true;Capture("Logs/TownDiversity_Work.png",job.transform.position+actor.transform.forward*3.8f+actor.transform.right*2f+Vector3.up*2.5f,job.transform.position+Vector3.up);}
                }
                if(job.Phase==ConstructionJobPhase.Carrying&&job.HasCargo&&animator.GetCurrentAnimatorStateInfo(0).IsName("WalkCarry"))
                {
                    sawCarry.Add(job);
                    if(actor.Agent.velocity.magnitude>.7f)movingCarry.Add(job);
                    if(!capturedCarry&&actor.Agent.velocity.magnitude>.7f){capturedCarry=true;Capture("Logs/TownDiversity_Carry.png",job.transform.position+actor.transform.forward*3.8f+actor.transform.right*2f+Vector3.up*2.4f,job.transform.position+Vector3.up);}
                }
            }
            yield return null;
        }
        Time.timeScale=1f;
        foreach(var job in jobs)report.AppendLine("WORKER "+job.name+" phase="+job.Phase+" delivered="+job.CompletedDeliveries+" position="+job.transform.position+" speed="+job.GetComponent<TownNpcBrain>().Agent.velocity.magnitude);
        Check(sawWork.Count==3,"Every builder plays looping TreeChopping while stationed at work");
        Check(sawCarry.Count==3&&movingCarry.Count==3,"Every builder carries visible wood using Walk_Carry_Loop and actual movement");
        Check(jobs.All(j=>j.CompletedDeliveries>=1),"All three workers complete pickup, carry, delivery and return cycles");
        Check(inSite,"Routine work paths stay within the local construction area");
        Check(cargoPolicy,"Wood is shown only during carrying/delivery, never during chopping or empty walking");
        Check(cap&&population.SpawnedTotal==spawned,"Work cycles do not spawn extra NPCs or consume outing slots");
        Check(jobs.All(j=>j.CarriedWood.GetComponentsInChildren<Collider>(true).Length==0&&j.GetComponentInChildren<AudioSource>(true)==null),"Carried wood is visual only; hammer and work sound remain for later");

        // Female social animation and real movement use the shared ambient AI.
        var partner=People.First(p=>!p.IsConstructionWorker&&p.Gender==TownNpcGender.Male);
        foreach(var actor in new[]{woman,partner}){population.ReleaseOuting(actor);actor.Configure(population,false);Invoke(actor,"Idle");Set(actor,"socialCooldown",0f);}
        Warp(woman,new Vector3(75.76f,5.17f,-75));Warp(partner,new Vector3(77.7f,5.17f,-75));
        Check(woman.TryTalkWith(partner),"Female and male townsfolk can hold the existing paired conversation");yield return Wait(.4f);
        Vector3 hand=woman.transform.InverseTransformPoint(womanAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position);yield return Wait(.5f);
        Check(womanAnimator.GetCurrentAnimatorStateInfo(0).IsName("Talking")&&Vector3.Distance(hand,woman.transform.InverseTransformPoint(womanAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position))>.01f,"Female retargeted talking visibly animates the body and hands");
        Capture("Logs/TownDiversity_MixedConversation.png",new Vector3(75.8f,7.7f,-81),new Vector3(76.7f,6.2f,-75));
        Check(woman.TryVisit(population.CityPoints[4]),"Female citizen resumes normal town navigation after talking");yield return Wait(1f);
        Check(woman.Agent.velocity.magnitude>1f,"Female walk animation accompanies genuine navigation movement");

        // Interrupt an actual delivery, then recover naturally to the assigned station.
        yield return Until(()=>jobs.Any(j=>j.HasCargo&&j.Phase==ConstructionJobPhase.Carrying),18f);
        var carrier=jobs.First(j=>j.HasCargo&&j.Phase==ConstructionJobPhase.Carrying);var worker=carrier.GetComponent<TownNpcBrain>();
        SpawnThreat(worker);yield return Wait(.7f);
        Check(worker.State==TownNpcState.Fleeing&&!carrier.HasCargo,"Enemy danger interrupts a work/carry loop and immediately hides the carried wood");
        DestroyThreat();yield return Wait(4.3f);yield return Until(()=>carrier.Phase==ConstructionJobPhase.Chopping&&worker.State==TownNpcState.Working,18f);
        Check(carrier.Phase==ConstructionJobPhase.Chopping&&worker.State==TownNpcState.Working&&Vector3.Distance(worker.transform.position,carrier.Station.transform.position)<1.4f,"A safe builder returns to his own station and resumes work instead of roaming town");

        pause=FindFirstObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.Inventory,true);yield return null;yield return null;
        Vector3 frozen=worker.transform.position;var workerAnimator=worker.GetComponentInChildren<Animator>();float animationTime=workerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;int deliveries=carrier.CompletedDeliveries;
        yield return Wait(.4f);
        Check(Time.timeScale==0&&Vector3.Distance(frozen,worker.transform.position)<.001f&&Mathf.Abs(animationTime-workerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime)<.001f&&deliveries==carrier.CompletedDeliveries,"Inventory pause freezes worker navigation, animation and job timers");pause.SetPaused(GamePauseReason.Inventory,false);

        Set(population,"respawnDelay",1.4f);var station=carrier.Station;spawned=population.SpawnedTotal;
        worker.GetComponent<CivilianHealth>().TakeDamage(100f);
        Check(worker.State==TownNpcState.Dead&&!carrier.HasCargo&&population.AliveCount==11,"Builder death cancels the job and removes held cargo");yield return Wait(1.8f);
        var replacement=People.Where(p=>p.IsConstructionWorker).Select(p=>p.GetComponent<TownConstructionJob>()).FirstOrDefault(j=>j!=carrier&&j.Station==station);
        Check(population.AliveCount==12&&population.SpawnedTotal==spawned+1&&replacement!=null&&People.Count(p=>p.IsConstructionWorker)==3,"Exactly one male builder replacement returns to the same station");
        spawned=population.SpawnedTotal;woman.GetComponent<CivilianHealth>().TakeDamage(100f);yield return Wait(1.8f);
        Check(population.AliveCount==12&&population.SpawnedTotal==spawned+1&&People.Count(p=>p.Gender==TownNpcGender.Female)==5,"Female death replacement preserves the five-woman quota and overall cap");
        Check(FindObjectsByType<NpcIdleLook>().Count(n=>n.NpcId=="armory_merchant_01")==1,"Existing named weapon merchant remains unchanged and separate");
        Check(runtimeError==null,"No runtime errors through female animation, worker loops, threat, pause or role-preserving replacements");
    }
    private void SpawnThreat(TownNpcBrain target)
    {
        Vector3 spot=default;bool found=false;
        foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
        {
            if(!NavMesh.SamplePosition(target.transform.position+direction*4,out var hit,1.3f,NavMesh.AllAreas))continue;
            Vector3 from=hit.position+Vector3.up,delta=target.transform.position+Vector3.up*1.2f-from;
            if(Physics.Raycast(from,delta.normalized,delta.magnitude,1<<7,QueryTriggerInteraction.Ignore))continue;
            spot=hit.position;found=true;break;
        }
        if(!found)throw new InvalidOperationException("No visible threat position for worker fixture");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Imp_Green.prefab");
        enemy=Instantiate(source,spot,Quaternion.LookRotation(Vector3.ProjectOnPlane(target.transform.position-spot,Vector3.up))).GetComponent<EnemyAI>();enemy.GetComponent<EnemyAttack>().enabled=false;
    }
    private void DestroyThreat(){if(enemy!=null)DestroyImmediate(enemy.gameObject);enemy=null;}
    private static Bounds GeometryBounds(GameObject actor)
    {
        bool first=true;Bounds bounds=default;
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=new Mesh();skin.BakeMesh(mesh);
            foreach(var vertex in mesh.vertices)
            {
                Vector3 point=skin.transform.TransformPoint(vertex);
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
            DestroyImmediate(mesh);
        }
        return bounds;
    }
    private static void Warp(TownNpcBrain actor,Vector3 requested)
    {
        if(!NavMesh.SamplePosition(requested,out var hit,1.5f,NavMesh.AllAreas)||!actor.Agent.Warp(hit.position))throw new InvalidOperationException("Cannot place social fixture");
        actor.Agent.isStopped=true;actor.Agent.ResetPath();actor.Agent.velocity=Vector3.zero;Physics.SyncTransforms();
    }
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> condition,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool value,string label){passed&=value;report.AppendLine((value?"PASS ":"FAIL ")+label);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TownDiversityPlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    private void Capture(string path,Vector3 position,Vector3 target)
    {
        var offset=position-target;
        for(int angle=0;angle<360;angle+=45)
        {
            var candidate=Quaternion.Euler(0,angle,0)*offset;
            if(!Physics.SphereCast(target,.18f,candidate.normalized,out _,candidate.magnitude,1<<7,QueryTriggerInteraction.Ignore))
            {position=target+candidate;break;}
        }
        var go=new GameObject("Temporary diversity capture");var camera=go.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=40;camera.nearClipPlane=.1f;
        var rt=RenderTexture.GetTemporary(1400,1000,24);var old=RenderTexture.active;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(image);Destroy(go);}
    }
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(pause!=null)pause.SetPaused(GamePauseReason.Inventory,false);Time.timeScale=oldScale;DestroyThreat();Application.logMessageReceived-=OnLog;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TownDiversityPlaytest.txt",report.ToString());
        if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Women/builders regression interrupted"));}
}
#endif
