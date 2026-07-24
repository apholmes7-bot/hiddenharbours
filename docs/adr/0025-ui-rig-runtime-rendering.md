# ADR 0025 — How the diegetic UI rigs (watch, lever, sounders, compass, helm consoles) render in the game

- **Status: PROPOSED — awaiting owner + lead-architect sign-off.** Records the options and a recommended
  direction for turning the art director's UI-rig `.js` drop (`docs/art/rigs/ui/`) into on-screen
  instruments. **The owner's steer (2026-07-24): these are "live rigs" — too many variables to pre-draw
  the whole screen** — which this ADR takes as the starting constraint and reconciles with the one hard
  line below. Nothing here is built yet; the analog-throttle model, the watch mapper, and the
  console/equipment data model that landed alongside this ADR are render-agnostic and do **not** depend on
  which option wins.
- **Date:** 2026-07-24
- **Decision owner:** lead-architect owns the dependency/fencing and the Core render seam; **art-pipeline**
  owns the look + any editor bake; **ui-ux** owns the console window host; **gameplay-systems** owns the
  instrument→state binding. (`agents/coordination.md` §1.1; CLAUDE.md rule 4.)
- **Serves:** **P1 (The Sea Has Moods)** and the keystone diegetic rule — *information is an earned
  instrument* (`docs/design/diegetic-ui-and-inventory.md` §3): depth lives on the glass of a sounder you
  own, not a HUD number. **P2/P4** — a boat's dash is a visible capability you grow.
- **Related:** `0021-in-engine-js-rig-baking.md` (the editor-only V8 bake this extends — and whose licence
  fence it must not break), `0003-data-driven-content.md` (rule 2), `0005-pc-first-target.md` (rule 7 — the
  60 fps budget), `docs/art/rigs/ui/README.md` (the imported rigs + their hit-geometry),
  `docs/design/diegetic-instruments-and-consoles.md` (the design handoff this implements).

## Context

The art director's UI-rig drop (`docs/art/rigs/ui/`) is ten parametric renderers — the watch, the single
lever, the outboard tiller, the depth & fish sounders, the compass, and four helm consoles. They are the
same *kind* of software renderer as the iso boat rigs (ADR 0021), with **two differences that decide this
ADR**:

1. **They are Canvas2D _painters_.** `render(opts)` returns a **drawn** `HTMLCanvasElement`, not the raw
   `Uint8ClampedArray` the boat rigs return. ADR 0021's spike found the boat rigs needed **no host shim**
   precisely because they never touch a canvas; these rigs draw with `fillRect`/`fill`/`clip`, linear &
   radial gradients, `drawImage`, the transform stack, and `globalCompositeOperation='lighter'`. Anything
   that *runs* them (in editor or at runtime) must supply a real 2D rasteriser.
2. **Their state space is continuous, not finite.** The boat rigs have 8/32 facings × a few rock frames —
   a countable sheet. These show live continuous values: heading 0–359, depth, water temp, rpm, fuel, a
   **scrolling** sonar with fish marks placeable anywhere at any size, and the ticking clock. **You cannot
   pre-bake every screen** — which is exactly the owner's point.

### The one hard constraint (non-negotiable, inherited from ADR 0021)

> **No JavaScript may run in the shipped player build.** ADR 0021's editor-only V8 fence is load-bearing
> for (a) the **LGPL-2.1 licence basis** — which explicitly depends on the native binaries never reaching
> a player build — and (b) **rule 7** — a per-frame software rasteriser in the game would blow the 60 fps
> budget. So a rig's *pixels* may be produced by running its `.js` **in the editor**, but its *behaviour
> in the shipped game must be pure C#*. "Live rig" therefore means **live C# rendering**, never a JS
> interpreter in the build. This constraint is what the options below are ranked against.

## Options

### Option A — Live C# renderers (a faithful port; the owner's "live rig", done legally)

Re-implement each rig's `render(opts)` as C# that draws into a `Texture2D` (or a mesh) **every frame from
live state**. Behaves exactly like the art director's rig — arbitrary continuous values, no finite atlas —
with **zero JS in the build**.

- **For:** handles the genuinely continuous rigs (the **fish-finder** above all: a scrolling waterfall with
  marks anywhere) with no contortion; one uniform model ("the rig draws itself, live"); no new editor
  bake pipeline; matches the owner's mental model.
- **Against (recorded honestly, it is ADR 0021's own objection to porting, `0021:337`):** it **forks the
  art director's source** — every future rig tweak must be re-translated by hand, and a translation bug
  reads as an art bug. Mitigations that make this tolerable *here* where it wasn't for boats: (i) the rigs
  expose their geometry/anchors as data we bake once and re-read (no hand-duplication of numbers), (ii) a
  **golden-master pixel diff** in the editor (render the C# port and the rig's own PNG export, assert they
  match) turns "translation bug" into a failing test, and (iii) UI rigs tweak far less often than hulls,
  and there are ten of them, not a growing fleet.
- **Cost:** the C# 2D drawing layer (paths + clip, linear/radial gradients, nearest-neighbor `drawImage`,
  the transform stack, `'lighter'`) is real work, but it is *our* code, tested, shipped — no native binary.

