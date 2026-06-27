# ADR 0014 — Painted seabed-height authoring: hand-paint the coast, the SAME map drives render and sim

- **Status:** **Accepted — slice landed.** Records the decision for HOW seabed/terrain heights are
  **hand-authored** (painted) and how a painted map becomes the single height source both the water
  render and the tide sim read — **the one-height-map / three-consumers invariant of ADR 0009/0010/0012
  preserved**. Unlike ADR 0010/0012 (docs-only), this ADR ships a working **vertical slice**: the
  painted-height **asset format**, a `PaintedTidalTerrain` (`ITidalTerrain`) the sim reads, a
  `WaterSurface` path that bakes the same map for the visual, an **edit-mode shoreline preview**, a
  Scene-view **paint tool**, a one-click **export of today's analytic St Peters terrain to a painted
  map** to paint FROM, and the pure-sampler **determinism tests**.
- **Date:** 2026-06-27
- **Decision owner:** lead-architect (the seabed height is the P1 render==sim integrity seam — a
  cross-cutting/architectural call: `agents/coordination.md` §1.1 "Water/fog/lighting" seam, §8;
  CLAUDE.md rule 4). tools-editor owns the paint tool; art-pipeline owns `WaterSurface`/the water look.
- **Flagged from:** the owner wanting to **design the coast by PAINTING elevation** — and to **SEE the
  coast while editing** (today it only appears at runtime, after `WaterSurface` bakes the height
  texture; in edit mode the whole scene reads as deep water, so the water's edge can't be designed).
  Today St Peters' terrain is **analytic code constants** in `StPetersBuilder` (island +6 m, sandbar
  crest +1.6 m, channel −0.6 m, deep −4 m) baked by `TidalTerrain.ElevationAtZones`. Reshaping the coast
  means editing C# + re-running the builder.
