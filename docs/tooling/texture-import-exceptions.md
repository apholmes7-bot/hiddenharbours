# Texture importer exceptions with owning contracts

This records existing, contract-backed settings for the six PPU differences and
nine filter differences in the [texture inventory](texture-memory-import-audit.md).
It changes no importer setting or art policy. The inventory is a historical editor
snapshot; the metadata and source references below were checked at
`12bc04b19a2efb33e352b4eea58104f7a5599be1` (refresh, 2026-11-12).

The refreshed main retains all fifteen listed metadata settings and the cited
producer/consumer contracts. The texture inventory and CSV remain the original
editor snapshot; their counts and memory totals are historical measurements,
not a current-main remeasurement. No editor was launched for this refresh.

## How the default and a kit contract fit together

[Art & Audio Bible section 9.2](../design/art-and-audio-bible.md)
states the sprite defaults: 32 PPU, Point, uncompressed, no mipmaps.
[`ArtImportPipeline.OnPreprocessTexture`](../../Assets/_Project/Art/Editor/ArtImportPipeline.cs)
applies those defaults when `importSettingsMissing` is true. Its documented
first-import scope preserves later deliberate overrides. The kit's importer and
its consumers explain the narrower contracts below. This is a reference list for
reviewing audit candidates, not an instruction to run Apply Import Lock on them.

A Default-type texture used as geometry or packed numeric data has a different
role from a displayed sprite. Its stored sprite PPU is inactive. Linear sampling,
Repeat wrap, CPU readability and an uncompressed channel format must be assessed
against the producer and consumer of that data.

## Tanker interior sheets: 16 PPU

All six files are Sprite/Multiple colour sheets. Their metadata has **16 PPU,
Point filtering, sRGB on and mipmaps off**. Their shared directory is
`Assets/_Project/Art/Boats/Interiors/`.

| Sheet metadata | PPU |
|---|---:|
| [TankerIsoInterior0.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior0.png.meta) | 16 |
| [TankerIsoInterior1.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior1.png.meta) | 16 |
| [TankerIsoInterior2.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior2.png.meta) | 16 |
| [TankerIsoInterior3.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior3.png.meta) | 16 |
| [TankerIsoInterior4.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior4.png.meta) | 16 |
| [TankerIsoInterior5.png.meta](../../Assets/_Project/Art/Boats/Interiors/TankerIsoInterior5.png.meta) | 16 |

The owning rule is [ADR 0038, Proposal 2](../adr/0038-boat-interiors.md): carry
pixels per metre per hull and refuse an omitted scale. The
[tanker interior sidecar](../art/rigs/boat-interiors-kit/tankerIsoRig.interior.json)
declares `frame.scale_px_per_m: 16`.
[`BoatInteriorSidecarReader`](../../Assets/_Project/Code/Tools/Editor/RigBaking/BoatInteriorSidecarReader.cs)
requires that field. The sheet baker copies it to `SheetEntry.pixelsPerMetre`, and
[`BoatInteriorSheetSlicer.LockImportSettings`](../../Assets/_Project/Art/Editor/BoatInteriorSheetSlicer.cs)
assigns it to `spritePixelsPerUnit` while retaining Point, uncompressed, no mips
and Clamp. Its audit checks that the imported PPU matches the sheet contract.

Changing these sheets to 32 PPU alone would halve their sprite dimensions in world
units and break registration with the exterior. The two pixel grids remain an
explicit legacy constraint in ADR 0038; this entry grants no general exception to
the game's scale standard. It also makes no claim that every sheet is resident
in a particular scene or that a mesh conversion can be done by changing PPU.

## Cliff profiles: Bilinear numeric geometry

These nine files are **Default-type displacement maps**, with Bilinear filtering,
**sRGB off, CPU readability on, mipmaps off, Repeat U and Clamp V**. Their directory
is `Assets/_Project/Art/Terrain/Cliffs/Profiles/`. The metadata's stored sprite PPU
of 32 is inactive for their Default texture type.

| Profile metadata | Filter |
|---|---|
| [Basalt.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt.png.meta) | Bilinear |
| [Basalt_S62.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt_S62.png.meta) | Bilinear |
| [Basalt_S76.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt_S76.png.meta) | Bilinear |
| [Sandstone.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone.png.meta) | Bilinear |
| [Sandstone_S62.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone_S62.png.meta) | Bilinear |
| [Sandstone_S76.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone_S76.png.meta) | Bilinear |
| [Till.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Till.png.meta) | Bilinear |
| [Till_S62.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Till_S62.png.meta) | Bilinear |
| [Till_S76.png.meta](../../Assets/_Project/Art/Terrain/Cliffs/Profiles/Till_S76.png.meta) | Bilinear |

The owning rule is the
[cliff-face kit, sections 5 and 6](../art/rigs/cliff-face-kit/README.md#6-import-settings):
the profile stores plan displacement and uses Bilinear because it drives geometry.
[`CliffCatalog.Filter`, `IsCpuReadable`, `IsSpriteAsset`, `WrapU` and `WrapV`](../../Assets/_Project/Art/Editor/CliffCatalog.cs)
encode that distinction; only strips and ledges are sprites.
[`CliffBaker.ApplyImportSettings`](../../Assets/_Project/Code/Tools/Editor/RigBaking/CliffBaker.cs)
applies the kind-specific settings, including uncompressed data and linear profile
values. `CliffBakeMenu` invokes this pass after import.

The CPU consumer is
[`CliffWallSurface.DisplacementAt`](../../Assets/_Project/Code/Art/CliffWallSurface.cs):
it samples the profile with `GetPixelBilinear`, wraps U and converts grey to metres.
The baker documents 0.25 m vertex spacing. The CPU path explicitly requests
bilinear interpolation; its interpolation is not selected by `filterMode`.
Bilinear remains the kit's declared import contract, while linear values and CPU
readability preserve the displacement data and its CPU access. The inventory's
filter difference alone does not establish a change to CPU geometry. A profile
is numeric input to that geometry, so sprite edge rules do not classify it.

## Verification and limits

Read-only checks reconciled all **15 metadata paths** with the inventory CSV's
exact six PPU and nine filter candidate sets, checked the values stated above and
the sidecar's scale, and recorded metadata hashes. No importer method, bake, asset
save or Play API was invoked for this document. The PR changes only its two
documents relative to the refreshed main; all Assets and ProjectSettings match
that main. The fifteen metadata paths and cited source contracts were also
checked for changes since the original exception audit; none changed.

Existing guards include `BoatInteriorPlacementTests.ThePixelGridIsStatedPerHull_AndTheTankerIsNotTheFleet`
and the sheet slicer's imported-PPU audit; these are references, not newly executed
test claims. Unity was not needed or run for this documentation check.
The other inventory candidates retain their original measured classifications.
This list approves no new settings and does not certify other platform overrides,
peak scene memory or the visual correctness of any asset.
