# Winding Forest Map — editable first-pass outdoor level

Main scene: `Assets/_Project/Scenes/WorldPrototype.unity`.

## Layout

200 × 200 m terrain, north = world +Z, east = +X.

| Region | Approximate center (X, Z) | Contents |
| --- | --- | --- |
| Southwest start | (-82.63, -82.18) | Original sanctuary, player spawn and summoning effect retained |
| Northwest village | (-63, 62) | Houses, inn, blacksmith, mill, stable, market, well |
| Southeast town | (61, -57) | Denser buildings, market square, bell tower, low perimeter wall and two gateways |
| Northeast demon castle | (62, 65) | Raised stone ground, curtain walls, six towers, rear keep exterior, open boss courtyard |
| Central camp | (-2, -3) | Campfire, benches, cart; shortcut junction |

No NPC, shop, quest, boss encounter or building-interior gameplay is implemented. FutureNPC / FutureBoss markers reserve positions only. Existing enemy waves and their six spawn markers were not moved or replaced.

## Hierarchy / editing

`World/Map_WindingForest` contains:

- `00_WindingRoutes_EditableWaypoints` — seven named routes and ordinary Transform waypoints.
- `01_Settlements` — separate Village_NW, Town_SE and DemonCastle_NE groups.
- `02_Forest` — original batch of new trees, project-owned ForestOak prefab with trunk-only collision.
- `03_RocksAndUndergrowth` — rocks, saplings, shoulder plants.
- `04_TrailLandmarks` — central camp and minor trail props.
- `06_MapBoundary_VisualCliffsAndSafetyColliders` — cliff visuals plus four continuous invisible boundary colliders.
- `07_AxisAndFoliageFinish` — additional shoulder trees and an extension of the existing forest's local color grading.

Select Map_WindingForest to see region references and route waypoint gizmos. Terrain paint is editable with normal Terrain brushes. Moving a waypoint changes its marker/gizmo only: it does **not** automatically repaint terrain or move trees. Edit the road paint/foliage alongside the marker when changing a route.

BuildingFootprintCollider is a child of its building, so it follows later building moves/rotations. Small decorative props have no collision; buildings, tree trunks, walls and the outer safety boundary block movement. Keep gateways clear. Cliff visuals use the continuous boundary colliders as their reliable physical barrier.

After moving solid scenery, rebake navigation and save the scene. Keep the new output under this feature folder; do not overwrite original source-pack assets or old navigation backups.

## Terrain and sources

- Working terrain: WindingForestTerrain.asset (also used by TerrainCollider).
- Previous terrain: Assets/_Project/World/ForestDemo/ForestTerrain.asset retained.
- Terrain near the original forest center (-60,-60), radius 31 m, and sanctuary radius 20 m preserves original sculpt/painting, blending into the new terrain outside. Original grass edits retained in the protected area.
- Roads follow smooth sampled curves; main routes approximately 6–7 m wide, shortcuts 4.5 m. Broader shoulders let the player fight, strafe and dash without tight trunk collisions.
- Quaternius Medieval Village (`Assets_Imported/villanger`) supplies houses/props. KayKit Dungeon (`Assets_Imported/city`) supplies modular stonework. Inferno World Free supplies towers/cliffs. Fantasy Worlds supplies forest/terrain assets.
- Materials under Materials/ are project-owned URP copies. FBX source axis rotations are preserved before applying gameplay yaw; source packs are not edited.
- Player movement/combat/audio, four waves, original sanctuary objects and magic-circle VFX are unchanged.

## Tools / safety

Tools > World > Full Map:

- Focus Full Map: overview in Scene view.
- Rebake Full Map Navigation: after moving solid scenery, backs up the live scene and creates a new uniquely named NavMesh asset in this folder, preserving prior bakes.
- Validate Full Map: shaders, regions, original spawn, all arrivals/routes and continuous boundary colliders; Logs/WindingMapValidation.txt.
- Capture Full Map: Logs/WindingForestMap_Overview.png; transient render camera, no scene fixtures saved.
- Run Full Map Playtest: actual player input in each area, real Bear navigation, both player layers against every boundary, inventory pause, protected-sculpt audit; Logs/WindingMapPlaytest.txt.

Build/finalization commands are one-shot and guarded to avoid replacing later manual edits. Do not rerun them for normal play. Original scene including unsaved edits was copied to Logs/SceneBackups/WorldPrototype_BeforeFullMap_*.unity before installation; additional snapshots precede finishing passes. Old TerrainData and NavMesh assets remain available.

Verified on 26 October 2026: full-map static validation 142 PASS (including sampled smoothed curves), actual map Play Mode checks 26 PASS, original platform movement/dash/shoot/pause/reload regression 24 PASS. Original start-area sculpt (22,700 height samples), paint and grass-brush contents were compared with the previous TerrainData. Previous TerrainData and sanctuary NavMesh file hashes are unchanged. C# build: zero warnings/errors. No test fixtures were serialized into the scene.

Use the Full Map validator for the expanded level. The earlier Forest Demo/Platform terrain-baseline validators describe the older small-zone phase and expect the *entire* old terrain to be unchanged; they are not the full-map acceptance test.
