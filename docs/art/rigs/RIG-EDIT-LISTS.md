# Rig edit lists: every addition owed to each boat rig (2026-11-15)

The owner ruled that additions owed to the boat rigs are batched, so each rig is edited **once**:

> "add them with list of other addtitions to each rig edit"

and, for the new purse seiner hull, "add it to when we do rig edits".

This file is that list. The art-director lane works from it: one section per hull, what the rig has today
(with the file that says so), and a numbered list of what each rig edit must add. It is a work order, not
accepted art: every proposed position must be checked on the rig host before baking.

**Read at.** Main `3b1bac088d` (#1247). Open PRs were read from their descriptions only (their branches were
not fetched): #1229 walk-sourced sleep and galley audit, #1235 kitchens in buildings, #1237 cleanliness and
wash stations, #1238 galley stores and seats, #1248 lobster seasons, #1249 protected species, #1250 rod racks.
The vessel roles, deck gear and deep-water audit of 2026-11-15 (a coordination document outside the repo) is
cited as "the vessel audit", by section.

**Conventions.** Hull-local metres: +x starboard, +y toward the bow, z up from the keel datum (the same frame
as `BoatInteriorDef` reach points and `BoatDeckGearDef.Mounts`). "Unconfirmed" means I could not find it in
a committed file. Real-world sizes are general knowledge of small-craft fit-outs unless a source is named;
they are starting sizes for the rig, not balance numbers.

---

## 1. How a rig edit lands (read this first)

There are three kinds of source, and "edit once" means something slightly different for each.

