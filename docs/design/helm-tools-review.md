# Helm tools review (2026-11-12)

**Asked by the owner, verbatim:** "review all sounder, plotter, charter, radar. all helm tools"

**The owner's requirement that goes with it, verbatim:** "lobster boats will need to rotate traps as
catch migrates, crab and lobster for crustacenas out of NMC, crab likes mud, lobsters need rock,
sounder shows rock/bottom types"

**Status of this document.** A read-only review of the code as it stood on main `3504d3ae7d` plus the
South Water seams (PR 1119). One reader, no editor run. Each finding is marked CONFIRMED (the code
path was read) or PLAUSIBLE. Nothing here is fixed. "Charter" matches no tool in the code and is
read as the chartplotter. Paths are under `Assets/_Project/` unless they start with `docs/`.

A separate lane is writing helm instrument logic (sounder with shallow alarm, speed, wind, plotter,
engine panel, per-hull fit). This review is written so the two meet: section 4 is a requirement on
that work.

## 1. What exists

All card instruments read one Core seam, `GameServices.HelmInstruments` (`IHelmInstruments`,
`Code/Core/Boats/HelmInstrumentsService.cs:39`), implemented by `Code/Boats/HelmControlRelay.cs:306-399`.
What a hull carries is resolved by `Code/Boats/BoatEquipment.cs:42-81` from the hull's `HelmConsoleDef`
and per-hull ownership in the save (`Code/Core/Save/InstrumentLocker.cs:60`).

| Tool | The player sees | Data from | Can it be bought? | Tests |
|---|---|---|---|---|
| Depth sounder | depth, shallow alarm blink, feet or metres, night face, "water temp" | `ITidalTerrain.ElevationAt` and `IEnvironmentService.WaterLevelAt`, throttled (`HelmControlRelay.cs:332-373`) | Yes: `instrument.depth_sounder`, 140, at `seller.leblancs` (`Data/Resources/Catalog/Instruments/DepthSounderOffer.asset:15-26`) | DepthSounderModelTests, SounderOverlayLayoutTests, SounderPlayTests |
| Fish finder | depth, a scrolling bottom picture, fish marks, range | depth as above; fish from `GameServices.FishSchools.MarksAt` (`Code/UI/FishFinderOverlayHost.cs:265`) | No: the offer asset exists (480) but has no catalog block | FishFinderOverlayTests, FishRigGoldenTests, FishFinderPlayTests |
| Chartplotter | baked chart, own boat, track, waypoints, route, tide and set and drift | `ITidalTerrain`, `IActiveBoatService`, `IEnvironmentService`, NavLocker (`Code/UI/ChartplotterOverlayHost.cs:302, 430, 482, 506-511`) | No: offer `instrument.gps` (650) has no catalog block | ChartplotterGlass, Host and MaxFace tests, ChartplotterPlayTests |
| Radar | sweep, coast echoes, vessels, buoys, sea clutter, cursor | coast scanned from `ITidalTerrain`; contacts from `GameServices.RadarContacts` (`Code/UI/RadarOverlayHost.cs:379-384`) | No: there is no offer asset | RadarSeamAndPilotCycleTests, RadarGlassTests, RadarPlayTests |
| Dash compass | a compass on the dash | heading from `IActiveBoatService` (`Code/UI/HelmOverlayHost.cs:312-318`) | No offers; skiffs ship with a dome | CompassHeadingGoldenTests, HelmInstrumentMountTests |
| HUD compass, set and drift | text heading and rose tape | `Code/UI/HudController.cs:121-134, 533` | not an instrument | CompassReadoutTests, HudCompassSmokeTests |

## 2. Defects and risks, most serious first

1. **CONFIRMED. The fish finder's bottom is not the seabed.** The contour is three sine trains
   around depth over range, recomputed every repaint, and the soft-to-hard colour bands are
   decoration (`Code/UI/Draw/FishRigRender.cs:324-358`). The offer's text says it paints the bottom
   under you (`FishFinderOffer.asset:18`).
2. **CONFIRMED. Placeholder readings are shown as real.** Water temperature is a constant 12
   (`Code/Core/Config/GameConfig.cs:1474, 1488`; `Code/UI/SounderOverlayHost.cs:185`). The finder's
   sensitivity, link, battery and volts are placeholders (`FishFinderOverlayHost.cs:225-229`;
   `GameConfig.cs:1511-1512`).
3. **CONFIRMED. The shop does not check that the hull can mount the unit.** `TryBuy` checks hull id,
   already owned, then spends (`Code/Economy/InstrumentShop.cs:102-114`). A hull with no console
   resolves to no fit (`BoatEquipment.cs:44`): the money is taken and nothing shows. Live today for
   the depth sounder on a console-less hull.
