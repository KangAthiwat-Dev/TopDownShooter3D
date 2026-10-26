#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyAwarenessRegression
{
    private const string Pending = "TopDownShooter3D.EnemyAwarenessRegression";
    static EnemyAwarenessRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/Enemies/Run Awareness Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Enemy awareness regression (temporary)").AddComponent<EnemyAwarenessRegressionDriver>();
    }
}
#endif
