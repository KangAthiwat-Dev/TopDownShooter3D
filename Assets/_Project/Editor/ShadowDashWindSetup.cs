using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ShadowDashWindSetup
{
    private const string MaterialPath = "Assets/_Project/Materials/ShadowDash/M_CartoonShadowWind.mat";

    [MenuItem("Tools/VFX/Upgrade to Flowing Cartoon Wind")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Shader shader = Shader.Find("TopDownShooter/VFX/Cartoon Shadow Wind");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Cartoon Wind shader is missing or has compile errors.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowDashTrailSetup.PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Install Shadow Trails first.");
        Directory.CreateDirectory("Logs/PrefabBackups");
        string backup = "Logs/PrefabBackups/ShadowDashTrails_BeforeCartoonWind_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";
        File.Copy(ShadowDashTrailSetup.PrefabPath, backup, false);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "M_CartoonShadowWind" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        GameObject root = PrefabUtility.LoadPrefabContents(ShadowDashTrailSetup.PrefabPath);
        try
        {
            var wind = root.GetComponent<ShadowDashWindVfx>();
            if (wind == null) wind = root.AddComponent<ShadowDashWindVfx>();
            var windSettings = new SerializedObject(wind);
            windSettings.FindProperty("windMaterial").objectReferenceValue = material;
            windSettings.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(root.GetComponent<ShadowDashTrailVfx>());
            settings.FindProperty("windVortex").objectReferenceValue = wind;
            settings.FindProperty("useWindVortex").boolValue = true;
            AudioSource audio = root.GetComponent<AudioSource>();
            if (audio == null) audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = true; audio.spatialBlend = 1f;
            audio.minDistance = 2f; audio.maxDistance = 25f;
            settings.FindProperty("loopAudio").objectReferenceValue = audio;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ShadowDashTrailSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowDashTrailSetup.PrefabPath);
        Debug.Log("Flowing horizontal cartoon wind installed. Both scene instances inherit it. Scene files and appearance settings preserved. Assign a new loop clip if the deleted Trails pack is unavailable. Backup: " + backup);
    }
}
