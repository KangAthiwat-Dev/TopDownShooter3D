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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AlertMenuPresentationRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();private readonly List<string> errors=new List<string>();
    private PlayerHealth player;private PlayerStoryProgress story;private PlayerProgressionState progression;private PlayerGoldWallet wallet;private GameAlertsUI alerts;private GameMenuUI menu;
    private Keyboard keyboard;private bool done,oldBackground;private int oldFps,oldVsync;
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=Log;
        keyboard=InputSystem.AddDevice<Keyboard>("PresentationTestKeyboard");var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private void Bind()
    {
        player=FindAnyObjectByType<PlayerHealth>();story=player.GetComponent<PlayerStoryProgress>();progression=player.GetComponent<PlayerProgressionState>();wallet=player.GetComponent<PlayerGoldWallet>();alerts=FindAnyObjectByType<GameAlertsUI>();menu=FindAnyObjectByType<GameMenuUI>();
        Set(wallet,"persistGold",false);Set(story,"persistStory",false);Set(progression,"persistProgress",false);
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerAttackController>().enabled=false;
        var population=FindAnyObjectByType<WorldMonsterDirector>();if(population!=null)population.gameObject.SetActive(false);
        foreach(var wave in FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None)){wave.StopAllCoroutines();wave.enabled=false;}
        foreach(var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))ai.gameObject.SetActive(false);
        foreach(var e in FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None))e.enabled=false;
        foreach(var follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))follow.enabled=false;
    }
    private IEnumerator Checks()
    {
        yield return null;Bind();
        PlayerPrefs.DeleteKey(PlayerAppearanceProfile.SaveKey);
        Check(Get<GameAlertsUI.Art[]>(alerts,"art").Length==8&&Get<GameAlertsUI.Art[]>(alerts,"art").All(a=>a.sprite!=null),"All eight non-modal artwork banners have valid imported sprites");
        var hud=FindAnyObjectByType<AdventureHudUI>();hud.enabled=false;ClearAlerts();
        progression.RestoreSave("{\"version\":3,\"level\":1,\"experience\":0,\"upgrades\":[0,0,0,0,0,0,0,0,0,0,0]}");progression.GainExperience(100);yield return Wait(.4f);
        Check(alerts.IsVisible&&alerts.CurrentKind==GameAlertKind.LevelUp&&progression.Level==2,"Real EXP level-up triggers Level Up artwork with Thai level details");
        Check(Time.timeScale==1&&!Get<CanvasGroup>(alerts,"group").blocksRaycasts&&alerts.GetComponent<GraphicRaycaster>()==null,"Notifications do not pause movement or intercept UI input");yield return Capture("Level_Up");
        Check(menu.TryPause(),"Existing pause menu still opens");Get<Button>(menu,"optionsButton").onClick.Invoke();Check(Get<GameObject>(menu,"optionsPage").activeSelf&&Time.timeScale==0,"Pause Options page is ready without changing gameplay settings");menu.Back();
        Set(progression,"persistProgress",true);Get<Button>(menu,"saveButton").onClick.Invoke();yield return Wait(.4f);
        Check(alerts.IsVisible&&alerts.CurrentKind==GameAlertKind.GameSaved&&PlayerPrefs.GetString("TopDownShooter3D.Progression.v1").Contains("\"level\":2"),"Save button actually flushes progress and shows Game Saved while paused");Set(progression,"persistProgress",false);yield return Capture("Game_Saved");menu.Resume();ClearAlerts();

        wallet.RestoreSave("{\"version\":2,\"gold\":0}");player.GetComponent<PlayerInventory>().RestoreContents(new ItemDefinition[8],new int[8]);
        int mask=(1<<story.DefenseQuest.requiredCount)-1;
        Check(story.RestoreSave("{\"version\":1,\"moved\":8,\"shot\":true,\"dashed\":true,\"guardMet\":true,\"defense\":3,\"wolf\":0,\"defenseMask\":"+mask+"}"),"Quest reward fixture uses a valid ready-to-turn-in state");
        Check(story.TryClaimReward(story.DefenseQuest,out _),"Actual quest reward transaction succeeds");yield return Wait(.4f);
        Check(alerts.IsVisible&&alerts.CurrentKind==GameAlertKind.QuestCompleted,"Quest completion artwork follows a successful reward claim");yield return Capture("Quest_Completed");
        Check(!story.TryClaimReward(story.DefenseQuest,out _),"Repeated reward clicks cannot award or announce the same completed quest again");ClearAlerts();
        var victory=Get<DemonArenaEncounter[]>(alerts,"victories")[0];Set(victory,"<Completed>k__BackingField",true);
        Get<Action>(victory,"Changed")?.Invoke();yield return Wait(.4f);Check(alerts.IsVisible&&alerts.CurrentKind==GameAlertKind.Victory,"Completed boss encounter signal displays Victory artwork");yield return Capture("Victory");ClearAlerts();

        // Feed the same authoritative area label the unchanged region detector publishes.
        string[] names={"SILVERWELL TOWN","OAKSHADE VILLAGE","THORNVEIL FOREST","MALACHAR'S CASTLE"};
        GameAlertKind[] kinds={GameAlertKind.Town,GameAlertKind.Village,GameAlertKind.Forest,GameAlertKind.Castle};
        for(int i=0;i<names.Length;i++)
        {
            ClearAlerts();Get<TMP_Text>(hud,"areaName").text=names[i];yield return Wait(1.25f);
            Check(alerts.IsVisible&&alerts.CurrentKind==kinds[i],"Area change displays matching artwork: "+names[i]);yield return Capture("Zone_"+kinds[i]);
        }
        ClearAlerts();wallet.RestoreSave("{\"version\":2,\"gold\":0}");story.RestoreSave("{\"version\":1,\"moved\":8,\"shot\":true,\"dashed\":true,\"guardMet\":true,\"defense\":2,\"wolf\":0,\"defenseMask\":0}");string attempt=story.CaptureSave();
        player.TakeDamage(999999);var death=player.GetComponent<PlayerDeathPresentation>();yield return Wait(death.GameOverDelay+.65f);
        var gameOver=FindAnyObjectByType<GameOverUI>();var deathWindow=Get<GameObject>(gameOver,"gameOverPanel");
        Check(deathWindow.activeSelf&&Time.timeScale==0&&deathWindow.GetComponent<CanvasGroup>().alpha>.99f,"Defeat UI fades in after the full existing death animation while world stays paused");
        Check(deathWindow.transform.Find("Defeat artwork").GetComponent<Image>().sprite!=null&&Get<GameObject>(gameOver,"questFailedBadge").activeSelf,"Defeat and Quest Failed artwork appear for a failed active quest attempt");
        Check(story.CaptureSave()==attempt&&!alerts.IsVisible,"Death preserves quest progress and clears ordinary banner notifications");yield return Capture("Defeat_Quest_Attempt");
        deathWindow.transform.Find("Return to Main Menu").GetComponent<Button>().onClick.Invoke();yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.MainScene,12);yield return Wait(.2f);
        menu=FindAnyObjectByType<GameMenuUI>();Check(menu!=null&&Time.timeScale==1&&!AudioListener.pause,"Defeat return button loads main menu with time and audio restored");
        var background=FindObjectsByType<RawImage>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(r=>r.name=="Kingdom Background");
        Check(background.texture!=null&&background.texture.name=="MenuGame"&&background.color==Color.white,"Main menu uses the supplied Screen/MenuGame artwork without tinting");
        var play=Get<Button>(menu,"playButton");var corners=new Vector3[4];play.GetComponent<RectTransform>().GetWorldCorners(corners);
        Check(corners[2].x<Screen.width*.4f,"Main menu choices are positioned on the left, clear of the supplied logo");yield return Capture("Main_Menu");
        Get<Button>(menu,"optionsButton").onClick.Invoke();Check(Get<GameObject>(menu,"optionsPage").activeSelf,"Title Options button opens its prepared page");Keys(Key.Escape);yield return Wait(.15f);Keys();yield return Wait(.1f);Check(!Get<GameObject>(menu,"optionsPage").activeSelf&&Get<GameObject>(menu,"choices").activeSelf,"Esc returns from Options to main choices");
        menu.ShowHelp();Check(Get<GameObject>(menu,"helpPage").activeSelf,"How-to-play page remains usable with Thai controls");menu.Back();menu.AskQuit();Get<Button>(menu,"confirmNo").onClick.Invoke();Check(Get<GameObject>(menu,"choices").activeSelf,"Quit can still be cancelled safely");

        play.onClick.Invoke();yield return Wait(.25f);var creator=FindAnyObjectByType<CharacterCreationUI>();Check(creator!=null&&creator.IsOpen&&SceneManager.GetActiveScene().name==GameMenuUI.MainScene,"First play opens appearance selection before loading the game");
        Check(creator.PreviewAppearance.HairStyleCount==3&&Get<RawImage>(creator,"portrait").texture!=null,"Character creator renders an actual 3D HumanMale preview with all existing hairstyles");
        Get<Button[]>(creator,"skinButtons")[2].onClick.Invoke();Get<Button[]>(creator,"hairButtons")[3].onClick.Invoke();Get<Button[]>(creator,"styleButtons")[1].onClick.Invoke();yield return Wait(.3f);
        var draft=creator.PreviewAppearance.CurrentAppearance;Check(draft.hairStyleId=="short"&&draft.hairColor.r>.8f,"Skin, hair colour and hairstyle buttons update the live preview");yield return Capture("Character_Creator");
        Keys(Key.Escape);yield return Wait(.15f);Keys();yield return Wait(.1f);Check(!creator.IsOpen&&!PlayerPrefs.HasKey(PlayerAppearanceProfile.SaveKey),"Cancelling a first appearance draft does not create or overwrite a profile");
        string progressBefore=PlayerPrefs.GetString("TopDownShooter3D.Progression.v1"),goldBefore=PlayerPrefs.GetString("TopDownShooter3D.Gold.v1");
        play.onClick.Invoke();yield return Wait(.1f);Get<Button[]>(creator,"skinButtons")[2].onClick.Invoke();Get<Button[]>(creator,"hairButtons")[3].onClick.Invoke();Get<Button[]>(creator,"styleButtons")[1].onClick.Invoke();Get<Button>(creator,"confirm").onClick.Invoke();
        yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.WorldScene,12);yield return null;yield return null;Bind();
        var visual=player.GetComponentInChildren<PlayerAppearance>(true);Check(PlayerAppearanceProfile.TryRead(out var saved)&&visual.CurrentAppearance.hairStyleId==saved.hairStyleId&&visual.CurrentAppearance.hairColor==saved.hairColor&&visual.CurrentAppearance.skinColor==saved.skinColor,"Confirmed look survives menu-to-world loading and applies to the actual player");
        Check(PlayerPrefs.GetString("TopDownShooter3D.Progression.v1")==progressBefore&&PlayerPrefs.GetString("TopDownShooter3D.Gold.v1")==goldBefore,"Appearance saving preserves existing progression, Gold and loadout saves");
        yield return Capture("Saved_Look_In_Game");
        menu.TryPause();menu.AskMainMenu();Get<Button>(menu,"confirmYes").onClick.Invoke();yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.MainScene,12);yield return Wait(.2f);
        menu=FindAnyObjectByType<GameMenuUI>();creator=FindAnyObjectByType<CharacterCreationUI>();Get<Button>(menu,"customizeButton").onClick.Invoke();Get<Button[]>(creator,"skinButtons")[5].onClick.Invoke();Get<Button>(creator,"cancel").onClick.Invoke();
        Check(PlayerAppearanceProfile.TryRead(out var unchanged)&&unchanged.skinColor==saved.skinColor,"Cancelling a later edit leaves the saved appearance intact");
        Get<Button>(menu,"playButton").onClick.Invoke();yield return Until(()=>SceneManager.GetActiveScene().name==GameMenuUI.WorldScene,12);yield return null;yield return null;Bind();Check(player!=null&&player.IsAlive&&Time.timeScale==1,"Returning players enter the world directly using their saved appearance");
        Check(errors.Count==0,"No runtime errors across alerts, death, scene changes, creator rendering and profile loading");
    }
    private void ClearAlerts(){Get<IList>(alerts,"queue").Clear();Set(alerts,"current",null);Get<CanvasGroup>(alerts,"group").alpha=0;}
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Until(Func<bool> ready,float seconds){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    private IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/PresentationTests/"+name+".png");yield return Wait(.1f);}
    private void Check(bool value,string label){report.AppendLine((value?"PASS ":"FAIL ")+label);File.WriteAllText("Logs/PresentationPlaytest.txt",report.ToString());if(!value)throw new InvalidOperationException(label);}
    private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null)report.AppendLine("FAIL "+error);foreach(var e in errors)report.AppendLine("ERROR "+e);
        File.WriteAllText("Logs/PresentationPlaytest.txt",report.ToString());Application.logMessageReceived-=Log;
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new Exception("Test interrupted"));}
}
#endif
