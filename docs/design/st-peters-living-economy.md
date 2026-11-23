# St Peters: a living island economy, with or without the player

Status: plan for the owner's review (2026-11-22). Docs only; slice 1 is built separately as a draft PR.
Serves **P3 A Living Working Coast** (the island runs on its own rhythm) and **P4 Earn It, Then Automate
It** (the jobs the player later takes over, or hires for, already exist and are already done by someone),
with **P1** (the bar and the tide decide supply days) and **P5** (a gale at springs empties the back room).

The owner's ask, verbatim: *"lets create missing npcs and routines to have this a functioning island
economy wether the player is there or not"*.

---

## 0. In plain words (read this first)

1. Today six people live on St Peters. They walk believable days, but **nothing they do moves goods or
   money**: the store's shelves restock on a fixed weekday, nobody fishes from the island, nobody buys or
   ships fish, and Junior's quad run is a walk with a speed, not a load.
2. The island already has the numbers worked out: what it must import from Nine Mile Creek each day
   (about 100 kg, mostly groceries, fuel, ice and bait), what can carry it, and when the bar is dry.
3. The plan adds the missing working roles one small step at a time, mostly using people who **already
   exist as characters** but have no day yet (the Gillis pair, Dougie MacNeil, Rene Chiasson, the Landry
   family).
4. The island's daily life becomes a simple loop: **boats land fish, hands ice and crate it, it goes over
   the bar to the Nine Mile Creek plant; groceries and fuel come back over the bar to Marguerite's store;
   households and boats use them up; wages pay for them.**
5. All of it is worked out from the calendar and the tide, the same way the villagers' walking already
   is. So when the player is away or asleep, nothing has to "run" in the background: the island's state
   on any day is **recomputed** when he comes back.
6. **Only what the player changes needs saving** (what he bought off the shelf, what he sold into the
   island). That is a small save change, and it is **yours to approve later**; nothing in the first
   slices needs it.
7. Slice 1 (built now, invisible in game) is the island's **back-room ledger**: how much of each good
   the store holds each morning, given the supply runs the tide allows.
8. The first visible slices: Marguerite's shelves thin before a spring tide and fill after Junior's run;
   Junior and the quad actually cross every second day with a loaded trailer.
9. Later slices bring island boats, the wharf stage, the truck run to the plant, the new residents, and
   the tractor with forks.
10. Nine one-line questions at the end need your call; each has my lean.

---

## 1. Who is there today (main at `f1b4ab2f59`)

`NpcDef` has no home or job field: home is the routine's last `home_*` station (or a `HomeDef`), the job
is read from the routine's Work stations and from `SellerDef.KeeperNpcId`. Routines are one pure function
of the clock (`RoutinePlan.SampleAt(hour)`): nothing ticks, nothing is saved. Activity codes are tags only;
nothing in the economy reads them.

### 1.1 Placed, with a routine (`Scenes/StPeters.unity`, `region.st_peters`)

Hours are departures (the walk starts then). The last row runs past midnight.

| Def id | Home | Job (from data) | Day, hour by hour | Touches the economy? |
|---|---|---|---|---|
| `npc.aunt_ginny` | her cottage on her plot | teaches the loop; gardener | 04:30 village mark (work) · 17:00 cottage garden (work) · 20:00 her step (home) | Plot `GinnyKitchenPlot` (garden produce, an island-own good). No code effect |
| `npc.basil_samson` | sage cottage (`home_sage_b`) | wharf watcher, takes lines | 05:00 wharf head · 12:00 slip head · 15:00 wharf head (leisure) · 18:00 home | None in code |
| `npc.eileen_doiron` | white farmhouse | teacher | 07:30 dooryard · 08:15 school desk · 12:00 store (customer) · 13:15 school desk · 17:30 flats lookout · 20:30 home | Store customer (no purchase in code) |
| `npc.junior_poirier` | sage cottage (`home_sage_a`) | clam digger; quad supply runner (`supplyrole.quad_runner`) | 06:00 dooryard · 06:30 flats head (digs) · 12:00 store (customer) · 14:30 flats head · 19:00 green · 21:00 home. **Wednesdays:** 08:30 quad over the bar to the NMC landing · 10:00 NMC fuel stop · 11:30 quad back · then as usual | The crossing is a carrier label and a speed only; it carries nothing |
| `npc.marguerite_leblanc` | over the store | storekeeper and mail carrier; keeper of `seller.leblancs` | 07:00 store counter · 13:00 post office door (errand) · 14:30 counter · 19:00 green · 21:00 home | Keeper of `shop.st_peters_general` (rod, capelin, ice lines; deliveries twice a week from a fixed weekday mask). Her presence is not read: the shop has no hours tied to her day |
| `npc.rose_macisaac` | red saltbox (`home.st_peters_red_saltbox`) | post office counter | 07:00 dooryard · 08:30 post office counter · 12:30 store (customer) · 14:00 counter · 18:00 green · 21:30 home | None in code |

