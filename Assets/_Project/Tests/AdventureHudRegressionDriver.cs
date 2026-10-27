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
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public sealed class AdventureHudRegressionDriver : MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private TownMusicZone city;private AdventureHudUI hud;private PlayerMovement movement;private PlayerInput input;
    private InventoryPanelUI inventory;private GamePauseController pause;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? previousDevices;
    private ItemDefinition supply;private bool passed=true,done,oldBackground;private float oldScale;private int oldFps,oldVsync;private string runtimeError;
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldScale=Time.timeScale;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
        report.AppendLine("Actual city music / adventure HUD "+DateTime.Now.ToString("s"));var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0)
        {
            object next=null;Exception error=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){error=e;}
            if(error!=null){Finish(error);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        foreach(var spawner in FindObjectsByType<EnemyWaveSpawner>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var enemy in FindObjectsByType<EnemyAI>())enemy.gameObject.SetActive(false);
        city=FindFirstObjectByType<TownMusicZone>();hud=FindFirstObjectByType<AdventureHudUI>();movement=FindFirstObjectByType<PlayerMovement>();
        input=movement.GetComponent<PlayerInput>();inventory=FindFirstObjectByType<InventoryPanelUI>();pause=FindFirstObjectByType<GamePauseController>();
        movement.enabled=false;var devices=input.actions.devices;if(devices.HasValue)previousDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());
        keyboard=InputSystem.AddDevice<Keyboard>("AdventureHudTestKeyboard");input.actions.devices=new InputDevice[]{keyboard};
        yield return Wait(.3f);
        Check(!city.IsInside&&city.MusicSource.volume==0&&!city.MusicSource.isPlaying,"Starting outside the city is silent; music never auto-plays at spawn");
        Check(city.MusicSource.loop&&city.MusicSource.spatialBlend==0&&FindObjectsByType<TownMusicZone>().Length==1,"One dedicated looping 2D town music source");
        Warp(new Vector3(75.76f,5.2f,-55));yield return Wait(.3f);
        Check(city.IsInside&&city.MusicSource.isPlaying&&city.MusicSource.volume>0&&city.MusicSource.volume<.02f,"Entering the real city starts the requested clip near silence");
        float first=city.MusicSource.volume;yield return Wait(1f);
        Check(city.MusicSource.volume>first&&city.MusicSource.volume<city.MusicVolume*.8f,"Music ramps gradually instead of jumping to full background volume");
        pause.SetPaused(GamePauseReason.Inventory,true);yield return null;float envelope=city.Envelope,volume=city.MusicSource.volume,audioTime=city.MusicSource.time;
        yield return Wait(.4f);
        Check(Time.timeScale==0&&AudioListener.pause&&Mathf.Abs(city.Envelope-envelope)<.001f&&Mathf.Abs(city.MusicSource.volume-volume)<.001f&&Mathf.Abs(city.MusicSource.time-audioTime)<.1f,"Inventory pause freezes both music playback and the fade envelope");
        pause.SetPaused(GamePauseReason.Inventory,false);yield return Wait(city.FadeInSeconds);
        Check(Mathf.Abs(city.MusicSource.volume-.22f)<.001f&&city.MusicSource.time>1f,"Music reaches the moderate 22 percent level and really advances playback");
        var samples=new float[2048];float rms=0f;float sampleUntil=Time.realtimeSinceStartup+.4f;
        // The DSP output tap is established by the first request; allow subsequent
        // audio blocks to arrive before checking a streaming source's samples.
        while(Time.realtimeSinceStartup<sampleUntil){city.MusicSource.GetOutputData(samples,0);rms=Mathf.Max(rms,Mathf.Sqrt(samples.Sum(s=>s*s)/samples.Length));yield return null;}
        report.AppendLine("MUSIC output RMS="+rms.ToString("F5")+" listener="+AudioListener.volume+" virtual="+city.MusicSource.isVirtual+" load="+city.MusicSource.clip.loadState);
        Check(rms>.00001f,"Streaming audio produces nonzero output samples");
        Warp(new Vector3(75.76f,5.2f,-50.1f));yield return Wait(.25f);
        Check(city.IsInside&&Mathf.Abs(city.MusicSource.volume-.22f)<.001f,"Exit margin prevents music pumping at the gate boundary");
        Warp(new Vector3(75.76f,5.2f,-47));yield return Wait(.5f);volume=city.MusicSource.volume;
        Check(!city.IsInside&&volume>0&&volume<.22f,"Leaving the city gradually fades the music out");
        audioTime=city.MusicSource.time;Warp(new Vector3(75.76f,5.2f,-55));yield return null;
        Check(city.IsInside&&Mathf.Abs(city.MusicSource.volume-volume)<.02f&&city.MusicSource.time>=audioTime-.1f,"Re-entering during fade reverses smoothly without restarting the song");
        Warp(new Vector3(75.76f,5.2f,-47));yield return Wait(city.FadeOutSeconds+.2f);
        audioTime=city.MusicSource.time;Check(city.MusicSource.volume==0&&!city.MusicSource.isPlaying,"Music becomes fully silent and pauses after the exit fade");
        Warp(new Vector3(75.76f,5.2f,-75.8f));yield return Wait(.5f);
        Check(city.MusicSource.isPlaying&&city.MusicSource.time>audioTime&&city.MusicSource.time<audioTime+1.3f,"Returning resumes the existing song position with a new fade-in");
        yield return Wait(.3f);Check(hud.DisplayedArea==city.AreaName,"HUD location reflects the real city zone");
        var progression=movement.GetComponent<PlayerProgressionState>();var progressHud=FindFirstObjectByType<PlayerProgressionUI>();
        string progressionBefore=progression.CaptureSave();
        progression.RestoreSave("{\"version\":1,\"level\":1,\"experience\":0}");
        Check(progression!=null&&progressHud.DisplayedLevel=="LV. 1"&&progressHud.DisplayedRank==PlayerProgressionState.RankForLevel(1)&&progressHud.DisplayedProgress==0,"Level/rank display starts at level one, novice and empty progress");
        Check(FindFirstObjectByType<PlayerHealthUI>().GetComponentsInChildren<TMP_Text>().Length==0&&FindFirstObjectByType<PlayerEnergyUI>().GetComponentsInChildren<TMP_Text>().Length==0,"Health and energy bars display no labels or numeric text");
        int previewXp=Mathf.RoundToInt(PlayerProgressionState.ExperienceForLevel(7)*.65f);
        progression.RestoreSave("{\"version\":1,\"level\":7,\"experience\":"+previewXp+"}");
        Check(progressHud.DisplayedLevel=="LV. 7"&&progressHud.DisplayedRank==PlayerProgressionState.RankForLevel(7)&&Mathf.Abs(progressHud.DisplayedProgress-.65f)<.001f,"Gold bar, level and derived rank respond to actual EXP");
        yield return Capture("Level_Rank_Preview");progression.RestoreSave(progressionBefore);
        yield return Capture("City_Default");

        var health=movement.GetComponent<PlayerHealth>();health.TakeDamage(27f);movement.GetComponent<PlayerEnergy>().ConsumeShadowDashEnergy(.8f);yield return null;
        var healthView=FindFirstObjectByType<PlayerHealthUI>();var energyView=FindFirstObjectByType<PlayerEnergyUI>();
        Check(Mathf.Abs(Get<Image>(healthView,"healthFill").fillAmount-.73f)<.001f&&Get<TMP_Text>(healthView,"healthText").text.Contains("73"),"Fantasy health fill and number react to real damage");
        Check(Get<Image>(energyView,"energyFill").fillAmount<.9f&&Get<TMP_Text>(energyView,"energyText").text!="100 / 100","Blue shadow-energy bar uses the existing energy value");
        var hotbar=FindFirstObjectByType<InventoryHotbarUI>();var slots=Get<InventoryHotbarUI.SlotView[]>(hotbar,"slots");
        Check(slots.Length==10&&FindObjectsByType<InventoryHotbarUI>().Length==1,"One active ten-slot hotbar replaces the old presentation");
        movement.enabled=true;
        var equipment=movement.GetComponent<PlayerMeleeEquipment>();var weapon=AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>("Assets/_Project/Prefabs/Weapons/TrainingClub.asset");
        Check(equipment.TryEquip(weapon)&&equipment.TryEquip(weapon),"Existing two-weapon equipment accepts live HUD test items");
        Check(slots[8].frame.sprite==Get<Sprite>(hotbar,"selectedSlotSprite")&&slots[9].button.interactable,"Equipped weapon highlights the ninth slot; the tenth is selectable");
        slots[9].button.onClick.Invoke();Check(equipment.SelectedSlotIndex==1&&slots[9].frame.sprite==Get<Sprite>(hotbar,"selectedSlotSprite"),"Clicking the second melee slot changes the original equipment selection");
        supply=ScriptableObject.CreateInstance<ItemDefinition>();Set(supply,"displayName","Forest Herb");Set(supply,"maxStack",20);
        Check(movement.GetComponent<PlayerInventory>().TryAdd(supply,3)&&slots[0].itemName.text=="Forest Herb"&&slots[0].stateLabel.text=="x3","Supply stack name and quantity update through the original inventory API");
        movement.enabled=true;Keys(Key.Space);yield return Wait(.08f);
        Check(movement.IsDashing&&hud.DisplayedDashCooldown>.7f,"Dash HUD cooldown follows an actual input-triggered dash");
        Keys();yield return Wait(.9f);movement.enabled=false;
        Check(hud.DisplayedDashCooldown<.01f,"Dash HUD returns to ready when the real cooldown ends");
        yield return Capture("City");
        Check(inventory.TryOpen()&&Time.timeScale==0,"Restyled inventory button/modal retains pause behaviour");yield return Capture("Inventory");
        Check(inventory.DetailTitle.Length>0&&Get<Button>(inventory,"closeButton").interactable,"Themed inventory retains item details and its working close button");
        Get<Button>(inventory,"closeButton").onClick.Invoke();Check(!inventory.IsOpen&&Time.timeScale==1,"Red cancel button closes the inventory and restores gameplay");
        var views=FindFirstObjectByType<CameraViewSettings>();views.ApplyView(CameraViewMode.TopDown,false);
        var rig=views.GetComponent<CameraFollow>();rig.transform.rotation=Quaternion.Euler(0,90,0);yield return Wait(.2f);
        Check(Mathf.Abs(Mathf.DeltaAngle(Get<RectTransform>(hud,"compass").localEulerAngles.z,90f))<.1f,"Compass north dial responds to the camera heading");views.ApplyView(CameraViewMode.TopDown,false);
        var hudRect=hud.GetComponent<RectTransform>();var corners=new Vector3[4];hudRect.GetWorldCorners(corners);
        bool onScreen=true;
        foreach(var branch in new[]{"Vitals","Area Banner","Compass","Abilities","Combat Readout","Inventory Hotbar"})
        {
            var child=hud.transform.Find(branch).GetComponent<RectTransform>();var points=new Vector3[4];child.GetWorldCorners(points);
            onScreen&=points.All(p=>p.x>=corners[0].x-2&&p.x<=corners[2].x+2&&p.y>=corners[0].y-2&&p.y<=corners[2].y+2);
        }
        Check(onScreen,"All principal HUD groups remain inside the current game viewport");
        health.TakeDamage(1000f);yield return Wait(.2f);Check(pause.IsGameOver&&Time.timeScale==0&&AudioListener.pause,"Restyled game-over state still pauses time and audio");yield return Capture("GameOver");
        Check(runtimeError==null,"No runtime errors through town audio, HUD stats, equipment, inventory, compass and death");
    }
    private void Warp(Vector3 position){var cc=movement.GetComponent<CharacterController>();cc.enabled=false;movement.transform.position=position;cc.enabled=true;Physics.SyncTransforms();}
    private void Keys(params Key[] keys){if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    private static IEnumerator Capture(string label){yield return new WaitForEndOfFrame();Directory.CreateDirectory("Logs");ScreenCapture.CaptureScreenshot("Logs/AdventureHUD_"+label+".png",1);yield return Wait(.15f);}
    private void Check(bool value,string label){passed&=value;report.AppendLine((value?"PASS ":"FAIL ")+label);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AdventureHudPlaytest.txt",report.ToString());}
    private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    private void Finish(Exception error)
    {
        if(done)return;done=true;if(error!=null){passed=false;report.AppendLine("EXCEPTION "+error);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        Keys();if(input!=null)input.actions.devices=previousDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        if(supply!=null)Destroy(supply);if(pause!=null)pause.ResetForRestart();Time.timeScale=oldScale;
        Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AdventureHudPlaytest.txt",report.ToString());
        if(passed)Debug.Log(report.ToString());else Debug.LogError(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!done)Finish(new InvalidOperationException("HUD regression interrupted"));}
}
#endif
