# Hull mesh reflections — R3, default off

This serves P1, the sea has moods. A top-down hull picture flipped about one screen row
reflects both rig height and ground depth. It shows the deck and hangs cabin windows below
it. R3 instead draws the boat's existing posed geometry from its reflected physical view.
The owner authorized implementation on 2026-10-01; activation and appearance remain held
for matched GPU plates. `GameConfig.HullMeshReflections` defaults to false. No shipped
configuration enables it. The normal view and the legacy reflection remain the default.

## Geometry and visibility

The rigs are **Z-up**. For a posed point `q = Rz(heading) Rx(trim) Ry(heel) p`, reflect
`Jw(q) = (q.x, q.y, 2w - q.z)` before applying `IsoFacetMath.RigToWorld`'s projection:
`S q = (q.x, sin(E) q.y + cos(E) q.z, cos(E) q.y - sin(E) q.z)`.
The world transform is `T S Jw S T^-1`, where T includes the current mesh origin's heave
and calibrated depth offset. S is its own inverse. This fixes every point on the plane
and preserves left/right. It is not a reflection about a screen row or the unposed keel.

The calibrated local plane is
`w = sin(E) [L(cos(E)+sin(E)) - ZH sin(E) - H cos(E)]`.
H is the current screen heave in metres; ZH is the actual depth heave captured by
`IsoFacetHullRenderer.ApplyPose`, including its existing watertight clamp. L samples the
published wave field at the hull anchor with the sea's frequency, fetch, exaggeration and
shore fade. Without the calibrated displaced frame, w is zero. This remains a local flat
approximation, not a curved-wave mirror; local wake/contact residuals require the plates.
The supported pose envelope keeps heel below camera elevation and excludes capsize.

Hull/leaf geometry draws two-sided with nearest depth deciding visibility. Stored normals
are not reliable outward labels and UV0.w is water reachability, not reflection eligibility.
The reflected hull variant excludes only appended rooms (`UV1.y` under `HH_LEVEL_GATE`),
keeping the exterior shell even when the player view cuts it away. Rig 9 has a different
UV1 layout; its existing face-role/expression contract uses reflected view normals.
Lighting still uses the source normal and authored palette. Head snap uses the reflected
head and the source target's dimensions. The reflected depth span includes the entire
selected shell and all parts; it is independent of the normal camera's near/far range.

Below the calibrated local water plane, reflected mesh fragments remain **colourless
depth occluders**; a winning blocker yields no mesh reflection and cannot receive keyline
or figure ink. This prevents the water's warped lookup from fetching inside through the
clipped shell, at the stated cost of conservative suppression where submerged geometry
projects beyond the exact waterline section. Real geometry holes remain an Art issue and
are measured with shipped-warp contact controls.

The fragment interpolates the unreflected signed height. At height >= 0 it writes ordinary
facet/dark/key/depth. Below it writes zero colour/ownership/dark, actual reflected depth,
and key alpha 128/255. Untouched key alpha is 0 and ordinary solid alpha is 1. This overwrites
deeper colour regardless of draw order; depth-only writes with ColorMask 0 would not.
The reflection resolve checks this marker before invoking the ordinary resolve. A blocker
is neither an ink destination nor a solid neighbour. It blends zero mesh contribution over
any tree reflection; water/sky/foam remain visible instead of an artificial black patch.
Half-size scratch colour and marker use exactly the same nearest source coordinate.

The selection includes main hull meshes, the selected door leaf, enabled hull-owned
`IsoFacetPropRenderer` meshes (including fixed fittings), and hull-attached rig 9 figures.
It excludes ashore figures, lamp glow/cones and their lighting/shadow passes, sprite-only
fittings, and riders without mesh representations. Those omissions are accepted scope.
Trees keep their existing sprite mirror. Art owns missing bottoms/soffits and real holes;
R3 changes no rig source or baked mesh and generates no caps. The offline audit could not
certify a closed whole mesh for any of the 36 hulls: they contain overlapping panels and
open/detail geometry. In particular, dory thwarts have no underside, Cape covering-board
soffit/base continuity needs Art review, and Cape/base-lobster door leaves lack downfaces.
The pinned ray witnesses below are local evidence, not a contrary fleet-wide closure claim.

## Recording, lifetime and normal-path isolation

