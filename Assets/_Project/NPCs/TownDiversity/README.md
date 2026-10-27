# Women and the construction crew

WorldPrototype's existing population cap remains 12, excluding the stationary weapon merchant. Reserved slots now produce 5 female citizens, 4 ordinary male citizens and 3 male construction workers. Death replacement retains the slot's gender/job and, for builders, the assigned station. This does not add another population on top of the old 12.

## Women

Three modular Female_Peasant variants use a real female outfit, extracted Superhero female head/neck, eyes, eyebrows and Long/Buns hair. They share a single female Humanoid rig per NPC. An explicit human-bone map is stored on the PRIVATE Female_Peasant source copy because Unity's automatic mapping was invalid. Its reference pose is calibrated using the installed Editor's Enforce T-Pose operation, including character-facing correction and skeleton parent names; simply recording the imported bind transforms gave raised arms despite a valid avatar. TownFemaleRigRepair performs this editor-only preparation and grounds the three prefab variants after calibration. The source pack's imports, meshes and materials are unchanged. Head/eyes/hair are skinned to the outfit's bones in the editor, not attached as a second animated body.

Teal/Long, Amber/Buns and Plum/Buns variants cycle across female slots so an initial population cannot randomly be all one hairstyle. The usual walk/run, talking, prop interaction, fleeing, outings, health and death behaviour is retained. The female avatar retargets the existing ambient Humanoid controller; root motion is off and appearance is scaled to approximately 1.78 m. Male citizens retain their existing approximately 1.88 m setup.

## Construction workers

Each male worker has a separate editable TownNpcWorkStation under `Town_NPC_Population/Construction_Work_Routes`. The station root is the work stop; its forward direction controls the work facing. Its two child transforms mark pickup and delivery. All nine stops are on navigation around the existing southwest construction site; buildings, scaffolding and supplies were not moved.

The repeating sequence is return to work -> TreeChopping_Loop for 6–10 seconds -> walk to supplies -> pickup gesture -> Walk_Carry_Loop with visible wood -> delivery gesture -> return to work. Carry speed is 1.1 m/s, empty walking is 1.65 m/s. Navigation, not root motion, moves the worker. The brain remains the only navigation/facing/animation writer; TownConstructionJob plans phases rather than running a competing movement Update.

Regular job paths are constrained to the station's XZ work area. Workers do not join ordinary town conversations, choose random town destinations or obtain outing permits. Enemy danger can temporarily force them to flee within city rules; after safety they return to their assigned station. Unreachable routes retry instead of teleporting or silently switching to an unrelated town routine.

The carried item is a small, colliderless visual bundle of the existing kit's planks. It follows the midpoint of the Humanoid hands while carrying/delivering, and is hidden during empty walking, chopping, fleeing, death and disable. It is not an inventory item and does not add/remove real world resources. No hammer, axe, impact event, knock sound or work audio has been added yet. TreeChopping is used as the requested temporary work gesture; an actual tool/impact timing can be added later.

Workers use the existing neutral CivilianHealth and enemy perception system. They can die and are replaced after the ordinary population delay, at the same work station. Corpse visuals do not count as living population. Pause freezes both the worker's navigation/animation and phase timers.

## Adjusting and recovery

In Edit Mode, adjust `Female Count`, `Construction Workers`, prefab references and construction station references on TownNpcPopulation. These quotas are included in Max Population, with workers reserved first and female slots clamped to the remainder. The fixed slot roles are created when Play Mode starts. Keep at least one valid male citizen prefab for regular male slots.

Move work/pickup/delivery transforms onto reachable clear ground, and keep the local work-area rectangle large enough to include all three stops plus the path around obstacles. Timings and carrying speed live on the worker prefab's TownConstructionJob. Visual wood and looping work clips belong only to the private builder prefab/controller; the old townsfolk controller and merchant are not edited.

Tools > NPC > Women and Builders contains inspection, installation, validation and the Play Mode regression. A completed BuildRecord.json makes the installer one-shot; an interrupted preparation can resume its private generated assets. The record contains a fresh current-scene backup, scope count and original source hashes. Do not restore the entire old scene over newer user changes just to change a quota.

Reports/captures are under Logs/TownDiversity*. No standalone player build is produced by the Play Mode tests.

## Verified in Unity 6000.5.10f1

The mixed-population Play Mode regression passed 26 checks on 2026-10-26 after reference-pose calibration. Each of the three builders completed four deliveries during 85 game seconds of observed routine work. Checks cover real carry movement and animation, local routes, hidden cargo during empty work/flee/death, mixed-gender conversation and walking, relaxed female idle arms, worker recovery from a threat, inventory pause, and gender/station-preserving replacement. The original population/enemy regression also passed all 44 checks with the mixed population, and static validation passed 18 checks. Captures show mixed conversation, work and carrying. The installer preserved 17,090 other pre-existing scene components and records source/merchant/player/navigation hashes for static validation.
