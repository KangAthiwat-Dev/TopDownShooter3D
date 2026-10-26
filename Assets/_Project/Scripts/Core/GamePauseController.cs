using System;
using UnityEngine;

[Flags]
public enum GamePauseReason { None = 0, Inventory = 1, GameOver = 2 }

// One owner of time/cursor/audio. Closing Inventory cannot clear Game Over's
// independent reason, even if the two death subscribers run in another order.
[DefaultExecutionOrder(-1000), DisallowMultipleComponent]
public class GamePauseController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    private GamePauseReason reasons;
    private float previousTimeScale;
    private bool previousCursorVisible, previousAudioPause, ownsPause;
    private CursorLockMode previousCursorLock;
    public bool IsPaused => reasons != GamePauseReason.None;
    public bool IsGameOver => (reasons & GamePauseReason.GameOver) != 0;
    public bool BlocksGameplay => IsPaused || Time.timeScale <= 0f;
    public GamePauseReason Reasons => reasons;
    public event Action<bool> PauseChanged;

    private void Awake()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }
    }
    private void OnEnable() { if (playerHealth != null) playerHealth.Died += HandleDeath; }
    private void HandleDeath() => SetPaused(GamePauseReason.GameOver, true);

    public void SetPaused(GamePauseReason reason, bool paused)
    {
        if (reason == GamePauseReason.None || (paused && !isActiveAndEnabled)) return;
        GamePauseReason next = paused ? reasons | reason : reasons & ~reason;
        if (next == reasons) return;
        bool wasPaused = IsPaused;
        reasons = next;
        if (IsPaused)
        {
            if (!ownsPause)
            {
                previousTimeScale = Time.timeScale;
                previousCursorVisible = Cursor.visible; previousCursorLock = Cursor.lockState;
                previousAudioPause = AudioListener.pause; ownsPause = true;
            }
            Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; AudioListener.pause = true;
        }
        else Restore();
        if (wasPaused != IsPaused) PauseChanged?.Invoke(IsPaused);
    }
    private void Restore()
    {
        if (!ownsPause) return;
        ownsPause = false;
        Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
    }
    public void ResetForRestart()
    {
        reasons = GamePauseReason.None; Restore();
        Time.timeScale = 1f; PauseChanged?.Invoke(false);
    }
    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= HandleDeath;
        reasons = GamePauseReason.None; Restore(); PauseChanged?.Invoke(false);
    }
}
