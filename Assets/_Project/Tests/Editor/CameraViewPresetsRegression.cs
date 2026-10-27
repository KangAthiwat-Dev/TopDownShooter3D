#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CameraViewPresetsRegression
{
    private const string Pending="TopDownShooter3D.ViewPresetTest";
    static CameraViewPresetsRegression()=>EditorApplication.playModeStateChanged+=Changed;
    [MenuItem("Tools/Camera/View Presets/Run Default and Switching Regression")]
    private static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Pending+".HadPreference",PlayerPrefs.HasKey(CameraViewSettings.PreferenceKey));
        SessionState.SetInt(Pending+".Preference",PlayerPrefs.GetInt(CameraViewSettings.PreferenceKey,0));
        SessionState.SetBool(Pending,true);PlayerPrefs.DeleteKey(CameraViewSettings.PreferenceKey);PlayerPrefs.Save();
        EditorApplication.isPlaying=true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Temporary camera preset regression").AddComponent<CameraViewPresetsRegressionDriver>();
        if(state!=PlayModeStateChange.ExitingPlayMode)return;
        if(SessionState.GetBool(Pending+".HadPreference",false))PlayerPrefs.SetInt(CameraViewSettings.PreferenceKey,SessionState.GetInt(Pending+".Preference",0));
        else PlayerPrefs.DeleteKey(CameraViewSettings.PreferenceKey);
        PlayerPrefs.Save();SessionState.EraseBool(Pending);
    }
}
#endif
