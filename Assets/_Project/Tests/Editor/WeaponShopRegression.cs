#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WeaponShopRegression
{
    private const string Pending = "WeaponShop.Regression";
    static WeaponShopRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/NPC/Weapon Merchant/Run Shop Purchase Rotation and Save Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Temporary weapon shop regression").AddComponent<WeaponShopRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
