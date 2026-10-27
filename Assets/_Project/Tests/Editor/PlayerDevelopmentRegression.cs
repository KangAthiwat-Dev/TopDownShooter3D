#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerDevelopmentRegression
{
    private const string Pending="PlayerDevelopment.Regression";
    static PlayerDevelopmentRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Player Development/Run Upgrades Burst Grounding and Turns Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary player development test").AddComponent<PlayerDevelopmentRegressionDriver>();
        if(change==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
