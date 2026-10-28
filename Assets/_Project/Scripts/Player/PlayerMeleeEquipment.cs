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
    private bool purchaseInProgress;

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
        return !purchaseInProgress && isActiveAndEnabled && WeaponCount < Capacity &&
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

    // Shop owns the stock reservation. Both equipment and currency are updated before events.
    internal bool TryPurchase(MeleeWeaponDefinition weapon, int slot, int price, PlayerGoldWallet wallet)
    {
        if (purchaseInProgress || !isActiveAndEnabled || weapon == null || weapon.HeldPrefab == null ||
            weaponHand == null || slot < 0 || slot >= Capacity || wallet == null || wallet.gameObject != gameObject ||
            price <= 0 || wallet.Gold < price || (playerHealth != null && !playerHealth.IsAlive) ||
            (meleeAttack != null && meleeAttack.IsAttacking) || (playerInteractor != null && playerInteractor.IsInteracting)) return false;
        purchaseInProgress = true;
        try
        {
            // Instantiate first so a missing/unusable visual cannot consume Gold or the old weapon.
            GameObject visual = Instantiate(weapon.HeldPrefab, weaponBack);
            GameObject previous = weaponVisuals[slot];
            bool first = SelectedSlotIndex < 0;
            if (weapons[slot] == null) WeaponCount++;
            weapons[slot] = weapon; weaponVisuals[slot] = visual;
            if (first) SelectedSlotIndex = slot;
            ApplyWeaponPose(slot); visual.SetActive(weaponsVisible);
            if (previous != null) { previous.SetActive(false); Destroy(previous); }
            wallet.DebitPurchase(price);
            wallet.Commit(); // One currency record also contains the resulting two melee slots.
            if (SelectedSlotIndex == slot) WeaponChanged?.Invoke(CurrentWeapon);
            SlotsChanged?.Invoke();
            return true;
        }
        finally { purchaseInProgress = false; }
    }

    internal void RestoreLoadout(MeleeWeaponDefinition[] loadout, int selected)
    {
        if (purchaseInProgress || loadout == null || loadout.Length != Capacity || weaponHand == null) return;
        for (int i = 0; i < Capacity; i++)
        {
            if (weaponVisuals[i] != null) { weaponVisuals[i].SetActive(false); Destroy(weaponVisuals[i]); }
            weapons[i] = loadout[i];
            weaponVisuals[i] = loadout[i] != null && loadout[i].HeldPrefab != null ? Instantiate(loadout[i].HeldPrefab, weaponBack) : null;
            if (weaponVisuals[i] == null) weapons[i] = null;
        }
        WeaponCount = Array.FindAll(weapons, w => w != null).Length;
        SelectedSlotIndex = selected >= 0 && selected < Capacity && weapons[selected] != null ? selected : Array.FindIndex(weapons, w => w != null);
        IsDrawn = false; RefreshWeaponPoses();
        foreach (var visual in weaponVisuals) if (visual != null) visual.SetActive(weaponsVisible);
        WeaponChanged?.Invoke(CurrentWeapon); SlotsChanged?.Invoke();
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
            if (weaponVisuals[i] != null && weapons[i] != null) ApplyWeaponPose(i);
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
