#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CastleRouteRegression
{
    private const string Pending="CastleRoute.Playtest";
    static CastleRouteRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/Castle Route/Run Actual Player Traversal Test")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {if(!SessionState.GetBool(Pending,false))return;if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary castle route traversal test").AddComponent<CastleRouteRegressionDriver>();if(state==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);}
}
#endif
