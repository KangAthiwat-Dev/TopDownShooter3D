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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameMenuRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerHealth player;private GameMenuUI menu;private PlayerGoldWallet wallet;private string walletBefore;private Keyboard keyboard;private bool done;
    private static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);Application.logMessageReceived+=Log;keyboard=InputSystem.AddDevice<Keyboard>("MenuTestKeyboard");
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private void Bind()
    {
        player=FindAnyObjectByType<PlayerHealth>();menu=FindAnyObjectByType<GameMenuUI>();wallet=player.GetComponent<PlayerGoldWallet>();
        Set(wallet,"persistGold",false);Set(player.GetComponent<PlayerStoryProgress>(),"persistStory",false);Set(player.GetComponent<PlayerProgressionState>(),"persistProgress",false);
        var input=player.GetComponent<PlayerInput>();input.neverAutoSwitchControlSchemes=true;input.actions.devices=new InputDevice[]{keyboard};
        var director=FindAnyObjectByType<WorldMonsterDirector>();if(director!=null)director.gameObject.SetActive(false);
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))enemy.enabled=false;
    }
    private IEnumerator Checks()
    {
        yield return null;Bind();walletBefore=wallet.CaptureSave();var bag=player.GetComponent<PlayerInventory>();var loot=FindAnyObjectByType<MonsterLootWorld>();var pickup=player.GetComponent<PlayerPickupFeedback>();int sounds=0;pickup.Played+=()=>sounds++;
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Items/MonsterLoot/Items/monster_fang.asset");
        bag.RestoreContents(new ItemDefinition[8],new int[8]);
        Check(loot.TryCollect(item,2)==2&&sounds==1&&Get<AudioSource>(pickup,"source").isPlaying,"Successful loot collection plays the requested pickup sound after adding items");
        yield return Wait(.1f);Check(loot.TryCollect(item,0)==0&&sounds==1,"Rejected zero-quantity pickup stays silent");
        bag.RestoreContents(Enumerable.Repeat(item,8).ToArray(),Enumerable.Repeat(item.MaxStack,8).ToArray());
        Check(loot.TryCollect(item,1)==0&&sounds==1,"Full inventory does not play a false pickup sound");wallet.RestoreSave(walletBefore);
        var equipment=player.GetComponent<PlayerMeleeEquipment>();var weapon=equipment.GetWeapon(0)??AssetDatabase.FindAssets("t:MeleeWeaponDefinition",new[]{"Assets/_Project"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>).First(w=>w.HeldPrefab!=null);
        equipment.RestoreLoadout(new MeleeWeaponDefinition[2],-1);
        var pickupObject=new GameObject("Temporary weapon pickup");pickupObject.transform.position=player.transform.position+player.transform.forward;
        var weaponPickup=pickupObject.AddComponent<MeleeWeaponPickup>();Set(weaponPickup,"weapon",weapon);var collector=player.GetComponent<PlayerInteractor>();
        Check(collector.TryStartPickup(weaponPickup)&&sounds==1,"Melee pickup starts its animation without playing a premature pickup sound");
        yield return Until(()=>sounds==2,4);
        Check(sounds==2&&equipment.HasWeapon,"Weapon pickup plays the sound at the actual successful animation commit");
        yield return Until(()=>!collector.IsInteracting,3);wallet.RestoreSave(walletBefore);if(pickupObject!=null)Destroy(pickupObject);
        var inventory=FindAnyObjectByType<InventoryPanelUI>();Check(inventory.TryOpen(),"Inventory opens normally before the pause test");
        var slot=inventory.GetComponentsInChildren<UISlotHoverAudio>().First();var tint=Get<Image>(slot,"highlight");var pointer=new PointerEventData(EventSystem.current){pointerId=-1};
        ExecuteEvents.Execute(slot.gameObject,pointer,ExecuteEvents.pointerEnterHandler);yield return Wait(.16f);
        Check(tint.color.a>.07f&&!tint.raycastTarget&&Time.timeScale==0,"Slot hover gently tints the background during pause without blocking clicks");yield return Capture("Inventory_Hover");
        ExecuteEvents.Execute(slot.gameObject,pointer,ExecuteEvents.pointerExitHandler);yield return Wait(.22f);Check(tint.color.a<.005f,"Slot tint fades back after pointer exit");
        Keys(Key.Escape);yield return Wait(.15f);Keys();yield return Wait(.1f);
        Check(!inventory.IsOpen&&!menu.IsOpen&&Time.timeScale==1,"Esc closes inventory without reopening the pause menu on the same key press");
        Keys(Key.Escape);yield return Wait(.15f);Keys();yield return Wait(.1f);
        Check(menu.IsOpen&&Time.timeScale==0&&AudioListener.pause,"A fresh Esc opens the actual pause menu and stops world audio and gameplay");
        Check(!inventory.TryOpen()&&!FindAnyObjectByType<PlayerUpgradePanelUI>().TryOpen(),"Inventory and skill menus cannot open behind pause");yield return Capture("Pause_Menu");
        menu.ShowHelp();Check(Get<GameObject>(menu,"helpPage").activeSelf&&Time.timeScale==0,"Pause help shows controls without unpausing");
        Keys(Key.Escape);yield return Wait(.12f);Keys();yield return Wait(.1f);Check(menu.IsOpen&&!Get<GameObject>(menu,"helpPage").activeSelf,"Esc returns from help to pause choices");
        menu.AskMainMenu();menu.Back();Check(menu.IsOpen&&!Get<GameObject>(menu,"confirmPage").activeSelf,"Returning to title can be cancelled without leaving the world");
        menu.AskQuit();Check(Get<GameObject>(menu,"confirmPage").activeSelf,"Quit uses an explicit confirmation page");menu.Back();
        Get<Button>(menu,"playButton").onClick.Invoke();yield return Wait(.1f);Check(!menu.IsOpen&&Time.timeScale==1&&!AudioListener.pause,"Resume button restores normal time and sound");
        var sceneList=EditorBuildSettings.scenes;Check(sceneList[0].path.EndsWith("MainMenu.unity")&&sceneList[1].path.EndsWith("WorldPrototype.unity"),"Standalone build starts at the new main menu and includes the real world scene");
        menu.TryPause();menu.AskMainMenu();Get<Button>(menu,"confirmYes").onClick.Invoke();yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.MainScene,12);yield return null;yield return null;
        Check(SceneManager.GetActiveScene().name==GameMenuUI.MainScene&&Time.timeScale==1&&!AudioListener.pause,"Confirmed return loads the title scene without leaving global audio or time paused");
        menu=FindAnyObjectByType<GameMenuUI>();Check(menu!=null&&menu.IsOpen&&FindAnyObjectByType<PlayerHealth>()==null,"Title screen is independent of gameplay objects");yield return Capture("Main_Menu");
        menu.ShowHelp();Check(Get<GameObject>(menu,"helpPage").activeSelf,"Title help is usable");menu.Back();
        Get<Button>(menu,"playButton").onClick.Invoke();yield return null;var creator=FindAnyObjectByType<CharacterCreationUI>();if(creator!=null&&creator.IsOpen)creator.Confirm();yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.WorldScene,12);yield return null;yield return null;Bind();
        Check(SceneManager.GetActiveScene().name==GameMenuUI.WorldScene&&!menu.IsOpen&&Time.timeScale==1,"Title play button loads WorldPrototype ready for gameplay");
        Check(wallet.CaptureSave()==walletBefore,"Title roundtrip preserves player Gold, inventory, weapons and reward receipts");
        Check(Vector3.Distance(player.transform.position,FindAnyObjectByType<PlayerSceneSpawnPoint>().Position)<1,"Entering from title uses the established summoning spawn");
        Check(errors.Count==0,"No runtime errors across pickup, hover, pause, title and real scene transitions");
    }
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/MenuTests/"+name+".png");yield return Wait(.1f);}
    private void Check(bool okay,string label){report.AppendLine((okay?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/GameMenuPlaytest.txt",report.ToString());if(!okay)throw new InvalidOperationException(label);}
    private void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+trace);}
    private void Finish(Exception failure)
    {
        if(done)return;done=true;Application.logMessageReceived-=Log;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        if(wallet!=null&&walletBefore!=null)wallet.RestoreSave(walletBefore);
        if(failure!=null)report.AppendLine(failure.ToString());foreach(var e in errors)report.AppendLine("RUNTIME ERROR "+e);report.AppendLine(failure==null&&errors.Count==0?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/GameMenuPlaytest.txt",report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Game menu test interrupted"));}
}
#endif
