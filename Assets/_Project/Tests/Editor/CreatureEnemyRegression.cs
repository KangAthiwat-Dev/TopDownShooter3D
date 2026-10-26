#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CreatureEnemyRegression
{
    private const string Pending = "TopDownShooter3D.CreatureRegression";
    static CreatureEnemyRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/Enemies/Run Spider and Bear Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Creature regression (temporary)").AddComponent<CreatureEnemyRegressionDriver>();
    }
}
#endif
