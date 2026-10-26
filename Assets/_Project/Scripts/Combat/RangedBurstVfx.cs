using UnityEngine;

// A one-shot COPY of the pack's looping demo. No colliders, damage or force.
[DisallowMultipleComponent]
public class RangedBurstVfx : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField, Min(0.1f)] private float lifetime = 0.7f;
    private bool played;
    private void OnEnable()
    {
        // The gun hierarchy may hide/reappear during dash or target changes.
        // This is not a pool: re-enabling it must not replay an old shot.
        if (!Application.isPlaying || played) return;
        played = true;
        foreach (ParticleSystem particle in particles)
            if (particle != null) particle.Play(false);
        Destroy(gameObject, lifetime); // Scaled time: inventory also freezes cleanup.
    }
}
