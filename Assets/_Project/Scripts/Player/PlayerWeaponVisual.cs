using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class PlayerWeaponVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerTargeting playerTargeting;

    [SerializeField]
    private PlayerAnimationController animationController;

    [SerializeField]
    private GameObject gunVisual;

    private PlayerMeleeAttack meleeAttack;
    private PlayerMeleeEquipment equipment;
    private PlayerInteractor playerInteractor;
    private PlayerMovement playerMovement;
    private PlayerDashVisual dashVisual;
    private PlayerHealth health;

    private void Awake()
    {
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        equipment = GetComponent<PlayerMeleeEquipment>();
        playerInteractor = GetComponent<PlayerInteractor>();
        playerMovement = GetComponent<PlayerMovement>();
        dashVisual = GetComponent<PlayerDashVisual>();
        health = GetComponent<PlayerHealth>();
        if (playerTargeting == null)
        {
            playerTargeting =
                GetComponent<PlayerTargeting>();
        }

        if (animationController == null)
        {
            animationController =
                GetComponent<PlayerAnimationController>();
        }
    }

    private void OnEnable()
    {
        if (playerTargeting == null)
        {
            Debug.LogError(
                "PlayerWeaponVisual: PlayerTargeting is missing.",
                this
            );

            return;
        }

        playerTargeting.TargetChanged +=
            HandleTargetChanged;

        RefreshWeaponState();
    }

    private void OnDisable()
    {
        if (playerTargeting != null)
        {
            playerTargeting.TargetChanged -=
                HandleTargetChanged;
        }
    }

    private void HandleTargetChanged(
        Targetable newTarget
    )
    {
        RefreshWeaponState();
    }

    private void Update()
    {
        // Close override อาจเปลี่ยนได้แม้ยังล็อกศัตรูตัวเดิม
        if (playerTargeting != null)
        {
            RefreshWeaponState();
        }
    }

    private void RefreshWeaponState()
    {
        if (health != null && !health.IsAlive)
        {
            SetWeaponState(false);
            if(equipment!=null){equipment.SetWeaponDrawn(false);equipment.SetWeaponVisible(false);}
            if(animationController!=null)animationController.SetHasMeleeWeapon(false);
            return;
        }
        // Keep the current gun/back-weapon presentation until the wind covers
        // it. Attack eligibility still uses the real Movement Dash state.
        if (dashVisual != null && dashVisual.IsShadowWindEntering) return;
        bool pickingUp = playerInteractor != null && playerInteractor.IsInteracting;
        bool dashing = playerMovement != null &&
                       (playerMovement.IsDashing || (dashVisual != null
                           ? dashVisual.IsHiddenByShadow : playerMovement.IsShadowDashing));
        bool attacking = meleeAttack != null && meleeAttack.IsAttacking;
        bool hasTarget = playerTargeting.CurrentTarget != null &&
                         playerTargeting.CurrentTarget.IsTargetable;
        bool showGun = hasTarget &&
            !playerTargeting.IsCloseTarget &&
            !attacking && !pickingUp && !dashing;
        SetWeaponState(showGun);

        bool showMelee = equipment != null && equipment.HasWeapon &&
                         !pickingUp && !dashing;
        // Finish the swing before stowing, even if the hit just killed the target.
        bool drawMelee = showMelee &&
                         (attacking || (hasTarget && playerTargeting.IsCloseTarget));
        if (equipment != null)
        {
            equipment.SetWeaponDrawn(drawMelee);
            equipment.SetWeaponVisible(showMelee);
        }
        if (animationController != null) animationController.SetHasMeleeWeapon(drawMelee);
    }

    private void SetWeaponState(bool hasTarget)
    {
        if (gunVisual != null &&
            gunVisual.activeSelf != hasTarget)
        {
            gunVisual.SetActive(hasTarget);
        }

        if (animationController != null)
        {
            animationController.SetHasGun(hasTarget);
        }
    }
}