- **Related:** `0009-tidal-exposure-and-region-display-name-seams.md` (the `ITidalTerrain.ElevationAt`
  + `IEnvironmentService.WaterLevelAt` + `Core.TidalExposure` seam — *the one rule render and sim share*),
  `0010-water-rendering.md` (decision (3): **one height map, three consumers** — render / walkability /
  boat-cross — `depth = WaterLevelAt(t) − terrainHeight`; the rejected "separate height fields for
  rendering vs gameplay" is the exact drift this ADR also forbids), `0012-shoreline-rendering.md` (the
  smoothed-shader shore + the bake-resolution discussion this reuses), `0011-committed-hand-authored-scenes.md`
  (committed authored scenes / data the owner edits, the LOGIC-vs-VISUAL split — the painted height map
  is **authored DATA**, read by the sim, *not* a visual-only layer), `design/water-rendering.md`,
  `design/time-tides-weather.md` §3.5 (the water-depth rule), `vision-and-pillars.md` §5.8 / P1 ("The
  Sea Has Moods" — what you SEE == what you SAIL/WALK).
- **Implementation pointers (shipped by this ADR):**
  `Assets/_Project/Code/World/PaintedHeightField.cs` (the **pure** bilinear sampler — the determinism
  core), `Assets/_Project/Code/World/PaintedHeightMap.cs` (the **ScriptableObject** asset: a CPU-readable
  height texture + world rect + elevation range, decoded once into a cached `float[]`),
  `Assets/_Project/Code/World/PaintedTidalTerrain.cs` (`ITidalTerrain` over the painted map; registers
  into `GameServices.TidalTerrain`), `Assets/_Project/Code/Art/WaterSurface.cs` (the new
  `DepthSource.PaintedHeightMap` that feeds the SAME painted texture to the shader),
  `Assets/_Project/Code/App/Editor/TerrainPaintTool.cs` (the Scene-view brush + tide-preview + St Peters
  export — **renamed from `SeabedPaintTool.cs`** and extended into the unified terrain-type tool below;
  alongside the builders, the tools-editor home with the StPeters constants + World/Art refs it needs),
  `Assets/_Project/Code/World/TerrainHeightPalette.cs` (the pure elevation→colour ramp for the edit-mode
  overlay), `Assets/Tests/EditMode/World/PaintedHeightFieldTests.cs`,
  `Assets/Tests/EditMode/World/TerrainHeightPaletteTests.cs`, `Assets/Tests/EditMode/TerrainPaintToolTests.cs`.

## Addendum — the unified Terrain Paint Tool (height + LOOK in one stroke)

The original tool (§6 below) painted **only the height map**. Per the owner's explicit choice of the "paint a
terrain TYPE" model (over auto-texture-from-height or two separate brushes), the Scene-view tool is extended —
**still editor-only, the height side / the P1 invariant UNCHANGED** — and renamed to **Terrain Paint Tool
(height + look)** (menu `Hidden Harbours ▸ Tools ▸ Terrain Paint Tool (height + look)`):

- **Terrain-TYPE brush (the headline).** A TUNABLE list of presets (rule 6), each `{ name, optional ground
  tile, elevation, clearTile }`. Painting a type in ONE stroke (a) sets the height-map cells under the brush
  to the type's elevation AND (b) stamps the type's tile onto the target GROUND tilemap at those cells.
  Defaults (owner-editable): **Deep** (−4, no tile), **Channel** (−0.6, no tile), **Beach** (~0.3, Sand),
  **Sandbar** (1.6, Foam — the closest "wet sand" the generated set has), **Grass/Land** (6, Grass), **Cliff**
  (~8, Rock). Underwater types (no tile / `clearTile`) set height only and CLEAR any land tile so the water
  shows. Presets bind to the closest tile from `TileAssetBuilder.Terrain`; a missing tile leaves the preset
  EMPTY (height-only) and the UI hints to run "Build Scene-Painting Toolkit".
- **The height brushes are kept** (Raise / Lower / Set-height / Smooth) for fine-tuning, as are New /
  Export-St-Peters / Adopt / the PaintedHeightMap asset / the WaterSurface preview. **The height map remains
  the single source of truth for water + tide (P1); the tile is authored VISUAL content, not sim.**
- **Edit-mode HEIGHT COLOUR OVERLAY.** A toggle renders the terrain false-coloured by elevation (deep blue →
  cyan shallows → sand → green → brown/rock) in the **Scene view ONLY**, with a legend and the current
  preview-tide waterline — a "hidden height map" the owner can SEE while adjusting. It is a designer aid
  drawn with GL/Handles from the decoded height field (`World.TerrainHeightPalette` is the pure ramp); it
  **never serializes and never renders in Play or a build**, and rebuilds its small CPU texture only when the
  field changes (no per-frame churn — rule 7).
- **Target ground tilemap.** Auto-found (prefers the `TerrainTilemap` that "Add Paintable Tilemap" creates),
  owner-assignable, and create-on-demand (it reuses `PaintableTilemapMenu.AddPaintableTilemap` rather than
  failing). Tiles are written via the Tilemap API, Undo-recorded, scene marked dirty.

## Context

The shoreline is the most P1-load-bearing pixel in the game (ADR 0012). ADR 0009/0010/0012 locked the
architecture that keeps render and sim honest: **one height map, three consumers** (water render, on-foot
walkability, boat-cross), all reading `depth = WaterLevelAt(t) − ElevationAt(pos)`. The visible waterline
and the playable waterline are the **same line by construction** — the **P1 integrity rule**.

But how the height field is *authored* was left open (ADR 0009/0010 "height-map authoring source"). St
Peters backs `ITidalTerrain.ElevationAt` **analytically** (`World.TidalTerrain.ElevationAtZones`: a few
blended zones — island plateau, sandbar ridge, channel trough, deep floor — from serialized constants
mirrored from `StPetersBuilder`). Two problems the owner hit:

1. **The coast can only be reshaped in code.** Want the bar wider, or a second cove? Edit C# constants
   and re-run the builder. The non-dev owner cannot draw the coast he wants.
2. **The coast is invisible in edit mode.** `WaterSurface` bakes the height texture in `OnEnable` (play
   only) and the shader reads `_HeightTex` at runtime. In the Scene view (not playing) `_USE_HEIGHTTEX`
   is off, so the water plane reads as **uniform deep water** — the designer can't see where the land,
   bar, and channel sit while composing the scene. He's painting blind.

The owner's ask: **paint elevation by hand**, have **what he paints be BOTH what the water shows AND
what the tide bares/floods**, and **SEE it while editing**.

## Decision

**Add a hand-painted height map as an alternative `ITidalTerrain` source — authored DATA, sampled by the
sim and fed to the render as the SAME texture — with an edit-mode preview and a Scene-view paint tool.
The analytic `TidalTerrain` stays intact as the default/fallback; adopting the painted map is an explicit
per-region step. The one-height-map / three-consumers invariant is preserved by construction.**

### (1) The painted-height ASSET — a `PaintedHeightMap` ScriptableObject wrapping a CPU-readable height texture

A new `World.PaintedHeightMap : ScriptableObject` (content-as-data, CLAUDE.md rule 2 — one asset per
region, a stable name) holds:

- a **`Texture2D` height texture** (R channel = normalized elevation 0..1), authored at a chosen
  resolution (default **192²**, matching the ADR-0012 bake — ~0.83 m/texel over 160×120 m);
- the **world rectangle** it covers (`WorldCenter`, `WorldSize`) — the same frame the shader's
  `_HeightWorldMin/_HeightWorldSize` use;
- the **elevation range** (`MinElevation`, `MaxElevation`, m above chart datum) the R channel maps across
  — the same `_HeightMin/_HeightMax` the shader lerps.

**The texture MUST be CPU-readable** (`isReadable = true`, set on its importer) — otherwise the sim
cannot sample it, the known gotcha. To keep the sim off the GPU read path entirely (and frame-rate-safe),
`PaintedHeightMap` **decodes the texture once** into a cached **`float[]` of metres-above-datum** (lazily,
on first sample / on `Rebuild()`), and all `ElevationAt` calls read that array — never `GetPixel` per
call (rule 7). The decoded field is wrapped in a pure `PaintedHeightField` (below).

The texture is committed like any authored asset: an **external `.png` next to the `.asset`** (the `.png`
in **LFS**, both `.meta`s committed), and the `.asset` references it by GUID. This keeps the `.asset`
Force-Text/smart-mergeable and the height bytes LFS-managed — the paint tool writes/imports the `.png`
(CPU-readable + linear + R-usable) and points the map at it (it does **not** embed the texture as a
sub-object, which had drifted from the committed seed and orphaned its PNG — see the rejected alternative).
It is **authored DATA the sim reads**, distinct from ADR 0011's *visual-only* painted layer.

### (2) The PURE sampler — `PaintedHeightField` (the determinism core, headless-testable)

A plain POCO (no `MonoBehaviour`, no `ScriptableObject`, no scene) that owns a `float[]` + resolution +
world rect + elevation range and implements:

- **`ElevationAt(worldPos)`** — **bilinear** sample of the float field, with the **exact same world→uv
  mapping the shader uses** (`uv = (worldPos − worldMin) / worldSize`), so the rendered depth and the
  sampled-by-sim depth come from the *same* interpolation → they cannot diverge (P1);
- **world↔texel mapping**, min/max↔normalized mapping, and **out-of-rect clamp** (sampling outside the
  painted rect clamps to the edge texel — a boat far offshore reads the edge depth, never an exception).

Because it is pure (no Unity object graph), the **EditMode determinism tests** exercise it directly:
bilinear interpolation, world↔texel round-trip, min/max mapping, out-of-rect clamp, and `ElevationAt`
reproducibility. This is the determinism guard the seam needs (CLAUDE.md rule 5 — authored data, no RNG,
nothing saved at runtime).

### (3) `PaintedTidalTerrain : ITidalTerrain` (the sim reads the painted heights — painted == sailed)

A `World.PaintedTidalTerrain : MonoBehaviour, ITidalTerrain` references a `PaintedHeightMap`, builds the
cached `PaintedHeightField` on enable, and registers itself into `GameServices.TidalTerrain` (the same
self-installing pattern as `World.TidalTerrain`, ADR 0009). `ElevationAt(worldPos)` delegates to the
field. **This is what makes the SIM — the on-foot walkability gate (`TidalWalkability`), the clam-baring
(`ClamDig`/`ClamHoleVisual`), the boat-cross (`BoatCrossing`), the clam-hole scatter — read the PAINTED
heights**, because they all resolve elevation through `GameServices.TidalTerrain` / `TidalExposure`. So
what the owner paints is what bares, floods, and grounds — **painted == sailed/walked** (P1).

The analytic `World.TidalTerrain` is **untouched** — it stays the default for St Peters (and every other
region) until a region explicitly swaps in a `PaintedTidalTerrain`. A region has **at most one**
`ITidalTerrain` registered (last-writer-wins via `OnEnable`); the export step (below) is the explicit
hand-over.

### (4) `WaterSurface` reads the SAME painted map (render == sim by construction)

`WaterSurface` gains a **`DepthSource.PaintedHeightMap`**: instead of baking `ITidalTerrain.ElevationAt`
into a fresh texture, it feeds the painted map's **own texture** straight into `_HeightTex` (and its world
rect / min-max into `_HeightWorldMin/Size`, `_HeightMin/Max`). The render therefore samples the **exact
bytes** the sim decodes — there is no second bake to drift. (`DepthSource.Auto` still prefers a wired
`ITidalTerrain` bake, then distance-to-land; the painted path is selected explicitly or auto-detected
when a `PaintedTidalTerrain`/map is present — see the file.) Because the painted texture *is* the sim's
source and the render's source, "separate height fields for rendering vs gameplay" (ADR 0010's rejected
alternative) is structurally impossible here.

