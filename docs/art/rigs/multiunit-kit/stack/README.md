# Three-decker stack rig kit — multi-unit phase 3

`StackFlatsIso` + `StackUnitIso`: a New-England three-decker, 2 or 3 storeys, one or two flats per
storey, in a porch tier and a gambrel captain's tier. Schema `hidden-harbours/building-gameplay@1`.

**Generated, never edited:**

    StackFlatsIso.gameplayAll(opts)  // the whole house, interiors merged
    StackFlatsIso.gameplay(opts)     // exterior sections only
    StackUnitIso.gameplaySections()  // SOLE, THRESHOLD, STAIRS, INTERACT, BLOCKERS

Frame: metres, 32 px = 1 m, heading-independent, origin at the **ground centre of the plot
footprint**, `+x` across the front, `+y` the street, `−y` the yard. The interior rig paints into the
same 1280 × 1400 cell at pivot 640,960, so a flat registers inside its storey to the pixel.

## What a three-decker is, in sections

It is **not a small walk-up**, and the sidecar says so in what it leaves out as much as what it
carries. There is no corridor and no lift. The stair bay *is* the shared space. And the porches are
stacked outdoor rooms every flat reaches from its own hall — **`PORCH` is present here and nowhere
else in this folder**.

Every flat is **through-plan**: full depth, street to yard, one level, its own back door.

## Committed presets

| Preset | Variant | Footprint | Flats | Slots | SOLE | THRESHOLD | STAIRS | INTERACT | BLOCKERS |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `decker6` | porch_6u_2bed_3st_2pf | 17.6 × 12.2 m | 6 | 18 | 54 | 77 | 4 | 125 | 248 |
| `decker3` | porch_3u_3bed_3st_1pf | 11.4 × 12.2 m | 3 | 18 | 36 | 47 | 4 | 71 | 149 |
| `decker4` | porch_4u_2bed_2st_2pf | 17.6 × 12.2 m | 4 | 12 | 36 | 52 | 3 | 84 | 168 |
| `deckerMixed` | porch_6u_2bed_3st_2pf | 17.6 × 12.2 m | 6 | 16 | 54 | 77 | 4 | 129 | 258 |
| `captains6` | gambrel_6u_2bed_3st_2pf | 18.85 × 12.9 m | 6 | 18 | 54 | 77 | 4 | 144 | 251 |
| `captains2` | gambrel_2u_3bed_2st_1pf | 12.15 × 12.9 m | 2 | 12 | 24 | 32 | 3 | 51 | 98 |
| `captains4` | gambrel_4u_2bed_2st_2pf | 18.85 × 12.9 m | 4 | 12 | 36 | 52 | 3 | 97 | 170 |

Any build in the rig's space generates as well; these seven are the committed set.

## Sections, and who owns each

| Section | Owner | What it carries |
|---|---|---|
| `UNITS` | shell | Per flat: beds, baths, area, resident_slots, interior box, bands, glazed edges, entry (off the vestibule or the landing), `through_plan`, porches, shared access |
| `PORCH` | shell | The stacked street porches, one per storey, roofed and walkable, open-railed or glazed as a sun porch; and the rear service porches the kitchen doors land on |
| `SHARED` | shell | Vestibule and landing per storey, stair hall per storey (`means_of_escape`), the shared laundry at the yard |
| `THRESHOLD` | both | Front door, rear door, every flat entry, every porch and kitchen door, and every interior door |
| `STAIRS` | both | The core flight per storey with exact rise, `void_above` and reserved **landings**; the front steps; and the rear stair, published **per flight** because grade-to-first-floor is not a storey rise |
| `SOLE` | rooms | One polygon per room per storey with `room_height_m`, wet and circulation flags, obstruction `_notes`, stairwell as a real `hole` |
| `INTERACT` | rooms | Every verb with host, level and a tested reach verdict |
| `BLOCKERS` | both | Walls, stairs, furniture, plus the skirt, oil tank, bins and drying yard |
| `ROOF` | shell | Pitch, material, and on the gambrel tier the **widow's walk** — a hatch off the top landing onto a railed deck that is genuinely standable |

## `_excluded` — and the absences that are the point

**No `ELEVATOR`.** A three-decker is walk-up by definition: no shaft, no pit, no overrun.
**No `CORRIDOR`.** Flat doors open off the vestibule or the landing directly, which is what makes
the plan a stair bay rather than a block. **No `LOBBY`** — a 2.95 m vestibule with mail on one wall
is published as a vestibule so nothing reads it as a lounge. No lockers, no basement, no private
stair.

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

Open `Stack Flats Iso.dc.html` and use the GAMEPLAY SIDECAR button, or run `Art/_multiunitKit.js` from a
page with the rigs loaded. Re-run the writer rather than patching a number.
