# Spider / Bear enemies

Project-owned Generic animation controllers, clips and URP materials. Source packs remain unchanged.

## Prefabs and gameplay

- `Assets/_Project/Prefabs/Enemies/Enemy_Spider.prefab` — Black Widow, HP 80, patrol 1.5 m/s, chase 4.6 m/s, damage 10, reach 1.6 m.
- `Assets/_Project/Prefabs/Enemies/Enemy_Bear.prefab` — Bear_4, HP 280, patrol 1.4 m/s, chase 4 m/s, damage 20, reach 2.2 m.
- Both inherit EnemyBase contracts: EnemyAI perception/memory/patrol, Targetable, EnemyHealth, EnemyAttack, EnemyAnimationController and EnemyDeathController.
- WorldPrototype wave 4 adds Spider ×3 and Bear ×1. Existing waves, markers, terrain and navigation were preserved.
- Player's equipped one-hit melee still kills either creature and triggers normal wave death bookkeeping.

## Animation and visuals

Each controller uses Speed 0 = Idle, 0.5 = Walk, 1 = Run, plus Attack and Die triggers. Bear uses its own dedicated RunForward. Spider has no separate Run: its Walk plays at 2.2× for chase. Spider Attack1 plays at 1.8×; damage is delayed about 0.585 s, cooldown 1.583 s. Bear damage delay is 0.51 s, cooldown 1.8 s.

The source rigs face -Z. CreatureVisual rotates 180° to align with gameplay +Z and normalizes size/ground level without changing source files. Root motion is disabled; the navigation root alone moves the enemy. Keep its kinematic Rigidbody interpolation None.

## Audio

No source pack audio was present. EnemyVoice uses project Audio/Mobs files:

- Spider: spider-1 (ambient/alert/death), spider-2 (ambient/hurt), spider-attack (attack).
- Bear: sound.mp3 for all voice roles (the only available Bear clip).

Adjust EnemyVoice clips, volume, pitch range and ambient interval on each prefab. Its CreatureVoice child contains one non-looping 3D AudioSource (linear 4–25 m falloff). No play-on-awake and no overlapping voice layers. Higher priority feedback replaces lower priority; the same long roar is not restarted every swing. Death overrides other feedback. Inventory pause freezes voices and corpse cleanup. Death delay allows the voice/animation to finish before destruction.

EnemyAttack exposes an optional AttackStarted event for voice synchronization; enemies without EnemyVoice retain their previous behavior.

## Verification / editor tools

Under Tools > Enemies:

- Inspect Spider and Bear Sources: Logs/CreatureSources.txt.
- Validate Spider and Bear: Logs/CreatureValidation.txt.
- Capture Spider and Bear: Logs/SpiderBear_Preview.png, temporary preview objects only.
- Run Spider and Bear Regression: Logs/CreaturePlaytest.txt, isolated temporary NavMesh, real prefabs, rig motion, displacement, combat, sound, pause and wave lifecycle.

Do not rerun installation on an installed feature: guards intentionally prevent replacing edits. The recovery command only accepts an interrupted installation with neither final prefab present.
