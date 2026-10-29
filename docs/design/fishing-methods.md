# Fishing methods

| # | Method | What you do | Carried by | Phase, row | On main | Next |
|---|---|---|---|---|---|---|
| 1 | Clam dig | Dig the squirting holes at low water | On foot | M1, M2-32 | Built | Nothing owed |
| 2 | Rod, handline and jig | Cast or drop, read the bite, fight her in | Ashore or any boat | M1 | Built | Two open owner items; consumables deferred |
| 3 | Traps and pots | Set, soak, haul on the swell, re-bait | A hauler station | M2-33 | Built; winch owed | The winch row |
| 4 | Longline | Lay a line of hooks, soak it, haul hook by hook | A hauler station | M2-16 | The flag only | FIRST |
| 5 | Ice fishing for smelt | Cut a hole in the Lock, jig at night | On foot | M2-47 | Nothing | SECOND |
| 6 | Nets | Set a gillnet; tow a trawl | Dragger, trawler | M3-04 | The flag only | At M3 |
| 7 | Dredge | Tow a scallop bed, cull on deck | A specialist mount | Late, no row | The flag only | Nothing owed |
| 8 | Mussel lease | Grow a lease, haul its lines | Any boat alongside | M3-16 | Design only | Four leans pending the owner's word |

## Build order

1. **F0: this plan.** Docs only; no editor slot and no implementation.
2. **Longline first:** L1, L2 and L3 of its separate build charter.
3. **Ice fishing for smelt second:** its own build charter is owed after the longline's. It lands
   M2-35's first frozen water together with the fishing, at M2.
4. **M2-33's powered winch,** when its row is taken. One hauler upgrade then serves pots and longline.
5. **The rest at their phases:** nets with the M3 hulls (M3-03, M3-04), the lease at M3-16, the dredge late.

The eight methods serve the five pillars: each reads the sea in its own way (P1), follows the boat and gear
ladder (P2), belongs to the working NPC coast (P3), is worked by hand before its upgrades or staff arrive
(P4), and has its own teeth and cozy failure (P5). The named method docs retain their rules; this plan
links them and orders the builds.

Implementation notes and `file:line` citations are checked at base `fe9bb996`. Below, `Code/` means
`Assets/_Project/Code/`, and `Data/` means `Assets/_Project/Data/`. Backlog line numbers refer to that base,
before this plan adds its row. Ten fish assets exist; the remaining species are design entries in
`docs/design/fish-and-content.md:349` onward.

