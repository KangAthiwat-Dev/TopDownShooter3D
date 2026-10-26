using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerShadowCollision : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField]
    private string shadowLayerName = "PlayerShadow";

    private PlayerMovement playerMovement;

    private int normalLayer;
    private int shadowLayer;

    private bool wasShadowDashing;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();

        normalLayer = gameObject.layer;
        shadowLayer =
            LayerMask.NameToLayer(shadowLayerName);

        if (shadowLayer < 0)
        {
            Debug.LogError(
                $"Layer '{shadowLayerName}' does not exist.",
                this
            );

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        bool isShadowDashing =
            playerMovement.IsShadowDashing;

        if (isShadowDashing == wasShadowDashing)
        {
            return;
        }

        gameObject.layer = isShadowDashing
            ? shadowLayer
            : normalLayer;

        wasShadowDashing = isShadowDashing;
    }

    private void OnDisable()
    {
        if (normalLayer >= 0)
        {
            gameObject.layer = normalLayer;
        }

        wasShadowDashing = false;
    }
}