# ADR 0038 — Boat interiors: a cabin is a level that RIDES

**Status:** **Accepted** · **Date:** 2026-08-19 · **Deciders:** lead-architect (ruling, 2026-08-19),
art-pipeline (proposal, this PR) · **Supersedes nothing** · **Related:** ADR 0036 (interior levels
as layers), ADR 0032 (sorting band rebase), ADR 0033 (hull depth shear), ADR 0026 (rig pivot
conventions), the owner's 2026-07-30 seamless-interiors ruling

## Ruling (lead-architect, 2026-08-19)

**All four proposals accepted**, ruled as a set — 1 and 3 couple, and the scale in 1 is safe
*because* the swap in 3 means the two poses are never co-visible.

1. **Motion** — accepted as stated. `InteriorRockScale` on `GameConfig`, default 0.45, floor 0.0 as
   the accessibility setting, with the rule-5 boundary exactly as written below: a comfort filter on
   one draw, never feeding back into pose, handling, or the save.
2. **Water mask** — accepted. The hull is the mask; no second authority to disagree with the depth
   shear. The 16 px/m tanker caveat noted: px/m per def, builder refuses an omission.
3. **Occlusion** — accepted. Off-not-sorted, ADR 0036's property carried verbatim.
   ⚠️ The compositing-window-at-frame-edge problem **stays OPEN and recorded** — no future runtime
   PR may close it silently.
4. **Region hop** — accepted. The interior shares the hull's lifetime; the `OnDisable` law stands.

The proposal text below is kept **verbatim**, in the "Propose:" voice it was written in. The
judgment record is never rewritten after the fact — provenance is the point (the sidecar contract's
own `_confirm` → `_ruled` convention, applied to an ADR).

## Context

The boat-interiors drop (`docs/art/rigs/boat-interiors-kit/`, landed verbatim at `d2950cef`) ships
a rig-rendered interior for the working fleet: one renderer, 27 per-hull sidecars, and revised
exterior rigs that publish the HOUSE/loft/DOOR geometry the interiors were measured against. The
intake handoff asks three architecture questions before anything consumes it. This ADR answers all
three **as proposals**, each with the evidence the kit itself supplies.

The governing constraint is the owner's 2026-07-30 ruling, unchanged: interiors are **seamless**
(no scene load, no separate screen, no camera cut, no input-mode change) and **true to the
footprint**. ADR 0036 already settled the building case — a storey is a **layer on one footprint**,
exactly one drawn at a time. A boat cabin is the same shape of problem with one difference that
changes everything: **the footprint moves.** It rocks, it yaws, and there is water under it.

## Proposal 1 — Motion: accept "interiors ride `rock(i)`", and clamp for comfort

**The kit already answers this**, and answers it the way the architecture handoff hoped. Every
interior sidecar carries a `motion` block; the sport fishers' reads:

```json
"motion": {
  "rides_hull_rock": true,
  "source": "exterior rig ROCK + rock(i) — pass the SAME roll/pitch/heave to both renders and
             registration holds mid-wave",
  "level_floor_assumptions": "none — nothing is gimballed; the lamp, kettle and door tilt with the
             hull (diegetic)",
  "comfort_clamp_compatible": true
}
```

