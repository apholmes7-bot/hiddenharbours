using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
            var player = New("Player").AddComponent<PlayerWalkController>(); // + Rigidbody2D + SpriteRenderer
            var boatGo = New("Boat");
            var boat = boatGo.AddComponent<BoatController>();                // + Rigidbody2D + CapsuleCollider2D
            var switcher = New("Switcher").AddComponent<ControlSwitcher>();

            var anchor = New("GreywickAnchor").AddComponent<RegionAnchor>();
            anchor.Configure("region.port_greywick",
                             At("Arr", GreywickBuilder.ArrivalPos),
                             At("Dock", GreywickBuilder.DockZonePos),
                             At("Dis", GreywickBuilder.DisembarkPos));

            // Start the switcher pointed elsewhere (radius = the cove/persistent default), then arrive in
            // Greywick exactly as RegionTravelCoordinator does on the hop.
            switcher.Configure(player, boat, null, null, GreywickBuilder.DockZoneRadius, null);
            RegionTravelCoordinator.ApplyArrival(player.transform, boatGo.transform, switcher, anchor);

            Assert.AreEqual(GreywickBuilder.ArrivalPos, boatGo.transform.position,
                "the boat is parked at the Greywick arrival point on arrival");
            Assert.IsTrue(switcher.InDockZone(),
                "with the boat parked at arrival, the Greywick dock zone is in range → E disembarks (the fix)");
            Assert.IsTrue(switcher.CanInteract(),
                "and CanInteract reports the disembark is available the moment you arrive aboard");
        }

        [Test]
        public void DisembarkPoint_IsOnTheWharfDeck_NotInOpenWater()
        {
            // The PublicWharf deck is centred (0,0) size (6,8) → x ∈ [-3,3], y ∈ [-4,4]. The disembark spot
            // must land the on-foot player ON that deck (so they can walk to the Fish Buyer / Shipwright),
            // not in the harbour. (A loose sanity box around the deck; mainly catches a sign/typo regression.)
            var p = GreywickBuilder.DisembarkPos;
            Assert.GreaterOrEqual(p.x, -3f); Assert.LessOrEqual(p.x, 3f);
            Assert.GreaterOrEqual(p.y, -4f); Assert.LessOrEqual(p.y, 4f);
        }

        // ---- the shoreline fence: a closed land/water boundary that leaves a dockable wharf head ---------

        [Test]
        public void Shoreline_DipsAroundTheWharfDeck_LeavingTheHeadDockable()
        {
            var pts = GreywickBuilder.ShorelinePoints;
            Assert.GreaterOrEqual(pts.Length, 4, "the shoreline fence needs enough points to dip around the wharf");

            // The fence must reach the wharf HEAD (y = -4, the deck's seaward edge) so the boat stops against
            // it to dock — and the dock zone sits right there.
            bool reachesHead = false;
            float headY = float.MaxValue;
            foreach (var p in pts) { if (p.y < headY) headY = p.y; if (Mathf.Approximately(p.y, GreywickBuilder.DockZonePos.y)) reachesHead = true; }
            Assert.IsTrue(reachesHead, "the shoreline must trace down to the wharf head (y = dock zone y) so the boat docks against it");
            Assert.AreEqual(GreywickBuilder.DockZonePos.y, headY, 1e-3f, "and the head is the southernmost point of the fence (the deep harbour stays open south of it)");

            // The gap across the head must span the deck width (the boat reaches the head between the sides),
            // i.e. there are points at x = ±3 sitting at the head depth.
            bool westSide = false, eastSide = false;
            foreach (var p in pts)
            {
                if (Mathf.Approximately(p.y, headY) && p.x <= -3f + 1e-3f) westSide = true;
                if (Mathf.Approximately(p.y, headY) && p.x >=  3f - 1e-3f) eastSide = true;
            }
            Assert.IsTrue(westSide && eastSide, "the fence dips down BOTH sides of the wharf deck (x = ±3) to the head, making it a solid peninsula");
        }
    }
}
