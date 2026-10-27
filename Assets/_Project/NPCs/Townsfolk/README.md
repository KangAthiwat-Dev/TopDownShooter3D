# Town NPC population

Scene: `Assets/_Project/Scenes/WorldPrototype.unity`. Controller: `Town_NPC_Population`.

Current population composition: 5 women, 4 regular male townsfolk and 3 male construction workers, still 12 total. See [../TownDiversity/README.md](../TownDiversity/README.md) for female assembly, reserved role replacement and the local construction work/carry loop. The original male prefabs and ambient controller below remain shared/recoverable.

## Behaviour

- 12 ordinary, neutral citizens are spawned at runtime on existing city navigation. The named weapon merchant is separate, unchanged, and not counted in those 12.
- Citizens walk at 1.65 m/s, occasionally run at 4.5 m/s, idle for 2–5 seconds, stop to talk in pairs, or visit a prop activity. Conversation partners stop, face each other, play the looping `Idle_Talking_Loop`, and release each other when interrupted. `Interact` plays on arrival at an interaction point.
- Four original male clothing/hair palettes reuse the modular male Peasant rig/head; the later diversity extension adds real female variants and a builder prefab. One Humanoid Animator per citizen; no body duplication, root motion, runtime mesh extraction or imported-pack changes.
- The ambient animation controller blends Idle / Walk / Run by actual agent speed. Talking, Interact and Death are independent states, using private UAL1 clip copies with animation events removed. Only intended loops are looped; imported clips/settings are untouched.
- Most citizens are residents. One slot always permits a roaming personality and other slots have a 30% random chance per life. Roaming personalities still spend most of their time in town: 18–45 second outing cooldown, then a short trip with 8–16 seconds outside exploration before returning.
- At most 2 outing permits are active, including outward/return travel and fleeing while on a trip. Even if Max Outside is raised, it is capped below half the population. Resident paths reject outside destinations AND outside path corners; residents also flee within city bounds. Permits are released on safe return, death or disable.
- The existing NavMesh connects the city gates and five nearby outdoor destinations. NPCs follow complete reachable paths; they do not teleport through walls or need a map rebuild. Stuck/unreachable routines retry instead of accumulating broken destinations.
- Grounded solid props placed/moved after the map's last bake get runtime-only NavMeshObstacle helpers beneath the population root when their centres still contain a walkable polygon. Colliderless urban Kit/URP meshes (such as the user's fountain) have a conservative bounds fallback, excluding foliage and elevated roofs. Existing baked blockers are left alone. These helpers prevent navigation through newer decorations without editing the original prop or NavMesh asset; startup waits 0.3 game seconds for carving to apply. They are a snapshot of Edit Mode scenery, not a general moving-prop synchronisation system or physical player collision. Add an actual Collider separately if a colliderless prop also needs to stop the player's CharacterController.

## Enemies, damage and death

Citizens notice visible enemies in a 10 m radius (Environment walls block sight), interrupt talking/activities and flee at 5 m/s. They recover after 4 seconds without visible danger. Flee destinations are checked against navigation and residency restrictions. A trapped resident may be caught and killed; there is no combat/fighting back in this implementation.

EnemyAI can acquire a living civilian through its existing frontal sight cone and world-occlusion checks. A visible player retains priority. Once acquired, a civilian uses the same last-seen / investigate / search / return behaviour as the player; hiding does not leak the civilian's live position. EnemyAttack rechecks the intended victim at the delayed hit frame so a target switch cannot redirect an already-started swing to somebody else.

`CivilianHealth` implements the existing IDamageable interface (50 HP), not EnemyHealth. Citizens are Default-layer objects with no Targetable, so player auto-targeting does not select them. Existing physical projectiles can still hit/damage a citizen in crossfire; no new friendly-fire exemption was added. At death, navigation stops, collision switches off, the death animation plays, and the corpse is removed after 4.5 game seconds.

Each population slot schedules exactly one replacement 12 game seconds after death. Healthy citizens are not periodically replaced and repeated damage cannot queue extra spawns. Spawn points avoid solid scenery, nearby living citizens, the player and nearby enemies; if no safe spot exists the same vacancy retries after 2 seconds. The cap counts living slot occupants, not short-lived corpses. Inventory pause freezes AI, navigation, animations, outing/death timers and pending replacements.

## Adjusting the system

On `Town_NPC_Population > TownNpcPopulation`, adjust Max Population, Max Outside, Roaming Citizen Chance, Respawn Delay and retry timing. Set the population cap in Edit Mode before starting Play Mode; the fixed slot array is created at Start. Edit the XZ city center/size if walls move (the initial bounds include an inner margin). Prefabs and city/outside destination arrays are explicit references.

Children under `Editable_Routes_and_Activities` are normal editable points. Move them onto supported reachable ground. Interact-point rotation determines the citizen's facing towards a prop. Social points are gathering destinations; conversations are also allowed when two available citizens meet elsewhere. Interaction/talking here are ambient animations, not player dialogue, a shop UI, item transfers or quests.

Prefab folder contains Citizen_Russet / Sage / Blue / Plum. Movement, threat, idle and excursion timings live on TownNpcBrain. Material copies in Materials are safe to recolour without changing the merchant or imported pack. AI and health run offscreen; Animator culling reduces offscreen bone work.

## Verification / recovery

Tools > NPC > Townsfolk provides source/navigation inspection, one-shot installation, static validation and a Play Mode population/enemy regression. The installer refuses to overwrite an existing population folder/root and backs up the CURRENT live scene before adding anything. `BuildRecord.json` contains the backup path, original source hashes, route counts and preserved-component count. No existing map geometry, camera, player, spawn point, merchant or navigation asset is edited by installation.

Reports/captures are under Logs/TownNpc*. These are development output, not shipped gameplay assets. Tests modify only Play Mode instances and restore/exit without saving NPC warps or fixtures. No standalone player build is produced by these tests.

Verified on 2026-10-26: 18 static checks and 44 real-city Play Mode checks passed, including walking/run speed and bone motion, paired talking, prop interaction, frontal/blocked enemy detection, last-seen memory, fleeing/recovery, real timed enemy damage, death animation, one-for-one replacement, outing cap, real gate exit/return, inventory pause and the colliderless fountain's blocked navigation footprint. The existing enemy-awareness regression also passed all 43 checks, and the Spider/Bear animation/combat/audio/wave regression passed all 39 checks. No runtime errors were reported. Installation preserved all 17,037 pre-existing scene components and the recorded original model/animation/merchant/player asset hashes.
