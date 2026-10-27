#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyBalanceDeathRegression
{
    private const string Pending="EnemyBalanceDeath.Regression";
    static EnemyBalanceDeathRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Combat Balance/Run Balance Death and Restart Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary enemy balance and death regression").AddComponent<EnemyBalanceDeathRegressionDriver>();
        if(state==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