**Propose: accept it as stated.** It is the cheap answer and the correct one. The interior sheets
bake into the *same cell at the same pivot* as the exterior (`cell.note`: "composites under the
exterior 1:1"), so passing one pose to both renders keeps them in register by construction rather
than by a correction term. The alternative — a level floor inside a rocking hull — needs a second
camera basis, a second pivot convention, and a standing decision about what the horizon does. It
would also read as wrong: you are on a boat.

**What is NOT decided by the kit, and is proposed here: the comfort clamp.** A cabin fills the
frame in a way a deck does not, so the same rock that reads as life outdoors reads as nausea
indoors. Propose a single tunable, on `GameConfig` beside the other feel constants (rule 6 — no
magic numbers):

- **`InteriorRockScale`** — a scalar in `[0, 1]` multiplying roll/pitch/heave for the interior
  render **only**, applied to the pose handed to the interior sheets, never to the hull.
  Proposed default **0.45**, floor **0.0** (dead-flat, the accessibility setting), ceiling **1.0**
  (full fidelity, what the kit bakes).

Two properties worth stating, because they are why a *scale* is the right shape and a *cap* is not:

- **A scale cannot break registration on its own** — but it does mean the interior and the exterior
  are posed differently while both are visible. That is fine precisely because they are **never**
  both visible (Proposal 3): the layer swap means the exterior hull's interior-facing sheet is off
  while you are inside. This is a real coupling between Proposals 1 and 3, and it is the reason to
  rule on them together.
- **The clamp belongs to the render, not the sim.** Rule 5 — tide/wind/weather are recomputed from
  `(worldSeed, gameTime)` and the boat's physical motion is part of that. `InteriorRockScale` must
  never feed back into the hull's pose, her handling, or anything saved. It is a comfort filter on
  one draw.

## Proposal 2 — The interior water mask: join the level ray, do not fight it

The building case has no water in it. The boat case does, and the naive reading — "mask the water
wherever a cabin is drawn" — would put a second, screen-space authority in a system that already
has a world-space one.

**Propose: the cabin sole is a LEVEL, and the level is what the existing interior test already
asks about.** Concretely, mirroring ADR 0036 rather than inventing beside it:

- `BoatInteriorDef` publishes its walkable levels with their **hull-local `z`** (the sidecars
  already do: `house_sole` at z 1.78, `below_sole` at 0.55, `helm_deck` at 2.23 on the 53).
- Being inside is decided **exactly as ADR 0036 decides it** — a containment test against one
  footprint — with the footprint transformed by the hull's pose each tick instead of standing
  still. There is still **one** footprint per hull, so "true to the footprint" remains structurally
  unable to fail, which was ADR 0036's best property and is worth keeping literally.
- The water is then masked by the thing that already masks it: **the hull**. The cabin sole sits at
  a hull-local `z` above the waterline and inside the hull's own silhouette, so the sea is already
  behind the hull's facets at every pixel the sole covers. Nothing new subtracts from the water
  surface. The one genuinely new case is the **below-deck** level (z 0.55 on the 53, beneath the
  waterline) — and that one is *still* inside the hull silhouette, so it is still the hull doing
  the masking.

**Why this is the "rather than fighting it" answer.** A separate interior water mask would need to
agree with the hull's depth shear (ADR 0033) frame by frame, on a hull that is rocking; any
disagreement shows as sea leaking through a cabin floor on the wave crests only — the worst class
of bug to chase. Deriving the mask from the hull we are already drawing means there is nothing to
disagree with.

**Open, and flagged rather than answered:** the tanker bakes at **16 px/m** where the fleet is 32.
Two pixel grids in one kit. Whatever consumes these levels must carry px/m per def and scale at
composite time — never assume 32. The def below states px/m as the sidecar states it, and the
builder refuses a sidecar that omits it.

## Proposal 3 — Occlusion and sorting: entering swaps the cabin for the layer

Today a hull occludes what is behind it through the fore-block band encoding in
`IsoFacetHullRegistry` — `_HullIdFore` / `_HullIdForeSpan` bound a contiguous id block, and a
sprite on that deck carries `_HHDeckOccluderId` / `_HHDeckOccluderIdTop` so it can discard behind
the geometry in front of it.

**Propose: entering a cabin does not add a sorting rule — it swaps which sheet is on.** The house
is currently an occluder: it is geometry in front of the deck, and anything behind it is discarded.
On entry, the cabin's occluding sheet turns **off** and the interior level's sheet turns **on**, at
the same cell and the same pivot. This is ADR 0036's property carried over verbatim: *the storey
you are not on is switched off, not sorted behind*, so the band is never asked to rank a bunk
against a lobster crate. Exactly one of {cabin exterior, interior level} is drawn.

Three consequences, stated so they are ruled on and not discovered:

- **The player's own occluder ids change on entry**, because the set of geometry in front of them
  changed. That is a write to the two per-renderer properties on the swap, not a new mechanism.
- **The level resets on exit**, as ADR 0036 already requires — a boat that remembered "below" would
  open on her forepeak next time you stepped aboard.
- ⚠️ **The compositing window rides the hull, and this is KNOWN AND OPEN.** The interior sheets bake
  into the full hull cell at the hull pivot, so the window through which the interior is composited
  moves and rocks with the boat. At the frame edge — a hull part-way off screen, or a cabin whose
  cell extends past the camera — the window and the viewport disagree. **This ADR deliberately does
  not fix that**; the intake handoff flags it as open and it is out of scope here. It is recorded so
  that whoever builds the runtime meets it on purpose. It does not block the def or the builder,
  neither of which composites anything.

## Proposal 4 — Interiors survive a region hop exactly as the boat does

The boat is persistent core; her interior must be too. A region hop must not lose the cabin, and —
stated because it is the specific bug this sentence exists to prevent — **a Core service is never
unregistered in `OnDisable`.** `OnDisable` fires on a scene unload that the very next frame's
additive load undoes; unregistering there is how a persistent service dies on a region boundary
while looking like a lifecycle nicety. The interior registers with the same lifetime as the hull it
belongs to and outlives the region scene, as `BuildingInterior` already does for the level reset
(`ADR 0036`, "the level resets when the occupant leaves" — a region hop is one of the three ways
that happens).

## What this ADR does NOT decide

- Whether the refused hulls (see `docs/art/rigs/boat-interiors-intake/s0-verdicts.json`) get their
  interiors at all. That is upstream re-measure work, not an architecture question.
- The runtime component itself. This PR ships the **def, the reader and the merge** — data intake
  only. Nothing here draws a cabin.
- **The compositing-window-at-frame-edge problem above. Still OPEN after the ruling**, and recorded
  here so whoever builds the runtime meets it on purpose rather than discovering it.

## Consequences

- `InteriorRockScale` is an accepted tunable on `GameConfig`. It is deliberately **not added in this
  PR**: a constant nothing reads is the later-phase scope creep CLAUDE.md §8 warns about, and the
  field belongs in the runtime change that consumes it. Recorded here so it reads as scheduled, not
  forgotten. Nothing else in this ADR adds a constant.
- `BoatInteriorDef` lands in **Core** (`HiddenHarbours.Core`) because Boats poses the hull and Art
  draws it and neither may reference the other — the same reason `HullMeshDef` lives there (rule 4).
- Proposals 1 and 3 are **coupled** (see Proposal 1's second bullet); they were ruled on together,
  2 and 4 stand alone.

## Amendment — the CUTAWAY lands, and Proposal 3 gains a sentence (2026-08-26)

The owner ruled on 2026-08-26 that a boat interior is a **cutaway composite**: the boat's exterior
with a wall/roof cut away to reveal the interior, **exterior-only at the helm and on deck**. The
interior-mesh spike ([`interior-mesh-verdict.md`](../design/spikes/interior-mesh-verdict.md), PR
#644) had already proved the mechanism and named the one upstream item it was blocked on; the
cutaway kit's pass-3 rigs supplied it, and batch 1 (lobster, trawler, packet) is now baked and
gated. Four amendments follow, three of them the spike's own.

1. **Proposal 3 gains its second sentence.** *"Entering a cabin does not add a sorting rule — it
   swaps which sheet is on"* is true and **insufficient on a mesh hull**: culling the house does not
   cull the hull's own near **topsides**, which in a ¾ view stand between the camera and a cabin
   sole. Measured on the lobster: the swap alone leaves a revealed room **20.3%** visible; with the
   rig's own per-face depth bias it is **97.6%**. The mechanism is `db` in **UV0.z**, already in
   every baked mesh, subtracted from clip depth while the true depth (`o.wpos.z` — the
   deck-occupant band and the keyline resolve) is left alone. The value is the hull's own
   bounding-sphere diameter, so there is no constant to tune. **Recorded as the mechanism so nobody
   invents a sorting rule for it later**, and pinned by `HullLevelTagBakeTests` so a re-bake cannot
   drop it quietly.

2. **Proposal 3's ⚠ open item is CLOSED for geometry and stays open for sheets.** Geometry has no
   compositing window — it is in world space and the camera clips it like anything else. The
   fit-out sheets keep the problem, so the warning survives with its scope shrunk to the layers that
   are still pixels.

3. **A level must publish a ceiling, and an open sky must say so.** The spike proposed this as a new
   `BoatInteriorDef.CeilingZMeters`. It landed **one seam earlier instead** — on the RIG, as
   `geometry().levels[].ceilingZ` plus an explicit `ceiling:{kind:'open'}` — and reaches the game on
   `HullMeshDef.LevelTags`. That is the better place for the same reason the tag is: the rig knows
   the number by DECLARATION (it draws the roof lip), and both of the spike's attempts to measure
   one back off the mesh were wrong in ways only a render caught. `BoatInteriorDef` is unchanged.
   **A level with no declared ceiling is refused a cut** rather than guessed at, which is what makes
   "an absent field and an open sky must never look the same" enforceable rather than aspirational.

4. **The exterior half of the swap has an answer on a mesh hull.** #622 left `BoatInterior`'s
   `exterior` argument null and said so; on a mesh hull the house is geometry, not a sheet, and
   there was nothing to hand it. The answer is not a `Renderer` at all — it is
   `Core.IHullCutaway.ShowCutawayLevel`, driven by `BoatCutaway` from `CabinSignals` plus
   `HelmSlot` occupancy. `ExactlyOneLayerOn` is untouched and still reports false on a mesh hull:
   it asks about two sheets, and one of them has never existed.

**Two vocabularies, one join, and it is DATA.** The rig names a room `house`; `BoatInteriorDef`
names it `house_sole`; the interior sheets run a third order that is neither. The rig publishes the
def's id in its own `deck` field and the baker carries it to `HullMeshDef.LevelTags[].DeckId`, so
nothing re-derives the map from a `_sole` suffix at runtime. This is the same defect class that
already shipped once on this fleet (the tanker's `house_sole` resolving to the sheet row `below`).

### A cut takes its DECLARED ceiling (coordinator, 2026-08-27)

Building the tag surfaced one thing the presentation ruling did not anticipate: **three enclosed
levels have no hull faces of their own**, so they engaged the gate and removed nothing. Both ships'
`below` is an engine space walled by the shell — which is `hull`, the one class a cut may never
take — and the lobster's `cuddy` is a berth whose lid is the **foredeck**, a walkable level in its
own right. Going below drew a whole boat.

**Ruled:** when level *L* engages the gate, *L*'s own faces are culled **and** the faces of the
level *L*'s `geometry()` ceiling record names as its lid. **One hop only, declaration-driven, never
inferred from geometry.** Partial covers are untouched, and a per-level veto lets a rig opt out.

Three properties are worth stating because each is enforced by the shape of the data rather than by
a rule somebody has to remember:

- **One hop is unrepresentable, not merely forbidden.** The shader has ONE lid uniform, a level
  carries ONE lid field, and the bake refuses a lid that itself has a lid. A chain cannot be
  written down, cannot be baked, and cannot be expressed on the GPU.
- **The lid is culled; the lid's ROOM is not shown.** The interior branch tests only the level the
  occupant is in. A lid is a thing that comes off, not a second room you are also standing in.
- **An open level may not be cut INTO and may perfectly well BE a lid.** All three lids in batch 1
  are open decks. That asymmetry looks like a bug until you say it out loud, so it is said here and
  asserted in `HullMeshCutawayTableTests`.

> ⚠️ **The declaration the ruling names does not exist in the data yet, and this is the standing
> upstream ask.** Batch 1's ceiling records carry `of:`, which is PROSE — `'main-deck underside
> (DECK-0.12)'`, `'foredeck underside = sheerZ(y)-0.16'`. Those spellings are not level ids
> (`main-deck` is hyphenated where the id is `main_deck`; `boat-deck` and `wheelhouse deckhead` are
> not levels at all), and substring-matching a human sentence is a worse inference than the
> geometric one the ruling forbids. Matching a ceiling z against another level's sole z is that
> forbidden inference AND needs a per-hull tolerance nobody can justify (the gaps are 0.110 m,
> 0.120 m and 0.200 m — a deck-plate thickness the rig states in prose and publishes nowhere).
> So the three lids are declared in `RigLevelLids`, each quoting the rig's own words for it, and
> the extractor **refuses a bake where that table and a rig's `ceiling.lid` disagree** — the table
> cannot outlive the field it stands in for. **Ask upstream for `ceiling.lid` (and `lid: null` as
> the veto) and delete it.**

**Still not decided here:** the shell (sole, walls, ceiling, door aperture) as geometry — the
spike's HYBRID recommendation — which needs the three shell palette ramps inside the 16-slot
`_RampMeta` budget and is a separate lane. Nothing in this amendment emits interior geometry; the
mesh's TexCoord1.y is reserved for it and is 0 on every face baked so far.
