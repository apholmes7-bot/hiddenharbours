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

        /// <summary>The buyer's own object, out of the cast the region just stood up — by the
        /// <see cref="Interactable"/>'s GameObject name, which <c>NineMileCreekPeople</c> sets to the
        /// asset stem.</summary>
        static GameObject DriverGameObject(IReadOnlyList<Interactable> creekPeople)
        {
            if (creekPeople == null) return null;
            foreach (Interactable person in creekPeople)
                if (person != null && person.gameObject.name == FishBuyerAssetName) return person.gameObject;
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
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
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
