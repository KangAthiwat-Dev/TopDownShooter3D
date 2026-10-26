using UnityEditor;

// Retain the established menu and prefab identity. Installation now uses the
// Ice asset and animation setup, never resaves the user's open gameplay scene.
public static class ShadowDashTrailSetup
{
    public const string PrefabPath = "Assets/_Project/Prefabs/VFX/ShadowDashTrails.prefab";

    [MenuItem("Tools/VFX/Build and Install Shadow Trails")]
    private static void Install() => IceDashSetup.Install();
}
