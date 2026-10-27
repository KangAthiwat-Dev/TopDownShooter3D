# Castle approach: stairs, islands and bridge

The user's current arrangement in `Assets/_Project/Scenes/WorldPrototype.unity` is preserved. The old generated cliff layout remains inactive.

## Causes and repair

- The three flat island primitives had CapsuleColliders extending above and outside their visible geometry. Those capsules are retained but disabled; MeshColliders now follow each actual mesh.
- `SM_bridge` had no walkable collision and used an untextured material. It now uses its original base-color atlas, a private copy of its normal texture imported as a normal map, mesh collision, a continuous deck contact, and two short wooden landing pieces.
- Uneven stair treads and terrain seams needed a continuous contact. `CastleRoute_WalkabilityRepair/Stair_To_Island_Smooth_Contact` follows the existing lower passage beside the rock ridge, rather than crossing its tall crest. It does not alter the terrain, rock transforms, painted textures, player step offset, or slope limit. The maximum slope envelope adjustment above sampled ground is 0.135 m.
- Repaired walking surfaces use the Environment layer so ordinary movement and shadow dash both collide with them.
- Navigation was rebaked to a new asset in `Assets/_Project/World/CastleRouteRepair`; the original navigation asset is preserved.

## Validation

`Logs/CastleRoutePlaytest.txt` records a successful actual-player test: stairs and the passage to the first island, bridge in both directions, continuous sprint, ordinary dash, shadow dash, and movement across the next two islands. Navigation connections and unchanged player step/slope settings also passed, with no runtime errors.

`Logs/CastleRouteBuild.txt` records the initial repair and preserved source files. `Logs/CastleStairContact.txt` records the final fitted approach. `Logs/CastleRoute_Bridge_Gameplay.png` shows the textured bridge during the test.

## Editing later

The repair objects are grouped under `CastleRoute_WalkabilityRepair`. If stairs, bridge, or islands are moved again, refit their walking contacts and rebake navigation to match. `Tools > World > Castle Route > Run Actual Player Traversal Test` repeats the traversal test against the stored route in `Assets/_Project/World/CastleRouteRepair/RoutePlan.json`.

Scene backups are in `Logs/SceneBackups`. The disabled original island capsules should remain disabled; enabling them recreates the obstruction.
