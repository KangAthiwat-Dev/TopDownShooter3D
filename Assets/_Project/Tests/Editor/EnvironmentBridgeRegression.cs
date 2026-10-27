#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnvironmentBridgeRegression
{
    private const string Pending = "EnvironmentBridge.Regression";
    static EnvironmentBridgeRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/World/Environment Bridges/Run Player Bridge Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary Environment bridge regression").AddComponent<EnvironmentBridgeRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
