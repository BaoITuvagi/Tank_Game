# Winter outpost igloo

This standalone sprite was generated using the built-in `image_gen` tool on **2026-11-07** for Tank Battle. It is new AI-generated artwork, separate from the externally sourced Kenney artwork and its CC0 licence. Reference files, their GUIDs and notices remain unchanged. The exact prompt and reference roles are recorded in [PROMPTS.md](PROMPTS.md).

| Resource | Original source | Dimensions / format | SHA-256 of original and repository copy |
| --- | --- | --- | --- |
| `igloo.png` | `C:/Users/GIABAO PC/.codex/generated_images/01a1aa50-5a29-7431-91bb-b739c5aa9e23/exec-79c119da-2a5e-4fc4-8d16-ef4274f7b984.png` | 1254 × 1254, RGBA8, transparent cutout | `baa56efa921ac0c0d5108330f7ee8f39aa9bf056a199f40c1c8f3a418785cc16` |

The PNG is a byte-for-byte copy. No raster cropping, recolouring, resampling or alpha editing was applied. The images in `ArenaBiomes/props.png` and `CampaignCartoon/scenery.png` served only as style references for outlines, snow colours and the overhead camera. Python/Pillow inspected dimensions, hashes and alpha only.

The sprite has 42.21% coverage at alpha ≥128 and 57.63% clear pixels at alpha ≤8. Its visible bounds at alpha >8 are `(172,142)` through `(1056,1104)` exclusive; the outermost edge has at most 1/255 alpha haze, below the shared cutout validator's 8/255 edge limit. There is no visible background tile.

`catalog.json` records the source-byte hash, dimensions and cutout intent. `ArenaWinterArt` caches the full source texture without creating cropped copies. The texture imports as RGBA32, bilinear/clamp, non-readable, no mipmaps, no compression and NPOT scale none, for **6,290,064 bytes** of pixel storage under a separate **8 MiB** budget. `ArenaWinterImporter` reuses the terrain source/alpha validator and checks source bytes, runtime settings, prompt and provenance before build. Builds include `ThirdParty/ArenaWinter-PROVENANCE.md` and the linked `ArenaWinter-PROMPTS.md`.
