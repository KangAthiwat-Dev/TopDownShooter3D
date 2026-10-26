using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMeleeEquipment : MonoBehaviour
{
    public const int Capacity = 2;

    [SerializeField] private Transform weaponHand;
    [SerializeField] private Transform weaponBack;
    [SerializeField] private Vector3 secondBackOffset = new Vector3(-0.48f, 0f, -0.06f);
    [SerializeField] private Vector3 secondBackEulerAngles = new Vector3(0f, 0f, -70f);

    private readonly MeleeWeaponDefinition[] weapons = new MeleeWeaponDefinition[Capacity];
    private readonly GameObject[] weaponVisuals = new GameObject[Capacity];
    private PlayerMeleeAttack meleeAttack;
    private PlayerInteractor playerInteractor;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private bool weaponsVisible;

    public int SelectedSlotIndex { get; private set; } = -1;
    public int WeaponCount { get; private set; }
    public MeleeWeaponDefinition CurrentWeapon => GetWeapon(SelectedSlotIndex);
    public bool HasWeapon => CurrentWeapon != null;
    public bool IsDrawn { get; private set; }
    public event Action<MeleeWeaponDefinition> WeaponChanged;
    public event Action SlotsChanged;

    private void Awake()
    {
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        playerInteractor = GetComponent<PlayerInteractor>();
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        Animator animator = GetComponentInChildren<Animator>();
        if (weaponHand == null && animator != null && animator.isHuman)
        {
            weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (weaponBack == null)
        {
            Transform torso = null;
            if (animator != null && animator.isHuman)
            {
                torso = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                        animator.GetBoneTransform(HumanBodyBones.Chest) ??
                        animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            weaponBack = new GameObject("MeleeBackSocket").transform;
            weaponBack.SetParent(torso != null ? torso : transform, false);
            weaponBack.localPosition = torso != null ? Vector3.zero : Vector3.up * 1.35f;
            // Imported rigs have different bone axes. Keep back offsets in player axes
            // at rest, while still following the animated torso afterwards.
            weaponBack.rotation = transform.rotation;
        }
    }

    public bool CanEquip(MeleeWeaponDefinition weapon)
    {
        return isActiveAndEnabled && WeaponCount < Capacity &&
               weapon != null && weapon.HeldPrefab != null && weaponHand != null;
    }

    public MeleeWeaponDefinition GetWeapon(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < Capacity ? weapons[slotIndex] : null;
    }

    public bool TryEquip(MeleeWeaponDefinition weapon)
    {
        if (!CanEquip(weapon))
        {
            return false;
        }

        int slotIndex = Array.FindIndex(weapons, stored => stored == null);
        GameObject visual = Instantiate(weapon.HeldPrefab, weaponBack);
        weapons[slotIndex] = weapon;
        weaponVisuals[slotIndex] = visual;
        WeaponCount++;

        bool firstWeapon = SelectedSlotIndex < 0;
        if (firstWeapon) SelectedSlotIndex = slotIndex;
        ApplyWeaponPose(slotIndex);
        visual.SetActive(weaponsVisible);
        if (firstWeapon) WeaponChanged?.Invoke(CurrentWeapon);
        SlotsChanged?.Invoke();
        return true;
    }

    public bool TrySelectSlot(int slotIndex)
    {
        if (!CanSelectSlot(slotIndex)) return false;
        if (SelectedSlotIndex == slotIndex) return true;
        SelectedSlotIndex = slotIndex;
        RefreshWeaponPoses();
        WeaponChanged?.Invoke(CurrentWeapon);
        SlotsChanged?.Invoke();
        return true;
    }

    public bool CanSelectSlot(int slotIndex)
    {
        return !(!isActiveAndEnabled || GetWeapon(slotIndex) == null ||
            (meleeAttack != null && meleeAttack.IsAttacking) ||
            (playerInteractor != null && playerInteractor.IsInteracting) ||
            (playerHealth != null && !playerHealth.IsAlive) ||
            (playerMovement != null &&
             (!playerMovement.isActiveAndEnabled || playerMovement.IsDashing || playerMovement.IsShadowDashing)));
    }

    public void SetWeaponDrawn(bool drawn)
    {
        drawn &= HasWeapon;
        if (IsDrawn == drawn)
        {
            return;
        }

        IsDrawn = drawn;
        RefreshWeaponPoses();
    }

    private void RefreshWeaponPoses()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (weaponVisuals[i] != null) ApplyWeaponPose(i);
        }
    }

    private void ApplyWeaponPose(int slotIndex)
    {
        bool drawn = IsDrawn && slotIndex == SelectedSlotIndex;
        MeleeWeaponDefinition weapon = weapons[slotIndex];
        Transform visualTransform = weaponVisuals[slotIndex].transform;
        visualTransform.SetParent(drawn ? weaponHand : weaponBack, false);
        visualTransform.localPosition = drawn ? weapon.GripPosition : weapon.BackPosition;
        visualTransform.localRotation = drawn ? weapon.GripRotation : weapon.BackRotation;
        if (!drawn && slotIndex == 1)
        {
            visualTransform.localPosition += secondBackOffset;
            visualTransform.localRotation = Quaternion.Euler(secondBackEulerAngles) * visualTransform.localRotation;
        }
    }

    public void SetWeaponVisible(bool visible)
    {
        weaponsVisible = visible;
        foreach (GameObject visual in weaponVisuals)
        {
            if (visual != null && visual.activeSelf != visible) visual.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        SetWeaponVisible(false);
    }
}
