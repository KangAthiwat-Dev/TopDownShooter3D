#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AdventureHudSetup
{
    [MenuItem("Tools/HUD and Audio/Add Gold Level and Rank Above Health")]
    public static void AddProgressionHud()
    {
        RequireScene();var hud=UnityEngine.Object.FindFirstObjectByType<AdventureHudUI>();var player=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        Directory.CreateDirectory("Logs/SceneBackups");
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Logs/SceneBackups/WorldPrototype_BeforeProgressionHud_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true))throw new IOException("Cannot back up the live scene.");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");PrepareSprites("xp_bar");
        BuildProgressionHud(hud.transform,player);RefinePresentation(hud.GetComponentInParent<Canvas>(),hud);
        TrackOverrides(hud.GetComponentInParent<Canvas>().gameObject);EditorUtility.SetDirty(player.GetComponent<PlayerProgressionState>());
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Validate();
    }
    private static void BuildProgressionHud(Transform hud,PlayerMovement player)
    {
        var vitals=hud.Find("Vitals").GetComponent<RectTransform>();vitals.sizeDelta=new Vector2(380,228);
        var heading=vitals.Find("Player Label");if(heading!=null)heading.gameObject.SetActive(false);
        foreach(var groupName in new[]{"Health","Shadow Energy"})
        {
            var group=vitals.Find(groupName);
            foreach(var text in group.GetComponentsInChildren<TMP_Text>(true))text.gameObject.SetActive(false);
            group.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,groupName=="Health"?-79:-158);
        }
        var state=player.GetComponent<PlayerProgressionState>();if(state==null)state=Undo.AddComponent<PlayerProgressionState>(player.gameObject);
        var previous=vitals.Find("Level and Rank");
        if(previous!=null){Wire(previous.GetComponent<PlayerProgressionUI>(),"state",state);return;}
        var root=Rect("Level and Rank",vitals,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(344,76));
        var level=Text("Level",root,new Vector2(14,0),new Vector2(100,23),"LV. "+state.Level,18,Cream);
        var rank=Text("Rank",root,new Vector2(126,1),new Vector2(204,22),state.Rank,15,Gold);rank.alignment=TextAlignmentOptions.Right;
        var frame=Picture("Gold Level Bar",root,new Vector2(0,26),new Vector2(344,50),S("xp_bar"),Color.white);
        Picture("Unfilled Progress",frame.transform,new Vector2(29,12),new Vector2(286,27),null,new Color(.095f,.052f,.02f));
        var fill=Picture("Progress Fill",frame.transform,new Vector2(29,12),new Vector2(286,27),S("xp_bar_fill"),Color.white);
        fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=state.LevelProgress;
        var ui=root.gameObject.AddComponent<PlayerProgressionUI>();var data=new SerializedObject(ui);
        Ref(data,"state",state);Ref(data,"levelText",level);Ref(data,"rankText",rank);Ref(data,"progressFill",fill);data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
