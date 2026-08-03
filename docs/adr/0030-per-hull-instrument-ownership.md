# ADR 0030 — Helm instruments are owned PER HULL, and the save stores only the deviations

- **Status: ACCEPTED (2026-08-03)** — lead-architect, as the save-schema co-sign ADR 0025's addendum
  asks for. Landed with the depth-sounder slice (ADR 0025 S2). Owner may veto the buy-target rule in §4
  without disturbing the schema.
- **Date:** 2026-08-03
- **Decision owner:** lead-architect (Core + save schema). Boats owns the fit resolution, Economy the
  purchase, UI the glass.
- **Serves:** **P2 (Dory to Dynasty)** and **P4 (Earn It Then Automate It)** — a boat's dash is a
  visible capability you buy, per boat, and the keystone diegetic rule that *information is an earned
  instrument* (`docs/design/diegetic-ui-and-inventory.md` §3).
- **Related:** `0008-save-schema-and-versioning.md` (the versioning contract this follows),
  `0025-ui-rig-runtime-rendering.md` (the instruments themselves), `0003-data-driven-content.md`
  (rule 2 — ids are data, append-only), `0020-world-placed-object-persistence.md` (the
  store-the-anchor-not-the-result precedent).

## Context

Until now everything the player owned lived in one of three shapes:

| shape | example | why |
|---|---|---|
| presence-only wallet | `OwnedGear` ("gear.rod") | you have a rod or you don't, and you carry it |
| counted stock | `BaitStock` / `PotStock` / `SupplyStock` | it is spent, so it needs a quantity |
| world placement | `PlacedTraps` | it is a thing at a position |

A **helm instrument is none of these.** It is bolted into *one boat's dash*. Buying a depth sounder for
the skiff does nothing for the punt; selling the skiff would sell the sounder with it. `BoatEquipment`
already modelled this correctly as a pure function — `EffectiveFit(console, ownedForHull)` — and its own
doc deferred "the save schema that persists it … to their milestone". S2 is that milestone, and it needs
a fourth shape.

Two further pressures arrived with it. The instruments carry **preferences** (a shallow-alarm set-point,
metric/imperial, night backlight) that a fisherman sets once and expects to find tomorrow — and S3–S5
(fish finder, radar, chartplotter) will each want the same per-hull treatment, the plotter with
waypoints and routes on top.

## Decision

**Store per-hull instrument ownership as flat `(hullId, instrumentId)` rows, sparsely — and never store
the resolved fit or anything an instrument reads.**

1. **Two new `SaveData` lists, added at schema v8:**
   - `List<HullInstrument> HullInstruments` — one row per (hull, instrument) pair.
   - `List<SounderPrefsDto> HullSounderPrefs` — one row per hull that has been touched.
2. **Sparse is the contract, not an optimization.** A hull with no rows carries its
   `HelmConsoleDef`'s **authored default fit**. That is what makes the v7→v8 migration a pure addition:
   every existing boat comes out of it exactly as authored, because "nothing bought" is the same state
   it was already in.
3. **Flat pairs, not a nested id list per hull.** Follows `PlacedTrapDto`'s flat-scalar precedent —
   JsonUtility-friendly, human-readable on disk, and fitting another instrument is one more row rather
   than a reshaped record.
4. **One accessor: `InstrumentLocker` (Core).** Economy writes it, Boats reads it, UI reads/writes the
   preferences — none of them referencing each other (rule 4), and none of them hand-rolling a
   find-the-row-and-write-it-back loop (the `SupplyLocker`/`PotLocker` reason).
5. **The resolved fit and the instrument's READING are never stored** (rule 5). `EffectiveFit` stays
   pure and is recomputed on every read; the depth is `waterLevel − seabedElevation` over the one shared
   height map, taken on a throttled tick. A saved depth would be stale the moment the tide moved — it is
   the precise bug rule 5 exists to prevent, and `SaveMigrationV8Tests` asserts by reflection that no
   save field ever names one.
6. **Preferences are preferences.** The set-point, units and night flag ride the same per-hull keying but
   are never sim inputs: the alarm decides whether the glass flashes, not whether the boat grounds.

## Consequences

- **A fourth ownership shape exists, and it is the one S3–S5 will reuse.** The fish finder, radar, GPS
  and compasses are already `BoatEquipment` ids; each becomes rows in the same list with **no further
  schema bump** — one bump for the shape, none for the content (the `SupplyStock` lesson). The
  chartplotter's waypoints/routes are a genuinely different shape and still need their own step.
- **Selling a boat must eventually take its instruments with it.** No boat can be sold today, so nothing
  is broken; when a sale lands it must clear that hull's rows or the fitment resurrects on a re-purchase.
  Recorded here so it is not discovered later. *(Open — no owner action needed yet.)*
- **`DevIgnoreEquipmentGating` starts doing its job.** Declared as a no-op seam in S1, it now widens the
  resolved fit in the editor/dev builds so the owner can F-cycle the fleet without shopping. A shipped
  build gates on ownership + the console default.
- **Which boat a purchase fits is a UX rule, not a schema one** — see §4 below. The schema supports any
  answer.
- **No determinism impact.** Nothing here feeds the sim; the tide, the seabed and the sounding are
  unchanged and still recomputed from `(worldSeed, gameTime)` + authored geometry.

## §4 — The buy-target rule (owner may veto)

**A purchase fits the hull the player is currently aboard (`SaveData.ActiveHullId`); with no active hull
the vendor refuses and charges nothing.** Chosen because it needs no extra UI and matches how a fitting
actually happens — you bring the boat, they bolt it in. The buy-screen row NAMES the target hull so the
choice is informed rather than surprising.

The alternative (a pick-a-boat step on the buy screen) is a pure UI addition on top of this schema; if
the owner prefers it, nothing here changes.

## §5 — "Can this helm take it?"

`HelmConsoleDef` carries `SupportsFishFinder` but no `SupportsSounder`, and this ADR deliberately does
**not** add one: **any hull with a console can take the basic depth sounder.** A console *is* a dash with
a brow, and the sounder is the smallest instrument in the set — the flag would be a knob no authored
console would ever set false (rule 6: don't add a dial that never moves). `SupportsFishFinder` earns its
existence because a small brow genuinely may not take the bigger colour unit. Adding the flag later, if a
console ever needs to refuse the basic unit, is additive.
