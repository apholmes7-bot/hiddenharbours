#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite — the GEAR joins the decor band; the FACE does not
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

        /// <summary>The lamp posts' own root — every light in the region under one object.</summary>
        public const string LampsRootName = "Lamps";
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

        /// <summary>
        /// The row the LAMP POSTS stand on: the FRONT of the gear band, half a metre in — the closest to
        /// the mooring edge that anything standing is allowed to get.
        ///
        /// <para>⭐ Not <see cref="BackRowY"/>, and a 02:00 plate is why. This quay is ten metres deep and
        /// a <see cref="LightPresets.Kind.Lightpost"/> reaches 3.6 m, so a lamp against the yard lights the
        /// gear and leaves the berths — the one place a crew steps off a boat in the dark — entirely
        /// unlit. There is no row on a 10 m quay that reaches both, so the lamp takes the working edge and
        /// the gear behind it keeps the dark it has always had.</para>
        ///
        /// <para><see cref="WorkingStripMetres"/> is what stops it going further: the strip is where fish
        /// are landed and lines are handled, and a post in it is a post in the way of the one thing the
        /// wharf is for. So the lamp stands at the strip's back edge and reaches across it.</para>
        /// </summary>
        public static float LampRowY => GearBandMinY + 0.5f;

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
        /// <c>NineMileCreekMainland</c> states and the fill's own shape confirms. Also the heading the
        /// WHARF HEAD looks along — the quay's east end faces the same bay.</summary>
        public const float ApronSeawardHeading = 90f;

        /// <summary>…and the heading its OUTER face looks along: the reciprocal, out over the shoal.
        /// Derived from the working face rather than typed, so turning the apron turns both.</summary>
        public static float ApronWestSeawardHeading => (ApronSeawardHeading + 180f) % 360f;

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
        /// <b>THE LAMPS</b> — the owner's 2026-09-03 ruling, <i>"yes i want lights on land"</i>, as seven
        /// places on this region's ground.
        ///
        /// <para><b>What was here before: one unlit sprite.</b> The yard light at the wharf entrance has
        /// stood since #462 carrying the note <i>"the only lit thing out here at night"</i> — and it was a
        /// picture of a lamp. Nothing in Nine Mile Creek emitted a photon. It keeps its site (the plan chose
        /// it and the plan was right) and finally carries the light the note always claimed.</para>
        ///
        /// <para><b>Sited on things that already exist, never on a coordinate.</b> The quay lamps take the
        /// wharf's own berth rhythm and <see cref="LampRowY"/>; the road lamps take Wharf Road's route and
        /// the SAME 5 m north offset the pole line uses — because a lamp goes where the wire is, and
        /// <c>NineMileCreekMainland</c> §12 already decided which side of the road that is. Move the road or
        /// the wharf and every lamp follows.</para>
        ///
        /// <para><b>Varied, not regular.</b> <c>docs/design/municipal-infrastructure.md</c> §3.4's
        /// acceptance test is NEGATIVE — <i>if the island reads REGULAR at night the slice is wrong</i>. So
        /// 322 m of Wharf Road gets TWO lamps, at the two places a person stops (the junction it leaves the
        /// through-road by, and the neck where it steps onto the spit), and dark gravel in between; the 84 m
        /// quay gets two warm posts and the one cool flood at its entrance, not a run.</para>
        ///
        /// <para>Berths 2, 6, 7 and 11 already carry the wood stack, the standpipe, the net frame and the
        /// winter stack, so the lamps take <b>4 and 9</b> — clear of all of them, and spread along the wall
        /// the moored fleet lies against.</para>
        ///
        /// <para>⚠ <b>They stand at the FRONT of the gear band, not against the yard.</b> See
        /// <see cref="LampRowY"/>: on a ten-metre quay a 3.6 m pool cannot reach both the berths and the
        /// back, and the berths are where somebody steps off a boat in the dark.</para>
        ///
        /// <para>⚠ <b>Nothing on this quay throws a lamp shadow, and that is not this table's doing.</b>
        /// Unlike <c>StPetersWharf</c> — whose <c>IsStandingFitting</c> gives its bollards and pileheads a
        /// <c>SpriteShadow</c> — no Nine Mile Creek builder makes any quay fitting a caster at all, so there
        /// is nothing here for a lamp to throw and nothing that casts by day either. Giving them shadows is
        /// a change to this region's daylight as much as its night, and belongs to a PR that says so.</para>
        /// </summary>
        public static IReadOnlyList<LampPosts.Site> Lamps(ITidalTerrain terrain)
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            float lampRow = LampRowY, sea = SeawardHeading;
            Rect yard = NineMileCreekLaydown.ApronArea();
            Rect forecourt = NineMileCreekStation.Route91ApronArea();

            return new[]
            {
                // --- the wall the fleet lies against ------------------------------------------------
                LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.StreetLamp,
                    new Vector2(AtBerth(4), lampRow), sea,
                    "the west end of the mooring wall — at the front of the gear band, as near the berths " +
                    "as anything may stand, where a crew comes off a boat in the dark"),
                LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.StreetLamp,
                    new Vector2(AtBerth(9), lampRow), sea,
                    "the east end of the same wall, five berths along: two lit stretches with a dark one " +
                    "between them, which is what a working quay looks like at night"),

                // --- where the road arrives ----------------------------------------------------------
                LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.YardLight,
                    WharfEntrance() + new Vector2(0f, 2f), sea,
                    "where the pole line ends: the only lit thing out here at night, and it stands in " +
                    "the yard rather than on the working deck — #462's site, now actually lit"),

                // --- Wharf Road, on the pole line ---------------------------------------------------
                OnThePoleLine(road, 0, LampPosts.StreetLamp, terrain,
                    "the town end, where Wharf Road leaves the through-road — the junction you turn at " +
                    "in the dark"),
                OnThePoleLine(road, 4, LampPosts.StreetLamp, terrain,
                    "the approach across the spit — anchored where the road leaves the fields, walked " +
                    "forward to the first site that is dry"),

                // --- the working yards ---------------------------------------------------------------
                LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.FloodMast,
                    new Vector2(yard.center.x, yard.yMin - 1.5f), 0f,
                    "the laydown yard's lane mouth — OFF the pavement, because a mast in a bay is a mast " +
                    "a machine backs into; it lights the lane the nine are driven along"),
                LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.YardLight,
                    new Vector2(forecourt.xMax + 1.5f, forecourt.yMax + 1.5f),
                    NineMileCreekStation.Route91RoadHeadingDegrees,
                    "the Route 91 forecourt, at its corner clear of the paving and the canopy — a fuel " +
                    "stop on a trunk road is lit or it is closed"),
            };
        }

        /// <summary>
        /// A lamp on Wharf Road's pole line, anchored at the route's <paramref name="nodeIndex"/>th NODE:
        /// the route's own position, pushed onto the north side by the SAME
        /// <c>NineMileCreekMainland.UtilityPoleOffsetMetres</c> the poles use, facing across the road it
        /// lights. Derived rather than typed, so a lamp cannot end up on the far side from the wire.
        ///
        /// <para><b>⚠ Snapped to the MIDPOINT between two poles, and it has to be.</b> <see cref="Poles"/>
        /// stands one every <c>UtilityPoleSpacingMetres</c> from along = 0, so a lamp placed at node 0 —
        /// which is exactly what the first draft did — lands inside pole 0. Snapping to the nearest
        /// half-spacing puts a lamp twenty metres from its two neighbouring poles, which is both
        /// collision-free by construction and how a road is really strung: the lamps go in the gaps.</para>
        ///
        /// <para><b>⭐⭐ And then it WALKS FORWARD until the ground is dry, which is not a nicety.</b> The
        /// second draft anchored at node 4 and snapped to along = 220 m, where the pole line's own 5 m north
        /// offset puts a post at <b>−0.16 m</b> — the road crosses the neck between the barachois and the
        /// marsh pool, and five metres north of the centre-line there is water. Measured along the whole
        /// line, every half-spacing site is 6.00 m of dry field except that one, and 3.60 m out on the spit
        /// beyond it.
        ///
        /// <para>⚠ <b>The existing pole line steps over that hole by luck.</b> The poles at 200 m (5.71 m)
        /// and 240 m (5.04 m) straddle the notch, so nothing has ever stood in it — a pole at 220 m would be
        /// in the water exactly as this lamp was. Worth knowing before anyone changes
        /// <c>UtilityPoleSpacingMetres</c>.</para>
        ///
        /// <para>Walking forward rather than hand-picking a dry number keeps the site DERIVED: the anchor
        /// still names a place the road goes, and a terrain edit that floods or drains the neck moves the
        /// lamp instead of silently leaving it paddling. A null terrain takes the anchor unchecked, and
        /// <see cref="LampPosts.Place"/>'s own guard is the backstop.</para>
        /// </summary>
        static LampPosts.Site OnThePoleLine(Vector2[] route, int nodeIndex, string key, ITidalTerrain terrain,
                                            string reason)
        {
            float spacing = NineMileCreekMainland.UtilityPoleSpacingMetres;
            float length = NineMileCreekMainland.RouteLength(route);
            float anchor = (Mathf.Floor(AlongAtNode(route, nodeIndex) / spacing) + 0.5f) * spacing;

            for (float along = anchor; along < length; along += spacing)
            {
                Vector2 at = PoleLinePoint(route, along, out float heading);
                if (terrain != null && terrain.ElevationAt(at) <= NineMileCreekMainland.SpringHighWater)
                    continue;
                string where = along > anchor
                    ? $"{reason} (anchored at {anchor:0} m, walked to {along:0} m: the ground at the anchor " +
                      "is at or below spring high water)"
                    : reason;
                return LampPosts.OnGround(LampPosts.UtilityFamily, key, at, heading, where);
            }

            // Nowhere dry ahead: take the anchor and let Place()'s guard say so out loud.
            Vector2 fallback = PoleLinePoint(route, anchor, out float fallbackHeading);
            return LampPosts.OnGround(LampPosts.UtilityFamily, key, fallback, fallbackHeading, reason);
        }

        /// <summary>A point on the pole line at <paramref name="along"/>, with the heading a LAMP takes
        /// there. A lamp looks ACROSS the road — unlike a pole, whose crossarm must lie ALONG the wires it
        /// carries — so from the north side it looks south, at the gravel it lights.</summary>
        static Vector2 PoleLinePoint(Vector2[] route, float along, out float heading)
        {
            Vector2 on = MainlandCoast.PositionAt(route, along);
            Vector2 north = NorthNormalAt(route, along);
            heading = IsoPackSprites.HeadingOf(-north);
            return on + north * NineMileCreekMainland.UtilityPoleOffsetMetres;
        }

        /// <summary>How far along a route its <paramref name="index"/>th NODE lies. The road's nodes are
        /// authored PLACES with names of their own ("the neck between the barachois and the marsh pool"),
        /// so siting a lamp at one is siting it at something — where a fraction of the route length would
        /// be a number somebody chose.</summary>
        public static float AlongAtNode(Vector2[] route, int index)
        {
            float along = 0f;
            int last = Mathf.Clamp(index, 0, route.Length - 1);
            for (int i = 0; i < last; i++) along += Vector2.Distance(route[i], route[i + 1]);
            return along;
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
                // ⭐ THE YARD LIGHT IS NOT HERE ANY MORE — it moved to Lamps() and became a light. It
                // stood at this entrance from #462 described as "the only lit thing out here at night" and
                // emitted nothing whatever: a picture of a lamp on a pole. Placing it in both tables would
                // draw two poles, so it is placed ONCE, by the file that knows how to light one.

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

        /// <summary>
        /// ⭐ How far the piece's COMMITTED PICTURE reaches above and below its own pivot, in world
        /// units — read off the sprite the placer will actually hand the renderer, not derived from the
        /// footprint. False if the sheet has not sliced (the pack bakes to order; a caller decides
        /// whether that is a skip or a failure).
        ///
        /// <para>The cell is the pack's own <b>pivot-aligned union of the returned buffer extents across
        /// all eight facings</b> (its contract's <c>cellRule</c>), so this is an upper bound on the ink
        /// of any one facing and a safe thing to assert a sort line against. It exists because
        /// <c>NineMileCreekQuayFace.DrawnTopRiseFromPivot</c> is <i>not</i> such a bound — the crib's
        /// header logs stand proud of the footprint the geometry knows about.</para>
        /// </summary>
        public static bool DrawnExtent(FacePiece piece, out float abovePivot, out float belowPivot)
        {
            abovePivot = belowPivot = 0f;
            Sprite sprite = IsoPackSprites.Facing(WharfFamily, piece.Key, FacingFor(piece));
            if (sprite == null) return false;
            abovePivot = sprite.bounds.max.y;
            belowPivot = -sprite.bounds.min.y;
            return true;
        }

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

        // --- ⭐ THE THREE EDGES THAT WERE NEVER FACED (owner playtest 2026-09-04: "theres gaps between
        // sections"). A-1 drew the two working faces and the arm and stopped there. But the wharf is a
        // FILL: every edge of it stands 4.60 m above the harbour shoal (deck +3.00, shoal −1.60,
        // measured on the built terrain), so every edge with water outside it is a wall, and three of
        // them had nothing drawn on them at all — the ground simply fell away over the fill's 1.2 m of
        // falloff and the bay lapped against a raw cut. Walked as a perimeter, the wharf's edges are:
        //
        //     quay  SOUTH  x 92.3…170.0   77.7 m   drawn  (NorthWallRun)
        //     quay  EAST   y  87.0… 93.8   6.8 m   NOT DRAWN → QuayHeadRun
        //     quay  NORTH  —                       no wall: the spit stands behind it
        //     apron EAST   y  44.0… 86.0  42.0 m   drawn  (WestWallRun)
        //     apron WEST   y  44.0… 92.0  48.0 m   NOT DRAWN → ApronWestRun   ← the big one
        //     apron SOUTH  x  82.0… 92.0  10.0 m   NOT DRAWN → ApronSouthRun
        //     apron NORTH  —                       no wall: the spit's bank runs onto it
        //
        // Each new run takes its ends from the SAME fills the drawn ones do, so re-siting a wall re-cuts
        // all six together. (The one stretch still bare is a 1.5 m sliver of the quay's WEST side where
        // the apron stops short of it at y = 92 — shorter than a piece is drawn, so a run there would
        // stand 10.9 m of crib across the wharf to cover 1.5 m of notch. Named, not drawn.)

        /// <summary>The apron's OUTER face — its west side, over the shoal the creek drains across. The
        /// longest undrawn edge on the wharf and the one a player walking down from the road sees first.</summary>
        public static Vector2 ApronWestFaceSouth => new Vector2(Apron.xMin, Apron.yMin);

        /// <inheritdoc cref="ApronWestFaceSouth"/>
        public static Vector2 ApronWestFaceNorth => new Vector2(Apron.xMin, Apron.yMax);

        /// <summary>The apron's SOUTH end — the seaward end of the west wall, where the float run leaves
        /// it. Ten metres, one corner of the wharf, and the whole of it stands over water.</summary>
        public static Vector2 ApronSouthFaceWest => new Vector2(Apron.xMin, Apron.yMin);

        /// <inheritdoc cref="ApronSouthFaceWest"/>
        public static Vector2 ApronSouthFaceEast => new Vector2(Apron.xMax, Apron.yMin);

        /// <summary>The WHARF HEAD — the quay's east end, which <see cref="NorthFaceEast"/> already
        /// calls "a real corner" and then turns away from. It runs north from the mooring lip until the
        /// spit's own ground stands behind it (<c>SpitFill</c>), so the head never draws a wall into the
        /// bank.</summary>
        public static Vector2 QuayHeadFaceSouth => new Vector2(Quay.xMax, Quay.yMin);

        /// <inheritdoc cref="QuayHeadFaceSouth"/>
        public static Vector2 QuayHeadFaceNorth =>
            new Vector2(Quay.xMax,
                        Mathf.Min(Quay.yMax,
                                  NineMileCreekMainland.SpitFill.Center.y -
                                  NineMileCreekMainland.SpitFill.HalfSize.y));

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

            /// <summary>How far UP-SCREEN this piece's lip sits above its own transform. This used to
            /// be the piece's SORT LINE (<c>YSortSprite.SortPivotYOffset</c>); it is kept because
            /// <c>NineMileCreekDressingTests</c> measures the drawn picture against it to prove that
            /// sorting there could never have worked — see <see cref="SortingOrder"/>.</summary>
            public float LipRiseFromPivot => Lip.y - Position.y;

            /// <summary>The order this piece draws at — a FIXED rung of the wharf-deck band, not a
            /// Y-sorted one. <see cref="NineMileCreekDressing.FaceSortingOrder"/> is the rule and the
            /// reason it is not the decor band any more.</summary>
            public int SortingOrder => FaceSortingOrder(Wall);
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
        /// <inheritdoc cref="NorthWallRun"/>
        public const string ApronWestRun = "ApronWest";
        /// <inheritdoc cref="NorthWallRun"/>
        public const string ApronSouthRun = "ApronSouth";
        /// <inheritdoc cref="NorthWallRun"/>
        public const string QuayHeadRun = "QuayHead";

        /// <summary>Every run, in the order they are drawn — so a sweep names them all and a new run
        /// cannot be added without this list noticing.</summary>
        public static readonly string[] FaceRuns =
        {
            NorthWallRun, WestWallRun, ApronWestRun, ApronSouthRun, QuayHeadRun, BreakwaterRun,
        };

        // =============================================================================================
        //  ⭐⭐ WHAT THE QUAY FACE SORTS AGAINST — and why it left the decor band (owner playtest
        //  2026-09-04: "the wharfs need layering work as you can disappear under them")
        // =============================================================================================
        //
        // It used to be a Y-sorted decor sprite anchored on its LIP. Both halves of that were wrong, and
        // the second one could not be fixed by moving the sort line:
        //
        // 1. THE PIECE IS NOT A FACE — IT IS A FACE PLUS ITS OWN DECK TOP. `logCrib` is a 9.6 × 5.0 m
        //    crib, and at the mooring face's facing its committed sheet draws ink from 2.63 units BELOW
        //    its pivot to 5.56 units ABOVE it, while the lip sits only 2.38 above. So 3.19 units of
        //    drawn deck stand UP-SCREEN of the sort line, and anything Y-sorted standing in that band —
        //    the player 1 m in from the edge, and the quay's own bollards at lip + 0.5 m (order 852
        //    against the face's 854) — sorted BEHIND the piece and was overdrawn by it. That is the
        //    owner's "you can disappear under them", and it is 3.19 m deep along the whole wall. The
        //    apron's facing measures 3.20. (The painted ground already IS the deck, so those pixels
        //    were a duplicate of terrain that cost the player her body.)
        //
        // 2. A MOORED HULL CANNOT BE Y-SORTED AGAINST AT ALL. Every `BoatVisualDef` ships
        //    `SortingOrder 1` and `BoatHullSkinner` hands it straight to the hull's composite overlay —
        //    BELOW `SortingBands.DecorFloor`. A face anywhere in the decor band (this wall drew at 854)
        //    therefore beat every boat at the wall at every position, and at Nine Mile Creek that meant
        //    the five moored boats were drawn INSIDE the wall and invisible. No sort-line offset can
        //    reach that: the whole decor band is above them. [[hull-sorts-below-the-decor-floor]]
        //
        // So the face is not decor. A quay face is the EDGE OF THE LAND, and that has a property no
        // other sprite in this region has: NOTHING IS EVER BEHIND IT. Everything that can overlap it is
        // either standing on the deck it holds up (draw over) or floating in the water in front of it
        // (draw over); the only things behind it are the sea and the seabed. So it belongs on a FIXED
        // rung of the band #462 kept open for exactly this — `SortingBands.WharfDeckMin…Max`, "a pier
        // stands over water, so the whole deck is above Sea and below DecorFloor" — under the hull's
        // rung at the top of it, and over the sea at −5.
        //
        // (#462's objection to that band — "six orders cannot resolve an 84 m wall" — was about
        // resolving the wall against ITSELF, which it never has to do: every piece of a run shares one
        // lip line and overlapping pieces of a run are the same picture at the same height. What the six
        // orders must resolve is the six RUNS against each other, and only five pairs of them overlap.)

        /// <summary>
        /// The rung of the wharf-deck band a run draws on — a ladder, nearest the camera first, because
        /// where two runs overlap the nearer one has to cover the further one:
        /// <list type="bullet">
        /// <item><description><b>0</b> — the apron's SOUTH end (the seaward corner of the whole wharf,
        /// and the run every other one at the apron passes behind) and the BREAKWATER arm, which
        /// overlaps nothing and so may share the top rung.</description></item>
        /// <item><description><b>−1</b> — the apron's EAST face: the working face the basin sees, over
        /// the west one and over the mooring wall it crosses at the L.</description></item>
        /// <item><description><b>−3</b> — the quay's MOORING face and the apron's WEST face. The two
        /// never meet each other (they are 10 m and a whole wall apart), so they share; the apron's
        /// west face has to sit under its east one because the two overlap down the middle of a 10 m
        /// apron — each course is 5 m deep, so together they cover it.</description></item>
        /// <item><description><b>−4</b> — the WHARF HEAD, which the mooring face's last piece
        /// crosses at the corner. <c>WharfDeckMin</c>, the floor of the band.</description></item>
        /// </list>
        /// <para><b>−2 is skipped on purpose:</b> <c>SeaMistEmitter</c> draws its haze there, and a run
        /// sharing that rung would be in front of or behind a drifting wisp by renderer order rather
        /// than by rule. Below it the mist passes over the wall, which is what mist does.</para>
        /// <para>Every rung is inside <c>WharfDeckMin…Max</c>, strictly below the top of it (where a
        /// hull composites) and strictly above <c>SortingBands.Sea</c>, so the face keeps every
        /// relationship it had with the water and the seabed and loses only the two that were
        /// wrong.</para>
        /// </summary>
        public static int FaceSortingOrder(string wall)
        {
            const int near = SortingBands.WharfDeckMax - 1;      // 0 — the top rung under the hull
            switch (wall)
            {
                case ApronSouthRun:
                case BreakwaterRun: return near;                 //  0
                case WestWallRun:   return near - 1;             // -1
                case NorthWallRun:
                case ApronWestRun:  return near - 3;             // -3, clear of the sea mist at -2
                case QuayHeadRun:   return SortingBands.WharfDeckMin;   // -4
                default:            return SortingBands.WharfDeckMin;
            }
        }

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

            list.AddRange(FaceRun(ApronWestRun, ApronWestFaceSouth, ApronWestFaceNorth,
                ApronWestSeawardHeading,
                "the apron's OUTER face — 48 m of wall over the shoal the creek drains across, and the " +
                "longest edge of this wharf that was never drawn at all. It runs the whole west side: " +
                "the spit's bank stands behind the apron's north end, so nothing needs a face there"));

            list.AddRange(FaceRun(ApronSouthRun, ApronSouthFaceWest, ApronSouthFaceEast,
                SeawardHeading,
                "the apron's south end — the seaward corner of the wharf, where the float run leaves " +
                "the wall. Ten metres, all of it standing over water, and undrawn until #724"));

            list.AddRange(FaceRun(QuayHeadRun, QuayHeadFaceSouth, QuayHeadFaceNorth,
                ApronSeawardHeading,
                "the WHARF HEAD — the quay's east end, which NorthFaceEast has called 'a real corner' " +
                "since #462 and then turned away from. Without it the mooring face stopped dead at " +
                "x = 170 and the bay lapped against a raw cut in the ground"));

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

            // THE LAMPS, on their own root so the owner can toggle every light in the region at once.
            var lampRoot = new GameObject(LampsRootName);
            lampRoot.transform.SetParent(root.transform, worldPositionStays: false);
            int lamps = LampPosts.Place(lampRoot.transform, Lamps(terrain), terrain,
                                        NineMileCreekMainland.SpringHighWater,
                                        "[NineMileCreekDressing]");

            int finds = PlaceFinds(root, terrain);

            // ⭐ THE STATE OF THE QUAY, said out loud on every rebuild rather than left in a merged PR
            // body where the owner will never see it again. For the whole of #471 this line read "THE
            // DRAWN QUAY IS STILL NOT DRAWN"; it now reports the face it drew and the numbers it drew it
            // from, so a re-bake that moved them would be visible in the console as well as in the tests.
            Debug.Log(NineMileCreekQuayFace.FaceReport());

            Debug.Log(
                $"[NineMileCreekDressing] Dressed Nine Mile Creek: {face} course(s) of quay face across " +
                $"every water-facing edge of the wharf and the breakwater, {props} prop(s) from the " +
                $"wharf-decor and utility packs, {lamps} lamp post(s) that actually emit, and {finds} " +
                $"shore find(s) on the foreshore. The gear is in the Y-sort decor band; the FACE is on a " +
                $"fixed rung of the wharf-deck band, under the boats and under everything on the deck " +
                $"(NineMileCreekDressing.FaceSortingOrder). " +
                $"The face is anchored on its LIP and the gear is laid out on the BERTH LINE, with " +
                $"the poles on Wharf Road's own published route, so all three follow the wharf if it " +
                $"moves. NOT built, and deliberately: the ~16 moored lobster boats and the mussel-boat " +
                $"class are owner vision and phase-gated, and the dory yard is left clear because a " +
                $"sightline test measures across it.");

            return face + props + finds;
        }

        /// <summary>
        /// Draw the quay. One sprite per course, anchored so its deck lip lands on the wall's lip.
        ///
        /// <para><b>⭐ IT DRAWS ON A FIXED RUNG OF THE WHARF-DECK BAND, NOT IN THE DECOR BAND.</b> The
        /// piece is a face PLUS its own deck top, so no Y-sort line inside it is right for both halves;
        /// and a moored hull composites below <c>SortingBands.DecorFloor</c>, so no decor-band order can
        /// lose to a boat at the berth. <see cref="FaceSortingOrder"/> carries the whole argument and
        /// the measurements behind it. The placement — pivot at chart datum, lip on the wall's own lip —
        /// is unchanged, and is still the only line this file guarantees.</para>
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
                // ⚠️ NO YSortSprite. The face is structure at the water's edge, and nothing is ever
                // behind it — see FaceSortingOrder for the two defects a Y-sorted face shipped.
                sr.sortingOrder = piece.SortingOrder;

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
