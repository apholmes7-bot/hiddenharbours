# ADR 0020 — World-placed object persistence: an append-only `PlacedTraps` list on `SaveData`, storing only irreducible facts

- **Status:** **Proposed — awaiting lead-architect sign-off.** This PR *is* the proposal; the
  coordinator's review + merge is the sign-off (`agents/coordination.md` §5.2/§8). Extends ADR 0008
  (the save schema) — schema-only: it adds fields, bumps the version, and migrates old saves. It
  ships **no trap runtime, placement, or `TrapDef`** — those are later builds of the trap-fishing arc
  (Build 0 of that arc: the save-schema groundwork so a dropped trap can survive save/load once the
  runtime lands in Build 3). The new fields are **unused until Build 3**, deliberately — the same
  inert-stub pattern as `Fishing.Gear.Trap` (a defined bit with no consumer yet).
- **Date:** 2026-07-06
- **Decision owner:** lead-architect (save format is an architectural/cross-cutting call —
  `agents/coordination.md` §8; `CLAUDE.md` rule 5 — the determinism dividend lives or dies here).
- **Serves:** **P4 (Earn It, Then Automate It)** — passive trap-fishing is the first "set it and
  come back" loop, and it only feels honest if the trap you dropped is *still there* next session;
  and **P5 (Cozy but with Teeth)** — a save that never strands your placed gear is what lets the sea
  be dangerous without the *game* feeling unfair (the lead-architect's standing save invariant). P1
  is *protected*, not served: soak progress and catch are **recomputed from `(worldSeed, gameTime)`**,
  never stored (rule 5) — the same discipline that keeps tide/wind/weather out of the save.
- **Related:** `0008-save-schema-and-versioning.md` (**this ADR extends it** — the v1 schema +
  append-only + forward-migration contract; ADR 0008's "Open questions → world-persistence" line is
  answered here for the trap case); `docs/design/diegetic-ui-and-inventory.md` §4.3 (flagged this
  exact work: "World-placed items and containers are *mutable world state* that must persist across
  save/load … World persistence therefore **extends the save schema** and is its own future ADR") and
  §9 item 3 ("World-persistence save format … extending ADR 0008's schema, with a migration and an
  old-save load test. *Its own save-schema ADR*"); `Core/Save/SaveData.cs` + `Core/Save/SaveMigration.cs`
  (the append-only DTO and the forward-only migrator this touches — the v2 OwnedGear/OwnedLicenses
  step is the template); `Fishing/Gear.cs` (`Gear.Trap`, the inert-stub precedent); `CLAUDE.md`
  rule 5 (determinism) and rule 6 (no magic numbers — tunables stay in Defs, never in the save).

## Context

The save schema (ADR 0008) records only state that **cannot be recomputed**: money, owned boats +
active hull, owned licenses, repaired boats, owned gear, and onboarding flags. Everything else —
tide, wind, weather, authored geometry, dormant NPCs — regenerates from `WorldSeed` + `GameTimeSeconds`
at load (data-model §4). The schema records **no world-placed objects**: nothing the player *sets
down in the world* survives a save, because until now nothing needed to.

The trap-fishing arc breaks that. A trap is the first object the player **drops into the world and
walks away from** — you bait it, place it on a spot, leave, and come back later to a catch. That is
new *mutable world state*: the fact "there is a trap of this kind, here, baited with this, placed at
this time" cannot be regenerated from the seed — it is a **player choice**, exactly the class of
fact the save exists to hold. The design doc already saw this coming and reserved it for its own ADR:

> **Flag (touches the save system, ADR 0008).** World-placed items and containers are *mutable world
> state* that must persist across save/load. The current save (ADR 0008) records money, owned boats,
> owned gear, and flags — **not** world-placed object positions or container contents. World
> persistence therefore **extends the save schema** and is its own future ADR (§9). Do not assume the
> current schema covers it.
> — `docs/design/diegetic-ui-and-inventory.md` §4.3

This ADR answers that flag for the **trap** — the first, narrowest instance — as **Build 0** of the
trap arc: get the *durable record* right and green before any trap runtime, placement UI, or
`TrapDef` exists. Build 3 will fill and read these fields; Builds 1–2 (buoy visual, etc.) don't touch
the save. Doing the schema first keeps every later build additive and keeps every already-shipped
save loadable.

The constraint that shapes the design is the same one ADR 0008 lived under, plus rule 5: **save the
irreducible facts, recompute everything derivable.** A trap's *soak progress* and *contents* are
derivable — given the placement facts we store and the deterministic sim, "what has this trap caught
by time T?" is a pure function of `(worldSeed, placement time, gameTime, trap/bait Defs)`. Storing
the catch would (a) bloat and desync the save, (b) duplicate a truth the sim already owns, and (c)
break the determinism dividend the way saving tide would. So we store placement, and **only**
placement.

## Decision

**World-placed persistent objects — starting with dropped traps — are saved as an append-only list
on `SaveData`, persisting only irreducible placement facts. Soak progress and trap contents are
recomputed from `(worldSeed, gameTime)` + the placement record, never stored. Bump the schema 2→3;
migrate older saves to empty lists. Keep the DTO concrete and minimal now, but shape it so the future
world-placed-*container* schema (buckets/racks per the diegetic-inventory doc §4.3) is a
generalization of it, not a rewrite.**

### (1) `PlacedTrapDto` — the placement record, irreducible facts only

A new `[Serializable]`, `JsonUtility`-friendly DTO (public fields only, matching every existing
`SaveData` type), carrying exactly what cannot be recomputed:

- **`InstanceId` (string)** — a stable per-instance id, unique per placed trap. Distinct from
  `TrapDefId` (which trap *kind*) because the player can place many of the same kind; the sim keys a
  trap's deterministic soak stream on this id, so it must be stable across save/load.
- **`TrapDefId` (string)** — the stable Def id of the trap *kind* (`trap.*`, resolved against the
  ContentDatabase at load, once `TrapDef` exists in Build 3). Content stays data (rule 2); the save
  carries the *reference*, never the trap's stats.
- **`PosX` / `PosY` (float)** — world position, stored as two flat floats. `SaveData` stores no
  vectors today, so this **sets the precedent**: flat scalar fields keep the DTO `JsonUtility`-clean
  and human-readable in the on-disk JSON (ADR 0008's readability goal), and dodge `JsonUtility`'s
  `Vector2` verbosity. If a later field genuinely needs a shared vector type, it can be introduced
  then; for one position, two floats is the minimal honest shape.
- **`BaitId` (string)** — the stable Def id of the bait loaded (`bait.*`), or empty for an unbaited
  trap. What's baited drives what soaks — an irreducible placement fact, not derivable.
- **`PlacementGameTimeSeconds` (double)** — the game-clock instant the trap was placed, at full
  precision (matching `SaveData.GameTimeSeconds`). This is the anchor the deterministic soak is
  computed *from*: catch = f(seed, this time → now, trap/bait Defs). Storing the anchor, not the
  result, is the whole determinism play.
- **`Region` (string)** — the region id the trap lives in, so a trap in an unloaded region is still
  recorded and restored when that region loads (scene-per-region, ADR 0004).

Explicitly **absent**: no contents, no catch list, no soak percentage, no "next ready" timestamp —
all recomputed (rule 5). No stats, no tunables — those live on the `TrapDef`/`GameConfig` (rule 6).

### (2) `BaitStock` — bait owned, with quantities

A second `[Serializable]` DTO recording bait the player owns as **counted** stock:

- **`BaitId` (string)** — stable bait Def id (`bait.*`).
- **`Count` (int)** — how many the player holds.

This mirrors `OwnedGear` (a wallet of owned ids) but bait is *consumable*, so it needs a **count**,
not just presence — you spend bait to arm a trap. Modeled as a list of `(id, count)` records rather
than a dictionary because `JsonUtility` serializes lists, not dictionaries (the same reason
`OnboardingFlags` is a `List<SaveFlag>`, not a `Dictionary`). One record per bait kind.

### (3) Generalize toward containers — intent noted, not built

The diegetic-inventory doc (§4.3, §9 item 3) foresees a broader world-placed-**container** schema:
buckets, tool racks, and other objects the player sets down, each with *contents*. `PlacedTrapDto` is
deliberately shaped as a **special case of that future record**: a stable `InstanceId`, a `DefId`,
a position, a region, and a placement time are exactly the fields a placed *container* would also
carry — a future `PlacedContainerDto` (or a generalized `PlacedObjectDto`) can share this skeleton.
We **note that trajectory and do not build it**: no generic `PlacedObject` base, no polymorphic
serialization, no contents model now. Over-engineering a generic world-object system before the
second use case exists would violate rule 8 (stay in phase) and add serialization complexity
`JsonUtility` handles badly (it has no polymorphic support). When containers arrive, they get their
own ADR extending *this* one, and can refactor the shared shape then with two real use cases in hand
— the same append-only, migrate-forward discipline. The concrete-first choice keeps this build
minimal and green.

### (4) Schema bump 2 → 3 + forward migration

`SaveMigration.CurrentVersion` goes `2 → 3`. A new `if (data.SchemaVersion < 3)` step gives any save
loaded at v2-or-earlier **non-null, empty** `PlacedTraps` and `BaitStock` lists and stamps it v3 —
the exact filler pattern the v2 OwnedGear/OwnedLicenses step uses. An older save simply had no placed
traps and no bait; empty lists are the correct upgrade, and every scalar (seed/time/money) and every
v1/v2 list carries through untouched. The unconditional defensive null-repair at the tail of
`Migrate` also gains the two new lists, so even a hand-edited or partial v3 JSON that omits them loads
usable. No shipped field is renamed or repurposed (append-only, ADR 0008).

### What does NOT change (determinism, data, Core seams)

- **Determinism is preserved, not spent.** Only placement facts are saved; soak/contents recompute
  from `(worldSeed, gameTime)` + the record (rule 5). This ADR *adds* to the save exactly the class
  of state ADR 0008 sanctions (player choices that can't be regenerated) and *keeps out* the class it
  forbids (anything derivable).
- **Content stays data.** Traps and bait are Defs (`trap.*`, `bait.*`); the save stores ids, the
  ContentDatabase resolves them (rule 2). No trap/bait stat ever enters the save (rule 6).
- **Core stays additive.** New DTO types + list fields on the existing `SaveData`, a new migration
  step — no breaking change, no new `GameSignals`, no new service. The fields sit inert (like
  `Gear.Trap`) until Build 3's runtime reads them, which is correct for a schema-groundwork build.
- **Runtime cost: nil.** Two empty lists on a DTO that already exists; no per-frame work, no scene
  presence (rule 7).

## Consequences

- **A dropped trap can survive save/load — once Build 3 wires it.** The durable record is in place
  and green now; the runtime that fills and reads it lands later without any further schema churn.
- **No shipped save is stranded.** v0/v1/v2 saves climb to v3 with empty lists; the old-save load
  test pins it. The lead-architect's standing invariant (never strand a save) holds.
- **The determinism dividend is protected.** Traps join tide/wind/weather as things whose *behavior*
  is recomputed; only the player's *placement choice* is stored. Saves stay small and can't desync
  from the sim.
- **The container schema has a proven skeleton to grow from.** When world-placed containers arrive
  (§4.3), they extend this record rather than inventing a parallel one — with two real use cases to
  justify any generalization.
- **One honest deferral:** if a future field genuinely needs many placed objects to share a vector or
  a contents model, that refactor is a *later* append-only ADR. This build stays concrete.

## Rejected alternatives

- **Store soak progress / trap contents in the save.** The tempting shortcut, and the wrong one:
  it's exactly the tide-in-the-save mistake (rule 5) — it bloats the save, lets it desync from the
  deterministic sim, and duplicates a truth the sim already computes. Placement + seed + time is
  strictly enough to recompute the catch; storing the catch buys nothing and costs the determinism
  dividend.
- **A generic `PlacedObjectDto` / polymorphic world-object list now.** Over-engineering before the
  second use case (containers) exists — rule 8. `JsonUtility` has no polymorphic serialization, so a
  generic base would force a type tag + manual dispatch for zero present benefit. Concrete
  `PlacedTrapDto` now, generalize when containers land, is the honest sequence.
- **A separate world-state save file / sidecar.** A second persisted artifact that can drift from the
  main save, needs its own atomic-write + migration machinery, and complicates load ordering. ADR
  0008's single versioned `SaveData` blob is the one source of truth; world-placed objects belong
  *in* it, as fields, migrating with it.
- **`Vector2`/`Vector3` fields for position.** `JsonUtility` serializes `Vector2` verbosely and it
  reads worse in the human-readable JSON ADR 0008 values. Two flat floats are minimal and clear for a
  single position; a shared vector type can be introduced later if many fields need one.
- **A `Dictionary<string,int>` for bait stock.** `JsonUtility` doesn't serialize dictionaries — the
  same reason `OnboardingFlags` is a list of structs. A `List<BaitStock>` of `(id, count)` records is
  the JsonUtility-friendly, on-disk-readable shape.
- **Defer the schema until Build 3 (build the runtime and the save together).** Bundling schema +
  runtime makes a bigger, riskier PR and a later save-migration. Landing the schema first (this
  Build 0), inert, keeps each PR small and single-purpose (§6) and every save backward-compatible
  from the moment the field exists — the same reason `Gear.Trap` was reserved as a bit before its
  gear existed.
