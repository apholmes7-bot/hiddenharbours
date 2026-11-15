using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// PR 1223 fix pass: the three pure decisions behind "a seam is not a wall for the camera, the neighbour
    /// is warm before you reach it, and a hop over the seam does not re-berth the boat".
    /// <list type="bullet">
    /// <item>the camera clamp with SEAM sides left open (<see cref="CameraBounds.Clamp(Vector2, in CameraBounds, float, float, CameraOpenEdges)"/>);</item>
    /// <item>which neighbour to preload (<see cref="WorldFrame.TryGetApproachingNeighbour"/>);</item>
    /// <item>the berth guard (<see cref="RegionTravelCoordinator.ShouldRememberBoatBerth"/>), and the
    /// registry fact that made the old guard dead;</item>
    /// <item>the re-seat rule on a hop (<c>ControlSwitcher.ShouldReseatOnReassert</c>).</item>
    /// </list>
    /// </summary>
    public sealed class SeamCameraAndPreloadTests
    {
        const float Eps = 1e-4f;

        static RegionFrame Frame(string id, Vector2 origin, Vector2 size) =>
            new RegionFrame(id, origin, new Rect(-size * 0.5f, size));

        // west 100x60 at the origin; east 100x80 whose west edge meets west's east edge at world x = 50.
        static WorldFrame TwoSideBySide()
        {
            var frame = new WorldFrame();
            frame.Register(Frame("region.west", Vector2.zero, new Vector2(100f, 60f)));
            frame.Register(Frame("region.east", new Vector2(100f, -10f), new Vector2(100f, 80f)));
            return frame;
        }

        // ---- camera clamp ---------------------------------------------------------------------------

        static readonly CameraBounds World100x60 = new CameraBounds(Vector2.zero, new Vector2(100f, 60f));

        [Test]
        public void Clamp_NoOpenEdges_IsTheOldClamp_BitForBit()
        {
            var probes = new[] { new Vector2(0f, 0f), new Vector2(49f, 29f), new Vector2(-80f, 5f), new Vector2(60f, -40f) };
            foreach (Vector2 p in probes)
                Assert.That(CameraBounds.Clamp(p, World100x60, 16f, 9f, CameraOpenEdges.None),
                            Is.EqualTo(CameraBounds.Clamp(p, World100x60, 16f, 9f)), $"probe {p}");
            Assert.That(CameraBounds.Clamp(new Vector2(60f, 0f), World100x60, 16f, 9f).x, Is.EqualTo(34f).Within(Eps),
                "closed east side: the view stops a half-width short of the edge");
        }

        [Test]
        public void Clamp_OpenEast_FollowsTheMoverPastTheSeam_ButWestStillHolds()
        {
            const CameraOpenEdges east = CameraOpenEdges.East;
            Assert.That(CameraBounds.Clamp(new Vector2(52f, 0f), World100x60, 16f, 9f, east).x, Is.EqualTo(52f).Within(Eps),
                "past the east seam the camera stays on the mover");
            Assert.That(CameraBounds.Clamp(new Vector2(-80f, 0f), World100x60, 16f, 9f, east).x, Is.EqualTo(-34f).Within(Eps),
                "the closed west side keeps its clamp");
            Assert.That(CameraBounds.Clamp(new Vector2(52f, 40f), World100x60, 16f, 9f, east).y, Is.EqualTo(21f).Within(Eps),
                "the other axis is untouched by an east seam");
        }

        [Test]
        public void Clamp_OpenWestAndNorth_DropOnlyTheirOwnLimits()
        {
            const CameraOpenEdges open = CameraOpenEdges.West | CameraOpenEdges.North;
            Vector2 c = CameraBounds.Clamp(new Vector2(-70f, 45f), World100x60, 16f, 9f, open);
            Assert.That(c.x, Is.EqualTo(-70f).Within(Eps));
            Assert.That(c.y, Is.EqualTo(45f).Within(Eps));
            Vector2 d = CameraBounds.Clamp(new Vector2(70f, -45f), World100x60, 16f, 9f, open);
            Assert.That(d.x, Is.EqualTo(34f).Within(Eps), "east still clamps");
            Assert.That(d.y, Is.EqualTo(-21f).Within(Eps), "south still clamps");
        }

        [Test]
        public void Clamp_BothSidesOpen_IsTheMover_AndANarrowClosedRegionStillCentres()
        {
            Assert.That(CameraBounds.Clamp(new Vector2(-90f, 0f), World100x60, 16f, 9f,
                        CameraOpenEdges.West | CameraOpenEdges.East).x, Is.EqualTo(-90f).Within(Eps));
            var narrow = new CameraBounds(new Vector2(5f, 0f), new Vector2(20f, 60f));
            Assert.That(CameraBounds.Clamp(new Vector2(30f, 0f), narrow, 16f, 9f, CameraOpenEdges.None).x,
                        Is.EqualTo(5f).Within(Eps), "view wider than the region: centred, as before");
        }

        [Test]
        public void Clamp_Unbounded_IsTheIdentity_WhateverIsOpen()
        {
            var p = new Vector2(1234f, -567f);
            Assert.That(CameraBounds.Clamp(p, CameraBounds.Unbounded, 16f, 9f, CameraOpenEdges.East), Is.EqualTo(p));
        }

        [Test]
        public void SeamEdges_OpensExactlyTheSharedSides()
        {
            WorldFrame f = TwoSideBySide();
            Assert.That(CameraBounds.SeamEdges(f, "region.west"), Is.EqualTo(CameraOpenEdges.East));
            Assert.That(CameraBounds.SeamEdges(f, "region.east"), Is.EqualTo(CameraOpenEdges.West));
            Assert.That(CameraBounds.SeamEdges(f, "region.nowhere"), Is.EqualTo(CameraOpenEdges.None));
            Assert.That(CameraBounds.SeamEdges(null, "region.west"), Is.EqualTo(CameraOpenEdges.None),
                "no world frame: every side keeps the old clamp");
        }

        [Test]
        public void OpenSeam_TheViewIsContinuousAcrossTheHop()
        {
            // The camera's world x either side of the hop must be the mover's world x: no jump.
            WorldFrame f = TwoSideBySide();
            f.TryGet("region.west", out RegionFrame w);
            f.TryGet("region.east", out RegionFrame e);
            var wb = new CameraBounds(Vector2.zero, new Vector2(100f, 60f));
            var eb = new CameraBounds(Vector2.zero, new Vector2(100f, 80f));
            var moverWestLocal = new Vector2(51.5f, 0f);                              // just past the seam, pre-hop
            Vector2 moverEastLocal = WorldFrame.MapAcross(w, e, moverWestLocal);       // same world point, post-hop
            Vector2 camBefore = w.ToWorld(CameraBounds.Clamp(moverWestLocal, wb, 16f, 9f, CameraBounds.SeamEdges(f, w.RegionId)));
            Vector2 camAfter = e.ToWorld(CameraBounds.Clamp(moverEastLocal, eb, 16f, 9f, CameraBounds.SeamEdges(f, e.RegionId)));
            Assert.That(Vector2.Distance(camBefore, camAfter), Is.LessThan(Eps), $"{camBefore} -> {camAfter}");
            Assert.That(Vector2.Distance(camBefore, w.ToWorld(moverWestLocal)), Is.LessThan(Eps), "on the mover");
        }

        // ---- preload decision -----------------------------------------------------------------------

        [Test]
        public void ApproachingNeighbour_WithinTheDistance_NamesTheNeighbour_OutsideItDoesNot()
        {
            WorldFrame f = TwoSideBySide();
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(15f, 0f), 40f, out RegionFrame n), Is.True);
            Assert.That(n.RegionId, Is.EqualTo("region.east"));
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(5f, 0f), 40f, out _), Is.False,
                "45 m from the seam is beyond 40 m");
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(51f, 0f), 40f, out n), Is.True,
                "inside the hysteresis band past the edge still warms it");
            Assert.That(n.RegionId, Is.EqualTo("region.east"));
        }

        [Test]
        public void ApproachingNeighbour_ZeroOrNegativeDistance_TurnsPreloadingOff()
        {
            WorldFrame f = TwoSideBySide();
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(49.9f, 0f), 0f, out _), Is.False);
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(49.9f, 0f), -1f, out _), Is.False);
        }

        [Test]
        public void ApproachingNeighbour_AWorldBoundary_WarmsNothing()
        {
            WorldFrame f = TwoSideBySide();
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(-45f, 0f), 40f, out _), Is.False, "west edge");
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(0f, 25f), 40f, out _), Is.False, "north edge");
            Assert.That(f.TryGetApproachingNeighbour("region.nowhere", Vector2.zero, 40f, out _), Is.False, "unknown region");
        }

        [Test]
        public void ApproachingNeighbour_APartlySharedEdge_AnswersOnlyAlongItsSharedStretch()
        {
            // east is 80 tall centred at world y = -10, so it spans world y -50..30; west spans -30..30.
            // Make a third, short neighbour north-east to prove the along-edge position decides.
            var f = new WorldFrame();
            f.Register(Frame("region.west", Vector2.zero, new Vector2(100f, 60f)));
            f.Register(Frame("region.ne", new Vector2(75f, 20f), new Vector2(50f, 20f)));   // spans world x 50..100, y 10..30
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(40f, 20f), 40f, out RegionFrame n), Is.True);
            Assert.That(n.RegionId, Is.EqualTo("region.ne"));
            Assert.That(f.TryGetApproachingNeighbour("region.west", new Vector2(40f, -20f), 40f, out _), Is.False,
                "opposite an unshared stretch of the same edge");
        }

        [Test]
        public void ApproachingNeighbour_PicksTheNearestEdge()
        {
            var f = new WorldFrame();
            f.Register(Frame("region.mid", Vector2.zero, new Vector2(100f, 60f)));
            f.Register(Frame("region.east", new Vector2(100f, 0f), new Vector2(100f, 60f)));
            f.Register(Frame("region.north", new Vector2(0f, 60f), new Vector2(100f, 60f)));
            Assert.That(f.TryGetApproachingNeighbour("region.mid", new Vector2(40f, 10f), 40f, out RegionFrame n), Is.True);
            Assert.That(n.RegionId, Is.EqualTo("region.east"), "10 m to the east seam vs 20 m to the north");
            Assert.That(f.TryGetApproachingNeighbour("region.mid", new Vector2(20f, 28f), 40f, out n), Is.True);
            Assert.That(n.RegionId, Is.EqualTo("region.north"), "2 m to the north seam vs 30 m to the east");
        }

        // ---- the berth guard (defect 4) -------------------------------------------------------------

        GameObject _anchorGo, _boatGo;

        [TearDown]
        public void TearDown()
        {
            if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
            if (_boatGo != null) Object.DestroyImmediate(_boatGo);
            GameServices.Reset();
        }

        static void Invoke(RegionAnchor a, string method) =>
            typeof(RegionAnchor).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(a, null);

        [Test]
        public void TheOldGuard_WasDead_AnAnchorIsGoneFromTheRegistryOnceItsRootsSleep()
        {
            // The premise of the fix: ForScene(previous) read AFTER the previous region's roots were switched
            // off always answers null, so "came from a region with no anchor" was always true.
            _anchorGo = new GameObject("c1223-anchor");
            RegionAnchor a = _anchorGo.AddComponent<RegionAnchor>();
            a.Configure("region.test", null, null, null);
            Invoke(a, "OnEnable");
            Assert.That(RegionAnchor.ForScene(_anchorGo.scene), Is.SameAs(a), "read before deactivation: found");
            Invoke(a, "OnDisable");
            Assert.That(RegionAnchor.ForScene(_anchorGo.scene), Is.Null, "read after deactivation: always null");
        }

        [Test]
        public void ShouldRememberBoatBerth_OnlyWhenComingFromARegionWithNoAnchor()
        {
            _anchorGo = new GameObject("c1223-anchor");
            RegionAnchor arrived = _anchorGo.AddComponent<RegionAnchor>();
            RegionAnchor left = arrived;   // any non-null anchor stands for "the region we left had one"
            _boatGo = new GameObject("c1223-boat");
            Transform boat = _boatGo.transform;

            Assert.That(RegionTravelCoordinator.ShouldRememberBoatBerth(arrived, boat, null), Is.True,
                "from an anchor-less scene (boot / greybox): remember where the boat lies");
            Assert.That(RegionTravelCoordinator.ShouldRememberBoatBerth(arrived, boat, left), Is.False,
                "a hop from a real region must not re-berth the boat where the hop dropped it");
            Assert.That(RegionTravelCoordinator.ShouldRememberBoatBerth(null, boat, null), Is.False, "no anchor arrived at");
            Assert.That(RegionTravelCoordinator.ShouldRememberBoatBerth(arrived, null, null), Is.False, "no boat");
        }

        // ---- the rider pop (defect 3) ---------------------------------------------------------------

        [Test]
        public void Reassert_KeepsTheSeat_OnlyWhenTheHopCarriedHimAboard()
        {
            Assert.That(HiddenHarbours.Player.ControlSwitcher.ShouldReseatOnReassert(keepSeat: true, playerRidesBoat: true), Is.False,
                "a boat crossing carried him on his seat: leave him on it");
            Assert.That(HiddenHarbours.Player.ControlSwitcher.ShouldReseatOnReassert(keepSeat: true, playerRidesBoat: false), Is.True,
                "not actually under the boat: seat him");
            Assert.That(HiddenHarbours.Player.ControlSwitcher.ShouldReseatOnReassert(keepSeat: false, playerRidesBoat: true), Is.True,
                "a door or legacy hop placed them apart: re-seat, as before");
        }
    }
}
