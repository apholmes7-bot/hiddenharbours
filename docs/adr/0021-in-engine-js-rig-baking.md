# ADR 0021 — Bake the art director's rigs IN-ENGINE: embed a JavaScript interpreter in the Unity EDITOR and run his `.js` source unmodified

- **Status:** **Proposed — awaiting owner sign-off.** Docs + a throwaway measurement harness; this ADR
  ships no tool. Merging this PR = the owner's go-ahead to build the baker (Arc D below). Records the
  decision that rig sheets stop being hand-exported from a browser and become an **editor operation**:
  place a rig in a scene, tweak it, press BAKE.
- **Date:** 2026-07-19
- **Decision owner:** lead-architect (a new editor-only third-party dependency + a new authoring seam
  that crosses art-pipeline, tools-editor and Boats — `agents/coordination.md` §1.1, CLAUDE.md rule 4).
  **tools-editor** owns the baker window and the bake pipeline; **art-pipeline** owns the import
  settings, sheet layout and the look; **lead-architect** owns the dependency, the asmdef/platform
  fencing, and the Def contract the bake writes into.
- **Serves:** **P3 "Living Working Coast"** and the owner's standing want to **author content himself**
  (`docs/…` level-design tooling plan): a rig you can place, nudge and re-bake in the editor is the
  same "paint = sail" move ADR 0014 made for the seabed, applied to sprites. Indirectly **P2** — the
  fleet grows to many hulls, and *many more rigs are coming* (houses, buildings, other types); the
  hand-export path does not scale to that.
- **Related:** `0006-boat-art-pipeline.md` (the sheet/pivot/facing conventions this bake must emit),
  `0003-data-driven-content.md` (rule 2 — the bake writes Def assets, it does not hard-code),
  `0014-painted-seabed-height-authoring.md` (the precedent: an **editor tool** that makes authored
  data the single truth), `0019-hand-authored-scenes-and-refresh.md` (non-destructive Refresh — the
  bake must behave the same way), `0005-pc-first-target.md` (editor-only cost, zero player cost).

## Context

Every iso kit in the repo — dory, punt, console skiff, sport skiff, the character rig — was produced
by the art director as a **parametric JavaScript software renderer**. Three of those rigs are already
in the repo and are the ground truth for this decision:

- `docs/art/punt-iso-rig/puntIsoRig.js`
- `docs/art/skiff-fleet-rigs/consoleIsoRig.js`
- `docs/art/skiff-fleet-rigs/sportSkiffIsoRig.js`

Each is a self-contained IIFE that installs a global (`globalThis.PuntIso`, …) exposing
`render(dir, opts)` → an RGBA byte buffer, plus `W, H, PX, pivot, order, ROCK, rock(i)`, per-rig
overlay renderers (`renderMotor`), and **measured anchors** (`motorMount`, `tubMounts`, `helmSeat`,
`tillerGrip`). Internally they are z-buffered triangle rasterisers with flat-facet shading, ordered
dither and a 1px keyline — real software rendering, ~470 quads over a 184×168 or 244×216 cell.

Today the path from rig to game is: the art director opens a browser turntable, exports PNG sheets by
hand, and hands over a folder. That path has three costs that are now biting:

1. **It does not scale.** More rigs are coming (houses, buildings, other types). Every tweak is a
   round-trip through a human and a browser.
2. **The owner cannot author.** He can place a prefab; he cannot place a rig, nudge the elevation, and
   see the sheet re-bake. That is exactly the leverage ADR 0014 and 0019 were built to give him.
