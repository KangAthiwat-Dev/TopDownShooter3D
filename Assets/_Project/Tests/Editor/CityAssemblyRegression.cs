#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CityAssemblyRegression
{
    private const string Pending = "CityAssembly.Regression";
    static CityAssemblyRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/World/City Assembly/Run Grounded City Player NPC and Music Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary movable city regression").AddComponent<CityAssemblyRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
