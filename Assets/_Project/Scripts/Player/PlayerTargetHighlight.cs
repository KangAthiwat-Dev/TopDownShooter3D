using UnityEngine;

[RequireComponent(typeof(PlayerTargeting))]
public class PlayerTargetHighlight : MonoBehaviour
{
    [SerializeField]
    private PlayerTargeting playerTargeting;

    private TargetHighlight activeHighlight;

    private void Awake()
    {
        if (playerTargeting == null)
        {
            playerTargeting =
                GetComponent<PlayerTargeting>();
        }
    }

    private void OnEnable()
    {
        playerTargeting.TargetChanged +=
            HandleTargetChanged;
    }

    private void Start()
    {
        HandleTargetChanged(
            playerTargeting.CurrentTarget
        );
    }

    private void OnDisable()
    {
        if (playerTargeting != null)
        {
            playerTargeting.TargetChanged -=
                HandleTargetChanged;
        }

        ClearHighlight();
    }

    private void HandleTargetChanged(
        Targetable newTarget
    )
    {
        ClearHighlight();

        if (newTarget == null)
        {
            return;
        }

        activeHighlight =
            newTarget.GetComponentInChildren
                <TargetHighlight>(true);

        if (activeHighlight != null)
        {
            activeHighlight.SetHighlighted(true);
        }
    }

    private void ClearHighlight()
    {
        if (activeHighlight == null)
        {
            return;
        }

        activeHighlight.SetHighlighted(false);
        activeHighlight = null;
    }
}