# ADR 0031 — The keyline retires from the world-art style

- **Status: ACCEPTED (2026-08-05)** — owner decision, made in the art-direction session of
  2026-08-05; this ADR records it. Half A (this slice): the engine gate, the brief boilerplate, this
  record. Half B — a bulk re-bake of every sprite family — is **deliberately skipped**: sheets go
  outline-free naturally as their families are redone.
- **Date:** 2026-08-05
- **Decision owner:** owner (the style ruling); art-pipeline (the engine gate + briefs);
  art-director (rig sources, later, family by family — untouched here).
- **Serves:** **P1 (The Sea Has Moods)** and **P3 (A Living Working Coast)** — the painterly
  KTC read the art bible actually asks for: form carried by light and value, not by ink.
- **Related:** `0006-boat-art-pipeline.md` (the recipe phrase that propagated the outline — amended),
  `0022-3d-boat-hulls.md` phase 3 (the engine keyline this gates — amended),
  `0015-water-palette-guard-rail.md` (the ramps discipline the dark-side rule leans on),
  `0021-in-engine-js-rig-baking.md` / `0024-deck-character-mesh.md` (mention the keyline in passing;
  unchanged — their claims are about transcription fidelity, which the gate preserves).

## Context

**The 1 px keyline was never canon.** The art-and-audio bible — the style's design of record — has
*zero* keyline/outline mentions (verified 2026-08-05). It entered the project as a recipe phrase in
ADR 0006 ("post-pass outline, explicit dither, no AA"), and from there it propagated by copy-paste:
the rigs' shared rasteriser drew it, kit READMEs restated it, brief boilerplate locked it in as the
"standard fleet contract", and ADR 0022 phase 3 finally ported it verbatim into the engine as half of
the mesh fleet's fullscreen "Hull Keyline Resolve" pass.

That resolve pass does **two different jobs in one shader**, and only one of them is the outline:

1. **Depth-edge darkening** (the rig's `doEdge`): where two adjacent solid pixels differ in true
   view depth by more than 0.30 m, the far side is darkened two RINDEX ramp steps. This is an
   **interior** rule — it is what keeps overlapping parts of *one* object readable (a wheelhouse
   against its own deck, a gunwale against the hold).
2. **The 1 px keyline flood**: an empty pixel touching a hull becomes the neighbour's keyline
   colour. This is the drawn outline around the silhouette — the convention being retired.

The owner's ruling, from the art session: the outline flattens the painterly read. The silhouette
should be carried by **the form's own dark side** — the turning face going dark enough to separate
from any background in the master palette — not by a line around it.

## Decision

1. **The outline is retired from world art.** The silhouette is carried by the form's own dark side.
   Never let a lit face run to the sprite edge. Pale subjects need this deliberately: a white hull or
   wall separates on a darkened sheer strake, a shadowed tumblehome, a shaded eave — and if a form
   cannot hold its edge without an outline, the form needs work, not a line around it.
2. **Depth-edge darkening stays, everywhere, untouched.** It is the separate interior rule (rule 1
   above), not part of the outline convention.
3. **The engine keyline is gated by data, default OFF.** `GameConfig.HullKeylineFlood` (code default
   `false` — the asset lags the code, so the code default is what ships) → `GameServices.
   HullKeylineFlood` → `IsoFacetHullFeature` → the `_HHKeylineFlood` uniform, branched **inside the
   same resolve pass** production runs — never a forked shader path that can drift. The whole mesh
   fleet — the most-seen art in the game — converts in one afternoon, and one bool restores
   yesterday's look byte-for-byte for an A/B.
4. **Sheets migrate naturally (Half B skipped).** Sprite bakers (`TreeRigBaker`, `ShrubBaker`,
   `BuildingRigBaker`, …) and the rig `.js` sources keep their keyline code until each family is
   redone in the normal course of art work. **A mixed period — outline-free mesh boats beside
   outlined sprite buildings — is expected and accepted by the owner.**
5. **Brief boilerplate is replaced** with the owner-endorsed contract (no keyline; dark-side
   silhouette; the pale-subject rule; depth-edge darkening kept). Applied to every brief carrying
   the old contract line.

## Consequences

- **The oracle fixtures force the gate ON.** The GPU acceptances (`IsoFacetUrpPassTests`,
  `IsoFacetLobsterEndToEndTests`, `IsoFacetSideDraggerAcceptanceTests`,
  `HullWaterlineAcceptanceTests`) pin the pass against rig-drawn truths that include the keyline, so
  each forces `HullKeylineFlood = true` through the real dial (`GameServices.Config`) around every
  render — the port's "verbatim from the rigs' shared rasteriser" property stays provable, and the
  waterline fixture's bit-stable #263 pins stay untouched.
- **The gate is proven headless, with sabotage arms.** `IsoFacetKeylineGateTests` pins the style
  default, the GameServices resolution, the one production apply method against the real resolve
  shader, and the shader source gating the flood and only the flood — with two arms that break the
  mechanism deliberately and require the checks to throw ("SABOTAGE NOT DETECTED" otherwise). The
  pixel truth for gate-off (`KeylineGate_Off_RemovesTheFloodAndOnlyTheFlood`) is GPU-gated like every
  rendering acceptance: it skips loudly on CI and bites on any dev machine.
- **Instrument/UI rig chrome is exempt.** `WheelRigRender` and the other diegetic instrument faces
  draw keylines as part of the instrument's own look — gauge bezels are drawn objects, not
  world-sprite style. Untouched.
- **Sprite light masks are unaffected** (the dependency flagged against
  `docs/art/briefs/sprite-light-masks.md` §2). Verified 2026-08-05: the back-rim channel is computed
  by the rig's `packMask()` from a **distance transform over the cell's own alpha coverage**
  (`treeIsoRig.js` — rim = `d <= RIM_PX`), i.e. off the *silhouette*, never off the drawn keyline
  pixels. Outline-free rebakes keep rim lighting working; the only effect is the ~2 px rim band
  starting at the body's edge instead of at the 1 px ring.
- **ADRs 0006 and 0022 carry one-line amendment pointers** to this record at their outline clauses;
  their bodies stand as written (ADRs are records).
- **Owner eyeballs owed** (CI has no GPU; pixel truth is owner-side): one mesh hull afloat with the
  gate **off** — no outline, depth-edge darkening intact, water/waterline behaviour unchanged — and
  with the gate **on** — today's look, byte-identical.
