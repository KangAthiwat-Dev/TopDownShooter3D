using UnityEngine;

// Adapts the existing rifle animation to the proportions of this character.
// Runs after the locomotion spine correction, before the weapon commits a shot.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(175)]
public class PlayerRifleHandIK : MonoBehaviour
{
    [SerializeField] private Transform gunRoot;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;

    [Header("Rifle Aim")]
    [SerializeField, Min(1f)] private float aimRotationSpeed = 720f;
    [SerializeField, Range(1f, 80f)] private float maxAimYaw = 35f;
    [SerializeField, Range(1f, 80f)] private float maxAimPitch = 30f;

    private Animator animator;
    private Transform rightUpper, rightLower, rightHand;
    private Transform leftUpper, leftLower, leftHand;
    private int weaponLayer = -1;
    private PlayerCombatFacing combatFacing;
    private PlayerRangedWeapon rangedWeapon;
    private Quaternion aimCorrection = Quaternion.identity;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combatFacing = GetComponentInParent<PlayerCombatFacing>();
        rangedWeapon = GetComponentInParent<PlayerRangedWeapon>();
        if (!animator.isHuman) return;
        rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        weaponLayer = animator.GetLayerIndex("WeaponUpperBody");
    }

    private void LateUpdate()
    {
        if (animator == null || !animator.isActiveAndEnabled || gunRoot == null ||
            !gunRoot.gameObject.activeInHierarchy || weaponLayer < 0)
        {
            aimCorrection = Quaternion.identity;
            return;
        }
        float weight = animator.GetLayerWeight(weaponLayer);
        if (weight <= 0.001f) return;
        // Position the rifle closer to the torso. Use the animated wrist pose
        // as the base, then align the barrel before solving the support hand.
        Solve(rightUpper, rightLower, rightHand, rightHandTarget, weight, false);
        UpdateRifleAim(weight);
        // Its target is attached to the rifle, so it follows recoil as well.
        Solve(leftUpper, leftLower, leftHand, leftHandTarget, weight, true);
    }

    private void UpdateRifleAim(float weight)
    {
        Transform muzzle = rangedWeapon != null ? rangedWeapon.MuzzlePoint : null;
        if (rightHand == null || muzzle == null || combatFacing == null) return;
        Transform playerRoot = combatFacing.transform;
        Targetable target = combatFacing.AimTarget;
        Quaternion desiredCorrection = Quaternion.identity;
        if (target != null)
        {
            Vector3 localDirection = playerRoot.InverseTransformDirection(target.AimPoint.position - rightHand.position);
            float distance = localDirection.magnitude;
            float yaw = Mathf.Clamp(Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg, -maxAimYaw, maxAimYaw);
            float pitch = Mathf.Clamp(Mathf.Atan2(localDirection.y,
                new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg, -maxAimPitch, maxAimPitch);
            Vector3 aimPoint = rightHand.position + playerRoot.rotation *
                (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * distance);

            Quaternion animatedWrist = rightHand.rotation;
            // Account for the offset between the wrist pivot and the barrel.
            // Solve a desired rotation first, then smooth its root-local offset.
            for (int i = 0; i < 3; i++)
            {
                Vector3 direction = aimPoint - muzzle.position;
                if (direction.sqrMagnitude > 0.0001f)
                    rightHand.rotation = Quaternion.FromToRotation(muzzle.forward, direction) * rightHand.rotation;
            }
            Quaternion worldCorrection = rightHand.rotation * Quaternion.Inverse(animatedWrist);
            desiredCorrection = Quaternion.Inverse(playerRoot.rotation) * worldCorrection * playerRoot.rotation;
            rightHand.rotation = animatedWrist;
        }

        aimCorrection = Quaternion.RotateTowards(aimCorrection, desiredCorrection, aimRotationSpeed * Time.deltaTime);
        Quaternion correction = playerRoot.rotation * Quaternion.Slerp(Quaternion.identity, aimCorrection, weight) *
                                Quaternion.Inverse(playerRoot.rotation);
        rightHand.rotation = correction * rightHand.rotation;
    }

    private void OnDisable() => aimCorrection = Quaternion.identity;

    private static void Solve(Transform upper, Transform lower, Transform hand, Transform target, float weight, bool matchRotation)
    {
        if (upper == null || lower == null || hand == null || target == null) return;
        Quaternion upperBefore = upper.localRotation, lowerBefore = lower.localRotation, handBefore = hand.localRotation;
        Quaternion wristWorld = hand.rotation;
        Vector3 start = upper.position;
        float upperLength = Vector3.Distance(start, lower.position), lowerLength = Vector3.Distance(lower.position, hand.position);
        Vector3 towards = target.position - start;
        if (towards.sqrMagnitude < 0.000001f || upperLength < 0.001f || lowerLength < 0.001f) return;
        float distance = Mathf.Clamp(towards.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
        Vector3 direction = towards.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(lower.position - start, direction);
        if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(Vector3.down, direction);
        if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(Vector3.right, direction);
        bend.Normalize();
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 elbow = start + direction * along + bend * height;
        Vector3 goal = start + direction * distance;
        upper.rotation = Quaternion.FromToRotation(lower.position - start, elbow - start) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * lower.rotation;
        hand.rotation = matchRotation ? target.rotation : wristWorld;
        upper.localRotation = Quaternion.Slerp(upperBefore, upper.localRotation, weight);
        lower.localRotation = Quaternion.Slerp(lowerBefore, lower.localRotation, weight);
        hand.localRotation = Quaternion.Slerp(handBefore, hand.localRotation, weight);
    }
}
