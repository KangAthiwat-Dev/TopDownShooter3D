using UnityEngine;

[DisallowMultipleComponent,RequireComponent(typeof(StoryActor))]
public sealed class StoryDialogueInteractable : Interactable
{
    [SerializeField] private StoryDialogueUI dialogue;
    [SerializeField,Min(.5f)] private float interactionRange=2.5f;
    [SerializeField] private LayerMask walls=1<<7;
    public StoryActor Actor=>GetComponent<StoryActor>();
    public bool IsPlayerInRange(GameObject player)
    {
        if(player==null||!isActiveAndEnabled)return false;
        Vector3 offset=player.transform.position-transform.position;
        return Mathf.Abs(offset.y)<1.8f&&Vector3.ProjectOnPlane(offset,Vector3.up).sqrMagnitude<=interactionRange*interactionRange&&
            !Physics.Linecast(transform.position+Vector3.up*1.2f,player.transform.position+Vector3.up*1.2f,walls,QueryTriggerInteraction.Ignore);
    }
    public override bool CanInteract(GameObject player)=>base.CanInteract(player)&&dialogue!=null&&!dialogue.IsOpen&&
        dialogue.PlayerObject==player&&IsPlayerInRange(player)&&player.GetComponent<PlayerInteractor>().CanStartInteraction;
    public override void Interact(GameObject player){if(CanInteract(player))dialogue.TryOpen(this);}
}
