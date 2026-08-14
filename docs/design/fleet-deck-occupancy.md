# Fleet deck occupancy — slot budgets for the rig-pack hulls

**What this is.** The deck-occupant **slot budget** of the same **twenty-three** incoming hulls
`fleet-flotation.md` floats: the 18 lobster-boat variants, the two coast-guard RHIB builds, the
reshaped sport skiff and the two sport fishers. One number per hull — how many things stand on her
deck at once and therefore need the hull to hide them per pixel — measured against the one capacity
the whole fleet shares.

**Why it needs measuring at all.** `IsoFacetHullRenderer.DeckOccupantSlots` is **12**, a single
compile-time constant for every hull in the game (the shader holds the same literal as
`HH_DECK_OCCUPANT_SLOTS`, and `DeckOccupantSlotTests` fails if the two disagree). It is not per-hull
data and there is no asset field for it. So "a budget for hull X" means: *what does X demand, and
does the shared 12 cover her?* A hull that demands more does not crash and does not corrupt — the
13th claim is **refused loudly** and that occupant draws un-occluded, which is the pre-#461 picture
(`IsoFacetHullRenderer.ClaimSlot`). It is a visual regression, not a fault, and it is the failure
this document exists to see coming.

**Status (2026-08-14).** Measured and pinned by `FleetDeckOccupancyTableTests`. Like the flotation
table, **nothing here is on an asset**, and for the same reason: `lobsterBoatVariantsIsoRig.js`,
`zodiacIsoRig.js`, `sportSkiffMk2IsoRig.js` and `sportFisherIsoRig2.js` are all in
`HullMeshFleet.NotHulls`, so there is no `BoatDeckGearDef` to write a mount onto. There is also no
field to write to even once they bake — the capacity is global. The table is therefore a
**measurement with a verdict**, and the verdict is what the bake PRs and the deck-loop lane read.

**Headline.** ✅ **All 23 hulls fit inside 12 as the shipped pattern actually dresses a deck.**
⚠️ **20 of the 23 overflow if every anchor their own rig defines is dressed** — the 18 lobster
variants by 3, the sport fisher by 5 and 10. §5 is that finding and what to do about it.

---

## 1. What claims a slot, and what does not

The rule is not invented here; it is the shipped one, from `DeckOccupantSlots.cs` and the two PRs
that built the seam.

