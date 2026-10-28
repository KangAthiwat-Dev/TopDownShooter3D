using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CityServiceNpc))]
public sealed class GuildBuyerInteractable : Interactable
{
    [SerializeField] private CityServiceNpc contact;
    [SerializeField] private GuildTradeUI tradeUI;
    [SerializeField, Min(.5f)] private float interactionRange = 2.5f;
    [SerializeField] private LayerMask walls = 1 << 7;
    public CityServiceNpc Contact => contact;
    public GuildTradeUI TradeUI => tradeUI;
    public bool IsPlayerInRange(GameObject player)
    {
        if (player == null || contact == null || contact.Role != CityServiceRole.GuildBuyer || !isActiveAndEnabled) return false;
        Vector3 offset = player.transform.position - transform.position;
        return Mathf.Abs(offset.y)<1.8f && Vector3.ProjectOnPlane(offset,Vector3.up).sqrMagnitude<=interactionRange*interactionRange &&
            !Physics.Linecast(transform.position+Vector3.up*1.2f,player.transform.position+Vector3.up*1.2f,walls,QueryTriggerInteraction.Ignore);
    }
    public override bool CanInteract(GameObject player)
    {
        if (!base.CanInteract(player) || !IsPlayerInRange(player) || tradeUI == null || tradeUI.IsOpen) return false;
        var interactor = player.GetComponent<PlayerInteractor>();
        return interactor != null && interactor.CanStartInteraction && tradeUI.PlayerObject == player;
    }
    public override void Interact(GameObject player)
    {
        if (CanInteract(player)) tradeUI.TryOpen(this);
    }
}
