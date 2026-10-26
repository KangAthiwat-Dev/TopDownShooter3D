using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-900), DisallowMultipleComponent]
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Player / State")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerMeleeEquipment equipment;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GamePauseController pauseController;
    [Header("View")]
    [SerializeField] private GameObject window;
    [SerializeField] private InventorySlotUI[] slots;
    [SerializeField] private Button openButton, closeButton, selectWeaponButton;
    [SerializeField] private TMP_Text capacityText, detailName, detailType, detailDescription, detailStats, detailGlyph, selectLabel;
    [SerializeField] private Image detailIcon;
    private InputAction toggleAction, closeAction;
    private InputActionMap menuMap;
    private bool enabledMenuMap;
    private int selectedSlot = -1, lastToggleFrame = -1;
    private GameObject previousSelection;
    public bool IsOpen { get; private set; }
    public int SelectedSlot => selectedSlot;
    public string DetailTitle => detailName != null ? detailName.text : string.Empty;

    private void Awake()
    {
        if (inventory == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) inventory = player.GetComponent<PlayerInventory>();
        }
        if (inventory != null)
        {
            if (equipment == null) equipment = inventory.GetComponent<PlayerMeleeEquipment>();
            if (playerInput == null) playerInput = inventory.GetComponent<PlayerInput>();
            if (playerHealth == null) playerHealth = inventory.GetComponent<PlayerHealth>();
        }
        if (pauseController == null) pauseController = FindAnyObjectByType<GamePauseController>();
        menuMap = playerInput.actions.FindActionMap("Menus", true);
        toggleAction = menuMap.FindAction("Inventory", true); closeAction = menuMap.FindAction("Close", true);
        for (int i = 0; i < slots.Length; i++) slots[i].Bind(i, SelectSlot);
        openButton.onClick.AddListener(Toggle); closeButton.onClick.AddListener(Close);
        selectWeaponButton.onClick.AddListener(SelectWeapon);
        window.SetActive(false);
    }
    private void OnEnable()
    {
        inventory.Changed += Refresh; equipment.SlotsChanged += Refresh; playerHealth.Died += HandleDeath;
        toggleAction.performed += OnToggle; closeAction.performed += OnClose;
        enabledMenuMap = !menuMap.enabled;
        menuMap.Enable();
    }
    private void OnDisable()
    {
        Close();
        if (inventory != null) inventory.Changed -= Refresh;
        if (equipment != null) equipment.SlotsChanged -= Refresh;
        if (playerHealth != null) playerHealth.Died -= HandleDeath;
        if (toggleAction != null) toggleAction.performed -= OnToggle;
        if (closeAction != null) closeAction.performed -= OnClose;
        if (menuMap != null && enabledMenuMap) menuMap.Disable();
    }
    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (selectWeaponButton != null) selectWeaponButton.onClick.RemoveListener(SelectWeapon);
    }
    private void OnToggle(InputAction.CallbackContext _) => Toggle();
    private void OnClose(InputAction.CallbackContext _) { if (IsOpen) Close(); }
    private void HandleDeath() { Close(); openButton.interactable = false; }
    public void Toggle()
    {
        if (lastToggleFrame == Time.frameCount) return;
        lastToggleFrame = Time.frameCount;
        if (IsOpen) Close(); else TryOpen();
    }
    public bool TryOpen()
    {
        if (IsOpen || !isActiveAndEnabled || !playerHealth.IsAlive || pauseController == null ||
            !pauseController.isActiveAndEnabled || pauseController.IsGameOver) return false;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        pauseController.SetPaused(GamePauseReason.Inventory, true);
        IsOpen = true; window.SetActive(true); window.transform.SetAsLastSibling(); openButton.interactable = false;
        if (selectedSlot < 0)
        {
            selectedSlot = 0;
            for (int i = 0; i < PlayerInventory.Capacity; i++) if (inventory.GetItem(i) != null) { selectedSlot = i; break; }
            if (inventory.OccupiedSlots == 0 && equipment.SelectedSlotIndex >= 0) selectedSlot = PlayerInventory.Capacity + equipment.SelectedSlotIndex;
        }
        Refresh();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slots[selectedSlot].Button.gameObject);
        return true;
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        bool alive = playerHealth != null && playerHealth.IsAlive;
        if (pauseController != null)
        {
            if (!alive) pauseController.SetPaused(GamePauseReason.GameOver, true);
            pauseController.SetPaused(GamePauseReason.Inventory, false);
        }
        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(window.transform))
                EventSystem.current.SetSelectedGameObject(alive && previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        window.SetActive(false); openButton.interactable = alive;
    }
    public void SelectSlot(int index)
    {
        if (!IsOpen || index < 0 || index >= slots.Length) return;
        selectedSlot = index; Refresh();
    }
    private void SelectWeapon()
    {
        if (IsOpen && selectedSlot >= PlayerInventory.Capacity) equipment.TrySelectSlot(selectedSlot - PlayerInventory.Capacity);
    }
    private void Refresh()
    {
        if (!IsOpen) return;
        capacityText.text = $"SUPPLIES  {inventory.OccupiedSlots} / 8     |     MELEE  {equipment.WeaponCount} / 2";
        for (int i = 0; i < slots.Length; i++)
        {
            bool melee = i >= PlayerInventory.Capacity;
            ItemDefinition item = !melee ? inventory.GetItem(i) : null;
            MeleeWeaponDefinition weapon = melee ? equipment.GetWeapon(i - PlayerInventory.Capacity) : null;
            string name = item != null ? item.DisplayName : weapon != null ? weapon.DisplayName : "EMPTY SLOT";
            bool active = weapon != null && equipment.SelectedSlotIndex == i - PlayerInventory.Capacity;
            slots[i].Show(name, item != null ? item.Icon : weapon != null ? weapon.HudIcon : null,
                item != null ? Initial(item.DisplayName) : weapon != null ? "M" : "+",
                item != null ? "x" + inventory.GetQuantity(i) : string.Empty,
                active ? "ACTIVE" : weapon != null ? "STORED" : item != null ? item.Category.ToString().ToUpperInvariant() : melee ? "MELEE" : "SUPPLY",
                i == selectedSlot, melee);
        }
        ItemDefinition selectedItem = inventory.GetItem(selectedSlot);
        MeleeWeaponDefinition selectedWeapon = selectedSlot >= PlayerInventory.Capacity ? equipment.GetWeapon(selectedSlot - PlayerInventory.Capacity) : null;
        bool occupied = selectedItem != null || selectedWeapon != null;
        detailName.text = selectedItem != null ? selectedItem.DisplayName : selectedWeapon != null ? selectedWeapon.DisplayName : "Nothing here yet";
        detailType.text = selectedItem != null ? selectedItem.Category.ToString().ToUpperInvariant() : selectedWeapon != null ? "MELEE WEAPON" : "EMPTY SLOT";
        detailDescription.text = selectedItem != null ? selectedItem.Description : selectedWeapon != null ? selectedWeapon.Description :
            "Supplies go in slots 1-8.\n\nYour two melee weapons stay linked to the equipment system. Pick one up in the world to see it here.";
        detailStats.text = selectedItem != null ? $"QUANTITY   {inventory.GetQuantity(selectedSlot)} / {selectedItem.MaxStack}\n\nItem use and dropping will be added later." :
            selectedWeapon != null ? $"HIT RANGE   {selectedWeapon.HitRange:0.#} m\nDAMAGE      {(selectedWeapon.KillsInOneHit ? "ONE-HIT KILL" : selectedWeapon.Damage.ToString("0.#"))}\n\nDrawn automatically near enemies." : "";
        detailIcon.sprite = selectedItem != null ? selectedItem.Icon : selectedWeapon != null ? selectedWeapon.HudIcon : null;
        detailIcon.gameObject.SetActive(detailIcon.sprite != null);
        detailGlyph.gameObject.SetActive(detailIcon.sprite == null);
        detailGlyph.text = selectedItem != null ? Initial(selectedItem.DisplayName) : selectedWeapon != null ? "M" : "+";
        selectWeaponButton.gameObject.SetActive(occupied && selectedWeapon != null);
        bool alreadySelected = selectedWeapon != null && equipment.SelectedSlotIndex == selectedSlot - PlayerInventory.Capacity;
        bool canSelect = selectedWeapon != null && equipment.CanSelectSlot(selectedSlot - PlayerInventory.Capacity);
        selectWeaponButton.interactable = canSelect && !alreadySelected;
        selectLabel.text = alreadySelected ? "CURRENT WEAPON" : canSelect ? "SELECT WEAPON" : "BUSY - FINISH CURRENT ACTION";
    }
    private static string Initial(string name) => string.IsNullOrWhiteSpace(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
}
