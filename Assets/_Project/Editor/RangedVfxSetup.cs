using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RangedVfxSetup
{
    public const string ProjectilePath = "Assets/_Project/Prefabs/Projectiles/PlayerEnergyTracer.prefab";
    public const string MuzzlePath = "Assets/_Project/Prefabs/VFX/RifleSpikyMuzzle.prefab";
    public const string ImpactPath = "Assets/_Project/Prefabs/VFX/RifleSpikyImpact.prefab";
    private const string Pack = "Assets/Vefects/Flipbook VFX Bundle Lite URP/";
    private const string Combat = Pack + "Combat Flipbook VFX URP/";
    private const string MaterialFolder = "Assets/_Project/Materials/Ranged";
    public const string ShotSound = "Assets/_Project/Audio/Gun/pistol-sound-effect.mp3";
    private static Material core, glow, refraction, spiky;

    [MenuItem("Tools/Combat/Use Pistol Shot Sound")]
    public static void UsePistolShotSound()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ShotSound);
        if (clip == null) throw new InvalidOperationException("Missing pistol sound: " + ShotSound);
        Scene initial = SceneManager.GetActiveScene();
        try
        {
            foreach (string path in new[] { "Assets/_Project/Scenes/WorldPrototype.unity", "Assets/_Project/Scenes/PrototypeArena.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Directory.CreateDirectory("Logs/SceneBackups");
                    if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforePistolSound_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true))
                        throw new IOException("Could not back up " + scene.name);
                    PlayerRangedFeedback feedback = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerRangedFeedback>(true)).First();
                    Undo.RecordObject(feedback, "Change rifle shot sound");
                    var data = new SerializedObject(feedback); data.FindProperty("shotClip").objectReferenceValue = clip; data.ApplyModifiedProperties();
                    if (PrefabUtility.IsPartOfPrefabInstance(feedback)) PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + scene.name);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial); }
        Debug.Log("pistol-sound-effect is now the shot sound in both gameplay scenes. Impact sound, volume, pitch, cadence and VFX are unchanged.");
    }

    [MenuItem("Tools/Combat/Install Thin Tracer and Spiky VFX")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Ranged");
        core = MakeMaterial("M_Rifle_Tracer_Core", "TopDownShooter/VFX/Energy Tracer URP", new Color(3.5f, 2.4f, 0.7f, 1f));
        glow = MakeMaterial("M_Rifle_Tracer_Glow", "TopDownShooter/VFX/Energy Tracer URP", new Color(1.6f, 0.65f, 0.12f, 0.28f));
        refraction = MakeMaterial("M_Rifle_Refraction", "TopDownShooter/VFX/Ranged Refraction URP", Color.white);
        spiky = MakeSpikyMaterial();
        BuildBurst(MuzzlePath, true); BuildBurst(ImpactPath, false); BuildProjectile();
        Scene initial = SceneManager.GetActiveScene();
        try
        {
            InstallScene("Assets/_Project/Scenes/WorldPrototype.unity");
            InstallScene("Assets/_Project/Scenes/PrototypeArena.unity");
        }
        finally { if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial); }
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        Debug.Log("Thin amber tracers + one-shot Spiky Hit + transparent refraction + pistol shot sound installed. Rifle interval 0.18s; damage, bullet speed and melee unchanged. Backups: Logs/SceneBackups/*BeforeRangedVfx*.unity. Original asset pack/projectile are unchanged.");
    }
    private static Material MakeMaterial(string name, string shaderName, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find(shaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Invalid shader " + shaderName);
        material = new Material(shader) { name = name };
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Material MakeSpikyMaterial()
    {
        string path = MaterialFolder + "/M_Rifle_SpikyHit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Material source = AssetDatabase.LoadAssetAtPath<Material>(Combat + "_ Lite/Material/Lite_M_VFX_Radial_Spiky_Hit_01.mat");
        if (source == null || source.shader == null || ShaderUtil.ShaderHasError(source.shader)) throw new InvalidOperationException("Spiky Hit shader/material is unavailable");
        // Copy only properties the Lite shader actually uses: old unused
        // texture slots in the source .mat contain unrelated dangling GUIDs.
        material = new Material(source.shader) { name = "M_Rifle_SpikyHit" };
        foreach (string field in new[] { "_AlphaTexture", "_MaskTexture" })
        {
            Texture texture = source.GetTexture(field);
            if (texture == null) throw new InvalidOperationException("Missing Spiky texture: " + field);
            material.SetTexture(field, texture);
        }
        foreach (string field in new[] { "_R", "_G", "_B", "_Outline" }) material.SetColor(field, source.GetColor(field));
        material.SetFloat("_Emissive", 1.5f); material.SetFloat("_AlphaGlow", 1f);
        material.SetFloat("_Cull", 0f); material.SetFloat("_Src", 5f); material.SetFloat("_Dst", 10f);
        material.SetFloat("_ZWrite", 0f); material.SetFloat("_ZTest", 4f);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void BuildBurst(string path, bool muzzle)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Combat + "_ Lite/Particles/Lite_VFX_Radial_Spiky_Hit_01_Random_Rotate_Loop.prefab");
        if (source == null) throw new InvalidOperationException("Import the free Flipbook VFX Bundle Lite URP first");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = muzzle ? "Rifle Spiky Muzzle" : "Rifle Spiky Impact";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            foreach (AudioSource sound in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(sound);
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main;
                main.loop = false; main.playOnAwake = false; main.prewarm = false;
                main.duration = muzzle ? 0.09f : 0.2f; main.startLifetime = muzzle ? 0.085f : 0.18f;
                main.startDelay = 0f; main.startSpeed = 0f; main.startSize = muzzle ? 0.32f : 0.85f;
                main.simulationSpace = ParticleSystemSimulationSpace.Local; main.useUnscaledTime = false;
                main.stopAction = ParticleSystemStopAction.None;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                bool visible = renderer != null && renderer.renderMode != ParticleSystemRenderMode.None;
                var emission = system.emission; emission.enabled = visible;
                emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
                emission.SetBursts(visible ? new[] { new ParticleSystem.Burst(0f, 1) } : Array.Empty<ParticleSystem.Burst>());
                if (visible)
                {
                    renderer.sharedMaterial = spiky;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
            }
            MakeRefraction(root.transform, muzzle ? 0.045f : 0.1f, muzzle ? 0.34f : 0.68f, muzzle ? 0.13f : 0.23f, muzzle ? 0.002f : 0.0035f, 0.55f);
            var burst = root.AddComponent<RangedBurstVfx>(); var settings = new SerializedObject(burst);
            SerializedProperty array = settings.FindProperty("particles"); array.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            settings.FindProperty("lifetime").floatValue = muzzle ? 0.24f : 0.7f; settings.ApplyModifiedPropertiesWithoutUndo();
            if (!muzzle)
            {
                AudioSource audio = root.AddComponent<AudioSource>();
                audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Pack + "Pixel Craft VFX URP/Audio/WAV/Sergi/SFX_Vefects_Projectile_Burst_Impact_03.wav");
                audio.playOnAwake = true; audio.loop = false; audio.volume = 0.1f;
                audio.spatialBlend = 0.75f; audio.minDistance = 3f; audio.maxDistance = 25f; audio.dopplerLevel = 0f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void BuildProjectile()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null) return;
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Projectiles/Projectile.prefab");
        try
        {
            // New prefab, original ball/collision prefab stays available. Root
            // scale is normalized but the original world collision radius is kept.
            float radius = root.GetComponent<SphereCollider>().radius * root.transform.lossyScale.x;
            root.name = "Player Energy Tracer"; root.transform.localScale = Vector3.one; root.transform.localPosition = Vector3.zero;
            root.GetComponent<SphereCollider>().radius = radius;
            root.GetComponent<MeshRenderer>().enabled = false;
            TrailRenderer trail = root.GetComponent<TrailRenderer>();
            trail.sharedMaterial = glow; trail.time = 0.045f; trail.widthMultiplier = 0.028f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.colorGradient = FadeGradient(); trail.minVertexDistance = 0.03f;
            trail.numCapVertices = 4; trail.numCornerVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Stretch; trail.Clear();
            MakeLine(root.transform, "Thin Core", core, 0.028f);
            MakeLine(root.transform, "Soft Glow", glow, 0.07f);
            MakeRefraction(root.transform, 0.16f, 0.16f, 0f, 0.0012f, 0.3f);
            var data = new SerializedObject(root.GetComponent<Projectile>());
            data.FindProperty("impactVfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath).GetComponent<RangedBurstVfx>();
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void MakeLine(Transform parent, string name, Material material, float width)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false;
        line.positionCount = 2; line.SetPositions(new[] { Vector3.back * 0.5f, Vector3.forward * 0.08f });
        line.widthMultiplier = width; line.sharedMaterial = material;
        line.numCapVertices = 4; line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        line.startColor = line.endColor = Color.white;
    }
    private static Gradient FadeGradient()
    {
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0.5f, 0), new GradientAlphaKey(0, 1) }); return gradient;
    }
    private static void MakeRefraction(Transform parent, float start, float end, float duration, float strength, float opacity)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Transparent Refraction";
        SceneManager.MoveGameObjectToScene(quad, parent.gameObject.scene); quad.transform.SetParent(parent, false);
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
        var renderer = quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial = refraction;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var data = new SerializedObject(quad.AddComponent<RangedRefraction>());
        data.FindProperty("meshRenderer").objectReferenceValue = renderer;
        data.FindProperty("startRadius").floatValue = start; data.FindProperty("endRadius").floatValue = end;
        data.FindProperty("duration").floatValue = duration; data.FindProperty("strength").floatValue = strength;
        data.FindProperty("opacity").floatValue = opacity; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void InstallScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Directory.CreateDirectory("Logs/SceneBackups");
            if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforeRangedVfx_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true))
                throw new IOException("Could not back up " + scene.name);
            PlayerRangedWeapon weapon = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerRangedWeapon>(true)).First();
            var data = new SerializedObject(weapon); Undo.RecordObject(weapon, "Install rifle visuals");
            data.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath).GetComponent<Projectile>();
            data.ApplyModifiedProperties();
            PlayerAttackController attack = weapon.GetComponent<PlayerAttackController>(); Undo.RecordObject(attack, "Rifle cadence");
            data = new SerializedObject(attack); data.FindProperty("rangedAttackInterval").floatValue = 0.18f; data.ApplyModifiedProperties();
            PlayerRangedFeedback feedback = weapon.GetComponent<PlayerRangedFeedback>() ?? Undo.AddComponent<PlayerRangedFeedback>(weapon.gameObject);
            Transform existing = weapon.transform.Find("Rifle Shot Audio");
            AudioSource audio;
            if (existing == null)
            {
                var go = new GameObject("Rifle Shot Audio"); SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Rifle sound"); go.transform.SetParent(weapon.transform, false); audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.loop = false; audio.dopplerLevel = 0f; audio.spatialBlend = 0.25f;
                audio.minDistance = 4f; audio.maxDistance = 30f;
            }
            else audio = existing.GetComponent<AudioSource>();
            Undo.RecordObject(feedback, "Rifle effects"); data = new SerializedObject(feedback);
            data.FindProperty("muzzleVfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzlePath).GetComponent<RangedBurstVfx>();
            data.FindProperty("shotAudio").objectReferenceValue = audio;
            data.FindProperty("shotClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ShotSound);
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + scene.name);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
