using UnityEngine;

// Kevin's locomotion clips animate a separate spine proxy so the upper body can be
// independent of hip rotation when combined with a masked weapon pose.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(150)]
public class PlayerLocomotionSpineProxy : MonoBehaviour
{
    [SerializeField] private Transform animatedProxy;
    [SerializeField] private float proxyYawCorrection = 180f;

    private Animator animator;
    private Transform spine;
    private Quaternion rotationOffset;
    private PlayerMovement movement;
    private int gunLayer = -1;
    private int meleeLayer = -1;
    private static readonly int LocomotionHash = Animator.StringToHash("Base Layer.Locomotion");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<PlayerMovement>();
        if (!animator.isHuman || animatedProxy == null) return;
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (spine == null) return;
        rotationOffset = Quaternion.Inverse(animatedProxy.rotation) * spine.rotation;
        gunLayer = animator.GetLayerIndex("WeaponUpperBody");
        meleeLayer = animator.GetLayerIndex("MeleeUpperBody");
    }

    private void LateUpdate()
    {
        if (spine == null || animatedProxy == null || !animator.isActiveAndEnabled ||
            (movement != null && (!movement.isActiveAndEnabled || movement.IsDashing ||
                                 movement.IsShadowDashing || movement.IsInteractionLocked))) return;
        if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != LocomotionHash ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash != LocomotionHash)) return;

        float weaponWeight = Mathf.Max(gunLayer >= 0 ? animator.GetLayerWeight(gunLayer) : 0f,
                                       meleeLayer >= 0 ? animator.GetLayerWeight(meleeLayer) : 0f);
        // Idle, Walk, Run, Sprint and Turn now all provide the proxy track.
        float weight = weaponWeight;
        if (weight <= 0f) return;

        // Copied locomotion clips have a 180-degree humanoid orientation correction;
        // the generic proxy curve needs the same correction in world space.
        Quaternion target = Quaternion.AngleAxis(proxyYawCorrection, transform.up) * animatedProxy.rotation * rotationOffset;
        spine.rotation = Quaternion.Slerp(spine.rotation, target, weight);
    }
}
