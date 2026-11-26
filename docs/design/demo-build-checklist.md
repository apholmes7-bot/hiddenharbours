# Island demo build checklist

The demo is the first week on St Peters. The player arrives, stays on the island,
digs clams, fishes from shore, buys and repairs the broken dory by hand, launches
her and fishes from her. Around day seven, an islander's word will open the way
to Nine Mile Creek in the full game. In the demo, weather must keep every route
off the island closed. This page specifies a one-scene Windows package and the
checks needed before anyone distributes it.

**Status: release plan, not a verified demo.** Source audit: main
`d46bde303e6aa256d23d47a05ede5d7d6f38e51e`, 2026-11-25. The missing-scene safety
proposal is #1504; the build option and shortcut gates are #1510. Neither supplies
the complete island week, a replacement arrival or an ending card. Main now has
dormant weather on all four island sea edges; its activation and the walking
bar closure remain release prerequisites.
No editor, player build or clean-machine playthrough was run for this document.
The existing arrival still depends on an omitted scene, so release is blocked
until an island-only arrival is authored and demonstrated.

## Package contents

The following is the target package after the opening prerequisite is met.
Draft #1510 currently refuses both development and release demo packaging before
setup. Its separate demo save slot is implemented, but it supplies no import.

Use the existing [player build entry point](../../Assets/_Project/Code/Tools/Editor/PlayerBuild.cs)
and [Windows packaging command](../../tools/build/build-windows.ps1). In #1510,
`-Demo` passes `-buildDemo`, selects the first scene and compiles `DEMO_BUILD`.
The option defaults off. `build-info.txt` records `demo: true`; the build report
must independently show this exact enabled scene list:

| Build index | Scene asset | Purpose |
| --- | --- | --- |
| 0 | `Assets/_Project/Scenes/StPeters.unity` | Boot, title, persistent services and the playable island |

There is no additional bootstrap, persistent-core, UI or shared scene to include.
[StPetersBuilder](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs) embeds
the core produced by
[PersistentCoreBuilder](../../Assets/_Project/Code/App/Editor/PersistentCoreBuilder.cs).
Persistence at runtime does not imply another scene in the package.

The ordinary build's complete list is StPeters, NineMileCreek, RicePoint,
Cumberland, EastWater and SouthWater, in that order, all under
`Assets/_Project/Scenes/` with the `.unity` extension. The last five must be absent
from the demo report. Greybox, SampleScene and Greywick are also excluded;
WestWater has a region definition but no committed scene. Do not substitute the
longer list in `EditorBuildSettings.asset` for the explicit player-build list.

Scene exclusion alone does not strip every asset belonging to that region:

| Carry with the scene | Source and reason |
| --- | --- |
| Serialized dependencies | The island's configuration, input bindings, boat roster, arrival skipper, characters, dialogue, licences, items, buildings, interiors, audio, materials, textures and fonts, including their transitive references. Disabling a development picker does not remove its serialized roster. |
| Resource catalogues | Preserve `Assets/_Project/Data/Resources/`, `Assets/_Project/Resources/`, `Assets/_Project/Art/Resources/`, `Assets/_Project/Art/UI/Resources/` and `Assets/_Project/Audio/Resources/`, with their dependencies. These include WorldMap, campaign barriers, runtime catalogues, fleet libraries, UI assets and material retention references. A catalogue entry can retain assets from an omitted region. |
| Streaming payload | The current build guard still requires `Assets/StreamingAssets/RoadFloor/NineMileCreek/RoadFloor.json` and baked PNG maps, even for the one-scene option. The existing build process also checks the cliff bake. Approved baked inputs are prerequisites; this checklist does not authorize a rebake or deletion. A future scoped dependency change would need its own proof. |
| Rendering setup | `Assets/Settings/UniversalRP.asset`, its `Renderer2D.asset` and renderer features, quality settings, global pipeline settings, all referenced materials and the retained shaders below. |
| Player runtime | Keep the supplied executable, matching `_Data` directory, `UnityPlayer.dll`, `MonoBleedingEdge`, native libraries and any other files emitted into the player folder together. Package the folder, not just the executable. |

