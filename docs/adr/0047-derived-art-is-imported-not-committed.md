# ADR 0047 — Derived art is imported, not committed

- **Status:** Accepted by the owner, 2026-09-27 ("I approve the array plan"); implementation awaiting editor and CI validation.
- **Decision owner:** owner; tools-editor with art-pipeline and qa-test.
- **Related:** ADR 0003 (content is data), 0014 (painted seabed), 0028 (terrain ground), 0046 (still water).
- **Scope:** the ground kit's five packed textures. No bake-rig, shader, seabed or cliff migration.

## Context

The terrain detail array, three relight arrays and palette ramp occupy 143,298,243 bytes of
LFS payload at the migration base. They contain 71,646,876 bytes of pixels serialized as hex.
The source PNGs and relight manifest are already committed. A new version of every output
costs another 143.30 MB of LFS storage and uncached downloads; byte-identical outputs reuse
their content-addressed objects. History remains unchanged.

The arrays have stable scene references, a shader-defined slice order and independent
bake-byte tests. Removing committed output must preserve all three.

## Decision

When an art binary is a deterministic transform of committed inputs and can be built during
headless import, commit the source, a small recipe, importer and stable metadata. Store its
generated objects as Unity Library import artifacts, not a second pixel copy under Assets.

Declare all input dependencies, version packing semantics, preserve object identifiers and
report source-specific import errors. CI must prove bytes and references against independent
expectations. A failed import must fail its consumers even if Unity retains a prior artifact.
An importer must not modify another asset's import settings.

Future exceptions for external/licensed tools, unavailable inputs, GPU-only generation or
non-reproducible output need a named decision and validation plan. Merely ignoring an output
without supplying a clean-machine import path does not meet this rule.

## Ground kit

Two reviewable JSON recipes live in Assets/_Project/Art/Terrain:

- TerrainDetail256.hhterrain: the sRGB RGBA32 detail array, nine mip levels, 63 slices.
- TerrainRelight.hhterrain: three linear RGBA32 arrays without mips and an 81 x 63 RGBAFloat ramp.

TerrainArrayImporter uses TerrainTexArrayBuilder as its CPU packer. It reads PNG bytes directly
and preserves bottom-up Unity pixel order. Inputs are 256-square non-interlaced 8-bit RGB or
RGBA PNGs without colour-management/transparency chunks. Unsupported input is refused by
path. This includes all current sources, including the three RGB Lawn tiles. Order256,
LadderSteps and map suffixes remain the shader's append-only layout contract.

The detail recipe preserves GUID 9eb63aa02acc68540b3b7a835b4d8d47. The relight recipe has a new
GUID with four stable named subobjects. Obtain numeric local IDs from the editor, never by
guessing. Migrate only the two named scene detail fields; relight scene wiring belongs to
terrain pass 9 PR 5. TerrainArrayAssets is the shared loader and supplies explicit reimport
menus. Ordinary loads never rebuild as a side effect.

DependsOnSourceAsset tracks PNGs, the manifest and packing-code sources. Increment the importer
version when decode/packing/layout semantics change. Source TextureImporter settings and import
order do not define the output. Refusals publish no partial texture set, and the loader examines
the import log before returning objects that might survive from an earlier successful import.

## Source authority and existing exceptions

**Ground:** PNGs and TerrainRelight.json remain the committed source boundary. Moving the
JavaScript bake itself into Unity is a separate decision.

**Seabed:** painted height textures and assets remain authoritative authored data for both
rendering and simulation (ADRs 0014 and 0046). A first version originating in an analytic bake
does not make subsequent authored data disposable. This migration changes none of them.

**Cliffs:** the ignored local bake is an existing exception recorded in .gitignore: a fresh
clone can be untextured until it is baked. This ADR neither retrofits cliffs nor permits the
ground's byte tests to pass without generated textures.

Keep the old Derived/*.asset LFS rule to protect forced accidental adds. Ignore that retired
folder, and test that neither the folder nor its old writer returns.

## Validation and costs

Preserve independent bake/source/slice/ramp checks. Add import-twice byte/identity, dependency
change, failed-import recovery and scene-reference guards. Preserve JsonUtility's float parser:
alternative parsing paths can round ramp parameters differently. A small baseline captured
from the old native assets checks every detail mip, relight map and exact ramp float bit on
Windows and Linux CI. Rebaseline only for an intentional reviewed art change, never by asking
the importer under test to generate its own expectations.

Exactly two old test names retire: TerrainTexArrayBuilderTests.Build_Twice_TheInPlaceRebuildSurvives_AndTheGuidHolds
(native SaveInPlace), and TerrainSplatBandPinTests.KitTextures_CarryTheLoadBearingImportSettings
(the source-readability packing premise). Replacement guards cover imported identity/output
settings and source-import independence; standalone source settings retain their other checks.
No import or byte guard gains an Ignore/Inconclusive path.

First import decodes 243 PNGs. Unchanged later artifacts can use Library cache. All output
textures remain readable: including all five in a player retains about 68.33 MiB of CPU pixels
in addition to graphics storage. Readability/stripping is a separate follow-up; this migration
saves repository traffic, not renderer memory.
