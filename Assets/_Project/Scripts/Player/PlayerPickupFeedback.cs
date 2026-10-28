using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPickupFeedback : MonoBehaviour
{
    [SerializeField] private MonsterLootWorld loot;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip clip;
    [SerializeField,Range(0,1)] private float volume=.27f;
    private PlayerInteractor interactor;
    private PlayerHealth health;
    private float nextSound;
    public event System.Action Played;
    private void Awake(){interactor=GetComponent<PlayerInteractor>();health=GetComponent<PlayerHealth>();}
    private void OnEnable(){if(loot!=null)loot.Collected+=OnLoot;if(interactor!=null)interactor.WeaponCollected+=Play;}
    private void OnDisable(){if(loot!=null)loot.Collected-=OnLoot;if(interactor!=null)interactor.WeaponCollected-=Play;}
    private void OnLoot(ItemDefinition item,int amount){if(item!=null&&amount>0)Play();}
    private void Play()
    {
        if(source==null||clip==null||!health.IsAlive||Time.timeScale<=0||Time.time<nextSound)return;
        nextSound=Time.time+.08f;source.clip=clip;source.volume=volume;source.Play();Played?.Invoke();
    }
}
