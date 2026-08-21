## Not a hull, not a vehicle — the gas station (`gasStationRig`)

`gasStationRig.gameplay.json`. The first sidecar for a **place** rather than a thing: one file carrying
**21 pieces** (7 dispenser varieties × its sizes, 4 islands, 3 canopies, 3 signs, 2 storefronts, 2 fill
points), because there is no single "station" object to describe — only the kit a forecourt is
assembled from, plus the plan call that places it. **Generated, never edited:**

    StationIso.gameplayAll({ grades })          // whole kit
    StationIso.gameplay(type, size, opts)       // one piece

Frame as the campers and trucks: metres, 32 px = 1 m, heading-independent, origin at the **ground
centre of each piece's own footprint**, `+z` up. The storefront's front is `+y`.

**The one rule, and why it is in the sidecar too.** A station is a LIST OF GRADE POSITIONS. `HOSES`,
the sign's `ROWS` and the fill cluster's `CAPS` are all generated from that one list, so a station
selling three grades cannot publish four of anything. Grades come from `FuelIso` **by reference**
(gas · diesel · mixed · oil) plus **stove oil**, added by this rig and deliberately the only achromatic
code in the set.

| Section | What it carries |
|---------|-----------------|
| `HOSES` | Per grade position: side, index, grade, outlet and boot points, `reach_m`, `lpm`. The hose itself is **never baked** — `StationIso.serve()` returns a polyline plus honest metres, and slack grows as the pull shortens. |
| `WALK` | The island top (a 0.165 m **kerb**, `step_over`, not a floor), the storefront walkway and service stoop, the store's **roof deck** (inside the parapet face, reached by `roof_ladder`), the fill pad (flush). |
| `SLOTS` | Island bolt-down plates on a 3.30 m pitch — any variety mounts at its own pivot. |
| `CLEAR` | Canopy underside height (4.55 m), deck z, bays, span, and the rectangle it covers. |
| `THRESHOLD` | The storefront entry: **bipart slider**, so `keep_clear` is `null` and the collider is the leaf, which travels inside the wall line. `clear_at_open_fraction` is when 0.80 m of body fits. Doors default SHUT. |
| `SERVICE_DOOR` | Back of house: a single leaf swinging **out**, which unlike the entry *does* own a `keep_clear`. Staff only. |
| `ROOF` | Deck z, parapet z, plant count, `walkable`, and — on the store — `access`: a caged fixed ladder off the service stoop, `cage_from_m` 2.30, stringers cresting the parapet as grab rails. A pay booth gets no ladder and stays `walkable: false`. |
| `SOLE` + `INTERACT` | Written by the **second** rig (see below). |
| `BLOCKERS` | Cabinets, kerbs, bollards, posts, poles, the building, the ice box and the propane cage — each with `height_above_grade_m` and a `treatment` (`flat` / `step_over` / `waist_block` / `wall`). |

`visible_facings` is **computed** with the rasteriser's own camera-facing test
(`-nx·sin(45d) + ny·cos(45d) < 0`), not the trucks' near-side approximation — same intent, this rig's
projector.

**`reach_point` is no longer a request.** It is the first in this set that is *tested*. `StationIso.auditReach()`
runs at bake time over every interactable in every piece and, for each one, marches a 0.22 m body out
along the fitting→point line in 6 cm steps until it clears every blocker at that level — 1.20 m of grasp,
1.90 m for a `read` or a card slot, and no vertical test on a `read` because a price sign needs line of
sight, not arm's length. The fixture a fitting sits on is its **host** and is tested at its true footprint
rather than inflated, because a body stands right up against the cooler it is opening. If the requested
side is blocked all the way the marcher tries the same line reversed, then the four axes; a point that
still cannot clear is published as **`null` with a reason** rather than as a plausible lie. Every point
carries its verdict:

    "reach": { "tested": true, "verdict": "ok|moved|flipped|relocated|no_clear_spot",
               "on": "sales_floor", "dist_m": 0.86, "moved_m": 0, "lift_m": 1.10,
               "body_r_m": 0.22, "arm_m": 1.2 }

and each piece carries a `REACH_AUDIT` tally, rolled up on the file as `reach_audit`. The current bake:
**45 checked, 42 as written, 3 repaired, 0 nulled** — a display window nudged 0.36 m clear, and the cooler
and snack-aisle spots, which faced *into* the fixture they served and were flipped. The
scope is **one piece**: a dispenser is not told the island is under it, so a forecourt still has to be
checked as a whole after `StationIso.plan()` places it.

`_excluded` records the absences a reviewer will look for: **no walkable canopy roof** (no ladder, no hatch,
no parapet to stand behind), **no roof hatch** on the store — the ladder crests the parapet instead — no
walkable deck on the pay booth, no baked hose pixels, nothing interactable on a vent riser, and the
storefront's glass is **opaque by design** — the interior is its own sprite, not something seen through a
window.

### The room — two rigs, one cell (`stationInteriorRig`)

`Art/stationInteriorRig.js` (`StationInterior`) builds the sales floor and writes the storefront's
`SOLE` and `INTERACT`:

    StationInterior.gameplaySections({ size })   // merged into the store piece by gameplayAll()

**Seamless means one cell.** The interior rig measures nothing. It reads `StationIso.shell(size)` — wall
thickness, floor and ceiling height, the entry opening and its travel, the glazing runs — and paints
into `StationIso.cell('store', size)`, the exterior's own quad and pivot. Blit the room, blit the shell
over it, and the doorway lines up to the pixel; an open door shows the room because the shell carries no
masking plane behind it. The **section cut is per facing**: a wall whose outside is camera-facing is not
drawn at all.

Six interactables on the sales floor — `till` (buy) · `cooler` (drinks) · `coffee` (coffee) · `snacks`
(browse) · `hotcase` (food) · `atm` (cash) — each with a `level` of `sales_floor`, and fit-out published
as `SOLE._notes` obstructions with footprint, height above the sole and a treatment: a 0.98 m counter is
a waist block, the 2.10 m gantry is the only wall-height fixture in the room. The room's standing spots
are derived here and **tested by the exterior rig's audit** against this same obstruction list — which is
how the cooler and snack-aisle points were caught facing the wrong way.

The back wall carries the **service door** from the shell block, drawn from the inside with a push bar and
an exit light. Same numbers, other side: the two rigs cannot disagree about where the way out is.

**Interiors ashore do not move.** The boat interiors ride their hull's `rock()`; a building is bolted to
the ground and gets no such parameter. That one law is why this is its own rig rather than a boat-interior
family member.

Stamped twice — `derivedFromRigSha256` (shell bytes) and `interiorDerivedFromRigSha256` (room bytes) —
so drift in either rig is visible without opening the file. Builder page: `Gas Station Iso.dc.html`
(forecourt, the seven machines, grade positions, island configuration, filling reach, the price sign,
the storefront beside its own sales floor, and the download buttons that stamp both hashes).