The pinned [package manifest](../../Packages/manifest.json) contains no Addressables
package; no Addressables catalogue or remote-content delivery is configured in
this build path. A fresh player must work with the supplied local payload.
Resource and streaming dependencies need an actual build-report audit before
claiming any size saving.

### Release shader retention

A successful editor view does not prove release shader retention. The earlier
release reported missing deck-occlusion, foam-advection and silhouette shaders;
the deck figure could draw through the boat. A pink title surface was also
reported without a proven cause. Those observations require a new release check,
not a declaration that the present settings have fixed every symptom.

The nine project entries currently in
[Always Included Shaders](../../ProjectSettings/GraphicsSettings.asset) are below.
Labels identify their role; GUIDs identify the exact assets without relying on
the shortened labels as shader lookup strings. Preserve the seven existing
built-in entries as well.

| Role | Shader asset GUID |
| --- | --- |
| Interaction affordance | `e8b12c02e8ac18b6eec877819e6f59ff` |
| Faceted hull, prop and figure rendering | `afa9df20cb8009f4b90aab0c474c2357` |
| Faceted overlay and creator preview | `d96f074bdb27514459fe6b424e817210` |
| Deck-occluded sprites | `c0084724f192043b50fad6a140be3520` |
| Foam-buffer advection | `5e97971af9254a87ac97cb2ac30caaf6` |
| Silhouette mask | `c2e78043073d72b489d5be9ff0b4f32d` |
| Figure pixel target | `a9f736b500508ffe6b0ce1e9d6a9adc3` |
| Foam transport | `d9d005d31eb743e0b2f147655ebd8461` |
| Calm-water mirror | `6a5e1af317bb477a9e05add1ed7284e0` |

The facet resolve shader, `67d9d1239e5587841bf1d251b3a2a947`, is serialized in
[Renderer2D](../../Assets/Settings/Renderer2D.asset); retain that reference too.
Runtime lookups also exist for ordinary sprites, water overlays, night overlays,
buoys, lamps, shadows, submergence, terrain and tidal faces. Their material,
resource and scene references remain required; the nine-row table is not a
replacement for them. In particular,
[TidalFaceWaterline](../../Assets/_Project/Code/Art/TidalFaceWaterline.cs) loads a
resource material before its shader fallback. Do not prune these retention
materials because their names appear unrelated to a scene.

Check the release player at the title, creator, cabin, deck, first step ashore,
day and night, with water and interactions visible. Inspect its log for missing
shaders and its screen for pink surfaces, invisible figures or failed occlusion.
The calm-water mirror from #1477 is now on main. It adds the ninth retained
project shader; this page changes no rendering assets or accepted appearance.

## Size: measured baseline and planning allowance

The available 2026-11-06 Windows x64 / Mono release used uncompressed player data
and **five** scenes: StPeters, NineMileCreek, RicePoint, Cumberland and EastWater.
It predates SouthWater's addition to the build list. Its recorded BuildReport was
2,295,488,896 bytes; the output folder measured 2,295,748,211 bytes. No final zip
or compression comparison was produced for that measurement. These are historical
measurements, not a build of the pinned main or of the proposed demo.

| Large packed assets in that report | Bytes | MiB |
| --- | ---: | ---: |
| Six tanker interior images, sum | 294,928,296 | 281.27 |
| Four lobster/cape islander rocking images, sum | 196,343,144 | 187.25 |
| Terrain relight and detail, sum | 71,647,464 | 68.33 |
| Small shipyard image | 42,201,884 | 40.25 |
| Two base lobster/cape islander images, sum | 49,085,624 | 46.81 |
| These fifteen entries together | 654,206,412 | 623.90 |

