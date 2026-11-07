# Frozen pond material and oasis palm

These two standalone images were generated using the built-in `image_gen` tool on **2026-11-06** for Tank Battle. They are new AI-generated artwork, separate from externally sourced Kenney artwork and its CC0 licence. Existing reference files, their GUIDs and licences remain unchanged. Exact generation prompts and reference paths are recorded in [PROMPTS.md](PROMPTS.md).

| Resource | Original source | Dimensions / format | SHA-256 of original and repository copy |
| --- | --- | --- | --- |
| `ice.png` | `C:/Users/GIABAO PC/.codex/generated_images/01a1aa50-5a29-7431-91bb-b739c5aa9e23/exec-29163b40-2e23-40cd-9e44-48e878ec5d18.png` | 1254 × 1254, RGB8, opaque | `78437317b2ef8e99c6662402dbf08e23148814e596814d9ff00e54bf366e7efd` |
| `oasis_palm.png` | `C:/Users/GIABAO PC/.codex/generated_images/01a1aa50-5a29-7431-91bb-b739c5aa9e23/exec-be8920c3-4b62-43a2-b2b7-78617f5e7bca.png` | 1254 × 1254, RGBA8, transparent cutout | `e57eaaaf0fc860ce39cba028bcfe2a0ad550514b82055d32ab5f6723ad9177e3` |

Both PNGs were copied byte for byte. No raster cropping, recolouring, resampling or alpha editing was applied. Python/Pillow inspected dimensions, hashes and alpha coverage only. The ice used `ArenaEnvironment/materials.png` as its style reference; the palm used `CampaignCartoon/scenery.png`. The images use the existing overhead cartoon language and do not inherit the reference artwork's external licence.

The ice source is fully opaque. The palm has 33.70% coverage at alpha ≥128 and 66.11% clear pixels at alpha ≤8. Its visible bounds at alpha >8 are `(182, 162)` through `(1048, 1101)` exclusive, leaving visible padding on every side. The generated source also contains negligible haze with maximum alpha **1/255** at the outermost edge. The original bytes are preserved; validation permits outer-edge alpha at most 8/255 and rejects visible clipping. This haze is not an opaque background or an additional ground tile.

`catalog.json` records dimensions, source-byte hashes and the different alpha intents. Each texture is imported once as RGBA32, bilinear/clamp, no mipmaps, no compression, NPOT scale none and non-readable. Together they occupy **12,580,128 bytes** of pixel storage, below the shared **16 MiB** budget. `ArenaTerrainArt` caches full textures without creating cropped copies; rendering owns geometry and placement. The importer validates original source hashes, decoded dimensions, alpha, runtime import settings and this provenance before build. Builds include a copy in `ThirdParty/ArenaTerrain-PROVENANCE.md`.
