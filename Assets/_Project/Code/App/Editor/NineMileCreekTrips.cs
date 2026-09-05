#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite — a driver who walks must sort where he is
using HiddenHarbours.Vehicles;            // ParkedVehicle, ScheduledTrip, VehicleTripDef
using HiddenHarbours.World;               // Interactable — the thing the player walks up to and talks to

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE FISH BUYER'S RUN</b> — the creek's first scheduled trip, and the geometry half of it.
    ///
    /// <para><c>design/nine-mile-creek-wharf.md</c>: <i>"the first fish buyers arrive in trucks."</i>
    /// Wendell Arsenault's shipped spot is already described as being <b>at his truck</b> — he has stood
    /// beside a truck that was not there since the day he was placed. This is the truck: she rests in the
    /// village's truck park, she comes down Wharf Road at his hour, and she stands on the buyers' gravel
    /// while he works his stall.</para>
    ///
    /// <para><b>⭐ EVERY POINT HERE IS DERIVED, NONE IS TYPED.</b> The bay at the town end is the one
    /// constant the whole truck park hangs off; the bay at the wharf end is the authored parking site;
    /// the road between them is the published <see cref="NineMileCreekMainland.WharfRoad"/> and the park's
    /// own spur; and the buyer's post is the exact spot <see cref="NineMileCreekPeople"/> already stands
    /// him on, read from there rather than copied. Move the park, re-cut the road or walk the buyer
    /// somewhere else and the trip follows in the same one-line change (#345's lesson).</para>
    ///
    /// <para><b>Where the truck does NOT park: the winch apron.</b> That concrete is a working surface —
    /// <c>NineMileCreekRoads</c> says so in as many words ("The apron is a working surface, not a car
    /// park") — and the region already authors gravel for exactly this truck, derived to cover the
    /// buyer's own tailgate. <see cref="NineMileCreekMainland.ParkingPos"/> is commented "the buyers'
    /// trucks". She stands there.</para>
    ///
    /// <para><b>The hours are NOT here.</b> They are on the <c>VehicleTripDef</c> asset, because they are
    /// the owner's (rule 6) and because the window between them is a gameplay ruling: the buyer is the
    /// first money in the game and he is not at his stall while he is away.</para>
    /// </summary>
    public static class NineMileCreekTrips
    {
        /// <summary>Repo path of the buyer's timetable.</summary>
        public const string FishBuyerTripPath = "Assets/_Project/Data/Vehicles/Trips/FishBuyerRun.asset";

        /// <summary>Which of the creek's people drives her.</summary>
        public const string FishBuyerAssetName = "WendellArsenault";

        /// <summary>The object the trip runs on, so a rebuild can find and replace it.</summary>
        public const string TripRootName = "FishBuyerRun";

        // =============================================================================================
        //  THE TWO BAYS
        // =============================================================================================

        /// <summary>
        /// Where she rests overnight: the truck park, on the one point the whole park derives from. The
        /// same point <see cref="NineMileCreekTruckPark"/> stands her on, so the bay she is placed in and
        /// the bay her timetable rests her in cannot be two different answers.
        /// </summary>
        public static Vector2 HomeBay() =>
            new Vector2(NineMileCreekMainland.TruckParkPos.x, NineMileCreekMainland.TruckParkPos.y);

        /// <summary>Where the park's spur meets a published road — the derived join
        /// <see cref="NineMileCreekRoads.ParkSpurRoute"/> is itself built from.</summary>
        public static Vector2 SpurJoin() => NineMileCreekRoads.NearestPointOnAnyRoad(HomeBay());

        /// <summary>Where she stands at the wharf: the authored parking site, which the region's own
        /// gravel is sized around and which is commented "the buyers' trucks".</summary>
        public static Vector2 WharfBay() =>
            new Vector2(NineMileCreekMainland.ParkingPos.x, NineMileCreekMainland.ParkingPos.y);

        /// <summary>
        /// Where she leaves the carriageway for the gravel: the point on Wharf Road nearest her bay,
        /// backed off along the road by <see cref="ApproachRunMetres"/> so the pull-in is a turn a truck
        /// can take rather than a right-angle hop off the road.
        /// </summary>
        public static Vector2 PullOff()
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            Vector2 bay = WharfBay();
            Vector2 onRoad = NineMileCreekRoads.NearestPointOnRoute(road, bay);
            Vector2 dir = DirectionOn(road, onRoad);
            return onRoad - dir * ApproachRunMetres;
        }

        /// <summary>
        /// How much road she uses to line up for the gravel: two vehicle lengths (13.4 m) — <b>the truck
        /// park's own rule</b>, "one to park in and one to turn in"
        /// (<see cref="NineMileCreekRoads.ParkLengthsDeep"/> × the fleet envelope). Stated in the
        /// vehicle's units rather than the region's, because what makes a pull-in tight is the truck.
        /// </summary>
        public static float ApproachRunMetres =>
            NineMileCreekRoads.ParkLengthsDeep * NineMileCreekRoads.ParkedVehicleLengthMetres;

        // =============================================================================================
        //  THE TWO ROADS
        // =============================================================================================

        /// <summary>
        /// Her road down to the wharf: out of the park on its own spur, east along Wharf Road, off at the
        /// pull-in and onto the buyers' gravel.
        /// </summary>
        public static Vector2[] OutboundRoute()
        {
            var route = new List<Vector2> { HomeBay(), SpurJoin() };
            route.AddRange(AlongWharfRoad(SpurJoin(), PullOff()));
            route.Add(WharfBay());
            return Dedup(route);
        }

        /// <summary>Her road home — the reverse. Its own array rather than a reversed view, because a
        /// one-way pair of streets would be expressible here and a reversed view would not.</summary>
        public static Vector2[] ReturnRoute()
        {
            var route = new List<Vector2> { WharfBay(), PullOff() };
            route.AddRange(AlongWharfRoad(PullOff(), SpurJoin()));   // ends AT the spur join
            route.Add(HomeBay());
            return Dedup(route);
        }

        // =============================================================================================
        //  THE DRIVER'S TWO POSTS
        // =============================================================================================

        /// <summary>
        /// Where the buyer is when he is not at the wharf: beside his own truck in the park, half a
        /// vehicle width plus a pace off her flank, so he is standing on the park's gravel and not
        /// inside the truck.
        /// </summary>
        public static Vector2 ParkPost()
        {
            Vector2 bay = HomeBay();
            Vector2 nose = (SpurJoin() - bay).normalized;                 // she rests nose-out to the spur
            Vector2 flank = new Vector2(nose.y, -nose.x);                 // her starboard side
            return bay + flank * (NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f + PaceMetres);
        }

        /// <summary>A pace clear of the truck's flank — enough that a man is beside her rather than in
        /// her.</summary>
        public const float PaceMetres = 1.5f;

        /// <summary>Which way he is turned in the park: at his truck.</summary>
        public static Vector2 ParkPostFacing() => (HomeBay() - ParkPost()).normalized;

        /// <summary>
        /// Where he stands at the wharf — <b>read from <see cref="NineMileCreekPeople"/>, never
        /// recomputed</b>. It is the spot he already occupies in the shipped scene, so the trip does not
        /// move him: it explains him.
        /// </summary>
        public static Vector2 WharfPost() => NineMileCreekPeople.Named(FishBuyerAssetName).Position;

        /// <summary>Which way he is turned at his stall: out at the quay, the way he was already
        /// placed.</summary>
        public static Vector2 WharfPostFacing()
        {
            Vector2 quay = NineMileCreekWharf.DeckFootprint().center;
            Vector2 post = WharfPost();
            Vector2 d = quay - post;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down;
        }

        // =============================================================================================
        //  THE VERGE — where a village with no car park leaves a truck
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A PULL-OFF BESIDE SOMEBODY.</b> The point on <paramref name="road"/> nearest
        /// <paramref name="besideWhom"/>, stepped out onto the verge on their side and then a truck's
        /// length ALONG the road, so she stands beside their post rather than on top of it.
        ///
        /// <para><b>Why the village parks like this at all.</b> Nine Mile Creek authors exactly three
        /// pieces of vehicle ground — the truck park, the laydown apron and the buyers' gravel — and all
        /// three are at the two ends of the village. The chandlery and the dory yard have neither, which
        /// is true of a real rural settlement: you pull onto the grass outside the shop. Inventing a pad
        /// at each would be paving a farming coast into a suburb, which the region's own walks doc
        /// already refuses.</para>
        ///
        /// <para><b>The offset is MEASURED against passing traffic, not chosen.</b> Half a carriageway
        /// plus half a truck is where her near flank would just touch the road's edge; the clearance
        /// below puts it clear, so a truck keeping to the carriageway passes her without touching. That
        /// matters here because the fish buyer's route runs down the same road the outboard man parks
        /// beside, every morning — and the road fleet still carries no colliders to notice.</para>
        /// </summary>
        public static Vector2 ShoulderBay(Vector2[] road, Vector2 besideWhom)
        {
            Vector2 onRoad = NineMileCreekRoads.NearestPointOnRoute(road, besideWhom);
            Vector2 along = DirectionOn(road, onRoad);
            Vector2 out_ = new Vector2(-along.y, along.x);
            if (Vector2.Dot(out_, besideWhom - onRoad) < 0f) out_ = -out_;
            return onRoad + out_ * ShoulderOffsetMetres
                          + along * NineMileCreekRoads.ParkedVehicleLengthMetres;
        }

        /// <summary>How far off the centre-line a pull-off stands: half a carriageway, half a truck, and
        /// <see cref="PassingClearanceMetres"/> of air.</summary>
        public static float ShoulderOffsetMetres =>
            NineMileCreekRoads.CarriagewayHalfWidthMetres
            + NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f
            + PassingClearanceMetres;

        /// <summary>Air between a parked machine's near flank and the carriageway's edge. Half a metre:
        /// enough that a truck on the road does not clip her, small enough that she is still a vehicle
        /// pulled over rather than one abandoned in a field.</summary>
        public const float PassingClearanceMetres = 0.5f;

        /// <summary>
        /// ⭐ <b>A truck length further along the verge — and it is what keeps her PARALLEL to the road.</b>
        ///
        /// <para>Measured, the expensive way: without this the leg out of a pull-off ran straight at the
        /// road's nearest point, so a machine's parked heading came out perpendicular to it and her 6.7 m
        /// length lay ACROSS the carriageway — her tail 1.05 m from the centre-line, with the fish
        /// buyer's route running through it every morning. `NoTwoOfTheThreeEverOccupyTheSameGround` found
        /// it at 05:20. A leg along the verge before the merge makes the parked heading the road's own,
        /// so her length lies along the road and the offset above is the only thing that has to
        /// clear.</para>
        /// </summary>
        public static Vector2 VergeAhead(Vector2[] road, Vector2 besideWhom)
        {
            Vector2 bay = ShoulderBay(road, besideWhom);
            return bay + DirectionOn(road, bay) * NineMileCreekRoads.ParkedVehicleLengthMetres;
        }

        /// <summary>The point on <paramref name="road"/> a pull-off rejoins it at — where a machine
        /// leaving <see cref="ShoulderBay"/> gets back on the carriageway, measured from the verge point
        /// ahead of her so the merge is a shallow one rather than a right-angle hop.</summary>
        public static Vector2 ShoulderJoin(Vector2[] road, Vector2 besideWhom)
            => NineMileCreekRoads.NearestPointOnRoute(road, VergeAhead(road, besideWhom));

        // =============================================================================================
        //  THE CHANDLER'S FUEL RUN — the town end, mid-morning
        // =============================================================================================

        /// <summary>Repo path of the chandler's timetable.</summary>
        public const string ChandleryTripPath =
            "Assets/_Project/Data/Vehicles/Trips/ChandleryFuelRun.asset";

        /// <summary>Which of the creek's people drives her, and the def she drives.</summary>
        public const string ChandlerAssetName = "ClaudetteBoudreau";

        /// <inheritdoc cref="ChandlerAssetName"/>
        public const string ChandleryVanDefPath = "Assets/_Project/Data/Vehicles/HightopVan.asset";

        /// <summary>The placed van's object name, so a rebuild finds and replaces her.</summary>
        public const string ChandleryVanName = "ChandleryVan";

        /// <summary>Where the storekeeper stands — <b>read from <see cref="NineMileCreekPeople"/></b>, the
        /// spot she already occupies in the shipped scene.</summary>
        public static Vector2 ChandleryPost() => NineMileCreekPeople.Named(ChandlerAssetName).Position;

        /// <summary>Her van, pulled onto the verge of Route 19 at the head of her own shop walk.</summary>
        public static Vector2 ChandleryBay() =>
            ShoulderBay(NineMileCreekMainland.ThroughRoad, ChandleryPost());

        // =============================================================================================
        //  THE OUTBOARD MAN'S FUEL RUN — the wharf end, afternoon
        // =============================================================================================

        /// <summary>Repo path of the outboard man's timetable.</summary>
        public const string OutboardTripPath =
            "Assets/_Project/Data/Vehicles/Trips/OutboardFuelRun.asset";

        /// <summary>Which of the creek's people drives her, and the def she drives.</summary>
        public const string OutboardManAssetName = "HectorBernard";

        /// <inheritdoc cref="OutboardManAssetName"/>
        public const string OutboardTruckDefPath = "Assets/_Project/Data/Vehicles/CaboverBox.asset";

        /// <summary>The placed truck's object name, so a rebuild finds and replaces her.</summary>
        public const string OutboardTruckName = "OutboardMansBox";

        /// <summary>Where the outboard man stands — read from the cast, like every other post here.</summary>
        public static Vector2 DoryYardPost() => NineMileCreekPeople.Named(OutboardManAssetName).Position;

        /// <summary>His box truck, pulled onto Wharf Road's verge beside his own yard.</summary>
        public static Vector2 DoryYardBay() =>
            ShoulderBay(NineMileCreekMainland.WharfRoad, DoryYardPost());

        // =============================================================================================
        //  THE ONE PLACE THEY BOTH DRIVE TO — Route 91
        // =============================================================================================

        /// <summary>
        /// The pump apron's own pull-off on Route 19: where a customer leaves a machine while she fills
        /// cans. <b>One bay, two customers</b> — the village has one filling station and they come at
        /// different hours, which a test proves rather than assumes.
        /// </summary>
        public static Vector2 ForecourtBay() =>
            ShoulderBay(NineMileCreekMainland.ThroughRoad, NineMileCreekStation.Route91ForecourtPos);

        /// <summary>Where a customer stands to work a hose: out from the island's own ground centre
        /// toward the road, so they are at the pump face rather than standing on the kerb. The island is
        /// 7.75 m of kerb with the machines on it — <see cref="AtThePumpsMetres"/> clears it.</summary>
        public static Vector2 ForecourtPost()
        {
            Vector2 island = NineMileCreekStation.Route91ForecourtPos;
            Vector2 toRoad = ForecourtBay() - island;
            return toRoad.sqrMagnitude > 1e-6f
                ? island + toRoad.normalized * AtThePumpsMetres
                : island;
        }

        /// <summary>How far out from the pump island a customer stands. Half the island's own 7.75 m of
        /// kerb, rounded up to a pace: at the hose, not on the concrete the machines are bolted to.</summary>
        public const float AtThePumpsMetres = 4f;

        /// <summary>Which way a customer at the pumps is turned: at the machine they are working.</summary>
        public static Vector2 ForecourtPostFacing()
        {
            Vector2 d = NineMileCreekStation.Route91ForecourtPos - ForecourtPost();
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down;
        }

        /// <summary>The road out to the pumps from a pull-off beside <paramref name="post"/> on
        /// <paramref name="road"/>: onto the carriageway, along it, and off again at the station's own
        /// pull-off. Both extra runs are the same shape from opposite ends of the village.</summary>
        public static Vector2[] ToTheForecourt(Vector2[] road, Vector2 post)
        {
            Vector2 bay = ShoulderBay(road, post);
            Vector2 join = ShoulderJoin(road, post);
            Vector2 pumpsJoin = ShoulderJoin(NineMileCreekMainland.ThroughRoad,
                                             NineMileCreekStation.Route91ForecourtPos);

            var route = new List<Vector2> { bay, VergeAhead(road, post), join };
            if (!ReferenceEquals(road, NineMileCreekMainland.ThroughRoad))
            {
                // From Wharf Road: run west to the junction it dead-ends on, which is Route 19's own
                // node, then continue down Route 19. Two roads, one polyline, joined at the point both
                // publish (NineMileCreekStation.Route91Junction reads it rather than restating it).
                route.AddRange(AlongRoad(road, join, NineMileCreekStation.Route91Junction));
                route.AddRange(AlongRoad(NineMileCreekMainland.ThroughRoad,
                                         NineMileCreekStation.Route91Junction, pumpsJoin));
            }
            else
            {
                route.AddRange(AlongRoad(NineMileCreekMainland.ThroughRoad, join, pumpsJoin));
            }
            // …and off again the same way: down the pumps' own verge, so she comes to rest alongside the
            // road rather than nose-in to it. See VergeAhead.
            route.Add(VergeAhead(NineMileCreekMainland.ThroughRoad,
                                 NineMileCreekStation.Route91ForecourtPos));
            route.Add(ForecourtBay());
            return Dedup(route);
        }

        /// <summary>The way home — the same road, walked back.</summary>
        public static Vector2[] FromTheForecourt(Vector2[] road, Vector2 post)
        {
            var back = new List<Vector2>(ToTheForecourt(road, post));
            back.Reverse();
            return back.ToArray();
        }

        // =============================================================================================
        //  PLACEMENT
        // =============================================================================================

        /// <summary>
        /// Hang the buyer's run on the truck the park already stands, with the buyer the cast already
        /// placed. Returns the component, or null (with a stated warning) when a piece is missing — a
        /// region built before the trip asset imported keeps a parked truck and an anchored buyer, which
        /// is exactly what it had before this existed.
        ///
        /// <para><b>Places, does NOT drive.</b> Nothing here reads a clock; the component does that at
        /// play, off a plan it builds once. Same discipline as the moorage law next door.</para>
        /// </summary>
        public static ScheduledTrip Place(GameObject truck, IReadOnlyList<Interactable> creekPeople)
        {
            var trip = AssetDatabase.LoadAssetAtPath<VehicleTripDef>(FishBuyerTripPath);
            if (trip == null)
            {
                Debug.LogWarning($"[NineMileCreekTrips] No trip asset at {FishBuyerTripPath} — the buyer's " +
                                 "truck stays parked and he stays anchored at his stall, which is the " +
                                 "creek as it was before scheduled trips.");
                return null;
            }

            ParkedVehicle machine = truck != null ? truck.GetComponent<ParkedVehicle>() : null;
            if (machine == null)
            {
                Debug.LogWarning("[NineMileCreekTrips] The truck park stood no machine — no trip. Build " +
                                 "the vehicle (#556) before the region.");
                return null;
            }

            GameObject driver = DriverGameObject(creekPeople);
            if (driver == null)
            {
                Debug.LogWarning($"[NineMileCreekTrips] '{FishBuyerAssetName}' is not in the placed cast — " +
                                 "his NpcDef has not imported. The truck would drive herself, so the trip " +
                                 "is not hung at all.");
                return null;
            }

            // ⭐ A DRIVER WHO WALKS MUST SORT WHERE HE IS. NineMileCreekPeople stands the cast up with a
            // STATIC YSortSprite — correct while nobody walked, and it self-disables at play, so a buyer
            // left static would keep the draw order of the truck park he slept in for the whole of his
            // day at the wharf. One flag, and it is this file's business because it is this file that
            // made him move.
            var sort = driver.GetComponent<YSortSprite>();
            if (sort != null) sort.Dynamic = true;

            var scheduled = truck.GetComponent<ScheduledTrip>();
            if (scheduled == null) scheduled = truck.AddComponent<ScheduledTrip>();

            scheduled.Configure(trip, machine, driver.transform,
                                OutboundRoute(), ReturnRoute(),
                                ParkPost(), ParkPostFacing(),
                                WharfPost(), WharfPostFacing(),
                                driver.GetComponent<SpriteRenderer>(),
                                driver.GetComponent<Interactable>());

            Vector2[] outbound = OutboundRoute();
            Debug.Log($"[NineMileCreekTrips] {trip.DisplayName}: {machine.Vehicle?.DisplayName} rests at " +
                      $"({HomeBay().x:0.#}, {HomeBay().y:0.#}) and runs " +
                      $"{Length(outbound):0.#} m to ({WharfBay().x:0.#}, {WharfBay().y:0.#}) — out at " +
                      $"{trip.OutboundDepartureHour:0.00}, home from {trip.ReturnDepartureHour:0.00}. " +
                      $"{FishBuyerAssetName} drives.");
            return scheduled;
        }

        /// <summary>
        /// ⭐ <b>THE OTHER TWO RUNS</b> — the chandler's fuel run mid-morning and the outboard man's in the
        /// afternoon, both to the one filling station the village has, from opposite ends of it.
        ///
        /// <para><b>Each machine is PLACED here, and that is the one fence this widens.</b> The creek's
        /// only parked machines are the buyer's Dually at the truck park and the laydown's nine, and the
        /// laydown is 105–190 m from the nearest villager — while a <c>ScheduledTrip</c> owns its driver's
        /// position for the whole day, so a driver's post has to be somewhere he can stand for twenty-two
        /// hours. A laydown-origin run would therefore strand its driver in an empty yard. Two more
        /// instances of two SHIPPED defs (no new art, no new asset, no new road) stood on the verge beside
        /// the two villagers who drive them is the smallest thing that works, and it is also what the
        /// village would look like.</para>
        /// </summary>
        public static List<ScheduledTrip> PlaceTownRuns(IReadOnlyList<Interactable> creekPeople)
        {
            var made = new List<ScheduledTrip>(2);

            ScheduledTrip chandler = HangRun(
                creekPeople, ChandlerAssetName, ChandleryTripPath, ChandleryVanDefPath, ChandleryVanName,
                ChandleryBay(), ChandleryPost(),
                ToTheForecourt(NineMileCreekMainland.ThroughRoad, ChandleryPost()),
                FromTheForecourt(NineMileCreekMainland.ThroughRoad, ChandleryPost()));
            if (chandler != null) made.Add(chandler);

            ScheduledTrip outboard = HangRun(
                creekPeople, OutboardManAssetName, OutboardTripPath, OutboardTruckDefPath, OutboardTruckName,
                DoryYardBay(), DoryYardPost(),
                ToTheForecourt(NineMileCreekMainland.WharfRoad, DoryYardPost()),
                FromTheForecourt(NineMileCreekMainland.WharfRoad, DoryYardPost()));
            if (outboard != null) made.Add(outboard);

            return made;
        }

        /// <summary>
        /// Stand one machine on her verge and hang one run on her. Null-tolerant end to end: a missing
        /// def, a missing timetable or an unimported villager each warn and leave the village exactly as
        /// it was, rather than placing half a trip.
        /// </summary>
        static ScheduledTrip HangRun(IReadOnlyList<Interactable> creekPeople, string driverAssetName,
                                     string tripPath, string defPath, string objectName,
                                     Vector2 bay, Vector2 homePost, Vector2[] outbound, Vector2[] home)
        {
            var trip = AssetDatabase.LoadAssetAtPath<VehicleTripDef>(tripPath);
            if (trip == null)
            {
                Debug.LogWarning($"[NineMileCreekTrips] No trip asset at {tripPath} — {driverAssetName} " +
                                 "stays anchored where he always was.");
                return null;
            }

            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(defPath);
            if (def == null)
            {
                Debug.LogWarning($"[NineMileCreekTrips] No vehicle def at {defPath} — {trip.DisplayName} " +
                                 "has nothing to drive. Bake the road fleet before the region.");
                return null;
            }

            GameObject driver = PersonNamed(creekPeople, driverAssetName);
            if (driver == null)
            {
                Debug.LogWarning($"[NineMileCreekTrips] '{driverAssetName}' is not in the placed cast — " +
                                 $"{trip.DisplayName} would drive itself, so it is not hung at all.");
                return null;
            }

            // Nose along the road she will set off down, so the picture the builder saves already matches
            // the first frame the plan poses — the same derivation VehicleTripPlan makes at runtime.
            Vector2 nose = outbound.Length > 1 ? (outbound[1] - outbound[0]).normalized : Vector2.up;

            var go = new GameObject(objectName, typeof(Rigidbody2D));
            go.transform.position = new Vector3(bay.x, bay.y, 0f);
            go.transform.up = new Vector3(nose.x, nose.y, 0f);
            // Serialized gravityScale 0 too: a truck must not fall south through a top-down world.
            go.GetComponent<Rigidbody2D>().gravityScale = 0f;
            var machine = go.AddComponent<ParkedVehicle>();
            machine.Configure(def, drivable: true);

            // A driver who walks must sort where he is — see the note in Place().
            var sort = driver.GetComponent<YSortSprite>();
            if (sort != null) sort.Dynamic = true;

            var scheduled = go.AddComponent<ScheduledTrip>();
            scheduled.Configure(trip, machine, driver.transform, outbound, home,
                                homePost, (bay - homePost).normalized,
                                ForecourtPost(), ForecourtPostFacing(),
                                driver.GetComponent<SpriteRenderer>(),
                                driver.GetComponent<Interactable>());

            Debug.Log($"[NineMileCreekTrips] {trip.DisplayName}: {def.DisplayName} stands at " +
                      $"({bay.x:0.#}, {bay.y:0.#}) and runs {Length(outbound):0.#} m to the Route 91 " +
                      $"pumps — out at {trip.OutboundDepartureHour:0.00}, home from " +
                      $"{trip.ReturnDepartureHour:0.00}. {driverAssetName} drives.");
            return scheduled;
        }

        /// <summary>The buyer's own object, out of the cast the region just stood up — by the
        /// <see cref="Interactable"/>'s GameObject name, which <c>NineMileCreekPeople</c> sets to the
        /// asset stem.</summary>
        static GameObject DriverGameObject(IReadOnlyList<Interactable> creekPeople)
            => PersonNamed(creekPeople, FishBuyerAssetName);

        /// <inheritdoc cref="DriverGameObject"/>
        static GameObject PersonNamed(IReadOnlyList<Interactable> creekPeople, string assetName)
        {
            if (creekPeople == null) return null;
            foreach (Interactable person in creekPeople)
                if (person != null && person.gameObject.name == assetName) return person.gameObject;
            return null;
        }

        /// <summary>Walked length of a route, metres — for the build read-out.</summary>
        public static float Length(Vector2[] route)
        {
            float total = 0f;
            for (int i = 0; route != null && i < route.Length - 1; i++)
                total += Vector2.Distance(route[i], route[i + 1]);
            return total;
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        /// <summary>
        /// The stretch of Wharf Road between two points ON it, as the road's own nodes in the road's own
        /// order, exclusive of the two ends (the caller already has those).
        ///
        /// <para>By distance ALONG the road rather than by index, so it reads the same whichever way she
        /// is going: hand it (west, east) and it counts up, hand it (east, west) and it counts down.</para>
        /// </summary>
        public static IEnumerable<Vector2> AlongWharfRoad(Vector2 from, Vector2 to)
            => AlongRoad(NineMileCreekMainland.WharfRoad, from, to);

        /// <inheritdoc cref="AlongWharfRoad"/>
        public static IEnumerable<Vector2> AlongRoad(Vector2[] road, Vector2 from, Vector2 to)
        {
            float a = DistanceAlong(road, from);
            float b = DistanceAlong(road, to);

            var nodes = new List<Vector2>();
            float run = 0f;
            for (int i = 0; i < road.Length; i++)
            {
                if (i > 0) run += Vector2.Distance(road[i - 1], road[i]);
                float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                if (run > lo && run < hi) nodes.Add(road[i]);
            }
            if (b < a) nodes.Reverse();
            nodes.Add(to);
            return nodes;
        }

        /// <summary>How far along a route a point on (or near) it lies, in metres from the route's
        /// start.</summary>
        public static float DistanceAlong(Vector2[] route, Vector2 at)
        {
            float best = 0f, bestSq = float.MaxValue, run = 0f;
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 p = route[i], q = route[i + 1];
                Vector2 pq = q - p;
                float len2 = pq.sqrMagnitude;
                float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(at - p, pq) / len2);
                Vector2 hit = p + pq * t;
                float d = (hit - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = run + Mathf.Sqrt(len2) * t; }
                run += Mathf.Sqrt(len2);
            }
            return best;
        }

        /// <summary>The road's direction where <paramref name="at"/> stands — the segment whose nearest
        /// point to it is closest, in the order the polyline is published.</summary>
        public static Vector2 DirectionOn(Vector2[] route, Vector2 at)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 p = route[i], q = route[i + 1];
                Vector2 pq = q - p;
                float len2 = pq.sqrMagnitude;
                float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(at - p, pq) / len2);
                float d = (p + pq * t - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            return (route[best + 1] - route[best]).normalized;
        }

        /// <summary>Drop repeated points. A zero-length segment is a division waiting to happen in the
        /// polyline walkers, and a spur join that lands exactly on a road node produces one.</summary>
        static Vector2[] Dedup(List<Vector2> points)
        {
            var kept = new List<Vector2>(points.Count);
            const float SameSq = 1e-4f;   // 1 cm²
            foreach (Vector2 p in points)
                if (kept.Count == 0 || (kept[kept.Count - 1] - p).sqrMagnitude > SameSq) kept.Add(p);
            return kept.ToArray();
        }
    }
}
#endif
