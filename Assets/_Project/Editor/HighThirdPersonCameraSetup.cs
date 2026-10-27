#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HighThirdPersonCameraSetup
{
    private const string RecordPath = "Assets/_Project/World/HighThirdPersonCameraRecord.json";
    [Serializable] private sealed class Record
    {
        public string backup, previousFollowSettings, previousMovementSettings;
        public Vector3 previousCameraPosition;
        public Quaternion previousCameraRotation, previousRigRotation;
        public float previousFov, previousNearClip;
        public int unchangedSceneComponents;
    }
    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/WorldPrototype.unity")
            throw new InvalidOperationException("Open WorldPrototype in Edit Mode.");
    }
    [MenuItem("Tools/Camera/Install High Third Person Camera")]
    public static void Install()
    {
        RequireScene();
        if (File.Exists(RecordPath)) throw new InvalidOperationException("Already installed; preserve the user's camera adjustments.");
        var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(); var camera = follow.FollowCamera;
        var movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(); var fade = follow.GetComponent<CameraOcclusionFade>();
        var allowed = new Component[] { follow, follow.transform, camera, camera.transform, movement, fade };
        var components = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true))
            .Where(c => c != null && !allowed.Contains(c)).ToArray();
        var before = components.ToDictionary(c => c.GetEntityId().ToString(), c => EditorJsonUtility.ToJson(c));
        Directory.CreateDirectory("Logs/SceneBackups");
        var record = new Record
        {
            backup = "Logs/SceneBackups/WorldPrototype_BeforeHighThirdPerson_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity",
            previousFollowSettings = EditorJsonUtility.ToJson(follow), previousMovementSettings = EditorJsonUtility.ToJson(movement),
            previousCameraPosition = camera.transform.localPosition, previousCameraRotation = camera.transform.localRotation,
            previousRigRotation = follow.transform.rotation, previousFov = camera.fieldOfView, previousNearClip = camera.nearClipPlane
        };
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), record.backup, true)) throw new IOException("Cannot back up the current live scene.");
        Undo.RecordObjects(allowed.Where(c => c != null).Cast<UnityEngine.Object>().ToArray(), "High third-person camera and view-relative input");
        if (fade != null) { fade.enabled = false; EditorUtility.SetDirty(fade); }
        var so = new SerializedObject(follow);
        so.FindProperty("followMovementHeading").boolValue = true;
        so.FindProperty("alignBehindTargetOnStart").boolValue = true;
        so.FindProperty("headingSmoothTime").floatValue = .45f;
        so.FindProperty("maxHeadingSpeed").floatValue = 120f;
        so.FindProperty("minimumHeadingSpeed").floatValue = .8f;
        so.FindProperty("headingConfirmTime").floatValue = .1f;
        so.FindProperty("headingDeadZone").floatValue = 3f;
        so.FindProperty("targetOffset").vector3Value = new Vector3(0, 1.35f, 0);
        so.FindProperty("smoothTime").floatValue = .1f;
        so.ApplyModifiedProperties();
        var input = new SerializedObject(movement);
        input.FindProperty("cameraRelativeMovement").boolValue = true;
        input.FindProperty("movementReference").objectReferenceValue = camera.transform;
        input.ApplyModifiedProperties();
        follow.transform.rotation = Quaternion.identity;
        camera.transform.localPosition = new Vector3(0, 6, -8);
        camera.transform.localRotation = Quaternion.LookRotation(-camera.transform.localPosition, Vector3.up);
        camera.fieldOfView = 55f; camera.nearClipPlane = .1f;
        var boom = follow.GetComponent<CameraBoomCollision>(); if (boom == null) boom = Undo.AddComponent<CameraBoomCollision>(follow.gameObject);
        var boomSettings = new SerializedObject(boom); boomSettings.FindProperty("viewCamera").objectReferenceValue = camera;
        boomSettings.ApplyModifiedProperties();
        foreach (var c in allowed) if (c != null) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(boom);
        var changed = components.Where(c => c == null || EditorJsonUtility.ToJson(c) != before[c.GetEntityId().ToString()]).ToArray();
        if (changed.Length > 0) throw new InvalidOperationException("Unexpected changes outside camera/control scope: " + string.Join(",", changed.Select(c => c != null ? c.name : "destroyed")));
        record.unchangedSceneComponents = components.Length;
        File.WriteAllText(RecordPath, JsonUtility.ToJson(record, true)); AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Validate(); Selection.activeGameObject = follow.gameObject;
    }
    [MenuItem("Tools/Camera/Validate High Third Person Camera")]
    public static void Validate()
    {
        RequireScene(); var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>(); var camera = follow.FollowCamera;
        var movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(); var fade = follow.GetComponent<CameraOcclusionFade>();
        var preset = follow.GetComponent<CameraViewSettings>()?.HighThirdPersonPreset;
        var report = new StringBuilder("High third-person camera " + DateTime.Now.ToString("s") + "\n"); bool passed = true;
        void Check(bool value, string label) { passed &= value; report.AppendLine((value ? "PASS " : "FAIL ") + label); }
        Check(preset != null ? preset.followMovementHeading && preset.alignBehindPlayerOnStart : follow.FollowsMovementHeading && new SerializedObject(follow).FindProperty("alignBehindTargetOnStart").boolValue, "Third-person follows walking velocity, initially behind the player");
        Check(preset != null ? preset.cameraRelativeMovement : movement.UsesCameraRelativeMovement && movement.MovementReference == camera.transform, "Third-person uses view-relative controls");
        Check(Vector3.Distance(preset != null ? preset.localPosition : camera.transform.localPosition, new Vector3(0, 6, -8)) < .001f && Quaternion.Angle(preset != null ? Quaternion.Euler(preset.localEulerAngles) : camera.transform.localRotation, Quaternion.LookRotation(new Vector3(0, -6, 8))) < .001f, "High third-person framing: 6 m above pivot, 8 m behind, 36.87 degree pitch");
        Check(Mathf.Abs((preset != null ? preset.fieldOfView : camera.fieldOfView) - 55) < .001f && Mathf.Abs((preset != null ? preset.nearClipPlane : camera.nearClipPlane) - .1f) < .001f, "55 degree field of view and near clip configured");
        Check(fade == null || !fade.enabled, "Scenery transparency disabled");
        Check(follow.GetComponent<CameraBoomCollision>() != null && (preset != null ? preset.avoidSolidObstacles : follow.GetComponent<CameraBoomCollision>().enabled), "Solid-obstacle camera boom retained for third-person");
        Check(follow.GetComponent<CameraDashEffect>() != null && movement.GetComponent<PlayerCombatFacing>() != null, "Existing aim-facing and dash effects retained");
        var record = JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath));
        Check(record.unchangedSceneComponents > 0 && File.Exists(record.backup), "Fresh backup exists; " + record.unchangedSceneComponents + " other scene components preserved");
        Directory.CreateDirectory("Logs"); report.AppendLine(passed ? "ALL CHECKS PASSED" : "CHECKS FAILED"); File.WriteAllText("Logs/HighThirdPersonValidation.txt", report.ToString());
        if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
    }
}
#endif
