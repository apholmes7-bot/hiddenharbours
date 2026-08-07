# Hidden Harbours — the larger world map

> **Status: RATIFIED IN PART — 2026-08-07, the Hillsborough Bay rulings.** The owner attached the
> real chart of Hillsborough Bay, PEI, and ruled the home world onto it (chat, 2026-08-07). What is
> ruled below is marked **RULED**; §7 lists what is still open. Earlier drafts of this document
> proposed an abstract "home cluster + eastern arc"; that framing is **superseded** by the bay.
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon — amended for these
> rulings in the same PR that rewrote this document) and
> [`world-and-regions.md`](world-and-regions.md).
>
> The delightful accident this ruling surfaced: the canon was already this chart in disguise. The
> **real** Nine Mile Creek Wharf and the **real** St Peters Island are neighbours in the real
> Hillsborough Bay, and the game had placed them next to each other before anyone noticed.

---

## 1. The world is Hillsborough Bay **(RULED)**

The home world is a variant of Hillsborough Bay, Prince Edward Island — red sandstone, shallow
water, mud and sand bottom, a working coast. Sail east across the strait and the coast gets harder:
that gradient (P1, P2, P5 stacked into geography) survives from every earlier draft; it just has a
real bay for a home end now.

```
                the channel inland
                       |
              FINNIGAN'S LANDING          (the island's main city — commercial hub,
                       |                   ALL vessel sizes, late-game destination)
        ~ north shore: coves and points ~
                       |
   NINE MILE CREEK     |
   (town + wharf)      |         EAST POINT
        |          GOVERNORS     (commercial fishing port —
        | the bar   ISLAND        "almost mirrors Nine Mile Creek")
        |  610 m   (uninhabited,
     ST PETERS      rocks all round)
     (home island)     |
        ~ home grounds: fish · mussels · lobster · crab ~
                       |
      ==================================== the strait
                       |
                  NEW SCOTLAND            (across the strait SE — the cargo run,
                                           a LONG sail, larger vessels primarily)
```

Compass facts, all **RULED** from the chart:

- **St Peters Island** sits in the bay. The open water to its **north, east and south** is the home
  fishing ground — local fish, mussels, lobsters and crabs.
- **Nine Mile Creek is on the west shore, further north than St Peters.** Crossing the sandbar
  westward you land with **shoreline on the south**, then the shore **runs north** until you reach
  the town. *(The built A-2 mainland already matches: `NineMileCreekMainland` authors the coast
  north–south, fields west, the crossing leaving ESE with St Peters offshore to the south-east.
  Verified 2026-08-07 — no rework.)*
