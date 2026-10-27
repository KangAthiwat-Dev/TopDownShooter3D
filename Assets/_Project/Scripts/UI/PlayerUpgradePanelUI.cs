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
        if(IsOpen||!isActiveAndEnabled||!health.IsAlive||pause==null||!pause.isActiveAndEnabled||pause.IsGameOver||(inventory!=null&&inventory.IsOpen))return false;
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        pause.SetPaused(GamePauseReason.Upgrades,true);IsOpen=true;window.SetActive(true);window.transform.SetAsLastSibling();openButton.interactable=false;Refresh();
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
    private void Refresh()
    {
        int available=progression.AvailableUpgradePoints;
        openLabel.text="[C]  UPGRADES"+(available>0?"   +"+available:"");
        pointsLabel.text="LEVEL "+progression.Level+"   /   "+available+" UPGRADE POINT"+(available==1?"":"S");
        if(!IsOpen)return;
        for(int i=0;i<upgradeButtons.Length;i++)
        {
            int rank=progression.UpgradeRank((PlayerUpgrade)i);
            rankLabels[i].text=rank+" / "+PlayerProgressionState.MaximumUpgradeRank;
            previews[i].text=rank>=PlayerProgressionState.MaximumUpgradeRank?"MAXIMUM RANK":stats.Preview((PlayerUpgrade)i);
            upgradeButtons[i].interactable=available>0&&rank<PlayerProgressionState.MaximumUpgradeRank;
        }
        var energy=progression.GetComponent<PlayerEnergy>();var weapon=progression.GetComponent<PlayerRangedWeapon>();
        summary.text=$"HEALTH  {health.MaxHealth:0}       ENERGY  {energy.MaxEnergy:0}       DAMAGE  {weapon.Damage:0.#}       3-ROUND BURST";
    }
}
