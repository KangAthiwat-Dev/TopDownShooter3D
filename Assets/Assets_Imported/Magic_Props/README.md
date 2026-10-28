# Magic Props

Project note: this checkout retains only the Red, Blue and Green potion bottle
FBX files and their shared palette, README and LICENSE. Other props were removed.
Game-ready URP prefabs, icons and consumables are in `Assets/_Project/Items/Potions`.
See `Docs/POTION_SHOP_AND_USE.md` for integration and usage.

A free low poly collection for fantasy environments and inventory props.

## Contents
Spellbook; potion bottle in blue, red and green; staff; cauldron; mortar and pestle; candle holder; chest; ingredient jar; scroll; crystal amulet; mushroom cluster; filled ingredient pouch. Extras: key, coin, crystal and small bottle.

19 individual assets in each of BLEND, FBX and GLB; one assembled animated chest in each format; one assembled Blender demo; one palette texture. Color variants and separate chest parts are included in the 19 meshes.

Open Demo/MagicProps_Demo.blend to explore the complete collection. Use individual files from Blender, FBX or GLB in your project; use MagicChest_Animated in the chosen format for the assembled chest.

## Use
Keep the Textures folder next to the format folders. Blender and GLB files contain the palette; if an FBX importer does not reconnect its embedded texture, select Textures/T_Magic_Palette_BC.png for base color. Use the supplied material values as a starting point: Matte metallic 0 / roughness 0.72; Cloth 0 / 0.95; Gem 0.18 / 0.24; Metal 0.62 / 0.38. The gem material is opaque.

The chest uses Chest_Root and Chest_LidPivot with rigid object animation, not a skeleton. Its open/close cycle spans frames 1-150 at 30 fps: closed at 1 and 150, open at 60-90. Move the root to position the assembly. For an FBX reimport into Blender, set Animation Offset to 0 to preserve the source frame numbers. Engine interaction must be implemented in your project.

## Technical notes
19 individual render meshes, including potion variants and separate chest body/lid; 10,208 triangles total, 34-1,534 per mesh. One shared 256 x 128 palette with 32 colors and four materials. UV0 uses the palette; UV1 is a separate lightmap layout. Base meshes only (LOD0).
The individual BLEND/FBX files include 71 convex UCX collision hulls in total. The demo, assembled animated chest and GLB files do not include collision geometry. Keep collision proxies separate from visible meshes and configure them in the destination engine.
Checked in Blender 5.2 with FBX/GLB reimports. Unreal Engine and Unity integration, runtime physics and light baking have not been tested. Material and collision setup may be required. No native engine project or gameplay interaction is supplied.

## License
See LICENSE.txt for the applicable download terms. Parts of this pack were developed with AI assistance.
