# ADR 0012 — Shoreline rendering: smooth the height-map shore (keep the real moving tide); Rule-Tiles only for permanent non-tidal coast

- **Status:** **Accepted** — lead-architect. Records the **decision + a prototype spec** for how the
  water meets the land. **Docs only:** this ADR ships **no** shader/scene/Core/material change. It is a
  design exploration the owner steers, and a one-shot recipe a follow-up agent builds in the art pass.
- **Date:** 2026-06-26
- **Decision owner:** lead-architect (the shoreline is where the URP water *render* meets the **P1
  tide-as-gameplay** integrity rule — a cross-cutting/architectural call: `agents/coordination.md` §1.1
  "Water/fog/lighting" seam, §8; CLAUDE.md rule 4). art-pipeline owns the *look* (palette, foam/edge
  textures); the two tune together.
- **Flagged from:** the owner's report that the shader-driven shoreline renders **BLOCKY/jarring** (big
  rectangular steps where water meets sand), that the game currently carries **two inconsistent
  shoreline treatments** (one smooth-but-no-tide, one tidal-but-blocky), and a reference video using a
  **Rule-Tile shoreline + an animated tile** to wash waves in/out (smooth, hand-authored). The owner
  asks whether the game needs that full water-edge tile set.
- **Related:** `0010-water-rendering.md` (the layered URP water shader this refines — esp. decision (4)
  "static type-edges = tiles, the live wet edge = shader", and the depth≈0 foam band), its addenda (the
  shipped swash + cohesion + living-foam art-pass tunables), `design/water-rendering.md` (the layer
  recipe + §5.6 beach-swash + §10 painted slots), `0009-tidal-exposure-and-region-display-name-seams.md`
  (`IEnvironmentService.WaterLevelAt` + `Core.TidalExposure` + `Core.ITidalTerrain.ElevationAt` — the one
  height-map rule render and sim share), `0011-committed-hand-authored-scenes.md` (Rule-Tiles / painted
  scenes vs builder logic), `design/time-tides-weather.md` §3.5 (the water-depth rule, OQ1 tide→visual),
  `design/world-and-regions.md` §6.0/§7 (the St Peters sandbar), `vision-and-pillars.md` §5.8 (the
  falling-tide opening), P1 ("The Sea Has Moods" — what you SEE == what you SAIL/WALK).
- **Implementation pointers (read-only; the files diagnosed below):**
  `Assets/_Project/Art/Shaders/HiddenHarboursWater.shader`,
  `Assets/_Project/Code/Art/WaterSurface.cs`, `Assets/_Project/Art/Materials/Water.mat`,
  `Assets/_Project/Code/Environment/TidalFlatVisual.cs`, `Assets/_Project/Code/Art/TideShoreline.cs`,
  `Assets/_Project/Code/App/Editor/StPetersBuilder.cs` (the St Peters wiring),
  `Assets/_Project/Code/App/Editor/GreyboxBuilder.cs` / `GreywickBuilder.cs` (the static-shore regions).

## Context

The shoreline is the single most P1-load-bearing pixel in the game. The St Peters opening
(`vision-and-pillars.md` §5.8) turns on a **falling tide baring a sandbar** the player walks across, and
on a boat-crossing that **gates on real water depth**. ADR 0010 already locked the architecture that
keeps render and sim honest: **one height map, three consumers** (water render, on-foot walkability,
boat-cross), all reading `depth = WaterLevelAt(t) − terrainHeight`. The waterline the player *sees* and
the waterline the physics *enforces* are the **same line by construction** — that is the **P1 integrity
rule**, and it is non-negotiable for any shoreline approach.

The owner reports the shader shoreline reads **blocky** — big rectangular steps where water meets sand —
and that the game shows **two inconsistent shorelines**. Both reports are correct. This ADR diagnoses
the root cause precisely, then weighs three ways forward against the **tide-fidelity** constraint.

### Diagnosis 1 — WHY it reads as big blocky squares (root cause, with the numbers)

The blockiness is **not** the pixel-art pixelization (`_PixelsPerUnit`), and **not** the foam math. It
is the **coarse baked height-map texel grid**, amplified by the **posterized depth bands**. The chain,
file by file:

