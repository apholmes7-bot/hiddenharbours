# Building lifecycle — the gameplay the art is now waiting for

> **Status:** backlog detail for **Epic M3-F** (`backlog.md`). Four items, all **M3**, all **not started**.
> **The art landed 2026-08-19** — see below. Nothing in this file is built; it is written so the arc is
> captured while the reasoning is fresh, rather than re-derived from a scene six months from now.
>
> **Canon:** [`../docs/vision-and-pillars.md`](../docs/vision-and-pillars.md).
> **Phasing:** [`../docs/roadmap.md`](../docs/roadmap.md) — do not start these during M1/M2 (CLAUDE.md rule 8).

---

## What already exists (the dependency, discharged)

The **building lifecycle pass** — `docs/art/rigs/building-lifecycle-kit/buildingLifecycleRig.js` — runs
between an iso building rig's `build(b)` and its `paint()`, and returns the same building at a different
point in its life:

- **7 construction phases**, in order: `site · foundation · frame · rafters · sheathed · cladding · finished`
- **4 states of dereliction**, kept-up first: `sound · neglected · abandoned · collapsing · ruin`
- **`burnt: true`**, a modifier on any of them

Decay **composes with phase** (an `abandoned` half-framed shell is a stalled build site — a real state a
save file can be in), and a state change is a **sprite swap**: same cell, same pivot, same ground line,
nothing moves. It is bound into `wharfBuildingRig`, `houseIsoRig` and `shopfrontRig`, and it reads the
*real faces* of whatever build it is handed — so every preset and every dialled config is covered with no
per-config authoring.

**The ids are append-only** (`BuildingLifecycleStates`, CLAUDE.md §5). They are already sheet names and
contract keys; the moment any item below ships they are also save-file values.

**⚠️ The pass has one measured trap and every item below inherits it.** An id it does not recognise is
*silently ignored*: `active()` returns true for any non-default value, then `normPhase`/`normDecay` fall
back to `finished`/`sound` and the faces come back untouched. A misspelled state does not throw — it draws
a building in perfect repair. `BuildingLifecycleStates.AssertKnown` refuses one on the bake side; anything
that ever reads a state out of a **save file** needs the same guard, because a save written by an older
build is exactly where an unknown id comes from.

**What the pass does NOT do**, so no item below assumes it: no interior at any phase (`frame` and
`rafters` are see-through by geometry, not by a cutaway); site props are baked into the cell rather than
separately addressable; smoke, lit windows and sign lettering stay host-side runtime overlays — **a ruin
has no lit windows, so gate those overlays on decay**. The Shop Building (cutaway interior) and Shipyard
(yard-parts) rigs take a different signature and are not covered.

**What is standing in the world today (2026-08-19, scenery only):** three derelict outbuildings on Aunt
Ginny's woods plot at St Peters (`ginnyWoodshed` @ `neglected`, `ginnyLeanTo` @ `collapsing`,
`ginnyNetStore` @ `ruin`) and the shut cannery beside the east pier (`stPetersCannery` @ `collapsing`).
They carry no interaction, no def and no save field.

---

## M3-27 — Building repair: derelict → sound

**Owner:** `economy-sim` (cost/materials) + `gameplay-systems` (the interaction) · **Pillar: P2 From Dory
to Dynasty.** Repairing a ruin you were given is the smallest possible version of the whole game's arc —
you turn something worthless into something that works, with money you earned.

**One-liner.** Walk up to a derelict building you own, pay materials + cash, and walk it back up the decay
ladder one rung at a time until it is `sound`.

**Seed AC.**
- A building's state is **saved**, not recomputed — this is the first thing in the world that is not a pure
  function of `(worldSeed, gameTime)`, and it must be versioned (ADR 0003 family, save-format change ⇒
  `lead-architect` sign-off).
- Repair moves **one rung** per job: `ruin → collapsing → abandoned → neglected → sound`. No teleporting to
  sound, and no repairing what you do not own.
- Cost scales with the rung: pulling a ruin back is the expensive one.
- The art is a **sprite swap** through the existing kit — a rung is a bake, not a new rig.
- Unknown state id read from a save ⇒ refused loudly, never normalised to `sound` (see the trap above).

**Art dependency:** the lifecycle kit. Every intermediate rung already bakes for every one of these
buildings; only `ginny*` and `stPetersCannery` at their *current* rungs are baked today, so shipping this
means baking the rest of each ladder (11 sheets per building, and the union crop makes each one cheap).

**Open question for the owner:** whose ruin is the starter ruin? Ginny's sheds are on *her* land, and there
is no land-ownership system — see M3-28.

