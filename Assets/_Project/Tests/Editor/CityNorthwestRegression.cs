#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CityNorthwestRegression
{
    private const string Pending="TopDownShooter3D.NorthwestQuarterRegression";
    static CityNorthwestRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/Northwest Quarter/Run Actual Player Alley Test")]
    private static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.EraseBool(Pending);new GameObject("Temporary northwest quarter regression").AddComponent<CityNorthwestRegressionDriver>();
    }
}
#endif
