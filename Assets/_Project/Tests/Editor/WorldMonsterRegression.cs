#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WorldMonsterRegression
{
    private const string Pending="WorldMonster.Regression";
    static WorldMonsterRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Enemies/World Population/Run Population Camp and Combat Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary world population regression").AddComponent<WorldMonsterRegressionDriver>();
        if(state==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
