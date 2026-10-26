#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemyAwarenessSetup
{
    private const string ControllerPath = "Assets/_Project/Animations/Enemies/EnemyAnimator.controller";
    private const string UalPath = "Assets/_Project/Art/ThirdParty/Quaternius/UAL1/UAL1_Standard.fbx";

    [MenuItem("Tools/Enemies/Install Awareness Locomotion")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory("Logs/SceneBackups");
        Directory.CreateDirectory("Logs/EnemyAI_Backup");
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            if (!EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/" + scene.name + "_BeforeEnemyAwareness_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true))
                throw new IOException("Cannot back up the current scene.");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var locomotion = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.name == "Locomotion");
        var blend = locomotion.motion as BlendTree;
        if (blend == null) throw new InvalidOperationException("Enemy Locomotion BlendTree missing.");
        var clips = AssetDatabase.LoadAllAssetsAtPath(UalPath).OfType<AnimationClip>().ToArray();
        File.WriteAllText("Logs/EnemyAnimationClipAudit.txt", string.Join("\n", clips.Select(c =>
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string guid, out long id);
            return $"{c.name}: human={c.isHumanMotion}, loop={c.isLooping}, id={id}";
        })));
        // FBXs also expose __preview__ clips with the same suffix. Bind actual takes.
        AnimationClip walk = clips.Single(c => c.name == "Armature|Walk_Loop");
        AnimationClip run = clips.Single(c => c.name == "Armature|Sprint_Loop");
        if (!walk.isHumanMotion || !run.isHumanMotion || !walk.isLooping || !run.isLooping)
            throw new InvalidOperationException("Expected looping Humanoid walk/run clips.");
        if (!File.Exists("Logs/EnemyAI_Backup/EnemyAnimator.controller.preinstall"))
            File.Copy(ControllerPath, "Logs/EnemyAI_Backup/EnemyAnimator.controller.preinstall");
        Undo.RecordObject(blend, "Add enemy walk/run blending");
        Motion idle = blend.children[0].motion;
        blend.useAutomaticThresholds = false;
        blend.blendType = BlendTreeType.Simple1D;
        blend.blendParameter = "Speed";
        blend.children = new[]
        {
            new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f, directBlendParameter = "Speed" },
            new ChildMotion { motion = walk, threshold = 0.5f, timeScale = 1f, directBlendParameter = "Speed" },
            new ChildMotion { motion = run, threshold = 1f, timeScale = 1f, directBlendParameter = "Speed" }
        };
        EditorUtility.SetDirty(blend);
        AssetDatabase.SaveAssets();
        Inspect();
    }

    [MenuItem("Tools/Enemies/Inspect Awareness Setup")]
    public static void Inspect()
    {
        var report = new StringBuilder("Enemy awareness configuration — " + DateTime.Now.ToString("s") + "\n");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var blend = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.name == "Locomotion").motion as BlendTree;
        foreach (ChildMotion child in blend.children)
            report.AppendLine($"Blend {child.threshold}: {child.motion.name} ({AssetDatabase.GetAssetPath(child.motion)})");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var ai = prefab.GetComponent<EnemyAI>(); if (ai == null) continue;
            var settings = new SerializedObject(ai);
            report.AppendLine($"{prefab.name}: walk={settings.FindProperty("patrolSpeed").floatValue}, original chase={prefab.GetComponent<NavMeshAgent>().speed}, FOV={settings.FindProperty("fieldOfView").floatValue}, sight={settings.FindProperty("detectionRange").floatValue}, search={settings.FindProperty("searchDuration").floatValue}s");
            foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
                report.AppendLine($"  {animator.name}: avatar={animator.avatar?.name}, humanoid={animator.avatar != null && animator.avatar.isHuman}, controller={animator.runtimeAnimatorController?.name}");
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/EnemyAwarenessSetup.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}

[CustomEditor(typeof(EnemyAI))]
[CanEditMultipleObjects]
public sealed class EnemyAwarenessInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Sight uses the enemy root's forward direction. Walls/tree trunks need a solid Collider to hide the player. Triggers and other enemies are ignored. Chase Speed = 0 retains the NavMeshAgent's original speed.", MessageType.Info);
        if (!Application.isPlaying || targets.Length != 1) return;
        var ai = (EnemyAI)target;
        EditorGUILayout.LabelField("Awareness", ai.State.ToString());
        EditorGUILayout.LabelField("Can see player", ai.CanSeeTarget.ToString());
        EditorGUILayout.LabelField("Remembers player", ai.HasTargetMemory.ToString());
        EditorGUILayout.LabelField("Time without sight", ai.TimeWithoutSight.ToString("F1") + " s");
        if (ai.HasTargetMemory)
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Vector3Field("Last seen (not live target)", ai.LastSeenPosition);
        Repaint();
    }
}
#endif
