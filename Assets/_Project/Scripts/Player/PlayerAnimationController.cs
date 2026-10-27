using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(100)]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAttackController attackController;

    [Header("Movement Animation")]
    [SerializeField, Min(0f)] private float dampTime = 0.1f;
    [SerializeField, Min(0f)] private float directionDampTime = 0.12f;
    [SerializeField, Min(1f)] private float turnFullSpeed = 240f;
    [SerializeField, Min(0f)] private float turnDampTime = 0.08f;
    [SerializeField, Min(.1f)] private float walkAnimationSpeed=2f;
    [SerializeField, Min(.1f)] private float runAnimationSpeed=4.5f;
    [SerializeField, Min(.1f)] private float sprintAnimationSpeed=7.5f;
    private float movementAngle;
    private bool hasMovementAngle;
    private static readonly int LocomotionRateHash=Animator.StringToHash("LocomotionRate");

    [Header("Weapon Animation")]
    private bool hasGun;
    [SerializeField] private string weaponLayerName = "WeaponUpperBody";
    [SerializeField, Min(0f)] private float layerBlendSpeed = 12f;
    [SerializeField] private string meleeLayerName = "MeleeUpperBody";

    private static readonly int MoveSpeedHash =
        Animator.StringToHash("MoveSpeed");

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int TurnAmountHash = Animator.StringToHash("TurnAmount");
    private static readonly int TurnBlendHash = Animator.StringToHash("TurnBlend");
    private float previousFacingYaw;

    private static readonly int DashHash =
        Animator.StringToHash("Dash");

    private static readonly int HasGunHash =
        Animator.StringToHash("HasGun");

    private static readonly int ShootHash =
        Animator.StringToHash("Shoot");

    private bool wasDashing;
    private int weaponLayerIndex = -1;
    private PlayerMeleeAttack meleeAttack;
    private bool nextPunchIsCross;
    private bool hasMeleeWeapon;
    private int meleeLayerIndex = -1;
    private PlayerInteractor playerInteractor;
    private PlayerDashVisual dashVisual;
    private int shadowEntrySequence, shadowLandingSequence;
    private bool deathPlaying;
    private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Death");
    public bool IsPlayingDeath => deathPlaying;
    private static readonly int ShadowRollHash = Animator.StringToHash("Base Layer.ShadowRollEnter");
    private static readonly int ShadowLandHash = Animator.StringToHash("Base Layer.ShadowNinjaLand");

    public bool IsDashLanding => animator != null && animator.isActiveAndEnabled &&
        (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == ShadowLandHash ||
         (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == ShadowLandHash));

    private static readonly int PunchStateHash =
        Animator.StringToHash("Base Layer.UnarmedPunch");

    private static readonly int CrossPunchStateHash =
        Animator.StringToHash("Base Layer.UnarmedPunchCross");

    private static readonly int LocomotionStateHash =
        Animator.StringToHash("Base Layer.Locomotion");

    private static readonly int MeleeWeaponStateHash =
        Animator.StringToHash("Base Layer.MeleeWeaponAttack");

    private static readonly int PickupStateHash =
        Animator.StringToHash("Base Layer.Pickup");

    private static readonly int GunIdleHash = Animator.StringToHash("GunIdle");
    private static readonly int GunShootHash = Animator.StringToHash("GunShoot");

    public bool IsRangedPoseReady
    {
        get
        {
            if (!isActiveAndEnabled || !hasGun || animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null || weaponLayerIndex < 0 ||
                animator.GetLayerWeight(weaponLayerIndex) < 0.99f ||
                animator.GetCurrentAnimatorStateInfo(0).fullPathHash != LocomotionStateHash ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash != LocomotionStateHash)) return false;

            int state = animator.GetCurrentAnimatorStateInfo(weaponLayerIndex).shortNameHash;
            if (state != GunIdleHash && state != GunShootHash) return false;
            if (!animator.IsInTransition(weaponLayerIndex)) return true;
            int next = animator.GetNextAnimatorStateInfo(weaponLayerIndex).shortNameHash;
            return next == GunIdleHash || next == GunShootHash;
        }
    }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }

        if (attackController == null)
        {
            attackController = GetComponent<PlayerAttackController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        meleeAttack = GetComponent<PlayerMeleeAttack>();
        playerInteractor = GetComponent<PlayerInteractor>();
        dashVisual = GetComponent<PlayerDashVisual>();
        previousFacingYaw = transform.eulerAngles.y;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            weaponLayerIndex = animator.GetLayerIndex(weaponLayerName);
            meleeLayerIndex = animator.GetLayerIndex(meleeLayerName);
        }
    }

    private void OnEnable()
    {
        previousFacingYaw = transform.eulerAngles.y;
        if (attackController != null)
        {
            attackController.RangedShotFired += HandleRangedShotFired;
        }
    }

    private void Start()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.SetBool(HasGunHash, hasGun);

        if (weaponLayerIndex >= 0)
        {
            animator.SetLayerWeight(
                weaponLayerIndex,
                hasGun ? 1f : 0f
            );
        }

        if (meleeLayerIndex >= 0)
        {
            animator.SetLayerWeight(meleeLayerIndex, hasMeleeWeapon ? 1f : 0f);
        }
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.RangedShotFired -= HandleRangedShotFired;
        }
    }

    private void Update()
    {
        if (deathPlaying) return;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        bool isDashing = playerMovement.IsDashing;

        UpdateMovementAnimation();
        UpdateTurnAnimation(Time.deltaTime);
        UpdateDashAnimation(isDashing);
        UpdateShadowDashAnimation(isDashing);
        UpdateWeaponLayer(isDashing);
    }

    private void UpdateMovementAnimation()
    {
        Vector3 horizontalVelocity = characterController.velocity;
        if (!playerMovement.isActiveAndEnabled || playerMovement.IsInteractionLocked ||
            playerMovement.IsDashing || playerMovement.IsShadowDashing)
        {
            horizontalVelocity = Vector3.zero;
        }

        UpdateLocomotionParameters(horizontalVelocity, Time.deltaTime);
    }

    private void UpdateLocomotionParameters(Vector3 horizontalVelocity, float deltaTime)
    {
        horizontalVelocity.y = 0f;
        float speed = horizontalVelocity.magnitude;
        // Gait follows actual speed, including acceleration and braking. A tap
        // stays in Walk; holding movement reaches Run; Shift reaches Sprint.
        float normalizedSpeed = speed <= playerMovement.WalkSpeed
            ? Mathf.InverseLerp(0f, playerMovement.WalkSpeed, speed) * 0.25f
            : speed <= playerMovement.RunSpeed
                ? Mathf.Lerp(0.25f, 0.65f, Mathf.InverseLerp(playerMovement.WalkSpeed, playerMovement.RunSpeed, speed))
                : Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(playerMovement.RunSpeed, playerMovement.SprintSpeed, speed));

        animator.SetFloat(
            MoveSpeedHash,
            normalizedSpeed,
            dampTime,
            deltaTime
        );

        Vector3 localDirection = horizontalVelocity.sqrMagnitude > 0.0001f
            ? transform.InverseTransformDirection(horizontalVelocity.normalized)
            : Vector3.zero;
        if(localDirection.sqrMagnitude>.001f)
        {
            float desired=Mathf.Atan2(localDirection.x,localDirection.z)*Mathf.Rad2Deg;
            movementAngle=hasMovementAngle?Mathf.LerpAngle(movementAngle,desired,1f-Mathf.Exp(-deltaTime/Mathf.Max(.001f,directionDampTime))):desired;
            hasMovementAngle=true;
            // Blend around the unit circle, so turns never blend through the idle pose at (0,0).
            localDirection=new Vector3(Mathf.Sin(movementAngle*Mathf.Deg2Rad),0,Mathf.Cos(movementAngle*Mathf.Deg2Rad));
        }
        else hasMovementAngle=false;
        animator.SetFloat(MoveXHash, localDirection.x);
        animator.SetFloat(MoveZHash, localDirection.z);
        float authoredSpeed=speed<=playerMovement.WalkSpeed?walkAnimationSpeed:
            speed<=playerMovement.RunSpeed?Mathf.Lerp(walkAnimationSpeed,runAnimationSpeed,Mathf.InverseLerp(playerMovement.WalkSpeed,playerMovement.RunSpeed,speed)):
            Mathf.Lerp(runAnimationSpeed,sprintAnimationSpeed,Mathf.InverseLerp(playerMovement.RunSpeed,playerMovement.SprintSpeed,speed));
        animator.SetFloat(LocomotionRateHash,speed<.1f?1:Mathf.Clamp(speed/authoredSpeed,.7f,1.6f));
    }

    private void UpdateTurnAnimation(float deltaTime)
    {
        float yaw = transform.eulerAngles.y;
        float angularSpeed = deltaTime > 0f ? Mathf.DeltaAngle(previousFacingYaw, yaw) / deltaTime : 0f;
        previousFacingYaw = yaw;
        bool suppressed = !playerMovement.isActiveAndEnabled || playerMovement.IsDashing ||
                          playerMovement.IsShadowDashing || playerMovement.IsInteractionLocked ||
                          (meleeAttack != null && meleeAttack.IsAttacking) || hasGun;
        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;
        float turn = suppressed || Mathf.Abs(angularSpeed) < 8f ? 0f : Mathf.Clamp(angularSpeed / turnFullSpeed, -1f, 1f);
        // At full running speed the directional gait handles the turn. The
        // planted Turn clips are strongest at low speed, not during a sprint.
        float lowSpeedWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(velocity.magnitude / playerMovement.RunSpeed));
        float blend = suppressed ? 0f : Mathf.Abs(turn) * lowSpeedWeight;
        animator.SetFloat(TurnAmountHash, turn, turnDampTime, deltaTime);
        animator.SetFloat(TurnBlendHash, blend, turnDampTime, deltaTime);
    }

    private void UpdateDashAnimation(bool isDashing)
    {
        if (isDashing && !wasDashing)
        {
            animator.SetTrigger(DashHash);
        }

        wasDashing = isDashing;
    }

    private void UpdateWeaponLayer(bool isDashing)
    {
        bool fullBodyAction = isDashing || playerMovement.IsShadowDashing ||
                              IsDashLanding || (dashVisual != null && dashVisual.IceForm != null && dashVisual.IceForm.IsEntering) ||
                              (meleeAttack != null && meleeAttack.IsAttacking) ||
                              (playerInteractor != null && playerInteractor.IsInteracting);
        BlendWeaponLayer(weaponLayerIndex, hasGun && !fullBodyAction ? 1f : 0f);
        BlendWeaponLayer(meleeLayerIndex, hasMeleeWeapon && !hasGun && !fullBodyAction ? 1f : 0f);
    }

    private void UpdateShadowDashAnimation(bool shortDashing)
    {
        ShadowDashIceForm form = dashVisual != null ? dashVisual.IceForm : null;
        if (form == null) return;
        if (shortDashing)
        {
            shadowEntrySequence = form.EntrySequence;
            shadowLandingSequence = form.LandingSequence;
            return; // The AnyState Shield Dash transition owns the base layer.
        }
        if (form.EntrySequence != shadowEntrySequence)
        {
            shadowEntrySequence = form.EntrySequence;
            TryPlayFullBodyState(ShadowRollHash);
        }
        if (form.LandingSequence != shadowLandingSequence)
        {
            shadowLandingSequence = form.LandingSequence;
            TryPlayFullBodyState(ShadowLandHash);
        }

        AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == ShadowRollHash && !form.IsEntering)
            animator.CrossFadeInFixedTime(LocomotionStateHash, 0.08f, 0);
        if (state.fullPathHash == ShadowLandHash)
        {
            Vector3 velocity = characterController.velocity; velocity.y = 0f;
            // Keep the impact/compression, then blend early into locomotion if
            // already moving. Standing recovery gets the longer settling pose.
            float exitAt = velocity.sqrMagnitude > 0.25f ? 0.48f : 0.82f;
            if (state.normalizedTime >= exitAt || (!form.IsReturning && !playerMovement.isActiveAndEnabled))
                animator.CrossFadeInFixedTime(LocomotionStateHash, 0.12f, 0);
        }
    }

    private void BlendWeaponLayer(int layerIndex, float targetWeight)
    {
        if (layerIndex < 0) return;

        float currentWeight =
            animator.GetLayerWeight(layerIndex);

        float nextWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            layerBlendSpeed * Time.deltaTime
        );

        animator.SetLayerWeight(
            layerIndex,
            nextWeight
        );
    }

    private void HandleRangedShotFired(Targetable target)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            !hasGun ||
            playerMovement.IsDashing ||
            playerMovement.IsShadowDashing ||
            (playerInteractor != null && playerInteractor.IsInteracting) ||
            (meleeAttack != null && meleeAttack.IsAttacking))
        {
            return;
        }

        animator.SetTrigger(ShootHash);
    }

    public void SetHasGun(bool value)
    {
        hasGun = value;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(HasGunHash, hasGun);
        }
    }

    public bool TryPlayPunch()
    {
        int nextPunchStateHash = nextPunchIsCross
            ? CrossPunchStateHash
            : PunchStateHash;

        if (!TryPlayFullBodyState(nextPunchStateHash))
        {
            return false;
        }

        nextPunchIsCross = !nextPunchIsCross;
        return true;
    }

    public bool TryPlayMeleeAttack(bool armed)
    {
        return armed ? TryPlayFullBodyState(MeleeWeaponStateHash) : TryPlayPunch();
    }

    public void SetHasMeleeWeapon(bool value)
    {
        hasMeleeWeapon = value;
    }

    public bool TryPlayPickup()
    {
        return TryPlayFullBodyState(PickupStateHash);
    }

    private bool TryPlayFullBodyState(int stateHash)
    {
        if (deathPlaying) return false;
        if (stateHash != ShadowRollHash && stateHash != ShadowLandHash &&
            (IsDashLanding || (dashVisual != null && dashVisual.IceForm != null && dashVisual.IceForm.IsReturning)))
            return false; // Melee/pickup must not replace the landing on its first frame.
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null ||
            !animator.HasState(0, stateHash))
        {
            return false;
        }

        animator.ResetTrigger(ShootHash);

        // ท่าโจมตีและหยิบของใช้ทั้งตัว จึงปิดท่าถืออาวุธตั้งแต่เฟรมแรก
        if (weaponLayerIndex >= 0)
        {
            animator.SetLayerWeight(weaponLayerIndex, 0f);
        }
        if (meleeLayerIndex >= 0)
        {
            animator.SetLayerWeight(meleeLayerIndex, 0f);
        }

        animator.CrossFadeInFixedTime(stateHash, 0.03f, 0, 0f);
        return true;
    }

    public bool TryGetMeleeProgress(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null)
        {
            return false;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);

            if (IsMeleeState(nextState.fullPathHash))
            {
                normalizedTime = nextState.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

        if (!IsMeleeState(currentState.fullPathHash))
        {
            return false;
        }

        normalizedTime = currentState.normalizedTime;
        return true;
    }

    private static bool IsMeleeState(int stateHash)
    {
        return stateHash == PunchStateHash || stateHash == CrossPunchStateHash ||
               stateHash == MeleeWeaponStateHash;
    }

    public void CancelMeleeAttack()
    {
        if (deathPlaying) return;
        if (!TryGetMeleeProgress(out _) ||
            (playerMovement != null && playerMovement.IsDashing))
        {
            return;
        }

        animator.CrossFadeInFixedTime(LocomotionStateHash, 0.05f, 0);
    }

    public bool TryGetPickupProgress(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null)
        {
            return false;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (nextState.fullPathHash == PickupStateHash)
            {
                normalizedTime = nextState.normalizedTime;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.fullPathHash != PickupStateHash) return false;
        normalizedTime = currentState.normalizedTime;
        return true;
    }

    public void CancelPickup()
    {
        if (deathPlaying) return;
        if (TryGetPickupProgress(out _))
        {
            animator.CrossFadeInFixedTime(LocomotionStateHash, 0.05f, 0);
        }
    }

    public bool PlayDeath()
    {
        deathPlaying = true;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            !animator.HasState(0, DeathStateHash)) return false;
        hasGun = hasMeleeWeapon = false;
        animator.ResetTrigger(ShootHash); animator.ResetTrigger(DashHash); animator.SetBool(HasGunHash, false);
        animator.SetFloat(MoveSpeedHash, 0); animator.SetFloat(MoveXHash, 0); animator.SetFloat(MoveZHash, 0);
        animator.SetFloat(TurnBlendHash, 0); animator.SetFloat(TurnAmountHash, 0);
        for (int layer=1; layer<animator.layerCount; layer++) animator.SetLayerWeight(layer, 0);
        // Game Over freezes the world immediately; only this one-shot pose uses real time.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false; animator.speed = 1;
        animator.CrossFadeInFixedTime(DeathStateHash, .08f, 0, 0);
        return true;
    }
}
