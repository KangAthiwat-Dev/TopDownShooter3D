# High third-person camera / camera-relative controls

**Optional preset:** WorldPrototype now defaults to the original fixed top-down view with no transparency. This elevated third-person view remains available through CameraViewSettings. See [CAMERA_VIEW_PRESETS.md](CAMERA_VIEW_PRESETS.md) for selection, persistence, editing and current validation tools.

When selected, the elevated third-person view follows actual walking/running travel, not the player's body, gun or enemy-facing rotation. Aiming still uses the existing auto-targeting and PlayerCombatFacing code. There is no manual camera-rotation input in this version.

## Controls

- W: choose forward in the current camera view.
- D: choose right in the current camera view (A is left).
- S: choose back in the current camera view.
- Diagonals are normalised, retaining the existing movement speeds.
- Shift, Space, M, J, interaction and inventory bindings are unchanged.

The world movement heading is captured when movement starts or the input direction changes; it is retained while that same direction is held. This breaks the positive feedback between automatic camera rotation and view-relative movement. Holding D turns right once and keeps travelling straight while the camera catches up, rather than causing an endless circular path. After it aligns behind that direction, releasing D and pressing W continues straight in the new view. Releasing and pressing D again chooses the new view's right. Changing sprint state alone does not recapture the movement heading. Analogue input magnitude still controls movement strength; a 5-degree direction threshold avoids recapturing from tiny stick jitter.

Idle stops choosing a new camera heading; any existing turn finishes smoothly. Walking backwards with S chooses the original view's back direction and eventually turns the camera behind that travel. Short and Shadow Dash use the same captured input reference but do not choose new camera headings while dashing. Interaction lock and component disable clear the captured heading. Pause preserves the current movement reference and freezes all camera effects.

## Camera settings

On `CameraRig > CameraFollow`, **Follow Movement Heading** and **Align Behind Target On Start** are enabled. The latter aligns behind the spawn facing before the first input frame. The pivot is 1.35 m above the player's root. Main Camera's authored offset is `(0, 6, -8)`, a 10 m boom with 36.87-degree downward pitch, FOV 55, near clip 0.1 m. Follow position damping is 0.1 s. Orbit damping is 0.45 s, max yaw 120 degrees/s, direction confirmation 0.1 s, minimum travel speed 0.8 m/s and heading dead zone 3 degrees. Existing subtle sprint bob and dash FOV remain.

On `Player > PlayerMovement`, **Camera Relative Movement** is enabled and **Movement Reference** points to Main Camera. Defaults remain off for older scenes, preserving their world-axis control scheme. Player movement speeds, acceleration/deceleration, combat facing priority and dash mechanics are unchanged.

`CameraBoomCollision` runs after camera follow/bob. It sphere-casts the boom against solid Environment colliders (radius 0.22 m, padding 0.1 m), immediately shortens inward to avoid clipping, then smoothly extends again over 0.25 s when clear. It ignores trigger volumes and characters. It changes camera position only, never colliders or materials. Collision avoidance needs actual colliders: a purely visual roof with no collider cannot push the camera inward. Very narrow spaces can bring the camera close to the player; no extra self-hiding or fade system is enabled.

`CameraOcclusionFade` is disabled. Its earlier code/assets remain recoverable but no scene materials are made transparent by the camera service.

## Tools / recovery

Use **Tools > Camera > Validate High Third Person Camera** or **Run High Third Person Camera Regression**. Previous experiments are grouped under Legacy camera menus. `Assets/_Project/World/HighThirdPersonCameraRecord.json` records a fresh live pre-change backup, previous scalar camera/control settings and a non-camera scope check. It is not a runtime save file. Do not restore serialized object-reference JSON between scenes; use the scene backup if full recovery is necessary.

Reports: `Logs/HighThirdPersonValidation.txt`, `Logs/HighThirdPersonPlaytest.txt`. Preview: `Logs/HighThirdPerson_City.png`. Tests use the actual PlayerInput/CharacterController/weapon with a virtual keyboard and temporary Play Mode objects. Player warps, test enemies, props and input devices are not saved to the scene. No standalone player build is produced by these tests.

Verified on 2026-10-26: all 8 static checks and 52 actual-player Play Mode checks passed. These cover the requested D -> W sequence, A/S and diagonal travel, a six-second D hold without circular feedback, movement and shooting in different directions, changing enemy aim while stationary, sprint bob, both dashes, solid-wall boom collision/recovery, trigger exclusion, production inventory pause and requested 30/120 FPS runs. Captured-direction lateral drift measured 0.000 m at report precision; peak same-frame camera yaw was 117.63 degrees/s against the 120 cap. No runtime errors were reported. Installation preserved all 17,030 other existing scene components.
