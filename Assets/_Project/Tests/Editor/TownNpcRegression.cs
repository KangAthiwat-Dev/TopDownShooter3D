#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TownNpcRegression
{
    private const string Pending = "TopDownShooter3D.TownNpcRegression";
    static TownNpcRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/NPC/Townsfolk/Run Population and Enemy Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Temporary town NPC regression").AddComponent<TownNpcRegressionDriver>();
    }
}
#endif
