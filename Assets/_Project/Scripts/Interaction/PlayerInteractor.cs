using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference interactAction;

    [Header("Detection")]
    [SerializeField]
    private Transform interactionOrigin;

    [SerializeField]
    private float interactionRadius = 2.2f;

    [SerializeField]
    private LayerMask interactableLayer;

    private readonly Collider[] overlapResults =
        new Collider[16];

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerAnimationController animationController;
    private PlayerMeleeAttack meleeAttack;
    private PlayerInputGate inputGate;
    private MeleeWeaponPickup pendingPickup;
    private Vector3 pickupFacingPoint;
    private bool pickupAnimationStarted;
    private bool pickupCommitted;
    private float pickupStartDeadline;

    [Header("Pickup Animation")]
    [SerializeField, Range(0f, 1f)] private float pickupNormalizedTime = 0.55f;
    [SerializeField, Min(0.1f)] private float animationStartTimeout = 0.5f;

    public bool IsInteracting { get; private set; }

    public bool CanStartInteraction =>
        isActiveAndEnabled && !IsInteracting &&
        (inputGate == null || inputGate.CanUse("Interact")) &&
        (playerHealth == null || playerHealth.IsAlive) &&
        (playerMovement == null ||
         (playerMovement.isActiveAndEnabled && !playerMovement.IsDashing &&
          !playerMovement.IsShadowDashing)) &&
        (meleeAttack == null || !meleeAttack.IsAttacking);

    public Interactable CurrentInteractable
    {
        get;
        private set;
    }

    public event Action<Interactable> FocusChanged;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        animationController = GetComponent<PlayerAnimationController>();
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        inputGate = GetComponent<PlayerInputGate>();

        if (interactionOrigin == null)
        {
            interactionOrigin = transform;
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died += StopPickup;
        }

        if (interactAction == null)
        {
            return;
        }

        interactAction.action.performed += HandleInteract;
        interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= StopPickup;
        }

        StopPickup();
        if (interactAction != null)
        {
            interactAction.action.performed -= HandleInteract;
            interactAction.action.Disable();
        }

        SetCurrentInteractable(null);
    }

    private void Update()
    {
        if (!CanStartInteraction)
        {
            SetCurrentInteractable(null);
            return;
        }

        Interactable nearest = FindNearestInteractable();
        SetCurrentInteractable(nearest);
    }

    private void HandleInteract(
        InputAction.CallbackContext context
    )
    {
        if (!CanStartInteraction || CurrentInteractable == null)
        {
            return;
        }

        if (!CurrentInteractable.CanInteract(gameObject))
        {
            return;
        }

        CurrentInteractable.Interact(gameObject);
    }

    public bool TryStartPickup(MeleeWeaponPickup pickup)
    {
        if (!CanStartInteraction || pickup == null ||
            playerMovement == null || animationController == null ||
            !animationController.isActiveAndEnabled ||
            !IsPickupInRange(pickup) || !pickup.TryReserve(this))
        {
            return false;
        }

        if (!animationController.TryPlayPickup())
        {
            pickup.Release(this);
            return false;
        }

        pendingPickup = pickup;
        pickupFacingPoint = pickup.transform.position;
        pickupAnimationStarted = false;
        pickupCommitted = false;
        pickupStartDeadline = Time.time + animationStartTimeout;
        IsInteracting = true;
        playerMovement.SetInteractionLocked(true);
        FacePickup();
        SetCurrentInteractable(null);
        return true;
    }

    private void LateUpdate()
    {
        if (inputGate != null && inputGate.BlocksGameplay) return;
        if (!IsInteracting)
        {
            return;
        }

        if ((playerHealth != null && !playerHealth.IsAlive) ||
            playerMovement == null || !playerMovement.isActiveAndEnabled ||
            animationController == null || !animationController.isActiveAndEnabled ||
            (!pickupCommitted &&
             (pendingPickup == null || !pendingPickup.isActiveAndEnabled ||
              !IsPickupInRange(pendingPickup))))
        {
            StopPickup();
            return;
        }

        FacePickup();
        if (!animationController.TryGetPickupProgress(out float progress))
        {
            if (pickupAnimationStarted || Time.time >= pickupStartDeadline)
            {
                StopPickup();
            }
            return;
        }

        pickupAnimationStarted = true;
        if (!pickupCommitted && progress >= pickupNormalizedTime)
        {
            if (!pendingPickup.TryCollect(this))
            {
                StopPickup();
                return;
            }
            pickupCommitted = true;
        }
    }

    private bool IsPickupInRange(MeleeWeaponPickup pickup)
    {
        return (pickup.transform.position - interactionOrigin.position).sqrMagnitude
               <= interactionRadius * interactionRadius;
    }

    private void FacePickup()
    {
        Vector3 direction = pickupFacingPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void StopPickup()
    {
        bool wasInteracting = IsInteracting;
        if (pendingPickup != null)
        {
            pendingPickup.Release(this);
        }
        pendingPickup = null;
        IsInteracting = false;
        pickupAnimationStarted = false;
        pickupCommitted = false;
        if (playerMovement != null)
        {
            playerMovement.SetInteractionLocked(false);
        }
        if (wasInteracting && animationController != null)
        {
            animationController.CancelPickup();
        }
    }

    private Interactable FindNearestInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            interactionOrigin.position,
            interactionRadius,
            overlapResults,
            interactableLayer,
            QueryTriggerInteraction.Collide
        );

        Interactable nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];

            if (hit == null)
            {
                continue;
            }

            Interactable candidate =
                hit.GetComponentInParent<Interactable>();

            if (candidate == null ||
                !candidate.CanInteract(gameObject))
            {
                continue;
            }

            float distance =
                (candidate.transform.position -
                 interactionOrigin.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void SetCurrentInteractable(
        Interactable interactable
    )
    {
        if (CurrentInteractable == interactable)
        {
            return;
        }

        CurrentInteractable = interactable;
        FocusChanged?.Invoke(CurrentInteractable);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin =
            interactionOrigin != null
                ? interactionOrigin
                : transform;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            origin.position,
            interactionRadius
        );
    }
}
