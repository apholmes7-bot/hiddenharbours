#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.Art.Editor;          // RoadKitV3, RoadKitContract — the kit's own identity + lookup

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE ROADS OF NINE MILE CREEK — the network, and the cells it covers.</b> Pure and deterministic:
    /// no scene, no assets, no <c>Tilemap</c>, so a test asserts the road the region is paved with without
    /// loading anything. <see cref="NineMileCreekRoadPainter"/> does the Unity work.
    ///
    /// <para><b>⭐ THE ROUTES ARE NOT AUTHORED HERE — THEY ARE ALREADY PUBLISHED.</b>
    /// <see cref="NineMileCreekMainland"/> §9 has carried the bar road, Wharf Road, the through-road and
    /// the gully path since Phase A-1, as derived data with their own dry-ground test. Nothing in this
    /// file re-types a coordinate: a way is <i>a published route plus a width plus a surface</i>, so
    /// re-siting a road in the geography file moves the tiles with it and the two can never disagree.
    /// That is the same arrangement the pole line already has
    /// (<see cref="NineMileCreekDressing.Poles"/> walks <c>WharfRoad</c> rather than a copy of it).</para>
    ///
    /// <para><b>What the surfaces are, and who ruled them.</b> The mainland doc §1.2 names the bar road
    /// <i>dirt</i> and both Wharf Road and the through-road <i>gravel</i>; the gully path is a footpath.
    /// The wharf doc §3's kit table adds the yard: <i>"Roads, parking, apron — dirt/gravel for the yard,
    /// a <c>concrete</c> apron at the winch"</i>. Every one of those words is a surface the v3 kit bakes,
    /// so the paving is canon read back rather than taste.</para>
    ///
    /// <para><b>⚠ THE ONE CHOICE THAT IS NOT CANON: the town walks.</b> Sidewalks appear nowhere in this
    /// region's canon, and the owner's photographs are of red dirt and gravel, not curbs. So there is no
    /// sidewalk here — what there is instead is a short <b>crushed-shell walk from the road up to each of
    /// the five PUBLIC town lots</b> (the harbourmaster, the chandlery, the store, the tavern, the parish
    /// hall). The four houses get none: nobody surfaces a farm dooryard. See <see cref="TownWalks"/> for
    /// the whole argument, and the PR that landed it flags this for the owner's eye.</para>
    ///
    /// <para><b>⚠ NO RANDOMNESS AT ALL — rule 5 by construction, not by seeding.</b> There is no hash, no
    /// <c>System.Random</c> and no seed anywhere in this file: a cell is paved iff it is within a
    /// published half-width of a published route and the ground under it stays dry. Two runs of the
    /// builder produce the same cells because the same arithmetic produces the same answer, which is a
    /// stronger guarantee than a reproducible scatter. (The kit's own pattern seed — 7 — is baked INTO
    /// the sprites, <see cref="RoadKitV3.BakedSeed"/>, and is not this file's to vary.)</para>
    ///
    /// <para><b>⚠ A ROAD DOES NOT FORD, AND THAT IS THE PAVING RULE ITSELF.</b> A cell whose ground is at
    /// or below spring high water is not paved — the carriageway simply stops where the ground goes
    /// under. That is what makes the bar road taper into the beach at the landing instead of running out
    /// over the flats. The legality guard is therefore NOT "no wet cell" (which the rule makes trivially
    /// true) but <see cref="CentreLineIsContinuous"/>: <b>the route's own centre-line must be paved end to
    /// end</b>. A road driven through the marsh pool loses its middle and fails that test loudly — which
    /// is the defect this region has already shipped once and the reason the dry-ground test exists.</para>
    /// </summary>
    public static class NineMileCreekRoads
    {
        // =================================================================================================
        //  1. HOW WIDE A ROAD IS — carriageways, against the corridor the plan already reserves
        // =================================================================================================
        // ⚠ THE CORRIDOR IS NOT THE CARRIAGEWAY, and conflating them would pave the verge.
        // NineMileCreekMainland.RoadHalfWidth (3 m) is the CLEARED corridor — "nothing may be sited
        // inside it" — which is what the lot tests, the dressing tests and the pole line all measure
        // against. The tiles go inside it, never over it, and RoadsFitInsideTheirOwnCorridor pins that.

        /// <summary>
        /// The through-road and Wharf Road: <b>5 m of gravel</b>. Two vehicles pass on a rural PEI
        /// carriageway and a buyer's truck comes down Wharf Road with a trailer behind it, so this is the
        /// widest thing in the region and it still leaves half a metre of verge inside the corridor.
        /// </summary>
        public const float CarriagewayHalfWidthMetres = 2.5f;

        /// <summary>
        /// The bar road: <b>4 m of red dirt</b>. One vehicle, grass up the middle, the road the owner's
        /// first photograph actually shows running down to the shore. Narrower than the gravel roads
        /// because that difference is the whole point of giving it a different surface.
        /// </summary>
        public const float DirtRoadHalfWidthMetres = 2f;

        /// <summary>
        /// The gully path and the town walks: <b>1.5 m of tread</b>.
        ///
        /// <para>Not a number invented here — it is <see cref="StPetersStarterSplat.PathWidthMetres"/>,
        /// the width the other region's walked paths are painted at and the width its meadow and its woods
        /// both keep clear of. One footpath width in the world, so a player who walks both coasts is not
        /// walking two different games' paths.</para>
        /// </summary>
        public static float FootpathHalfWidthMetres => StPetersStarterSplat.PathWidthMetres * 0.5f;

        // =================================================================================================
        //  2. WHICH SURFACE — every one of them a key the kit actually bakes
        // =================================================================================================
        // ⚠ NAMED, not typed as literals at the call site: RoadKitV3.Surfaces is the kit's own list and
        // SurfacesAreAllBakedByTheKit checks every name below against it, so a re-bake that renamed a
        // surface fails a test instead of painting nothing.

        /// <summary>The bar road. Canon: "the bar road (dirt)", and the photographs are of red dirt.</summary>
        public const string DirtSurface = "dirt";

        /// <summary>Wharf Road, the through-road and the yard's parking. Canon: "(gravel)".</summary>
        public const string GravelSurface = "gravel";

        /// <summary>
        /// The unloading apron at the winch. Canon (wharf doc §3): "a <c>concrete</c> apron at the winch".
        ///
        /// <para>⚠ <b>The v3 kit also bakes a surface literally called <c>apron</c></b> — "wharf apron",
        /// new in v3 and not in the v1 kit the wharf doc was written against. It may well be the better
        /// pixel. But canon says <i>concrete</i>, canon wins (CLAUDE.md), and swapping is a one-constant
        /// change here rather than a re-derivation — so the alternative is named instead of taken.</para>
        /// </summary>
        public const string ApronSurface = "concrete";

        /// <summary>The town walks. Crushed clam shell is what a Maritime front walk actually is — see
        /// <see cref="TownWalks"/> for why this is a shell walk and not a sidewalk.</summary>
        public const string WalkSurface = "shell";

        // =================================================================================================
        //  3. RANK — who wins where two of them overlap
        // =================================================================================================
        // Roads cross. At the junction (−16, 92) the dirt bar road meets gravel Wharf Road, and one metre
        // of ground can only carry one surface. The bigger road carries THROUGH the smaller one, which is
        // what a real junction looks like and what stops a gravel road being interrupted by a dirt square.
        // Ranks are spaced so a way can be inserted between two of them without renumbering.

        public const int RankApron = 50;        // the worked wharf surface: nothing paves over concrete
        public const int RankThroughRoad = 40;  // Route 19 carries through everything it meets
        public const int RankWharfRoad = 35;
        public const int RankParking = 30;
        public const int RankParkSpur = 25;     // ⭐ gives way to Wharf Road AND to the park it serves
        public const int RankBarRoad = 20;
        public const int RankGullyPath = 10;
        public const int RankTownWalk = 5;      // a walk gives way to every road it joins

        // =================================================================================================
        //  4. A WAY, AND A PAD
        // =================================================================================================

        /// <summary>
        /// One stroked route: a published polyline, a carriageway half-width, a surface and a rank.
        /// <see cref="CentreLineCritical"/> marks the ways whose centre-line must survive the dry-ground
        /// filter unbroken — every road does; a pad does not have one.
        /// </summary>
        public readonly struct Way
        {
            /// <summary>Name in the hierarchy and in a failing test's message — a road, never an index.</summary>
            public readonly string Name;
            public readonly string Surface;
            public readonly Vector2[] Route;
            public readonly float HalfWidth;
            public readonly int Rank;
            public readonly bool CentreLineCritical;
            /// <summary>One line on what this way is for. A way that cannot say is the one to cut.</summary>
            public readonly string Reason;

            public Way(string name, string surface, Vector2[] route, float halfWidth, int rank,
                       bool centreLineCritical, string reason)
            {
                Name = name; Surface = surface; Route = route; HalfWidth = halfWidth;
                Rank = rank; CentreLineCritical = centreLineCritical; Reason = reason;
            }
        }

        /// <summary>An axis-aligned paved rectangle — an apron or a parking pad. A stroked polyline gives
        /// rounded ends, and a poured concrete apron on a wharf has corners.</summary>
        public readonly struct Pad
        {
            public readonly string Name;
            public readonly string Surface;
            public readonly Rect Area;
            public readonly int Rank;
            public readonly string Reason;

            public Pad(string name, string surface, Rect area, int rank, string reason)
            {
                Name = name; Surface = surface; Area = area; Rank = rank; Reason = reason;
            }
        }

        // =================================================================================================
        //  5. THE NETWORK
        // =================================================================================================

        public const string BarRoadName = "TheBarRoad";
        public const string WharfRoadName = "WharfRoad";
        public const string ThroughRoadName = "TheThroughRoad";
        public const string GullyPathName = "TheGullyPath";
        public const string TownWalkNamePrefix = "TownWalk_";
        public const string ParkSpurName = "TruckParkSpur";
        public const string TruckParkName = "TruckPark";

        /// <summary>
        /// Every stroked way in the region: the canon four, then the town walks.
        ///
        /// <para>Read the routes straight off <see cref="NineMileCreekMainland"/> — <b>the array, not a
        /// copy of it</b> — so this list is a window onto the geography rather than a second geography.</para>
        /// </summary>
        public static IReadOnlyList<Way> Ways()
        {
            var list = new List<Way>
            {
                new Way(ThroughRoadName, GravelSurface, NineMileCreekMainland.ThroughRoad,
                        CarriagewayHalfWidthMetres, RankThroughRoad, true,
                        "the overhead's Route 19 — the road the community is strung along, and the only " +
                        "one that runs the whole length of the region"),

                new Way(WharfRoadName, GravelSurface, NineMileCreekMainland.WharfRoad,
                        CarriagewayHalfWidthMetres, RankWharfRoad, true,
                        "the owner's named road: the town, the neck between the two ponds, onto the spit " +
                        "and into the yard. The pole line already walks this route; now the road under it " +
                        "is drawn"),

                new Way(BarRoadName, DirtSurface, NineMileCreekMainland.BarRoad,
                        DirtRoadHalfWidthMetres, RankBarRoad, true,
                        "the owner's other named road: from the crossing north along the clifftop to the " +
                        "junction. Red dirt, and narrower than the gravel — the difference is the point"),

                new Way(GullyPathName, DirtSurface, NineMileCreekMainland.GullyPath,
                        FootpathHalfWidthMetres, RankGullyPath, true,
                        "nineteen metres of foot tread off the bar road to the head of the Access gully — " +
                        "without it the whole cliff foreshore is scenery you can see and never reach"),

                // ⭐ THE PARK SPUR. Gravel and full carriageway width, because a truck turns off it: a
                // footpath-width spur would be a driveway you cannot actually drive. It gives way to Wharf
                // Road (RankParkSpur < RankWharfRoad) so the through gravel is unbroken at the mouth, and
                // to the park itself, so the pad's corners stay square where the stroke's round end
                // overlaps them.
                new Way(ParkSpurName, GravelSurface, ParkSpurRoute(),
                        CarriagewayHalfWidthMetres, RankParkSpur, true,
                        "the turn-off from Wharf Road into the truck park — the region's first road built " +
                        "for a vehicle to stop rather than to pass through"),
            };

            list.AddRange(TownWalks());
            return list;
        }

        /// <summary>
        /// <b>⚠ THE ONE JUDGMENT CALL IN THIS FILE, flagged for the owner.</b>
        ///
        /// <para><b>There is no sidewalk at Nine Mile Creek and there should not be.</b> Sidewalks are not
        /// in this region's canon, and the reference photographs are of a rural coast — red dirt, gravel,
        /// grass to the road edge, no kerbs. A kerbed footway strung along the through-road would be the
        /// single most wrong thing this pass could add, and it would be wrong in a way that reads as
        /// finished.</para>
        ///
        /// <para><b>What a Maritime village actually has is a crushed-shell walk up to the door.</b> So
        /// each of the five PUBLIC town lots — the harbourmaster (the cod licence), the chandlery (the
        /// rod), the general store, the tavern and the parish hall — gets one short shell walk from the
        /// nearest road up to its own door. Five walks, ~22 m each, 1.5 m wide. The three HOUSES and the
        /// boat shed get nothing: a dooryard is grass and ruts, and paving them would turn a farming coast
        /// into a suburb.</para>
        ///
        /// <para><b>Derived, like everything else.</b> The walk runs from the lot to the NEAREST POINT on
        /// the nearest published carriageway, so moving a lot or re-siting a road moves the walk; and it
        /// ranks below every road (<see cref="RankTownWalk"/>), so where it reaches the carriageway the
        /// road is what is drawn. Nothing here is measured by eye.</para>
        /// </summary>
        public static IReadOnlyList<Way> TownWalks()
        {
            var walks = new List<Way>();
            Vector2[] lots = PublicTownLots;
            string[] names = PublicTownLotNames;

            for (int i = 0; i < lots.Length; i++)
            {
                Vector2 lot = lots[i];
                Vector2 joinsAt = NearestPointOnAnyRoad(lot);

                // ⚠ THE WALK STOPS AT THE DOOR, NOT IN THE PARLOUR. A run all the way to the lot CENTRE
                // would put shell under the building standing on it. Every town lot reserves
                // TownLotRadius (6 m — the village kit's biggest house plus a margin), so stopping the
                // walk on that radius lands it about where a doorstep is, whatever gets built there.
                Vector2 toLot = lot - joinsAt;
                float run = toLot.magnitude;
                if (run <= NineMileCreekMainland.TownLotRadius) continue;   // a lot ON the road wants none
                Vector2 doorstep = lot - toLot / run * NineMileCreekMainland.TownLotRadius;

                walks.Add(new Way(
                    TownWalkNamePrefix + names[i], WalkSurface,
                    new[] { joinsAt, doorstep }, FootpathHalfWidthMetres, RankTownWalk, false,
                    $"a crushed-shell walk from the road up to {names[i]}'s door — the Maritime " +
                    "alternative to a sidewalk, and the only paving in this region that canon does not " +
                    "ask for"));
            }
            return walks;
        }

        /// <summary>
        /// The five town lots a stranger walks INTO — the ones a walk is for. The three houses and the
        /// boat shed in <see cref="NineMileCreekMainland.TownLots"/> are deliberately not here.
        ///
        /// <para>⚠ A PROPERTY, not a <c>static readonly</c> array, and the region's own file-header
        /// warning is why: a static field initialised from ANOTHER type's static fields binds at type-init
        /// time, and a run where this type initialised first would silently take five copies of
        /// <c>Vector3.zero</c> and put the whole town's walks at the origin with nothing failing.</para>
        /// </summary>
        public static Vector2[] PublicTownLots => new[]
        {
            new Vector2(NineMileCreekMainland.HarbourmasterPos.x, NineMileCreekMainland.HarbourmasterPos.y),
            new Vector2(NineMileCreekMainland.ChandleryPos.x,     NineMileCreekMainland.ChandleryPos.y),
            new Vector2(NineMileCreekMainland.GeneralStorePos.x,  NineMileCreekMainland.GeneralStorePos.y),
            new Vector2(NineMileCreekMainland.TavernPos.x,        NineMileCreekMainland.TavernPos.y),
            new Vector2(NineMileCreekMainland.ParishHallPos.x,    NineMileCreekMainland.ParishHallPos.y),
        };

        /// <inheritdoc cref="PublicTownLots"/>
        public static readonly string[] PublicTownLotNames =
        {
            "Harbourmaster", "Chandlery", "GeneralStore", "Tavern", "ParishHall",
        };

        /// <summary>
        /// The paved rectangles: the concrete apron at the winch, and the gravel the buyers' trucks stand
        /// on. Both come out of the wharf doc's own kit table ("Roads, parking, apron"), and both are
        /// derived from wharf geometry rather than drawn on the yard by hand.
        /// </summary>
        public static IReadOnlyList<Pad> Pads() => new[]
        {
            new Pad("WinchApron", ApronSurface, WinchApronArea(), RankApron,
                "the concrete the catch is landed onto — the wharf doc's 'concrete apron at the winch', " +
                "covering the working end of the west wall from the unloading point up past the winch"),

            new Pad("BuyersParking", GravelSurface, ParkingArea(), RankParking,
                "gravel under the buyers' trucks — the wharf doc's 'dirt/gravel for the yard', on the " +
                "authored parking site so the man at his tailgate is standing on something"),

            new Pad(TruckParkName, GravelSurface, TruckParkArea(), RankParking,
                "⭐ the truck park at the east edge of the village — the ground a road vehicle is LEFT " +
                "on. Same rank as the buyers' gravel because it is the same kind of thing; the two are " +
                "210 m apart and never contend for a cell"),
        };

        /// <summary>
        /// The concrete apron: the full 10 m width of the west wall, from a working margin south of the
        /// authored unloading point to the same margin north of the winch — <b>and stopping dead at the
        /// quay deck's south edge</b>, exactly where <see cref="NineMileCreekDressing.ApronFaceNorth"/>
        /// stops the drawn face for the same reason. Past that line you are on the north wall, which is
        /// the mooring wall's deck and not this apron.
        /// </summary>
        public static Rect WinchApronArea()
        {
            Rect apron = NineMileCreekWharf.ApronFootprint();
            Rect quay = NineMileCreekWharf.DeckFootprint();

            float south = Mathf.Min(NineMileCreekMainland.UnloadApronPos.y,
                                    NineMileCreekMainland.WinchPos.y) - ApronWorkingMarginMetres;
            float north = Mathf.Max(NineMileCreekMainland.UnloadApronPos.y,
                                    NineMileCreekMainland.WinchPos.y) + ApronWorkingMarginMetres;

            south = Mathf.Max(south, apron.yMin);
            north = Mathf.Min(north, Mathf.Min(apron.yMax, quay.yMin));

            return Rect.MinMaxRect(apron.xMin, south, apron.xMax, Mathf.Max(north, south));
        }

        /// <summary>How far past the two working points the concrete runs: room to swing a hoist and set a
        /// tote down beyond it, and no more. The apron is a working surface, not a car park.</summary>
        public const float ApronWorkingMarginMetres = 4f;

        /// <summary>
        /// The parking pad: <b>the rectangle that covers the two things it is for</b> — the authored
        /// parking site and the buyer's own tailgate — plus a truck's length of margin round both, clipped
        /// to the spit's made ground so no gravel spills off the fill into the marsh behind it.
        ///
        /// <para>Derived that way rather than given a size, because the size is not the point: the point
        /// is that the man you sell your first clams to is standing on something. Move
        /// <see cref="NineMileCreekMainland.FishBuyerPos"/> and the gravel goes with him.</para>
        /// </summary>
        public static Rect ParkingArea()
        {
            var parking = new Vector2(NineMileCreekMainland.ParkingPos.x,
                                      NineMileCreekMainland.ParkingPos.y);
            var buyer = new Vector2(NineMileCreekMainland.FishBuyerPos.x,
                                    NineMileCreekMainland.FishBuyerPos.y);

            var pad = Rect.MinMaxRect(
                Mathf.Min(parking.x, buyer.x) - ParkingMarginMetres,
                Mathf.Min(parking.y, buyer.y) - ParkingMarginMetres,
                Mathf.Max(parking.x, buyer.x) + ParkingMarginMetres,
                Mathf.Max(parking.y, buyer.y) + ParkingMarginMetres);

            Rect spit = NineMileCreekWharf.FootprintOf(NineMileCreekMainland.SpitFill);
            return Rect.MinMaxRect(Mathf.Max(pad.xMin, spit.xMin), Mathf.Max(pad.yMin, spit.yMin),
                                   Mathf.Min(pad.xMax, spit.xMax), Mathf.Min(pad.yMax, spit.yMax));
        }

        /// <summary>Ground left round the parking sites, in metres — a truck is six metres long and has to
        /// get out again.</summary>
        public const float ParkingMarginMetres = 6f;

        // -------------------------------------------------------------------------------------------------
        //  THE TRUCK PARK — the ground a road vehicle is left on, and the spur that reaches it
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>THE VEHICLE ENVELOPE THIS PARK IS SIZED FOR — declared here, proved elsewhere.</b>
        ///
        /// <para>6.7 m is a one-tonne crew-cab dually with the long box, rounded up from the drop's
        /// measured 6.64 m LOA. It is stated as an ENVELOPE rather than read off a def on purpose: this
        /// file is region geometry and must not take a dependency on the vehicle content that parks here.
        /// The binding in the other direction is a test's job — the vehicle pass asserts that the def that
        /// ships is no longer than this, so a future long-wheelbase truck that outgrows the park fails
        /// loudly instead of hanging its tail over the verge.</para>
        /// </summary>
        public const float ParkedVehicleLengthMetres = 6.7f;

        /// <summary>The envelope's width — 2.8 m, rounded up from 2.72 m over the tow mirrors, which are
        /// the widest thing on a dually and 0.24 m wider than its flares. A park sized to the flares would
        /// be a park you fold the mirrors to use.</summary>
        public const float ParkedVehicleWidthMetres = 2.8f;

        /// <summary>
        /// The truck park: <b>three bays wide and two lengths deep</b>, centred on
        /// <see cref="NineMileCreekMainland.TruckParkPos"/>.
        ///
        /// <para>Both numbers are the vehicle's, not the region's. Three <see cref="ParkedVehicleLengthMetres"/>
        /// across (20.1 m) is three trucks side by side with room to open the doors — the drop's sidecar
        /// measures a front door's swept arc out to 2.07 m from the centreline, a foot wider than the
        /// truck's own flares. Two lengths deep (13.4 m) is one truck parked with a second length to turn in:
        /// a THREE-POINT turn, not a sweep, which is what a 4.3 m wheelbase actually does on a 20 m pad and
        /// what the village would have bulldozed.</para>
        ///
        /// <para>⚠ Unclipped by any fill, unlike <see cref="ParkingArea"/>. The buyers' gravel is clipped to
        /// the spit because it sits on made ground with marsh behind it; this park is on the mainland
        /// plateau at <see cref="NineMileCreekMainland.LandElevation"/>, where there is nothing to spill
        /// off. If the owner walks the park onto softer ground, the dry-ground rule in <see cref="Pave"/>
        /// trims it and <c>NineMileCreekTruckParkTests</c> fails on the first trimmed cell.</para>
        /// </summary>
        public static Rect TruckParkArea()
        {
            var centre = new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                     NineMileCreekMainland.TruckParkPos.y);
            float halfWidth = ParkBaysAcross * ParkedVehicleLengthMetres * 0.5f;
            float halfDepth = ParkLengthsDeep * ParkedVehicleLengthMetres * 0.5f;

            return Rect.MinMaxRect(centre.x - halfWidth, centre.y - halfDepth,
                                   centre.x + halfWidth, centre.y + halfDepth);
        }

        /// <summary>How many vehicles stand side by side. Three, because one is a driveway and the village
        /// has more than one truck.</summary>
        public const float ParkBaysAcross = 3f;

        /// <summary>How many vehicle lengths deep — one to park in and one to turn in.</summary>
        public const float ParkLengthsDeep = 2f;

        /// <summary>
        /// The spur: from the nearest point on the nearest published carriageway <b>to the park's centre</b>.
        ///
        /// <para>Derived exactly the way <see cref="TownWalks"/> derives a front walk, and for the same
        /// reason — re-site the park or re-route Wharf Road and the spur follows, because there is no
        /// second copy of either end. <see cref="NearestPointOnAnyRoad"/> reads only the through-road and
        /// Wharf Road, so a spur can never join itself.</para>
        ///
        /// <para><b>⚠ It runs to the CENTRE, not to the near edge, and that is deliberate.</b> A walk stops
        /// at the doorstep because shell under a house is wrong; gravel under gravel is not. Running to the
        /// centre means the spur meets the pad whatever direction the owner walks the park to — no edge to
        /// clip against, no geometry that breaks when the park moves north of the road instead of south —
        /// and the pad outranks the spur (<see cref="RankParking"/> &gt; <see cref="RankParkSpur"/>), so
        /// every cell inside the park is drawn as park and the stroke's rounded end never rounds off a
        /// corner. <see cref="CentreLineIsContinuous"/> asks only whether a cell is paved, not by whom, so
        /// the spur's centre-line is unbroken end to end.</para>
        /// </summary>
        public static Vector2[] ParkSpurRoute()
        {
            var park = new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                   NineMileCreekMainland.TruckParkPos.y);
            Vector2 joinsAt = NearestPointOnAnyRoad(park);

            // A park sited ON the road wants no spur at all — but a zero-length route would claim no cells
            // and read as a silent omission, so return the degenerate two-point route and let
            // NineMileCreekTruckParkTests.TheSpurReachesTheParkFromARoad say so out loud.
            return new[] { joinsAt, park };
        }

        // =================================================================================================
        //  6. THE RASTER — which square metre carries which surface
        // =================================================================================================

        /// <summary>One paved square metre: the cell, the surface on it, and which way claimed it.</summary>
        public readonly struct PavedCell
        {
            /// <summary>Cell coordinates on the 1 m grid — the tile's own address. Cell (x, y) covers
            /// world [x, x+1) × [y, y+1) and its ground square is centred at (x + ½, y + ½).</summary>
            public readonly Vector2Int Cell;
            public readonly string Surface;
            /// <summary>The way or pad that won this cell, by name — so a hierarchy and a failing test
            /// both say "Wharf Road" rather than "cell 412".</summary>
            public readonly string Way;
            public readonly int Rank;

            public PavedCell(Vector2Int cell, string surface, string way, int rank)
            {
                Cell = cell; Surface = surface; Way = way; Rank = rank;
            }

            /// <summary>The world point the tile's ground square is centred on.</summary>
            public Vector2 Centre => new Vector2(Cell.x + 0.5f, Cell.y + 0.5f);
        }

        /// <summary>What one paving pass produced. <see cref="Trimmed"/> is the honest number: cells the
        /// ways claimed and the water took back.</summary>
        public sealed class Paving
        {
            /// <summary>Every paved cell, in a stable order (south to north, then west to east).</summary>
            public readonly List<PavedCell> Cells = new List<PavedCell>();

            /// <summary>Lookup by cell — what the neighbour mask is built from.</summary>
            public readonly HashSet<Vector2Int> Paved = new HashSet<Vector2Int>();

            /// <summary>Cells a way claimed and the water took back. Legitimately zero on the plan as it
            /// stands — the coast profile holds the fields at their full height right up to the shoreline
            /// and only falls SEAWARD of it, so every published route clears spring high water with room.
            /// The number is reported rather than assumed, because a re-tuned coast is exactly what would
            /// change it.</summary>
            public int Trimmed;

            /// <summary>Cells per surface — the number worth reading when the region stops looking right.</summary>
            public readonly Dictionary<string, int> PerSurface = new Dictionary<string, int>();

            public bool IsPaved(int x, int y) => Paved.Contains(new Vector2Int(x, y));

            public string SurfaceSummary()
            {
                var parts = new List<string>();
                foreach (var kv in PerSurface) parts.Add($"{kv.Key} {kv.Value:N0}");
                parts.Sort();
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Pave the region. Pure apart from the terrain read, and the terrain is the one the builder
        /// pushes into the scene (<see cref="NineMileCreekMainland.ConfigureTerrain"/>), so a test can
        /// never assert a road the scene does not carry.
        ///
        /// <para><b>The rule, in one line:</b> a cell is paved by the HIGHEST-RANKED way or pad whose
        /// shape covers its centre, unless the ground there is at or below spring high water.</para>
        ///
        /// <para><paramref name="terrain"/> may be null — the raster then skips the dry-ground rule
        /// entirely and returns the full claimed footprint. That is for the geometry tests, which have
        /// questions about corridors and widths that no terrain is involved in; the builder always passes
        /// the real one.</para>
        /// </summary>
        public static Paving Pave(ITidalTerrain terrain)
        {
            var paving = new Paving();

            // Claim first, filter second — a cell has to know its winner before it is worth asking the
            // terrain about, and the terrain read is by far the expensive half.
            var claims = new Dictionary<Vector2Int, PavedCell>();

            foreach (var way in Ways()) ClaimWay(claims, way);
            foreach (var pad in Pads()) ClaimPad(claims, pad);

            // ⚠ SORTED, not enumerated out of the dictionary. A HashSet/Dictionary walk order is not
            // guaranteed across runtimes, and the builder writes these into a committed scene: an
            // unsorted order would produce a different-but-equivalent .unity on every machine and every
            // rebuild would read as a real diff.
            var keys = new List<Vector2Int>(claims.Keys);
            keys.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            float highWater = NineMileCreekMainland.SpringHighWater;
            foreach (var key in keys)
            {
                PavedCell cell = claims[key];
                if (terrain != null && terrain.ElevationAt(cell.Centre) <= highWater)
                {
                    paving.Trimmed++;
                    continue;
                }

                paving.Cells.Add(cell);
                paving.Paved.Add(key);
                paving.PerSurface.TryGetValue(cell.Surface, out int n);
                paving.PerSurface[cell.Surface] = n + 1;
            }

            return paving;
        }

        static void ClaimWay(Dictionary<Vector2Int, PavedCell> claims, Way way)
        {
            if (way.Route == null || way.Route.Length < 2) return;

            foreach (var cell in CellsNear(Bounds(way.Route, way.HalfWidth)))
            {
                var centre = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (NineMileCreekMainland.DistanceToRoute(way.Route, centre) > way.HalfWidth) continue;
                Claim(claims, cell, way.Surface, way.Name, way.Rank);
            }
        }

        static void ClaimPad(Dictionary<Vector2Int, PavedCell> claims, Pad pad)
        {
            if (pad.Area.width <= 0f || pad.Area.height <= 0f) return;

            foreach (var cell in CellsNear(pad.Area))
            {
                var centre = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                if (!pad.Area.Contains(centre)) continue;
                Claim(claims, cell, pad.Surface, pad.Name, pad.Rank);
            }
        }

        /// <summary>Highest rank wins; a tie keeps the sitting tenant, so declaration order is the
        /// tie-break and the result does not depend on which way was walked first by accident.</summary>
        static void Claim(Dictionary<Vector2Int, PavedCell> claims, Vector2Int cell,
                          string surface, string way, int rank)
        {
            if (claims.TryGetValue(cell, out PavedCell sitting) && sitting.Rank >= rank) return;
            claims[cell] = new PavedCell(cell, surface, way, rank);
        }

        /// <summary>The cells of a rectangle, clipped to the region's own rectangle. Nothing may be paved
        /// off the edge of the world — <c>EverythingAuthoredSitsInsideTheRegionsOwnRectangle</c> is a
        /// standing rule here and the through-road runs to both edges.</summary>
        static IEnumerable<Vector2Int> CellsNear(Rect area)
        {
            Rect region = RegionRect();
            int minX = Mathf.Max(Mathf.FloorToInt(area.xMin), Mathf.FloorToInt(region.xMin));
            int maxX = Mathf.Min(Mathf.CeilToInt(area.xMax), Mathf.CeilToInt(region.xMax) - 1);
            int minY = Mathf.Max(Mathf.FloorToInt(area.yMin), Mathf.FloorToInt(region.yMin));
            int maxY = Mathf.Min(Mathf.CeilToInt(area.yMax), Mathf.CeilToInt(region.yMax) - 1);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                yield return new Vector2Int(x, y);
        }

        /// <summary>
        /// The cell a world point belongs to, clamped into the region's own grid.
        ///
        /// <para><b>⚠ THE CLAMP IS THE POINT, and CI is what found it.</b> The grid covers
        /// <c>[min, max)</c> — cell <c>y</c> owns world <c>[y, y+1)</c> — so a point lying exactly ON the
        /// region's north or east boundary floors to a cell one PAST the last one. The through-road's
        /// last node is <c>(−186, 280)</c>, which is the region's north edge to the metre, so
        /// <see cref="CentreLineIsContinuous"/> read the road's own end as a hole in it. The road was
        /// paved correctly; the arithmetic that asked about it was off by one cell.</para>
        ///
        /// <para>Both the raster and the continuity check go through this, so the cell a road is LAID in
        /// and the cell it is CHECKED in cannot be two different answers.</para>
        /// </summary>
        public static Vector2Int CellOf(Vector2 p)
        {
            Rect region = RegionRect();
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(p.x),
                            Mathf.FloorToInt(region.xMin), Mathf.CeilToInt(region.xMax) - 1),
                Mathf.Clamp(Mathf.FloorToInt(p.y),
                            Mathf.FloorToInt(region.yMin), Mathf.CeilToInt(region.yMax) - 1));
        }

        /// <summary>The region rectangle, from the plan.</summary>
        public static Rect RegionRect() => new Rect(
            NineMileCreekMainland.RegionWorldCenter.x - NineMileCreekMainland.RegionWorldSize.x * 0.5f,
            NineMileCreekMainland.RegionWorldCenter.y - NineMileCreekMainland.RegionWorldSize.y * 0.5f,
            NineMileCreekMainland.RegionWorldSize.x, NineMileCreekMainland.RegionWorldSize.y);

        static Rect Bounds(Vector2[] route, float pad)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in route)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
        }

        // =================================================================================================
        //  7. THE TILE — from a paved neighbourhood to an atlas cell
        // =================================================================================================

        /// <summary>
        /// The raw blob-47 neighbour mask for a cell.
        ///
        /// <para><b>⭐ ANY PAVED NEIGHBOUR COUNTS, NOT ONLY A SAME-SURFACE ONE, and that is a deliberate
        /// reading of the kit.</b> The rig's wiring note splits a cell's neighbours into <c>con</c> (the
        /// same surface continues) and <c>abut</c> (a DIFFERENT paved surface continues — that edge gets a
        /// shared kerb, "no verge, no nibble, no soil gutter"). The committed atlases bake <c>con</c>
        /// only. Given that, masking on same-surface neighbours alone would put a road END, complete with
        /// its soil-spill free edge, on both sides of every junction — the dirt bar road and gravel Wharf
        /// Road would stop dead facing each other at (−16, 92) with a seam of verge between them. Counting
        /// any paved neighbour instead draws both cells as road-continues, so the two surfaces butt flush:
        /// not the shared kerb face a real <c>abut</c> bake would give, but the same intent, and much the
        /// nearer of the two to it. <b>The missing <c>abut</c> bake is a reported art gap, not a defect
        /// here.</b></para>
        ///
        /// <para>Diagonals are passed RAW. <c>RoadKitContract.IndexForMask</c> canonicalises with the
        /// rig's own <c>canon()</c> — clearing any diagonal whose two cardinals are not both set — and
        /// re-deriving that here is exactly the second-implementation mistake the contract exists to
        /// prevent.</para>
        /// </summary>
        public static int MaskAt(Paving paving, int x, int y) =>
            RoadKitV3.Mask(
                n:  paving.IsPaved(x,     y + 1),
                e:  paving.IsPaved(x + 1, y),
                s:  paving.IsPaved(x,     y - 1),
                w:  paving.IsPaved(x - 1, y),
                ne: paving.IsPaved(x + 1, y + 1),
                se: paving.IsPaved(x + 1, y - 1),
                sw: paving.IsPaved(x - 1, y - 1),
                nw: paving.IsPaved(x - 1, y + 1));

        /// <summary>The atlas cell a paved square metre draws from — the contract's answer, never ours.</summary>
        public static int AtlasIndexAt(Paving paving, int x, int y) =>
            RoadKitContract.IndexForMask(MaskAt(paving, x, y));

        // =================================================================================================
        //  8. THE LEGALITY GUARD — a road may taper, but it may not have a hole
        // =================================================================================================

        /// <summary>
        /// Is every cell the route's own centre-line passes through actually paved?
        ///
        /// <para><b>This is the guard, and it is the marsh-pool guard.</b> The dry-ground rule lets a
        /// carriageway lose its outer cells wherever the ground goes under — which is right at the
        /// landing, where the bar road runs down to a beach. It must NOT let a road lose its middle. A
        /// route driven through the marsh pool or across the barachois comes back from
        /// <see cref="Pave"/> in two pieces with the pond-crossing gone, and this returns false with the
        /// first metre that went missing.</para>
        ///
        /// <para>Sampled at a quarter of a metre, not at the nodes: the first draft of the bar road had
        /// dry NODES and ran straight through the marsh pool in between, which is the finding
        /// <c>NineMileCreekMainlandTerrainTests.EveryRoadStaysAboveTheHighestWaterAlongItsWholeLength</c>
        /// was written for. Same lesson, one layer down.</para>
        /// </summary>
        public static bool CentreLineIsContinuous(Paving paving, Way way, out Vector2 firstGap)
        {
            firstGap = Vector2.zero;
            if (way.Route == null || way.Route.Length < 2) return true;

            for (int i = 0; i < way.Route.Length - 1; i++)
            {
                float length = Vector2.Distance(way.Route[i], way.Route[i + 1]);
                int steps = Mathf.Max(2, Mathf.CeilToInt(length * CentreLineSamplesPerMetre));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(way.Route[i], way.Route[i + 1], k / (float)steps);
                    // ⚠ CellOf, not a raw floor — a node lying exactly on the region's closing edge
                    // belongs to the last cell, not to a cell past it. See CellOf.
                    Vector2Int cell = CellOf(p);
                    if (paving.IsPaved(cell.x, cell.y)) continue;
                    firstGap = p;
                    return false;
                }
            }
            return true;
        }

        /// <summary>Samples per metre along a centre-line. Four is finer than a cell, so no cell the line
        /// crosses can be stepped over.</summary>
        public const float CentreLineSamplesPerMetre = 4f;

        // =================================================================================================
        //  9. small pure helpers
        // =================================================================================================

        /// <summary>
        /// The point on the nearest published CARRIAGEWAY to <paramref name="from"/> — the through-road or
        /// Wharf Road, the two roads the town is strung along. What a front walk joins.
        /// </summary>
        public static Vector2 NearestPointOnAnyRoad(Vector2 from)
        {
            Vector2 best = from;
            float bestDistance = float.MaxValue;

            foreach (var route in new[] { NineMileCreekMainland.ThroughRoad,
                                          NineMileCreekMainland.WharfRoad })
            {
                Vector2 p = NearestPointOnRoute(route, from);
                float d = Vector2.Distance(p, from);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = p;
            }
            return best;
        }

        /// <summary>The point on a polyline nearest <paramref name="from"/>. The companion to
        /// <see cref="NineMileCreekMainland.DistanceToRoute"/>, which answers how far but not where.</summary>
        public static Vector2 NearestPointOnRoute(Vector2[] route, Vector2 from)
        {
            if (route == null || route.Length == 0) return from;
            if (route.Length == 1) return route[0];

            Vector2 best = route[0];
            float bestDistance = float.MaxValue;

            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 p = NearestPointOnSegment(route[i], route[i + 1], from);
                float d = Vector2.Distance(p, from);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = p;
            }
            return best;
        }

        static Vector2 NearestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-12f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
            return a + ab * t;
        }
    }
}
#endif
