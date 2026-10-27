#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FixedCameraOcclusionRegression
{
    private const string Pending = "TopDownShooter3D.FixedCameraOcclusionTest";
    static FixedCameraOcclusionRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Camera/Legacy Fixed Camera Fade/Run Fixed Camera and Scenery Fade Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var fade = UnityEngine.Object.FindFirstObjectByType<CameraOcclusionFade>();
        if (fade == null || !fade.enabled) { Debug.Log("The current scene no longer uses transparency. Run High Third Person Camera Regression instead."); return; }
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Temporary fixed camera occlusion regression").AddComponent<FixedCameraOcclusionRegressionDriver>();
    }
}
#endif
