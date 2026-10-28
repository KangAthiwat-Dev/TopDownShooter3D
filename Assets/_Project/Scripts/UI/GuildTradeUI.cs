using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Guild-only dialogue and a priced, explicit sale of existing supply stacks.</summary>
public sealed class GuildTradeUI : MonoBehaviour
{
    public const string Greeting = "สวัสดีนักภจญภัย มีอะไรให้ข้าช่วยมั้ย";
    public const string SellChoice = "ฉันอยากขาย item จากการกำจัด monster";
    public const string LeaveChoice = "ไม่มีอะไรแล้ว";
    [Serializable] public sealed class Row
    {
        public Button button;
        public Image icon, frame;
        public TMP_Text name, quantity, price;
    }
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGoldWallet wallet;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInput input;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private GameObject window, dialoguePage, salePage;
    [SerializeField] private Button sellChoice, leaveChoice, closeButton, backButton, sellButton, lessButton, moreButton, stackButton;
    [SerializeField] private TMP_Text goldLabel, selectedName, quantityLabel, totalLabel, statusLabel, sellLabel;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private Row[] rows;
    private readonly ItemDefinition[] rowItems = new ItemDefinition[PlayerInventory.Capacity];
    private InputAction close;
    private GuildBuyerInteractable buyer;
    private GameObject previousSelection;
    private ItemDefinition selectedItem;
    private int selectedSlot = -1, quantity = 1;
    private bool selling;
    public bool IsOpen { get; private set; }
    public bool IsSelling => IsOpen && salePage.activeSelf;
    public int SelectedSlot => selectedSlot;
    public int SelectedQuantity => quantity;
    public string Status => statusLabel.text;
    public GameObject PlayerObject => inventory.gameObject;

