# Municipal infrastructure — routes, sidewalks, power

> **Status: DESIGN ONLY.** No scene is edited by this PR, no prop is placed, no route is built. It is a
> plan, plus the route data the region builders will consume, plus an art-asks list for the owner's
> Design sessions. Every placement slice is deliberately kept out (§7).
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon), then
> [`world-and-regions.md`](world-and-regions.md), [`npcs-and-routines.md`](npcs-and-routines.md) (the
> routine engine the routes exist to serve), [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md)
> and [`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md) (that region's published geography), and
> [`lighting-and-daynight.md`](lighting-and-daynight.md) + ADR 0013 / ADR 0016 (why night is the frame the
> power difference is actually seen in).
>
> **Sibling documents this one now depends on** (both in flight, both cross-referencing back):
> [`harbour-geography.md`](harbour-geography.md) (PR #605) — **Route 91** and Finnigan's Landing; and
> [`settlement-population.md`](settlement-population.md) (PR #606) — **who lives at Nine Mile Creek**,
> which is what §4's routes exist to carry. Where those two and this one describe the same fact, they
> should be reporting it from the same place, not restating it.
>
> **What prompted it:** the owner's 2026-08-20 dispatch — *map out municipal routes and design the decor,
> roads, sidewalks and needed infrastructure for each settlement, and the infrastructure differs by place:
> St Peters has no mainland power, Nine Mile Creek is on the grid, larger cities later get the full urban
> treatment.* Serves **P3 A Living Working Coast**, and is the thing that separates a settlement which
> reads as a place from one that reads as a prop pile.
>
> ### ⭐ Later rulings folded in (2026-08-20, after the first draft)
>
> Four rulings landed after this document's first commit and are folded in throughout. They are recorded
> here as relayed by the coordinator, not as a verbatim transcript.
>
> | # | ruling | what it changed |
> |---|---|---|
> | 1 | **St Peters' energy is a COMBINATION of all types, depending on the use case** | ⚠ **Supersedes the first draft's "off-grid means oil only".** §3.4 is rewritten around *the island makes its own power, and each source has a job*. The night contrast survives, but as **varied vs uniform**, not *fire vs electricity*. **This is a ruling about ENERGY, not an art licence** — the windmill/solar period call is still owed (§8) |
> | 2 | **Nine Mile Creek HAS residents — more than St Peters, with "all the basic amenities of a town"** | ⚠ **Supersedes the first draft's "the region has nobody to give a routine to".** §4.1 and slice **S6 are unblocked at design level.** Sizing comes from the fleet and is already done: **[`settlement-population.md`](settlement-population.md) (PR #606) puts the roster at 35 working adults vs St Peters' 6** |
> | 3 | **The trunk road is ROUTE 91**, and Wharf Road merges onto it | The road this document has been calling "the through-road" **is** Route 91 — [`harbour-geography.md`](harbour-geography.md) (PR #605) establishes it is already built, not to be built. Named as such throughout |
> | 4 | **Lead-architect: alleys are LEAVES through M1–M2**; the graph + tie-break ADR is deferred to the first-city trigger | §2.3's recommendation **is now the ruling.** Recorded as settled rather than proposed |

---

## 0. The whole design in one breath

**St Peters makes its own power, one source per job. Nine Mile Creek takes it off the wire.** Everything
below follows from that, and it is only legible at night — so this is as much a *lighting* plan as a
routes plan. By day the two settlements differ in surface and furniture; by night they differ in kind.

⚠ **The contrast is VARIED versus UNIFORM, not fire versus electricity.** Ruling 1 means the island's
lights are a mixture — a lamp here, a genset there, a battery set on the wharf — scattered, at different
heights, different colours, different steadinesses. The mainland's are **one thing repeated**: a line of
identical lamps at a constant height and spacing, all the same colour, all fed from the same wire. *That*
is the difference the player reads from the water, and it survives the island having some electricity.

---

## 1. What already exists — read this table before proposing anything

Half of what the dispatch asks for is already built, and the other half has nothing at all. **Which half
is which is the opposite way round in each region**, and that is what shapes this whole document.

| | **St Peters** (island) | **Nine Mile Creek** (mainland) |
|---|---|---|
| Walkable lane network (`RoutineLanes`) | **12 nodes**, a tree rooted at the green | **NONE** |
| Villagers on `RoutineDef` days, **built today** | 6 | **0** — two static stallholders (Wendell, Hector) |
| Working adults the region **implies** (PR #606) | 6 | ⭐ **35** — and none of them can walk anywhere |
| Drawn roads (RoadsV3 tilemaps) | none | 4 ways + 3 pads + 5 shell walks, all stroked from published routes |
| Painted walked dirt (terrain splat) | 2 paths — village→slip, village→bar head | none |
| Utility props placed | **NONE** | 9-pole line, yard light, 2 shore-power pedestals, standpipe, fuel pump, oil tank, yard hydrant |
| Light presets available | `WindowGlow` / `Lightpost` / `Worklight` — shared, ADR 0016 | the same three |

> **St Peters has the movement truth and no infrastructure. Nine Mile Creek has the infrastructure and no
> movement truth.** Each region is exactly the other's missing half. §3 and §4 are shaped by that
> asymmetry rather than being two copies of one template.
>
> ⭐ **The last row is what the 2026-08-20 residents ruling changed.** Nine Mile Creek's missing half is
> no longer a tidiness problem — the region's own wharf, lots and shops imply **thirty-five working
> adults**, and there is not a single lane for one of them to walk. That is what makes slice **S2** the
> first thing to build.

### 1.1 ⭐ The art the dispatch asks for very largely already exists

The dispatch's art-asks list guesses at *"pole line, transformer, well, propane bottle, lamp…"*. All of
those are **already baked and committed**: `docs/art/rigs/iso-rig-pack/utility-iso/` is a 42-piece rig in
six categories (power · light · water · sewer · fuel · telecom), and all 42 sheets sit in the repo at
`Assets/_Project/Art/Sprites/Utility/`, eight facings each.

| the dispatch asks for | it already ships as |
|---|---|
| pole line | `powerPole` (9.08 m; **crossarm / transformer / lamp-arm** variants), `hFrame`, `guyAnchor` |
| transformer | `padTransformer` (2.05 × 1.67 m), or the pole's own transformer variant |
| well | `wellCap`, `handPump` |
| propane bottle | `propaneTank` |
| lamp | `streetLamp` (4.48 m), `yardLight` (7.26 m cobra head), `floodMast` (7.8 m), `lanternPost` (wharf-decor rig) |
| generator | `genset` — "standby set", enclosed, 1.94 × 1.13 m |
| culvert | `culvert` end, plus `catchBasin`, `trenchDrain`, `manhole` |
| signage | `noticeBoard`, `chalkSign`, `harbourSign`, `tideStaff` (wharf-decor rig) |
| rain barrel / water store | `cistern`, `waterTank`, `drumPlanter` |

**§6 is therefore short, and that is the finding — not an omission.** What is genuinely missing is a much
smaller and more interesting list, and the biggest item on it is not a sprite at all (§6.2).

---

## 2. The architecture: one route table, three consumers

### 2.1 ⚠ There must not be a third path system — there are already three

Today the world describes "where a route goes" in three unrelated ways, and none of them can see the
others:

| | what it is | what it drives | where it lives |
|---|---|---|---|
| `RoutineLanes` | a **tree** of nodes + polyline edges, builder-wired into the scene | what a **villager walks** | `World/Routines/RoutineLanes.cs` — St Peters only |
| `NineMileCreekRoads.Way` | a published polyline + half-width + surface + rank | which **tiles are laid** | `App/Editor/NineMileCreekRoads.cs` — NMC only |
| `StPetersStarterSplat` paths | polylines dabbed into the terrain splat | which **ground reads as walked** | `App/Editor/StPetersStarterSplat.cs` — StP only |

The dispatch's instruction is exactly right and this document adopts it as its governing rule:

> **One polyline table per region is the route truth. The lane tree, the road tilemaps and the terrain
> splat are three CONSUMERS of it, never three authors of it.**

This is the same shape the water arc already settled on — *one height map, three consumers* — and it is a
shape each region has half-invented by accident and got right. Two of St Peters' three lanes **are
literally** the dirt paths the terrain painter paints (`StPetersStarterSplat.VillageToSlipPath` /
`VillageToBarHeadPath`), so a villager walking east walks on ground the world already draws as walked.
Nine Mile Creek got the other half the same way: `NineMileCreekDressing.Poles()` walks `WharfRoad` itself
rather than a copy of it, and `NineMileCreekRoads` strokes tiles along the published routes rather than
re-typing them. **Neither region has ever had to reconcile the two, because neither has both.** This
document is written so that the day each gains its missing half, it gains it onto the same table.

**Which of the three is the truth?** `RoutineLanes`. It is the only one that already stands in the scene
as an object, the only one with a `Validate()` that refuses a malformed network out loud, and the one the
scene-export contract already treats as the single polyline table. So:

- **a street is a lane that is DRAWN** (RoadsV3 tiles at carriageway width);
- **a footpath is a lane that is PAINTED** (splat dirt at 1.5 m);
- **an alley is a lane that is neither** — a gap the fences make, not one the ground shows.

### 2.2 Route classes, and what each consumer does with one

| class | lane node | RoadsV3 tiles | splat dirt | fences | furniture that belongs to it |
|---|---|---|---|---|---|
| **street** | yes | yes — 5 m gravel / 4 m dirt | no | to the verge, never across it | poles or lamps (per region), ditch, culvert at a crossing, signage at junctions |
| **lane** (service / back) | yes | yes — 3 m | no | both sides; gates where a yard opens | gateposts, woodpile, bins |
| **footpath** | yes | no | yes — 1.5 m | stile or gap where it meets a fence | none; a footpath is bare ground |
| **walk** (front walk) | **leaf only** | yes — 1.5 m shell or concrete | no | gate at the road end | doorstep light |
| **alley** | **leaf only — see §2.3** | no | yes, faint | both sides, by definition | nothing; an alley *is* the absence |

Widths, surfaces and ranks already have a published home in `NineMileCreekRoads` §1–§3 and this document
does not restate them. It adds the **class** above them, so the two regions can share one vocabulary and
the second region does not invent a parallel one.

### 2.3 ⚠⚠ THE TREE FORBIDS A BLOCK — the one real architectural finding

`RoutineLaneTree` is a tree **on purpose**, and its own header argues the case well: in a tree there is
exactly one path between any two nodes, so there is nothing to search, no priority queue, no
non-deterministic tie-break, and *"is this network sane?"* is one checkable property rather than a
judgement. `RoutineLanes.Validate()` refuses a cycle loudly, naming the offending node.

**But an alley that joins two streets is a cycle. So is a green you can walk round. So is a block.**

The dispatch asks which gaps are streets, footpaths and alleys. For the alleys, the answer is: *an alley
can only be a dead end.* Two ways out were put to the lead-architect, **and the call has been made**:

- ⭐ **(a) Every alley is a LEAF — RULED (lead-architect, 2026-08-20), through M1–M2.** An alley goes in
  from one street and stops — at a woodpile, a privy, a back gate, a boat under a tarp. A real Maritime
  fishing village is very largely like this: buildings back onto each other and you come out the way you
  went in. Costs nothing and changes no code. **§3 and §4 below are built on it.**
- **(b) The lane table becomes a graph with a published tie-break — DEFERRED to the first-city trigger.**
  `RoutineLaneTree`'s own doc already names the day this happens: *the day a village genuinely needs a
  loop, this becomes a graph and gains a tie-break rule.* That day is the first city (§5), and it wants
  an ADR of its own rather than a slice.

⭐ **The ruling requires no change to anything shipped.** Nine Mile Creek's four published roads already
form a tree: the bar road T's into Wharf Road at the junction `(−16, 92)`, and Route 91 T's into
Wharf Road at the town end `(−178, 92)`. St Peters' twelve nodes are a tree and are validated as one every
build. **There is no cycle in the shipped geography of either region.** The constraint costs nothing
today, and this document's job is to keep it costing nothing — by never proposing a loop a later pass
would have to unpick.

### 2.4 Route ids, and the derivation law

- **Ids:** `route.<region>.<snake_name>` — append-only and stable, the same law as Def ids (CLAUDE.md §5).
- **Node names:** St Peters' twelve shipped node names are bare strings (`green`, `lane_store`,
  `yard_saltbox`, `slip_head`…). **They are shipped ids — keep them exactly as they are.** New nodes take
  the same bare form inside their region's table. The `route.` prefix names the *routes*, which is what
  this document and the builders talk about; it is not a rename of the nodes.
- ⭐ **Never a copied coordinate.** Every position in §3 and §4 is given as its **derivation** —
  `Dooryard(GeneralStorePos)`, `PositionAt(WharfRoad, 160 m)` — not as a literal, because that is the
  standing rule in both regions' builders and #345 is the standing lesson. Where a literal appears below
  it is already the published constant, and is quoted with its source.

### 2.5 A route must serve somebody's day

The routine engine is a pure function of the clock. A route exists **because a resident's day walks it**,
never the reverse. Every route proposed below names whose day uses it — and where nobody's does yet, it
says so plainly and defers to the pass that adds the person. A route with no routine on it is decor, and
decor is §3.3 / §4.3's business, not the route map's.

---

## 3. St Peters — the island that services itself

### 3.1 The twelve nodes that already exist

Rooted at the green, which is derived as the midpoint of the hearth and the player's spawn —
`(VillageHearthPos + StartSpawnPos) × 0.5` = **(2.5, 7)**. Everything else hangs off it.

| node | parent | serves | derived from |
|---|---|---|---|
| `green` | — (root) | the village's centre; four villagers end their day here | `StPetersBuilder.VillageGreen` |
| `lane_store` | `green` | the general store | `Dooryard(GeneralStorePos)` |
| `lane_school` | `lane_store` | the school | `Dooryard(SchoolPos)` |
| `lane_farmhouse` | `lane_store` | the white farmhouse | `Dooryard(WhiteFarmhousePos)` |
| `lane_post_office` | `lane_store` | the post office | `Dooryard(StPetersShops.PostOfficePos)` |
| `yard_saltbox` | `green` | the red saltbox | `Dooryard(RedSaltboxPos)` |
| `yard_sage_cottage` | `green` | the fisherman's cottage (two housemates) | `Dooryard(SageCottagePos)` |
| `yard_cottage` | `green` | Ginny's village mark | `StPetersBuilder.GinnyPos` |
| `yard_ginny_plot` | `green` | Ginny's plot out in the eastern woods | `StPetersGinnyPlot.Dooryard` |
| `flats_head` | `green` | the clam flats — **carries bend points**, the painted bar path | `JuniorSpot`, via `VillageToBarHeadPath` |
| `slip_head` | `green` | the slip — **carries bend points**, the painted slip path | `BerthTo`, via `VillageToSlipPath` |
| `wharf_head` | `slip_head` | up the wharf where Basil works | `BasilSpot` |

> ⚠ **Twelve, not eleven** — the dispatch says eleven. `StPetersRoutines.BuildLaneTable` makes twelve
> `Add(Lane…)` calls and the shipped table has twelve nodes. Noted because a route plan that starts from
> the wrong count would propose a node that already exists.
>
> `wharf_head` hanging off `slip_head` rather than off the green is the island's **only two-deep branch**,
> and it is the shape the rest of this section extends: destinations chain off the node that reaches
> them, not off the root.

### 3.2 The route map

**St Peters has ONE street and everything else is a footpath.** That is not a simplification; it is what
a village of half a dozen roofs actually is — four dwellings, three public doors, and Ginny's plot out
in the woods. The dispatch's warning about sidewalks (§3.3) is the same argument from the other end.

| id | class | from → to | length | whose day walks it | status |
|---|---|---|---|---|---|
| `route.stpeters.the_green` | — | the root itself | — | everyone's | **shipped** |
| `route.stpeters.school_lane` | **street** | `green` → `lane_store` → school / farmhouse / post office | 24.0 m to the store, then 16.1 / 17.7 / 16.3 m | the storekeeper, the schoolmistress, the postmistress | **shipped as lanes; UNDRAWN** |
| `route.stpeters.slip_path` | footpath | `green` → `slip_head` (bent) | 187.6 m straight-line — **1:02 at the walk** | Basil, and the player every single day | **shipped, painted** |
| `route.stpeters.bar_path` | footpath | `green` → `flats_head` (bent) | 48.0 m to the bar head | Junior, Eileen | **shipped, painted** |
| `route.stpeters.wharf_walk` | footpath | `slip_head` → `wharf_head` | short | Basil | **shipped** |
| `route.stpeters.plot_path` | footpath | `green` → `yard_ginny_plot` | 84.2 m | Ginny | **shipped as ONE straight edge — see ⚠ below** |
| `route.stpeters.camper_spur` | footpath, **leaf** | `yard_ginny_plot` → the camper lot | 14.0 m | the player, once the camper is his | **PROPOSED** |
| `route.stpeters.bucket_walk` | footpath, **leaf** | `flats_head` → the wet bucket | 61.9 m from the green | the player; nobody's routine yet | **PROPOSED** |
| `route.stpeters.saltbox_alley` | alley, **leaf** | `yard_saltbox` → the woodpile behind it | ~8 m | whoever fetches wood — needs the routine first | **PROPOSED, blocked on §7** |
| `route.stpeters.sage_alley` | alley, **leaf** | `yard_sage_cottage` → the gear line behind it | ~8 m | Junior, hanging oilskins | **PROPOSED, blocked on §7** |

⚠ **`plot_path` is the one route that is actively wrong today.** `yard_ginny_plot` hangs directly off the
green as a single **84 m straight edge through the woods** with no bend points — which means Ginny walks a
straight line for 28 seconds through trees, on ground nothing draws as walked. The two painted paths carry
bends for exactly this reason and this one does not. **It should bend along the forest lane's own path
corridor** and be painted like its two siblings. That is a small, well-scoped slice (§7, S1).

⚠ **`slip_path` is 187.6 m — a full minute of walking on one edge.** It is bent and it is painted, so it
reads correctly, but it is by far the longest thing anyone on the island walks, and any routine block that
sends a villager green→slip→green spends **2:05 in transit before it does anything**. `StPetersRoutines`
already validates that no leg overruns its block; this is flagged so the next routine author sizes blocks
against it rather than discovering it in the log.

### 3.3 Street furniture — and ⚠ NO SIDEWALKS. NONE. NOT ONE.

The dispatch says sidewalks belong *"only where a settlement would really have them — a fishing village
mostly does NOT"*, and St Peters is the clearest case in the game. **A kerbed footway on this island would
be the single most wrong thing this arc could add**, and wrong in the worst way: it would read as
*finished*. Grass goes to the edge of the lane; the lane is dirt; where feet go often enough the dirt
shows. That is the whole surface language of the island and the terrain splat already speaks it.

⚠ **Nine Mile Creek already shipped five crushed-shell front walks and the owner has not yet ruled on
them.** Until he does, **do not extend the idea here.** If he rules against them there, this section
needs no change at all; if he rules for them, St Peters would take at most a shell walk at the store and
the post office — the two public doors — and nowhere else.

What the island *does* get:

| where | what | why |
|---|---|---|
| the green | `bench` ×2, `noticeBoard` | the green is where four villagers end their day; a notice board is how a village talks to itself |
| every dooryard | woodpile, `trashBarrel`, a gate in the yard fence | a dooryard is grass and ruts, plus the things a household keeps outside |
| yard boundaries | **post-and-rail / picket fences** — see the yard lane | the fences are what *make* the alleys; the two lanes must agree on the same segments |
| the store, the post office | `chalkSign`, doorstep light | the only two public doors on the island |
| `flats_head` | `tideStaff` | you read the tide before you walk out on the flats; diegetic information, P4 |
| the slip / wharf | `ringPost`, `rescueLadder`, `harbourSign` | already the wharf's own kit; named here so the route map and the wharf agree |
| where the bar path leaves the land | a single leaning **marker post** | the bar is the way off the island at low water and it should be *marked*, not merely walkable |
| road-to-shore crossings | none needed — no ditches, no culverts | nothing here is engineered enough to need drainage furniture |

⚠ **The fences are a shared boundary with the yards-and-fences lane, and the two must not each invent
them.** The route map's claim is only this: *a fence segment must not cross a lane, and every alley in
§3.2 exists because two fence lines make it.* The yard lane owns the polygons; this doc owns the gaps
between them. Neither owns both.

### 3.4 ⭐ POWER: the island services itself — the differentiator

**There is not one pole on St Peters. Not one wire in from the mainland. Not one street lamp.** What
there *is* — per the owner's 2026-08-20 ruling — is **a combination of every kind of energy, chosen by
use case.** The island is not pre-electric. It is **un-serviced**: nobody delivers power here, so each
household and each trade solved the problem its own way, and the mixture is the character.

> ⚠ **This supersedes the first draft, which had the island on oil alone.** The rewrite is not a
> softening: *one source per job* is a stronger read than *one source for everything*, because a mixture
> is what an un-serviced place actually looks like and a uniform one is what a stylised place looks like.

#### One source per job — the island's energy, by use case

| the job | the energy, and why *that* one | prop | light preset |
|---|---|---|---|
| light in a house at dusk | **oil lamp** — cheap, needs no infrastructure, and it is what you light first | (the building's own window) | `WindowGlow`, flicker 0.05 |
| cooking | **propane**, bottled and boated in — a stove must light instantly and every day | `propaneTank` at the back door | — |
| heat | **wood and coal** — free, or nearly, and the island has both | `coalBin`, woodpiles | — |
| a public room on a hall night | **the genset** — you only run it when there are people to justify the fuel | `genset` + `oilTank` | `Worklight`, *only then* |
| working after dark in a dooryard | **a hurricane lantern** — portable, which is the whole point | `lanternPost` | **a 4th preset — §6.2 #2** |
| the slip head | **one lantern**, lit by whoever is last off the water | `lanternPost` | small, warm |
| ⭐ radio, a light in a boat shed, a trickle charge | **battery + a small charging set** — the modern layer, and the one the first draft missed | `genset` (small), `pedestal` | steady, tiny |
| the wharf | **nothing standing** — you bring your own light | — | — |

⭐ **The genset is still the trick, and the ruling makes it a better one.** It is not the island's *only*
electricity now — it is its **loudest**. One at the parish hall, run for a hall night; a small one behind
the boat shed; and everything else on oil, wood and bottled gas. When the hall's set is running, one
cooler, brighter, steadier point stands out among a dozen warm scattered ones **and you can see it from
the water.**

⚠ **"All types" is a ruling about ENERGY, not a licence to place any generating prop.** A windmill or a
solar panel would date the world in one prop and the owner has not ruled on them (§8 #3). Everything in
the table above is a source the island could plausibly have had for a century.

#### Fuel — the chain that makes it real

The island's light is a *supply problem*, and the mixture makes it a richer one: **four different fuels
arrive by boat**, which is exactly the kind of thing the gas/diesel design already ruled on wants.

| what | prop | where |
|---|---|---|
| lamp oil / kerosene | `drumRack`, `jerryCans` | the store's back yard — the island's lamp oil comes off a boat |
| propane at the kitchens | `propaneTank` ×1 per household | beside the back door, on a small pad |
| the parish hall's genset fuel | `oilTank` (2 m) + `fillPipes` | behind the hall |
| the boat shed's small set | `jerryCans` | inside the shed door |
| coal / wood for the stoves | `coalBin`, woodpiles | every dooryard |

⭐ **Four fuels, four different containers, four different places they are kept** — and a player who
looks at a dooryard can tell what that household burns. That is the ruling paying for itself: a single
fuel would have been one prop repeated six times.

**Wood smoke is the day-time half of the same story.** A chimney with smoke over it says *somebody is
home and burning something*, which is the un-serviced read in daylight when the lamps are out. It is a
particle ask, not a rig ask (§6.2).

#### Water — wells and rain, no mains

| what | prop | where |
|---|---|---|
| the village well | `wellCap` + `handPump` | on the green, at the root node — a well is *why* a green is where it is |
| household water | `cistern` / rain barrel under each downspout | every house |
| the wet bucket | already shipped | at `(−59, 0)`, and `route.stpeters.bucket_walk` is what reaches it |
| drainage | nothing — no catch basins, no manholes, no lift station | the island does not have a sewer and it must not look like it does |

⚠ **Nothing from the `sewer` category may be placed at St Peters.** `manhole`, `catchBasin`, `cleanout`,
`liftStation` — all of them read *municipal*, and one manhole in a dooryard would quietly undo the whole
un-serviced argument. ⚠ **The energy ruling does not reach this.** "A combination of all types" was said
of *energy*; water and drainage are a separate service and the island still has neither. The one
legitimate exception is `septicLids`, because an island house does have a septic field, and it is the
*right* kind of detail: it says "this house handles its own waste".

#### What the island looks like from a boat, at night

**A scatter.** A dozen small points, no two alike: different heights, different warmths, some flickering
and some steady, **none of them in a line and none of them evenly spaced.** If the parish hall's set is
running, one cooler brighter point stands out among them and you know where everyone is.

**That silhouette is the deliverable, and the test is a negative one:** if a slice ships and the island
reads as a *street* at night — anything regular, anything evenly spaced, anything all one colour — the
slice is wrong, no matter how many of its individual lights are correct. **Regularity is the mainland's
signature (§4.4), and it is the only thing the island must never borrow.**

---

## 4. Nine Mile Creek — the mainland grid

### 4.1 ⚠⚠ The region has no lane network at all — and now it has 35 people who need one

`NineMileCreekPeople` places **two static stallholders** and nobody walks anywhere. There is no
`RoutineLanes`, no `RoutineStations`, no `RoutineDef` in this region. It has **Route 91** running 564.4 m
through it, 321.8 m of Wharf Road, **nine town lots**, a wharf with fourteen berths, a truck park, a
shipyard lot, a restaurant lot and a fish-market lot — and not one person whose day connects any of them.

⭐ **The owner's 2026-08-20 ruling makes this urgent rather than merely untidy.** Nine Mile Creek **has
residents — more than St Peters — with "all the basic amenities of a town"**, and
[`settlement-population.md`](settlement-population.md) (PR #606) has already sized them off the region's
own built geography:

| | working adults |
|---|---:|
| **Nine Mile Creek** — 7 owners + 11 crew afloat, 17 ashore | **35** |
| *(with the cannery, once it has a site)* | *(39)* |
| **St Peters** | **6** |

> ⚠ **Supersedes the first draft**, which said this region had nobody to give a routine to. It has 35 —
> they simply have not been authored yet. **Slice S6 is unblocked at design level** and the question it
> was blocked on is answered by another document rather than by this one.

**So this is now the largest single gap between what the region draws and what it is**: 564 m of road, a
14-berth wharf, nine lots, thirty-five people implied by all of it, and **no network for a single one of
them to walk.** §4.2 is the most load-bearing part of this document, and slice **S2 is the one to build
first.**

> ⚠ **Two of the 35 cannot be placed from this document's table.** PR #606 flags that the four processing
> NPCs need the cannery, which has **no site yet** (§4.2), and that three shipyard jobs ride the unresolved
> boat-shed question. Both are carried here, neither is this document's to close.

### 4.2 The lane table this region needs

**Root it at the town, not at the wharf.** The wharf is where the *player's* day starts, but the tree's
root should be where the *residents'* days start, because that is what makes every walk-up-and-back a
short climb rather than a traverse. The Route 91 / Wharf Road junction — `WharfRoad[0]` =
`ThroughRoad[3]` = `(−178, 92)`, **375.8 m along Route 91** — is the town's centre by
construction: **four of the nine lots stand within 45 m of it** (chandlery 36.9 m, harbourmaster 38.2 m,
parish hall 39.7 m, the south house 43.9 m). It is where a village green would be if this settlement had
one.

> ⚠ Not to be confused with `NineMileCreekMainland.RoadJunction` = `(−16, 92)`, which is the **bar road**
> T, 162.1 m out along Wharf Road. Two junctions, two nodes, and the names must not collapse.
>
> ⭐ **`ThroughRoad` is the code constant; ROUTE 91 is the road's name** (owner, 2026-08-20 — PR #605).
> The array keeps its identifier and the prose uses the name. **Route 91 is not a road to be built** —
> `harbour-geography.md` §2 establishes that it is already built and is exactly this polyline, leaving the
> region north at `ThroughRoad[5]` = `(−186, 280)`. Wharf Road *merges onto it* at `town`, which is what
> makes `town` the root and not merely a convenient node.

⭐ **Every node below hangs off a route that is already published, at a distance along it — never at a
copied coordinate.** `MainlandCoast.PositionAt(route, along)` is the primitive; the pole line already uses
it, so the lane table and the pole line would be reading the same route with the same call. **The
along-distances below are measured, not guessed** — each is the closest point on the published polyline to
the lot it serves, with the lot's own offset from the road quoted beside it.

| node | parent | class of the edge | derived as | serves |
|---|---|---|---|---|
| `town` | — (root) | — | `WharfRoad[0]` = `ThroughRoad[3]` — 375.8 m along Route 91 | the town centre |
| `road_civic` | `town` | **street** | `PositionAt(ThroughRoad, 400 m)` | the civic pair, ~25 m north |
| `lot_chandlery` | `road_civic` | walk | `Dooryard(ChandleryPos)` — 399.2 m along, 28.5 m off | the rod |
| `lot_harbourmaster` | `road_civic` | walk | `Dooryard(HarbourmasterPos)` — 402.5 m, 27.4 m off | the cod licence |
| `road_commercial` | `road_civic` | **street** | `PositionAt(ThroughRoad, 436 m)` | the store/tavern pair |
| `lot_store` | `road_commercial` | walk | `Dooryard(GeneralStorePos)` — 436.5 m, 26.6 m off | |
| `lot_tavern` | `road_commercial` | walk | `Dooryard(TavernPos)` — 435.2 m, 29.4 m off | |
| `road_north` | `road_commercial` | **street** | `PositionAt(ThroughRoad, 480 m)` | the north end |
| `lot_house_north` | `road_north` | walk | `Dooryard(HouseNorthPos)` — 477.0 m, 29.5 m off | |
| `lot_house_nw` | `road_north` | walk | `Dooryard(HouseNorthWestPos)` — 482.8 m, 28.3 m off | |
| `road_south` | `town` | **street** | `PositionAt(ThroughRoad, 345 m)` | the south pair |
| `lot_house_south` | `road_south` | walk | `Dooryard(HouseSouthPos)` — 341.3 m, 27.0 m off | |
| `lot_parish_hall` | `road_south` | walk | `Dooryard(ParishHallPos)` — 348.3 m, 28.6 m off | ⭐ the genset's mainland counterpart (§4.4) |
| `wharf_road_west` | `town` | **street** | `PositionAt(WharfRoad, 64 m)` | where the truck-park spur and the shed both leave |
| `truck_park` | `wharf_road_west` | lane, **leaf** | `TruckParkPos` — 57.6 m along, 15.5 m **north** | where a road vehicle is left |
| `lot_boat_shed` | `wharf_road_west` | walk, **leaf** | `Dooryard(BoatShedPos)` — 70.7 m, 26.2 m **south** | ⚠ the flagged lot — see below |
| `road_junction` | `wharf_road_west` | **street** | `RoadJunction` = `(−16, 92)` — 162.1 m along Wharf Road | the bar road T |
| `bar_road_gully` | `road_junction` | **street** (dirt) | `BarRoad[3]` = `(14, 0)` — 155.0 m along the bar road from the landing | where the gully path spurs |
| `gully_head` | `bar_road_gully` | footpath, **leaf** | `GullyPath[2]` — a 19 m spur | the only way down to the ledges at low water |
| `bar_road_head` | `bar_road_gully` | **street** (dirt) | `BarRoad[0]` = `(58, −146)`, the landing | ⭐ the crossing to St Peters |
| `wharf_yard` | `road_junction` | **street** | `PositionAt(WharfRoad, 281 m)` | the yard, the parking, the apron |
| `dory_yard` | `wharf_yard` | lane, **leaf** | `DerelictDoryPos` — 252.9 m, 9.8 m off | Hector, and the derelict dory — **the region's opening beat** |
| `buyers_parking` | `wharf_yard` | lane, **leaf** | `ParkingPos` | Wendell at his truck |
| `shanty_row` | `wharf_yard` | lane, **leaf** | the shanty row's own published site | the fishing shanties |
| `wharf_front` | `wharf_yard` | lane | `WharfRoad[6]` = `(140, 122)` — 321.8 m, the road's end | the quay |

**That is 25 nodes and it is a tree.** Check: every node has exactly one parent; `town` is the only root;
and the only two places published roads meet — the town junction and `RoadJunction` — each appear once, as
one node. **No cycle.** The bar road runs `road_junction` → `bar_road_gully` → `bar_road_head` and stops at
the landing 271.9 m from its far end; the gully path is a 19 m leaf. Wharf Road runs
`town` → `wharf_road_west` → `road_junction` → `wharf_yard` → `wharf_front`, and everything else leaves it
as a leaf.

⚠ **Route 91 is a strung-out settlement, not a square, and the table shows it.** Four nodes sit
*on* the carriageway between 345 m and 480 m, each carrying a pair of lots at ~27 m offset. That is what a
rural PEI community is — and it is why the lots could not simply hang off `town`: the store and the tavern
are **66.2 m** from the junction, which is a 22-second walk, not a dooryard.

**Walk budgets** (already published, quoted not recomputed): landing → wharf front **2:23**; landing →
town **2:24**; wharf → town **1:47**. Every one of those is longer than any walk on St Peters, and any
routine written here has to be blocked accordingly.

⚠ **The boat-shed lot is flagged, not resolved** — the 2026-07-25 ruling says there is no shipwright in
this region, but the shipped scene has one and the economy data hangs off it. `lot_boat_shed` is in the
table under the neutral name so nothing breaks; **where the shipwright's yard really lives stays the
coordinator's open question**, and this document does not close it.

⚠ **The new destinations from the wharf and lifecycle arcs are not in this table yet, on purpose.** Every
one of them is still landing, and each is a **one-line leaf off `wharf_yard`** when it merges — which is
exactly what the tree buys. Their offsets are already measurable against the published road:

| destination | along Wharf Road | offset | parent, when it lands |
|---|---|---|---|
| the fish-market lot | 264.3 m | 9.8 m | `wharf_yard` |
| the restaurant lot | 287.7 m | 11.6 m | `wharf_yard` |
| the shipyard lot | 321.8 m (the road's end) | 29.7 m | `wharf_front` |
| the lifecycle cannery | ⚠ **no site yet — the owner still owes one** | — | `wharf_yard`, presumably |

### 4.3 Street furniture — a working mainland shore

Nine Mile Creek is a *working* place and its furniture should say so: it is heavier, more of it is
metal, and none of it is decorative.

| where | what | why |
|---|---|---|
| Route 91, at each junction | ⭐ a **route shield reading 91**, plus `harbourSign` where Wharf Road leaves | the road now has a ruled name, so it should carry it — a numbered shield is the cheapest possible "you are on the mainland" signal, and the island has nothing like it |
| Route 91, both sides | shallow ditch + `culvert` ends at every lot entrance | ⭐ **this is the biggest single "reads as mainland" win** — a rural road is defined by its ditch |
| where a road crosses the neck between the ponds | `culvert` ends both sides | the causeway in the owner's first photograph is doing exactly this |
| the five public lots | the shipped shell walks | ⚠ **owner's ruling still owed** — see §8 |
| the truck park | `noticeBoard`, `trashBarrel` | where vehicles are left is where notices go up |
| the wharf yard | `noticeBoard`, `tideStaff`, `fireCabinet`, `ringStation` | already the wharf kit's; named so the two agree |
| the wharf edge | `ringPost`, `rescueLadder`, bollards | shipped |
| yard boundaries in town | fences meeting the yard lane's polygons | same shared boundary as §3.3 |
| the parking / apron edge | `tyrePile`, `oilDrum`, `pallet` | a working yard is untidy at its edges and tidy where it works |

**Sidewalks: still no.** Nine Mile Creek is a rural mainland community, not a town. Its "sidewalk" is the
gravel shoulder and the five shell walks already argued for. **The ditch is the thing to add, not a kerb**
— a ditch says rural mainland the way a kerb says urban, and it costs one prop family.

### 4.4 ⭐ POWER: on the grid — and the wire that isn't there

#### What is already placed

| what | count | where |
|---|---|---|
| `powerPole` | **9** — `floor(321.8 / 40) + 1` | walking `WharfRoad` at 40 m spacing, 5 m north of the centre-line |
| `yardLight` | 1 | at the wharf entrance — *"the only lit thing out here at night"* |
| `pedestal` (shore power) | 2 | west and east ends of the quay — two serve fourteen berths |
| `standpipe` | 1 | mid-quay, washdown water |
| `fuelPump` + `oilTank` | 1 each | on the apron, at the service end |
| `yardHydrant` | 1 | north of the parking |

#### ⚠⚠ THE POLES HAVE NO WIRES, AND NOTHING IN THE PROJECT CAN DRAW ONE

The utility rig is explicit that **spans are never baked**: `ties()` returns four sets of attach points in
metres — `wires`, `secondary`, `lamp`, `drop` — and *"the catenary between two poles, and the service drop
to a house, are drawn at runtime between ties; no span is ever baked into a cell."* That is the right
design, and it is why a pole line can span any distance and cross a corner without a sprite per gap.

**But no C# in this project consumes `ties()` or `anchors()` for a utility piece.** I checked: the only
`anchors()` callers are the building rig's azimuth probe and baker. There is no wire renderer, no
catenary component, nothing.

**So Nine Mile Creek today is nine bare poles standing in a field**, and the grid — the single visual fact
that separates this region from St Peters — **is not drawn at all.** This is the largest gap in the whole
municipal design and it is the one item on §6.2 that is code, not art. It is also small and self-contained:
a `LineRenderer`-per-span component fed from the rig's own tie points, sagging on the same catenary maths
`RodLineMath` already carries for the fishing line.

#### What the grid still wants, once the wire exists

| what | prop | where | why |
|---|---|---|---|
| the transformer | `padTransformer`, or a `powerPole` **transformer variant** at the line's town end | where Wharf Road merges onto Route 91 | a line has to come from somewhere; this is where the grid enters the region |
| service drops | the `drop` tie on `serviceMast` / `meterBank` | one per building the line passes | ⭐ **a drop to a house is what makes the line look like it is FOR something** |
| street lighting | `powerPole` **lamp-arm** variant on ~every third pole | Wharf Road | cold, high, evenly spaced — the exact opposite of St Peters' scattered warm points |
| the wharf floodlight | `floodMast` (7.8 m) | over the apron | the working shore stays lit after dark, because work continues after dark |
| a guy where the line turns | `guyAnchor` | **one only** — the pole at 200 m, just past the road's single real bend (+15.9° at `(14, 92)`, 192.1 m along, where Wharf Road turns onto the spit; every other vertex turns under 7°) | a line that changes direction is guyed, and guying a straight run would be the tell that nobody measured |
| telephone | `telecomPed`, `crossBox` | the town end | the mainland has a telephone and the island does not |
| the town's water | `hydrant` ×2, `curbStop`, `valveVault` | Route 91, in town | mains water is a mainland fact |
| drainage | `catchBasin` at the road low points, `manhole` on Route 91 | in town only | ⭐ **the sewer category is Nine Mile Creek's alone** — it is precisely what St Peters is forbidden |

#### What the mainland looks like from the water, at night

**A line.** Evenly spaced, cool, at a constant height, running from the town out to the wharf, ending in
one bright floodlit working yard. Warm window lights behind it, in town, dimmer than the street lamps in
front of them. **The eye reads "line" before it reads anything else, and that is the whole differentiator:**
an island's lights are scattered and warm, a grid's are in a row and cold. Sail from one to the other and
you should not need to be told which is which.

---

## 5. The larger city — principles only, no placement

One page, as asked. No city exists yet and none should be built from this section; it is here so that the
first city pass does not have to re-derive the vocabulary.

⭐ **The city has a name and a place: FINNIGAN'S LANDING.**
[`harbour-geography.md`](harbour-geography.md) (PR #605) puts it up **Route 91**, past two river
crossings, with an east-running river of its own and a harbour the player sails into on the late-game
city run. **So the road this document has been planning at Nine Mile Creek is literally the road to the
city** — Route 91 leaves the region north at `(−186, 280)` and Finnigan's Landing is what it goes to.
That is a better answer than the first draft's guess (Port Greywick) and it costs this section nothing
but the name. *(Owner: a confirmation would close §8 #2.)*

1. **A city is where the tree becomes a graph.** §2.3(b) is not optional at city scale: a block you can
   walk round is the defining unit of a city and it is a cycle by construction. **The first city is the
   trigger for the lane table gaining a tie-break rule** — the lead-architect has already deferred the
   ADR to exactly this moment, so it is booked rather than merely foreseen.
2. **Sidewalks, finally, and only here.** A kerbed footway is an *urban* signal. Its arrival should be a
   thing the player notices — the first time you step onto a kerb, the game has told you where you are.
   Paved with `concrete` or `brick` from the road kit, which already bakes both.
3. **The street grid is drawn, not painted.** No splat dirt anywhere in a city core; every route is a
   RoadsV3 way. `cobble`, `brick` and `asphalt` are all baked and unused — the city is what they are for.
4. **Full utilities, and they are visible.** Street lighting on every pole. Hydrants at intervals.
   Manholes and catch basins in the carriageway. Meter banks on the buildings. `switchgear` and
   `crossBox` at the substation end. The city is where the whole 42-piece rig finally earns itself.
5. **Underground where it is a town centre, overhead where it is a suburb.** The visual difference
   between a wired street and a clean one is one of the cheapest ways to say "this is the good part of
   town" without a single line of dialogue.
6. **The city is the third term, and it needs the other two to exist first.** Self-serviced island,
   wired village, wired city — the city only reads as a city because the player has walked the other two.
   **Do not build Finnigan's Landing before both §3 and §4 have shipped and been walked**, or its
   infrastructure has nothing to be a contrast to. ⭐ The player will most likely arrive **up Route 91
   from Nine Mile Creek**, which means the contrast is not even a memory — it is the road behind them.
7. **Scale by time-to-cross, as always.** The world-scene sizing rule does not change; a city is denser,
   not necessarily bigger.

---

## 6. Art asks

### 6.1 ⭐ Do NOT re-ask for these — they already ship

Every piece in §1.1's table, plus the full 42 of the utility rig and the 61 of the wharf-decor rig, is
already baked, committed and eight-facing. **The correct next step for most of §3 and §4 is placement, not
art.** An art session spent re-making `powerPole` would be a session wasted.

### 6.2 What is genuinely missing

| # | ask | kind | for | why it is not covered today |
|---|---|---|---|---|
| 1 | **A wire-span renderer** — catenary between two `ties()` points, plus a service drop | **code, not art** | NMC's whole grid read | the rig deliberately bakes no spans and nothing consumes `ties()`. **The single biggest gap in this document.** |
| 2 | **A `Lantern` light preset** — warm, small range, strong flicker (~0.15) | code, one enum case | StP's hung lanterns | `WindowGlow` (0.05 flicker) reads as a room, not as a flame in the open |
| 3 | **Chimney smoke** — a slow drifting wisp, wind-driven | VFX / particles | StP's day-time un-serviced read | nothing in the ambient-particles pass covers a point-source plume |
| 4 | **A fence kit** — post-and-rail, picket, wire-and-post; corners, gates, a run primitive | rig | both regions; the yard lane owes it | ⚠ **no fence rig exists at all.** `ropeFence` is wharf furniture, not a boundary. This is the yard lane's ask as much as this one's — **one kit, two consumers.** |
| 5 | **A roadside ditch profile** — a shallow V with a grass bank, as a *terrain* treatment | terrain / splat | NMC's mainland read | `culvert` gives the ends but nothing gives the run between them |
| 6 | **A windmill or a solar panel** | rig, small | StP, **only if the period allows** | not in the catalogue. ⚠⚠ **DO NOT COMMISSION ON THE STRENGTH OF THE ENERGY RULING.** "A combination of all types" was said of *energy use cases*, not of *era*; §3.4 deliberately uses only century-old sources. **Needs the owner's explicit period call — §8 #3.** |
| 7 | **A rain barrel** — a barrel under a downspout, not a 1.94 m `cistern` | rig, small | every StP house | `cistern` and `waterTank` are both too municipal for a dooryard |
| 8 | **A leaning marker post** for the bar head | rig, tiny | StP | the way off the island at low water should be *marked* |

Items 1–3 are what the night read actually depends on. Item 4 is shared with the yards lane and should be
scoped once, by whoever gets there first. Items 5–8 are polish.

---

## 7. Phasing — and every one of these is a LATER PR

**Nothing below is in this PR.** Each is a slice, scoped to be small, with what it waits on named.

| # | slice | region | waits on | why this order |
|---|---|---|---|---|
| **S1** | **Bend and paint `plot_path`** — give `yard_ginny_plot` its via points along the forest path corridor, and paint it like its two siblings | StP | nothing | the smallest slice in the arc, fixes an actual defect (§3.2), and proves the "route → lane + splat" seam end to end |
| **S2** | **The NMC lane table** — the 25 nodes of §4.2, builder-wired, validated as a tree, with the dry-ground check the roads already have | NMC | nothing | ⭐ **the highest-value slice in the document.** Everything else at NMC hangs off it, and it unblocks routines, which unblocks the region reading as inhabited at all |
| **S3** | **The wire-span renderer** (§6.2 #1) + the transformer at the line's town end + lamp arms on every third pole | NMC | S2 not required, but the ask is code | turns nine bare poles into a grid; the single biggest visual change per line of code in this arc |
| **S4** | **StP self-serviced pass** — the well on the green, propane at the kitchens, the genset at the parish hall, the small set behind the boat shed, lanterns at the slip and the dooryards | StP | the `Lantern` preset (§6.2 #2); best AFTER S3 | the other half of the differentiator — land it after S3 so the two night reads can be compared side by side. ⭐ **The acceptance test is the negative one in §3.4:** if the result reads as *regular*, it is wrong |
| **S5** | **NMC ditch + culverts** along Route 91 and at the neck | NMC | the ditch profile (§6.2 #5) | the mainland read by daylight |
| **S6** | **NMC routines** — give the town's residents days that walk S2's lanes | NMC | S2, plus the `ResidentDef` roster PR #606's own P1 slice produces | ⭐ **UNBLOCKED at design level** by the owner's 2026-08-20 ruling. The question this slice was blocked on — *who lives at Nine Mile Creek* — is answered elsewhere: **35 working adults**, sized off the fleet. It is now sequenced behind PR #606's roster-as-data slice, not behind a decision |
| **S7** | **Fences and alleys** — both regions | both | the fence kit (§6.2 #4) **and** the yard lane's polygons | ⚠ **must be one slice with the yard lane, not two.** The alleys in §3.2 exist only once the fences do |
| **S8** | **Route the new destinations** — restaurant, fish market, shipyard, the lifecycle cannery | NMC | those arcs merging | one `lot_*` leaf each; trivial once S2 exists |

**Suggested first two:** S1 (tiny, proves the seam) and S2 (unblocks the most). S3 is the one to reach for
if the owner wants the *visible* difference soonest.

---

## 8. Nag list for the owner — none of it blocking

> ✅ **Two of the original six are answered** by your 2026-08-20 rulings and are struck below rather than
> deleted, so the record shows what changed: *does Nine Mile Creek have residents* (**yes — 35**), and
> the trunk road's name (**Route 91**).

1. **Names for the rest of the routes.** **Route 91** and **Wharf Road** now have real names; nothing
   else does. Everything else above is functional — `route.stpeters.school_lane`, `road_civic`, "the bar
   road". Real places name their roads, and the names are free characterisation. **Does the lane past the
   store on St Peters have a name? The bar road? The gully path?** A settlement whose roads have names
   reads older than one whose roads have functions, and it costs nothing but the naming.
2. **Confirm the first city is FINNIGAN'S LANDING.** §5 now names it, on PR #605's authority — it sits up
   Route 91 with a harbour of its own, which makes the road this document plans at Nine Mile Creek
   literally the road to it. **A yes closes this; a different answer only changes the name in §5.**
3. **The period question, and it alone decides §6.2 #6.** ⚠ **Your "a combination of all types" ruling is
   about ENERGY USE CASES and I have not read it as an art licence.** §3.4 uses only sources the island
   could plausibly have had for a century — oil, wood, coal, bottled propane, a genset, a battery set. **Is
   a windmill or a solar panel in or out?** A solar panel on a saltbox would date the whole world in one
   prop, so I would rather have the ruling than guess.
4. ⚠ **The five crushed-shell town walks at Nine Mile Creek are still awaiting your eye** (carried over
   from the roads pass, not raised fresh here). They are the closest thing to a sidewalk in the game and
   the argument for them is genuinely balanced. **`NineMileCreekRoads.PublicTownLots` is the one list to
   delete if you rule against.** §3.3 defers to whatever you decide.
5. **Where the shipwright's yard really lives.** Carried, not raised: the boat-shed lot sits in §4.2's
   table under a neutral name because the 2026-07-25 ruling and the shipped scene disagree. ⭐ Now worth
   more than it was — PR #606 has **three shipyard jobs riding this same unresolved question.**
6. **The cannery still has no site**, and it is the last thing blocking §4.2's destination table and PR
   #606's four processing NPCs. Carried from the building-lifecycle arc, where you already owe it.

---

### ~~Answered by the 2026-08-20 rulings~~

- ~~**Does Nine Mile Creek have residents at all?**~~ **Yes — more than St Peters, with all the basic
  amenities of a town.** 35 working adults, sized off the fleet. S6 unblocked.
- ~~**Does the through-road have a name?**~~ **Route 91**, and it was already built.
