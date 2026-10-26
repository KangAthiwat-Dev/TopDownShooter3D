# Northwest red-roof city quarter

Built in `Assets/_Project/Scenes/WorldPrototype.unity` on 2026-10-26 using the **model-only Medieval Village Kit**. This is a static exterior art/layout pass, not a procedural generation or shop system.

## Edit in the Hierarchy

`Building_city / NW_RedRoof_Quarter`

- `00_User_Sketch_Preserved_Inactive`: the ten original loose wall/roof example pieces. They keep their original world positions and prefab references; the group is disabled to avoid duplicate buildings. Do not enable it on top of the finished quarter.
- `01_Modular_Buildings`: six independently movable building roots, each with walls/corners, red roof modules, facade details, future anchors and an exterior collision volume.
- `02_Alley_Layout_Reference`: empty transform waypoints describing the reserved walkways; not visible geometry or gameplay.
- `03_Courtyard_Details`: a bench and two planters beside the central alley.

| Building | Centre X/Z (m) | Uniform scale | Storeys | Exterior identity |
| --- | --- | --- | --- | --- |
| 01_Merchant_Corner | 60.2 / -55.6 | 0.82 | 2 | Timber/plaster, balcony, chimney |
| 02_Rose_Townhouse | 69.4 / -55.6 | 0.80 | 2 | Stone base, plaster upper floor, balcony |
| 03_Bakery_Counter | 60.1 / -61.4 | 0.84 | 1 | Shop-window module and striped awning |
| 04_Apothecary | 69.7 / -61.0 | 0.88 | 2 | Narrow gabled house, green shutters, potion sign; faces east/main road |
| 05_Craft_Workshop | 60.3 / -66.3 | 0.84 | 1 | Stone shopfront, anvil sign, chimney |
| 06_Cobblers_House | 69.4 / -66.3 | 0.82 | 2 | Ochre plaster, timber upper details and boot sign |

All main buildings use red tile roofs (`M_VK_RoofRed`), reduced uniformly from the original art scale. Ground storeys are 2.40–2.64 m tall, compared with the current player's 2 m height / 0.9 m controller diameter. Ground level remains Y = 5.14 m. No source prefab, material, texture or shader was edited; style differences are scene-instance material assignments.

## Walkways and future interaction

- Central north–south alley: X ≈ 65 m.
- Upper side alley: Z ≈ -58.5 m; connects back to the existing wooden main road.
- Lower side alley: Z ≈ -64 m.
- Original wooden roads, city walls/gates, existing bell tower/fountain and other map zones remain unchanged.
- Each building has `04_Future_Shop_Anchors_No_Gameplay/NPC_Stand_Point` and `Shop_Front_Point`. These are only empty transforms: no NPC, interaction trigger, shop code or items were installed.
- Exteriors are closed shells. The existing door/shopfront geometry is decorative; players cannot enter the houses yet. Future interiors/door-opening logic need a deliberate collision update.

The six building collision boxes live in `05_Solid_Exterior_Collision`; Unity AI Navigation modifiers mark the closed roof masses as not walkable. Decorative model colliders are disabled inside the new building groups so protruding trim does not snag the player. The bench and planters have their own simple colliders.

## Verification

- 14 static checks passed: parcel bounds clear of roads/walls, reduced scales, red roof materials, no missing shaders/scripts, collision shells, preserved sketch, future anchors and complete capsule/navmesh clearance on the main and upper cross-alley routes.
- 9 actual-player Play Mode checks passed at 11:10: main alley, upper/lower side alleys, level floor support, both street-facing building collision tests and no runtime errors.
- Initial scope comparison preserved 16,055 pre-existing components outside the ten deliberately archived sketch pieces; only the navigation surface data reference was refreshed outside the new group.
- Active navigation asset: `NorthwestNavigation_Final.asset`. The first bake, `NorthwestNavigation.asset`, is retained as an intermediate reference. Older city/full-map navigation assets were not overwritten.
- New quarter: 127 modular prefab instances plus three courtyard details; no imported MedievalKit C# was restored.

Reports and previews:

- `Logs/CityNorthwestAudit.txt`
- `Logs/CityNorthwestScope.txt`
- `Logs/CityNorthwestValidation.txt`
- `Logs/CityNorthwestPlaytest.txt`
- `Logs/CityNorthwest_Preview.png` and `Logs/CityNorthwest_Overview.png`

## Safe continuation

Use **Tools > World > Northwest Quarter > Focus Red Roof Quarter**, **Validate Red Roof Quarter**, **Capture Red Roof Quarter** or **Run Actual Player Alley Test** as appropriate. The build/detail commands are one-shot and refuse to replace a completed group, preserving later manual work. After moving buildings/colliders, intentionally rebake navigation and check the reserved alleys again.

Pre-build live scene backup (including the original example pieces):

`Logs/SceneBackups/WorldPrototype_BeforeNorthwestQuarter_20261026_110623.unity`

Do not rerun the old whole-map or city-surface builders to revise this neighbourhood. The previous model-only conversion's scene-component-count baseline predates these intentional additions; a changed count alone is not a model-only conversion fault.
