#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MovementCameraRegression
{
    private const string Pending = "TopDownShooter3D.MovementCameraTest";
    static MovementCameraRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Camera/Legacy Movement Orbit/Run Movement Camera Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var rig = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
        var movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (rig == null || !rig.FollowsMovementHeading || (movement != null && movement.UsesCameraRelativeMovement))
        {
            Debug.Log("This legacy regression requires world-axis input. Use the regression for the current camera configuration instead.");
            return;
        }
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Temporary movement camera regression").AddComponent<MovementCameraRegressionDriver>();
    }
}
#endif
