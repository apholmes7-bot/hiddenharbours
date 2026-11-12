# Proposal: texture memory and import-settings inventory

Status: **PROPOSAL, nobody has accepted this**. This is a read-only audit, not an
import-settings change or approval of a look. No texture, material, shader, scene,
palette, importer setting, atlas, rig or animation is changed by this document.

## Snapshot and coverage

Snapshot: 2026-11-10 09:24:37 UTC, base
`3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`, Unity 6000.5.0f1,
StandaloneWindows64, Direct3D11. The attached
[complete texture table](texture-memory-import-audit.csv) contains **3,258 texture
objects at 3,255 project asset paths**: 3,249 Texture2D objects and nine Texture2DArray
objects. Every row's asset path is tracked on the base. This is an editor-import
snapshot for that target, not a player-build or scene-residency measurement.

Discovery unions `AssetDatabase.FindAssets("t:Texture", new[] { "Assets" })` with
every TextureImporter path under Assets, then enumerates each path's texture
subassets. All **3247 tracked TextureImporter metadata paths**
were independently cross-checked against the output; none is missing. Native
texture assets and arrays are included. `(path, subasset)` keys are unique.
There were zero read errors. Package and built-in textures, runtime-created
textures, render targets and atlas pages created only during a player build are
outside this project-asset inventory. They need a separate runtime memory capture.

A temporary editor-only probe read importer properties and loaded textures, wrote
evidence, unloaded each path's textures, then exited. It called no save, reimport,
ApplyLockedSettings, bake or Play API. The probe and its new metadata were removed
after the run; neither is shipped in this PR. The editor's sole transient tracked
change, `runInBackground: 0` to `1`, was verified as the entire settings difference
and restored. No Assets or ProjectSettings changes remain. The lease was returned
with every launched process closed, and the slot keeper reported the shared save
unchanged. This proposal contains documentation and a CSV table only.

## What the memory columns mean

`payload_bytes` sums `GraphicsFormatUtility.ComputeMipmapSize` over every imported
mip level using the effective graphics format. Array slices and cubemap faces are
included; a 3D texture would reduce depth per mip. The observed catalog contains
only Texture2D and Texture2DArray. This estimates the unpadded format payload of
each texture object, excluding driver allocation alignment, other renderer buffers
and any CPU copy. It is not the compressed PNG size or a measured GPU allocation.

The catalog payload totals **3,645,772,940 bytes
(3.395 GiB)**. `profiler_bytes` records
`Profiler.GetRuntimeMemorySizeLong` for each loaded texture object, summing to
**7,316,118,823 bytes (6.814 GiB)**.
That editor object-memory measure can include CPU/native overhead and readable
copies; it is not an additional GPU bill, and it must not be added to payload.
The catalog is not all resident in one scene. This run does not measure peak scene
memory, build stripping, streaming, atlas duplication or savings from a proposed
change. In particular, disabling Read/Write without reviewing runtime consumers
is not authorized by this table.

`source_width/height` are the importer's source dimensions; native texture assets
without TextureImporter use zero to mean unavailable. `width/height`, mip count,
texture format and graphics format describe the imported object. Depth/slice
counts distinguish arrays. Importer-only columns are blank for native textures.
Standalone format is the configured override, which may say Automatic; the
effective format is the `graphics_format` column. Other platform overrides are
not measured; this is not a mobile-format audit.

| Effective graphics format | Texture objects | Payload MiB |
|---|---:|---:|
| `R16_UNorm` | 19 | 33.21 |
| `R32G32B32A32_SFloat` | 5 | 94.71 |
| `R8G8B8A8_SRGB` | 1965 | 2536.83 |
| `R8G8B8A8_UNorm` | 908 | 669.79 |
| `R8G8B8_SRGB` | 97 | 39.34 |
| `R8G8B8_UNorm` | 131 | 36.53 |
| `R8G8_UNorm` | 50 | 28.01 |
| `R8_UNorm` | 83 | 38.47 |

## Comparison with project import defaults

The authoritative default-setting implementation is
[`ArtImportPipeline`](../../Assets/_Project/Art/Editor/ArtImportPipeline.cs), with
the policy in [Art & Audio Bible section 9.2](../design/art-and-audio-bible.md).
The pipeline stamps the defaults **only on first import**, then respects intentional
manual overrides. Its scope is TextureImporter under `Assets/_Project/Art/`.
Other asset paths and native texture arrays receive inventory rows but are not
judged against sprite-import defaults. The audit never invokes the default-setting
method or treats every Default-type data map as a broken sprite.

