# Fixed top-down camera and temporary scenery transparency

**Historical / disabled:** the current scene uses fixed top-down by default and retains high third-person as an option. See [CAMERA_VIEW_PRESETS.md](CAMERA_VIEW_PRESETS.md). The fade experiment below remains disabled in both modes and does not modify materials during gameplay.

The movement-directed orbit was disabled at the user's request because it caused dizziness. `WorldPrototype` uses the original fixed orientation: CameraRig identity rotation; Main Camera local position `(0, 12, -10)`, pitch 50 degrees, FOV 45. Walking, running, turning and aiming do not rotate the rig. Existing position damping, subtle sprint bob and dash FOV remain unchanged. Player controls, combat, NPC, navigation, map layout and spawn position were not changed.

`CameraRig > CameraOcclusionFade` runs after the camera follow update. It checks bounds along camera-to-player rays at hip, chest and head height. It temporarily fades blocking static mesh pieces, including imported roofs with no colliders. Multiple stacked pieces can fade together; it does not fade an entire city/building hierarchy. Bounds checks are conservative, so a roof's bounding-box corner can fade slightly before the visible mesh actually overlaps the player.

## Inspector settings

| Setting | Default | Meaning |
| --- | --- | --- |
| Faded Opacity | 0.15 | 15% remaining opacity while blocking the player |
| Fade Out Seconds | 0.20 | Gradual transition to transparent |
| Fade In Seconds | 0.35 | Gradual return to the original material |
| Release Delay | 0.15 | Prevents flicker at piece boundaries |
| Scan Interval | 0.06 | Visibility detection frequency |
| Candidate Refresh Seconds | 2 | Discovers newly spawned scenery |
| Bounds Padding | 0.12 m | Small visibility margin around the player's body |
| Keep Ground Height | 0.50 m | Leaves low paving/floors opaque |
| Scenery Layers | Default + Environment | Never includes the Enemy/Interactable/PlayerShadow layers |

The player hierarchy, enemy health hierarchies, merchant NPC, skinned characters, particles, terrain and low paving are excluded. Source KitLit and URP Lit/Simple Lit materials are supported without changing their shader, textures, vertex colours, normal maps or moss. Custom Fantasy Worlds foliage shaders are excluded by default to preserve their wind and specialised appearance. `Fade Unsupported Shaders` is an opt-in generic Lit fallback, not an exact reproduction of arbitrary custom shaders.

Transparency affects rendering only: colliders, object activation, navigation and gameplay line-of-sight are unchanged. All shared source materials remain untouched. Runtime-only transparent copies are cached per source material; per-renderer/per-slot property blocks control opacity. Original material slots and property blocks are restored when clear, when disabling the component, or when leaving Play Mode. Pause freezes both camera movement and fade transitions.

Generated material assets under `Assets/_Project/World/CameraOcclusion/ShaderVariants` retain the scene's transparent shader/keyword combinations for builds. If new scenery uses new shader keyword combinations, regenerate/extend those references before a release build; the one-shot installer deliberately refuses to overwrite an existing setup. No standalone player build was produced as part of this change.

## Validation and recovery

Use **Tools > Camera > Validate Fixed Camera and Scenery Fade**, or **Run Fixed Camera and Scenery Fade Regression** for the actual-player Play Mode checks. The old orbit tools are under **Tools > Camera > Legacy Movement Orbit** and are not validators for the current fixed-camera scene.

Reports: `Logs/FixedCameraValidation.txt`, `Logs/FixedCameraPlaytest.txt`. Real city comparison captures: `Logs/FixedCamera_City_Opaque.png` and `Logs/FixedCamera_City_Faded.png`.

Verified in Unity on 2026-10-26: 8 static checks and 27 actual-player Play Mode checks passed. These include real keyboard movement in four directions, progressive fading of stacked/colliderless scenery, original-material/property-block recovery, shared-material isolation, pause, disable/re-enable, walking into a transparent solid wall, sprint bob, dash FOV, and five real city roof/wall pieces. No runtime errors were reported. The scope check preserved 17,031 non-camera scene components. The material copies are runtime-only; custom foliage and all source materials remain intact.

`Assets/_Project/World/CameraOcclusion/FixedCameraRecord.json` records the fresh pre-change live scene backup, restored camera pose, movement-source hash and non-camera scope count. The installer modifies only camera settings and adds the fade service/assets; it does not restore an old whole-map backup. Tests create temporary Play Mode objects and never save player warps into the scene.
