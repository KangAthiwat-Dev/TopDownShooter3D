using UnityEngine;

public enum StoryQuestStatus { Locked, Available, Active, ReadyToTurnIn, Completed }

[CreateAssetMenu(menuName = "TopDownShooter/Story Quest")]
public sealed class StoryQuestDefinition : ScriptableObject
{
    public string questId;
    public string title;
    [TextArea] public string description;
    [Min(1)] public int requiredCount = 1;
    [Min(1)] public int rewardGold = 100;
    public ItemDefinition rewardItem;
    [Min(1)] public int rewardQuantity = 1;
    public string RewardText => $"{rewardGold} Gold + {rewardItem.DisplayName} ×{rewardQuantity}";
}
