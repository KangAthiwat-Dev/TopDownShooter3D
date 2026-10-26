using UnityEngine;
using UnityEngine.Rendering.Universal;

// Camera-facing transparent refraction. Uses the opaque scene, not GrabPass;
// it never samples UI or modifies gameplay physics. duration=0 follows a bullet.
[DisallowMultipleComponent]
public class RangedRefraction : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField, Min(0f)] private float duration = 0.2f;
    [SerializeField, Min(0.01f)] private float startRadius = 0.08f;
    [SerializeField, Min(0.01f)] private float endRadius = 0.5f;
    [SerializeField, Range(0f, 0.02f)] private float strength = 0.002f;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.5f;
    private MaterialPropertyBlock properties;
    private float started;
    private bool hasStarted;
    private static readonly int Strength = Shader.PropertyToID("_Strength"), Opacity = Shader.PropertyToID("_Opacity"), Available = Shader.PropertyToID("_Available");
    private void OnEnable()
    {
        if (!hasStarted) { started = Time.time; hasStarted = true; }
        if (meshRenderer != null) meshRenderer.enabled = true;
    }
    private void LateUpdate()
    {
        if (meshRenderer == null) return;
        float progress = duration > 0f ? Mathf.Clamp01((Time.time - started) / duration) : 0f;
        Camera camera = Camera.main;
        bool visible = progress < 1f && camera != null && HasOpaqueTexture(camera);
        meshRenderer.enabled = visible;
        if (!visible) return;
        transform.rotation = camera.transform.rotation;
        float radius = Mathf.Lerp(startRadius, endRadius, 1f - Mathf.Pow(1f - progress, 2f));
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        properties ??= new MaterialPropertyBlock();
        properties.SetFloat(Strength, strength);
        properties.SetFloat(Opacity, opacity * (duration > 0f ? Mathf.Sin(progress * Mathf.PI) : 1f));
        properties.SetFloat(Available, 1f);
        meshRenderer.SetPropertyBlock(properties);
    }
    public static bool HasOpaqueTexture(Camera camera)
    {
        if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            if (data.requiresColorOption == CameraOverrideOption.On) return true;
            if (data.requiresColorOption == CameraOverrideOption.Off) return false;
        }
        return UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.supportsCameraOpaqueTexture;
    }
}
