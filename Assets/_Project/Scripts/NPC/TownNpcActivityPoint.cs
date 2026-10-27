using UnityEngine;

public enum TownNpcActivityKind { Wander, Social, Interact, Outside }

/// <summary>An editable destination; rotation is the facing used for a prop interaction.</summary>
public sealed class TownNpcActivityPoint : MonoBehaviour
{
    [SerializeField] private TownNpcActivityKind activity;
    public TownNpcActivityKind Activity => activity;
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = activity == TownNpcActivityKind.Outside ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, .4f);
        Gizmos.DrawRay(transform.position + Vector3.up * .1f, transform.forward);
    }
}
