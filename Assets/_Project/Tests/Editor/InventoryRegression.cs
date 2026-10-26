#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class InventoryRegression
{
    private const string Pending = "TopDownShooter3D.InventoryRegression";
    static InventoryRegression() => EditorApplication.playModeStateChanged += OnStateChanged;
    [MenuItem("Tools/Inventory/Run Inventory Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindAnyObjectByType<InventoryPanelUI>() == null)
        {
            Debug.LogError("Install Inventory in the gameplay scene first."); return;
        }
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    private static void OnStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        new GameObject("Inventory Regression (Temporary)").AddComponent<InventoryRegressionDriver>();
    }
}
#endif
