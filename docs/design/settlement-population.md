# Settlement population — who lives at the harbour, and what their day is

> **Status: DESIGN ONLY.** Nothing under `Assets/` changes. No NPC is placed, no routine authored,
> no Def created. This document does the **arithmetic** — how many people each settlement's own
> built geography implies, where they sleep, and how their day reaches the wharf — so that the
> slice which finally places them (`municipal-infrastructure.md` §7 **S6**) knows what it is filling.
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon). Rides the route
> substrate in [`municipal-infrastructure.md`](municipal-infrastructure.md) — **that document owns
> the lanes; this one owns the people who walk them.** Boat counts come from the shipped register,
> not from this document's imagination.

---

## 0. The ruling this document serves, 2026-08-20

The owner ruled five things:

1. **Every boat needs 2–3 NPCs to operate.**
2. They **all live at places**, and they **drive to the wharf** to work.
3. They go out fishing **on different routes**.
4. There are also **truck drivers who ship**, **NPCs running different equipment in processing**,
   and **NPCs at the restaurants and shopfronts**.
5. **Nine Mile Creek has more residents than St Peters Island**, and has *"all the basic amenities
   of a town"*.

⭐ **Ruling 5 overturns a standing position in shipped code.** `NineMileCreekPeople.cs` says, in its
own header: *"Nine Mile Creek is not a town and does not get a cast."* That was correct when the
region was a two-person creek; it is now wrong, and the docstring is part of what S6 rewrites. It is
flagged here so the slice does not read it as authority and stop.

It also closes `municipal-infrastructure.md` §8 nag **#6** — *"Does Nine Mile Creek have residents at
all? Nine lots, no people. If the answer is 'not yet', S6 drops off the plan."* **The answer is yes,
and more than the island has.** S6 stays on the plan, and §3 below sizes it.

### ⭐ Later rulings folded in (2026-08-20, after the first draft)

Three more rulings landed the same day, after this document's first commit, and **all three move numbers
this document computes.** They are recorded **as relayed by the coordinator, not as a verbatim
transcript** — **except** the one fragment set in quotation marks below, which is the owner's own words
as relayed.

| # | ruling | what it changed here |
|---|---|---|
| 6 | ⭐ **THE CANNERY HAS A SITE** — *"east part of the island near the docks — that's where the old exporting likely would've taken place."* Read as: **St Peters, the east shore by the docks**, where the historical exporting happened | ⚠️ **Closes F3 and Q7.** The four processing NPCs finally have ground to stand on — but **it is not in this region**, so they leave the Nine Mile Creek roster (§3.1, §3.2). Where they sleep and which way they cross is the roster slice's, and it lands squarely on **Q8**, which stays open |
| 7 | **The river lots are APPROVED — *"for now"*** | ⚠️ **Closes Q1, as option (a).** The town-river re-cut supplies Nine Mile Creek's dwellings. §3.3's shortfall is re-derived against it — and it is **bigger than the number the approval was given against**, because of ruling 8 |
| 8 | **The M2 boat target is TEN, not fourteen.** The owner ruled fourteen too many and **delegated the number**; the coordinator settled **10** (7 today + 3), leaving 4 of the wharf's 14 berths for the mussel class, visitors and the player | ⚠️ **Closes Q4** — which called itself *"the single number that most changes the size of everything above"*, and was right. §2.3 goes **18 afloat → 27**; §3.2 goes **35 → 44**; §3.3's shortfall goes **21 → 30** |

> ⚠️ **Ruling 8 is the only one here the owner did not fix personally, and it is recorded that way on
> purpose.** The ruling was *fourteen is too many*, plus a delegation of the number; **ten is the
> coordinator's, settled under that delegation.** It is written as settled so the arithmetic below has
> something firm to stand on — **and it is the owner's to override.** If it moves, §2.3, §3.2 and §3.3
> move with it, which is exactly what Q4 predicted.

---

## 1. What exists today — counted, not remembered

Every number in this section was read off the working tree at `c7a5c132`.

### 1.1 The two settlements, side by side

| | **St Peters** (island) | **Nine Mile Creek** (mainland) |
|---|---|---|
| `NpcDef` assets | **6** | **2** (Hector Bernard, Wendell Arsenault) |
| On a `RoutineDef` day | **6** | **0** — anchored, no clock |
| Housed | **6 of 6** ✅ | n/a |
| `RoutineLanes` nodes | **12** | **0** (S2 proposes 25) |
| Moored hulls in the scene | **0** | **7** |
| Registered boat owners | 0 | **7** `BoatOwnerDef` |
| Building lots | 4 dwellings + 3 public doors | **9** (3 houses + 6 non-dwelling) |
| Shops standing | general store, post office | fish market, restaurant |
| Road vehicles placed | none | **2** (Dually 3500, Otter 8×8) |

> ⚠️ **The asymmetry is the exact inverse of the population ruling.** St Peters has every person and
> no boat; Nine Mile Creek has every boat and almost no person. The island's six are fully housed and
> fully scheduled; the mainland's seven registered hulls carry **one figure each at most, and one not
> even that** — Hughie Campbell's `Skipper` field is `{fileID: 0}`.

### 1.2 St Peters — the six, and where they sleep

Read from `Data/Routines/*.asset`, last block of each day:

