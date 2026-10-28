#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NativeCreatureRegression
{
    private const string Pending="NativeCreatures.PlayTest";
    static NativeCreatureRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Story Creatures/Run Native Creatures and Rifle Play Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.EraseBool(Pending);new GameObject("Temporary native creature regression").AddComponent<NativeCreatureRegressionDriver>();
    }
}
#endif
