# Hidden Harbours — the larger world map

> **Status: PROPOSAL, nothing built.** Written for the owner's 2026-08-06 ask: *"St Peters is a scene,
> Nine Mile Creek is a scene, surrounding St Peters should be multiple water scenes — let's create a
> plan for a larger world map. I want it closer to my home province in design. The other regions will be
> recreated and renamed once we have a decent starter region, which will likely stop at the lobster boat
> in terms of progression. We will add other ports further east which will be used for the trawlers and
> will represent closer to Newfoundland, Nova Scotia, New Brunswick."*
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (**CANON — region names are
> LOCKED there**) and [`world-and-regions.md`](world-and-regions.md). This document proposes; §7 says
> exactly what has to be ratified before any of it can be built, and in what order.
>
> **⚠ Nothing here renames a canon region.** Renaming a locked name is an owner decision recorded in
> canon *first*, then in an ADR, then in code (CLAUDE.md rule 1 + §7). §5 lays out the mapping so the
> owner can rule on it in one sitting; until he does, every canon name stands.

---

## 1. The idea in one breath

**The Maritimes already contain the difficulty curve.** Prince Edward Island's south shore is the
gentlest working coast in Atlantic Canada — red sandstone you could climb, shallow bays, barachois
ponds, farm fields running down to the water, wharves that dry out under their own fleet. Nova Scotia's
Atlantic shore is granite and surf. The Bay of Fundy is a tide that runs like a river. Newfoundland is
cliff, fog, and the open Atlantic.

So the owner's two asks — *"closer to my home province"* and *"other ports further east for the
trawlers"* — are the same ask. **Sail east and the coast gets harder.** That is P1, P2 and P5 stacked
into geography that already exists, and it costs nothing to author because the world was already
designed as a gradient from sheltered inshore to lethal offshore (`world-and-regions.md` §2.1). This
proposal keeps every canon structure and re-skins the *near* half of it as PEI.

```
          ~ the open Atlantic ~
                                            THE SMOTHER  ·  ·  ·  (fog, optional)
                                                   ·
   THE OUTPORT COAST ....... THE BANKS ....... THE OUTER GROUNDS
   (Newfoundland: cliff,     (offshore:              (procedural, endless)
    storm, the outer         dragger grounds)
    grounds)                      |
        \                         |
         \                  THE NARROWS  (Fundy: tide as a wall — the gate east)
          \                       |
           +---------- THE MARKET PORT  (Nova Scotia: the auction, the yard,
                          |               the 6 m dredged berth, the shipwright)
                          |
   ======================= THE STRAIT ========================   <- the LOBSTER-BOAT CEILING
                          |
                     THE BAY   (open home water)
                    /         \
        THE ROADS  ---- ST PETERS ---- THE SHOAL GROUNDS
        (sheltered)      (island)       (reef + flats)
              \            ||  <- the tide-gated crossing
               \           ||
            NINE MILE CREEK (the mainland: the working wharf, the town)
                    |
              THE HOME COVE  (the cottage you sail the dory to)

        <---- Prince Edward Island: shallow, red, sheltered, farmed
                        Nova Scotia / Fundy / Newfoundland: deep, hard, cold ---->
```

---

## 2. The home cluster — six scenes, and it caps at the lobster boat

