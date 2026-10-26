#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SummoningCircleRegression
{
    private const string Pending = "TopDownShooter3D.SummoningCircleRegression";
    static SummoningCircleRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/World/Summoning/Run Magic Circle Playtest")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Object.FindAnyObjectByType<SummoningCircleVfx>() == null) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Summoning circle playtest (temporary)").AddComponent<SummoningCircleRegressionDriver>();
    }
}
#endif
