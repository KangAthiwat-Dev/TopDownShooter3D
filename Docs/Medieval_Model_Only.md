# Medieval Village Kit — model-only integration

This project uses the imported Medieval Village Kit as a static art library, not as a replacement for the project's player, navigation, world or gameplay systems.

## Retained library

Base folder: `Assets/Assets_Imported/MedievalVillageKitUnity/com.danielgruginski.medievalkit/`

- `Generated/Prefabs/`: individual ready-to-place pieces.
- `Generated/Structures/`: assembled buildings; their ordinary child geometry is retained.
- `Generated/Materials/`: original material assignments.
- `Art/Models/` and `Art/Textures/`: original model and texture sources.
- `Shaders/`: original URP shaders and HLSL includes, needed to preserve the existing look.
- `LICENSE.md`: original CC0 licence.

Drag a prefab or structure into the scene normally. There is no dependency on the kit's custom C# behaviours, JSON data, test walker, automatic snapping, procedural world tools or demo scenes. Standard Unity mesh renderers, transforms and colliders remain intact. Models without a collider still need a suitable collider when they should block gameplay.

Model-only does **not** mean shader-free: deleting the retained shader files would break the materials. No performance gain in frames per second is claimed without profiling.

The kit-only direct dependency on `com.unity.nuget.newtonsoft-json` was also removed after checking that no other asset C# or assembly definition references it. The project's existing Input System, URP, AI Navigation and Post Processing packages remain unchanged.

## Recoverable backup

The conversion creates a timestamped folder outside `Assets`:

`Backups/MedievalVillageKit_ModelOnly_20261026_102020/`

It contains:

- `OriginalPackage/`: byte-verified copy of the entire original imported package, including its `.meta` files.
- `WorldPrototype_BeforeModelOnly.unity`: live scene backup made before conversion, including any unsaved scene changes.
- `City_BeforeModelOnly.png`: city appearance before conversion.
- `manifest.before.json` and `packages-lock.before.json`: dependency snapshots.
- `RemovedFromAssets/`: the original C# tools, data, rules assets, demo levels and installation instructions moved out of Unity's import tree.

These files are deliberately not loaded or compiled by Unity. Keep them outside `Assets` to avoid importing duplicate GUIDs or re-enabling the original systems.
The timestamped backup folders are ignored by Git; copy them separately if the originals need to be preserved on another machine.

To restore the full kit later, close Unity and first back up the **current** scene and art library. Restore the original library with its original `.meta` files from `OriginalPackage`, and restore the pre-conversion scene only if its old state is actually wanted. Restoring a scene overwrites later layout edits; do not replace it blindly. The original tools also require the package dependencies recorded in the dependency snapshots, including `com.unity.nuget.newtonsoft-json` version `3.2.1`.

## Verification and maintenance

- Conversion helper: `Assets/_Project/Editor/MedievalModelOnlySetup.cs` (editor-only; not part of gameplay).
- Recheck with **Tools > Asset Cleanup > Medieval Kit > Validate Model Only**.
- Conversion record: `Logs/MedievalModelOnlyState.json`.
- Validation report: `Logs/MedievalModelOnlyValidation.txt`.
- Current city preview: `Logs/MedievalModelOnly_City.png`.

The conversion checks every retained prefab for missing scripts, meshes, materials and shader errors; checks original art/material/shader file hashes; compares the live scene's non-kit components before and after conversion; and refuses to archive files still required by the retained models or project scene/settings assets.

Do not run the original **Build All**, **Build Prefabs** or other kit generators against the model-only library: reimporting the full package can put the optional C# components back. Back up any later changes before upgrading/reimporting this asset.

## Conversion result (2026-10-26)

- 1,311 prefabs retained; 1,311 optional kit components detached.
- 48 original C# source files and their assembly definitions archived outside `Assets`.
- 3,540 original model/texture/material/shader files and metadata hashes unchanged.
- All 16,054 non-kit scene components preserved, including 2,291 renderers and 1,058 colliders.
- Ten model-only validation checks passed, including no missing scripts or broken mesh/material/shader references.
- The before/after city screenshots are byte-identical (SHA-256 `d87651cac950173081ff9a3900a87e33761289c8e0d66b0726fd5f58902ffd09`).
- Actual-player city Play Mode regression: all ten checks passed at 10:26, including both gateways, level stone/wood seams, wall collisions, navigation and no runtime errors. Unity was returned to Edit Mode with zero Console errors/warnings in that test run.

Scene-count and image comparisons describe the conversion-time baseline. Later intentional map edits can change these counts and images without indicating a conversion fault.
