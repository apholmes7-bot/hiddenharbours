using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Nine Mile Creek's arrival + dock geometry — the owner-playtest "no way to disembark here" fix
    /// (#52), re-derived onto the mainland.
    ///
    /// <para><c>ControlSwitcher.InDockZone()</c> is a pure DISTANCE test, so on arrival the boat must PARK
    /// within that radius of the dock zone or E cannot disembark. That invariant is unchanged by the
    /// recreation and is still the first thing here; what changed is everything it is measured against.
    /// The wharf is no longer an 8 m peninsula poking east into a dredged basin — it is an 84 m wall on a
    /// made spit, and you arrive alongside it rather than nose-on to its tip.</para>
    ///
    /// <para><b>⚠ THE SHORELINE FENCE IS GONE, and its tests with it.</b> The region used to carry a
    /// hand-traced <c>EdgeCollider2D</c> at x = −4 that dipped around the quay, and two tests here
    /// asserted its shape. It existed because a rectangular quay on a flat −6 m floor gave a hull nothing
    /// to ground on. The mainland's coast, beach, spit and quay decks are all authored terrain, so DEPTH
    /// stops the boat — the same read that already stops her on the tidal bar. Testing the fence's corners
    /// would now be testing a thing that is not there; the last two tests below test the mechanism that
    /// replaced it, which is the one that was always doing the real work on St Peters.</para>
    ///
    /// <para><b>NMC H2: the scene's ground is G3.</b> The arrival, the dock zone and the step ashore are berth 0 on G3's
    /// north face (<see cref="NineMileCreekGroundPlan"/>), and every depth here is read off G3
    /// (<see cref="NineMileCreekLiveGround"/>), the ground the scene stands on. The analytic plan's walls are the later
    /// waves' authoring premise and are no longer the quay these claims are about.</para>
    /// </summary>
    public class NineMileCreekDockTests
    {
        private readonly List<Object> _spawned = new();
        private GameObject New(string n) { var g = new GameObject(n); _spawned.Add(g); return g; }
        private Transform At(string n, Vector3 p) { var g = New(n); g.transform.position = p; return g.transform; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private static ITidalTerrain Ground => NineMileCreekLiveGround.Plan;

        // ---- the invariant the bug violated: arrival is within dock range ----------------------

        [Test]
        public void Arrival_IsWithinDockRadius_OfTheDockZone()
        {
            float d = Vector2.Distance(NineMileCreekBuilder.ArrivalPos, NineMileCreekBuilder.DockZonePos);
            Assert.LessOrEqual(d, NineMileCreekBuilder.DockZoneRadius,
                $"the Nine Mile Creek arrival ({NineMileCreekBuilder.ArrivalPos}) must park the boat within " +
                $"the dock radius ({NineMileCreekBuilder.DockZoneRadius} m) of the dock zone " +
                $"({NineMileCreekBuilder.DockZonePos}) — it's {d:0.00} m. Otherwise the player arrives out " +
                "of dock range and can't disembark");
        }

        [Test]
        public void Disembark_RegistersOnArrival_AtTheNineMileCreekWharf()
        {
            // A real rig + an anchor built from the SAME constants the builder writes.
            var playerGo = New("Player");
            var player = playerGo.AddComponent<PlayerWalkController>();      // + Rigidbody2D + SpriteRenderer
            var boatGo = New("Boat");
            var boat = boatGo.AddComponent<BoatController>();                // + Rigidbody2D + CapsuleCollider2D
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test"; hull.CameraWorldHeightMeters = 14f; hull.DraughtMeters = 0.3f;
            _spawned.Add(hull); boat.SetHull(hull);
            var switcher = New("Switcher").AddComponent<ControlSwitcher>();

            // The arrival is laid ALONGSIDE berth 0, bow east, as the builder lays it (NMC H5/H6).
            var anchor = New("NineMileCreekAnchor").AddComponent<RegionAnchor>();
            var arrival = At("Arr", NineMileCreekBuilder.ArrivalPos);
            arrival.localEulerAngles = new Vector3(0f, 0f, -NineMileCreekGroundPlan.BerthHeadingDegrees);
            anchor.Configure("region.nine_mile_creek",
                             arrival,
                             At("Dock", NineMileCreekBuilder.DockZonePos),
                             At("Dis", NineMileCreekBuilder.DisembarkPos));
            anchor.ConfigureAlongsideBerth(NineMileCreekGroundPlan.DesignHalfBeamMetres);

            // Board (the normal travel case is ARRIVING ABOARD), then arrive exactly as
            // RegionTravelCoordinator does on the hop (which re-asserts the aboard mode).
            switcher.Configure(player, boat, input, null, NineMileCreekBuilder.DockZoneRadius, null);
            playerGo.transform.position = boatGo.transform.position;        // step aboard from beside the boat
            Assert.IsTrue(switcher.TryInteract(), "board before travelling");
            Assert.IsTrue(RegionTravelCoordinator.ApplyArrival(player.transform, boatGo.transform, switcher, anchor, null),
                "the region's own arrival is an alongside berth, so she is laid alongside it");

            // An edit-mode rig carries no hull presence (SetHull registers one in play mode only), so she reports
            // no beam and lies as the design boat: on the point itself.
            Assert.AreEqual(NineMileCreekBuilder.ArrivalPos, boatGo.transform.position,
                "the boat is parked at the Nine Mile Creek arrival point on arrival");
            Assert.That(Quaternion.Angle(NineMileCreekGroundPlan.ArrivalRotation, boatGo.transform.rotation),
                Is.LessThan(0.01f), "…heading east along the face, her port side to the fender line");
            Assert.AreEqual(ControlMode.OnDeck, switcher.Mode, "you arrive still aboard — standing on the deck");
            Assert.IsTrue(switcher.InDockZone(),
                "with the boat parked at arrival, the dock zone is in range → E disembarks (the #52 fix)");
            Assert.IsTrue(switcher.CanInteract(),
                "and CanInteract reports the disembark is available the moment you arrive aboard");
        }

        [Test]
        public void DisembarkPoint_IsOnTheQuayDeck_NotInTheBasin()
        {
            // The quay on G3 is the ground behind the north face's cap: the cap spans the face's half-metre raster
            // transition, and the apron behind it is the ground itself (NMC H5).
            Rect cap = NineMileCreekGroundPlan.NorthCap;
            Vector2 p = NineMileCreekBuilder.DisembarkPos;

            Assert.That(p.x, Is.InRange(cap.xMin, cap.xMax),
                $"the disembark spot {p} must land the on-foot player on the north quay's run, x {cap.xMin}..{cap.xMax}");
            Assert.Greater(p.y, cap.yMax,
                $"…and landward of the cap's brow at y {cap.yMax}: stepping ashore into the basin is the bug this " +
                "exists to prevent");
            float ground = Ground.ElevationAt(p);
            Assert.Less(TidalExposure.WaterDepth(NineMileCreekMainland.SpringHighWater, ground), 0f,
                $"the quay at the disembark spot stands at {ground:0.00} m and must be dry at SPRING HIGH");
        }

        [Test]
        public void TheDockZone_IsAgainstTheMooringEdge_WhereTheFleetLies()
        {
            Vector2 dock = NineMileCreekBuilder.DockZonePos;
            float face = NineMileCreekGroundPlan.NorthFaceY;

            Assert.Less(dock.y, face,
                "you come alongside the SOUTH face — that is the wall with the fleet, the bollards and " +
                "the ladder on it, and the only edge the kit gives a tall face to");
            Assert.GreaterOrEqual(dock.y, face - NineMileCreekBuilder.DockZoneRadius,
                "…but against it, not adrift in the basin: the whole dock radius must reach the concrete");
            Assert.That(dock.x, Is.InRange(NineMileCreekGroundPlan.BerthSlotWestX, NineMileCreekGroundPlan.BerthSlotEastX),
                "and in berth 0's own slot on the face, not past the wall's end or in a neighbour's berth");
        }

        // ---- what replaced the fence: the ground itself ------------------------------------------

        [Test]
        public void TheQuayStopsTheBoat_ByDepth_WhichIsWhatTheFenceUsedToFake()
        {
            // The old fence's whole job, done by the ground: a hull cannot swim onto the quay, because G3 behind
            // each face's cap stands above spring high along the face's whole run. The cap and its blocker take
            // the face's own half-metre transition.
            float high = NineMileCreekMainland.SpringHighWater;

            foreach (Rect cap in new[] { NineMileCreekGroundPlan.NorthCap, NineMileCreekGroundPlan.CraneCap })
                for (float x = cap.xMin + 0.25f; x < cap.xMax; x += 0.5f)
                {
                    var p = new Vector2(x, cap.yMax);
                    float ground = Ground.ElevationAt(p);
                    Assert.Less(TidalExposure.WaterDepth(high, ground), 0f,
                        $"the quay at {p} carries {TidalExposure.WaterDepth(high, ground):0.00} m of " +
                        "water at SPRING HIGH — a boat could sail onto the wharf. This is the fence's job now");
                }
        }

        [Test]
        public void TheBasinBesideTheQuay_IsWater_SoTheBoatCanActuallyGetAlongside()
        {
            // The other half of the same claim, and the one that would fail if a fill were made too
            // generous: a wharf you cannot bring a boat to is a sea wall.
            float high = NineMileCreekMainland.SpringHighWater;
            float bed = Ground.ElevationAt(NineMileCreekBuilder.DockZonePos);

            Assert.Greater(TidalExposure.WaterDepth(high, bed), 0.3f,
                $"the dock zone lies over {bed:0.00} m of ground — the start dory could not come " +
                "alongside her own wharf at high water");
        }

        // ---- ⭐ the second door (#456): the bar landing ------------------------------------------

        [Test]
        public void TheRegionAuthorsTheWalkInArrival_AndItIsNotTheWharf()
        {
            Vector3 bar = NineMileCreekBuilder.WalkArrivalPos;
            Vector3 wharf = NineMileCreekBuilder.DisembarkPos;

            Assert.Greater(Vector2.Distance(bar, wharf), 100f,
                $"the two ways in must be genuinely different places — the bar landing at {bar} and the " +
                $"quay at {wharf} are {Vector2.Distance(bar, wharf):0} m apart, which is exactly why the " +
                "per-passage arrival seam had to exist before this region could be built");

            Assert.IsNotEmpty(NineMileCreekBuilder.BarArrivalKey,
                "a nameless arrival cannot be asked for: a blank key means 'the region's own', so the " +
                "walk-in would silently fall back to the wharf");
        }

        [Test]
        public void TheWalkInArrival_IsOnTheBar_AndTheBarBaresToBeWalkedOn()
        {
            // She is a CROSSING, so the ground under the landing has to do both things: carry you at low
            // water and cover at high. A landing that never bares is a landing you can only swim to.
            float ground = Ground.ElevationAt(NineMileCreekBuilder.WalkArrivalPos);

            Assert.IsTrue(TidalExposure.IsExposed(NineMileCreekMainland.SpringLowWater, ground),
                $"the bar at the walk-in arrival stands at {ground:0.00} m and must BARE at spring low, " +
                "or there is nothing to walk in on");
            Assert.IsFalse(TidalExposure.IsExposed(NineMileCreekMainland.SpringHighWater, ground),
                "…and must cover at spring high, or the crossing has no tide window and no teeth");
        }
    }
}
