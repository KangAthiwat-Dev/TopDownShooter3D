using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PotionShopUI : MonoBehaviour
{
    [Serializable] public sealed class Row { public Button button; public Image frame, icon; public TMP_Text name, effect, price; }
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGoldWallet wallet;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInput input;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private GameObject window;
    [SerializeField] private Button closeButton, buyButton, moreButton, lessButton;
    [SerializeField] private TMP_Text goldLabel, selectedName, description, quantityLabel, totalLabel, statusLabel, buyLabel;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private Row[] rows;
    private InputAction close;
    private GameObject previousSelection;
    private ItemDefinition selectedPotion;
    private int selectedIndex = -1, quantity = 1, unitPrice;
    private bool buying;
    public bool IsOpen { get; private set; }
    public GameObject PlayerObject => inventory.gameObject;
    public PotionMerchant Merchant { get; private set; }
    public int SelectedIndex => selectedIndex;
    public int Quantity => quantity;
    public string Status => statusLabel.text;
    private void Awake()
    {
        close = input.actions.FindAction("Menus/Close", true); closeButton.onClick.AddListener(Close); buyButton.onClick.AddListener(BuySelected);
        moreButton.onClick.AddListener(() => SetQuantity(quantity + 1)); lessButton.onClick.AddListener(() => SetQuantity(quantity - 1));
        for (int i = 0; i < rows.Length; i++) { int index = i; rows[i].button.onClick.AddListener(() => SelectPotion(index)); }
        window.SetActive(false);
    }
    private void OnEnable() { inventory.Changed += Refresh; wallet.Changed += Refresh; health.Died += Close; close.performed += OnClose; }
    private void OnDisable()
    {
        Close(); if (inventory != null) inventory.Changed -= Refresh; if (wallet != null) wallet.Changed -= Refresh;
        if (health != null) health.Died -= Close; if (close != null) close.performed -= OnClose;
    }
    private void OnClose(InputAction.CallbackContext _) { if (IsOpen) Close(); }
    private void Update() { if (IsOpen && (Merchant == null || !Merchant.IsPlayerInRange(PlayerObject) || !health.IsAlive)) Close(); }
    public bool TryOpen(PotionMerchant merchant)
    {
        if (IsOpen || !isActiveAndEnabled || merchant == null || merchant.ShopUI != this || !merchant.IsPlayerInRange(PlayerObject) || !health.IsAlive ||
            pause == null || !pause.isActiveAndEnabled || pause.IsPaused || !PlayerObject.GetComponent<PlayerInteractor>().CanStartInteraction) return false;
        Merchant = merchant; previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        selectedPotion = null; selectedIndex = -1; quantity = 1; IsOpen = true;
        pause.SetPaused(GamePauseReason.Dialogue, true); window.SetActive(true); window.transform.SetAsLastSibling();
        statusLabel.text = "สวัสดีนักผจญภัย เลือก Potion ที่ต้องการได้เลย"; SelectPotion(0); Refresh();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rows[0].button.gameObject); return true;
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; Merchant = null; selectedPotion = null; selectedIndex = -1; window.SetActive(false);
        if (pause != null) { if (!health.IsAlive) pause.SetPaused(GamePauseReason.GameOver, true); pause.SetPaused(GamePauseReason.Dialogue, false); }
        if (EventSystem.current != null && health.IsAlive) EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }
    public void SelectPotion(int index)
    {
        if (!IsOpen || Merchant == null || Merchant.GetPotion(index) == null) return;
        selectedIndex = index; selectedPotion = Merchant.GetPotion(index); unitPrice = Merchant.GetPrice(index); quantity = 1; Refresh();
    }
    public void SetQuantity(int value) { if (!IsOpen) return; quantity = Mathf.Clamp(value, 1, 10); Refresh(); }
    public void BuySelected()
    {
        if (buying || !IsOpen || selectedPotion == null || Merchant == null) return;
        buying = true;
        try { Merchant.TryBuy(selectedIndex, selectedPotion, unitPrice, quantity, PlayerObject, out string result); statusLabel.text = result; }
        finally { buying = false; }
        Refresh();
    }
    private void Refresh()
    {
        if (!IsOpen || Merchant == null) return;
        goldLabel.text = $"{wallet.Gold:N0} Gold";
        for (int i = 0; i < rows.Length; i++)
        {
            var item = Merchant.GetPotion(i); var row = rows[i]; row.button.interactable = item != null;
            row.icon.sprite = item != null ? item.Icon : null; row.icon.gameObject.SetActive(row.icon.sprite != null);
            row.name.text = item != null ? item.DisplayName : "";
            row.effect.text = item != null ? item.Description.Split('\n')[0] : "";
            int owned = 0; for (int slot = 0; slot < PlayerInventory.Capacity; slot++) if (inventory.GetItem(slot) == item) owned += inventory.GetQuantity(slot);
            row.price.text = item != null ? $"{Merchant.GetPrice(i):N0} Gold / ขวด  |  มี {owned}" : "";
            row.frame.color = selectedIndex == i ? new Color(1, .84f, .4f) : Color.white;
        }
        bool valid = selectedPotion != null && Merchant.GetPotion(selectedIndex) == selectedPotion && Merchant.GetPrice(selectedIndex) == unitPrice;
        selectedName.text = valid ? selectedPotion.DisplayName : "เลือก Potion ใหม่";
        selectedIcon.sprite = valid ? selectedPotion.Icon : null; selectedIcon.gameObject.SetActive(selectedIcon.sprite != null);
        description.text = valid ? selectedPotion.Description : ""; quantityLabel.text = quantity.ToString();
        long total = (long)unitPrice * quantity;
        totalLabel.text = valid ? $"รวม {total:N0} Gold" : "";
        bool space = valid && inventory.CanAdd(selectedPotion, quantity), enough = wallet.Gold >= total;
        buyButton.interactable = valid && space && enough; lessButton.interactable = valid && quantity > 1; moreButton.interactable = valid && quantity < 10;
        buyLabel.text = !valid ? "เลือกรายการใหม่" : !space ? "กระเป๋าเต็ม" : !enough ? "Gold ไม่พอ" : $"ซื้อ {quantity} ขวด";
    }
}