| villager | sleeps at | dwelling |
|---|---|---|
| Rose MacIsaac | `station.st_peters.home_saltbox` | the red saltbox |
| Eileen Doiron | `station.st_peters.home_farmhouse` | the white farmhouse |
| Junior Poirier | `station.st_peters.home_sage_a` | the fisherman's cottage |
| Basil Samson | `station.st_peters.home_sage_b` | the fisherman's cottage *(housemate)* |
| Marguerite LeBlanc | `station.st_peters.home_store` | **above the general store** |
| Aunt Ginny | — *(no `home_` station; her day ends at her own cottage)* | Ginny's cottage |

⭐ **Four roofs hold six people, and that is the housing law this document reuses.** The sage cottage
holds two, and the storekeeper lives over her trade. Neither is a hack — both are what a Maritime
village is — and both are the levers §3.3 pulls when Nine Mile Creek runs out of lots.

### 1.3 Nine Mile Creek — the register, as shipped

Seven `BoatOwnerDef` under `Data/Boats/Owners`. `Moorage`/`BerthIndex` shown as PR #597 (S3) leaves
them — that slice re-homes two owners onto the float rather than adding any boat:

| owner | boat | moorage | shed (`LotIndex`) | deck figure | prosperity |
|---|---|---|---|---|---|
| Leo Arsenault | Lobster boat | quay wall, berth 1 | 0 | `SkipperIso` | 3 |
| Marie Gallant | **Cape Islander** | quay wall, berth 3 | 1 | `DeckBossIso` | 3 |
| Ross MacDonald | Lobster boat | quay wall, berth 4 | 2 | `HandIso` | 2 |
| Yvette Doiron | Lobster boat | quay wall, berth 6 | 3 | `CutterIso` | 2 |
| Hughie Campbell | Lobster boat | quay wall, berth 7 | 4 | ⚠️ **none** | 1 |
| Celeste Bernard | Fishing skiff | **float**, berth 4 | 5 | `PackerIso` | 1 |
| Dan Peters | Punt | **float**, berth 2 | 6 | `FisherIso` | 0 |

**Seven boats. Five working hulls at the wall, two small craft on the float.** S3 and S4 move and
spread them; neither adds one.

> ⭐ **Seven is what ships; TEN is the M2 target** (ruling 8). This section counts the working tree and
> must keep counting it — the three extra boats are not in the register and nothing here pretends they
> are. **§2.3 is where the ten-boat arithmetic lives.**

> ⚠️ **`LotIndex` is a SHED, not a house.** `NineMileCreekLots` builds *"one shed per boat-owner on
> the register"* along the wharf. **Not one of the seven registered owners has a dwelling anywhere in
> the region.** They own a boat and a bait shed and sleep nowhere. That is finding **F1** (§3.3).

> ⚠️ **Four of the seven share one `LobsterBoat.asset`.** The wharf reads as four identical hulls;
> S3 spreads them across the variant pack. Crew variety rides the same problem — see §6.2.

---

## 2. Crew arithmetic — what the hulls can actually carry

**The owner's "2–3 per boat" is checked here against measured art, not applied flat.** Two shipped
sources constrain it, and they agree.

### 2.1 The rig measures two work stations on a lobster boat

`fleet-deck-occupancy.md` §3, from `lobsterBoatVariantsIsoRig.js`: every one of the 18 lobster
variants anchors **`helm 1 · hauler 1`** — **crew = 2**, byte-identical across all `(size, style,
region)` cells. The sport fisher anchors 4; the zodiac hurricane 4; the punt and skiff 1.

⭐ **So "2" is not a guess — it is what the boat is shaped for.** A skipper at the wheel and a hand
at the hauler is the whole lobster-boat scene.

### 2.2 A third hand fits the occlusion seam, with one slot to spare

Every mesh hull publishes exactly **12** deck-occupant slots
(`IsoFacetHullRenderer.DeckOccupantSlots = 12`), and `fleet-deck-occupancy.md` §3 measures a lobster
boat's *committed* demand at **10** — crew 2 · furniture 4 · stacks 2 · items 2.

