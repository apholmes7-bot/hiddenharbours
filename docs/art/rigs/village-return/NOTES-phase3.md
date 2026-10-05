# hh-village-return · phase 3 · shops and the schoolroom

2026-09-26. The owner picked shops next (the general store with Marguerite's flat above it, the post office) plus the school's classroom, and asked for one storefront of each kind so the two differ. The rigs are copied into `shop-building-kit/` from `Art/`; the originals are untouched. Boards are in `boards-shops/`, and the review is the top section of `Village Houses Phase 2.dc.html`.

## Shopfront (`shop-building-kit/shopfrontRig.js`)

- **Live light.** `render()` uses `CoastalPass.light` when it is loaded, as HouseIso does. It adds `frame`, `relight`, `castShadow`, `renderLive` and `lightsOn`. Shop glass burns during opening hours (08:00–18:30). The flat's glass burns while someone is up (06:30–22:30); `occupancy: 'home' | 'away' | 'asleep'` overrides that. The wall lamp burns from dusk. `classic:true` draws the old picture.
- **`CAST`.** The two St Peters shops. Both stand at facing 4 with the door facing S.
  - general store: a mustard false front. New `ffTall` squares off the whole gable and carries a big blank sign board for the game to letter. Striped awning, a stall and an A-frame board on the walk, and a flat above.
  - post office: a blue shingle gable-front with a plate shop window, a scalloped awning, a bracket sign and planters.
- **`placement()`.** The shop door is on the +Y street wall, so the listed facings are 3, 5 and 4, diagonals first.

## ShopInterior (`shop-building-kit/shopInteriorRig.js`)

- **`cutaway: 'section'`** (or `live: true`) draws with the live light. Dropped walls stand to the sill, 0.9 m above that storey's floor.
  - The stubs carry the shop's own siding outside (pass the Shopfront options as `front`), a pale cap and the plinth.
  - The street wall keeps the door's gap and threshold.
  - The flat's section removes the roof and stands the shop's front and walls whole beneath, with its glass and door.
- **`room: 'flat'`** is now furnished as a home by `CoastalPass.cottagePlan`: kitchen at the back by the stair door (range, sink, icebox, table and two chairs), bedroom at the front (bed, dresser, sea chest), with a partition between them. Every piece is reachable. `flatPlan(opts)` returns the plan, and `beds` sets how many beds there are. `rooms: false` keeps the old fixture layout.
- The shop floors keep their trade layouts (store: shelving, counter, gondolas, stove, barrels, sacks, crates; post office: wicket, counter, benches, table, stove).

## Schoolroom (`houses-kit`)

- InteriorIso `program: 'school'` (a shell with `belfry: true` implies it). It is one room with no stair: two rows of desks and benches facing the teacher's desk and chair at the far gable, an aisle down the middle, a stove, a bookcase and the long clock. Every desk is reachable.

## To do on node

- Add the shops kit to the manifest and SHA sums, and run the building checks on the two CAST shops. Their door visibility and approach have not been measured by a checker yet.
- A blackboard on the schoolroom wall, and pigeonholes behind the post office wicket, are art asks.