**696 rows differ from one or more of those defaults.** The table's final column
lists each candidate's differences; counts below overlap, so do not sum them.
They are a review queue, not 696 proven defects or instructions to change art.

| Comparison with default | Rows |
|---|---:|
| type differs from Sprite default | 631 |
| PPU differs from 32 | 6 |
| filter differs from Point | 9 |
| default compression is not Uncompressed | 0 |
| mipmaps enabled | 69 |
| NPOT scale differs from None | 0 |
| data suffix imports as sRGB | 0 |
| non-data-suffix imports as linear | 335 |
| wrap differs from path default | 433 |
| imported dimensions smaller than source | 0 |
| Standalone override compression differs from Uncompressed | 0 |

The six PPU differences are tanker interior views at **16**, as required by
their per-hull scale contract. The nine filter differences below are displacement
profiles with a **Bilinear** kit contract. The
[owning-rule references and per-file exception list](texture-import-exceptions.md)
document both groups and distinguish geometry data from sprite defaults.
These remain raw deviations in this historical table; no setting or audit count
has been changed.

| Texture | Filter | Importer role |
|---|---|---|
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt_S62.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Basalt_S76.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone_S62.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Sandstone_S76.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Till.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Till_S62.png` | Bilinear | Default |
| `Assets/_Project/Art/Terrain/Cliffs/Profiles/Till_S76.png` | Bilinear | Default |

Data-role candidates and Repeat wraps can be intentional: the first-import naming
defaults do not encode every baker's purpose. Mipmaps may be deliberate for a
specific shader's sampling path. The bible explicitly permits exact narrower
**uncompressed** Standalone data formats: R16 painted height, RG16 plant light,
and R8 plant detail, subject to preserving the channels their shaders read.
The existing `PlantMapImportTests` guard the plant formats. These formats are reported
as measured, not relabelled compression violations. None of the observed effective
graphics formats is a BC/ETC/ASTC block format; default compression deviations and
source downscaling candidates both count zero in this snapshot.

This audit compares type, sprite PPU, filter, default compression, mip setting,
NPOT scaling, suffix-based sRGB default, path-based wrap, imported dimensions and
Standalone override compression. It does **not** certify pivots, per-sprite slicing,
all possible data-channel suffixes, pixel-perfect camera setup, atlas packing or
channel-by-channel preservation. No new rule is inferred for an intentional override.

## Largest individual imported payloads

| Texture | Imported dimensions | Payload MiB |
|---|---|---:|
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior0.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior1.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior2.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior3.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior4.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/Interiors/TankerIsoInterior5.png` | 3840 x 3200 | 46.88 |
| `Assets/_Project/Art/Boats/CapeIslanderIsoRock0.png` | 3648 x 3360 | 46.76 |
| `Assets/_Project/Art/Boats/CapeIslanderIsoRock1.png` | 3648 x 3360 | 46.76 |
| `Assets/_Project/Art/Boats/LobsterBoatIsoRock0.png` | 3648 x 3360 | 46.76 |
| `Assets/_Project/Art/Boats/LobsterBoatIsoRock1.png` | 3648 x 3360 | 46.76 |
| `Assets/_Project/Art/Sprites/Wharf/Iso/gangway.png` | 3256 x 3636 | 45.16 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/smallYard.png` | 3690 x 2859 | 40.24 |

These are prioritization measurements, not a request to compress, downscale or
re-bake them. Any memory optimization needs its own scoped proposal, consumer
audit and proof that its pixels remain unchanged. Art lanes or the owner decide
art-affecting questions. This lane changes no import setting and accepts no look.

## Reproducibility and review proof

The evidence probe was compiled offline against the editor's cached assembly
references, then executed once under an editor lease in this project. The CSV was
checked for complete tracked-importer coverage, unique keys, nonnegative byte
counts and reconciliation with the summary. The final document diff touches only
this Markdown file and its CSV; tracked runtime, editor code, Assets metadata and
ProjectSettings match the stated base. As a document-only change there is no
executable or imported asset change that could alter a visible pixel.

The underlying raw evidence, probe source, compiler output, launch/return record
and coverage check remain outside the published change. A later re-audit should
use the same explicit discovery scope and effective target, recompute sizes from
the loaded formats, record failures, unload between paths and preserve deliberate
overrides. Do not apply sprite defaults en masse to make the audit table green.