`npc.ned_letter` is an object (the letter), not a person.

### 1.2 Characters that exist but have no day (no routine, not placed)

From the village plan §8 (homes ruled: the cannery hands live on the island, children live in St Peters,
2026-11-13). Their gift lines already hint at a life by the water.

| Def id | Home (plan) | Role in the plan |
|---|---|---|
| `npc.theresa_gillis`, `npc.angus_gillis` | cream saltbox | "cannery hands"; Theresa is also proposed for the foot errand pair |
| `npc.dougie_macneil`, `npc.rene_chiasson` | harbour gable | "cannery hands" |
| `npc.paul_landry`, `npc.monique_landry` | white gable | parents; work left open |
| `npc.lucie_landry`, `npc.sam_landry` | white gable | children for Eileen's school; Lucie is proposed for the foot errand pair |

### 1.3 Buildings and who uses them

| Building | Lives there | Works there |
|---|---|---|
| General store (`building.stp_general_store`) | Marguerite (upstairs) | Marguerite; Eileen, Junior, Rose come at noon |
| Post office (`building.stp_post_office`) | | Rose; Marguerite calls at 13:00 |
| School (`building.stp_school`) | | Eileen (no pupils placed yet) |
| Red saltbox | Rose | |
| White farmhouse | Eileen | |
| Sage cottage | Junior, Basil | |
| Ginny's cottage and plot | Ginny | Ginny (garden) |
| Cream saltbox, harbour gable, white gable | empty (proposed homes above) | |
| The cannery (`Village_stPetersCannery`) | | **nobody: it is derelict by ruling (2026-08-19)**; restarting it is the player's long-arc goal (M3) |
| Wharf and slip | | Basil watches; no boat works from the island |
| Flats and bar head | | Junior digs clams; the bar is the low-tide road to Nine Mile Creek |

### 1.4 Across the bar (what the island trades with)

At Nine Mile Creek: Wendell Arsenault (fish buyer, truck run 04:45 to 20:30), Hector Bernard (outboards,
fuel run), Claudette Boudreau (chandlery and store; her Def is listed by the builder but not found placed
in the scene), Bernadette Pitre (fishing gear wholesaler), Lionel Cormier (boat gear wholesaler), the
harbourmaster, the yard, the vehicle dealer and industrial yard (no keepers). Three NPC lobster boats
(Arsenault, Doiron, Gallant) fish and land a pure-function day, but **their landings move no money and
touch no market**. The NMC fish plant (`fishplant.nmc_industrial`) has a full logic model
(`FishChainModel`) that nothing composes yet. No ferry. Rice Point has no people yet.

### 1.5 What already exists for the economy, but sleeps