### Option B — Bake the reusable parts in-editor, composite live in C#

Keep ADR 0021's editor-only V8 bake, but extended with a Canvas2D shim so it can run these painters, and
bake only the **finite parts alphabet** (the 7-seg glyphs, the 3×5 font, the lever's own 9-frame strip,
the compass card, needle-at-N-angles, the static console chrome). The game blits pre-baked sprites and
paints the **one** continuous element (the sonar column) procedurally in C#.

- **For:** the runtime is pure sprite-blitting (cheapest against rule 7); the `.js` stays the untouched
  source of truth (the bake runs *his file*, ADR 0021's thesis extended from finite facings to finite
  parts); zero forking.
- **Against:** this is the option the owner pushed back on — the parts alphabet is only clean for the
  *discrete* instruments (watch, lever, compass, gauges). The fish-finder's continuous column still needs
  a live C# painter, so B never fully escapes A for that rig; and slicing every helm into bakeable parts +
  a hit-geometry sidecar is its own fiddly pipeline. It also still needs the **editor** Canvas2D shim
  (the main new cost), the same one A avoids by drawing in C# directly.

### Option C — Run the JS rigs live at runtime — **REJECTED**

Ship V8 (or any JS engine) in the player build and call `render()` live. **Forbidden:** it breaks the hard
constraint above on both counts — the LGPL-2.1 fence and rule 7 — and ADR 0021 already rejected it for the
boats. Recorded only so it is visibly off the table.

## Recommendation (proposed, for owner sign-off)

**A hybrid that defaults to Option A (live C# renderers) — the owner's "live rig" instinct, done inside the
no-JS fence — and adopts Option B's editor-bake only where a rig is genuinely finite and the blit is a
measured win.** Concretely:

- **Continuous rigs → live C# renderers (Option A):** the **fish-finder** for certain, and the **console
  dashes** as live compositors (they already draw as compose-of-parts internally, so a C# port is a
  faithful mirror, not a reinvention).
- **Finite rigs → may be editor-baked (Option B) as an optimization:** the **watch** (ten digit glyphs),
  the **lever** (a 9-frame strip it already bakes itself), the **compass card**, the **gauges**. These can
  start as live C# too and only move to baked sprites if profiling asks for it — so we are never *blocked*
  on the editor bake pipeline to ship a first instrument.
- **Either way:** the rig `.js` under `docs/art/rigs/ui/` stays **immutable source** (ADR 0021's rule); the
  **hit-geometry and anchors come OUT of the rig as data** (baked once in the editor, or lifted by a
  one-time editor dump), never hand-duplicated into the C# port; and a **golden-master pixel diff** guards
  every ported rig against drift.

This keeps the first instruments un-blocked (pure C#, no new native tooling on the critical path), honors
the owner's read that the sounders/consoles are too variable to pre-draw, and preserves the licence + perf
fences that ADR 0021 depends on.

## Consequences

- **A new C# 2D drawing layer** (paths/clip, gradients, nearest-neighbor blit, transform stack, `'lighter'`)
  becomes shared UI-render infrastructure. It is *our* code — no native binary, no licence exposure.
- **The rig source stays the truth**, but a **live C# port per rig** is now a maintained artifact; the
  golden-master diff is what keeps the port honest when the art director re-drops a rig.
- **Performance is the item to watch (rule 7):** unlike the finite boat sheets, a console redraws often at
  sea (heading, sonar, tach, 7-seg all move). The render must be designed for it from the start — throttled
  slow-tick repaint, one reused `Color32` buffer, change-detection repainting only the changed glyph, sonar
  as a ring-buffer column — and **profiled on the largest console** before it ships. This is the biggest
  open unknown.
- **No save-format change and no determinism impact** (rule 5): rendering is presentation; the sim state it
  reads is unchanged. Held throttle and window layout are transient/preference, never sim state.
- **Editor-bake fencing (if Option B parts are adopted):** any bake reuses ADR 0021's double fence
  (`includePlatforms:[Editor]` + `overrideReferences`); no runtime assembly may reference the JsEngine
  asmdef, or the licence basis reopens.

## Open questions (resolve before or during the build)

- **How much of the console is one live renderer vs. composited baked parts?** Prototype the fish-finder
  live first (the hardest case); let its cost decide whether the watch/lever/compass also stay live or move
  to baked sprites.
- **Mesh vs. Texture2D for the live path.** ADR 0024 moved fishing characters to facet meshes to escape the
  bake→slice→wire chain; a similar "draw the rig as geometry" path may beat per-frame CPU texture fills for
  the moving needles/card. Measure both.
- **The Canvas2D shim** (only if Option B parts are pursued): a JS Canvas2D polyfill executed *in V8*
  (reuses ADR 0021's bulk `ReadBytes` readback, avoids a C#-vs-browser fill-rule mismatch) vs. a C#-side
  rasteriser. Prefer the in-V8 polyfill for fidelity.
- **Does the live C# renderer share code with the boat rig baker at all,** or is it a clean-room UI render
  layer? Likely the latter — different primitives, different lifetime (runtime vs editor).
