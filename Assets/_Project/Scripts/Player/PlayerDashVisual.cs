using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerDashVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;

    [SerializeField]
    private TrailRenderer dashTrail;

    [SerializeField]
    private ParticleSystem shadowDashParticles;

    [SerializeField, Tooltip("URP trail replacement. Legacy particles are used only when this is unassigned.")]
    private ShadowDashTrailVfx shadowDashVfx;

    [Header("Short Dash Stretch")]
    [SerializeField]
    private Vector3 dashScale =
        new Vector3(0.75f, 0.85f, 1.35f);

    [SerializeField]
    private float scaleTransitionSpeed = 18f;

    private PlayerMovement playerMovement;
    private Vector3 normalScale;

    private Renderer[] visualRenderers;
    private bool[] originalRendererStates;

    private bool wasDashing;
    private bool wasShadowDashing;
    private bool characterHidden;

    public bool IsHiddenByShadow => shadowDashVfx != null ? shadowDashVfx.ShouldHideCharacter
        : playerMovement != null && playerMovement.IsShadowDashing;
    public bool IsShadowWindEntering => playerMovement != null && playerMovement.IsShadowDashing &&
        shadowDashVfx != null && shadowDashVfx.IsEntering;
    public ShadowDashIceForm IceForm => shadowDashVfx != null && shadowDashVfx.UsesIceForm ? shadowDashVfx.IceForm : null;

    private void Awake()
    {
        playerMovement =
            GetComponent<PlayerMovement>();

        if (visual != null)
        {
            normalScale = visual.localScale;

            visualRenderers =
                visual.GetComponentsInChildren<Renderer>(
                    true
                );

            originalRendererStates =
                new bool[visualRenderers.Length];

            for (
                int i = 0;
                i < visualRenderers.Length;
                i++
            )
            {
                originalRendererStates[i] =
                    visualRenderers[i].enabled;
            }
        }

        ResetTrail();

        if (shadowDashParticles != null)
        {
            shadowDashParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }
    }

    private void Update()
    {
        if (playerMovement.IsDashing && !wasDashing && IceForm != null)
        {
            // Space after releasing M may interrupt the visual return. Restore
            // full size before Shield Dash; never blend a tiny landing body.
            shadowDashVfx.StopImmediately();
            if (visual != null) visual.localScale = normalScale;
        }
        // Request the visual state before weapon and animation Updates, not one
        // frame later in LateUpdate. LateUpdate only commits body visibility.
        UpdateShadowParticles(playerMovement.isActiveAndEnabled && playerMovement.IsShadowDashing);
    }

    private void LateUpdate()
    {
        bool isDashing =
            playerMovement.isActiveAndEnabled && playerMovement.IsDashing;

        bool isShadowDashing =
            playerMovement.isActiveAndEnabled && playerMovement.IsShadowDashing;

        UpdateDashTrail(isDashing);

        UpdateShadowParticles(
            isShadowDashing
        );

        bool shouldHide = IsHiddenByShadow;
        if (shouldHide != characterHidden)
        {
            SetCharacterVisible(!shouldHide);
            characterHidden = shouldHide;
        }

        UpdateVisualScale(isDashing);

        wasDashing = isDashing;
        wasShadowDashing = isShadowDashing;
    }

    private void UpdateDashTrail(
        bool isDashing
    )
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

    private void UpdateShadowParticles(
        bool isShadowDashing
    )
    {
        if (shadowDashVfx != null)
        {
            shadowDashVfx.SetShadowDashing(isShadowDashing);
            return;
        }
        if (shadowDashParticles == null)
        {
            return;
        }

        if (
            isShadowDashing &&
            !wasShadowDashing
        )
        {
            shadowDashParticles.Clear(true);
            shadowDashParticles.Play(true);
        }
        else if (
            !isShadowDashing &&
            wasShadowDashing
        )
        {
            shadowDashParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmitting
            );
        }
    }

    private void SetCharacterVisible(
        bool isVisible
    )
    {
        if (visualRenderers == null)
        {
            return;
        }

        for (
            int i = 0;
            i < visualRenderers.Length;
            i++
        )
        {
            Renderer targetRenderer =
                visualRenderers[i];

            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.enabled = isVisible
                ? originalRendererStates[i]
                : false;
        }
    }

    private void UpdateVisualScale(
        bool isDashing
    )
    {
        if (visual == null)
        {
            return;
        }

        if (!isDashing && IceForm != null)
        {
            visual.localScale = normalScale * IceForm.CharacterScale;
            return;
        }

        Vector3 targetScale =
            isDashing
                ? dashScale
                : normalScale;

        float transitionAmount =
            1f - Mathf.Exp(
                -scaleTransitionSpeed *
                Time.deltaTime
            );

        visual.localScale = Vector3.Lerp(
            visual.localScale,
            targetScale,
            transitionAmount
        );
    }

    private void ResetTrail()
    {
        if (dashTrail == null)
        {
            return;
        }

        dashTrail.emitting = false;
        dashTrail.Clear();
    }

    private void OnDisable()
    {
        if (shadowDashVfx != null) shadowDashVfx.StopImmediately();
        ResetTrail();
        SetCharacterVisible(true);
        characterHidden = false;

        if (shadowDashParticles != null)
        {
            shadowDashParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }

        if (visual != null)
        {
            visual.localScale = normalScale;
        }

        wasDashing = false;
        wasShadowDashing = false;
    }
}
