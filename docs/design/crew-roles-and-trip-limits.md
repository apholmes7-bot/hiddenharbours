# Crew roles and trip limits (crew slice R2)

**Status:** contract and data shipped 2026-11-15 (crew slice R2). Pulled forward from M3 (M3-05 hiring,
M3-07 roles; `backlog/backlog.md`) at the owner's direct ask: *"start with realistic roles/catches for our
current regions"*. The audit behind it is the seat's
`VESSEL-ROLES-GEAR-DEEPWATER-AUDIT-2026-11-15.md` (§1, §2, §8 R2, §9).

**Pillars:** P2 Dory to Dynasty (a bigger boat needs a bigger crew), P4 Earn It Then Automate It (one
player can do every job, slowly; hands make it fast), P5 Cozy but with Teeth (a long trip needs bunks,
stores and the right people).

## The rules

1. **By day**, a boat carries at most her **crew slots** (`BoatHullDef.CrewSlots`). This is the one
   number the roster enforces when someone steps aboard.
2. **Any night aboard needs a bunk.** A bunk is a `sleep` anchor in the hull's interior, counted through one
   function (`BunkCount.Of`). A hull with no bunk cannot go out overnight, whatever else she could do.
3. **Players never share a bunk** (owner ruling 2026-11-15). Not a setting.
4. **Hired hands may share a bunk in two watches** (owner ruling 2026-11-15, audit Q3). The switch
   `GameConfig > Crew > Hired Hands Share Bunks` ships **ON**: each bunk left after the players have theirs
   sleeps two hands (`Hands Per Shared Bunk`). So the most aboard overnight is
   **players + 2 x (bunks - players), capped at the crew slots**. Turned off, every hand needs a bunk.
5. **Minimum crew is 1 on every hull** so you can always play alone. Some *work* needs more: a trawl tow
   needs a skipper and a winchman on the side dragger, plus a gallows hand on the stern trawler.
