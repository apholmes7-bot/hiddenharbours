# NPC lobster boats off Nine Mile Creek: Phase A measure

Measured 2026-11-11 at main `d9d4b8558a`, no editor. Every claim below was read from the tree at that commit
unless it says otherwise.

## 1. What exists today

### Pots (the player's)
| Piece | Kind | An NPC crew can use it? |
|---|---|---|
| `Fishing/TrapDef`, `Data/Traps/LobsterTrap.asset` (`trap.lobster`: 4 units, soak 12 h, full 36 h, 3 to 40 m) | Def | yes |
| `TrapSoak`, `TrapFill`, `PlacedTrapCatch`, `StableHash` | static, pure | yes: catch is a function of (seed, instance id, placement time, now) |
| `DeckWork`, `DeckWorkDef` (`deckwork.pot`: lobster keep 83 mm, 62 to 140 mm, 15 % berried), `DeckPot` | pure / POCO | yes: size, berried, keeper, band already exist |
| `TrapHaulMath.SampleHaulRope` | static, pure | yes |
| `PlacedTrapService`, `PotLocker`, `SaveData.PlacedTraps` | player save | **no**: an NPC pot there corrupts the player's pot stock |
| `TrapHaulController`, `PotDeckWorkController`, `SternDeckGearPresenter`, `DeckContainerPresenter` | player input and global signals | no |
| `Boats/TrapBuoyPresenter` | keyed on `TrapPlaced`, always the yellow player buoy | no: publishing `TrapPlaced` would make radar, weed and the ambient fleet treat it as player gear |
| `Boats/BuoyWaveVisual` | the wave-riding buoy | yes (the ambient fleet already reuses it) |

