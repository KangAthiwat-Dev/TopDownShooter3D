#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MonsterLootRegression
{
    private const string Pending="MonsterLoot.Regression";
    static MonsterLootRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Enemies/Monster Loot/Run Death Magnet and Inventory Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary monster loot regression").AddComponent<MonsterLootRegressionDriver>();
        if(change==PlayModeStateChange.ExitingPlayMode)SessionState.EraseBool(Pending);
    }
}
#endif
