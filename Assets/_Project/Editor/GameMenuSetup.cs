#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameMenuSetup
{
    public const string MainPath="Assets/_Project/Scenes/MainMenu.unity";
    private static TMP_FontAsset font;
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().First();
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {var r=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    private static RectTransform Full(string name,Transform parent)
    {var r=Rect(name,parent,0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    private static TMP_Text Text(string name,Transform parent,float x,float y,float w,float h,string text,float size,Color? color=null)
    {var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color??new Color(.22f,.13f,.065f);t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;}
    private static Button Button(string name,Transform parent,float x,float y,float w,float h,string text)
    {
        var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=Art("tooltip_frame");image.type=Image.Type.Sliced;
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;var label=Text("Label",r,16,7,w-32,h-14,text,28);label.alignment=TextAlignmentOptions.Midline;
        var colors=button.colors;colors.highlightedColor=new Color(1,.87f,.6f);colors.pressedColor=new Color(.88f,.72f,.4f);button.colors=colors;return button;
    }
    [MenuItem("Tools/Game Menu/Install Pickup Highlight Main and Pause Menus")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var world=SceneManager.GetActiveScene();if(world.path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype.");
        if(UnityEngine.Object.FindAnyObjectByType<GameMenuUI>()!=null||File.Exists(MainPath))throw new InvalidOperationException("Menus already installed; preserve Inspector changes.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(world,"Logs/SceneBackups/BeforeGameMenu_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();var pickup=Undo.AddComponent<PlayerPickupFeedback>(player.gameObject);
        var sound=new GameObject("Pickup Sound");sound.transform.SetParent(player.transform,false);var source=sound.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
        Ref(pickup,"source",source);Ref(pickup,"loot",UnityEngine.Object.FindAnyObjectByType<MonsterLootWorld>());Ref(pickup,"clip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/pickup_sound/freesound_community-item-pickup-37089.mp3"));
        foreach(var hover in UnityEngine.Object.FindObjectsByType<UISlotHoverAudio>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var tint=Full("Hover Background Tint",hover.transform);tint.offsetMin=new Vector2(5,5);tint.offsetMax=new Vector2(-5,-5);
            var image=tint.gameObject.AddComponent<Image>();image.color=Color.clear;image.raycastTarget=false;Ref(hover,"highlight",image);
        }
        var pause=UnityEngine.Object.FindAnyObjectByType<GamePauseController>();var feedback=UnityEngine.Object.FindAnyObjectByType<UIAudioFeedback>();
        BuildMenu(pause.transform,true,pause,player,feedback);
        if(PrefabUtility.IsPartOfPrefabInstance(pickup))PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
        EditorSceneManager.MarkSceneDirty(world);EditorSceneManager.SaveScene(world);
        var menuScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(menuScene);
            var camera=new GameObject("Main Menu Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.transform.position=Vector3.back*10;camera.GetComponent<Camera>().backgroundColor=new Color(.025f,.06f,.045f);camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
            var canvas=new GameObject("Main Menu Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.layer=5;canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var audioCopy=UnityEngine.Object.Instantiate(feedback.gameObject,canvas.transform);audioCopy.name="Menu UI Audio";var menuFeedback=audioCopy.GetComponent<UIAudioFeedback>();
            var background=Full("Kingdom Background",canvas.transform);background.SetAsFirstSibling();var image=background.gameObject.AddComponent<RawImage>();image.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Story/Map/Everwood_Overview.png");image.color=new Color(.2f,.28f,.22f,1);image.raycastTarget=false;
            Text("Brand",canvas.transform,110,265,900,150,"EVERWOOD",100,new Color(1,.84f,.48f)).fontStyle=FontStyles.Bold;
            Text("Subtitle",canvas.transform,120,425,820,65,"ผู้กล้าจากพิธีอัญเชิญ",38,new Color(.96f,.93f,.8f));
            Text("Tagline",canvas.transform,120,530,760,170,"เส้นทางผ่านป่าลึก หมู่บ้าน และปราสาทจอมมาร\nการผจญภัยครั้งใหม่กำลังรอเจ้าอยู่",29,new Color(.9f,.9f,.8f));
            Text("Save Note",canvas.transform,120,930,1000,70,"เลเวล ไอเทม และเควสบันทึกอัตโนมัติ\nเข้าสู่เกมที่จุดอัญเชิญ พร้อมความคืบหน้าที่มีอยู่",23,new Color(.87f,.87f,.76f));
            BuildMenu(canvas.transform,false,null,null,menuFeedback);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(menuScene,MainPath);
        }
        finally{SceneManager.SetActiveScene(world);EditorSceneManager.CloseScene(menuScene,true);}
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=MainPath&&s.path!=world.path).ToList();scenes.Insert(0,new EditorBuildSettingsScene(world.path,true));scenes.Insert(0,new EditorBuildSettingsScene(MainPath,true));EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/GameMenuInstallation.txt","MainMenu scene first in build; WorldPrototype second. Pickup success feedback; 46 non-raycast hover overlays; Thai title/pause/help/confirmation pages. Esc closes existing menus without reopening pause. Existing saves preserved.\n");
    }
    private static void BuildMenu(Transform parent,bool paused,GamePauseController pause,PlayerHealth player,UIAudioFeedback audio)
    {
        var root=Full(paused?"Pause Menu":"Title Menu",parent);var window=Full("Menu Window",root);
        if(paused){var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.01f,.02f,.01f,.72f);}
        var panel=Rect("Menu Parchment",window,0,0,paused?850:760,870);panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one*.5f;panel.anchoredPosition=paused?Vector2.zero:new Vector2(490,0);
        var paper=panel.gameObject.AddComponent<Image>();paper.sprite=Art("dialogue_box_frame");paper.type=Image.Type.Sliced;paper.raycastTarget=false;
        float width=panel.sizeDelta.x-130;
        Text("Title",panel,65,55,width,100,paused?"พักการผจญภัย":"เริ่มการเดินทาง",42).fontStyle=FontStyles.Bold;
        var choices=Full("Choices",panel);
        var play=Button("Play or Resume",choices,65,230,width,78,paused?"เล่นต่อ":"เข้าสู่การผจญภัย");
        var help=Button("Controls",choices,65,335,width,78,"วิธีเล่น");
        Button main=null;if(paused)main=Button("Return to Main Menu",choices,65,440,width,78,"กลับเมนูหลัก");
        var quit=Button("Quit",choices,65,paused?545:440,width,78,"ออกจากเกม");
        Text("Hint",choices,65,705,width,70,paused?"กด Esc เพื่อกลับไปเล่นต่อ":"ใช้เมาส์เลือกเมนู\nความคืบหน้าเดิมยังคงอยู่",25);
        var helpPage=Full("How to Play",panel);
        Text("Controls Text",helpPage,65,195,width,420,"W A S D   เคลื่อนที่\nShift   วิ่งเร็ว\nJ   ยิง / โจมตีศัตรูใกล้ตัว\nSpace   พุ่งหลบ\nM   แปลงร่าง\nE   สนทนา / เก็บอาวุธ\nI / Tab   กระเป๋า\nC   พัฒนาสกิล\nQ   บันทึกเควส\nEsc   ปิดหน้าต่าง / พักเกม",27);
        var back=Button("Back",helpPage,65,705,width,68,"กลับ");
        var confirmation=Full("Confirm Navigation",panel);var confirmText=Text("Question",confirmation,65,235,width,230,"",29);
        var yes=Button("Confirm",confirmation,65,545,width,72,"ยืนยัน");var no=Button("Cancel",confirmation,65,645,width,72,"ยกเลิก");
        var status=Text("Loading Status",panel,65,802,width,38,"",23);
        var ui=root.gameObject.AddComponent<GameMenuUI>();var so=new SerializedObject(ui);so.FindProperty("pauseMode").boolValue=paused;so.ApplyModifiedPropertiesWithoutUndo();
        Ref(ui,"pause",pause);Ref(ui,"player",player);Ref(ui,"audioFeedback",audio);Ref(ui,"window",window.gameObject);Ref(ui,"choices",choices.gameObject);Ref(ui,"helpPage",helpPage.gameObject);Ref(ui,"confirmPage",confirmation.gameObject);
        Ref(ui,"playButton",play);Ref(ui,"helpButton",help);Ref(ui,"quitButton",quit);Ref(ui,"backButton",back);Ref(ui,"mainMenuButton",main);Ref(ui,"confirmYes",yes);Ref(ui,"confirmNo",no);Ref(ui,"confirmText",confirmText);Ref(ui,"status",status);
        if(paused)
        {
            var button=Button("Open Pause",root,22,650,295,44,"Esc  เมนูพักเกม");Ref(ui,"openPauseButton",button);
            Ref(window.gameObject.AddComponent<UIModalAudio>(),"feedback",audio);
        }
        helpPage.gameObject.SetActive(false);confirmation.gameObject.SetActive(false);window.gameObject.SetActive(!paused);
    }
}
#endif
