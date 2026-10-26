using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum ShadowTrailPreset { PurpleShadow, AzureVoid, CrimsonDark, Ice, Fire }

// Attach outside Player/Visual: that hierarchy is intentionally hidden during
// Shadow Dash. Per-instance property blocks never mutate the shared materials.
[ExecuteAlways, DisallowMultipleComponent]
public class ShadowDashTrailVfx : MonoBehaviour
{
    [Header("Ice Trails + Rounded Head")]
    [SerializeField] private ShadowDashIceForm iceForm;
    [SerializeField] private bool overrideMaterialColors;
    [SerializeField, ColorUsage(true, true)] private Color materialColor1 = new Color(0.7f, 0.85f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color materialColor2 = new Color(0.2f, 0.87f, 1f);
    [Header("Previous Cartoon Wind (unused when Ice Form is assigned)")]
    [SerializeField] private bool useWindVortex = true;
    [SerializeField] private ShadowDashWindVfx windVortex;
    [Header("Imported Trail References / Audio")]
    [SerializeField] private TrailRenderer glowTrail;
    [SerializeField] private TrailRenderer smokeTrail;
    [SerializeField] private TrailRenderer distortionTrail;
    [SerializeField] private AudioSource loopAudio;

    [Header("Colors")]
    [SerializeField] private Gradient glowColors = MakeGradient(new Color(0.42f, 0.12f, 0.9f), false);
    [SerializeField] private Gradient smokeColors = MakeGradient(new Color(0.035f, 0.012f, 0.065f), true);
    [SerializeField, Range(0f, 5f)] private float intensity = 1f;
    [SerializeField, Range(0.5f, 3f), Tooltip("Balances the imported Ice tail against its rounded head.")] private float iceTailBrightness = 1.6f;
    [SerializeField, Range(0f, 1f), Tooltip("Opacity of the Ice head and imported trails.")] private float opacity = 0.9f;

    [Header("Trail Shape and Motion")]
    [SerializeField, Range(0.05f, 4f)] private float width = 0.8f;
    [SerializeField, Range(0.02f, 2f), Tooltip("Seconds of history; at 11 m/s, 0.32 s is roughly a 3.5 m trail.")]
    private float lifetime = 0.32f;
    [SerializeField, Range(0.1f, 4f)] private float smokeWidthMultiplier = 1.7f;
    [SerializeField, Range(0.1f, 2f)] private float smokeLifetimeMultiplier = 1.25f;
    [SerializeField, Range(0f, 5f), Tooltip("Also scales cartoon wind rotation and flutter.")] private float textureAnimationSpeed = 1f;
    [SerializeField, Min(0.5f), Tooltip("Clear old points after a teleport to avoid a line across the map.")]
    private float teleportClearDistance = 4f;

    [Header("Screen Distortion (requires camera Opaque Texture)")]
    [SerializeField] private bool enableDistortion = true;
    [SerializeField, Range(0f, 0.05f)] private float distortionStrength = 0.012f;

    [Header("Looping Sound")]
    [SerializeField] private bool enableSound = true;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.25f;
    [SerializeField, Range(0.25f, 2f)] private float soundPitch = 1f;
    [SerializeField, Range(0.01f, 0.5f)] private float soundFadeOut = 0.12f;

    private MaterialPropertyBlock properties;
    private bool appearanceDirty = true;
    private bool shadowDashing;
    private Vector3 previousPosition;
    private bool previousOpaqueAvailable;
    private float tailExpiresAt;

    private static readonly int IntensityId = Shader.PropertyToID("_IntensityMultiplier");
    private static readonly int OpacityId = Shader.PropertyToID("_OpacityMultiplier");
    private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeedMultiplier");
    private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int DistortionEnabledId = Shader.PropertyToID("_DistortionEnabled");

    public bool IsEmitting => shadowDashing;
    public ShadowDashIceForm IceForm => iceForm;
    public bool UsesIceForm => iceForm != null && iceForm.isActiveAndEnabled && iceForm.IsConfigured;
    public bool IsEntering => UsesIceForm ? iceForm.IsEntering : UsesWindVortex && !windVortex.ShouldHideCharacter;
    public bool UsesWindVortex => !UsesIceForm && useWindVortex && windVortex != null && windVortex.isActiveAndEnabled && windVortex.IsConfigured;
    public bool ShouldHideCharacter => UsesIceForm ? iceForm.ShouldHideCharacter : useWindVortex && windVortex != null
        ? UsesWindVortex && windVortex.ShouldHideCharacter : shadowDashing;
    public float TrailLifetime => lifetime * Mathf.Max(1f, smokeLifetimeMultiplier);
    public bool IsDistortionAvailable => enableDistortion && OpaqueTextureAvailable();

    private void OnEnable()
    {
        previousPosition = transform.position;
        StopImmediately();
        RefreshAppearance();
    }

    private void OnValidate() => appearanceDirty = true;

    private void Update()
    {
        bool opaqueAvailable = OpaqueTextureAvailable();
        if (appearanceDirty || opaqueAvailable != previousOpaqueAvailable) RefreshAppearance();
        if (!Application.isPlaying) return;
        if (UsesWindVortex)
        {
            SetEmitting(glowTrail, false); SetEmitting(smokeTrail, false); SetEmitting(distortionTrail, false);
            SetRenderersEnabled(false);
        }

        if ((transform.position - previousPosition).sqrMagnitude > teleportClearDistance * teleportClearDistance)
            ClearTrails();
        previousPosition = transform.position;

        if (!shadowDashing && tailExpiresAt > 0f && Time.time >= tailExpiresAt)
        {
            tailExpiresAt = 0f;
            ClearTrails();
            SetRenderersEnabled(false);
        }

        if (loopAudio == null) return;
        bool shouldPlay = shadowDashing && enableSound && loopAudio.clip != null;
        if (shouldPlay && !loopAudio.isPlaying) loopAudio.Play();
        float targetVolume = shouldPlay ? soundVolume : 0f;
        loopAudio.volume = shouldPlay ? targetVolume : Mathf.MoveTowards(loopAudio.volume, 0f,
            Mathf.Max(0.01f, soundVolume) * Time.deltaTime / Mathf.Max(0.01f, soundFadeOut));
        if (!shouldPlay && loopAudio.volume <= 0f && loopAudio.isPlaying) loopAudio.Stop();
    }

    public void SetShadowDashing(bool active)
    {
        active &= Application.isPlaying && isActiveAndEnabled;
        if (UsesIceForm)
        {
            iceForm.SetRequested(active);
            active = iceForm.IsTrailEmitting;
        }
        if (windVortex != null) windVortex.SetDashing(active && UsesWindVortex);
        if (active == shadowDashing) return;
        shadowDashing = active;
        tailExpiresAt = active ? 0f : Time.time + TrailLifetime;
        if (active)
        {
            ClearTrails();
            previousPosition = transform.position;
        }
        RefreshAppearance();
        SetEmitting(glowTrail, active && !UsesWindVortex);
        SetEmitting(smokeTrail, active && !UsesWindVortex);
        SetEmitting(distortionTrail, active && !UsesWindVortex && distortionTrail != null && distortionTrail.enabled);
        // On exit leave renderers alive so the existing world-space tail fades.
        if (active && loopAudio != null && enableSound && loopAudio.clip != null)
        {
            loopAudio.volume = soundVolume;
            loopAudio.Play();
        }
    }

    public void StopImmediately()
    {
        if (iceForm != null) iceForm.StopImmediately();
        if (windVortex != null) windVortex.StopImmediately();
        shadowDashing = false;
        tailExpiresAt = 0f;
        SetEmitting(glowTrail, false);
        SetEmitting(smokeTrail, false);
        SetEmitting(distortionTrail, false);
        ClearTrails();
        SetRenderersEnabled(false);
        if (loopAudio != null)
        {
            loopAudio.Stop();
            loopAudio.volume = 0f;
        }
    }

    public void RefreshAppearance()
    {
        appearanceDirty = false;
        width = Safe(width, 0.05f, 4f, 0.8f);
        lifetime = Safe(lifetime, 0.02f, 2f, 0.32f);
        intensity = Safe(intensity, 0f, 5f, 1f);
        iceTailBrightness = Safe(iceTailBrightness, 0.5f, 3f, 1.6f);
        opacity = Safe(opacity, 0f, 1f, 0.9f);
        smokeWidthMultiplier = Safe(smokeWidthMultiplier, 0.1f, 4f, 1.7f);
        smokeLifetimeMultiplier = Safe(smokeLifetimeMultiplier, 0.1f, 2f, 1.25f);
        textureAnimationSpeed = Safe(textureAnimationSpeed, 0f, 5f, 1f);
        distortionStrength = Safe(distortionStrength, 0f, 0.05f, 0.012f);
        teleportClearDistance = Safe(teleportClearDistance, 0.5f, 100f, 4f);
        soundVolume = Safe(soundVolume, 0f, 1f, 0.25f);
        soundPitch = Safe(soundPitch, 0.25f, 2f, 1f);
        soundFadeOut = Safe(soundFadeOut, 0.01f, 0.5f, 0.12f);
        if (glowColors == null) glowColors = MakeGradient(new Color(0.42f, 0.12f, 0.9f), false);
        if (smokeColors == null) smokeColors = MakeGradient(new Color(0.035f, 0.012f, 0.065f), true);
        previousOpaqueAvailable = OpaqueTextureAvailable();
        ApplyTrail(glowTrail, glowColors, width, lifetime);
        ApplyTrail(smokeTrail, smokeColors, width * smokeWidthMultiplier, lifetime * smokeLifetimeMultiplier);
        ApplyTrail(distortionTrail, glowColors, width * 1.2f, lifetime * 0.75f);
        if (windVortex != null)
            windVortex.SetPalette(glowColors.Evaluate(0.07f), smokeColors.Evaluate(0.07f), intensity, textureAnimationSpeed);
        if (iceForm != null)
        {
            Material source = glowTrail != null ? glowTrail.sharedMaterial : null;
            Color primary = !overrideMaterialColors && source != null && source.HasProperty("_Color01") ? source.GetColor("_Color01") : materialColor1;
            Color secondary = !overrideMaterialColors && source != null && source.HasProperty("_Color02") ? source.GetColor("_Color02") : materialColor2;
            iceForm.SetAppearance(primary, secondary, intensity, opacity, textureAnimationSpeed);
        }
        bool renderTail = !UsesWindVortex && Application.isPlaying && (shadowDashing || Time.time < tailExpiresAt);
        if (glowTrail != null) glowTrail.enabled = renderTail;
        if (smokeTrail != null) smokeTrail.enabled = renderTail;
        if (distortionTrail != null)
        {
            distortionTrail.enabled = renderTail && enableDistortion && previousOpaqueAvailable && distortionStrength > 0f;
            distortionTrail.emitting = shadowDashing && distortionTrail.enabled;
            if (!distortionTrail.enabled) distortionTrail.Clear();
        }
        if (loopAudio != null)
        {
            loopAudio.playOnAwake = false;
            loopAudio.loop = true;
            loopAudio.pitch = soundPitch;
            loopAudio.spatialBlend = 1f;
            loopAudio.dopplerLevel = 0f;
        }
    }

    private void ApplyTrail(TrailRenderer trail, Gradient gradient, float trailWidth, float trailTime)
    {
        if (trail == null) return;
        trail.widthMultiplier = trailWidth;
        trail.time = trailTime;
        trail.colorGradient = gradient;
        trail.autodestruct = false;
        properties ??= new MaterialPropertyBlock();
        trail.GetPropertyBlock(properties);
        properties.SetFloat(IntensityId, intensity * (UsesIceForm ? iceTailBrightness : 1f));
        properties.SetFloat(OpacityId, opacity);
        properties.SetFloat(AnimationSpeedId, textureAnimationSpeed);
        properties.SetFloat(DistortionId, distortionStrength);
        properties.SetFloat(DistortionEnabledId, enableDistortion && previousOpaqueAvailable ? 1f : 0f);
        if (trail.sharedMaterial != null && trail.sharedMaterial.HasProperty("_Color01"))
        {
            properties.SetColor("_Color01", overrideMaterialColors ? materialColor1 : trail.sharedMaterial.GetColor("_Color01"));
            properties.SetColor("_Color02", overrideMaterialColors ? materialColor2 : trail.sharedMaterial.GetColor("_Color02"));
        }
        trail.SetPropertyBlock(properties);
    }

    public void SetColors(Color glow, Color smoke)
    {
        if (UsesIceForm || iceForm != null)
        {
            overrideMaterialColors = true; materialColor1 = glow; materialColor2 = smoke;
            glowColors = MakeGradient(Color.white, false); smokeColors = MakeGradient(Color.white, true);
        }
        else { glowColors = MakeGradient(glow, false); smokeColors = MakeGradient(smoke, true); }
        RefreshAppearance();
    }

    public void SetDimensions(float newWidth, float newLifetime)
    {
        width = newWidth;
        lifetime = newLifetime;
        RefreshAppearance();
    }

    public void SetIntensity(float value) { intensity = value; RefreshAppearance(); }

    public void ApplyPreset(ShadowTrailPreset preset)
    {
        switch (preset)
        {
            case ShadowTrailPreset.Ice:
                SetColors(new Color(0.7f, 0.85f, 1f), new Color(0.2f, 0.87f, 1f));
                break;
            case ShadowTrailPreset.Fire:
                SetColors(new Color(1f, 0.65f, 0.18f), new Color(1f, 0.12f, 0.02f));
                break;
            case ShadowTrailPreset.AzureVoid:
                SetColors(new Color(0.12f, 0.4f, 1f), new Color(0.012f, 0.025f, 0.07f));
                break;
            case ShadowTrailPreset.CrimsonDark:
                SetColors(new Color(0.9f, 0.06f, 0.24f), new Color(0.06f, 0.008f, 0.025f));
                break;
            default:
                SetColors(new Color(0.42f, 0.12f, 0.9f), new Color(0.035f, 0.012f, 0.065f));
                break;
        }
    }

    private static Gradient MakeGradient(Color color, bool smoke)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.3f, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(smoke ? 0.7f : 1f, 0.07f),
                new GradientAlphaKey(0.55f, 0.65f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static float Safe(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    private static bool OpaqueTextureAvailable()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return false;
        Camera camera = Camera.main;
        return camera != null && camera.TryGetComponent(out UniversalAdditionalCameraData data)
            ? data.requiresColorTexture : pipeline.supportsCameraOpaqueTexture;
    }

    private static void SetEmitting(TrailRenderer trail, bool emitting)
    {
        if (trail != null) trail.emitting = emitting;
    }

    private void ClearTrails()
    {
        if (glowTrail != null) glowTrail.Clear();
        if (smokeTrail != null) smokeTrail.Clear();
        if (distortionTrail != null) distortionTrail.Clear();
    }

    private void SetRenderersEnabled(bool active)
    {
        if (glowTrail != null) glowTrail.enabled = active;
        if (smokeTrail != null) smokeTrail.enabled = active;
        if (distortionTrail != null) distortionTrail.enabled = active && IsDistortionAvailable;
    }

    private void OnDisable() => StopImmediately();
}
