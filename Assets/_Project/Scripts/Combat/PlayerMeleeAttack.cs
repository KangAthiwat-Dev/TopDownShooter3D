using System;
using UnityEngine;

[RequireComponent(typeof(PlayerAttackController))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAttackController attackController;

    [Header("Unarmed Attack")]
    [SerializeField, Min(0f)] private float unarmedDamage = 15f;
    [SerializeField, Min(0.1f)] private float hitRange = 2.2f;

    [Header("Animation Timing")]
    [SerializeField, Range(0f, 1f)] private float hitNormalizedTime = 0.35f;
    [SerializeField, Min(0.1f)] private float animationStartTimeout = 0.5f;

    [Header("Runtime")]
    [SerializeField] private bool attackInProgress;

    private Targetable pendingTarget;
    private PlayerAnimationController animationController;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private bool animationStarted;
    private bool hitResolved;
    private float animationStartDeadline;
    private PlayerMeleeEquipment equipment;
    private PlayerInteractor playerInteractor;
    private PlayerInputGate inputGate;
    private float activeDamage;
    private bool activeKillsInOneHit;
    private float activeHitRange;
    private float activeHitTime;
    private bool activeWeaponAttack;
    private PlayerCombatSkills skills;
    private CombatHitSettings hitSettings;
    public event Action<bool> SwingCommitted;

    public bool IsAttacking => attackInProgress;
    public bool IsWeaponAttack => attackInProgress && activeWeaponAttack;
    public int AttackSequence { get; private set; }

    // เก็บไว้ใช้ต่อกับเสียง, VFX และ Animation ในอนาคต
    public event Action<Targetable> MeleeHitConfirmed;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController = GetComponent<PlayerAttackController>();
        }

        animationController = GetComponent<PlayerAnimationController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        equipment = GetComponent<PlayerMeleeEquipment>();
        playerInteractor = GetComponent<PlayerInteractor>();
        inputGate = GetComponent<PlayerInputGate>();
        skills = GetComponent<PlayerCombatSkills>();
    }

    private void OnEnable()
    {
        attackController.MeleeAttackRequested += HandleMeleeAttack;

        if (playerHealth != null)
        {
            playerHealth.Died += CancelAttack;
        }
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.MeleeAttackRequested -= HandleMeleeAttack;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= CancelAttack;
        }

        CancelAttack();
    }

    private void LateUpdate()
    {
        if (inputGate != null && inputGate.BlocksGameplay) return;
        if (!attackInProgress)
        {
            return;
        }

        if (!CanContinueAttack())
        {
            CancelAttack();
            return;
        }

        if (IsUsableTarget(pendingTarget))
        {
            FaceTarget(pendingTarget);
        }

        if (!animationController.TryGetMeleeProgress(out float progress))
        {
            if (animationStarted || Time.time >= animationStartDeadline)
            {
                CancelAttack();
            }

            return;
        }

        animationStarted = true;

        // ใช้จังหวะจากคลิปจริง เพื่อให้เปลี่ยนความเร็วท่าได้โดย damage ยังตรงกัน
        if (!hitResolved && progress >= activeHitTime)
        {
            hitResolved = true;
            SwingCommitted?.Invoke(activeWeaponAttack);

            if (IsTargetInHitRange(pendingTarget))
            {
                Targetable hitTarget = pendingTarget;
                float damage = activeKillsInOneHit && hitTarget.Health.AllowInstantKill
                    ? hitTarget.Health.CurrentHealth : activeDamage;
                if(skills!=null)skills.DealDamage(hitTarget.Health,damage,hitSettings,activeKillsInOneHit);
                else hitTarget.Health.TakeDamage(damage, gameObject);
                MeleeHitConfirmed?.Invoke(hitTarget);
            }
        }
    }

    private void HandleMeleeAttack(Targetable target)
    {
        MeleeWeaponDefinition weapon = equipment != null ? equipment.CurrentWeapon : null;
        float requestedRange = weapon != null ? weapon.HitRange : hitRange;
        if (attackInProgress ||
            !CanContinueAttack() ||
            !IsTargetInHitRange(target, requestedRange) ||
            !animationController.TryPlayMeleeAttack(weapon != null))
        {
            return;
        }

        attackInProgress = true;
        AttackSequence++;
        activeDamage = weapon != null ? weapon.Damage : unarmedDamage;
        activeWeaponAttack = weapon != null;
        hitSettings = skills != null ? skills.HitSettings : default;
        activeKillsInOneHit = weapon != null && weapon.KillsInOneHit;
        activeHitRange = requestedRange;
        activeHitTime = weapon != null ? weapon.HitNormalizedTime : hitNormalizedTime;
        pendingTarget = target;
        animationStarted = false;
        hitResolved = false;
        animationStartDeadline = Time.time + animationStartTimeout;
        FaceTarget(target);
    }

    private void CancelAttack()
    {
        bool wasAttacking = attackInProgress;
        pendingTarget = null;
        attackInProgress = false;
        animationStarted = false;
        hitResolved = false;

        if (wasAttacking && animationController != null)
        {
            animationController.CancelMeleeAttack();
        }
    }

    private bool CanContinueAttack()
    {
        return attackController != null &&
               attackController.isActiveAndEnabled &&
               animationController != null &&
               animationController.isActiveAndEnabled &&
               (playerInteractor == null || !playerInteractor.IsInteracting) &&
               (playerHealth == null || playerHealth.IsAlive) &&
               (playerMovement == null ||
                (playerMovement.isActiveAndEnabled &&
                 !playerMovement.IsDashing &&
                 !playerMovement.IsShadowDashing));
    }

    private bool IsUsableTarget(Targetable target)
    {
        return target != null && target.IsTargetable;
    }

    private bool IsTargetInHitRange(Targetable target)
    {
        return IsTargetInHitRange(target, activeHitRange);
    }

    private bool IsTargetInHitRange(Targetable target, float range)
    {
        if (!IsUsableTarget(target))
        {
            return false;
        }

        return target.HorizontalDistanceSquared(transform.position) <= range * range;
    }

    private void FaceTarget(Targetable target)
    {
        if (!IsUsableTarget(target))
        {
            return;
        }

        Vector3 direction = target.AimPoint.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}