4. **CONFIRMED. The radar misses things in the water.** The only producers are the ambient fleet
   and the player's traps (`Code/Boats/RadarContactSource.cs:119-131`). The Nine Mile Creek pot
   boats (`Code/App/NpcPotBoats.cs`) and the nav marks produce no echo. Rain clutter is fixed at
   zero (`RadarOverlayHost.cs:350`); the Rain and Flock echo kinds have no producer
   (`Code/Core/Nav/RadarContact.cs:26-27`).
5. **CONFIRMED. `RadarContactSource` reads a concrete class**, `AmbientFleetPresenter.Instance`
   (`RadarContactSource.cs:121`). Same module, so it is inside rule 4, but it is why App's pot boats
   cannot register themselves.
6. **CONFIRMED. The shallow alarm is visual only.** Its three uses are blink logic
   (`SounderOverlayHost.cs:168`, `FishFinderOverlayHost.cs:208`, `HelmOverlayHost.cs:336`); nothing in
   `Code/Audio` mentions an alarm or a sounder. The offer says it will squeal
   (`DepthSounderOffer.asset:18`). The collapsed-dash glance is drawn only on pilothouse rigs
   (`HelmOverlayHost.cs:284`).
7. **CONFIRMED, minor. Magic numbers:** `SecondsPerHour * 0.05` for tide rising
   (`ChartplotterOverlayHost.cs:493`); `trails: true` (`RadarOverlayHost.cs:351`).
8. **PLAUSIBLE. Per-frame string garbage while the sounder card is up:** `Repaint` builds three
   strings every frame before its unchanged early return (`SounderOverlayHost.cs:227-234`).
9. **PLAUSIBLE. Radar contacts are re-queried every frame while raised**, with no throttle
   (`RadarOverlayHost.cs:243, 384`). The land scan is cached.

Null handling on optional services looked sound in every path read.

## 3. Gaps

- **Radar, chartplotter and fish finder are built and tested but cannot be reached in a shipped
  game.** All four consoles have `DefaultRadar: 0` and `DefaultGps: 0` (`Data/Boats/Helms/*.asset`),
  no shop lists them, and the only path is the developer cycle
  (`Code/Boats/DevInstrumentCycle.cs:399-402`).
- **Compasses:** ids `instrument.compass_dome` and `instrument.compass_flush`
  (`BoatEquipment.cs:33-34`) have no offers. The Novi and the Cape ship with no compass and cannot
  get one.
- **Named in docs and deliberately not built:** VHF and bilge (`docs/art/briefs/helms-pass.md:231`);
  radio, autopilot head and windlass counter (`helms-pass.md:290-292`).

## 4. Requirement: the sounder shows bottom type

**Requirement (owner):** the sounder shows rock against mud and the other bottom types, because
lobster need rock and crab like mud, and the player and the NPC boats choose where to set by it.

**Today.** The depth sounder shows a number. The fish finder draws a bottom, but it is invented
(defect 1). Nothing at runtime knows the bottom type.

**Bottom data that exists.**
- `FishingGroundDef.Bottom` (an `EcologyHabitat` flag set) and `Hardness01`
  (`Code/Fishing/FishingGroundDef.cs:37-39`). Three assets, all in the South Water: the Bank (rock
  and gravel, 0.8), the Deep Mud (mud, 0.05), the Shelf (sand, 0.2). None for Nine Mile Creek.
- No runtime code reads `FishingGroundDef`; its folder is not under Resources.
- A second enum, `FishHabitat` (`Code/Fishing/LivingFishBehaviour.cs:8-12`), has no gravel.
- `ITidalTerrain` gives elevation only. There is no Core seabed seam.

**Shortest honest path, in order.**
1. **Core:** one seam, "bottom at a point", returning a kind and a hardness, with a never-null empty
   default. The kind enum must live in Core (`EcologyHabitat` is in Fishing).
2. **Core:** one read on `IHelmInstruments` beside the depth read, so every instrument asks the helm
   and none asks Fishing.
3. **Fishing:** a provider that loads the grounds at runtime and answers by containment.
4. **Content:** grounds authored for Nine Mile Creek, rock for lobster and mud for crab. This is the
   real missing data.
5. **UI:** the finder's bands (`FishRigRender.cs:343-358`) driven by the kind and hardness; the depth
   sounder gains a bottom word or mark.

**Open, not determined by this review:** whether trap catch reads the bottom at all (a search of the
trap and pot files for habitat, bottom or substrate found nothing), and how a hull with no console
reaches the shop counter.
