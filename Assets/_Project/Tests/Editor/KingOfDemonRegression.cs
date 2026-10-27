#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KingOfDemonRegression
{
    private const string Pending = "KingOfDemon.EncounterRegression";
    static KingOfDemonRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/Enemies/King Of Demon/Run Boss Combat and HUD Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary boss encounter regression").AddComponent<KingOfDemonRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