    private void Awake()
    {
        close=input.actions.FindAction("Menus/Close",true);
        sellChoice.onClick.AddListener(ShowSelling);leaveChoice.onClick.AddListener(Close);closeButton.onClick.AddListener(Close);backButton.onClick.AddListener(ShowDialogue);
        sellButton.onClick.AddListener(SellSelected);lessButton.onClick.AddListener(()=>SetQuantity(quantity-1));moreButton.onClick.AddListener(()=>SetQuantity(quantity+1));
        stackButton.onClick.AddListener(()=>SetQuantity(inventory.GetQuantity(selectedSlot)));
        for(int i=0;i<rows.Length;i++){int slot=i;rows[i].button.onClick.AddListener(()=>SelectSlot(slot));}
        window.SetActive(false);
    }
    private void OnEnable()
    {
        inventory.Changed+=Refresh;wallet.Changed+=Refresh;health.Died+=Close;
        close.performed+=OnClose;
    }
    private void OnDisable()
    {
        Close();if(inventory!=null)inventory.Changed-=Refresh;if(wallet!=null)wallet.Changed-=Refresh;if(health!=null)health.Died-=Close;
        if(close!=null)close.performed-=OnClose;
    }
    private void OnClose(InputAction.CallbackContext _){if(IsOpen)Close();}
    private void Update()
    {
        if(IsOpen&&(buyer==null||!buyer.IsPlayerInRange(PlayerObject)||!health.IsAlive))Close();
    }
    public bool TryOpen(GuildBuyerInteractable npc)
    {
        if(IsOpen||!isActiveAndEnabled||npc==null||!npc.IsPlayerInRange(PlayerObject)||!health.IsAlive||
            pause==null||!pause.isActiveAndEnabled||pause.IsPaused||!PlayerObject.GetComponent<PlayerInteractor>().CanStartInteraction) return false;
        buyer=npc;previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        IsOpen=true;pause.SetPaused(GamePauseReason.Dialogue,true);window.SetActive(true);window.transform.SetAsLastSibling();
        ShowDialogue();return true;
    }
    public void Close()
    {
        if(!IsOpen)return;
        IsOpen=false;buyer=null;selectedItem=null;selectedSlot=-1;window.SetActive(false);
        if(pause!=null){if(!health.IsAlive)pause.SetPaused(GamePauseReason.GameOver,true);pause.SetPaused(GamePauseReason.Dialogue,false);}
        if(EventSystem.current!=null&&health!=null&&health.IsAlive)
            EventSystem.current.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    public void ShowDialogue()
    {
        if(!IsOpen)return;
        dialoguePage.SetActive(true);salePage.SetActive(false);Refresh();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(sellChoice.gameObject);
    }
    public void ShowSelling()
    {
        if(!IsOpen)return;
        dialoguePage.SetActive(false);salePage.SetActive(true);selectedSlot=-1;selectedItem=null;quantity=1;statusLabel.text="เลือกไอเทมและจำนวนที่ต้องการขาย";
        Refresh();for(int i=0;i<PlayerInventory.Capacity;i++)if(inventory.GetItem(i)!=null&&inventory.GetItem(i).CanSell){SelectSlot(i);break;}
    }
    public void SelectSlot(int slot)
    {
        if(!IsSelling||slot<0||slot>=PlayerInventory.Capacity||rowItems[slot]==null||!rowItems[slot].CanSell)return;
        if(inventory.GetItem(slot)!=rowItems[slot]){Refresh();return;}
        selectedSlot=slot;selectedItem=rowItems[slot];quantity=1;Refresh();
    }
    public void SetQuantity(int amount)
    {
        if(!IsSelling||selectedItem==null)return;
        quantity=Mathf.Clamp(amount,1,Mathf.Max(1,inventory.GetQuantity(selectedSlot)));Refresh();
    }
    public void SellSelected()
    {
        if(selling||!IsSelling||buyer==null||!buyer.IsPlayerInRange(PlayerObject)||!health.IsAlive||selectedItem==null)return;
        var soldItem=selectedItem;int sold=quantity;long total=(long)sold*selectedItem.SellPrice;
        selling=true;
        try
        {
            bool success=inventory.TrySell(selectedSlot,selectedItem,sold,wallet);
            statusLabel.text=success?$"ขาย {soldItem.DisplayName} ×{sold} ได้รับ {total:N0} Gold":"ขายไม่ได้: ไอเทมเปลี่ยนหรือยอด Gold เต็ม";
        }
        finally{selling=false;}
        Refresh();
    }
    private void Refresh()
    {
        if(!IsOpen)return;
        goldLabel.text=wallet.Gold.ToString("N0")+" Gold";
        if(!IsSelling)return;
        int sellable=0;
        for(int i=0;i<rows.Length;i++)
        {
            var item=inventory.GetItem(i);var row=rows[i];rowItems[i]=item;
            bool allowed=item!=null&&item.CanSell;if(allowed)sellable++;
            row.button.interactable=allowed;row.icon.sprite=item!=null?item.Icon:null;row.icon.gameObject.SetActive(row.icon.sprite!=null);
            row.name.text=item!=null?item.DisplayName:"ช่องว่าง";
            row.quantity.text=item!=null?"×"+inventory.GetQuantity(i):"";
            row.price.text=allowed?item.SellPrice+" Gold / ชิ้น":item!=null?"รับซื้อเฉพาะวัตถุดิบมอนสเตอร์":"";
            row.frame.color=i==selectedSlot?new Color(1,.86f,.45f):Color.white;
        }
        if(selectedSlot<0||inventory.GetItem(selectedSlot)!=selectedItem||selectedItem==null||!selectedItem.CanSell)
        {selectedItem=null;selectedSlot=-1;quantity=1;}
        bool selected=selectedItem!=null;
        if(selected)quantity=Mathf.Clamp(quantity,1,inventory.GetQuantity(selectedSlot));
        selectedName.text=selected?selectedItem.DisplayName:"เลือกไอเทมจากกระเป๋า";
        selectedIcon.sprite=selected?selectedItem.Icon:null;selectedIcon.gameObject.SetActive(selectedIcon.sprite!=null);
        quantityLabel.text=selected?quantity+" / "+inventory.GetQuantity(selectedSlot):"—";
        long value=selected?(long)quantity*selectedItem.SellPrice:0;
        totalLabel.text=selected?$"ราคาต่อชิ้น  {selectedItem.SellPrice:N0} Gold\nได้รับทั้งหมด  {value:N0} Gold":"ขายเฉพาะของจากมอนสเตอร์\nอาวุธประชิดจะไม่ถูกขาย";
        sellLabel.text=selected?$"ขาย {quantity} ชิ้น  +{value:N0} Gold":"ขายไอเทม";
        sellButton.interactable=selected&&wallet.CanReceive(value);lessButton.interactable=selected&&quantity>1;
        moreButton.interactable=selected&&quantity<inventory.GetQuantity(selectedSlot);stackButton.interactable=selected;
        if(sellable==0&&!selling&&!statusLabel.text.StartsWith("ขาย ",StringComparison.Ordinal))statusLabel.text="ยังไม่มีวัตถุดิบมอนสเตอร์สำหรับขาย";
    }
}
