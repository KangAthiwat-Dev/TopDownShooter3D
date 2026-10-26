# City expansion — construction, armory, rounded streets and ground support

Implemented in `Assets/_Project/Scenes/WorldPrototype.unity` on 2026-10-26. All new geometry is static; no NPC, working shop, production, crafting or construction gameplay was added.

## Hierarchy and editing

`City_Streets_Construction_Armory`

- `00_Previous_Floor_and_Wood_Roads_Inactive`: the original `Floor_city` and `Way_city`, kept with prefab links and transforms intact but disabled. Do not enable them on top of the new floor.
- `01_Construction_Site_SW`: unfinished walls/roof trusses, assembled scaffolding and ladder, stone/brick/plank/log supplies, cart, wheelbarrow and worktable. Reuses all nine original construction sketch/tool objects.
- `02_Armory_Block_SE`: two-storey modular red-roof armory, covered forge with chimney, anvil, grinding wheel, tools table, weapon rack, armour stand and target. Reuses all six user weapon-shop props; other existing shops/buildings were not relocated.
- `03_Continuous_Paving_Rounded_Streets`: level city paving, a continuous rounded road mesh, low curved visual kerbs and flat gate-approach support.

The armory has empty future NPC/interaction anchors near X 86.4 / Z -87.7 to -86.8. The construction site has a builder anchor near X 70.8 / Z -82. They are only transform markers, not active gameplay. The armory building is a closed exterior; the forge forecourt is accessible. Construction scaffolding is fenced out by a simple solid footprint for now, rather than a playable climbing structure.

## Streets and physical floor

- The previous route layout and two gate approaches are retained: central east–west road, north–south spine, southwest turn and southeast shop spur.
- Road outlines are baked from smoothed, rounded segment unions at 20 cm sampling. They are continuous meshes, not overlapping rectangular planks. Intersections, turns and endpoints have rounded edges.
- City paving collision remains level at Y = 5.14 m, using a simple continuous box instead of hundreds of tile seams.
- Road artwork is 1.4 cm above that floor; its UVs use an existing flagstone texture from the kit. Warm paving and cooler road materials are new local URP Lit materials, not edits to source materials.
- Kerbs are 24 cm-wide low chamfered strips, visually up to 7.9 cm above the paving. They deliberately have no collision so players can cross smoothly; they are not barriers.
- New simple collision and navigation modifiers block solid building masses, workshop equipment and the construction footprint. The retained source model pack still has no MedievalKit C# runtime dependency.

## Grounding the city

The city floor was already level at 5.14 m, but the original terrain below its eastern corners was approximately 3.4–4 m, leaving a visible air gap.

- A new `GroundedCityTerrain.asset` was copied from `Assets/_Project/World/WindingForestMap/WindingForestTerrain.asset`.
- Only height samples under the city, wall margin and gate approaches were adjusted, with a 6 m smooth transition to the surrounding terrain.
- Terrain underneath the complete city footprint is now flat at Y = 4.99 m, meeting the city wall/floor foundation.
- 19,159 height samples changed. All height samples beyond the scoped transition region are unchanged. Terrain paint/grass/tree data was not deliberately edited.
- Terrain and TerrainCollider reference the same new TerrainData. The original TerrainData file is byte-unchanged and remains available.

Do not move the entire city down to fix terrain again: existing buildings, NPC anchors, fountain and walls keep their authored elevations.

## Navigation and safety

Active navigation is stored in `CityExpansionNavigation_Final.asset`. Previous city, northwest-quarter and original full-map navigation assets were not overwritten. The first expansion bake is retained in `CityExpansionNavigation.asset` as an intermediate.

The initial scope comparison checked 15,968 pre-existing components outside the floor/road replacements and 15 deliberately reused props. Existing northwest buildings, other city buildings, walls, furniture, player and other zones were unchanged. Only the terrain data/collider and navigation references were intentionally refreshed outside the new root.

Live scene backup, including any unsaved changes at the time:

`Logs/SceneBackups/WorldPrototype_BeforeCityExpansion_20261026_122632.unity`

Original floor/road geometry is also recoverable from the disabled group. Do not restore the old scene over later manual edits without a separate current backup.

## Tests and tools

Use **Tools > World > City Expansion** for inspection, validation, captures, focus and the actual-player Play Mode regression. Build/finish operations are one-shot and refuse to overwrite a completed feature. The old city-surface validators expect the old rectangular tile groups and are no longer the validator for these deliberately replaced streets.

- Static validation: 18 checks, including level terrain, unchanged original TerrainData, continuous flat road, curved kerbs, valid materials, no missing scripts, reused props and grounded navigation.
- Actual player: 14 Play Mode checks — both gate approaches, the rounded southwest road turn, crossing a kerb into the armory forecourt, building collision, the construction access lane, the blocked scaffold footprint and a complete navigation path between the two new zones.
- Reports: `Logs/CityExpansionAudit.txt`, `Logs/CityExpansionScope.txt`, `Logs/CityExpansionValidation.txt`, `Logs/CityExpansionPlaytest.txt`.
- Images: `Logs/CityExpansion_Before.png`, `Logs/CityExpansion_Preview.png`, `Logs/CityExpansion_Armory.png`, `Logs/CityExpansion_Construction.png`.

For future manual edits, adjust the named groups and the three local materials. Moving building/collision objects requires a new navigation bake. The baked road meshes are regenerated by the editor helper's route/distance-field definitions, not by importing the original kit's procedural tools.
