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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public sealed class GuildTradeRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
    private readonly List<string> errors=new List<string>();
    private PlayerMovement movement;private PlayerInventory inventory;private PlayerGoldWallet wallet;private PlayerHealth health;
    private PlayerInput input;private Keyboard keyboard;private Mouse mouse;private ReadOnlyArray<InputDevice>? devicesBefore;
    private bool persistBefore,autoSwitchBefore,backgroundBefore,done,passed=true;private int fpsBefore,vsyncBefore;private float timeBefore;
    private string goldBefore,saveTestKey;private GuildTradeUI ui;private GuildBuyerInteractable guild;private GamePauseController pause;
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static ItemDefinition Item(string id)=>AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Items/MonsterLoot/Items/"+id+".asset");
    private IEnumerator Start()
    {
        backgroundBefore=Application.runInBackground;fpsBefore=Application.targetFrameRate;vsyncBefore=QualitySettings.vSyncCount;timeBefore=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Guild E / mouse / sale / Gold regression "+DateTime.Now.ToString("s"));
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
        movement=FindAnyObjectByType<PlayerMovement>();inventory=movement.GetComponent<PlayerInventory>();wallet=movement.GetComponent<PlayerGoldWallet>();health=movement.GetComponent<PlayerHealth>();
        goldBefore=wallet.CaptureSave();persistBefore=Get<bool>(wallet,"persistGold");Set(wallet,"persistGold",false);wallet.RestoreSave("{\"version\":1,\"gold\":0,\"weapons\":[\"\",\"\"],\"selected\":-1}");
        inventory.RestoreContents(new ItemDefinition[PlayerInventory.Capacity],new int[PlayerInventory.Capacity]);
        ui=FindAnyObjectByType<GuildTradeUI>();guild=FindAnyObjectByType<GuildBuyerInteractable>();pause=FindAnyObjectByType<GamePauseController>();input=movement.GetComponent<PlayerInput>();
        FindAnyObjectByType<WorldMonsterDirector>().gameObject.SetActive(false);foreach(var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        if(input.actions.devices.HasValue)devicesBefore=new ReadOnlyArray<InputDevice>(input.actions.devices.Value.ToArray());
        autoSwitchBefore=input.neverAutoSwitchControlSchemes;input.neverAutoSwitchControlSchemes=true;
        keyboard=InputSystem.AddDevice<Keyboard>("GuildTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("GuildTestMouse");input.actions.devices=new InputDevice[]{keyboard,mouse};Keys();
        var bones=Item("monster_bone");var horns=Item("demon_horn");inventory.TryAdd(bones,5);inventory.TryAdd(horns,2);
        Check(guild.Contact.Role==CityServiceRole.GuildBuyer&&guild.Contact.Building!=null,"Only the existing Gild female buyer receives the selling interaction");
        Check(FindObjectsByType<GuildBuyerInteractable>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None).Length==3,"Hotel and Potion NPC roles remain unchanged");
        Warp(guild.transform.position+Vector3.forward*10);yield return Wait(.15f);
        Check(!guild.CanInteract(movement.gameObject)&&!ui.TryOpen(guild),"Distant players cannot open the guild dialogue");
        Warp(guild.Contact.InteractionPoint.position);yield return Wait(.4f);
        var interactor=movement.GetComponent<PlayerInteractor>();
        Check(interactor.CurrentInteractable==guild&&guild.CanInteract(movement.gameObject),"Standing at the real guild approach discovers its E prompt");
        Keys(Key.E);yield return Wait(.1f);Keys();yield return Wait(.05f);
        Check(ui.IsOpen&&!ui.IsSelling&&Time.timeScale==0&&Cursor.visible,"Pressing real E opens dialogue with a mouse cursor and paused gameplay");
        var texts=Get<GameObject>(ui,"dialoguePage").GetComponentsInChildren<TMP_Text>();
        Check(texts.Any(t=>t.text==GuildTradeUI.Greeting)&&texts.Any(t=>t.text==GuildTradeUI.SellChoice)&&texts.Any(t=>t.text==GuildTradeUI.LeaveChoice),"Exact requested Thai greeting and both dialogue choices are visible");
        foreach(var t in texts)
        {
            t.ForceMeshUpdate();Check(t.text.All(c=>char.IsWhiteSpace(c)||t.font.HasCharacter(c,true,true)),"Thai and Latin glyphs resolve in "+t.name);
        }
        Check(!FindAnyObjectByType<InventoryPanelUI>().TryOpen()&&!FindAnyObjectByType<PlayerUpgradePanelUI>().TryOpen(),"Inventory and upgrades cannot overlap trading");
        Vector3 position=movement.transform.position;Keys(Key.W,Key.J,Key.Space,Key.M,Key.E);yield return Wait(.2f);
        Check(movement.transform.position==position&&!movement.IsDashing&&!movement.IsShadowDashing&&movement.GetComponent<PlayerRangedWeapon>().PendingTarget==null,"Movement, shooting, both dashes and repeated interaction stay blocked during dialogue");Keys();
        yield return Screenshot("GuildTrade_Dialogue");
        yield return Click(Get<Button>(ui,"sellChoice"));
        Check(ui.IsSelling,"An actual mouse click chooses the sell-materials answer");
        var rows=Get<GuildTradeUI.Row[]>(ui,"rows");yield return Click(rows[0].button);
        Check(ui.SelectedSlot==0&&ui.SelectedQuantity==1&&rows[0].icon.sprite==bones.Icon&&rows[0].price.text.Contains("4 Gold"),"Mouse selection shows the real inventory icon and per-unit selling price");
        yield return Click(Get<Button>(ui,"moreButton"));yield return Click(Get<Button>(ui,"moreButton"));
        Check(ui.SelectedQuantity==3&&Get<TMP_Text>(ui,"totalLabel").text.Contains("12 Gold"),"Quantity controls preview the exact payout before a sale");
        yield return Click(Get<Button>(ui,"sellButton"));
        Check(inventory.GetQuantity(0)==2&&wallet.Gold==12&&ui.Status.Contains("12 Gold"),"Selling three bones removes exactly three and grants 12 Gold");
        Check(FindAnyObjectByType<PlayerGoldUI>().DisplayedGold.Contains("12"),"Gold HUD reflects a completed sale immediately");
        yield return Click(Get<Button>(ui,"stackButton"));yield return Click(Get<Button>(ui,"sellButton"));
        Check(inventory.GetItem(0)==null&&wallet.Gold==20,"Whole-stack sale clears its resource slot and credits the remaining value");
        ui.SellSelected();ui.SellSelected();Check(wallet.Gold==20,"Rapid stale clicks cannot sell an already removed stack twice");
        yield return Click(rows[1].button);yield return Click(Get<Button>(ui,"stackButton"));
        yield return Screenshot("GuildTrade_Selling");
        yield return Click(Get<Button>(ui,"sellButton"));
        Check(wallet.Gold==50&&inventory.OccupiedSlots==0&&!Get<Button>(ui,"sellButton").interactable,"Selling the last horns pays their higher value and disables empty sales");
        yield return Click(Get<Button>(ui,"backButton"));yield return Click(Get<Button>(ui,"leaveChoice"));
        Check(!ui.IsOpen&&!pause.IsPaused&&Time.timeScale==1,"The requested no-more-business mouse choice restores gameplay");
        Keys(Key.E);yield return Wait(.1f);Keys();yield return Wait(.05f);yield return Click(Get<Button>(ui,"sellChoice"));
        Check(ui.IsSelling&&ui.Status.Contains("ยังไม่มี"),"An empty bag opens a clear empty-state selling page");
        Keys(Key.Escape);yield return Wait(.08f);Keys();Check(!ui.IsOpen&&Time.timeScale==1,"Escape exits selling and restores the previous time state");
        // Verify inventory/wallet atomicity and reject stale slot, wrong type, over-quantity and overflow.
        inventory.TryAdd(bones,4);int gold=wallet.Gold;
        Check(!inventory.TrySell(0,horns,1,wallet)&&!inventory.TrySell(0,bones,5,wallet)&&!inventory.TrySell(-1,bones,1,wallet)&&wallet.Gold==gold&&inventory.GetQuantity(0)==4,"Invalid or stale sales change neither items nor currency");
        var quest=ScriptableObject.CreateInstance<ItemDefinition>();temporary.Add(quest);Set(quest,"category",InventoryItemCategory.Quest);Set(quest,"sellPrice",100);inventory.TryAdd(quest);
        Check(!inventory.TrySell(1,quest,1,wallet)&&inventory.GetQuantity(1)==1,"Quest objects cannot be sold even when assigned a price");
        wallet.RestoreSave("{\"version\":1,\"gold\":999999998}");
        Check(!inventory.TrySell(0,bones,1,wallet)&&inventory.GetQuantity(0)==4&&wallet.Gold==PlayerGoldWallet.MaximumGold-1,"Gold overflow rejects the entire sale without consuming items");
        wallet.RestoreSave("{\"version\":1,\"gold\":50}");
        bool observedAtomic=false;Action changed=()=>observedAtomic=inventory.GetQuantity(0)==3&&wallet.Gold==54;inventory.Changed+=changed;
        Check(inventory.TrySell(0,bones,1,wallet)&&observedAtomic,"Sale observers see inventory and currency already updated together");inventory.Changed-=changed;
        string saved=wallet.CaptureSave();wallet.RestoreSave("{\"version\":1,\"gold\":0}");wallet.RestoreSave(saved);
        Check(wallet.Gold==54&&!wallet.TrySpend(55)&&wallet.TrySpend(4)&&wallet.Gold==50,"Saved Gold roundtrips and future purchasing rejects insufficient funds");
        string unchanged=wallet.CaptureSave();Check(!wallet.RestoreSave("{}")&&!wallet.RestoreSave("{\"version\":1,\"gold\":-1}")&&!wallet.RestoreSave("{\"version\":3,\"gold\":999}")&&wallet.CaptureSave()==unchanged,"Malformed or future Gold saves cannot overwrite a valid balance");
        saveTestKey="TopDownShooter3D.Gold.Test."+Guid.NewGuid().ToString("N");
        var writer=new GameObject("Temporary Gold save writer");temporary.Add(writer);writer.SetActive(false);var w=writer.AddComponent<PlayerGoldWallet>();Set(w,"saveKey",saveTestKey);writer.SetActive(true);w.TryCredit(135);w.TrySpend(20);Destroy(writer);yield return null;
        var reader=new GameObject("Temporary Gold save reader");temporary.Add(reader);reader.SetActive(false);var read=reader.AddComponent<PlayerGoldWallet>();Set(read,"saveKey",saveTestKey);reader.SetActive(true);
        Check(read.Gold==115,"A new wallet instance reloads committed sale/purchase currency");
        for(int i=0;i<PlayerInventory.Capacity;i++)if(inventory.GetItem(i)!=null)inventory.TryRemove(i,inventory.GetQuantity(i));
        var materials=AssetDatabase.FindAssets("t:ItemDefinition",new[]{"Assets/_Project/Items/MonsterLoot/Items"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(i=>i.ItemId).ToArray();
        foreach(var material in materials)inventory.TryAdd(material,3);
        Check(inventory.OccupiedSlots==8&&movement.GetComponent<PlayerMeleeEquipment>().WeaponCount==0,"A full resource bag is presented without using or selling the two melee slots");
        Check(ui.TryOpen(guild),"The guild accepts a full resource bag");ui.ShowSelling();
        int dragon=Array.FindIndex(materials,i=>i.ItemId=="dragon_scale");ui.SelectSlot(dragon);ui.SetQuantity(3);
        yield return Wait(.15f);Check(ui.IsSelling&&Get<Button>(ui,"sellButton").interactable,"Full-bag material selection stays open with a valid sale action");
        yield return Screenshot("GuildTrade_Selling");ui.Close();yield return Wait(.12f);
        // Do not remove other pause owners, even if the NPC/window goes away.
        Keys(Key.E);yield return Wait(.1f);Keys();yield return Wait(.04f);Check(ui.IsOpen,"Guild dialogue reopens normally after multiple sales");
        pause.SetPaused(GamePauseReason.Inventory,true);ui.Close();Check(Time.timeScale==0&&pause.Reasons==GamePauseReason.Inventory,"Closing trade cannot clear another system's pause reason");pause.SetPaused(GamePauseReason.Inventory,false);
        Keys(Key.E);yield return Wait(.1f);Keys();yield return Wait(.04f);ui.enabled=false;Check(!ui.IsOpen&&!pause.IsPaused&&Time.timeScale==1,"Disabling trading UI releases its pause and leaves gameplay usable");ui.enabled=true;
        Keys(Key.E);yield return Wait(.1f);Keys();yield return Wait(.04f);Check(ui.IsOpen,"Re-enabled UI subscribes to interaction once");
        health.TakeDamage(99999);yield return Wait(.1f);
        Check(!ui.IsOpen&&pause.IsGameOver&&Time.timeScale==0,"Death closes guild trading while retaining the Game Over pause");
        Check(errors.Count==0,"No runtime errors or missing glyph warnings in dialogue, mouse, selling, save or death");
    }
    private IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();Vector2 position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return Wait(.05f);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position}.WithButton(MouseButton.Left));yield return Wait(.05f);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return Wait(.07f);
    }
    private void Warp(Vector3 position){var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.position=position+Vector3.up*.03f;cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Screenshot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/"+name+".png",2);yield return Wait(.1f);}
    private void Check(bool valid,string label){passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/GuildTradePlaytest.txt",report.ToString());if(!valid)throw new InvalidOperationException(label);}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||type==LogType.Warning&&message.Contains("Unicode"))errors.Add(message+"\n"+trace);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}foreach(var e in errors){passed=false;report.AppendLine("RUNTIME ERROR "+e);}
        Keys();if(ui!=null)ui.Close();if(wallet!=null&&goldBefore!=null){wallet.RestoreSave(goldBefore);Set(wallet,"persistGold",persistBefore);}
        if(input!=null){input.actions.devices=devicesBefore;input.neverAutoSwitchControlSchemes=autoSwitchBefore;}
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
        foreach(var obj in temporary)if(obj!=null)Destroy(obj);if(saveTestKey!=null){PlayerPrefs.DeleteKey(saveTestKey);PlayerPrefs.Save();}
        Application.logMessageReceived-=OnLog;Application.runInBackground=backgroundBefore;Application.targetFrameRate=fpsBefore;QualitySettings.vSyncCount=vsyncBefore;Time.timeScale=timeBefore;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");File.WriteAllText("Logs/GuildTradePlaytest.txt",report.ToString());Debug.Log("Guild trading regression complete. Passed="+passed);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("Guild trade test interrupted."));}
}
#endif
