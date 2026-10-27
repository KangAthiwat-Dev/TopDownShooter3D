#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CityAssemblyNavigationGuard
{
    static CityAssemblyNavigationGuard() => EditorApplication.playModeStateChanged += Changed;
    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        var city = Object.FindAnyObjectByType<CityAssemblyRoot>();
        if (city != null && city.NavigationNeedsRebuild) city.GroundAndRebuildNavigation();
    }
}
#endif