Arithmetic: `294,928,296 + 196,343,144 + 71,647,464 + 42,201,884 + 49,085,624
= 654,206,412` bytes, about 28.5% of the old folder. All twenty listed top entries
sum to 744,352,504 bytes. These are already inside the total, not additions to it.
The old road bake separately reported 153.0 MiB. The currently available local
road payload totals 162,807,070 bytes across 3,744 PNGs plus its manifest, about
155.26 MiB. The PNGs are build inputs rather than all being committed assets;
this local count is not a portable fresh-checkout size guarantee.

For planning, reserve **about 3.0 GB extracted**:
`2,295,748,211 × 1.30 = 2,984,472,674` bytes rounded to whole bytes. The 30%
allowance is a provisional buffer for later content, not a measured upper bound.
The five omitted scenes may save space, but shared references prevent subtracting
five sixths of the folder or any unmeasured per-scene amount. Source image sizes
and small LFS pointer files do not establish imported texture sizes.

Zip size remains unknown. The existing packager rejects a zip over
4,000,000,000 bytes; use that as a ceiling, not a predicted download. For a first
tester allow about **7 GB free** for a possible 4 GB archive plus 3 GB extraction,
then revise this from the actual package and allow extra space if extraction
software creates temporary copies. Publish both final byte counts and a checksum.
Compare identical content before claiming savings from Lz4HC. Any asset trimming
or changed texture quality is separate work and needs approval of its appearance.

## Fresh-machine experience

The target is a Windows x64 PC with keyboard, mouse, an ordinary writable user
profile and a graphics driver capable of running the shipped renderer. Extract
the entire zip into a writable folder, then double-click the supplied executable.
No editor, project checkout or development SDK should be required. Do not invent
a minimum GPU, RAM amount or Windows-version promise: a supported hardware matrix
and any prerequisite installer still need clean-machine verification. A renderer
initialization failure or missing runtime DLL is a failed distribution check.
Test the local single-player journey without a network connection.

On a new profile, the title should show New game, Settings and Quit; Continue
appears only when a save exists. This is implemented in
[TitleScreen](../../Assets/_Project/Code/UI/Shell/TitleScreen.cs). New game on an
existing profile requires confirmation before replacing that game's save. Use a
disposable profile for acceptance, never an existing player's save.

The intended first journey is: open the title, choose New game, create the male
player, experience the arrival aboard the skipper's boat, come out of the cabin,
wait for the boat to lie alongside, use Step ashore and walk on the island with
ordinary control. Keeping the arrival entirely inside St Peters is the proposed
adaptation, subject to the owner decision below. Exact timing is unmeasured.

The pinned scene does **not** yet provide that journey in a one-scene package.
It references [ArrivalSea](../../Assets/_Project/Data/Intro/ArrivalSea.asset),
which names EastWater as its sea region and StPeters as home. In
[ArrivalOpening](../../Assets/_Project/Code/App/ArrivalOpening.cs), `TryBegin`
returns `BeginSeaHop` directly; failure does not call `BeginLocalArrival`.
The character creator is opened on the sea handoff in
[ArrivalOpening.Sea](../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs).
With #1504, refusal releases the clock and cover and clears pending arrival state,
but that is not a replacement introduction. A missing boat journey or creator,
even with a controllable ordinary spawn, fails the first-step acceptance check.

Continue applies the save in the boot scene; it does not select a region scene
from disk. An out-of-region rest anchor currently emits a wake request and leaves
the local spawn in place; no runtime subscriber loads the requested region.
Use synthetic save fixtures to check this safety case. It is not proof that a
full-game save is suitable for the demo. Save format is version 26: store purchases,
special orders and the shopping bag are represented, with no demo completion field.
The configuration draft reserves `DemoEnded` without a publisher. Draft #1535
proposes a session-only signal after a saved sleep; that differs from the canonical
ending at the islander's word in ADR 0062. No closing card is implemented, and a
fixed day-seven stop is not the accepted ending rule.

## Every route off St Peters

