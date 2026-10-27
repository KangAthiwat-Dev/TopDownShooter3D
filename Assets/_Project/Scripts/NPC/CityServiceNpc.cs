using UnityEngine;

public enum CityServiceRole { PotionStore, GuildBuyer, HotelSave }

/// <summary>Named stationary service contact, ready for future shop/save interaction.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(NpcIdleLook))]
public sealed class CityServiceNpc : MonoBehaviour
{
    [SerializeField] private CityServiceRole role;
    [SerializeField] private Transform building;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private Animator animator;
    private NpcIdleLook idleLook;
    private bool wasWatching;
    public CityServiceRole Role => role;
    public Transform Building => building;
    public Transform InteractionPoint => interactionPoint;
    private void Awake()
    {
        idleLook=GetComponent<NpcIdleLook>();
        if(animator==null)animator=GetComponentInChildren<Animator>();
        if(animator!=null)
        {
            // These three named contacts stay animated when a preview camera or
            // the high third-person camera has not made them visible yet.
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat("Speed",0);
        }
    }
    private void Update()
    {
        if(Time.timeScale<=0||animator==null||idleLook.IsWatching==wasWatching)return;
        wasWatching=idleLook.IsWatching;
        animator.CrossFadeInFixedTime(wasWatching?"Talking":"Locomotion",.2f);
    }
}