6. **Somebody aboard must be able to steer** (a role with the `task.helm` task: skipper or mate).
7. **Each hull has an honest longest trip** (its voyage record's `LongestTrip`): an open boat goes for the
   day; an inshore lobster boat may stay out a night or so; draggers go for about a week.
8. **A solo player may sail for days, weeks or months, with resupply** (owner ruling 2026-11-15, audit
   Q5). Month trips do **not** need a hired skipper: `Month Trips Need Hired Skipper` ships **OFF**. It
   stays a switch; turned on, a month trip would need a hired hand who can helm aboard.
9. **Stores, water, ice and fuel** are checked only when the game tracks them. Today none of them is fed to
   the check (stores, water and an ice consumer arrive in slice R3), so they never refuse a trip yet.

Trip kinds: **Day** (home before night), **Overnight** (up to `Overnight Nights`, shipped 3), **Week** (up
to `Week Nights`, shipped 10), **Month** (`Month Nights`, shipped 30). All in `GameConfig > Crew`.

## Per hull (for you to read and tune)

"Overnight max" is with you (one player) aboard and hands sharing in watches (the shipped setting):
1 + 2 x (bunks - 1), capped at the crew slots. The figure in brackets is with sharing turned OFF (one
sleeper a bunk). "Bunks" is the hull's sleep anchors on main today, the second berths of PR 1228 included;
the limits follow the bunk count by themselves. "Longest trip now" is what the rules allow today with a
solo skipper; "Record" is the hull's honest longest trip before bunks are counted.

| Hull | Min crew | Day max (crew slots) | Bunks | Overnight max [sharing off] | Longest trip now | Record | Work fitted (min crew: roles) |
|---|---|---|---|---|---|---|---|
| Dory, dory outboard | 1 | 1 | 0 | 0 | Day | Day | rod and jig, tub longline (1) |
| Punt, punt upgraded | 1 | 1 | 0 | 0 | Day | Day | rod and jig, lobster pots, crab pots (1) |
| Console skiff | 1 | 2 | 0 | 0 | Day | Day | rod and jig, lobster pots, crab pots (1) |
| Sport skiffs (3), bowrider | 1 | 2 | 0 | 0 | Day | Day | rod and jig (1) |
| Yacht tender, Zodiac FRC, Zodiac Hurricane, jet ski | 1 | 2 | 0 | 0 | Day | Day | none |
| Lobster inshore 8.6 m (6 variants) | 1 | 2 | 2 | 2 [2] | Overnight | Overnight | lobster pots, crab pots, rod and jig (1) |
| Lobster 12 m (6 variants) and the Lobster Boat | 1 | 3 | 2 | 3 [2] | Overnight | Overnight | lobster pots, crab pots, rod and jig, tub longline (1) |
| Cape Islander 12.9 m | 1 | 3 | 2 | 3 [2] | Overnight | Overnight | as the 12 m, plus bluefin rod (2: skipper, mate), swordfish harpoon (3: skipper, striker, lookout), scallop drag (3: skipper, deckhand, shucker) |
| Lobster offshore 14.6 m (6 variants) | 1 | 4 | 2 | 3 [2] | Week | Week | as the 12 m, plus swordfish harpoon (3) |
| Express cruiser | 1 | 2 | 2 | 2 [2] | Overnight | Overnight | rod and jig (1) |
| Go-fast | 1 | 2 | 0 (her cuddy has no usable level) | 0 | Day | Day | none |
| Sport fisher convertible 16.2 m | 1 | 4 | 3 | 4 [3] | Overnight | Overnight | rod and jig (1), bluefin rod (2: skipper, mate) |
| Sport fisher skybridge 27.4 m | 1 | 6 | 4 | 6 [4] | Week | Week | rod and jig (1), bluefin rod (2) |
| Motor yacht 84 | 1 | 6 | 6 (the audit finds 5 reachable) | 6 [6] | Month | Month | none |
| Expedition yacht 120 | 1 | 8 | 8 | 8 [8] | Month | Month | none |
| Side dragger 25 m | 1 | 5 | 4 | 5 [4] | Week | Week | otter trawl (2: skipper, winchman), scallop drag (3: skipper, winchman, shucker) |
| Stern trawler, Mk II 38 m | 1 | 8 | 4 | 7 [4] | Week | Week | otter trawl (3: skipper, winchman, gallows hand) |
| Coastal packet 60 m | 1 | 10 | 2 | 3 [2] | Month | Month | cargo (2: skipper, deckhand) |
| Tanker 110 m | 1 | 14 | 3 | 5 [3] | Month | Month | cargo (2: skipper, engineer) |
| Sloop 30 | 1 | 2 | 0 (no interior def yet) | 0 | Day | Week | rod and jig (1) |
| Sloop 88 | 1 | 6 | 0 (no interior def yet) | 0 | Day | Month | none |

An all-hired crew (no player aboard, a hired skipper running the boat) can sleep 2 x bunks, capped at
the crew slots. With two players aboard each takes a bunk of their own first.

What the real fleets carry (audit §2.1, sources in its §10): a lobster boat runs a captain and one or two
helpers; a swordfish harpoon boat three or four (captain, striker, mast lookout); an eastern-rig side
trawler five to eight on 6-10 day trips; an offshore stern trawler 10 to 16 on 7-14 day trips. The game's
bunks are tighter than that on every working hull (stern trawler: 8 slots, 4 bunks). More berths on the
big working hulls are coming (owner answer to audit Q4, below); each record's `RealWorldCrew` and
`LimitedBy` say where its hull falls short today.

**Where to tune:** each hull's record is `Data/Boats/Voyages/Voyage<Hull>.asset` (minimum crew by trip kind,
longest trip, the work list). The day max is the hull's own `CrewSlots`. The bunks come from the interior
art, so a new berth is an art change, never a number here.

## Roles and what they do

Each role is one asset under `Data/Resources/Staff/`. Tasks are ids (`task.*`) that stations and hands'
routines will ask about; "playable now" means the game already has the loop, for a player.

