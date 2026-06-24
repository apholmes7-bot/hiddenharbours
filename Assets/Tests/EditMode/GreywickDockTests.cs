using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the VS-22 Greywick arrival/dock geometry the GreywickBuilder lays down (the owner-playtest
    /// "no way to disembark in Greywick" fix). ControlSwitcher.InDockZone() is a pure DISTANCE test
    /// (Vector2.Distance(boat, dockZone) &lt;= radius) — the cove's proven pattern — so on a Cove→Greywick
    /// hop the boat must PARK within that radius of the dock zone or E can't disembark. The bug was the
    /// arrival point sitting 4.0 m from the dock zone (radius 3.5 m): just out of range. These tests pin
    /// the invariant at the builder's shared constants AND drive the real switcher through the arrival
    /// bind, so a future re-tune that re-breaks dock range fails here rather than in a manual playtest.
    ///
    /// The wharf was reoriented to face EAST so the crossing reads true (canon: Greywick lies WEST of the
    /// cove — you sail west, arrive from the east, continue west onto the wharf). The disembark invariant
    /// is unchanged: arrival must still park within DockZoneRadius of the dock zone (the #52 fix).
    ///
    /// The collider/visual side (the boat not sailing through the wharf) is play-mode/manual — see the
    /// report's note that the owner must re-run "Hidden Harbours ▸ Build Greywick Scene" and re-test.
    /// </summary>
    public class GreywickDockTests
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
        }

        // ---- the invariant the bug violated: arrival is within dock range ----------------------

        [Test]
        public void Arrival_IsWithinDockRadius_OfTheDockZone()
        {
            float d = Vector2.Distance(GreywickBuilder.ArrivalPos, GreywickBuilder.DockZonePos);
            Assert.LessOrEqual(d, GreywickBuilder.DockZoneRadius,
                $"the Greywick arrival ({GreywickBuilder.ArrivalPos}) must park the boat within the dock " +
                $"radius ({GreywickBuilder.DockZoneRadius} m) of the dock zone ({GreywickBuilder.DockZonePos}) " +
                $"— it's {d:0.00} m. Otherwise the player arrives out of dock range and can't disembark.");
        }

        [Test]
        public void Disembark_RegistersOnArrival_AtTheGreywickWharf()
        {
            // A real rig + a Greywick anchor built from the SAME constants the GreywickBuilder uses.
            var playerGo = New("Player");
            var player = playerGo.AddComponent<PlayerWalkController>();      // + Rigidbody2D + SpriteRenderer
            var boatGo = New("Boat");
            var boat = boatGo.AddComponent<BoatController>();                // + Rigidbody2D + CapsuleCollider2D
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.greywick"; hull.CameraWorldHeightMeters = 14f; hull.DraughtMeters = 0.3f;
            _spawned.Add(hull); boat.SetHull(hull);
            var switcher = New("Switcher").AddComponent<ControlSwitcher>();

            var anchor = New("GreywickAnchor").AddComponent<RegionAnchor>();
            anchor.Configure("region.port_greywick",
                             At("Arr", GreywickBuilder.ArrivalPos),
                             At("Dock", GreywickBuilder.DockZonePos),
                             At("Dis", GreywickBuilder.DisembarkPos));

            // Board (the normal travel case is ARRIVING ABOARD), then arrive in Greywick exactly as
            // RegionTravelCoordinator does on the hop (which re-asserts the aboard mode).
            switcher.Configure(player, boat, input, null, GreywickBuilder.DockZoneRadius, null);
            playerGo.transform.position = boatGo.transform.position;        // step aboard from beside the boat
            Assert.IsTrue(switcher.TryInteract(), "board before travelling");
            RegionTravelCoordinator.ApplyArrival(player.transform, boatGo.transform, switcher, anchor);

            Assert.AreEqual(GreywickBuilder.ArrivalPos, boatGo.transform.position,
                "the boat is parked at the Greywick arrival point on arrival");
            Assert.AreEqual(ControlMode.Aboard, switcher.Mode, "you arrive still aboard");
            Assert.IsTrue(switcher.InDockZone(),
                "with the boat parked at arrival, the Greywick dock zone is in range → E disembarks (the fix)");
            Assert.IsTrue(switcher.CanInteract(),
                "and CanInteract reports the disembark is available the moment you arrive aboard (dock step-off)");
        }

        [Test]
        public void DisembarkPoint_IsOnTheWharfDeck_NotInOpenWater()
        {
            // EAST-facing wharf: the PublicWharf deck is centred (0,0) size (8,6) → x ∈ [-4,4], y ∈ [-3,3]
            // (it pokes east, head at the east tip x=4). The disembark spot must land the on-foot player ON
            // that deck (so they can walk WEST to the Fish Buyer / Shipwright), not in the harbour. (A loose
            // sanity box around the deck; mainly catches a sign/typo regression.)
            var p = GreywickBuilder.DisembarkPos;
            Assert.GreaterOrEqual(p.x, -4f); Assert.LessOrEqual(p.x, 4f);
            Assert.GreaterOrEqual(p.y, -3f); Assert.LessOrEqual(p.y, 3f);
        }

        // ---- the shoreline fence: a closed land/water boundary that leaves a dockable wharf head ---------

        [Test]
        public void Shoreline_DipsAroundTheWharfDeck_LeavingTheHeadDockable()
        {
            var pts = GreywickBuilder.ShorelinePoints;
            Assert.GreaterOrEqual(pts.Length, 4, "the shoreline fence needs enough points to dip around the wharf");

            // EAST-facing wharf: the fence must reach the wharf HEAD (x = the deck's seaward EAST tip = dock
            // zone x) so the boat stops against it to dock — and the head is the EASTERNMOST point of the
            // fence (the deep harbour stays open east of it).
            bool reachesHead = false;
            float headX = float.MinValue;
            foreach (var p in pts) { if (p.x > headX) headX = p.x; if (Mathf.Approximately(p.x, GreywickBuilder.DockZonePos.x)) reachesHead = true; }
            Assert.IsTrue(reachesHead, "the shoreline must trace out to the wharf head (x = dock zone x) so the boat docks against it");
            Assert.AreEqual(GreywickBuilder.DockZonePos.x, headX, 1e-3f, "the head is the easternmost point of the fence (deep harbour open east of it)");

            // The gap across the head must span the deck height (the boat reaches the head between the sides),
            // i.e. there are points at y = ±3 sitting at the head depth (x = headX).
            bool northSide = false, southSide = false;
            foreach (var p in pts)
            {
                if (Mathf.Approximately(p.x, headX) && p.y >=  3f - 1e-3f) northSide = true;
                if (Mathf.Approximately(p.x, headX) && p.y <= -3f + 1e-3f) southSide = true;
            }
            Assert.IsTrue(northSide && southSide, "the fence dips around BOTH sides of the wharf deck (y = ±3) to the head, making it a solid peninsula");
        }
    }
}
