#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Read-only inspection of the supplied boss, using an isolated preview scene.</summary>
public static class KingOfDemonAssetReview
{
    private const string Folder = "Assets/Character/KingOfDemon";

    [MenuItem("Tools/Assets/King Of Demon/Inspect Model Animations and Effects")]
    public static void Inspect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before inspecting the boss asset.");

        var scene = EditorSceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;
        var report = new StringBuilder("KingOfDemon asset inspection\n");
        var preview = new PreviewRenderUtility();
        Texture2D image = null;
        int failures = 0;
        void Check(bool valid, string description)
        {
            report.AppendLine((valid ? "PASS " : "FAIL ") + description);
            if (!valid) failures++;
        }

        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/KingOfDemon.prefab");
            if (prefab == null) throw new InvalidOperationException("KingOfDemon prefab is missing.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(instance);
            foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) script.enabled = false;

            var animator = instance.GetComponentInChildren<Animator>();
            Check(animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                "valid Humanoid avatar");
            Check(instance.GetComponentsInChildren<Transform>(true).All(t =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing model scripts");

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Check(controller != null && controller.animationClips.Length == 8, "controller references all eight animation clips");
            if (controller != null)
            {
                report.AppendLine("Parameters: " + string.Join(", ", controller.parameters.Select(p => p.name + " (" + p.type + ")")));
                var locomotion = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.motion as BlendTree;
                Check(locomotion != null && locomotion.blendParameter == "Speed" && locomotion.children.Length == 3,
                    "Speed blend tree contains Idle / Walk / Run");
                if (locomotion != null)
                    report.AppendLine("Locomotion thresholds: " + string.Join(", ", locomotion.children.Select(c => c.motion.name + "=" + c.threshold)));
            }

            animator.runtimeAnimatorController = null;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var bones = instance.GetComponentsInChildren<Transform>(true).Where(t => t != instance.transform).ToArray();
            var receivers = instance.GetComponents<MonoBehaviour>().Where(m => m != null).ToArray();
            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder + "/Animations" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(c => c.name).ToArray();

            foreach (var clip in clips)
            {
                var graph = PlayableGraph.Create("Read-only boss clip preview");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    AnimationPlayableOutput.Create(graph, "Model", animator).SetSourcePlayable(playable);
                    graph.Play();
                    playable.SetTime(clip.length * .15);
                    graph.Evaluate(0);
                    var rotations = bones.Select(t => t.localRotation).ToArray();
                    playable.SetTime(clip.length * .65);
                    graph.Evaluate(0);
                    int moved = bones.Where((t, i) => Quaternion.Angle(rotations[i], t.localRotation) > .1f).Count();
                    bool finite = bones.All(t => float.IsFinite(t.position.x) && float.IsFinite(t.position.y) && float.IsFinite(t.position.z));
                    Check(clip.isHumanMotion && finite && moved > 0,
                        clip.name + ": " + clip.length.ToString("F3") + "s, loop=" + clip.isLooping + ", animated bones=" + moved);
                    foreach (var evt in AnimationUtility.GetAnimationEvents(clip))
                        Check(receivers.Any(m => m.GetType().GetMethod(evt.functionName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null),
                            clip.name + " event " + evt.functionName + " @ " + evt.time.ToString("F3") + "s has a receiver");
                }
                finally { graph.Destroy(); }
            }

            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            report.AppendLine("Skinned renderers=" + renderers.Length + ", vertices=" + renderers.Sum(r => r.sharedMesh.vertexCount) +
                ", triangles=" + renderers.Sum(r => Enumerable.Range(0, r.sharedMesh.subMeshCount).Sum(i => (long)r.sharedMesh.GetIndexCount(i) / 3)));
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Check(material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader),
                    material.name + " shader supported: " + (material.shader != null ? material.shader.name : "missing"));
            }

            var fx = instance.GetComponent<BossVFXController>();
            Check(fx != null, "BossVFXController is attached");
            if (fx != null)
                foreach (var field in typeof(BossVFXController).GetFields().Where(f => f.FieldType == typeof(GameObject)))
                {
                    var effect = field.GetValue(fx) as GameObject;
                    Check(effect != null, field.Name + " prefab is assigned");
                    if (effect == null) continue;
                    Check(effect.GetComponentsInChildren<Transform>(true).All(t =>
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), effect.name + " has no missing scripts");
                }

            report.AppendLine("Gameplay components present: " + string.Join(", ", instance.GetComponents<Component>().Select(c => c.GetType().Name)));
            report.AppendLine("AudioSources=" + instance.GetComponentsInChildren<AudioSource>(true).Length);

            // A neutral pose image only; no animation events or gameplay scripts run in this preview.
            var idle = clips.First(c => c.name.EndsWith("_Idle"));
            var idleGraph = PlayableGraph.Create("Boss portrait");
            try
            {
                var idlePlayable = AnimationClipPlayable.Create(idleGraph, idle);
                AnimationPlayableOutput.Create(idleGraph, "Portrait", animator).SetSourcePlayable(idlePlayable);
                idleGraph.Play();
                idleGraph.Evaluate(.15f);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                report.AppendLine("Prefab rendered dimensions=" + bounds.size.ToString("F3") + ", root scale=" + instance.transform.localScale);
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .12f, .15f);
                camera.fieldOfView = 35;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 200;
                camera.transform.position = bounds.center + new Vector3(.5f, .12f, 1).normalized * bounds.extents.magnitude * 3.7f;
                camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 135, 0);
                preview.lights[1].intensity = .7f;
                preview.lights[1].transform.rotation = Quaternion.Euler(20, 310, 0);
                preview.ambientColor = new Color(.4f, .4f, .4f);
                preview.BeginStaticPreview(new Rect(0, 0, 900, 1000));
                preview.Render(true);
                image = preview.EndStaticPreview();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/KingOfDemon_Preview.png", image.EncodeToPNG());
            }
            finally { idleGraph.Destroy(); }
        }
        finally
        {
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            preview.Cleanup();
            Check(scene.isDirty == wasDirty, "active scene dirty state preserved");
            report.AppendLine("Inspection failures=" + failures);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/KingOfDemonAssetReview.txt", report.ToString());
        }
        Debug.Log("KingOfDemon inspection complete; failures=" + failures + ". See Logs/KingOfDemonAssetReview.txt");
    }
}
#endif
