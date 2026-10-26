#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ForestZoneRegression
{
    private const string Pending = "TopDownShooter3D.ForestZoneRegression";
    static ForestZoneRegression() => EditorApplication.playModeStateChanged += OnState;
    [MenuItem("Tools/World/Run Forest Playtest")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || GameObject.Find(ForestZoneSetup.RootName) == null) return;
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Forest playtest (temporary)").AddComponent<ForestZoneRegressionDriver>();
    }
}
#endif
