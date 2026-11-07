# Item art provenance

## Active artwork — Kenney compositions and one coin

The active `catalog.json` uses `kenney-flat-v1`. All six item types share the project's existing, untouched `ArenaArt/crateMetal.png` (28×28, GUID `b1addcef057347640af4534e5e5b4c87`). Explosive ammunition and splitting ammunition additionally draw the existing `ArenaArt/bulletSand1_outline.png` (8×14, GUID `9e3c0d85247c9fc42bb525fdff21aedb`). These sources belong to **Kenney — Top-down Tanks Remastered**, under CC0; the original `ArenaArt/Kenney-LICENSE.txt` is retained. No external asset pack, new dependency, image generation, source recolouring, source raster editing or source importer change was used for this revision.

`ItemIconArt.cs` draws each original crate and overlays code-native flat functional marks: a medical cross, riveted steel plate, explosive burst and original shell, cleansing canister and drop, gold protection shield, or three original shells with branching paths. Outlines, upright orientation, restricted materials and scale are shared. The marks are cached 128×128 RGBA textures rasterized from the code's own polygon/circle geometry, not edited illustrations. They are bilinear, clamped, without mipmaps and released by `ItemIconArt.Reset()`.

Gold uses **one face-on outlined coin**, drawn from the same code-native circle geometry with a rim and embossed glint. It has no stack, crate, Tiny Battle pixel-art sprite or AI illustration. `DrawGold` is the canonical currency presentation; map pickups, account wallet, prices, campaign progress and results use it. `DrawItem` remains shared by map pickups, shop and inventory. Gameplay item IDs, hitboxes, prices, durations, gold amounts and rewards are unchanged.

## Retained first iteration — inactive AI PNGs

The seven PNGs still present in this folder are retained with their original GUIDs and exact bytes as provenance/history. **They are not loaded or drawn by the active item/currency renderer or required by its build validator.** They were created with the built-in `image_gen` tool on 2026-11-05; `CampaignCartoon/scenery.png` was the style reference. Each selected PNG was copied byte-for-byte; no recoloring, cropping, background removal, atlas packing or raster editing was applied. Original generated files remain in the tool-managed `generated_images` directory. Their source canvases are RGBA 1254×1254, imported at 512×512, uncompressed RGBA32, bilinear, clamp, no mipmaps and not readable. Their historical generation prompts are preserved below.

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

The table records semantic keys for both iterations. PNG filenames are historical only; active sources are the Kenney compositions and code-native coin described above. Original ArenaArt and CampaignArt assets, importers, GUIDs and licences remain intact; atlas references retain their own licence records.

## Exact generation prompts

### medical.png

Tool output filename: `exec-6f56b377-15c3-40df-80b0-69e4463def49.png`.

```text
Use case: stylized-concept. Generate ONE production game pickup icon for Tank Battle: an olive military medical kit, chunky rectangular field case with carry handle, cream front badge bearing a bold red medical cross, latch and bevel details. Existing attached atlas is STYLE REFERENCE ONLY: match its thick dark charcoal outline, flat cartoon planes, muted military palette, cream highlights, top-left light and tiny lower-right shadow. Icon camera: three-quarter near-front/top view appropriate as a UI pickup illustration, not a top-down environment tile. Large simple silhouette clearly readable at 24–48px. Single centered object occupying about 78% of square canvas; minimum 8% fully transparent padding on every side. True transparent background, no text, no numbers, no letters, no framing tile, no border, no extra objects, no floor, no glow, no watermark. Deliver medical kit only.
```

### armor.png

Tool output filename: `exec-6e66c062-5488-4e9a-994b-3c99e81087e7.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: a reinforced blue-grey steel tank armor shield plate, broad beveled shield silhouette with dark steel rim, two chunky bolts, three layered metal plates and cream glint; no cross, no symbols, no letters. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```

### he.png

Tool output filename: `exec-2e057ffa-113b-46e5-bb54-aab9413659f0.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: a chunky brass artillery shell with red tip, in front of a compact orange explosion burst, visible brass casing and shell seam; a single cohesive explosive ammunition icon, no letters, no text. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```

### purification.png

Tool output filename: `exec-14805915-37c3-4ac9-b916-64d3658603f7.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: a pale blue military antidote canister with silver cap, central cyan water-drop emblem and two large clean white sparkle shapes, clean restorative feeling; one main canister, no medical cross. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```

### invincibility.png

Tool output filename: `exec-6f32004f-f975-4944-ae2c-46952ef8b3b9.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: a radiant golden heavy protection shield with thick gold rim, dark ochre inset, one raised silver star in the center and two cream shine sparks; an invulnerability relic with broad unmistakable gold shield silhouette, no text, no letters. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors with readable item accent, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```

### mirror.png

Tool output filename: `exec-edf7b588-f20b-4bb0-8344-78f2c4ee128a.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: three chunky brass bullets with purple tips fanning out from one purple armored ammunition casing, clear one-to-three splitting ammunition silhouette, two short violet branching trails connecting casing to bullets; no mirror glass, no letters. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors with readable item accent, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```

### gold.png

Tool output filename: `exec-912365c5-3296-49d5-88b5-8a97b6b1247e.png`.

```text
Use case: stylized-concept. Asset type: production Tank Battle game pickup icon. Primary request: a small rich pile of bright gold coins, three chunky stacked round coins and one large upright coin in front, each coin with broad raised circular rim and a small embossed four-point glint, warm ochre shadows and cream highlights; unmistakable currency treasure pickup; no lettering or numerals, no bars, no chest. Attached scenery atlas is STYLE REFERENCE ONLY. Match thick charcoal outlines, chunky flat cartoon planes, muted military colors with readable item accent, cream highlights, top-left lighting, tiny lower-right shadow. Three-quarter near-front/top icon view. Large clear silhouette recognizable at 24–48 pixels. Single centered icon occupying 78% of square canvas, minimum 8% fully transparent padding on all sides. True transparent background. No framing tile, no border, no extra unrelated objects, no floor, no glow, no watermark, no text, no numbers, no letters. Strong difference from the other item types.
```
