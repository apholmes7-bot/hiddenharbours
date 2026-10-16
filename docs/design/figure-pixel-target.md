# Mesh figures on the art-pixel grid — Phase A

Status: design and headless evidence only, 2026-10-11. Base
`640e3cc7234460f49aee79defb447c5aa32485c1`. No runtime change is made here.
P3: people read as the coast's pixel art. P5: long sessions remain visually coherent.
ADR 0044's smoothly rotating mesh, discrete clips, and the hulls' existing look remain constraints.

**Recommendation for D1: figures first, route 1.** This costs more plumbing than resizing
the shared facet block. Route 2 is a useful disposable probe, but changes every boat.
The owner rules D1 before the next phase; this note does not authorize implementation.

## 1. Evidence at this base

All paths below are repository-relative. Code under `Assets/_Project/Code/` is abbreviated
as `Code/`; shaders are under `Assets/_Project/Art/Shaders/`.

| Read at this base | Finding and consequence |
|---|---|
| `Code/Art/IsoCharacterFigureRenderer.cs:10–46,779–813,984–1003` | CPU-skinned MeshRenderer uses `HHHullFacet`. Aboard it inherits the posed hull frame, writes the hull's id, takes no id, and shares private depth. Ashore it takes one single, creates an iso frame and cell overlay, and copies the source sprite's sort. The header's claim that shipped scenes do not use ashore is stale; the callers below do. |
| `Code/Art/IsoFacetHullFeature.cs:162–176,223–233,640–667` | Gate is hull count OR ashore figure count. Null device records nothing. Targets derive from the camera descriptor. Reflection divisor is a precedent, not permission to resize water or reflections. |
| Same file, `946–959,1098–1188,1264–1286` | Three RGBA8 sRGB MRTs plus R32 true depth, a separate Depth32 hardware buffer, then ARGB32 resolve; Point sampling, no MSAA. Facets and deck list share depth with displaced water. Resolve is retained per camera EntityId. |
| `HiddenHarboursIsoFacet.shader:15–24,110–122,224–253,438–451,575–595` | `HH_FIGURE` has separate ramp windows, gain/bias, face, head snap and ink. Head snap reads `_ScreenParams`, so a small attachment alone is insufficient: the pass must bind its own projection and dimensions and restore them. Hull dither uses the hull-cell world frame at `_PixelsPerMetre=32`. Figure tones bypass Bayer entirely. |
| `HiddenHarboursIsoFacetResolve.shader:106–133` and figure-ring rules | Inner contour reads four neighbours and true depth. Figure flags reside in dark alpha. Hull/figure id equality alone does not identify figure coverage. Rig 10 has inner contour but no default outer keyline ring. |
| `HiddenHarboursIsoFacetOverlay.shader:44–95` | Reads the resolved screen image at screen coordinates; accepts its hull single or fore-id range. Resizing the target requires an explicit coordinate mapping; Point filtering alone cannot fix integer `Load` addresses. |
| `Code/Core/Iso/CharacterSkinPose.cs:69–88` | `floor(elapsed*fps)`, hold/clamp or wrap; no interpolation. Keep untouched. |
| `Code/Core/Art/PixelGrid.cs:21–36,51–86` | Existing camera/vehicle rule uses rendered-pixel spacing, not the asset grid. Snap is presentation only, inert at nonpositive spacing, and never rounds depth. A low-resolution figure is the other case: its samples use the sprite asset lattice. Do not replace the vehicle rule. |
| `Code/App/CameraZoomPolicy.cs:125–139`, `CameraFollow.cs:64–75,548–582,640–668,783–839` | PPU 32; steps -8…-2,1…8. At 1080p, foot request 9 m gives ×4 (8.4375 m), deck ×5, haul ×6, dory request 14 m gives ×3. Feel temporarily disables Pixel Perfect Camera. The published mesh-motion grid is derived from the design ladder, not arbitrary live viewport dimensions. |
| `StPeters.unity:61081–61089`, `NineMileCreek.unity:31260–31268,81637–81645`, `Greywick.unity:1228–1236,3213–3221` | PPU 32, 480×270 reference, sprite snapping 1, UpscaleRT 0, crop 0. CameraFollow rewrites reference dimensions. Duplicate camera components must not be treated as one global target. |
| `Code/Core/Config/GameConfig.cs:609–635` | PixelGridSnap has an explicit true constant and flat initialized field. Comments explain why a missing serialized nested settings value is unsafe. New fields must use the flat pattern. |
| `docs/art/rigs/character/rig10/tools/kit.js` and `tools/check.cjs` | Thirty builds; four kinds, each at 1× and nearest-enlarged 4×, plus cast sheet. `idle8` means idle frame 0 in all eight directions; `walkS` means frames 0…7 in south only. |
| Rig `Art/characterIsoRig10.js:112–115,1270–1272,1288–1348` | Revision 10.3, default 32 px/m; `px` scales cell and pivot, head snap, one-pixel marks and contour. Contrary to the initial hypothesis, **this rig does not dither**. Its exported Bayer table is descriptive compatibility data. |

