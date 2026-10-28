using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class WeaponShopUI : MonoBehaviour
{
    [Serializable] public sealed class Row
    {
        public Button button;
        public Image icon, frame;
        public TMP_Text name, stats, price;
    }
    [SerializeField] private PlayerMeleeEquipment equipment;
    [SerializeField] private PlayerGoldWallet wallet;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInput input;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private GameObject window;
    [SerializeField] private Button closeButton, buyButton;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private TMP_Text goldLabel, timerLabel, selectedName, description, priceLabel, statusLabel, buyLabel, slotHint;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private Row[] rows;
    private InputAction close;
    private GameObject previousSelection;
    private WeaponMerchant.Offer selectedOffer;
    private string displayedRotation;
    private int selectedIndex = -1, equipmentSlot = -1;
    private bool buying;
    public bool IsOpen { get; private set; }
    public GameObject PlayerObject => equipment.gameObject;
    public WeaponMerchant Merchant { get; private set; }
    public int SelectedIndex => selectedIndex;
    public int EquipmentSlot => equipmentSlot;
    public string Status => statusLabel.text;

    private void Awake()
    {
        close = input.actions.FindAction("Menus/Close", true);
        closeButton.onClick.AddListener(Close); buyButton.onClick.AddListener(BuySelected);
        for (int i = 0; i < rows.Length; i++) { int index = i; rows[i].button.onClick.AddListener(() => SelectOffer(index)); }
        for (int i = 0; i < slotButtons.Length; i++) { int slot = i; slotButtons[i].onClick.AddListener(() => SelectEquipmentSlot(slot)); }
        window.SetActive(false);
    }
    private void OnEnable() { wallet.Changed += Refresh; equipment.SlotsChanged += Refresh; health.Died += Close; close.performed += OnClose; }
    private void OnDisable()
    {
        Close(); if (wallet != null) wallet.Changed -= Refresh; if (equipment != null) equipment.SlotsChanged -= Refresh;
        if (health != null) health.Died -= Close; if (close != null) close.performed -= OnClose;
    }
    private void OnClose(InputAction.CallbackContext _) { if (IsOpen) Close(); }
    private void Update()
    {
        if (!IsOpen) return;
        if (Merchant == null || !Merchant.IsPlayerInRange(PlayerObject) || !health.IsAlive) { Close(); return; }
        int seconds = Merchant.SecondsRemaining;
        timerLabel.text = $"สินค้าใหม่ใน {seconds / 60:00}:{seconds % 60:00}";
    }
    public bool TryOpen(WeaponMerchant merchant)
    {
        if (IsOpen || !isActiveAndEnabled || merchant == null || merchant.ShopUI != this || !merchant.IsPlayerInRange(PlayerObject) || !health.IsAlive ||
            pause == null || !pause.isActiveAndEnabled || pause.IsPaused || !PlayerObject.GetComponent<PlayerInteractor>().CanStartInteraction) return false;
        merchant.RefreshIfDue();
        Merchant = merchant; Merchant.Changed += Refresh;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        selectedIndex = -1; selectedOffer = default; displayedRotation = merchant.GetOffer(0).Rotation;
        equipmentSlot = -1;
        for (int i = 0; i < PlayerMeleeEquipment.Capacity; i++) if (equipment.GetWeapon(i) == null) { equipmentSlot = i; break; }
        statusLabel.text = "สวัสดีนักผจญภัย เลือกชมดาบได้เลย";
        IsOpen = true; pause.SetPaused(GamePauseReason.Dialogue, true); window.SetActive(true); window.transform.SetAsLastSibling(); Refresh();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rows[0].button.gameObject);
        return true;
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; if (Merchant != null) Merchant.Changed -= Refresh;
        Merchant = null; selectedIndex = -1; selectedOffer = default; window.SetActive(false);
        if (pause != null) { if (!health.IsAlive) pause.SetPaused(GamePauseReason.GameOver, true); pause.SetPaused(GamePauseReason.Dialogue, false); }
        if (EventSystem.current != null && health.IsAlive)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }
    public void SelectOffer(int index)
    {
        if (!IsOpen || Merchant == null) return;
        Merchant.RefreshIfDue(); var offer = Merchant.GetOffer(index);
        if (offer.Weapon == null || offer.Sold) return;
        selectedIndex = index; selectedOffer = offer;
        statusLabel.text = "เลือกรายการแล้ว ตรวจราคาและช่องอาวุธก่อนซื้อ"; Refresh();
    }
    public void SelectEquipmentSlot(int slot)
    {
        if (!IsOpen || slot < 0 || slot >= PlayerMeleeEquipment.Capacity) return;
        equipmentSlot = slot; Refresh();
    }
    public void BuySelected()
    {
        if (buying || !IsOpen || Merchant == null || selectedIndex < 0 || !health.IsAlive) return;
        buying = true;
        try
        {
            bool success = Merchant.TryBuy(selectedIndex, selectedOffer, equipmentSlot, PlayerObject, out string result);
            if (success)
            {
                equipmentSlot = -1;
                for (int i = 0; i < PlayerMeleeEquipment.Capacity; i++) if (equipment.GetWeapon(i) == null) { equipmentSlot = i; break; }
            }
            statusLabel.text = result;
        }
        finally { buying = false; }
        Refresh();
    }
    private void Refresh()
    {
        if (!IsOpen || Merchant == null) return;
        var rotation = Merchant.GetOffer(0).Rotation;
        if (displayedRotation != rotation)
        {
            displayedRotation = rotation; selectedIndex = -1; selectedOffer = default;
            statusLabel.text = "สินค้ารอบใหม่มาแล้ว กรุณาเลือกดาบและตรวจราคาอีกครั้ง";
        }
        goldLabel.text = $"{wallet.Gold:N0} Gold";
        for (int i = 0; i < rows.Length; i++)
        {
            var offer = Merchant.GetOffer(i); var row = rows[i]; bool valid = offer.Weapon != null;
            row.button.interactable = valid && !offer.Sold;
            row.icon.sprite = valid ? offer.Weapon.HudIcon : null; row.icon.gameObject.SetActive(row.icon.sprite != null);
            row.name.text = valid ? offer.Weapon.DisplayName : "ไม่มีสินค้า";
            row.stats.text = valid ? $"พลังโจมตีบอส {offer.Weapon.Damage:0}  |  ระยะ {offer.Weapon.HitRange:0.0} ม." : "";
            row.price.text = !valid ? "" : offer.Sold ? "ขายแล้ว · รอรอบใหม่" : $"{offer.Price:N0} Gold";
            row.frame.color = i == selectedIndex ? new Color(1, .84f, .4f) : offer.Sold ? new Color(.7f, .7f, .7f) : Color.white;
        }
        var current = Merchant.GetOffer(selectedIndex);
        bool selected = selectedIndex >= 0 && current.Weapon != null && current.Rotation == selectedOffer.Rotation && current.Weapon == selectedOffer.Weapon && current.Price == selectedOffer.Price;
        selectedName.text = selected ? current.Weapon.DisplayName : "เลือกดาบที่ต้องการ";
        selectedIcon.sprite = selected ? current.Weapon.HudIcon : null; selectedIcon.gameObject.SetActive(selectedIcon.sprite != null);
        description.text = selected ? current.Weapon.Description + $"\n\nบอสรับดาเมจ {current.Weapon.Damage:0}\nระยะโจมตี {current.Weapon.HitRange:0.0} เมตร" : "สินค้าสุ่มเปลี่ยนตามเวลา\nมีอย่างละ 1 เล่มต่อรอบ\nราคาแต่ละรอบอาจต่างกัน";
        priceLabel.text = selected ? $"ราคา {current.Price:N0} Gold" : "";
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var weapon = equipment.GetWeapon(i);
            slotLabels[i].text = $"ช่อง {i + 1}: " + (weapon != null ? weapon.DisplayName : "ว่าง");
            slotButtons[i].GetComponent<Image>().color = i == equipmentSlot ? new Color(1, .84f, .4f) : Color.white;
        }
        var old = equipment.GetWeapon(equipmentSlot);
        slotHint.text = equipmentSlot < 0 ? "เลือกช่องที่จะใส่หรือเปลี่ยนอาวุธ" : old != null ? $"แทนที่ {old.DisplayName}\nอาวุธเดิมจะหายไป ไม่มี Gold คืน" : $"ดาบใหม่จะอยู่ในช่อง {equipmentSlot + 1}";
        bool enough = selected && wallet.Gold >= current.Price;
        buyButton.interactable = selected && !current.Sold && enough && equipmentSlot >= 0;
        buyLabel.text = !selected ? "เลือกดาบก่อนซื้อ" : current.Sold ? "ขายแล้ว" : !enough ? "Gold ไม่พอ" : equipmentSlot < 0 ? "เลือกช่องอาวุธก่อน" : old != null ? "ซื้อและแทนที่อาวุธ" : "ซื้อดาบ";
    }
}
