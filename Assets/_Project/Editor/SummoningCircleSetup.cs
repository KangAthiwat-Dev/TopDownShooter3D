#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SummoningCircleSetup
{
    public const string Folder = "Assets/_Project/World/SummoningSanctum/MagicCircle";
    public const string PrefabPath = Folder + "/PlayerSummonCircle.prefab";
    public const string SourcePath = "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab";

    [MenuItem("Tools/World/Summoning/Inspect Magic Circle Source")]
    public static void Inspect()
    {
        var report = new StringBuilder("Hovl magic circle audit — " + DateTime.Now.ToString("s") + "\n");
        foreach (string name in new[] { "Magic circle", "Magic circle 2" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/" + name + ".prefab");
            report.AppendLine("PREFAB " + name);
            foreach (ParticleSystem p in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = p.main;
                report.AppendLine($"  {p.name}: duration={main.duration}, size={main.startSize.constantMax}, lifetime={main.startLifetime.constantMax}, loop={main.loop}, color={main.startColor.color}, render={p.GetComponent<ParticleSystemRenderer>().renderMode}");
            }
            foreach (Material material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                report.AppendLine($"  MATERIAL {material.name}: shader={material.shader.name}, texture={material.mainTexture?.name}, source={AssetDatabase.GetAssetPath(material)}");
        }
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>())
            if (volume.sharedProfile != null && volume.sharedProfile.TryGet(out Bloom bloom))
                report.AppendLine($"BLOOM {volume.name}: active={bloom.active}, intensity={bloom.intensity.value}");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningCircleSourceAudit.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/World/Summoning/Install Magic Circle Spawn")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var spawn = UnityEngine.Object.FindAnyObjectByType<PlayerSceneSpawnPoint>();
        if (spawn == null) throw new InvalidOperationException("The scene's player spawn marker is missing.");
        Transform anchor = spawn.transform.parent.Find("SummonVfxAnchor");
        if (anchor == null) throw new InvalidOperationException("Existing SummonVfxAnchor missing; refusing guessed placement.");
        if (UnityEngine.Object.FindAnyObjectByType<SummoningCircleVfx>() != null || AssetDatabase.IsValidFolder(Folder))
            throw new InvalidOperationException("Magic circle already installed. Edit the existing copy instead of replacing it.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var shader = Shader.Find("TopDownShooter/VFX/Summoning Circle URP");
        if (source == null || shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Magic circle source or working URP shader missing.");
        Directory.CreateDirectory("Logs/SceneBackups");
        Scene scene = SceneManager.GetActiveScene();
        string backup = "Logs/SceneBackups/" + scene.name + "_BeforeMagicCircle_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
        AssetDatabase.CreateFolder("Assets/_Project/World/SummoningSanctum", "MagicCircle");
        AssetDatabase.CreateFolder(Folder, "Materials");
        var go = UnityEngine.Object.Instantiate(source); go.name = "PlayerSummonCircle";
        try
        {
            var mapping = new Dictionary<Material, Material>();
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    if (original == null) throw new InvalidOperationException("Source renderer has a missing material.");
                    if (mapping.TryGetValue(original, out Material cached)) return cached;
                    var material = new Material(shader) { name = "M_Summon_" + original.name.Replace(' ', '_') };
                    material.SetTexture("_MainTex", original.mainTexture);
                    material.SetTextureScale("_MainTex", original.mainTextureScale); material.SetTextureOffset("_MainTex", original.mainTextureOffset);
                    material.SetColor("_SourceColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
                    material.SetColor("_EffectTint", new Color(0.12f, 0.45f, 1f, 1)); material.SetFloat("_Intensity", 2); material.SetFloat("_Opacity", 0);
                    AssetDatabase.CreateAsset(material, Folder + "/Materials/" + material.name + ".mat"); mapping.Add(original, material); return material;
                }).ToArray();
            }
            foreach (ParticleSystem particle in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main; main.playOnAwake = false; main.useUnscaledTime = false; main.stopAction = ParticleSystemStopAction.None;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var collision = particle.collision; collision.enabled = false;
                var trigger = particle.trigger; trigger.enabled = false;
                var particleLights = particle.lights; particleLights.enabled = false;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            // Replace the demo's particle-spawned lights with one bounded light
            // that follows the same tint/fade and cannot linger after the sequence.
            foreach (Light originalLight in go.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(originalLight);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2; // Ignore Raycast; visual only.
            var lightObject = new GameObject("Summoning ground light"); lightObject.layer = 2;
            lightObject.transform.SetParent(go.transform, false); lightObject.transform.localPosition = Vector3.up * 0.7f;
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.shadows = LightShadows.None; light.intensity = 0; light.range = 6; light.enabled = false;
            var effect = go.AddComponent<SummoningCircleVfx>();
            var settings = new SerializedObject(effect);
            settings.FindProperty("groundLight").objectReferenceValue = light;
            settings.FindProperty("authoredRadius").floatValue = Mathf.Max(0.1f, go.GetComponent<ParticleSystem>().main.startSize.constantMax * 0.5f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        var installed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), anchor);
        Undo.RegisterCreatedObjectUndo(installed, "Add one-shot player summoning circle");
        installed.transform.localPosition = Vector3.up * 0.035f; installed.transform.localRotation = Quaternion.identity; installed.transform.localScale = Vector3.one;
        // No player visibility/input lock, enemy/wave changes, terrain editing or rebake.
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save magic circle integration.");
        Selection.activeGameObject = installed;
        Validate(); Capture();
    }

    [MenuItem("Tools/World/Summoning/Validate Magic Circle")]
    public static void Validate()
    {
        bool passed = true; var report = new StringBuilder("Summoning circle validation — " + DateTime.Now.ToString("s") + "\n");
        Action<bool, string> check = (ok, message) => { passed &= ok; report.AppendLine((ok ? "PASS " : "FAIL ") + message); };
        var effects = UnityEngine.Object.FindObjectsByType<SummoningCircleVfx>();
        check(effects.Length == 1, "Exactly one circle at the existing player spawn anchor");
        foreach (var effect in effects)
        {
            check(effect.transform.parent.name == "SummonVfxAnchor", "Circle uses the reserved anchor, not guessed world coordinates");
            check(effect.GetComponentsInChildren<Collider>(true).Length == 0, "No physics/interaction/sight-blocking colliders");
            check(effect.GetComponentsInChildren<ParticleSystem>(true).All(p => !p.main.playOnAwake && !p.main.useUnscaledTime), "Particles start only through the one-shot sequence and obey pause");
            foreach (Material m in effect.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                check(m != null && AssetDatabase.GetAssetPath(m).StartsWith(Folder) && !ShaderUtil.ShaderHasError(m.shader), "Project-owned working URP material: " + m?.name);
            check(effect.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
        }
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SummoningCircleValidation.txt", report.ToString()); Debug.Log(report.ToString());
    }

    [MenuItem("Tools/World/Summoning/Capture Magic Circle")]
    public static void Capture()
    {
        var sceneEffect = UnityEngine.Object.FindAnyObjectByType<SummoningCircleVfx>();
        if (sceneEffect == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject preview = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        preview.name = "Temporary summoning circle preview";
        preview.transform.SetPositionAndRotation(sceneEffect.transform.position, sceneEffect.transform.rotation);
        EditorUtility.CopySerialized(sceneEffect, preview.GetComponent<SummoningCircleVfx>());
        // Restore the preview's own light reference after copying scene tuning.
        var settings = new SerializedObject(preview.GetComponent<SummoningCircleVfx>()); settings.FindProperty("groundLight").objectReferenceValue = preview.GetComponentInChildren<Light>(true); settings.ApplyModifiedPropertiesWithoutUndo();
        var cameraObject = new GameObject("Temporary summoning preview camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.transform.position = preview.transform.position + new Vector3(5.6f, 6.8f, -6.4f); camera.transform.LookAt(preview.transform.position + Vector3.up * 0.6f); camera.fieldOfView = 43;
        var rt = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32); var old = RenderTexture.active;
        var pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        try
        {
            preview.GetComponent<SummoningCircleVfx>().SampleForPreview(1.2f);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/SummoningCircle_Preview.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = old; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
#endif
