using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
public sealed class PlayerUpgradePanelUI : MonoBehaviour
{
    [SerializeField] private PlayerProgressionState progression;
    [SerializeField] private PlayerUpgradeStats stats;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInput input;
    [SerializeField] private GamePauseController pause;
    [SerializeField] private InventoryPanelUI inventory;
    [SerializeField] private GameObject window;
    [SerializeField] private Button openButton,closeButton;
    [SerializeField] private TMP_Text openLabel,pointsLabel,summary;
    [SerializeField] private Button[] upgradeButtons;
    [SerializeField] private TMP_Text[] rankLabels,previews;
    [SerializeField] private Button basicsTab,combatTab;
    [SerializeField] private GameObject[] cards;
    private bool combatPage=true;
    private InputAction toggle,close;
    private InputActionMap menuMap;
    private bool enabledMap;
    private GameObject previousSelection;
    public bool IsOpen { get; private set; }
    public string DisplayedPoints => pointsLabel.text;

    private void Awake()
    {
        menuMap=input.actions.FindActionMap("Menus",true);
        toggle=menuMap.FindAction("Upgrades",true);close=menuMap.FindAction("Close",true);
        openButton.onClick.AddListener(Toggle);closeButton.onClick.AddListener(Close);
        for(int i=0;i<upgradeButtons.Length;i++){int index=i;upgradeButtons[i].onClick.AddListener(()=>Spend((PlayerUpgrade)index));}
        if(basicsTab!=null)basicsTab.onClick.AddListener(()=>ShowPage(false));
        if(combatTab!=null)combatTab.onClick.AddListener(()=>ShowPage(true));
        window.SetActive(false);
    }
    private void OnEnable()
    {
        progression.Changed+=Refresh;health.Died+=OnDeath;
        toggle.performed+=OnToggle;close.performed+=OnClose;
        enabledMap=!menuMap.enabled;menuMap.Enable();Refresh();
    }
    private void OnDisable()
    {
        Close();if(progression!=null)progression.Changed-=Refresh;if(health!=null)health.Died-=OnDeath;
        if(toggle!=null)toggle.performed-=OnToggle;if(close!=null)close.performed-=OnClose;
        if(enabledMap&&menuMap!=null)menuMap.Disable();
    }
    private void OnToggle(InputAction.CallbackContext _) => Toggle();
    private void OnClose(InputAction.CallbackContext _){if(IsOpen)Close();}
    private void OnDeath(){Close();openButton.interactable=false;}
    public void Toggle(){if(IsOpen)Close();else TryOpen();}
    public bool TryOpen()
    {
        if(IsOpen||!isActiveAndEnabled||!health.IsAlive||pause==null||!pause.isActiveAndEnabled||pause.IsGameOver||
            pause.IsPaused||(inventory!=null&&inventory.IsOpen))return false;
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        pause.SetPaused(GamePauseReason.Upgrades,true);IsOpen=true;window.SetActive(true);transform.SetAsLastSibling();window.transform.SetAsLastSibling();openButton.interactable=false;Refresh();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        return true;
    }
    public void Close()
    {
        if(!IsOpen)return;
        IsOpen=false;
        if(pause!=null){if(health!=null&&!health.IsAlive)pause.SetPaused(GamePauseReason.GameOver,true);pause.SetPaused(GamePauseReason.Upgrades,false);}
        window.SetActive(false);openButton.interactable=health!=null&&health.IsAlive;
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    public bool Spend(PlayerUpgrade kind)=>IsOpen&&progression.TryUpgrade(kind);
    public void ShowPage(bool combat)
    {
        combatPage=combat;
        if(cards!=null)for(int i=0;i<cards.Length;i++)
        {
            bool basic=i==(int)PlayerUpgrade.Endurance||i==(int)PlayerUpgrade.Transformation||i==(int)PlayerUpgrade.Firepower;
            if(cards[i]!=null)cards[i].SetActive(combat?!basic:basic);
        }
        if(basicsTab!=null)basicsTab.interactable=combat;if(combatTab!=null)combatTab.interactable=!combat;
    }
    private void Refresh()
    {
        int available=progression.AvailableUpgradePoints;
        openLabel.text="[C]  พัฒนาสกิล"+(available>0?"   +"+available:"");
        pointsLabel.text="เลเวล "+progression.Level+"  |  แต้มคงเหลือ "+available;
        if(!IsOpen)return;
        ShowPage(combatPage);
        for(int i=0;i<upgradeButtons.Length;i++)
        {
            int rank=progression.UpgradeRank((PlayerUpgrade)i);
            int cap=PlayerProgressionState.MaximumRank((PlayerUpgrade)i);
            rankLabels[i].text=rank+" / "+cap;
            previews[i].text=rank>=cap?"พัฒนาถึงขั้นสูงสุดแล้ว":stats.Preview((PlayerUpgrade)i);
            upgradeButtons[i].interactable=available>0&&rank<cap;
        }
        var energy=progression.GetComponent<PlayerEnergy>();var weapon=progression.GetComponent<PlayerRangedWeapon>();
        summary.text=$"เลือด {health.MaxHealth:0}   |   พลังงาน {energy.MaxEnergy:0}   |   ดาเมจกระสุนหลัก {weapon.Damage:0.#}   |   ยิงชุดละ 3 จังหวะ";
    }
}
