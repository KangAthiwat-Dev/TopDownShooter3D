# Adventure HUD and town music

WorldPrototype uses the imported Free Fantasy HUD Sampler artwork for its implemented gameplay displays. Music is `PerituneMaterial_Pastorale3`, played only while the player is in the city. Existing map, NPCs, combat, inventory data, camera presets and player spawn are retained.

## City music

`City_Music_Zone > TownMusicZone` owns one looping 2D AudioSource. Its bounds match the city: X 54.7626–96.7626, Z -94.7745–-50.7745, with a broad supported height range. A 1.5 m exit margin avoids rapid on/off changes near the gate. Position checks handle walking, scene spawn and teleports without relying on trigger callbacks.

The source starts silent. Entry fades up over 3.5 seconds to volume 0.22, and exit fades out over 2.5 seconds. A smooth envelope reverses from its current value if the player re-enters during a fade. Once fully silent, the source pauses; returning resumes the same song position with a fresh fade. Disable stops and resets the source. The existing pause controller freezes music playback and its envelope during inventory/game over.

The source file's measured average level is -10.8 dBFS, with peaks at 0 dBFS; the chosen source volume applies about -13.2 dB gain so it serves as background music. Perceived balance can still be adjusted against the user's listening level. Music Volume, Fade In Seconds, Fade Out Seconds, area name, bounds and exit margin are Inspector fields. Music does not change the existing weapon/monster sound volumes or global AudioListener volume.

The long clip streams from a private copy at `Assets/_Project/UI/AdventureHUD/Audio/City_Pastorale3.mp3`, avoiding a fully decompressed 149-second clip in memory. The original MP3 and its importer remain unchanged.

## HUD

- Top left: real health and shadow energy as red/blue bars with fixed decorative frames. Labels and numbers on these bars are hidden at the user's request.
- Above health: the sampler's gold XP bar, with level and rank in a separate header row. The bars themselves have no text. Initial display is LV. 1, UNRANKED, and empty level progress.
- Top centre: current area and area type, read from the city zone and existing map landmarks.
- Top right: a compass that shows world north relative to the current camera heading. It is a compass, not a terrain minimap and adds no second rendering camera.
- Bottom centre: 8 supply slots plus 2 melee slots, reading the existing PlayerInventory and PlayerMeleeEquipment. Selected melee slots use the gold frame; clicking the two weapon slots still changes the equipped selection.
- Bottom left: actual short-dash cooldown, shadow-energy readiness, and the existing I/Tab inventory button.
- Bottom right: current ranged/melee weapon mode and attack hint. No invented ammunition counter is shown.
- The inventory window, item details, interaction prompt, close button and game-over/retry presentation share the same parchment/bronze theme. Existing modal, pause, selection and restart behaviour remains authoritative.

The original health/energy panels and hotbar are retained inactive for recovery, with exactly one active presenter per display. The original inventory prefab is not overwritten; current scene overrides provide its skin. The HUD adapts using the existing 1920x1080 CanvasScaler reference. Decorative images ignore pointer raycasts, while real buttons remain interactive.

Currency and quest assets stay unused until those systems exist. The gold XP artwork is now used for the requested level-progress display. PlayerProgressionState stores only level, rank and normalized progress; future progression/save code can call SetState(level, rank, progress). PlayerProgressionUI subscribes to that state. No XP rewards, level thresholds, rank ladder or automatic promotions were invented. The gold radial artwork serves as an ability frame; its inner mask reads the actual dash cooldown or energy value.

## Artwork and maintenance

Private PNG copies under `Assets/_Project/UI/AdventureHUD/Sprites` have alpha-bound sprite rectangles, separate health/energy fill slices and sliced panel borders. Original sampler PNG bytes and import settings are preserved. Unity's bundled `com.unity.2d.sprite` 1.0.0 editor package enables the sprite slicing APIs; no external asset pack was downloaded. The original sampler's license remains at `Assets/_Project/UI/HUD/LICENSE.txt` (Antahonist; game use permitted).

Edit the generated scene HUD under `HUDCanvas > AdventureHUD`. Adjust town music on `City_Music_Zone`. `AdventureHudUI` only reads gameplay state. PlayerMovement exposes its cooldown values for the HUD without changing dash timing. InventoryHotbarUI and InventorySlotUI retain their old styling when no themed sprite references are assigned.

Tools > HUD and Audio contains the one-shot installer, validator and actual-game regression. `BuildRecord.json` stores a fresh live-scene backup, hashes of original artwork/music and the non-HUD preservation count. The installer refuses to overwrite a completed installation. Reports and game screenshots are under `Logs/AdventureHud*` and `Logs/AdventureHUD_*`; no standalone build is produced.

Verified in Unity 6000.5.10f1 on 2026-10-26: all 29 music/HUD Play Mode checks, 59 existing inventory checks and 11 static setup checks passed. Music tests cover real nonzero streaming output, entry/exit, reversing a fade, boundary hysteresis, pause and preserving the song position. HUD checks use real damage, energy, dash input, stack changes and two-weapon selection. Inventory checks include I/Tab/Escape, pickup interruption, gamepad navigation, independent game-over pause and an actual scene restart. Layouts were visually inspected at 1920x1080 and 1366x768. No test stock or temporary actors were saved into the scene. The installation preserved 16,410 existing components outside the HUD.

The later level/rank UI update is installed by Tools > HUD and Audio > Add Gold Level and Rank Above Health. It backs up the current scene, adds the display state to the player, places the gold bar above health and hides only the health/energy text children; existing health/energy event bindings still drive their fill amounts. All 32 updated music/HUD Play Mode checks passed, including hidden HP/energy text, default level/rank and live progression display updates. Original XP artwork and importer are preserved in the source hash record. Level_Rank_Preview captures use temporary test values and reset them before the default-state screenshot; the saved scene starts at level one and unranked.
