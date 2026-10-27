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

public sealed class MonsterLootRegressionDriver : MonoBehaviour
{
    private const string Folder="Assets/_Project/Items/MonsterLoot";
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
    private readonly List<string> errors=new List<string>();
    private MonsterLootWorld world;private PlayerInventory inventory;private PlayerMovement movement;private InventoryPanelUI panel;
    private PlayerInput input;private ReadOnlyArray<InputDevice>? devicesBefore;
    private PlayerProgressionState progression;private string saveBefore;private bool persistBefore,backgroundBefore,done,passed=true;
    private int fpsBefore,vsyncBefore;private float timeBefore;private Vector3 origin;
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private ItemDefinition Item(string id)=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(Folder+"/Items/"+id+".asset");
    private LootPickup[] Drops()=>world.Pickups.Where(p=>p!=null&&!p.IsCollected).ToArray();
    private int Count(ItemDefinition item)=>Enumerable.Range(0,PlayerInventory.Capacity).Where(i=>inventory.GetItem(i)==item).Sum(inventory.GetQuantity);
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Monster drops / magnet / inventory regression "+DateTime.Now.ToString("s"));
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
        world=FindAnyObjectByType<MonsterLootWorld>();inventory=world.Inventory;movement=inventory.GetComponent<PlayerMovement>();panel=FindAnyObjectByType<InventoryPanelUI>();
        progression=inventory.GetComponent<PlayerProgressionState>();saveBefore=progression.CaptureSave();persistBefore=Get<bool>(progression,"persistProgress");Set(progression,"persistProgress",false);
        progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        input=inventory.GetComponent<PlayerInput>();if(input.actions.devices.HasValue)devicesBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());input.actions.devices=Array.Empty<InputDevice>();
        movement.enabled=false;inventory.GetComponent<PlayerAttackController>().enabled=false;inventory.GetComponent<PlayerRangedWeapon>().enabled=false;inventory.GetComponent<PlayerMeleeAttack>().enabled=false;
        MovePlayer(new Vector3(-150,4,-150));var director=FindAnyObjectByType<WorldMonsterDirector>();
        yield return Until(()=>director.Initialized,15);
        director.enabled=false;
        foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        origin=director.roamingSites.OrderBy(s=>(s.point.position-new Vector3(-20,4,-65)).sqrMagnitude).First().point.position;
        MovePlayer(origin);
        var bone=Item("monster_bone");var items=AssetDatabase.FindAssets("t:ItemDefinition",new[]{Folder}).Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Check(items.Length==8&&items.All(i=>i.Icon!=null&&i.MaxStack==99),"Eight transparent hunting icons are linked to stackable resource items");
        var random=new System.Random(24681);
        foreach(string guid in AssetDatabase.FindAssets("t:MonsterDropTable",new[]{Folder}))
        {
            var table=AssetDatabase.LoadAssetAtPath<MonsterDropTable>(AssetDatabase.GUIDToAssetPath(guid));bool valid=true;
            for(int i=0;i<100;i++)
            {
                int units=0;table.Roll(random,(item,count)=>{valid&=items.Contains(item)&&count>0;units+=count;});valid&=units>0;
            }
            Check(valid,"Guaranteed valid material rewards from "+table.name+" across 100 rolls");
        }
        int received=0;var controlled=ScriptableObject.CreateInstance<MonsterDropTable>();temporary.Add(controlled);
        controlled.entries=new[]{new MonsterDropTable.Entry{item=bone,chance=0,minimum=999,maximum=999},new MonsterDropTable.Entry{item=bone,chance=1,minimum=2,maximum=2}};
        controlled.Roll(random,(_,n)=>received+=n);Check(received==2,"Zero-probability entries never drop; guaranteed fixed quantities are exact");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Skeleton.prefab");
        var actor=Instantiate(prefab,origin+Vector3.forward*12,Quaternion.identity);temporary.Add(actor);Freeze(actor);
        var health=actor.GetComponent<EnemyHealth>();int stacks=world.SpawnedStacks;
        health.TakeDamage(1,progression.gameObject);Check(world.SpawnedStacks==stacks,"Non-lethal hits do not generate drops");
        health.TakeDamage(9999,progression.gameObject);health.TakeDamage(9999,progression.gameObject);
        Check(world.SpawnedStacks==stacks+1&&Drops().Length==1&&progression.Experience==75,"Real Skeleton death drops once and preserves its single EXP reward");
        var drop=Drops().Single();int quantity=drop.Amount;
        Check(drop.Item==bone&&quantity>=1&&quantity<=3&&drop.GetComponentInChildren<SpriteRenderer>().sprite==bone.Icon,"Skeleton drops its configured bone icon and amount");
        Destroy(actor);yield return Wait(.65f);
        Check(drop!=null&&drop.CanMerge&&Count(bone)==0&&!drop.transform.IsChildOf(actor!=null?actor.transform:transform),"Loot outlives its corpse and waits outside magnet range");
        CaptureWorld("MonsterLoot_OnGround",drop.transform.position,3);
        MovePlayer(drop.GroundPoint+Vector3.back*3);
        yield return Until(()=>drop.IsAttracting,2);
        Check(drop.IsAttracting&&Count(bone)==0,"Nearby loot starts visible magnet flight before entering inventory");
        Check(panel.TryOpen(),"Inventory can open during a magnet flight");Vector3 held=drop.transform.position;
        yield return Wait(.25f);
        Check(drop.transform.position==held&&Count(bone)==0,"Opening inventory pauses loot motion and collection");panel.Close();
        yield return Until(()=>Count(bone)==quantity,3);
        Check(Count(bone)==quantity&&Drops().Length==0,"Magnet arrival credits the exact stack and removes its world icon");
        var hotbar=FindAnyObjectByType<InventoryHotbarUI>();var views=Get<InventoryHotbarUI.SlotView[]>(hotbar,"slots");
        Check(views[0].icon.sprite==bone.Icon&&views[0].stateLabel.text=="x"+quantity,"Existing HUD updates the material icon and quantity");
        panel.TryOpen();panel.SelectSlot(0);Check(panel.DetailTitle=="Monster Bone","Bag details show the collected resource");panel.Close();
        // A living actor being destroyed is ordinary cleanup, never a kill.
        stacks=world.SpawnedStacks;var alive=Instantiate(prefab,origin+Vector3.right*12,Quaternion.identity);temporary.Add(alive);Freeze(alive);Destroy(alive);yield return null;
        Check(world.SpawnedStacks==stacks,"Destroying a living monster creates no loot");
        ClearInventory();var filler=ScriptableObject.CreateInstance<ItemDefinition>();temporary.Add(filler);Set(filler,"maxStack",1);
        Check(inventory.TryAdd(filler,8)&&inventory.SpaceFor(bone)==0,"Eight supply slots can be full independently of the two melee slots");
        MovePlayer(origin);world.SpawnItem(bone,2,origin+Vector3.forward*3);yield return Wait(1.2f);
        Check(Drops().Length==1&&Drops()[0].Amount==2&&!Drops()[0].IsAttracting&&Count(bone)==0,"A full bag leaves all dropped units on the ground");
        inventory.TryRemove(3);yield return Until(()=>Count(bone)==2,3);
        Check(Count(bone)==2&&inventory.GetItem(3)==bone&&inventory.OccupiedSlots==8,"Freeing a supply slot automatically resumes pickup");
        ClearInventory();inventory.TryAdd(filler,7);inventory.TryAdd(bone,97);
        world.SpawnItem(bone,5,origin+Vector3.forward*3);yield return Until(()=>Count(bone)==99,4);yield return Wait(.6f);
        Check(Count(bone)==99&&Drops().Sum(p=>p.Amount)==3,"Partially full stacks collect only what fits and retain the remainder");
        inventory.TryRemove(7,10);yield return Until(()=>Count(bone)==92,3);
        Check(Count(bone)==92&&Drops().Length==0,"Remaining units collect once space becomes available");
        ClearInventory();inventory.TryAdd(filler,7);inventory.TryAdd(bone,98);
        world.SpawnItem(bone,1,origin+new Vector3(-2,0,3));world.SpawnItem(bone,1,origin+new Vector3(2,0,3));
        yield return Until(()=>Count(bone)==99,4);yield return Wait(.7f);
        Check(Count(bone)==99&&Drops().Sum(p=>p.Amount)==1,"Competing in-flight drops conserve units when the final stack fills");
        yield return ClearDrops();ClearInventory();MovePlayer(origin+Vector3.back*25);
        world.SpawnItem(bone,1,origin);yield return Wait(.6f);drop=Drops().Single();stacks=world.SpawnedStacks;
        world.SpawnItem(bone,2,drop.GroundPoint);
        Check(Drops().Length==1&&drop.Amount==3&&world.SpawnedStacks==stacks,"Nearby identical resting drops merge into one bounded world pile");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);wall.name="Temporary loot obstruction";wall.layer=7;
        Vector3 playerPoint=drop.GroundPoint+Vector3.left*4;
        wall.transform.position=(drop.GroundPoint+playerPoint)*.5f+Vector3.up*1.5f;wall.transform.localScale=new Vector3(.4f,3,4);Physics.SyncTransforms();
        MovePlayer(playerPoint);yield return Wait(.6f);
        Check(!drop.IsAttracting&&Count(bone)==0,"A solid wall blocks magnet collection");Destroy(wall);yield return Until(()=>Count(bone)==3,4);
        Check(Count(bone)==3&&Drops().Length==0,"Removing the obstruction resumes automatic collection");
        ClearInventory();foreach(var item in items)inventory.TryAdd(item,3);
        Check(inventory.OccupiedSlots==8&&inventory.GetComponent<PlayerMeleeEquipment>().WeaponCount==0,"All materials fit the eight resource slots without consuming melee slots");
        panel.TryOpen();panel.SelectSlot(0);yield return CaptureScreen("MonsterLoot_Inventory");panel.Close();
        var actualBosses=FindObjectsByType<EnemyLootDropper>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(d=>d.name=="KingOfDemon"||d.name=="DemonLord"||d.name=="DragonNightmare").ToArray();
        Check(actualBosses.Length==3&&actualBosses.All(d=>d.Table!=null),"All three existing boss actors retain their own drop tables");
        Check(errors.Count==0,"No runtime errors in death, sprites, magnet, pause or inventory");
    }
    private static void Freeze(GameObject actor)
    {
        actor.GetComponent<EnemyAI>().enabled=false;actor.GetComponent<EnemyAttack>().enabled=false;
        var agent=actor.GetComponent<NavMeshAgent>();if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}
    }
    private void MovePlayer(Vector3 point)
    {
        var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.position=point;cc.enabled=true;Physics.SyncTransforms();
    }
    private void ClearInventory(){for(int i=0;i<PlayerInventory.Capacity;i++)if(inventory.GetItem(i)!=null)inventory.TryRemove(i,inventory.GetQuantity(i));}
    private IEnumerator ClearDrops(){foreach(var d in Drops())Destroy(d.gameObject);yield return null;}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> condition,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    private void CaptureWorld(string name,Vector3 target,float size)
    {
        var go=new GameObject("Temporary loot photo camera");temporary.Add(go);var cam=go.AddComponent<Camera>();cam.transform.position=target+new Vector3(3,4,-6);cam.transform.LookAt(target);
        cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=100;
        var rt=RenderTexture.GetTemporary(1200,900,24);var previous=RenderTexture.active;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(pixels);Destroy(go);}
    }
    private IEnumerator CaptureScreen(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/"+name+".png");yield return Wait(.15f);}
    private void Check(bool valid,string label){passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/MonsterLootPlaytest.txt",report.ToString());if(!valid)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}
        foreach(var message in errors){passed=false;report.AppendLine("RUNTIME ERROR "+message);}
        if(panel!=null)panel.Close();
        if(progression!=null&&saveBefore!=null){progression.RestoreSave(saveBefore);Set(progression,"persistProgress",persistBefore);}
        if(input!=null)input.actions.devices=devicesBefore;
        foreach(var obj in temporary)if(obj!=null)Destroy(obj);
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/MonsterLootPlaytest.txt",report.ToString());
        Debug.Log("Monster loot regression complete. Passed="+passed);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Loot regression interrupted."));}
}
#endif