Every method uses the same species gates: region, gear, season, hour, tide and moving-water windows must
match (`Code/Fishing/CatchResolver.cs:161`). `CatchContext` carries those inputs together with floor depth,
lure, bait favours and school (`Code/Fishing/CatchResolver.cs:14`). The gear flags are Handline, Longline,
Net, Trap, Dredge, Jig and ClamFork (`Code/Fishing/Gear.cs:8-28`); each species declares `AllowedGear`
(`Code/Fishing/FishSpeciesDef.cs:28`). The interface remains the
[boat gear and catch contract](boats-and-navigation.md#61-gear-methods-mounted-as-gear-components),
sections 6.1 and 6.2 (`docs/design/boats-and-navigation.md:574`, `docs/design/boats-and-navigation.md:584`).

Each block uses the same eleven fields, in order: Loop; Skill; Teeth; Gear and boat; Grounds and time;
Catch; Licence; NPC fleet; Automated later; Phase and row; Next. Its final **On main** paragraph separates
what is built from the planned loop.

## 1. Clam dig

Method sources: [fish and content, sections 3.4 and 3.5](fish-and-content.md#34-the-catch-mini-interaction-cozy-p5-mild)
(`docs/design/fish-and-content.md:212`, `docs/design/fish-and-content.md:235`).

- **Loop:** at low water, walk the bared flats; spot the two squirting holes; dig before the hole closes;
  pail the clams (the wet bucket keeps them alive); sell at the wharf.
- **Skill:** reading the spurt and the tide's clock. Spring lows bare the most flat.
- **Teeth:** the flood comes back across the flats. A slow dig loses the clam when the hole closes,
  and nothing else.
- **Gear and boat:** the shovel. On foot; no boat.
- **Grounds and time:** St Peters' flats at low water; the clam's `MaxTide` is 0, its hours 06:00 to
  20:00, all year on main (`Data/Fish/SoftShellClam.asset:28-32`).
- **Catch:** soft-shell clam on main. Razor clam remains proposed for Rice Point
  (`docs/design/rice-point.md:128`).
- **Licence:** `license.clam`.
- **NPC fleet:** diggers on the same flats at the low. Their landings move the clam price through
  M2-19 (`backlog/backlog.md:209`).
- **Automated later:** never. It stays the cozy hand job
  (`docs/design/rod-fishing-v2-brainstorm.md:36-37`).
- **Phase and row:** M1 implementation; M2-32 is the St Peters opening's row. Built.
- **Next:** nothing owed.

**On main:** built (`Code/Fishing/ClamDig.cs:56`); the shovel is `gear.shovel`
(`Data/Tools/Shovel.asset:17`); soft-shell clam uses `AllowedGear: 64`, ClamFork
(`Data/Fish/SoftShellClam.asset:27`). `license.clam` costs 15
(`Data/Resources/Catalog/Licenses/ClamLicense.asset:15-23`).

## 2. Rod, handline and jig

Method sources: [rod fishing v2](rod-fishing-v2-brainstorm.md) and
[fish and content, section 3.4](fish-and-content.md#34-the-catch-mini-interaction-cozy-p5-mild)
(`docs/design/fish-and-content.md:212`).

- **Loop:** cast from shore or the wharf, or drop from a boat; bait or lure sinks through the depth
  bands; read the nibbles and strike on the take; fight her in, leaning against her and giving line
  when she runs; land her. To jig, work the lure up and down at the depth the fish hold.
- **Skill:** reading the bite, timing the strike, the fight's rhythm, lure choice for the species,
  depth (stop the drop in midwater), tide and hour.
- **Teeth:** an ordinary failure loses the catch, the bait and the time; a bad enough sea may cost
  terminal tackle. The old absolute never-lose-gear bullet at
  `docs/design/rod-fishing-v2-brainstorm.md:279` is superseded by the owner's amendment of 2026-07-25,
  `docs/design/rod-fishing-v2-brainstorm.md:281-296`: gear loss is permitted in a blow. **Bait and
  tackle consumption and restocking are NOT BUILT today (the rod consumes neither) and stay deferred
  to the brainstorm's section 8.** These are the planned costs; current ordinary failure loses the
  fish and time without consuming bait or tackle.
- **Gear and boat:** the rod, five pieces of tackle, bait (capelin, herring). Ashore or any boat.
- **Grounds and time:** anywhere with water, inside each species' windows.
- **Catch:** cod, haddock, pollock, mackerel, winter flounder and striped bass on main.
- **Licence:** `license.cod` for cod.
- **NPC fleet:** anglers on the wharf and jiggers in their boats. Little market weight.
- **Automated later:** the rod itself stays hands-on and skill-expressive: what you do yourself
  while the fleet works (`docs/design/rod-fishing-v2-brainstorm.md:36`).
- **Phase and row:** M1. Built.
- **Next:** two owner questions remain open: choosing bait and lures in the world, with ui-ux
  (`docs/design/rod-fishing-v2-brainstorm.md:404`), and what "portion" meant
  (`docs/design/rod-fishing-v2-brainstorm.md:336`, `docs/design/rod-fishing-v2-brainstorm.md:407`).
  Their leans are recorded below; consumables remain deferred as above.

**On main:** the rod fishes as Handline (`Code/Fishing/FishingController.cs:41`). The bite
(`Code/Fishing/BiteSequenceSim.cs:57`), fight (`Code/Fishing/RodFightSim.cs:25`), jig
(`Code/Fishing/JigWork.cs:47`), depth drop (`Code/Fishing/DepthDropMath.cs:45`) and schools
(`Code/Fishing/FishSchoolMath.cs:43`) are built, with five tackle assets in `Data/Tackle/` and
`gear.rod` (`Data/Tools/Rod.asset:17`). `license.cod` costs 120 and permits only `fish.atlantic_cod`
(`Data/Resources/Catalog/Licenses/CodLicense.asset:15-20`); species-based landing enforcement
(`Code/Economy/CatchLicensePolicy.cs:29`) gates cod on every gear. The rod consumes neither bait
nor tackle; the rough-weather terminal-tackle cost is not built.

## 3. Traps and pots

Method sources: [fish and content, section 3.5](fish-and-content.md#35-phased-gear--content--st-peters-clam-dig--lobster-gaffing--aquaculture-leases)
(`docs/design/fish-and-content.md:235`) and
[boat interactions, section 6.3](boats-and-navigation.md#63-trap-hauling-interaction--the-lobster-loop-phased-m2)
(`docs/design/boats-and-navigation.md:608`).

- **Loop:** bait the pot; lay it with its buoy in 3 to 40 m; let it soak; approach the buoy, gaff
  the line and haul with the swell over the hauler; bring the pot aboard; empty it, band the keepers
  and throw back the rest; chop and re-bait; stack; toss to re-set.
- **Skill:** where to lay (depth, bottom, grounds), when to haul (a pot fills through its soak),
  hauling on the swell's lift, and the approach and drift after leaving the helm.
- **Teeth:** a rushed grab gets you nipped; hauling against the swell parts the rope; weather keeps
  you off your gear.
- **Gear and boat:** bought and counted pots, bait, and a hull with a hauler station: Cape Islander
  or Lobster Boat.
- **Grounds and time:** inshore, all year on main. The canon's seasonal trap moves remain owed
  under their separate charter.
- **Catch:** lobster and rock crab.
- **Licence:** none on main; lobster is ungated.
- **NPC fleet:** other boats' strings of buoys on the grounds, in their own colours, with seasonal
  pot moves still owed. Their landings move prices (M2-19).
- **Automated later:** M2-33's powered winch takes the haul (P4); a sternman works the deck
  (M3-07, `backlog/backlog.md:324`); a hired skipper fishes a string for you
  (M3-06, `backlog/backlog.md:323`).
- **Phase and row:** M2; M2-33. Built, except the winch.
- **Next:** M2-33's winch, as its row stands. This plan owes no charter for it.

**On main:** the trap arc's Builds 1 to 7 are built. Pot data is `Code/Fishing/TrapDef.cs:20`
(capacity at `Code/Fishing/TrapDef.cs:57`, catch list at `Code/Fishing/TrapDef.cs:60`, bait at
`Code/Fishing/TrapDef.cs:63`, soak at `Code/Fishing/TrapDef.cs:92`, hours to full at
`Code/Fishing/TrapDef.cs:99`, depth at `Code/Fishing/TrapDef.cs:101-103`). Placement
(`Code/Fishing/PlacedTrap.cs:52`, `Code/Fishing/TrapPlacement.cs:20`), soak
(`Code/Fishing/TrapSoak.cs:15`), seeded fill (`Code/Fishing/TrapFill.cs:26`, per-slot hash at
`Code/Fishing/TrapFill.cs:60`, count at `Code/Fishing/TrapFill.cs:96`), service
(`Code/Fishing/PlacedTrapService.cs:33`) and bait-weighted catch pool
(`Code/Fishing/PlacedTrapCatch.cs:129`, favour multiplier 3 at `Code/Fishing/PlacedTrapCatch.cs:37`)
exist. Swell haul math (`Code/Fishing/TrapHaulMath.cs:32`), haul and deck controllers
(`Code/Fishing/TrapHaulController.cs:55`, `Code/Fishing/PotDeckWorkController.cs:52`) and deck stages
(`Code/Core/Boats/SternDeckLoop.cs:19`) are built. Saved trap state is placement only
(`Code/Core/Save/SaveData.cs:74`), beside bait stock (`Code/Core/Save/SaveData.cs:79`) and pot stock
(`Code/Core/Save/SaveData.cs:87`). Both hulls have the hauler station
(`Code/Boats/DeckGearKitDef.cs:23`, `Data/Boats/DeckGear/CapeIslanderDeckGear.asset:18`,
`Data/Boats/DeckGear/LobsterBoatDeckGear.asset:18`). M2-33's powered winch remains unbuilt
(`backlog/backlog.md:250`).

## 4. Longline (first)

Method source: [boat gear, sections 6.1 and 6.2](boats-and-navigation.md#61-gear-methods-mounted-as-gear-components)
(`docs/design/boats-and-navigation.md:574`, `docs/design/boats-and-navigation.md:584`). Its separate
build charter is `CHARTER-2026-10-25-fishing-longline.md`.

- **Loop:** bait the tubs, each a coil of groundline with its hooks; steam to the grounds; drop
  the first end (anchor, buoy and flag); steam on a heading as the line pays out over the stern;
  drop the second end. Soak through a tide. Return to either end, gaff its buoy line and haul over
  the hauler, hook by hook: fish, empty hook or stripped hook. Fish go to the hold; line returns
  to its tub. Land and sell.
- **Skill:** lay over the depth you want: each hook fishes the water under it. Along an edge,
  every hook fishes the edge; across a slope, the hooks spread. Set before dawn or the tide's
  turn and haul after the bite. Find the soak's sweet spot and haul on the swell's lift.
- **Teeth:** too long a soak brings stripped hooks, bait and fish taken by sea fleas and dogfish.
  Hauling against the swell parts the line; the remainder waits on the far end's buoy. Weather
  keeps you off the gear. You never lose the boat or the line.
- **Gear and boat:** bought and counted tubs, bait from the locker (seven kinds sold on main),
  and a hauler station. Cape Islander first, the canon's inshore longliner
  (`docs/design/boats-and-navigation.md:47`).
- **Grounds and time:** inshore groundfish water in `region.coddle_cove`, `region.st_peters` and
  `region.nine_mile_creek`, subject to each species' windows. The main pool spans the year; the
  canon's cod is best in fall and good in winter (`docs/design/fish-and-content.md:356`).
- **Catch:** main currently allows cod, haddock, winter flounder, pollock and mackerel. The
  longline build targets cod, haddock and winter flounder: pollock and mackerel lose this flag
  under the canon's gear column and longline charter decision 6. The canon later adds cusk
  (`docs/design/fish-and-content.md:359`), halibut, redfish and monkfish
  (`docs/design/fish-and-content.md:392-394`), and the legendary Sunker King
  (`docs/design/fish-and-content.md:416`). This plan changes no species asset.
- **Licence:** `license.cod` for cod; the rest are ungated on main.
- **NPC fleet:** longliners' end flags on the grounds. A good week of sets gluts groundfish prices
  (M2-19).
- **Automated later:** a baiter baits tubs ashore (M3-07); a powered drum takes the haul on bigger
  boats (`docs/design/boats-and-navigation.md:579`); a hired skipper sets and hauls a second
  boat's line (M3-06).
- **Phase and row:** M2; M2-16 (`backlog/backlog.md:202`). Pots landed through M2-33 and the trap
  arc; the remainder of M2-16 is this longline.
- **Next:** L1, L2 and L3 of its own charter, before ice fishing. Each build waits its own paste;
  F0 implements none of it.

**On main:** the flag alone (`Code/Fishing/Gear.cs:12`), already counted as bottom gear by the
depth drop (`Code/Fishing/DepthDropMath.cs:45`). The five named species allow it with
`AllowedGear: 3`, Handline and Longline; that is the greybox builder's default
(`Code/App/Editor/GreyboxBuilder.cs:828`), rather than the canon's gear column. Nothing sets,
soaks or hauls a fishing longline.

## 5. Ice fishing for smelt (second)

Method sources: the [smelt row](fish-and-content.md#44-tidepool--flats)
(`docs/design/fish-and-content.md:385`) and [regional winter freezing](time-tides-weather.md#48-phased-weather--sea-enhancements-owner-ratified-vision--m2)
(`docs/design/time-tides-weather.md:567`).

- **Loop:** in Hard Winter, once the lagoon freezes, walk out and read the ice; cut a hole with
  the auger; drop a short jig line. Smelt school through on the flood and after dark: jig, feel
  the tap, lift. Lay them on the ice where cold keeps them; fill the pail; walk them to the buyer
  for the winter delicacy trade. The first build also offers a bought shack placed on the ice,
  with a stove, lantern and two holes in its floor, for fishing into the night in warmth.
- **Skill:** where smelt run (channel edges, deeper holes, brook mouths), when (flood, darkness,
  calm cold nights), and a light jigging hand: a tap, not a fight. The shack's tip-ups flag bites
  across several lines that you still tend yourself.
- **Teeth:** thin ice by channels, brook mouths, pressure cracks and the season's end. Go through
  and scramble out soaked: the pail goes down, and you are cold and slow until a stove or home
  warms you. Never death or a stranded save. At the Breakup, a neighbour drags any remaining
  shack ashore for a favour or fee; the shack is never lost.
- **Gear and boat:** ice auger, short jig rod and pail; the bought and placed shack in the first
  ice build, with stove and two holes. Save its placement only. No boat.
- **Grounds and time:** Hard Winter, the Lock (`docs/design/time-tides-weather.md:89`). First
  frozen water is St Peters' lagoon: Lagoon Flats and Lagoon Marsh, closed by the bar to the
  west and North Point to the east, fed by Barren Brook
  (`docs/design/st-peters-terrain-pass-9.md:395`, `docs/design/st-peters-terrain-pass-9.md:403`).
  The canon's smelt takes the high half of the tide at night, best in winter and good in spring
  (`docs/design/fish-and-content.md:385`); this ice loop is Hard Winter only. When the Lock keeps
  the boats in, this is where you fish. M2-35's ice is regional and seasonal, a pure function of
  region, season and day from seed and time: recomputed, never saved, never save-stranding
  (`docs/design/time-tides-weather.md:567`).
- **Catch:** rainbow smelt, approved id `fish.rainbow_smelt`; its future asset is not part of F0.
  Canon row: shallows and tidepool; dip net and rod; 13 to 22 cm; base value 4; perishable.
- **Licence:** none; smelt stays ungated.
- **NPC fleet:** village shacks: neighbours tow them out in the Lock, walk out at dusk and fish
  into the night. A winter gathering place (P3), on routines that name their region. Light
  market weight.
- **Automated later:** nothing does it for you. Its P4 is the shack and tip-ups: more lines
  tended in comfort.
- **Phase and row:** M2-47, with M2-35's first winter freezing (`backlog/backlog.md:252`), after
  the longline. The row's dependency remains after M2-35; the ice charter lands that first
  frozen water with its fishing.
- **Next:** a separate ice fishing build charter, owed after the longline's.

**On main:** no ice fishing and no smelt species asset. Hard Winter exists
(`Code/Core/Time/Calendar.cs:4`, `Code/Environment/GameClock.cs:36`). Seasonal foliage snow is
already a pure function (`Code/Core/Environment/FoliageSnowMath.cs:38`), the pattern for ice
cover. Freshness "on ice" (`Code/Core/Config/GameConfig.cs:2283`) is deck ice-box ice that melts
(`Code/Boats/DeckIceBox.cs:226`), not sea ice.

## 6. Nets: gillnet and trawl

Method source: [boat gear, section 6.1](boats-and-navigation.md#61-gear-methods-mounted-as-gear-components)
(`docs/design/boats-and-navigation.md:574`).

- **Loop:** at dusk, set a buoyed wall of gillnet across the herring run; haul at dawn and pick
  fish from the meshes. From a dragger, tow a trawl along clean bottom for a timed tow; winch
  the bag over the stern and sort the bulk catch.
- **Skill:** reading the shoals and herring run, the evening set, a trawl's path over clean
  bottom, and tow length.
- **Teeth:** rough bottom tears a net or hangs a tow; your own bulk catch gluts the market.
- **Gear and boat:** nets, trawl winch, Side Dragger and Stern Trawler (T4 and T5; M3-03,
  `backlog/backlog.md:316`).
- **Grounds and time:** herring in spring and fall (`Seasons: 5` on main); trawl on the Banks
  (M3-01).
- **Catch:** herring on main, with Net as its only gear; the canon's Banks groundfish and pelagic
  volume later.
- **Licence:** by species; `license.cod` still gates cod on every gear.
- **NPC fleet:** herring boats and draggers. Their landings set the herring price, which is the
  pots' bait price.
- **Automated later:** the offshore branch is crewed (M3-05 to M3-07).
- **Phase and row:** M3; M3-04 (`backlog/backlog.md:317`).
- **Next:** at M3. No inshore herring net before then; herring remains bought bait before M3.

**On main:** the Net flag alone (`Code/Fishing/Gear.cs:13`). Herring has `AllowedGear: 4`, Net,
and `Seasons: 5` (`Data/Fish/AtlanticHerring.asset:24-25`), so it currently comes as bought
`bait.herring`, with no playable net loop.

## 7. Dredge

Method source: [boat gear, section 6.1](boats-and-navigation.md#61-gear-methods-mounted-as-gear-components)
(`docs/design/boats-and-navigation.md:582`).

- **Loop:** tow a toothed dredge over a scallop bed for a set time; winch it up; cull scallops
  from stones and shell on deck; land them.
- **Skill:** finding the bed through the sounder, chart and other boats' tows; tow speed and length.
- **Teeth:** a dredge hangs on a rock and the boat swings on the warp; culling time runs against
  freshness.
- **Gear and boat:** a specialist dredge mount and winch (`docs/design/boats-and-navigation.md:582`).
- **Grounds and time:** flats and banks, within the eventual species' windows.
- **Catch:** none on main; no scallop species asset exists.
- **Licence:** by species.
- **NPC fleet:** scallop draggers.
- **Automated later:** a crew at the culling table (M3-07).
- **Phase and row:** optional, late; no row on main.
- **Next:** nothing owed.

**On main:** the Dredge flag alone (`Code/Fishing/Gear.cs:15`). No scallop species asset or
playable dredge loop exists.

## 8. Mussel lease

Method sources: [mussel lease and growing longline](mussel-lease-and-longline.md) and
[fish and content, section 3.5](fish-and-content.md#35-phased-gear--content--st-peters-clam-dig--lobster-gaffing--aquaculture-leases)
(`docs/design/fish-and-content.md:235`).

- **Loop:** lease water; hang buoyed backbone lines with grow-socks below; the crop grows through
  the cycle, recomputed and never stored. Lay alongside a backbone and haul it over the gunwale
  (the pot-haul verb at a heavier weight); strip, grade and land; re-seed.
- **Skill:** choosing water, depth and shelter; reading crop readiness; timing the growing cycle.
- **Teeth:** lean, pending the owner's word: storms foul the line and lose yield, with no
  permanent gear loss. The method doc's storm-damage ruling remains open
  (`docs/design/mussel-lease-and-longline.md:319`).
- **Gear and boat:** a lease, a property of water (`docs/design/economy-and-business.md:414`),
  backbones and socks; any boat that can lay alongside. Its growing rope shares the fishing
  longline's haul verb only: fishing types and ids say `Longline`; lease types and ids must not.
- **Grounds and time:** lean, pending the owner's word: leaseable water remains undecided until
  the owner names the tide window and spots. Lean for the season shape: a rolling cycle, with no
  duration chosen, pending the owner's word. Both remain open in the method doc.
- **Catch:** mussels, with no species asset on main; oysters later. This is a grown crop, not
  a wild catch roll.
- **Licence:** the lease itself.
- **NPC fleet:** neighbours' leases.
- **Automated later:** owned production (P4); staff harvest at M3.
- **Phase and row:** lean, pending the owner's word: retain M3-16 (`backlog/backlog.md:347`)
  until the owner rephases the starter-ruin fork (`docs/design/mussel-lease-and-longline.md:267`).
- **Next:** the method doc's four owner rulings, with all four leans below. This plan settles
  none of that doc's open rulings.

**On main:** design only ([mussel lease and longline](mussel-lease-and-longline.md)); backlog
M3-16 (`backlog/backlog.md:347`). The growing longline and fishing longline are separate methods.

## Approved decisions and IDs

The owner approved these positions for F0:

1. Keep this as a new plan linking the individual method docs.
2. Ice fishing is M2, after the longline, and lands M2-35's first frozen water together with it.
3. The first frozen water is St Peters' lagoon, Lagoon Flats and Lagoon Marsh, fed by Barren Brook.
4. Include the bought, placed shack in the first ice build: save placement only, with stove and
   two holes.
5. Going through loses the pail and leaves you cold and slow until warm; never death or a stranded
   save. A shack left at the Breakup is brought ashore, never lost.
6. Nets stay at M3; herring remains bought bait before then.
7. Licences stay by species. A licence names its species; `license.cod` gates cod on every gear.
   No per-gear licence before M2-25's tower (`backlog/backlog.md:223`); smelt stays ungated.
8. **Approved, append-only IDs:** `fish.rainbow_smelt` and `M2-47`. These are ruled IDs, no longer
   proposals. The species id agrees with the existing Rice Point ground-plan entries at
   `Data/Terrain/RicePointGround/plan.json:4995` and `Data/Terrain/RicePointGround/plan.json:5225`;
   those entries are not a FishSpeciesDef asset. F0 creates no fish asset, and the
   previously unused backlog id adds a row without renumbering anything.
9. The rod Teeth field follows the existing owner amendment at
   `docs/design/rod-fishing-v2-brainstorm.md:281-296`, including the rough-weather terminal-tackle
   exception and deferred consumption/restocking, rather than the old bullet at line 279.

## Open owner items and leans

These remain open; each lean is the plan's position pending the owner's word. They do not amend
the linked method docs.

| Item | Open question | Lean, pending the owner's word |
|---|---|---|
| Rod: bait and lure choosing | How does choosing bait and tackle work in the world? (`docs/design/rod-fishing-v2-brainstorm.md:404`) | World interactions, with ui-ux; no menu. |
| Rod: "portion" | What did the owner mean by "portion"? (`docs/design/rod-fishing-v2-brainstorm.md:407`) | Wait for the owner's meaning before any build interprets it. |
| Mussels 1: starter ruin | Is the lapsed lease the starter ruin? (`docs/design/mussel-lease-and-longline.md:314`) | Retain M3-16 until the owner rephases it. |
| Mussels 2: water and window | What tide window and spots make water leaseable? (`docs/design/mussel-lease-and-longline.md:316`) | Leave leaseable water undecided until the owner names the tide window and spots. |
| Mussels 3: storm damage | Foul and lose yield, or part and lose the line? (`docs/design/mussel-lease-and-longline.md:319`) | Foul and lose yield; no permanent gear loss. |
| Mussels 4: season shape | Seasonal calendar or rolling cycle? (`docs/design/mussel-lease-and-longline.md:321-323`) | A rolling cycle, no duration chosen. |

The ice fishing build charter remains owed after the longline's. Its ice geometry follows the approved
lagoon choice; this plan does not edit terrain, resolve the other Rice Point species proposals, or
start any method's implementation.
