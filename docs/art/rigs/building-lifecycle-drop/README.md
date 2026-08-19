# Hidden Harbours — Building Lifecycle Pass

Construction phases and dereliction for the iso buildings. **This is not a rig.** It is a *pass* that
runs between a host rig's `build(b)` and its `paint()`: it takes the finished building's own face list
and hands back the same building at an earlier or a later point in its life.

Because it reads the **real faces**, every build config is covered with no per-config authoring — the
frame that goes up is the frame of the building the player actually picked (gable triangles, ells,
wings, dormers and all), and the ruin left behind sits on that same footprint. One pass, three host
rigs, every preset.

Conventions are the host rigs' (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**,
flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, 1px keyline, no AA.
Phase and decay sprites share the host's cell size, pivot and ground line, so **a phase advance is a
sprite swap** — nothing moves.

## Files

- **buildingLifecycleRig.js** → `globalThis.BuildingLifecycle` — the pass. Plain browser script, no
  deps. Load it any time before the first `render()`; order against the host rigs does not matter
  (they look it up at render time and no-op if it is absent).
- **hosts/** — the three bound host rigs, exactly as they ship in their own kits, each carrying the
  two-line hook: `wharfBuildingRig.js`, `houseIsoRig.js`, `shopfrontRig.js`. Included so the kit runs
  standalone; if you already have these files, you only need the hook.
- **_preview-lifecycle.png** — reference only: the full ladder for `WharfBuilding · netShed` on one
  ground line — 7 construction phases, then 4 states of dereliction and the burnt ruin.
- **WharfBuilding_netShed_frame_8dir.png** — the `frame` phase at **1:1 px, all 8 facings** (4 per
  row, order N NE E SE S SW W NW). Proof that framing rotates with the building: studs follow each
  wall's real bottom edge, so no facing needs a fix-up.

## States

**7 construction phases** (`PHASES`, in order):

| key | label | what it is |
| --- | --- | --- |
| `site` | Staked | pad cleared, corners staked, line strung, first drop of material |
| `foundation` | Foundation | piers and stem wall poured, sills bolted down |
| `frame` | Framed | studs, plates and corner posts up — open to the sky |
| `rafters` | Roof framed | rafters and ridge board on, collar ties in |
| `sheathed` | Sheathed | walls and roof deck sheathed, openings cut, staging up |
| `cladding` | Siding up | roof on and watertight, siding going up from the sill |
| `finished` | Finished | untouched — the host rig's own output |

**4 states of dereliction** (`DECAY`, after `sound` = kept up): `neglected` · `abandoned` ·
`collapsing` · `ruin`. Plus **`burnt: true`**, a modifier on any of them — chars every ramp, strips
the roof, leaves sooty stub timbers.

Decay **composes with phase**: an `abandoned` half-framed shell is a stalled build site, and that is a
real state a save file can be in. The useful ladder per config is 7 + 4 = **11 sprites** (22 with
burnt); the full cross product is available if you want it.

Decay also raises the host's own `weather` axis (`+0.55` at ruin), so paint chalks, shingles grey and
metal rusts through the host's existing ramps rather than a parallel set.

## API (`globalThis.BuildingLifecycle`)

- `active(opts)` → bool. True when `opts.phase` is set and not `finished`, or `opts.decay` is set and
  not `sound`, or `opts.burnt`. Use it to skip the pass entirely on finished buildings.
- `apply(faces, MATS, b, opts)` → `{ faces, b }`. Reads `opts.{phase, decay, burnt, seed}`, **adds its
  own material keys to `MATS` in place**, and returns the new face list plus a **cloned** build record
  for `post()` (weather bumped, decay-aware). Deterministic: the seed is derived from the building's
  span, eave height, phase and decay, so the same building always ruins the same way — pass
  `opts.seed` to reroll.
- `survey(faces, b)` → `{ x0,x1,y0,y1,z1, fH, eaveZ, ridgeZ, walls, roofs, lines, cx, cy, span }` —
  the read-back of the finished building: wall faces, roof planes, and `lines`, the outward-ordered
  bottom edge of every real wall (the true plan outline, wings and all), longest first. Exposed
  because it is useful on its own for site props, fencing and scaffold.
- Tables: `PHASES · DECAY · PHASE_LABEL · PHASE_NOTE · DECAY_LABEL · DECAY_NOTE`.
- `RAMPS` — the 13 ramps this pass introduces, dark → light, 6 steps each: `LUMBER · SHEATH · DECK ·
  CONC · DIRT · GRAVEL · TARP · WEED · MOSS · RUSTC · CHAR · LINE · PIPE`. Fresh sawn lumber,
  sheathing, roof deck, concrete, churned mud, gravel, blue tarp, mason's line, pipe staging, weed,
  moss, rust, char. **No new hues** — every ramp is a KTC ladder in the harbour family, so night and
  weather transform them with the rest of the building.
- `mulberry32(seed)` — the same PRNG the pass uses, for matching runtime overlays to a bake.

## Wiring a host rig (the whole patch)

In the rig's `render()`, immediately after `faces = build(b)`:

```js
const LC = root.BuildingLifecycle;                       // construction phase / dereliction pass
if (LC && LC.active(opts)) { const r = LC.apply(faces, MATS, b, opts); faces = r.faces; b = r.b; }
```

Then pass `{ phase, decay, burnt }` through `render(dir, opts)` alongside the rig's own axes. That is
the entire integration — no new render path, no second sheet format, no rig-side authoring.

**What a host rig must provide** to be bindable:

1. Faces as the shared record `{v, mat, b, db, uv, tex, flat}`; world units metres, z up, ground z=0,
   32 px = 1 m.
2. Wall faces tagged with a known wall material key — `body · lower · stone · cinder · brick · galv ·
   rust` — and roof planes tagged `roof`. That is how `survey()` finds the walls and roofs.
3. `render(dir, opts)` shaped like the others: `build → (pass) → paint → post`.

## Baking

- **Per stage, 8 facings**: `new ImageData(Host.render(d, {...opts, phase, decay, burnt}), Host.W, Host.H)`
  for `d` 0..7 → one sheet row. Pivot is the host's (`WharfBuilding.pivot = {x:600, y:780}`), identical
  across facings and across stages.
- **Ladder strip**: hold `dir`, walk `PHASES` then `DECAY.slice(1)` → 11 cells on one ground line.
- Trim to a **union bbox across the whole ladder**, not per cell, or the building will jump between
  phases. The demo page's download buttons do exactly this.
- Suggested path: `Art/Sprites/Buildings/<Host>_<preset>_lifecycle.png`.

## Coverage and limits

- Bound into **wharfBuildingRig · houseIsoRig · shopfrontRig** — the three rigs that share the
  wall/roof/opening face vocabulary.
- **Shop Building** (cutaway interior rig) and **Shipyard** (yard-parts rig) take a different
  signature and are the next pass, not this one.
- The pass does not touch anchors: smoke, lit windows and sign lettering stay host-side runtime
  overlays. A ruin has no lit windows — gate your overlays on decay.
- Site props (stakes, staging, material drops, debris) are baked into the cell, not separately
  addressable.
- No interior at any phase. `frame` and `rafters` are see-through by geometry, not by a cutaway.

## Demo page (in the main project, not this zip)

`Building Lifecycle Iso.dc.html` — rig and preset chips, the phase and dereliction ladders side by
side, turntable, the 8-dir sheet / 11-stage strip / single-cell downloads, and the ramp readout.