Two land scenes exist; four water scenes are proposed. **Sizes are derived by time-to-cross in the boat
each is gated to** ([`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md) §1.3), not
picked.

| # | Scene | Kind | Extent | Gating hull | Cross | Identity |
|---|---|---|---|---|---|---|
| 1 | **St Peters** | land | 760 × 520 *(built)* | on foot | 2:30 | the home island; clam flats; the crossing leaves west |
| 2 | **Nine Mile Creek** | land | **760 × 560** *(this slice)* | on foot | 3:06 N–S | the mainland: working wharf, the town, the crossing lands here |
| 3 | **The Roads** | water | **700 × 700** | punt 2.32 m/s | 5:02 | sheltered water between island and mainland; your first sail |
| 4 | **The Shoal Grounds** | water | **900 × 900** | console skiff 3.90 | 3:51 | the reef-and-flats water: shellfish, grounding, tide pools |
| 5 | **The Bay** | water | **1100 × 1100** | Cape Islander 4.20 | 4:22 | open home water; the run to the cove; weather starts to matter |
| 6 | **The Home Cove** | land | 520 × 400 | dory | 2:53 | the cottage and its wharf — the home you *arrive at* |

Plus the corridor that closes the cluster:

| 7 | **The Strait** | water | **1200 × 700** | lobster boat | 4:46 | the way east — and **the ceiling** |

**Every one of them lands at 20–27 screens across**, which is the sanity check the sizing doc noticed
falls out of honest arithmetic. Nothing here is a new kind of thing: a water scene is a sea plane, a
painted seabed, a couple of authored hazards and a passage — the sizing doc's §3 already establishes
that extent is nearly free and that the real cost is authoring, which for open water is small.

### 2.1 Why *multiple* water scenes and not one big one

The owner asked for several, and he is right, for three reasons that are not aesthetic:

1. **They are different places, and difficulty is per-place.** The Roads are forgiving; the Shoal Grounds
   will hole your hull at low water; the Bay has weather; the Strait has current. One scene would have
   to be uniformly as dangerous as its worst corner or as safe as its best.
2. **Painted seabed is per-region.** One 3000 m scene at 2 px/m is a 6000² texture — 36 MB and over the
   4096 cap. Four 900 m scenes at 2 px/m are 3.2 MiB each. The split is what keeps the seabed paintable
   at inshore resolution at all.
3. **Camera bounds, tide profile and region state are all per-region seams that already exist.** Four
   scenes cost four `RegionDef`s and four builders; one scene costs a new streaming system.

### 2.2 The lobster-boat ceiling is bathymetry, not a rule

The owner's ceiling enforces itself, and it needs no new system:

| Harbour | Berth depth | Admits | Turns away |
|---|---|---|---|
| St Peters dock | ~0.6 m | dory · skiff · punt · console skiff | everything larger |
| **Nine Mile Creek** | **~1.6 m** | + **lobster boat 1.30 · Cape Islander 1.40** | **side dragger 2.90** |
| The Home Cove | ~1.6 m *(proposed, to match)* | as above | as above |
| **The Market Port** *(east)* | **6 m dredged** | **everything, including the dragger** | — |

**The dragger you save up for has nowhere at home to lie.** She can cross the Strait and berth in the
east, and that is the whole point: you feel your own promotion in where you are allowed to tie up. The
mechanism is emergent already (`waterLevel − bed > draught`), so the ceiling costs one painted number
per harbour.

---

## 3. The eastern arc — where the trawlers live

Everything east of the Strait is the *rest* of the Maritimes, and it maps onto the canon's existing
slots without inventing new ones.

| Order | Region | Real-coast flavour | What it is for | Gate |
|---|---|---|---|---|
| E1 | **The Market Port** | Nova Scotia — a real commercial harbour | The auction, the **shipwright's yard**, the chandlery, the chart shop, the processing plant, the 6 m dredged berth. **This is where the market-town job goes** now that Nine Mile Creek is a working wharf. | Cape Islander / lobster tier + story |
| E2 | **The Flats** | PEI/NS head-of-bay | The walkable-seabed flats; spring-low secret runs | tide table |
| E3 | **The Narrows** | Bay of Fundy | Tide as a wall; slack-water transit; the graduation gate | capable hull + nav skill |
| E4 | **The Banks** | offshore | Dragger grounds; industry-scale fishing; overnight steaming | side dragger |
| E5 | **The Outport Coast** | Newfoundland | Cliff, fog, storm; rare and legendary fish; the lighthouse keeper | stern trawler + weather skill |
| E✦ | **The Fogbank** | offshore | Navigate by instrument and sound; the uncanny | late instruments |
| E⚓ | **The Lanes** | — | Freight, contracts, the fleet | freighter tier + business |

**Nothing in that table is new canon.** It is canon §5.3's slots 3–7 + ✦ + ⚓ with a coastal identity
attached to each, plus the one genuine structural change the 2026-07-25 ruling already forced: **the
market town is no longer Nine Mile Creek and needs a home.** E1 is that home, and it closes the
outstanding *"where does the shipwright's yard live?"* question at the same time.

---

## 4. What has to be built for a multi-scene world to work

Ordered by what blocks what. The first two are small and they block everything.

1. **⛔ Per-passage arrival points.** `RegionAnchor` exposes ONE `DisembarkPoint`, and
   `RegionTravelCoordinator.ApplyArrival` teleports the player to it on *every* arrival. That is correct
   for a region with one door and wrong the moment a region has two — Nine Mile Creek already has a
   wharf you sail into and a bar you walk in across, and every water scene will have three or four
   seams. **Until this lands, a multi-door region drops the player at the wrong door.** Small change,
   App/Core, `lead-architect`'s call. *(Flagged in this slice; the mainland's walk-in arrival point is
   already authored and waiting for it.)*
2. **⛔ `MapGraph` as data.** Canon §9.3 specifies a `MapGraph` ScriptableObject holding region nodes and
   gated edges, read by both the chart UI and the router. It **does not exist**: today the edges are
   hard-wired as `RegionPassage` components pointing at `RegionDef`s. Twelve scenes with three or four
   doors each is 40 hand-wired triggers with no single place to ask "where can I go from here."
3. **A tide-continuity rule, enforced.** *Adjacent regions that share a piece of terrain must share a
   tide profile.* This slice discovered it the hard way: Nine Mile Creek ran a ±0.8 m tide and St Peters
   ±2.2 m, and a sandbar spanning that seam would have been dry on one side and flooded on the other at
   the same instant. As the world grows this stops being a two-region coincidence and becomes a law —
   worth a `RegionValidation` check rather than a comment.
4. **Seams in featureless water.** A scene load fires best where nothing is happening. Put every seam in
   open water clear of hazards, never on a landfall or in a channel the player is threading.
5. **A water-scene builder template.** The four water scenes are ~90% identical (sea plane, painted
   seabed, region anchor, passages, camera bounds, weather). One parameterised builder, four small
   plans — not four 1200-line files.
6. **Set-a-course, eventually.** Twelve scenes hand-sailed is romantic at the dory and a chore at the
   freighter. Canon already allows fast travel once a route is discovered; that is an M3 concern, but
   the map graph (item 2) is its prerequisite, which is another reason to do item 2 early.

---

## 5. Names — the owner's call, laid out for one sitting

Canon §5.3 **locks** region names. The owner has said the other regions "will be recreated and renamed."
That is a canon amendment, and it should be made once, deliberately, rather than drifting.

**The recommendation is to keep the fictional frame and re-skin the geography.** The world is the
*Sablewick Banks*, "a small fictional archipelago off Atlantic Canada" (canon §5.1) — which is already
exactly what this plan describes; it just wasn't drawn as PEI. Keeping it fictional means real place
names never constrain the map, and the vernacular canon already asks for (*sunkers, rips, drownded,
barachois*) does the work of place.

| Canon slot | Job it does | Recommendation |
|---|---|---|
| 0 · St Peters Island | prologue home island | **keep** — it is already a real PEI name and it is built |
| 1 · Coddle Cove | the home you sail the dory to | **keep the name, re-skin the coast** to PEI red sandstone + spruce |
| 2 · The Sunkers | reef field | **keep** — "sunker" is Maritime vernacular, canon flavour |
| 3 · Nine Mile Creek | ⚠ *was* the market town | **keep the name, keep the new job**: the working wharf + its small community (already ruled 2026-07-25) |
| — | ⚠ **the market town's job is now homeless** | **NEW region needed** — the eastern Market Port (§3 E1). Needs a name and a canon row. |
| 4 · The Drownded Lands | walkable flats | **keep** |
| 5 · Fundy Rips | narrows | **keep** — Fundy is the right water and the right gate east |
| 6 · The Banks | offshore | **keep** |
| 7 · Ironbound | storm coast | **keep the name, skin it Newfoundland** |
| ✦ · The Smother | fogbank | **keep** |
| ⚓ · Shipping Lanes | commerce | **keep** |
| — | **Port Greywick** | ⚠ **retire or relocate.** Canon still names it in places as the market town; the 2026-07-25 ruling took that job away from Nine Mile Creek without giving it back to Greywick explicitly. The cleanest reading: **Greywick IS the eastern Market Port.** One decision closes three open questions. |

So the amendment the owner actually has to make is **small**: confirm that Port Greywick is the eastern
market port (which houses the auction, the shipwright's yard and the deep berth), and add the four home
water scenes as regions. Everything else keeps its locked name.

---

## 6. Order of work

Deliberately paced so the owner can play and rule at each step, and so nothing is built twice.

| Step | What | Why here |
|---|---|---|
| **0** | **Nine Mile Creek mainland, Phase A + B** *(in progress)* | The starter region has to be good before anything is copied from it |
| **1** | **Per-passage arrival points** (§4.1) | Blocks every multi-door region, including the one being built now |
| **2** | **`The Roads`** — the first water scene | Proves the water-scene template and the seam model on the *easiest* possible case; it is also the first sail in the dory, which the owner should play before four more are built |
| **3** | **Owner plays the home arc** — St Peters → crossing → Nine Mile Creek → the Roads | The go/no-go gate. Is it *fun* before more is spent (roadmap §6) |
| **4** | **`MapGraph`** (§4.2) | Before the door count gets away from us |
| **5** | **The Shoal Grounds + The Bay + The Home Cove** | The rest of the home cluster; the lobster-boat ceiling lands with them |
| **6** | **The Strait**, and the canon amendment of §5 | The ceiling becomes a door east |
| **7** | **The Market Port** | The auction, the yard, the 6 m berth — and the dragger finally has somewhere to lie |
| **8** | Eastward: the Narrows → the Banks → the Outport Coast | The trawler world, in the order the difficulty ramps |

**What NOT to do yet:** do not recreate or rename the drafted eastern regions before step 3. The owner
has said the starter region comes first, and every hour spent on the Banks before the home arc is proven
fun is an hour spent on the wrong end of the map.

---

## 7. Open questions for the owner

1. **Is Port Greywick the eastern Market Port?** One yes closes three open questions: where the market
   town went, where the shipwright's yard lives, and what the dragger's home berth is.
2. **How many water scenes around St Peters — four, or fewer?** §2 proposes The Roads, The Shoal
   Grounds, The Bay and The Strait. Two (a near one and a far one) would work; four gives each its own
   hazard and its own tier.
3. **Should the crossing get its teeth back** — a ~900 m total bar, at the cost of a ~300 m wider Nine
   Mile Creek region? The arithmetic is in
   [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md) §3.3.
4. **Fictional names or real ones?** §5 recommends keeping the fictional Sablewick Banks frame and
   re-skinning the *look* to PEI. The alternative — real PEI place names throughout — is warmer and
   riskier (it constrains the map to a real coastline that does not have a Fundy or a Newfoundland
   within sailing distance).
5. **Does the home cluster get a season?** Canon has four seasons. A PEI winter closes the wharves and
   ices the bay, which is either a wonderful pressure or a month of the game where nothing works.
