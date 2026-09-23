using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TargetHighlight : MonoBehaviour
{
    [Header("Ring")]
    [SerializeField, Min(0.1f)]
    private float radius = 0.7f;

    [SerializeField, Range(8, 128)]
    private int segments = 48;

    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.07f;

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float pulseAmount = 0.08f;

    private LineRenderer lineRenderer;
    private Vector3 normalScale;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        normalScale = transform.localScale;

        BuildRing();
        SetHighlighted(false);
    }

    private void Update()
    {
        if (!lineRenderer.enabled)
        {
            return;
        }

        float pulse =
            1f + Mathf.Sin(Time.time * pulseSpeed) *
            pulseAmount;

        transform.localScale =
            normalScale * pulse;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.enabled = highlighted;

        if (!highlighted)
        {
            transform.localScale = normalScale;
        }
    }

    private void BuildRing()
    {
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment =
            LineAlignment.TransformZ;

        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.positionCount = segments;

        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle =
                i / (float)segments *
                Mathf.PI * 2f;

            points[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
        }

        lineRenderer.SetPositions(points);
    }

    private void OnValidate()
    {
        segments = Mathf.Max(8, segments);
        radius = Mathf.Max(0.1f, radius);
        lineWidth = Mathf.Max(0.01f, lineWidth);

        LineRenderer renderer =
            GetComponent<LineRenderer>();

        renderer.loop = true;
        renderer.useWorldSpace = false;
        renderer.alignment =
            LineAlignment.TransformZ;

        renderer.widthMultiplier = lineWidth;
        renderer.positionCount = segments;

        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle =
                i / (float)segments *
                Mathf.PI * 2f;

            points[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
        }

        renderer.SetPositions(points);
    }
}