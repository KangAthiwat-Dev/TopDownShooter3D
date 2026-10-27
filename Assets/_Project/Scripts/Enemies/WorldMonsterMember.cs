using UnityEngine;

/// <summary>Per-life identity and one death signal, ready for a later drop table.</summary>
[RequireComponent(typeof(EnemyHealth))]
public sealed class WorldMonsterMember : MonoBehaviour
{
    public string SpeciesId { get; private set; }
    public int CampId { get; private set; } = -1;
    public EnemyHealth Health { get; private set; }
    public bool Defeated { get; private set; }
    private WorldMonsterDirector owner;

    public void Initialize(WorldMonsterDirector director, string speciesId, int campId)
    {
        owner = director;
        SpeciesId = speciesId;
        CampId = campId;
        Health = GetComponent<EnemyHealth>();
        Health.Died += OnDeath;
    }

    private void OnDeath()
    {
        if (Defeated) return;
        Defeated = true;
        if (owner != null) owner.MemberDefeated(this);
    }

    private void OnDestroy()
    {
        if (Health != null) Health.Died -= OnDeath;
        if (owner != null) owner.MemberRemoved(this);
    }
}
