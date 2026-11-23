# Cliff walls and walking

Neither **St Peters** nor **Nine Mile Creek** has a complete cliff barrier: the data provides a barrier along the drawn foot of each standing wall, but none specifically along its top edge. The walking controller does not reject a step because the ground becomes steep. This explains how a walker can leave the cliff top and enter the face; it does **not** establish why a walker could pass through every foot barrier from below.

Both regions use the same blocking component and collision layer. All 259 standing walls have valid data for their foot barriers, and their declared joins have no gaps. There is no evidence here that Nine Mile Creek has a working top-edge barrier which St Peters lacks. The other six region scenes contain no standing cliff-kit walls to compare. Intended openings, including beach approaches, remain separate from wall joins.

This is a text audit of revision `598013532f96006a90c324ae86d79c9e24a9e202`, dated 2026-11-22. **No editor, EditMode test, PlayMode walk, screenshot, height-texture sampling or build was run.** “Derived barrier” below means the current code will construct it from the saved data when the component enables; it is not a claim that a live walk was observed stopping there. The reported universal pass-through at St Peters remains a runtime question. No fix is proposed or applied here.

## Region results

The inventory covers all eight tracked scenes under `Assets/_Project/Scenes`, plus the tracked sample and scene template as negative controls. Cliff identification used the `CliffWallSurface` script GUID, wall names, wall-definition GUIDs, prefab references and the builder call sites; only St Peters and Nine Mile Creek contain this kit. No tracked prefab contains `CliffWallSurface`. A terrain height field or a quay face alone is not a cliff-kit wall.

| Region scene | Live walls | Derived foot quads | Declared joins | Brow barrier / cliff walkability edge | Drawn-foot barrier | Region conclusion |
|---|---:|---:|---:|---|---|---|
| St Peters | 210 | 877 | 206 | None authored | All 210 derive solid colliders on Default | Foot blocking supported by data; top-edge blocking absent |
| Nine Mile Creek | 49 | 353 | 44 | None authored | All 49 derive solid colliders on Default | Same cliff limitation as St Peters |
| Cumberland | 0 | 0 | 0 | Not applicable to this kit | None from this kit | No standing cliff-kit walls; not a verdict on steep terrain |
| Rice Point | 0 | 0 | 0 | Not applicable to this kit | None from this kit | No standing cliff-kit walls; not a verdict on steep terrain |
| Greybox | 0 | 0 | 0 | Not applicable | None from this kit | No standing cliff-kit walls |
| Greywick | 0 | 0 | 0 | Not applicable | None from this kit | No standing cliff-kit walls |
| East Water | 0 | 0 | 0 | Not applicable | None from this kit | No standing cliff-kit walls |
| South Water | 0 | 0 | 0 | Not applicable | None from this kit | No standing cliff-kit walls |