| Source kind | Files | Hulls | What one edit touches |
|---|---|---|---|
| **Kit interiors** (one shared renderer) | `docs/art/rigs/boat-interiors-kit/boatInteriorRig.js` (`LAYOUT` entries, :64-219), hand-authored sidecars `boat-interiors-kit/*.interior.json` and `boat-interiors-kit/lobsterBoatVariants/*.interior.json` | Cape Islander, Lobster Boat, the 18 lobster variants, Coastal Packet, Tanker (the "22 kit sidecars") | Any renderer change re-stamps **every** kit sidecar and def and needs a mesh re-bake (`boat-interiors-kit/README.md:166-169`). So all kit-interior items below go into **one renderer pass**. |
| **Walk-sourced interiors** (one rig script per hull) | `docs/art/rigs/<hull>IsoRig*.js` writing `docs/art/rigs/gameplay/*.walk.json` | Express cruiser, go-fast, both sport fishers (one script, `sportFisherIsoRig2.js`), Motor Yacht 84, Expedition Yacht 120, side dragger, both stern trawlers | One script edit per hull, except the sport fishers, which share one script and one digest (PR #1229 notes), so both variants are edited and re-pinned together. |
| **Hull rigs** (exterior, deck, lamps) | `docs/art/rigs/<hull>IsoRig.js` + `docs/art/rigs/gameplay/*.gameplay.json` (`ANCHORS`) → `Data/Boats/Decks/*Iso.asset`, `Data/Boats/HullMeshes/*IsoHullMesh.asset` (`Lamps`) | every hull | One edit per hull rig. `lobsterBoatVariantsIsoRig.js` is one file for all 18 variants. |

Two more art surfaces are **not hull rigs** and are edited once per console, not per hull:
- **Helm instruments** are 2D helm-band rigs under `docs/art/rigs/ui/` (`novi-helm`, `cape-islander-helm`,
  `console-helm`, `sport-skiff-helm`, `compass`, `wind-dial`, `depth-finder`, `fish-finder`, `radar`,
  `chartplotter`). A console (`HelmConsoleDef.Rig`, `ConsoleRigKind` in
  `Assets/_Project/Code/Core/Boats/HelmInstruments.cs:45-52`) is shared by several hulls (table in §3.7).
- **Placed fixtures with their own Def** (rod rack, kitchen stores) need an anchor on the hull plus the
  fixture's own art; the fixture art is drawn once and placed per hull.

The kit renderer also has **owed work already recorded**: emit the hand-authored `prep` anchors, `bunk`
blocks and second berths so a regeneration keeps them; a centreline leeboard and split quilt on the Cape
and Lobster Boat V-berths; cushion, pillow and blanket on the variants' bench berth
(`boat-interiors-kit/README.md:124-169`). Those ride in the same renderer pass as everything below.

---

## 2. Shared rules for every list

### 2.1 Rulings that owe rig work

| Ruling (owner, 2026-11-15 unless dated) | Where it is recorded | What it owes a rig |
|---|---|---|
| Players never share a bunk; in co-op the night passes only when every player is in a bunk | `docs/design/crew-roles-and-trip-limits.md:18`; `boat-interiors-kit/README.md:150-151`; `Code/Core/Crew/CrewPolicy.cs:11` | at least one separate `sleep` anchor per player who can be aboard overnight |
| Hired hands may hot-bunk in two watches (shipped ON) | `crew-roles-and-trip-limits.md:19-22,136`; `CrewPolicy.cs:12-13,46-47` | hands need half a bunk each; players still need whole ones |
| More berths on the big working hulls, batched as **one rig edit per hull** | `crew-roles-and-trip-limits.md:132,140-141` | new `sleep` anchors (count: open question Q1) |
| A wheelhouse-bench bunk is fine | owner ruling as relayed in this work order; precedent `bunk_bench` on the 18 variants (`boat-interiors-kit/README.md:160-161`) | a bench berth counts as a berth |
| Cleanliness: wash stations (sea, sink, shower, bath, toilet), outhouse | PR #1237 (open) | washroom fixtures with anchors |
| Kitchens: food stores (pantry locker, pantry, cooler, fridge), seats, dining | PR #1238 (open) | store, `sit` and `table` anchors, and the art for each |
| Microwave, reheat only, per galley | owner ruling as relayed in this work order | a shelf or counter fixture beside the galley |
| Rods stow only in a rod rack or rod locker; "rod rack" owed at the helm per hull | PR #1250 (open), notes | a rack anchor near the helm |
| Autopilot is a purchased tool, offered on recommended hulls | `crew-roles-and-trip-limits.md:144-145` | an instrument slot on the console |
| Helm: heading and wind on the dash, not floating text (PR 1180 hold) | vessel audit §1.4, §7 | flush compass, wind dial; Loran as a period fix (vessel audit Q11) |
| Movable deck gear by size: S anywhere, M alongside, L/XL at a wharf with a crane | owner ruling as relayed in this work order (vessel audit §3.2, §3.4 item 5, Q7) | a fixed slot anchor per mount, with a size and mount class |
| Real scale: footprint = real size x the hull's width compression | owner ruling as relayed in this work order (vessel audit Q6) | sizes in §2.3 |

### 2.2 The scale rule

Footprint on the rig = real footprint x **k**, where k = walk width ÷ real beam. Walk width is twice
`WalkHalfExtents.x` in the hull's `Data/Boats/Decks/<Hull>Iso.asset`. **Real beams are not data** (vessel
audit §1.1: "Beam, price, class and passenger count are not fields"), so the beams below are general
knowledge and **unconfirmed**; the owner or art lane should correct any that are wrong, and k moves with them.
Heights are not compressed (the character's headroom is fixed).

| Hull | Walk width m (deck asset) | Assumed real beam m | k |
|---|---|---|---|
| Dory | 0.45 (`DoryIso`) | 1.5 | 0.30 |
| Punt | 1.01 (`PuntIso`) | 1.9 | 0.53 |
| Console skiff | 1.87 (`ConsoleIso`) | 2.4 | 0.78 |
| Sport skiff / Mk2 | 1.47 / 2.14 | 2.3 / 2.4 | 0.64 / 0.89 |
| Bowrider | 1.36 | 2.3 | 0.59 |
| Zodiac FRC / Hurricane | 1.95 / 2.11 | 2.5 / 2.6 | 0.78 / 0.81 |
| Lobster inshore 8.6 m | 2.62-2.90 | 3.4 | 0.77-0.85 (use 0.81) |
| Lobster 12 m variants / Lobster Boat | 3.29-3.65 / 3.49 | 4.4 | 0.75-0.83 / 0.79 |
| Cape Islander | 3.31 | 4.6 (vessel audit: 4.3-4.9) | 0.72 |
| Lobster offshore 14.6 m | 3.77-4.18 | 5.2 | 0.73-0.80 (use 0.77) |
| Express cruiser | 2.38 | 3.2 | 0.74 |
| Go-fast | 1.67 | 2.6 | 0.64 |
| Sport fisher convertible | 3.62 | 5.2 | 0.70 |
| Sport fisher skybridge | 5.92 | 7.3 | 0.81 |
| Motor Yacht 84 | 5.42 | 6.4 | 0.85 |
| Expedition Yacht 120 | 7.82 | 8.5 | 0.92 |
| Side dragger | 5.57 | 7.0 | 0.80 |
| Stern trawler / Mk II | 7.35 / 7.48 | 10.0 | 0.75 |
| Coastal packet | 9.88 | 11.0 | 0.90 |
| Tanker | 16.65 | 19.0 | 0.88 |
| Sloop 30 / 88 | no deck asset | n/a | unknown until a deck asset exists |
| Yacht tender, jet ski | 0.66 / 0.60 (walk is a standing patch, not the hull) | 2.2 / 1.2 | not meaningful: no fixtures owed |

### 2.3 Fixture sizes (real, then x k for the hulls that take them)

| Fixture | Real m | Inshore 0.81 | 12 m 0.79 | Cape 0.72 | Offshore 0.77 | SF conv 0.70 | SF sky 0.81 | MY84 0.85 | Exp 0.92 | Dragger 0.80 | Trawler 0.75 | Packet 0.90 | Tanker 0.88 | Express 0.74 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Single berth | 1.90 x 0.65 | 1.54 x 0.53 | 1.50 x 0.51 | 1.37 x 0.47 | 1.46 x 0.50 | 1.33 x 0.46 | 1.54 x 0.53 | 1.62 x 0.55 | 1.75 x 0.60 | 1.52 x 0.52 | 1.43 x 0.49 | 1.71 x 0.59 | 1.67 x 0.57 | 1.41 x 0.48 |
| Toilet compartment | 0.70 x 0.90 | 0.57 x 0.73 | 0.55 x 0.71 | 0.50 x 0.65 | 0.54 x 0.69 | 0.49 x 0.63 | 0.57 x 0.73 | 0.60 x 0.77 | 0.64 x 0.83 | 0.56 x 0.72 | 0.53 x 0.68 | 0.63 x 0.81 | 0.62 x 0.79 | 0.52 x 0.67 |
| Basin / sink | 0.40 x 0.30 | 0.32 x 0.24 | 0.32 x 0.24 | 0.29 x 0.22 | 0.31 x 0.23 | 0.28 x 0.21 | 0.32 x 0.24 | 0.34 x 0.26 | 0.37 x 0.28 | 0.32 x 0.24 | 0.30 x 0.23 | 0.36 x 0.27 | 0.35 x 0.26 | 0.30 x 0.22 |
| Shower stall | 0.70 x 0.70 | 0.57 x 0.57 | 0.55 x 0.55 | 0.50 x 0.50 | 0.54 x 0.54 | 0.49 x 0.49 | 0.57 x 0.57 | 0.60 x 0.60 | 0.64 x 0.64 | 0.56 x 0.56 | 0.53 x 0.53 | 0.63 x 0.63 | 0.62 x 0.62 | 0.52 x 0.52 |
| Wet head (toilet + hand shower) | 0.80 x 1.00 | 0.65 x 0.81 | 0.63 x 0.79 | 0.58 x 0.72 | 0.62 x 0.77 | 0.56 x 0.70 | 0.65 x 0.81 | 0.68 x 0.85 | 0.74 x 0.92 | 0.64 x 0.80 | 0.60 x 0.75 | 0.72 x 0.90 | 0.70 x 0.88 | 0.59 x 0.74 |
| Washer-dryer combo | 0.60 x 0.55 | (too small) | (too small) | (too small) | (too small) | (too small) | 0.49 x 0.45 | 0.51 x 0.47 | 0.55 x 0.51 | 0.48 x 0.44 | 0.45 x 0.41 | 0.54 x 0.50 | 0.53 x 0.48 | (too small) |
| Upright fridge | 0.60 x 0.65 | - | - | - | - | 0.42 x 0.46 | 0.49 x 0.53 | 0.51 x 0.55 | 0.55 x 0.60 | 0.48 x 0.52 | 0.45 x 0.49 | 0.54 x 0.59 | 0.53 x 0.57 | - |
| Under-counter fridge | 0.50 x 0.55 | - | - | - | 0.39 x 0.42 | - | - | - | - | - | - | - | - | 0.37 x 0.41 |
| Cooler (50 L) | 0.70 x 0.40 | 0.57 x 0.32 | 0.55 x 0.32 | 0.50 x 0.29 | 0.54 x 0.31 | - | - | - | - | - | - | - | - | - |
| Pantry locker | 0.40 x 0.40 | 0.32 x 0.32 | 0.32 x 0.32 | 0.29 x 0.29 | 0.31 x 0.31 | 0.28 x 0.28 | 0.32 x 0.32 | 0.34 x 0.34 | 0.37 x 0.37 | 0.32 x 0.32 | 0.30 x 0.30 | 0.36 x 0.36 | 0.35 x 0.35 | 0.30 x 0.30 |
| Microwave (shelf, no floor footprint) | 0.45 x 0.35 | - | 0.36 x 0.28 | 0.32 x 0.25 | 0.35 x 0.27 | 0.32 x 0.25 | 0.36 x 0.28 | 0.38 x 0.30 | 0.41 x 0.32 | 0.36 x 0.28 | 0.34 x 0.26 | 0.41 x 0.32 | 0.40 x 0.31 | 0.33 x 0.26 |
| Rod rack, 4 rods (overhead) | 2.10 x 0.35 | 1.70 x 0.28 | 1.66 x 0.28 | 1.51 x 0.25 | 1.62 x 0.27 | 1.47 x 0.25 | 1.70 x 0.28 | 1.79 x 0.30 | 1.93 x 0.32 | 1.68 x 0.28 | 1.58 x 0.26 | 1.89 x 0.32 | 1.85 x 0.31 | 1.55 x 0.26 |
| Dinette table for 4 | 0.90 x 0.60 | - | 0.71 x 0.47 | 0.65 x 0.43 | 0.69 x 0.46 | 0.63 x 0.42 | 0.73 x 0.49 | 0.77 x 0.51 | 0.83 x 0.55 | 0.72 x 0.48 | 0.68 x 0.45 | 0.81 x 0.54 | 0.79 x 0.53 | 0.67 x 0.44 |

"-" means that fixture is not on that hull's list. The rod rack is 4 rods because `stowage.rod_rack` has
capacity 4 (PR #1250).

### 2.4 Each item, its anchor shape and the code that consumes it

| Item | Anchor / field it needs | Consumer | Consumer on main? |
|---|---|---|---|
| Berth | `BoatInteriorDef` anchor, `Action: sleep` (`BoatInteriorDef.ActionSleep`, `Code/Core/Boats/BoatInteriorDef.cs:223`) with `BunkLevel`, `BunkObstruction`, `MattressCentre`, `PillowEnd`; ids append-only | `BunkCount.Of` (`Code/Core/Crew/VoyageSpec.cs:140`), `CrewPolicy`, the sleep-in-a-bunk flow (#1210) | **yes** |
| Toilet | today's data uses `Action: toilet` (e.g. `Data/Boats/Interiors/MotorYacht84Iso.asset`, `ExpressCruiserIso.asset`); PR #1237 spells the toilet verb `sit` (`BoatInteriorDef.ActionSit`) | PR #1237 wash stations | **no** (open PR; the verb mismatch is flagged in §8) |
| Sink, shower, bath | a wash-station anchor per kind (PR #1237: one station Def per kind; sea < sink < shower ceilings) | PR #1237 | **no** |
| Food store (pantry locker, pantry, cooler, fridge) | `food_storage` anchor with a `kitchen_storage` def id (PR #1238; 4 shipped `KitchenStorageDef` assets under `Resources/Kitchen/Storage`, ids unconfirmed) | PR #1238 `KitchenStores` | **no** |
| Seat, table | `sit` anchor with a `seat` block; `table` anchor with a `table` block (PR #1238) | PR #1238 `DiningRules`, `SeatSession` | **no** |
| Microwave | no anchor shape exists; proposed: an interact anchor beside `cook`, action unconfirmed | none (no "microwave" anywhere in `Assets/_Project/Code`) | **no consumer yet** |
| Rod rack | hull-local anchor for a `StowageRack` fixture holding `stowage.rod_rack` (PR #1250) | PR #1250 `StowageRack`, `StowageRule` | **no** |
| Helm instruments | `HelmConsoleDef` fields (`Data/Boats/Helms/*.asset`) + the console's helm-band rig | `HelmControlRelay`, `BoatEquipment` (ids `Code/Boats/BoatEquipment.cs:29-40`) | compass, sounder, finder, radar, GPS, wind dial ids **yes**; Loran and autopilot ids **no** |
| Deck gear slot | today `BoatDeckGearDef.Mounts[]` / `StackBases[]` (`Code/Boats/BoatDeckGearDef.cs`; kinds `Code/Boats/DeckGearKitDef.cs:15-23`); proposed `Slots[]` with size and mount class (vessel audit §3.4, slice R4) | `SternDeckGearPresenter`, trap haul; proposed `IBoatLoadout` | mounts **yes** (2 hulls); slots **no** |
| Lamp | `HullMeshDef.Lamps[]` (`Code/Core/Boats/HullMeshDef.cs:337`), kinds `HullLampKind` 0-7 (`Code/Core/Boats/HullLamp.cs`: port, starboard, stern, masthead, cabin glow, spotlight, anchor light, range light) | `BoatLamps` (`Code/Art/BoatLamps.cs`), probe `Code/Tools/Editor/RigBaking/BoatLampAnchorProbe.cs` | **yes** |
| Laundry (washer-dryer) | no anchor shape exists; PR #1237 lists no laundry station kind | none | **no consumer yet** |

Items with no consumer yet (microwave, laundry, Loran, autopilot) are still drawn in the batched edit so the
rig is not reopened later; their anchors use a provisional id and are noted as unconsumed in the sidecar.

---

## 3. Per-hull lists

Counts in each heading are owed items. Where a family shares one rig file and one parametric layout, the
family is one section with per-variant numbers.

### 3.1 Cape Islander (`boat.cape_islander`, 12.9 m, crew slots 3) — 10 owed

**Has today**
- Interior `Data/Boats/Interiors/CapeIslanderIso.asset` (source `boat-interiors-kit/capeIslanderIsoRig.interior.json`): levels `house_sole`, `cuddy_sole`; obstructions `helm_console`, `helm_seat`, `stove_counter`, `locker`, `v_berth`.
- Berths: 2, `bunk` (-0.314, 2.9, 0.3) and `bunk_starboard` (0.314, 2.9, 0.3), split V-berth. Galley: `stove` (cook) and `prep` on `stove_counter`; `locker` (storage). No washroom, no seat anchor, no table.
- Helm: `helm.cape_islander` (`Data/Boats/Helms/CapeIslanderHelm.asset`): ships a depth sounder only (`DefaultCompass: 0`), supports finder, both compasses, radar, GPS, `MaxWind: 2`. Deck helm station (0, 1.35, 0.74) (`Decks/CapeIslanderIso.asset`).
- Deck gear `Data/Boats/DeckGear/CapeIslanderDeckGear.asset`: hauler (1.3, -2.56, 1.592), banding table (-0.95, -3.2), chop board (0.45, -4.1), bait bin (-1.05, -5.0); stacks: deck (-0.25, -5.9) x3, washboard (-1.47, -5.45) x2.
- Lamps: all of kinds 0-6 (`HullMeshes/CapeIslanderIsoHullMesh.asset`). Rod storage: none.
- Voyage record says "two bunks for three slots" (`Data/Boats/Voyages/VoyageCapeIslander.asset:57`).

**Owed**
1. **Third berth**: a wheelhouse-bench berth (ruled fine). Why: three crew slots, players never share; "one rig edit per hull" (`crew-roles-and-trip-limits.md:140`). Where: the house sole has no bench box today; the free strip is starboard, outboard of the console (x 0.58 to 1.25, y 1.48 to 2.54, about 0.67 x 1.06 m). Footprint 1.37 x 0.47 needs y 1.1-2.5 and would close the locker's aisle: **check on the rig host**; fallback is a pilot berth over the V-berth. Consumer: `BunkCount.Of`.
2. **Head**: marine toilet + basin in the cuddy forward of the companionway, or under a V-berth infill. Why: PR #1237. Footprint compartment 0.50 x 0.65, basin 0.29 x 0.22. No shower: too small (the sea and a deck hose are her wash). Consumer: PR #1237.
3. **Food stores art**: draw the pantry locker and cooler at PR #1238's `food_storage` anchors, and a cupboard door on the stove counter. Why: PR #1238 notes "no cooler or fridge is drawn on any hull". Footprint cooler 0.50 x 0.29.
4. **Seat and dining art**: draw whatever PR #1238 placed as `sit` / `table` here (positions unconfirmed; the PR pins "which hulls dine"). Recommended: a drop-leaf table on the port house side, 0.65 x 0.43.
5. **Microwave** on a shelf above the stove counter (the counter's forward part is the prep station, README :132-136). 0.32 x 0.25. Consumer: none yet.
6. **Rod rack at the helm**: overhead, on the wheelhouse deckhead along the centreline aft of the console, 1.51 x 0.25, reached from the helm seat aisle (interior `helm` reach (0, 0.3, 0.72)). Consumer: PR #1250.
7. **Deck slot anchors** (vessel audit §3.3): `rail_port_mid` (Rail, S, abreast the house), `stern_transom` (Stern, M, transom centre), `bow_pulpit` (Bow, M, at the stem, off the deck area), `mast` (Mast, S, house top aft). Existing mounts become slots unchanged. Consumer: proposed `BoatDeckGearDef.Slots[]` (R4).
8. **Renderer-owed V-berth dressing**: centreline leeboard and split quilt (`boat-interiors-kit/README.md:166-167`).
9. **Helm fit** (console art, edited once in `ui/cape-islander-helm`, shared with five other hulls, §3.7): flush compass by default, wind display, Loran slot, autopilot slot (recommended hull: 12 m+, vessel audit §7).
10. **Hold access for ice** (vessel audit §3.3 `hold` slot): a hatch anchor on the cockpit sole if the hold is to be walked to; unconfirmed whether the rig already draws one.

Too small for: shower, laundry, fridge (cooler instead, per PR #1238's small-hull rule).

### 3.2 Lobster Boat (`boat.lobster_boat`, 12 m, crew slots 3) — 9 owed

**Has today**
- Interior `Interiors/LobsterBoatIso.asset` (source `boat-interiors-kit/lobsterBoatIsoRig.interior.json`): `helm_console`, `helm_seat`, `stove_counter`, `locker`, **`bench`**, `v_berth`.
- Berths 2: `bunk` (-0.3, 3.81, 0.24), `bunk_starboard` (0.3, 3.81, 0.24). Galley: `stove`, `prep`, `locker`. No washroom, seat or table anchor.
- Helm `helm.novi` (`Helms/NoviHelm.asset`): sounder only by default; supports finder, both compasses, radar, GPS, `MaxWind: 2`.
- Deck gear `DeckGear/LobsterBoatDeckGear.asset`: hauler (1.34, -1.5, 1.44), banding (-0.85, -1.6), chop (0.55, -3.4), bait bin (-1.05, -4.4); stacks deck x5, washboard x2.
- Lamps kinds 0-6. Rod storage none.

**Owed**
1. **Third berth** on the existing `bench` box (the variants' `bunk_bench` precedent): 1.50 x 0.51. Why: as 3.1 item 1.
2. **Head** (toilet + basin) in the cuddy: compartment 0.55 x 0.71, basin 0.32 x 0.24. No shower.
3. **Food stores art** at PR #1238's anchors (cooler 0.55 x 0.32, pantry locker 0.32 x 0.32).
4. **Seat and dining art** at PR #1238's anchors; the bench doubles as the seat; table 0.71 x 0.47.
5. **Microwave** shelf over the stove counter, 0.36 x 0.28.
6. **Rod rack at the helm**, overhead, 1.66 x 0.28.
7. **Deck slot anchors** as the Cape plan, at this hull's own coordinates (vessel audit §3.3): `rail_port_mid`, `stern_transom`, `bow_pulpit`, `mast`.
8. **Renderer-owed V-berth dressing** (README :166-167).
9. **Helm fit** (console art in `ui/novi-helm`, shared by 19 hulls): flush compass default, wind display, Loran, autopilot.

Too small for: shower, laundry, fridge.

### 3.3 Lobster standard 12 m, six variants (`boat.lobster_standard_{open,hardtop}_{fundy,newfoundland,northumberland}`, crew slots 3) — 9 owed each

One rig (`docs/art/rigs/lobsterBoatVariantsIsoRig.js`, kit copy `boat-interiors-kit/hull-rigs/lobsterBoatVariantsIsoRig.js`) and one parametric layout (`LAYOUT.lobvar`, `boatInteriorRig.js:210`) draw all six; open and hardtop share the interior numbers.

**Has today** (`Interiors/LobsterStandard*Iso.asset`)

| Variant | Helm reach | Stove / prep | Locker | V-berth `bunk` | `bunk_bench` | Walk width |
|---|---|---|---|---|---|---|
| Fundy | (0, 1.536) | (-0.26, 2.281) / (-0.31, 2.46) | (0.129, 2.107) | (0, 3.95, 0.08) | (0.9, 3.73, 0.5) | 3.29 |
| Newfoundland | (0, 1.307) | (-0.389, 1.71) / (-0.389, 1.91) | (0.429, 1.47) | (0, 4.19, 0.08) | (1.3, 3.51, 0.5) | 3.65 |
| Northumberland | (0, 1.056) | (-0.212, 1.373) / (-0.33, 1.615) | (0.252, 1.08) | (0, 4.37, 0.08) | (1.12, 3.48, 0.5) | 3.49 |

- Helm `helm.novi`. **No `BoatDeckGearDef`** (only the Cape and Lobster Boat have one, vessel audit §1.3), though every variant sidecar carries `ANCHORS.hauler` and `tubs` (vessel audit §1.3). Lamps kinds 0-6.

**Owed**
1. **Third berth**: split the cuddy V-berth into two singles like the Cape (the README keeps it "one person's bed" today, :160-161). Footprint 1.50 x 0.51 each; the drawn V-berths are compressed well below that (vessel audit §1.1: 0.35 to 1.15 m), so **check fit per variant**; fallback a port pilot berth.
2. **Head**: portable or marine toilet in the cuddy, compartment 0.55 x 0.71. Basin only if it fits.
3. **Food stores art** at PR #1238's anchors.
4. **Seat and dining art** at PR #1238's anchors (bench seat).
5. **Microwave** shelf, 0.36 x 0.28.
6. **Rod rack at the helm**, overhead, 1.66 x 0.28.
7. **Deck gear mounts**: hauler, banding table, chop board, bait bin and pot stack bases, so a `BoatDeckGearDef` can be authored per variant (vessel audit §3.4 item 2), plus the Cape-plan slots.
8. **Renderer-owed bench dressing**: cushion, pillow, folded blanket on `bunk_bench` (README :167-168).
9. **Helm fit** (`ui/novi-helm`): as 3.2 item 9.

Too small for: shower, laundry, fridge.

### 3.4 Lobster inshore 8.6 m, six variants (`boat.lobster_inshore_*`, crew slots 2) — 7 owed each

Same rig file and `LAYOUT.lobvar` as 3.3.

**Has today** (`Interiors/LobsterInshore*Iso.asset`)

| Variant | V-berth `bunk` | `bunk_bench` | Stove / prep | Walk width |
|---|---|---|---|---|
| Fundy | (0, 2.862, 0.07) | (0.3, 2.02, 0.44) | (0.156, 1.736) / (0.156, 1.912) | 2.62 |
| Newfoundland | (0, 3.034, 0.07) | (0.93, 2.76, 0.44) | (-0.29, 1.294) / (-0.29, 1.472) | 2.90 |
| Northumberland | (0, 3.163, 0.07) | (0.84, 2.6, 0.44) | (0.068, 0.989) / (0.068, 0.99) | 2.78 |

- Berths 2 = crew slots 2: **no berth owed**. Helm `helm.novi`. No `BoatDeckGearDef`. Lamps kinds 0-6.

**Owed**
1. **Toilet**: a portable toilet under the V-berth infill, compartment 0.57 x 0.73 or none if it does not fit. Why: PR #1237. No basin, no shower.
2. **Food stores art** at PR #1238's anchors (cooler 0.57 x 0.32).
3. **Seat art** at PR #1238's `sit` anchor (the bench). No table: no room.
4. **Rod rack at the helm**, overhead, 1.70 x 0.28.
5. **Deck gear mounts** for a new `BoatDeckGearDef`, and slots per the vessel audit's inshore plan: the Cape names minus `bow_pulpit`, `mast` and the scallop A-frame; `deck_centre_aft` is size M (§3.3).
6. **Renderer-owed bench dressing** (README :167-168).
7. **Helm fit** (`ui/novi-helm`): flush compass, wind display, Loran. **No autopilot** (vessel audit §7 recommends it from 12 m).

Too small for: shower, basin, microwave (the counter holds stove and prep), table, laundry, fridge.

### 3.5 Lobster offshore 14.6 m, six variants (`boat.lobster_offshore_*`, crew slots 4) — 10 owed each

Same rig file and `LAYOUT.lobvar` as 3.3.

**Has today** (`Interiors/LobsterOffshore*Iso.asset`)

| Variant | V-berth `bunk` | `bunk_bench` | Stove / prep | Walk width |
|---|---|---|---|---|
| Fundy | (0, 4.782, 0.088) | (1.12, 4.27, 0.55) | (-0.262, 2.698) / (-0.32, 2.872) | 3.77 |
| Newfoundland | (0, 5.074, 0.088) | (1.53, 4.19, 0.55) | (-0.61, 2.08) / (-0.61, 2.325) | 4.18 |
| Northumberland | (0, 5.293, 0.088) | (1.32, 4.16, 0.55) | (-0.409, 1.668) / (-0.409, 1.962) | 4.00 |

- Berths 2 for 4 slots ("bunks; stores; hold 126 HU", `Voyages/VoyageLobsterOffshore*.asset:46`). Helm `helm.novi`. No `BoatDeckGearDef`. Lamps 0-6.

**Owed**
1. **Two more berths** (to 4): split the V-berth into two singles and add a port pilot or bench berth; 1.46 x 0.50 each. Why: four crew slots, players never share (count: Q1).
2. **Head**: wet head (toilet + hand shower) 0.62 x 0.77 with a basin 0.31 x 0.23. A 14.6 m offshore boat on week trips is the smallest hull where a shower is recommended.
3. **Food stores**: PR #1238 art, plus an under-counter fridge 0.39 x 0.42 if the PR's def for this hull is a cooler (unconfirmed which it chose).
4. **Seat and dining**: dinette 0.69 x 0.46 at PR #1238's anchors.
5. **Microwave** shelf, 0.35 x 0.27.
6. **Rod rack at the helm**, 1.62 x 0.27.
7. **Deck gear mounts and slots**: the Cape set plus `deck_centre_fwd` (L), `rail_port_fwd` (S), `stern_transom` as L (vessel audit §3.3).
8. **Renderer-owed bench dressing** (README :167-168).
9. **Helm fit** (`ui/novi-helm`): flush compass, wind display, Loran, autopilot.
10. **Hold hatch anchor** for the ice / hold slot (unconfirmed whether drawn).

Too small for: laundry.

### 3.6 Side dragger (`boat.side_dragger`, 25 m, crew slots 5) — 11 owed

**Has today**
- Interior `Interiors/SideDraggerIso.asset`, walk-sourced from `gameplay/sideDraggerIsoRig.side_dragger.walk.json` (rig `docs/art/rigs/sideDraggerIsoRig.js`; a kit `LAYOUT.dragger` also exists, `boatInteriorRig.js:81`). Levels `house`, `bridge`, `below`.
- Berths 4 in a row at x 0.32: `berth_aft` y -8.98, `berth_mid_aft` -8.08, `berth_mid_fwd` -7.18, `berth_fwd` -6.28 (z 0.3). Galley: `galley` (cook) (-0.03, -5.46, 2.07); `mess_table` (action `use`) (0.3, -5.5); `chart_table` (use) on the bridge. **No storage, toilet, sink, sit or table anchor.**
- Working anchors: `winch` (haul), `net` (shoot_haul), `gallows_fore`, `gallows_aft` (trawl_door), `hold_hatch`, `hold_hatch_fwd`, `windlass`; crew stations winchman / gallows hand / hatch hand in the walk json (vessel audit §1.3).
- Helm: **none** (`SideDragger.asset` `Helm: {fileID: 0}`). Lamps kinds 0-4 and 6, no spotlight (`HullMeshes/SideDraggerIsoHullMesh.asset`). Rod storage none.
- Voyage: "4 bunks against 5 slots" (`Voyages/VoyageSideDragger.asset:35`).

**Owed**
1. **Berths**: at least one more (to 5 = crew slots); 1.52 x 0.52 each. Why: big working hull, batched berth ruling (count: Q1).
2. **Head**: toilet compartment 0.56 x 0.72, basin 0.32 x 0.24, shower stall 0.56 x 0.56. Why: PR #1237; week trips (`crew-roles-and-trip-limits.md:62`).
3. **Laundry**: washer-dryer combo 0.48 x 0.44 near the head. Big enough at 25 m on week trips. Consumer: none yet.
4. **Food stores**: pantry 0.32 x 0.32 footprint and an upright fridge 0.48 x 0.52 by the galley (walk-sourced, so not in PR #1238's 22 kit sidecars).
5. **Mess seats and table**: turn `mess_table` into a `table` anchor and add `sit` anchors for 4 (PR #1238 shape); table 0.72 x 0.48.
6. **Microwave** shelf by `galley`, 0.36 x 0.28.
7. **Rod rack at the helm**, bridge deckhead, 1.68 x 0.28.
8. **Stand-in helm console** on the bridge: today there is no console to put instruments on (helm plan slice 6, vessel audit §7 "Stand-in consoles"). Fit: sounder, finder, flush compass, radar, GPS or Loran, wind display, autopilot.
9. **Swappable deck slots** (vessel audit §3.3): `deck_pound` (L), `deck_fwd` (L), `rail_stbd_mid` (S), `mast` (S). Fixed gallows, winch and hatches stay.
10. **Searchlight** (`HullLampKind.Spotlight`) on the wheelhouse top. Recommended, not ruled: the only working hulls without one are the dragger, trawlers, packet and tanker.
11. **Dry stores locker** (a `storage` anchor): she has none at all.

### 3.7 Helm consoles (edited once each, not per hull)

| Console (`Data/Boats/Helms/`) | Rig (`ui/`) | Hulls on main | Owed in its one edit |
|---|---|---|---|
| `helm.novi` (`ConsoleRigKind.Novi`) | `novi-helm` | Lobster Boat + 18 variants | flush compass shown by default (plan slice 1), wind display, Loran slot, autopilot slot |
| `helm.cape_islander` (`Cape`) | `cape-islander-helm` | Cape Islander, express cruiser, go-fast, Motor Yacht 84, both sport fishers | as Novi |
| `helm.console_skiff` (`Console`) | `console-helm` | console skiff, yacht tender, both Zodiacs | wind dial (`MaxWind: 1`); dome compass ships |
| `helm.sport_skiff` (`Sport`) | `sport-skiff-helm` | three sport skiffs, bowrider, jet ski | wind dial; dome compass ships |
| **none** | none | dory, dory outboard, punt, punt upgraded, sloops, side dragger, stern trawlers, packet, tanker, expedition yacht | stand-in consoles (plan slice 6) for the offshore hulls and the sloops; open boats get a handheld only (vessel audit §7) |

The wind dial art exists (`docs/art/rigs/ui/wind-dial/README.md`, grades `dial` and `display`). Loran and
autopilot need new `ui/` rigs and new `instrument.*` ids (not in `BoatEquipment.cs:29-40`).

### 3.8 Stern trawler and Mk II (`boat.stern_trawler`, `boat.stern_trawler_mk2`, 38 m, crew slots 8) — 11 owed each

Separate rig scripts (`sternTrawlerIsoRig.js`, `sternTrawlerMk2IsoRig.js`), shared kit layout (`LAYOUT.trawler2 = LAYOUT.trawler`, `boatInteriorRig.js:110`). Do both in one pass.

**Has today**
- Interiors `Interiors/SternTrawlerIso.asset` / `SternTrawlerMk2Iso.asset`, walk-sourced (`gameplay/sternTrawler*IsoRig.*.walk.json`). Levels `house`, `bridge`, `below`.
- Berths 4: `berth_aft_port` (-0.3, 2.62, 1.15), `berth_aft_stbd` (0.3, 2.62), `berth_fwd_port` (-0.3, 8.38), `berth_fwd_stbd` (0.3, 8.38). Galley `galley` (cook) (-0.1, 8.6, 3.52); `mess_table` (use) (0.2, 7.9); `chart_table`. No storage, toilet, sink, sit or table anchor.
- Working: `winch` (0, 0, 3.5), `net_drum` (0, -9.98), gallows port/stbd (I: ±2.3, -17.2; Mk II: ±3, -12.7), `hold_hatch` (+ `hold_hatch_fwd` on the Mk II), `windlass`.
- Helm none. Lamps kinds 0-4, 6. "4 bunks against 8 slots (real crew 10-16)" (`Voyages/VoyageSternTrawler*.asset:30`).

**Owed**
1. **Four more berths** (to 8 = crew slots; vessel audit Q4 recommended 8), 1.43 x 0.49 each, in the below deck. Count: Q1.
2. **Head**: toilet 0.53 x 0.68, basin 0.30 x 0.23, shower 0.53 x 0.53. A second toilet is recommended at 8 crew.
3. **Laundry**: washer-dryer 0.45 x 0.41.
4. **Food stores**: pantry and upright fridge (0.45 x 0.49) by the galley.
5. **Mess seats and table**: `table` anchor at `mess_table`, `sit` anchors for 6-8; table 0.68 x 0.45 (or two).
6. **Microwave** shelf, 0.34 x 0.26.
7. **Rod rack at the helm**, 1.58 x 0.26.
8. **Stand-in helm console** (plan slice 6), full fit with autopilot.
9. **Swappable deck slots** (vessel audit §3.3): `deck_sort_port` (L), `deck_sort_stbd` (L), `deck_fwd` (L), `mast` (S); `net_drum` (XL) reuses the existing anchor; `stern_ramp` (XL) is implied by the hull and needs a named anchor.
10. **Searchlight** (recommended, not ruled).
11. **Dry stores locker** (`storage` anchor).

### 3.9 Sport fisher convertible (`boat.sport_fisher_convertible`, 16.2 m, crew slots 4) — 10 owed

Shares `sportFisherIsoRig2.js` and its digest with the skybridge (PR #1229): edit both together.

**Has today**
- Interior `Interiors/SportFisherConvertibleIso.asset`, walk-sourced (`gameplay/sportFisherIsoRig2.convertible.walk.json`). Levels `house_sole`, `below_sole`, `vip`.
- Berths 3: `bunk` (-0.287, 0.765, 0.55), `bunk_starboard` (0.833, 1.32), `bunk_vip` (0, 4.495, 0.98). Galley `stove` (-1.095, -1.205, 1.72); `locker`, `locker_below` (storage). Ride seats (`enter_ride`): mezzanine, two helm, two salon, flybridge, `fighting_chair` (-0.64, -7.7, 1.3). **Rod storage**: `rods_port` / `rods_starboard` (storage) at the cockpit sides, plus `tub_port` / `tub_starboard`.
- Drawn but not anchored: a fridge column and an ensuite head "shower + MSD" as a dressed volume in `LAYOUT.sport53` (`boatInteriorRig.js:159,168-169`). Whether that kit layout is the art this hull ships with is **unconfirmed** (its def is walk-sourced).
- Helm `helm.cape_islander`. Lamps kinds 0-6.

**Owed**
1. **Fourth berth** (to 4 = crew slots), 1.33 x 0.46: a saloon settee berth is the likely fit.
2. **Head anchors**: toilet, basin and shower in the drawn head compartment (0.70-1.38 x, 2.40-3.00 y in the kit layout): toilet compartment 0.49 x 0.63, shower 0.49 x 0.49.
3. **Food stores**: fridge anchor at the drawn fridge column (upright, 0.42 x 0.46) and a pantry locker.
4. **Dining**: `sit` and `table` anchors on the drawn settees and low table (`boatInteriorRig.js:156-158`); the `enter_ride` salon seats are ride seats, not dining seats.
5. **Microwave** shelf in the galley, 0.32 x 0.25.
6. **Rod rack at the helm** (flybridge), 1.47 x 0.25. The cockpit `rods_*` anchors stay and become rod lockers (a `StowageDef` tag, data only).
7. **Deck slots** (vessel audit §3.3): `cockpit_centre` (M, the existing fighting chair), `rail_port` / `rail_stbd` (S rod banks), `transom` (S), `bow_pulpit` (M), `tower` (Mast, S).
8. **Helm fit** (`ui/cape-islander-helm`): as Novi; autopilot.
9. **Digest refresh** for both sport fisher variants after the script edit (PR #1229).
10. **Sea-temperature gauge slot**: listed for sport fishers in vessel audit §7; not ruled, so draw the slot only if the console has room.

Too small for: laundry.

### 3.10 Sport fisher skybridge (`boat.sport_fisher_skybridge`, 27.4 m, crew slots 6) — 10 owed

**Has today**
- Interior `Interiors/SportFisherSkybridgeIso.asset` (`gameplay/sportFisherIsoRig2.skybridge.walk.json`). Levels `house_sole`, `bridge_sole`, `below_sole`, `vip`.
- Berths 4: `bunk`, `bunk_port`, `bunk_starboard`, `bunk_vip`. `head` (toilet) (-1.73, -6.55, 2.88). Galley `stove`, `locker`. Ride seats: salon x5, stools x3, skylounge x2, aft deck, helm x2, `fighting_chair`. Rod storage `rods_port` / `rods_starboard`.
- Drawn but not anchored (kit `LAYOUT.sport90`, `boatInteriorRig.js:173-207`, same caveat as 3.9): island, fridge, `head_master` "shower + MSD", `head_guest`.
- Helm `helm.cape_islander`. Lamps kinds 0-4, 6 (no spotlight).

**Owed**
1. **Two more berths** (to 6), 1.54 x 0.53.
2. **Head anchors**: shower and basin in the master head, toilet and basin in the guest head (one `head` toilet exists): shower 0.57 x 0.57, basin 0.32 x 0.24.
3. **Laundry**: washer-dryer 0.49 x 0.45. Big enough at 27 m, week trips.
4. **Food stores**: fridge anchor at the drawn fridge (0.49 x 0.53), pantry.
5. **Dining**: `table` at the island, `sit` at the stools and settees.
6. **Microwave**, 0.36 x 0.28.
7. **Rod rack at the helm**, 1.70 x 0.28.
8. **Deck slots** as the convertible, larger.
9. **Helm fit** (`ui/cape-islander-helm`), autopilot.
10. **Searchlight** (recommended; the convertible has one).

### 3.11 Motor Yacht 84 (`boat.motor_yacht_84`, 25.5 m, crew slots 6) — 9 owed

**Has today**
- Interior `Interiors/MotorYacht84Iso.asset` (`gameplay/motorYacht84IsoRig.motor_yacht_84.walk.json`). Levels `saloon_sole`, `dining_helm`, `lower_sole`, `garage`, `crew_mess`.
- Berths 6 (`sleep_master`, `sleep_guest_out`, `sleep_guest_in`, `sleep_vip`, `sleep_crew_lower`, `sleep_crew_upper`); the vessel audit finds 5 reachable (§1.1). Toilets 4 (`toilet_master_bath`, `_guest_head`, `_vip_head`, `_crew_head`); `sink` (wash_dishes) and `stove` in the galley; `dine_port`, `dine_stbd` (dine); 15 `sit` anchors.
- The rig draws the master bath "between its basin and its shower" (`docs/art/rigs/motorYacht84IsoRig.js:1587`); no shower or basin anchor.
- Helm `helm.cape_islander`. **Lamps: none** (`HullMeshes/MotorYacht84IsoHullMesh.asset` `Lamps: []`).

**Owed**
1. **Fix the sixth berth's reach** (not a new berth): vessel audit §1.1.
2. **Shower and basin anchors** in the master bath (drawn), and basins in the three heads: shower 0.60 x 0.60, basin 0.34 x 0.26.
3. **Laundry**: washer-dryer 0.51 x 0.47 in the crew area.
4. **Food stores**: upright fridge 0.51 x 0.55 and pantry by the galley.
5. **Microwave**, 0.38 x 0.30.
6. **Rod rack at the helm**, 1.79 x 0.30.
7. **Navigation lamps**: port, starboard, stern, masthead, cabin glow, anchor light (kinds 0-4, 6).
8. **Helm fit** (`ui/cape-islander-helm`), autopilot.
9. **Writer version prerequisite**: PR #1229 records a writer mismatch (QA 10.4 vs available 10.3) to settle before regenerating.

Existing dining (`dine`, `sit`) covers the dining ask. No fishing deck slots (vessel audit §3.3).

### 3.12 Expedition Yacht 120 (`boat.expedition_yacht_120`, 36.75 m, crew slots 8) — 9 owed

**Has today**
- Interior `Interiors/ExpeditionYacht120Iso.asset` (`gameplay/expeditionYacht120IsoRig.expedition_yacht_120.walk.json`). Levels `below_sole`, `lazarette`, `house_sole`, `bridge_sole`, `top_sole`.
- Berths 8 (`bed_guest_aft_port/stbd`, `bed_vip_port/stbd`, `bed_crew_port/stbd`, `bed_master`, `bed_captain`): equal to crew slots. Galley and bars: `bench_crew_pantry`, `chop_galley`, `bench_galley`, `bench_sky_bar`, `bench_fly_bar` (cook). **Laundry room** with `bench_laundry` (wash_dishes) (`expeditionYacht120IsoRig.js:1338-1340`), "Linen and stores" room. Two helms (`helm`, `helm_flybridge`).
- The rig names ensuites and a crew head (`expeditionYacht120IsoRig.js:540,1370`); **no toilet, shower or sit anchor**.
- Helm **none**. Lamps none (`Lamps: []`).

**Owed**
1. **Washer-dryer anchors** in the existing laundry room (0.55 x 0.51 each).
2. **Ensuite and crew head anchors**: toilet 0.64 x 0.83, basin 0.37 x 0.28, shower 0.64 x 0.64 per ensuite, plus the crew head.
3. **Food stores**: fridge and pantry in the galley and crew pantry; the "Linen and stores" room as a dry store.
4. **Dining**: `sit` and `table` anchors (none today).
5. **Microwave** in the crew pantry, 0.41 x 0.32.
6. **Rod rack at the helm**, 1.93 x 0.32.
7. **Helm console** for both helms (a yacht console; plan slice 6 lists her among the hulls with none), autopilot.
8. **Navigation lamps**, kinds 0-4, 6, plus a searchlight.
9. **Tank note**: her record says "her 700 L tank is small for her class" (`Voyages/VoyageExpeditionYacht120.asset:24`); not art, listed so the edit does not forget a tank volume change if the owner rules one.

### 3.13 Express cruiser (`boat.express_cruiser`, 10.2 m, crew slots 2) — 7 owed

**Has today** (`Interiors/ExpressCruiserIso.asset`, `gameplay/cruiserIsoRig.express_cruiser.walk.json`): berths `bunk` (0, 0.95, 0.22), `bunk_aft` (0, -0.71) (aft reach unverified, `Voyages/VoyageExpressCruiser.asset:28`); `stove`, `sink` (wash_dishes), `head` (toilet), `locker`; seven ride seats. Helm `helm.cape_islander`. Lamps none.

**Owed**
1. **Verify or fix the aft berth's reach.**
2. **Hand shower** in the head (wet head 0.59 x 0.74) or a transom shower; check fit.
3. **Food stores**: under-counter fridge 0.37 x 0.41 or cooler.
4. **Dining**: convertible dinette, `table` 0.67 x 0.44 and two `sit` anchors.
5. **Microwave**, 0.33 x 0.26.
6. **Rod rack at the helm**, 1.55 x 0.26.
7. **Navigation lamps**, kinds 0-4, 6.

Too small for: laundry, more berths (2 = crew slots). No autopilot (not a recommended hull, vessel audit §7).

### 3.14 Go-fast (`boat.go_fast`, 12.36 m, crew slots 2) — 3 owed

**Has today** (`Interiors/GoFastIso.asset`, `gameplay/goFastIsoRig.go_fast.walk.json`): `Levels:` empty, so `bunk` (0, -0.78, 0.26) is unusable; `cooler` (storage, "Icebox", `goFastIsoRig.js:1717`), `head` (toilet), `locker`; five ride seats. Helm `helm.cape_islander`. Lamps none.

**Owed**
1. **A walkable cuddy level** so the existing berth and head can be used; PR #1229 finds she cannot take two singles and a galley in her current envelope.
2. **Tag the icebox as a cooler** store (PR #1238 shape; data plus a lid in the art).
3. **Navigation lamps**, kinds 0-4, 6.

Too small for: second berth, galley, microwave, shower, laundry, table, rod rack (not a fishing hull; add one only if the owner wants rods aboard every hull).

### 3.15 Coastal packet (`boat.coastal_packet`, 60 m, crew slots 10) — 10 owed

**Has today** (`Interiors/CoastalPacketIso.asset`, kit `boat-interiors-kit/coastalPacketIsoRig.interior.json`, `LAYOUT.packet` `boatInteriorRig.js:111-124`): berths `bunk`, `bunk_starboard` (±2.5, -27.85, 2.2); `stove`, `prep`, `locker`; `bench` and `table_port` obstructions; `crane_pedestal`, `hold_coaming`. Helm none. Lamps kinds 0-4, 6. "bunks: needs more cabins" (`Voyages/VoyageCoastalPacket.asset:29`).

**Owed**
1. **Berths to 10** (8 more), 1.71 x 0.59, in cabins on the below deck (count: Q1).
2. **Heads**: toilet 0.63 x 0.81, basin, shower 0.63 x 0.63; two heads for 10 crew.
3. **Laundry**: washer-dryer 0.54 x 0.50.
4. **Food stores art** at PR #1238's anchors (the PR gives "the two galley hulls" a pantry and fridge; that these are the packet and tanker is my reading, unconfirmed).
5. **Seat and dining art** at PR #1238's anchors (bench and table exist as obstructions).
6. **Microwave**, 0.41 x 0.32.
7. **Rod rack at the helm**, 1.89 x 0.32.
8. **Stand-in helm console** on the bridge (plan slice 6), autopilot.
9. **Cargo deck anchors**: the vessel audit suggests a fish pump on `hold_1` later (§3.3, Q14); draw the slot if the hold coaming allows.
10. **Searchlight** (recommended).

### 3.16 Tanker (`boat.tanker`, 110 m, crew slots 14) — 9 owed

**Has today** (`Interiors/TankerIso.asset`, kit `tankerIsoRig.interior.json`, `LAYOUT.tanker` `boatInteriorRig.js:125-139`): berths 3 (`bunk`, `bunk_starboard`, `bunk_forward`); `stove`, `prep`, `locker`; `bench`, `table_port`. Helm none. Lamps kinds 0-3, 7, 4, 6 (range light included).

**Owed**
1. **Berths to 14** (11 more), 1.67 x 0.57, as crew cabins (count: Q1).
2. **Heads** (at least three for 14): toilet 0.62 x 0.79, basin, shower 0.62 x 0.62.
3. **Laundry**: washer-dryer 0.53 x 0.48.
4. **Food stores art** at PR #1238's anchors.
5. **Seat and dining art** at PR #1238's anchors; a mess for 14 likely needs a second table.
6. **Microwave**, 0.40 x 0.31.
7. **Rod rack at the helm**, 1.85 x 0.31.
8. **Stand-in helm console** (plan slice 6), autopilot.
9. **Searchlight** (recommended).

### 3.17 Sloop 30 and Sloop 88 (`boat.sloop_30` 9.4 m crew 2; `boat.sloop_88` 27 m crew 6) — 8 and 10 owed

**Has today**: no interior def and no deck asset (`Voyages/VoyageSloop30.asset:28`, `VoyageSloop88.asset:24`: "no interior def yet, so no bunk is built"). The rigs draw a cabin: Sloop 30 has galley, head compartment, settees and table, lockers and a V-berth (`docs/art/rigs/sail-rig-kit/sloop-30/sloopIsoRig.js:30-33`); Sloop 88 a deck saloon, galley, guest double, VIP island berth and crew cabin, with the owner's cabin unmodelled (`sail-rig-kit/sloop-88/sloop88IsoRig.js:24-27`). Helm none. Lamps none.

**Owed (Sloop 30)**: 1 interior def and walk sidecar; 2 two separate berths (V-berth split or a settee berth); 3 toilet and basin anchors in the drawn head; 4 food stores (cooler, pantry locker); 5 `sit` / `table` at the drawn settees and table; 6 rod rack; 7 navigation lamps (sidelights, stern, anchor; masthead for motoring); 8 a helm console with a wind dial (wind matters most under sail; plan slice 6 lists the sloops).

**Owed (Sloop 88)**: the same eight with 6 berths (and the unmodelled owner's cabin), plus 9 shower anchors in the heads and 10 a washer-dryer combo and microwave (a 27 m yacht; counted as one item).

### 3.18 Dory and dory outboard (`boat.dory`, `boat.dory_outboard`, 4.5 m, crew slots 1) — 2 owed

**Has today**: deck `Decks/DoryIso.asset` (walk 0.45 m wide; whether the outboard dory reuses it is unconfirmed, vessel audit §11); no interior, no helm, `Lamps: []` (`HullMeshes/DoryIsoHullMesh.asset`); oars as hull props (`Data/Boats/HullProps/DoryOar*`).

**Owed**
1. **A lantern**: one all-round white (`HullLampKind.AnchorLight`, or a stern light) on a short pole aft. Why: lamps on every hull that can be out after dark.
2. **Deck slots** (vessel audit §3.3): `thwart_aft` (S: one longline tub or handline reel), `bow` (S: bait or one pot). A rod rests in a rail holder in the S slot; no rack.

Too small for: everything else.

### 3.19 Punt and punt upgraded (`boat.punt`, `boat.punt_upgraded`, 5.2 m, crew slots 1) — 2 owed

**Has today**: deck `Decks/PuntIso.asset` (walk 1.01 m); no interior, no helm; `HullMeshes/PuntIsoHullMesh.asset` has **no `Lamps` field at all**; motors as hull props.

**Owed**
1. **Lamps**: a combined bow sidelight (port and starboard) and an all-round white aft.
2. **Deck slots**: `rail_stbd` (S: T1 hauler or rod holder), `deck_aft` (M: tote or 2 pots), `bow` (S) (vessel audit §3.3).

Too small for: everything else.

### 3.20 Skiffs, RIBs and personal craft — one row per hull

None of these has an interior or a voyage longer than a day (`crew-roles-and-trip-limits.md:49-51`), so berths, galley, washroom, microwave and laundry are all too small. All have `Lamps: []` in their hull mesh def.

| Hull | Helm (`Data/Boats/Helms/`) | Owed | Count |
|---|---|---|---|
| Console skiff (`boat.console_skiff`, 7 m) | `helm.console_skiff` | 1 lamps (sidelights, stern/all-round); 2 rail rod-holder slots (S) and one deck slot (M: cooler or live well) (vessel audit §3.3) | 2 |
| Sport skiff (`boat.sport_skiff`, 7 m) | `helm.sport_skiff` | 1 lamps; 2 rod-holder slots and a cooler slot | 2 |
| Sport skiff Mk2 (`boat.sport_skiff_mk2`) | `helm.sport_skiff` | as the sport skiff | 2 |
| Sport skiff twin (`boat.sport_skiff_twin`) | `helm.sport_skiff` | as the sport skiff (deck asset reuse unconfirmed) | 2 |
| Bowrider (`boat.bowrider`, 5.98 m) | `helm.sport_skiff` | 1 lamps; 2 a cooler slot | 2 |
| Yacht tender (`boat.yacht_tender`, 5.82 m) | `helm.console_skiff` | 1 lamps | 1 |
| Zodiac FRC (`boat.zodiac_frc`, 6.66 m) | `helm.console_skiff` | 1 wire the drawn `searchlight` anchor (`gameplay/zodiacFrcIso.gameplay.json:264`) into `Lamps` as a Spotlight, plus sidelights and stern | 1 |
| Zodiac Hurricane (`boat.zodiac_hurricane`, 7.28 m) | `helm.console_skiff` | 1 as the FRC (`gameplay/zodiacHurricaneIso.gameplay.json:292`) | 1 |
| Jet ski (`boat.jet_ski`, 3.3 m) | `helm.sport_skiff` | none: a day craft with no room for any item | 0 |

The wind dial for these consoles is a console edit (§3.7), not a hull edit.

---

## 4. The purse seiner (new rig) — owner: "add it to when we do rig edits"

**Role.** Herring (and mackerel) purse seining, night sets in the Bay of Fundy / south-west Nova Scotia
fishery: about 6 vessels of about 7 crew each (vessel audit §2.1, source [S4]: a 2025 fishery improvement
project presentation). The vessel audit deferred the fishery because "Purse seining needs a seiner hull we do
not have" (Q14); the owner's ruling makes the hull part of the rig-edit batch. It also gives the packet its
carrier role later (Q14).

**Size class.** 20-30 m in the real fleet (vessel audit §2.1). Recommended: **about 27 m LOA, about 8 m
beam** (general knowledge, unconfirmed), placing her between the side dragger (25 m) and the stern trawler
(38 m), with a walk compression near the dragger's (k ≈ 0.80). Crew slots **7**, berths **7** (players
never share; equal to crew slots, Q1).

**Must carry** (general knowledge of the gear, unconfirmed against a named vessel):
1. **Seine net stack** aft on the deck (L/XL slot) and a **power block** on a boom over it (Overhead, XL).
2. **Seine skiff** carried on the stern and launched to hold the net's end: a deck slot plus a launch point.
3. **Purse winch** (Deck, XL) and **purse davit** at the rail where the rings come aboard (Overhead).
4. **Fish pump or brailer** at the rail (Deck, M) and **RSW tanks** below (Hold), with hatch anchors.
5. **Mast** with a lookout (S) and a **searchlight** plus deck work lights (night sets; `HullLampKind.Spotlight`).
6. Navigation lamps (kinds 0-4, 6).
7. **Helm**: a console from the start (not "none"), with sounder, fish finder, flush compass, radar, GPS or Loran, wind display, autopilot; a sonar slot if a later slice adds one (no sonar instrument id exists).
8. **Interior**: 7 berths (1.52 x 0.52 at k 0.80), galley with pantry and upright fridge, mess table and seats for 7, microwave, a head (toilet, basin, shower), a washer-dryer combo, a dry stores locker, a rod rack at the helm.
9. **Crew stations** for the roles: skipper, skiff man, winchman, deckhands (new roles beyond `crew-roles-and-trip-limits.md:88-101`).

**Data she needs** (one entity per file, append-only ids): a `BoatHullDef` (`boat.purse_seiner`), its
visual, `HullMeshDef` (with `Lamps`), `BoatDeckDef`, `BoatInteriorDef`, `BoatDeckGearDef`, a voyage record
`voyage.purse_seiner` with a `work.purse_seine` entry, and fuel/speed and paint assets like her siblings.
The catch side has only an enum bit today (`Gear.Net`, `Code/Fishing/Gear.cs:12`; herring = Net, vessel
audit §1.2): no net controller exists. Phase: the fishery is M3-shaped (vessel audit Q1); drawing the hull now
follows the owner's ruling, wiring the fishery does not.

---

## 5. Order of work (recommended)

1. **Land the consumers first**: PRs #1237 (wash stations), #1238 (kitchen anchors) and #1250 (rod rack) fix
   the anchor shapes the art must emit. Drawing before they merge risks a second edit, which is what the
   ruling forbids.
2. **The kit renderer pass** (`boatInteriorRig.js`): Cape Islander, Lobster Boat, 18 variants, packet,
   tanker in one edit, absorbing the hand-authored anchors and the README's owed dressing. It is the most
   used family (the Cape and Lobster Boat are the only hulls with deck gear today) and the renderer re-stamps
   all 22 anyway.
3. **Cape Islander and Lobster Boat hull rigs** (deck slots), then **`lobsterBoatVariantsIsoRig.js`** (one file,
   18 hulls): the core working boats.
4. **Dory and punt**: the starter boats (the only shipwright offers, vessel audit §1.1). Their lists are two
   items each, so this is cheap.
5. **Side dragger, then both stern trawlers**: berths and the missing helm consoles block their crew rules.
6. **Purse seiner**: after the dragger and trawlers, so she reuses their station and console work.
7. **Sport fishers** (one script, both variants), then **skiffs and RIBs** (lamps and slots only).
8. **Packet and tanker** hull rigs (berths, consoles), then the **yachts, express cruiser, go-fast and
   sloops** (rare or luxury hulls).
9. **Consoles** (§3.7) can run beside any of these: they are `ui/` rigs, not hull rigs.

## 6. Open questions for the owner (only what needs you)

1. **Q1, how many berths on the big working hulls?** Recommended: berths = crew slots (Cape 3, 12 m and
   standard variants 3, offshore 4, side dragger 5, stern trawlers 8, packet 10, tanker 14, purse seiner 7),
   so every co-op player can sleep aboard; hands hot-bunk anyway. The alternative is fewer berths and
   hot-bunking hands only, which caps how many players can stay overnight.
2. **Q2, purse seiner size**: about 27 m with 7 crew (recommended), or another figure.
3. **Q3, rod rack on every hull with a helm, or only fishing hulls?** Recommended: every hull with a helm except
   the go-fast and the jet ski.

## 7. What I could not confirm

- **Real beams** of every hull (not data); every k and every compressed footprint follows from them.
- **PR #1238's** exact anchor positions, which hulls dine, the four `KitchenStorageDef` ids, and which two
  hulls are "the two galley hulls" (read from its description only).
- **PR #1237's** anchor shape for wash stations, and whether existing `toilet` anchors will be renamed to its
  `sit` verb (see §8).
- Whether the kit `LAYOUT.sport53` / `LAYOUT.sport90` art (fridge, heads) is what the shipped sport fisher
  hulls draw, given their defs are walk-sourced.
- Whether the outboard dory, upgraded punt and twin sport skiff reuse their base deck assets.
- Whether the Cape and offshore variants already draw a hold hatch on the cockpit sole.
- Every proposed position in this file except the existing anchors quoted with coordinates: none has been
  checked on the rig host.
- The purse seiner's dimensions and gear list (general knowledge; [S4] gives only fleet size and crew).

## 8. Notes for the lanes (not owner questions)

- **Toilet verb**: walk-sourced defs on main use `Action: toilet` (Motor Yacht 84, express cruiser, go-fast,
  skybridge); PR #1237 spells its toilet verb `sit` (`BoatInteriorDef.ActionSit`, which PR #1238 adds). The
  two need one answer before the art emits toilet anchors.
- **PRs that owe no rig work**: #1248 (lobster seasons: a data rule on `DeckPot`), #1249 (protected species:
  a catch rule), #1235 (kitchens in buildings: no hulls).
- **Digest pins**: any walk-sourced script edit re-pins its sidecar digests; the sport fishers share one.
