#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AlertMenuPresentationSetup
{
    private const string Alerts="Assets/_Project/UI/alerts/";
    public const string PreviewPath="Assets/_Project/Prefabs/UI/CharacterMenuPreview.prefab";
    private static TMP_FontAsset font;
    private static UIAudioFeedback feedback;
    private static readonly Color Ink=new Color(.22f,.13f,.065f);
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);}
    private static T Get<T>(UnityEngine.Object owner,string field)where T:UnityEngine.Object=>(T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static void Objects(UnityEngine.Object owner,string field,UnityEngine.Object[] values)
    {var so=new SerializedObject(owner);var p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static Sprite Sprite(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    private static Sprite Frame(string name)=>Sprite(AdventureHudSetup.Folder+"/Sprites/"+name+".png");
    private static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {var r=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;}
    private static RectTransform Full(string name,Transform parent)
    {var r=Rect(name,parent,0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    private static TMP_Text Text(string name,Transform parent,float x,float y,float w,float h,string value,float size,Color? color=null)
    {var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color??Ink;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
    private static Image Image(string name,Transform parent,float x,float y,float w,float h,Sprite sprite)
    {var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;}
    private static Image Paper(string name,Transform parent,float x,float y,float w,float h)
    {var image=Image(name,parent,x,y,w,h,Frame("dialogue_box_frame"));image.preserveAspect=false;image.type=UnityEngine.UI.Image.Type.Sliced;return image;}
    private static Button Button(string name,Transform parent,float x,float y,float w,float h,string text)
    {
        var image=Image(name,parent,x,y,w,h,Frame("tooltip_frame"));image.preserveAspect=false;image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var label=Text("Label",button.transform,12,3,w-24,h-6,text,27);label.alignment=TextAlignmentOptions.Midline;
        var colors=button.colors;colors.highlightedColor=new Color(1,.87f,.6f);colors.pressedColor=new Color(.88f,.72f,.4f);button.colors=colors;
        Ref(button.gameObject.AddComponent<UISlotHoverAudio>(),"feedback",feedback);return button;
    }
    private static void Clear(Transform root){for(int i=root.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);}
    private static void ImportArt()
    {
        foreach(var path in Directory.GetFiles(Alerts,"*.png",SearchOption.AllDirectories))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        var background=(TextureImporter)AssetImporter.GetAtPath("Assets/_Project/UI/Screen/MenuGame.png");background.mipmapEnabled=false;background.wrapMode=TextureWrapMode.Clamp;background.maxTextureSize=2048;background.SaveAndReimport();
    }
    [MenuItem("Tools/Game Presentation/Install Alert Art Menu and Character Creator")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var world=SceneManager.GetActiveScene();if(world.name!=GameMenuUI.WorldScene)throw new Exception("Open WorldPrototype first.");
        if(UnityEngine.Object.FindAnyObjectByType<GameAlertsUI>()!=null)throw new Exception("Presentation already installed; preserve Inspector edits.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(world,"Logs/SceneBackups/BeforeAlertUI_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        File.Copy(GameMenuSetup.MainPath,"Logs/SceneBackups/BeforeMenuArt_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        ImportArt();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);feedback=UnityEngine.Object.FindAnyObjectByType<UIAudioFeedback>();
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
        if(player.GetComponent<PlayerAppearanceProfile>()==null)player.gameObject.AddComponent<PlayerAppearanceProfile>();
        BuildAlerts(player);BuildDeath(player);UpgradePause();BuildPreviewPrefab();
        EditorSceneManager.MarkSceneDirty(world);EditorSceneManager.SaveScene(world);
        var title=EditorSceneManager.OpenScene(GameMenuSetup.MainPath,OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(title);
            feedback=title.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIAudioFeedback>(true)).First();
            var menu=title.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameMenuUI>(true)).Single();
            BuildTitle(menu);EditorSceneManager.MarkSceneDirty(title);EditorSceneManager.SaveScene(title);
        }
        finally{SceneManager.SetActiveScene(world);EditorSceneManager.CloseScene(title,true);}
        AssetDatabase.SaveAssets();Debug.Log("Alert artwork, left title menu, Options placeholder and saved character creator installed.");
    }
    private static void BuildAlerts(PlayerHealth player)
    {
        var root=new GameObject("Game Alert Canvas",typeof(Canvas),typeof(CanvasScaler));root.layer=5;
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=150;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var group=Rect("Animated Alert",root.transform,460,230,1000,410);group.anchorMin=group.anchorMax=new Vector2(.5f,1);group.pivot=new Vector2(.5f,1);group.anchoredPosition=new Vector2(0,-230);
        var canvasGroup=group.gameObject.AddComponent<CanvasGroup>();canvasGroup.alpha=0;canvasGroup.blocksRaycasts=false;
        var emblem=Image("Alert artwork",group,50,0,900,300,null);var detail=Text("Thai alert detail",group,50,305,900,75,"",29,new Color(1,.88f,.6f));detail.alignment=TextAlignmentOptions.Top;
        var shadow=detail.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(2,-2);
        var ui=root.AddComponent<GameAlertsUI>();Ref(ui,"health",player);Ref(ui,"progression",player.GetComponent<PlayerProgressionState>());Ref(ui,"story",player.GetComponent<PlayerStoryProgress>());
        Ref(ui,"hud",UnityEngine.Object.FindAnyObjectByType<AdventureHudUI>());Ref(ui,"pause",UnityEngine.Object.FindAnyObjectByType<GamePauseController>());Ref(ui,"audioFeedback",feedback);Ref(ui,"group",canvasGroup);Ref(ui,"emblem",emblem);Ref(ui,"detail",detail);
        Objects(ui,"victories",UnityEngine.Object.FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None).Where(e=>!e.name.StartsWith("01")).Cast<UnityEngine.Object>().ToArray());
        string[] files={"alert_state/Ornate Fantasy Level Up Emblem.png","alert_state/Quest Completed Fantasy Emblem.png","alert_state/Ornate Fantasy Game Saved Banner.png","alert_state/Enchanted Victory Crest.png","alert_zone/Silverwell Town Fantasy Logo.png","alert_zone/Ornate Oakshade Village Logo.png","alert_zone/Thornveil Forest Fantasy Logo.png","alert_zone/Malachar’s Castle Gothic Logo.png"};
        var so=new SerializedObject(ui);var array=so.FindProperty("art");array.arraySize=files.Length;
        for(int i=0;i<files.Length;i++){var entry=array.GetArrayElementAtIndex(i);entry.FindPropertyRelative("kind").enumValueIndex=i;entry.FindPropertyRelative("sprite").objectReferenceValue=Sprite(Alerts+files[i]);}so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void BuildDeath(PlayerHealth player)
    {
        var ui=UnityEngine.Object.FindAnyObjectByType<GameOverUI>();var old=Get<GameObject>(ui,"gameOverPanel");var parent=old.transform.parent;UnityEngine.Object.DestroyImmediate(old);
        var panel=Full("GameOverPanel",parent);var shade=panel.gameObject.AddComponent<Image>();shade.color=new Color(.025f,.008f,.008f,.88f);panel.gameObject.AddComponent<CanvasGroup>();panel.gameObject.AddComponent<AlertPanelReveal>();
        Image("Defeat artwork",panel,440,90,1040,350,Sprite(Alerts+"alert_state/Molten Gothic Defeat Logo.png"));
        Text("Death title",panel,480,445,960,55,"การผจญภัยยังไม่สิ้นสุด",36,new Color(1,.85f,.65f)).alignment=TextAlignmentOptions.Center;
        Text("Death note",panel,480,505,960,70,"กลับสู่จุดอัญเชิญ พร้อมเลเวล ไอเทม และความคืบหน้าที่มีอยู่",25,new Color(.9f,.84f,.77f)).alignment=TextAlignmentOptions.Center;
        var restart=Button("RestartButton",panel,580,615,760,70,"กลับสู่จุดอัญเชิญ");UnityEditor.Events.UnityEventTools.AddPersistentListener(restart.onClick,ui.RestartGame);
        var main=Button("Return to Main Menu",panel,580,705,760,70,"กลับเมนูหลัก");UnityEditor.Events.UnityEventTools.AddPersistentListener(main.onClick,ui.ReturnToMainMenu);
        var failed=Image("Quest attempt failed",panel,620,795,680,170,Sprite(Alerts+"alert_state/Quest Failed Fantasy Logo.png"));
        Text("Retry quest note",failed.transform,0,155,680,50,"ภารกิจยังคงอยู่ กลับมาท้าทายใหม่ได้",24,new Color(.9f,.78f,.68f)).alignment=TextAlignmentOptions.Center;
        Ref(ui,"gameOverPanel",panel.gameObject);Ref(ui,"questFailedBadge",failed.gameObject);Ref(ui,"audioFeedback",feedback);panel.gameObject.SetActive(false);
    }
    private static GameObject Options(Transform panel,float width,out Button back)
    {
        var page=Full("Options",panel);Text("Options title",page,55,190,width,55,"ตัวเลือกเกม",32);
        Text("Options availability",page,55,280,width,180,"ตัวเลือกเพิ่มเติมจะเปิดให้ใช้งานในภายหลัง",27);
        back=Button("Options Back",page,55,670,width,62,"กลับ");page.gameObject.SetActive(false);return page.gameObject;
    }
    private static void UpgradePause()
    {
        var menu=UnityEngine.Object.FindAnyObjectByType<GameMenuUI>();var choices=Get<GameObject>(menu,"choices");var panel=choices.transform.parent;
        var options=Button("Options",choices.transform,65,279,720,60,"ตัวเลือก");var save=Button("Save Progress",choices.transform,65,427,720,60,"บันทึกเกม");
        string[] fields={"playButton","helpButton","mainMenuButton","quitButton"};float[] ys={205,353,501,575};
        for(int i=0;i<fields.Length;i++){var r=Get<Button>(menu,fields[i]).GetComponent<RectTransform>();r.anchoredPosition=new Vector2(65,-ys[i]);r.sizeDelta=new Vector2(720,60);var label=r.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(688,54);}
        Ref(menu,"optionsButton",options);Ref(menu,"saveButton",save);Ref(menu,"optionsPage",Options(panel,720,out var back));Ref(menu,"optionsBack",back);
        foreach(var button in menu.GetComponentsInChildren<Button>(true))if(button.GetComponent<UISlotHoverAudio>()==null)Ref(button.gameObject.AddComponent<UISlotHoverAudio>(),"feedback",feedback);
    }
    private static void BuildPreviewPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab");
        try
        {
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))if(script!=null&&!(script is PlayerAppearance))UnityEngine.Object.DestroyImmediate(script);
            foreach(var gun in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weapon_Rifle_1").ToArray())if(gun!=null)UnityEngine.Object.DestroyImmediate(gun.gameObject);
            root.name="CharacterMenuPreview";root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(root,PreviewPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Tools/Game Presentation/Resume Prepared Menu Installation")]
    public static void Resume()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(File.ReadAllText(GameMenuSetup.MainPath).Contains("m_Name: Character Creator"))throw new Exception("Presentation already completed; edit its Inspector settings.");
        var world=SceneManager.GetActiveScene();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GuildTradeSetup.FontAsset);
        BuildPreviewPrefab();EditorSceneManager.MarkSceneDirty(world);EditorSceneManager.SaveScene(world);
        var title=EditorSceneManager.OpenScene(GameMenuSetup.MainPath,OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(title);feedback=title.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIAudioFeedback>(true)).First();
            BuildTitle(title.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameMenuUI>(true)).Single());
            EditorSceneManager.MarkSceneDirty(title);EditorSceneManager.SaveScene(title);
        }
        finally{SceneManager.SetActiveScene(world);EditorSceneManager.CloseScene(title,true);}
        AssetDatabase.SaveAssets();Debug.Log("Alert presentation installation completed.");
    }
    private static void BuildTitle(GameMenuUI menu)
    {
        var canvas=menu.GetComponentInParent<Canvas>();var background=canvas.transform.Find("Kingdom Background").GetComponent<RawImage>();
        background.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/UI/Screen/MenuGame.png");background.color=Color.white;
        var fitter=background.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fitter.aspectRatio=16f/9f;
        foreach(string name in new[]{"Brand","Subtitle","Tagline","Save Note"}){var old=canvas.transform.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
        var window=Get<GameObject>(menu,"window");Clear(window.transform);var panel=Paper("Left Main Menu",window.transform,70,130,580,840).transform;
        Text("Title",panel,60,45,460,70,"เมนูหลัก",42).fontStyle=FontStyles.Bold;Text("Invitation",panel,60,125,460,55,"พร้อมเริ่มการผจญภัยหรือยัง?",25);
        var choices=Full("Choices",panel);var play=Button("Play or Resume",choices,55,210,470,66,"เข้าสู่การผจญภัย");
        var customize=Button("Customize Character",choices,55,298,470,66,"ปรับแต่งตัวละคร");var options=Button("Options",choices,55,386,470,66,"ตัวเลือก");
        var help=Button("Controls",choices,55,474,470,66,"วิธีเล่น");var quit=Button("Quit",choices,55,562,470,66,"ออกจากเกม");Text("Hint",choices,60,710,460,60,"ใช้เมาส์เลือกเมนู",24);
        var helpPage=Full("How to Play",panel);Text("Controls Text",helpPage,55,185,470,460,"W A S D   เคลื่อนที่\nShift   วิ่งเร็ว\nJ   ยิง / โจมตีระยะประชิด\nSpace   พุ่งหลบ\nM   แปลงร่าง\nE   สนทนา / เก็บอาวุธ\nI / Tab   กระเป๋า\nC   พัฒนาสกิล\nQ   บันทึกเควส\nEsc   ปิดหน้าต่าง / พักเกม",25);
        var back=Button("Back",helpPage,55,690,470,62,"กลับ");var confirm=Full("Confirm Navigation",panel);var question=Text("Question",confirm,55,240,470,190,"",29);
        var yes=Button("Confirm",confirm,55,565,470,66,"ยืนยัน");var no=Button("Cancel",confirm,55,655,470,66,"ยกเลิก");
        var status=Text("Loading Status",panel,55,785,470,38,"",22);
        Ref(menu,"choices",choices.gameObject);Ref(menu,"helpPage",helpPage.gameObject);Ref(menu,"confirmPage",confirm.gameObject);Ref(menu,"playButton",play);Ref(menu,"helpButton",help);Ref(menu,"quitButton",quit);Ref(menu,"backButton",back);Ref(menu,"confirmYes",yes);Ref(menu,"confirmNo",no);Ref(menu,"confirmText",question);Ref(menu,"status",status);
        Ref(menu,"customizeButton",customize);Ref(menu,"optionsButton",options);Ref(menu,"optionsPage",Options(panel,470,out var optionsBack));Ref(menu,"optionsBack",optionsBack);
        Ref(menu,"characterCreation",BuildCreator(menu));helpPage.gameObject.SetActive(false);confirm.gameObject.SetActive(false);
    }
    private static CharacterCreationUI BuildCreator(GameMenuUI menu)
    {
        var root=Full("Character Creator",menu.transform);var window=Full("Character Creation Window",root);var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.015f,.04f,.032f,.97f);
        Text("Heading",window,155,55,1500,65,"ปรับแต่งผู้กล้า",42,new Color(1,.86f,.54f));Text("Description",window,155,125,1500,45,"เลือกสีผิว สีผม และทรงผมก่อนออกผจญภัย",27,new Color(.9f,.9f,.8f));
        Paper("Portrait Frame",window,165,185,610,750);var portrait=Rect("Character Portrait",window,190,210,560,700).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;
        var left=Button("Rotate Left",window,245,945,180,55,"หมุนซ้าย");var right=Button("Rotate Right",window,455,945,180,55,"หมุนขวา");
        var panel=Paper("Appearance Controls",window,900,195,820,740).transform;Text("Skin heading",panel,50,45,700,50,"สีผิว",31);
        var skin=new[]{new Color(1,.627451f,.478431f),new Color(.97f,.84f,.72f),new Color(.84f,.62f,.44f),new Color(.67f,.44f,.28f),new Color(.43f,.27f,.18f),new Color(.25f,.15f,.11f)};
        var hair=new[]{new Color(.06f,.045f,.035f),new Color(.23f,.1f,.045f),new Color(.46f,.23f,.09f),new Color(.9f,.72f,.33f),new Color(.55f,.13f,.07f),new Color(.48f,.5f,.54f),Color.white,new Color(.16f,.31f,.48f)};
        var skinButtons=Swatches(panel,"Skin",skin,110,80,105);Text("Hair heading",panel,50,245,700,50,"สีผม",31);var hairButtons=Swatches(panel,"Hair",hair,305,64,85);
        Text("Style heading",panel,50,420,700,50,"ทรงผม",31);string[] names={"โกนศีรษะ","ผมสั้น","มัดจุก"};var styles=new Button[3];
        for(int i=0;i<3;i++){styles[i]=Button("Style "+i,panel,50+i*235,480,210,60,names[i]);styles[i].gameObject.AddComponent<Outline>().effectColor=new Color(1,.7f,.1f);styles[i].GetComponent<Outline>().effectDistance=new Vector2(3,-3);}
        var defaults=Button("Default Appearance",panel,50,585,275,60,"คืนค่าเริ่มต้น");var cancel=Button("Cancel Appearance",panel,420,585,300,60,"ยกเลิก");var confirm=Button("Confirm Appearance",panel,50,660,670,62,"บันทึกรูปลักษณ์");
        var ui=root.gameObject.AddComponent<CharacterCreationUI>();Ref(ui,"window",window.gameObject);Ref(ui,"previewPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath));Ref(ui,"portrait",portrait);Ref(ui,"menu",menu);Ref(ui,"confirm",confirm);Ref(ui,"confirmLabel",confirm.GetComponentInChildren<TMP_Text>());Ref(ui,"cancel",cancel);Ref(ui,"defaults",defaults);Ref(ui,"rotateLeft",left);Ref(ui,"rotateRight",right);
        Objects(ui,"skinButtons",skinButtons);Objects(ui,"hairButtons",hairButtons);Objects(ui,"styleButtons",styles);
        var so=new SerializedObject(ui);foreach(var pair in new[]{("skinColors",skin),("hairColors",hair)}){var array=so.FindProperty(pair.Item1);array.arraySize=pair.Item2.Length;for(int i=0;i<pair.Item2.Length;i++)array.GetArrayElementAtIndex(i).colorValue=pair.Item2[i];}so.ApplyModifiedPropertiesWithoutUndo();
        Ref(window.gameObject.AddComponent<UIModalAudio>(),"feedback",feedback);window.gameObject.SetActive(false);return ui;
    }
    private static Button[] Swatches(Transform parent,string name,Color[] colors,float y,float size,float step)
    {
        var buttons=new Button[colors.Length];
        for(int i=0;i<colors.Length;i++)
        {
            var image=Image(name+" "+i,parent,50+i*step,y,size,size,null);image.color=colors[i];image.raycastTarget=true;
            var outline=image.gameObject.AddComponent<Outline>();outline.effectColor=new Color(1,.7f,.1f);outline.effectDistance=new Vector2(3,-3);
            buttons[i]=image.gameObject.AddComponent<Button>();buttons[i].targetGraphic=image;Ref(image.gameObject.AddComponent<UISlotHoverAudio>(),"feedback",feedback);
        }
        return buttons;
    }
}
#endif
