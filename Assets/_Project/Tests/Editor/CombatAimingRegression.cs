#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Opt-in only. All fixtures and input devices exist in Play Mode, never in the
// saved scene. The launcher survives the domain reload when entering Play Mode.
[InitializeOnLoad]
public static class CombatAimingRegression
{
    private const string PendingKey = "TopDownShooter3D.CombatAimingRegression";

    static CombatAimingRegression()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Combat/Run Aiming Regression")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindAnyObjectByType<PlayerAttackController>() == null)
        {
            Debug.LogError("Open WorldPrototype or PrototypeArena before running the aiming regression.");
            return;
        }
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        new GameObject("Combat Aiming Regression (Play Mode Only)").AddComponent<CombatAimingRegressionDriver>();
    }
}
#endif
