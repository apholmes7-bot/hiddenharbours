# Rowhouse rig kit

Multi-unit harbour housing: **one parametric terrace**, 2–6 attached units, two tiers, with fully
furnished interiors and one gameplay sidecar per building.

    shell  1018d53f51ad6e97e77b513253c2fe7ea8a8e6f625222c1318eb9a8678795c93
    room   bd7222b69e7409f83c88da016544c0b1bb96a8eb3463ec8099c659eb6ad72ea5
    props  bc361678ab4f6e7b2d63f04e34f5d54cd9b9696eb2afdeeaf397fba758469307

## Three rigs, one entry point

| File | Owns |
|------|------|
| `rigs/rowhouseIsoRig.js` | The shell. Bays, cladding, roofline, entries, stoops, balconies. Publishes `dims()`, `shell()`, `units()`, `anchors()` — and `gameplayAll()`, the one call that produces a whole sidecar. |
| `rigs/rowhouseUnitIsoRig.js` | The rooms. Measures nothing: reads `RowhouseIso.shell()` and lays out every unit's plan, then furnishes it. Publishes `gameplaySections()`. |
| `rigs/interiorPropRig.js` | The furniture. Every bed, counter, tub and sofa an interior places, period and modern. |

Load them in that order, then:

```js
const sidecar = RowhouseIso.gameplayAll({ tier:'basic', units:4, beds:2, storeys:2 });
```

`gameplay()` alone gives the exterior sections without the room rig. If the room rig is missing,
`gameplayAll()` says so in `_confirm` rather than shipping a file that looks complete.

## Why three hashes

Three renderers can drift independently, so each is stamped: `derivedFromRigSha256` (shell),
`interiorDerivedFromRigSha256` (rooms), `propsDerivedFromRigSha256` (props). If any moves,
**re-run `Art/_rowhouseKit.js`** — never patch a number. The builder page checks all three against
the committed copies on load.

## Shipped terraces

| Preset | Variant | Footprint | Units | Resident slots | SOLE | THRESHOLD | STAIRS | INTERACT | BLOCKERS |
|--------|---------|-----------|------:|---------------:|-----:|----------:|-------:|---------:|---------:|
| `millRow4` | basic_4u_2bed_2st | 22.4 × 10.6 m | 4 | 16 | 32 | 28 | 4 | 76 | 196 |
| `duplexPair` | basic_2u_3bed_2st | 12.4 × 10.6 m | 2 | 8 | 16 | 14 | 2 | 38 | 98 |
| `coastalRow4` | luxury_4u_3bed_3st | 28 × 11.6 m | 4 | 24 | 64 | 56 | 8 | 154 | 359 |

Any build in the rig's space generates as well; these three are the committed set.

## Sections

| Section | Owner | What it carries |
|---------|-------|-----------------|
| `UNITS` | shell | Per dwelling: beds, baths, ensuite, storeys, floor area, **resident_slots** (real sleep anchors), interior box, entry, party walls, private outdoor, level table. |
| `THRESHOLD` | both | Street entries (hinge axis, 95° outward swing, swept rect) and every interior door. Interior leaves park **flat against the wall**, so the collider is the opening, not an arc. |
| `STAIRS` | rooms | Steps, rise, run, bottom/top points, handrail — and `void_above`, the hole the flight needs in the plate above it. |
| `SOLE` | rooms | One walkable polygon per room, per storey, with wet flag, circulation flag, obstruction `_notes` (footprint + height + treatment) and, on a landing, the stairwell as a real **hole**. |
| `INTERACT` | rooms | Every routine anchor: sleep (per bed side) · cook · wash_dishes · dine · sit · toilet · bathe · wash · laundry · storage · desk. Each with its host fixture, level and a **tested** reach verdict. |
| `WALK` | shell | Stoops with their treads and handrails, luxury entry landings, private balconies with balustrade specs. |
| `MAIL` | shell | Wall boxes with a delivery stand point — basic tier only. |
| `BLOCKERS` | both | Every wall, stair, fixture and piece of furniture with a height and a treatment (`step_over` / `waist_block` / `wall`). |
| `ROOF` | shell | Deck and parapet heights, walkability, and the bulkhead where there is roof access. |

### Reach is tested, not requested

A body of radius 0.22 m marches out from each fitting in 6 cm steps up to 1.20 m of arm, inside the
anchor's **own unit**, clearing every blocker at that level. Each point carries its verdict
(`ok` / `moved`), and a point that cannot clear would be published `null` with a reason rather than
as a plausible lie. Across the 48-configuration contract run: **0 nulled**.

## What is deliberately absent

No elevator, lobby, corridor, shared laundry or basement lockers: a terrace is walk-up by
definition — every unit has its own street door and its own private stair. Those belong to the
walk-up block rig. No washboards or cleats (not a hull). No walkable roof on the basic tier. No
rear garden: the rig bakes the building, not its plot.

## Contract

`CONTRACT.md` — 18 assertions × 48 configurations, **864 checks, 0 failed**. Re-run with
`RowhouseKit.contract()`.

## Builder pages

`Rowhouse Iso.dc.html` (the terrace, 8 facings, both tiers, the sidecar button and the hash check)
and `Rowhouse Interiors.dc.html` (the rooms, per unit or per floorplate).

## Re-issued 2026-09-16 under the multi-unit contract

Phase 1 was written before there were three phases. It now shares the placement engine
(`_interiorPlacer.js`) and the sidecar writer (`_buildingGameplay.js`) with the walk-up and the
stack, so the three cannot drift into three dialects of one schema. **Five hashes now**, not three:
the placer and the writer are renderers that can move while both rig hashes sit still.

**Numbers that moved, and why.** A double bed used to claim a sleep anchor on both sides whatever
the room did. It now earns one per side there is actually floor to stand on, so:

| Preset | INTERACT | `resident_slots` | Reach audit |
|---|---:|---:|---|
| `millRow4` | 76 → **72** | 16 → **12** | 72 checked, 36 as written, 36 repaired, 0 nulled |
| `duplexPair` | 38 → **38** | 8 → **8** | 38 / 20 / 18 / 0 |
| `coastalRow4` | 154 → **154** | 24 → **24** | 154 / 94 / 60 / 0 |

`millRow4`'s back bedroom is 2.4 m across: its bed fits, and one side of it does not. A 2-bed flat
there sleeps three, not four. Anything reading occupancy should re-read it.

Also new on this phase: `heights` (one rise and one clear height per storey), `room_height_m` on
every `SOLE` entry, reserved stair landings in `STAIRS[].landings`, exact flight rises, a
`floorAtPivot` option on the room rig, and room-sized rugs in the luxury bedrooms.
