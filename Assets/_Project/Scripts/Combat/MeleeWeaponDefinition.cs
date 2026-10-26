using UnityEngine;

[CreateAssetMenu(menuName = "TopDownShooter/Melee Weapon")]
public class MeleeWeaponDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Training Club";
    [SerializeField, TextArea(3, 7)] private string description = "A close-range weapon. Carried on your back and drawn automatically near an enemy.";
    [SerializeField] private GameObject heldPrefab;
    [SerializeField] private Sprite hudIcon;
    [SerializeField, Min(0f)] private float damage = 30f;
    [SerializeField] private bool killsInOneHit = true;
    [SerializeField, Min(0.1f)] private float hitRange = 2.5f;
    [SerializeField, Range(0f, 1f)] private float hitNormalizedTime = 0.35f;
    [SerializeField] private Vector3 gripPosition;
    [SerializeField] private Vector3 gripEulerAngles = new Vector3(0f, 0f, 90f);
    [Header("Stowed Pose (relative to back socket)")]
    [SerializeField] private Vector3 backPosition = new Vector3(0.24f, -0.45f, -0.22f);
    [SerializeField] private Vector3 backEulerAngles = new Vector3(0f, 0f, 35f);

    public string DisplayName => displayName;
    public string Description => description;
    public GameObject HeldPrefab => heldPrefab;
    public Sprite HudIcon => hudIcon;
    public float Damage => damage;
    public bool KillsInOneHit => killsInOneHit;
    public float HitRange => hitRange;
    public float HitNormalizedTime => hitNormalizedTime;
    public Vector3 GripPosition => gripPosition;
    public Quaternion GripRotation => Quaternion.Euler(gripEulerAngles);
    public Vector3 BackPosition => backPosition;
    public Quaternion BackRotation => Quaternion.Euler(backEulerAngles);
}
