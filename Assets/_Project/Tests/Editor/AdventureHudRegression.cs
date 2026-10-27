#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AdventureHudRegression
{
    private const string Pending="TopDownShooter3D.AdventureHudTest";
    static AdventureHudRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/HUD and Audio/Run Music and HUD Regression")]
    private static void Run()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.EraseBool(Pending);new GameObject("Temporary adventure HUD regression").AddComponent<AdventureHudRegressionDriver>();
    }
}
#endif
