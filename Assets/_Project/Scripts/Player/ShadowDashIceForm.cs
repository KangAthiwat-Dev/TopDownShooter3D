using UnityEngine;

// Only the rounded leading head and the visual hand-off live here. The tail is
// the imported Ice TrailRenderer prefab; physics never waits for animation.
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public class ShadowDashIceForm : MonoBehaviour
{
    [SerializeField] private MeshRenderer headRenderer;
    [Header("Roll → Trails → Land (seconds)")]
    [SerializeField, Range(0.15f, 0.5f)] private float entryDuration = 0.26f;
    [SerializeField, Range(0.4f, 0.9f)] private float shrinkStartFraction = 0.52f;
    [SerializeField, Range(0.15f, 0.5f)] private float returnDuration = 0.22f;
    [Header("Rounded Comet Head")]
    [SerializeField, Range(0.3f, 1.5f)] private float radius = 0.72f;
    [SerializeField, Range(0.7f, 2f)] private float headLength = 1.35f;
    [SerializeField, Range(-0.5f, 0.8f)] private float forwardOffset = 0.16f;
    [SerializeField, Range(3f, 40f)] private float turnResponsiveness = 24f;
    [SerializeField, Range(0f, 0.15f)] private float flutter = 0.045f;

    private enum Phase { Idle, Roll, Trail, Return }
    private Phase phase;
    private float phaseStart, returnScale, returnHead, characterScale = 1f, headAmount;
    private Vector3 axis = Vector3.forward;
    private CharacterController controller;
    private MaterialPropertyBlock block;
    private Color core = new Color(0.7f, 0.85f, 1f), edge = new Color(0.2f, 0.87f, 1f);
    private float intensity = 1f, opacity = 0.9f, animationSpeed = 1f;
    public int EntrySequence { get; private set; }
    public int LandingSequence { get; private set; }
    public bool IsConfigured => headRenderer != null && headRenderer.sharedMaterial != null;
    public bool IsEntering { get { Evaluate(); return phase == Phase.Roll; } }
    public bool IsReturning { get { Evaluate(); return phase == Phase.Return; } }
    public bool IsTrailEmitting { get { Evaluate(); return phase == Phase.Trail ||
        (phase == Phase.Roll && Time.time - phaseStart >= entryDuration * 0.7f); } }
    public bool ShouldHideCharacter { get { Evaluate(); return characterScale < 0.005f; } }
    public float CharacterScale { get { Evaluate(); return characterScale; } }
    public bool IsHeadVisible => headRenderer != null && headRenderer.enabled;
    public Vector3 Axis => axis;
    public float EntryDuration => entryDuration;
    public float ReturnDuration => returnDuration;

    private void Awake() => controller = GetComponentInParent<CharacterController>();
    private void OnEnable() => StopImmediately();
    private void OnValidate()
    {
        entryDuration = Safe(entryDuration, 0.15f, 0.5f, 0.26f);
        shrinkStartFraction = Safe(shrinkStartFraction, 0.4f, 0.9f, 0.52f);
        returnDuration = Safe(returnDuration, 0.15f, 0.5f, 0.22f);
        radius = Safe(radius, 0.3f, 1.5f, 0.72f);
        headLength = Safe(headLength, 0.7f, 2f, 1.35f);
        forwardOffset = Safe(forwardOffset, -0.5f, 0.8f, 0.16f);
        turnResponsiveness = Safe(turnResponsiveness, 3f, 40f, 24f);
        flutter = Safe(flutter, 0f, 0.15f, 0.045f);
    }
    private static float Safe(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    public void SetRequested(bool active)
    {
        active &= Application.isPlaying && isActiveAndEnabled && IsConfigured;
        Evaluate();
        bool requested = phase == Phase.Roll || phase == Phase.Trail;
        if (active == requested) return;
        phaseStart = Time.time;
        if (active)
        {
            // Re-entry starts from the current scale, not from a hard reset.
            returnScale = characterScale;
            returnHead = headAmount;
            if (headAmount < 0.01f) axis = DesiredAxis();
            phase = Phase.Roll; EntrySequence++;
        }
        else
        {
            returnScale = characterScale; returnHead = headAmount;
            phase = Phase.Return; LandingSequence++;
        }
    }

    private void Evaluate()
    {
        float age = Mathf.Max(0f, Time.time - phaseStart);
        if (phase == Phase.Roll)
        {
            float t = Mathf.Clamp01(age / entryDuration);
            characterScale = returnScale * (1f - Smooth((t - shrinkStartFraction) / (1f - shrinkStartFraction)));
            headAmount = Mathf.Lerp(returnHead, 1f, Smooth((t - 0.4f) / 0.5f));
            if (t >= 1f) { phase = Phase.Trail; characterScale = 0f; headAmount = 1f; }
        }
        else if (phase == Phase.Trail) { characterScale = 0f; headAmount = 1f; }
        else if (phase == Phase.Return)
        {
            float t = Mathf.Clamp01(age / returnDuration);
            // The land pose has a few Animator evaluations before it becomes
            // full size. No rigid shell or instant on/off body swap is needed.
            characterScale = Mathf.Lerp(returnScale, 1f, Smooth((t - 0.08f) / 0.72f));
            headAmount = returnHead * (1f - Smooth(t));
            if (t >= 1f) { phase = Phase.Idle; characterScale = 1f; headAmount = 0f; }
        }
    }
    private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

    public void SetAppearance(Color primary, Color secondary, float brightness, float alpha, float speed)
    {
        core = primary; edge = secondary; intensity = brightness; opacity = alpha; animationSpeed = speed;
    }
    private Vector3 DesiredAxis()
    {
        Vector3 velocity = controller != null && controller.enabled ? controller.velocity : transform.forward;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.01f) velocity = transform.forward;
        velocity.y = 0f;
        return velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector3.forward;
    }
    private void LateUpdate()
    {
        Evaluate();
        if (headRenderer == null) return;
        headRenderer.enabled = Application.isPlaying && isActiveAndEnabled && headAmount > 0.001f;
        if (!headRenderer.enabled) return;
        Vector3 desired = DesiredAxis();
        float yaw = Mathf.LerpAngle(Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg,
            Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));
        axis = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        Transform head = headRenderer.transform;
        head.SetPositionAndRotation(transform.position + axis * forwardOffset, Quaternion.LookRotation(axis, Vector3.up));
        float size = radius * 2f * headAmount;
        head.localScale = new Vector3(size, size, size * headLength);
        block ??= new MaterialPropertyBlock();
        block.SetColor("_CoreColor", core); block.SetColor("_EdgeColor", edge);
        block.SetFloat("_Intensity", intensity); block.SetFloat("_Opacity", opacity * headAmount);
        block.SetFloat("_MotionSpeed", animationSpeed); block.SetFloat("_Flutter", flutter);
        headRenderer.SetPropertyBlock(block);
    }
    public void StopImmediately()
    {
        phase = Phase.Idle; characterScale = 1f; headAmount = 0f;
        if (headRenderer != null) headRenderer.enabled = false;
    }
    private void OnDisable() => StopImmediately();
}
