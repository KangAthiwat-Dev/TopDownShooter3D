using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerDashVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Dash Stretch")]
    [SerializeField] private Vector3 dashScale =
        new Vector3(0.75f, 0.85f, 1.35f);

    [SerializeField] private float scaleTransitionSpeed = 18f;

    private PlayerMovement playerMovement;
    private Vector3 normalScale;
    private bool wasDashing;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (visual != null)
        {
            normalScale = visual.localScale;
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }
    }

    private void LateUpdate()
    {
        bool isDashing = playerMovement.IsDashing;

        UpdateTrail(isDashing);
        UpdateVisualScale(isDashing);

        wasDashing = isDashing;
    }

    private void UpdateTrail(bool isDashing)
    {
        if (dashTrail == null)
        {
            return;
        }

        if (isDashing && !wasDashing)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }
        else if (!isDashing && wasDashing)
        {
            dashTrail.emitting = false;
        }
    }

    private void UpdateVisualScale(bool isDashing)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 targetScale =
            isDashing ? dashScale : normalScale;

        float transitionAmount =
            1f - Mathf.Exp(
                -scaleTransitionSpeed * Time.deltaTime
            );

        visual.localScale = Vector3.Lerp(
            visual.localScale,
            targetScale,
            transitionAmount
        );
    }

    private void OnDisable()
    {
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }

        if (visual != null)
        {
            visual.localScale = normalScale;
        }
    }
}