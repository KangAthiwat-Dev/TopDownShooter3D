using UnityEngine;

/// <summary>Runs after action cleanup; lets the player fall while the rest of the game is paused.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent, RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerDeathPresentation : MonoBehaviour
{
    [SerializeField] private AnimationClip deathClip;
    [SerializeField, Min(0)] private float finalPoseHold = .15f;
    private PlayerHealth health;
    private PlayerAnimationController animationController;
    private Animator animator;
    private bool started;
    public bool Started => started;
    public float GameOverDelay => deathClip != null && animator != null && animator.isActiveAndEnabled &&
        animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash("Base Layer.Death"))
        ? deathClip.length + finalPoseHold : 0;

    private void Awake()
    {
        health=GetComponent<PlayerHealth>();animationController=GetComponent<PlayerAnimationController>();
        animator=GetComponentInChildren<Animator>();
    }
    private void OnEnable()=>health.Died+=HandleDeath;
    private void OnDisable(){if(health!=null)health.Died-=HandleDeath;}
    private void HandleDeath()
    {
        if(started)return;started=true;
        var melee=GetComponent<PlayerMeleeAttack>();if(melee!=null)melee.enabled=false;
        var interactor=GetComponent<PlayerInteractor>();if(interactor!=null)interactor.enabled=false;
        var ranged=GetComponent<PlayerRangedWeapon>();if(ranged!=null){ranged.CancelPendingShot();ranged.enabled=false;}
        var dash=GetComponent<PlayerDashVisual>();if(dash!=null)dash.enabled=false;
        var shadow=GetComponent<PlayerShadowCollision>();if(shadow!=null)shadow.enabled=false;
        if(animationController!=null)animationController.PlayDeath();
    }
}
