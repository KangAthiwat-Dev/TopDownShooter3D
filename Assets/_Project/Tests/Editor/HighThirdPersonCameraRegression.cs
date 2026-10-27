#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HighThirdPersonCameraRegression
{
    private const string Pending = "TopDownShooter3D.HighThirdPersonCameraTest";
    static HighThirdPersonCameraRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Camera/Run High Third Person Camera Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (movement == null || (!movement.UsesCameraRelativeMovement && UnityEngine.Object.FindFirstObjectByType<CameraViewSettings>() == null)) { Debug.Log("Install the high third-person preset first."); return; }
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Temporary high third person camera regression").AddComponent<HighThirdPersonCameraRegressionDriver>();
    }
}
#endif
