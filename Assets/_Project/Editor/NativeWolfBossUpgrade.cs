#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static partial class StoryCreatureSetup
{

    [MenuItem("Tools/Story Creatures/Replace Prepared Wolf With Native Wolf Boss")]
    public static void UpgradeWolf()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(WolfSource)==null)throw new InvalidOperationException("Import the downloaded Wolf Boss package first.");
        Directory.CreateDirectory(Folder+"/WolfBoss");AssetDatabase.Refresh();ImportRig(WolfSource,"Bip01");
        var wolf=PrefabUtility.LoadPrefabContents(WolfPrefab);
        try
        {
            AddModel(wolf,WolfSource,"WolfBoss",1.5f);
            var agent=wolf.GetComponent<NavMeshAgent>();agent.height=1.6f;agent.radius=.52f;
            var capsule=wolf.GetComponent<CapsuleCollider>();capsule.height=1.35f;capsule.center=new Vector3(0,.675f,.15f);capsule.radius=.5f;
            Number(wolf.GetComponent<EnemyAI>(),"eyeHeight",1.25f);Number(wolf.GetComponent<EnemyAttack>(),"hitDelay",.58f);
            Number(wolf.GetComponent<EnemyDeathController>(),"destroyDelay",AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/WolfBoss/Death.anim").length+.3f);
            wolf.transform.Find("AimPoint").localPosition=new Vector3(0,.9f,.3f);
            PrefabUtility.SaveAsPrefabAsset(wolf,WolfPrefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(wolf);}
        AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/StoryCreatureInstallation.txt","Alpha Wolf now uses native HATOGAME Wolf Boss mesh, texture, idle, walk, run, bite and death. Temporary dragon conversions are retired.\nGiant Spider Lite replaces the old spider with its own rig and animations, old balanced stats, voices and loot preserved.\nElder wizard placeholder remains near the summoning ruins, ready for a later model replacement.\nQuest and opening cinematic progression are separate work.\n");
        CaptureCreatures();Debug.Log("Native Wolf Boss installed; existing population identity and combat stats preserved.");
    }
}
#endif
