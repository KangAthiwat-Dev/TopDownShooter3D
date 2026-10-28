using System;
using UnityEngine;

// Fire after Animator, spine correction (150) and rifle hand/aim IK (175).
[RequireComponent(typeof(PlayerAttackController))]
[DefaultExecutionOrder(200)]
public class PlayerRangedWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerAttackController attackController;

    [SerializeField]
    private Projectile projectilePrefab;

    [SerializeField]
    private Transform muzzlePoint;

    [Header("Weapon Stats")]
    [SerializeField, Min(0f)]
    private float damage = 8f;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 18f;

    [Header("Aim Readiness")]
    [SerializeField, Range(0.1f, 15f)] private float maxBodyAimError = 5f;
    [SerializeField, Range(0.1f, 10f)] private float maxMuzzleAimError = 2f;

    private PlayerCombatFacing combatFacing;
    private PlayerAnimationController animationController;
    private Targetable pendingTarget;
    [Header("Skill Projectiles")]
    [SerializeField] private Projectile piercingProjectile;
    [SerializeField] private Projectile explosiveProjectile;
    private PlayerCombatSkills skills;

    public Transform MuzzlePoint => muzzlePoint;
    public float BaseDamage => damage;
    public float Damage => damage * Mathf.Max(1f,DamageMultiplier);
    public float DamageMultiplier { get; set; } = 1f;
    public Targetable PendingTarget => pendingTarget;
    public event Action<Projectile> ProjectileFired;
    public event Action<Projectile> ProjectileSpawned;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController =
                GetComponent<PlayerAttackController>();
        }
        combatFacing = GetComponent<PlayerCombatFacing>();
        animationController = GetComponent<PlayerAnimationController>();
        skills = GetComponent<PlayerCombatSkills>();
    }

    private void OnEnable()
    {
        attackController.RangedAttackRequested +=
            HandleRangedAttack;
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.RangedAttackRequested -=
                HandleRangedAttack;
        }
        CancelPendingShot();
    }

    private void HandleRangedAttack(
        Targetable target
    )
    {
        pendingTarget = target;
    }

    public void CancelPendingShot() => pendingTarget = null;

    private void LateUpdate()
    {
        if (pendingTarget == null) return;
        if (attackController == null || !attackController.IsAttackHeld ||
            attackController.CurrentMode != AttackMode.Ranged ||
            attackController.CurrentAttackTarget != pendingTarget || !pendingTarget.IsTargetable ||
            combatFacing == null || combatFacing.AimTarget != pendingTarget)
        {
            CancelPendingShot();
            return;
        }

        if (projectilePrefab == null || muzzlePoint == null || !muzzlePoint.gameObject.activeInHierarchy ||
            animationController == null || !animationController.IsRangedPoseReady) return;

        Vector3 bodyDirection = pendingTarget.AimPoint.position - transform.position;
        bodyDirection.y = 0f;
        Vector3 muzzleDirection = pendingTarget.AimPoint.position - muzzlePoint.position;
        if (bodyDirection.sqrMagnitude < 0.0001f || muzzleDirection.sqrMagnitude < 0.0001f ||
            Vector3.Angle(transform.forward, bodyDirection) > maxBodyAimError ||
            Vector3.Angle(muzzlePoint.forward, muzzleDirection) > maxMuzzleAimError) return;

        Targetable shotTarget = pendingTarget;
        pendingTarget = null;
        // Capture the FINAL barrel pose. Never steer this projectile towards a
        // target or overwrite its direction with a separate auto-aim vector.
        Vector3 direction = muzzlePoint.forward;
        int count=skills!=null?skills.ProjectilesPerVolley:1;
        int middle=count/2;
        Projectile center=null;
        var prefab=skills!=null&&skills.BlastRadius>0&&explosiveProjectile!=null?explosiveProjectile:
            skills!=null&&skills.ExtraPierceTargets>0&&piercingProjectile!=null?piercingProjectile:projectilePrefab;
        for(int i=0;i<count;i++)
        {
            // Spread rotates the final barrel pose. Projectiles never seek a target.
            Vector3 spread=Quaternion.AngleAxis((i-middle)*(skills!=null?skills.SpreadDegrees:0),Vector3.up)*direction;
            Projectile projectile=Instantiate(prefab,muzzlePoint.position,Quaternion.LookRotation(spread,muzzlePoint.up));
            projectile.Initialize(spread,projectileSpeed,Damage*(i==middle||skills==null?1:skills.SideBulletDamage),gameObject);
            if(skills!=null)projectile.ConfigureSkills(skills,skills.ExtraPierceTargets,skills.BlastRadius,skills.BlastDamageRatio,skills.HitSettings);
            ProjectileSpawned?.Invoke(projectile);if(i==middle)center=projectile;
        }
        attackController.ConfirmRangedShot(shotTarget);
        // Cadence, muzzle/audio and tutorial still receive one event per volley.
        ProjectileFired?.Invoke(center);
    }
}
