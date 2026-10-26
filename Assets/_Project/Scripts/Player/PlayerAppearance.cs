using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerAppearance : MonoBehaviour
{
    [Serializable]
    private struct ColorTarget
    {
        public Renderer renderer;
        [Min(0)] public int materialIndex;

        public ColorTarget(Renderer renderer, int materialIndex)
        {
            this.renderer = renderer;
            this.materialIndex = materialIndex;
        }
    }

    [Serializable]
    private sealed class HairStyle
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        // A style can share base hair with another style, e.g. shaved sides + knot.
        public GameObject[] parts = Array.Empty<GameObject>();
    }

    [Header("Current Appearance (previewable in the Inspector)")]
    [SerializeField] private PlayerAppearanceData appearance = PlayerAppearanceData.Default;
    [Header("Explicit material slots: never recolor eyes, armor or accessories")]
    [SerializeField] private ColorTarget[] skinTargets = Array.Empty<ColorTarget>();
    [SerializeField] private ColorTarget[] hairTargets = Array.Empty<ColorTarget>();
    [Header("Stable IDs for future character-creation UI and saved profiles")]
    [SerializeField] private HairStyle[] hairStyles = Array.Empty<HairStyle>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock colorBlock;
    private readonly HashSet<GameObject> selectedHairParts = new HashSet<GameObject>();
    private readonly HashSet<GameObject> allHairParts = new HashSet<GameObject>();
    private bool needsRefresh;

    public PlayerAppearanceData CurrentAppearance => appearance;
    public int HairStyleCount => hairStyles.Length;
    public event Action<PlayerAppearanceData> AppearanceChanged;

    private void OnEnable() => RefreshAppearance();
    private void OnValidate() => needsRefresh = true;
    private void Update()
    {
        // OnValidate may run off the main thread; apply renderer/active-state
        // changes here instead. ExecuteAlways also gives the editor a preview.
        if (needsRefresh) RefreshAppearance();
    }

    public string GetHairStyleId(int index) => hairStyles[index].id;
    public string GetHairStyleName(int index) => hairStyles[index].displayName;

    public void SetSkinColor(Color color)
    {
        appearance.skinColor = Sanitize(color);
        RefreshAppearance();
        AppearanceChanged?.Invoke(appearance);
    }

    public void SetHairColor(Color color)
    {
        appearance.hairColor = Sanitize(color);
        RefreshAppearance();
        AppearanceChanged?.Invoke(appearance);
    }

    public bool TrySetHairStyle(string id)
    {
        if (FindHairStyle(id) < 0) return false;
        appearance.hairStyleId = id;
        RefreshAppearance();
        AppearanceChanged?.Invoke(appearance);
        return true;
    }

    public bool ApplyAppearance(PlayerAppearanceData data)
    {
        // Invalid saved IDs cannot partially change a character's colors.
        if (FindHairStyle(data.hairStyleId) < 0) return false;
        data.skinColor = Sanitize(data.skinColor);
        data.hairColor = Sanitize(data.hairColor);
        appearance = data;
        RefreshAppearance();
        AppearanceChanged?.Invoke(appearance);
        return true;
    }

    [ContextMenu("Refresh Appearance")]
    public void RefreshAppearance()
    {
        needsRefresh = false;
        appearance.skinColor = Sanitize(appearance.skinColor);
        appearance.hairColor = Sanitize(appearance.hairColor);
        ApplyColor(skinTargets, appearance.skinColor);
        ApplyColor(hairTargets, appearance.hairColor);

        int selected = FindHairStyle(appearance.hairStyleId);
        if (selected < 0 || hairStyles[selected] == null) return;
        selectedHairParts.Clear();
        allHairParts.Clear();
        foreach (GameObject part in hairStyles[selected].parts)
            if (part != null) selectedHairParts.Add(part);
        foreach (HairStyle style in hairStyles)
        {
            if (style == null) continue;
            foreach (GameObject part in style.parts)
                if (part != null) allHairParts.Add(part);
        }
        foreach (GameObject part in allHairParts)
        {
            bool visible = selectedHairParts.Contains(part);
            // Leave Renderer.enabled alone: Shadow Dash owns visibility there.
            if (part.activeSelf != visible) part.SetActive(visible);
        }
    }

    private void ApplyColor(ColorTarget[] targets, Color color)
    {
        if (colorBlock == null) colorBlock = new MaterialPropertyBlock();
        foreach (ColorTarget target in targets)
        {
            if (target.renderer == null) continue;
            Material[] materials = target.renderer.sharedMaterials;
            if (target.materialIndex < 0 || target.materialIndex >= materials.Length) continue;
            Material material = materials[target.materialIndex];
            if (material == null) continue;
            colorBlock.Clear();
            target.renderer.GetPropertyBlock(colorBlock, target.materialIndex);
            if (material.HasProperty(BaseColorId)) colorBlock.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) colorBlock.SetColor(ColorId, color);
            // Per-instance colors; do not allocate .materials or mutate a shared
            // material asset used by other players or the imported character.
            target.renderer.SetPropertyBlock(colorBlock, target.materialIndex);
        }
    }

    private int FindHairStyle(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < hairStyles.Length; i++)
            if (hairStyles[i] != null && string.Equals(hairStyles[i].id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

    private static Color Sanitize(Color color) => new Color(Channel(color.r), Channel(color.g), Channel(color.b), 1f);
    private static float Channel(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
}