| lobster boat | slots |
|---|---:|
| committed today | 10 |
| **+ a third hand** | **11** ✅ *(1 spare)* |
| + a third hand, *and* the cockpit crates dressed (§5's ceiling) | **16** ❌ *(over by 4)* |

> ⚠️⚠️ **F2 — the third hand and the bait-crate dressing pass cannot both land at 12 slots.**
> `fleet-deck-occupancy.md` §5 already records that dressing the five `tubSlots` crate anchors takes
> a lobster boat to **15**, over by 3, and explicitly declines to raise the constant because *"nothing
> in the fleet needs it today."* **A third crew member is the thing that needs it** — it burns the
> last spare slot, and makes the later overflow 4 instead of 3. The doc's own cheapest lever still
> applies and still clears it: **make the crates mesh fittings, which cost no slot at all.** This is
> a `lead-architect` / `art-pipeline` call, not a world-content one, and it is named here rather than
> solved.

⭐ **The seam itself is open — the wharf doc's blocker is stale.** `nine-mile-creek-wharf.md` §9 item
3 says a second deck figure is *"blocked on a Core seam."* It is not, any more: `IHullMeshRenderer`
now exposes `IDeckOccupantSlots DeckOccupants` alongside the single-occupant `SetDeckOccupant` shim,
and its own docstring says *"a hull carrying more than one thing (gear, pots, **a second hand**) must
claim its own slots there."* **#481 closed it.** §9 should be corrected when S6 lands.

⚠️ **What is still missing is an anchor, not a slot.** The rig anchors `helm` and `hauler`; a third
hand has a slot to occupy but **no measured place to stand**. She either stands on the
`cockpit_sole` deck polygon (which exists on both `LobsterBoatIso` and `CapeIslanderIso`) or the
art-director adds a third crew anchor to the rig. **Art ask, §6.1.**

### 2.3 The crew table this document proposes

Sized per hull rather than flat, and the aggregate still lands inside the owner's 2–3:

| boat | owners | crew each | total | rig stations | verdict |
|---|---:|---:|---:|---|---|
| Lobster boat | 4 | **3** | 12 | 2 + 1 unanchored | needs the §6.1 anchor |
| Cape Islander | 1 | **3** | 3 | 2 + 1 unanchored | same |
| Fishing skiff | 1 | **2** | 2 | 1 + 1 | fits |
| Punt | 1 | **1** | 1 | 1 | ⚠️ a punt is a one-man boat |
| **Total afloat** | **7** | | **18** | | |

**18 people put to sea from Nine Mile Creek**, of whom the 7 registered owners are already named —
so **11 crew NPCs are new**. Against the owner's flat rule, 7 boats × 2–3 = **14–21**; 18 sits
comfortably inside it, so the hull-sized reading *satisfies* the ruling rather than bending it.

#### ⭐ And at TEN boats — the M2 target, ruling 8

Ruling 8 adds **three boats** to the register and says nothing about their class, **because class is the
register's to choose and not this document's to invent.** So the honest output is a band, not a number:

| the three, if they turn out to be… | crew each | total afloat |
|---|---|---:|
| all working hulls (lobster boat / Cape Islander) | 3 · 3 · 3 | **27** |
| two working hulls and a skiff | 3 · 3 · 2 | 26 |
| all small craft (skiff, punt) | 2 or 1 each | as few as **21** |

⭐ **27 is the planning number, and it is the top of the band on purpose.** The quay wall is where working
hulls lie, and the float already carries both of the region's small craft; three more boats at a *working*
wharf are three more working boats until somebody rules otherwise. **Everything downstream is computed on
27 — and every figure below moves DOWN, never up, if the register picks smaller hulls.**

| | 7 boats (shipped) | **10 boats (M2)** |
|---|---:|---:|
| registered owners — named already | 7 | **10** |
| crew NPCs — new people to author | 11 | **17** |
| **total afloat** | **18** | **27** |

Against the owner's flat rule, 10 boats × 2–3 = **20–30**, and 27 sits inside it — so the hull-sized
reading still *satisfies* the ruling at the new target rather than bending it, exactly as it did at seven.

> ⚠️ **The three new boats inherit every constraint the seven carry**, and one of them bites harder: F2's
> deck-slot arithmetic (§2.2) is **per hull**, so three more working hulls is three more hulls wanting a
> third crew anchor and three more that overflow 12 slots if the bait crates stay sprites. **The §6.1 art
> ask does not get bigger; the cost of not doing it does.**

> ⚠️ **Dan Peters' punt is the one exception and it is deliberate.** Prosperity 0, a 4.5 m shed, and
> a deck def of `floor · bow_1 · stern_port`. Putting two men in a punt to satisfy a rule would read
> as a mistake. **Owner question Q3.**

---

## 3. The Nine Mile Creek roster

### 3.1 Shore jobs, derived from sites that are actually built

Every line below is a **placed or reserved site in the region**, not an invented workplace.

| workplace | status in the repo | people | what they do |
|---|---|---:|---|
| **Fish market** | ✅ shop placed (`NineMileCreekShops`) | 3 | grader, packer, counter |
| **Restaurant** | ✅ shop placed | 3 | cook, server, kitchen hand |
| **Harris & Sons shipyard** | ✅ drawn, `smallYard` tier | 3 | ⚠️ the flagged lot — see F4 |
| **General store** | ✅ lot | 1 | storekeeper |
| **Tavern** | ✅ lot | 2 | keeper + hand |
| **Chandlery** | ✅ lot | 1 | the rod |
| **Harbourmaster** | ✅ lot | 1 | the cod licence |
| **Buyer's truck** | ✅ Wendell Arsenault, placed | 1 | *(exists)* |
| **Used outboards** | ✅ Hector Bernard, placed | 1 | *(exists)* |
| **Freight / shipping** | ✅ Dually 3500 at the truck park | 1 | the trucker who ships |
| | **subtotal, built sites** | **17** | |
| ~~**Cannery / processing**~~ | ⭐ **SITED 2026-08-20 — but on ST PETERS**, the east shore by the docks | ~~4~~ | ⚠️ **leaves this region's roster — see F3** |
| | **subtotal, this region** | ~~21~~ → **17** | |

> ⭐ **F3 — CLOSED, and it closed by MOVING.** The owner's ruling named *"NPCs who run different equipment
> in processing"* and this region had no processing building. The answer, later the same day, is that the
> cannery goes *"east part of the island near the docks — that's where the old exporting likely would've
> taken place."*
>
> ~~The four processing NPCs are sized here and cannot be placed until the cannery has a site.~~
> **They can be placed now — but not here.** Three consequences, and the third is the live one:
>
> - ⭐ **Where they process:** the cannery, on **St Peters, the east shore by the docks** —
>   `cannery_yard` in [`municipal-infrastructure.md`](municipal-infrastructure.md) §3.2, a leaf off
>   `slip_head`, in the harbour-cove beach sector the island's coast plan already draws. That document
>   owns the site and the route; **this one owns only the fact that four people work there.**
> - **What it costs this region:** the four leave the Nine Mile Creek roster entirely. `FishStorePos` is
>   still documented as *"holds; **NO processing**"* — and that reads as **correct now rather than as a
>   gap.** ⭐ **The mainland holds, the island processes.** That is a cleaner division of labour than the
>   one this document assumed, and it gives the crossing a *cargo* reason to exist alongside the
>   player's.
> - ⚠️⚠️ **What is still open, and it is the roster slice's, not this section's:** **where the four
>   sleep, and which way they cross.** St Peters houses six in four roofs with **no spare bed** (§1.2),
>   so they are either islanders the island has to house, or they come over the water. **That is Q8
>   arriving from the other direction — and Q8 stays open.** Their names, builds, outfits and days are
>   P1's and P5's to author; nothing about them is settled here beyond the building they walk into.

> ⚠️ **F4 — the shipyard's three are contingent.** `lot_boat_shed` is flagged in two documents: the
> 2026-07-25 ruling says there is no shipwright in this region, and the shipped scene has one with the
> economy data hanging off it. Three jobs ride that unresolved question. **Not this document's to
> close** — it is the coordinator's.

### 3.2 The whole settlement

| | people |
|---|---:|
| Afloat (§2.3) — 10 owners + 17 crew, at ruling 8's ten boats | **27** |
| Shore, on built sites (§3.1) | **17** |
| ~~Shore, awaiting a cannery site~~ | ~~*(+4)*~~ ⭐ **the island's now — F3** |
| **NINE MILE CREEK — working adults** | **44** |

And the island, which is the other half of the comparison ruling 5 turns on:

| | people |
|---|---:|
| St Peters, housed and scheduled today (§1.2) | **6** |
| + the cannery's four, once the building stands (F3) | *(+4)* |
| **ST PETERS — working adults** | **6 → 10** |

**Nine Mile Creek: 44. St Peters: 6, or 10 with the cannery.** Ruling 5 still holds comfortably —
**more than four times over even at the island's larger figure** — and it holds for the same reason it
did at 35: the region's *own built geography* asks for these people, rather than a decree supplying them.

> ⚠️ **Both figures moved on 2026-08-20, in opposite directions, and neither move is this document's
> invention.** The mainland **grew by nine** (ruling 8's three boats) and **shed four** (ruling 6 put the
> cannery on the island); the island **gained those same four**. ~~35 buildable / 39 with the cannery~~ is
> struck rather than corrected so the record shows which ruling did which. Both are the owner's to move
> again — ruling 8 especially, since ten is a delegated number.

> **These are working adults, not residents.** A settlement of 44 jobs, with the partners and
> children a real community carries, is a *population* closer to **70–88** — the same **1.6–2.0×** the
> 35-job reading used (55–70), applied to the new figure. **This document counts only people the world must draw
> and schedule.** Whether children and non-working partners exist as NPCs at all is **owner question Q6**;
> the art already has `BoyBuild` / `GirlBuild` / `NanBuild` if they do.

### 3.3 ⚠️⚠️ F1 — the housing does not remotely fit, and that is the headline finding

Nine Mile Creek's **nine lots** break down as **three dwellings** and six non-dwelling doors:

| dwelling lots | non-dwelling lots |
|---|---|
| `HouseNorthPos` · `HouseNorthWestPos` · `HouseSouthPos` | chandlery · harbourmaster · general store · tavern · parish hall · boat shed |

Apply St Peters' own housing law (§1.2) — a cottage can hold housemates, and a shopkeeper lives over
the trade:

| source of beds | households | adults housed |
|---|---:|---:|
| 3 houses, at St Peters' 2-adult cottage rate | 3 | **6** |
| Over the trade: store, tavern, chandlery *(the Marguerite precedent)* | 3 | **6** |
| Harbourmaster's quarters | 1 | **2** |
| **Total, stretching every shipped precedent** | | **14** |

**Roster 44. Housing 14. Short by 30 — the region can house fewer than one in three of the people its
own wharf implies.** Even counting only the **27** who go to sea, it is short by **13**.

> ⚠️ ~~**Roster 35. Housing 14. Short by 21.**~~ **The shortfall grew by nine on 2026-08-20, and it grew
> for exactly one reason:** ruling 8's three extra boats put nine more people afloat. ⚠️ **The cannery
> ruling did NOT shrink it** — the four processing hands were never counted in the 35 (§3.2), so moving
> them to St Peters takes nothing off this region's beds. **The struck line is left standing because the
> river-lot approval below was given against it, not against 30.**

**And on 2026-08-20 the owner picked one of the three ways forward.** All three are kept below, because
the two that were not picked are now *supplements* to the one that was, rather than alternatives to it:

1. ⭐⭐ **APPROVED, owner 2026-08-20 (Q1) — the river lots, *"for now"*.** The town-river re-cut
   ([`harbour-geography.md`](harbour-geography.md) §5) supplies Nine Mile Creek's dwellings, and
   **waterfront lots along a boardwalk is exactly the shape that houses fishermen.** Two things the
   approval must be read with, and neither is a quibble:
   - **The hedge is the owner's own, relayed: *"for now".*** It approves the lots. It does not approve a
     final count of them, and it can be revisited.
   - ⚠️⚠️ **THE NUMBER IS BIGGER THAN THE ONE THE APPROVAL WAS GIVEN AGAINST.** This option was costed
     at *"roughly ten more dwellings"* against a shortfall of 21. At **30**, and at this document's own
     2-adults-per-dwelling rate, the river banks are being asked for **≈ 15 dwellings**. And
     `harbour-geography.md` §5 sizes the river, the bridge, the bank slopes and the boardwalks but
     **does not size the lots** — so *nobody has yet checked that fifteen of them fit.* That check
     belongs to the river slice and should happen **before** the lots are drawn.
2. **Not everyone lives in town.** Rural PEI strings houses along the road out of the settlement —
   past `road_north` and `road_south`. Cheap: the through-road already runs 564 m and S2's lane table
   already has the nodes. ⭐ **Now a supplement, not an alternative** — it is the cheapest way to take
   pressure off the fifteen if the banks will not hold them.
3. **Some crew commute from off-region.** Honest, free, and invisible: a hand who drives in from
   Finnigan's Landing needs no bed, only a truck at the truck park. ⚠️ But it weakens *"they all live
   at places"*, which the owner said plainly. **Owner question Q2 — STILL OPEN**, and it is now the
   single biggest lever on the fifteen: ⭐ **every hand who commutes is half a dwelling the river does
   not have to carry.**

---

## 4. The working day

### 4.1 The engine's shape, and what a day may therefore be

`RoutineDef` is one asset per person: an `Id`, an `NpcDef`, and an array of
`RoutineEntry { StartHour, StationId, Activity, Why, Uninterruptible }`. A block runs from its own
`StartHour` to the **next** entry's; the last runs through midnight to the first. **There is no way
to author a gap or an overlap, because neither is expressible.**

`StartHour` is a **departure, not an arrival** — at that hour the villager leaves and starts walking.
`RoutinePlanner.Build` runs **once, at spawn**, resolves every station to a lane node, writes the
polylines and measures them; after that, where anyone is at time *t* is a pure sample. **Nothing
ticks and nothing is saved** (CLAUDE.md rule 5).

⭐ **Every day proposed below is expressible in exactly that shape** — a list of `(hour, station,
activity)` with no new persistent state. That was the binding constraint on this section, and it held
everywhere except the two places §4.2 and §4.4 name explicitly.

### 4.2 The fishing day, gated by the tide

⚠️ **The harbour dries, and that is not a flavour note — it is measured.** Against Nine Mile Creek's
−1.6 m bed under the mainland's ±2.2 m swing (`nine-mile-creek-wharf.md` §4, 2026-08-06 correction),
the fraction of the tidal cycle each hull is afloat:

| hull | afloat | in a 12.42 h cycle |
|---|---:|---|
| Dory | 70.1% | 8 h 42 m |
| Fishing skiff | 69.2% | 8 h 36 m |
| Punt | 66.7% | 8 h 17 m |
| **Lobster boat** | **54.4%** | **6 h 45 m** |
| **Cape Islander** | **52.9%** | **6 h 34 m** |
| Side dragger | 29.9% | 3 h 43 m |

*(Tidal period `GameConfig.TidalPeriodHours = 12.4206`.)*

⭐ **The five working hulls are afloat a little over half the time, centred on high water — so the
fleet's departure is not a habit, it is a window.** A lobster boat has ~6 h 45 m of water per cycle;
her crew must be aboard inside it or she sits on the mud.

**What that buys the world, for free:**

- **The wharf empties and fills on the tide, not on the clock.** Two tides a day, running ~50 minutes
  later each day, means the fleet's departure walks around the clock across a week. A player who
  learns *"the boats go on the flood"* has learned something true.
- **The float is the exception, and it now reads as one.** The two small craft are afloat ~2 hours
  longer per cycle — and the float's deck rises with them (PR #594). **The small-craft owners have a
  wider window than the working fleet**, which is the correct and slightly unfair-feeling truth.
- ⚠️ **A departure block must be authored against the tide, and `RoutineEntry.StartHour` is a clock
  hour.** This is the one place the day model and the engine rub. **It does not need a new system**:
  the tide is a pure function of `(worldSeed, gameTime)` too, so *"leave on the next flood"* is
  derivable at plan time from the same inputs the planner already has. But **it is not expressible in
  the shipped `StartHour` field**, and S6 will discover that. Named, not specced — see §5.

### 4.3 The shape of a crew's day

Six blocks, all expressible as `RoutineEntry`s:

| block | activity | notes |
|---|---|---|
| **wake at home** | home station | the dwelling §3.3 owes them |
| **drive to the wharf** | the commute (§4.4) | walk to the vehicle, drive, park, walk to the berth |
| **muster on deck** | deck-occupant slot on their own hull | §2.2 — the beat the seam already supports |
| **at sea** | *off the region's lanes entirely* | §4.5 |
| **land the catch** | fish market / buyer's truck | where the shore roster meets the afloat one |
| **home** | reverse the commute | |

⭐ **The "muster on deck" beat is the cheapest and most valuable one to build first.** It needs no new
engine at all — the deck-occupant seam is shipped, the hulls are moored, the tide is computed.
**Giving the existing seven skippers a second and third figure, and having them arrive and leave on
the tide, is most of what the ruling asks for visually**, before a single boat moves.

### 4.4 ⚠️ The commute — what a "drive" routine needs that a walk routine does not

The owner ruled that crews **drive** to the wharf. The region has two drivable vehicles placed
(`Dually3500` at the truck park, `Otter8x8` at the boat ramp), both as `ParkedVehicle`, both
**player-driven only**. Nothing in the repo drives an NPC anywhere.

`municipal-infrastructure.md` §2.1's governing rule binds here: **one polyline table per region is the
route truth, and there must not be a third path system.** A drive therefore runs on the same
`RoutineLanes` a walk runs on. What it needs *on top* — **named, not specced**:

1. ⭐ **A per-leg travel mode. This is the real one.** `RoutineDef` carries **one**
   `WalkSpeedMetresPerSecond` for the whole person. A resident who walks to her truck, drives to the
   wharf, and walks to her berth has **three speeds in one day** and the Def has room for one. The
   speed has to move from the *person* to the *leg*, or a per-leg mode token has to select it.
2. **The vehicle's position must stay a pure function of the clock.** The truck is at the house at
   06:00 and at the wharf at 07:00. That is shared mutable state across two legs — exactly what rule 5
   forbids. **Resolution: the vehicle is not an actor, it is a dependent** — its pose is *sampled from
   its driver's plan*, the same way the driver's is. A vehicle with no driver's routine simply stands
   where it was placed. ⭐ **This keeps the engine pure and needs no save data.**
3. **Corner radii.** A lane polyline is a walking centreline and turns 90° in a dooryard. `VehicleDef`
   models real steering (`SteerRateFullLocksPerSecond`, `SteerFalloffHalfSpeedMetersPerSecond`).
   Either the drive follows the polyline and the steering model is ignored for NPCs, or street-class
   edges gain a radius. **The cheap answer is the first**, and it is a `gameplay-systems` call.
4. **Parking is a leaf, so a commute is a composite.** `truck_park` is already a **leaf** in S2's
   table. A drive ends there and continues on foot — so **one block cannot be one mode**, which is the
   same finding as (1) reached from the other end.
5. **Block sizing changes by an order of magnitude.** Published walk budgets: landing → town **2:24**,
   wharf → town **1:47**. A Dually at its 11 m/s cap covers Wharf Road's 322 m in **29 seconds**; even
   at a sane in-town 6 m/s it is **54 seconds**. ⚠️ **Driving is 5–10× faster than walking, and
   `StartHour` is authored in decimal hours.** A 29-second leg is 0.008 h. The engine samples
   continuously, so it is fine to *simulate*; it is **not fine to author**. S6 will want to express a
   departure as something other than a decimal hour — the same conclusion §4.2 reaches from the tide.

> ⚠️ **F5 — none of the above is scheduled anywhere.** The truck arc built a drivable vehicle and the
> routine arc built a walkable day, and **no lane in the backlog joins them.** The owner's commute
> ruling is the first thing that requires it. **This is a `gameplay-systems` + `lead-architect` ask,
> and it is the largest engine item the ruling implies.**

### 4.5 "Different routes" — the fishing grounds

The ruling says the boats go out on **different routes**. Two things make that cheap:

- **A route out of the harbour is not a `RoutineLane`.** Lanes are a *land* tree, validated as acyclic
  and rooted at the town. Water is not in it and must not be — putting sea legs in the lane tree would
  break the tree property `municipal-infrastructure.md` §2.3 defends. **A boat's route is its own
  polyline, owned by the register, not by the region's lane table.**
- **The ambient fleet already sails NPC boats.** `nine-mile-creek-wharf.md` §9 records it:
  *"`AmbientFleetDef` and friends already sails NPC boats, sets buoys and hauls them; the arc is
  joining that behaviour to these owners rather than writing it fresh."* ⭐ **The route system to
  reuse already exists.**

**The data shape, by name only:** a **`FishingGroundDef`** (a named patch of water — the ledges, the
bar, the mussel leases) and a **`RouteAssignment`** on the register binding one owner to one ground
plus the leg out. Per-boat, so **ten boats leave ten ways** at the M2 target — seven today; append-only
ids per CLAUDE.md §5.

⭐ **The nav marks are already placed and already computed** (PR #575: channels are data, IALA-B
buoyage derived). **A boat leaving Nine Mile Creek already has a marked channel to leave by** — the
routes should start on it, not beside it.

---

## 5. Proposed Def shapes — names only

Per CLAUDE.md rule 2, all content is data. **None of these is designed here; each is named so S6 and
the economy lane are talking about the same object.**

| name | one entity per | what it would carry | why it does not exist yet |
|---|---|---|---|
| **`ResidentDef`** | person | who they are, their build + outfit, their dwelling, their workplace | ⭐ **the missing keystone.** `NpcDef` is an *interaction* (dialogue, voice, build); `RoutineDef` is a *day*. **Nothing says where a person lives or what they do for a living.** |
| **`DwellingDef`** | dwelling | which lot, how many beds, which household | ⚠️ `HomeDef` exists but is **the player's lodging only** (deed, price, purchasable) — `Data/Homes` holds exactly one asset, the camper. **It is not a villager housing def and should not be bent into one.** |
| **`CrewAssignment`** | crew berth | resident → boat → deck station | the register has a single `Skipper` field; 2–3 crew needs a list |
| **`RouteAssignment`** | boat | owner → fishing ground → the leg out | §4.5 |
| **`FishingGroundDef`** | ground | the patch of water, its species, its tide gate | §4.5 |
| **`ShiftDef`** *(or fields on `ResidentDef`)* | job | which workplace, which hours, tide-gated or not | ⚠️ **must not become the M3 staffing system** — see below |

> ⚠️ **`Data/Staff` exists and is EMPTY.** That folder is the M3 hiring/automation lane's, and **this
> document deliberately puts nothing in it.** The distinction the phase rule turns on: **this doc is
> about who EXISTS and what their day is; M3 is about who the PLAYER employs.** A resident who works
> at the cannery is world content. A resident the player *hires* is economy. The Defs above describe
> the first and must not quietly implement the second.

⚠️ **One engine-shaped gap, named not specced:** §4.2's tide-gated departure and §4.4's per-leg travel
mode are both **`RoutineDef` shape changes**, not new systems, and both stay inside "a pure function
of the clock". Whoever owns S6 should expect to touch `RoutineEntry`.

---

## 6. Art asks

### 6.1 What the crews need that does not exist

1. ⭐ **A third crew anchor on the lobster-boat rig.** §2.2: the rig anchors `helm` and `hauler`; the
   owner's ruling wants a third hand who has a slot but nowhere measured to stand. Either a third
   anchor in `lobsterBoatVariantsIsoRig.js` (and the Cape Islander's), or a ruling that she stands on
   the `cockpit_sole` polygon. **Art-director call.**
2. **Bait crates as mesh fittings rather than sprites** — §2.2 / F2. Not a new sprite: a change of
   path for one that already exists, and the cheapest thing that clears the slot overflow.

### 6.2 ⚠️ Do NOT re-ask for these — they ship

The character art for a 44-person town is **almost entirely already baked**:

| what a roster needs | it already ships as |
|---|---|
| working bodies | **5 builds** — `CutterBuild`, `DeckBossBuild`, `HandBuild`, `PackerBuild`, `SkipperBuild` |
| elders / children | `GinnyBuild`, `NanBuild`, `BoyBuild`, `GirlBuild` |
| clothing variety | **8 outfits** — Capelin, DeepNavy, Dogwatch, HarbourTeal, Oilskins, RustAndCream, Spruce, SquallGrey |

**5 working builds × 8 outfits = 40 combinations for a roster of 44.**

⚠️⚠️ **That is no longer enough to go round, and it stopped being enough on 2026-08-20.** At 35 the
wardrobe was *adequate, but only just*; at ruling 8's ten boats it is **four short of one-each**, so the
roster now **must** repeat a build/outfit pair before anybody chooses to. Two caveats carried from the
wardrobe arc make that worse rather than better: **oilskins collide with blond hair** (4 of 5 shades
byte-identical — owner owes a ruling), and **four of seven owners already share one hull**. Visual
sameness is a live risk at this wharf from **three** directions at once now, not two.

⚠️ **Assigning build + outfit across the roster should be deliberate and recorded, not left to whoever
places each NPC.** ⭐ **And it is a real argument for Q5's generated answer** — a generator can place its
unavoidable repeats where the player is least likely to read them as duplicates, which a hand-authored
roster of 44 cannot promise. **Q5 stays the owner's, and this is evidence for it, not a decision.**

---

## 7. Phasing — what this sizes, and in what order

**Nothing here is in this PR.** Slice letters are this document's; the S-numbers are
`municipal-infrastructure.md` §7's.

| # | slice | waits on | why this order |
|---|---|---|---|
| **P0** | ⭐ **Crews on the moored decks** — a second and third figure on the **seven** hulls the register ships, arriving and leaving on the tide | nothing but the §6.1 anchor | **the most visible beat for the least work.** The seam is shipped (§2.2), the hulls are moored, the tide is computed. No routine engine, no lanes, no housing. ⭐ **Do it at seven** — ruling 8's three extra boats are an M2 register change and must not be smuggled in here |
| **P1** | **The roster as data** — `ResidentDef` + `DwellingDef` for the **44**, unplaced | ✅ **Q1 answered** (river lots, 2026-08-20) · ⚠️ **Q2 still open** | makes the population reviewable before anything is built. ⚠️ **P1 cannot finish while Q2 is open** — a `DwellingDef` per resident is exactly the thing a commuter does not have |
| **P2** | **NMC routines on foot** — this is `municipal-infrastructure.md` **S6**, now sized | **S2** (the 25-node lane table) + P1 | S6 finally has a cast |
| **P3** | **The commute** — per-leg travel mode, vehicle-as-dependent | §4.4, `gameplay-systems` | the largest engine item; P2 works on foot without it |
| **P4** | **Different routes** — `FishingGroundDef` + `RouteAssignment`, joined to the ambient fleet | P0, and the three M2 hulls if they have landed | the boats finally leave — **ten ways at the target** |
| **P5** | **Processing** — the four cannery hands | ~~the cannery site the owner owes~~ ✅ **SITED** · now: the building standing on St Peters, + `municipal-infrastructure.md` **S9** | ⭐ **no longer blocked on a ruling.** It is blocked on *work* now, and on a different region's work: a building placed in the harbour cove and one lane leaf to reach it |

⭐ **P0 and P2 are the two that make the ruling read.** P0 needs nothing that is not already shipped.

⚠️ **P5 moved regions on 2026-08-20 and the phasing table is the place that is easiest to miss it.**
Processing is no longer a Nine Mile Creek slice at all — it is a **St Peters** one, sequenced behind that
island's building-lifecycle work rather than behind S2 or S6. **A slice plan that still reaches for
`wharf_yard` is reading the pre-ruling draft.**

---

## 8. Questions for the owner

> ✅ **Three of the eight are answered** by your later 2026-08-20 rulings. They are **struck in place
> rather than deleted**, so the record shows what changed and what it was changed from. **Five remain
> open, and Q2 and Q8 got sharper rather than smaller.**

1. ~~⚠️⚠️ **The housing shortfall is the one real blocker (F1).** Nine Mile Creek can house ~14 and the
   roster is 35. Which way — **(a)** the town-river re-cut adds ~10 waterfront lots on the boardwalks,
   **(b)** houses string out along the through-road, or **(c)** some crew commute from off-region?~~
   ✅ **ANSWERED 2026-08-20 — (a), the river lots, approved *"for now"*.** §3.3 is rewritten around it.
   ⚠️ **One thing to know about your own answer:** you approved it against a shortfall of **21**
   (≈ 10 dwellings). Q4's ten boats moved the shortfall to **30**, so the river banks are now being asked
   for **≈ 15 dwellings** — and nobody has yet checked that fifteen fit. **Q2 below is the lever that
   brings that number back down**, which is why it is worth more than it looks.
2. ⚠️ **STILL OPEN — and it is worth more than it was. "They all live at places" — does that mean all,
   strictly?** If a hand may drive in from Finnigan's Landing and never be housed, beds come off the
   river's bill at no cost at all. ⭐ **Every commuter is half a dwelling the river banks do not have to
   carry**, and Q1's approval is now being asked for ≈ 15 of them. If the answer is *all, strictly*, the
   river must supply real beds for every one of the 30.
3. ⚠️ **STILL OPEN — Dan Peters' punt, one man or two?** Sized at 1 here, against your flat 2–3, because
   two men in a punt reads as an error. Say the word and it goes to 2. **Unaffected by the ten-boat
   ruling** — this is about the hull that is already in the register, not about the three being added.
4. ~~**How many boats is the M2 target?** Seven today. `nine-mile-creek-wharf.md` carries *"~16 lobster
   boats and the new mussel-boat class"* as vision, and the wharf has **fourteen berths**. Fourteen
   boats at three crew is 42 afloat alone — which turns Q1 from "short by 21" into "short by 60".
   **This is the single number that most changes the size of everything above.**~~
   ✅ **ANSWERED 2026-08-20 — fourteen is too many, and you delegated the number. ⭐ SETTLED AT TEN**
   (7 today + 3), which leaves **4 of the 14 berths** for the mussel class, visitors and you.
   ⚠️ **Recorded as settled under your delegation — ten is the coordinator's number, not yours, and it
   is yours to override.** It was indeed the single number that most changed the size of everything: 18
   afloat → **27**, roster 35 → **44**, shortfall 21 → **30**, and the wardrobe (§6.2) ran out.
5. ⚠️ **STILL OPEN — are the crews named individuals, or generated?** **44** hand-authored people with
   dialogue is a very large content bill, and it grew by nine on 2026-08-20; **27 named** (the owners
   and their mates) with the rest as recognisable-but-unnamed working figures is the Animal
   Crossing-scale answer. ⭐ **§6.2 now argues for the generated half**: 5 builds × 8 outfits = 40
   combinations no longer covers 44, so *something* repeats — the only question left is whether the
   repeats are placed deliberately or discovered by the player.
6. **Do children and non-working partners exist as NPCs?** `BoyBuild` / `GirlBuild` / `NanBuild` are
   baked and unused. A town with no children reads wrong; a town with 20 more scheduled bodies costs.
7. ~~⚠️ **The cannery still has no site** (F3), and four processing jobs wait on it. Carried from
   `municipal-infrastructure.md`, not raised fresh.~~
   ✅ **ANSWERED 2026-08-20 — *"east part of the island near the docks — that's where the old exporting
   likely would've taken place."*** F3 is closed, and it closed by **moving**: the cannery is on
   **St Peters**, so the four processing hands are the **island's**, not Nine Mile Creek's.
   ⚠️ **What is left is not yours and is not blocking:** whether the building stands as a relic the
   building-lifecycle arc restores or as a working plant from the start is that arc's call, and it is
   what decides *when* the four have a shift. **See Q8, which your ruling just made load-bearing.**
8. ⚠️⚠️ **STILL OPEN — and your cannery ruling just made it load-bearing. Does anyone live on St Peters
   and commute BY BOAT?** It used to be a lovely optional thread: the island has 6 residents and **zero
   working hulls in its scene**, so an islander who crossed to the Creek to crew would use the bar-road
   landing that is already a lane node — **but it needs a boat the island does not have.**
   ⭐ **Now the crossing has traffic in the other direction whether or not you take that thread.** The
   cannery is on St Peters and four people work there; the island houses six in four roofs with **no
   spare bed** (§1.2). So either the island gains housing, or four people cross to it daily. **This is
   the question F3 hands you, and P1 cannot finish the roster without an answer.**
