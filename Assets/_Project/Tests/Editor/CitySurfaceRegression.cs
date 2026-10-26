#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CitySurfaceRegression
{
    private const string Pending="TopDownShooter3D.CitySurfaceRegression";
    static CitySurfaceRegression()=>EditorApplication.playModeStateChanged+=OnState;
    [MenuItem("Tools/World/City Surfaces/Run City Surface Playtest")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void OnState(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;SessionState.EraseBool(Pending);new GameObject("City surface regression (temporary)").AddComponent<CitySurfaceRegressionDriver>();}
}
#endif
