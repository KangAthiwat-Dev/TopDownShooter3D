# Movement-directed top-down camera

**Current implementation:** [CAMERA_VIEW_PRESETS.md](CAMERA_VIEW_PRESETS.md) describes fixed top-down by default and an optional high third-person view. Both keep scenery opaque. This movement-orbit experiment is historical.

**Historical / disabled as of 2026-10-26:** the user found the orbit uncomfortable. WorldPrototype now uses the original fixed camera plus temporary building transparency. See [FIXED_CAMERA_OCCLUSION.md](FIXED_CAMERA_OCCLUSION.md) for the current behaviour and tests. The notes below describe the earlier experiment, not the current scene configuration.

Previously enabled on `CameraRig > CameraFollow` in `Assets/_Project/Scenes/WorldPrototype.unity`.

The camera orbits behind actual horizontal walking/running velocity, not the Player's rotation, head bone, gun barrel or selected enemy. This keeps aiming independent: the player can shoot north while moving east and the camera stays behind eastward travel. Stopping retains the last stable travel heading, allowing an in-progress turn to settle smoothly.

The original camera pose is retained: local offset `(0, 12, -10)`, pitch 50 degrees, base FOV 45. Only the rig's world-Y rotation changes. Existing follow-position damping, sprint bob and dash FOV are retained.

## Inspector controls

| CameraFollow field | Current value | Purpose |
| --- | --- | --- |
| Follow Movement Heading | Off (current scene) | The earlier experiment used On; current fixed-heading behaviour uses Off |
| Heading Smooth Time | 0.4 s | Higher values make turns more gradual |
| Max Heading Speed | 110 deg/s | Limits orbit turn speed |
| Minimum Heading Speed | 0.8 m/s | Ignores tiny movement/collision jitter |
| Heading Confirm Time | 0.12 s | Requires a stable direction before choosing a new heading |
| Heading Dead Zone | 7 degrees | Avoids reacting to small heading fluctuations |

Dash, Shadow Dash and interaction-locked movement do not choose a new camera heading. Inventory/game pause freezes position follow, orbit and bob. The orbit derives its initial alignment from the camera-to-rig offset, so it does not assume a particular authored yaw.

## Deliberately unchanged

- WASD still refers to the existing world axes, not the rotating camera axes. PlayerMovement and combat code were not changed. Recomputing camera-relative movement every frame while auto-orbiting behind it can create circular feedback; any future camera-relative controls need a separately designed heading/input policy.
- Player model, NPC, map geometry, materials, navigation and spawn locations are unchanged by the camera installer.
- Building/wall transparency and camera obstacle avoidance are not implemented in this step. Orbiting improves the available angle but does not guarantee a clear view through all buildings.

## Tools and recovery

**Tools > Camera** has a one-shot installer, static validator and actual-player Play Mode regression. The installer backs up the current live scene before changing settings and refuses to overwrite an existing setup record.

`Assets/_Project/World/MovementCameraRecord.json` records the pre-install scene backup path, original camera pose, movement-source hash and scope-check count. Recover from the scene backup, not by copying the editor-only object reference JSON between scenes. To simply stop orbiting, untick **Follow Movement Heading** on CameraFollow.

Reports: `Logs/MovementCameraValidation.txt`, `Logs/MovementCameraPlaytest.txt`.

Preview: `Logs/MovementCamera_City.png`, captured with the actual main camera after eastward movement along the city street. Tests use temporary Play Mode-only objects and virtual keyboard input; they do not save player relocations into the scene.

Verified on 2026-10-26: 7 static checks and 34 actual-player checks passed. These cover five travel directions, straight world-axis input through orbiting, shooting in a different direction to travel, changing targets while stationary, tiny taps, sprint bob/reset, dash FOV/reset, production inventory pause and requested 30/120 FPS runs. The same-frame LateUpdate sampler measured a peak of 107.07 deg/s against the configured 110 deg/s cap in the cardinal/diagonal checks. No runtime errors were reported. Installation preserved all 17,037 other existing scene components.