1. **The seabed depth the shader reads is a baked, low-res texture — and the bake is too coarse.**
   `WaterSurface.cs` bakes `ITidalTerrain.ElevationAt` into a height texture once on enable
   (`BakeHeightMapIfNeeded`, `BakeTerrainElevation`). Its resolution is `_heightResolution = 96`
   (`WaterSurface.cs:115`), and on St Peters that texture is stretched over a world rectangle of
   `_heightWorldSize = 160 × 120 m` (`StPetersBuilder.cs:202`, via `ConfigureWaterSurface`).
   **→ one height texel covers `160/96 ≈ 1.67 m` across × `120/96 = 1.25 m` down.**

2. **The shader derives the entire shoreline from that texel.** `SeabedElevation(worldXY)`
   (`HiddenHarboursWater.shader:597`) samples `_HeightTex`, and the fragment computes
   `depth = _WaterLevel − elevation` (`shader:644`) then `clip(depth + 1e-4)` (`shader:647`) — the hard
   land/water cut — and the foam band `1 − smoothstep(0, _FoamWidth, foamDepth)` (`shader:759`). **Every
   one of these is a function of that ~1.67 m texel value.** So the water-edge cut and the foam band land
   on **texel boundaries ~1.5–1.7 m apart** → the waterline is a staircase of ~1.5 m rectangular steps.
   That staircase *is* the "big blocky squares."

3. **The bake samples elevation at texel CENTERS, so a near-flat crest snaps to whole texels at once.**
   The St Peters bar crest sits at `SandbarCrestElevation = 1.6 m`, with the tide at mean `0` swinging
   `±3.5 m` (`StPetersBuilder.cs:79,80,95`) — so the bar covers near high water and bares as the level
   falls past `1.6 m`. Across the bar the elevation is nearly uniform, so as the tide crosses ~1.6 m the
   depth crosses zero across a whole **band of texels simultaneously** — the zero-crossing
   isocontour has nothing finer than 1.67 m to follow, so it reads as a chunky rectangular front rather
   than a smooth line. (The height texture's `FilterMode.Bilinear` (`WaterSurface.cs:290`) *does*
   interpolate between texels, which softens it a little — but bilinear over a 1.67 m grid still yields
   visible ~1.5 m facets, and on a near-flat crest the interpolation gradient is tiny, so the step
   survives.)

4. **The posterized depth bands add visible terraces on top.** `_DepthBands = 5` (`Water.mat:71`)
   quantizes the shallow→deep colour ramp into 5 flat steps over `_ShallowDepth 0.15 → _DeepDepth 3 m`
   (`shader:651`: `dt = floor(dt * _DepthBands + 0.5) / _DepthBands`). On a gently shelving bottom those
   5 steps draw as **depth contour terraces**; combined with the coarse texel grid (which already facets
   the contours), the shallows read as nested rectangular plateaus — exactly the "blocky/jarring"
   character. The depth-tint **bands** and the height-map **texels** are two independent quantizations
   stacking into one staircase.

> **One-line root cause:** the shoreline is drawn from a **96² height texel grid stretched over 160×120 m
> (~1.67 m/texel)** and then **posterized into 5 depth bands** — both far coarser than the eye expects a
> waterline to be, so the wet edge and the shallows read as big rectangular steps. `WaterSurface.cs:115`
> (the bake resolution) + `Water.mat:71` (`_DepthBands: 5`) are the two knobs that cause it. The fix does
> **not** require new art — it requires a **finer bake + a smooth (non-posterized) near-shore gradient**.

