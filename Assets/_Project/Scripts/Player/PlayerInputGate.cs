using UnityEngine;
using UnityEngine.InputSystem;

// Keep Gameplay enabled (Interactor also uses an asset action reference), but
// gate every gameplay consumer. Menus remain live at timeScale=0.
[DefaultExecutionOrder(-500), RequireComponent(typeof(PlayerInput)), DisallowMultipleComponent]
public class PlayerInputGate : MonoBehaviour
{
    [SerializeField] private GamePauseController pauseController;
    private readonly string[] buttonNames = { "Attack", "Dash", "ShadowDash", "Interact" };
    private readonly InputAction[] buttons = new InputAction[4];
    private readonly bool[] requireRelease = new bool[4];
    public bool BlocksGameplay => (pauseController != null && pauseController.BlocksGameplay) || Time.timeScale <= 0f;
    private void Awake()
    {
        if (pauseController == null) pauseController = FindAnyObjectByType<GamePauseController>();
        PlayerInput player = GetComponent<PlayerInput>();
        for (int i = 0; i < buttons.Length; i++) buttons[i] = player.actions.FindAction("Gameplay/" + buttonNames[i], true);
    }
    private void OnEnable() { if (pauseController != null) pauseController.PauseChanged += OnPauseChanged; }
    private void OnDisable() { if (pauseController != null) pauseController.PauseChanged -= OnPauseChanged; }
    private void OnPauseChanged(bool _)
    {
        for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null && buttons[i].IsPressed()) requireRelease[i] = true;
        if (BlocksGameplay)
        {
            GetComponent<PlayerRangedWeapon>()?.CancelPendingShot();
            GetComponent<PlayerCombatFacing>()?.ClearAim();
        }
    }
    private void Update()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool pressed = buttons[i] != null && buttons[i].IsPressed();
            if (!pressed) requireRelease[i] = false;
            else if (BlocksGameplay) requireRelease[i] = true;
        }
    }
    public bool CanUse(string actionName)
    {
        if (BlocksGameplay) return false;
        for (int i = 0; i < buttonNames.Length; i++) if (buttonNames[i] == actionName) return !requireRelease[i];
        return true;
    }
    public bool IsHeld(InputAction action) => action != null && CanUse(action.name) && action.IsPressed();
    public bool WasPressed(InputAction action) => action != null && CanUse(action.name) && action.WasPressedThisFrame();
}
