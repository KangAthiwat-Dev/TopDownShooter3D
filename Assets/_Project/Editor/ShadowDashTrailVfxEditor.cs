using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShadowDashTrailVfx)), CanEditMultipleObjects]
public class ShadowDashTrailVfxEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Presets (shape and sound stay unchanged)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        PresetButton("Ice", ShadowTrailPreset.Ice);
        PresetButton("Fire", ShadowTrailPreset.Fire);
        PresetButton("Purple Shadow", ShadowTrailPreset.PurpleShadow);
        PresetButton("Azure Void", ShadowTrailPreset.AzureVoid);
        PresetButton("Crimson Dark", ShadowTrailPreset.CrimsonDark);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Ice mode: imported Ice trails form the tail; Shadow Dash Ice Form controls the rounded head and Roll/return hand-off. " +
            "Width, lifetime, material colors, opacity, noise speed, distortion and sound above apply live. The old wind component is retained but disabled. " +
            "Keep this object outside Player/Visual. " +
            "Play Mode edits are previews and revert when you stop.", MessageType.Info);
    }

    private void PresetButton(string label, ShadowTrailPreset preset)
    {
        if (!GUILayout.Button(label)) return;
        Undo.RecordObjects(targets, "Change Shadow Trail Colors");
        foreach (Object selected in targets)
        {
            var effect = (ShadowDashTrailVfx)selected;
            effect.ApplyPreset(preset);
            EditorUtility.SetDirty(effect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
        }
    }
}
