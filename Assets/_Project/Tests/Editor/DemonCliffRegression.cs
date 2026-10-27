#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class DemonCliffRegression
{
    private const string Pending="DemonCliffs.Playtest";
    static DemonCliffRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/Demon Cliffs/Run Actual Player and Bear Playtest")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary demon cliff regression").AddComponent<DemonCliffRegressionDriver>();
        if(state==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