> **Note — `_PixelsPerUnit: 12` is a red herring for the blockiness.** The material runs `Pixelize` at
> PPU 12 (`Water.mat:99`), so world coords snap to `1/12 ≈ 0.083 m` before sampling — *finer* than the
> 1.67 m texel, so pixelize is **not** the limiter (the bake is). PPU 12 (vs canon §2's PPU=32) is a
> deliberate art choice to make the *surface noise/foam* read chunkier; it is orthogonal to the shoreline
> staircase and the prototype below leaves it to the owner's taste. Raising the bake resolution is what
> moves the needle.

### Diagnosis 2 — the TWO (really THREE) inconsistent shoreline treatments

The repo currently carries **three** shoreline mechanisms; **two are live in scenes and they disagree**,
and a **third is orphaned**:

| # | Treatment | Where it's used | Tide-fidelity | Look |
|---|---|---|---|---|
| **A** | **Real tide gate** — `TidalTerrain` (authored elevation) + `WaterSurface` (96² bake over 160×120 m) feeding the shader's depth/foam, **plus** `TidalFlatVisual` (a 2 m-cell colour grid overlay) | **St Peters only** (`StPetersBuilder.cs:201,300`) | **TRUE** — the waterline moves with the deterministic tide; the bar bares; the crossing gates on real depth | **Blocky** (Diagnosis 1) |
| **B** | **Static painted/collider shore** — a fixed `EdgeCollider2D` shoreline (`ShoreEdge`, `Shoreline`) + a plain sea sprite; **no** `TidalTerrain`, **no** `WaterSurface`, **no** `TidalFlatVisual` | **Coddle Cove** (`GreyboxBuilder.cs:264`) & **Greywick** (`GreywickBuilder.cs:453`) | **NONE** — the shore is a fixed wall the boat bumps; the waterline never moves | Smooth (it's just a static edge) |
| **C** | **`TideShoreline.cs`** — a smooth transform-slide of a water plane by tide height (no height map, no posterize) | **Nowhere** — referenced only by its own unit test (`ArtRenderingTests.cs`); in **no** builder/scene | (would be partial — slides a plane, no per-position depth) | Smooth, but unused |

So the owner's "two inconsistent shorelines" is **A (tidal-but-blocky) on St Peters vs B
(smooth-but-no-tide) in the cove/Greywick**. That inconsistency is *by design for now* (St Peters is the
only tide-gated region in the greybox; the cove/Greywick deliberately use Greywick's "deliberately
deep/dredged harbour" exception, canon §5.8), but it means there is **no single shoreline look** across
the game yet. `TideShoreline.cs` (C) is dead weight — a leftover smooth approach superseded by the
height-map shader; flagged for cleanup below.

### The hard constraint (front and centre)

**The tide is gameplay here.** Any shoreline approach must preserve a **REAL, continuously-moving
waterline driven by the deterministic tide sim** — the sandbar must actually **bare** at low water (clam
digging) and the boat crossing must **gate on real water depth** (P1). A *faked* shoreline (a fixed-
position animated loop that merely *looks* like surf) **breaks** P1: it cannot bare the bar or gate the
crossing, and it forks the shoreline truth away from the height map — the exact drift ADR 0009/0010 exist
to prevent. This constraint is the spine of the decision below.

## Decision

**Smooth the existing height-map shader shore (approach A), keeping the real moving tide, as the primary
fix.** Reserve hand-authored Rule-Tiles for **permanent, non-tidal** coast only (approach B's role, per
ADR 0010 decision (4)). Adopt a hand-painted **animated foam-edge sprite set that rides the shader's real
depth≈0 line** (approach C-hybrid) **only if** the smoothed shader isn't crisp enough after the owner sees
it. **Do not** build the full Rule-Tile water-edge connectivity set as the shoreline — it cannot carry the
tide.

This ADR ships **only the decision + the prototype spec**; the build is the art pass (M1 VS-24 → M2/M3),
per CLAUDE.md rule 8.

### The three approaches, scored

Scoring axes: **visual quality**, **tide-fidelity** (does the waterline really move with the sim + bare
the sandbar?), **art cost** (tiles/sprites), **engineering cost**, **P1-integrity**.

---

#### (A) SMOOTHED SHADER + height-map — *keep the architecture, fix the blockiness* — **RECOMMENDED FIRST**

Keep ADR 0010's one-height-map architecture; remove the two quantizations that cause the staircase.
Concretely: **raise the bake resolution**, **replace the hard `_DepthBands` posterize with a smooth
near-shore gradient**, lean on the **already-shipped beach-swash** for the in/out wash, and (optionally)
sharpen the near-shore detail with a higher-res or analytic distance field. The full parameter recipe is
the **Prototype Spec** below.

| Axis | Verdict |
|---|---|
| **Visual quality** | **Good→very good.** A finer bake + smooth gradient gives a clean, continuous wet edge; the swash already washes it in/out; foam already hugs it. Not hand-authored-crisp, but the blockiness is the bug, and this removes it. |
| **Tide-fidelity** | **TRUE — unchanged.** Still reads the deterministic `WaterLevelAt(t)` + the same height map the walkability/boat-cross gate uses. The bar bares; the crossing gates on real depth. **Zero risk to P1.** |
| **Art cost** | **ZERO new art.** It is parameter changes (bake res, `_DepthBands`, swash). The owner's painted slots already work on top. |
| **Engineering cost** | **Lowest.** A bake-resolution bump (one serialized field / Inspector value), a material value (`_DepthBands → 0`), optional small shader tweaks (analytic SDF or a near-shore smooth term). No new system, no Core change, no save change. |
| **P1-integrity** | **Perfect.** One height map, three consumers; the visible line *is* the playable line. |

> **The cheap fix that keeps the tide.** This is the steer: try A first.

---

#### (B) PURE RULE-TILE + animated tile — *the video's approach* — **decoration only**

Author the whole water edge as a **Rule-Tile / autotile** set (a tile per shore-to-water connectivity
case) plus an **animated tile** that flip-books surf washing in/out. Smooth and hand-authored — and the
honest problem: **the animated tile FAKES the tide.** It is a **fixed-position loop**; it cannot move with
`WaterLevelAt(t)`, cannot bare the sandbar, and cannot gate the boat crossing. It would **break P1's
tide-as-gameplay** wherever the tide actually matters.

To make it tide-true you would have to **re-stamp the shoreline tiles by tide threshold each tick** —
swapping rows of tiles wet↔dry as the level crosses each tile's elevation. That is **clunky and discrete**
(the waterline jumps a whole tile, ~1 m, at a time — *blockier* than approach A's bug, not smoother), it
is **per-frame authoring churn**, and it **forks the shoreline truth away from the height map** — the
precise anti-pattern ADR 0010 decision (4) and its "Rejected alternatives" already reject.

**Full water-edge tile count it would require:** a complete 2-terrain (water/land) **blob/47-tile**
Rule-Tile set is the standard for smooth arbitrary coastlines — **47 tiles** for the connectivity cases
(the "47-blob" set; a reduced corner-matched set is ~15–16 tiles but reads chunkier on diagonals and
curves). Add the **animated surf**: a wash-in/out flip-book of **~4–8 frames per edge orientation**.
Authored as one animated overlay strip that's masked to the edge, that's **~6–8 more frames**; authored
per-orientation it balloons toward **47 × ~6 ≈ 280 frames**. Plus inner/outer **corner** variants and any
rock/wharf-specific edges. So: **~47 base tiles + ~6–8 animated wash frames at minimum**, realistically
**60–90 hand-authored tiles/frames** for a polished, varied coast — and after all that, **it still cannot
carry the tide.**

| Axis | Verdict |
|---|---|
| **Visual quality** | **Excellent** (hand-authored, the reference look) — *for a static coast.* |
| **Tide-fidelity** | **NONE** as an animated loop (fakes it). **Discrete/clunky** if re-engineered to threshold-swap tiles — and that re-engineering breaks the one-height-map rule. **Fails the hard constraint.** |
| **Art cost** | **Highest — ~47 base tiles + ~6–8 animated wash frames (60–90 tiles/frames for a varied coast).** |
| **Engineering cost** | Low for a static tilemap; **high + architecturally wrong** for the tide-threshold tile-swap. |
| **P1-integrity** | **Violates it** where the tide is gameplay. Acceptable **only** for permanent non-tidal coast as decoration (e.g. Greywick's dredged harbour wall, a cliff base that never floods). |

> **Verdict:** the game does **not** need the full water-edge tile set as its shoreline. Rule-Tiles stay
> in their ADR-0010 role — **static terrain-type edges only** (grass↔sand↔rock, a fixed harbour wall) —
> never the live wet edge.

---

#### (C) HYBRID — *shader computes the real moving waterline; a hand-painted ANIMATED foam-edge sprite rides it* — **fallback if A isn't crisp enough**

Keep approach A's real, moving depth≈0 waterline (the shader still owns the tide truth), and add a **small
set of hand-painted, animated foam-edge sprites** that ride **along** that moving line for authored
crispness — **NOT** the full 47-tile connectivity set. The shader already locates the wet edge every tide;
the painted foam is *dressing* placed on it. ADR 0010 §10 already ships **`_FoamTex`** (a painted foam
slot masked to the depth≈0 band) — so the *plumbing exists*; the hybrid is mostly **art** (paint a crisp
animated foam strip + tune `_FoamTexStrength`), with at most a small shader tweak to march the foam sprite
**along** the edge tangent rather than just churn in place.

| Axis | Verdict |
|---|---|
| **Visual quality** | **Very good→excellent.** Authored foam crispness on a smooth, real edge — the reference look's *appeal* without its tide lie. |
| **Tide-fidelity** | **TRUE.** The waterline is still the shader's deterministic depth≈0 line; the painted foam cannot move it (masked to the band — a painted tile never changes `depth`/`clip`, per ADR 0010 §10). The bar still bares; the crossing still gates. **P1 holds.** |
| **Art cost** | **Low — a handful of seamless foam-edge sprites + a short wash flip-book (~4–8 frames), NOT 47 tiles.** One animated foam strip masked to the band covers all orientations because the shader, not the tile grid, places it. |
| **Engineering cost** | **Low.** `_FoamTex` already exists; add (optionally) an edge-tangent march. No Core/save change. |
| **P1-integrity** | **Perfect** — same one-height-map truth as A. |

> **Verdict:** the right way to get the *hand-authored look* the owner liked **without** breaking the
> tide. Pursue if A's procedural foam isn't crisp enough after the owner sees it.

---

### Recommendation (the brief)

1. **Do (A) first — the cheap fix that keeps the tide.** Raise the bake resolution and drop the depth-band
   posterize near shore (prototype spec below). Zero new art, lowest risk, **P1 untouched**. This removes
   the blockiness root cause. **Owner reviews the result.**
2. **If A still isn't crisp enough, add (C) — the hybrid.** Paint a small animated foam-edge sprite set
   (~4–8 frames, not 47 tiles) that rides the shader's real depth≈0 line via the existing `_FoamTex` slot.
   Real tide + authored crispness, low art cost.
3. **Use (B) — Rule-Tiles + animated tile — ONLY for permanent, non-tidal coast as decoration.** A dredged
   harbour wall, a cliff base that never floods. **Never** as the tide-gated shoreline — it cannot carry
   the moving waterline, and threshold-swapping tiles to fake it is clunky *and* forks the height-map
   truth (ADR 0010 decision (4)).
4. **Unify the live shoreline on the shader path.** As more regions become tide-relevant, give them the
   approach-A treatment (a `TidalTerrain` + `WaterSurface`) rather than spreading approach B's static
   edge. The cove/Greywick may *stay* static (Greywick is canon-deep; the cove is the M1 stand-in) — but
   the **default for any tide-gated coast is the shader**, so the game converges on one shoreline look.
5. **Retire the orphan.** `TideShoreline.cs` is used in no scene (only its own test). It is a third,
   superseded smooth approach; remove it (and its test) in a separate small cleanup PR so it doesn't get
   mistaken for a live path. *(Out of scope for this docs-only ADR — flagged, not done.)*

## Prototype spec — the smoothed-shader shore (approach A), buildable in one shot

A follow-up agent (art-pipeline + lead-architect, the VS-24 water pass) builds this to compare against the
tile look. **All changes are parameter/value tweaks + at most a tiny near-shore shader term; no new system,
no Core change, no save change, P1 untouched.** Every value stays a tunable (CLAUDE.md rule 6).

**1. Raise the baked height-map resolution (the headline fix).**
- `WaterSurface._heightResolution` (`WaterSurface.cs:115`, the serialized field, **currently 96**) → **192**
  (and raise its `[Range(16, 256)]` cap to allow up to **256** if 192 still facets on the bar). At 192 over
  160×120 m the texel is `160/192 ≈ 0.83 m` (half the current 1.67 m); at 256 it is `0.625 m`. Pick the
  smallest resolution where the wet edge stops reading as steps on the St Peters bar — **start at 192**.
- Cost: a one-time bake of a 192² (36 864-texel) R8 texture on enable — trivial CPU/VRAM, well within the
  rule-7 budget (the bake already runs once; this just makes the texture bigger). The `FilterMode.Bilinear`
  (`WaterSurface.cs:290`) interpolation then has a finer grid to smooth across.

**2. Turn OFF the depth-band posterize near shore (remove the terraces).**
- `Water.mat _DepthBands` (`Water.mat:71`, **currently 5**) → **0** (the shader treats `< 1` as "smooth",
  `shader:651`). This makes the shallow→deep tint a **continuous** gradient — no contour terraces.
- *If the owner wants to keep some banded pixel-art feel in deep water* but a smooth wet edge: leave
  `_DepthBands` at a low value (e.g. 3) for the deep tint, **but** that does not re-introduce the staircase
  as long as the bake is fine (step 1) — the bands quantize *colour*, the bake quantizes *position*. The
  staircase needs **both** to be coarse; fixing the bake alone already removes most of it. **Default the
  prototype to `_DepthBands = 0`** and let the owner dial back up if they miss the banding.

**3. Lean on the already-shipped beach swash for the in/out wash (the reference video's appeal).**
- The swash is **already ON** (`Water.mat`): `_SwashAmplitude = 1`, `_SwashSpeed = 0.08`, `_SwashScale =
  0.2`. It continuously advances/recedes the **foam fringe** (confined to the depth≈0 band, never touching
  the gameplay `depth` — `shader:755-757`, `design/water-rendering.md` §5.6). This is the "waves wash in
  and out" the owner saw in the tile video — already delivered, tide-safe. **Tune** `_SwashSpeed` up
  slightly (e.g. 0.08 → 0.12–0.18) if the wash reads too slow against the finer edge; keep `_SwashAmplitude`
  ≤ the foam reach so it stays band-confined (the `SwashBandGate` invariant, `WaterSurface.cs:717`).

**4. Tighten the foam fringe so the crisp edge reads.**
- `Water.mat _FoamWidth` (**currently 0.45 m**) and `_FoamSoftness` (**0.18 m**): with the finer bake, a
  slightly **narrower** `_FoamWidth` (e.g. 0.30–0.40) + a **small** `_FoamSoftness` (0.08–0.12) gives a
  crisper wet line; widen them back if the foam reads too thin. (`shader:759` builds the band from these.)

**5. (Optional, only if texels still facet a near-flat crest) — an analytic near-shore distance term.**
- The remaining facet risk is on the **bar crest**, where the elevation is near-flat, so even a 0.83 m
  texel grid quantizes a nearly-horizontal zero-crossing. Two options, cheapest first:
  - **(5a) Just go to 256** (step 1) — usually enough; try this before any shader change.
  - **(5b) Smooth the depth read near zero in the shader** — replace the single point-sample in
    `SeabedElevation` (`shader:597`) with a small **3×3 bilinear-weighted tap** of `_HeightTex` (a cheap
    blur) *only* feeding the foam-band/edge term, so the wet line follows a smoothed elevation while the
    gameplay `depth` (which the sim reads via `TidalExposure`, **not** this shader) is unaffected. This is a
    visual-only smoothing of the *rendered* edge — it **must not** change what `ITidalTerrain.ElevationAt`
    returns (the sim's truth), so it lives **only** in the shader's foam/edge path, never in
    `WaterSurface`'s bake of the gameplay elevation. Keep it behind a tunable (`_EdgeSmooth`, default 0 =
    current behaviour) so the owner opts in. **Skip 5b unless 256-res still steps** — prefer the no-shader-
    change path.

**6. Leave `_PixelsPerUnit` to taste.** It is **not** the blockiness cause (`1/12 m ≪ texel`); it is the
   surface-noise/foam chunkiness knob. The owner art-directs it independently (canon §2 PPU=32 is the scale
   standard for *sprites*; the water surface noise grid is an art choice). **Do not** change it as part of
   this shoreline fix.

**Acceptance for the prototype:** open `StPeters.unity`, press Play, scrub the tide (the `DevFastTide`
object / Tide Scrubber). The wet edge should sweep **smoothly** across the bar as a continuous line (no
~1.5 m rectangular steps), the shallows should read as a **continuous** shallow→deep gradient (no contour
terraces), the swash should wash in/out, and — **the invariant** — the bar must still **bare** exactly when
`TidalFlatVisual` / the walkability gate say it does (the visible edge == the playable edge). Compare that
against the Rule-Tile look the owner referenced and let the owner pick crispness vs cost (→ approach C if
the procedural foam isn't crisp enough).

### Determinism & save (the invariant guarded)

Nothing here touches determinism or saves. The smoothed shore is still a **pure function** of the
deterministic `WaterLevelAt(t)` (recomputed from `(worldSeed, gameTime)`, never saved — CLAUDE.md rule 5)
+ the authored, read-only height map. The bake-resolution bump changes texture size, not the *values*
sampled; `_DepthBands → 0` and the swash/foam tweaks are **visual-only** (`col.rgb`/foam dressing), driving
no sim and entering no save (ADR 0008). The optional 5b edge-smooth is explicitly confined to the *render*
edge and **must not** alter the gameplay elevation the sim reads — the P1 integrity rule holds by
construction. The painted foam-edge of approach C is a read-only authored texture masked to the band
(ADR 0010 §10) — it cannot move the waterline.

### Performance posture (planned, validated at build time)

Targets the 60fps desktop budget, mobile-portable (ADR 0005). The only cost delta is a **larger one-time
bake** (192²/256² R8, still tiny) and, *if* 5b is taken, **8 extra texture taps** on the foam-edge path
(within the rule-7 fixed-samples-per-pixel budget; gated by `_EdgeSmooth` so it's free when off). No
per-frame CPU allocation is added — `WaterSurface` still pushes uniforms on the throttled tick. The
runtime-shader-vs-M3-bake fork (ADR 0010 §7) is unchanged.

## Consequences

- **The blockiness is fixed at its root** (the coarse bake + posterize), **with zero new art**, by an
  art-pass parameter change — and the **real moving tide is fully preserved** (P1 untouched).
- **One shoreline truth, one look to converge on.** The decision names the shader path (A, optionally
  C) as the **default for any tide-gated coast**, so the game stops carrying two divergent live shorelines
  as it grows; Rule-Tiles (B) are pinned to permanent non-tidal coast only (ADR 0010 decision (4) holds).
- **The owner's "do we need the full water-edge tile set?" is answered: no** — ~47 tiles + animated wash
  that still can't carry the tide is the wrong tool for a tide-gated coast. The hand-authored *look* is
  available far cheaper via approach C (a few foam-edge sprites riding the real edge).
- **Zero new Core surface, zero coupling, zero save change.** Everything reuses ADR 0009/0010 seams
  (`WaterLevelAt`, `TidalExposure`, `ITidalTerrain`, the `_FoamTex` slot). This ADR adds only a doc.
- **A clear next step + a one-shot prototype spec** for the VS-24 water pass, with exact parameters
  (`_heightResolution 96→192/256`, `_DepthBands 5→0`, swash/foam tunables, optional `_EdgeSmooth`).
- **Two cleanup follow-ups flagged (not done here):** (a) retire the orphaned `TideShoreline.cs` + its
  test; (b) when the prototype lands, fold the result into `design/water-rendering.md` (a §5.11 "shoreline
  smoothing" note) and add an addendum to ADR 0010 if the shader gains `_EdgeSmooth`.

## Rejected alternatives

- **Build the full 47-tile Rule-Tile water-edge set as the shoreline (approach B).** It cannot carry the
  deterministic tide; an animated tile fakes the wash (breaks P1's bar-baring / crossing-gating), and
  threshold-swapping tiles to make it tide-true is clunky (≥1 m discrete jumps — blockier than the bug),
  per-frame authoring churn, and forks the shoreline truth from the height map (ADR 0010 decision (4)).
  Highest art cost (~60–90 tiles/frames) for a worse-than-the-bug tide. Kept only for static, non-tidal
  decoration.
- **Leave the blockiness and just paint over it with a tile edge.** Doesn't address the root cause (the
  coarse bake + posterize), and a painted tile edge over a tidal coast re-introduces the tide-fidelity
  problem of approach B.
- **Lower-res-but-smooth: drop `_DepthBands` alone, leave the 96² bake.** Removes the colour terraces but
  **not** the positional staircase — the wet edge still steps on the ~1.67 m texel grid. The bake
  resolution is the dominant cause; both must be addressed, bake first.
- **Bake the gameplay elevation at higher res too / change `ITidalTerrain.ElevationAt`.** Not needed and
  risky: the *gameplay* reads `ElevationAt` analytically through `TidalExposure` (per-position, not the
  texture), so the sim is already as precise as its math. Only the **render** samples the coarse texture —
  so only the **render** bake needs raising. The optional 5b smoothing is confined to the render edge for
  exactly this reason (never alter the sim's truth).
- **Raise `_PixelsPerUnit` to "fix" the blockiness.** Misdiagnoses it — pixelize at PPU 12 is already finer
  than the texel; the bake is the limiter. Changing PPU only alters surface-noise chunkiness, an
  orthogonal art choice.
- **Build any of this now as code.** Scope creep (rule 8) — the shader is the deferred VS-24 → M2/M3 art
  pass. This ADR records the decision + prototype spec; the build happens in the art pass, owner-steered.

## Open questions (later, for the art pass / owning lanes)

- **Exact prototype resolution (192 vs 256) and whether 5b is needed** — a profiled/eyeballed call in the
  art pass on the real St Peters bar. Start at 192 + `_DepthBands 0`; escalate only if it still facets.
- **Whether the cove/Greywick ever convert to the shader shoreline** — ~~a world-content/owner call. They may
  stay static (Greywick is canon-deep; the cove is the M1 stand-in). The *default* for new tide-gated coast
  is the shader regardless.~~ **RESOLVED (world-content): CONVERTED.** Both regions now run the approach-A model
  (recommendation 4): each builder authors a `World.RectTidalTerrain` (a rectangular-plateau analytic seabed —
  land strips/dock spurs over a deep floor, the rectangular twin of St Peters' `TidalTerrain`) that registers
  into `GameServices.TidalTerrain`, plus a `WaterSurface`-driven Sea plane (Water.mat, 192² bake, the ADR 0017
  weather palette) baking that terrain — one height, three consumers, in every region. The old treatment B is
  retired as a *look*: the cove/Greywick `ShoreEdge`/`Shoreline` EdgeCollider2D fences REMAIN as gameplay
  bounds (the boat wall / cozy player bounds), they just no longer stand in for a shoreline. Canon holds:
  Greywick's floor is DREDGED (−6 m, steep quay falloff → a modest waterline sweep against the quay), while
  the cove gets a gentle south beach the tide visibly sweeps. NOTE the on-foot walkability gate goes LIVE in
  both regions the moment a terrain registers — the terrains are authored so all town land / wharf decks /
  dock planks sit above the highest water. The Terrain Paint Tool's Adopt step now also swaps out a
  `RectTidalTerrain`, so the owner can hand-paint over either region exactly as on St Peters (ADR 0014).
  Alex must re-run **Build Greybox Scene** and **Build Greywick Scene** to see it (builder-authored).
- **Approach-C foam-edge sprite spec** (dims, frame count, seamlessness, the edge-tangent march) — an
  art-pipeline spec coordinated with the `_FoamTex` slot (ADR 0010 §10) **if** A isn't crisp enough.
- **`TidalFlatVisual` overlay's future** — ~~once the shader shore is smooth on St Peters, the 2 m-cell
  colour grid (the greybox tide-reveal) may become redundant with the shader's own depth tint; whether to
  keep it as a gameplay-clarity overlay or retire it is a follow-up once the smoothed shader is seen
  (it currently double-draws the bar alongside the shader).~~ **RESOLVED (gameplay-systems):** retired. The
  grid was the owner-reported blockiness — it stamped 2 m flat blue/teal cells ON TOP of the already-live
  layered shader (same tide, same `ITidalTerrain`, same flat), hiding the smooth shore. The St Peters builder
  no longer creates it; `TidalFlatVisual.cs` + `TidalFlatVisualTests.cs` are deleted; the Sea plane's
  sorting was raised (−10 → −5, above the authored sand/channel ground, below the clam holes/player) so the
  shader's `clip(depth)` does the wet/dry reveal directly; and the bake resolution was raised 96 → 192
  (§A step 1). P1 unchanged — the sim still gates on `WaterLevelAt`/`ElevationAt` directly, not the visual.
  Alex must re-run **Build St Peters Scene** for the grid to disappear (it is builder-placed).