| | claims a slot | why |
| --- | --- | --- |
| a **sprite** standing on the deck | **yes, one** | it is sorted above the hull's whole-object slot, so only the hull's own facet pass can hide it — that is the entire reason the seam exists |
| a **mesh fitting** built into the hull | **no** | it rides the hull's own z-buffer for free (#481) |
| a **stack** of pots | **yes, exactly one** | every tier stands on one ground point, so they share a view depth and one band hides all of them (**#484, finding 2** — claiming per pot asks a 12-slot hull for 13) |
| a **nav light / masthead** | **no** | precedent: the committed lobster boat carries `navMounts` incl. `mast` and #481 counted none of them |
| a **cleat, painter, stern eye, bollard** | **no** | tie-offs, modelled in the hull; the gameplay sidecars already record them as a separate section from anything that stands on deck |
| an **outboard on the transom** | **no** | it hangs off the transom, not on the deck, and it is a separate layer |

**Only things that can be aboard at the same time count.** The number wanted is the worst
simultaneous beat, not the sum of everything a hull could ever carry across a day.

## 2. The method, and the anchor that proves it

```
demand = crew + deck furniture + stack surfaces + items in play  (+ dressed rig anchors, §5)
```

Each term is read from the hull's own rig, or from the kit that serves her hull type:

- **crew** — the rig's own seat/station table (`seatsOf`, `anchors.helm`, `TOWER_STATION`, `CHAIR`,
  `MEZZ`), or for a working hull the deck-loop kit's **two hands**
  (`deck-loop-kit/README.md:108-109` — "skipper starboard at the hauler, sternman port at the bench
  and the board").
- **deck furniture** — the four kit stations both shipped working hulls carry:
  `HaulerStation / BandingTable / ChopBoard / BaitBin`, 4 `Mounts` each in
  `LobsterBoatDeckGear.asset` and `CapeIslanderDeckGear.asset`. (#484 dropped the bait **box** from
  both — a boat carries a bin *or* a box, not both.)
- **stack surfaces** — one slot per surface that takes pots, never one per pot. The shipped hulls
  carry two apiece (cockpit sole + washboard cap; `TrapIso.CAPS` gives lobster `deck:5 washboard:2`,
  cape `deck:3 washboard:2` — the *capacities* differ, the *surface count* does not).
- **items in play** — the pot being worked and the fish tray, 2, from #481's beat table.

### The anchor is exact

`fleet-flotation.md` §3 established that the variants rig's `standard/*/northumberland` **is** the
committed `hullmesh.lobster_boat_iso` — identical offsets table, `L`, `DECK` and `RAKE`. So the
method has to reproduce her measured demand, and #481 measured that at **10** on the shipped hull:

| what | count | source |
| --- | --: | --- |
| hands | 2 | deck-loop kit's twelve-beat shift table |
| working furniture | 4 | `LobsterBoatDeckGear.asset` — 4 `Mounts` |
| trap-stack surfaces | 2 | `TrapIso.CAPS.lobsterBoat` — deck + washboard |
| the pot in play | 1 | the beat table's "the pot" column |
| the fish tray | 1 | beats 6–8, "walks, carrying tray" |
| **total** | **10** | |

The method run over the variants rig returns **10** at that cell. That is the one falsifiable claim
in this document and `TheAnchor_ReproducesTheShippedLobsterBoatsMeasuredDemand` pins it.

## 3. The table

`committed` is the demand as the shipped pattern actually dresses a deck. `ceiling` adds every
further anchor the hull's own rig defines but nothing places yet (§5).

| hull | LOA | crew | furniture | stacks | items | **committed** | dressable | **ceiling** |
| --- | --: | --: | --: | --: | --: | --: | --: | --: |
| lobster inshore × 3 regions × 2 styles | 8.6 | 2 | 4 | 2 | 2 | **10** | 5 | **15** |
| lobster standard × 3 regions × 2 styles | 12.0 | 2 | 4 | 2 | 2 | **10** | 5 | **15** |
| lobster offshore × 3 regions × 2 styles | 14.6 | 2 | 4 | 2 | 2 | **10** | 5 | **15** |
| zodiac hurricane | 7.0 | 4 | 0 | 0 | 0 | **4** | 0 | **4** |
| zodiac frc | 6.4 | 1 | 0 | 0 | 0 | **1** | 0 | **1** |
| sport skiff v2 | 7.0 | 1 | 0 | 0 | 0 | **1** | 3 | **4** |
| sport fisher convertible | 16.2 | 4 | 0 | 0 | 0 | **4** | 13 | **17** |
| sport fisher skybridge | 27.4 | 4 | 0 | 0 | 0 | **4** | 18 | **22** |

**All 18 lobster variants carry one row.** Their anchor sets are byte-identical across every
`(size, style, region)` cell — measured, not assumed: `helm 1 · hauler 1 · tubs 5 · mast 1`, 2 deck
polygons, 2 washboards, 3 cleats, for all eighteen. Size scales the metres (LOA 8.6 / 12.0 / 14.6,
`DECK` 0.44 / 0.50 / 0.55) and style changes the roof, neither adds a place to stand — the same
finding `fleet-flotation.md` §3 recorded for the planking.

### Provenance, per family

- **lobster variants** — `lobsterBoatVariantsIsoRig.js`: `tubSlots()` line 790 (5 cockpit crate
  anchors), `helmSeat` 781, `haulerMount` 785, `navMounts` 798, `anchors()` 810, `gameplayGeometry()`
  824. The 18 cells enumerated through the rig's own `SIZES × STYLES × REGIONS`.
- **zodiac** — `zodiacIsoRig.js`: `seatsOf(B)` line 626. Hurricane has `B.seats`, giving **4** jockey
  seats at `(±0.34, 0.115·L−0.62 | 0.115·L−1.42)`; FRC has none, giving the single console seat.
  `HELM` (line 621) is `{0.34, 0.115·L−0.62}` — **exactly jockey seat 0 on the hurricane**, verified
  numerically, so the helm is not a separate station and is not double-counted. She carries no deck
  furniture, no stack surface and no crate anchor at all.
- **sport skiff v2** — `sportSkiffMk2IsoRig.js`: `TUBS` line 1007 (3), `HELM` 999, `PILOT` 1013.
  `HELM` is the leaning-post cushion top and `PILOT` is the sole in front of it — one station, one
  person, counted once. `MOUNTS` (992) are the two outboards, excluded per §1.
- **sport fisher** — `sportFisherIsoRig2.js`: `RODS` 867, `TUBS` 878, `MEZZ` 885, `CHAIR` 886, per-build
  specs at 961-964 (convertible) and 1032-1035 (skybridge). Four crew stations — `HELM`,
  `TOWER_STATION`, `CHAIR`, `MEZZ` — at four separate heights (convertible 4.80 / 7.10 / 1.45 / 2.17 m),
  which is the sportfisher scene: captain in the tower, angler in the chair, hands in the cockpit and
  on the mezzanine.

## 4. What the count costs, and what it does not

Nothing in this document moves `DeckOccupantSlots`, so nothing here changes the id budget. Restating
it so the next reader does not have to re-derive it: each hull reserves `1 + 12` of the 255 ids the
facet alpha can carry, leaving **19 simultaneous mesh hulls**.

**Adding 23 hulls to the catalogue does not touch that 19.** The limit is on hulls *registered in a
scene at one time*, not on hulls in the fleet — a harbour of a dozen is the roadmap's largest scene.
A fleet of 34 rigs and a headroom of 19 simultaneous are not in tension, and no bake PR needs to
widen anything on this account.

## 5. ⚠️ The finding: 20 of 23 overflow at the ceiling

Every hull's committed demand fits. But three families' rigs anchor **more places to put something
than the shipped pattern currently dresses**, and if a later lane dresses them all, the budget goes:

| hull | committed | + dressable | ceiling | over 12 by |
| --- | --: | --- | --: | --: |
| each lobster variant | 10 | 5 crate anchors (`tubSlots`) | 15 | **3** |
| sport fisher convertible | 4 | 4 tubs + 9 rod mounts | 17 | **5** |
| sport fisher skybridge | 4 | 5 tubs + 13 rod mounts | 22 | **10** |

**The lobster overflow is the interesting one**, because it is not hypothetical arithmetic about a
hull nobody has built: the *committed* lobster boat has those same 5 crate anchors today
(`lobsterBoatIsoRig.js:528-529`, `TUBS`, "cockpit crate anchors") and the current loop simply does
not place anything on them. The moment a lane fills the cockpit with bait crates, the shipped hull
and all 18 variants alike go to 15 and **three claims are refused**.

That is the same number, by a different route, that #481 predicted for a different decision: *"if
handoff 3 gives every pot in a stack its own depth, the worst beat becomes 15 and three claims will
be refused, loudly."* Two independent ways to reach 15 on one deck is the signal that **12 is sized
for the loop as it exists and has no room for a second dressing pass**.

### What this document recommends, and what it does not do

**It does not raise the constant.** Raising 12 shrinks the 19-hull headroom, it is a Core + shader
change owned by `lead-architect` and `gameplay-systems`, and nothing in the fleet needs it *today* —
every committed demand fits. Changing it on a measurement of work nobody has scheduled would be
scope creep (CLAUDE.md rule 8).

**The levers, cheapest first, for whoever hits this:**

1. **Crates and rod-holders are strong candidates for mesh fittings, not sprites.** A mesh fitting
   costs no slot at all (§1). A bait crate that never moves and a rod holder that is part of the
   covering board are exactly the case the mesh path is for; only a thing that is picked up, carried
   or worked needs to be a sprite. This alone clears all three overflows.
2. **A row of crates on one surface is one occupant**, by the §1 stack rule — the same argument #484
   made for pots. Five crate anchors in two rows are plausibly two slots, not five.
3. **Raise `DeckOccupantSlots`** — last, deliberately, and only with the id-budget trade in §4 on
   the table. It is one constant in two places and `DeckOccupantSlotTests` fails if they disagree.

**⚠️ One thing measured and deliberately not claimed.** The sport fisher's rocket-launcher rods sit
at z 5.99 / 8.42 m — **below** her tower station (7.10 / 9.85) and well below her masthead
(9.50 / 11.95). Tower structure therefore stands above and around them and *can* occlude them from
some facings, so they cannot be waved off as "too high to need a slot." That was the obvious
argument for shrinking her ceiling and the geometry does not support it; it is recorded here so
nobody spends the measurement again.

## 6. What is not decided here

- **Crew counts are station counts, not a staffing rule.** Four stations on a sport fisher means she
  *can* carry four; whether the game ever puts four aboard is an economy/world call. The budget has
  to assume the hull's own maximum, which is what is tabulated.
- **No hull-type loop is designed here.** A sport fisher does not run the lobster stern-deck loop,
  so her furniture/stack/item terms are 0 — not "unmeasured". If a big-game fighting loop is ever
  built, it lands its own furniture and this table is the thing it must be re-measured against.
- **Nothing here touches the bake.** No `HullMeshDef`, no `HullMeshFleet.Hulls` entry, no sidecar,
  no `BoatDeckGearDef`. Those belong to the bake arc (PRs 2–3) and are Unity-gated.
