using UnityEngine;
using UnityEngine.AI;

/// <summary>Keep the root and its BoxCollider editable; visual rocks sink after victory.</summary>
public sealed class DemonProgressionGate : MonoBehaviour
{
    [SerializeField] private DemonArenaEncounter prerequisite;
    [SerializeField] private Transform rocks;
    [SerializeField, Min(.1f)] private float sinkSeconds = 2;
    private Vector3 original;
    private float opened;
    public bool IsOpen => prerequisite != null && prerequisite.Completed;
    private void Awake() { if (rocks != null) original = rocks.localPosition; }
    private void Update()
    {
        if (!IsOpen) return;
        var collider = GetComponent<Collider>(); if (collider != null) collider.enabled = false;
        var obstacle = GetComponent<NavMeshObstacle>(); if (obstacle != null) obstacle.enabled = false;
        opened = Mathf.MoveTowards(opened, 1, Time.deltaTime / sinkSeconds);
        if (rocks != null)
        {
            rocks.localPosition = original + Vector3.down * (12 * Mathf.SmoothStep(0,1,opened));
            if (opened >= 1) rocks.gameObject.SetActive(false);
        }
    }
}
