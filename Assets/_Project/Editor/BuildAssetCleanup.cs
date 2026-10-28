#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class BuildPreparationSetup
{
    public static readonly string[] ShippingScenes={"Assets/_Project/Scenes/MainMenu.unity","Assets/_Project/Scenes/WorldPrototype.unity"};
    [Serializable] private sealed class AssetPlan
    {
        public string created,backup;
        public string[] roots,keep,remove;
        public long removeBytes;
    }
    private static string Normalize(string path)=>path.Replace('\\','/');
    private static AssetPlan ScanAssets()
    {
        var files=Directory.EnumerateFiles("Assets","*",SearchOption.AllDirectories).Select(Normalize).Where(p=>!p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var roots=new HashSet<string>(ShippingScenes,StringComparer.Ordinal);
        var code=new HashSet<string>{".cs",".asmdef",".asmref",".rsp",".dll",".so",".dylib"};
        var shader=new HashSet<string>{".shader",".shadergraph",".shadersubgraph",".hlsl",".cginc",".compute",".shadervariants"};
        foreach(var path in files)
        {
            string extension=Path.GetExtension(path).ToLowerInvariant(),name=Path.GetFileName(path).ToLowerInvariant();
            // Scripts are compiled together, including types referenced from code rather
            // than scene components. Preserve source and shader includes for both targets.
            bool legal=(extension==".txt"||extension==".md"||extension==".pdf")&&new[]{"license","licence","credit","copyright","notice","eula"}.Any(name.Contains);
            if(code.Contains(extension)||shader.Contains(extension)||legal||path.StartsWith("Assets/_Project/Resources/",StringComparison.Ordinal)||path.StartsWith("Assets/TextMesh Pro/Resources/",StringComparison.Ordinal)||path.Contains("/StreamingAssets/"))roots.Add(path);
        }
        foreach(var settings in Directory.EnumerateFiles("ProjectSettings","*.asset"))
        {
            string text=File.ReadAllText(settings);
            // Retired sample scenes must not become roots merely because they were
            // still enabled in the old build list. Keep its input configuration.
            if(Path.GetFileName(settings)=="EditorBuildSettings.asset")
            {int start=text.IndexOf("m_configObjects:",StringComparison.Ordinal);text=start<0?"":text.Substring(start);}
            foreach(Match match in Regex.Matches(text,@"guid: ([0-9a-fA-F]{32})"))
            {string path=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);if(!string.IsNullOrEmpty(path)&&path.StartsWith("Assets/",StringComparison.Ordinal))roots.Add(path);}
        }
        foreach(string guid in AssetDatabase.FindAssets("t:SpriteAtlas"))roots.Add(AssetDatabase.GUIDToAssetPath(guid));
        var kept=new HashSet<string>(AssetDatabase.GetDependencies(roots.Where(File.Exists).ToArray(),true),StringComparer.Ordinal);
        kept.UnionWith(roots);
        // Model import metadata may refer to a separate Humanoid avatar, or remap a
        // material. Explicitly preserve these imports as well as serialized dependencies.
        for(int pass=0;pass<8;pass++)
        {
            var extra=new HashSet<string>(StringComparer.Ordinal);
            foreach(var path in kept.Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).ToArray())
            {
                if(!File.Exists(path+".meta"))continue;
                foreach(Match match in Regex.Matches(File.ReadAllText(path+".meta"),@"guid: ([0-9a-fA-F]{32})"))
                {string referenced=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);if(!string.IsNullOrEmpty(referenced)&&!kept.Contains(referenced))extra.Add(referenced);}
            }
            if(extra.Count==0)break;kept.UnionWith(AssetDatabase.GetDependencies(extra.ToArray(),true));kept.UnionWith(extra);
        }
        var remove=files.Where(p=>!kept.Contains(p)).ToArray();
        return new AssetPlan{created=DateTime.Now.ToString("s"),roots=roots.OrderBy(p=>p).ToArray(),keep=kept.Where(File.Exists).OrderBy(p=>p).ToArray(),remove=remove,removeBytes=remove.Sum(p=>new FileInfo(p).Length)};
    }
    private static void WritePlan(AssetPlan plan)
    {
        Directory.CreateDirectory(LogFolder);File.WriteAllText(LogFolder+"/UnusedAssetPlan.json",JsonUtility.ToJson(plan,true));
        var report=new StringBuilder();report.AppendLine($"Kept files={plan.keep.Length}; unused files={plan.remove.Length}; unused source size={plan.removeBytes/1048576f:F1} MiB");
        report.AppendLine("Roots: MainMenu + WorldPrototype, project settings, compiled C# / assemblies, shader libraries, game/TMP Resources, streaming assets, sprite atlases, licences and all recursive dependencies.");
        foreach(var group in plan.remove.GroupBy(p=>p.Split('/')[1]).OrderByDescending(g=>g.Sum(p=>new FileInfo(p).Length)))report.AppendLine($"{group.Key}: {group.Count()} files, {group.Sum(p=>new FileInfo(p).Length)/1048576f:F1} MiB");
        File.WriteAllText(LogFolder+"/UnusedAssetAudit.txt",report.ToString());
    }
    [MenuItem("Tools/Build Preparation/Audit Unused Assets")]
    public static void AuditAssets()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;AssetDatabase.SaveAssets();var plan=ScanAssets();WritePlan(plan);Debug.Log("Unused asset audit ready: "+plan.remove.Length+" files / "+(plan.removeBytes/1048576f).ToString("F1")+" MiB. Nothing removed.");
    }
    [MenuItem("Tools/Build Preparation/Archive and Remove Verified Unused Assets")]
    public static void CleanupAssets()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().name!=GameMenuUI.WorldScene)throw new InvalidOperationException("Open WorldPrototype first.");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());var plan=ScanAssets();WritePlan(plan);
        if(plan.remove.Length==0){Debug.Log("No unused assets remain.");return;}
        plan.backup="Logs/AssetCleanupBackups/BuildPreparation_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(plan.backup);
        File.WriteAllText(plan.backup+"/Manifest.json",JsonUtility.ToJson(plan,true));
        File.Copy("ProjectSettings/EditorBuildSettings.asset",plan.backup+"/EditorBuildSettings.asset");
        var moved=new List<string>();
        void Move(string path)
        {
            string destination=plan.backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(destination));long length=new FileInfo(path).Length;File.Move(path,destination);moved.Add(path);
            if(new FileInfo(destination).Length!=length)throw new IOException("Backup length mismatch: "+path);
        }
        AssetDatabase.DisallowAutoRefresh();AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var path in plan.remove){Move(path);if(File.Exists(path+".meta"))Move(path+".meta");}
            // Retain folder GUIDs alongside their contents when a directory becomes empty.
            foreach(var directory in Directory.EnumerateDirectories("Assets","*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length).ToArray())
            {
                if(Directory.EnumerateFileSystemEntries(directory).Any())continue;string path=Normalize(directory);if(File.Exists(path+".meta"))Move(path+".meta");Directory.Delete(directory);
            }
            EditorBuildSettings.scenes=ShippingScenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            File.WriteAllText(plan.backup+"/MovedFiles.json",JsonUtility.ToJson(new MovedFiles{paths=moved.ToArray()},true));
        }
        catch
        {
            foreach(var path in moved.AsEnumerable().Reverse()){Directory.CreateDirectory(Path.GetDirectoryName(path));if(!File.Exists(path))File.Move(plan.backup+"/"+path,path);}throw;
        }
        finally{AssetDatabase.StopAssetEditing();AssetDatabase.AllowAutoRefresh();AssetDatabase.Refresh();}
        var missing=plan.keep.Where(p=>!File.Exists(p)).ToArray();if(missing.Length>0)throw new IOException("Retained assets disappeared: "+string.Join(", ",missing));
        File.WriteAllText(LogFolder+"/AssetCleanupResult.txt",$"PASS Retained {plan.keep.Length} files and all their GUIDs.\nRemoved {plan.remove.Length} unused files, {plan.removeBytes/1048576f:F1} MiB from Assets.\nBackup with original assets/metas: {plan.backup}\nShipping scene list: MainMenu, WorldPrototype.\n");
        Debug.Log("Unused assets archived and removed from Unity. Backup: "+plan.backup);
    }
    [Serializable] private sealed class MovedFiles{public string[] paths;}
}
#endif