At 4K, foot ×8 still gives 480×270 nominal figure texels. At the ladder maximum,
other framings need not have that same target size. Recompute per actual camera.
ADR 0044's old 43–57% result was rig 6 history, not a present measurement or acceptance gate.

### All mesh figure paths

| Subject | Creation and presentation path | Pass / id |
|---|---|---|
| Player aboard, including carried arrival passenger and supported mounted states | `Player/DeckRiderMeshPresenter.cs:327,661–668`; arrival uses Core `ICarriedFigure`, not another mesh drawer | `IsoCharacterFigureRenderer` → HHHullFacet; parent hull id |
| Player ashore | Same presenter `395–464,673–685` → EnterAshore | HHHullFacet; one single; source-body sort |
| Cast/skippers aboard | `Boats/MooredBoat.cs:336,403–429` → Core `CharacterFigurePresentation` → `Art/CharacterFigurePresentationService.cs:29–46` → `CharacterFigurePresenter.cs:410,678` | Same renderer; parent hull id; restaging/suspension can hand back to sprites |
| Villagers ashore | `World/Interactable.cs` → `World/NpcFigureStand.cs:56` → same service → `CharacterFigurePresenter.cs:480–552,756` | Same renderer; one single; stable identity phases clips; copies sort after YSortSprite |

Search of all shader LightMode declarations found **no production HHHullDeck figure shader**.
Only `Assets/Tests/EditMode/RigBaking/Shaders/HullDeckProbe.shader` declares that tag.
The production mesh figures all use HHHullFacet; the second list is still part of the
occlusion contract and its probe must continue working. Hulls, vehicles, props and door
geometry are not figures and must remain on the existing path. Reflection registration
in `Art/HullMeshReflection.cs` already includes figures; its separate path stays unchanged.

ADR 0044 §8 is live: villagers now use ashore meshes. The older #877 design note at
`8d466ec9` correctly describes the pool but predates those callers. At this base singles
and twelve-id blocks still have separate free stacks; figures refuse 255 and exhaustion.
Route 1 spends **no additional ids** and is not a workaround for exhaustion. The #877
lazy-block proposal remains separate work, not an assumed merged fix.

## 2. Real Node control and mechanism

Node v24.19.0, revision 10.3. Rig SHA-256:
`ad563075475740cea1258de68d05c184f2fc136491357bc22988a10d810413f3`.

The original directory's `tools/check.cjs --quick` verified 63/63 data files,
241/241 pixel-identical renders and 241/241 manifest RGBA hashes. Its **overall result
was FAIL**, solely because four repository intake additions are not in the kit's own
`SHA256SUMS.txt`: `INTAKE.md`, `INTAKE.SHA256SUMS.txt`, `node_check_char10_3.cjs`,
`node_probe_char10_3.cjs`. No source or checksum was changed to hide that finding.

For the complete kit control, exactly the 319 checksum-listed files plus SHA256SUMS.txt
were copied, byte for byte, into local ignored `artifacts/pixel-look-a/checked-kit/`.
The unmodified checker there, with no flags, **passed**: 570/570 golden gates on thirty
builds, 63/63 data, 241/241 renders and 319/319 sums (73 seconds). This is a bounded kit
check, not a claim that the original directory's packaging check passed.

Then `node tools/measure-figure-pixel-target.cjs` independently used `renderPlan()` with
an explicit `px:32` render adapter. **120/120 1× images** matched their manifest
`rgbaSha256`, and their shipped PNG pixels matched too, before any measurements ran.
No generated kit file, skin def or preset was rebaked.

The measurement uses all thirty CAST builds, idle frame 0 at directions 0…7 and walk
frames 0…7 at direction 4, exactly as `cellsOf()` specifies: 480 matched poses × four
resolutions = **1,920 comparisons**. Frame, pose, direction, camera, contour and head-snap
defaults are held; only `px` changes. Every high-resolution image is compared at the
same scaled pivot to the corresponding 32 px/m image enlarged by integer replication.
No alignment search, blur, tolerance, background weighting, or engine capture is used.

