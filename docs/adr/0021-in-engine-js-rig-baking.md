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

## Decision

**Embed Jint — a pure-C#, dependency-light JavaScript interpreter — in an EDITOR-ONLY assembly, and
bake the art director's rig `.js` files by executing them unmodified. The rig source is the single
source of truth for the artwork; sheets and the Def metadata that describes them become BUILD OUTPUT
of an editor tool, not hand-carried assets.**

### (1) Why Jint, and not V8

Jint is fast enough, and it is the only option that stays a *pure managed* dependency:

- **Two DLLs, ~2.7 MB, no native binaries** (`Jint.dll` + `Acornima.dll`, both netstandard2.0). It
  drops into an editor-only plugin folder and resolves in Unity with no interop, no per-platform
  payload, and nothing for a build machine to install.
- **No platform coupling.** ClearScript/V8 would pull a native `win-x64` (and later `osx-arm64`, …)
  binary per editor host — tens of MB each, committed to the repo or restored by an out-of-band step,
  and a fresh licence/attribution surface. It buys speed we do not need for a bake.
- The measured numbers (below) put a full 8-facing bake in **seconds**, which is the only bar an
  editor bake has to clear.

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
- **ClearScript / V8 embedded.** Materially faster per facing, and unnecessary: a bake measured in
  seconds does not need it. The cost is native binaries per editor platform committed to the repo (or
  a restore step), platform coupling, and a bigger licence/attribution surface — all to speed up an
  operation that already fits inside a keypress. **Kept as an escape hatch**: if a future rig is an
  order of magnitude heavier, the engine sits behind one host seam and can be swapped.
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