---

## M3-28 — Vacant lots you can buy

**Owner:** `economy-sim` + `world-content` · **Pillar: P2 From Dory to Dynasty**, with **P3 A Living
Working Coast** — a coast where every parcel is already spoken for is a postcard, not a place.

**One-liner.** Named, priced parcels of the island that the player can buy, and that then permit building.

**Seed AC.**
- **There is no land-ownership system today.** A sweep of the data model finds only `BoatOwnerDef`;
  "Ginny's land" is a declared radius (`StPetersGinnyPlot.ClearingRadius`) that placement reads and nothing
  grants a right from. This item is where that becomes real, and it is a **Core data-model change** —
  ADR territory, `lead-architect` gate.
- A lot is **data** (a Def asset with a stable id), not code: boundary, price, what may be built on it,
  and who holds it. (CLAUDE.md rule 2.)
- Ties into the land-purpose vision and the ADR 0036 storey/interior family — a lot the player owns is
  also the thing that answers "may I put a bed here?".
- Buying a lot must not silently re-draw the woods: the woodland zones read building positions from their
  own files today, and a lot bought at runtime has no such file. **The clearing mechanism has to learn
  about runtime buildings** — the current comment in `StPetersBuilder` says so explicitly ("if a future
  clearing ever needs to know what actually got BUILT, it has to move above the planter, not hope").

**Art dependency:** none new. Lot markers are the greybox convention until the owner asks otherwise.

---

## M3-29 — Erect a building, phase by phase, on a lot you own

**Owner:** `gameplay-systems` + `economy-sim` · **Pillar: P4 Earn It, Then Automate It.** You watch the
thing you paid for actually go up, in stages you can see from across the harbour, before you ever get to
use it.

**One-liner.** Commission a building on an owned lot and walk it up the kit's seven construction phases —
`site → foundation → frame → rafters → sheathed → cladding → finished` — paying materials and time at each
step.

**Seed AC.**
- The **visible build IS the kit's phase ladder**. That is the whole reason this is cheap: seven states of
  every building in the game already exist, on one ground line, as a sprite swap.
- Phase advance is gated on **materials + game time**, not on a menu confirm — a build site the player
  passes twice a day and sees change is the point (P3).
- A stalled build is a real state and must survive a save: decay composes with phase, so an abandoned
  half-framed shell is representable and should be reachable (stop paying, and the site starts to go).
- Depends on **M3-28** (you cannot build on a lot you do not own).

**Art dependency:** the lifecycle kit's `PHASES`. Baking the ladder for a given building is one row in
`VillageBuildingKit.LifecycleSet` per rung.

---

## M3-30 — The cannery restart

**Owner:** `economy-sim` (the business) + `world-content` (the people) · **Pillars: P2 From Dory to
Dynasty · P3 A Living Working Coast · P4 Earn It, Then Automate It.** This is the arc where the three meet:
the player stops being a fisherman with a boat and becomes the reason other people have work.

**One-liner.** There was a thriving fish cannery at St Peters; the business went to the mainland and it
shut. Buy it, repair it, restart it, and employ the village.

**The owner's framing (2026-08-19):** *"a long-arc player goal: get it running again and employ St Peters
residents."*

**Seed AC.**
- **Repair first** (M3-27): the building is `collapsing` today and has to be walked back up before it can
  process a single fish.
- Restarting it turns the cannery into a **production facility** — takes catch in, puts processed goods
  out, on the M3 production-chain machinery (`M3-08`/`M3-09` family) rather than a bespoke system.
- **Employment is the payoff, and it has to be visible.** Villagers with a routine that now starts at the
  cannery door is what makes this land as more than a number going up (`NPC routine engine`, P3).
- The village's mood/dialogue should know: a shut cannery and a working one are two different towns.
- It should be **possible to fail**: a restarted cannery that cannot be supplied is a running cost, which
  is what makes the commitment mean anything (P5 Cozy, but with Teeth).

**Art dependency:** the lifecycle kit (the ladder from `collapsing` back to `finished` for the `cannery`
preset — all of it bakes from the build already in the kit). Later: lit windows and a smoke plume gated on
the building being *working*, which is a host-side runtime overlay the pass deliberately leaves alone.

**Open question for the owner:** does the cannery deserve its own preset/silhouette upstream? At
`collapsing`, the existing `wharfBuilding.cannery` preset reads as a big rusted-corrugated processing shed
with one roof slope stove in and a wall down — which is right. Whether a *restored* one wants to look more
particular than "the biggest wharf building" is an art-director call, not a blocker.
