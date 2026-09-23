using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class Targetable : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;

    private EnemyHealth health;

    public Transform AimPoint =>
        aimPoint != null ? aimPoint : transform;

    public EnemyHealth Health => health;

    public bool IsTargetable =>
        isActiveAndEnabled &&
        health != null &&
        health.IsAlive;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }
}