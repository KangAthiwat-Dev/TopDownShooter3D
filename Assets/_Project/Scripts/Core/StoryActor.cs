using UnityEngine;

/// <summary>Stable NPC identity. PlayerStoryProgress owns saved dialogue/quest progress.</summary>
[DisallowMultipleComponent]
public sealed class StoryActor : MonoBehaviour
{
    [SerializeField] private string actorId;
    [SerializeField] private string displayName;
    [SerializeField] private string storyRole;
    public string ActorId => actorId;
    public string DisplayName => displayName;
    public string StoryRole => storyRole;
}