### (5) EDIT-MODE visibility — the headline UX win

`WaterSurface` is made **`[ExecuteAlways]`** so the height bake/feed and the uniform push run **in the
Scene view, not only in Play**. A serialized **`_previewTideLevel`** + **`_previewInEditMode`** drive the
shader's `_WaterLevel` while not playing (in Play the live sim drives it). So the designer **sees the
coast — land dry, bar baring, channel flooded — and can scrub a tide-level slider to watch what bares and
floods at any tide, WITHOUT pressing Play.** This is what unblocks coast design. The preview is
**presentation-only**: it sets a material uniform for the editor view and feeds no sim, saves nothing
(rule 5); at runtime the live `WaterLevelAt(t)` overrides it.

### (6) The PAINT TOOL — a Scene-view brush + zone stamps + live preview (tools-editor)

`World.Editor.SeabedPaintTool` (an `EditorWindow` + Scene-view input) paints elevation onto a
`PaintedHeightMap` over the scene's world rect:

- **Brushes:** Raise / Lower / Set-to-height / Smooth, with **tunable** brush radius, strength, and
  target height (rule 6 — no magic numbers; every brush value is a window field).
- **Zone stamps:** one-click Land / Sandbar / Channel / Deep at the **canon St Peters heights**
  (+6 / +1.6 / −0.6 / −4 m, read from `StPetersBuilder`'s public constants so they can't drift) so the
  owner can block out tidal areas fast.
