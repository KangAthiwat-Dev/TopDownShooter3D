#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CityExpansionRegression
{
    private const string Pending="TopDownShooter3D.CityExpansionPlaytest";
    static CityExpansionRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/City Expansion/Run Actual Player Street and Shop Test")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;SessionState.EraseBool(Pending);new GameObject("Temporary city expansion playtest").AddComponent<CityExpansionRegressionDriver>();}
}
#endif
