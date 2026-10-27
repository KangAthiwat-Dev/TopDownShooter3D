using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only HUD for implemented gameplay; inventory continues to use its existing owner.</summary>
[DisallowMultipleComponent]
public sealed class AdventureHudUI : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerEnergy energy;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerAttackController attack;
    [SerializeField] private PlayerMeleeEquipment equipment;
    [SerializeField] private Camera view;
    [SerializeField] private TownMusicZone city;
    [SerializeField] private ForestMapLayout map;
    [SerializeField] private Image dashMask,shadowMask;
    [SerializeField] private TMP_Text dashStatus,shadowStatus,weaponName,weaponStatus,areaName,areaKind,heading;
    [SerializeField] private RectTransform compass;
    private string previousArea;
    private float nextLabels;
    private DemonCliffZone demonCliffs;
    private static readonly string[] Directions={"N","NE","E","SE","S","SW","W","NW"};
    private void Start()=>demonCliffs=FindAnyObjectByType<DemonCliffZone>();
    public string DisplayedArea => areaName != null ? areaName.text : string.Empty;
    public float DisplayedDashCooldown => dashMask != null ? dashMask.fillAmount : 0f;

    private void Update()
    {
        if(movement==null||energy==null)return;
        if(dashMask!=null)dashMask.fillAmount=movement.NormalizedDashCooldown;
        if(shadowMask!=null)shadowMask.fillAmount=1f-energy.NormalizedEnergy;
        if(dashStatus!=null)dashStatus.text=health!=null&&!health.IsAlive?"--":movement.IsDashing?"DASH":movement.DashCooldownRemaining>.01f?movement.DashCooldownRemaining.ToString("0.0"):"READY";
        if(shadowStatus!=null)shadowStatus.text=health!=null&&!health.IsAlive?"--":movement.IsShadowDashing?"IN FORM":energy.CanStartShadowDash?"READY":"LOW ENERGY";
        float yaw=view!=null?view.transform.eulerAngles.y:0f;
        if(compass!=null)compass.localRotation=Quaternion.Euler(0,0,yaw);
        if(Time.unscaledTime<nextLabels)return;
        nextLabels=Time.unscaledTime+.15f;
        if(heading!=null)
        {
            heading.text=Directions[Mathf.RoundToInt(yaw/45f)%8]+"  "+Mathf.RoundToInt(yaw).ToString("000")+" deg";
        }
        bool melee=attack!=null&&attack.CurrentMode==AttackMode.Melee;
        if(weaponName!=null)weaponName.text=melee?(equipment!=null&&equipment.HasWeapon?equipment.CurrentWeapon.DisplayName.ToUpperInvariant():"BARE HANDS"):"RIFLE";
        if(weaponStatus!=null)weaponStatus.text=melee?"CLOSE COMBAT  /  J":"J  ATTACK   /   AUTO AIM";
        string location="FOREST",kind="WILDERNESS";
        if(city!=null&&city.IsInside){location=city.AreaName;kind="SETTLEMENT";}
        else if(demonCliffs!=null&&demonCliffs.Contains(movement.transform.position)){location=demonCliffs.displayName;kind="CASTLE APPROACH";}
        else if(map!=null)
        {
            var position=movement.transform.position;
            if(Near(map.sanctuary,position,24f)){location="SUMMONING GROVE";kind="SANCTUARY";}
            else if(Near(map.village,position,24f)){location="THE VILLAGE";kind="SETTLEMENT";}
            else if(demonCliffs==null&&Near(map.castle,position,28f)){location="DARK CASTLE";kind="HOSTILE TERRITORY";}
            else if(Near(map.camp,position,13f)){location="FOREST CAMP";kind="CAMPSITE";}
        }
        if(previousArea!=location){previousArea=location;if(areaName!=null)areaName.text=location;}
        if(areaKind!=null)areaKind.text=kind;
    }
    private static bool Near(Transform landmark,Vector3 position,float radius)
    {return landmark!=null&&Vector3.ProjectOnPlane(landmark.position-position,Vector3.up).sqrMagnitude<radius*radius;}
}
