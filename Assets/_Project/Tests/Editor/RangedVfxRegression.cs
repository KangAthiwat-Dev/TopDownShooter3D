#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RangedVfxRegression
{
    private const string Pending = "TopDownShooter3D.RangedVfxRegression";
    static RangedVfxRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/Combat/Run Tracer and Spiky VFX Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindAnyObjectByType<PlayerRangedFeedback>() == null) { Debug.LogError("Install Thin Tracer and Spiky VFX first"); return; }
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Tracer VFX Regression (Temporary)").AddComponent<RangedVfxRegressionDriver>();
    }
}
#endif
