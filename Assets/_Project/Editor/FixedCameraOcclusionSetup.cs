#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixedCameraOcclusionSetup
{
    private const string Root = "Assets/_Project/World/CameraOcclusion";
    private const string RecordPath = Root + "/FixedCameraRecord.json";
    [Serializable] private sealed class Record
    {
        public string backup, playerMovementHash;
        public Vector3 cameraLocalPosition;
        public Quaternion cameraLocalRotation, rigRotation;
        public float fieldOfView;
        public int unchangedSceneComponents, retainedVariantCount;
    }
    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    [MenuItem("Tools/Camera/Inspect Scenery Materials")]
    public static void Inspect()
    {
        RequireScene(); var log = new StringBuilder();
        foreach (var group in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).GroupBy(r => r.gameObject.layer))
            log.AppendLine("Layer " + group.Key + " " + LayerMask.LayerToName(group.Key) + ": " + group.Count() + " mesh renderers");
        foreach (var group in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().GroupBy(m => m.shader.name))
            log.AppendLine(group.Key + ": " + group.Count() + " materials");
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.transform.position.x > 60 && r.transform.position.z < -60).Take(35))
            log.AppendLine(r.name + " layer=" + r.gameObject.layer + " bounds=" + r.bounds);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CameraOcclusionInspection.txt", log.ToString()); Debug.Log("Scenery material inspection written to Logs/CameraOcclusionInspection.txt");
    }
    [MenuItem("Tools/Camera/Legacy Fixed Camera Fade/Restore Fixed Camera and Install Scenery Fade")]
    public static void Install()
    {
        RequireScene(); if (File.Exists(RecordPath)) throw new InvalidOperationException("Already installed; preserve Inspector adjustments.");
        var rig = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(); var camera = rig.FollowCamera;
        var allowed = new Component[] { rig, rig.transform, camera, camera.transform };
        var components = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null && !allowed.Contains(c)).ToArray();
        var before = components.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        Directory.CreateDirectory("Logs/SceneBackups");
        var original = JsonUtility.FromJson<Record>(File.ReadAllText("Assets/_Project/World/MovementCameraRecord.json"));
        var record = new Record
        {
            backup = "Logs/SceneBackups/WorldPrototype_BeforeCameraOcclusion_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity",
            playerMovementHash = Hash("Assets/_Project/Scripts/Player/PlayerMovement.cs"),
            cameraLocalPosition = original.cameraLocalPosition, cameraLocalRotation = original.cameraLocalRotation,
            rigRotation = original.rigRotation, fieldOfView = original.fieldOfView
        };
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), record.backup, true)) throw new IOException("Cannot back up live scene.");
        Undo.RecordObjects(allowed, "Restore original fixed camera");
        var follow = new SerializedObject(rig); follow.FindProperty("followMovementHeading").boolValue = false; follow.ApplyModifiedProperties();
        rig.transform.rotation = original.rigRotation; camera.transform.localPosition = original.cameraLocalPosition;
        camera.transform.localRotation = original.cameraLocalRotation; camera.fieldOfView = original.fieldOfView;
        foreach (var item in allowed) EditorUtility.SetDirty(item);

        Directory.CreateDirectory(Root + "/ShaderVariants"); AssetDatabase.Refresh();
        var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CameraFade_Fallback" };
        CameraOcclusionFade.ConfigureTransparent(fallback); AssetDatabase.CreateAsset(fallback, Root + "/CameraFade_Fallback.mat");
        // An asset per unique shader/keyword combination prevents build-time stripping of the
        // transparent versions while runtime copies keep every source's textures and settings.
        var variants = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .SelectMany(r => r.sharedMaterials).Where(CameraOcclusionFade.SupportsTransparency)
            .GroupBy(m => m.shader.name + ":" + string.Join(",", m.shaderKeywords.OrderBy(k => k))).Select(g => g.First()).ToArray();
        var retained = new Material[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            retained[i] = new Material(variants[i]) { name = "CameraFade_Variant_" + i.ToString("D2") };
            CameraOcclusionFade.ConfigureTransparent(retained[i]); AssetDatabase.CreateAsset(retained[i], Root + "/ShaderVariants/" + retained[i].name + ".mat");
        }
        var fade = Undo.AddComponent<CameraOcclusionFade>(rig.gameObject); var settings = new SerializedObject(fade);
        settings.FindProperty("viewCamera").objectReferenceValue = camera;
        settings.FindProperty("playerTarget").objectReferenceValue = follow.FindProperty("target").objectReferenceValue;
        settings.FindProperty("fallbackTransparentMaterial").objectReferenceValue = fallback;
        var array = settings.FindProperty("retainedTransparentVariants"); array.arraySize = retained.Length;
        for (int i = 0; i < retained.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = retained[i];
        settings.ApplyModifiedProperties(); EditorUtility.SetDirty(fade);
        var changed = components.Where(c => c == null || EditorJsonUtility.ToJson(c) != before[c.GetEntityId().ToString()]).ToArray();
        if (changed.Length != 0) throw new InvalidOperationException("Unexpected changes outside camera scope: " + string.Join(",", changed.Select(c => c != null ? c.name : "destroyed")));
        record.unchangedSceneComponents = components.Length; record.retainedVariantCount = retained.Length;
        File.WriteAllText(RecordPath, JsonUtility.ToJson(record, true)); AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Validate(); Selection.activeGameObject = rig.gameObject;
    }
    [MenuItem("Tools/Camera/Legacy Fixed Camera Fade/Validate Fixed Camera and Scenery Fade")]
    public static void Validate()
    {
        RequireScene(); var rig = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(); var camera = rig.FollowCamera;
        var fade = rig.GetComponent<CameraOcclusionFade>(); var record = JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath));
        var report = new StringBuilder("Fixed camera / scenery fade " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool condition, string label) { passed &= condition; report.AppendLine((condition ? "PASS " : "FAIL ") + label); }
        Check(!rig.FollowsMovementHeading, "Automatic camera orbit disabled");
        Check(camera.transform.localPosition == record.cameraLocalPosition && Quaternion.Angle(camera.transform.localRotation, record.cameraLocalRotation) < .001f && Quaternion.Angle(rig.transform.rotation, record.rigRotation) < .001f, "Original fixed top-down camera pose restored");
        Check(Mathf.Abs(camera.fieldOfView - record.fieldOfView) < .001f, "Original 45 degree field of view retained");
        Check(fade != null && fade.enabled && new SerializedObject(fade).FindProperty("playerTarget").objectReferenceValue != null, "Scenery fade is wired to the actual camera and player");
        Check(record.retainedVariantCount > 0 && new SerializedObject(fade).FindProperty("fallbackTransparentMaterial").objectReferenceValue != null, "Transparent shader variants retained for player builds");
        Check(Hash("Assets/_Project/Scripts/Player/PlayerMovement.cs") == record.playerMovementHash, "Player movement / aiming code untouched");
        Check(rig.GetComponent<CameraDashEffect>() != null && new SerializedObject(rig).FindProperty("enableRunningBob").boolValue, "Existing subtle sprint bob and dash FOV retained");
        Check(record.unchangedSceneComponents > 0 && File.Exists(record.backup), "Live backup exists; " + record.unchangedSceneComponents + " non-camera scene components preserved");
        report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/FixedCameraValidation.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
    }
}
#endif
