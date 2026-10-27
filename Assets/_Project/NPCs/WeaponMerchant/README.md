# Weapon merchant NPC prototype

Scene: `Assets/_Project/Scenes/WorldPrototype.unity`

Prefab: `NPC_WeaponMerchant.prefab` in this folder.

## What is implemented

- One male Peasant merchant at the existing `Future_Weapon_Shop_NPC_Anchor` in the southeast armory forecourt (approximately X 86.4 / Y 5.14 / Z -87.7).
- Universal Base Characters Superhero male head, eyes, eyebrows and Simple Parted hair, attached to the Peasant's single 65-bone rig. The hidden full body is not rendered under the clothing.
- Valid Humanoid Avatar and an independent, looping Idle animation/controller copied from the project's existing idle. Root motion is disabled; the NPC stays at its authored position.
- Smooth body turning towards a nearby living player: notice radius 4 m, release radius 5 m, turn speed 105 degrees/second. Environment-layer walls block sight. The NPC returns to its original facing when the player leaves or is hidden; pause freezes animation and turning.
- Capsule collision blocks walking through the NPC. A stationary carving NavMeshObstacle lets navigation route around it without rebaking the full map.
- No EnemyAI, EnemyHealth or Targetable component. This is a neutral visual prototype, not a combat actor.
- Stable NPC ID `armory_merchant_01`, display name and empty future dialogue/nameplate anchors.

Not implemented: conversation, E-to-talk prompt, shop UI, buying/selling, quests, patrols, voice, character creator or runtime equipment swapping.

## Safe editing

- Move the NPC/its existing parent anchor to relocate it. Keep the merchant on supported ground; do not move the Player spawn.
- `NpcIdleLook` exposes the notice/release distances, turn speed, sight mask, identity and player reference. Only `Facing` rotates; the physical root stays still.
- The five local `Materials/Merchant_*.mat` files are unique to this merchant. Adjust their Base Color/tints without changing the imported pack's shared materials. Skin and hands use different UV textures and should be tinted consistently.
- The eight SkinnedMeshRenderers share one rig. Do not simply drop a second full-body model or Animator onto the NPC to change clothes. New skinned parts must be matched to the existing bones and bind pose first.
- Original model packs and the Player visual/controller are unchanged. Private source copies in `Sources/` hold the prototype's import settings. Generated head/hair/eye meshes are under `Meshes/`; head extraction is an editor-time operation, not a per-frame runtime task.
- The original sources face -Z; `Peasant_Visual` has a 180-degree model correction. The behaviour's `Facing` pivot uses +Z as forward.

## Tools and recovery

Use **Tools > NPC > Weapon Merchant** for source inspection, validation, captures, focus and the actual-player Play Mode test. **Build and Install Prototype** is one-shot and refuses to overwrite later manual edits.

`BuildRecord.json` records the live pre-install scene backup, source hashes and the original-component preservation check. The scene backup is outside Assets under `Logs/SceneBackups/`. Do not restore it over newer manual work without first making a fresh backup.

Reports/captures are generated under `Logs/NpcMerchant*`; these logs and backup files are intentionally not included in Git.

Verified on 2026-10-26: 14 static checks and 21 actual-player Play Mode checks passed. The latter covers active Idle deformation, true face-forward orientation, smooth tracking, radius hysteresis, line-of-sight obstruction/recovery, the production inventory-pause controller, player collision/grounding, neutral targeting, navigation carving and an unobstructed shop path. During the measured paused interval both animation-time advance and facing change were zero. No runtime errors were reported. The pre-install scope comparison preserved all 16,946 pre-existing scene components (apart from adding a child to the designated NPC anchor).