| Piece | What it does | Runs today? |
|---|---|---|
| `Market` (three outlets incl. `StPetersStore`) | supply and demand per fish category; halves the glut each midnight | only for loaded scenes; its glut is not saved |
| `ShopStockService` | shelf stock per line, lazily catches up missed delivery days | dormant: nothing composes it; state in memory only |
| `FishChainModel` | wharf, plant lines, trucks, outlets; `AdvanceTo(seconds)` catches up any gap | tests only |
| `FishPlantIntake` | grades delivered catch and pays | tests only |
| Island supply (`Core/IslandSupply`) | goods, carriers, roles, bar windows, run plan arithmetic | pure maths; nothing stocks or sells a good |
| `CustomLedger` | session tally of trade per day | session only |

---

## 2. Who is missing for a working island

"Exists" means a placed character with a routine doing the job. "Def only" means the character exists
with no day. Roles are limited to what the canon and the owner's rulings support; anything else is marked
**decision**.

| Job a small fishing island needs | Support in canon / rulings | Today | Proposal |
|---|---|---|---|
| Inshore lobster fishers (in season) | P3; lobster season data for St Peters exists (`LobsterStPeters`); NMC already has three NPC pot boats | none | **decision Q1**: one island lobster boat, Monique Landry skipper, Angus Gillis sternman |
| Handline / mackerel fishers | P3; the player's own early loop | none | the same boat out of lobster season (no new person) |
| Clam digger | built | Junior (exists) | keep; his dig becomes a supply of clams to the stage in slice 5 |
| Mussel and oyster pickers at low water | ruled 2026-11-13 (shellfish forward; villagers pick at low water) | none yet | folded into the residents' days in slice 6 (Rene and Lucie's gift lines already point to oysters) |
| Fish buyer / stage hands on the island | the plant model has `fishstore.st_peters_wharf` and `fishrun.st_peters_to_plant` | none | **decision Q2**: while the cannery is shut, Theresa Gillis, Dougie MacNeil and Rene Chiasson work the **wharf stage** (weigh, ice, crate, salt on the flakes) |
| Cannery workers | the cannery is derelict by ruling; restart is the player's M3 arc | none | wait for the restart; the stage hands are its first staff when it reopens (P4) |
| Storekeeper | built | Marguerite (exists) | slice 2 ties her shelves to the back room |
| Postmaster / mail | built | Rose at the counter, Marguerite carries | mail rides the quad run (`islandgood.mail_parcels`, 3 kg a day) |
| Supply runner over the bar | ruled 2026-11-15 (people cross at low tide with real carriers) | Junior, weekly, carries nothing | slice 3: every second day with the trailer load (**decision Q4**) |
| Foot errand pair | ruled 2026-11-15 (proposal) | Def only (Theresa, Lucie) | weekly on market day; later slice, after Q2 |
| Tractor driver (forks and bucket) | ruled 2026-11-15: one St Peters tractor driven by one NPC | none | **decision Q3**: Paul Landry, the island's carter, who also keeps the family's rows in the west garden |
| Farmer / gardener | player farming ruled; Ginny's plot exists | Ginny gardens | Ginny and the Landrys' rows; produce is an island-own good (not imported) |
| Teacher | built | Eileen | keep |
| Children / pupils | ruled 2026-11-13 | Def only (Lucie, Sam) | slice 6: school 08:15 to 15:00 on school days |
| Boat builder / repair | canon: no shipwright on the island; the player repairs his dory himself; the NMC yard repairs | none on island (correct) | none |
| Fuel | gasoline is an imported island good (20 L a day, jerry cans) | none | the store keeps the jerry-can fuel shelf (slice 2); no pump on the island |
| Bait | imported top-up (10 kg a day); the store sells capelin | the store line | slice 2 ties it to the back room |
| Ferry / freight link | not in canon; the island data already says fleet diesel, bulk heating oil and bulk bait come "by sea at high water" | none | **decision Q6**: no passenger ferry; a weekly NPC freight call at high water, later |
| Wharf watcher | built | Basil | keep; he catches the island boat's lines (slice 4) |