Definitions:

- Difference: exact straight-RGBA mismatch divided by the union of nontransparent pixels
  in the two images. Also report mismatch inside the reference silhouette alone.
- Ink: opaque pixels of exported `INK[0]` or `INK[1]` RGB, tested at corresponding screen
  locations. Denominator is the enlarged reference ink coverage. This does not silently
  count every dark material as ink, or label an absent outer keyline as ink.
- Inner contour: the renderer's `step < lit` pixels; same-location contour coverage
  retention. This measures the depth-break marks separately from literal face ink.
- Aggregate percentages divide summed integer counts, not averages of per-image percentages.

| Scale / px per m | Differing / union | Difference | Difference in reference | Ink kept / reference ink | Ink retained | Contour retained |
|---|---:|---:|---:|---:|---:|---:|
| ×3 / 96 | 497,543 / 1,944,272 | 25.5902% | 22.7958% | 720 / 6,480 | 11.1111% | 16.0937% |
| ×4 / 128 | 946,646 / 3,466,373 | 27.3094% | 24.3638% | 720 / 11,520 | 6.2500% | 10.2354% |
| ×5 / 160 | 1,472,502 / 5,414,967 | 27.1932% | 24.2602% | 719 / 18,000 | 3.9944% | 7.7733% |
| ×6 / 192 | 2,157,714 / 7,802,603 | 27.6538% | 24.6906% | 717 / 25,920 | 2.7662% | 6.1390% |

| Strip | ×3 difference | ×4 difference | ×5 difference | ×6 difference |
|---|---:|---:|---:|---:|
| idle8, thirty builds × eight directions | 24.7953% | 26.3083% | 26.2428% | 26.6031% |
| walkS, thirty builds × eight frames | 26.3316% | 28.2423% | 28.0790% | 28.6329% |

The strong ink-coverage decline is primarily scale: the reference's 720 literal ink
texels become 720×scale² screen pixels, while the rig still draws each point mark as
one render pixel. It does not mean 94% of the eyes vanish at ×4. Contours likewise remain
one render pixel wide. This demonstrates the mechanism and motivates rasterizing small;
it is **not a measured gap between the game and the art**, and promises no exact engine parity.

**Dither correction beside the numbers:** the charter hypothesized rig-pixel dither
versus game art-pixel dither. Rig 10.3 rounds tones without dither, and the game's
HH_FIGURE branch also bypasses Bayer. The hull branch indexes Bayer on art cells.
There is no figure dither-phase penalty in these results. The ruled smooth hull `dith`
weights stay untouched.

Local evidence (ignored, not shipped): `artifacts/pixel-look-a/measurements.json` contains
every count and pose; `.csv` contains all 1,920 rows; `measurement-run.txt` is the run log.
`contact-sheet.html` labels `contact-sheet.png`: five first CAST builds, each with idle
frame 0 SE and walk frame 3 S, reference ×4 then native px128. The PNG was visually
inspected. Both sides are revision 10.3, not a mixture with the pending 10.4 intake.

## 3. Route 1 — figures' own target (recommended, highest plumbing risk)

Use a per-camera art-grid target and the existing facet shader. Keep main hull, water,
reflection, foam and guard attachments and passes at their current sizes. Do not merely
run the same all-renderer list twice: ON must exclude figure facet draws from the main
colour list, while retaining their current guard/reflection participation. Use an
explicit figure registration/filter contract; never globally disable the renderer or
change its scene layer and accidentally remove it from other cameras and reflections.
OFF must select the existing recording path and existing bindings without changes.

### Occlusion and ink aboard

There are two depths: hardware depth includes the sorting shear/bias; `_HHDepthTex`
is unbiased true depth for contour. They are not interchangeable. A naive copy of
full-size depth to the small target has a different projection/grid, and cannot supply
the colour/id/true-depth neighbourhood needed by the resolve.

Proposed safe starting point: replay visible nonfigure occluder geometry (hulls, props,
doors and deck list) into a **private low-resolution context MRT and depth**, using the
same art-grid projection as the figures. Then draw the figures into that context with
the facet shader unchanged. Hull geometry carries its original hull/fore ids; an aboard
figure still writes the parent hull single. Same projection and ZTest preserve which
geometry is in front at each art-texel sample. Figure-vs-figure depth also remains shared.
This duplicate hull work is a real cost, not part of the 1/step² fill saving.

