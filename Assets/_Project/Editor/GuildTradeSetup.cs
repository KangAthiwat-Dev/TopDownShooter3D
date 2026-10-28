#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class GuildTradeSetup
{
    public const string FontFolder="Assets/_Project/UI/Fonts/NotoSansThaiLooped";
    public const string FontAsset=FontFolder+"/NotoSansThaiLooped SDF.asset";
    private static TMP_FontAsset font;
    private static readonly Color Ink=new Color(.22f,.13f,.065f);
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().Single(s=>s.name==name);
    private static TMP_FontAsset BuildFont()
    {
        var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);if(existing!=null)return existing;
        var source=AssetDatabase.LoadAssetAtPath<Font>(FontFolder+"/NotoSansThaiLooped-Regular.ttf");
        if(source==null)throw new InvalidOperationException("Import the bundled open-source Thai font first.");
        var thai=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        thai.name="NotoSansThaiLooped SDF";
        var latin=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        thai.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{latin};
        string characters=string.Concat(Enumerable.Range(32,95).Concat(Enumerable.Range(0x0E01,0x3A)).Concat(Enumerable.Range(0x0E3F,0x1D)).Select(c=>(char)c))+"×—";
        thai.TryAddCharacters(characters,out string missing,true);
        foreach(char c in GuildTradeUI.Greeting+GuildTradeUI.SellChoice+GuildTradeUI.LeaveChoice)
            if(!thai.HasCharacter(c,true))throw new InvalidOperationException("Thai glyph unavailable: "+c);
        AssetDatabase.CreateAsset(thai,FontAsset);
        AssetDatabase.AddObjectToAsset(thai.material,thai);
        foreach(var atlas in thai.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,thai);
        EditorUtility.SetDirty(thai);return thai;
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var rt=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(pos.x,-pos.y);rt.sizeDelta=size;return rt;
    }
    private static void Full(RectTransform t){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
    private static Image Picture(string name,Transform parent,Vector2 pos,Vector2 size,Sprite sprite)
    {
        var image=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
        if(sprite!=null&&sprite.border.sqrMagnitude>0)image.type=Image.Type.Sliced;return image;
    }
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,string value,float fontSize)
    {
        var t=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fontSize;t.color=Ink;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.MidlineLeft;return t;
    }
    private static Button Button(string name,Transform parent,Vector2 pos,Vector2 size,string value,out TMP_Text label)
    {
        var frame=Picture(name,parent,pos,size,Art("tooltip_frame"));frame.raycastTarget=true;var button=frame.gameObject.AddComponent<Button>();button.targetGraphic=frame;
        label=Text("Label",frame.transform,new Vector2(14,8),size-new Vector2(28,16),value,25);label.alignment=TextAlignmentOptions.Midline;
        var colors=button.colors;colors.highlightedColor=new Color(1,.87f,.5f);colors.pressedColor=new Color(.87f,.7f,.4f);colors.disabledColor=new Color(.6f,.56f,.48f,.7f);button.colors=colors;
        return button;
    }

    [MenuItem("Tools/NPC/Guild Trade/Install Dialogue Selling and Gold")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(UnityEngine.Object.FindAnyObjectByType<GuildTradeUI>()!=null)throw new InvalidOperationException("Guild selling already installed. Preserve Inspector adjustments.");
        AssetDatabase.Refresh();font=BuildFont();
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");
        EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeGuildTrade_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var guild=UnityEngine.Object.FindObjectsByType<CityServiceNpc>(FindObjectsSortMode.None).Single(n=>n.Role==CityServiceRole.GuildBuyer);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
        var wallet=player.GetComponent<PlayerGoldWallet>();if(wallet==null)wallet=Undo.AddComponent<PlayerGoldWallet>(player.gameObject);
        string[] ids={"monster_bone","monster_fang","monster_claw","beast_hide","raw_meat","demon_horn","demon_wing","dragon_scale"};
        int[] prices={4,6,8,12,5,15,22,40};
        var report=new StringBuilder("Guild selling installation "+DateTime.Now.ToString("s")+"\n");
        for(int i=0;i<ids.Length;i++)
        {
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(MonsterLootSetup.Folder+"/Items/"+ids[i]+".asset");
            var s=new SerializedObject(item);s.FindProperty("sellPrice").intValue=prices[i];s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(item);
            report.AppendLine(item.DisplayName+" = "+prices[i]+" Gold each");
        }
        var pause=UnityEngine.Object.FindAnyObjectByType<GamePauseController>();var canvas=pause.GetComponent<Canvas>();
        var root=Rect("Guild Trade",canvas.transform,Vector2.zero,Vector2.zero);Full(root);
        var window=Rect("Guild Window",root,Vector2.zero,Vector2.zero);Full(window);var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.02f,.015f,.01f,.72f);
        var panel=Rect("Guild Ledger",window,Vector2.zero,new Vector2(1280,850));panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
        var paper=panel.gameObject.AddComponent<Image>();paper.sprite=Art("dialogue_box_frame");paper.type=Image.Type.Sliced;paper.raycastTarget=false;
        Text("Title",panel,new Vector2(58,45),new Vector2(650,58),"กิลด์นักผจญภัย",34).fontStyle=FontStyles.Bold;
        var gold=Text("Your Gold",panel,new Vector2(795,58),new Vector2(340,40),"0 Gold",28);gold.alignment=TextAlignmentOptions.Right;
        var close=Button("Close Conversation",panel,new Vector2(1160,46),new Vector2(62,54),"X",out _);
        var dialogue=Rect("Dialogue",panel,Vector2.zero,new Vector2(1280,850));
        Text("Speaker",dialogue,new Vector2(80,165),new Vector2(1100,42),"เจ้าหน้าที่รับซื้อวัตถุดิบ",25);
        Text("Greeting",dialogue,new Vector2(80,235),new Vector2(1100,114),GuildTradeUI.Greeting,34);
        var sellChoice=Button("Sell Materials Choice",dialogue,new Vector2(80,435),new Vector2(1120,82),GuildTradeUI.SellChoice,out _);
        var leaveChoice=Button("Leave Choice",dialogue,new Vector2(80,545),new Vector2(1120,82),GuildTradeUI.LeaveChoice,out _);
        Text("Dialogue Hint",dialogue,new Vector2(82,717),new Vector2(1100,40),"ใช้เมาส์เลือกคำตอบ  ·  Esc เพื่อออกจากบทสนทนา",24);
        var sale=Rect("Selling",panel,Vector2.zero,new Vector2(1280,850));
        Text("Inventory Title",sale,new Vector2(60,123),new Vector2(720,42),"วัตถุดิบจากกระเป๋าของเจ้า",26);
        var rows=new GuildTradeUI.Row[8];
        for(int i=0;i<8;i++)
        {
            var frame=Picture("Supply "+i,sale,new Vector2(58+(i%2)*376,175+(i/2)*126),new Vector2(360,116),Art("tooltip_frame"));frame.raycastTarget=true;
            var button=frame.gameObject.AddComponent<Button>();button.targetGraphic=frame;
            var icon=Picture("Item Icon",frame.transform,new Vector2(14,22),new Vector2(70,70),null);icon.preserveAspect=true;
            var name=Text("Item Name",frame.transform,new Vector2(96,13),new Vector2(250,38),"ช่องว่าง",23);
            var quantity=Text("Amount",frame.transform,new Vector2(15,76),new Vector2(74,28),"",20);quantity.alignment=TextAlignmentOptions.Center;
            var price=Text("Unit Price",frame.transform,new Vector2(96,60),new Vector2(250,44),"",21);
            rows[i]=new GuildTradeUI.Row{button=button,frame=frame,icon=icon,name=name,quantity=quantity,price=price};
        }
        var card=Picture("Selected Material",sale,new Vector2(826,175),new Vector2(394,494),Art("tooltip_frame"));
        var selectedName=Text("Selected Name",card.transform,new Vector2(22,15),new Vector2(350,44),"",26);selectedName.alignment=TextAlignmentOptions.Center;
        var selectedIcon=Picture("Selected Icon",card.transform,new Vector2(137,68),new Vector2(120,120),null);selectedIcon.preserveAspect=true;
        var less=Button("Less",card.transform,new Vector2(22,205),new Vector2(66,52),"−",out _);
        var more=Button("More",card.transform,new Vector2(306,205),new Vector2(66,52),"+",out _);
        var amount=Text("Selected Amount",card.transform,new Vector2(97,211),new Vector2(199,40),"—",28);amount.alignment=TextAlignmentOptions.Center;
        var all=Button("Whole Stack",card.transform,new Vector2(22,269),new Vector2(350,54),"เลือกทั้งกอง",out _);
        var total=Text("Price Preview",card.transform,new Vector2(22,335),new Vector2(350,74),"",24);
        var sell=Button("Sell Selected",card.transform,new Vector2(18,422),new Vector2(358,58),"ขายไอเทม",out var sellLabel);sellLabel.fontSize=23;
        var status=Text("Sale Status",sale,new Vector2(60,688),new Vector2(1160,40),"",24);
        var back=Button("Back to Dialogue",sale,new Vector2(58,743),new Vector2(335,54),"กลับไปสนทนา",out _);
        Text("Sale Help",sale,new Vector2(426,749),new Vector2(792,44),"เลือกไอเทม → จำนวน → ขาย  ·  Esc เพื่อออก",23);
        var ui=root.gameObject.AddComponent<GuildTradeUI>();
        Ref(ui,"inventory",player);Ref(ui,"wallet",wallet);Ref(ui,"health",player.GetComponent<PlayerHealth>());Ref(ui,"input",player.GetComponent<PlayerInput>());Ref(ui,"pause",pause);
        Ref(ui,"window",window.gameObject);Ref(ui,"dialoguePage",dialogue.gameObject);Ref(ui,"salePage",sale.gameObject);
        Ref(ui,"sellChoice",sellChoice);Ref(ui,"leaveChoice",leaveChoice);Ref(ui,"closeButton",close);Ref(ui,"backButton",back);Ref(ui,"sellButton",sell);
        Ref(ui,"lessButton",less);Ref(ui,"moreButton",more);Ref(ui,"stackButton",all);Ref(ui,"goldLabel",gold);Ref(ui,"selectedName",selectedName);Ref(ui,"quantityLabel",amount);
        Ref(ui,"totalLabel",total);Ref(ui,"statusLabel",status);Ref(ui,"sellLabel",sellLabel);Ref(ui,"selectedIcon",selectedIcon);
        var data=new SerializedObject(ui);var list=data.FindProperty("rows");list.arraySize=rows.Length;
        for(int i=0;i<rows.Length;i++)
        {
            var r=list.GetArrayElementAtIndex(i);r.FindPropertyRelative("button").objectReferenceValue=rows[i].button;r.FindPropertyRelative("icon").objectReferenceValue=rows[i].icon;
            r.FindPropertyRelative("frame").objectReferenceValue=rows[i].frame;r.FindPropertyRelative("name").objectReferenceValue=rows[i].name;r.FindPropertyRelative("quantity").objectReferenceValue=rows[i].quantity;r.FindPropertyRelative("price").objectReferenceValue=rows[i].price;
        }
        data.ApplyModifiedPropertiesWithoutUndo();window.gameObject.SetActive(false);
        var interaction=Undo.AddComponent<GuildBuyerInteractable>(guild.gameObject);Ref(interaction,"contact",guild);Ref(interaction,"tradeUI",ui);
        var trigger=new GameObject("Guild Interaction Trigger");trigger.transform.SetParent(guild.transform,false);trigger.layer=8;
        var capsule=trigger.AddComponent<CapsuleCollider>();capsule.isTrigger=true;capsule.height=1.8f;capsule.radius=.4f;capsule.center=Vector3.up*.9f;
        var config=new SerializedObject(interaction);config.FindProperty("interactionPrompt").stringValue="คุยกับเจ้าหน้าที่กิลด์";config.ApplyModifiedPropertiesWithoutUndo();
        var prompt=UnityEngine.Object.FindAnyObjectByType<InteractionPromptUI>();var promptData=new SerializedObject(prompt);
        var promptText=(TMP_Text)promptData.FindProperty("promptText").objectReferenceValue;promptText.font=font;promptText.fontSize=26;EditorUtility.SetDirty(promptText);
        var hud=Rect("Gold Wallet",canvas.transform,new Vector2(22,288),new Vector2(328,44));
        var hudFrame=hud.gameObject.AddComponent<Image>();hudFrame.sprite=Art("tooltip_frame");hudFrame.type=Image.Type.Sliced;hudFrame.raycastTarget=false;
        var hudText=Text("Gold Amount",hud,new Vector2(14,7),new Vector2(300,32),"0 Gold",24);hudText.alignment=TextAlignmentOptions.Center;
        var goldUI=hud.gameObject.AddComponent<PlayerGoldUI>();Ref(goldUI,"wallet",wallet);Ref(goldUI,"amount",hudText);
        // Keep all modal surfaces above ambient readouts.
        root.SetAsLastSibling();
        foreach(var owner in new UnityEngine.Object[]{wallet,interaction,promptText,ui})
        {EditorUtility.SetDirty(owner);if(PrefabUtility.IsPartOfPrefabInstance(owner))PrefabUtility.RecordPrefabInstancePropertyModifications(owner);}
        report.AppendLine("Guild buyer="+guild.name+" building="+guild.Building.name+" triggerLayer="+trigger.layer);
        report.AppendLine("Greeting="+GuildTradeUI.Greeting);report.AppendLine("Currency saved as TopDownShooter3D.Gold.v1; future purchases call PlayerGoldWallet.TrySpend.");
        RefineLayout();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/GuildTradeInstallation.txt",report.ToString());Debug.Log(report.ToString());Selection.activeGameObject=guild.gameObject;
    }

    [MenuItem("Tools/NPC/Guild Trade/Refine Thai Text and Layout")]
    public static void RefineLayout()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        var ui=UnityEngine.Object.FindAnyObjectByType<GuildTradeUI>();
        foreach(var t in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            t.overflowMode=TextOverflowModes.Overflow;t.textWrappingMode=TextWrappingModes.NoWrap;
            bool center=t.name=="Label"||t.name=="Selected Name"||t.name=="Selected Amount"||t.name=="Amount";
            t.alignment=center?TextAlignmentOptions.Midline:TextAlignmentOptions.MidlineLeft;
            if(t.name=="Your Gold")t.alignment=TextAlignmentOptions.MidlineRight;
            if(t.name=="Price Preview")
            {
                t.rectTransform.sizeDelta=new Vector2(350,90);t.rectTransform.anchoredPosition=new Vector2(22,-325);t.fontSize=23;
            }
            if(t.name=="Selected Name")t.fontSize=27;
            if(t.name=="Selected Name")t.rectTransform.anchoredPosition=new Vector2(22,-30);
            if(t.name=="Item Name")t.rectTransform.anchoredPosition=new Vector2(96,-26);
            if(t.name=="Unit Price")
            {t.rectTransform.anchoredPosition=new Vector2(96,-65);t.rectTransform.sizeDelta=new Vector2(178,32);t.fontSize=20;}
            if(t.name=="Amount")
            {t.rectTransform.anchoredPosition=new Vector2(276,-65);t.rectTransform.sizeDelta=new Vector2(68,32);t.fontSize=20;}
            EditorUtility.SetDirty(t);
        }
        var guildWindow=ui.transform.Find("Guild Window/Guild Ledger");
        foreach(string path in new[]{"Selling/Selected Material","Selling/Supply 0","Selling/Supply 1","Selling/Supply 2","Selling/Supply 3","Selling/Supply 4","Selling/Supply 5","Selling/Supply 6","Selling/Supply 7"})
        {var image=guildWindow.Find(path).GetComponent<Image>();image.sprite=Art("dialogue_box_frame");image.type=Image.Type.Sliced;EditorUtility.SetDirty(image);}
        foreach(var t in ui.GetComponentsInChildren<Button>(true).Where(b=>b.name=="Less"||b.name=="More"||b.name=="Close Conversation"))
        {t.GetComponent<Image>().sprite=Art("inventory_slot_empty");var label=t.GetComponentInChildren<TMP_Text>();label.color=new Color(1,.91f,.65f);EditorUtility.SetDirty(label);EditorUtility.SetDirty(t.GetComponent<Image>());}
        var gold=UnityEngine.Object.FindAnyObjectByType<PlayerGoldUI>().GetComponentInChildren<TMP_Text>();
        gold.overflowMode=TextOverflowModes.Overflow;gold.alignment=TextAlignmentOptions.Midline;EditorUtility.SetDirty(gold);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
    }
}
#endif
