using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class IceDashSetup
{
    private const string Source = "Assets/Vefects/Trails VFX/";
    private const string Materials = "Assets/_Project/Materials/ShadowDash/";
    private const string Animations = "Assets/_Project/Animations/Player/Dash/";
    private const string Controller = "Assets/_Project/Animations/Player/PlayerAnimator.controller";
    private const string Ual = "Assets/_Project/Art/ThirdParty/Quaternius/";

    [MenuItem("Tools/VFX/Repair Missing Ice Head References")]
    public static void RepairSceneHeads()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene initial = SceneManager.GetActiveScene();
        int repaired = 0;
        try
        {
            foreach (string scenePath in new[] { "Assets/_Project/Scenes/WorldPrototype.unity", "Assets/_Project/Scenes/PrototypeArena.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var missing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShadowDashIceForm>(true))
                        .Where(form => new SerializedObject(form).FindProperty("headRenderer").objectReferenceValue == null).ToArray();
                    if (missing.Length == 0) continue;
                    Directory.CreateDirectory("Logs/SceneBackups");
                    if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforeIceReferenceRepair_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true))
                        throw new IOException("Could not back up " + scene.name);
                    foreach (ShadowDashIceForm form in missing)
                    {
                        Transform head = form.transform.Find("Rounded Ice Comet Head");
                        MeshRenderer renderer = head != null ? head.GetComponent<MeshRenderer>() : null;
                        if (renderer == null) throw new InvalidOperationException("No existing Ice head to reconnect in " + scene.name);
                        Undo.RecordObject(form, "Reconnect missing Ice head");
                        var data = new SerializedObject(form); data.FindProperty("headRenderer").objectReferenceValue = renderer; data.ApplyModifiedProperties();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(form); repaired++;
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + scene.name);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial); }
        Debug.Log("Reconnected " + repaired + " missing Ice head reference(s). Existing palettes, dimensions, animation and movement tuning are unchanged.");
    }

    [MenuItem("Tools/VFX/Install Ice Trails and Dash Animations")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs/PrefabBackups");
        Directory.CreateDirectory("Logs/AnimationBackups");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        File.Copy(ShadowDashTrailSetup.PrefabPath, "Logs/PrefabBackups/ShadowDashTrails_BeforeIce_" + stamp + ".prefab");
        File.Copy(Controller, "Logs/AnimationBackups/PlayerAnimator_BeforeIce_" + stamp + ".controller");
        if (!AssetDatabase.IsValidFolder(Animations.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/_Project/Animations/Player", "Dash");
        AnimationClip roll = CopyClip("UAL1", "Roll", "ShadowRoll_Enter");
        AnimationClip land = CopyClip("UAL2", "NinjaJump_Land", "ShadowNinjaJump_Land");
        AnimationClip shield = CopyClip("UAL2", "Shield_Dash", "Shield_Dash");
        ConfigureAnimator(roll, land, shield);
        Material glow = CopyMaterial("M_VFX_BIRP_Trail_Ice_01", "M_IceTrail_Glow", "TopDownShooter/VFX/Shadow Trail URP");
        Material smoke = CopyMaterial("M_VFX_BIRP_Trail_Ice_02", "M_IceTrail_Secondary", "TopDownShooter/VFX/Shadow Trail URP");
        Material distortion = CopyMaterial("M_VFX_BIRP_Trail_Distortion_01", "M_IceTrail_Distortion", "TopDownShooter/VFX/Shadow Trail Distortion URP");
        Shader headShader = Shader.Find("TopDownShooter/VFX/Ice Trail Comet Head");
        if (headShader == null || ShaderUtil.ShaderHasError(headShader)) throw new InvalidOperationException("Invalid comet head shader");
        Material head = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_IceTrail_Head.mat");
        if (head == null)
        {
            head = new Material(headShader) { name = "M_IceTrail_Head" };
            head.SetTexture("_Noise", glow.GetTexture("_Distortion"));
            AssetDatabase.CreateAsset(head, Materials + "M_IceTrail_Head.mat");
        }
        GameObject root = PrefabUtility.LoadPrefabContents(ShadowDashTrailSetup.PrefabPath);
        try
        {
            Transform imported = root.transform.Find("Ice Trail Asset");
            bool firstInstall = imported == null;
            if (firstInstall)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "VFX/Particles/VFX_Trail_Ice.prefab");
                if (source == null) throw new InvalidOperationException("Import Trails VFX Ice prefab first");
                GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source, root.scene);
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                copy.name = "Ice Trail Asset";
                copy.transform.SetParent(root.transform, false);
                imported = copy.transform;
                // Root controller owns the one loop sound, not the source copy.
                foreach (AudioSource extraAudio in copy.GetComponentsInChildren<AudioSource>()) UnityEngine.Object.DestroyImmediate(extraAudio);
            }
            ShadowDashIceForm form = root.GetComponent<ShadowDashIceForm>();
            if (form == null) form = root.AddComponent<ShadowDashIceForm>();
            Transform existingHead = root.transform.Find("Rounded Ice Comet Head");
            if (existingHead == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Rounded Ice Comet Head";
                UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root.transform, false);
                existingHead = sphere.transform;
            }
            MeshRenderer headRenderer = existingHead.GetComponent<MeshRenderer>();
            headRenderer.sharedMaterial = head; headRenderer.enabled = false;
            headRenderer.shadowCastingMode = ShadowCastingMode.Off; headRenderer.receiveShadows = false;
            var formData = new SerializedObject(form);
            formData.FindProperty("headRenderer").objectReferenceValue = headRenderer;
            formData.ApplyModifiedPropertiesWithoutUndo();
            ShadowDashTrailVfx effect = root.GetComponent<ShadowDashTrailVfx>();
            var settings = new SerializedObject(effect);
            settings.FindProperty("iceForm").objectReferenceValue = form;
            settings.FindProperty("useWindVortex").boolValue = false;
            foreach (TrailRenderer trail in imported.GetComponentsInChildren<TrailRenderer>(true))
            {
                string field = trail.name == "Trail 01" ? "glowTrail" : trail.name == "Trail 02" ? "smokeTrail" : "distortionTrail";
                trail.sharedMaterial = field == "glowTrail" ? glow : field == "smokeTrail" ? smoke : distortion;
                trail.numCapVertices = 8; trail.numCornerVertices = 4;
                if (firstInstall) trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.02f);
                trail.emitting = false; trail.enabled = false; trail.Clear();
                trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
                settings.FindProperty(field).objectReferenceValue = trail;
            }
            AudioSource loop = root.GetComponent<AudioSource>() ?? root.AddComponent<AudioSource>();
            if (firstInstall || loop.clip == null) loop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Source + "Sounds/WAV/Vefects_SFX_Trail_Ice.wav");
            loop.playOnAwake = false; loop.loop = true; loop.volume = 0f;
            settings.FindProperty("loopAudio").objectReferenceValue = loop;
            if (firstInstall)
            {
                settings.FindProperty("width").floatValue = 1.35f;
                settings.FindProperty("lifetime").floatValue = 0.3f;
                settings.FindProperty("smokeWidthMultiplier").floatValue = 1.6f;
                settings.FindProperty("smokeLifetimeMultiplier").floatValue = 1.05f;
                settings.FindProperty("distortionStrength").floatValue = 0.008f;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (firstInstall) effect.ApplyPreset(ShadowTrailPreset.Ice);
            var oldWind = root.GetComponent<ShadowDashWindVfx>();
            if (oldWind != null) oldWind.enabled = false;
            effect.StopImmediately();
            PrefabUtility.SaveAsPrefabAsset(root, ShadowDashTrailSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller));
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowDashTrailSetup.PrefabPath);
        Debug.Log("Ice Trails + rounded comet head, UAL1 Roll enter, UAL2 NinjaJump_Land and Shield_Dash installed. Scene overrides and Short Dash trail unchanged. Backups in Logs.");
    }

    private static AnimationClip CopyClip(string library, string name, string target)
    {
        string source = Ual + library + "/" + library + "_Standard.fbx";
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>()
            .FirstOrDefault(item => item.name == "Armature|" + name);
        if (clip == null || !clip.humanMotion) throw new InvalidOperationException("Missing humanoid clip: " + name);
        string destination = Animations + target + ".anim";
        AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
        if (copy != null) return copy;
        copy = UnityEngine.Object.Instantiate(clip); copy.name = target;
        var settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = false;
        settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        AssetDatabase.CreateAsset(copy, destination);
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/IceDashClipInspection.txt", name + " source=" + source + " length=" + clip.length + " fps=" + clip.frameRate + " humanoid=" + clip.humanMotion + "\n");
        return copy;
    }

    private static void ConfigureAnimator(AnimationClip roll, AnimationClip land, AnimationClip shield)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = machine.states.First(item => item.state.name == "Locomotion").state;
        AnimatorState dash = machine.states.First(item => item.state.name == "DashRoll" || item.state.name == "DashShield").state;
        dash.name = "DashShield"; dash.motion = shield; dash.speed = shield.length / 0.23f;
        foreach (AnimatorStateTransition transition in dash.transitions)
        { transition.exitTime = 0.82f; transition.duration = 0.08f; }
        ConfigureState(machine, "ShadowRollEnter", roll, 1.35f, locomotion, 0.34f);
        ConfigureState(machine, "ShadowNinjaLand", land, land.length / 0.48f, locomotion, 0.84f);
        EditorUtility.SetDirty(controller);
    }
    private static void ConfigureState(AnimatorStateMachine machine, string name, AnimationClip clip, float speed, AnimatorState locomotion, float exitAt)
    {
        AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == name);
        bool added = state == null;
        if (added) state = machine.AddState(name);
        state.motion = clip; state.speed = speed; state.writeDefaultValues = true;
        if (added)
        {
            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = exitAt; exit.hasFixedDuration = true; exit.duration = 0.1f;
        }
        EditorUtility.SetDirty(state);
    }
    private static Material CopyMaterial(string sourceName, string targetName, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Materials + targetName + ".mat");
        if (material != null) return material;
        Material original = AssetDatabase.LoadAssetAtPath<Material>(Source + "VFX/Materials/" + sourceName + ".mat");
        Shader shader = Shader.Find(shaderName);
        if (original == null || shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Missing valid Ice material/shader: " + sourceName);
        material = new Material(original) { name = targetName, shader = shader };
        material.renderQueue = shaderName.Contains("Distortion") ? 2990 : 3000;
        AssetDatabase.CreateAsset(material, Materials + targetName + ".mat");
        return material;
    }
}
