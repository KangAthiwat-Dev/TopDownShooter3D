# Camera view presets

WorldPrototype starts in the original fixed top-down view. The elevated third-person camera is retained as an optional preset. Scenery transparency is disabled in both views. There is no settings-screen UI yet; the runtime selection API, preference storage and editor preview tools are ready for that screen.

| Behaviour | Top-down (default, index 0) | High third-person (index 1) |
|---|---|---|
| Main Camera local offset | `(0, 12, -10)` | `(0, 6, -8)` |
| Downward pitch | 50 degrees | 36.87 degrees |
| FOV / near clip | 45 / 0.3 | 55 / 0.1 |
| Follow pivot above player | 1 m | 1.35 m |
| Heading | Fixed, original world orientation | Automatically follows actual movement |
| WASD | Original world axes | Captures the camera heading on a new input direction |
| Camera collision | Disabled to preserve fixed framing | Solid obstacle boom shortening |
| Scenery fade | Disabled | Disabled |

Both modes retain position damping, subtle sprint bob, short dash FOV and existing enemy-facing combat. Player movement speed, targeting, projectiles, NPCs, map and spawn are unchanged. The third-person preset was captured from the live scene before top-down was restored, including its current framing and mode flags. Shared heading damping/confirmation values remain on CameraFollow.

## Wiring a future settings screen

`CameraRig > CameraViewSettings` stores the default and the two editable presets. A dropdown can call `SelectView(int)`: 0 selects TopDown and 1 selects HighThirdPerson. `ApplyView(CameraViewMode mode, bool savePreference = true)` also supports runtime code. Invalid values are ignored. `ViewChanged` and `CurrentView` can keep UI selection in sync.

User selections are saved under the single PlayerPrefs key `TopDownShooter3D.CameraView`. Without a saved selection, startup uses TopDown. `ResetToDefault()` clears only that key and reapplies the authored default. `ApplyView(mode, false)` previews without overwriting the saved preference. Turning off Remember Selection ignores stored choices.

Switching refreshes the follow/orbit rest pose, clears the captured movement input heading, updates the dash FOV baseline, and resets collision-boom distance. It does not reposition the player, change body aiming, or resume a paused game. Camera switching is immediate, suitable for a paused settings menu. Gameplay controls continue through the existing input/pause gate.

For now, use **Tools > Camera > View Presets > Preview Top Down in Play Mode** or **Preview Third Person in Play Mode** to inspect either mode without saving a preference. Edit the preset fields rather than only Main Camera's transform: startup applies the chosen preset.

## Installation and validation

The one-shot installer backs up the current live scene and changes only camera/control components. `Assets/_Project/World/CameraViewPresetsRecord.json` retains the previous third-person preset, backup path and a preservation count for 17,097 non-camera components. Older camera experiment assets remain available; avoid rerunning their legacy installers over the preset controller.

Use **Tools > Camera > View Presets > Validate Top Down Default** and **Run Default and Switching Regression**. The latter tests actual player input, fixed top-down yaw, sprint/dash effects, switching while moving or paused, preference APIs and transparency staying off. It restores the pre-test camera preference on exiting Play Mode. **Run High Third Person Camera Regression** explicitly activates the retained third-person preset without saving a preference.

Reports: `Logs/CameraViewPresetsValidation.txt`, `Logs/CameraViewPresetsPlaytest.txt`, `Logs/HighThirdPersonPlaytest.txt`. Captures: `Logs/TopDownDefault_Spawn.png` and `Logs/TopDownDefault_City.png`. Test actors and relocations exist only in Play Mode; no standalone build is produced.

Verified in Unity 6000.5.10f1 on 2026-10-26: all 23 default/switching Play Mode checks and all 52 retained third-person checks passed, including actual aiming/shooting, dash/shadow dash, solid camera collision, pause, straight held-key movement and 30/120 FPS input checks. Static validation passed 9 default-preset checks and 8 third-person checks. The saved scene remains in top-down mode after tests.
