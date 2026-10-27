#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AdventureHudSetup
{
    [MenuItem("Tools/HUD and Audio/Refine HUD Readability and Panel Insets")]
    public static void Refine()
    {
        RequireScene();var hud=UnityEngine.Object.FindFirstObjectByType<AdventureHudUI>();var canvas=hud.GetComponentInParent<Canvas>();
        RefinePresentation(canvas,hud);TrackOverrides(canvas.gameObject);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();
    }
    private static void RefinePresentation(Canvas canvas,AdventureHudUI hud)
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        string path=Folder+"/HudText_Outline.mat";var outline=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(outline==null)
        {
            outline=new Material(font.material){name="HudText_Outline"};outline.SetFloat("_OutlineWidth",.15f);outline.SetColor("_OutlineColor",new Color(.055f,.025f,.012f));
            outline.EnableKeyword("OUTLINE_ON");AssetDatabase.CreateAsset(outline,path);
        }
        foreach(var text in hud.GetComponentsInChildren<TMP_Text>(true))
            if(text.color.r>.6f){text.fontSharedMaterial=outline;text.UpdateMeshPadding();}
        var bar=new SerializedObject(hud.GetComponentInChildren<InventoryHotbarUI>(true));var barSlots=bar.FindProperty("slots");
        for(int i=0;i<barSlots.arraySize;i++)
        {
            var slot=barSlots.GetArrayElementAtIndex(i);
            var icon=(Image)slot.FindPropertyRelative("icon").objectReferenceValue;
            if(icon!=null&&icon.sprite==null)icon.gameObject.SetActive(false);
            var button=(Button)slot.FindPropertyRelative("button").objectReferenceValue;
            if(button!=null){var colors=button.colors;colors.disabledColor=Color.white;button.colors=colors;}
        }
        var fields=new SerializedObject(hud);
        var areaTitle=(TMP_Text)fields.FindProperty("areaName").objectReferenceValue;areaTitle.rectTransform.anchoredPosition=new Vector2(28,-26);
        areaTitle.transform.parent.Find("Parchment").GetComponent<Image>().pixelsPerUnitMultiplier=2f;
        var areaKind=(TMP_Text)fields.FindProperty("areaKind").objectReferenceValue;areaKind.rectTransform.anchoredPosition=new Vector2(28,-53);
        var weapon=(TMP_Text)fields.FindProperty("weaponName").objectReferenceValue;weapon.rectTransform.anchoredPosition=new Vector2(52,-25);weapon.rectTransform.sizeDelta=new Vector2(190,29);
        var weaponState=(TMP_Text)fields.FindProperty("weaponStatus").objectReferenceValue;weaponState.rectTransform.anchoredPosition=new Vector2(52,-58);weaponState.rectTransform.sizeDelta=new Vector2(190,26);
        weapon.transform.parent.Find("Weapon Card").GetComponent<Image>().pixelsPerUnitMultiplier=2f;
        var inventory=canvas.GetComponentInChildren<InventoryPanelUI>(true);var config=new SerializedObject(inventory);
        var window=(GameObject)config.FindProperty("window").objectReferenceValue;var pack=window.transform.Find("Pack");
        void At(string name,float x,float y,float width,float height)
        {var child=pack.Find(name);if(child!=null)Place(child.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(width,height));}
        At("Eyebrow",66,34,650,18);pack.Find("Eyebrow").GetComponent<TMP_Text>().text="TRAVELER'S PACK";
        At("Title",66,58,650,43);At("Capacity",66,106,1000,24);At("Divider",66,140,1108,1);
        At("Supply Heading",66,152,500,24);At("Detail Heading",800,152,370,24);At("Melee Heading",66,509,704,26);
        At("Footer Divider",66,692,1108,1);At("Footer Controls",66,710,800,26);At("Pause Hint",968,710,206,26);
        var slots=config.FindProperty("slots");
        for(int i=0;i<slots.arraySize;i++)
        {
            var slot=(InventorySlotUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
            Place(slot.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),i<8?new Vector2(66+(i%4)*176,-188-(i/4)*160):new Vector2(66+(i-8)*352,-548),i<8?new Vector2(164,144):new Vector2(344,120));
            if(i<8)
            {
                var data=new SerializedObject(slot);
                var name=(TMP_Text)data.FindProperty("itemName").objectReferenceValue;name.fontSize=16;name.rectTransform.anchoredPosition=new Vector2(14,-94);name.rectTransform.sizeDelta=new Vector2(136,23);
                var status=(TMP_Text)data.FindProperty("status").objectReferenceValue;status.rectTransform.anchoredPosition=new Vector2(14,-117);status.rectTransform.sizeDelta=new Vector2(136,17);
            }
        }
        var card=pack.Find("Detail Card");Place(card.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(800,-188),new Vector2(380,480));
        void Detail(string name,float x,float y,float width,float height)
        {var child=card.Find(name);if(child!=null)Place(child.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(width,height));}
        Detail("Detail Icon Frame",62,28,70,70);Detail("Detail Icon",70,36,54,54);Detail("Detail Glyph",62,28,70,70);
        Detail("Item Type",150,28,202,20);Detail("Item Name",150,55,202,70);Detail("Description",62,135,276,158);Detail("Item Stats",62,312,276,104);Detail("Select Weapon",62,428,276,36);
        var itemName=card.Find("Item Name").GetComponent<TMP_Text>();itemName.fontSizeMax=24;itemName.fontSizeMin=18;
        card.Find("Item Type").GetComponent<TMP_Text>().fontSize=12;card.Find("Description").GetComponent<TMP_Text>().fontSize=19;card.Find("Item Stats").GetComponent<TMP_Text>().fontSize=16;
        var open=(Button)config.FindProperty("openButton").objectReferenceValue;open.GetComponentInChildren<TMP_Text>(true).fontSize=14;open.GetComponent<Image>().pixelsPerUnitMultiplier=3f;
        ((Button)config.FindProperty("selectWeaponButton").objectReferenceValue).GetComponent<Image>().pixelsPerUnitMultiplier=4f;
        var prompt=new SerializedObject(canvas.GetComponent<InteractionPromptUI>());
        ((GameObject)prompt.FindProperty("promptPanel").objectReferenceValue).GetComponent<Image>().pixelsPerUnitMultiplier=2f;
    }
}
#endif
