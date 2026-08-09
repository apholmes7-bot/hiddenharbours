#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite — dressing joins the decor band, never a fixed order
using HiddenHarbours.Core;                // ITidalTerrain, SortingBands
using HiddenHarbours.Tools.RigBaking;     // IsoPackSprites — the read side of the ISO rig pack
using HiddenHarbours.World;               // MainlandCoast, CoastPlan, MainlandZone

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>PHASE B — THE DRESSING OF NINE MILE CREEK.</b> A-1 (#462) built the wharf's GEOMETRY: two walls
    /// as terrain fills registered as standable floor, fourteen berths with a bollard apiece, a climbable
    /// ladder, a solid breakwater. It drew almost nothing. This is the drawing — the gear, the services
    /// and the tideline that turn a correct wharf into an inhabited one.
    ///
    /// <para><b>Sparse on purpose.</b> The owner's photographs are of a working wharf on <i>a coast of
    /// fields</i>: a handful of trap stacks, an oilskin line, one machine, one light. The temptation with
    /// a 61-piece decor kit is to spend it, and a wharf carpeted in props reads as a diorama rather than
    /// a place somebody works. Everything below earns its spot by naming what it is for.</para>
    ///
    /// <para><b>⭐ EVERY POSITION IS DERIVED, none measured by eye.</b> The quay's dressing is laid out on
    /// the BERTH LINE, because that is the rhythm the wharf already has — #462 derived the mooring
    /// fittings from it and the gear a boat lands belongs opposite the boat that lands it. The apron's
    /// stations march from the authored unloading point. The poles walk Wharf Road at the spacing
    /// <c>NineMileCreekMainland</c> already published for them. Re-site the wharf and the dressing goes
    /// with it.</para>
    ///
    /// <para><b>⭐ AND SINCE #478, THE QUAY ITSELF.</b> #471 left this file with one hole in it: the ISO
    /// pack was baked for a different coast and could not draw a 4.6 m face, so the wharf was dressed and
    /// the wall it stood on was not. The re-bake closed it, and §7 below draws all three runs — both
    /// walls and the breakwater — in <b>one course</b> of <c>logCrib</c> apiece, no vertical tiling. The
    /// arithmetic lives in <see cref="NineMileCreekQuayFace"/>, which this file reads rather than
    /// re-derives.</para>
    ///
    /// <para><b>⚠️ WHAT THIS FILE DELIBERATELY DOES NOT PLACE, and why each is a trap rather than an
    /// omission.</b></para>
    /// <list type="bullet">
    /// <item><description><b>A second deck.</b> The face pieces are drawn for their FACE — the wall from
    /// the lip down to the seabed. The deck you stand on is still terrain (#462 authored both walls as
    /// fills and registered them as standable floor), so the pieces are anchored by their lip and the
    /// sprite's own deck band is what lands on the ground the terrain already owns. Nothing here creates
    /// floor, and nothing here can move it.</description></item>
    /// <item><description><b>The decor kit's <c>rescueLadder</c> and <c>ringPost</c>.</b> #462's
    /// guarantee is that <i>the ladder you can see is the ladder you can climb</i> and <i>the bollard you
    /// can see is the bollard you can tie to</i> — both are real components derived from one table. A
    /// decorative ladder or mooring ring beside them is a second one that does nothing, which is exactly
    /// the promise those two components exist to keep. <c>ringStation</c> IS placed: it is a LIFE-ring
    /// station, rescue gear rather than a tie-off, and nothing in the sim claims it.</description></item>
    /// <item><description><b>The dory yard.</b> <c>NineMileCreekDoryTests</c> measures the sightline from
    /// where the player lands to the derelict, and props are exactly what breaks a sightline. Left clear;
    /// <see cref="DoryYardClearanceMetres"/> is the bubble every piece here stays out of.</description></item>
    /// <item><description><b>The ~16 moored lobster boats and the mussel-boat class.</b> Owner vision,
    /// phase-gated. Logged here, not built.</description></item>
    /// </list>
    ///
    /// <para><b>Sorting: the band, never a hand-picked order (ADR 0032).</b> Every sprite this file
    /// places takes a <see cref="YSortSprite"/> and layers by world Y with the rest of the world. A prop
    /// on a wharf is something you walk in front of and behind, and a fixed order cannot express
    /// that.</para>
    /// </summary>
    public static class NineMileCreekDressing
    {
        /// <summary>The root everything Phase B places hangs under — one object the owner can hide.</summary>
        public const string RootName = "NineMileCreekDressing";

        /// <summary>Sub-roots, so the hierarchy reads as the four jobs this file does.</summary>
        public const string QuayRootName = "QuayGear";
        public const string ApronRootName = "ApronGear";
        public const string YardRootName = "YardGear";
        public const string UtilityRootName = "Services";
        public const string FindsRootName = "ShoreFinds";

        /// <summary>The pack families this file draws from, by <c>IsoPackContract</c> key.</summary>
        public const string DecorFamily = "wharfDecor";
        public const string UtilityFamily = "utilityIso";
        public const string FindsFamily = "shoreFinds";

        // =============================================================================================
        //  1. THE BANDS — where on a 10 m deck a thing may stand
        // =============================================================================================

        static Rect Quay => NineMileCreekWharf.DeckFootprint();
        static Rect Apron => NineMileCreekWharf.ApronFootprint();

        /// <summary>
        /// The clear strip behind the mooring lip that nothing may stand in. Fish are landed ACROSS this
        /// strip and lines are handled on it; a wharf whose edge is stacked with gear is a wharf you
        /// cannot work. Three metres is a boat's width of working room and it is also what the reference
        /// photographs show — the gear is always against the back.
        /// </summary>
        public const float WorkingStripMetres = 3f;

        /// <summary>Clear ground left at the deck's landward edge, so gear does not spill into the yard
        /// behind it.</summary>
        public const float BackSetbackMetres = 1.5f;

        /// <summary>Nothing may stand this close to one of #462's mooring fittings. A bollard needs its
        /// turns taken round it and a hung fender needs a hull against it; a crate at arm's length of
        /// either is in the way of the one thing the wharf is for.</summary>
        public const float FittingClearanceMetres = 1.2f;

        /// <summary>
        /// The ground kept clear around the derelict dory and the yard she is sold off.
        ///
        /// <para><b>⚠️ Two different constraints live here and only one of them is a radius.</b> The one
        /// that matters is the SIGHTLINE — <c>NineMileCreekDory.SightlineIsClear</c> measures from where
        /// the player lands to the hull, and a prop standing on that segment hides the beat the region is
        /// built around. That is a segment test, not a bubble, and the dressing tests assert it directly.
        /// This radius is the lesser second rule: do not stand IN the yard. It is
        /// <c>WharfShedRadius</c> — the repo's own "ground a working thing reserves" — rather than a
        /// number invented here, because an invented bubble is how a real constraint gets replaced by a
        /// convenient one.</para>
        /// </summary>
        public static float DoryYardClearanceMetres => NineMileCreekMainland.WharfShedRadius;

        /// <summary>The southern edge of the gear band on the quay — the back of the working strip.</summary>
        public static float GearBandMinY => Quay.yMin + WorkingStripMetres;

        /// <summary>The northern edge of the gear band on the quay.</summary>
        public static float GearBandMaxY => Quay.yMax - BackSetbackMetres;

        /// <summary>The row working gear stands in: the middle of the band, so a piece has room on both
        /// sides whatever its footprint.</summary>
        public static float GearRowY => (GearBandMinY + GearBandMaxY) * 0.5f;

        /// <summary>The row against the yard, for the tall things that should not stand in the middle of
        /// a working deck.</summary>
        public static float BackRowY => GearBandMaxY;

        /// <summary>Where the safety gear stands: in from the lip far enough to be out of the way of a
        /// line, close enough to be at the edge where somebody goes in.</summary>
        public static float LipRowY => Quay.yMin + FittingClearanceMetres;

        /// <summary>Berth <paramref name="index"/>'s x — the wharf's own rhythm, and the same table
        /// #462 hangs the bollards on.</summary>
        public static float AtBerth(int index) => NineMileCreekWharf.BerthPos(index).x;

        /// <summary>Midway between two berths — where the gaps in the fitting run are. The tyre fenders
        /// take the EVEN gaps and the ladder takes one odd one, so an odd gap that is not the ladder's is
        /// the only stretch of lip with nothing already hanging on it.</summary>
        public static float BetweenBerths(int index) => (AtBerth(index) + AtBerth(index + 1)) * 0.5f;

        /// <summary>
        /// Where Wharf Road arrives at the quay: the point on the deck's landward edge nearest the road's
        /// last node. Derived, so the sign, the bunting and the yard light follow the road if it moves.
        /// </summary>
        public static Vector2 WharfEntrance()
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            Vector2 end = road[road.Length - 1];
            return new Vector2(Mathf.Clamp(end.x, Quay.xMin, Quay.xMax), Quay.yMax);
        }

        // =============================================================================================
        //  2. A PIECE
        // =============================================================================================

        /// <summary>One placed prop: which family, which key, where it stands, which way it looks, and
        /// one line on why it is there. The <c>Reason</c> is not decoration — a prop that cannot say what
        /// it is for is the one to cut.</summary>
        public readonly struct Prop
        {
            public readonly string Family;
            public readonly string Key;
            public readonly Vector2 Position;
            /// <summary>Compass heading (N = 0, clockwise) the piece's front looks along. Turned into a
            /// facing cell by the PACK's own declared convention, never by a guess here.</summary>
            public readonly float Heading;
            public readonly string Reason;

            public Prop(string family, string key, Vector2 position, float heading, string reason)
            {
                Family = family; Key = key; Position = position; Heading = heading; Reason = reason;
            }
        }

        /// <summary>The heading a thing on the quay looks along to face the water it serves — #462's
        /// mooring-face heading, so the two cannot disagree about which way the sea is.</summary>
        public static float SeawardHeading => NineMileCreekWharf.MooringFaceHeadingDegrees;

        /// <summary>The heading the apron's working face looks along: its water side is EAST, which
        /// <c>NineMileCreekMainland</c> states and the fill's own shape confirms.</summary>
        public const float ApronSeawardHeading = 90f;

        // =============================================================================================
        //  3. THE QUAY — 84 m of working deck
        // =============================================================================================

        /// <summary>
        /// The gear on the north wall. Six pieces of working gear over eighty-four metres, three tall
        /// things against the back, and the safety and tide gear at the edge — which is roughly one
        /// object per fourteen metres of quay, and reads as a wharf somebody uses rather than a shop.
        ///
        /// <para>The berth indices are spread rather than regular, and berth 13 is avoided throughout:
        /// #462 stands its two piletheads at the deck's seaward corners, both at that berth's x, and a
        /// crate against a pilehead is the one collision this layout could actually make.</para>
        /// </summary>
        public static IReadOnlyList<Prop> QuayGear()
        {
            float gear = GearRowY, back = BackRowY, lip = LipRowY, sea = SeawardHeading;

            return new[]
            {
                // --- the working row: what comes off a boat and what goes back on one ---------------
                new Prop(DecorFamily, "trapStack", new Vector2(AtBerth(1), gear), sea,
                    "the gear this wharf is FOR, at the west end by the apron where the catch lands"),
                new Prop(DecorFamily, "buoyRack", new Vector2(AtBerth(3), gear), sea,
                    "buoys off the traps, racked where they dry"),
                new Prop(DecorFamily, "netPile", new Vector2(AtBerth(5), gear), sea,
                    "a heap of net — the creek takes more than lobster"),
                new Prop(DecorFamily, "trapStack", new Vector2(AtBerth(8), gear), sea,
                    "a second stack further along, so the gear reads as a run and not a display"),
                new Prop(DecorFamily, "ropeCoil", new Vector2(AtBerth(10), gear), sea,
                    "warp coiled where it was flaked down"),
                new Prop(DecorFamily, "toteStack", new Vector2(AtBerth(12), gear), sea,
                    "empty totes at the east end, waiting for the next boat in"),

                // --- against the yard: the tall things, out of the working middle ------------------
                new Prop(DecorFamily, "woodStack", new Vector2(AtBerth(2), back), sea,
                    "lath and cull wood — a trap is a thing you are always mending"),
                new Prop(DecorFamily, "netFrame", new Vector2(AtBerth(7), back), sea,
                    "a drying frame against the back, where it is out of the way of a barrow"),
                new Prop(DecorFamily, "trapStack", new Vector2(AtBerth(11), back), sea,
                    "the winter stack, back against the yard rather than out on the working deck"),

                // --- the edge: what belongs at the lip and nowhere else ----------------------------
                // ⚠️ At the ODD gaps in the fitting run. The tyres take the even gaps and the ladder one
                // odd one, so these are the only metres of lip with nothing already hanging on them.
                new Prop(DecorFamily, "tideStaff", new Vector2(BetweenBerths(1), lip), sea,
                    "the tide board — the one place P1 is written on the wharf itself, and sited at the " +
                    "first clear gap east of where you step ashore so it is read on the way past"),
                new Prop(DecorFamily, "ringStation", new Vector2(BetweenBerths(3), lip), sea,
                    "a life ring — rescue gear, NOT a tie-off (the tie-offs are #462's and they are real)"),
                new Prop(DecorFamily, "ringStation", new Vector2(BetweenBerths(9), lip), sea,
                    "the second one, spaced down the wall the way a wharf actually spaces them"),

                // --- the entrance: where the road arrives -------------------------------------------
                new Prop(DecorFamily, "harbourSign", WharfEntrance() + new Vector2(-4f, 1.6f),
                    RoadArrivalHeading(),
                    "the wharf names itself to whoever comes down the road, facing back along it"),
                new Prop(DecorFamily, "noticeBoard", WharfEntrance() + new Vector2(-1.5f, 1.6f),
                    RoadArrivalHeading(),
                    "seasons, closures and the price the buyer is paying — beside the sign, read together"),
                new Prop(DecorFamily, "bunting", WharfEntrance() + new Vector2(2.5f, 1.4f),
                    RoadArrivalHeading(),
                    "the community's one bit of colour, strung at the entrance — a small wharf's " +
                    "bunting goes up over the way in, not out along the working face"),
                new Prop(DecorFamily, "flagpole", new Vector2(Quay.xMax - 3f, back), sea,
                    "at the wharf head, the far end — the thing you can see from the water"),
            };
        }

        /// <summary>Which way the sign and the bunting turn: back along Wharf Road's last leg, into the
        /// face of whoever is walking down it.</summary>
        public static float RoadArrivalHeading()
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            Vector2 leg = road[road.Length - 1] - road[road.Length - 2];
            return IsoPackSprites.HeadingOf(-leg);
        }

        // =============================================================================================
        //  4. THE APRON — the west wall, where a boat is unloaded
        // =============================================================================================

        /// <summary>How far apart the unloading stations sit along the apron: far enough to get a barrow
        /// between two of them.</summary>
        public const float ApronStationSpacingMetres = 2.5f;

        /// <summary>How far off the apron's centre line a station stands, so the middle stays a lane.</summary>
        public const float ApronSideOffsetMetres = 2f;

        /// <summary>
        /// Station <paramref name="index"/> on the unloading apron — measured from the authored unloading
        /// point, marching north toward the winch, alternating sides of the centre line so the middle of
        /// the apron stays walkable.
        /// </summary>
        public static Vector2 ApronStation(int index)
        {
            float y = NineMileCreekMainland.UnloadApronPos.y + index * ApronStationSpacingMetres;
            float x = Apron.center.x + ((index & 1) == 0 ? -ApronSideOffsetMetres : ApronSideOffsetMetres);
            return new Vector2(x, y);
        }

        /// <summary>
        /// The apron's gear. This is the busiest ground in the region and it should be: it is where a
        /// boat's catch is craned up, graded, weighed and iced, and every one of those is a thing the
        /// economy already models.
        /// </summary>
        public static IReadOnlyList<Prop> ApronGear() => new[]
        {
            // ⭐ THE WINCH THE PLAN ASKED FOR BY NAME. NineMileCreekMainland: the apron's water side is a
            // curb-only edge at this camera, "which is why the plan wants the winch to be a tall legible
            // object rather than a detail on a wall". davitHoist is 3.00 m — the tallest working machine
            // in the decor kit and the only one that reads as lifting.
            new Prop(DecorFamily, "davitHoist",
                     new Vector2(NineMileCreekMainland.WinchPos.x, NineMileCreekMainland.WinchPos.y),
                     ApronSeawardHeading,
                     "the winch, at the authored WinchPos and turned to the water it lifts out of"),

            new Prop(DecorFamily, "sortingTrough", ApronStation(0), ApronSeawardHeading,
                "the catch goes into the trough first — this IS the unloading point"),
            new Prop(DecorFamily, "guttingTable", ApronStation(1), ApronSeawardHeading,
                "gutted on the wharf, the way a day boat's catch is"),
            new Prop(DecorFamily, "weighScale", ApronStation(2), ApronSeawardHeading,
                "weighed where the buyer can see it — the number the market pays on"),
            new Prop(DecorFamily, "iceChest", ApronStation(3), ApronSeawardHeading,
                "iced immediately, or the price the buyer quotes stops meaning anything"),
            new Prop(DecorFamily, "dockCart", ApronStation(-1), ApronSeawardHeading,
                "the cart it all goes up the apron in, parked south of the trough"),
        };

        // =============================================================================================
        //  5. THE YARD — the shanty row and the sheds behind it
        // =============================================================================================

        /// <summary>Midway between two shanties in the row — the only ground along it that is outside
        /// both sheds' reserved radius, and the natural place gear ends up.</summary>
        public static Vector2 BetweenShanties(int index)
        {
            Vector3 a = NineMileCreekMainland.ShantyRow[index];
            Vector3 b = NineMileCreekMainland.ShantyRow[index + 1];
            return new Vector2((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f);
        }

        /// <summary>
        /// The yard. Four pieces along the shanty row and two at the working sheds — the domestic edge of
        /// a working coast, where the gear is drying rather than in use.
        /// </summary>
        public static IReadOnlyList<Prop> YardGear()
        {
            float toQuay = SeawardHeading;   // the row turns its working side to the water, like the houses

            return new[]
            {
                new Prop(DecorFamily, "oilskinLine", BetweenShanties(0), toQuay,
                    "oilskins out between the sheds — the most working-coast object in the kit, and the " +
                    "one that says somebody was out this morning"),
                new Prop(DecorFamily, "codFlake", BetweenShanties(1), toQuay,
                    "a drying flake: this creek salted and dried long before it iced and trucked"),
                new Prop(DecorFamily, "woodStack", BetweenShanties(2), toQuay,
                    "cordwood against the shanty wall"),
                new Prop(DecorFamily, "herringSticks", BetweenShanties(3), toQuay,
                    "herring sticks — bait, and the reason the bait shed is where it is"),

                new Prop(DecorFamily, "baitBarrel",
                    new Vector2(NineMileCreekMainland.BaitShedPos.x - 3f,
                                NineMileCreekMainland.BaitShedPos.y - 3.5f), toQuay,
                    "salt bait barrels outside the bait shed, on the yard side"),
                new Prop(DecorFamily, "trapStack",
                    new Vector2(NineMileCreekMainland.TrapStorePos.x,
                                NineMileCreekMainland.TrapStorePos.y - 4f), toQuay,
                    "the trap store's overflow, stacked outside it in season"),
            };
        }

        // =============================================================================================
        //  6. THE SERVICES — poles, light, water, fuel
        // =============================================================================================

        /// <summary>
        /// The utility poles, walking Wharf Road from the town to the wharf at the offset and spacing
        /// <c>NineMileCreekMainland</c> §12 already published — <b>positioned there precisely so Phase B
        /// would not have to re-decide it</b>, and this file takes it at its word rather than inventing a
        /// second route.
        ///
        /// <para>The offset side is derived, not typed: the plan says NORTH of the centre-line, so the
        /// perpendicular chosen is whichever of the two has a positive y. Wharf Road turns
        /// east-north-east onto the spit and the north side turns with it.</para>
        /// </summary>
        public static IReadOnlyList<Prop> Poles()
        {
            var list = new List<Prop>();
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            float length = NineMileCreekMainland.RouteLength(road);
            float spacing = NineMileCreekMainland.UtilityPoleSpacingMetres;
            float offset = NineMileCreekMainland.UtilityPoleOffsetMetres;

            int count = Mathf.FloorToInt(length / spacing) + 1;
            for (int i = 0; i < count; i++)
            {
                float along = i * spacing;
                Vector2 on = MainlandCoast.PositionAt(road, along);
                Vector2 north = NorthNormalAt(road, along);

                // ⚠️ A POLE FACES ALONG THE LINE, NOT ACROSS IT. Rendered at dir 0 the rig's crossarm
                // lies along its X axis, so the wires — which run perpendicular to a crossarm — run
                // along its +Y. Turning the pole to face the road's own heading is therefore what puts
                // the wires down the road; facing it across the road would stand every crossarm parallel
                // to the wires it carries. Measured off the art, not assumed from the name.
                float heading = IsoPackSprites.HeadingOf(RouteDirectionAt(road, along));

                list.Add(new Prop(UtilityFamily, "powerPole", on + north * offset, heading,
                    $"pole {i} of the Wharf Road line, {along:0} m from the town end — the route " +
                    "NineMileCreekMainland §12 published for exactly this"));
            }
            return list;
        }

        /// <summary>The unit direction a route runs in at a distance along it.</summary>
        public static Vector2 RouteDirectionAt(Vector2[] route, float along)
        {
            int seg = Mathf.Clamp(MainlandCoast.SegmentIndexAt(route, along), 0, route.Length - 2);
            Vector2 d = route[seg + 1] - route[seg];
            return d.sqrMagnitude < 1e-12f ? Vector2.right : d.normalized;
        }

        /// <summary>The unit perpendicular to a route that points NORTH — the side the plan puts the
        /// poles on. Of the two perpendiculars, the one whose y is positive.</summary>
        public static Vector2 NorthNormalAt(Vector2[] route, float along)
        {
            Vector2 d = RouteDirectionAt(route, along);
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            return n.y >= 0f ? n : -n;
        }

        /// <summary>
        /// The rest of the services — what a working wharf has that is not gear. Deliberately short: this
        /// is a community wharf, not a port, and the plan is explicit that the yard light is <i>the only
        /// lit thing out there at night</i>.
        /// </summary>
        public static IReadOnlyList<Prop> Services()
        {
            Vector2 entrance = WharfEntrance();
            float sea = SeawardHeading;

            return new[]
            {
                new Prop(UtilityFamily, "yardLight", entrance + new Vector2(0f, 2f), sea,
                    "where the pole line ends: the only lit thing out here at night, and it stands in " +
                    "the yard rather than on the working deck"),

                // ⚠️ ON THE APRON, not on the quay's west end — which is where they were first sited,
                // and where the fuel pump came within 0.28 m of the line from where the player steps
                // ashore to the derelict dory. That line is the region's opening beat
                // (NineMileCreekDory.SightlineIsClear) and a 1.9 m pump standing on it hides her. The
                // apron is also the truer home: it is the service end, and its east face is the side a
                // boat lies against to take fuel.
                new Prop(UtilityFamily, "fuelPump",
                         new Vector2(Apron.center.x - 3f, Apron.yMax - 4f), ApronSeawardHeading,
                    "diesel at the service end, where a boat lies against the apron to take it"),
                new Prop(UtilityFamily, "oilTank",
                         new Vector2(Apron.center.x - 3f, Apron.yMax - 1.5f), ApronSeawardHeading,
                    "the tank the pump draws from, behind it at the apron's landward corner"),

                new Prop(UtilityFamily, "standpipe", new Vector2(AtBerth(6), BackRowY), sea,
                    "washdown water, mid-quay — the deck gets hosed after every landing"),

                new Prop(UtilityFamily, "pedestal", new Vector2(AtBerth(2), GearRowY), sea,
                    "shore power, west end"),
                new Prop(UtilityFamily, "pedestal", new Vector2(AtBerth(9), GearRowY), sea,
                    "shore power, east end — two serve fourteen berths, which is what a creek affords"),

                // ⚠️ NORTH of the parking, not south: Wharf Road passes within 1.2 m on the south side,
                // and NineMileCreekMainland.RoadHalfWidth reserves 3 m of corridor that nothing may sit
                // in. The first draft put it there and the corridor check caught it.
                new Prop(UtilityFamily, "yardHydrant",
                         new Vector2(NineMileCreekMainland.ParkingPos.x,
                                     NineMileCreekMainland.ParkingPos.y + 3f), sea,
                    "a hydrant by the parking — the sheds are timber and the nearest engine is in town"),
            };
        }

        // ---------------------------------------------------------------------------------------------
        //  what the PACK says, re-exported
        // ---------------------------------------------------------------------------------------------
        // HiddenHarbours.Tests.EditMode cannot see HiddenHarbours.Tools.RigBaking.Editor, and adding the
        // reference to test one integer would let every future test reach past the region into the pack.
        // These few are the only pack facts the dressing tests need, so the region hands them over and
        // the tests stay tests OF THE REGION.

        /// <summary>Facings on a directional sheet.</summary>
        public static int Facings => IsoPackSprites.Facings;

        /// <summary>Lie angles a shore find bakes in.</summary>
        public static int FindLieAngles => IsoPackSprites.FindLieAngles;

        /// <summary>Variants a shore find bakes in.</summary>
        public static int FindVariants => IsoPackSprites.FindVariants;

        /// <summary>The facing cell a prop resolves to, by the PACK's declared azimuth convention.</summary>
        public static int FacingFor(Prop prop) =>
            IsoPackSprites.FacingForHeading(prop.Family, prop.Heading);

        /// <summary>…and the facing cell a course of quay face resolves to. Its own re-export because
        /// the face comes from a DIFFERENT family, and the convention is read per family: the wharf pack
        /// is registered counter-clockwise, so cell <c>i</c> depicts heading −45°·i and reading the
        /// sheet's <c>N NE E SE…</c> label order as a compass would turn every wall the wrong way with
        /// nothing failing.</summary>
        public static int FacingFor(FacePiece piece) =>
            IsoPackSprites.FacingForHeading(WharfFamily, piece.Heading);

        /// <summary>Every distinct <c>zone</c> the finds contract declares — what
        /// <see cref="Bands"/> has to cover, or a find is baked and never placed.</summary>
        public static IReadOnlyList<string> DeclaredFindZones()
        {
            var zones = new List<string>();
            var contract = IsoPackSprites.ContractOf(FindsFamily);
            if (contract == null) return zones;

            foreach (var cell in contract.Cells)
                if (!string.IsNullOrEmpty(cell.zone) && !zones.Contains(cell.zone)) zones.Add(cell.zone);
            return zones;
        }

        /// <summary>Every prop this file places, in one sequence, so a test can sweep the lot.</summary>
        public static IEnumerable<Prop> AllProps()
        {
            foreach (var p in QuayGear()) yield return p;
            foreach (var p in ApronGear()) yield return p;
            foreach (var p in YardGear()) yield return p;
            foreach (var p in Poles()) yield return p;
            foreach (var p in Services()) yield return p;
        }

        // =============================================================================================
        //  7. THE QUAY FACE — the run #471 measured and could not draw
        // =============================================================================================
        // ⭐ ONE COURSE, THREE RUNS. NineMileCreekQuayFace owns every number below the plan: which piece,
        // how tall it bakes, and where its pivot goes so the drawn lip lands on the real one. This
        // section owns only the PLAN — which stretches of wall are actually faces, and which way each of
        // them looks — and it derives all of that from NineMileCreekWharf and NineMileCreekMainland.

        /// <summary>The pack family the wharf's STRUCTURE is drawn from — <c>wharfIso</c>, as against the
        /// <c>wharfDecor</c> and <c>utilityIso</c> families everything else here comes out of.</summary>
        public const string WharfFamily = "wharfIso";

        /// <summary>The sub-root the drawn quay hangs under, so the owner can hide the wall without
        /// hiding the gear standing on it.</summary>
        public const string FaceRootName = "QuayFace";

        /// <summary>The plan direction a compass heading points along (N = 0, clockwise) — the inverse of
        /// <c>IsoPackSprites.HeadingOf</c>, so a run's seaward vector and the facing its sprite resolves
        /// to are two views of ONE number rather than two numbers that must be kept agreeing.</summary>
        public static Vector2 PlanDirectionOf(float headingDegrees) =>
            new Vector2(Mathf.Sin(headingDegrees * Mathf.Deg2Rad), Mathf.Cos(headingDegrees * Mathf.Deg2Rad));

        // --- which stretches are actually FACES -------------------------------------------------------
        // ⚠️ THE TWO WALLS MEET IN AN L, and the inside of that corner is not a face. The apron runs
        // north to y = 92 and the quay's deck starts at y = 87, so the apron's last five metres of east
        // side stand against the quay's own ground; likewise the quay's south side west of x = 92 stands
        // against the apron's. Drawing either would put a 4.6 m wall of log crib in the middle of the
        // wharf, facing a deck. Both bounds are DERIVED from the other wall's footprint, so re-siting
        // either wall re-cuts the corner.

        /// <summary>Where the north wall's face begins: the east side of the apron, not the west end of
        /// the deck.</summary>
        public static Vector2 NorthFaceWest =>
            new Vector2(Mathf.Max(Quay.xMin, Apron.xMax), NineMileCreekWharf.MooringEdgeY);

        /// <summary>…and where it ends — the wharf head, which is a real corner.</summary>
        public static Vector2 NorthFaceEast =>
            new Vector2(Quay.xMax, NineMileCreekWharf.MooringEdgeY);

        /// <summary>The apron's face runs up its EAST side — the water side
        /// <c>NineMileCreekMainland</c> states — from its south corner…</summary>
        public static Vector2 ApronFaceSouth => new Vector2(Apron.xMax, Apron.yMin);

        /// <summary>…to where it disappears under the north wall's deck.</summary>
        public static Vector2 ApronFaceNorth => new Vector2(Apron.xMax, Mathf.Min(Apron.yMax, Quay.yMin));

        /// <summary>The breakwater is drawn on its CREST LINE, not on its south edge — the same line
        /// #462 lays its collision on (<c>NineMileCreekWharf.BreakwaterPoints</c>) and the same one the
        /// retired tile kit hung its armour from. An arm is a symmetric box of stone-filled cribs, so its
        /// "lip" runs down the middle of it rather than along one side.</summary>
        public static Vector2 BreakwaterCrestWest =>
            new Vector2(NineMileCreekWharf.BreakwaterWestX, NineMileCreekWharf.BreakwaterY);

        /// <inheritdoc cref="BreakwaterCrestWest"/>
        public static Vector2 BreakwaterCrestEast =>
            new Vector2(NineMileCreekWharf.BreakwaterEastX, NineMileCreekWharf.BreakwaterY);

        /// <summary>Which way the breakwater's exposed side looks: AWAY from the basin the arm shelters.
        /// Derived from which side of the arm the quay lies on, so re-siting either turns the arm's face
        /// with it rather than leaving it presenting its sheltered side to the open bay.</summary>
        public static float BreakwaterSeawardHeading =>
            IsoPackSprites.HeadingOf(new Vector2(
                0f, Mathf.Sign(NineMileCreekWharf.BreakwaterY - NineMileCreekWharf.MooringEdgeY)));

        /// <summary>One placed course of quay face.</summary>
        public readonly struct FacePiece
        {
            /// <summary>The pack preset — one key for the whole quay, because one material is what #462
            /// ruled this wharf is built of.</summary>
            public readonly string Key;

            /// <summary>⚠️ Where the sprite's PIVOT goes, which is NOT where the piece is in plan. The
            /// wharf pack pivots at chart datum, metres of drawn height below the deck — see
            /// <c>NineMileCreekQuayFace.PivotForLip</c>, which is the only thing that computes this.</summary>
            public readonly Vector2 Position;

            /// <summary>The plan line the piece is READ as standing on — its deck lip, on the wall's own
            /// edge. What it sorts by, and the one line the placement guarantees.</summary>
            public readonly Vector2 Lip;

            /// <summary>Compass heading the working face looks along, out over the water.</summary>
            public readonly float Heading;

            /// <summary>Which run it belongs to, so the hierarchy and any failure message name a wall
            /// rather than an index.</summary>
            public readonly string Wall;

            public readonly string Reason;

            public FacePiece(string key, Vector2 position, Vector2 lip, float heading,
                             string wall, string reason)
            {
                Key = key; Position = position; Lip = lip;
                Heading = heading; Wall = wall; Reason = reason;
            }

            /// <summary>How far UP-SCREEN this piece's lip sits above its own transform — what
            /// <c>YSortSprite.SortPivotYOffset</c> takes, so the wall layers on the line it looks like it
            /// stands on instead of on a pivot out in the basin.</summary>
            public float SortYOffset => Lip.y - Position.y;
        }

        /// <summary>
        /// A run of face along one straight stretch of wall, from <paramref name="from"/> to
        /// <paramref name="to"/> along the LIP.
        ///
        /// <para>The count and pitch come from <c>NineMileCreekQuayFace.CoverRun</c> — ceiling, so the run
        /// covers the wall end to end and pieces butt or overlap rather than leaving a hole. Each piece
        /// is centred in its own slot, which is the same "west end PLUS half a block" rule #462's
        /// breakwater armour uses and the same reason: a piece placed at the start of its slot puts half
        /// of itself on the beach.</para>
        /// </summary>
        public static List<FacePiece> FaceRun(string wall, Vector2 from, Vector2 to, float seawardHeading,
                                              string reason)
        {
            var list = new List<FacePiece>();

            Vector2 span = to - from;
            float run = span.magnitude;
            if (run <= 1e-3f) return list;

            Vector2 along = span / run;
            Vector2 seaward = PlanDirectionOf(seawardHeading);
            NineMileCreekQuayFace.CoverRun(run, out int count, out float pitch);

            for (int i = 0; i < count; i++)
            {
                Vector2 lip = from + along * (pitch * (i + 0.5f));
                list.Add(new FacePiece(
                    NineMileCreekQuayFace.FaceCourseKey,
                    NineMileCreekQuayFace.PivotForLip(lip, seaward),
                    lip, seawardHeading, wall, reason));
            }
            return list;
        }

        /// <summary>Run names, so a test names the wall it is checking.</summary>
        public const string NorthWallRun = "NorthWall";
        /// <inheritdoc cref="NorthWallRun"/>
        public const string WestWallRun = "WestWall";
        /// <inheritdoc cref="NorthWallRun"/>
        public const string BreakwaterRun = "Breakwater";

        /// <summary>
        /// The whole drawn quay: the mooring face, the apron's face and the breakwater arm.
        ///
        /// <para><b>Not part of <see cref="AllProps"/>, and that is deliberate.</b> Every sweep over the
        /// props asks questions that are right for gear and wrong for structure — stand clear of the
        /// working strip, stand clear of a mooring fitting, stand on ground above spring high water. A
        /// quay face fails all three by DOING ITS JOB: it stands on the lip, it carries the fittings, and
        /// its feet are 1.4 m under the lowest water. So the face is its own list and its own sweep.</para>
        /// </summary>
        public static IReadOnlyList<FacePiece> FacePieces()
        {
            var list = new List<FacePiece>();

            list.AddRange(FaceRun(NorthWallRun, NorthFaceWest, NorthFaceEast, SeawardHeading,
                "the mooring face — the wall the fleet lies against, and the only stretch of this " +
                "region a player ever sees from the water. It starts at the apron's east side because " +
                "west of that the quay's south side stands against the apron's own ground"));

            list.AddRange(FaceRun(WestWallRun, ApronFaceSouth, ApronFaceNorth, ApronSeawardHeading,
                "the apron's east face — a curb-only edge in the retired kit, which is the whole reason " +
                "the plan wanted the winch to be a tall legible object. It is a drawn wall now, and it " +
                "stops where it goes under the north wall's deck"));

            list.AddRange(FaceRun(BreakwaterRun, BreakwaterCrestWest, BreakwaterCrestEast,
                BreakwaterSeawardHeading,
                "the arm, on its crest line — 92 m of the same stone-filled log crib the walls are, " +
                "which is what #462 read off the owner's photographs and what " +
                "NineMileCreekWharf.BreakwaterArmour has said since"));

            return list;
        }

        // =============================================================================================
        //  8. THE FORESHORE — what the tide left
        // =============================================================================================
        // The finds kit bakes THREE STATES of every find — wet, dry, bleached — and its own contract says
        // what they are for: `zone` is where a find belongs (tide / wrack / upper) and the state is what
        // it looks like there. So the band is not decoration: it decides both WHERE a find goes and WHICH
        // sheet is loaded, and the two cannot disagree because they are the same lookup.

        /// <summary>A tide band, as the finds kit divides the shore.</summary>
        public readonly struct Band
        {
            /// <summary>The kit's <c>zone</c> name — <c>tide</c>, <c>wrack</c> or <c>upper</c>.</summary>
            public readonly string Zone;
            /// <summary>The kit's state sheet to load for a find lying in it.</summary>
            public readonly string State;
            /// <summary>Elevation range (m above chart datum) the band occupies.</summary>
            public readonly float MinElevation, MaxElevation;

            public Band(string zone, string state, float minElevation, float maxElevation)
            {
                Zone = zone; State = state; MinElevation = minElevation; MaxElevation = maxElevation;
            }

            public float Middle => (MinElevation + MaxElevation) * 0.5f;
        }

        /// <summary>How far above the wrack line the upper beach runs before it stops being beach. A
        /// metre and a half: the marram starts about there, and above it the finds would be lying in the
        /// dune rather than on the shore.</summary>
        public const float UpperBeachMetres = 1.5f;

        /// <summary>
        /// The three bands, keyed to the region's own tide. The wrack line is spring high water — the
        /// strandline is where the biggest tide stopped — and everything below it is intertidal.
        /// </summary>
        public static IReadOnlyList<Band> Bands() => new[]
        {
            new Band("tide", "wet",
                     NineMileCreekMainland.SpringLowWater, NineMileCreekMainland.SpringHighWater),
            new Band("wrack", "dry",
                     NineMileCreekMainland.SpringHighWater - WrackBandMetres,
                     NineMileCreekMainland.SpringHighWater + WrackBandMetres),
            new Band("upper", "bleached",
                     NineMileCreekMainland.SpringHighWater,
                     NineMileCreekMainland.SpringHighWater + UpperBeachMetres),
        };

        /// <summary>Half-thickness of the wrack line, in metres of ELEVATION. A strandline is a line, not
        /// a zone — this keeps it one.</summary>
        public const float WrackBandMetres = 0.25f;

        /// <summary>How often the shore is sampled for finds, in metres along the coast run. Twelve is
        /// the kit's own smallest useful spacing at this camera: closer and two finds share a footprint,
        /// further and the beach reads as swept.</summary>
        public const float FindStationMetres = 12f;

        /// <summary>How far seaward or landward of the coast line the search for a band's elevation is
        /// allowed to reach. Bounded by the authored beach band the waterline sweeps
        /// (<c>NineMileCreekMainland.ShoreFalloff</c>) plus a margin — beyond that a "find" is out in the
        /// bay, not on the shore.</summary>
        public static float FindSearchMetres => NineMileCreekMainland.ShoreFalloff + 8f;

        /// <summary>Steps the band search takes across <see cref="FindSearchMetres"/>. Half-metre
        /// resolution: finer than the finds are wide, so no crossing is stepped over.</summary>
        public static int FindSearchSteps => Mathf.CeilToInt(FindSearchMetres * 2f / 0.5f);

        /// <summary>Fraction of candidate (station × band) slots that actually get a find. Two thirds
        /// leaves the beach reading as scattered rather than as a row.</summary>
        public const float FindDensity = 0.66f;

        /// <summary>Salt for the scatter hash, so this scatter cannot correlate with any other in the
        /// world that happens to index the same way.</summary>
        public const int FindHashSalt = 90711;

        /// <summary>One find, placed.</summary>
        public readonly struct Find
        {
            public readonly string Key, State, Zone;
            public readonly Vector2 Position;
            public readonly int LieAngle, Variant;
            public readonly float Elevation;

            public Find(string key, string state, string zone, Vector2 position,
                        int lieAngle, int variant, float elevation)
            {
                Key = key; State = state; Zone = zone; Position = position;
                LieAngle = lieAngle; Variant = variant; Elevation = elevation;
            }
        }

        /// <summary>
        /// The finds the kit declares for a zone, in contract order. Read from the contract rather than
        /// listed here, so a re-bake that adds a find puts it on the beach without a code change.
        /// </summary>
        public static IReadOnlyList<string> FindsInZone(string zone)
        {
            var keys = new List<string>();
            var contract = IsoPackSprites.ContractOf(FindsFamily);
            if (contract == null) return keys;

            foreach (var cell in contract.Cells)
                if (string.Equals(cell.zone, zone, System.StringComparison.Ordinal))
                    keys.Add(cell.key);
            return keys;
        }

        /// <summary>
        /// Where the finds go: walk the coast run, and at each station look for each band's elevation on
        /// the ground that is actually there.
        ///
        /// <para><b>The elevation is SEARCHED, not assumed.</b> A band is a height, and where a height
        /// falls on the shore depends on how steep that stretch is — the same wrack line sits 4 m out on
        /// a bank and 30 m out on the flats. Marching the seaward normal until the terrain crosses the
        /// band's middle puts every find exactly where that height is, which is also what makes the state
        /// honest: a wet find is wet because it is below the tide, not because a table said so.</para>
        ///
        /// <para>Soft coast only. A cliff has no foreshore to comb — its foot is rock or it is under
        /// water — and the coast plan already says which stretches are which.</para>
        /// </summary>
        public static List<Find> Finds(ITidalTerrain terrain)
        {
            var list = new List<Find>();
            if (terrain == null) return list;

            Vector2[] coast = NineMileCreekMainland.CoastPoints;
            var sectors = NineMileCreekMainland.CoastSectors;
            float run = NineMileCreekMainland.CoastRunLength;
            var bands = Bands();

            // One lookup per band rather than one per candidate — the pools are fixed for the whole
            // scatter, and re-deriving them inside the loop would read as if they were not.
            var pools = new List<string>[bands.Count];
            for (int b = 0; b < bands.Count; b++) pools[b] = new List<string>(FindsInZone(bands[b].Zone));

            int stations = Mathf.Max(1, Mathf.FloorToInt(run / FindStationMetres));
            for (int s = 0; s < stations; s++)
            {
                float along = (s + 0.5f) * FindStationMetres;

                // Soft coast only — a cliff foot is not a beach.
                if (CoastPlan.IsCliff(MainlandCoast.ClassAt(sectors, along))) continue;

                Vector2 on = MainlandCoast.PositionAt(coast, along);
                float azimuth = MainlandCoast.OutwardNormalAzimuthAt(coast, along);
                Vector2 seaward = new Vector2(Mathf.Sin(azimuth * Mathf.Deg2Rad),
                                              Mathf.Cos(azimuth * Mathf.Deg2Rad));
                Vector2 alongShore = new Vector2(seaward.y, -seaward.x);

                for (int b = 0; b < bands.Count; b++)
                {
                    Band band = bands[b];
                    if (StPetersShoreMap.Hash01(s, b, FindHashSalt) > FindDensity) continue;

                    var pool = pools[b];
                    if (pool.Count == 0) continue;

                    // Jitter ALONG the shore before searching across it, so two bands at one station do
                    // not stack into a column running straight out to sea.
                    float jitter = (StPetersShoreMap.Hash01(s, b, FindHashSalt + 1) * 2f - 1f)
                                   * FindStationMetres * 0.5f;
                    Vector2 origin = on + alongShore * jitter;

                    if (!TryFindElevation(terrain, origin, seaward, band.Middle, out Vector2 at)) continue;
                    if (IsOnMadeGround(at)) continue;

                    int pick = Mathf.Min(pool.Count - 1,
                        Mathf.FloorToInt(StPetersShoreMap.Hash01(s, b, FindHashSalt + 2) * pool.Count));
                    int lie = Mathf.Min(IsoPackSprites.FindLieAngles - 1,
                        Mathf.FloorToInt(StPetersShoreMap.Hash01(s, b, FindHashSalt + 3)
                                         * IsoPackSprites.FindLieAngles));
                    int variant = Mathf.Min(IsoPackSprites.FindVariants - 1,
                        Mathf.FloorToInt(StPetersShoreMap.Hash01(s, b, FindHashSalt + 4)
                                         * IsoPackSprites.FindVariants));

                    list.Add(new Find(pool[pick], band.State, band.Zone, at, lie, variant,
                                      terrain.ElevationAt(at)));
                }
            }
            return list;
        }

        /// <summary>
        /// March from <paramref name="origin"/> along <paramref name="direction"/> looking for where the
        /// ground crosses <paramref name="targetElevation"/>, and bisect the bracket that finds it.
        /// Starts LANDWARD of the coast line and walks out to sea, so the first crossing found is the
        /// highest one — the shore, not a bar further out.
        /// </summary>
        public static bool TryFindElevation(ITidalTerrain terrain, Vector2 origin, Vector2 direction,
                                            float targetElevation, out Vector2 hit)
        {
            hit = origin;
            if (terrain == null) return false;

            float from = -FindSearchMetres, to = FindSearchMetres;
            int steps = Mathf.Max(4, FindSearchSteps);
            float step = (to - from) / steps;

            float prevT = from;
            float prevE = terrain.ElevationAt(origin + direction * prevT);

            for (int i = 1; i <= steps; i++)
            {
                float t = from + step * i;
                float e = terrain.ElevationAt(origin + direction * t);

                // The ground falls seaward, so a crossing is prev >= target >= now.
                if ((prevE - targetElevation) * (e - targetElevation) <= 0f && !Mathf.Approximately(prevE, e))
                {
                    float lo = prevT, hi = t, loE = prevE;
                    for (int k = 0; k < BisectionSteps; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        float midE = terrain.ElevationAt(origin + direction * mid);
                        if ((loE - targetElevation) * (midE - targetElevation) <= 0f) { hi = mid; }
                        else { lo = mid; loE = midE; }
                    }
                    hit = origin + direction * ((lo + hi) * 0.5f);
                    return true;
                }
                prevT = t; prevE = e;
            }
            return false;
        }

        /// <summary>Bisections after the bracket is found. Eight halvings of a 0.5 m step lands the find
        /// inside 2 mm — far finer than the sprite, and cheap at edit time.</summary>
        public const int BisectionSteps = 8;

        /// <summary>
        /// True on the wharf's MADE GROUND — the spit, the two walls, the breakwater and the harbour
        /// shoal. Finds belong on the natural foreshore; a scallop shell on a concrete deck is a prop,
        /// not a tideline, and the shoal's own elevation would otherwise pull the search onto it.
        /// </summary>
        public static bool IsOnMadeGround(Vector2 p)
        {
            foreach (var fill in NineMileCreekMainland.Fills)
                if (Mathf.Abs(p.x - fill.Center.x) <= fill.HalfSize.x &&
                    Mathf.Abs(p.y - fill.Center.y) <= fill.HalfSize.y)
                    return true;
            return false;
        }

        // =============================================================================================
        //  9. PLACEMENT
        // =============================================================================================

        /// <summary>
        /// Dress the region. Returns how many objects were placed — the quay face, the props and the finds.
        ///
        /// <para>Null-tolerant throughout, the <see cref="NineMileCreekFlavour"/> arrangement: a pack that
        /// has not baked warns once and is skipped, a key with no pixels is skipped with its reason, and
        /// the rest of the dressing still lands. "Declared in the contract" and "has pixels on disk" are
        /// different questions and a partial art state should still dress what it can.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain)
        {
            var root = new GameObject(RootName);

            // ONE warning per family, not one per piece. An un-baked pack would otherwise report itself
            // forty-four times and bury the two lines that actually matter at the bottom of the console.
            foreach (string family in new[] { WharfFamily, DecorFamily, UtilityFamily, FindsFamily })
                if (!IsoPackSprites.FamilyHasSheets(family))
                    Debug.LogWarning(
                        $"[NineMileCreekDressing] the '{family}' pack has no sheets at " +
                        $"{IsoPackSprites.FolderFor(family)} — everything it would have drawn is skipped. " +
                        "The PLACEMENT is unaffected and still correct: re-run the builder after the " +
                        "sheets import and the same objects land in the same places.");

            // THE WALL FIRST, so it is behind everything that stands on it in the hierarchy as well as
            // on the screen.
            int face = PlaceFace(root, FacePieces());

            int props = 0;
            props += PlaceProps(root, QuayRootName, QuayGear(), terrain);
            props += PlaceProps(root, ApronRootName, ApronGear(), terrain);
            props += PlaceProps(root, YardRootName, YardGear(), terrain);

            var services = new List<Prop>(Poles());
            services.AddRange(Services());
            props += PlaceProps(root, UtilityRootName, services, terrain);

            int finds = PlaceFinds(root, terrain);

            // ⭐ THE STATE OF THE QUAY, said out loud on every rebuild rather than left in a merged PR
            // body where the owner will never see it again. For the whole of #471 this line read "THE
            // DRAWN QUAY IS STILL NOT DRAWN"; it now reports the face it drew and the numbers it drew it
            // from, so a re-bake that moved them would be visible in the console as well as in the tests.
            Debug.Log(NineMileCreekQuayFace.FaceReport());

            Debug.Log(
                $"[NineMileCreekDressing] Dressed Nine Mile Creek: {face} course(s) of quay face across " +
                $"the mooring wall, the apron and the breakwater, {props} prop(s) from the wharf-decor " +
                $"and utility packs and {finds} shore find(s) on the foreshore, all in the Y-sort decor " +
                $"band. The face is anchored on its LIP and the gear is laid out on the BERTH LINE, with " +
                $"the poles on Wharf Road's own published route, so all three follow the wharf if it " +
                $"moves. NOT built, and deliberately: the ~16 moored lobster boats and the mussel-boat " +
                $"class are owner vision and phase-gated, and the dory yard is left clear because a " +
                $"sightline test measures across it.");

            return face + props + finds;
        }

        /// <summary>
        /// Draw the quay. One sprite per course, anchored so its deck lip lands on the wall's lip.
        ///
        /// <para><b>⭐ IT SORTS BY ITS LIP, NOT BY ITS TRANSFORM.</b> The wharf pack pivots at chart
        /// datum, so a face piece's transform sits several metres down-screen of the wall — out where the
        /// boats are. Sorted from there it would draw IN FRONT of anything moored against it, which is
        /// the one thing a quay must never do and is invisible until there is a boat at the berth to be
        /// hidden. <see cref="YSortSprite.SortPivotYOffset"/> moves the sort point back up to the lip;
        /// the band is still the decor band, and the order is still computed from world Y (ADR 0032).</para>
        ///
        /// <para><b>Not the wharf-deck band</b> (<c>SortingBands.WharfDeckMin</c>…<c>Max</c>), even
        /// though #462 kept that band open for "the ISO quay Phase B draws". Six orders cannot resolve an
        /// 84 m wall — that is #462's own argument against the retired tile kit's per-row scheme, and it
        /// applies unchanged to the piece it was written about. The face joins the fittings in the decor
        /// band for the same reason they did.</para>
        /// </summary>
        static int PlaceFace(GameObject root, IReadOnlyList<FacePiece> pieces)
        {
            if (pieces.Count == 0) return 0;

            var group = new GameObject(FaceRootName);
            group.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var piece in pieces)
            {
                int facing = IsoPackSprites.FacingForHeading(WharfFamily, piece.Heading);
                Sprite sprite = IsoPackSprites.Facing(WharfFamily, piece.Key, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekDressing] the {piece.Wall} face piece '{piece.Key}' facing " +
                        $"{facing} has no sprite at {IsoPackSprites.SheetPath(WharfFamily, piece.Key)} — " +
                        $"skipping it rather than placing a blank. It would have been: {piece.Reason}.");
                    continue;
                }

                var go = new GameObject($"{piece.Wall}_{piece.Key}_{placed}");
                go.transform.SetParent(group.transform, worldPositionStays: false);
                // ⚠️ The PIVOT, not the plan position — see FacePiece.Position.
                go.transform.position = new Vector3(piece.Position.x, piece.Position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                go.AddComponent<YSortSprite>().SortPivotYOffset = piece.SortYOffset;

                placed++;
            }

            if (placed == 0)
                Debug.LogWarning(
                    $"[NineMileCreekDressing] {pieces.Count} course(s) of quay face were sited and none " +
                    $"had pixels — the wharf ISO pack has not sliced at " +
                    $"{IsoPackSprites.FolderFor(WharfFamily)}. The PLACEMENT is unaffected and still " +
                    "correct; re-run the builder after the sheets import and the same wall lands in the " +
                    "same place.");

            return placed;
        }

        static int PlaceProps(GameObject root, string groupName, IReadOnlyList<Prop> props,
                              ITidalTerrain terrain)
        {
            if (props.Count == 0) return 0;

            var group = new GameObject(groupName);
            group.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var prop in props)
            {
                // ⚠️ CHECKED AGAINST THE AUTHORED TERRAIN, not against the constants the site was derived
                // from — the #345 lesson, and NineMileCreekFlavour's rule. A prop standing in water is an
                // authoring bug to fix loudly, not a prop to quietly drop. It applies to the gear as much
                // as to the poles: the deck is 0.8 m of freeboard at spring high, so a quay prop is only
                // ever a terrain edit away from paddling.
                if (terrain != null)
                {
                    float ground = terrain.ElevationAt(prop.Position);
                    if (ground <= NineMileCreekMainland.SpringHighWater)
                    {
                        Debug.LogError(
                            $"[NineMileCreekDressing] '{prop.Key}' is sited at {prop.Position} where the " +
                            $"ground is {ground:0.00} m — at or below spring high water " +
                            $"({NineMileCreekMainland.SpringHighWater:0.0} m). {prop.Reason}. Move the " +
                            "site; do not lower the tide.");
                        continue;
                    }
                }

                int facing = IsoPackSprites.FacingForHeading(prop.Family, prop.Heading);
                Sprite sprite = IsoPackSprites.Facing(prop.Family, prop.Key, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekDressing] '{prop.Key}' facing {facing} has no sprite at " +
                        $"{IsoPackSprites.SheetPath(prop.Family, prop.Key)} — skipping it rather than " +
                        $"placing a blank. It would have been: {prop.Reason}.");
                    continue;
                }

                var go = new GameObject($"{prop.Key}_{placed}");
                go.transform.SetParent(group.transform, worldPositionStays: false);
                // ⚠️ The pivot IS the ground centre of the footprint (both packs' contracts say so), so
                // the site position is the position and nothing here offsets it.
                go.transform.position = new Vector3(prop.Position.x, prop.Position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // The band, never a hand-picked order (ADR 0032). A trap stack is something you walk
                // around, so it layers by world Y with everything else.
                go.AddComponent<YSortSprite>();

                placed++;
            }
            return placed;
        }

        static int PlaceFinds(GameObject root, ITidalTerrain terrain)
        {
            var finds = Finds(terrain);
            if (finds.Count == 0) return 0;

            var group = new GameObject(FindsRootName);
            group.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var find in finds)
            {
                Sprite sprite = IsoPackSprites.Find(find.Key, find.State, find.LieAngle, find.Variant);
                if (sprite == null) continue;

                var go = new GameObject($"{find.Key}_{find.State}_{placed}");
                go.transform.SetParent(group.transform, worldPositionStays: false);
                go.transform.position = new Vector3(find.Position.x, find.Position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                go.AddComponent<YSortSprite>();
                // No collider: a shell is something you walk over. Making these PICKUPS is a separate
                // lane (the kit publishes `pick` and `catch` anchors for it) and not dressing's call.

                placed++;
            }

            if (placed == 0)
                Debug.LogWarning(
                    $"[NineMileCreekDressing] {finds.Count} find(s) were sited on the foreshore but none " +
                    $"had pixels — the shore-finds pack has not sliced at " +
                    $"{IsoPackSprites.FolderFor(FindsFamily)}. The banding is still correct; re-run after " +
                    "the sheets import.");

            return placed;
        }
    }
}
#endif
