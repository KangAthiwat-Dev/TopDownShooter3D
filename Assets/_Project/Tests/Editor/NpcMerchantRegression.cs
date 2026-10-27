#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NpcMerchantRegression
{
    private const string Pending = "TopDownShooter3D.NpcMerchantTest";
    static NpcMerchantRegression() => EditorApplication.playModeStateChanged += Changed;
    [MenuItem("Tools/NPC/Weapon Merchant/Run Actual Player Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Temporary merchant regression").AddComponent<NpcMerchantRegressionDriver>();
    }
}
#endif
