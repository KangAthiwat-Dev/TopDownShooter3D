#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class VillageFarmRegression
{
    private const string Pending="VillageFarm.Regression";
    static VillageFarmRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/World/Yellow Roof Village/Run Player and Villager Test")]
    private static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))
        {SessionState.EraseBool(Pending);new GameObject("Temporary village regression").AddComponent<VillageFarmRegressionDriver>();}
    }
}
#endif
