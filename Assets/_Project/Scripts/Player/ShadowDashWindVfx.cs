using UnityEngine;
using UnityEngine.Rendering;

// Cosmetic only. Movement, energy, collision and invulnerability never wait for
// these transitions. This object must remain outside Player/Visual.
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public class ShadowDashWindVfx : MonoBehaviour
{
    [SerializeField] private Material windMaterial;
    [Header("Horizontal Vortex")]
    [SerializeField, Range(1.5f, 6f)] private float length = 3.3f;
    [SerializeField, Range(0.55f, 1.8f)] private float radius = 0.95f;
    [SerializeField, Range(0.1f, 0.9f)] private float ribbonWidth = 0.72f;
    [SerializeField, Range(2, 6)] private int ribbonCount = 3;
    [SerializeField, Range(0.4f, 2f)] private float turns = 0.85f;
    [SerializeField, Range(0.1f, 4f)] private float rotationsPerSecond = 1.6f;
    [SerializeField, Range(0f, 0.35f)] private float flutter = 0.13f;
    [SerializeField, Range(2f, 30f)] private float directionResponsiveness = 12f;
    [Header("Cartoon Palette")]
    [SerializeField, Range(0f, 1f)] private float whiteMix = 0.96f;
    [Header("Transformation (visual only)")]
    [SerializeField, Range(0.2f, 0.8f)] private float enterDuration = 0.38f;
    [SerializeField, Range(0.2f, 0.8f)] private float exitDuration = 0.36f;
    [SerializeField, Range(0.8f, 1.5f)] private float coverHalfHeight = 1.2f;
    [SerializeField, Range(0.8f, 2f), Tooltip("Room for hands and back weapons under the cover.")] private float coverRadius = 1.15f;
    [SerializeField, Range(1f, 2.5f), Tooltip("Room for the rifle muzzle under the cover.")] private float coverHalfLength = 1.5f;

    private const int Segments = 72;
    private const int CoverRings = 24;
    private const int CoverSides = 48;
    private enum Phase { Idle, Entering, Active, Exiting }
    private Phase phase;
    private float phaseStart, startEnvelope, startCover, envelope, cover;
    private bool hidden;
    private int visibilitySwapFrame = -1;
    private Vector3 axis = Vector3.forward;
    private CharacterController controller;
    private Mesh ribbons, shell;
    private MeshRenderer ribbonRenderer, coverRenderer;
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Color[] colors;
    private int builtCount;
    private MaterialPropertyBlock properties;
    private Color tint = new Color(0.42f, 0.12f, 0.9f);
    private Color accent = new Color(0.035f, 0.012f, 0.065f);
    private float strength = 1f, speed = 1f;
    private static readonly int MainColor = Shader.PropertyToID("_MainColor");
    private static readonly int AccentColor = Shader.PropertyToID("_AccentColor");
    private static readonly int Cover = Shader.PropertyToID("_Cover");
    private static readonly int IsCover = Shader.PropertyToID("_IsCover");
    private static readonly int MotionTime = Shader.PropertyToID("_MotionTime");

    public bool ShouldHideCharacter { get { Evaluate(); return hidden; } }
    public float CoverAmount { get { Evaluate(); return cover; } }
    public float Envelope { get { Evaluate(); return envelope; } }
    public bool IsVisible => (ribbonRenderer != null && ribbonRenderer.enabled) || (coverRenderer != null && coverRenderer.enabled);
    public bool IsConfigured => windMaterial != null;
    public Vector3 Axis => axis;
    public float EnterDuration => enterDuration;
    public float ExitDuration => exitDuration;

    private void Awake() => controller = GetComponentInParent<CharacterController>();
    private void OnValidate()
    {
        length = Valid(length, 1.5f, 6f, 3.3f);
        radius = Valid(radius, 0.55f, 1.8f, 0.95f);
        ribbonWidth = Valid(ribbonWidth, 0.1f, 0.9f, 0.72f);
        ribbonCount = Mathf.Clamp(ribbonCount, 2, 6);
        turns = Valid(turns, 0.4f, 2f, 0.85f);
        rotationsPerSecond = Valid(rotationsPerSecond, 0.1f, 4f, 1.6f);
        flutter = Valid(flutter, 0f, 0.35f, 0.13f);
        directionResponsiveness = Valid(directionResponsiveness, 2f, 30f, 12f);
        whiteMix = Valid(whiteMix, 0f, 1f, 0.96f);
        enterDuration = Valid(enterDuration, 0.2f, 0.8f, 0.38f);
        exitDuration = Valid(exitDuration, 0.2f, 0.8f, 0.36f);
        coverHalfHeight = Valid(coverHalfHeight, 0.8f, 1.5f, 1.2f);
        coverRadius = Valid(coverRadius, 0.8f, 2f, 1.15f);
        coverHalfLength = Valid(coverHalfLength, 1f, 2.5f, 1.5f);
    }
    private static float Valid(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    public void SetDashing(bool active)
    {
        active &= Application.isPlaying && isActiveAndEnabled && windMaterial != null;
        Evaluate();
        bool wasActive = phase == Phase.Entering || phase == Phase.Active;
        if (wasActive == active) return;
        startEnvelope = envelope;
        startCover = cover;
        phaseStart = Time.time;
        phase = active ? Phase.Entering : Phase.Exiting;
        if (active && envelope < 0.01f) axis = DesiredAxis();
    }

    // Absolute-time evaluation is shared by PlayerDashVisual and LateUpdate, so
    // a low frame rate cannot hide/reveal the body one frame ahead of its cover.
    private void Evaluate()
    {
        float age = Mathf.Max(0f, Time.time - phaseStart);
        if (phase == Phase.Entering)
        {
            float p = Mathf.Clamp01(age / enterDuration);
            envelope = Mathf.Lerp(startEnvelope, 1f, Smooth(p / 0.65f));
            cover = p < 0.78f ? Mathf.Lerp(startCover, 1f, Smooth(p / 0.62f))
                : 1f - Smooth((p - 0.78f) / 0.22f);
            if (p >= 0.68f && !hidden) { hidden = true; visibilitySwapFrame = Time.frameCount; }
            if (p >= 1f) { phase = Phase.Active; cover = 0f; envelope = 1f; }
        }
        else if (phase == Phase.Exiting)
        {
            float p = Mathf.Clamp01(age / exitDuration);
            // Early taps never hide a body which has not yet transformed.
            cover = p < 0.55f ? Mathf.Lerp(startCover, 1f, Smooth(p / 0.28f))
                : 1f - Smooth((p - 0.55f) / 0.45f);
            envelope = startEnvelope * (1f - Smooth((p - 0.35f) / 0.65f));
            if (p >= 0.34f && hidden) { hidden = false; visibilitySwapFrame = Time.frameCount; }
            if (p >= 1f) { phase = Phase.Idle; cover = envelope = 0f; hidden = false; }
        }
        else if (phase == Phase.Active) { envelope = 1f; cover = 0f; }
        else { envelope = cover = 0f; }
        // Even if one slow frame crosses the entire cover plateau, render one
        // fully covered frame for the visibility swap before peeling it away.
        if (visibilitySwapFrame == Time.frameCount) cover = 1f;
    }
    private static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

    public void SetPalette(Color primary, Color secondary, float intensity, float animationSpeed)
    {
        tint = primary; accent = secondary;
        strength = Mathf.Clamp(intensity, 0f, 2f); speed = Mathf.Clamp(animationSpeed, 0f, 5f);
    }

    private Vector3 DesiredAxis()
    {
        Vector3 direction = controller != null && controller.enabled ? controller.velocity : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.1f) direction = axis.sqrMagnitude > 0.1f ? axis : transform.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    private void LateUpdate()
    {
        Evaluate();
        if (windMaterial == null || (envelope <= 0f && cover <= 0f)) { SetVisible(false); return; }
        EnsureMeshes();
        if (ribbonRenderer.sharedMaterial != windMaterial) ribbonRenderer.sharedMaterial = windMaterial;
        if (coverRenderer.sharedMaterial != windMaterial) coverRenderer.sharedMaterial = windMaterial;
        Vector3 desired = DesiredAxis();
        // Slerp through a horizontal yaw also handles exact 180-degree turns.
        float yaw = Mathf.LerpAngle(Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg,
            Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-directionResponsiveness * Time.deltaTime));
        axis = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        BuildRibbons(right, Time.time * speed);
        // Shell and ribbon share a horizontal basis but shell covers the full
        // standing body while it swaps visibility underneath opaque wind.
        coverRenderer.transform.rotation = Quaternion.LookRotation(axis, Vector3.up);
        coverRenderer.transform.localScale = new Vector3(Mathf.Max(radius, coverRadius), coverHalfHeight, Mathf.Max(coverHalfLength, radius));
        properties ??= new MaterialPropertyBlock();
        Color white = Color.Lerp(tint, Color.white, whiteMix) * Mathf.Lerp(0.9f, 3.5f, strength / 2f);
        white.a = 1f;
        properties.SetColor(MainColor, white);
        properties.SetColor(AccentColor, Color.Lerp(Color.Lerp(accent, tint, 0.8f), Color.white, 0.4f));
        properties.SetFloat(MotionTime, Time.time * speed);
        properties.SetFloat(IsCover, 0f);
        properties.SetFloat(Cover, 1f);
        ribbonRenderer.SetPropertyBlock(properties);
        properties.SetFloat(IsCover, 1f);
        properties.SetFloat(Cover, cover);
        coverRenderer.SetPropertyBlock(properties);
        ribbonRenderer.enabled = envelope > 0.001f;
        coverRenderer.enabled = cover > 0.001f;
    }

    private void EnsureMeshes()
    {
        if (ribbonRenderer == null)
        {
            ribbons = new Mesh { name = "Shadow Wind Ribbons (Runtime)" };
            ribbons.MarkDynamic();
            ribbonRenderer = MakeRenderer("Flowing Horizontal Wind", ribbons);
            shell = MakeShell();
            coverRenderer = MakeRenderer("Transformation Wind Cover", shell);
        }
        if (builtCount == ribbonCount) return;
        builtCount = ribbonCount;
        int strips = ribbonCount + 3;
        vertices = new Vector3[strips * (Segments + 1) * 2];
        uvs = new Vector2[vertices.Length]; colors = new Color[vertices.Length];
        var triangles = new int[strips * Segments * 6];
        for (int strip = 0; strip < strips; strip++)
        for (int j = 0; j <= Segments; j++)
        {
            int v = (strip * (Segments + 1) + j) * 2;
            float t = j / (float)Segments;
            uvs[v] = new Vector2(t, 0f); uvs[v + 1] = new Vector2(t, 1f);
            colors[v] = colors[v + 1] = Color.white;
            if (j == Segments) continue;
            int q = (strip * Segments + j) * 6;
            triangles[q] = v; triangles[q + 1] = v + 2; triangles[q + 2] = v + 1;
            triangles[q + 3] = v + 1; triangles[q + 4] = v + 2; triangles[q + 5] = v + 3;
        }
        ribbons.Clear(); ribbons.vertices = vertices; ribbons.uv = uvs;
        ribbons.colors = colors; ribbons.triangles = triangles;
    }

    private void BuildRibbons(Vector3 right, float time)
    {
        for (int strip = 0; strip < ribbonCount + 3; strip++)
        {
            bool wisp = strip >= ribbonCount;
            float seed = strip * 2.39996f;
            float strandLength = length * (wisp ? 0.48f : 1f) * Mathf.Lerp(0.5f, 1f, envelope);
            float phaseAngle = seed - time * rotationsPerSecond * Mathf.PI * 2f;
            for (int j = 0; j <= Segments; j++)
            {
                float t = j / (float)Segments;
                float angle = phaseAngle + t * turns * Mathf.PI * 2f * (wisp ? 0.65f : 1f);
                float wave = Mathf.Sin(t * 11f - time * 7f + seed) * flutter * Mathf.Sin(t * Mathf.PI);
                float r = radius * (0.8f + 0.22f * Mathf.Sin(t * Mathf.PI)) * Mathf.Lerp(0.3f, 1f, envelope);
                r *= wisp ? 1.22f : 1f;
                r += wave;
                Vector3 radial = right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                Vector3 around = -right * Mathf.Sin(angle) + Vector3.up * Mathf.Cos(angle);
                Vector3 centre = axis * ((t - 0.58f) * strandLength + (wisp ? -length * 0.25f : 0f)) + radial * r;
                centre += Vector3.up * Mathf.Sin(t * 8f - time * 5f + seed) * flutter * 0.4f;
                // Wide curved crescent, sharp endpoints, slight roll; not a tube
                // or a fixed-width line. Width also travels down the ribbon.
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.7f);
                float halfWidth = ribbonWidth * (wisp ? 0.22f : 0.5f) * taper * envelope;
                halfWidth *= 1f + 0.18f * Mathf.Sin(t * 9f - time * 6f + seed);
                Vector3 across = (around * 0.85f - axis * 0.45f + radial * wave).normalized;
                int v = (strip * (Segments + 1) + j) * 2;
                vertices[v] = transform.InverseTransformVector(centre - across * halfWidth);
                vertices[v + 1] = transform.InverseTransformVector(centre + across * halfWidth);
            }
        }
        ribbons.vertices = vertices;
        ribbons.RecalculateBounds();
    }

    private MeshRenderer MakeRenderer(string title, Mesh mesh)
    {
        var child = new GameObject(title) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = windMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;
        return renderer;
    }

    private static Mesh MakeShell()
    {
        var points = new Vector3[(CoverRings + 1) * (CoverSides + 1)];
        var texcoords = new Vector2[points.Length];
        var triangles = new int[CoverRings * CoverSides * 6];
        for (int y = 0; y <= CoverRings; y++)
        for (int x = 0; x <= CoverSides; x++)
        {
            float v = y / (float)CoverRings, u = x / (float)CoverSides;
            float phi = v * Mathf.PI, theta = u * Mathf.PI * 2f;
            int i = y * (CoverSides + 1) + x;
            points[i] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi));
            texcoords[i] = new Vector2(u, v);
            if (y == CoverRings || x == CoverSides) continue;
            int q = (y * CoverSides + x) * 6;
            triangles[q] = i; triangles[q + 1] = i + 1; triangles[q + 2] = i + CoverSides + 1;
            triangles[q + 3] = i + 1; triangles[q + 4] = i + CoverSides + 2; triangles[q + 5] = i + CoverSides + 1;
        }
        var mesh = new Mesh { name = "Wind Cover (Runtime)", vertices = points, uv = texcoords, triangles = triangles };
        mesh.RecalculateBounds(); return mesh;
    }

    public void StopImmediately()
    {
        phase = Phase.Idle; envelope = cover = 0f; hidden = false; visibilitySwapFrame = -1;
        SetVisible(false);
    }
    private void SetVisible(bool visible)
    {
        if (ribbonRenderer != null) ribbonRenderer.enabled = visible;
        if (coverRenderer != null) coverRenderer.enabled = visible;
    }
    private void OnDisable() => StopImmediately();
    private void OnDestroy()
    {
        if (ribbonRenderer != null) Destroy(ribbonRenderer.gameObject);
        if (coverRenderer != null) Destroy(coverRenderer.gameObject);
        if (ribbons != null) Destroy(ribbons);
        if (shell != null) Destroy(shell);
    }
}