| Role | Tasks | Specialist for |
|---|---|---|
| Skipper (`role.skipper`) | helm, navigate, mooring lines, night watch | (every hull's work lists the skipper) |
| Mate (`role.mate`) | helm, navigate, mooring lines, night watch, haul pots, haul longline, gaff, ice the hold, mend gear, cargo handling | bluefin rod |
| Engineer (`role.engineer`) | engine watch, pump bilge, mend gear, night watch | |
| Deckhand (`role.deckhand`) | mooring lines, bait, haul pots, band lobster, tend rods, gaff, haul longline, ice the hold, sort catch, mend gear, night watch, cargo handling | rod and jig, tub longline, cargo |
| Cook (`role.cook`) | cook, provision, night watch | |
| Sternman (`role.sternman`) | haul pots, band lobster, bait, mooring lines | lobster pots, crab pots |
| Winchman (`role.winchman`) | winch, gallows, mend gear | otter trawl, scallop drag |
| Gallows hand (`role.gallows_hand`) | gallows, mooring lines, mend gear, sort catch | otter trawl |
| Hatch hand (`role.hatch_hand`) | ice the hold, sort catch | otter trawl |
| Shucker (`role.shucker`) | shuck, sort catch | scallop drag |
| Striker (`role.striker`) | strike, gaff | swordfish harpoon |
| Lookout (`role.lookout`) | lookout, night watch | swordfish harpoon |

| Task | Playable now? |
|---|---|
| helm, navigate | yes |
| haul pots, band lobster | yes (the player's hauler and banding table) |
| bait | partly (bait is spent on pots and the rod) |
| tend rods | yes (the rod and jig loop) |
| mooring lines | partly (moor and anchor exist; no line-handling station) |
| night watch | sleep is yes (#1210); watches no |
| cook, provision | partly (recipes and prep stations exist; a galley meal at sea is not wired) |
| haul longline | no (data and save exist, no controller: slice R8) |
| winch, gallows, sort catch, ice the hold | no (the trawl prototype fails closed; ice has no consumer) |
| shuck, strike, lookout, gaff | no (no scallop drag, harpoon or mast fitting yet) |
| engine watch, pump bilge, mend gear, cargo handling | no |

## What is playable now, and what is contract only

- **Playable now:** nothing new on screen. The rules, the roles and the per-hull records exist and are
  tested; nothing in a scene asks them yet.
- **Contract only:** `GameServices.Crew` (the roster) works in memory, but nothing puts the player on it
  and nothing registers a voyage catalog at runtime, so today it answers "unknown hull" for every boat.

## Next slices

- **Register the catalog and put the player on the roster** when they board (a Boats/App wiring slice).
- **Hiring (R7 / M3-05):** a hired deckhand from the NMC wharf on the roster, standing at a station.
- **Saved roster:** a save bump that persists the boat key, hull id and member list (nothing derived).
- **Tasks wired to stations:** the hauler, banding table, winch and galley ask `StaffRoleBook.HasTask`
  before a hand works them.
- **Stores, water and ice (R3)** feed `VoyageSupplies`, and the fuel tank converts to game days.
- **Berths on the big working hulls** (see the owner's answers below): one batched rig edit per hull.

## The owner's answers (2026-11-15)

- **Q3, hired hands sharing bunks:** yes, in watches. `Hired Hands Share Bunks` ships ON. Players still
  never share.
- **Q5, month trips:** a solo player may sail days, weeks or months, with resupply.
  `Month Trips Need Hired Skipper` ships OFF.
- **Q4, more berths on the big working hulls:** they will come, batched into **one rig edit per hull**
  (not a berth at a time). The limits follow the bunk count, so no voyage record needs editing when they land.
- **Protected species are release-only:** a protected catch can only be released, never kept or sold
  (for the fishery slices that add them).
- **Autopilot** will be a **purchased tool, offered on recommended hulls**: a later slice, not part of
  these trip rules.
