# ADR 0021 — Bake the art director's rigs IN-ENGINE: embed V8 in the Unity EDITOR, run his `.js` source unmodified, and go to 32 directions

- **Status:** **Accepted — owner signed off 2026-07-19** on (a) vendoring the V8 engine as an
  editor-only dependency and (b) committing to **32-direction boat movement**. Docs + an inert
  measurement harness; this ADR ships no tool. The baker itself (Arc D) is separate work, not yet
  built. Records the decision that rig sheets stop being hand-exported from a browser and become an
  **editor operation**: place a rig in a scene, tweak it, press BAKE.
- **Date:** 2026-07-19
- **Decision owner:** lead-architect (a new editor-only third-party dependency + a new authoring seam
  that crosses art-pipeline, tools-editor and Boats — `agents/coordination.md` §1.1, CLAUDE.md rule 4).
  **tools-editor** owns the baker window and the bake pipeline; **art-pipeline** owns the import
  settings, sheet layout and the look; **gameplay-systems** owns the 32-direction heading consumers;
  **lead-architect** owns the dependency, the asmdef/platform fencing, and the Def contract.
- **Serves:** **P3 "Living Working Coast"** and the owner's standing want to **author content himself**
  (level-design tooling plan): a rig you can place, nudge and re-bake in the editor is the same
  "paint = sail" move ADR 0014 made for the seabed, applied to sprites. **P2 "Dory to Dynasty"** —
  directly, now: *"some of the boats are huge, you notice when it's only 8 directions."* A 14 m Cape
  Islander snapping between 45° facings reads as cheap in exactly the way a growing fleet must not.
- **Related:** `0006-boat-art-pipeline.md` (the sheet/pivot/facing conventions this bake must emit —
  and which 32 directions changes), `0003-data-driven-content.md` (rule 2 — the bake writes Def
  assets), `0014-painted-seabed-height-authoring.md` (the precedent: an **editor tool** that makes
  authored data the single truth), `0019-hand-authored-scenes-and-refresh.md` (non-destructive
  Refresh), `0005-pc-first-target.md` (editor-only cost, zero player cost).

## Context

Every iso kit in the repo — dory, punt, console skiff, sport skiff, Cape Islander, the character rig —
was produced by the art director as a **parametric JavaScript software renderer**. Three of those rigs
are in the repo and are the ground truth for this decision:

- `docs/art/punt-iso-rig/puntIsoRig.js`
- `docs/art/skiff-fleet-rigs/consoleIsoRig.js`
- `docs/art/skiff-fleet-rigs/sportSkiffIsoRig.js`

Each is a self-contained IIFE that installs a global (`globalThis.PuntIso`, …) exposing
`render(dir, opts)` → an RGBA byte buffer, plus `W, H, PX, pivot, order, ROCK, rock(i)`, per-rig
overlay renderers (`renderMotor`), and **measured anchors** (`motorMount`, `tubMounts`, `helmSeat`,
`tillerGrip`). Internally they are z-buffered triangle rasterisers with flat-facet shading, ordered
dither and a 1px keyline — real software rendering, ~470 quads per cell.

Today the path from rig to game is: the art director opens a browser turntable, exports PNG sheets by
hand, and hands over a folder. That has three costs that are now biting:

1. **It does not scale.** More rigs are coming (houses, buildings, other types). Every tweak is a
   round-trip through a human and a browser.
2. **The owner cannot author.** He can place a prefab; he cannot place a rig, nudge the elevation, and
   see the sheet re-bake.
