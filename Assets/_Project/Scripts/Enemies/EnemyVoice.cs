using UnityEngine;

/// <summary>Optional creature voice. The existing enemy gameplay does not require it.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth), typeof(EnemyAttack))]
public sealed class EnemyVoice : MonoBehaviour
{
    [SerializeField] private AudioSource voice;
    [Header("Use pack voices first; otherwise project Audio/Mobs clips")]
    [SerializeField] private AudioClip[] ambientClips;
    [SerializeField] private AudioClip alertClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip deathClip;
    [Header("Mix")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private Vector2 ambientInterval = new Vector2(8f, 14f);
    [Min(0.1f)] [SerializeField] private float hurtCooldown = 0.6f;

    private EnemyHealth health;
    private EnemyAttack attack;
    private EnemyAI ai;
    private float lastHealth, nextAmbient, nextHurt;
    private int playingPriority;
    private bool previouslyAlert;

    public AudioSource Source => voice;
    public AudioClip AttackClip => attackClip;
    public AudioClip DeathClip => deathClip;
    public int VoicePlayCount { get; private set; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>(); attack = GetComponent<EnemyAttack>(); ai = GetComponent<EnemyAI>();
        if (voice == null) voice = GetComponent<AudioSource>();
    }
    private void OnEnable()
    {
        lastHealth = health.IsAlive ? health.CurrentHealth : health.MaxHealth;
        nextAmbient = Time.time + Random.Range(ambientInterval.x, ambientInterval.y);
        health.HealthChanged += OnHealthChanged; health.Died += OnDeath;
        attack.AttackStarted += OnAttack;
    }
    private void OnDisable()
    {
        if (health != null) { health.HealthChanged -= OnHealthChanged; health.Died -= OnDeath; }
        if (attack != null) attack.AttackStarted -= OnAttack;
        if (voice != null) voice.Stop();
    }
    private void Update()
    {
        if (Time.timeScale <= 0f || !health.IsAlive || ai == null || !ai.enabled) return;
        bool alerted = ai.HasTargetMemory;
        if (alerted && !previouslyAlert) PlayVoice(alertClip, 1, 1f);
        previouslyAlert = alerted;
        if (alerted || Time.time < nextAmbient) return;
        nextAmbient = Time.time + Random.Range(ambientInterval.x, Mathf.Max(ambientInterval.x, ambientInterval.y));
        if (ambientClips != null && ambientClips.Length > 0)
            PlayVoice(ambientClips[Random.Range(0, ambientClips.Length)], 0, 0.65f);
    }
    private void OnAttack() => PlayVoice(attackClip, 2, 1f);
    private void OnHealthChanged(float current, float maximum)
    {
        if (current > 0 && current < lastHealth && Time.time >= nextHurt)
        {
            nextHurt = Time.time + hurtCooldown; PlayVoice(hurtClip, 2, 0.8f);
        }
        lastHealth = current;
    }
    private void OnDeath() => PlayVoice(deathClip, 3, 1f);

    private void PlayVoice(AudioClip clip, int priority, float gain)
    {
        if (clip == null || voice == null || Time.timeScale <= 0f) return;
        // One voice per creature. Long roars are not stacked/restarted every swing.
        if (voice.isPlaying && (priority < playingPriority || (voice.clip == clip && priority <= playingPriority))) return;
        voice.Stop(); voice.clip = clip; voice.loop = false;
        voice.volume = volume * gain;
        voice.pitch = Random.Range(Mathf.Max(0.1f, pitchRange.x), Mathf.Max(0.1f, pitchRange.y));
        playingPriority = priority; voice.Play(); VoicePlayCount++;
    }
}
