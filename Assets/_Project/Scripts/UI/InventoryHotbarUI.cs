using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventoryHotbarUI : MonoBehaviour
{
    public const int ReservedDropSlots = PlayerInventory.Capacity;
    public const int TotalSlots = ReservedDropSlots + PlayerMeleeEquipment.Capacity;

    [Serializable]
    public sealed class SlotView
    {
        public Image frame;
        public Image background;
        public Image icon;
        public GameObject fallbackIcon;
        public TMP_Text emptyMark;
        public TMP_Text itemName;
        public TMP_Text stateLabel;
        public Button button;
    }

    [SerializeField] private PlayerMeleeEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private SlotView[] slots = new SlotView[TotalSlots];
    [SerializeField] private TMP_Text meleeCountText;
    [SerializeField] private Color selectedColor = new Color(1f, 0.76f, 0.32f);
    [SerializeField] private Color meleeColor = new Color(0.58f, 0.45f, 0.27f);
    [SerializeField] private Color reservedColor = new Color(0.25f, 0.29f, 0.34f);

    private void Awake()
    {
        if (equipment == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) equipment = player.GetComponent<PlayerMeleeEquipment>();
        }
        if (inventory == null && equipment != null) inventory = equipment.GetComponent<PlayerInventory>();

        for (int i = ReservedDropSlots; i < slots.Length && i < TotalSlots; i++)
        {
            int weaponSlot = i - ReservedDropSlots;
            if (slots[i]?.button != null)
            {
                slots[i].button.onClick.AddListener(() => SelectWeapon(weaponSlot));
            }
        }
    }

    private void OnEnable()
    {
        if (equipment != null) equipment.SlotsChanged += RefreshSlots;
        if (inventory != null) inventory.Changed += RefreshSlots;
        RefreshSlots();
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.SlotsChanged -= RefreshSlots;
        if (inventory != null) inventory.Changed -= RefreshSlots;
    }

    private void SelectWeapon(int weaponSlot)
    {
        if (equipment != null) equipment.TrySelectSlot(weaponSlot);
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slots.Length && i < TotalSlots; i++)
        {
            SlotView view = slots[i];
            if (view == null) continue;
            bool meleeSlot = i >= ReservedDropSlots;
            int weaponSlot = i - ReservedDropSlots;
            MeleeWeaponDefinition weapon = meleeSlot && equipment != null ? equipment.GetWeapon(weaponSlot) : null;
            ItemDefinition item = !meleeSlot && inventory != null ? inventory.GetItem(i) : null;
            bool occupied = weapon != null || item != null;
            bool selected = weapon != null && equipment != null && equipment.SelectedSlotIndex == weaponSlot;

            if (view.frame != null) view.frame.color = selected ? selectedColor : meleeSlot ? meleeColor : reservedColor;
            if (view.background != null) view.background.color = selected
                ? new Color(0.19f, 0.15f, 0.10f, 0.98f)
                : new Color(0.055f, 0.068f, 0.085f, 0.96f);
            if (view.icon != null)
            {
                view.icon.sprite = meleeSlot ? weapon != null ? weapon.HudIcon : null : item != null ? item.Icon : null;
                view.icon.gameObject.SetActive(view.icon.sprite != null);
            }
            if (view.fallbackIcon != null) view.fallbackIcon.SetActive(weapon != null && weapon.HudIcon == null);
            if (view.emptyMark != null)
            {
                view.emptyMark.gameObject.SetActive(!occupied || (item != null && item.Icon == null));
                view.emptyMark.text = item != null && !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName.Substring(0, 1).ToUpperInvariant() : "+";
            }
            if (view.itemName != null) view.itemName.text = weapon != null ? weapon.DisplayName : item != null ? item.DisplayName : meleeSlot ? "EMPTY" : "SUPPLY";
            if (view.stateLabel != null)
            {
                view.stateLabel.text = selected ? "ACTIVE" : weapon != null ? "STORED" : item != null ? "x" + inventory.GetQuantity(i) : string.Empty;
                view.stateLabel.color = selected ? selectedColor : new Color(0.54f, 0.59f, 0.65f);
            }
            if (view.button != null) view.button.interactable = weapon != null;
        }

        if (meleeCountText != null)
        {
            meleeCountText.text = $"MELEE   {(equipment != null ? equipment.WeaponCount : 0)} / {PlayerMeleeEquipment.Capacity}";
        }
    }
}
