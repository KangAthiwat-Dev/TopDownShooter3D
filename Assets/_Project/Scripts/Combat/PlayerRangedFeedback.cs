using UnityEngine;

[RequireComponent(typeof(PlayerRangedWeapon)), DisallowMultipleComponent]
public class PlayerRangedFeedback : MonoBehaviour
{
    [SerializeField] private RangedBurstVfx muzzleVfx;
    [SerializeField] private AudioSource shotAudio;
    [SerializeField] private AudioClip shotClip;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.2f;
    [SerializeField] private Vector2 pitchRange = new Vector2(1.02f, 1.12f);
    private PlayerRangedWeapon weapon;
    private void Awake() => weapon = GetComponent<PlayerRangedWeapon>();
    private void OnEnable() => weapon.ProjectileFired += OnShot;
    private void OnDisable() { if (weapon != null) weapon.ProjectileFired -= OnShot; }
    private void OnShot(Projectile _)
    {
        // Confirmed projectile, never aim requests: cancellation cannot play a
        // phantom shot. Follow the actual gun for the very short muzzle flash.
        if (muzzleVfx != null && weapon.MuzzlePoint != null)
        {
            RangedBurstVfx flash = Instantiate(muzzleVfx, weapon.MuzzlePoint.position, weapon.MuzzlePoint.rotation);
            flash.transform.SetParent(weapon.MuzzlePoint, true); // Preserve world size across imported gun/bone scales.
        }
        if (shotAudio != null && shotClip != null && shotVolume > 0f)
        {
            shotAudio.pitch = Random.Range(pitchRange.x, pitchRange.y);
            shotAudio.PlayOneShot(shotClip, shotVolume);
        }
    }
}
