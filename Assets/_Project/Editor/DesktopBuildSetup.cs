#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Builds the shipping scenes, resuming safely after platform import/domain reload.</summary>
[InitializeOnLoad]
public static class DesktopBuildSetup
{
    public const string ProductName="EverwoodTheSummonedHero";
    private const string Pending="DesktopBuild.Pending", Index="DesktopBuild.Index", Targets="DesktopBuild.Targets";
    private const string ReportFolder="Logs/BuildPreparation";
    private static bool running;

    static DesktopBuildSetup()
    {
        if(SessionState.GetBool(Pending,false))EditorApplication.update+=ContinueBuild;
    }

    [MenuItem("Tools/Build Preparation/Build Windows and macOS")]
    public static void BuildBoth()=>Begin(new[]{BuildTarget.StandaloneOSX,BuildTarget.StandaloneWindows64});
    [MenuItem("Tools/Build Preparation/Build Windows")]
    public static void BuildWindows()=>Begin(new[]{BuildTarget.StandaloneWindows64});
    [MenuItem("Tools/Build Preparation/Build macOS")]
    public static void BuildMac()=>Begin(new[]{BuildTarget.StandaloneOSX});

    [MenuItem("Tools/Build Preparation/Prepare Boss Navigation Startup")]
    public static void PrepareBossNavigation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)return;
        var scene=SceneManager.GetActiveScene();
        if(scene.name!=GameMenuUI.WorldScene)throw new InvalidOperationException("Open WorldPrototype first.");
        Directory.CreateDirectory(ReportFolder);
        EditorSceneManager.SaveScene(scene,ReportFolder+"/BeforeBossNavigationStartup.unity",true);
        int changed=0;
        foreach(var encounter in UnityEngine.Object.FindObjectsByType<DemonArenaEncounter>())
        {
            var boss=new SerializedObject(encounter).FindProperty("boss").objectReferenceValue as GameObject;
            if(boss==null||!boss.TryGetComponent<NavMeshAgent>(out var agent))continue;
            Undo.RecordObject(agent,"Delay boss navigation until surface is ready");
            agent.enabled=false;EditorUtility.SetDirty(agent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(agent);changed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(ReportFolder+"/BossNavigationStartup.txt",$"Prepared {changed} scene boss agents. Their AI Start enables them on a valid NavMesh after surface registration.\n");
        Debug.Log("Prepared boss navigation startup: "+changed);
    }

    private static void Begin(BuildTarget[] targets)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer||SessionState.GetBool(Pending,false))
            throw new InvalidOperationException("Stop Play Mode and wait for the current build before starting another.");
        if(!EditorSceneManager.SaveOpenScenes())throw new InvalidOperationException("Could not save open scenes.");
        foreach(string scene in BuildPreparationSetup.ShippingScenes)
            if(!File.Exists(scene))throw new FileNotFoundException("Missing shipping scene",scene);
        PlayerSettings.productName=ProductName;
        EditorBuildSettings.scenes=BuildPreparationSetup.ShippingScenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder+"/DesktopBuildResults.txt","Started "+DateTime.Now.ToString("s")+"\nScenes: "+string.Join(", ",BuildPreparationSetup.ShippingScenes)+"\n");
        SessionState.SetString(Targets,string.Join(",",targets.Select(t=>((int)t).ToString())));
        SessionState.SetInt(Index,0);SessionState.SetBool(Pending,true);
        EditorApplication.update-=ContinueBuild;EditorApplication.update+=ContinueBuild;
    }

    private static void ContinueBuild()
    {
        if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)return;
        if(!SessionState.GetBool(Pending,false)){EditorApplication.update-=ContinueBuild;return;}
        var targets=SessionState.GetString(Targets,"").Split(',').Where(s=>!string.IsNullOrEmpty(s)).Select(s=>(BuildTarget)int.Parse(s)).ToArray();
        int index=SessionState.GetInt(Index,0);
        if(index>=targets.Length)
        {
            SessionState.SetBool(Pending,false);EditorApplication.update-=ContinueBuild;
            File.AppendAllText(ReportFolder+"/DesktopBuildResults.txt","Finished "+DateTime.Now.ToString("s")+"\n");
            File.WriteAllText(ReportFolder+"/DesktopBuildProgress.txt","Finished "+DateTime.Now.ToString("s")+". See DesktopBuildResults.txt for each platform result.");
            Debug.Log("Desktop builds finished. See Logs/BuildPreparation/DesktopBuildResults.txt.");return;
        }
        BuildTarget target=targets[index];
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))
        {RecordFailure(target,"Platform support module is not installed.");SessionState.SetInt(Index,index+1);return;}
        if(EditorUserBuildSettings.activeBuildTarget!=target)
        {
            File.WriteAllText(ReportFolder+"/DesktopBuildProgress.txt","Importing platform "+target+" at "+DateTime.Now.ToString("s"));
            if(!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,target))
            {RecordFailure(target,"Could not switch platform.");SessionState.SetInt(Index,index+1);}
            return;
        }
        running=true;
        try
        {
            string output=target==BuildTarget.StandaloneOSX?"Builds/macOS/"+ProductName+".app":"Builds/Windows/"+ProductName+".exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(ReportFolder+"/DesktopBuildProgress.txt","Building "+target+" at "+DateTime.Now.ToString("s"));
            var options=new BuildPlayerOptions{scenes=BuildPreparationSetup.ShippingScenes,locationPathName=output,target=target,options=BuildOptions.None};
            var report=BuildPipeline.BuildPlayer(options);
            var summary=report.summary;
            var text=new StringBuilder();
            text.AppendLine($"{target}: {summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; bytes={summary.totalSize}; time={summary.totalTime}");
            text.AppendLine("Output: "+Path.GetFullPath(output));
            foreach(var step in report.steps)
                foreach(var message in step.messages)
                    if(message.type==LogType.Error||message.type==LogType.Exception||message.type==LogType.Warning)
                        text.AppendLine(message.type+": "+message.content);
            File.WriteAllText(ReportFolder+"/"+target+".txt",text.ToString());
            File.AppendAllText(ReportFolder+"/DesktopBuildResults.txt",text.ToString());
            if(summary.result==BuildResult.Succeeded)Debug.Log("Build succeeded: "+output);
            else Debug.LogError("Build failed: "+target+". See build report.");
        }
        catch(Exception exception){RecordFailure(target,exception.ToString());Debug.LogException(exception);}
        finally{SessionState.SetInt(Index,index+1);running=false;}
    }

    private static void RecordFailure(BuildTarget target,string error)
    {
        Directory.CreateDirectory(ReportFolder);string text=target+": FAILED\n"+error+"\n";
        File.WriteAllText(ReportFolder+"/"+target+".txt",text);File.AppendAllText(ReportFolder+"/DesktopBuildResults.txt",text);
    }
}
#endif
