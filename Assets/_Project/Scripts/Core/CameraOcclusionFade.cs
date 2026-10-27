using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Temporarily fades scenery between the fixed camera and the player's body.</summary>
[DefaultExecutionOrder(2000)]
[DisallowMultipleComponent]
public sealed class CameraOcclusionFade : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private LayerMask sceneryLayers = (1 << 0) | (1 << 7);
    [Tooltip("Off keeps custom foliage/VFX shaders intact. Buildings use KitLit or URP Lit and do not need this fallback.")]
    [SerializeField] private bool fadeUnsupportedShaders;
    [Tooltip("Build-time references retain the transparent shader variants used by the scene.")]
    [SerializeField] private Material[] retainedTransparentVariants;
    [SerializeField] private Material fallbackTransparentMaterial;

    [Header("Visibility")]
    [SerializeField, Range(.02f, .6f)] private float fadedOpacity = .15f;
    [SerializeField, Min(.01f)] private float fadeOutSeconds = .2f;
    [SerializeField, Min(.01f)] private float fadeInSeconds = .35f;
    [SerializeField, Min(0f)] private float releaseDelay = .15f;
    [SerializeField, Min(.01f)] private float scanInterval = .06f;
    [SerializeField, Min(.2f)] private float candidateRefreshSeconds = 2f;
    [SerializeField, Min(0f)] private float boundsPadding = .12f;
    [SerializeField, Min(0f)] private float keepGroundHeight = .5f;
    [SerializeField] private Vector3 bodySampleHeights = new Vector3(.55f, 1.1f, 1.7f);

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly List<MeshRenderer> candidates = new List<MeshRenderer>();
    private readonly Dictionary<Renderer, FadeState> active = new Dictionary<Renderer, FadeState>();
    private readonly Dictionary<Material, Material> transparentCopies = new Dictionary<Material, Material>();
    private readonly List<Renderer> finished = new List<Renderer>();
    private readonly Ray[] bodyRays = new Ray[3];
    private readonly float[] bodyRayLengths = new float[3];
    private float nextScan, nextRefresh;

    private sealed class FadeState
    {
        public Renderer renderer;
        public Material[] originals;
        public MaterialPropertyBlock[] originalBlocks, fadeBlocks;
        public Color[] colors;
        public int[] colorProperties;
        public float opacity = 1f, lastSeen;
    }

    public int ActiveFadeCount => active.Count;
    public float GetOpacity(Renderer item) => item != null && active.TryGetValue(item, out var state) ? state.opacity : 1f;

    private void OnEnable()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        RefreshCandidates();
        nextScan = 0f;
    }

    // Mesh bounds also catch imported wall/roof pieces with no collider. Skinned characters,
    // particles, terrain and low paving are deliberately never included.
    public void RefreshCandidates()
    {
        candidates.Clear();
        foreach (var item in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if ((sceneryLayers.value & (1 << item.gameObject.layer)) == 0) continue;
            if (playerTarget != null && item.transform.IsChildOf(playerTarget)) continue;
            if (item.GetComponentInParent<EnemyHealth>() != null || item.GetComponentInParent<NpcIdleLook>() != null) continue;
            if (!fadeUnsupportedShaders)
            {
                bool supported = true;
                foreach (var material in item.sharedMaterials)
                    if (material != null && !SupportsTransparency(material)) { supported = false; break; }
                if (!supported) continue;
            }
            candidates.Add(item);
        }
        nextRefresh = Time.time + candidateRefreshSeconds;
    }

    private void LateUpdate()
    {
        if (viewCamera == null || playerTarget == null || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        if (Time.time >= nextRefresh) RefreshCandidates();
        if (Time.time >= nextScan)
        {
            Scan();
            nextScan = Time.time + scanInterval;
        }
        finished.Clear();
        foreach (var pair in active)
        {
            var state = pair.Value;
            if (state.renderer == null) { finished.Add(pair.Key); continue; }
            bool obscuring = Time.time - state.lastSeen <= releaseDelay;
            float goal = obscuring ? fadedOpacity : 1f;
            float seconds = obscuring ? fadeOutSeconds : fadeInSeconds;
            state.opacity = Mathf.MoveTowards(state.opacity, goal, (1f - fadedOpacity) * Time.deltaTime / Mathf.Max(.01f, seconds));
            if (!obscuring && state.opacity >= 1f)
            {
                Restore(state);
                finished.Add(pair.Key);
                continue;
            }
            for (int i = 0; i < state.fadeBlocks.Length; i++)
            {
                if (state.colorProperties[i] == 0) continue;
                Color color = state.colors[i]; color.a *= state.opacity;
                state.fadeBlocks[i].SetColor(state.colorProperties[i], color);
                state.renderer.SetPropertyBlock(state.fadeBlocks[i], i);
            }
        }
        foreach (var item in finished) active.Remove(item);
    }

    private void Scan()
    {
        Vector3 origin = viewCamera.transform.position;
        for (int i = 0; i < bodyRays.Length; i++)
        {
            Vector3 delta = playerTarget.position + Vector3.up * bodySampleHeights[i] - origin;
            bodyRays[i] = new Ray(origin, delta.normalized);
            bodyRayLengths[i] = Mathf.Max(0f, delta.magnitude - .2f);
        }
        foreach (var item in candidates)
        {
            if (item == null || !item.enabled || !item.gameObject.activeInHierarchy) continue;
            if ((sceneryLayers.value & (1 << item.gameObject.layer)) == 0 || item.transform.IsChildOf(playerTarget)) continue;
            Bounds bounds = item.bounds;
            if (bounds.max.y <= playerTarget.position.y + keepGroundHeight) continue;
            bounds.Expand(boundsPadding * 2f);
            bool blocks = false;
            for (int i = 0; i < bodyRays.Length; i++)
                if (bounds.IntersectRay(bodyRays[i], out float distance) && distance < bodyRayLengths[i]) { blocks = true; break; }
            if (!blocks) continue;
            if (!active.TryGetValue(item, out var state))
            {
                state = BeginFade(item);
                if (state == null) continue;
                active.Add(item, state);
            }
            state.lastSeen = Time.time;
        }
    }

    private FadeState BeginFade(Renderer item)
    {
        Material[] originals = item.sharedMaterials;
        if (originals.Length == 0) return null;
        var materials = new Material[originals.Length];
        var state = new FadeState
        {
            renderer = item, originals = originals,
            originalBlocks = new MaterialPropertyBlock[originals.Length],
            fadeBlocks = new MaterialPropertyBlock[originals.Length],
            colors = new Color[originals.Length], colorProperties = new int[originals.Length]
        };
        for (int i = 0; i < originals.Length; i++)
        {
            var source = originals[i];
            materials[i] = source != null ? TransparentCopy(source) : null;
            var originalBlock = new MaterialPropertyBlock(); item.GetPropertyBlock(originalBlock, i);
            state.originalBlocks[i] = originalBlock;
            var fadeBlock = new MaterialPropertyBlock(); item.GetPropertyBlock(fadeBlock, i);
            // A per-material block overrides the renderer-level block: inherit it when necessary.
            if (fadeBlock.isEmpty) item.GetPropertyBlock(fadeBlock);
            state.fadeBlocks[i] = fadeBlock;
            if (materials[i] == null) continue;
            int property = materials[i].HasProperty(BaseColor) ? BaseColor : ColorId;
            state.colorProperties[i] = property;
            state.colors[i] = fadeBlock.HasProperty(property) ? fadeBlock.GetColor(property) : materials[i].GetColor(property);
        }
        item.sharedMaterials = materials;
        return state;
    }

    private Material TransparentCopy(Material source)
    {
        if (transparentCopies.TryGetValue(source, out var cached)) return cached;
        bool supported = SupportsTransparency(source);
        if (!supported && fallbackTransparentMaterial == null) return source;
        var copy = new Material(supported ? source : fallbackTransparentMaterial) { name = source.name + " (camera fade)", hideFlags = HideFlags.DontSave };
        if (!supported)
        {
            string texture = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (texture != null)
            {
                copy.SetTexture("_BaseMap", source.GetTexture(texture));
                copy.SetTextureScale("_BaseMap", source.GetTextureScale(texture)); copy.SetTextureOffset("_BaseMap", source.GetTextureOffset(texture));
            }
            if (IsColor(source, "_BaseColor")) copy.SetColor(BaseColor, source.GetColor(BaseColor));
            else if (IsColor(source, "_Color")) copy.SetColor(BaseColor, source.GetColor(ColorId));
        }
        ConfigureTransparent(copy);
        transparentCopies.Add(source, copy);
        return copy;
    }

    public static bool SupportsTransparency(Material source) => source != null &&
        (source.shader.name == "MedievalKit/KitLit" || source.shader.name == "Universal Render Pipeline/Lit" || source.shader.name == "Universal Render Pipeline/Simple Lit");

    private static bool IsColor(Material material, string name)
    {
        int index = material.shader.FindPropertyIndex(name);
        return index >= 0 && material.shader.GetPropertyType(index) == ShaderPropertyType.Color;
    }

    // Used on PRIVATE runtime copies / generated variant assets only, never shared source assets.
    public static void ConfigureTransparent(Material material)
    {
        SetFloat(material, "_Surface", 1f); SetFloat(material, "_Blend", 0f);
        SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha); SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One); SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_ZWrite", 0f); SetFloat(material, "_AlphaClip", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.DisableKeyword("_ALPHAMODULATE_ON");
        material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false); material.SetShaderPassEnabled("DepthOnly", false); material.SetShaderPassEnabled("DepthNormals", false);
        material.SetShaderPassEnabled("DepthNormalsOnly", false);
    }
    private static void SetFloat(Material material, string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }
    private static void Restore(FadeState state)
    {
        if (state.renderer == null) return;
        state.renderer.sharedMaterials = state.originals;
        for (int i = 0; i < state.originalBlocks.Length; i++)
            state.renderer.SetPropertyBlock(state.originalBlocks[i].isEmpty ? null : state.originalBlocks[i], i);
    }
    private void OnDisable()
    {
        foreach (var state in active.Values) Restore(state);
        active.Clear(); candidates.Clear();
        foreach (var material in transparentCopies.Values)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        transparentCopies.Clear();
    }
}