- **Live preview:** the tool writes into the map's texture and calls `Rebuild()`, and because
  `WaterSurface` is `[ExecuteAlways]` the Scene view updates immediately (colour-by-depth via the real
  water shader + the tide-level slider). Pixel-art-friendly (Point-filter option; the brush snaps to
  texels).

The tool only ever writes the **painted-height asset** (DATA) — it never edits the analytic
`TidalTerrain` or any scene LOGIC object, so it composes cleanly with the ADR-0011 single-author rule.

### (7) Seed St Peters from TODAY'S layout (paint FROM the coast, not a blank canvas)

A one-click **"Export analytic St Peters → painted height map"** (in the paint tool / a menu) samples the
current `TidalTerrain.ElevationAtZones` (the shipped St Peters constants) across the bake rect and writes
a `PaintedHeightMap` asset. The owner then paints **from the existing coast**. Adopting the painted map
for St Peters is an **explicit, optional step** (swap the scene's `TidalTerrain` for a
`PaintedTidalTerrain` pointing at the exported map, or point `WaterSurface` at it) — **this ADR does not
silently change the shipped St Peters look**; the analytic terrain remains the default until the owner
opts in.

### Determinism & save (the invariant guarded)

The painted map is **authored DATA committed like a tilemap** — read at runtime, never written at runtime.
`ElevationAt` is a pure function of (painted bytes, position): **no RNG, nothing serialized at runtime**.
The tide is still **recomputed from `(worldSeed, gameTime)`** via `WaterLevelAt(t)` and never saved
(rule 5). Exposure/depth = `WaterLevelAt(t) − ElevationAt(pos)` exactly as before — only the *source* of
`ElevationAt` changed (analytic → painted), and both are deterministic. **No save-format change**, so no
schema bump or migration (ADR 0008). The edit-mode tide preview is editor-only presentation, never sim or
save.

### Performance posture (rule 7)

The sim samples a **cached `float[]`** (decoded once on enable / on `Rebuild()`), never a per-call texture
read. The render feeds the painted texture directly — **no per-region bake** when the painted path is
used (cheaper than the analytic bake). `WaterSurface` still pushes uniforms on the throttled tick through
a pooled `MaterialPropertyBlock`, no per-frame allocation. A 192² R8/R16 field is ~36 k floats (~144 KB) —
trivial, mobile-portable. `[ExecuteAlways]` adds editor-time work only (gated to not run the live push
when not playing beyond the throttle).

## Consequences

- **The owner can design the coast by painting, and SEE it while editing** — the two problems that
  motivated the ADR are both solved (paintable + edit-mode-visible).
