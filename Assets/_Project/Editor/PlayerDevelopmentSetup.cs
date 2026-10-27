#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerDevelopmentSetup
{
    private const string Controller="Assets/_Project/Animations/Player/PlayerAnimator.controller";
    private const string Actions="Assets/_Project/Input/PlayerControls.inputactions";
    private static TMP_FontAsset font;
    private static readonly Color Ink=new Color(.22f,.13f,.065f),Gold=new Color(.95f,.76f,.36f);
    private static void Float(Component owner,string field,float value)
    {var s=new SerializedObject(owner);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static Sprite Art(string name)=>AssetDatabase.LoadAllAssetsAtPath(AdventureHudSetup.Folder+"/Sprites/"+name+".png").OfType<Sprite>().Single(s=>s.name==name);

    [MenuItem("Tools/Player Development/Install Upgrades Burst and Grounded Movement")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(UnityEngine.Object.FindAnyObjectByType<PlayerUpgradePanelUI>()!=null)throw new InvalidOperationException("Player development already installed. Use its Inspector settings.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SceneBackups");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforePlayerDevelopment_"+stamp+".unity",true);
        File.Copy(Controller,"Logs/SceneBackups/BeforePlayerDevelopment_"+stamp+".controller");
        var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Actions);
        var menus=input.FindActionMap("Menus",true);
        if(menus.FindAction("Upgrades")==null)
        {var action=menus.AddAction("Upgrades",InputActionType.Button);action.AddBinding("<Keyboard>/c");action.AddBinding("<Gamepad>/start");File.WriteAllText(Actions,input.ToJson());AssetDatabase.ImportAsset(Actions,ImportAssetOptions.ForceUpdate);}
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if(!controller.parameters.Any(p=>p.name=="LocomotionRate"))controller.AddParameter(new AnimatorControllerParameter{name="LocomotionRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
        foreach(var layer in controller.layers)
        {
            foreach(var state in layer.stateMachine.states.Select(s=>s.state).Where(s=>s.name=="Locomotion"))
            {state.speed=1;state.speedParameter="LocomotionRate";state.speedParameterActive=true;EditorUtility.SetDirty(state);}
        }
        foreach(var tree in AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<BlendTree>())
        {
            if(tree.blendType!=BlendTreeType.FreeformDirectional2D&&tree.blendType!=BlendTreeType.SimpleDirectional2D)continue;
            if(tree.blendParameter!="MoveX"||tree.blendParameterY!="MoveZ")continue;
            tree.children=tree.children.Where(c=>c.position.sqrMagnitude>.01f).ToArray();EditorUtility.SetDirty(tree);
        }
        EditorUtility.SetDirty(controller);
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if(player.GetComponent<PlayerUpgradeStats>()==null)Undo.AddComponent<PlayerUpgradeStats>(player.gameObject);
        Float(player,"acceleration",48);Float(player,"deceleration",60);Float(player,"turnSpeed",24);
        var cc=player.GetComponent<CharacterController>();cc.minMoveDistance=0;cc.skinWidth=.03f;cc.stepOffset=.35f;
        var animation=player.GetComponent<PlayerAnimationController>();Float(animation,"dampTime",.055f);Float(animation,"directionDampTime",.055f);Float(animation,"turnDampTime",.055f);
        var attack=player.GetComponent<PlayerAttackController>();Float(attack,"rangedAttackInterval",.2f);Float(attack,"burstRecovery",.8f);
        Float(player.GetComponent<PlayerRangedWeapon>(),"damage",8);
        BuildUI(player);
        foreach(var c in player.GetComponents<Component>())if(c!=null){EditorUtility.SetDirty(c);if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Inspect();Debug.Log("Player development installed: C / gamepad Start, one point per level, 3-round bursts, grounded movement.");
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var t=new GameObject(name,typeof(RectTransform)){layer=5}.GetComponent<RectTransform>();t.SetParent(parent,false);
        t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(position.x,-position.y);t.sizeDelta=size;return t;
    }
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,string value,float fontSize)
    {
        var t=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fontSize;t.color=Ink;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.NoWrap;return t;
    }
    private static Button Button(string name,Transform parent,Vector2 pos,Vector2 size,string label,out TMP_Text text)
    {
        var rt=Rect(name,parent,pos,size);var image=rt.gameObject.AddComponent<Image>();image.sprite=Art("tooltip_frame");image.type=Image.Type.Sliced;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;
        text=Text("Label",rt,new Vector2(8,5),size-new Vector2(16,10),label,18);text.alignment=TextAlignmentOptions.Center;text.fontStyle=FontStyles.Bold;
        var colors=button.colors;colors.highlightedColor=new Color(1,.9f,.64f);colors.disabledColor=new Color(.62f,.57f,.48f,.65f);button.colors=colors;return button;
    }
    private static void BuildUI(PlayerMovement player)
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var pause=UnityEngine.Object.FindAnyObjectByType<GamePauseController>();var canvas=pause.GetComponent<Canvas>();
        var root=Rect("Player Upgrades",canvas.transform,Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        var open=Button("Open Upgrades",root,new Vector2(22,236),new Vector2(328,44),"[C]  UPGRADES",out var openLabel);
        var window=Rect("Upgrade Window",root,Vector2.zero,Vector2.zero);window.anchorMin=Vector2.zero;window.anchorMax=Vector2.one;window.offsetMin=window.offsetMax=Vector2.zero;
        var shade=window.gameObject.AddComponent<Image>();shade.color=new Color(.02f,.015f,.01f,.72f);
        var panel=Rect("Character Development",window,Vector2.zero,new Vector2(1160,790));panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
        var paper=panel.gameObject.AddComponent<Image>();paper.sprite=Art("dialogue_box_frame");paper.type=Image.Type.Sliced;paper.raycastTarget=false;
        Text("Title",panel,new Vector2(52,40),new Vector2(900,48),"CHARACTER DEVELOPMENT",32).fontStyle=FontStyles.Bold;
        var points=Text("Available Points",panel,new Vector2(54,94),new Vector2(940,28),"",20);
        var close=Button("Close Upgrades",panel,new Vector2(1050,40),new Vector2(56,44),"X",out _);
        var buttons=new Button[5];var ranks=new TMP_Text[5];var descriptions=new TMP_Text[5];
        string[] names={"VITALITY","ENDURANCE","TRANSFORMATION","FIREPOWER","FIRE RATE"};
        string[] subtitles={"Health and resistance","Sprint energy","Stay transformed longer","Stronger bullets","Shorter firing intervals"};
        for(int i=0;i<5;i++)
        {
            var row=Rect(names[i],panel,new Vector2(52,145+i*102),new Vector2(1056,92));
            var fill=row.gameObject.AddComponent<Image>();fill.color=new Color(.22f,.12f,.055f,.11f);fill.raycastTarget=false;
            Text("Name",row,new Vector2(16,11),new Vector2(280,28),names[i],21).fontStyle=FontStyles.Bold;
            Text("Purpose",row,new Vector2(16,45),new Vector2(280,25),subtitles[i],16);
            ranks[i]=Text("Rank",row,new Vector2(306,10),new Vector2(150,28),"0 / 20",20);
            descriptions[i]=Text("Next Upgrade",row,new Vector2(306,46),new Vector2(558,28),"",16);
            buttons[i]=Button("Spend Point",row,new Vector2(872,20),new Vector2(164,50),"+ UPGRADE",out _);
        }
        var summary=Text("Current Stats",panel,new Vector2(54,674),new Vector2(1052,30),"",18);summary.fontStyle=FontStyles.Bold;
        Text("Help",panel,new Vector2(54,719),new Vector2(1052,28),"1 POINT PER LEVEL     /     C OR ESC TO CLOSE     /     GAME PAUSED",16);
        var ui=root.gameObject.AddComponent<PlayerUpgradePanelUI>();
        Ref(ui,"progression",player.GetComponent<PlayerProgressionState>());Ref(ui,"stats",player.GetComponent<PlayerUpgradeStats>());Ref(ui,"health",player.GetComponent<PlayerHealth>());
        Ref(ui,"input",player.GetComponent<PlayerInput>());Ref(ui,"pause",pause);Ref(ui,"inventory",UnityEngine.Object.FindAnyObjectByType<InventoryPanelUI>());
        Ref(ui,"window",window.gameObject);Ref(ui,"openButton",open);Ref(ui,"closeButton",close);Ref(ui,"openLabel",openLabel);Ref(ui,"pointsLabel",points);Ref(ui,"summary",summary);
        var settings=new SerializedObject(ui);
        void Array(string name,UnityEngine.Object[] objects){var a=settings.FindProperty(name);a.arraySize=objects.Length;for(int i=0;i<objects.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];}
        Array("upgradeButtons",buttons);Array("rankLabels",ranks);Array("previews",descriptions);settings.ApplyModifiedPropertiesWithoutUndo();window.gameObject.SetActive(false);
        Selection.activeGameObject=root.gameObject;
    }

    [MenuItem("Tools/Player Development/Inspect Player and Locomotion")]
    public static void Inspect()
    {
        var p=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var a=p.GetComponentInChildren<Animator>();
        var report=new StringBuilder("Player development audit "+DateTime.Now.ToString("s")+"\n");
        report.AppendLine("Player="+p.transform.position+" controller="+p.GetComponent<CharacterController>().center+" avatar human="+a.isHuman+" rootMotion="+a.applyRootMotion);
        report.AppendLine("Run="+p.RunSpeed+" Sprint="+p.SprintSpeed+" Damage="+p.GetComponent<PlayerRangedWeapon>().BaseDamage);
        foreach(string path in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/_Project/Animations/Player/Locomotion"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null)continue;var s=AnimationUtility.GetAnimationClipSettings(clip);
            report.AppendLine(clip.name+" avgSpeed="+clip.averageSpeed+" loop="+s.loopTime+" rootY="+s.loopBlendPositionY+" keepY="+s.keepOriginalPositionY);
        }
        File.WriteAllText("Logs/PlayerDevelopmentAudit.txt",report.ToString());Debug.Log(report.ToString());
    }
}
#endif
