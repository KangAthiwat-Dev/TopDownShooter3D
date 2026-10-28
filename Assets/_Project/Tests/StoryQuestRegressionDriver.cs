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
using UnityEngine.SceneManagement;

public sealed class StoryQuestRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerStoryProgress story;private PlayerMovement player;private PlayerGoldWallet wallet;private PlayerInventory bag;private StoryDialogueUI dialogue;private StoryDialogueInteractable guard;private VillageDefenseEncounter defense;
    private Keyboard keyboard;private PlayerInput input;private bool done;private int fps,vsync;private bool background;
    private GameObject fixture;private NavMeshData data;private NavMeshDataInstance nav;
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);fps=Application.targetFrameRate;vsync=QualitySettings.vSyncCount;background=Application.runInBackground;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        keyboard=InputSystem.AddDevice<Keyboard>("StoryQuestTestKeyboard");report.AppendLine("Story quest end-to-end regression "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;
            try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}
            if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private void Bind()
    {
        story=FindAnyObjectByType<PlayerStoryProgress>();player=story.GetComponent<PlayerMovement>();wallet=story.GetComponent<PlayerGoldWallet>();bag=story.GetComponent<PlayerInventory>();
        dialogue=FindAnyObjectByType<StoryDialogueUI>();guard=FindAnyObjectByType<StoryDialogueInteractable>();defense=FindAnyObjectByType<VillageDefenseEncounter>();
        Set(story,"persistStory",false);Set(wallet,"persistGold",false);Set(story.GetComponent<PlayerProgressionState>(),"persistProgress",false);
        input=story.GetComponent<PlayerInput>();input.neverAutoSwitchControlSchemes=true;input.actions.devices=new InputDevice[]{keyboard};
        foreach(var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)){enemy.enabled=false;enemy.GetComponent<EnemyAttack>().enabled=false;}
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
    }
    private IEnumerator Checks()
    {
        yield return null;Bind();
        // Start a fresh quest fixture even when the user's backed-up wallet has
        // already claimed story rewards. Keep its Gold/items/weapons unchanged.
        wallet.RestoreSave(System.Text.RegularExpressions.Regex.Replace(wallet.CaptureSave(),"\"questReceipts\":\\[[^\\]]*\\]","\"questReceipts\":[]"));
        Check(Vector3.Distance(player.transform.position,FindAnyObjectByType<PlayerSceneSpawnPoint>().Position)<1,"Player starts at the summoning ruins");
        Check(story.RestoreSave("{\"version\":1}"),"Fresh story save accepted without changing the existing player profile");
        Check(!story.AcceptDefense()&&!story.AcceptWolf(),"Tutorial and prerequisite quests gate acceptance");
        Check(!story.RestoreSave("{\"version\":99}")&&!story.RestoreSave("{\"version\":1,\"moved\":8,\"defense\":2}"),"Invalid versions and impossible story transitions are rejected");
        yield return Capture("Story_Tutorial");
        // Isolate actual movement/fire input from decorative spawn colliders.
        Vector3 origin=new Vector3(2000,20,2000);fixture=new GameObject("Temporary story input fixture");fixture.transform.SetParent(transform);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(fixture.transform);floor.transform.position=origin+Vector3.down*.1f;floor.transform.localScale=new Vector3(50,.2f,50);
        data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(50,.2f,50),transform=Matrix4x4.TRS(origin+Vector3.down*.1f,Quaternion.identity,Vector3.one)}},new Bounds(origin,new Vector3(54,10,54)),Vector3.zero,Quaternion.identity);nav=NavMesh.AddNavMeshData(data);
        // The real profile may already have completed shooting before this fresh
        // test state was restored. Explicitly reset its inactive fixture too.
        var practice=FindObjectsByType<StoryTrainingTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();practice.gameObject.SetActive(true);practice.transform.position=origin+new Vector3(8,0,8);
        Warp(origin);yield return Wait(.1f);Check(story.TutorialDistance==0,"Teleporting does not count as tutorial movement");
        Keys(Key.W);float deadline=Time.realtimeSinceStartup+5;while(story.TutorialDistance<8&&Time.realtimeSinceStartup<deadline)yield return null;Keys();yield return Wait(.3f);
        Check(story.TutorialDistance>=8&&!story.TutorialCompleted,"Real WASD movement advances the tutorial without skipping fire or dash");
        var camera=Camera.main;var follow=camera.GetComponentInParent<CameraFollow>();if(follow!=null)follow.enabled=false;
        camera.transform.position=player.transform.position+new Vector3(3,6,-7);camera.transform.LookAt(practice.transform.position+Vector3.up);
        Keys(Key.J);deadline=Time.realtimeSinceStartup+6;while(!story.TutorialShot&&Time.realtimeSinceStartup<deadline)yield return null;Keys();yield return Wait(.2f);
        Check(story.TutorialShot&&!practice.gameObject.activeSelf,"Actual committed J fire completes shooting and retires the practice target");
        var ui=FindAnyObjectByType<StoryQuestUI>();ui.Toggle();yield return Wait(.1f);Check(ui.IsOpen&&Time.timeScale==0,"Q journal pauses gameplay using its independent pause reason");
        Keys(Key.Space);yield return Wait(.2f);Check(!story.TutorialDash,"Dash input while a menu is open cannot finish the tutorial");ui.Close();yield return Wait(.1f);
        Check(!story.TutorialDash,"Holding Space when closing a modal still requires release");Keys();yield return Wait(.15f);Keys(Key.Space);yield return Wait(.1f);Keys();yield return Wait(.5f);
        Check(story.TutorialCompleted&&story.DefenseStatus==StoryQuestStatus.Available,"Successful short dash completes tutorial and unlocks Explore Everwood");
        ui.Toggle();yield return Capture("Story_Journal");
        Check(ui.transform.GetSiblingIndex()==ui.transform.parent.childCount-1,"Quest journal renders above the whole HUD and minimap");ui.Close();
        if(nav.valid)nav.Remove();Destroy(fixture);Destroy(data);fixture=null;data=null;
        Warp(guard.transform.position+guard.transform.forward*1.55f);if(follow!=null)follow.enabled=true;yield return Wait(.4f);
        Check(player.GetComponent<PlayerInteractor>().CurrentInteractable==guard,"Existing E interaction discovers the village guard");
        Keys(Key.E);yield return Wait(.15f);Keys();yield return Wait(.1f);
        Check(dialogue.IsOpen&&Time.timeScale==0&&story.GuardMet&&dialogue.Body.Contains("แนวป้องกัน"),"Real E opens the first guard dialogue with Story text and pauses combat");
        yield return Capture("Story_Guard_Offer");
        Check(dialogue.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>!string.IsNullOrEmpty(t.text)).All(t=>t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c=>c.isVisible)),"Guard speaker, dialogue and all choice labels render visible text");
        Check(!FindAnyObjectByType<GuildTradeUI>().IsOpen&&!FindAnyObjectByType<PotionShopUI>().IsOpen,"Story conversation coexists with the existing closed shops");
        dialogue.PrimaryButton.onClick.Invoke();yield return Wait(.15f);Check(!dialogue.IsOpen&&story.DefenseStatus==StoryQuestStatus.Active&&Time.timeScale==1,"Accept choice starts defense and restores gameplay");
        yield return WaitForRaid();var members=LivingRaid();
        Check(members.Length==2&&members.All(m=>m.GetComponent<NavMeshAgent>().isOnNavMesh),"First wave spawns exactly two invaders on the real village navigation");
        var wandering=new GameObject("Unrelated enemy");var unrelated=wandering.AddComponent<EnemyHealth>();unrelated.TakeDamage(99999);Destroy(wandering);
        Check(story.DefenseCount==0,"Unrelated monster deaths never advance village defense");
        int slot=members[0].Slot;Destroy(members[0].gameObject);yield return Wait(1.2f);
        Check(story.DefenseCount==0&&LivingRaid().Any(m=>m.Slot==slot),"Despawn is not a kill; missing encounter members are reconstructed");
        members=LivingRaid();members[0].GetComponent<EnemyHealth>().TakeDamage(99999);members[0].GetComponent<EnemyHealth>().TakeDamage(99999);
        Check(story.DefenseCount==1,"Repeated lethal damage credits one stable encounter slot");
        string middle=story.CaptureSave();yield return Reload();
        Check(story.DefenseCount==1&&story.DefenseStatus==StoryQuestStatus.Active&&story.CaptureSave()==middle,"Actual scene reload preserves a partially completed defense");
        Warp(guard.transform.position+guard.transform.forward*2);yield return WaitForRaid();
        Check(LivingRaid().Length==1,"Reload reconstructs only the remaining member of the first wave");
        for(int wave=0;wave<3&&story.DefenseStatus==StoryQuestStatus.Active;wave++)
        {
            yield return WaitForRaid();foreach(var m in LivingRaid())m.GetComponent<EnemyHealth>().TakeDamage(99999);
            yield return null;
        }
        Check(story.DefenseCount==6&&story.DefenseStatus==StoryQuestStatus.ReadyToTurnIn,"All three waves complete the objective and require guard turn-in");
        // Fill all slots, including full stacks, to exercise the non-lossy reward path.
        var savedItems=Enumerable.Range(0,8).Select(bag.GetItem).ToArray();var savedCounts=Enumerable.Range(0,8).Select(bag.GetQuantity).ToArray();
        var filler=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Items/MonsterLoot/Items/monster_fang.asset");
        bag.RestoreContents(Enumerable.Repeat(filler,8).ToArray(),Enumerable.Repeat(filler.MaxStack,8).ToArray());int before=wallet.Gold;
        Check(!story.TryClaimReward(story.DefenseQuest,out _)&&wallet.Gold==before&&story.DefenseStatus==StoryQuestStatus.ReadyToTurnIn,"Full bag leaves both Gold and the complete reward unclaimed");
        bag.RestoreContents(savedItems,savedCounts);
        // Ensure sufficient room in this disposable test profile without touching the original inventory.
        for(int i=0;i<8;i++)if(bag.GetItem(i)!=null)bag.TryRemove(i,bag.GetQuantity(i));
        string walletBeforeCap=wallet.CaptureSave();Set(wallet,"gold",PlayerGoldWallet.MaximumGold);
        Check(!story.TryClaimReward(story.DefenseQuest,out _)&&bag.OccupiedSlots==0,"Gold cap also rejects the entire reward atomically");wallet.RestoreSave(walletBeforeCap);
        Warp(guard.transform.position+guard.transform.forward*1.55f);yield return Wait(.2f);Check(dialogue.TryOpen(guard),"Guard reopens for completed defense turn-in");
        dialogue.PrimaryButton.onClick.Invoke();Check(story.DefenseStatus==StoryQuestStatus.Completed&&wallet.Gold==before+100&&Count(story.DefenseQuest.rewardItem)==2,"Guard turn-in grants exactly 100 Gold and two hides");
        int afterDefense=wallet.Gold;Check(!story.TryClaimReward(story.DefenseQuest,out _)&&wallet.Gold==afterDefense,"Completed quests cannot grant rewards twice");
        Check(dialogue.Body.Contains("ราชาแห่งฝูงหมาป่า")&&story.WolfStatus==StoryQuestStatus.Available,"Defense completion offers the Alpha Wolf sequel");
        yield return Capture("Story_Defense_Reward");dialogue.PrimaryButton.onClick.Invoke();yield return Wait(.2f);
        Check(story.WolfStatus==StoryQuestStatus.Active&&!dialogue.IsOpen,"Guard accepts the wolf hunt only after defense is rewarded");
        var director=FindAnyObjectByType<WorldMonsterDirector>();deadline=Time.realtimeSinceStartup+8;
        WorldMonsterMember Wolf()=>FindObjectsByType<WorldMonsterMember>(FindObjectsSortMode.None).FirstOrDefault(m=>m.SpeciesId=="alpha_wolf"&&!m.Defeated&&m.Health.IsAlive);
        while(Wolf()==null&&Time.realtimeSinceStartup<deadline)yield return null;
        var wolf=Wolf();Check(wolf!=null&&story.ObjectiveTarget==wolf.transform,"Hunt targets the existing single native Wolf Boss, with a live map marker");
        // Environmental deaths do not fulfil a player hunting objective.
        if(wolf.TryGetComponent<EnemyLootDropper>(out var loot))DestroyImmediate(loot);wolf.Health.TakeDamage(99999);
        Check(story.WolfStatus==StoryQuestStatus.Active,"Wolf killed without the player does not falsely complete the hunt");
        director.roamingRespawnDelay=.2f;
        // Its slot already has the previous delay. Reload also verifies active hunt persistence and makes a fresh population.
        yield return Reload();deadline=Time.realtimeSinceStartup+8;while(Wolf()==null&&Time.realtimeSinceStartup<deadline)yield return null;
        wolf=Wolf();Check(story.WolfStatus==StoryQuestStatus.Active&&wolf!=null,"Active wolf hunt survives scene reload and locates the new population member");
        if(wolf.TryGetComponent<EnemyLootDropper>(out loot))DestroyImmediate(loot);wolf.Health.TakeDamage(99999,player.gameObject);wolf.Health.TakeDamage(99999,player.gameObject);
        Check(story.WolfStatus==StoryQuestStatus.ReadyToTurnIn,"Real native Wolf Boss death credited to the player completes the hunt once");
        Warp(guard.transform.position+guard.transform.forward*1.55f);yield return Wait(.2f);Check(dialogue.TryOpen(guard),"Wolf turn-in uses the same guard contact");
        before=wallet.Gold;dialogue.PrimaryButton.onClick.Invoke();
        Check(story.WolfStatus==StoryQuestStatus.Completed&&wallet.Gold==before+180&&Count(story.WolfQuest.rewardItem)==1,"Wolf turn-in grants 180 Gold and one sellable Wolf Fang");
        Check(story.WolfQuest.rewardItem.CanSell&&story.WolfQuest.rewardItem.SellPrice==60,"Wolf Fang integrates with existing guild buying at 60 Gold");
        yield return Capture("Story_Wolf_Reward");dialogue.Close();
        string completed=story.CaptureSave(),walletCompleted=wallet.CaptureSave();yield return Reload();
        Check(story.CaptureSave()==completed&&wallet.CaptureSave()==walletCompleted,"Actual reload preserves completed quests, Gold, inventory and reward receipts");
        Check(!story.TryClaimReward(story.WolfQuest,out _)&&Count(story.WolfQuest.rewardItem)==1,"Reload cannot duplicate Wolf Fang or Gold");
        Check(!FindObjectsByType<StoryTrainingTarget>(FindObjectsSortMode.None).Any()&&LivingRaid().Length==0,"Completed tutorial and raid stay retired after reload");
        // Simulate a shutdown after the wallet commit, before the story commit.
        string paid=story.CaptureSave();Check(story.RestoreSave(middle)&&story.DefenseStatus==StoryQuestStatus.Completed&&story.WolfStatus==StoryQuestStatus.Completed,"Wallet receipts reconcile an older story save after interrupted reward delivery");story.RestoreSave(paid);
        Warp(guard.transform.position+guard.transform.forward*1.55f);yield return Wait(.2f);Check(dialogue.TryOpen(guard)&&!dialogue.PrimaryButton.gameObject.activeSelf,"Repeat dialogue after both quests contains no reward choice");
        var pause=FindAnyObjectByType<GamePauseController>();pause.SetPaused(GamePauseReason.GameOver,true);dialogue.Close();
        Check(pause.IsGameOver&&Time.timeScale==0,"Closing story dialogue preserves the independent Game Over pause");pause.ResetForRestart();
        Check(errors.Count==0,"No runtime errors across tutorial, waves, rewards or scene reloads");
    }
    private int Count(ItemDefinition item)=>Enumerable.Range(0,8).Where(i=>bag.GetItem(i)==item).Sum(bag.GetQuantity);
    private VillageDefenseMember[] LivingRaid()=>FindObjectsByType<VillageDefenseMember>(FindObjectsSortMode.None).Where(m=>!m.HealthDead).ToArray();
    private IEnumerator WaitForRaid()
    {
        float deadline=Time.realtimeSinceStartup+9;while(LivingRaid().Length==0&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(LivingRaid().Length>0,"Next required invasion wave becomes available");
        foreach(var m in LivingRaid()){m.GetComponent<EnemyAttack>().enabled=false;}
    }
    private IEnumerator Reload()
    {
        Keys();dialogue.Close();FindAnyObjectByType<StoryQuestUI>().Close();
        PlayerPrefs.SetString(PlayerStoryProgress.DefaultSaveKey,story.CaptureSave());PlayerPrefs.SetString("TopDownShooter3D.Gold.v1",wallet.CaptureSave());PlayerPrefs.Save();
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);yield return null;yield return null;Bind();
    }
    private void Warp(Vector3 point){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.position=point;c.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/StoryTests/"+name+".png");yield return Wait(.12f);}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private void Check(bool okay,string label){report.AppendLine((okay?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/StoryQuestPlaytest.txt",report.ToString());if(!okay)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;if(keyboard!=null){Keys();InputSystem.RemoveDevice(keyboard);}if(nav.valid)nav.Remove();if(data!=null)Destroy(data);if(fixture!=null)Destroy(fixture);
        Application.logMessageReceived-=OnLog;Application.targetFrameRate=fps;QualitySettings.vSyncCount=vsync;Application.runInBackground=background;
        if(failure!=null)report.AppendLine("EXCEPTION "+failure);foreach(string error in errors)report.AppendLine("RUNTIME ERROR "+error);
        report.AppendLine(failure==null&&errors.Count==0?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/StoryQuestPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Story regression interrupted"));}
}
#endif
