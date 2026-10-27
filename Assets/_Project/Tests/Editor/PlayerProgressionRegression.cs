#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerProgressionRegression
{
    private const string Pending = "PlayerProgression.Regression";
    static PlayerProgressionRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Progression/Run EXP Combat Save and HUD Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary progression regression").AddComponent<PlayerProgressionRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
