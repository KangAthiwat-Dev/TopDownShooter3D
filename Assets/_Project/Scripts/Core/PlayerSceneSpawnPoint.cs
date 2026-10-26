using UnityEngine;

/// <summary>Places the scene's player before movement/camera startup. No VFX or respawn rules.</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class PlayerSceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private bool placePlayerOnAwake = true;

    public Transform Player => player;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        if (placePlayerOnAwake) PlacePlayer();
    }

    public bool PlacePlayer()
    {
        if (player == null) return false;
        // The controller must not retain the old physics position after teleporting.
        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.SetPositionAndRotation(transform.position, transform.rotation);
        if (wasEnabled) controller.enabled = true;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}
