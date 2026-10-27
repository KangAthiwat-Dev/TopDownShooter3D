using UnityEngine;

/// <summary>Generic dragon animation clips send their contact frames to the gameplay root.</summary>
public sealed class EncounterAnimationEvents : MonoBehaviour
{
    [SerializeField] private EncounterCombatant owner;
    public void EncounterImpact() => owner?.ResolveMeleeImpact();
}
