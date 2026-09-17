# Multi-unit gameplay contract

One harness, three phases. **2464 checks / 0 failed** — 22 assertions across 112 configurations:
all 16 committed presets, plus a grid over each rig's own space (tier × units/perFloor × storeys ×
beds). Generated 2026-09-17T00:09:27.152Z.

Run it from a page with the rigs loaded; it needs no canvas, because every number it reads comes
from `gameplayAll()` rather than from pixels.

## The 22 assertions

| # | Assertion | What a failure means |
|--:|---|---|
| 1 | `schema` is `hidden-harbours/building-gameplay@1` | the file is not this schema |
| 2 | `frame` is 32 px = 1 m and heading-independent | the cell convention moved |
| 3 | `UNITS.length === build.units` | the sidecar publishes more dwellings than the build has |
| 4 | every unit has an interior box and a positive floor area | a dwelling with no room in it |
| 5 | `resident_slots` === that unit's sleep-anchor count | an occupancy figure not backed by a bed |
| 6 | every `SOLE` polygon is 4 points with non-zero area | a degenerate floor |
| 7 | `SOLE.z === building.storey_z[storey]` | a room floating off its plate |
| 8 | `SOLE.room_height_m === heights.room_height_m[storey]` | a room at a height the building does not have |
| 9 | every `holes` entry lies inside its own polygon | a stairwell cut outside the room it is cut from |
| 10 | every `THRESHOLD` clear width ≥ 0.70 m | a doorway a body cannot pass |
| 11 | every `THRESHOLD` storey exists | a door on a storey the building does not have |
| 12 | **`steps × rise_m === floor_rise_m`** for every flight | the rounded-rise defect: a top tread that misses its plate |
| 13 | `top.z − bottom.z === floor_rise_m` | the double-rise trap |
| 14 | a flight's `floor_rise_m` === the storey rise it leaves | the same trap from the other side |
| 15 | every interior flight has a 4-point `void_above` | a flight with no hole in the plate above it |
| 16 | **every interior flight has both landings** | an apron not reserved before furnishing |
| 17 | every `INTERACT` carries `reach.tested === true` and a verdict | an untested standing spot |
| 18 | no `INTERACT` reach point is null | a fixture nothing can reach |
| 19 | every non-null `host` appears in `BLOCKERS` at that storey | a verb hosted on a fixture that is not there |
| 20 | every `BLOCKERS` treatment is in the vocabulary | an unknown collider treatment |
| 21 | `room_height + slab === storey_rise` on every non-top storey | the heights block disagrees with itself |
| 22 | `_unplaced` is empty | the placer gave up on a piece |

## Assertions 12, 16 and 18 are the new ones

They are the three the manor and cottage pass put on the table, and each caught something real in
this pass:

- **12 (exact flights)** caught the gambrel tier's front steps: `1.06 / 6` rounded to `0.177`, and
  `6 × 0.177 = 1.062` — the top tread 2 mm above the porch it lands on. Same shape as the manor's
  `18 × 0.197 = 3.546` against a 3.55 floor rise.
- **16 (landings)** is the manor's own rule: reserve the apron BEFORE furnishing, not around the
  furniture afterwards.
- **18 (reach not null)** caught a bed on `captains2` with a dresser standing exactly where you get
  in, and four beds across the terrace publishing a second sleeper on a side with no floor. Both are
  fixed in the placement, not in the anchor — an anchor says where the rig draws the thing.

## Configurations swept

| Phase | Grid | Count |
|---|---|---:|
| walk-up | 6 presets + tier(2) × units(4,8,12) × storeys(2,3) × beds(1,2,3) | 42 |
| stack | 7 presets + tier(2) × perFloor(1,2) × storeys(2,3) × beds(1,2,3) | 31 |
| terrace | 3 presets + tier(2) × units(2,4,6) × storeys(2,3) × beds(1,2,3) | 39 |
| | | **112** |
