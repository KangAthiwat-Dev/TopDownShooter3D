#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GuildTradeRegression
{
    private const string Pending="GuildTrade.Regression";
    static GuildTradeRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/NPC/Guild Trade/Run E Mouse Sale Gold and Save Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary guild selling regression").AddComponent<GuildTradeRegressionDriver>();
        if(state==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