Scene filenames above are identifiers, not a change to place names. [Build settings](../../ProjectSettings/EditorBuildSettings.asset#L8) include these scenes and the sample; the sample and URP scene template also have zero cliff components. Quay walls, building walls, fences and dock access are not counted as proof of a continuous cliff barrier.

## How a wall blocks, and what it does not block

1. A [CliffWallDef](../../Assets/_Project/Code/World/TerrainPlan/CliffWallDef.cs#L53) stores its stable ID, status, predecessor (`Follows`), brow, plan toe, drop and toe elevation. It has **no block-brow flag, slope threshold or walkability-edge declaration**. Retired and held definitions are excluded by [IsLive](../../Assets/_Project/Code/World/TerrainPlan/CliffWallDef.cs#L111).
2. [StPetersCliffWalls](../../Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs#L1233) and [NineMileCreekCliffWalls](../../Assets/_Project/Code/App/Editor/NineMileCreekCliffWalls.cs#L111) both create a `CliffWallSurface` and pass the definition's station arrays to `Configure`. The text-refresh path also copies these fields into saved components ([StPetersLayerRefresh.CliffWalls](../../Assets/_Project/Code/App/Editor/StPetersLayerRefresh.CliffWalls.cs#L232)). Definitions are authoring inputs; the saved component arrays are what scene enable uses.
3. [OnEnable calls Rebuild](../../Assets/_Project/Code/Art/CliffWallSurface.cs#L401). Its [array guard and BuildBlocker call](../../Assets/_Project/Code/Art/CliffWallSurface.cs#L441) precede the checks for face textures and material. All 259 saved components are enabled, have active ancestors and have matching brow/toe/drop/elevation arrays with at least two stations. No segment is discarded by the `1e-4` length guard.
4. The barrier follows the **drawn toe**, not the brow or the ground-plan toe: `(toe.x, toe.y - drop * cos(40 degrees))`. See [ToeScreen](../../Assets/_Project/Code/Art/CliffWallGeometry.cs#L90) and [camera projection constants](../../Assets/_Project/Code/Art/SpriteLightMath.cs#L71). The height field does not turn this projected face area into solid ground.
5. [BuildBlocker](../../Assets/_Project/Code/Art/CliffWallSurface.cs#L573) creates a `CliffWallToeBlocker` child on [layer 0 / Default](../../Assets/_Project/Code/Art/CliffWallSurface.cs#L290), with one non-trigger `PolygonCollider2D` quad per segment. Thickness is [0.35 m](../../Assets/_Project/Code/Art/CliffWallGeometry.cs#L114); each end extends 0.175 m beyond its station. The child has `HideFlags.DontSave`. Consequently **zero saved collider components below the wall objects is expected**, not evidence of missing runtime foot blocking. The wall roots themselves have only transforms; there is no saved root-level brow collider either.
6. The player uses a dynamic `Rigidbody2D`, a non-trigger circle of radius **0.35 m** (diameter **0.70 m**) and a foot offset of `(0, -0.7)`. Both player and blocker use Default. [TagManager](../../ProjectSettings/TagManager.asset#L7) names layer 0; the [2D collision matrix](../../ProjectSettings/Physics2DSettings.asset#L56) enables every layer pair. Player collider/body exclusion masks are zero. The controller [enables continuous collision detection](../../Assets/_Project/Code/Player/PlayerWalkController.cs#L443) and [assigns linear velocity](../../Assets/_Project/Code/Player/PlayerWalkController.cs#L554); it does not use a separate cliff cast mask. In normal free-foot mode, [ControlSwitcher restores body simulation and the collider](../../Assets/_Project/Code/Player/ControlSwitcher.cs#L2941).
7. The water gate asks about **water depth at a point**, not slope, a previous-to-next elevation difference or a cliff edge ([TidalWalkability](../../Assets/_Project/Code/Player/TidalWalkability.cs#L65), [FixedUpdate](../../Assets/_Project/Code/Player/PlayerWalkController.cs#L561)). `MovementConstraint` has no cliff implementation wired to it; the non-controller assignments are in [BoatMooring](../../Assets/_Project/Code/Boats/BoatMooring.cs#L337). Neither a steep dry height field nor a painted path creates a walking barrier by itself.

“No brow barrier” means no cliff-owned collider or walkability edge enforcing that boundary. It does not claim that an unrelated nearby prop cannot obstruct a particular step, or that a projected toe quad can never overlap a brow locally. Conversely, a solid toe strip can stop crossing that strip from either side; it does not keep a walker out of the whole face between the strip and the brow, or prevent entry around an open run end.

## St Peters versus Nine Mile Creek, field by field

The representative wall in each column is ID `000`; every other live wall is enumerated below and was checked against its own definition. Scene line references point to the exact base revision above.

| Check | St Peters | Nine Mile Creek | Blocking consequence |
|---|---|---|---|
| Saved wall object | [39046](../../Assets/_Project/Scenes/StPeters.unity#L39046), `CliffWall_DeepShoreCliff_E_ramp_000` | [239690](../../Assets/_Project/Scenes/NineMileCreek.unity#L239690), `CliffWall_LedgeCliff_E_ramp_000` | Different face class, same runtime surface |
| Wall layer / active | [39057–39063](../../Assets/_Project/Scenes/StPeters.unity#L39057): layer 0, active | [239701–239707](../../Assets/_Project/Scenes/NineMileCreek.unity#L239701): layer 0, active | Generated blocker explicitly uses layer 0 regardless |
| Surface enabled / script | [39099–39101](../../Assets/_Project/Scenes/StPeters.unity#L39099), enabled; GUID `54da58bd612c4da42b8ec4712a70b75c` | [239743–239745](../../Assets/_Project/Scenes/NineMileCreek.unity#L239743), same enabled script | No missing component difference |
| Brow / toe / drop / elevation arrays | [39104 / 39108 / 39112 / 39116](../../Assets/_Project/Scenes/StPeters.unity#L39104) | [239748 / 239766 / 239784 / 239802](../../Assets/_Project/Scenes/NineMileCreek.unity#L239748) | Each equals its Def; geometry differs, barrier construction does not |
| Builder handoff | [StPetersCliffWalls 1279–1290](../../Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs#L1279) | [NineMileCreekCliffWalls 153–164](../../Assets/_Project/Code/App/Editor/NineMileCreekCliffWalls.cs#L153) | Same `CliffWallSurface.Configure` call |
| Chunk span | Plan-selected 1 m, [1238–1240](../../Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs#L1238) | Shared 2 m, [49](../../Assets/_Project/Code/App/Editor/NineMileCreekCliffWalls.cs#L49), [95](../../Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs#L95) | Rendering/sorting subdivision; does not enable brow blocking |
| Saved player layer | [39571](../../Assets/_Project/Scenes/StPeters.unity#L39571), Default | [161523](../../Assets/_Project/Scenes/NineMileCreek.unity#L161523), Default | Both eligible for the toe collision |
| Foot shape / exclusions | [43205–43240](../../Assets/_Project/Scenes/StPeters.unity#L43205): radius .35, offset -.7, trigger 0, exclusions 0 | [161705–161740](../../Assets/_Project/Scenes/NineMileCreek.unity#L161705): same | Same .70 m gap comparison |
| Body / exclusions | [43241–43267](../../Assets/_Project/Scenes/StPeters.unity#L43241): dynamic, simulated, continuous, exclusions 0 | [161741–161767](../../Assets/_Project/Scenes/NineMileCreek.unity#L161741): same | No body/layer explanation for one region blocking and the other not |
| Water-gate flag | [39769](../../Assets/_Project/Scenes/StPeters.unity#L39769): `_tideGatedWalk: 1` | [161577](../../Assets/_Project/Scenes/NineMileCreek.unity#L161577): `_tideGatedWalk: 0` on the local development player | A water-depth difference, **not** a cliff or slope gate |
| Which player is used | Start-scene player | Local player is under inactive [DevCore](../../Assets/_Project/Scenes/NineMileCreek.unity#L314963) | [DevRegionBootstrap 64–94](../../Assets/_Project/Code/App/DevRegionBootstrap.cs#L64) activates it only for direct editor entry without a persistent core; travel uses the arriving persistent player. Do not treat the local flag as the travelled player's setting |

**There is no differing cliff-blocking component, layer, flag or builder step to name.** Both use the same toe-only design. The water flag and chunk span are real differences with different purposes. The reported ability to cross every foot strip cannot be resolved from these serialized differences; it would require observing the active player's physics state and the generated children during that walk.

## Wall joins and intentional openings

Measurements use the saved station coordinates, the projection above and the exact four-corner construction in `BuildBlocker`. Unit scales and identity rotations were checked for wall/root transforms and player ancestry. Distances below are geometric clearances between the derived solid quads in world XY, rounded to .001 m, before adding the player's radius. No texture, renderer bounds or silhouette was used.

All **250 `Follows` joins** have exactly equal brow, plan-toe, drop and toe-elevation endpoints: 206 in St Peters, 44 in Nine Mile Creek. The projected endpoint gap is therefore **0 m** in every case, with overlapping extended quads. No declared join admits a .70 m foot collider. [ChunksOfDefs](../../Assets/_Project/Code/App/Editor/StPetersCliffWalls.cs#L639) also rejects non-identical stations. The numeric order of IDs is not coast order after re-cutting: follow the predecessor in each wall row.

Independent runs need a second check: unequal endpoints can still produce overlapping quads. The following are the adjoining breaks along the standing coast, including the deliberate openings. In addition to these, the outer ends of the cliff coast remain open onto non-cliff shore; there is no closed cliff ring.

| Region and neighbouring ends | Endpoint separation | Solid-quad clearance | Wider than .70 m? | Interpretation from data |
|---|---:|---:|---|---|
| St Peters `022 → 221` | 9.806 m | 9.341 m | Yes | East Gap access; `221` continues the run containing `023`, named by the coast asset |
| St Peters `269 → 154` | .435 m | 0 m | No | Separate run declarations, overlapping toe barriers |
| St Peters `043 → 056` | 68.401 m | 67.912 m | Yes | Open main beach / West Gap approach; walls `044–055` retired |
| Nine Mile Creek `016 → 017` | .516 m | 0 m | No | Overlapping toe barriers at a run break |
| Nine Mile Creek `023 → 088` | 3.182 m | 0 m | No | Toe polylines overlap along their runs; an endpoint-only test would be misleading |
| Nine Mile Creek `028 → 029` | 26.025 m | 25.670 m | Yes | Gully opening; road-network footpath ends at `(32, -6)` inside the open interval |
| Nine Mile Creek `035 → 036` | .804 m | 0 m | No | Overlapping toe barriers; endpoint separation exceeds .70 m but there is no solid-geometry gap |

The measurements establish cliff-strip continuity, not clearance from all props or safe water depth across an opening. The broad gaps are supported by [East Gap](../../Assets/_Project/Data/Terrain/StPetersPlan/Coast_EastGap.asset#L66), [West Gap](../../Assets/_Project/Data/Terrain/StPetersPlan/Coast_WestGap.asset#L66), the [retired beach wall record](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_044.asset#L162), and the [current gully path](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json#L513). The old West Gap description names retired `054/055`; the current standing sides of the much wider beach opening are `043/056`. The asset's older IDs are not evidence of present walls.

## Intended ways up and down

These are **authored intentions**, not a newly approved route or a playtest pass. `PaintOnly` paths do not install colliders or grant a slope exception. Several key-scene records say `Placed: 0`; their requested steps or planks must not be counted as built traversal merely because a data row exists. The word `ramp` in a cliff wall's `Batter` is a face-kit choice, not permission to walk that face.

| Region / route | Data and coordinates | What is actually established |
|---|---|---|
| St Peters — East Gap | [Path_EastGapPath](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_EastGapPath.asset#L15), .9 m wide, `(158.5,-39) → (165.5,-49.5)` | Explicit descent beside Gap Brook to pocket beach/spit, between the east cliff runs; paint only |
| St Peters — main beach / West Gap | [Heath path](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_HeathPath.asset#L15), 1.2 m, `(2.5,7) → (12.5,-55.3)`; [West Gap path](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_WestGapPath.asset#L15), .9 m, onwards to `(10,-66.5)` | Main intended beach descent. [Beach steps](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_main_beach.asset#L46) run from `(11,-59)` to `(10.4,-63.8)`; the key scene is unplaced and the path note calls the steps pending. No stair collision or traversal is inferred |
| St Peters — top-of-cliff route and crossings | [Cliff walk](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_CliffWalk.asset#L15), 1 m, `(178,-2.5) → (-43.5,-4)`; [Gap bridge record](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_gap_bridge.asset#L15) | Feeds the gaps and crosses the brooks; chiefly along the top, not a descent through a face. Its note describes stepping stones; the gap key scene separately requests a walkable plank bridge and seaward rail |
| St Peters — north-east head / neck | [Ground_NeNeck](../../Assets/_Project/Data/Terrain/StPetersPlan/Ground_NeNeck.asset#L15), +6 to +11 m over 16 m; [Woods track](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_WoodsTrack.asset#L15) ends `(163.6,48.5)`; [NE light record](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ne_light.asset#L15) declares `path.stp_ne_plank` | Intended climb to the head via the neck/plank route. [Ground ask](../../Assets/_Project/Data/Terrain/StPetersGround/GroundAsk_StpNeHead.asset#L38) preserves those heights. The head's wall IDs `079–146` are held, not standing barriers |
| St Peters — northern bank / cove | [Cart track](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_GinnysTrack.asset#L15), 1.6 m, `(86,45) → (101,69.5)` | Explicit route down the bank to the cove; outside the standing cliff runs |
| St Peters — Alder Fall / north shore | [Shore path](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_ShorePath.asset#L15), [Alder Fall walk](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_AlderFallWalk.asset#L15), [Alder Run](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_AlderRun.asset#L15) | Shore path crosses below the fall's pool using the flat stone and five stepping-stone positions. The stream descends a gully; a waterfall is not declared a climbable stair |
| St Peters — reef, landing and bar approaches | [Reef walk](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_ReefWalk.asset#L15), `(70,64) → (70,88)`; [Slip road](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_SlipRoad.asset#L15); [Bar-head road](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_BarHeadRoad.asset#L15); [Bar walk](../../Assets/_Project/Data/Terrain/StPetersPlan/Path_BarWalk.asset#L15), `(-45,.4) → (-352,0)` | Authored ways to softer shore/landing or out across exposed ground. Reef access becomes tidal past the bluff foot; the bar is tidal. These are not hidden stairways through live wall chunks |
| Nine Mile Creek — gully footpath | [Road network](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json#L513), 1.5 m, `(14,0) → (26,-4) → (32,-6)` | Explicit access route into the opening between `028/029`; the older [coast-sector premise](../../Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs#L219) calls this the way down to the ledges |
| Nine Mile Creek — cove gully steps | [Weather-face key scene](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_weather_face.asset#L22), `structure.nmc_gully_steps`, width 1.1 m, run 7.2 m, rise 6.72 m | Requested stairs from `(37.2,-23.2)` south to `(37.2,-30.4)` with rope rails. [Context 91–101](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_weather_face.asset#L91) explicitly moves them 17 m south of the path end. `Placed: 0`; neither placement, connection to the path nor passage through the toe barriers is verified |
| Nine Mile Creek — shore, falls and river trails | [Shore path](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json#L489), [falls path](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json#L524), [river trail](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json#L535) | Top/shore access and approaches to falls/river; the falls path ends `(-276,188)`, river trail runs `(-270,214) → (-380,232)`. No cliff-wall traversal exception is attached to these routes |
| Nine Mile Creek — wharf road climb | [Ground-plan road](../../Assets/_Project/Code/App/Editor/NineMileCreekGroundPlan.cs#L155) climbs via `(22,95), (27,101), (29,108), (30.5,114), (34,118)` | Ramp to the working yard, beyond the current cliff-wall run. The [old boat-ramp location is absent from G3](../../Assets/_Project/Code/App/Editor/NineMileCreekGroundPlan.cs#L123); its builder constants or `OtterAtTheRamp` name cannot establish a current cliff access |

Creek beds can create a low route in the height data, but a named watercourse is not an instruction that it is safe or intended to walk its entire bed. The following completes the stream/creek candidates around St Peters; none supplies a cliff collider or slope rule:

| Stream / continuation | Endpoints in plan XY | Relationship to access |
|---|---|---|
| [Gap Brook](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_GapBrook.asset#L15) / [Gap Creek](../../Assets/_Project/Data/Terrain/StPetersPlan/Creek_GapCreek.asset#L15) | `(154,-36) → (170,-55.5) → (184.9,-104.3)` | East Gap descent and tidal continuation; path is beside the brook |
| [Heath Brook](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_HeathBrook.asset#L15) / [Heath Creek](../../Assets/_Project/Data/Terrain/StPetersPlan/Creek_HeathCreek.asset#L15) | Brook `(16.5,-55) → (11.616,-78.402)`; creek `(6.5,-72.5) → (-.5,-112.9)` | Main beach / West Gap drainage; the current endpoints do not coincide, so this is not asserted to be one continuous foot route |
| [Storm Brook](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_StormBrook.asset#L15) / [Storm Creek](../../Assets/_Project/Data/Terrain/StPetersPlan/Creek_StormCreek.asset#L15) | `(-27.5,-11) → (-63.5,-21.5) → (-88.8,-35.8)` | Western soft-shore drain and tidal extension, not a declared stair |
| [Alder Run](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_AlderRun.asset#L15) | `(107,55) → (102,108)` | Cove-bank gully/fall; explicit walking line crosses it below the fall |
| [Barren Brook](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_BarrenBrook.asset#L15) | `(41.5,50.5) → (-11,99)` | Shallow valley to marsh/lagoon, outside standing cliff walls |
| [Cove Brook](../../Assets/_Project/Data/Terrain/StPetersPlan/Stream_CoveBrook.asset#L15) | `(56,55.5) → (50,82)` | Runnel through North Cove's dune |
| [Marsh East Creek](../../Assets/_Project/Data/Terrain/StPetersPlan/Creek_MarshEast.asset#L15) / [Marsh West Creek](../../Assets/_Project/Data/Terrain/StPetersPlan/Creek_MarshWest.asset#L15) | `(-3,83) → (12,75.5)` / `(-5,81) → (-19,72.5)` | Marsh drainage, not cliff descents |

Additional Nine Mile Creek steps are declared away from the standing kit walls: north-creek dock `1_steps`, `3_gangway`, `3_steps`, `4_gangway`, `4_steps` ([north-creek record](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L812)); shore-lane `6_stairs`, `7_stairs`, `8_stairs` ([shore-lane record](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L130)); town `wall_stairs_01/02`, `bank_stairs_01/02` and the river deck's west-end stairs ([town-bridge record](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_town_bridge.asset#L1233)). These records all say `Placed: 0`. They cover creek-bank, retaining-wall and deck access, not proof that a natural cliff brow is blocked. The creek mouth's older [soft-coast sectors](../../Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs#L231), dune boardwalks and marsh/river routes likewise provide no continuous cliff edge. No other region has live cliff walls for which to list a kit-specific access opening.

## Every standing wall

Each row identifies one scene object by stable suffix, links its Def and the saved `CliffWallSurface`, and gives its derived number of foot quads. All rows have the same result: **B0 = no authored brow/plan-foot walkability edge or dedicated collider; F1 = solid drawn-foot colliders derived on enable; L0 = Default, colliding with the Default-layer player**. There are no saved wall colliders; no row has a disabled component, inactive ancestor, missing Def, mismatching station array or zero-length toe segment.

The scene link opens the exact component; the object name immediately preceding it is `CliffWall_<class>_<aspect>_<batter>_<suffix>`. “Start” means no `Follows` predecessor, not an accidental gap. Every numbered predecessor has **0 m** join separation. “Banked” and face batter “bank/ramp” still derive a solid toe strip; they do not declare an access route.

### St Peters

| Wall ID / Def | Scene surface | Status | Brow / foot / layer | Foot quads | Follows; join gap |
|---|---|---|---|---:|---|
| [wall.stp_000](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_000.asset#L15) | [L39092](../../Assets/_Project/Scenes/StPeters.unity#L39092) | Kept | B0 / F1 / L0 | 2 | 189; 0 m |
| [wall.stp_001](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_001.asset#L15) | [L61643](../../Assets/_Project/Scenes/StPeters.unity#L61643) | Kept | B0 / F1 / L0 | 3 | 190; 0 m |
| [wall.stp_002](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_002.asset#L15) | [L101072](../../Assets/_Project/Scenes/StPeters.unity#L101072) | Kept | B0 / F1 / L0 | 3 | 191; 0 m |
| [wall.stp_003](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_003.asset#L15) | [L5678](../../Assets/_Project/Scenes/StPeters.unity#L5678) | Kept | B0 / F1 / L0 | 3 | 193; 0 m |
| [wall.stp_004](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_004.asset#L15) | [L20425](../../Assets/_Project/Scenes/StPeters.unity#L20425) | Kept | B0 / F1 / L0 | 2 | 194; 0 m |
| [wall.stp_005](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_005.asset#L15) | [L61754](../../Assets/_Project/Scenes/StPeters.unity#L61754) | Kept | B0 / F1 / L0 | 3 | 196; 0 m |
| [wall.stp_006](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_006.asset#L15) | [L101839](../../Assets/_Project/Scenes/StPeters.unity#L101839) | Kept | B0 / F1 / L0 | 3 | 198; 0 m |
| [wall.stp_007](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_007.asset#L15) | [L51902](../../Assets/_Project/Scenes/StPeters.unity#L51902) | Kept | B0 / F1 / L0 | 4 | 200; 0 m |
| [wall.stp_008](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_008.asset#L15) | [L27335](../../Assets/_Project/Scenes/StPeters.unity#L27335) | Kept | B0 / F1 / L0 | 1 | 007; 0 m |
| [wall.stp_009](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_009.asset#L15) | [L99407](../../Assets/_Project/Scenes/StPeters.unity#L99407) | Kept | B0 / F1 / L0 | 4 | 201; 0 m |
| [wall.stp_010](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_010.asset#L15) | [L90525](../../Assets/_Project/Scenes/StPeters.unity#L90525) | Kept | B0 / F1 / L0 | 4 | 202; 0 m |
| [wall.stp_011](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_011.asset#L15) | [L76715](../../Assets/_Project/Scenes/StPeters.unity#L76715) | Kept | B0 / F1 / L0 | 4 | 203; 0 m |
| [wall.stp_012](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_012.asset#L15) | [L18517](../../Assets/_Project/Scenes/StPeters.unity#L18517) | Kept | B0 / F1 / L0 | 4 | 204; 0 m |
| [wall.stp_013](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_013.asset#L15) | [L26690](../../Assets/_Project/Scenes/StPeters.unity#L26690) | Kept | B0 / F1 / L0 | 4 | 012; 0 m |
| [wall.stp_014](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_014.asset#L15) | [L25823](../../Assets/_Project/Scenes/StPeters.unity#L25823) | Kept | B0 / F1 / L0 | 4 | 206; 0 m |
| [wall.stp_015](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_015.asset#L15) | [L109647](../../Assets/_Project/Scenes/StPeters.unity#L109647) | Kept | B0 / F1 / L0 | 4 | 209; 0 m |
| [wall.stp_016](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_016.asset#L15) | [L69090](../../Assets/_Project/Scenes/StPeters.unity#L69090) | Kept | B0 / F1 / L0 | 5 | 211; 0 m |
| [wall.stp_017](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_017.asset#L15) | [L16220](../../Assets/_Project/Scenes/StPeters.unity#L16220) | Kept | B0 / F1 / L0 | 5 | 212; 0 m |
| [wall.stp_018](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_018.asset#L15) | [L91628](../../Assets/_Project/Scenes/StPeters.unity#L91628) | Kept | B0 / F1 / L0 | 5 | 213; 0 m |
| [wall.stp_019](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_019.asset#L15) | [L91141](../../Assets/_Project/Scenes/StPeters.unity#L91141) | Kept | B0 / F1 / L0 | 5 | 214; 0 m |
| [wall.stp_020](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_020.asset#L15) | [L60276](../../Assets/_Project/Scenes/StPeters.unity#L60276) | Kept | B0 / F1 / L0 | 5 | 216; 0 m |
| [wall.stp_021](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_021.asset#L15) | [L23075](../../Assets/_Project/Scenes/StPeters.unity#L23075) | Kept | B0 / F1 / L0 | 12 | 219; 0 m |
| [wall.stp_022](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_022.asset#L15) | [L5532](../../Assets/_Project/Scenes/StPeters.unity#L5532) | Kept | B0 / F1 / L0 | 13 | 220; 0 m |
| [wall.stp_023](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_023.asset#L15) | [L86306](../../Assets/_Project/Scenes/StPeters.unity#L86306) | Kept | B0 / F1 / L0 | 5 | 221; 0 m |
| [wall.stp_024](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_024.asset#L15) | [L3322](../../Assets/_Project/Scenes/StPeters.unity#L3322) | Kept | B0 / F1 / L0 | 4 | 222; 0 m |
| [wall.stp_025](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_025.asset#L15) | [L18411](../../Assets/_Project/Scenes/StPeters.unity#L18411) | Kept | B0 / F1 / L0 | 3 | 224; 0 m |
| [wall.stp_026](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_026.asset#L15) | [L16706](../../Assets/_Project/Scenes/StPeters.unity#L16706) | Kept | B0 / F1 / L0 | 6 | 225; 0 m |
| [wall.stp_027](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_027.asset#L15) | [L80945](../../Assets/_Project/Scenes/StPeters.unity#L80945) | Kept | B0 / F1 / L0 | 9 | 227; 0 m |
| [wall.stp_028](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_028.asset#L15) | [L110698](../../Assets/_Project/Scenes/StPeters.unity#L110698) | Relined | B0 / F1 / L0 | 9 | 228; 0 m |
| [wall.stp_029](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_029.asset#L15) | [L71917](../../Assets/_Project/Scenes/StPeters.unity#L71917) | Relined | B0 / F1 / L0 | 3 | 149; 0 m |
| [wall.stp_030](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_030.asset#L15) | [L3121](../../Assets/_Project/Scenes/StPeters.unity#L3121) | Relined | B0 / F1 / L0 | 21 | 029; 0 m |
| [wall.stp_031](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_031.asset#L15) | [L99960](../../Assets/_Project/Scenes/StPeters.unity#L99960) | Relined | B0 / F1 / L0 | 9 | 231; 0 m |
| [wall.stp_032](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_032.asset#L15) | [L60687](../../Assets/_Project/Scenes/StPeters.unity#L60687) | Relined | B0 / F1 / L0 | 9 | 233; 0 m |
| [wall.stp_033](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_033.asset#L15) | [L86577](../../Assets/_Project/Scenes/StPeters.unity#L86577) | Relined | B0 / F1 / L0 | 9 | 154; 0 m |
| [wall.stp_034](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_034.asset#L15) | [L94882](../../Assets/_Project/Scenes/StPeters.unity#L94882) | Relined | B0 / F1 / L0 | 12 | 237; 0 m |
| [wall.stp_035](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_035.asset#L15) | [L38810](../../Assets/_Project/Scenes/StPeters.unity#L38810) | Relined | B0 / F1 / L0 | 9 | 238; 0 m |
| [wall.stp_036](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_036.asset#L15) | [L44003](../../Assets/_Project/Scenes/StPeters.unity#L44003) | Relined | B0 / F1 / L0 | 12 | 270; 0 m |
| [wall.stp_037](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_037.asset#L15) | [L81075](../../Assets/_Project/Scenes/StPeters.unity#L81075) | Relined | B0 / F1 / L0 | 7 | 165; 0 m |
| [wall.stp_038](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_038.asset#L15) | [L114512](../../Assets/_Project/Scenes/StPeters.unity#L114512) | Relined | B0 / F1 / L0 | 23 | 037; 0 m |
| [wall.stp_039](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_039.asset#L15) | [L48148](../../Assets/_Project/Scenes/StPeters.unity#L48148) | Kept | B0 / F1 / L0 | 23 | 166; 0 m |
| [wall.stp_040](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_040.asset#L15) | [L112311](../../Assets/_Project/Scenes/StPeters.unity#L112311) | Kept | B0 / F1 / L0 | 23 | 039; 0 m |
| [wall.stp_041](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_041.asset#L15) | [L16034](../../Assets/_Project/Scenes/StPeters.unity#L16034) | Kept | B0 / F1 / L0 | 23 | 040; 0 m |
| [wall.stp_042](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_042.asset#L15) | [L1676](../../Assets/_Project/Scenes/StPeters.unity#L1676) | Banked | B0 / F1 / L0 | 23 | 041; 0 m |
| [wall.stp_043](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_043.asset#L15) | [L57466](../../Assets/_Project/Scenes/StPeters.unity#L57466) | Return | B0 / F1 / L0 | 3 | 042; 0 m |
| [wall.stp_056](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_056.asset#L15) | [L92953](../../Assets/_Project/Scenes/StPeters.unity#L92953) | Banked | B0 / F1 / L0 | 1 | Start |
| [wall.stp_057](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_057.asset#L15) | [L95427](../../Assets/_Project/Scenes/StPeters.unity#L95427) | Banked | B0 / F1 / L0 | 13 | 169; 0 m |
| [wall.stp_058](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_058.asset#L15) | [L94752](../../Assets/_Project/Scenes/StPeters.unity#L94752) | Kept | B0 / F1 / L0 | 9 | 241; 0 m |
| [wall.stp_059](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_059.asset#L15) | [L36930](../../Assets/_Project/Scenes/StPeters.unity#L36930) | Relined | B0 / F1 / L0 | 16 | 243; 0 m |
| [wall.stp_060](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_060.asset#L15) | [L52355](../../Assets/_Project/Scenes/StPeters.unity#L52355) | Relined | B0 / F1 / L0 | 4 | 244; 0 m |
| [wall.stp_061](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_061.asset#L15) | [L85429](../../Assets/_Project/Scenes/StPeters.unity#L85429) | Relined | B0 / F1 / L0 | 16 | 278; 0 m |
| [wall.stp_062](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_062.asset#L15) | [L76217](../../Assets/_Project/Scenes/StPeters.unity#L76217) | Relined | B0 / F1 / L0 | 8 | 173; 0 m |
| [wall.stp_063](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_063.asset#L15) | [L50152](../../Assets/_Project/Scenes/StPeters.unity#L50152) | Relined | B0 / F1 / L0 | 8 | 177; 0 m |
| [wall.stp_064](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_064.asset#L15) | [L61517](../../Assets/_Project/Scenes/StPeters.unity#L61517) | Relined | B0 / F1 / L0 | 8 | 178; 0 m |
| [wall.stp_065](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_065.asset#L15) | [L48389](../../Assets/_Project/Scenes/StPeters.unity#L48389) | Relined | B0 / F1 / L0 | 7 | 248; 0 m |
| [wall.stp_066](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_066.asset#L15) | [L28110](../../Assets/_Project/Scenes/StPeters.unity#L28110) | Relined | B0 / F1 / L0 | 8 | 065; 0 m |
| [wall.stp_067](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_067.asset#L15) | [L97815](../../Assets/_Project/Scenes/StPeters.unity#L97815) | Relined | B0 / F1 / L0 | 5 | 250; 0 m |
| [wall.stp_068](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_068.asset#L15) | [L46509](../../Assets/_Project/Scenes/StPeters.unity#L46509) | Toe lifted | B0 / F1 / L0 | 8 | 188; 0 m |
| [wall.stp_069](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_069.asset#L15) | [L111575](../../Assets/_Project/Scenes/StPeters.unity#L111575) | Toe lifted | B0 / F1 / L0 | 6 | 252; 0 m |
| [wall.stp_070](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_070.asset#L15) | [L18147](../../Assets/_Project/Scenes/StPeters.unity#L18147) | Toe lifted | B0 / F1 / L0 | 4 | 253; 0 m |
| [wall.stp_071](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_071.asset#L15) | [L95975](../../Assets/_Project/Scenes/StPeters.unity#L95975) | Toe lifted | B0 / F1 / L0 | 5 | 255; 0 m |
| [wall.stp_072](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_072.asset#L15) | [L84072](../../Assets/_Project/Scenes/StPeters.unity#L84072) | Kept | B0 / F1 / L0 | 4 | 257; 0 m |
| [wall.stp_073](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_073.asset#L15) | [L29374](../../Assets/_Project/Scenes/StPeters.unity#L29374) | Kept | B0 / F1 / L0 | 3 | 258; 0 m |
| [wall.stp_074](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_074.asset#L15) | [L65022](../../Assets/_Project/Scenes/StPeters.unity#L65022) | Kept | B0 / F1 / L0 | 3 | 260; 0 m |
| [wall.stp_075](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_075.asset#L15) | [L69361](../../Assets/_Project/Scenes/StPeters.unity#L69361) | Kept | B0 / F1 / L0 | 3 | 074; 0 m |
| [wall.stp_076](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_076.asset#L15) | [L45850](../../Assets/_Project/Scenes/StPeters.unity#L45850) | Kept | B0 / F1 / L0 | 3 | 262; 0 m |
| [wall.stp_077](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_077.asset#L15) | [L88892](../../Assets/_Project/Scenes/StPeters.unity#L88892) | Kept | B0 / F1 / L0 | 3 | 263; 0 m |
| [wall.stp_078](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_078.asset#L15) | [L10558](../../Assets/_Project/Scenes/StPeters.unity#L10558) | Kept | B0 / F1 / L0 | 2 | 077; 0 m |
| [wall.stp_147](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_147.asset#L15) | [L114723](../../Assets/_Project/Scenes/StPeters.unity#L114723) | Relined | B0 / F1 / L0 | 1 | 230; 0 m |
| [wall.stp_148](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_148.asset#L15) | [L114821](../../Assets/_Project/Scenes/StPeters.unity#L114821) | Relined | B0 / F1 / L0 | 1 | 267; 0 m |
| [wall.stp_149](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_149.asset#L15) | [L114919](../../Assets/_Project/Scenes/StPeters.unity#L114919) | Relined | B0 / F1 / L0 | 1 | 148; 0 m |
| [wall.stp_150](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_150.asset#L15) | [L115017](../../Assets/_Project/Scenes/StPeters.unity#L115017) | Relined | B0 / F1 / L0 | 1 | 030; 0 m |
| [wall.stp_151](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_151.asset#L15) | [L115115](../../Assets/_Project/Scenes/StPeters.unity#L115115) | Relined | B0 / F1 / L0 | 2 | 232; 0 m |
| [wall.stp_152](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_152.asset#L15) | [L115217](../../Assets/_Project/Scenes/StPeters.unity#L115217) | Relined | B0 / F1 / L0 | 2 | 234; 0 m |
| [wall.stp_153](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_153.asset#L15) | [L115319](../../Assets/_Project/Scenes/StPeters.unity#L115319) | Relined | B0 / F1 / L0 | 2 | 268; 0 m |
| [wall.stp_154](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_154.asset#L15) | [L115431](../../Assets/_Project/Scenes/StPeters.unity#L115431) | Relined | B0 / F1 / L0 | 3 | Start |
| [wall.stp_155](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_155.asset#L15) | [L115542](../../Assets/_Project/Scenes/StPeters.unity#L115542) | Relined | B0 / F1 / L0 | 6 | 236; 0 m |
| [wall.stp_156](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_156.asset#L15) | [L115660](../../Assets/_Project/Scenes/StPeters.unity#L115660) | Relined | B0 / F1 / L0 | 3 | 155; 0 m |
| [wall.stp_157](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_157.asset#L15) | [L115766](../../Assets/_Project/Scenes/StPeters.unity#L115766) | Relined | B0 / F1 / L0 | 5 | 034; 0 m |
| [wall.stp_158](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_158.asset#L15) | [L115880](../../Assets/_Project/Scenes/StPeters.unity#L115880) | Relined | B0 / F1 / L0 | 4 | 239; 0 m |
| [wall.stp_159](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_159.asset#L15) | [L116002](../../Assets/_Project/Scenes/StPeters.unity#L116002) | Relined | B0 / F1 / L0 | 5 | 271; 0 m |
| [wall.stp_160](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_160.asset#L15) | [L116129](../../Assets/_Project/Scenes/StPeters.unity#L116129) | Relined | B0 / F1 / L0 | 3 | 272; 0 m |
| [wall.stp_161](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_161.asset#L15) | [L116235](../../Assets/_Project/Scenes/StPeters.unity#L116235) | Relined | B0 / F1 / L0 | 2 | 160; 0 m |
| [wall.stp_162](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_162.asset#L15) | [L116337](../../Assets/_Project/Scenes/StPeters.unity#L116337) | Relined | B0 / F1 / L0 | 5 | 161; 0 m |
| [wall.stp_163](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_163.asset#L15) | [L116458](../../Assets/_Project/Scenes/StPeters.unity#L116458) | Relined | B0 / F1 / L0 | 5 | 162; 0 m |
| [wall.stp_164](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_164.asset#L15) | [L116572](../../Assets/_Project/Scenes/StPeters.unity#L116572) | Relined | B0 / F1 / L0 | 2 | 163; 0 m |
| [wall.stp_165](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_165.asset#L15) | [L116678](../../Assets/_Project/Scenes/StPeters.unity#L116678) | Relined | B0 / F1 / L0 | 5 | 164; 0 m |
| [wall.stp_166](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_166.asset#L15) | [L116799](../../Assets/_Project/Scenes/StPeters.unity#L116799) | Relined | B0 / F1 / L0 | 1 | 038; 0 m |
| [wall.stp_167](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_167.asset#L15) | [L116897](../../Assets/_Project/Scenes/StPeters.unity#L116897) | Banked | B0 / F1 / L0 | 1 | 056; 0 m |
| [wall.stp_168](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_168.asset#L15) | [L116995](../../Assets/_Project/Scenes/StPeters.unity#L116995) | Banked | B0 / F1 / L0 | 2 | 274; 0 m |
| [wall.stp_169](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_169.asset#L15) | [L117113](../../Assets/_Project/Scenes/StPeters.unity#L117113) | Banked | B0 / F1 / L0 | 3 | 277; 0 m |
| [wall.stp_170](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_170.asset#L15) | [L117219](../../Assets/_Project/Scenes/StPeters.unity#L117219) | Relined | B0 / F1 / L0 | 1 | 245; 0 m |
| [wall.stp_171](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_171.asset#L15) | [L117317](../../Assets/_Project/Scenes/StPeters.unity#L117317) | Relined | B0 / F1 / L0 | 5 | 170; 0 m |
| [wall.stp_172](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_172.asset#L15) | [L117438](../../Assets/_Project/Scenes/StPeters.unity#L117438) | Relined | B0 / F1 / L0 | 2 | 171; 0 m |
| [wall.stp_173](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_173.asset#L15) | [L117550](../../Assets/_Project/Scenes/StPeters.unity#L117550) | Relined | B0 / F1 / L0 | 1 | 061; 0 m |
| [wall.stp_174](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_174.asset#L15) | [L117648](../../Assets/_Project/Scenes/StPeters.unity#L117648) | Relined | B0 / F1 / L0 | 3 | 062; 0 m |
| [wall.stp_175](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_175.asset#L15) | [L117754](../../Assets/_Project/Scenes/StPeters.unity#L117754) | Relined | B0 / F1 / L0 | 3 | 174; 0 m |
| [wall.stp_176](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_176.asset#L15) | [L117865](../../Assets/_Project/Scenes/StPeters.unity#L117865) | Relined | B0 / F1 / L0 | 1 | 175; 0 m |
| [wall.stp_177](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_177.asset#L15) | [L117963](../../Assets/_Project/Scenes/StPeters.unity#L117963) | Relined | B0 / F1 / L0 | 2 | 176; 0 m |
| [wall.stp_178](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_178.asset#L15) | [L118065](../../Assets/_Project/Scenes/StPeters.unity#L118065) | Relined | B0 / F1 / L0 | 2 | 279; 0 m |
| [wall.stp_179](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_179.asset#L15) | [L118177](../../Assets/_Project/Scenes/StPeters.unity#L118177) | Relined | B0 / F1 / L0 | 2 | 280; 0 m |
| [wall.stp_180](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_180.asset#L15) | [L118295](../../Assets/_Project/Scenes/StPeters.unity#L118295) | Relined | B0 / F1 / L0 | 2 | 284; 0 m |
| [wall.stp_181](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_181.asset#L15) | [L118407](../../Assets/_Project/Scenes/StPeters.unity#L118407) | Relined | B0 / F1 / L0 | 3 | 285; 0 m |
| [wall.stp_182](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_182.asset#L15) | [L118518](../../Assets/_Project/Scenes/StPeters.unity#L118518) | Relined | B0 / F1 / L0 | 2 | 286; 0 m |
| [wall.stp_183](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_183.asset#L15) | [L118630](../../Assets/_Project/Scenes/StPeters.unity#L118630) | Relined | B0 / F1 / L0 | 2 | 182; 0 m |
| [wall.stp_184](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_184.asset#L15) | [L118742](../../Assets/_Project/Scenes/StPeters.unity#L118742) | Relined | B0 / F1 / L0 | 2 | 287; 0 m |
| [wall.stp_185](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_185.asset#L15) | [L118854](../../Assets/_Project/Scenes/StPeters.unity#L118854) | Relined | B0 / F1 / L0 | 1 | 251; 0 m |
| [wall.stp_186](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_186.asset#L15) | [L118952](../../Assets/_Project/Scenes/StPeters.unity#L118952) | Relined | B0 / F1 / L0 | 1 | 185; 0 m |
| [wall.stp_187](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_187.asset#L15) | [L119050](../../Assets/_Project/Scenes/StPeters.unity#L119050) | Relined | B0 / F1 / L0 | 2 | 186; 0 m |
| [wall.stp_188](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_188.asset#L15) | [L119152](../../Assets/_Project/Scenes/StPeters.unity#L119152) | Relined | B0 / F1 / L0 | 2 | 289; 0 m |
| [wall.stp_189](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_189.asset#L15) | [L119264](../../Assets/_Project/Scenes/StPeters.unity#L119264) | Kept | B0 / F1 / L0 | 3 | Start |
| [wall.stp_190](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_190.asset#L15) | [L119375](../../Assets/_Project/Scenes/StPeters.unity#L119375) | Kept | B0 / F1 / L0 | 1 | 000; 0 m |
| [wall.stp_191](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_191.asset#L15) | [L119473](../../Assets/_Project/Scenes/StPeters.unity#L119473) | Kept | B0 / F1 / L0 | 2 | 001; 0 m |
| [wall.stp_192](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_192.asset#L15) | [L119579](../../Assets/_Project/Scenes/StPeters.unity#L119579) | Kept | B0 / F1 / L0 | 1 | 002; 0 m |
| [wall.stp_193](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_193.asset#L15) | [L119686](../../Assets/_Project/Scenes/StPeters.unity#L119686) | Kept | B0 / F1 / L0 | 3 | 192; 0 m |
| [wall.stp_194](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_194.asset#L15) | [L119803](../../Assets/_Project/Scenes/StPeters.unity#L119803) | Kept | B0 / F1 / L0 | 3 | 003; 0 m |
| [wall.stp_195](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_195.asset#L15) | [L119920](../../Assets/_Project/Scenes/StPeters.unity#L119920) | Kept | B0 / F1 / L0 | 1 | 004; 0 m |
| [wall.stp_196](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_196.asset#L15) | [L120018](../../Assets/_Project/Scenes/StPeters.unity#L120018) | Kept | B0 / F1 / L0 | 3 | 195; 0 m |
| [wall.stp_197](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_197.asset#L15) | [L120124](../../Assets/_Project/Scenes/StPeters.unity#L120124) | Kept | B0 / F1 / L0 | 1 | 005; 0 m |
| [wall.stp_198](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_198.asset#L15) | [L120222](../../Assets/_Project/Scenes/StPeters.unity#L120222) | Kept | B0 / F1 / L0 | 3 | 197; 0 m |
| [wall.stp_199](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_199.asset#L15) | [L120328](../../Assets/_Project/Scenes/StPeters.unity#L120328) | Kept | B0 / F1 / L0 | 1 | 006; 0 m |
| [wall.stp_200](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_200.asset#L15) | [L120426](../../Assets/_Project/Scenes/StPeters.unity#L120426) | Kept | B0 / F1 / L0 | 4 | 199; 0 m |
| [wall.stp_201](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_201.asset#L15) | [L120536](../../Assets/_Project/Scenes/StPeters.unity#L120536) | Kept | B0 / F1 / L0 | 4 | 008; 0 m |
| [wall.stp_202](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_202.asset#L15) | [L120646](../../Assets/_Project/Scenes/StPeters.unity#L120646) | Kept | B0 / F1 / L0 | 4 | 009; 0 m |
| [wall.stp_203](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_203.asset#L15) | [L120756](../../Assets/_Project/Scenes/StPeters.unity#L120756) | Kept | B0 / F1 / L0 | 4 | 010; 0 m |
| [wall.stp_204](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_204.asset#L15) | [L120866](../../Assets/_Project/Scenes/StPeters.unity#L120866) | Kept | B0 / F1 / L0 | 4 | 011; 0 m |
| [wall.stp_205](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_205.asset#L15) | [L120976](../../Assets/_Project/Scenes/StPeters.unity#L120976) | Kept | B0 / F1 / L0 | 4 | 013; 0 m |
| [wall.stp_206](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_206.asset#L15) | [L121086](../../Assets/_Project/Scenes/StPeters.unity#L121086) | Kept | B0 / F1 / L0 | 1 | 205; 0 m |
| [wall.stp_207](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_207.asset#L15) | [L121184](../../Assets/_Project/Scenes/StPeters.unity#L121184) | Kept | B0 / F1 / L0 | 4 | 014; 0 m |
| [wall.stp_208](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_208.asset#L15) | [L121294](../../Assets/_Project/Scenes/StPeters.unity#L121294) | Kept | B0 / F1 / L0 | 1 | 207; 0 m |
| [wall.stp_209](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_209.asset#L15) | [L121392](../../Assets/_Project/Scenes/StPeters.unity#L121392) | Kept | B0 / F1 / L0 | 4 | 208; 0 m |
| [wall.stp_210](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_210.asset#L15) | [L121502](../../Assets/_Project/Scenes/StPeters.unity#L121502) | Kept | B0 / F1 / L0 | 1 | 015; 0 m |
| [wall.stp_211](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_211.asset#L15) | [L121600](../../Assets/_Project/Scenes/StPeters.unity#L121600) | Kept | B0 / F1 / L0 | 4 | 210; 0 m |
| [wall.stp_212](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_212.asset#L15) | [L121710](../../Assets/_Project/Scenes/StPeters.unity#L121710) | Kept | B0 / F1 / L0 | 1 | 016; 0 m |
| [wall.stp_213](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_213.asset#L15) | [L121808](../../Assets/_Project/Scenes/StPeters.unity#L121808) | Kept | B0 / F1 / L0 | 5 | 017; 0 m |
| [wall.stp_214](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_214.asset#L15) | [L121922](../../Assets/_Project/Scenes/StPeters.unity#L121922) | Kept | B0 / F1 / L0 | 5 | 018; 0 m |
| [wall.stp_215](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_215.asset#L15) | [L122036](../../Assets/_Project/Scenes/StPeters.unity#L122036) | Kept | B0 / F1 / L0 | 5 | 019; 0 m |
| [wall.stp_216](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_216.asset#L15) | [L122150](../../Assets/_Project/Scenes/StPeters.unity#L122150) | Kept | B0 / F1 / L0 | 1 | 215; 0 m |
| [wall.stp_217](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_217.asset#L15) | [L122248](../../Assets/_Project/Scenes/StPeters.unity#L122248) | Kept | B0 / F1 / L0 | 5 | 020; 0 m |
| [wall.stp_218](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_218.asset#L15) | [L122362](../../Assets/_Project/Scenes/StPeters.unity#L122362) | Kept | B0 / F1 / L0 | 1 | 217; 0 m |
| [wall.stp_219](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_219.asset#L15) | [L122460](../../Assets/_Project/Scenes/StPeters.unity#L122460) | Kept | B0 / F1 / L0 | 6 | 218; 0 m |
| [wall.stp_220](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_220.asset#L15) | [L122578](../../Assets/_Project/Scenes/StPeters.unity#L122578) | Kept | B0 / F1 / L0 | 1 | 021; 0 m |
| [wall.stp_221](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_221.asset#L15) | [L122676](../../Assets/_Project/Scenes/StPeters.unity#L122676) | Kept | B0 / F1 / L0 | 3 | Start |
| [wall.stp_222](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_222.asset#L15) | [L122782](../../Assets/_Project/Scenes/StPeters.unity#L122782) | Kept | B0 / F1 / L0 | 1 | 023; 0 m |
| [wall.stp_223](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_223.asset#L15) | [L122880](../../Assets/_Project/Scenes/StPeters.unity#L122880) | Kept | B0 / F1 / L0 | 2 | 024; 0 m |
| [wall.stp_224](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_224.asset#L15) | [L122982](../../Assets/_Project/Scenes/StPeters.unity#L122982) | Kept | B0 / F1 / L0 | 3 | 223; 0 m |
| [wall.stp_225](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_225.asset#L15) | [L123088](../../Assets/_Project/Scenes/StPeters.unity#L123088) | Kept | B0 / F1 / L0 | 3 | 025; 0 m |
| [wall.stp_226](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_226.asset#L15) | [L123194](../../Assets/_Project/Scenes/StPeters.unity#L123194) | Kept | B0 / F1 / L0 | 2 | 026; 0 m |
| [wall.stp_227](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_227.asset#L15) | [L123296](../../Assets/_Project/Scenes/StPeters.unity#L123296) | Kept | B0 / F1 / L0 | 8 | 226; 0 m |
| [wall.stp_228](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_228.asset#L15) | [L123422](../../Assets/_Project/Scenes/StPeters.unity#L123422) | Kept | B0 / F1 / L0 | 1 | 027; 0 m |
| [wall.stp_229](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_229.asset#L15) | [L123520](../../Assets/_Project/Scenes/StPeters.unity#L123520) | Relined | B0 / F1 / L0 | 5 | 028; 0 m |
| [wall.stp_230](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_230.asset#L15) | [L123634](../../Assets/_Project/Scenes/StPeters.unity#L123634) | Relined | B0 / F1 / L0 | 1 | 229; 0 m |
| [wall.stp_231](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_231.asset#L15) | [L123944](../../Assets/_Project/Scenes/StPeters.unity#L123944) | Relined | B0 / F1 / L0 | 8 | 150; 0 m |
| [wall.stp_232](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_232.asset#L15) | [L124070](../../Assets/_Project/Scenes/StPeters.unity#L124070) | Relined | B0 / F1 / L0 | 4 | 031; 0 m |
| [wall.stp_233](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_233.asset#L15) | [L124180](../../Assets/_Project/Scenes/StPeters.unity#L124180) | Relined | B0 / F1 / L0 | 5 | 151; 0 m |
| [wall.stp_234](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_234.asset#L15) | [L124301](../../Assets/_Project/Scenes/StPeters.unity#L124301) | Relined | B0 / F1 / L0 | 3 | 032; 0 m |
| [wall.stp_235](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_235.asset#L15) | [L124631](../../Assets/_Project/Scenes/StPeters.unity#L124631) | Relined | B0 / F1 / L0 | 5 | 033; 0 m |
| [wall.stp_236](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_236.asset#L15) | [L124745](../../Assets/_Project/Scenes/StPeters.unity#L124745) | Relined | B0 / F1 / L0 | 1 | 235; 0 m |
| [wall.stp_237](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_237.asset#L15) | [L124843](../../Assets/_Project/Scenes/StPeters.unity#L124843) | Relined | B0 / F1 / L0 | 8 | 156; 0 m |
| [wall.stp_238](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_238.asset#L15) | [L124979](../../Assets/_Project/Scenes/StPeters.unity#L124979) | Relined | B0 / F1 / L0 | 5 | 157; 0 m |
| [wall.stp_239](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_239.asset#L15) | [L125093](../../Assets/_Project/Scenes/StPeters.unity#L125093) | Relined | B0 / F1 / L0 | 2 | 035; 0 m |
| [wall.stp_240](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_240.asset#L15) | [L125317](../../Assets/_Project/Scenes/StPeters.unity#L125317) | Relined | B0 / F1 / L0 | 1 | 036; 0 m |
| [wall.stp_241](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_241.asset#L15) | [L126206](../../Assets/_Project/Scenes/StPeters.unity#L126206) | Banked | B0 / F1 / L0 | 6 | 057; 0 m |
| [wall.stp_242](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_242.asset#L15) | [L126324](../../Assets/_Project/Scenes/StPeters.unity#L126324) | Kept | B0 / F1 / L0 | 8 | 058; 0 m |
| [wall.stp_243](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_243.asset#L15) | [L126450](../../Assets/_Project/Scenes/StPeters.unity#L126450) | Kept | B0 / F1 / L0 | 1 | 242; 0 m |
| [wall.stp_244](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_244.asset#L15) | [L126548](../../Assets/_Project/Scenes/StPeters.unity#L126548) | Relined | B0 / F1 / L0 | 1 | 059; 0 m |
| [wall.stp_245](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_245.asset#L15) | [L126646](../../Assets/_Project/Scenes/StPeters.unity#L126646) | Relined | B0 / F1 / L0 | 4 | 060; 0 m |
| [wall.stp_246](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_246.asset#L15) | [L126863](../../Assets/_Project/Scenes/StPeters.unity#L126863) | Relined | B0 / F1 / L0 | 1 | 063; 0 m |
| [wall.stp_247](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_247.asset#L15) | [L127082](../../Assets/_Project/Scenes/StPeters.unity#L127082) | Relined | B0 / F1 / L0 | 2 | 064; 0 m |
| [wall.stp_248](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_248.asset#L15) | [L128204](../../Assets/_Project/Scenes/StPeters.unity#L128204) | Relined | B0 / F1 / L0 | 2 | 288; 0 m |
| [wall.stp_249](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_249.asset#L15) | [L128316](../../Assets/_Project/Scenes/StPeters.unity#L128316) | Relined | B0 / F1 / L0 | 2 | 066; 0 m |
| [wall.stp_250](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_250.asset#L15) | [L128418](../../Assets/_Project/Scenes/StPeters.unity#L128418) | Relined | B0 / F1 / L0 | 1 | 249; 0 m |
| [wall.stp_251](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_251.asset#L15) | [L128516](../../Assets/_Project/Scenes/StPeters.unity#L128516) | Relined | B0 / F1 / L0 | 4 | 067; 0 m |
| [wall.stp_252](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_252.asset#L15) | [L128738](../../Assets/_Project/Scenes/StPeters.unity#L128738) | Toe lifted | B0 / F1 / L0 | 6 | 068; 0 m |
| [wall.stp_253](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_253.asset#L15) | [L128856](../../Assets/_Project/Scenes/StPeters.unity#L128856) | Toe lifted | B0 / F1 / L0 | 5 | 069; 0 m |
| [wall.stp_254](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_254.asset#L15) | [L128970](../../Assets/_Project/Scenes/StPeters.unity#L128970) | Toe lifted | B0 / F1 / L0 | 3 | 070; 0 m |
| [wall.stp_255](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_255.asset#L15) | [L129076](../../Assets/_Project/Scenes/StPeters.unity#L129076) | Toe lifted | B0 / F1 / L0 | 2 | 254; 0 m |
| [wall.stp_256](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_256.asset#L15) | [L129178](../../Assets/_Project/Scenes/StPeters.unity#L129178) | Toe lifted | B0 / F1 / L0 | 4 | 071; 0 m |
| [wall.stp_257](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_257.asset#L15) | [L129288](../../Assets/_Project/Scenes/StPeters.unity#L129288) | Kept | B0 / F1 / L0 | 4 | 256; 0 m |
| [wall.stp_258](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_258.asset#L15) | [L129398](../../Assets/_Project/Scenes/StPeters.unity#L129398) | Kept | B0 / F1 / L0 | 1 | 072; 0 m |
| [wall.stp_259](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_259.asset#L15) | [L129496](../../Assets/_Project/Scenes/StPeters.unity#L129496) | Kept | B0 / F1 / L0 | 2 | 073; 0 m |
| [wall.stp_260](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_260.asset#L15) | [L129598](../../Assets/_Project/Scenes/StPeters.unity#L129598) | Kept | B0 / F1 / L0 | 3 | 259; 0 m |
| [wall.stp_261](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_261.asset#L15) | [L129715](../../Assets/_Project/Scenes/StPeters.unity#L129715) | Kept | B0 / F1 / L0 | 1 | 075; 0 m |
| [wall.stp_262](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_262.asset#L15) | [L129822](../../Assets/_Project/Scenes/StPeters.unity#L129822) | Kept | B0 / F1 / L0 | 3 | 261; 0 m |
| [wall.stp_263](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_263.asset#L15) | [L129939](../../Assets/_Project/Scenes/StPeters.unity#L129939) | Kept | B0 / F1 / L0 | 2 | 076; 0 m |
| [wall.stp_264](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_264.asset#L15) | [L130041](../../Assets/_Project/Scenes/StPeters.unity#L130041) | Kept | B0 / F1 / L0 | 2 | 078; 0 m |
| [wall.stp_265](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_265.asset#L15) | [L130153](../../Assets/_Project/Scenes/StPeters.unity#L130153) | Kept | B0 / F1 / L0 | 1 | 264; 0 m |
| [wall.stp_266](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_266.asset#L15) | [L123732](../../Assets/_Project/Scenes/StPeters.unity#L123732) | Relined | B0 / F1 / L0 | 2 | 147; 0 m |
| [wall.stp_267](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_267.asset#L15) | [L123838](../../Assets/_Project/Scenes/StPeters.unity#L123838) | Relined | B0 / F1 / L0 | 2 | 266; 0 m |
| [wall.stp_268](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_268.asset#L15) | [L124418](../../Assets/_Project/Scenes/StPeters.unity#L124418) | Relined | B0 / F1 / L0 | 2 | 152; 0 m |
| [wall.stp_269](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_269.asset#L15) | [L124524](../../Assets/_Project/Scenes/StPeters.unity#L124524) | Relined | B0 / F1 / L0 | 1 | 153; 0 m |
| [wall.stp_270](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_270.asset#L15) | [L125195](../../Assets/_Project/Scenes/StPeters.unity#L125195) | Relined | B0 / F1 / L0 | 4 | 158; 0 m |
| [wall.stp_271](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_271.asset#L15) | [L125415](../../Assets/_Project/Scenes/StPeters.unity#L125415) | Relined | B0 / F1 / L0 | 3 | 240; 0 m |
| [wall.stp_272](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_272.asset#L15) | [L125521](../../Assets/_Project/Scenes/StPeters.unity#L125521) | Relined | B0 / F1 / L0 | 2 | 159; 0 m |
| [wall.stp_273](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_273.asset#L15) | [L125633](../../Assets/_Project/Scenes/StPeters.unity#L125633) | Banked | B0 / F1 / L0 | 2 | 167; 0 m |
| [wall.stp_274](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_274.asset#L15) | [L125745](../../Assets/_Project/Scenes/StPeters.unity#L125745) | Banked | B0 / F1 / L0 | 2 | 273; 0 m |
| [wall.stp_275](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_275.asset#L15) | [L125863](../../Assets/_Project/Scenes/StPeters.unity#L125863) | Banked | B0 / F1 / L0 | 2 | 168; 0 m |
| [wall.stp_276](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_276.asset#L15) | [L125981](../../Assets/_Project/Scenes/StPeters.unity#L125981) | Banked | B0 / F1 / L0 | 2 | 275; 0 m |
| [wall.stp_277](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_277.asset#L15) | [L126099](../../Assets/_Project/Scenes/StPeters.unity#L126099) | Banked | B0 / F1 / L0 | 1 | 276; 0 m |
| [wall.stp_278](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_278.asset#L15) | [L126756](../../Assets/_Project/Scenes/StPeters.unity#L126756) | Relined | B0 / F1 / L0 | 1 | 172; 0 m |
| [wall.stp_279](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_279.asset#L15) | [L126970](../../Assets/_Project/Scenes/StPeters.unity#L126970) | Relined | B0 / F1 / L0 | 2 | 246; 0 m |
| [wall.stp_280](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_280.asset#L15) | [L127188](../../Assets/_Project/Scenes/StPeters.unity#L127188) | Relined | B0 / F1 / L0 | 2 | 247; 0 m |
| [wall.stp_281](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_281.asset#L15) | [L127300](../../Assets/_Project/Scenes/StPeters.unity#L127300) | Relined | B0 / F1 / L0 | 2 | 179; 0 m |
| [wall.stp_282](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_282.asset#L15) | [L127418](../../Assets/_Project/Scenes/StPeters.unity#L127418) | Relined | B0 / F1 / L0 | 2 | 281; 0 m |
| [wall.stp_283](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_283.asset#L15) | [L127536](../../Assets/_Project/Scenes/StPeters.unity#L127536) | Relined | B0 / F1 / L0 | 2 | 282; 0 m |
| [wall.stp_284](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_284.asset#L15) | [L127654](../../Assets/_Project/Scenes/StPeters.unity#L127654) | Relined | B0 / F1 / L0 | 2 | 283; 0 m |
| [wall.stp_285](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_285.asset#L15) | [L127766](../../Assets/_Project/Scenes/StPeters.unity#L127766) | Relined | B0 / F1 / L0 | 1 | 180; 0 m |
| [wall.stp_286](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_286.asset#L15) | [L127873](../../Assets/_Project/Scenes/StPeters.unity#L127873) | Relined | B0 / F1 / L0 | 2 | 181; 0 m |
| [wall.stp_287](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_287.asset#L15) | [L127985](../../Assets/_Project/Scenes/StPeters.unity#L127985) | Relined | B0 / F1 / L0 | 2 | 183; 0 m |
| [wall.stp_288](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_288.asset#L15) | [L128097](../../Assets/_Project/Scenes/StPeters.unity#L128097) | Relined | B0 / F1 / L0 | 1 | 184; 0 m |
| [wall.stp_289](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_289.asset#L15) | [L128626](../../Assets/_Project/Scenes/StPeters.unity#L128626) | Relined | B0 / F1 / L0 | 2 | 187; 0 m |

### Nine Mile Creek

| Wall ID / Def | Scene surface | Status | Brow / foot / layer | Foot quads | Follows; join gap |
|---|---|---|---|---:|---|
| [wall.nmc_000](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_000.asset#L15) | [L239736](../../Assets/_Project/Scenes/NineMileCreek.unity#L239736) | Kept | B0 / F1 / L0 | 16 | Start |
| [wall.nmc_001](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_001.asset#L15) | [L235641](../../Assets/_Project/Scenes/NineMileCreek.unity#L235641) | Kept | B0 / F1 / L0 | 10 | 000; 0 m |
| [wall.nmc_002](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_002.asset#L15) | [L49565](../../Assets/_Project/Scenes/NineMileCreek.unity#L49565) | Kept | B0 / F1 / L0 | 8 | 001; 0 m |
| [wall.nmc_003](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_003.asset#L15) | [L186568](../../Assets/_Project/Scenes/NineMileCreek.unity#L186568) | Kept | B0 / F1 / L0 | 8 | 002; 0 m |
| [wall.nmc_004](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_004.asset#L15) | [L293808](../../Assets/_Project/Scenes/NineMileCreek.unity#L293808) | Kept | B0 / F1 / L0 | 8 | 003; 0 m |
| [wall.nmc_005](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_005.asset#L15) | [L41814](../../Assets/_Project/Scenes/NineMileCreek.unity#L41814) | Kept | B0 / F1 / L0 | 8 | 004; 0 m |
| [wall.nmc_006](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_006.asset#L15) | [L149324](../../Assets/_Project/Scenes/NineMileCreek.unity#L149324) | Kept | B0 / F1 / L0 | 8 | 005; 0 m |
| [wall.nmc_007](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_007.asset#L15) | [L268297](../../Assets/_Project/Scenes/NineMileCreek.unity#L268297) | Kept | B0 / F1 / L0 | 8 | 006; 0 m |
| [wall.nmc_008](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_008.asset#L15) | [L154689](../../Assets/_Project/Scenes/NineMileCreek.unity#L154689) | Kept | B0 / F1 / L0 | 8 | 007; 0 m |
| [wall.nmc_009](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_009.asset#L15) | [L60979](../../Assets/_Project/Scenes/NineMileCreek.unity#L60979) | Kept | B0 / F1 / L0 | 8 | 008; 0 m |
| [wall.nmc_010](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_010.asset#L15) | [L278586](../../Assets/_Project/Scenes/NineMileCreek.unity#L278586) | Kept | B0 / F1 / L0 | 8 | 009; 0 m |
| [wall.nmc_011](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_011.asset#L15) | [L17891](../../Assets/_Project/Scenes/NineMileCreek.unity#L17891) | Kept | B0 / F1 / L0 | 8 | 010; 0 m |
| [wall.nmc_012](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_012.asset#L15) | [L332333](../../Assets/_Project/Scenes/NineMileCreek.unity#L332333) | Kept | B0 / F1 / L0 | 8 | 011; 0 m |
| [wall.nmc_013](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_013.asset#L15) | [L44289](../../Assets/_Project/Scenes/NineMileCreek.unity#L44289) | Kept | B0 / F1 / L0 | 8 | 012; 0 m |
| [wall.nmc_014](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_014.asset#L15) | [L69959](../../Assets/_Project/Scenes/NineMileCreek.unity#L69959) | Kept | B0 / F1 / L0 | 8 | 013; 0 m |
| [wall.nmc_015](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_015.asset#L15) | [L229399](../../Assets/_Project/Scenes/NineMileCreek.unity#L229399) | Kept | B0 / F1 / L0 | 8 | 014; 0 m |
| [wall.nmc_016](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_016.asset#L15) | [L87795](../../Assets/_Project/Scenes/NineMileCreek.unity#L87795) | Kept | B0 / F1 / L0 | 3 | 015; 0 m |
| [wall.nmc_017](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_017.asset#L15) | [L290667](../../Assets/_Project/Scenes/NineMileCreek.unity#L290667) | Toe lifted | B0 / F1 / L0 | 8 | Start |
| [wall.nmc_018](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_018.asset#L15) | [L164468](../../Assets/_Project/Scenes/NineMileCreek.unity#L164468) | Kept | B0 / F1 / L0 | 8 | 017; 0 m |
| [wall.nmc_019](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_019.asset#L15) | [L102222](../../Assets/_Project/Scenes/NineMileCreek.unity#L102222) | Kept | B0 / F1 / L0 | 8 | 018; 0 m |
| [wall.nmc_020](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_020.asset#L15) | [L115253](../../Assets/_Project/Scenes/NineMileCreek.unity#L115253) | Kept | B0 / F1 / L0 | 8 | 019; 0 m |
| [wall.nmc_021](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_021.asset#L15) | [L33830](../../Assets/_Project/Scenes/NineMileCreek.unity#L33830) | Toe lifted | B0 / F1 / L0 | 5 | 020; 0 m |
| [wall.nmc_022](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_022.asset#L15) | [L366342](../../Assets/_Project/Scenes/NineMileCreek.unity#L366342) | Toe lifted | B0 / F1 / L0 | 4 | 086; 0 m |
| [wall.nmc_023](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_023.asset#L15) | [L18269](../../Assets/_Project/Scenes/NineMileCreek.unity#L18269) | Relined | B0 / F1 / L0 | 4 | 087; 0 m |
| [wall.nmc_024](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_024.asset#L15) | [L75644](../../Assets/_Project/Scenes/NineMileCreek.unity#L75644) | Banked | B0 / F1 / L0 | 6 | 088; 0 m |
| [wall.nmc_025](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_025.asset#L15) | [L329261](../../Assets/_Project/Scenes/NineMileCreek.unity#L329261) | Banked | B0 / F1 / L0 | 5 | 024; 0 m |
| [wall.nmc_026](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_026.asset#L15) | [L164098](../../Assets/_Project/Scenes/NineMileCreek.unity#L164098) | Banked | B0 / F1 / L0 | 5 | 025; 0 m |
| [wall.nmc_027](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_027.asset#L15) | [L51679](../../Assets/_Project/Scenes/NineMileCreek.unity#L51679) | Banked | B0 / F1 / L0 | 5 | 026; 0 m |
| [wall.nmc_028](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_028.asset#L15) | [L44979](../../Assets/_Project/Scenes/NineMileCreek.unity#L44979) | Banked | B0 / F1 / L0 | 3 | 027; 0 m |
| [wall.nmc_029](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_029.asset#L15) | [L297337](../../Assets/_Project/Scenes/NineMileCreek.unity#L297337) | Banked | B0 / F1 / L0 | 9 | Start |
| [wall.nmc_030](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_030.asset#L15) | [L300249](../../Assets/_Project/Scenes/NineMileCreek.unity#L300249) | Banked | B0 / F1 / L0 | 16 | 029; 0 m |
| [wall.nmc_031](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_031.asset#L15) | [L100052](../../Assets/_Project/Scenes/NineMileCreek.unity#L100052) | Banked | B0 / F1 / L0 | 1 | 030; 0 m |
| [wall.nmc_032](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_032.asset#L15) | [L60096](../../Assets/_Project/Scenes/NineMileCreek.unity#L60096) | Banked | B0 / F1 / L0 | 12 | 031; 0 m |
| [wall.nmc_033](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_033.asset#L15) | [L325326](../../Assets/_Project/Scenes/NineMileCreek.unity#L325326) | Banked | B0 / F1 / L0 | 8 | 032; 0 m |
| [wall.nmc_034](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_034.asset#L15) | [L125276](../../Assets/_Project/Scenes/NineMileCreek.unity#L125276) | Banked | B0 / F1 / L0 | 8 | 033; 0 m |
| [wall.nmc_035](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_035.asset#L15) | [L181996](../../Assets/_Project/Scenes/NineMileCreek.unity#L181996) | Banked | B0 / F1 / L0 | 4 | 034; 0 m |
| [wall.nmc_036](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_036.asset#L15) | [L19375](../../Assets/_Project/Scenes/NineMileCreek.unity#L19375) | Banked | B0 / F1 / L0 | 8 | Start |
| [wall.nmc_037](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_037.asset#L15) | [L3781](../../Assets/_Project/Scenes/NineMileCreek.unity#L3781) | Banked | B0 / F1 / L0 | 8 | 036; 0 m |
| [wall.nmc_038](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_038.asset#L15) | [L59058](../../Assets/_Project/Scenes/NineMileCreek.unity#L59058) | Banked | B0 / F1 / L0 | 8 | 037; 0 m |
| [wall.nmc_039](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_039.asset#L15) | [L231154](../../Assets/_Project/Scenes/NineMileCreek.unity#L231154) | Banked | B0 / F1 / L0 | 8 | 038; 0 m |
| [wall.nmc_040](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_040.asset#L15) | [L197277](../../Assets/_Project/Scenes/NineMileCreek.unity#L197277) | Banked | B0 / F1 / L0 | 8 | 039; 0 m |
| [wall.nmc_041](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_041.asset#L15) | [L93055](../../Assets/_Project/Scenes/NineMileCreek.unity#L93055) | Banked | B0 / F1 / L0 | 8 | 040; 0 m |
| [wall.nmc_042](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_042.asset#L15) | [L363945](../../Assets/_Project/Scenes/NineMileCreek.unity#L363945) | Banked | B0 / F1 / L0 | 8 | 041; 0 m |
| [wall.nmc_043](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_043.asset#L15) | [L76769](../../Assets/_Project/Scenes/NineMileCreek.unity#L76769) | Banked | B0 / F1 / L0 | 8 | 042; 0 m |
| [wall.nmc_044](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_044.asset#L15) | [L273413](../../Assets/_Project/Scenes/NineMileCreek.unity#L273413) | Banked | B0 / F1 / L0 | 8 | 043; 0 m |
| [wall.nmc_045](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_045.asset#L15) | [L297467](../../Assets/_Project/Scenes/NineMileCreek.unity#L297467) | Banked | B0 / F1 / L0 | 3 | 044; 0 m |
| [wall.nmc_086](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_086.asset#L15) | [L366452](../../Assets/_Project/Scenes/NineMileCreek.unity#L366452) | Toe lifted | B0 / F1 / L0 | 3 | 021; 0 m |
| [wall.nmc_087](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_087.asset#L15) | [L366558](../../Assets/_Project/Scenes/NineMileCreek.unity#L366558) | Toe lifted | B0 / F1 / L0 | 4 | 022; 0 m |
| [wall.nmc_088](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_088.asset#L15) | [L366668](../../Assets/_Project/Scenes/NineMileCreek.unity#L366668) | Banked | B0 / F1 / L0 | 3 | Start |


## Definitions which are not standing walls

These account for the remaining 149 definitions. They were checked for absence from the saved scene, not silently dropped from the census. Their stored station arrays are historical/forecast data and create no collider at runtime without a scene component.

| Region | Definitions | Status | Consequence |
|---|---|---|---|
| St Peters | `044–055` (12) | Retired | Open main beach; [044 reason](../../Assets/_Project/Data/Terrain/StPetersWalls/Wall_044.asset#L162) |
| St Peters | `079–146` (68) | Held | Forecast headland ring; no standing scene walls |
| Nine Mile Creek | `046–085` (40) | Retired | Old face no longer matches the ground; [046 reason](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_046.asset#L101) |
| Nine Mile Creek | `089–117` (29) | Held | Forecast faces, including the south-shore bluff; [089 reason](../../Assets/_Project/Data/Terrain/NineMileCreekWalls/Wall_089.asset#L80) |

Totals: St Peters **290 definitions = 210 live + 12 retired + 68 held**; Nine Mile Creek **118 = 49 + 40 + 29**. Held ground may still have steep elevations; absence of a wall is not a proof that a region is safe to walk.

## Existing coverage and limits of this result

The existing [CliffWallToeBlockerTests](../../Assets/Tests/EditMode/CliffWallToeBlockerTests.cs#L45) already covers both regions' generated toes, layer, thickness, non-trigger shape, joins and rebuild idempotence. It was found before making the measurements; no duplicate validator or new test was added. Its [join loop](../../Assets/Tests/EditMode/CliffWallToeBlockerTests.cs#L167) covers declared runs. It does not establish a brow barrier or a live collision sweep at every wall.

[StPetersMainBeachWalkPlayTests](../../Assets/Tests/PlayMode/StPetersMainBeachWalkPlayTests.cs#L265) has a water-side face leg at the western beach end. That is narrower than every wall, both approach directions and all intended stairs. Neither test class was run for this document, and no earlier pass is claimed as current evidence.

Reproduction of the text measurements: at the stated revision, enumerate tracked scene YAML documents and resolve `m_Script` against `CliffWallSurface.cs.meta`; follow `m_GameObject`/`Transform.m_Father` to check activation and transform scale; read every `CliffWallDef` in both wall directories; compare the four station arrays exactly; project each toe with `cos(40 degrees)`; build the 0.35 m quads with 0.175 m endpoint extensions; compare `Follows` endpoints and distances between neighbouring runs' polygons; read the player body/collider and the collision matrix. This yields 259 walls, 1,230 non-degenerate segments and 250 zero-gap declared joins. Inspect access-path and key-scene records separately so held pieces are not counted as present geometry.

This audit establishes a common missing brow rule and a data-supported toe barrier in both cliff regions. Live component teardown, player mode, contact generation, all-prop route clearance and the original pass-through report remain unverified. Scene, asset, code, configuration and save data are unchanged.
