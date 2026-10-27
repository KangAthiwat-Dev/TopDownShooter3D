#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class AdventureHudSetup
{
    private static void BuildVitals(Transform parent,Canvas canvas,PlayerMovement player)
    {
        var oldHealth=canvas.GetComponentInChildren<PlayerHealthUI>(true);var oldEnergy=canvas.GetComponentInChildren<PlayerEnergyUI>(true);
        oldHealth.gameObject.SetActive(false);oldEnergy.gameObject.SetActive(false);
        var root=Rect("Vitals",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-24),new Vector2(380,186));
        Text("Player Label",root,new Vector2(14,0),new Vector2(340,24),"PLAYER",18,Cream).characterSpacing=3;
        void Bar(string name,Vector2 position,Vector2 size,string fillName,bool health)
        {
            var group=Rect(name,root,new Vector2(0,1),new Vector2(0,1),new Vector2(position.x,-position.y),size);
            var inset=new Vector2(size.x*.09f,size.y*.30f);var interior=new Vector2(size.x*.82f,size.y*.40f);
            Picture("Empty",group,inset,interior,null,new Color(.09f,.045f,.035f,.96f));
            var fill=Picture("Fill",group,inset,interior,S(fillName+"_fill"),Color.white);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;
            Picture("Frame",group,Vector2.zero,size,S("health_bar_frame"),Color.white);
            var value=Text("Value",group,new Vector2(22,size.y*.27f),new Vector2(size.x-44,size.y*.45f),"100 / 100",19,Color.white);value.alignment=TextAlignmentOptions.Center;
            Text("Label",group,new Vector2(35,-3),new Vector2(size.x-70,20),health?"HEALTH":"SHADOW ENERGY",12,Cream).characterSpacing=1.5f;
            if(health)
            {
                var data=new SerializedObject(group.gameObject.AddComponent<PlayerHealthUI>());Ref(data,"playerHealth",player.GetComponent<PlayerHealth>());Ref(data,"healthFill",fill);Ref(data,"healthText",value);
                data.FindProperty("healthyColor").colorValue=Color.white;data.FindProperty("warningColor").colorValue=new Color(1f,.78f,.68f);data.FindProperty("dangerColor").colorValue=new Color(.9f,.5f,.5f);data.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var data=new SerializedObject(group.gameObject.AddComponent<PlayerEnergyUI>());Ref(data,"playerEnergy",player.GetComponent<PlayerEnergy>());Ref(data,"energyFill",fill);Ref(data,"energyText",value);
                data.FindProperty("normalColor").colorValue=Color.white;data.FindProperty("lowColor").colorValue=new Color(.6f,.55f,.8f);data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        Bar("Health",new Vector2(0,28),new Vector2(370,76),"health_bar_full",true);
        Bar("Shadow Energy",new Vector2(0,110),new Vector2(320,66),"mana_bar",false);
    }
    private static void BuildHotbar(Transform parent,Canvas canvas,PlayerMovement player)
    {
        var old=canvas.GetComponentInChildren<InventoryHotbarUI>(true);old.gameObject.SetActive(false);
        var root=Rect("Inventory Hotbar",parent,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,24),new Vector2(796,132));
        var hotbar=root.gameObject.AddComponent<InventoryHotbarUI>();var data=new SerializedObject(hotbar);
        Ref(data,"equipment",player.GetComponent<PlayerMeleeEquipment>());Ref(data,"inventory",player.GetComponent<PlayerInventory>());
        Ref(data,"emptySlotSprite",S("inventory_slot_empty"));Ref(data,"selectedSlotSprite",S("inventory_slot_selected"));
        Ref(data,"supplyCountText",Text("Supply Count",root,new Vector2(5,0),new Vector2(566,22),"SUPPLIES   0 / 8",13,Cream));
        Ref(data,"meleeCountText",Text("Melee Count",root,new Vector2(613,0),new Vector2(180,22),"MELEE   0 / 2",13,Gold));
        var array=data.FindProperty("slots");array.arraySize=10;
        for(int i=0;i<10;i++)
        {
            float x=i<8?i*73:610+(i-8)*91;float edge=i<8?68:80;
            var frame=Picture("Slot "+(i+1).ToString("00"),root,new Vector2(x,24),new Vector2(edge,edge),S("inventory_slot_empty"),Color.white);
            frame.raycastTarget=i>=8;
            var icon=Picture("Icon",frame.transform,new Vector2(edge*.25f,edge*.24f),new Vector2(edge*.5f,edge*.5f),null,Color.white);icon.preserveAspect=true;
            var empty=Text("Empty Mark",frame.transform,Vector2.zero,new Vector2(edge,edge),"+",26,Gold);empty.alignment=TextAlignmentOptions.Center;
            var fallback=Text("Melee Glyph",frame.transform,Vector2.zero,new Vector2(edge,edge),"M",27,Ink);fallback.alignment=TextAlignmentOptions.Center;fallback.gameObject.SetActive(false);
            Text("Slot Number",frame.transform,new Vector2(10,8),new Vector2(20,16),(i+1).ToString("00"),10,Cream);
            var title=Text("Item Name",root,new Vector2(x,106),new Vector2(edge,16),i<8?"SUPPLY":"EMPTY",11,Cream);title.alignment=TextAlignmentOptions.Center;
            var state=Text("State",root,new Vector2(x,121),new Vector2(edge,15),"",10,Gold);state.alignment=TextAlignmentOptions.Center;
            var slot=array.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("frame").objectReferenceValue=frame;slot.FindPropertyRelative("icon").objectReferenceValue=icon;
            slot.FindPropertyRelative("emptyMark").objectReferenceValue=empty;slot.FindPropertyRelative("fallbackIcon").objectReferenceValue=fallback.gameObject;
            slot.FindPropertyRelative("itemName").objectReferenceValue=title;slot.FindPropertyRelative("stateLabel").objectReferenceValue=state;
            if(i>=8)
            {
                var button=frame.gameObject.AddComponent<Button>();button.targetGraphic=frame;slot.FindPropertyRelative("button").objectReferenceValue=button;
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.93f,.75f);colors.selectedColor=Color.white;colors.disabledColor=Color.white;button.colors=colors;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void BuildReadouts(Transform parent,SerializedObject config)
    {
        var area=Rect("Area Banner",parent,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-22),new Vector2(350,91));
        Picture("Parchment",area,Vector2.zero,area.sizeDelta,S("dialogue_box_frame"),Color.white,true);
        var areaTitle=Text("Area Name",area,new Vector2(28,21),new Vector2(294,30),"FOREST",22,Ink);areaTitle.alignment=TextAlignmentOptions.Center;areaTitle.fontStyle=FontStyles.Bold;
        var areaKind=Text("Area Kind",area,new Vector2(28,53),new Vector2(294,18),"WILDERNESS",11,Ink);areaKind.alignment=TextAlignmentOptions.Center;areaKind.characterSpacing=2;
        Ref(config,"areaName",areaTitle);Ref(config,"areaKind",areaKind);
        var compassRoot=Rect("Compass",parent,new Vector2(1,1),new Vector2(1,1),new Vector2(-28,-24),new Vector2(148,188));
        var compass=Picture("North Dial",compassRoot,Vector2.zero,new Vector2(148,148),S("compass_minimap_frame"),Color.white).rectTransform;
        // Centre pivot is required for rotating the dial about its own centre.
        compass.pivot=new Vector2(.5f,.5f);compass.anchoredPosition=new Vector2(74,-74);
        var direction=Text("Heading",compassRoot,new Vector2(-10,154),new Vector2(168,24),"N  000 deg",13,Cream);direction.alignment=TextAlignmentOptions.Center;
        Ref(config,"compass",compass);Ref(config,"heading",direction);
        var skills=Rect("Abilities",parent,Vector2.zero,Vector2.zero,new Vector2(28,36),new Vector2(180,122));
        var circle=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        void Ability(string name,float x,string key,string caption,string maskField,string labelField)
        {
            var frame=Picture(name,skills,new Vector2(x,0),new Vector2(76,76),S("cooldown_radial"),Color.white);
            Picture("Centre",frame.transform,new Vector2(15,15),new Vector2(46,46),circle,new Color(.09f,.12f,.16f));
            var mask=Picture("Cooldown",frame.transform,new Vector2(13,13),new Vector2(50,50),circle,new Color(0,0,0,.78f));mask.type=Image.Type.Filled;mask.fillMethod=Image.FillMethod.Radial360;mask.fillOrigin=2;mask.fillClockwise=false;
            var letter=Text("Key",frame.transform,new Vector2(5,17),new Vector2(66,35),key,key=="SPACE"?12:23,Cream);letter.alignment=TextAlignmentOptions.Center;
            var title=Text("Title",skills,new Vector2(x-2,79),new Vector2(80,18),caption,12,Cream);title.alignment=TextAlignmentOptions.Center;
            var status=Text("Status",skills,new Vector2(x-5,99),new Vector2(86,18),"READY",11,Gold);status.alignment=TextAlignmentOptions.Center;
            Ref(config,maskField,mask);Ref(config,labelField,status);
        }
        Ability("Short Dash",0,"SPACE","DASH","dashMask","dashStatus");Ability("Shadow Form",94,"M","SHADOW","shadowMask","shadowStatus");
        var combat=Rect("Combat Readout",parent,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,30),new Vector2(274,112));
        Picture("Weapon Card",combat,Vector2.zero,combat.sizeDelta,S("tooltip_frame"),Color.white,true);
        var weapon=Text("Weapon Name",combat,new Vector2(39,25),new Vector2(204,29),"RIFLE",22,Ink);weapon.fontStyle=FontStyles.Bold;
        var statusLine=Text("Weapon Status",combat,new Vector2(39,58),new Vector2(204,26),"J  ATTACK   /   AUTO AIM",12,Ink);
        Ref(config,"weaponName",weapon);Ref(config,"weaponStatus",statusLine);
    }
    private static void SkinInventory(Canvas canvas)
    {
        var panel=canvas.GetComponentInChildren<InventoryPanelUI>(true);var data=new SerializedObject(panel);
        var window=(GameObject)data.FindProperty("window").objectReferenceValue;window.GetComponent<Image>().color=new Color(.015f,.012f,.009f,.78f);
        var pack=window.transform.Find("Pack");var paper=pack.GetComponent<Image>();paper.sprite=S("dialogue_box_frame");paper.type=Image.Type.Sliced;paper.color=Color.white;
        foreach(var text in pack.GetComponentsInChildren<TMP_Text>(true))text.color=Ink;
        foreach(var image in pack.GetComponentsInChildren<Image>(true))
        {
            if(image.name.Contains("Divider")||image.name=="Top Accent")image.color=new Color(.3f,.17f,.07f,.4f);
            if(image.name=="Detail Card"){image.sprite=S("tooltip_frame");image.type=Image.Type.Sliced;image.color=Color.white;}
            if(image.name=="Detail Icon Frame")image.color=new Color(.31f,.18f,.08f,.15f);
        }
        foreach(var slot in pack.GetComponentsInChildren<InventorySlotUI>(true))
        {
            var s=new SerializedObject(slot);Ref(s,"emptyFrameSprite",S("inventory_slot_empty"));Ref(s,"selectedFrameSprite",S("inventory_slot_selected"));
            var frame=(Image)s.FindProperty("frame").objectReferenceValue;frame.sprite=S("inventory_slot_empty");frame.color=Color.white;frame.type=Image.Type.Sliced;frame.pixelsPerUnitMultiplier=3f;
            var fill=(Image)s.FindProperty("background").objectReferenceValue;fill.color=Color.clear;
            ((Button)s.FindProperty("button").objectReferenceValue).targetGraphic=frame;s.ApplyModifiedPropertiesWithoutUndo();
        }
        var open=(Button)data.FindProperty("openButton").objectReferenceValue;
        Place(open.GetComponent<RectTransform>(),Vector2.zero,Vector2.zero,new Vector2(28,184),new Vector2(194,48));
        open.GetComponent<Image>().sprite=S("tooltip_frame");open.GetComponent<Image>().type=Image.Type.Sliced;open.GetComponent<Image>().color=Color.white;
        var openLabel=open.GetComponentInChildren<TMP_Text>(true);openLabel.text="[I / TAB]  BAG";openLabel.fontSize=16;openLabel.color=Ink;Full(openLabel.rectTransform);
        var close=(Button)data.FindProperty("closeButton").objectReferenceValue;close.GetComponent<Image>().sprite=S("cancel_button");close.GetComponent<Image>().color=Color.white;close.GetComponent<Image>().preserveAspect=true;
        foreach(var label in close.GetComponentsInChildren<TMP_Text>(true))label.gameObject.SetActive(false);
        var select=(Button)data.FindProperty("selectWeaponButton").objectReferenceValue;select.GetComponent<Image>().sprite=S("tooltip_frame");select.GetComponent<Image>().type=Image.Type.Sliced;select.GetComponent<Image>().color=Color.white;
        // Keep the ten original modal views, selection events and inventory ownership intact.
    }
    private static void SkinPromptAndDeath(Canvas canvas)
    {
        var prompt=new SerializedObject(canvas.GetComponent<InteractionPromptUI>());var panel=(GameObject)prompt.FindProperty("promptPanel").objectReferenceValue;
        Place(panel.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,203),new Vector2(450,76));
        var image=panel.GetComponent<Image>();image.sprite=S("tooltip_frame");image.type=Image.Type.Sliced;image.color=Color.white;image.raycastTarget=false;
        var text=(TMP_Text)prompt.FindProperty("promptText").objectReferenceValue;Full(text.rectTransform);text.rectTransform.offsetMin=new Vector2(35,10);text.rectTransform.offsetMax=new Vector2(-35,-10);text.color=Ink;text.fontSize=20;text.alignment=TextAlignmentOptions.Center;
        var gameOver=new SerializedObject(canvas.GetComponent<GameOverUI>());var death=(GameObject)gameOver.FindProperty("gameOverPanel").objectReferenceValue;
        death.GetComponent<Image>().color=new Color(.025f,.014f,.01f,.8f);
        var card=Rect("Fantasy Game Over Card",death.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,280));
        Picture("Parchment",card,Vector2.zero,card.sizeDelta,S("dialogue_box_frame"),Color.white,true);card.SetAsFirstSibling();
        var title=death.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="GameOverText");title.text="YOU HAVE FALLEN";title.fontSize=38;title.color=Ink;
        Place(title.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,45),new Vector2(520,60));title.alignment=TextAlignmentOptions.Center;
        var restart=death.GetComponentInChildren<Button>(true);Place(restart.GetComponent<RectTransform>(),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-48),new Vector2(72,72));
        var buttonImage=restart.GetComponent<Image>();buttonImage.sprite=S("confirm_button");buttonImage.color=Color.white;buttonImage.preserveAspect=true;
        var label=restart.GetComponentInChildren<TMP_Text>(true);label.text="TRY AGAIN";label.fontSize=15;label.color=Ink;
        Place(label.rectTransform,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(0,-4),new Vector2(150,25));
    }
}
#endif
