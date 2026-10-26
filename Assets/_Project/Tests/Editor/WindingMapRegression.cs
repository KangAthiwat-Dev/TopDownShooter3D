#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WindingMapRegression
{
    private const string Pending="TopDownShooter3D.WindingMapRegression";
    static WindingMapRegression()=>EditorApplication.playModeStateChanged+=OnState;
    [MenuItem("Tools/World/Full Map/Run Full Map Playtest")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void OnState(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;SessionState.EraseBool(Pending);new GameObject("Full map regression (temporary)").AddComponent<WindingMapRegressionDriver>();}
}
#endif
