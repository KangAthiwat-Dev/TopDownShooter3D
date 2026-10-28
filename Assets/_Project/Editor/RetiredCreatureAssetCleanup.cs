#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class RetiredCreatureAssetCleanup
{
    public const string RifleFolder="Assets/_Project/Weapons/SpaceRifle";
    public static readonly string[] Retired={
        "Assets/Assets_Imported/alphaWolf",
        "Assets/_Project/Enemies/StoryCreatures/AlphaWolf",
        "Assets/Spiders",
        "Assets/_Project/Enemies/Creatures/Spider",
        "Assets/_Project/Enemies/Creatures/Materials/M_Spider_BlackWidow.mat",
        "Assets/Assets_Imported/SciFi_Space_Soldier",
        "Assets/_Project/Materials/PlayerSoldier"
    };
    private static bool Inside(string path,string root)=>path==root||path.StartsWith(root+"/")||path==root+".meta";
    private static void Backup(string path,string destination)
    {
        if(File.Exists(path)){Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination,true);}
        else if(Directory.Exists(path))foreach(var file in Directory.GetFiles(path,"*",SearchOption.AllDirectories))
        {string copy=destination+file.Substring(path.Length);Directory.CreateDirectory(Path.GetDirectoryName(copy));File.Copy(file,copy,true);}
        if(File.Exists(path+".meta")){Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path+".meta",destination+".meta",true);}
    }
    [MenuItem("Tools/Story Creatures/Remove Retired Models and Keep Player Rifle")]
    public static void Cleanup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
        if(Retired.All(p=>!File.Exists(p)&&!Directory.Exists(p))){Debug.Log("Retired assets have already been removed; retained rifle preserved.");return;}
        var wolf=AssetDatabase.LoadAssetAtPath<GameObject>(StoryCreatureSetup.WolfPrefab);
        if(wolf==null||!AssetDatabase.GetDependencies(StoryCreatureSetup.WolfPrefab,true).Contains(StoryCreatureSetup.WolfSource))throw new InvalidOperationException("Install the native Wolf Boss before removing old assets.");
        const string oldPack="Assets/Assets_Imported/SciFi_Space_Soldier";
        var rifle=AssetDatabase.GetDependencies("Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab",true).Where(p=>p.StartsWith(oldPack+"/")).ToArray();
        if(rifle.Any(p=>!p.StartsWith(oldPack+"/Resources/Weapons/Rifle/")))throw new InvalidOperationException("The player still depends on a soldier body or animation. Preserve that dependency before cleanup.");
        string backup="Logs/AssetCleanupBackups/RetiredCreatures_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        foreach(string root in Retired)Backup(root,backup+"/"+root);
        Directory.CreateDirectory(RifleFolder);AssetDatabase.Refresh();
        foreach(var path in rifle)
        {
            string error=AssetDatabase.MoveAsset(path,RifleFolder+"/"+Path.GetFileName(path));if(!string.IsNullOrEmpty(error))throw new IOException(error);
        }
        const string oldMaterial="Assets/_Project/Materials/PlayerSoldier/M_Player_M_Weapon_Rifle_A.mat";
        if(File.Exists(oldMaterial))
        {
            string error=AssetDatabase.MoveAsset(oldMaterial,RifleFolder+"/PlayerRifle_URP.mat");if(!string.IsNullOrEmpty(error))throw new IOException(error);
        }
        AssetDatabase.SaveAssets();
        // Refuse deletion if any surviving serialized asset still points into a retiring folder.
        var guids=new Dictionary<string,string>();
        foreach(string root in Retired)
        {
            var metas=Directory.Exists(root)?Directory.GetFiles(root,"*.meta",SearchOption.AllDirectories).ToList():new List<string>();
            if(File.Exists(root+".meta"))metas.Add(root+".meta");
            foreach(string meta in metas){var match=Regex.Match(File.ReadAllText(meta),@"(?m)^guid: ([0-9a-f]{32})");if(match.Success)guids[match.Groups[1].Value]=meta;}
        }
        var extensions=new HashSet<string>{".prefab",".unity",".asset",".mat",".controller",".overridecontroller",".anim",".mask",".meta"};
        var blockers=new List<string>();
        foreach(string path in Directory.EnumerateFiles("Assets","*",SearchOption.AllDirectories))
        {
            if(!extensions.Contains(Path.GetExtension(path).ToLowerInvariant())||Retired.Any(root=>Inside(path,root)))continue;
            foreach(Match match in Regex.Matches(File.ReadAllText(path),@"guid: ([0-9a-f]{32})"))
                if(guids.TryGetValue(match.Groups[1].Value,out var source))blockers.Add(path+" -> "+source);
        }
        File.WriteAllText("Logs/RetiredAssetReferenceCheck.txt",blockers.Count==0?"PASS No surviving serialized references into retired assets.\n":string.Join("\n",blockers));
        if(blockers.Count>0)throw new InvalidOperationException("Retired assets are still referenced. See Logs/RetiredAssetReferenceCheck.txt; nothing has been deleted.");
        long bytes=0;int files=0;
        foreach(string root in Retired)
        {
            if(Directory.Exists(root))foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){bytes+=new FileInfo(file).Length;files++;}
            else if(File.Exists(root)){bytes+=new FileInfo(root).Length;files++;}
            if((File.Exists(root)||Directory.Exists(root))&&!AssetDatabase.DeleteAsset(root))throw new IOException("Could not remove "+root);
        }
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/RetiredAssetCleanup.txt",$"Removed {files} asset/meta files; {bytes/1048576f:F1} MiB from imported Assets.\nPlayer rifle mesh, four textures and URP material moved to {RifleFolder} with their original GUIDs.\nOld wolf, temporary wolf clips, old spider and Space Soldier model pack removed.\nRecovery backup: {backup}\n");
        Debug.Log("Retired models removed; player rifle and all surviving references preserved.");
    }
}
#endif
