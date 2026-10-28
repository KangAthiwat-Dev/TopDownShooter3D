#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public static class DemonCombatAudioSetup
{
    private const string Folder="Assets/_Project/Audio/SoundsBack/";
    private static AudioClip Import(string relative)
    {
        string path=Folder+relative;var importer=AssetImporter.GetAtPath(path) as AudioImporter;
        if(importer==null)throw new InvalidOperationException("Missing supported audio: "+path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;
        importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Tools/Player Audio/Install Demon Ambience and Combat Music")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!=GameMenuUI.WorldScene)throw new InvalidOperationException("Open WorldPrototype first.");
        var control=UnityEngine.Object.FindAnyObjectByType<WorldAmbientAudio>();if(control==null)throw new InvalidOperationException("World ambience owner is missing.");
        var so=new SerializedObject(control);
        if(so.FindProperty("demonSource").objectReferenceValue!=null||so.FindProperty("combatSource").objectReferenceValue!=null)throw new InvalidOperationException("Demon/combat music already installed; adjust the existing sources in Inspector.");
        var areas=UnityEngine.Object.FindObjectsByType<DemonArenaEncounter>(FindObjectsSortMode.None).Select(e=>e.Arena).Distinct().ToArray();
        if(areas.Length!=3||areas.Any(a=>a==null))throw new InvalidOperationException("Expected the three existing Demon arena boundaries.");
        var zone=GameObject.Find("Zone_Demon");if(zone==null)throw new InvalidOperationException("Demon zone is missing.");
        var surfaces=zone.GetComponentsInChildren<MeshRenderer>(true).Where(r=>new[]{"sample_zone","Lord_zone","King_zone","Lava","Lava_fall"}.Contains(r.name)).ToArray();
        if(surfaces.Length<3)throw new InvalidOperationException("Demon territory surfaces are missing.");
        var bounds=surfaces[0].bounds;foreach(var surface in surfaces)bounds.Encapsulate(surface.bounds);bounds.Expand(6);
        var mixer=Resources.Load<AudioMixer>(GameAudioSettings.MixerResource);if(mixer==null)throw new InvalidOperationException("Game audio mixer is missing.");
        var ambience=mixer.FindMatchingGroups("").Single(g=>g.name=="Ambience");var music=mixer.FindMatchingGroups("").Single(g=>g.name=="Music");
        Directory.CreateDirectory("Logs/SceneBackups");EditorSceneManager.SaveScene(scene,"Logs/SceneBackups/BeforeDemonCombatAudio_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        var demon=Import("demon/horror-ambience-2482.wav");var combat=Import("fighting/torone-music-loops-pack-01-dark-fantasy-combat.ogg");
        AudioSource Source(string name,AudioClip clip,AudioMixerGroup group)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add Demon and combat audio");go.transform.SetParent(control.transform,false);
            var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.playOnAwake=false;source.volume=0;source.spatialBlend=0;source.dopplerLevel=0;source.priority=128;source.outputAudioMixerGroup=group;return source;
        }
        Undo.RecordObject(control,"Connect Demon and combat audio");so.Update();
        so.FindProperty("demonSource").objectReferenceValue=Source("Demon Territory Ambience",demon,ambience);
        so.FindProperty("combatSource").objectReferenceValue=Source("Combat Music",combat,music);
        var boundary=new GameObject("Demon Audio Boundary");Undo.RegisterCreatedObjectUndo(boundary,"Add editable Demon music boundary");boundary.transform.SetParent(zone.transform,true);boundary.transform.position=bounds.center;
        so.FindProperty("demonTerritory").objectReferenceValue=boundary.transform;so.FindProperty("demonTerritorySize").vector2Value=new Vector2(bounds.size.x,bounds.size.z);
        var list=so.FindProperty("demonAreas");list.arraySize=areas.Length;for(int i=0;i<areas.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=areas[i];
        so.ApplyModifiedProperties();EditorUtility.SetDirty(control);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/DemonCombatAudioInstallation.txt",$"Demon={demon.name}, {demon.length:F2}s, Streaming, Ambience bus, volume .18\nCombat={combat.name}, {combat.length:F2}s, Streaming, Music bus, volume .22\nDemon areas={string.Join(", ",areas.Select(a=>a.name))}; boundary={bounds}\nCombat: actual player pursuit / firing near enemies / damage / active arena (including wave gaps). Six-second release. Zone fades 2.5s; combat fade-in .8s; city music ducks in combat.\n");
        Debug.Log("Demon territory ambience and player combat music installed with existing volume controls.");
    }
}
#endif
