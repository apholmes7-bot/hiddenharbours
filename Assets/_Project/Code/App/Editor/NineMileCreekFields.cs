#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // GrassFieldLayout / GrassFieldScatter — the runtime's own grid
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.World;               // CoastClass, CoastPlan, MainlandCoast, MainlandTidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE FIELDS OF NINE MILE CREEK — what grows on a farmed coast, and where.</b> Pure and
    /// deterministic, so a test asserts the hinterland the scene is planted from without loading
    /// anything; <see cref="NineMileCreekFieldPlanter"/> does the Unity work. The same split
    /// <see cref="StPetersWoods"/> / <see cref="StPetersWoodsPlanter"/> already uses for the island.
    ///
    /// <para><b>⭐ THE LAW, AND IT IS THE OPPOSITE OF ST PETERS'.</b> The mainland doc's first
    /// photograph is unambiguous: <i>the land behind the wharf is FIELDS, NOT FOREST.</i> St Peters is a
    /// reverting island whose twenty families left, so it grows in STANDS with meadow between them. Nine
    /// Mile Creek is <b>farmed</b> — a patchwork of strips running down to the shore, hedgerows on the
    /// boundaries, and the occasional tree standing in a hedge or a field corner. There is no stand
    /// field in this file and there must never be one: a mosaic threshold is exactly how a farmed coast
    /// turns into a wood by accident.</para>
    ///
    /// <para><b>⭐ SPARSE ON PURPOSE, EVERY POSITION DERIVED — <see cref="NineMileCreekDressing"/>'s own
    /// law, extended one layer out.</b> The strips come off a lattice anchored on the region origin; the
    /// hedges walk those boundaries and the roads' own published corridors; the trees stand in the
    /// hedges at a counted interval; the marsh reads the two ponds the plan already carved. Nothing here
    /// is measured by eye, and re-siting a road or re-carving a pond moves the planting with it.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Every roll is <see cref="StPetersShoreMap.Hash01"/> or
    /// <see cref="StPetersShoreMap.Wiggle"/> of a POSITION — no <c>System.Random</c>, no
    /// <c>DateTime</c>, no world seed. The grass field additionally carries the seed its meadow grows
    /// from, so every machine grows the same one and a save carries nothing.</para>
    ///
    /// <para><b>⚠ NOTHING ON THE MADE GROUND.</b> The wharf plan keeps the spit, the two decks, the
    /// breakwater and the harbour shoal clear, and every gate here asks
    /// <see cref="NineMileCreekShoreMap.IsMadeGround"/> rather than a bubble invented here. A hedgerow
    /// across a working yard is exactly as wrong as a spruce on the quay.</para>
    /// </summary>
    public static class NineMileCreekFields
    {
        // =================================================================================================
        //  1. THE FLOORS — how far down the coast each layer is allowed to grow
        // =================================================================================================

        /// <summary>
        /// Grass grows wherever the ground the splat shader already paints green is — <b>the same floor,
        /// not a second one</b> (<see cref="NineMileCreekShoreMap.GrassFloorElevation"/>, 4.2 m). St
        /// Peters' own rule, and the reason it is the right one: the band the paint calls grass and the
        /// band that grows blades must be the same band, or the meadow stops in the middle of a green
        /// field or runs on over painted sand.
        /// </summary>
        public static float GrassFloorElevation => NineMileCreekShoreMap.GrassFloorElevation;

        /// <summary>
        /// Hedges and trees want the FIELDS proper, not the green fringe below them: 1.5 m under the
        /// plateau's own +6.0 m, which on this coast's 24 m shore falloff is about four metres inland of
        /// the shoreline. That gap is not a safety margin — <b>it is what a salt-blasted coast looks
        /// like</b>. Nothing woody grows to the tideline here any more than it does on the island.
        /// </summary>
        public static float FieldFloorElevation =>
            NineMileCreekMainland.LandElevation - FieldFloorDropMetres;

        /// <inheritdoc cref="FieldFloorElevation"/>
        public const float FieldFloorDropMetres = 1.5f;

        // =================================================================================================
        //  2. WHAT MUST STAY CLEAR
        // =================================================================================================

        /// <summary>
        /// How far off a road's own carriageway the woody planting starts. The carriageway half-widths
        /// are <see cref="NineMileCreekRoads"/>'s published ones, so a wider road pushes its hedge out
        /// rather than growing a hedge down the middle of it; the extra metre is the verge the grass is
        /// allowed and the hedge is not.
        /// </summary>
        public const float RoadVergeMetres = 1f;

        /// <summary>Ground kept clear around every town lot, beyond the 6 m the lot itself reserves. A
        /// dooryard has a garden in it, not a thicket.</summary>
        public const float TownLotBreathingMetres = 2f;

        /// <summary>
        /// Clearance (m) around the bar's landing. <b>The arrival must be open ground</b> — you step off
        /// 610 m of wet cobble and the first thing you should see is the coast and the road, not a hedge.
        /// The same ruling St Peters makes about its own end of the crossing, at the same scale.
        /// </summary>
        public const float LandingClearanceMetres = 30f;

        /// <summary>
        /// Ground kept clear around the wharf plan's own working sites — the sheds, the parking, the
        /// buyer, the fish store. <see cref="NineMileCreekMainland.IsWorkingSiteInTheWay"/> answers the
        /// question at the radius the plan itself reserves, so this is a pass-through rather than a
        /// second opinion.
        /// </summary>
        public static bool OnAWorkingSite(Vector2 p) => NineMileCreekMainland.IsWorkingSiteInTheWay(p);

        /// <summary>
        /// The ground everything in this file may grow on, before any layer's own rules: dry field, not
        /// made ground, not pond.
        /// </summary>
        public static bool IsFieldGround(ITidalTerrain terrain, Vector2 p, float floorElevation)
        {
            if (terrain == null) return false;
            // ⚠ Cheapest gate first, and it matters: this runs eighty thousand times per build. The two
            // zone tests are a handful of rectangle distances; the terrain read walks a coast run, five
            // fills, two carves and a sandbar.
            if (NineMileCreekShoreMap.IsMadeGround(p)) return false;      // the wharf plan keeps it clear
            if (NineMileCreekShoreMap.IsPondGround(p)) return false;      // the marsh has its own layer
            if (terrain.ElevationAt(p) < floorElevation) return false;
            return true;
        }

        /// <summary>
        /// Ground a WOODY thing — a hedge shrub or a tree — may stand on. Everything
        /// <see cref="IsFieldGround"/> allows, minus the things a standing object would be in the way
        /// of: the roads, the town lots, the wharf's working sites, and the landing.
        ///
        /// <para>⚠ Deliberately stricter than <see cref="IsGrassGround"/>. A meadow grows up to a
        /// doorstep and right to the edge of a gravel road; a hedge does neither. That is the same split
        /// St Peters draws between its tree line and its meadow, and the reason both exist.</para>
        /// </summary>
        public static bool IsPlantable(ITidalTerrain terrain, Vector2 p)
        {
            if (!IsWoodyGround(terrain, p)) return false;

            // ⭐ THE WOOD LOTS GIVE THE FARMED LAYERS NOTHING. A hedgerow does not run through a wood and
            // out the other side, and a field tree does not stand inside one — the LOT is the planting
            // there, at its own grain (NineMileCreekWoodLots). Exactly the give-way idiom
            // NearAHedgedRoad already applies where a field boundary runs into a road's verge, and the
            // reason this gate is HERE rather than in IsWoodyGround: the lot pass has to be able to plant
            // on the ground it has taken, and asks the woody gate for precisely that reason.
            if (NineMileCreekWoodLots.InALot(p)) return false;

            return true;
        }

        /// <summary>
        /// Ground a woody thing may stand on <b>before the wood lots are consulted</b> — everything the
        /// region reserves for a reason other than "a wood is already growing here": the field floor, the
        /// made ground, the ponds, the roads and their pads, the town lots, the wharf's working sites, the
        /// bar landing, and any CUT LANE.
        ///
        /// <para><b>⭐ WHY THIS IS SPLIT OFF <see cref="IsPlantable"/>, and it is not tidiness.</b> The two
        /// passes that plant trees in this region want DIFFERENT answers about one thing and the same answer
        /// about everything else. The farmed pass (hedges, hedgerow trees, field trees) must refuse ground a
        /// wood lot has taken; the lot pass must be allowed to plant on it, or a lot would be a declaration
        /// with nothing in it. Asking one function for the shared part is how the two cannot drift apart —
        /// add a road here and both passes step off it.</para>
        ///
        /// <para>⚠ The CUT LANES are in here rather than in the farmed gate, deliberately: a lane must stay
        /// cut against the wood it runs through as well as against the hedges, so both callers inherit it.</para>
        /// </summary>
        public static bool IsWoodyGround(ITidalTerrain terrain, Vector2 p)
        {
            if (!IsFieldGround(terrain, p, FieldFloorElevation)) return false;
            if (OnAnyCarriageway(p, RoadVergeMetres)) return false;
            if (OnAnyPad(p, RoadVergeMetres)) return false;
            if (OnATownLot(p, TownLotBreathingMetres)) return false;
            if (OnAWorkingSite(p)) return false;

            if (Vector2.Distance(p, NineMileCreekMainland.BarRoad[0]) < LandingClearanceMetres)
                return false;

            // The way into a wood lot, kept open — see NineMileCreekWoodLots.IsCleared.
            if (NineMileCreekWoodLots.IsCleared(p)) return false;

            return true;
        }

        /// <summary>
        /// Ground the MEADOW may grow on. The grass floor rather than the field floor, and only the
        /// carriageway itself kept clear — <b>grass grows to the edge of a gravel road and up to a
        /// doorstep; that is what makes a road read as a road cut through a field.</b>
        /// </summary>
        public static bool IsGrassGround(ITidalTerrain terrain, Vector2 p)
        {
            if (!IsFieldGround(terrain, p, GrassFloorElevation)) return false;
            if (OnATownLot(p, 0f)) return false;
            if (OnAnyCarriageway(p, 0f)) return false;
            if (OnAnyPad(p, 0f)) return false;
            return true;
        }

        /// <summary>
        /// ⭐ <b>Is <paramref name="p"/> on (or within <paramref name="margin"/> of) a paved PAD?</b>
        ///
        /// <para><b>⚠ THIS EXISTED AS A HOLE UNTIL THE TRUCK PARK FOUND IT.</b> This pass has always
        /// asked <see cref="NineMileCreekRoads"/> about its <see cref="NineMileCreekRoads.Ways"/> and
        /// never about its <see cref="NineMileCreekRoads.Pads"/> — a gap that was invisible because both
        /// pads that existed sit on the wharf, and <see cref="IsFieldGround"/> already refuses made
        /// ground. The buyers' gravel was protected by accident, through the working-site radius round
        /// <c>ParkingPos</c>, rather than because it is paved.</para>
        ///
        /// <para>The truck park is the first pad on the mainland PLATEAU — field ground by every test
        /// this file applies — so without this a hedge would be planted across it and the meadow would
        /// grow over it. Asked of the published pad list rather than of the park by name, so the apron
        /// and the buyers' gravel are now covered on purpose instead of incidentally, and a pad added
        /// later inherits it.</para>
        /// </summary>
        public static bool OnAnyPad(Vector2 p, float margin)
        {
            foreach (var pad in Pads)
            {
                Rect area = pad.Area;
                if (p.x >= area.xMin - margin && p.x <= area.xMax + margin &&
                    p.y >= area.yMin - margin && p.y <= area.yMax + margin)
                    return true;
            }
            return false;
        }

        /// <summary>The paved pads, memoised for the same reason <see cref="Carriageways"/> is — this
        /// pass asks about eighty thousand candidate sites and <see cref="NineMileCreekRoads.Pads"/>
        /// rebuilds two rectangles from wharf geometry on every call.</summary>
        static NineMileCreekRoads.Pad[] Pads
        {
            get
            {
                if (_pads != null) return _pads;
                var pads = NineMileCreekRoads.Pads();
                var array = new NineMileCreekRoads.Pad[pads.Count];
                for (int i = 0; i < pads.Count; i++) array[i] = pads[i];
                _pads = array;
                return _pads;
            }
        }

        static NineMileCreekRoads.Pad[] _pads;

        /// <summary>
        /// Is <paramref name="p"/> on (or within <paramref name="margin"/> of) any paved carriageway?
        ///
        /// <para><b>⭐ IT ASKS <see cref="NineMileCreekRoads"/>, and that is the seam between the two
        /// passes.</b> The road pass publishes each way's own half-width; this reads it. Widen a road
        /// there and its hedge steps back here — nothing in this file knows how wide a road is.</para>
        /// </summary>
        public static bool OnAnyCarriageway(Vector2 p, float margin)
        {
            foreach (var way in Carriageways)
                if (NineMileCreekMainland.DistanceToRoute(way.Route, p) <= way.HalfWidth + margin)
                    return true;
            return false;
        }

        /// <summary>
        /// The road network, memoised.
        ///
        /// <para><b>⚠ A CACHE, and it earns its keep.</b> <see cref="NineMileCreekRoads.Ways"/> builds a
        /// list and re-projects five town lots onto two roads every time it is called, and this pass asks
        /// it about eighty thousand candidate sites. The memo is of a PURE function of published data, so
        /// it can never disagree with the thing it caches — the same arrangement
        /// <c>RoadKitContract</c> makes about the kit's contract, with the same escape hatch below.</para>
        /// </summary>
        static NineMileCreekRoads.Way[] Carriageways
        {
            get
            {
                if (_carriageways != null) return _carriageways;
                var ways = NineMileCreekRoads.Ways();
                var array = new NineMileCreekRoads.Way[ways.Count];
                for (int i = 0; i < ways.Count; i++) array[i] = ways[i];
                _carriageways = array;
                return _carriageways;
            }
        }

        static NineMileCreekRoads.Way[] _carriageways;

        /// <summary>Drop the memoised road network. Nothing in a build needs this — the roads are
        /// constants — but a test that wants to prove the cache is not the thing making its assertion
        /// true can clear it.</summary>
        public static void InvalidateRoadCache()
        {
            _carriageways = null;
            _pads = null;
        }

        /// <summary>Is <paramref name="p"/> inside a town lot's reserved ground?</summary>
        public static bool OnATownLot(Vector2 p, float margin)
        {
            float reach = NineMileCreekMainland.TownLotRadius + margin;
            foreach (var lot in NineMileCreekMainland.TownLots)
                if (Vector2.Distance(p, new Vector2(lot.x, lot.y)) < reach) return true;
            return false;
        }

        // =================================================================================================
        //  3. THE HABITAT FIELDS — how wet, how exposed
        // =================================================================================================

        /// <summary>
        /// Coherent damp/dry field: hollows hold water, ridges shed it. Its own salt, so a wet patch is
        /// independent of everything else scattered here. The same shape St Peters' wetness has, on this
        /// region's own salt — the noise is a pure function of world position and carries no region in
        /// it, which is precisely why it can be shared without sharing a coastline.
        /// </summary>
        public static float WetnessAt(Vector2 worldPos) =>
            (StPetersShoreMap.Wiggle(
                 worldPos * (StPetersShoreMap.BandWiggleScale / WetnessScaleMetres), salt: 907) + 1f) * 0.5f;

        /// <summary>Feature size (m) of the damp/dry mosaic — how big a wet hollow is.</summary>
        public const float WetnessScaleMetres = 42f;

        /// <summary>Above this wetness the ground is a damp hollow — sweet gale and meadowsweet country,
        /// and the reason a hedge is not one species from end to end.</summary>
        public const float WetThreshold = 0.72f;

        /// <inheritdoc cref="WetThreshold"/>
        public const float DampThreshold = 0.58f;

        /// <summary>
        /// How much salt and wind this ground takes, 0 (sheltered, inland) to 1 (the shore itself).
        ///
        /// <para><b>⚠ MEASURED FROM THE COAST RUN, not from elevation.</b> A mainland is a SIDE, not a
        /// shape: this region's whole interior sits at the same +6.0 m plateau height, so elevation says
        /// nothing at all about how far from the sea you are — the mistake St Peters' own exposure field
        /// records making, and one this geography would make far more comprehensively. Distance to the
        /// authored coastline is the honest measure, and it is the one <see cref="MainlandCoast"/>
        /// already computes.</para>
        /// </summary>
        public static float ExposureAt(Vector2 worldPos)
        {
            MainlandCoast.Project(NineMileCreekMainland.CoastPoints, worldPos,
                                  out float _, out float seaward);
            // `seaward` is positive out to sea and negative inland; the fields are inland, so the depth
            // that matters is how far back from the line we are.
            float inland = Mathf.Max(0f, -seaward);
            return 1f - Mathf.Clamp01(inland / ExposureDepthMetres);
        }

        /// <summary>How far inland the salt wind reaches before the ground counts as sheltered. 140 m on
        /// a region whose fields run ~400 m back from the shore leaves a genuine sheltered core for the
        /// hardwoods, which is the whole reason the gradient exists.</summary>
        public const float ExposureDepthMetres = 140f;

        /// <summary>Ground within this of a cliff sector's shoreline is HEADLAND — wind-scoured, and it
        /// gets the grass library's headland art rather than the meadow's.</summary>
        public const float HeadlandReachMetres = 22f;

        // =================================================================================================
        //  4. THE FIELD PATTERN — strips, and the boundaries between them
        // =================================================================================================
        // ⭐ A RULE, NOT A TABLE (NineMileCreekMainland.OwnerShedLot's own words). PEI's fields are long
        // strips running down to the water, and this coast runs north–south, so the strips run EAST–WEST
        // and their boundaries are lines of constant y. Anchored on the region origin so the lattice is
        // reproducible from nothing, and spaced at a real field's width.

        /// <summary>How wide one field strip is, north to south. Ninety-six metres is a working PEI field
        /// — big enough that a boundary is an event rather than a fence every few strides, small enough
        /// that the hinterland reads as a patchwork rather than as one prairie.</summary>
        public const float FieldStripMetres = 96f;

        /// <summary>The latitude of field boundary <paramref name="index"/>, counted from the region's
        /// centre line outward. Index 0 IS the centre line, which keeps the lattice symmetric about the
        /// origin and independent of how the region rectangle is later resized.</summary>
        public static float BoundaryY(int index) => index * FieldStripMetres;

        /// <summary>Every field boundary that crosses this region, south to north — derived from the
        /// region's own rectangle, so a taller region grows more of them without a code change.</summary>
        public static IReadOnlyList<float> FieldBoundaries()
        {
            var list = new List<float>();
            float half = NineMileCreekMainland.RegionWorldSize.y * 0.5f;
            int reach = Mathf.CeilToInt(half / FieldStripMetres);
            for (int i = -reach; i <= reach; i++)
            {
                float y = BoundaryY(i);
                if (Mathf.Abs(y) <= half) list.Add(y);
            }
            return list;
        }

        // =================================================================================================
        //  5. THE HEDGEROWS — the boundaries, and the roads
        // =================================================================================================

        /// <summary>How far apart the shrubs in a hedge stand. Three and a half metres reads as a run of
        /// bushes rather than a wall, and it is the one knob that trades how solid a hedge looks against
        /// how many GameObjects the region carries (rule 7).</summary>
        public const float HedgeSpacingMetres = 3.5f;

        /// <summary>Max deterministic offset (m) applied across a hedge line, so it wanders like a hedge
        /// instead of ruling a line across the map.</summary>
        public const float HedgeWanderMetres = 1.6f;

        /// <summary>Fraction of hedge stations that actually carry a shrub. A real hedgerow is gappy —
        /// gates, gaps a tractor goes through, stretches that were grubbed out — and a solid one reads as
        /// a wall the player will try to walk round.</summary>
        public const float HedgeDensity = 0.72f;

        /// <summary>How far off a road's carriageway a ROAD hedge stands: the verge, plus enough that a
        /// bush is beside the road and not in it.</summary>
        public static float RoadHedgeOffsetMetres => RoadVergeMetres + 1.5f;

        /// <summary>One station on a hedge: where it is, how wet the ground is, and which line it belongs
        /// to (so a failing test names a boundary rather than an index).</summary>
        public readonly struct HedgeStation
        {
            public readonly Vector2 Position;
            public readonly string Line;
            /// <summary>Index along the line — what the tree interval counts.</summary>
            public readonly int Step;

            public HedgeStation(Vector2 position, string line, int step)
            {
                Position = position; Line = line; Step = step;
            }
        }

        /// <summary>Name of the field-boundary hedge at a given latitude.</summary>
        public static string BoundaryLineName(float y) => $"FieldBoundary_{y:0}";

        /// <summary>Name of a road's hedge, on one side of it.</summary>
        public static string RoadLineName(string road, bool north) =>
            $"RoadHedge_{road}_{(north ? "N" : "S")}";

        /// <summary>
        /// Every hedge station in the region — the field boundaries first, then the roads.
        ///
        /// <para>The stations are laid out with NO reference to the terrain: they are the region's field
        /// pattern, and whether a shrub can actually stand on one is the caller's next question. That is
        /// what lets a test check the PATTERN (spacing, wander, which lines exist) separately from the
        /// PLANTING (what the ground allows), which are two different ways for this to be wrong.</para>
        /// </summary>
        public static List<HedgeStation> HedgeStations()
        {
            var stations = new List<HedgeStation>();

            float halfX = NineMileCreekMainland.RegionWorldSize.x * 0.5f;
            float westEdge = NineMileCreekMainland.RegionWorldCenter.x - halfX;

            // --- the field boundaries: east–west lines, from the region's west edge out to the coast ---
            // They stop at the coastline rather than at the region edge, because east of it is the bay.
            foreach (float y in FieldBoundaries())
            {
                float eastEnd = CoastXAt(y);
                int steps = Mathf.FloorToInt((eastEnd - westEdge) / HedgeSpacingMetres);
                for (int i = 0; i <= steps; i++)
                {
                    float x = westEdge + i * HedgeSpacingMetres;
                    float wander = (StPetersShoreMap.Hash01(i, Mathf.RoundToInt(y), 911) * 2f - 1f)
                                   * HedgeWanderMetres;
                    var at = new Vector2(x, y + wander);

                    // ⚠ A BOUNDARY GIVES WAY WHERE IT MEETS A ROAD'S VERGE. The y = 96 boundary runs
                    // within a few metres of Wharf Road's western leg for three hundred metres, so
                    // without this it would lay a second hedge alongside the road's own — and, worse,
                    // straight down the utility line the plan published 5 m to that road's north.
                    if (NearAHedgedRoad(at)) continue;

                    stations.Add(new HedgeStation(at, BoundaryLineName(y), i));
                }
            }

            // --- the road hedges: the two GRAVEL roads ----------------------------------------------
            // ⚠ The gravel roads only. The bar road is a red dirt track along an open clifftop and the
            // photographs show it running through bare field; hedging it would turn the region's most
            // exposed road into a lane. The gully path is nineteen metres of foot tread.
            foreach (var way in NineMileCreekRoads.Ways())
            {
                if (way.Surface != NineMileCreekRoads.GravelSurface) continue;
                if (way.Route.Length < 2) continue;

                foreach (bool north in new[] { true, false })
                {
                    if (CarriesThePoleLine(way, north)) continue;
                    AddRoadHedge(stations, way, north);
                }
            }

            return stations;
        }

        /// <summary>
        /// Does this side of this road already carry the utility line?
        ///
        /// <para><b>⭐ A HEDGE AND A POLE LINE DO NOT SHARE A VERGE</b>, and this is where that is
        /// decided. <c>NineMileCreekMainland</c> §12 published the pole route in Phase A — <i>along Wharf
        /// Road, 5 m to its NORTH</i> — and <see cref="NineMileCreekDressing.Poles"/> has walked it since.
        /// A hedge at the same offset would grow a bush round the foot of every pole and a line of wire
        /// through a hedgerow, which is a thing nobody builds. So Wharf Road is hedged on its SOUTH side
        /// only, and the reason is read out of the plan rather than typed here: re-side the poles and the
        /// hedge changes sides with them.</para>
        /// </summary>
        public static bool CarriesThePoleLine(NineMileCreekRoads.Way way, bool north) =>
            way.Name == NineMileCreekRoads.WharfRoadName && north;

        /// <summary>
        /// Is <paramref name="p"/> inside the verge of a road that carries its own hedge? A field
        /// boundary that runs into one gives way to it — see the note at the call site.
        /// </summary>
        public static bool NearAHedgedRoad(Vector2 p)
        {
            foreach (var way in Carriageways)
            {
                if (way.Surface != NineMileCreekRoads.GravelSurface) continue;
                if (NineMileCreekMainland.DistanceToRoute(way.Route, p) <= HedgeGiveWayMetres)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// How wide a hedged road's verge is, for the give-way rule. The FURTHER of the two things
        /// already standing in it — the road's own hedge, and the utility line the plan published 5 m off
        /// Wharf Road — plus one hedge spacing, so a boundary gives way cleanly rather than putting its
        /// last shrub against the road's first. Derived from both, so re-siting either moves it.
        /// </summary>
        public static float HedgeGiveWayMetres =>
            Mathf.Max(NineMileCreekRoads.CarriagewayHalfWidthMetres + RoadHedgeOffsetMetres,
                      NineMileCreekMainland.UtilityPoleOffsetMetres) + HedgeSpacingMetres;

        static void AddRoadHedge(List<HedgeStation> stations, NineMileCreekRoads.Way way, bool north)
        {
            float length = NineMileCreekMainland.RouteLength(way.Route);
            float offset = way.HalfWidth + RoadHedgeOffsetMetres;
            string line = RoadLineName(way.Name, north);

            int steps = Mathf.FloorToInt(length / HedgeSpacingMetres);
            for (int i = 0; i <= steps; i++)
            {
                float along = i * HedgeSpacingMetres;
                Vector2 on = MainlandCoast.PositionAt(way.Route, along);
                // The plan's own north-normal helper, so "which side" is decided once in the region and
                // not twice — the pole line already asks it the same question.
                Vector2 normal = NineMileCreekDressing.NorthNormalAt(way.Route, along);
                Vector2 at = on + (north ? normal : -normal) * offset;

                float wander = (StPetersShoreMap.Hash01(i, north ? 1 : 0, 919) * 2f - 1f)
                               * HedgeWanderMetres * 0.5f;
                stations.Add(new HedgeStation(at + normal * wander, line, i));
            }
        }

        /// <summary>Where the coastline is at a given latitude — how far east a field may run before it
        /// is the bay. Read off the authored coast polyline, never estimated.</summary>
        public static float CoastXAt(float y)
        {
            Vector2[] coast = NineMileCreekMainland.CoastPoints;
            float best = coast[0].x;
            float bestGap = float.MaxValue;

            for (int i = 0; i < coast.Length - 1; i++)
            {
                Vector2 a = coast[i], b = coast[i + 1];
                float lo = Mathf.Min(a.y, b.y), hi = Mathf.Max(a.y, b.y);
                if (y < lo || y > hi) continue;
                float t = Mathf.Approximately(b.y, a.y) ? 0f : (y - a.y) / (b.y - a.y);
                return Mathf.Lerp(a.x, b.x, t);
            }

            // Off the ends of the run — take the nearer end's x rather than nothing.
            foreach (var p in coast)
            {
                float gap = Mathf.Abs(p.y - y);
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = p.x;
            }
            return best;
        }

        /// <summary>
        /// Which of the shrub kit's five habitats a patch of field is. Order matters: wet beats
        /// everything (a hollow is a hollow whatever the wind does), then a wood lot's canopy, then
        /// exposure decides whether open ground is wind-scoured barren or ordinary hedgerow edge.
        ///
        /// <para><b>⭐ THE <c>woods</c> HABITAT EXISTS HERE NOW, AND IT IS STILL BOUNDED BY THE SAME LAW
        /// THAT USED TO EXCLUDE IT.</b> The kit's woods habitat is an UNDERSTOREY — it belongs INSIDE a
        /// stand — and until #554 this coast genuinely had none, so this function refused to name it and
        /// <c>NineMileCreekFieldsTests</c> pinned that refusal. #554 gave the region three bounded wood
        /// lots; this is the ask that finally reaches them. The bound did not move: <c>woods</c> is
        /// returned <b>only</b> inside a declared lot, so a hedgerow is still <c>edge</c> — rose and
        /// raspberry, which is exactly what grows in a Maritime hedge — and the open fields are still
        /// barren or edge by exposure. The old test's inverse (<i>the woods habitat is asked ONLY inside a
        /// stand</i>) is what guards it now.</para>
        ///
        /// <para><b>⚠ A HEDGE CAN NEVER REACH THIS BRANCH, and that is structural rather than lucky.</b>
        /// <see cref="IsPlantable"/> — the farmed layers' gate — refuses ground a lot has taken, so a hedge
        /// station inside a lot is never planted to ask the question. The branch is here rather than in the
        /// understorey pass because the two passes must agree about what kind of ground a metre is; a
        /// second opinion is how a hedge and a wood end up naming the same ground differently.</para>
        ///
        /// <para>⚠ A cut lane's tread is inside its lot's capsule and would answer <c>woods</c> too. Nothing
        /// is ever planted there to ask: <see cref="IsWoodyGround"/> refuses a corridor to BOTH passes, which
        /// keeps one gate answering "may anything stand here" — see its own remarks.</para>
        /// </summary>
        public static string ShrubHabitatAt(Vector2 p)
        {
            float wetness = WetnessAt(p);
            if (wetness >= WetThreshold) return "bog";
            if (wetness >= DampThreshold) return "swale";
            if (NineMileCreekWoodLots.InALot(p)) return StPetersShrubs.WoodsHabitat;
            return ExposureAt(p) >= BarrenExposure ? "barren" : "edge";
        }

        /// <summary>Above this exposure the open ground is BARREN — the wind-scoured clifftop, where a
        /// hedge is blueberry and juniper rather than rose.</summary>
        public const float BarrenExposure = 0.62f;

        /// <summary>One planted hedge shrub.</summary>
        public readonly struct ShrubSite
        {
            public readonly Vector2 Position;
            /// <summary>A habitat TAG (<see cref="ShrubHabitatAt"/>), not a species. The planter picks a
            /// species the CONTRACT places in this habitat, so a re-bake that moves a species between
            /// habitats moves it in the hedge too, with no code change here.</summary>
            public readonly string Habitat;
            /// <summary>Which hedge line this stands on — so a failing test names a boundary or a road
            /// rather than an index. ⚠ The UNDERSTOREY pass reuses this record and puts its WOOD LOT's name
            /// here instead, for the same reason and to the same effect (a failing test names a wood). The
            /// two populations are returned by two different calls and must never be concatenated:
            /// <c>EveryHedgeShrubRidesAFieldBoundaryOrARoad</c> is the test that would catch it.</summary>
            public readonly string Line;
            /// <summary>Column of the kit's variant-axis sheet: a different individual of the same
            /// species, which is what stops a hedge reading as one bush repeated.</summary>
            public readonly int Variant;
            /// <summary>A stable per-site roll, for the planter to pick between the species a habitat
            /// baked more than one of — so a hedge is rose AND raspberry rather than one long run.</summary>
            public readonly int Roll;

            public ShrubSite(Vector2 position, string habitat, string line, int variant, int roll)
            {
                Position = position; Habitat = habitat; Line = line; Variant = variant; Roll = roll;
            }
        }

        /// <summary>
        /// Every shrub in the region's hedges. Pure: the stations, the density roll, the habitat and the
        /// variant are all stable hashes of position, so a rebuild reproduces the hedge exactly.
        /// </summary>
        public static List<ShrubSite> ScatterHedges(ITidalTerrain terrain, int variants)
        {
            var sites = new List<ShrubSite>();
            if (terrain == null) return sites;

            foreach (var station in HedgeStations())
            {
                if (StPetersShoreMap.Hash01(station.Step, HashOfLine(station.Line), 929) > HedgeDensity)
                    continue;
                if (!IsPlantable(terrain, station.Position)) continue;

                int line = HashOfLine(station.Line);
                int variant = Mathf.Min(Mathf.Max(1, variants) - 1,
                    (int)(StPetersShoreMap.Hash01(station.Step, line, 937) * Mathf.Max(1, variants)));
                int roll = (int)(StPetersShoreMap.Hash01(station.Step, line, 1021) * 1024f);

                sites.Add(new ShrubSite(station.Position, ShrubHabitatAt(station.Position),
                                        station.Line, variant, roll));
            }
            return sites;
        }

        /// <summary>A stable integer for a line's name, so two lines' rolls cannot correlate. Ordinal and
        /// deliberately simple — <c>string.GetHashCode</c> is not stable across runtimes and this feeds a
        /// scatter that must reproduce.
        ///
        /// <para>⚠ The IMPLEMENTATION moved to <see cref="WoodlandZones.StableHash"/> when the wood lots
        /// needed the same thing for a lot's name; this is the same algorithm it always was, in one place
        /// instead of two, so no hedge shrub's roll changed. The name stays because a hedge asks about a
        /// LINE and reads better for saying so.</para></summary>
        public static int HashOfLine(string line) => WoodlandZones.StableHash(line);

        // =================================================================================================
        //  6. THE TREES — scattered, and mostly standing in a hedge
        // =================================================================================================
        // ⭐ NOT STANDS. There is no mosaic threshold here and there must not be one. A farmed coast's
        // trees stand in two places: IN the hedgerows (a hedge left long enough grows trees out of it —
        // which is why hedgerow trees are the commonest tree in farmland anywhere) and singly out in the
        // fields, where one was left for shade or a stone pile made it not worth ploughing round.

        /// <summary>One hedge station in this many carries a TREE rather than a shrub. Every eleventh at
        /// 3.5 m is a tree about every 38 m of hedge, which is what a grown-out hedgerow looks like.</summary>
        public const int HedgeTreeInterval = 11;

        /// <summary>Grid spacing (m) of the standalone field trees. Deliberately enormous next to the
        /// woods' 5.4 m: these are SINGLE trees in open fields, and the number of them is the whole
        /// difference between a farmed coast and a wooded one.</summary>
        public const float FieldTreeStepMetres = 48f;

        /// <summary>Max deterministic offset (m) from the grid, so the field trees do not read as an
        /// orchard. Just under half the step, as far as it can go without letting two swap places.</summary>
        public const float FieldTreeJitterMetres = 21f;

        /// <summary>Fraction of the field-tree grid cells that actually carry one. Roughly a third of a
        /// 48 m grid is one tree per ~7,000 m² of field — scattered, which is the word the directive
        /// uses, and still comfortably fewer than the hedgerow trees.</summary>
        public const float FieldTreeChance = 0.35f;

        /// <summary>One planted tree: where, which species, and which drawn individual of it.</summary>
        public readonly struct TreeSite
        {
            public readonly Vector2 Position;
            public readonly string Species;
            public readonly int Variant;
            /// <summary>True for a hedgerow tree, false for one standing alone in a field. Carried so a
            /// test can measure the two populations separately — they are different claims.</summary>
            public readonly bool InHedge;

            public TreeSite(Vector2 position, string species, int variant, bool inHedge)
            {
                Position = position; Species = species; Variant = variant; InHedge = inHedge;
            }
        }

        /// <summary>
        /// Every tree in the region.
        ///
        /// <para><b>The species gradient is the Acadian one, and it is deliberately St Peters'.</b>
        /// <see cref="StPetersWoods.SpeciesPreference"/> is a pure function of (exposure, wetness) — no
        /// island in it at all — and it encodes the real gradient: black spruce and balsam fir on the
        /// blasted edges, red spruce and cedar in the middle, the hardwoods only where it is sheltered.
        /// Two regions on one coast should not disagree about which tree grows in a salt wind, so this
        /// asks the same function with THIS region's own exposure and wetness fields.</para>
        /// </summary>
        public static List<TreeSite> ScatterTrees(ITidalTerrain terrain,
                                                  IReadOnlyCollection<string> available,
                                                  System.Func<string, int> variantsFor)
        {
            var sites = new List<TreeSite>();
            if (terrain == null || available == null || available.Count == 0) return sites;

            // --- the hedgerow trees --------------------------------------------------------------------
            foreach (var station in HedgeStations())
            {
                if (station.Step % HedgeTreeInterval != 0) continue;
                if (!IsPlantable(terrain, station.Position)) continue;

                int line = HashOfLine(station.Line);
                string species = StPetersWoods.PickSpecies(
                    StPetersWoods.SpeciesPreference(ExposureAt(station.Position),
                                                    WetnessAt(station.Position)),
                    available, StPetersShoreMap.Hash01(station.Step, line, 941));
                if (species == null) continue;

                sites.Add(new TreeSite(station.Position, species,
                                       VariantFor(species, variantsFor, station.Step, line, 947), true));
            }

            // --- the field trees -----------------------------------------------------------------------
            Rect region = NineMileCreekRoads.RegionRect();
            int nx = Mathf.Max(1, Mathf.CeilToInt(region.width / FieldTreeStepMetres));
            int ny = Mathf.Max(1, Mathf.CeilToInt(region.height / FieldTreeStepMetres));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                if (StPetersShoreMap.Hash01(ix, iy, 953) > FieldTreeChance) continue;

                float cx = region.xMin + (ix + 0.5f) * FieldTreeStepMetres
                           + (StPetersShoreMap.Hash01(ix, iy, 967) * 2f - 1f) * FieldTreeJitterMetres;
                float cy = region.yMin + (iy + 0.5f) * FieldTreeStepMetres
                           + (StPetersShoreMap.Hash01(ix, iy, 971) * 2f - 1f) * FieldTreeJitterMetres;
                var p = new Vector2(cx, cy);

                if (!region.Contains(p)) continue;
                if (!IsPlantable(terrain, p)) continue;

                string species = StPetersWoods.PickSpecies(
                    StPetersWoods.SpeciesPreference(ExposureAt(p), WetnessAt(p)),
                    available, StPetersShoreMap.Hash01(ix, iy, 977));
                if (species == null) continue;

                sites.Add(new TreeSite(p, species, VariantFor(species, variantsFor, ix, iy, 983), false));
            }

            return sites;
        }

        static int VariantFor(string species, System.Func<string, int> variantsFor,
                              int a, int b, int salt)
        {
            int variants = Mathf.Max(1, variantsFor != null ? variantsFor(species) : 1);
            return Mathf.Min(variants - 1, (int)(StPetersShoreMap.Hash01(a, b, salt) * variants));
        }

        // =================================================================================================
        //  7. THE MEADOW — the grass field on the strips
        // =================================================================================================

        /// <summary>
        /// The seed the fields grow from. 0, like St Peters': the salts in
        /// <see cref="GrassFieldScatter"/> are all offset by <c>seed × stride</c>, so a different seed
        /// re-rolls the whole hinterland — a look change that wants the owner's eye rather than a
        /// refactor's.
        /// </summary>
        public const int GrassSeed = 0;

        /// <summary>
        /// Grid spacing (m) of the meadow's candidate sites.
        ///
        /// <para><b>⭐ WELL OVER TWICE ST PETERS' 0.85 m, and both halves of that are on purpose.</b>
        /// <b>Truth:</b> St Peters is a reverting island where nobody has cut anything in a generation;
        /// this is a farmed coast, and a field that is grazed or mown is not a hayfield. <b>Cost
        /// (rule 7):</b> the field's payload is one byte per candidate site, and this region's plantable
        /// land is roughly seven times the island's — at the island's grain the scene line would be most
        /// of a megabyte and the derived meadow several times the geometry the batching pass was built
        /// to afford.</para>
        ///
        /// <para><b>⚠ 2.0 m, NOT 1.8 — AND CI IS WHY.</b> The first draft used 1.8 m on an estimate that
        /// about half the grid would turn out plantable. It measured at <b>86%</b>, and the meadow came
        /// back at <b>42,819 tufts</b> against the 40,000 the budget test pins — 7% over. The right move
        /// was to thin the grass rather than to widen the ceiling: the ceiling is the argument (half
        /// again the island's ratified count, for seven times the ground) and the grain is the knob. At
        /// 2.0 m the same land carries about 34,600 tufts on ~65 KB of scene, and the field reads more
        /// like worked ground into the bargain. <c>TheGrassFieldFitsItsPayloadBudget</c> is the pin.</para>
        /// </summary>
        public const float GrassStepMetres = 2.0f;

        /// <summary>Max wander (m) off the grid. Just under half the step, as far as it goes before two
        /// neighbours can swap places.</summary>
        public const float GrassJitterMetres = GrassStepMetres * 0.41f;

        /// <summary>How far a tuft's own sub-site may spread — small enough that a site reads as one
        /// clump of growth.</summary>
        public const float GrassSpreadMetres = GrassStepMetres * 0.32f;

        /// <summary>
        /// Sites per grid cell. <b>ONE</b>, against the island's two.
        ///
        /// <para>A second slot doubles both the payload and the derived geometry to make the meadow
        /// clumpier. St Peters wants clumpy — it is reverting. A worked field wants EVEN, so the second
        /// slot would cost the region's whole grass budget to buy a look that is wrong here.</para>
        /// </summary>
        public const int GrassSlotsPerCell = 1;

        /// <summary>
        /// Fraction of candidate sites that grow a tuft.
        ///
        /// <para>Lower than the island's 0.97 for the same reason the grid is coarser: this ground is
        /// worked. Combined with the grid it lands at roughly one tuft per 6 m² — texture on ground the
        /// splat shader already paints green, rather than a hayfield.</para>
        /// </summary>
        public const float GrassChance = 0.62f;

        /// <summary>The grass tint the dry coast bleaches toward — the island's own straw, because it is
        /// the same summer on the same coast.</summary>
        public static Color StrawTint => StPetersGrass.StrawTint;

        /// <summary>Grain (m) of the straw plane. The island's, and for the island's reason: tint is a
        /// smooth function of exposure and storing it per site would cost a byte a tuft to encode a
        /// signal that compresses to nothing.</summary>
        public static float StrawCellMetres => StPetersGrassField.StrawCellMetres;

        /// <summary>
        /// The meadow's grid.
        ///
        /// <para><b>⚠ BOUNDED TO THE LAND, not to the region.</b> The rectangle is 760 × 560 m and more
        /// than a third of it is open bay that can never grow a blade — but a field's payload is a byte
        /// per CELL whether or not anything grows there, so a grid over the whole rectangle would spend
        /// a third of the region's grass budget on water. The east edge is the coastline's own furthest
        /// point plus the shore band the grass floor reaches down into.</para>
        /// </summary>
        public static GrassFieldLayout FieldLayout(int seed = GrassSeed)
        {
            Rect region = NineMileCreekRoads.RegionRect();
            float east = Mathf.Min(region.xMax, GrassEastEdge());

            return new GrassFieldLayout
            {
                OriginX = region.xMin,
                OriginY = region.yMin,
                CellSize = GrassStepMetres,
                JitterMetres = GrassJitterMetres,
                SpreadMetres = GrassSpreadMetres,
                CellsX = Mathf.Max(1, Mathf.CeilToInt((east - region.xMin) / GrassStepMetres)),
                CellsY = Mathf.Max(1, Mathf.CeilToInt(region.height / GrassStepMetres)),
                Slots = GrassSlotsPerCell,
                Seed = seed,
            };
        }

        /// <summary>The furthest east grass can possibly grow: the coastline's own easternmost point,
        /// plus the shore band the grass floor reaches into. Derived, so re-authoring the coast moves the
        /// field's edge with it.</summary>
        public static float GrassEastEdge()
        {
            float east = float.MinValue;
            foreach (var p in NineMileCreekMainland.CoastPoints) east = Mathf.Max(east, p.x);
            return east + NineMileCreekMainland.ShoreFalloff;
        }

        /// <summary>
        /// Which of the grass library's habitats a metre of this region is. The tags are the LIBRARY's
        /// vocabulary (they come off the baked manifest), so they are quoted from
        /// <see cref="StPetersGrass"/> rather than re-spelled here — the same reason the shrub habitats
        /// come out of the shrub contract.
        /// </summary>
        public static string GrassHabitatAt(ITidalTerrain terrain, Vector2 p)
        {
            // The kept ground first: a town lot's surroundings are mown, and a road's verge is the strip
            // nobody cuts. Both are what they are regardless of how wet or exposed the ground is.
            if (OnATownLot(p, TownSwardMetres)) return StPetersGrass.HabitatSward;
            if (OnAnyCarriageway(p, RoadVergeMetres + 1f)) return StPetersGrass.HabitatVerge;

            if (terrain is MainlandTidalTerrain mainland)
            {
                CoastClass coast = mainland.CoastClassAt(p);
                if (CoastPlan.IsCliff(coast) && ExposureAt(p) >= HeadlandExposure)
                    return StPetersGrass.HabitatHeadland;
                if (coast == CoastClass.Dune && ExposureAt(p) >= DuneExposure)
                    return StPetersGrass.HabitatDune;
            }

            // The shore band below the fields — the green fringe between the marram and the plateau.
            if (terrain != null && terrain.ElevationAt(p) < FieldFloorElevation)
                return StPetersGrass.HabitatFringe;

            return StPetersGrass.HabitatMeadow;
        }

        /// <summary>How far out from a town lot the grass reads as kept SWARD rather than field.</summary>
        public const float TownSwardMetres = 10f;

        /// <summary>Exposure above which a cliff sector's grass is headland rather than meadow — the
        /// clifftop strip, not the whole field behind it.</summary>
        public static float HeadlandExposure => 1f - HeadlandReachMetres / ExposureDepthMetres;

        /// <summary>…and the same for the dune tops.</summary>
        public static float DuneExposure => HeadlandExposure;

        /// <summary>
        /// Every grass tuft on the fields, deterministically.
        ///
        /// <para><b>⚠ THE POSITIONS COME FROM THE RUNTIME SCATTER</b> (<see cref="GrassFieldScatter"/>),
        /// not from arithmetic restated here — this walk and <c>GrassField</c>'s derive-at-load must
        /// agree about where every blade stands, or the bake gates one point and the renderer draws
        /// another. Asking one function is how they cannot disagree. St Peters' own note, and its own
        /// near-miss.</para>
        /// </summary>
        public static List<StPetersGrass.GrassTuftSite> ScatterGrass(ITidalTerrain terrain,
                                                                     int seed = GrassSeed)
        {
            var sites = new List<StPetersGrass.GrassTuftSite>();
            if (terrain == null) return sites;

            var layout = FieldLayout(seed);

            for (int ix = 0; ix < layout.CellsX; ix++)
            for (int iy = 0; iy < layout.CellsY; iy++)
            {
                if (StPetersShoreMap.Hash01(ix, iy, 991) > GrassChance) continue;

                for (int t = 0; t < layout.Slots; t++)
                {
                    Vector2 q = GrassFieldScatter.SlotPosition(layout, ix, iy, t);
                    if (!IsGrassGround(terrain, q)) continue;

                    float shape = GrassFieldScatter.ShapeRoll(layout, ix, iy, t);
                    sites.Add(new StPetersGrass.GrassTuftSite
                    {
                        Position = q,
                        CellX = ix,
                        CellY = iy,
                        Slot = t,
                        Habitat = GrassHabitatAt(terrain, q),
                        Roll = GrassFieldScatter.VariantRoll(layout, ix, iy, t),
                        // ⚠ NO BROAD CLUMPS. The library's wide art is the island's cheapest coverage,
                        // and coverage is exactly what a worked field is not supposed to have.
                        Broad = false,
                        Mirror = GrassFieldScatter.Mirrored(layout, ix, iy, t),
                        // ⚠ Scale and Tint are the OBJECT path's fields and the FIELD path does not read
                        // them — the runtime derives both from the shape roll and the straw plane. Filled
                        // honestly rather than left at default so the record still describes the tuft,
                        // and named here so nobody tunes them expecting the meadow to change.
                        Scale = Mathf.Lerp(StPetersGrass.ScaleMin, StPetersGrass.ScaleMax, shape),
                        Tint = GrassFieldScatter.TintFor(StrawTint, StrawAt(q), shape),
                    });
                }
            }
            return sites;
        }

        /// <summary>How bleached this ground is, 0..1 — what the straw plane stores. The exposed coast
        /// dries; the sheltered inland fields stay green.</summary>
        public static float StrawAt(Vector2 p) => Mathf.Clamp01(ExposureAt(p) * 0.8f);

        // =================================================================================================
        //  8. THE MARSH — the barachois and the marsh pool
        // =================================================================================================
        // The two ponds the plan carved are the region's freshwater/brackish edge, and the directive asks
        // for marsh vegetation at both. The BARACHOIS is a coastal lagoon drained through the coastline's
        // notch — brackish, so it is salt marsh — and the marsh pool is the shallower second pond whose
        // whole job is to leave the road a neck. Both get the same banding, keyed to elevation.

        /// <summary>Grid spacing (m) of marsh candidates. The shore plants' own step: these are whole
        /// plants and clumps rather than blades.</summary>
        public static float MarshStepMetres => StPetersShorePlants.PlantStep;

        /// <inheritdoc cref="MarshStepMetres"/>
        public static float MarshJitterMetres => StPetersShorePlants.PlantJitter;

        /// <summary>Fraction of marsh candidates that carry a plant. A marsh is dense — that is what
        /// makes it a marsh — but a pond ringed at every station reads as a hedge round a swimming
        /// pool.</summary>
        public const float MarshDensity = 0.55f;

        /// <summary>How far outside a pond's own rectangle the marsh reaches. The carve's falloff is
        /// where the pond stops being a pond; a marsh grows on exactly that shoulder, so the band is the
        /// falloff plus the metre or so of wet ground above it.</summary>
        public const float MarshShoulderMetres = 6f;

        /// <summary>One planted marsh plant.</summary>
        public readonly struct MarshSite
        {
            public readonly Vector2 Position;
            /// <summary>The shore-plant kit's zone key — <c>low</c> or <c>high</c>.</summary>
            public readonly string Zone;
            public readonly string SpeciesKey;
            public readonly string Pond;
            public readonly float Scale;

            public MarshSite(Vector2 position, string zone, string speciesKey, string pond, float scale)
            {
                Position = position; Zone = zone; SpeciesKey = speciesKey; Pond = pond; Scale = scale;
            }
        }

        /// <summary>Names for the two ponds, so the hierarchy and a failing test say which one.</summary>
        public const string BarachoisName = "Barachois";
        /// <inheritdoc cref="BarachoisName"/>
        public const string MarshPoolName = "MarshPool";

        /// <summary>
        /// The marsh, both ponds. Banded by ELEVATION against the region's own tide, exactly as the
        /// foreshore finds are: below neap high water is low marsh (cordgrass and glasswort, the plants
        /// that stand in salt water), above it is high marsh (saltmeadow hay, black rush, threesquare).
        /// The species come from <see cref="StPetersShorePlants.ZoneSpecies"/> — the kit's own zone map,
        /// not a second list written here.
        /// </summary>
        public static List<MarshSite> ScatterMarsh(ITidalTerrain terrain)
        {
            var sites = new List<MarshSite>();
            if (terrain == null) return sites;

            Scatter(terrain, sites, NineMileCreekMainland.BarachoisCarve, BarachoisName, 1009);
            Scatter(terrain, sites, NineMileCreekMainland.MarshPoolCarve, MarshPoolName, 1031);
            return sites;
        }

        static void Scatter(ITidalTerrain terrain, List<MarshSite> sites,
                            MainlandZone pond, string name, int salt)
        {
            float reach = pond.Falloff + MarshShoulderMetres;
            Rect band = Rect.MinMaxRect(pond.Center.x - pond.HalfSize.x - reach,
                                        pond.Center.y - pond.HalfSize.y - reach,
                                        pond.Center.x + pond.HalfSize.x + reach,
                                        pond.Center.y + pond.HalfSize.y + reach);

            int nx = Mathf.Max(1, Mathf.CeilToInt(band.width / MarshStepMetres));
            int ny = Mathf.Max(1, Mathf.CeilToInt(band.height / MarshStepMetres));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                if (StPetersShoreMap.Hash01(ix, iy, salt) > MarshDensity) continue;

                float cx = band.xMin + (ix + 0.5f) * MarshStepMetres
                           + (StPetersShoreMap.Hash01(ix, iy, salt + 1) * 2f - 1f) * MarshJitterMetres;
                float cy = band.yMin + (iy + 0.5f) * MarshStepMetres
                           + (StPetersShoreMap.Hash01(ix, iy, salt + 2) * 2f - 1f) * MarshJitterMetres;
                var p = new Vector2(cx, cy);

                if (!band.Contains(p)) continue;
                // ⚠ Never on the made ground or in a road's corridor: the road runs the NECK between
                // these two ponds, and a marsh grown across it would drown the causeway the photographs
                // show. The neck is 12 m wide and this is what keeps it walkable.
                if (NineMileCreekShoreMap.IsMadeGround(p)) continue;
                if (OnAnyCarriageway(p, RoadVergeMetres)) continue;

                string zone = MarshZoneAt(terrain, p);
                if (zone == null) continue;

                var pool = StPetersShorePlants.ZoneSpecies[zone];
                int pick = Mathf.Min(pool.Length - 1,
                    (int)(StPetersShoreMap.Hash01(ix, iy, salt + 3) * pool.Length));

                float scale = Mathf.Lerp(StPetersShorePlants.ScaleMin, StPetersShorePlants.ScaleMax,
                                         StPetersShoreMap.Hash01(ix, iy, salt + 4));

                sites.Add(new MarshSite(p, zone, pool[pick], name, scale));
            }
        }

        /// <summary>
        /// The marsh band a point is in, or null where the ground is out of range — too deep (open pond
        /// water, which grows nothing this pass places) or too high (the field above the shoulder).
        /// </summary>
        public static string MarshZoneAt(ITidalTerrain terrain, Vector2 p)
        {
            float e = terrain.ElevationAt(p);
            if (e < MarshFloorElevation) return null;                      // open water in the pond
            if (e > MarshCeilingElevation) return null;                    // the field, not the marsh
            return e <= LowMarshCeilingElevation
                ? StPetersShorePlants.ZoneLowMarsh
                : StPetersShorePlants.ZoneHighMarsh;
        }

        /// <summary>
        /// Below this the pond is open water: <b>MEAN WATER</b>, which is where a salt marsh actually
        /// starts — the band from about mid-tide up to the top of the springs is the definition of one.
        ///
        /// <para>⚠ <b>Not the pond's own bed, and the difference is a whole pond.</b> The marsh pool is
        /// carved to −0.4 m, so a floor keyed to a bed would classify the entire pool interior as low
        /// marsh and plant a hundred cordgrass in it — turning the pond whose only job is to leave the
        /// road a neck into a reed bed. Keying the floor to the TIDE instead leaves both ponds a mirror
        /// at high water and a red mudflat at low, with the marsh on their shoulders, which is exactly
        /// what the owner's first photograph shows.</para>
        /// </summary>
        public static float MarshFloorElevation => NineMileCreekMainland.TideMean;

        /// <summary>Low marsh gives way to high marsh at neap high water — the line the ordinary tide
        /// stops at, which is exactly the botanical boundary.</summary>
        public static float LowMarshCeilingElevation => NineMileCreekShoreMap.NeapHighWater;

        /// <summary>…and above spring high water plus a shoulder it is field, not marsh.</summary>
        public static float MarshCeilingElevation =>
            NineMileCreekMainland.SpringHighWater + MarshUpperShoulderMetres;

        /// <inheritdoc cref="MarshCeilingElevation"/>
        public const float MarshUpperShoulderMetres = 0.8f;
    }
}
#endif
