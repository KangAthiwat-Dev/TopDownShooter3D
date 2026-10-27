using UnityEngine;

/// <summary>Editable encounter bounds; checks the player directly, including shadow-dash layers.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
[DefaultExecutionOrder(-100)]
public sealed class BossArenaZone : MonoBehaviour
{
    [SerializeField] private BoxCollider area;
    [SerializeField] private KingOfDemonAI boss;
    [SerializeField, Min(0f)] private float exitMargin = .65f;
    private PlayerHealth player;
    private float nextLookup;

    public bool PlayerInside { get; private set; }
    public PlayerHealth Player => player;
    public KingOfDemonAI Boss => boss;

    private void Awake()
    {
        if (area == null) area = GetComponent<BoxCollider>();
        area.isTrigger = true;
    }

    private void Update()
    {
        if (player == null && Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + .5f;
            var target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.GetComponent<PlayerHealth>();
        }
        PlayerInside = player != null && player.IsAlive &&
            Contains(player.transform.position, PlayerInside ? exitMargin : 0f);
    }

    public bool Contains(Vector3 position, float margin = 0f)
    {
        if (area == null) area = GetComponent<BoxCollider>();
        var local = area.transform.InverseTransformPoint(position) - area.center;
        var half = area.size * .5f + Vector3.one * margin;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public Vector3 ClampToArena(Vector3 position, float inset)
    {
        var local = area.transform.InverseTransformPoint(position) - area.center;
        var half = area.size * .5f;
        local.x = Mathf.Clamp(local.x, -half.x + inset, half.x - inset);
        local.z = Mathf.Clamp(local.z, -half.z + inset, half.z - inset);
        return area.transform.TransformPoint(area.center + local);
    }

    private void OnDisable() => PlayerInside = false;
    private void OnDrawGizmosSelected()
    {
        var box = area != null ? area : GetComponent<BoxCollider>();
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, .25f, .1f, .2f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, .6f, .15f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