The context cannot be composited wholesale: that would pixelate the boats. Distinguish
visible figure coverage from hull context despite their identical ids. One implementable
proposal is a private depth-stencil attachment: context writes stencil 0, figure list
writes stencil 1 only on depth pass, using render-state overrides. A stencil-tested
fullscreen extraction writes a Point R8 coverage mask; no extra facet colour output is
required. Resolve the context with the existing colour/ink rules, then export only figure
coverage. Preserve any enabled figure ring by extending coverage only where the resolve's
figure-ring rule produced it, not by blindly dilating the silhouette. Rig 10.3 defaults
to no outer ring. Verify stencil support/store behaviour on target APIs before selecting
D24S8 vs D32S8; this is an implementation risk, not a measured capability today.

Context-only hull texels inform neighbouring figure contour and prevent a false outline
against its own deck; **they never replace the displayed hull pixels**. Reject a design
that resolves isolated figures against transparent black and hopes an id match will
recreate the missing deck neighbourhood. The original high-resolution hull resolve
also loses the figures as depth neighbours when they move out; Phase C must check that
removal does not alter visible hull contour next to a figure. If needed, supply the
low figure depth as read-only context to hull resolve, without shrinking hull shading.
That potential resolve adapter is explicitly priced below.

As built (Phase C; the owner's ruling of 10-16): ON keeps the hull's contour against her
own deck, as OFF has it. The coverage pass's mask (her visible texels) and the small
target's true depth go to the full-size hull resolve, and to it alone, as rule-1
neighbours: a neighbour pixel whose texel, found by the overlays' own lookup, is hers is
solid at her depth there. A deck pixel more than 0.30 m farther than her darkens to its
own darkened colour, so the contour stays one screen pixel wide in the hull's own contour
colour. The read sits behind a global gate the hull feature raises around that one blit
and drops after it, so the mesh reflection resolve and the target's own resolve never see
her; OFF records nothing new.

Displaced water currently contributes to the private hardware depth before facets.
Route 1 must preserve its occlusion too. Read/reproject the existing full-resolution
hardware depth at the low texel's world sample into private depth before context replay;
test reversed Z, projection offset and uncovered samples. Do not copy unbiased contour
depth as hardware depth. This adds a depth-sampling/transfer pass and may need an
explicit sampleable depth view. **No edit to the water passes is authorized.** If the
available depth cannot be consumed without changing those passes, bring that file and
requirement to the seat; do not silently omit water occlusion. Phase B's dry-frame probe
can answer the look question, not prove this integration.

### Re-entry, sort and D4

- Ashore: the existing cell overlay reads the new texture via the camera's integer map,
  checks its existing figure id, and keeps the source sprite's sort and SortingGroup.
- Aboard: compose figure colour **inside the owning hull's existing overlay sort**, with
  coverage and id checks. Extend the hull overlay bounds to include its figures and a
  one-art-pixel ring when needed; a cell quad must not clip a tall passenger or carried
  pose. Retain the fore-id handling used by deck sprites. A global fullscreen overlay
  after the scene is wrong for scenery and deck occupants between sorts.
- D4 lean: the low context's occlusion sample owns the entire enlarged figure art pixel.
  The house edge can therefore be coarsened locally where it covers the figure; the hull
  shading elsewhere stays at native resolution. That edge decision needs owner acceptance.
  Alternative: also compare the figure's depth against main hardware depth at every
  composite screen pixel, retaining the fine house edge. That adds a native-resolution
  depth read and cuts blocks; it cannot simultaneously promise whole art-pixel edges.
- D4's eventual zero-through-house gate must use the ruled mask (art or fine), explicitly
  named in the plate. Do not claim both interpretations are byte-identical.
- As built (Phase C; the owner's D4 of 10-15 and ruling of 10-16): the facet pass leaves
  the small target's stencil at 1 where a figure shows, 2 where context stands alone, 3
  where context covers a figure, and 0 elsewhere (empty, or her ink ring). The export
  copies 0 and 1. A coverage pass writes 1 on each texel where she shows (stencil 1) into
  a texel-size R8 mask, and an edge pass copies a 3 only where one of the four texels that
  share an edge with it (the 4-neighbourhood) is hers. The house's own art pixel squares
  off the occlusion edge beside her visible pixels, one art pixel deep; deeper under the
  house nothing is exported, so the full-size house draws over her hidden body exactly as
  it does OFF. A 2 is never exported. Phase C checks it by removing her: a pixel that
  changes between the shot and the same frame without her, farther than one art pixel
  from her visible pixels, is a ghost, and the bar is zero.

### Lattice, motion, cameras and effects

Let `q=1/32 m` in the camera-visible world XY plane (the meshes are already iso-projected).
Place target **edges**, not centres, at integer multiples of q. Derive its lower-left
origin by flooring the visible lower-left world coordinate / q, then add a one-texel
guard border. Render at exactly 32 samples/metre with that padded projection, not merely
the screen projection in fewer texels. At integer step s, map each output screen pixel
to `floor((screenPixel - integerOriginOffset)/s)` and use Point/Load sampling.
The origin offset is the art-grid origin minus the camera viewport origin in screen
pixels, rounded once to whole screen pixels. Respect viewport origin, Y flip and odd
viewport sizes. A moving camera's residual inside an art pixel moves the composite by
whole screen pixels; it never moves the source lattice fractionally.

Ashore: snap only the drawn root XY to q, leaving source/body, simulated position, depth,
sort source and save untouched. Before a camera publishes a valid grid, it is inert.
Aboard: keep the hull's screen-pixel motion and continuous parent pose; do not snap the
hull or counter-move the rider root. Sample the attached figure onto the world art lattice.
Its silhouette changes in art-pixel increments over the smoother deck; a foot can appear
up to roughly half an art pixel from its continuous projection. Do not add a per-hull
fractional composite offset: that would make neighbouring sprites and riders use different
lattices. The moving strip must expose this tradeoff.

At step 1 or downscale, use the camera's own size and projection, as today; no extra
upscale. At upscale, nominal dimensions are `ceil(screenW/s) × ceil(screenH/s)`, plus
guard border, with crop of the padded projection. At feel effects off the ladder, keep
32 samples/metre and the world lattice, recompute visible extent, composite nearest and
round the origin offset to a screen pixel. Magnification is then noninteger: blocks can
alternate floor/ceil widths. Exact equal s×s blocks and a continuously changing ortho
cannot both hold. If magnification falls below 1, use native resolution. Shoot push-in,
shake, pull-back and the return to the rung; do not quantize the camera effect itself.
With PixelGridSnap OFF, whole-screen composite translation likewise quantizes residual
camera motion by at most half a screen pixel. The switch does not change simulation.

Own target handles, projection, sizes, offset, material snapshot and bindings **per camera**;
do not reuse the last camera's state or only the global GameServices grid. Mirror the
existing resolve-target lifetime and disposal; cap/release dead camera entries. Cache
packets, arrays and materials; allocate only on creation/resizing. Scope `_ScreenParams`
and view/projection overrides to the low pass and restore them before other draws.
No new recording on Null device, no live visible mesh figures, or switch OFF. Inactive
ashore figures can hold ids; FigureCount alone is insufficient for the new idle gate.

### Files, ownership and per-frame price

| Proposed touch | Owner / purpose |
|---|---|
| `Code/Art/IsoFacetHullFeature.cs`; new `FigurePixelTarget` pass/state helper | art-pipeline + lead-architect; list separation, low context, per-camera handles and scoped uniforms; blocked on F2 ordering |
| `Code/Art/IsoCharacterFigureRenderer.cs`; `IsoFacetHullRenderer.cs`; new figure registration helper | same owners; coverage submission, binding, hull overlay bounds; no new pool ids |
| `HiddenHarboursIsoFacetOverlay.shader`; new coverage/depth-transfer/composite helpers; possibly `HiddenHarboursIsoFacetResolve.shader` adapter | art-pipeline + lead-architect; mapped sampling and context-aware composite; retain the facet shader unchanged |
| New `Code/Core/Art/FigurePixelGrid.cs` and heading math; `Core/Services/GameServices.cs`; `Core/Config/GameConfig.cs` | lead-architect; flat defaults, camera publication contract, pure presentation math |
| `Code/App/CameraFollow.cs` | lead-architect; publish per-camera framing without altering the existing mesh grid |
| `Code/Player/DeckRiderMeshPresenter.cs`; `Code/Art/CharacterFigurePresenter.cs` | gameplay-systems / art-pipeline, lead-architect review; heading application, ashore drawn-root snap; shared-file ordering required |
| `Assets/Tests/EditMode/**`, new PlayMode plates and readback extensions | qa-test with art-pipeline; defaults, math, allocation and image gates; existing intro fixture read-only |
| `Assets/_Project/Data/Config/GameConfig.asset` only if explicit serialization is desired | lead-architect shape, economy-sim values; false/0 defaults work when fields are absent; no runtime config edit in Phase A |

For F visible figures and H context draws, cost is O(F+H) submissions, unchanged CPU
skinning, another context geometry traversal, four low MRT writes, depth/stencil, one
coverage extraction, one low resolve and screen-resolution overlay reads over figure
bounds. Water depth transfer and optional fine-edge clipping add reads. Small fill area
does **not** imply 1/s² GPU time or less vertex work. Pool packets and avoid LINQ,
temporary lists, per-frame materials and managed closures. Target changes can allocate;
steady-state frames must allocate zero managed bytes.

## 4. Other routes

| Route | Files and owners | Per-frame price and risk |
|---|---|---|
| **2. Whole facet block small** | `IsoFacetHullFeature.cs`, art-pipeline + lead-architect, after F2 order; low-size coordinate binding in `HiddenHarboursIsoFacetOverlay.shader` and every consumer that assumes screen-coordinate Load; qa-test plates | Smallest raster change: divide by step as reflection sizing already does. Shared occlusion, ids and contour relationships survive within the block by construction. Same geometry submissions, ~1/s² facet/resolve fill. But hulls, props and trucks visibly change; water shares depth and cannot attach a full-sized colour buffer to smaller depth. Resizing or separating that water work broadens scope and its consumers; do not call it a one-line complete fix. Probe on a local scratch branch only, dry frame if required. Not the PR route unless D1 brings boats in. |
| **3. Pixel Perfect Camera Upscale Render Texture** | Active region camera components, `App/CameraFollow.cs`, App camera builders and render setup, owners world-content + lead-architect; UI/UX for canvas/screen-space consumers; all image tests | Whole participating camera frame at reference size; all its scene passes pay low fill and a final upscale. Sprites, terrain, water, screen-coordinate shaders and camera motion change. UI rendered by that camera changes; overlay UI may remain high-res and requires alignment review. CameraFollow currently rewrites references and disables the component for downscale/feel. Enabling a serialized flag alone cannot establish those behaviours. Largest visual blast radius; unsuitable for a figures-only feature. |

Route 2's unmodified MRT relationships are attractive for a look probe. They do not make
the hull look acceptable without D1, nor preserve native-size external texture readers.
Route 3 can reduce more fill, but gives up the camera's screen-pixel smoothness and
needs an explicit downscale policy to keep large hulls uncropped. Neither is the lean.

### Size/cost ledger (headless arithmetic, not GPU timings)

Node counted allocated texels for 1920×1080. The six baseline attachments total 24 bytes
per texel: 3×RGBA8 + R32 true depth + Depth32 + RGBA32 resolve. Driver alignment and
transient aliasing are excluded. Route 1 adds coverage and stencil: budget 25–29 bytes
per padded texel (4–8 bytes for depth/stencil instead of 4). Both the main hull targets
and unchanged water/reflection targets still exist.

| Step | Nominal target | Texels | Screen area | Base 24-byte attachment estimate |
|---|---|---:|---:|---:|
| ×1 / downscale fallback | 1920×1080 | 2,073,600 | 100% | 49,766,400 B |
| ×2 | 960×540 | 518,400 | 25% | 12,441,600 B |
| ×3 | 640×360 | 230,400 | 11.1111% | 5,529,600 B |
| ×4 | 480×270 | 129,600 | 6.25% | 3,110,400 B |
| ×5 | 384×216 | 82,944 | 4% | 1,990,656 B |
| ×6 | 320×180 | 57,600 | 2.7778% | 1,382,400 B |
| ×8 | 240×135 | 32,400 | 1.5625% | 777,600 B |

At foot ×4 the one-texel border makes 482×272 = 131,104 texels, 6.3225% of screen;
route 1's attachment budget becomes 3,277,600–3,802,016 B per camera, before any extra
depth view/copy. A shifted origin/odd viewport can require an additional coverage texel;
derive allocation from covered world edges, not the nominal division alone. GPU ms,
actual bandwidth, per-frame allocation and game pixel differences are **not measured**
in Phase A. Phase B measures base/probe GPU cost; Phase C measures the actual route.

## 5. Tunables and future gates

Proposed flat fields, owned in shape by lead-architect:

| Field | Explicit code default | Meaning |
|---|---|---|
| `FigurePixelTarget` | `DefaultFigurePixelTarget = false` | Ship OFF. Gate the entire new drawing/snap/heading path; OFF must remain byte-identical. |
| `FigureHeadingSteps` | `DefaultFigureHeadingSteps = 0` provisionally, pending D2 | 0 = continuous; positive N = N equally spaced world compass headings. Quantize before applying the def's measured azimuth sign; aboard convert quantized world heading back through the continuous hull heading. Never quantize hull/simulation. |

Initialize fields from named constants and use the same constants when GameServices
has no config. Explicitly test missing fields by loading the real serialized asset
with the keys absent, not only CreateInstance or a JSON surrogate. Distinguish explicit
false/0 from missing; if D2 chooses a nonzero default, preserve a deliberately saved 0.
Negative heading steps should validate to 0. No camera dimensions or PPU tuning knobs:
PPU is the locked 32 and sizes derive from the camera. No fields were added in Phase A.

Phase C gates: all ladder sizes including downscale; negative coordinates and viewport
parity; art-texel edges, integer composite offsets; smooth identity and N headings with
azimuth sign aboard/ashore; no-camera snap inertness; missing-field defaults; zero
steady-state managed allocations. Multi-camera interleaving, null-device and no-figure
frames need coverage. GPU gates: OFF 0 differing pixels versus base; 0 figure pixels
through the ruled house mask; aboard id/ink rule; ashore sort; head/face/clip behaviour;
water/guard/reflection independence; frame-numbered moving and feel strips.

## 6. Blast radius and shared-file order

This is a **read/search inventory**, not a Unity run. Direct texture/feature references
and capture/ReadPixels/descriptor sites were inspected; local line indexes are in
`artifacts/pixel-look-a/blast-radius-files.txt` and `test-pixel-readers.txt`.
Generic source-text hits in unrelated ramp-width assertions are not GPU evidence.

| Tests / helpers | Why they matter |
|---|---|
| `AshoreFigurePlatePlayTests`, `VillagersAshorePlatePlayTests` | Actual figure footprint, ids, guard and OFF comparisons. Their HullScreenTex membership assertions need an ON figure-target equivalent. |
| `DeckRiderMeshPresenterPlatePlayTests`, `DeckRiderMeshPresenterPlayTests`, `CharacterMeshCastAboardPlayTests`, `CharacterMeshCastAshorePlayTests` | Supersampled cell RTs, sprite/mesh handover, sort, runtime cast coverage. |
| `IntroPassengerMeshFigurePlayTests.TheArrival_DrawsHerAsHerMesh_BelowAndOnDeck_TurnedByHerHeldHeading_AndHandsHerBackAshore` | Read-only overlap: carried figure under continuous hull pose, below/above deck, heading, handback. This is primarily presentation assertions, not a new pixel measurement. |
| `IsoFacetFigureIdTests` (9 tests), `IsoCharacterFigureRendererTests`, `CharacterFigurePresenterTests`, `DeckRiderAshoreHandOverTests`, `IsoCharacterFigureLifeTests`, `IsoCharacterFigureV9Tests` | Id refusal/recycling, one shared gate/source-text guard, material/face/ink and lifecycle invariants. Keep aboard id spending zero. |
| `IsoFacetFeatureInstallTests`, `IsoFacetKeylineGateTests`, `IsoFacetShaderCompileGuardTests`, `IsoFacetFigureVariantCompileGuardTests`, `IsoFacetFigureToneTests`, `DeckOccupantSlotTests` | Feature install, shader variants and keyword independence, keyline gate and fore-id bands. |
| `HullMeshReflectionTests` | Direct camera descriptors and resolve `TexSize`, including 1920×1080; per-camera snapshots, zero/overflow paths. The figure feature must not shrink these. |
| `IsoFacetUrpPassTests`, `CharacterSkinFacetPassTests`, `HullWaterlineAcceptanceTests` | Explicit RT sizes, private depth, actual GPU readbacks; Null-device separation. HullDeckProbe is exercised here. |
| `IsoFacetCapeEndToEndTests`, `IsoFacetLobsterEndToEndTests`, `IsoFacetLobsterVariantAcceptanceTests`, `IsoFacetFleetPackAcceptanceTests`, `IsoFacetSideDraggerAcceptanceTests` | Hull cell-size captures and pixel comparisons, as well as CPU/bake checks. Route 2 broadens this blast radius. |
| `BoatInteriorMeshSpikeRenderTests`, `FullMeshInteriorRenderTests`, `MeshInteriorLampsPlayTests`, `BoatLampRegimePlayTests`, `BoatWindowGlowPlayTests` | Interior occlusion and light readbacks with fixed RT dimensions. |
| `HullsRideTheTidePlatePlayTests`, `SloopsOnTheDevKeyPlatePlayTests`, `MeshHullPlayTests`, `HullReflectionPivotPlayTests` | Native hull pictures, depth/heave and reflection alignment. |
| `AtvPlatePlayTests`, `Modern3500PlatePlayTests`, `ModernTruckPlatePlayTests`, `VehicleSkinPlayTests` | Vehicles share the hull renderer; riders and overlays must not shrink vehicle targets. |
| `InFrameChannelReadback`, `WharfNightStage`, `BelowDecksDrawSources` | Read globals while the graph is alive. After graph teardown they can be rebound to 4×4 black. Keep per-channel dimensions, capture identity and camera identity. |
| `FoamHistoryInitializationGpuTests`, `FoamTransportGpuTests`, `FoamBufferPassTests`, `FoamTransportPassTests`, `FoamBufferQuantizationTests`, `FoamBufferFadeTests`, `FoamFadePlateTests`, `FoamTrailRootPlateTests`, `WakeDispersalStampTests`, `SurfSequenceTests`, `WakeFoamAgeingMeasurementTests` | Shared feature, target allocation/descriptors, globals and independent foam math/readbacks; do not divide their targets. |
| `WakeCentrePhotographPlayTests` and its `FoamVisibility`, `GameplayAcceptance`, `SternResponse` partials; `WakeFoamWindowPlayTests`; `WaterWhiteoutShoreSwirlAcceptanceTests` | Full-world water and foam captures share depth/composite context; broad regression photographs. |
| `ObjectReflectionProbeTests`, `ReflectiveObjectTests`, `DriftLineProbeTests`, `RippleBandProbeTests`, `LampShadowRenderTests`, `LampShadowMathTests`, `SunShadeReceiverRenderTests` | Screen textures/depth sampled by reflections, lamps and shadow receivers. |
| `CliffPxLookPlatePlayTests`, `VillageReturnPlatePlayTests`, `LightsIlluminatePlatePlayTests`, `LightsAreSourcesPlatePlayTests`, `MoodGradeTriptychPlatePlayTests`, `StPetersGroundPlatePlayTests`, `StPetersKeyScenePlatePlayTests`, `TreePass4PlatePlayTests`, `WharfBuildingPass2PlatePlayTests` | Whole-scene plate harness consumers. Most directly endangered by route 3, also contextual checks for route 1 and dry route-2 probe. |
| `CameraZoomPolicyTests`, `PixelGridSnapTests`, GameConfig default tests | Existing ladder/grid/default contract; add separate figure cases instead of rewriting the vehicle rule. |

Overlaps read, none edited:

1. F2 owns changes in `IsoFacetHullFeature.cs`, `FoamTransportField.cs` and
   `FoamTransportVelocity.cs`. Wait for its merge and the seat's new base before Phase C
   edits the feature, or explicit ordering. Reflection, foam, guard and water passes stay.
2. #938 at `6acad6f2` changes `Player/DeckRiderMeshPresenter.cs` for suspended skins/held
   props. Its held ruling and the seat's file order bind a later presenter edit.
3. Character 2b's current handoff covers builder/recipe/save/appearance seam. A read-only
   worktree status check now finds uncommitted changes, superseding the deployment's
   earlier clean-box snapshot. Exact shared files are
   `Assets/_Project/Code/Art/IsoCharacterFigureRenderer.cs`,
   `Assets/_Project/Code/Player/DeckRiderMeshPresenter.cs`, and
   `Assets/_Project/Code/Core/Services/GameServices.cs`. Other changes cover build data,
   recipe/save/Core events, builder tests and its ADR. Read its settled file list again
   before Phase C; these three files explicitly wait for the seat's order. Nothing in
   that worktree was edited by this lane.
4. #916 at `630b376b` is cabin door/threshold, App arrival and DeckWalkController work.
   The passenger test above is read-only; the seat says no figure renderer overlap.
5. #877 at `8d466ec9` was read before route 1's id/overlay design. No pool redesign here.
6. Pending kit 10.4 intake owns rig/preset changes; this lane rebakes nothing. Re-control
   and label both sides with the same revision if the seat moves the base later.
7. The St Peters light pass's build/overlap sections cover light/material/environment
   inputs and its own plates; it is not deployed. No expected figure-file collision;
   do not add its broad lighting work to this feature.

No Unity, editor slot, C# compilation, EditMode/PlayMode execution, GPU timing, fetch,
push or PR occurred in Phase A. The Node checks above are the only executed tests.

## 7. Stop and decisions

D1 now: figures first, boats later? Recommended **yes**, route 1, with route 2 only as
the disposable Phase B probe. The owner and seat must settle this and grant a slot.
D2 later: heading default, lean 0 smooth. D3 later: eye-led closeness; numbers inform.
D4 later: art-pixel or fine hull edge at occlusion. D5 at PR: switch ships OFF, owner
rules ON and merges. No action or wait is armed at this stop.