- **The north shore** (north of St Peters' water, north-east of Nine Mile Creek) is coves and
  points, as the chart draws them. Far enough along it, a **channel heads inland** to the island's
  main city.
- **Governors Island** lies mid-bay: **uninhabited, rocks all around** — the bay's standing hazard,
  square in the way of any sail from home water toward East Point.
- **The east side of the bay** almost mirrors Nine Mile Creek: land, a working shore, and the
  commercial fishing port — **East Point**.
- **Across the strait to the south-east** — a long sail — lies **New Scotland**, the cargo
  destination, used primarily by the larger cargo vessels.

---

## 2. The home bay — what stands and what it holds

### 2.1 The two built land scenes

| Scene | Extent | Status | Identity |
|---|---|---|---|
| **St Peters** | 760 × 520 m | **built** | The home island: clam flats, the village, the crossing leaves west. Dock ~0.6 m. |
| **Nine Mile Creek** | 760 × 560 m | **built** (owner's scene rebuild pending) | The mainland: small town + small wharf, its half of the crossing. Berth ~1.6 m. |

**Nine Mile Creek's town, RULED contents:** a fish buyer, a bait seller, supplies, a restaurant, a
post office — "everything needed for a small town and a small wharf." Its wharf is **home to
approximately 16 lobster boats** plus a **new smaller class of boat for mussel fishing** (and
lobster boats can be **outfitted** to fish mussels — the fishing role is gear on a hull, not the
hull class). The north wall was sized at ~14 berths, so a 16-boat fleet fills it past full —
rafted pairs and moorings — which is what a working wharf should look like.

### 2.2 The three home water scenes **(RULED "for now")**

The bay partitions into **three water scenes**; the channel to the city and the strait crossing are
**passages, not scenes**, until their phases arrive.

| Scene (working name) | Water | Character |
|---|---|---|
| **The west water** | the bar, St Peters' lee, the run up to Nine Mile Creek | The first sail. Sheltered, forgiving, the dory's water. |
| **The mid-bay** | the home grounds around St Peters | Fish, mussels, lobster, crab — with **Governors Island** as its hazard. |
| **The east water** | Governors Island across to East Point | The working run: longer, more exposed, the lobster boat's commute. |

Extents to be derived by time-to-cross in each scene's gating boat
([`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md) §1.3) when the water-scene
template is built — not picked in advance.

**Offshore seabed, RULED:** open-water scenes carry **no painted seabed** — they are deep enough
that the bottom is never visible. They still carry **height-map and terrain data everywhere**,
because the depth sounder and the fish finder read real depth (the one-height-map law). Paint is
a *look*; data is the *world*. This also removes the paint-resolution ceiling from open-water scene
size entirely — only the 6–10-minute crossing rule constrains them now.

### 2.3 The lobster-boat ceiling is bathymetry **(RULED: physics, never a rule)**

| Harbour | Berth depth | Admits | Turns away |
|---|---|---|---|
| St Peters dock | ~0.6 m | dory · punt · skiffs | everything larger |
| **Nine Mile Creek** | **~1.6 m** | + lobster boat 1.30 · Cape Islander 1.40 | side dragger 2.90 |
| **East Point** | working-port depth (TBD, ≥ NMC) | the fishing fleet | — |
| **Finnigan's Landing** | deep (dredged) | **everything** | — |

The owner ruled the dragger exclusion stays **emergent** (`waterLevel − bed > draught`): on NMC's
±2.2 m tide a dragger *can* nose in near high water (~30 % of the cycle) but can never lie there —
the ebb grounds her. That loophole is accepted as a story, not patched as a bug. Companion vision,
logged: **grounding hulls should ride the ground and keel over**, exposing the underside the 3D
iso rigs already model — flat-bottomed hulls (dory, punt) sit upright as they dry; round-bilged
hulls lie over. Per-hull resting heel is data.

---

## 3. The ports **(names RULED)**

The earlier draft's single eastern "Market Port" is **superseded** — its job splits three ways, and
**Port Greywick is retired as a place name** (canon amendment in this PR; region *ids* in code and
saves stay stable per ADR 0009 — display names are a seam, ids are append-only).

| Port | Where | What it is | When |
|---|---|---|---|
| **East Point** | east side of the bay | The **commercial fishing port** — the fleet, the fish trade at scale, a working shore that mirrors Nine Mile Creek. | mid-game |
| **Finnigan's Landing** | up the channel inland, off the north shore | The island's **main city** — the commercial hub **all vessel sizes can reach**. Business, buildings, the big money. A **late-game destination** — but the player may end up there early *for various reasons*, to see the potential (larger vessels, businesses, building ownership) without being granted it. | late, glimpsed early |
| **New Scotland** | across the strait, south-east | The **cargo destination** — a long sail, used primarily by the larger cargo vessels. The freight game's far end. | late (freight tier) |

**Shipyards, RULED: multiple — one of varying sizes at each port.** Boats are bought, sold and
upgraded at yards, and the yard's size sets what it can handle: Nine Mile Creek's is small (the
damaged-dory beat and small-boat work), East Point's serves the working fleet, Finnigan's Landing's
is the big yard. (Which tiers each yard buys/sells/upgrades is data to author when the shipyard
system is built.)

**The far arc** (the canon's Banks, Ironbound, the Smother, the Drownded Lands, Fundy Rips) is
**not re-placed by these rulings** — presumably east and offshore past New Scotland, but that
geography is deliberately unruled. See §7.

---

## 4. What has to be built for a multi-scene world to work

Ordered by what blocks what.

1. ~~**Per-passage arrival points.**~~ **DONE** — shipped as PR #456 (2026-08-07): a passage names
   its own arrival point; the fisher lands where they walked.
2. **⛔ `MapGraph` as data.** Canon §9.3 specifies a `MapGraph` ScriptableObject holding region
   nodes and gated edges, read by both the chart UI and the router. It **does not exist**: today the
   edges are hard-wired as `RegionPassage` components pointing at `RegionDef`s. A bay of five-plus
   scenes with three or four doors each needs one place that answers "where can I go from here."
3. **A tide-continuity rule, enforced.** Adjacent regions sharing terrain must share a tide profile
   (the bar taught this the hard way). Worth a `RegionValidation` check rather than a comment.
4. **Seams in featureless water.** Every seam in open water clear of hazards — never on a landfall
   or in a channel the player is threading. (Governors Island's rock fringe is exactly what a seam
   must stay away from.)
5. **A water-scene builder template.** The three bay scenes are ~90 % identical (sea plane, height
   data, region anchor, passages, camera bounds, weather). One parameterised builder, three small
   plans.
6. **Set-a-course, eventually.** An M3 concern; the map graph (item 2) is its prerequisite.

---

## 5. Names — resolved

The owner ruled for **variants of real PEI names** (2026-08-07) and then named the new places
directly. Canon names locked in `vision-and-pillars.md` §5.3, as amended:

| Place | Name | Status |
|---|---|---|
| Home island | **St Peters Island** | canon, built — a real bay name |
| Mainland town + wharf | **Nine Mile Creek** | canon, built — a real bay name |
| Mid-bay hazard island | **Governors Island** | **RULED 2026-08-07** — real bay name; uninhabited, rocks all round |
| East-shore fishing port | **East Point** | **RULED 2026-08-07** — replaces Port Greywick |
| The main city | **Finnigan's Landing** | **RULED 2026-08-07** |
| The cross-strait cargo port | **New Scotland** | **RULED 2026-08-07** |
| ~~Port Greywick~~ | — | **RETIRED 2026-08-07.** Historical mentions in ADRs and older docs stand as history; player-facing strings (`WorldStrings.OnboardBuyDory` / `OnboardRepairDory`, `OnboardingDirector`) are part of the already-flagged M2 onboarding rework; region ids stay stable (ADR 0009). |
| The three water scenes | working names only (§2.2) | names owed when the scenes are built |
| Far-arc regions | canon names stand (Banks, Ironbound, Smother…) | geography unruled — §7 |

---

## 6. Order of work

| Step | What | Why here |
|---|---|---|
| **0** | Nine Mile Creek Phase B (wharf dressing from the ISO kits) + the owner's scene rebuild | The starter region has to be good before anything is copied from it |
| **1** | **The west water** — the first water scene | Proves the water-scene template on the easiest case; it is the first sail, which the owner should play before more water is built |
| **2** | **Owner plays the home arc** — St Peters → the bar → Nine Mile Creek → the west water | The go/no-go gate: is it *fun* (roadmap §6) |
| **3** | **`MapGraph`** (§4.2) | Before the door count gets away from us |
| **4** | **The mid-bay + the east water** — Governors Island, the home grounds, the run to East Point | The rest of the bay; the mussel fishery's water arrives with it |
| **5** | **East Point** | The first port beyond home; the working fleet's world |
| **6** | The channel + **Finnigan's Landing** (glimpse-grade first) | The late-game window the owner asked for — show it before it is playable |
| **7** | The strait + **New Scotland** | The freight run |
| **8** | The far arc | Unruled geography; rule it when the bay is real |

---

## 7. Open questions for the owner

1. **Where does the Home Cove (canon: Coddle Cove — the cottage you row the dory to) sit in the
   bay?** The arc says: buy and repair the dory at Nine Mile Creek, row her *home*. The chart offers
   candidates (a cove on the west shore south of the bar; a cove on the north shore) but this is not
   ruled, and it decides the dory's first real voyage.
2. **The far arc's geography** — the Banks, Ironbound, the Smother, the Drownded Lands, Fundy Rips
   keep their canon names and jobs, but where they lie relative to New Scotland is unruled.
3. **East Point's berth depth** and how far its "almost mirrors Nine Mile Creek" goes — same kit,
   bigger fleet, or its own look?
4. **The three water scenes' real names** — owed when they are built; the owner's PEI-variant style
   applies.
5. **Winter** — deferred 2026-08-07 ("that can wait for now").
