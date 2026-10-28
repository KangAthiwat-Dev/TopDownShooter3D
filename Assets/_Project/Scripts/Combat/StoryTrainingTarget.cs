using UnityEngine;

/// <summary>Optional practice target: no attack, EXP, loot or quest-kill credit.</summary>
[RequireComponent(typeof(EnemyHealth))]
public sealed class StoryTrainingTarget : MonoBehaviour
{
    [SerializeField] private PlayerStoryProgress story;
    private void Start(){GetComponent<EnemyHealth>().IsInvulnerable=true;Refresh();story.Changed+=Refresh;}
    private void Refresh(){if(story.TutorialShot)gameObject.SetActive(false);}
    private void OnDestroy(){if(story!=null)story.Changed-=Refresh;}
}
