#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class StoryPresentationRegression
{
    private const string Pending = "Story.PresentationRegression";
    static StoryPresentationRegression() => EditorApplication.playModeStateChanged += Changed;

    [MenuItem("Tools/Story/Run NPC and Minimap Play Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Object.FindAnyObjectByType<KingdomMinimapUI>() == null) throw new System.InvalidOperationException("Install the minimap first.");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Temporary story presentation test").AddComponent<StoryPresentationRegressionDriver>();
        if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseBool(Pending);
    }
}
#endif