There is **no rope at rest and no trap on the bottom** anywhere today; the only rope is the player's haul line.
There is no market grade, no crate and no tank (the rig sidecar's `tubs` anchors are not baked).

### NPC boats
- The five wall-berth boats are static `MooredBoat`s baked into `NineMileCreek.unity` by
  `NineMileCreekMooredFleet` (`Moored_<ownerId>`). Its own comment: no motion, no routine, no schedule.
  A run-time system may move the object; that is not a scene edit.
- Owners (`Data/Boats/Owners`): Leo Arsenault (standard hardtop, berth 1), Marie Gallant (Cape Islander, 2),
  Yvette Doiron (standard open Fundy, 3, at (133.6, 83.1), bow 090), Ross MacDonald (inshore hardtop, 4),
  Hughie Campbell (inshore open, 5, no skipper Def).
- `Boats/AmbientFleet*` is the house pattern for a clock-driven fleet (self-installing presenter, pure plan
  and schedule, pooled buoys, never saved). It is decor: St Peters punts, real-time steering, no catch, no
  rope, no berth. Its boats are not where the clock says after a load; they steer there.
- **Nothing on main moves a Nine Mile Creek boat.** `feat/nmc-npc-working-day-pr1` (ee5c3d7bd0, unpushed,
  tests never finished) authors shore routines and leaves the boat leg as one "Away" block.
  `feat/npc-boat-trips` (05dde25b, 86 files, unmerged) has a compiled trip plan and a motion lease, for the
  punt only. Neither is on main; this lane reads them and builds on main.
- Routines (`World/Routines`): `RoutinePlan.SampleAt(hour)` is pure. No Nine Mile Creek routine is on main.

### The water (painted ground G3, the scene's live terrain)
Probed headless through `NineMileCreekGround.Load()`:
- The basin (x 80 to 204, y 14 to 84) lies at about -4.3 m. The mouth is on the **south** side
  (x 124 to 148, y -6 to 10). An east wall stands at x 204 to 216. East of it the bay is a flat -6.0 m.
- The analytic plan's `ChannelWaypoints` (out through (204, 50)) do **not** fit G3: that leg crosses the wall
  (+2.45 m). The route must be authored against G3.
- Candidate track berth (133.6, 83.1) > (136, 72) > (137, 30) > (137, -6) > (142, -26) > (200, -26) >
  (236.8, 2) > north along x = 236.8 (probed at 237.7; the committed line is re-checked by NpcPotDayContentTests): shallowest bed within 2.5 m of the line is **-4.28 m**, so 2.08 m of water
  at spring low (-2.2 m) against a 1.3 m keel.
- Pots along x = 240, y 16 to 90: bed -6.00 m, 3.8 m at spring low, inside `trap.lobster`'s 3 to 40 m.

### Clock, seed, tide
`IGameClock.TotalSeconds` (double); a day is `GameServices.SecondsPerDay` (1800 by default) of them and
`DayIndex = floor(t / day)`. Seed: `IEnvironmentService.WorldSeed`. Water level: `WaterLevelAt(t)`, a function of
time. No shared slow tick; each presenter keeps its own.

### Goods, deck, buyer
- ADR 0055 (Proposed) and the owner's 11-10/11-11 rulings: hands or a lifting device, no sale by proximity,
  a full container refuses. The research model's loaded lobster crate (0.80 x 0.50 x 0.40 m) is a
  **device load**, not a hand carry. The runtime container types are editor research code; main has one
  `DeckContainerDef` (the fish tray), no crate, no tote, no tank.
- Deck stations at run time: only `helm` is baked (`BoatDeckDef.HelmStationLocalMeters`). The sidecars carry
  `hauler` (1.296, -0.806, 1.674) and five `tubs`; nothing reads them. `docs/art/rigs/**` is the art
  director's.
- Figures aboard: `MooredBoat` stands one skipper (`ICharacterFigureStand`, a deck occupant slot).
- Buyer: Wendell Arsenault, `NmcWharfMarketCounter` (`Economy.Market`, id NineMileCreek). Supply rises only
  through `Market.RegisterSale(category, count)`. "NPC fleet landings" is written down as M2 and not built.
  The vendor lane's carried-counter code (`CarriedHold`, `WharfSellPoint.SellOverTheCounter`) is uncommitted
  in its box and reads the player's hands.
- Design PRs 1073, 1074, 1076 (open, docs only): living occupants with one owner each, and one merge across
  all competing pots. They say no catch leaves table rolls before a reviewed migration. An NPC pot today is
  therefore table-roll output that never touches a wild individual.

## 2. The model this lane builds

One pure day per boat, `NpcPotDay.Build(def, worldSeed, dayIndex, secondsPerDay)`:
- A timed track: berth, passage out, then for each pot **gaff, haul, deck work, shift, set**, then passage
  home and make fast. Boat pose at any second is a lookup on that track. Nothing steers, nothing is saved.
- Pot `i` on day `d` lies where day `d`'s plan set it; before today's haul it lies where day `d - 1` set it.
  So a pot's state at any time needs two days' plans and nothing else. The fishery did not start with the
  player: day 0 hauls what "day -1" set.
- The catch at a haul is `TrapFill` + `PlacedTrapCatch` + `DeckWork` keyed by (seed, `npc.<owner>.<pot>`,
  the set time, the haul time): the player's own maths, a different instance id, no save, no signal.
- Rope: fixed length; the buoy lies downstream of the pot by a share of the scope `sqrt(L² - depth²)`, flood
  one way and ebb the other, so it swings with the tide and closes up at high water.

## 3. The PRs

1. **A boat goes to work and sets gear** (this PR). `NpcPotDayDef` + `NpcPotDay` + `PotRopeMath` in Fishing
   (pure), `NpcPotBoats` in App (binds the moored boat, pooled buoy, rope and pot per pot), one Def for
   Yvette Doiron's open boat, a Resources library. The haul and set are shown as rope and pot; the deck work
   is a timed pause. EditMode: determinism, mid-day load, track depth on G3, day fits the clock.
   PlayMode: the boat leaves, a buoy goes, the rope spans buoy to pot.
2. **Hauling and the deck.** The catch per haul (pure), a second figure at the hauler, lobsters out one at a
   time, band, grade (short and berried over the side), keepers into a deck container that fills. Needs deck
   stations: read from the hull's deck Def where it can, else Def data on the day (the sidecar anchors are
   not baked; baking them is the art director's and tools' lane, listed as an ask).
3. **Landing and the sale.** Crew carry ashore to the counter; supply through `Market.RegisterSale` behind a
   Core seam so World/App never names Economy types. Depends on what the vendor lane lands. A loaded crate is
   a device load by the research model: STOP candidate, see leans.
4. **The fleet.** Leo, Marie, Ross, Hughie as Defs with their own grounds and hours, pooled, with a frame
   time and allocation proof.

## 4. Open points carried as leans (in the PR body)
- NPC catch is table-roll output, as 1074 requires until its migration exists.
- The boat is on rails: it does not yet give way to the player's hull.
- The crate carry: the research model says a loaded crate needs a device. PR 3 will carry part loads by hand
  (a tote under the hand limit) unless the owner rules otherwise.
