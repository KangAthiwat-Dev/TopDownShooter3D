# User city — walls and surfaces only

Aligned the existing Floor_city, Wall_city and Way_city groups. No houses, decorations or gameplay were added, and no other region objects were changed.

- Rectangular wall lines: 42 × 44 m, matching the user's existing location and two gate assemblies.
- All stone and wood surfaces: world top Y = 5.140 m. Wall baseline Y = 5 m.
- Reused all 73 stone and 25 wood modules; added 35 stone and 1 wood module to complete coverage.
- Interior stone tiles share a 4 m grid with the wooden routes; boundary pieces are trimmed by scaling. Stone is omitted beneath wooden cells, preventing coplanar overlap.
- Existing gate/arched/pillar/corner/half-wall types and source materials retained. Static floor/wall collision and a fresh NavMesh added for walking through the gates.
- The groups remain ordinary editable scene objects. AlignmentPlan.asset records this pass for validation; it does not run or rebuild anything during gameplay.

Tools > World > City Surfaces offers Inspect, Validate, Focus, Capture and an opt-in Play Mode test. The one-shot alignment command refuses to overwrite later hand edits. Source FBX/material assets and TerrainData are unchanged. The former NavMesh remains available; the scene now references CityAlignedNavigation.asset.

Backups: Logs/SceneBackups/WorldPrototype_BeforeCityAlignment_*.unity, including the user's then-unsaved scene state. Scope report: Logs/CityAlignmentScope.txt (15,153 outside-city components unchanged, excluding the intentionally refreshed NavMeshSurface data reference).

Verification on 26 October 2026: static validation 10 PASS (complete interior coverage sampled at 25 cm, no coplanar overlap, common levels, continuous collision, navigation); real-player Play Mode checks 10 PASS (both gateways, stone/wood seam with 0.000 m measured vertical variation, solid walls, complete navigation path, no runtime errors). Reports: Logs/CitySurfaceValidation.txt and Logs/CitySurfacePlaytest.txt. Preview: Logs/CitySurfaces_Aligned.png.