- **Painted == sailed/walked.** The sim reads the painted heights through the existing
  `GameServices.TidalTerrain` / `TidalExposure` seam; the render reads the SAME painted texture. One
  source, three consumers — P1 integrity holds by construction, no drift possible.
- **Zero new Core surface, zero new coupling.** Reuses `ITidalTerrain` / `WaterLevelAt` / `TidalExposure`
  as-is. The new types live in World (`PaintedHeightField`/`Map`/`TidalTerrain`) and Art (`WaterSurface`
  path) + a tools-editor window — all talk through Core (rule 4). No save change.
- **Non-breaking & opt-in.** The analytic `TidalTerrain` and the shipped St Peters look are untouched;
  adopting a painted map is an explicit per-region swap. Other regions/tests keep their current path.
- **Determinism preserved.** Authored data, pure sampling, tide recomputed-not-saved — guarded by the
  pure-`PaintedHeightField` EditMode tests.
- **Docs in the same change:** this ADR + a `design/water-rendering.md` §11 "painted seabed authoring"
  note, per the lead-architect DoD.

## Rejected alternatives

- **Keep terrain analytic-only (status quo).** Fails the owner's explicit ask: a non-dev cannot reshape
  the coast in C#, and the coast is invisible in edit mode. The analytic path stays as a *fallback*, not
  the only authoring route.
- **Two separate height fields — a "visual" painted texture for the shader and the analytic terrain for
  the sim.** The exact drift ADR 0009/0010 exist to prevent (ADR 0010 rejected alternative "separate
  height fields for rendering vs gameplay"). The painted map is **one** source both consume.
- **Bake the painted map into a *new* texture for the shader (a second bake).** Pointless and risky — the
  painted texture is already a GPU texture; feeding it directly avoids any chance of the bake diverging
  from what the sim decodes. (We still *decode* it to a `float[]` for the sim, but from the same bytes.)
- **A non-readable (GPU-only) height texture.** Then the sim can't sample it (the known gotcha) and
  painted ≠ sailed. The asset enforces `isReadable` and decodes to a CPU `float[]`.
- **Embedding the texture as a sub-object of the `.asset` (`AddObjectToAsset`).** Tried first, to dodge a
  stray un-LFS'd PNG / lost `.meta`, but it **drifted from the committed external-PNG seed and orphaned the
  PNG** (the export minted a `StPetersSeabed 1.asset` and left the committed `_HeightTex.png` dangling), and
  an embedded YAML-serialized texture is neither LFS-managed nor smart-mergeable. **Chosen instead:** an
  **external `.png` next to the `.asset`** — the `.png` in LFS + both `.meta`s committed (the project's
  standard binary-asset discipline guards the "lost meta / un-LFS'd PNG" hazard), the `.asset` Force-Text and
  smart-mergeable, and the paint tool overwrites the map+PNG **in place** (no unique-name duplicate). One
  storage model, tool and committed seed consistent.
- **Painting heights into the Tilemap (per-cell tile = elevation).** Coarse (1 m cells, discrete steps —
  the ADR-0012 blockiness), and forks the height authoring into the visual tile layer. A float texture is
  finer and is the same data shape the shader already samples.
- **A runtime/Play-only authoring tool.** The whole point is to SEE and design the coast **in edit mode**;
  the tool + `[ExecuteAlways]` preview must work without entering Play.
- **Auto-converting St Peters to the painted map on build.** Would silently change the shipped look and
  strand the analytic path. Adoption is an explicit owner step; the export seeds the canvas, nothing more.

## Open questions (later, for the owning lanes / the art pass)

- **R8 vs R16 precision.** 8-bit over a 10 m elevation span is ~0.04 m/step — fine for a greybox coast,
  but a gently shelving flat may band. The asset supports R16 (`TextureFormat.R16`) for finer authoring;
  default R8 for now, revisit if the owner sees elevation banding (orthogonal to the ADR-0012 *positional*
  texel grid).
- **Multiple painted maps per region / layered edits.** One map per region for now; if a region needs
  separable features (a painted bar over an analytic deep floor) a composite source is a later additive
  step.
- **Undo granularity in the paint tool.** Brush strokes register a single Undo per stroke; per-dab undo
  is a polish follow-up (PR2 candidate).
- **Whether St Peters ultimately ships painted or analytic.** An owner call after he paints — the export
  + the opt-in swap make either viable; the ADR keeps both paths alive.
