#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PotionShopRegression
{
    private const string Pending = "PotionShop.Regression";
    static PotionShopRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/NPC/Potion Shop/Run Purchase Use and Save Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Temporary potion shop regression").AddComponent<PotionShopRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
