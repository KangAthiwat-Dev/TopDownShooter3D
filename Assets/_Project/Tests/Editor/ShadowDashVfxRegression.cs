#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ShadowDashVfxRegression
{
    private const string Pending = "TopDownShooter3D.ShadowDashVfxRegression";
    static ShadowDashVfxRegression() => EditorApplication.playModeStateChanged += OnStateChanged;

    [MenuItem("Tools/VFX/Run Shadow Trail Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindAnyObjectByType<ShadowDashTrailVfx>() == null)
        {
            Debug.LogError("Install the Shadow Trails prefab in the gameplay scene first.");
            return;
        }
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Shadow Trail Regression (Temporary)").AddComponent<ShadowDashVfxRegressionDriver>();
    }
}
#endif
