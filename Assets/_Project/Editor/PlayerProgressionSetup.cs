#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerProgressionSetup
{
    private const string EnemyFolder="Assets/_Project/Prefabs/Enemies/";
    public static readonly Dictionary<string,int> Rewards=new Dictionary<string,int>
    {
        {"EnemyBase",35},{"EnemyDummy",0},{"Enemy_Spider",25},{"Enemy_Imp_Green",40},
        {"Enemy_Imp_Blue",60},{"Enemy_Imp_Red",90},{"Enemy_Bear",140},{"Enemy_Puglin",160},
        {"DragonNightmare_Guardian",1800},{"DemonLord_Boss",2500},{"KingOfDemon_Boss",5000}
    };
    [MenuItem("Tools/Progression/Install Monster EXP and Level Ranks")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Project/Scenes/WorldPrototype.unity")throw new InvalidOperationException("Open WorldPrototype.");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/WorldPrototype_BeforeMonsterEXP_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        foreach(var pair in Rewards)
        {
            string path=EnemyFolder+pair.Key+".prefab";var contents=PrefabUtility.LoadPrefabContents(path);
            try{var data=new SerializedObject(contents.GetComponent<EnemyHealth>());data.FindProperty("experienceReward").intValue=pair.Value;data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(contents,path);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        foreach(var health in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnemyHealth>(true)))
        {
            string asset=Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(health.gameObject));
            int reward=health.GetComponent<KingOfDemonAI>()!=null?5000:health.name=="DemonLord"?2500:health.name=="DragonNightmare"?1800:Rewards.TryGetValue(asset??"",out int value)?value:health.ExperienceReward;
            var data=new SerializedObject(health);data.FindProperty("experienceReward").intValue=reward;data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(health);
        }
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();var state=player.GetComponent<PlayerProgressionState>();
        if(state==null)state=Undo.AddComponent<PlayerProgressionState>(player.gameObject);
        var hud=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionUI>();var ui=new SerializedObject(hud);ui.FindProperty("state").objectReferenceValue=state;ui.ApplyModifiedPropertiesWithoutUndo();
        ((TMP_Text)ui.FindProperty("levelText").objectReferenceValue).text="LV. "+state.Level;
        ((TMP_Text)ui.FindProperty("rankText").objectReferenceValue).text=state.Rank;
        ((Image)ui.FindProperty("progressFill").objectReferenceValue).fillAmount=state.LevelProgress;
        foreach(var component in hud.GetComponentsInChildren<Component>(true))
        {if(component==null)continue;EditorUtility.SetDirty(component);if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
        EditorUtility.SetDirty(state);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Validate();
    }
    [MenuItem("Tools/Progression/Validate Monster EXP Rewards")]
    public static void Validate()
    {
        var report=new StringBuilder("Monster EXP reward validation\n");bool passed=true;
        foreach(var pair in Rewards)
        {
            var health=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder+pair.Key+".prefab").GetComponent<EnemyHealth>();bool valid=health.ExperienceReward==pair.Value;
            passed&=valid;report.AppendLine((valid?"PASS ":"FAIL ")+pair.Key+" EXP="+health.ExperienceReward);
        }
        foreach(var health in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))report.AppendLine("SCENE "+health.name+" EXP="+health.ExperienceReward);
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/PlayerProgressionSetup.txt",report.ToString());
        if(!passed)throw new InvalidOperationException("Monster reward validation failed.");
    }
}
#endif