3. **Hand-export loses the metadata.** The rigs *compute* the facing order and the anchor pixels, but
   only pixels survive the export. Two pieces of documented scar tissue:
   - **The counter-clockwise facing bug.** The rigs' `order` array reads `['N','NE','E',…]` but cell
     *i* actually depicts **−45°·i**. This has bitten **four** kits and forced a per-artwork
     `FacingsAreCounterClockwise` flag through `BoatVisualDef` → `BoatHullSkinner` →
     `DirectionalBoatSprite`, plus a twin in `CharacterVisualLibraryBuilder`.
   - **Hand-measured anchors.** Mount points (motor clamp, fish tubs, helm seat, tiller grip, and the
     character rig's `handL/handR/head/hip`) are re-measured by eye, or shipped as a side-car JSON
     that nothing validates.

And now a fourth, which is what settled the engine choice: **the owner wants 32-direction movement.**
Hand-exporting 32 facings × N rock frames per hull, by a human, in a browser, is not a pipeline.

**The one risk that could sink an in-engine bake is interpreter speed.** These are software
rasterisers. So this ADR is backed by measurement, not estimate.

### What was measured (spike `spike/js-rig-baking`, 2026-07-19)

Rigs were loaded **byte-for-byte unmodified** into a bare **Jint 4.13.0** engine and into
**ClearScript/V8 7.5.1**. The harness is `Assets/Tests/EditMode/RigSpike/` (inert — see §5).

**Shims required: NONE, for either engine.** Not `globalThis`, not `ImageData`, not `console`, not
typed arrays. The rigs touch no DOM and no host API: they use `globalThis` (with a `window` fallback
they never need), `Math.*` (incl. `Math.hypot` with spread), `Float32Array`/`Uint8ClampedArray`,
`Array` methods, `Object.assign`, `parseInt` and `JSON`. Both engines supply all of it out of the box.
`render()` returns a `Uint8ClampedArray` of RGBA — **not** an `ImageData`, so there is nothing to
emulate. Output verified correct by dumping facings to disk and inspecting them.

**Speed.** The Unity **editor runs Mono** (`Mono 6.13.0`, confirmed in the batch log), which costs
Jint ~6.5× against desktop CoreCLR. Measuring outside Unity alone would have badly misled us, so the
load-bearing numbers are the in-editor ones:

| Rig (cell) | Jint / Unity **Mono** | Jint / .NET 10 | **V8 / Unity Mono** |
|---|---|---|---|
| Punt (184×168) — 1 facing | 902–1051 ms | 162 ms | **4.5 ms** |
| Console skiff (244×216) — 1 facing | 1523–1942 ms | 293 ms | **5.1 ms** |
| Punt — 8 facings | 8.1–9.0 s | 1.5 s | **0.02 s** |
| Console skiff — 8 facings | 14.4–17.5 s | 2.7 s | **0.05 s** |
| Punt — 64 cells (8 dir × 8 rock) | ~65–72 s | 12.3 s | **0.27 s** |
| Console skiff — 64 cells | ~115–140 s | 21.9 s | **0.45 s** |

Engine install (parse + execute the rig) is negligible everywhere: 4–81 ms.

**Pixel readback is free.** Render timings above are measured inside JS; the buffer still has to cross
into C#. Using ClearScript's bulk `ITypedArray<byte>.ReadBytes`, render+readback of a 210,816-byte
buffer measured **7.61 ms against 7.94 ms for render alone** — i.e. within noise. (This is a real
implementation constraint, not a free lunch: the naive per-element marshalling path would destroy the
win. **The baker must use the bulk `ReadBytes` API.**)

**Jint parallelises ~3.9×** across facings (one `Engine` per thread, 8 facings, 8 threads) — the
mitigation that made Jint viable at 8 directions.

## The finding that changed the decision: 32 directions

At 8 directions, Jint was genuinely adequate (a ~4–5 s threaded sheet bake) and this ADR originally
chose it on distribution cost. **32-direction movement invalidated that**, because bake latency lands
exactly where iteration matters.

### Fractional `dir` works — 32 directions needs no rig change and no art-director involvement

`dir` is used exactly once in every rig (`th = dir*Math.PI/4`) and never as an array index. Verified
empirically two independent ways on the punt:

- **Pixel evidence** — PCA long-axis bearing of the rendered hull sweeps smoothly and monotonically:
  `dir 0 → 90.0°`, `0.25 → 71.9°`, `0.5 → 56.6°`, `0.75 → 44.2°`, `1 → 33.3°`, `1.5 → 15.7°`,
  `2 → 1.0°`, with elongation rising 2.41 → 4.23 as the hull turns from bow-on to broadside. Every
  buffer hash distinct. `dir = 0.5` is a genuine 22.5° view — not snapped, not degenerate.
- **Anchor evidence (the stronger test)** — the punt's two tub mounts are both at `x:0`, dead on the
  centreline, so the screen angle between them is the *analytic* hull bearing. It agrees with the
  pixel-measured PCA bearing **within ~1° at every fractional dir**. So anchors return correct
  intermediate values, not merely non-crashing ones.

> Method note for whoever re-runs this: the same probe on the console skiff shows a ~12° gap. That is
> the *probe* being wrong, not the rig — its `TUBS[0]` is at `x:-0.48` (an aft quarter), so the line
> between tubs is not the centreline. Use a rig whose anchors are collinear.

**⚠️ One integer-heading table must generalise.** `MOTOR.behind:[3,4,5]` (punt and skiff motor rigs)
is the "draw the motor's *lower* layer UNDER the hull for stern-away headings" rule. It is
consumer-side compositing data — `render()` never touches it — but at 32 directions three indices
must become a **continuous arc (≈ dir 10–22 in 32-dir units)**. Miss this and the outboard leg draws
over the transom on **nine** facings.

### What 32 directions costs, and the layout it forces

Reference hull: the **Cape Islander**, cell **456×420 = 191,520 px** (0.73 MB/cell at RGBA32).
Uncompressed texture memory, and the same figures with tight per-facing cropping:

| layout | cells | RGBA32 | + tight crop (~60%) |
|---|---|---|---|
| 8 dir × 8 rock (today) | 64 | 46.8 MB | 18.7 MB |
| 32 dir base only | 32 | 23.4 MB | 9.4 MB |
| 32 dir × 2 rock | 64 | 46.8 MB | 18.7 MB |
| **32 dir × 4 rock** | 128 | 93.5 MB | **37.4 MB** |
| 32 dir × 8 rock | 256 | **187.0 MB** | 74.8 MB |

- **Tight crop is worth more than expected: measured 66.2% (punt) and 61.4% (console skiff)** across
  32 facings. Roughly two thirds of every sheet is empty padding, because the cell is sized for the
  broadside while most facings are far narrower. (The 60% applied to the Cape Islander is an
  extrapolation — its rig is not in the repo yet — but a 14 m hull is *more* elongated than a 5 m
  punt, so it should crop at least as well.)
- **Express the crop as the per-sprite pivot, not a parallel rect table.** Unity sprites already carry
  a per-sprite pivot, so a tight-packed atlas can put the boat origin at the same world point with
  **no change to `DirectionalBoatSprite`, the wake, or the spotlight**. This is the difference between
  a metadata change and an invasive one across the Boats lane.
- **The 4096 cap forces a layout change.** A single row of 32 facings is 32×456 = 14,592 px. A 4096
  page holds 8 cols × 9 rows = 72 cells. **Recommended layout: 8 columns × N rows, one texture page
  per rock-frame group.** For 32×4 that is 2 pages of 3648×3360 — dimensions identical to today's
  existing rock sheets, so importer settings carry over unchanged. With tight packing it becomes a
  normal atlas and the cap stops being a constraint at all.

### Bake wall-clock at 32 directions — the number that flipped the engine

Extrapolated to the Cape Islander's 3.63×-larger cell, Jint with its 3.9× threading win, against
measured V8:

| job | Jint (Mono, threaded) | **V8** |
|---|---|---|
| 32 facings, base sheet | **45–60 s** | **~0.6 s** |
| 32 dir × 4 rock (128 cells) | ~3–4 min | **~2.4 s** |
| 32 dir × 8 rock (256 cells) | **6–8 min** | **~4.7 s** |

A 6–8 minute rebake per large hull is not a progress bar, it is a coffee break — and it arrives
precisely when the owner most wants to iterate. That is what moved this decision.

## Decision

**Embed V8 (via Microsoft ClearScript) in an EDITOR-ONLY assembly, behind an `IRigScriptHost` seam,
and bake the art director's rig `.js` files by executing them unmodified. Commit to 32-direction
facings on all hulls. The rig source is the single source of truth for the artwork; sheets and the Def
metadata that describes them become BUILD OUTPUT of an editor tool, not hand-carried assets.**

### (1) The engine: V8/ClearScript, with the costs accepted explicitly

Chosen for **seconds-not-minutes bakes and real iterability at 32 directions**. The costs the owner
has accepted, recorded honestly:

- **~41 MB of payload per editor RID** — `ClearScriptV8.<rid>.dll` (**29 MB**) +
  **`ClearScript.V8.ICUData.dll` (11 MB)** + two small managed shims (`ClearScript.Core.dll`,
  `ClearScript.V8.dll`, ~0.5 MB each).
  > **⚠️ The ICU assembly is easy to miss until load fails.** Omitting it does not produce a
  > compile error — Unity reports `Unable to resolve reference 'ClearScript.V8.ICUData'` and then
  > *silently skips the whole assembly*, so the test run reports `total=0` and looks green. This cost
  > a debugging cycle in the spike. Ship it, and assert its presence.
- **Two RIDs minimum: `win-x64` AND `linux-x64` (~80 MB)**, because the owner authors on Windows but
  **CI runs Unity on `ubuntu-latest`** (`.github/workflows/ci.yml`). An editor assembly referencing
  ClearScript fails to load without the matching native RID. Add `osx-arm64` the day anyone authors
  on a Mac.
- **LFS storage.** `*.dll` is already an LFS pattern in `.gitattributes`, so this lands in LFS by
  default — but it is ~80 MB every clone pays for.
- **A build-breaking coupling on editor-host changes.** Any new editor platform, or a ClearScript
  upgrade, is a native-binary event rather than a package bump.
- **Licences.** ClearScript itself is **MIT** (Microsoft). The native V8 bundle's `License.txt`
  enumerates ~15 components: V8 (BSD-3-Clause), ICU (Unicode licence), FDLIBM, Strongtalk, JsonCpp,
  ProtoBuf, zlib, SipHash and others — predominantly permissive, **but it also includes a
  GNU C Library section under LGPL-2.1**. This is *not* the clean attribution-only story Jint had,
  and it is flagged deliberately. It is not judged a blocker: the binaries are unmodified, editor-only,
  and never ship in a player build (§3), which is the configuration LGPL-2.1 most readily permits.
  A `THIRD-PARTY.md` must carry the full bundled licence text.

### (2) 32 directions, with rock frames traded by hull size

Facing granularity is what reads worst on a big hull; rock amplitude is what reads worst on a small
one. A 14 m Cape Islander genuinely rocks *less* than a 5 m dory, so this is art direction, not just
a budget cut:

- **Small hulls** (dory, punt, skiffs — cells ≤ 244×216): **32 dir × 8 rock**. Cheap — the punt at
  32×8 is ~2.4 MB tight-cropped.
- **Large hulls** (Cape Islander and up): **32 dir × 4 rock** — **~37 MB tight-cropped, which is
  LESS than today's uncropped 8×8 at 47 MB**, while quadrupling heading granularity.

### (3) Editor-only, fenced twice

Nothing may reach a player build. Two independent fences, both machine-checked:

- **PluginImporter:** every DLL imported with `AnyPlatform = false`, `Editor = true`, all build
  targets explicitly off. Verified by an EditMode test that reads the `PluginImporter` back and
  asserts `GetCompatibleWithAnyPlatform() == false` and
  `GetCompatibleWithPlatform(StandaloneWindows64 | Android | iOS) == false`. (Passing in the spike.)
- **asmdef:** the baker assembly declares `"includePlatforms": ["Editor"]` and names the DLLs in
  `precompiledReferences` with `overrideReferences: true`, so no runtime assembly can even see them.

### (4) The bake emits METADATA, not just pixels — this is the point

Because the rig is *executing*, the tool reads back everything the rig knows and writes it into the
Def in the same operation (rule 2 — the output is data):

- **TRUE facing order.** The baker chooses the `dir` argument per cell, so it emits cell *k* as
  `render((N − k) % N)` — the 8-facing `(8−k)%8` generalised to N=32. **`FacingsAreCounterClockwise`
  becomes `false` for everything baked in-engine**, and is retained only for legacy hand-exported
  sheets until they are re-baked, then retired.
- **Measured anchors, at fractional dir.** `motorMount`, `tubMounts`, `helmSeat`, `tillerGrip` (and
  the character rig's `handL/handR/head/hip`) are evaluated per facing and per rock frame — they are
  live projections that ride the wave — and written into the Def as cell-pixel anchors, verified
  correct at fractional dir to within ~1°.
- **Cell geometry, pivot, and the crop rect** (`W`, `H`, `pivot`, `PX` + the per-facing tight bounds)
  come from the rig instead of a README.
- **The generalised `MOTOR.behind` arc**, so the outboard composites correctly across 32 facings.

### (5) Placement

- `Assets/_Project/Plugins/Editor/JsEngine/` — the ClearScript DLLs per RID, editor-only, with a
  `THIRD-PARTY.md` carrying the bundled licence text.
- `Assets/_Project/Code/Tools/Editor/RigBaking/` — the baker (tools-editor lane): the `IRigScriptHost`
  seam, a rig registry, the bake operation, and the editor window. **No Core change**: the baker
  writes existing Def types.
- The rig `.js` files stay where they are, under `docs/art/**`, **unmodified — that is the contract**.
  Any shim a future rig needs lives in our host code, never in their file.

### (6) The `IRigScriptHost` seam stays — only the default implementation changed

The seam is still right, and this ADR is deliberately reversible: **Jint remains a documented,
measured, drop-in alternative** (see Rejected alternatives, which keeps its numbers). Someone who
later decides the ~80 MB native tax is not worth it should be able to switch the default without
re-running this spike.

### Determinism & save

Bakes are editor-time asset production. No runtime code, no save-format change, no schema bump, no
effect on the `(worldSeed, gameTime)` determinism spine (rule 5). A bake is reproducible: same rig
source + same options → same pixels (verified by hashing the buffer).

### Performance posture (rule 7)

Zero player cost by construction — editor-only. 32-direction sheets do increase **runtime texture
memory**, which is why §2 trades rock frames on large hulls and why tight cropping is part of the
decision rather than a later optimisation: the recommended layouts land at or below today's usage.

## Phase 1 should ship LIVE SCRUBBING, not a static preview

The original ADR proposed "bake with a static preview of the last bake", because Jint's 1–2 s per
facing could not support anything better. **V8 removes that constraint and phase 1 should take
advantage of it from day one.** The numbers:

- Small hulls: **4.5–5.1 ms** per facing measured in-editor.
- Cape Islander, scaled by its 3.63× larger cell: **~18–19 ms** per facing.
- Pixel readback via bulk `ReadBytes`: **free** (within noise).
- Plus a `Texture2D.SetPixels32` + `Apply` upload, low single-digit ms at these sizes.

That is a **~20–25 ms full preview refresh on the largest hull — 40–50 fps**, and better than 100 fps
on the small ones. Dragging an elevation slider, scrubbing heading through all 32 directions, and
watching the rock loop play are all viable interactively. Building a static preview first would be
throwing away the main thing the engine choice just bought, and would bake a worse interaction model
into the tool's architecture. **Recommendation: D3 (live preview) moves up, and the preview is
interactive from the first release.** The one implementation constraint that must not be missed is the
bulk `ReadBytes` path.

## Consequences

- **The art director's source becomes the truth.** Sheets are build output. A rig tweak is one file
  change plus a re-bake, not a browser session and a hand-off.
- **32 directions is producible with no rig change and no art-director involvement** — the single
  highest-value finding of the spike. Big hulls stop snapping between 45° facings (P2).
- **The owner can author, interactively.** Place a rig, scrub it, press BAKE.
- **A four-time bug class is designed out.** Facing order is emitted correctly at source instead of
  corrected by a per-artwork flag downstream.
- **Anchors stop being eyeballed**, and are correct at fractional headings.
- **~80 MB of native binaries enter the repo via LFS**, editor-only, with a partially-LGPL licence
  bundle. Accepted by the owner, recorded here so it is never a surprise.
- **A new failure mode: native-binary drift.** A new editor host, or a ClearScript upgrade, breaks the
  editor assembly until the matching RID is added — and it fails *silently* (assembly skipped,
  `total=0`), so CI must assert the engine actually loaded rather than trusting a green run.
- **Sheet layout changes** (multi-page, tight-packed, per-sprite pivots) — `0006-boat-art-pipeline.md`
  needs updating in the Arc D PR that lands it.
- **`MOTOR.behind` must generalise** before any 32-direction motor bake ships.
- **Docs in the same arc:** `docs/architecture/project-structure.md` gains the plugin folder and the
  baker assembly; `0006-boat-art-pipeline.md` gains the 32-facing layout + TRUE clockwise order.

## Rejected alternatives

- **Jint (pure-C# interpreter) — the original choice, reversed.** Kept here in full, with numbers, so
  this decision can be reversed without re-running the spike. **For:** two managed DLLs, **~2.7 MB**,
  no native binaries, no per-RID payload, no LFS weight, runs everywhere Unity does; verified working
  in the 6000.5.0f1 Mono editor; parallelises ~3.9× across facings; licences are clean and
  attribution-only (**Jint BSD-2-Clause**, **Acornima BSD-3-Clause**, no copyleft). **Against:**
  902–1942 ms per facing under Mono — adequate at 8 directions (~4–5 s threaded sheet bake), but at
  32 directions on a large hull that becomes **45–60 s for a base sheet and 6–8 minutes for
  32×8**, against V8's ~0.6 s and ~4.7 s. It also cannot support live scrubbing at any hull size, and
  the rigs expose no quality/scale knob to render a cheaper preview (`PX`/`S` are module-level
  constants, not options) — so preview speed cannot be bought inside Jint without editing the art
  director's source, which this ADR forbids. **Reverse this decision by swapping the
  `IRigScriptHost` implementation** if the ~80 MB native tax ever outweighs iteration speed.
- **Port the rigs to C#.** The obvious "no dependency" answer, and the worst one. It forks the art
  director's source: every future tweak has to be re-translated by hand, and a translation bug reads
  as an art bug. It throws away the thing that makes this worth doing — that *his file* is what runs.
- **Node + Puppeteer / headless Chrome.** Fastest available and explicitly rejected for this project:
  **there is no Node on the authoring machine** (checked, including Unity's bundled tools), and it
  would make the art pipeline depend on a browser install, a package manager, and an out-of-process
  handshake per bake. A tool the owner cannot run is not a tool.
- **Keep hand-exporting from the browser (status quo).** Does not scale to the incoming rigs, keeps
  the owner out of the loop, structurally discards the facing order and the anchors — and cannot
  produce 32 directions at all at human throughput.
- **Pre-bake in CI instead of the editor.** Solves scaling but not authoring: the owner still cannot
  place a rig and see the result. An in-editor bake can *later* be driven from CI for a full re-bake;
  the reverse is not true.
- **Runtime rig evaluation (render the rig live in the game).** Puts an interpreter and a software
  rasteriser in the player build, blows rule 7, and gains nothing — the art is static once baked.
- **Keeping 8 directions and interpolating/rotating sprites at runtime.** Rotating a ¾-iso baked
  sprite is exactly the error ADR 0006 and the CCW scar tissue came from: the artwork is a projection,
  not a top-down icon, so rotating it shears the perspective. 32 baked facings are correct by
  construction.

## Notes for whoever builds Arc D

- **Never pass `-quit` with `-runTests`.** It races the test runner: Unity exits **0**, writes
  `total=0`, and looks like a pass. Found the hard way in this spike; it would quietly poison any CI
  job written that way.
- **Use the bulk `ITypedArray<byte>.ReadBytes` path** for pixel readback — the naive per-element
  marshalling would erase the engine advantage.
- **Assert the engine loaded**, don't infer it from a green run (see the `total=0` failure mode).

## Open questions (for Arc D)

- **Is the bake non-destructive?** ADR 0019's Refresh discipline says a re-bake must not clobber
  hand-tuned import settings or authored Def fields. Proposed: the baker writes pixels + rig-derived
  metadata only, and never touches owner-tuned fields.
- **Do we re-bake the existing kits immediately, or only new ones?** Re-baking retires
  `FacingsAreCounterClockwise` everywhere and takes every hull to 32 directions, but perturbs shipped,
  playtested art. Proposed: new rigs first, existing kits re-baked one at a time behind a visual diff.
- **Does 32-direction heading need a gameplay-side change?** `DirectionalBoatSprite` picks a cell from
  a heading; going 8 → 32 is a divisor change, but the snap-directional counter-rotation seam and any
  consumer that assumes 45° steps should be audited by gameplay-systems.
- **Scope of Arc D.** Proposed: D1 host + registry + one rig baked to a 32-facing sheet;
  D2 anchors + crop rects into the Def; **D3 interactive preview (moved up — see above)**;
  D4 re-bake the existing kits.
