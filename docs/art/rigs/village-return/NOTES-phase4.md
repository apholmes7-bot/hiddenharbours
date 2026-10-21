# hh-village-return · phase 4 · dooryards

2026-09-26. The next phase after the shops: the yards on the village plan. The rigs are in `yard-landscaping-kit/`, the boards are in `boards-yards/`, and the review is the top section of `Village Houses Phase 2.dc.html`.

- **`yardIsoRig.js`** (a copy of `Art/yardIsoRig.js`) adds `frame` and `renderLive`. `render()` uses `CoastalPass.light` when it is loaded, so yard pieces match the houses (no dither, no keyline). Beds use native foliage mounds in the live look. `classic:true` draws the old picture.
- **`yardLots.js`** adds `YardLots`:
  - `LOTS`: one entry per building on the plan, with the fence the plan names, the gate, the walk and the dressing.
    - sage cottage, white gable and white farmhouse: picket.
    - school and red saltbox: post-and-rail.
    - harbour gable: stone wall with pillars.
    - cream saltbox: page wire.
    - general store and post office: open forecourt.
  - `plan(name)`: the lot rectangle and every piece, in the building's own model metres, with quarter turns. Rules:
    - The lot is the building's footprint plus margins, running 4.2 m past it on the street side, which is where the entry path leads.
    - The gate sits where that path, carried on, crosses the street edge.
    - The walk runs from the door's approach along the entry path to the gate.
    - Dressing goes in the first free spot in the yard ring, flowers in front and work behind, clear of the house, the porch and the walk.
    - `check` reports whether the door is reachable from the gate and whether every piece has a reachable standing point. All nine lots pass.
  - `compose(name, dir, o)`: a board. The building is drawn with its cast shadow, the pieces are painter-sorted by depth, and each piece faces with the building's quarter turns. Pass `o.night` for the night sky.

## To do on node

- Hand the game `plan()` per lot, placed with the building's facing. The lot sizes are margins round each building, not the plan's drawn outlines, so match them to the plan's own rectangles when it is frozen.
- Add yard-piece verbs (the clothesline, the woodpile, the trap bench) to the station list, as HouseIso does for its dooryard goods.