Missing roles, short list: **island fishers (1 boat, 2 people), wharf stage hands (3), a working supply
run (Junior's run made real), the tractor driver, the children's school day, and low-water pickers.**
Every one can be filled by a character that already exists. No new person is needed for slices 1 to 7.

---

## 3. The loop

### 3.1 The day, as a picture in text

```
            low tide: the bar is dry                      high tide: the gut is open
   NINE MILE CREEK  <==========================>  ST PETERS
   groceries, gas,        quad + trailer           back room (store) ---> shelves ---> households
   stove oil, propane,    every 2nd day            (Marguerite)              \           (eat, heat,
   ice, bait, mail,  ---> 385 kg / 420 L --->                                 \           cook)
   hardware, medicine                                                          '--> boats (gas, ice, bait)

   NMC fish plant  <---  truck/tractor run  <---  wharf stage  <---  island boat lands
   (grades, pays)        on low-tide days         (weigh, ice,       lobster / cod / mackerel
        |                                          crate, salt)  <---  Junior's clams, pickers' mussels
        v
   groceries, hotels, export (FishChainModel outlets)

   money:  plant pays the stage for the catch --> stage pays the fishers and hands
           wages --> households buy at the store --> store pays the NMC wholesalers on the run
```

### 3.2 Stocks and flows (units are what the data uses)

| Stock (where it sits) | Unit | In (per day) | Out (per day) | Set by |
|---|---|---|---|---|
| Back room, per island good (store) | kg or L per `IslandGoodDef.Unit` | on run days: top-up toward the buffer target, capped by the runner's kit (385 kg / 420 L) | island use: `DailyUnits` (stove oil ×2 in winter) | `IslandGoodDef`, `SupplyRoleDef.CadenceDays`, `IslandSupplyConfig.BufferDays` |
| Store shelves (per shop line) | items | from the back room (slice 2) | sales to villagers (recomputed) and to the player (saved, later) | `ShopDef` lines |
| Island boat's catch | fish (species, weight) | the boat's day plan | landed at the stage | a St Peters `NpcPotDayDef` (slice 4) |
| Wharf stage store | kg (and crates) | landed catch, clams, mussels | the run to the plant on low-tide days | `fishstore.st_peters_wharf`, `fishrun.st_peters_to_plant` |
| NMC plant stores and outlets | kg | runs from the island and NMC | outlets (grocery, hotel, export) | `FishChainSpec` and its Defs |
| NMC market fish supply | supply units per fish category | NPC landings (new) and player sales | half settles each midnight | `GameConfig` market fields |
| Household and stage money | coin | wages, catch payments | store purchases, wholesaler bills | wage and price fields in data (slice 5) |

Island import arithmetic (from `island-supply.md`, pinned by its tests): island use 99.7 kg / 155 L a day;
with the player's allowance 114.2 kg / 177.25 L; weekly demand 799.4 kg / 1,240.75 L; weekly capacity
1,543.5 kg / 1,860 L; volume binds.

---

## 4. The off-screen rule

The island must look the same whether the player watched the day or slept through it. The rule that
gives that for free:

1. **Everything the island does by itself is a pure function of `(worldSeed, day, hour)` and the data.**
   Routines already are. The tide is recomputed from the clock (`TideModel.Height`), so the bar windows
   are too. The run calendar, the back room, the island boat's day, the stage tally and the plant's
   intake from the island all follow the same pattern: given the day, replay a bounded number of earlier
   days from a known starting state and read the answer.
2. **Bounded replay instead of a background simulation.** The back room is "memoryless" after a run that
   tops it right up: from then on, history before that run does not matter. So the ledger starts from a
   full back room a fixed number of days earlier (`LookbackDays`, data, default 28 = one lunar month) and
   replays forward. A test proves the answer does not change when the lookback doubles. Cost: 28 days ×
   9 goods of arithmetic, once per game day (the slow tick); a return after any absence costs the same.
3. **The slow tick.** Economy settles on the existing `DayStarted` event (one per midnight, including each
   midnight crossed by a sleep's `FastForwardTo`) and lazily on read, as `ShopStockService` already does.
   No per-frame work. A load (`SeekTo`, which publishes nothing) just reads the day: replay makes the
   catch-up implicit.
4. **Cross-module talk through Core.** The ledger lives in Economy and reads only Core types
   (`IslandGoodDef`, `SupplyRoleDef`, `CarrierDef`, `IslandSupplyConfig`, `CrossingMath`). The tide comes in
   as a `waterAt(seconds)` function supplied by the App composition (Economy does not reference
   Environment). Villager presenters (World) learn run days through a Core-level query, never an Economy
   class.
5. **Content is data.** One `IslandLedgerDef` per island (`islandledger.st_peters`). New NPCs, routines,
   boats and stage Defs are one asset each, with `type.snake_case` ids. Every number above is a Def field.
6. **No hidden randomness.** Variation (the tractor's line, a boat's catch) is seeded from
   `(worldSeed, id, day)` like `NpcPotDay` and `ConversationSchedule`.

### 4.1 What must be saved, and what is recomputed

| State | Saved or recomputed | Why |
|---|---|---|
| Villager positions and days | recomputed | pure function of the clock (built) |
| Tide, wind, weather, bar windows | recomputed | project rule 5 |
| Run days, back-room levels, island use | recomputed | pure replay from data and the tide (slice 1) |
| The island boat's catch and landing | recomputed | pure day plan, as the NMC pot boats already are |
| Stage and plant intake **from NPCs** | recomputed | bounded replay; the plant's stores flush daily |
| Background NPC landings into the NMC market | recomputed | pure per day |
| **What the player bought off the island's shelves** | **saved** | only the player knows; a deviation from the recomputed level |
| **The player's catch sold into the island or the market (glut)** | **saved** | already unsaved today (`market-supply-persistence.md`) |
| Shop shelf stock (`ShopStockRecord`) | **saved** | contract exists, in memory only |
| Player-owned boats, gear, money | saved (already) | |

**Smallest save change I propose (needs your approval; nothing before slice 8 needs it):** one
append-only list, `EconomyLedgerRows`, each row `{ LedgerId, KeyId, Units, SettledThroughDay }`, at the
next free save version (v26 on current main). One shape covers the shop shelves (the existing
`ShopStockRecord`), the market glut (the persistence proposal's `MarketStates`) and the player's draw on
the back room. The player's draw is self-clearing: after the next run that tops the back room up fully,
the deviation is gone and the row is dropped, so the list stays short. Old saves load with an empty list,
which means "the island as the calendar says".

---

## 5. Slices

Each is one PR. "Sees" is what you would notice in game.

| # | Slice | Sees | Data it adds | Tests | Depends on | New art |
|---|---|---|---|---|---|---|
| 1 | **The back-room ledger** (built now, draft PR) | nothing yet: a pinned numbers table in this page | `IslandLedgerDef` type; `islandledger.st_peters` | run calendar from the real tide; replay converges (lookback 28 vs 56); top-up capped by kit; a 5-day gap starves goods with no allowance; deterministic | none | none |
| 2 | **Marguerite's shelves follow the back room** (shelf model, data and tests built; the counter, her line and shelf art not yet wired, see `island-supply.md`) | the store's gas, ice, bait and grocery shelves thin before a spring tide and fill the day after Junior's run; she says the quad has not been over | new store lines for island goods; dialogue lines | shelf equals back room on each day; player purchases come off the shelf (session only until slice 8) | 1 | icons for jerry can, propane bottle, grocery box if missing |
| 3 | **Junior's run is real** | Junior and the quad cross every second day at low water, the trailer loaded on the way back, never on the spring days the tide forbids | his routine gets run-day legs from the ledger's calendar (a Core day-source query) | his crossing days equal the ledger's run days; he is never on the bar when the crest is wet | 1; Q4, Q5 | quad trailer, a load of crates and jerry cans on it |
| 4 | **An island boat** | one small boat leaves the St Peters wharf in the morning, works pots in season, lands at the slip in the afternoon; Basil takes her lines | `npc.monique_landry`, `npc.angus_gillis` routines; a St Peters `NpcPotDayDef`; berth data | catch plan deterministic; lands only inside the season | Q1 | the boat (reuse a hull), two working-clothes builds |
| 5 | **The wharf stage and the run to the plant** | crates of iced fish stack on the wharf; on low-tide days they go over the bar to the NMC plant; NMC prices dip on big landing days | stage hands' routines (Theresa, Dougie, Rene); stage Defs; NPC landings feed the market | stage tally equals landed catch; one market registration per landed fish; replay converges | 4; Q2 | fish crates/tubs, a crate stack prop, the salt-fish flakes in use |
| 6 | **The new residents' days** | the Landry children walk to school; the families keep their rows; pickers on the mussel and oyster beds at low water | routines for the Landry family and pickers; school days | no two people on one station; school hours; picking only at low water | village plan V2 stations (world-content) | builds for the eight residents (child builds exist) |
| 7 | **The tractor with forks** | Paul Landry drives the tractor village to wharf and cannery yard, moving a fuel-drum pallet or a bucket of fish or seaweed, leaving tire marks, a slightly different line each run | tractor role, loads, its days | loads within forks/bucket limits (pinned in island supply); the line is seeded per day | Q3; vehicles module | tractor mesh, forks, bucket, tire-mark decal, pallet and drum props |
| 8 | **The island remembers the player** | what he buys or sells on the island survives save and load | `EconomyLedgerRows` at save v26 | round trip; migration from v25; self-clearing rows | **your approval (Q7)** | none |

Slices 1 to 3 can run without art beyond slice 3's trailer load; 4, 5 and 7 need the art first.

---

## 6. Open questions (one line each, my lean in brackets)

1. **Island boat:** one lobster/handline boat, Monique Landry skipper and Angus Gillis sternman? (**yes**)
2. **While the cannery is shut,** do Theresa, Dougie and Rene work the wharf stage, becoming the cannery's
   first staff when the player restarts it? (**yes**)
3. **Tractor driver:** Paul Landry, the island's carter? (**yes**)
4. **Junior's run:** every second day as the supply maths needs, instead of only Wednesdays? (**yes,
   keep Wednesday as his NMC errand day**)
5. **Spring-tide gap** (no daylight round trip on days 12 to 14 and 27): may the run leave before 06:00 or
   come back after 20:00 on those days, rather than raising the buffer? (**yes, early/late runs**)
6. **Freight by sea:** no passenger ferry; a weekly NPC freight boat at high water for diesel, heating oil
   and bulk bait, later? (**yes, later**)
7. **Save:** approve one small save change (v26, the list in §4.1) when slice 8 comes? (**yes, at slice 8**)
8. **Island money:** shown only through dialogue and shelves, never a meter? (**yes**, matches the
   11-14 no-bars ruling)
9. **Small fish sales on the island:** keep Marguerite's store buying small lots (it already has a
   market), with the bulk going over the bar? (**yes**)

---

## 7. Findings while reading (for the roles that own them)

- Rose's `HomeDef` names `ResidentMovesTo: station.st_peters.home_post_office`, a station not in
  `StPetersRoutines.AllStationIds`.
- Claudette Boudreau's Def is listed by the NMC builder but not found placed in `NineMileCreek.unity`.
- Junior's built crossing is weekly; the supply plan needs every second day (Q4).
- The village plan gives the four hands "a shift at the cannery", but the cannery is derelict by ruling
  (Q2 resolves this).
- By the replay rule, the 4-day buffer does **not** cover the spring gap for goods with no player
  allowance (gasoline, stove oil, propane, mail, medicine) whenever the last run before the gap falls two
  days before it (run on day 10, next possible run day 15): the back room holds 4 days and has to last 5,
  so one day goes short. If the last run falls on day 11 it exactly covers. Slice 1 tests the rule on
  synthetic data; slice 2 should pin it on the real tide, and Q5 removes it.
- The NMC pot boats' landings move no money and no market: the "market that breathes via an NPC fleet"
  in the charter is not wired yet (slice 5).