3. **Hand-export loses the metadata.** The rigs *compute* the facing order and the anchor pixels, but
   only pixels survive the export. The two consequences are documented scar tissue:
   - **The counter-clockwise facing bug.** The rigs' `order` array reads `['N','NE','E',…]` but cell
     *i* actually depicts **−45°·i**. This has bitten **four** kits and forced a per-artwork
     `FacingsAreCounterClockwise` flag through `BoatVisualDef` → `BoatHullSkinner` →
     `DirectionalBoatSprite`, plus a twin in `CharacterVisualLibraryBuilder`.
   - **Hand-measured anchors.** Mount points (motor clamp, fish tubs, helm seat, tiller grip, and the
     character rig's `handL/handR/head/hip`) are re-measured by eye in pixels, or shipped as a
     side-car JSON that nothing validates.

**The one risk that could sink an in-engine bake is interpreter speed.** These are software
rasterisers; if a facing takes tens of seconds in an embedded interpreter, "place it and press BAKE"
is not a workflow. So this ADR is backed by measurement, not estimate.

### What was measured (spike `spike/js-rig-baking`, 2026-07-19)

All three rigs were loaded **byte-for-byte unmodified** into a bare **Jint 4.13.0** engine and asked
to render. Results are in the spike harness
(`Assets/Tests/EditMode/RigSpike/JsRigInterpreterSpikeTests.cs`) and the standalone comparison run.

- **Shims required: NONE.** Not `globalThis`, not `ImageData`, not `console`, not typed arrays. The
  rigs touch no DOM and no host API: they use `globalThis` (with a `window` fallback they never
  need), `Math.*` (incl. `Math.hypot` with spread), `Float32Array`/`Uint8ClampedArray`, `Array`
  methods, `Object.assign`, `parseInt` and `JSON`. Jint supplies all of it out of the box.
  `render()` returns a `Uint8ClampedArray` of RGBA — **not** an `ImageData`, so there is nothing to
  emulate. (Note for future rigs: the *cell* is 30–53k pixels; the ~190k figure in circulation is the
  RGBA **byte** count, 244×216×4 = 210,816.)
- **The output is correct.** Renders were dumped to disk and eyeballed: the punt broadside and the
  console skiff plan view come out pixel-identical in character to the shipped sheets — correct
  palette ramps, dither, keyline, pivots.
- **Speed — and the finding that shaped this ADR.** The Unity **editor runs Mono** (`Mono 6.13.0`,
  confirmed in the batch-mode log), and Mono costs Jint roughly **6.5×** against desktop CoreCLR.
  Measuring on CoreCLR alone would have been badly misleading, so the load-bearing numbers are the
  Unity ones:

  | Rig (cell) | Jint / Unity **Mono** | Jint / .NET 10 | V8 (ClearScript) |
  |---|---|---|---|
  | Punt hull (184×168) — 1 facing | **1051 ms** | 162 ms | 2.8 ms |
  | Console skiff (244×216) — 1 facing | **1942 ms** | 293 ms | 4.4 ms |
  | Sport skiff (244×216) — 1 facing | **~1600 ms** | 296 ms | 4.2 ms |
  | Punt — 8 facings | **9.0 s** | 1.5 s | 0.02 s |
  | Console skiff — 8 facings | **17.5 s** | 2.7 s | 0.07 s |
  | Punt — 64 cells (8 dir × 8 rock) | **~72 s** | 12.3 s | 0.26 s |
  | Console skiff — 64 cells | **~140 s** | 21.9 s | 0.42 s |

  The V8 column was **also re-measured inside the Unity editor** (ClearScript 7.5.1, same Mono host):
  4.5 ms/facing for the punt, 5.1 ms for the console skiff, 0.27–0.45 s for a 64-cell sheet — i.e.
  V8's advantage survives the Mono host that costs Jint 6.5×, because the work happens in native V8.

  Engine install (parse + execute the rig) is negligible everywhere: 37–81 ms under Mono.

  Read plainly: **Jint is fast enough to BAKE and too slow to PREVIEW.** A one-shot sheet bake at
  9–18 s, or a full rock sheet at 1–2.5 minutes, is a progress-bar operation an artist will accept.
  A **1–2 second refresh per facing is sluggish** for the "nudge it and look" loop the owner asked
  for — you cannot drag an elevation slider against that. V8 renders the same facing in **3–4 ms**,
  which is a live preview with room to spare.

## Decision

**Embed Jint — a pure-C#, dependency-light JavaScript interpreter — in an EDITOR-ONLY assembly, and
bake the art director's rig `.js` files by executing them unmodified. The rig source is the single
source of truth for the artwork; sheets and the Def metadata that describes them become BUILD OUTPUT
of an editor tool, not hand-carried assets.**

### (1) Jint first — because BAKING is the requirement, and Jint bakes

Jint is the only option that stays a *pure managed* dependency, and the measurements say it clears
the bar that actually matters:

- **Two DLLs, ~2.7 MB, no native binaries** (`Jint.dll` + `Acornima.dll`, both netstandard2.0). It
  drops into an editor-only plugin folder and resolves in Unity with no interop, no per-platform
  payload, no LFS, and nothing for a build machine to install. Verified: it loads and runs in the
  6000.5.0f1 editor under Mono.
- **A bake is a background job, not a keystroke.** 9–18 s for an 8-facing sheet is acceptable, and
  it parallelises: the facings are independent and Jint engines are cheap, so one `Engine` per
  thread gives a measured **3.9× speedup** (8 facings, 8 threads). That puts a realistic in-editor
  bake at **~4–5 s for a sheet and ~35 s for a full 64-cell rock sheet** on the owner's machine.
  (Legal in the editor: the rig is pure C# with no Unity API calls; only the `Texture2D` write
  returns to the main thread.)
- **V8 was measured, not assumed — including inside Unity.** ClearScript 7.5.1 loads and runs in the
  6000.5.0f1 Mono editor and renders a facing in **4.5–5.1 ms** (full 64-cell sheet in 0.27–0.45 s).
  It is genuinely ~350× faster. Its cost:
  - **~41 MB of payload per editor platform** — `ClearScriptV8.<rid>.dll` (29 MB) +
    `ClearScript.V8.ICUData.dll` (11 MB) + two managed shims. `*.dll` is already an LFS pattern in
    `.gitattributes`, so this lands in LFS.
  - **It is at least TWO platforms, not one.** The owner authors on Windows, but **CI runs Unity on
    `ubuntu-latest`** (`.github/workflows/ci.yml`). An editor assembly referencing ClearScript fails
    to load without the matching native RID, so a V8 integration means shipping `win-x64` **and**
    `linux-x64` (**~80 MB**), plus `osx-arm64` the day anyone authors on a Mac — and it makes every
    future editor-host change a build-breaking event. Jint has none of this: one managed DLL pair
    runs everywhere Unity does.
  - A fresh licence/attribution surface (ClearScript is MIT; the bundled V8/ICU carry their own
    Chromium BSD-3 and Unicode terms).

**Where Jint does NOT clear the bar: live preview.** A single facing costs **1–2 s** under Mono, and
parallelism cannot help one cell. That is fine for "nudge, wait a beat, look" and is *not* fine for
dragging an elevation slider. The rigs expose no quality/scale knob to render a cheaper preview
(`PX`/`S` are module-level constants, not options), so there is no way to buy preview speed inside
Jint without editing their source — which this ADR forbids.

**So the decision is scoped, not absolute:** build on Jint behind a one-interface host seam
(`IRigScriptHost`), and ship the phase-1 workflow as **bake with a static preview of the last bake**.
If the owner uses it and the tweak loop feels sluggish, swapping in V8 is a contained change — the
seam exists precisely so this is a configuration decision and not a rewrite. The trigger is explicit:
**if live scrubbing becomes a requirement, V8 is the answer and its cost is accepted then.**

### (2) Editor-only, fenced twice

Nothing may reach a player build. Two independent fences, both machine-checked:

- **PluginImporter:** the DLLs are imported with `AnyPlatform = false`, `Editor = true`, and every
  build target explicitly off. Verified by an EditMode test that reads the `PluginImporter` back and
  asserts `GetCompatibleWithAnyPlatform() == false` and
  `GetCompatibleWithPlatform(StandaloneWindows64 | Android | iOS) == false`.
- **asmdef:** the baker assembly declares `"includePlatforms": ["Editor"]` and names the DLLs in
  `precompiledReferences` with `overrideReferences: true`, so no runtime assembly can even see them.

### (3) The bake emits METADATA, not just pixels — this is the point

The bake is not a screenshot. Because the rig is *executing*, the tool reads back everything the rig
knows and writes it into the Def in the same operation (rule 2 — the output is data):

- **TRUE facing order.** The baker chooses the `dir` argument per cell, so it emits cell *k* as
  `render((8 − k) % 8)` and produces a genuinely clockwise sheet. **`FacingsAreCounterClockwise`
  becomes `false` for everything baked in-engine** and is retained only for the legacy hand-exported
  sheets until they are re-baked, then retired.
- **Measured anchors.** `motorMount`, `tubMounts`, `helmSeat`, `tillerGrip` (and the character rig's
  `handL/handR/head/hip`) are evaluated per facing — and per rock frame, since they are live
  projections that ride the wave — and written into the Def as cell-pixel anchors. Rods, hats, hauled
  crates and outboards then attach to **measured** points instead of hand-measured ones.
- **Cell geometry and pivot** (`W`, `H`, `pivot`, `PX`) come from the rig instead of a README.

### (4) Placement

- `Assets/_Project/Plugins/Editor/JsEngine/` — the two DLLs, editor-only, with a `THIRD-PARTY.md`
  carrying the two licence texts.
- `Assets/_Project/Code/Tools/Editor/RigBaking/` — the baker (tools-editor lane): a rig registry, the
  bake operation, and the editor window. **No Core change**: the baker writes existing Def types.
- The rig `.js` files stay where they are, under `docs/art/**`, **unmodified — that is the contract**.
  Any shim a future rig needs lives in our host code, never in their file.

### Determinism & save

Bakes are editor-time asset production. No runtime code, no save-format change, no schema bump, no
effect on the `(worldSeed, gameTime)` determinism spine (rule 5). A bake is reproducible: same rig
source + same options → same pixels (verified by hashing the buffer).

### Performance posture (rule 7)

Zero player cost by construction — editor-only. Editor cost is measured and bounded (below); the
baker runs off the main loop's critical path and reports progress.

## Consequences

- **The art director's source becomes the truth.** Sheets are build output. A rig tweak is one file
  change plus a re-bake, not a browser session and a hand-off.
- **The owner can author.** Place a rig, nudge it, press BAKE — the ADR 0014/0019 pattern extended to
  sprites.
- **A four-time bug class is designed out.** The facing order is emitted correctly at the source
  instead of corrected by a per-artwork flag downstream.
- **Anchors stop being eyeballed.** Attachment points come from the same geometry that drew the pixels.
- **A new third-party dependency enters the repo** — permissively licensed, editor-only, 2.7 MB, but
  it is a dependency, and it needs the owner's sign-off (see below).
- **A new failure mode: rig source that outruns the interpreter.** Future rigs are written against a
  browser JIT. A rig that leans on a modern ES feature Jint lags on, or that is an order of magnitude
  heavier, fails at bake time. Mitigation: a smoke test per registered rig, and the ClearScript escape
  hatch (§Rejected) stays open — the host seam is one interface.
- **Docs in the same arc:** `docs/architecture/project-structure.md` gains the plugin folder and the
  baker assembly; `docs/adr/0006-boat-art-pipeline.md` gains a note that in-engine bakes emit TRUE
  clockwise order.

## Rejected alternatives

- **Port the rigs to C#.** The obvious "no dependency" answer, and the worst one. It forks the art
  director's source: every future tweak has to be re-translated by hand, and a translation bug reads
  as an art bug. It also throws away the thing that makes this worth doing — that *his file* is what
  runs. Four kits' worth of rasteriser, shading, dither and palette logic re-implemented and then kept
  in sync forever, for a saving of 2.7 MB of managed DLL.
- **Node + Puppeteer / headless Chrome.** Fastest interpreter available and explicitly rejected for
  this project: **there is no Node on the authoring machine** (checked, including Unity's bundled
  tools), and it would make the art pipeline depend on a browser install, a package manager, and an
  out-of-process handshake for every bake. A tool the owner cannot run is not a tool.
- **ClearScript / V8 embedded.** ~350× faster, verified working in the Unity editor — and rejected
  *for phase 1* on distribution cost, not on merit: ~41 MB of native payload **per editor platform**,
  and because CI runs Unity on Linux it is ~80 MB across two RIDs minimum, in LFS, with a new
  build-breaking coupling every time an editor host changes. That is a permanent tax on every clone
  to accelerate an operation that already fits inside a progress bar. **Kept as a measured escape
  hatch** behind the `IRigScriptHost` seam: if live scrubbing becomes the requirement, or a future
  rig (a house, a building) is an order of magnitude heavier than a boat, the numbers to justify the
  swap are already in this ADR.
- **Keep hand-exporting from the browser (status quo).** Does not scale to the incoming rigs, keeps
  the owner out of the loop, and structurally discards the facing order and the anchors — the two
  things that have cost this project the most rework.
- **Pre-bake in CI instead of the editor.** Solves scaling but not authoring: the owner still cannot
  place a rig and see the result. Also drags the dependency onto the build machine. An in-editor bake
  can *later* be driven from CI for a full re-bake; the reverse is not true.
- **Runtime rig evaluation (render the rig live in the game).** Tempting and wrong: it puts an
  interpreter and a software rasteriser in the player build, blows rule 7, and gains nothing — the art
  is static once baked.

## Open questions (for the owner and for Arc D)

- **Dependency sign-off.** Vendoring `Jint.dll` + `Acornima.dll` (BSD-2-Clause / BSD-3-Clause,
  attribution-only, no copyleft) into the repo, editor-only. This is the one call that needs the
  owner explicitly.
- **Where do baked sheets land, and is the bake non-destructive?** ADR 0019's Refresh discipline says
  a re-bake must not clobber hand-tuned import settings or authored Def fields. Proposed: the baker
  writes pixels + rig-derived metadata only, and never touches owner-tuned fields.
- **Do we re-bake the existing kits immediately, or only new ones?** Re-baking retires
  `FacingsAreCounterClockwise` everywhere but perturbs shipped, playtested art. Proposed: new rigs
  first, existing kits re-baked one at a time behind a visual diff.
- **Scope of Arc D.** Proposed: D1 host + registry + one rig baked to a sheet; D2 anchors into the
  Def; D3 the in-scene preview component; D4 re-bake the existing kits.
