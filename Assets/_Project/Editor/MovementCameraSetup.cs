#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MovementCameraSetup
{
    private const string Record = "Assets/_Project/World/MovementCameraRecord.json";
    [Serializable] private sealed class SettingsRecord
    {
        public string backup, originalSettings, playerMovementHash;
        public Vector3 cameraLocalPosition;
        public Quaternion cameraLocalRotation, rigRotation;
        public float fieldOfView;
        public int unchangedSceneComponents;
    }
    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    private static CameraFollow CameraRig() => UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    [MenuItem("Tools/Camera/Legacy Movement Orbit/Enable Movement Directed Top Down")]
    public static void Install()
    {
        RequireScene(); if (File.Exists(Record)) throw new InvalidOperationException("Camera already configured; preserve Inspector adjustments.");
        var rig = CameraRig(); if (rig == null) throw new InvalidOperationException("CameraFollow not found.");
        var camera = rig.GetComponentInChildren<Camera>();
        var components = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null && c != rig).ToArray();
        var before = components.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        Directory.CreateDirectory("Logs/SceneBackups");
        var record = new SettingsRecord
        {
            backup = "Logs/SceneBackups/WorldPrototype_BeforeMovementCamera_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity",
            originalSettings = EditorJsonUtility.ToJson(rig),
            cameraLocalPosition = camera.transform.localPosition, cameraLocalRotation = camera.transform.localRotation,
            fieldOfView = camera.fieldOfView, rigRotation = rig.transform.rotation,
            playerMovementHash = Hash("Assets/_Project/Scripts/Player/PlayerMovement.cs")
        };
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), record.backup, true)) throw new IOException("Cannot back up current scene.");
        Undo.RecordObject(rig, "Enable movement-directed top-down camera");
        var settings = new SerializedObject(rig);
        settings.FindProperty("followMovementHeading").boolValue = true;
        settings.FindProperty("headingSmoothTime").floatValue = .4f;
        settings.FindProperty("maxHeadingSpeed").floatValue = 110f;
        settings.FindProperty("minimumHeadingSpeed").floatValue = .8f;
        settings.FindProperty("headingConfirmTime").floatValue = .12f;
        settings.FindProperty("headingDeadZone").floatValue = 7f;
        settings.ApplyModifiedProperties(); EditorUtility.SetDirty(rig);
        var differences = components.Where(c => c == null || EditorJsonUtility.ToJson(c) != before[c.GetEntityId().ToString()]).ToArray();
        if (differences.Length != 0) throw new InvalidOperationException("Unexpected scene changes outside CameraFollow.");
        record.unchangedSceneComponents = components.Length;
        File.WriteAllText(Record, JsonUtility.ToJson(record, true)); AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Validate(); Selection.activeGameObject = rig.gameObject;
    }
    [MenuItem("Tools/Camera/Legacy Movement Orbit/Validate Movement Directed Top Down")]
    public static void Validate()
    {
        RequireScene(); var rig = CameraRig(); var camera = rig.GetComponentInChildren<Camera>();
        var record = JsonUtility.FromJson<SettingsRecord>(File.ReadAllText(Record));
        var log = new StringBuilder("Movement camera validation " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool value, string label) { passed &= value; log.AppendLine((value ? "PASS " : "FAIL ") + label); }
        Check(rig.FollowsMovementHeading, "Movement-directed orbit enabled only on the current WorldPrototype rig");
        Check(camera.transform.localPosition == record.cameraLocalPosition && Quaternion.Angle(camera.transform.localRotation, record.cameraLocalRotation) < .001f, "Authored camera height, distance and pitch unchanged");
        Check(Mathf.Abs(camera.fieldOfView - record.fieldOfView) < .001f && Quaternion.Angle(rig.transform.rotation, record.rigRotation) < .001f, "Field of view and initial rig orientation unchanged");
        Check(Hash("Assets/_Project/Scripts/Player/PlayerMovement.cs") == record.playerMovementHash, "Player movement, world-axis input and combat code untouched");
        Check(record.unchangedSceneComponents > 0, "All " + record.unchangedSceneComponents + " other scene components preserved during installation");
        var so = new SerializedObject(rig);
        Check(so.FindProperty("enableRunningBob").boolValue && rig.GetComponent<CameraDashEffect>() != null, "Existing sprint bob and dash FOV component retained");
        Check(File.Exists(record.backup), "Pre-install live scene backup exists");
        log.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MovementCameraValidation.txt", log.ToString());
        if (passed) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }
}
#endif
