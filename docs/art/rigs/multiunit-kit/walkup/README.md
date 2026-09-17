# Walk-up block rig kit — multi-unit phase 2

`WalkupIso` + `WalkupUnitIso`: a corridor-access walk-up block, 4 to 12 flats over 2 or 3 storeys,
in a basic brick tier and a luxury cannery-conversion tier. Schema `hidden-harbours/building-gameplay@1`.

**Generated, never edited:**

    WalkupIso.gameplayAll(opts)      // the whole block, interiors merged
    WalkupIso.gameplay(opts)         // exterior sections only
    WalkupUnitIso.gameplaySections() // SOLE, THRESHOLD, STAIRS, INTERACT, BLOCKERS

Frame as the rest of the folder: metres, 32 px = 1 m, heading-independent, origin at the **ground
centre of the block footprint**, `+x` along the corridor, `+y` the street. The interior rig paints
into the same 1536 × 1500 cell at pivot 772,900, so a flat registers inside its bay to the pixel.

## This is the file the terrace pointed at

`rowhouseIsoRig`'s `_excluded` says: *elevator, lobby, corridor, shared laundry and basement lockers
belong to the walk-up block rig.* All five are present here, and **`ELEVATOR` is the first one in
this folder with real kinematics** — shaft and pit, car box and its floor height per level, bipart
landing doors with dwell and door times, travel, speed, and a call point per storey.

**A flat is single-level.** That is the difference between this phase and the terrace, and it is
why the lift matters: the terrace gives every household its own stair, a walk-up gives every
household the same one. `UNITS[].single_level` is `true` on every flat and there is no private stair
in the model.

## Committed presets

| Preset | Variant | Footprint | Flats | Slots | SOLE | THRESHOLD | STAIRS | INTERACT | BLOCKERS |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `walkup8` | basic_8u_2bed_2st | 22.6 × 18.9 m | 8 | 24 | 75 | 82 | 1 | 137 | 320 |
| `walkup12` | basic_12u_2bed_3st | 22.6 × 18.9 m | 12 | 36 | 112 | 122 | 2 | 202 | 474 |
| `walkup12Fam` | basic_12u_3bed_3st | 28.2 × 18.9 m | 12 | 48 | 132 | 142 | 2 | 306 | 666 |
| `walkup4` | basic_4u_1bed_2st | 11.7 × 18.9 m | 4 | 8 | 35 | 38 | 1 | 61 | 136 |
| `cannery12` | luxury_12u_2bed_3st | 24.7 × 20.9 m | 12 | 48 | 124 | 134 | 2 | 307 | 619 |
| `cannery8Loft` | luxury_8u_3bed_2st | 30.3 × 20.9 m | 8 | 32 | 88 | 95 | 1 | 211 | 439 |

Any build in the rig's space generates as well; these six are the committed set.

## Sections, and who owns each

| Section | Owner | What it carries |
|---|---|---|
| `UNITS` | shell | Per flat: beds, baths, storeys (always 1), area, resident_slots, interior box, bands, glazed edges, entry, party walls, loggia, shared access, lift_served |
| `ELEVATOR` | shell | One car, one shaft, every storey. Shaft box with pit and overrun, car inside dimensions and per-level floor z, bipart landing doors (`keep_clear` null — the collider is the panel), travel, speed, dwell, call points |
| `SHARED` | shell | The rooms a terrace has none of: lobby, corridor per storey (`means_of_escape`), shared laundry, lockers |
| `THRESHOLD` | both | The street entry and every flat door off the corridor, plus every interior door. Flat doors open **into the flat** — a leaf swung into a 1.7 m means of escape is a door that cannot exist |
| `SOLE` | rooms | One polygon per room per storey with `room_height_m`, wet and circulation flags, obstruction `_notes`, and the stairwell and lift shaft as real `holes` |
| `STAIRS` | rooms | The shared core flight per storey: exact rise, `void_above`, and the **landings** reserved before furnishing |
| `INTERACT` | rooms | Every verb with its host fixture, level and a tested reach verdict |
| `WALK` | shell | Entry steps, recessed loggias, the luxury roof deck |
| `MAIL` | shell | One bank in the lobby, one box per flat — a block does not put a box on every door |
| `BLOCKERS` | both | Every wall, stair and piece of furniture with height and a treatment |
| `ROOF` | shell | Deck and parapet z, walkability, the lift overrun as plant |

## Five hashes, because five renderers can drift

| Field | Bytes |
|-------|-------|
| `derivedFromRigSha256` | the shell |
| `interiorDerivedFromRigSha256` | the rooms |
| `propsDerivedFromRigSha256` | the furniture — `interiorPropRig.js` |
| `placerDerivedFromRigSha256` | where furniture lands — `_interiorPlacer.js` |
| `writerDerivedFromRigSha256` | the sections themselves — `_buildingGameplay.js` |

The terrace stamped three. Two more are needed now because the placement engine and the sidecar
writer are shared files: a change to either moves every number in every phase while both rig hashes
sit still. Nobody types these — `Art/_sidecarExport.js` hashes the bytes at save time and refuses to
write an unstamped file.

## The contract

**2464 checks / 0 failed** — 22 assertions across 112 configurations (all 16 committed presets plus
a grid over each rig's own space). What it asserts, and why each one is in there:

| Assertion | Catches |
|---|---|
| schema, frame | a file that is not this schema, or a cell that is not 32 px = 1 m |
| unit count, unit boxes | a build that publishes more dwellings than it has |
| `resident_slots` = sleep anchors | an occupancy figure not backed by a bed in a room |
| sole polygon / z / room height | a room at the wrong storey height, or a zero-area floor |
| holes inside sole | a stairwell cut outside the room it is cut from |
| door widths, door levels | a 0.4 m doorway, a door on a storey that does not exist |
| **flights exact** | `steps × rise ≠ floor_rise` — the rounded-rise defect |
| stair z span, stair rise = storey rise | the double-rise trap, both directions |
| `void_above`, **landings reserved** | a flight with no hole above it or no apron either end |
| reach tested, reach not null | an untested standing spot, or one published as a lie |
| hosts exist | a verb hosted on a fixture that is not in the room |
| blocker treatments | a treatment outside the vocabulary |
| heights consistent | `room_height + slab ≠ storey_rise` |
| nothing unplaced | a piece the placer could not fit and gave up on |

Re-run it rather than trusting a number in a file.

## Rebuild

    node — no. These rigs run in the browser. Open `Walkup Iso.dc.html` and use the GAMEPLAY SIDECAR
    button, or run Art/_multiunitKit.js from a page that has the rigs loaded.

Re-run the writer rather than patching a number. If any of the five hashes moves, every file here is
stale by definition.
