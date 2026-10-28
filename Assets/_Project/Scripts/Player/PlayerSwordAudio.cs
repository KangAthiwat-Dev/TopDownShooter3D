using System;
using UnityEngine;

/// <summary>Presentation-only sword sequence. It never deals damage or changes attack cadence.</summary>
[DefaultExecutionOrder(120),DisallowMultipleComponent,RequireComponent(typeof(PlayerMeleeAttack))]
public sealed class PlayerSwordAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip normalSwing,finalSwing;
    [SerializeField,Range(0,1)] private float normalVolume=.38f,finalVolume=.42f;
    [Tooltip("Sound sequence only, until a gameplay combo system supplies the finisher.")]
    [SerializeField,Min(1)] private int swingsPerSequence=3;
    [SerializeField,Min(.1f)] private float sequenceResetSeconds=2;
    [SerializeField,Range(0,1)] private float swingNormalizedTime=.22f;
    [Tooltip("Skip the quiet lead-in in the original files without modifying them.")]
    [SerializeField,Min(0)] private float normalStartSeconds=.17f,finalStartSeconds=.03f;
    private PlayerMeleeAttack melee;
    private PlayerAnimationController animationControl;
    private PlayerMeleeEquipment equipment;
    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerInputGate gate;
    private int playedAttack=-1,sequenceStep;
    private float lastSwing=-100;
    public event Action<bool> SwingPlayed;
    private void Awake()
    {
        melee=GetComponent<PlayerMeleeAttack>();animationControl=GetComponent<PlayerAnimationController>();equipment=GetComponent<PlayerMeleeEquipment>();movement=GetComponent<PlayerMovement>();health=GetComponent<PlayerHealth>();gate=GetComponent<PlayerInputGate>();
        if(source!=null){source.playOnAwake=false;source.loop=false;source.spatialBlend=0;GameAudioSettings.Route(source,GameAudioBus.Effects);}
    }
    private void OnEnable(){if(health!=null)health.Died+=Reset;if(equipment!=null)equipment.WeaponChanged+=OnWeaponChanged;}
    private void OnDisable(){if(health!=null)health.Died-=Reset;if(equipment!=null)equipment.WeaponChanged-=OnWeaponChanged;Reset();}
    private void OnWeaponChanged(MeleeWeaponDefinition weapon)=>Reset();
    private void LateUpdate()
    {
        if(Time.timeScale<=0||AudioListener.pause)return;
        if(health!=null&&!health.IsAlive||movement!=null&&(!movement.isActiveAndEnabled||movement.IsDashing||movement.IsShadowDashing)||gate!=null&&gate.BlocksGameplay)
        {Reset();playedAttack=melee.AttackSequence;return;}
        if(source==null||animationControl==null||!melee.isActiveAndEnabled||!melee.IsWeaponAttack||playedAttack==melee.AttackSequence)return;
        if(!animationControl.TryGetMeleeProgress(out float progress)||progress<swingNormalizedTime)return;
        playedAttack=melee.AttackSequence;
        if(Time.time-lastSwing>sequenceResetSeconds)sequenceStep=0;
        bool final=++sequenceStep>=Mathf.Max(1,swingsPerSequence);if(final)sequenceStep=0;
        lastSwing=Time.time;var clip=final?finalSwing:normalSwing;if(clip==null)return;
        // One bounded voice avoids stacking long file tails while attacking rapidly.
        source.Stop();source.clip=clip;source.volume=final?finalVolume:normalVolume;source.pitch=1;
        source.time=Mathf.Clamp(final?finalStartSeconds:normalStartSeconds,0,Mathf.Max(0,clip.length-.01f));source.Play();SwingPlayed?.Invoke(final);
    }
    private void Reset(){sequenceStep=0;lastSwing=-100;playedAttack=melee!=null?melee.AttackSequence:-1;if(source!=null)source.Stop();}
}
