#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TownDiversityRegression
{
    private const string Pending = "TopDownShooter3D.WomenBuildersRegression";
    static TownDiversityRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/NPC/Women and Builders/Run Women and Builder Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.EraseBool(Pending); new GameObject("Temporary women and builders regression").AddComponent<TownDiversityRegressionDriver>();
    }
}
#endif
