#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SummoningPlatformRegression
{
    private const string Pending = "TopDownShooter3D.SummoningRegression";
    static SummoningPlatformRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/World/Summoning/Run Platform Playtest")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Object.FindAnyObjectByType<PlayerSceneSpawnPoint>() == null) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Summoning playtest (temporary)").AddComponent<SummoningPlatformRegressionDriver>();
    }
}
#endif
