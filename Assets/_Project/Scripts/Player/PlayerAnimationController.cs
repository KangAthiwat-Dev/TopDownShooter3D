using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAttackController attackController;

    [Header("Movement Animation")]
    [SerializeField, Min(0.01f)] private float sprintSpeed = 7f;
    [SerializeField, Min(0f)] private float dampTime = 0.1f;

    [Header("Weapon Animation")]
    [SerializeField] private bool hasGun = true;
    [SerializeField] private string weaponLayerName = "WeaponUpperBody";
    [SerializeField, Min(0f)] private float layerBlendSpeed = 12f;

    private static readonly int MoveSpeedHash =
        Animator.StringToHash("MoveSpeed");

    private static readonly int DashHash =
        Animator.StringToHash("Dash");

    private static readonly int HasGunHash =
        Animator.StringToHash("HasGun");

    private static readonly int ShootHash =
        Animator.StringToHash("Shoot");

    private bool wasDashing;
    private int weaponLayerIndex = -1;

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

        weaponLayerIndex = animator.GetLayerIndex(weaponLayerName);
    }

    private void OnEnable()
    {
        if (attackController != null)
        {
            attackController.RangedAttackRequested += HandleRangedAttack;
        }
    }

    private void Start()
    {
        animator.SetBool(HasGunHash, hasGun);

        if (weaponLayerIndex >= 0)
        {
            animator.SetLayerWeight(
                weaponLayerIndex,
                hasGun ? 1f : 0f
            );
        }
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.RangedAttackRequested -= HandleRangedAttack;
        }
    }

    private void LateUpdate()
    {
        bool isDashing = playerMovement.IsDashing;

        UpdateMovementAnimation();
        UpdateDashAnimation(isDashing);
        UpdateWeaponLayer(isDashing);
    }

    private void UpdateMovementAnimation()
    {
        Vector3 horizontalVelocity = characterController.velocity;
        horizontalVelocity.y = 0f;

        float normalizedSpeed = Mathf.Clamp01(
            horizontalVelocity.magnitude / sprintSpeed
        );

        animator.SetFloat(
            MoveSpeedHash,
            normalizedSpeed,
            dampTime,
            Time.deltaTime
        );
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
        if (weaponLayerIndex < 0)
        {
            return;
        }

        float targetWeight =
            hasGun && !isDashing ? 1f : 0f;

        float currentWeight =
            animator.GetLayerWeight(weaponLayerIndex);

        float nextWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            layerBlendSpeed * Time.deltaTime
        );

        animator.SetLayerWeight(
            weaponLayerIndex,
            nextWeight
        );
    }

    private void HandleRangedAttack(Targetable target)
    {
        if (!hasGun || playerMovement.IsDashing)
        {
            return;
        }

        animator.SetTrigger(ShootHash);
    }

    public void SetHasGun(bool value)
    {
        hasGun = value;
        animator.SetBool(HasGunHash, hasGun);
    }
}