The current [WorldMap](../../Assets/_Project/Data/Resources/WorldMap.asset) lists
six regions. [RegionDef.Frame](../../Assets/_Project/Code/World/RegionDef.cs)
places each local rectangle at `WorldOrigin`. St Peters is 760 × 520 m, centered
locally at zero with origin `(760, -200)`: world bounds are x `[380, 1140]`,
y `[-460, 60]`. The following overlaps are calculated from those committed
definitions, not from a sketch or an assumption about region names.

| Route or entry point | Pinned data and required demo check |
| --- | --- |
| West edge by sea | Local x `-380`. Nine Mile Creek meets the edge over local y `[-80, 260]`; outside that span there is no matching neighbour. Sweep the whole edge and both corners in a boat. The neighbour must report unavailable; weather must turn the player back before a seam or fade. |
| East edge by sea | Local x `380`. EastWater meets the edge over local y `[-220, 260]`; the bottom 40 m has no matching neighbour. Sweep the entire edge, including the arrival corridor and corners. EastWater is unavailable in the demo, including a boat left from the opening. |
| North edge by sea | Local y `260`. No region in the pinned map shares a horizontal edge here. Still require weather closure along the full edge and corners; the existing world-boundary hold is only a safety fallback. |
| South edge by sea | Local y `-260`. No region currently shares this edge. SouthWater's committed bounds are x `[-2020, 380]`, y `[-1480, -280]`; despite its descriptive text, it does not meet St Peters along a horizontal edge. Check weather closure and report this map-layout discrepancy; do not relocate a region in this release task. |
| Bar on foot at low tide | `crossing.st_peters_nine_mile_creek_bar` joins `station.st_peters.flats_head` to `station.nine_mile_creek.bar_landing`. The island path runs `(-55, 2.5)` → `(-350, 0)` → `(-356, 0)`; the neighbour path starts `(352, -200)`. Walk it at the lowest water as well as at flood. Exposed ground must not bypass demo availability or the authored weather closure. |
| Legacy bar passage | `PassageToNineMileCreek` at `(-356, 0)`, 6 × 120 m band. Test direct activation as well as the framed walk. Refusal must clear the pending arrival key without moving the player or his boat. |
| Legacy west sea passage | `PassageToWestWater` at `(-356, 150)`, 6 × 120 m band. WestWater is absent from the map and build list, although its definition and this passage exist. A direct activation or non-framed fallback must refuse safely. |
| Legacy east sea passage | `PassageToEastWater` uses the builder's east inset and entrance-landfall latitude, a 6 × 120 m band and the `east_water` return key. Check direct activation and the opening exception. Neither can smuggle EastWater into the demo. |
| Ferry or passenger departure | No functional outbound ferry service was found in the pinned runtime and island wiring. The skipper's arrival is a scripted inward journey, not a scheduled ferry. Check staying aboard, returning below and any post-arrival boarding opportunity; none may become an outbound route. Any later ferry interaction must use the same availability check. |
| Title, rest and autonomous boats | Continue and cross-region wake must retain a controllable local state. Region warm-up must skip omissions. NPC trawl/pot routines inspect loaded scenes rather than load them; absent neighbours must stay absent. Recheck after quit-to-title and restart. |
| Development access | F/V hull swaps, K/Shift+K instrument cycles, G free bait, I free ice, O water comparison, rotation-preview T/Y, preview grass movement/wind, fast tide, forced sea state and forced opening replay must be inert. Direct region requests still go through the availability query. Exercise these both at boot and after Continue. |

Passage names, coordinates and band sizes come from
[StPetersBuilder](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs), and the
foot route from the [bar crossing definition](../../Assets/_Project/Data/Regions/Crossings/StPetersNineMileCreekBar.asset).
[RegionPassage](../../Assets/_Project/Code/World/RegionPassage.cs) normally stands
down in a framed region: the full edge governs travel. The scripted opening and
explicit activation remain separate paths, so testing only trigger bands misses
the ordinary crossing path.

