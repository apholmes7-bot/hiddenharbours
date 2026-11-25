# Nine Mile Creek fish plant: siting proposal

Recommend **A, the south farm parcel centred on (-255, -40)**. It fits a substantial
plant and truck court on existing level ground, connects directly to Route 91,
and keeps industrial traffic out of the plaza's internal aisles. The seat still
chooses the site. These are three alternatives, not three reservations or an
accepted layout. This document changes no scene, asset, road or gameplay.

Audited main: `57f3f26390faf51e33dfc86168dcc5de13da5914`, 2026-11-12.
Coordinates are NMC region-local ground metres: x east, y north, z elevation;
one world unit is one metre. Boxes below mean **[west, east] x [south, north]**.
Building origins, footprint centres and projected sprite positions are distinct.
The November brief establishes a large NMC plant supplied by trucks, serving
groceries, restaurants, hotels and export. It does not move the separate
St Peters cannery or turn the plaza's proposed cannery **lofts** into a factory.
The [supply-chain design](fish-plant-supply-chain.md) on main is identical to
[PR 1127's](https://github.com/apholmes7-bot/hiddenharbours/pull/1127) inspected
head `68b52b868f8db0833a93f220d58a5f9a5bcee5c5`; its routes and live integration
remain unresolved. Player enterprise remains M3, with later freight hooks.

## The region that is actually built

The [RegionDef](../../Assets/_Project/Data/Regions/NineMileCreek.asset) bounds NMC
at x -380..380, y -280..280. Sea is east; farmland is west. The southern shore
turns west toward Rice Point. The bar's shore head is (60, -150); its eastern
transfer arrival is (338, -197.7), not a second wharf. The working harbour is
northeast of the landing, between the made quay and the two breakwater arms.

The committed [scene](../../Assets/_Project/Scenes/NineMileCreek.unity) has G3
painted ground, the road-floor streamer, three bridge instances, and placed
`NmcWharfLandSide`, `NmcWharfBoatYard` and `NmcWharfWaterSide` roots. Bounded scene
reads confirm the office at (86.4, 112.6), fish market at (91.2, 127.6), and
restaurant at (168, 136). The [wharf contract](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset)
is marked `Placed: 1`; the other eleven NMC key-scene contracts are `Placed: 0`.
That flag does not erase shared ground or roads already applied by H2/H4.

H1's [package contract and ownership table](nmc-key-scenes/README.md) is the
forward reservation record. H2/H3/H4's [activation steps](../../Assets/_Project/Code/App/Editor/NineMileCreekGroundSteps.cs)
and [road network](../../Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json)
give the current ground and carriageways. H5/H6's wharf data and
[yard intake](nine-mile-creek-yard-intake.md) describe the working waterfront.
H7 town/bridge/falls, H8 shores/landing, and H9 gas-bar contracts supply future
occupancy. The older [mainland](nine-mile-creek-mainland.md),
[wharf](nine-mile-creek-wharf.md) and [geography](harbour-geography.md) documents
provide context; their earlier road lines, building positions and depth claims
must not override the committed G3 data. In particular, the RegionDef currently
has `IsDeepHarbour: 0`, mean tide 0 and amplitude 2.2 m: a fish plant is not a
promise of all-tide freighter access.

### Wharves, buildings and occupied ground

Sizes are ground footprint or gameplay-envelope dimensions, not image sizes.
The small-building dimensions come from the committed
[wharf metadata](../../Assets/_Project/Art/Sprites/Buildings/NineMileCreek/)
and [set-piece sidecars](../../Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/nmcSetPieces.today.json).
Envelope dimensions include attached structures where the sidecar does.

| Existing feature | Position and occupied extent | Siting consequence |
|---|---|---|
| Fish landing / crane quay | Face y 76, x 50..78; use (64, 76) as the distance datum. Crane origin (72, 78.2). | Working unload frontage must stay available. |
| Fleet quay | Face y 86, x 78..212; player berth x 78.6..93.8. Fingers at x 109.7 and 142.3, y 43.9..71.1. | Existing berths, brows and hand-carry routes occupy the basin edge. |
| Cold store | Origin (64, 99.2), 9.54 x 15.4 m. | Its rig preset is called `fishPlant`; that does not make this small store the proposed industrial business. |
| Harbour office / fish market | Origins (86.4, 112.6) / (91.2, 127.6); envelopes 9.94 x 4.73 / 7.95 x 6.58 m before facing. | Preserve the opening's licence and carried-catch counters. |
| Wharf restaurant | Origin (168, 136); envelope 20.57 x 20.17 m before facing. | Its frontage and truck turn are already occupied. |
| Eight fleet sheds | Five at y 101.8, x 101, 118.8, 133.6, 149.3, 160.3; three at y 131, x 110.5, 118.5, 125.6. Footprints range 3.81..7.4 m wide and 4.95..11 m deep. | These are working premises, not spare factory land. |
| Bait shed / trap store | (136.5, 131.2), 6.3 x 8.75 m / (152, 131.4), 7.18 x 10.55 m. | Retain the north side of the yard road. |
| Boat yard | Gate x 184.4 across y 115..124; yard road continues to (190, 120). Shed origin (199, 131.4), envelope 11.6 x 24.5 m; quay part centred (198, 91), about 28.12 x 10.36 m. | The yard includes lift, slip, hardstands and supported boats; the shed rectangle alone is not its land claim. |
| Truck park / laydown | Centres (-131, 78) / (-95, 62), paved extents 20.1 x 13.4 / 44.8 x 35.9 m. | Useful logistics neighbours, but too small for the common plant site and already assigned. |

The yard dimensions are from its [placed part sidecars](../../Assets/_Project/Art/Sprites/NineMileCreek/Yard/nmcShipyard.today.json).
The [ground plan](../../Assets/_Project/Code/App/Editor/NineMileCreekGroundPlan.cs)
fixes the quay and road joins. Legacy township roots also remain: the parish hall
has its retained lot at (-152, 62), 12 x 18 m; the farmhouse's old position is
(-208, 60), pending H10's move to (-200, -8), with a 7 x 8.7 m lot before facing.
The legacy fuel forecourt is centred (-191, 81.6); H9's proposed store at
(-155, 139) and canopy at (-154, 111) are not placed replacements yet. Neither
old roots nor unplaced replacement lots are released for plant use here.

### Roads and connections

| Corridor in the committed network | Coordinates / width | Truck interpretation |
|---|---|---|
| Route 91, southern trunk | West edge (-380, -142), through (-266, -119), (-206, -82), (-190, -40), (-181, 10), junction (-177.45, 92), to bridge south seat near (-176.7, 187); 5.8 m. | Main local freight approach; all three candidates branch south of the town river. |
| Route 91 through town | Bridge near (-176.8, 205), then (-181, 242) to (-186, 281); carriageway 5.8 m, bridge envelope 9.5 m wide. | Northbound geography exists; a cross-region truck passage is not established. |
| Wharf Road and yard road | Junction (-177.45, 92) east to (14, 92), then (40, 118); kept stub to (50, 118), turning circle centred (62, 125), radius 15 m; yard road y 120 from x 76 to 190, width 7.5 m. Wharf Road width 5.8 m. | Existing local road connection to wharf parking; retain turning circle and hand-work frontage. |
| Mill Road | (-177.3, 158) to (-282, 170), width 5 m. | Candidate B's approach; the falls footpath starts at its western end. |
| Plaza drives | Entrance (-177.3, 92) to (-193.5, 92), width 9 m; east aisle x -193.5, width 7 m; yard-in through (-255.5, 67.5) to (-279, 78.6), width 6 m. | Candidate C can connect geometrically, but would share H10's customer/service circulation. |
| Bar Road | (14, 92), (20, 78), (14, 0), (20, -56), (36, -112), (58, -146); width 5 m. | Local road to the landing, not a truck road across the tidal bar. |

Water Street is 5.4 m wide; creek lanes 4.2 m; camp lanes 3.6 m; Shore Lane
3.1 m. Their residential/recreational role and narrower crossings make them poor
industrial approaches. Boardwalks and shore paths are pedestrian routes. Road
widths and connected paving are not proof that an articulated reefer can turn.
The scheduled buyer route in [NineMileCreekTrips](../../Assets/_Project/Code/App/Editor/NineMileCreekTrips.cs)
demonstrates a local vehicle path, not delivered industrial cargo.

The [seam audit](region-seam-audit.md) records a shut Rice Point road door and
missing reciprocal NMC road arrivals; Cumberland connections are incomplete.
Rice Point's sea and strand links are tide/hull conditional. The St Peters bar
is a tidal foot crossing. None supplies an authorized continuous truck route.
Reserve the western Route 91 endpoint (-380, -142) and northern continuation
near (-186, 280); the plant must not silently fix seams, use foot doors for
trucks, or teleport incoming fish. Export remains a future physical carrier
connection, not a new deepwater berth at NMC.

### Shore and apparent open land

South-shore mean-water coordinates run from (-380, -203) through (-230, -197)
to (88, -159). The western stretch is sand below a low red bank; farther east
are camp dunes, the brook-mouth berm/marsh near (-44, -164), and landing beach.
Northward up the east coast are dune, ledge bank, the access gully near (32, -6),
and the red weather bluff before the spit. The barachois occupies approximately
x -64..22, y 107..157.5; the marsh pool x -56..4, y 42.5..73. Their edges include
slopes beyond those water outlines. Camp Pond at (-92, 18) holds still water
at +4.8 m. These are not empty building pads. The source is the wharf contract's
`nmc.plan.coast`, `water` and `lots` sections.

The three western parcels below are **flat land free of the listed building and
road footprints**, but currently farm fields, not unclaimed vacant lots. A
read-only survey used the same decoder as [the road ground sampler](../../tools/nmc-roads/ground.cjs)
on the committed 16-bit G3 height/still maps. Each 80 x 60 m parcel was sampled
at 0.5 m spacing, including boundaries: **19,481 samples per site, all at +6 m,
zero wet samples at +2.2 m spring high or the local still-water level**. This is
3.8 m above the nominated spring-high sea level. It establishes a terrain pad,
not cleared vegetation, ownership, utilities, foundation approval or collision
clearance. The scene's painted-terrain reference resolves to this ground asset.

## Common plant allowance

Assume a **48 x 28 m single-storey building (1,344 m²)** within an **80 x 60 m
site (4,800 m²)**. That is about nine times the existing cold store's footprint,
large enough to make the requested industrial step legible. It reserves space
for separate raw intake, four processing lines, cold and dry stores, dispatch,
staff/utilities and physical byproduct handling from the supply-chain design.
It is a planning allowance, not an accepted rig, production capacity or detailed
room layout. If the eventual process layout needs more space, remeasure before
the seat commits the site.

Place the building 6 m inside the west boundary and 16 m inside each north/south
boundary. This leaves a **26 m deep court east of the building** for loading,
turning and queuing, with intake and dispatch separated along that facade.
The surrounding strips reserve pedestrian access and services. Use a rigid box
truck as the initial planning vehicle; the presence of semi-trailer artwork
does not validate articulated access. Fish must still be landed, carried,
loaded, driven and unloaded by hand; storage and trucks must visibly contain
their goods. No site choice authorizes an automatic inventory link.

## Three candidates

Distances use the crane quay midpoint **(64, 76)** to the parcel centre for the
straight-line comparison. The road estimates start at the existing scheduled
buyer bay **(101, 128.4)**, follow y 120 via the turning-pad exit (76.14, 120),
(50, 118), Wharf Road and the named approach, then the proposed spur to the gate.
They exclude on-site circulation and the hand-carry leg from quay to truck.
They are summed plan polylines, rounded to metres, not timed or driven routes.

| Candidate / rank | Parcel centre; site box | Building box | Quay distance; road estimate | New connection |
|---|---|---|---|---|
| **A / 1: south farm** | **(-255, -40)**; [-295, -215] x [-70, -10] | [-289, -241] x [-54, -26] | **339 m straight; 460 m road** (435 existing alignment + 25 proposed) | East gate (-215, -40) to Route 91 (-190, -40). |
| **B / 2: river farm** | **(-335, 140)**; [-375, -295] x [110, 170] | [-369, -321] x [126, 154] | **404 m straight; 521 m road** (474 + 47) | Mill Road (-282, 170) via (-282, 177), (-295, 177) to gate (-295, 150). |
| **C / 3: north grain field** | **(-335, 45)**; [-375, -295] x [15, 75] | [-369, -321] x [31, 59] | **400 m straight; 444 m road** (428 + 16) | Gate (-295, 67.5) to H10's yard-in aisle (-279, 67.5). |

### A: south farm, direct Route 91 connection

The site converts part of `field.nmc_west_farm_south` (hay/potatoes). Both its
parcel and proposed driveway centreline sample at +6 m. The road route runs
west from the wharf to (-177.45, 92), south through (-177.6, 57) and (-181, 10)
to (-190, -40), then west into its own court. There is no town-river bridge or
plaza parking aisle on that journey. The new entrance must cut the Route 91
verge/hedge deliberately; no access is granted by drawing this line.

It sits southwest of the future farmhouse (-200, -8), south of the plaza flats,
and north of the south-shore estates. The site ends at y -70, just **7 m north
of H8's estate reservation** at y -77, so keep its southern service edge quiet
and preserve that buffer. The H10 farmhouse lot remains outside; H13 must make
farm, housing and plant shift routines coexist. H11 creek homes and H12's
campground are outside the parcel and truck approach. Route 91 seam work still
controls incoming Rice Point/Cumberland freight; A leaves both endpoints clear.

**Vessel landing:** none at the plant. It is inland; fish arrive by road after
landing at the existing working quay. Do not infer a private wharf across the
south-shore homes. **Arrival experience, inferred from the plan:** by sea the
player first encounters the working harbour, then follows the catch inland;
the plant is not an assured harbour silhouette. By road from Rice Point it
appears on the west side of Route 91 beyond the shore estates, with its loading
court facing the road. The reserved buffer to the farmhouse keeps the change
from homes to industry readable without replacing a neighbourhood.

Best serves **P4** (a real first haul that can later be delegated) and **P2**
(a distinct industrial destination). It also serves P3 through deliveries and
shift work. P1/P5 remain at the fishing/landing legs; this proposal invents no
flood hazard on a +6 m plant pad.

### B: river farm, west of the plaza and below the falls

This converts pasture in `field.nmc_river_farm`. The parcel fits at +6 m and is
7 m west of the plaza's x -288 boundary. Trucks use Wharf Road, Route 91 north
to the Mill Road junction at (-177.3, 158), then Mill Road west. The proposed
47 m dogleg approaches from beyond the plaza's northern edge instead of
crossing its lumber yard. It is **not built**: its centreline ranges from
about **+5.40 to +6 m**, with zero wet samples, so entrance grading/support
needs design even though the building site itself is level.

H10's lumber exit at (-279, 167.4) and H7's falls path from (-282, 170) compete
for this small junction. The falls trail would need a deliberate crossing or
separation; the plant cannot claim it as a truck lane. Keep the river and its
bank access north of the site intact. B is only **5 m from the west region
edge**, with no assumed expansion beyond it. H8 shores, H11 creek houses and
H12 camp are outside the parcel; H13 would need new worker access without
turning the falls outing into a loading-yard shortcut. Future seam connections
do not supply this parcel with a second entrance.

**Vessel landing:** none. The nearby inland river/falls route is not industrial
berthing; all fish still land at the existing quay. **Arrival experience,
inferred:** sea arrivals meet the wharf before a longer inland drive, with no
guaranteed view of the plant. Road arrivals encounter it beyond the plaza and
lumber yard at the end of Mill Road. Its industrial presence would sit beside
the falls/pasture outing, which is a significant cost to that quiet destination.

Best serves **P2/P3**, a visible work district beside other businesses. It
serves P4 through a separate haul, but the falls conflict works against P5's
cozy respite. P1 still comes from the source fishery; river proximity alone
does not establish a tide-dependent plant.

### C: north grain field, shared plaza service access

This takes most of the central portion of `field.nmc_west_farm_north`, remaining
between the hedges at y 0 and y 96. Site and proposed spur centreline sample at
+6 m. Trucks leave Wharf Road straight across Route 91 into the plaza, descend
the east aisle to y 67.5, cross the southern parking aisle, then follow the
yard-in alignment west to the new gate. The 16 m spur is short; the **shared
shopping/service approach is the reason to rank it last**.

The parcel is 7 m west of H10's overall plaza boundary, and the spur enters
H10's reserved southern circulation. Lumber traffic, grocery deliveries,
customer cars and pedestrians would all meet incoming fish trucks. Additional
queue space inside the plant cannot cure that off-site conflict. The plant is
also 5 m from the west region edge, with no land claimed in a neighbouring
region. H8 shore reservations, H11 creek and H12 camp stay outside; H13 must
review worker and shopping routines. Keep H10's reefer seating and housing
plans intact. As for A/B, seam work is still required for outside suppliers.

**Vessel landing:** none; this is an inland processing site served from the
same quay. **Arrival experience, inferred:** the sea-to-plant journey reveals
industry behind the shops after unloading at the harbour. By road, the plant
would read behind the grocery/lumber district rather than as a standalone
Route 91 destination. That creates a strong market-to-processing association,
but makes the plant's working entrance harder to separate from shopping.

Best serves **P3**, with suppliers and a grocery visibly near one another,
then P2. It supports P4's physical deliveries, but concentrating truck and
pedestrian work in the plaza weakens both route clarity and P5's comfort.
P1 remains tied to the landing supply, not the inland pad.

## Decision and remaining proof

Choose **A first**, **B second**, **C third**. A sacrifices cultivated land and
requires a new entrance, but avoids taking an existing business, household,
camp pitch, wharf or scenic path. B needs a more difficult connection beside
the falls. C saves only about 16 m of road travel relative to A while imposing
shared plaza traffic. None has direct vessel access; that is consistent with
the truck-fed brief and preserves the working harbour's role.

The one fact this audit cannot establish without an editor is **whether the
intended rigid fish truck can enter, queue, back to an intake bay and leave the
26 m court with its full collision footprint continuously supported and clear
of the pedestrian strip**. The site dimensions reserve that possibility;
they do not prove it. An authorized placement/vehicle pass must run that
manoeuvre on the selected layout before accepting the footprint. Art lanes
also retain control of the eventual appearance; the arrival descriptions
above are geographic intentions, not accepted views or new plate baselines.

Verification here was read-only: scene grep/ranges; all twelve NMC key-scene
placement flags; road/lot/shore contracts; building sidecar dimensions; three
terrain grids; driveway centreline samples; and polyline distance arithmetic.
No Unity run, driving test, new look, completed freight connection or operating
plant is claimed. The [five pillars](../vision-and-pillars.md#3-the-five-pillars)
and [roadmap](../roadmap.md) remain the design and phase boundaries.
