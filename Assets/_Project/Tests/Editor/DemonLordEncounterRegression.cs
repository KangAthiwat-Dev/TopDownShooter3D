#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DemonLordEncounterRegression
{
    private const string Pending = "DemonLord.EncounterRegression";
    static DemonLordEncounterRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Enemies/Demon Lord/Run DemonLord and Dragon Combat Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary DemonLord and Dragon encounter regression").AddComponent<DemonLordEncounterRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