In mesh mode the hull overlay's `HHReflect` material pass is disabled. Its `Universal2D`
composition remains enabled. The gate uses the LightMode tag, per
[Unity's SetShaderPassEnabled contract](https://docs.unity3d.com/ScriptReference/Material.SetShaderPassEnabled.html). With mesh mode off that reflection pass is restored.

The existing `BeforeRenderingSprites` feature records:

1. Existing object reflections for non-hull reflectors, clearing the per-camera final target.
2. `HH Hull Mesh Reflections`: bounded explicit mesh packets into four scratch MRTs + D32.
3. `HH Hull Mesh Reflection Resolve`: current MRT reads, blending into that same final target
   and publishing `_HHReflectTex` with `SetGlobalTextureAfterPass`.
4. Existing foam, interior guard, displaced water, normal facets and normal keyline resolve.

With no trees, the final target clears on its first use by mesh resolve. Water declares
current reflection and foam reads, alongside its existing guard edge. There is no resolved
normal hull picture read and no water depth/colour split. The normal facet/guard entry points
call shared code with reflection=false; their original pass indices remain intact. The normal
resolve's `frag` body is unchanged. Reflection has a separate material and entry point,
preventing reflection uniforms from changing the normal path during deferred execution.

Packets carry copied MPBs, matrices, planes and material state. Mutable posed figure vertex
and normal buffers are copied into reusable camera-owned meshes; static hull/leaf/fitting
meshes are retained by reference. Camera leases cannot be overwritten before resolve executes.
There are at most two outstanding leases per camera; exhaustion skips mesh reflection rather
than borrowing mutable state. Storage grows on first use/topology/quality changes, then reuses
its arrays, lists, materials and figure meshes. Disposal releases owned caches and persistent
targets. GPU and allocation claims remain to be measured in C.

A resize retains an old final target if an outstanding mesh recording still references it.
Each camera can retire at most two such handles (the two-lease bound); they release after
those recordings finish, or at feature disposal. Normal steady rendering still owns one
final target. During deferred resize the additional peak is up to two old 8 B/pixel targets
at their old sizes (31.64 MiB if both were 1920×1080). The resize test checks that the old
handle stays alive and that the new descriptor and packet/material state are independent.
An abandoned graph never makes its lease available by a frame-number guess: after two
abandoned recordings this camera safely suppresses mesh work until feature disposal.

Selection does not use the camera's original `cullResults`. It tests complete mirrored bounds,
including below-plane blockers, against the target, with a source-texel ink margin. The water
bounds-checks its lookup **after** warp, so the complete target bounds every fetch regardless
of warp amplitude: a wholly off-target texel cannot contribute. This avoids inventing a warp
constant or relying on an unpublished material value. Camera layer masks and enabled renderers
still apply. Orthographic scene/inset cameras have separate state; perspective views skip this
2D path because the existing water lookup itself assumes an unrotated orthographic projection.

Ceilings live in GameConfig: 64 hull groups, 1,024 packets, 1,000,000 submitted triangles.
Overflow suppresses the entire camera's mesh contribution with a diagnostic; it never chooses
the first registered N. Packet order uses cached authored hierarchy keys, transforms and plane
state, not registration or the normal view's allocated HullId. Reflection ownership encodes
only occupied/unlit or occupied/lit; it does not consume the normal view's fore-band ID pool.
Trees retain their previous warning-only bound. Idle frames allocate no scratch targets and
record no mesh draw/resolve; the clear fallback replaces retained history. When existing water
work records, it declares and binds the constant fallback before drawing; existing foam/normal
resolve can also publish it. This preserves the per-camera binding even with deferred graphs,
without adding an idle raster pass or a render target.

## Cost per camera

Three `R8G8B8A8_SRGB` scratch targets (facet, dark, key), one `R32_SFloat` true-depth target,
and D32 raster depth total **20 bytes per source pixel**. All five are transient, single-sample
and point filtered where sampled. The existing persistent full-size ARGBHalf final reflection
target costs 8 bytes/output pixel. It is reused, not duplicated. All targets are per camera.

| Output size | Added full-source scratch | Added half-width/height scratch | Existing final target |
|---|---:|---:|---:|
| 1920x1080 | 39.55 MiB | 9.89 MiB | 15.82 MiB |
| 1280x720 | 17.58 MiB | 4.39 MiB | 7.03 MiB |
| 2400x1080 phone landscape | 49.44 MiB | 12.36 MiB | 19.78 MiB |

Full source is the default; divisor 2 is an explicit data option for C/mobile. The final output
stays full resolution, preserving the water's existing PPU-32 world-grid snap and point lookup.
Added draws are one per selected submesh packet plus one fullscreen resolve; the ceilings give
1,025 draws and 1,000,001 triangles including the fullscreen triangle. A dory alone submits
942 source triangles; a Cape Islander hull and its leaf submit 2,106, before occupants/fittings.
Below-plane geometry adds fragment/depth work within those same submissions.

The table excludes CPU packet/material caches and bounded mutable figure copies. Each figure
copy retains its mesh streams and uses reusable position/normal lists (24 bytes/vertex CPU
payload, plus list capacity and mesh/native storage). Palette textures are shared, not copied.
C records these residency costs as well as RT allocation, warm-frame GC, draw counts and GPU
time, for full/half, single/two cameras and resize. Use the granted synced camera.Render plus
1x1 ReadPixels timing control; batchmode FrameTimingManager is not a GPU timer here.

## Verification and remaining GPU gate

The new `HullMeshReflectionTests` contains 15 methods / 67 cases. Production geometry selection,
matrices, descriptors and `RecordRenderGraph` are invoked directly. The graph harness absorbed
from held #904 uses Core's real managed builders without constructing/compiling/executing a GPU
graph; its private API adapter is intentionally pinned to this project's package version.

| Method | Cases |
|---|---:|
| HullMeshMirror_ComposesWithRigToWorld | 12 |
| HullMeshMirror_FixesEachContactAndPreservesX | 6 |
| HullMeshMirror_UsesProductionSettleAndClamp | 4 |
| HullMeshReflection_UsesCurrentDoorAndFigurePose | 4 |
| HullMeshReflection_RealHullFirstHitsHideDeckAndRooms | 3 |
| HullMeshReflection_BoundsIncludeOffscreenSource | 4 |
| HullMeshReflection_SelectionIsOrderIndependent | 3 |
| HullMeshReflectionGraph_DeclaresCurrentSourceBeforeConsumption | 9 |
| HullMeshReflectionSource_CamerasAndResizeAreIndependent | 3 |
| HullMeshReflection_IdleAndDisableBindClear | 5 |
| HullMeshReflection_TargetBudgetMatchesQuality | 2 |
| HullMeshReflection_PreservesStyleAndLitMetadata | 3 |
| HullMeshReflection_ShaderCompiles | 1 |
| HullMeshReflection_PacketsRetainCutOccluders | 4 |
| HullMeshReflection_CutOutputAndResolveAreWired | 4 |

Real-asset tests pin source-identified dory thwart/Cape cockpit/lobster cockpit triangles and
trace independent two-sided rays through actual production packets. Dropping below-plane hits
must expose the pinned forbidden surface; full geometry must intercept it with a blocker, while
a separate exterior witness must remain visible. Both door states participate. These witnesses
are not a fleet-wide proof and do not execute HLSL. Shader wiring/compiler and graph checks
cover other boundaries; final pixel correctness remains a GPU control.

Against e8de9cd0 the forecast is EditMode **13,620 / 13,367 / 253 / 0** and PlayMode unchanged
**1,032 / 936 / 96 / 0** (total/passed/skipped/failed). If main moves, compare names against that
main, retaining the same skip names. The owner's subsequent 3ffd5712 forecast is EditMode
**13,727 / 13,474 / 253 / 0** and PlayMode **1,036 / 939 / 97 / 0**; #910 adds the GPU wharf plate skip. Main's protected tests are unchanged. #904 is absorbed only
for its Null-safe harness, idle fallback and explicit edges; its picture read and water split
are retired. Its held branch is untouched; the owner closes it when the R3 draft opens.

C requires a separate named editor-slot grant, launch/save checks and main-matched frames:
still side/end-on, heel, fast turn, pan, Cape night, two figures, rowing dory, two cameras and
resize, plus R1's pivot controls and the owner's four shots. Count forbidden returned colours
at visible **water receiver pixels after the shipped warp**, including contact and half-source
mapping. Require positive risky-fetch and exterior counts, zero forbidden counts on closed-shell
witnesses, and a positive leak when the blocker is diagnostically removed. Record actual Art
holes separately with triangle/pose/sample coordinates. Measure conservative clear fringes and
lost legitimate reflections, especially overlapping boats. Flat/warp-off contact tolerance is
one full-resolution pixel; shipped-warp contact uses matched expected displacement and owner
judgment. Never tune Water.mat/presets to conceal a mismatch. No appearance acceptance is
claimed by Phase B or by Null-device CI.
