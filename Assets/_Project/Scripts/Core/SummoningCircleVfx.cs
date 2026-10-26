using UnityEngine;

/// <summary>Visual-only one-shot of the installed Hovl magic circle. Does not own player state.</summary>
[DisallowMultipleComponent]
public sealed class SummoningCircleVfx : MonoBehaviour
{
    [Header("Appearance")]
    [ColorUsage(false, true)] [SerializeField] private Color effectColor = new Color(0.12f, 0.45f, 1f, 1f);
    [Min(0.1f)] [SerializeField] private float radius = 2.3f;
    [Min(0f)] [SerializeField] private float brightness = 2f;
    [Tooltip("Radius of the pack's original ring, before the configurable scale is applied.")]
    [Min(0.01f)] [SerializeField] private float authoredRadius = 2.5f;

    [Header("One-shot timing (scaled time; pauses with Inventory)")]
    [Min(0.01f)] [SerializeField] private float fadeInDuration = 0.65f;
    [Min(0f)] [SerializeField] private float holdDuration = 2f;
    [Min(0.01f)] [SerializeField] private float fadeOutDuration = 1.2f;
    [SerializeField] private bool playOnEnable = true;

    [Header("Optional ground light")]
    [SerializeField] private Light groundLight;
    [Min(0f)] [SerializeField] private float lightIntensity = 2f;
    [Min(0.1f)] [SerializeField] private float lightRange = 6f;

    private ParticleSystem[] particles;
    private Renderer[] renderers;
    private MaterialPropertyBlock properties;
    private bool hasPlayed, emissionStopped;
    public bool IsPlaying { get; private set; }
    public float ElapsedTime { get; private set; }
    public float Opacity { get; private set; }
    public float TotalDuration => Mathf.Max(0.01f, fadeInDuration) + Mathf.Max(0, holdDuration) + Mathf.Max(0.01f, fadeOutDuration);
    public Color EffectColor => effectColor;
    public float Radius => radius;
    private static readonly int TintId = Shader.PropertyToID("_EffectTint");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void OnEnable()
    {
        if (Application.isPlaying && playOnEnable && !hasPlayed) Play();
    }

    public void Play()
    {
        Cache();
        hasPlayed = true; IsPlaying = true; emissionStopped = false; ElapsedTime = 0;
        ApplyAppearance(0);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(false);
        }
    }

    private void Update()
    {
        if (!IsPlaying || Time.timeScale <= 0f) return;
        ElapsedTime += Time.deltaTime;
        ApplyAppearance(ElapsedTime);
        if (!emissionStopped && ElapsedTime >= fadeInDuration + holdDuration)
        {
            emissionStopped = true;
            foreach (ParticleSystem particle in particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        if (ElapsedTime < TotalDuration) return;
        IsPlaying = false;
        Destroy(gameObject);
    }

    private void Cache()
    {
        if (particles != null) return;
        particles = GetComponentsInChildren<ParticleSystem>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        properties = new MaterialPropertyBlock();
    }

    private void ApplyAppearance(float time)
    {
        Cache();
        float incoming = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / Mathf.Max(0.01f, fadeInDuration)));
        float outgoing = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - fadeInDuration - holdDuration) / Mathf.Max(0.01f, fadeOutDuration)));
        Opacity = incoming * outgoing;
        transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, authoredRadius)) * Mathf.Lerp(0.85f, 1f, incoming);
        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, effectColor);
            properties.SetFloat(OpacityId, Opacity);
            properties.SetFloat(IntensityId, brightness);
            renderer.SetPropertyBlock(properties);
        }
        if (groundLight != null)
        {
            groundLight.color = effectColor;
            groundLight.intensity = lightIntensity * Opacity;
            groundLight.range = lightRange;
            groundLight.enabled = Opacity > 0.001f && lightIntensity > 0f;
        }
    }

    private void OnDisable()
    {
        IsPlaying = false;
        if (particles != null)
            foreach (ParticleSystem particle in particles)
                if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (groundLight != null) groundLight.enabled = false;
        if (Application.isPlaying && hasPlayed) Destroy(gameObject);
    }

#if UNITY_EDITOR
    // Deterministic local previews. Never runs automatically in Edit Mode.
    public void SampleForPreview(float time)
    {
        Cache();
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Simulate(time, false, true, true);
        }
        ElapsedTime = time; ApplyAppearance(time);
    }
#endif
}
