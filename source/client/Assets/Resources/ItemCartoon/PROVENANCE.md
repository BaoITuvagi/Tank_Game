# Item art provenance

## Active artwork — Kenney compositions and one coin

The active `catalog.json` uses `kenney-flat-v1`. All six item types share the project's existing, untouched `ArenaArt/crateMetal.png` (28×28, GUID `b1addcef057347640af4534e5e5b4c87`). Explosive ammunition and splitting ammunition additionally draw the existing `ArenaArt/bulletSand1_outline.png` (8×14, GUID `9e3c0d85247c9fc42bb525fdff21aedb`). These sources belong to **Kenney — Top-down Tanks Remastered**, under CC0; the original `ArenaArt/Kenney-LICENSE.txt` is retained. No external asset pack, new dependency, image generation, source recolouring, source raster editing or source importer change was used for this revision.

`ItemIconArt.cs` draws each original crate and overlays code-native flat functional marks: a medical cross, riveted steel plate, explosive burst and original shell, cleansing canister and drop, gold protection shield, or three original shells with branching paths. Outlines, upright orientation, restricted materials and scale are shared. The marks are cached 128×128 RGBA textures rasterized from the code's own polygon/circle geometry, not edited illustrations. They are bilinear, clamped, without mipmaps and released by `ItemIconArt.Reset()`.

Gold uses **one face-on outlined coin**, drawn from the same code-native circle geometry with a rim and embossed glint. It has no stack, crate, Tiny Battle pixel-art sprite or AI illustration. `DrawItem` remains shared by map pickups, shop and inventory. Gameplay item IDs, hitboxes, prices, durations, gold amounts and rewards are unchanged.

## Mapping

| Resource | Game meaning |
|---|---|
| medical | Multiplayer item 0: heal 30% of maximum HP |
| armor | Multiplayer item 1: armor damage reduction |
| he | Multiplayer item 2: explosive shells |
| purification | Multiplayer item 3: remove debuffs |
| invincibility | Multiplayer item 4: temporary invulnerability |
| mirror | Multiplayer item 5: three splitting shots |
| gold | One shared currency coin; campaign pickup kind 3 remains run gold |