Pinned main retains
[IntroStormEastWaterSouth](../../Assets/_Project/Data/Resources/CampaignBarriers/IntroStormEastWaterSouth.asset).
It belongs to EastWater and lifts on `onboarded`; it does not close the island.
Merged #1494 adds `StPetersStormEast`, `StPetersStormWest`, `StPetersStormNorth`
and `StPetersStormSouth` in the same resource folder. All four stand only while
`st_peters.first_week` is set and lift on `st_peters.weather_turned`; the enabling
flag has no producer on this base, so the bands remain dormant. They push boats;
the walking bar still needs its weather closure. The demo must also keep the bands
active after the full-game gate would lift them. Region unavailability and `RegionTravelRefused`
are safety mechanisms; neither draws weather or supplies final player wording.
No visible wall is an acceptable substitute.

The full shortcut inventory is in #1510. T for ordinary owned, baited pot work,
Space for ordinary fishing, normal walking, steering, lamps, doors, camera,
charts and interactions remain gameplay. A filename beginning with `Dev` does
not by itself mean that its input should be disabled.

## Pre-release checklist

1. Record the exact candidate commit; reconcile #1504, #1510 and the island content/weather/licence/shop work.
2. Build a release Windows x64 Mono player with demo enabled; verify `demo: true` and the single StPeters scene in the report.
3. Confirm every ordinary, omitted and prototype scene is absent from that report except StPeters.
4. Confirm approved resource, streaming, rendering and baked dependencies are present; make no opportunistic asset changes.
5. Read CI compilation and test results; confirm the named safety and both-mode fixtures ran with nonzero counts.
6. Measure the final folder and zip, check the 4 GB zip ceiling, and record checksum and build identity.
7. Extract on a clean Windows profile without the editor; test runtime dependencies, graphics startup and offline play.
8. Complete New game through creation, the accepted arrival and the first controllable step ashore with no missing intro.
9. Check release title, creator, cabin, deck, figures, water and night lighting; investigate every missing-shader warning or pink surface.
10. Sail every edge and corner in available boats, including the arrival corridor; confirm weather closure without a fade or stuck controls.
11. Walk the bar at lowest water and flood; exercise all three legacy passages and confirm no omitted scene or stale arrival key.
12. Check staying aboard and boarding again; confirm no ferry or passenger route can leave the island.
13. Exercise every listed development shortcut after New game and Continue; verify ordinary fishing, pot work and movement still work.
14. Test warm-up, absent-region NPC schedules, sleep/wake and synthetic outside-region saves without hangs or unintended loads.
15. Test save, quit, relaunch, Continue and quit-to-title on a disposable profile; retain version 26 and the separate demo save namespace.
16. Play the island week: licensed catches kept, unlicensed catches released, separate shelf stocks, bag carry and boat repair by hand.
17. Verify weather stays closed after repair and around day seven; apply the approved ending policy without opening an omitted region.
18. Record outstanding defects and owner decisions; distribute only after first-step, closure and release-rendering evidence is accepted.

## Owner decisions still needed

| Decision | Lean and reason |
| --- | --- |
| How does the arrival fit entirely inside St Peters? | Keep the skipper, cabin, creator and step ashore, but author the complete approach inside the included scene. The existing sea hop cannot satisfy the one-scene requirement. Do not silently skip the introduction or add EastWater. |
| How is the canonical ending presented and remembered? | ADR 0062 places completion at the islander's word and allows continued island fishing. Keep every external scene unavailable. The saved-sleep timing in #1535 is a separate proposal; presentation and persistence still need a decision. |
| How does weather close the exposed bar and remain closed after repair? | Give the foot route a clear weather reason as well as the four sea edges; keep the demo closure independent of a full-game unlock flag. A generic unavailable-region message alone does not explain an exposed walkable bar. |
| Should the saved demo week be copied into the full game? | #1510 already separates the demo subfolder from the full-game slot. Prefer an explicit import that preserves both playthroughs; no import is implemented and no version-26 field change is proposed here. |
| Which minimum machines and download ceiling are promised? | Start with a private Windows x64 release and the existing 4 GB archive ceiling, then set public minimums from measured machines and the actual one-scene package. Do not promise a smaller download from scene count alone. |
