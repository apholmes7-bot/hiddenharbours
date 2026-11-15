using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// PR 1223 fix passes: the pure decisions behind "the camera stops at the edge, the screen fades as the
    /// mover runs up to it, the neighbour is warm before you reach it, and a hop over the seam does not
    /// re-berth the boat".
    /// <list type="bullet">
    /// <item>the camera clamp, CLOSED on seam sides as on every other (<see cref="CameraBounds.Clamp"/>);</item>
    /// <item>when the seam fade starts (<see cref="SeamCrossingFade.TryLookAhead"/>,
    /// <see cref="SeamCrossingFade.FadeStartInsetMetres"/>) and its sequence (<see cref="SeamCrossingFade"/>);</item>
    /// <item>which neighbour to preload (<see cref="WorldFrame.TryGetApproachingNeighbour"/>);</item>
    /// <item>the berth guard (<see cref="RegionTravelCoordinator.ShouldRememberBoatBerth"/>), and the
    /// registry fact that made the old guard dead;</item>
    /// <item>the re-seat rule on a hop (<c>ControlSwitcher.ShouldReseatOnReassert</c>).</item>
    /// </list>
    ///
    /// <para><b>Why the camera law changed between passes.</b> The first pass left the seam sides OPEN so the
    /// view followed the mover across in world space (its tests pinned that). The owner ruled against it on
    /// the play look: "half of a screen of void reads broken". The view now stops at every region edge,
    /// seams included, and a momentary fade covers the hop; the open-edge clamp and its tests are gone, and
    /// the tests below pin the closed clamp and the fade instead.</para>
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

        // ---- camera clamp: closed on seam sides (owner ruling) --------------------------------------

        static readonly CameraBounds World100x60 = new CameraBounds(Vector2.zero, new Vector2(100f, 60f));
        const float HalfW = 16f, HalfH = 9f;

        [Test]
        public void Clamp_SeamSide_StopsTheViewAtTheEdge_LikeEveryOtherSide()
        {
            // west's east side IS a seam in TwoSideBySide, and the clamp still holds there: past it the view
            // would show the void beyond the region's painted rectangle.
            Assert.That(TwoSideBySide().HasNeighbourAlong("region.west", RegionEdge.East), Is.True, "premise: a seam");
            foreach (float x in new[] { 34f, 40f, 52f, 500f })
            {
                Vector2 c = CameraBounds.Clamp(new Vector2(x, 0f), World100x60, HalfW, HalfH);
                Assert.That(c.x, Is.EqualTo(34f).Within(Eps), $"mover at {x}: the view stops a half-width short");
                Assert.That(c.x + HalfW, Is.LessThanOrEqualTo(50f + Eps), "its right edge never passes the seam");
            }
            Assert.That(CameraBounds.Clamp(new Vector2(-80f, 0f), World100x60, HalfW, HalfH).x, Is.EqualTo(-34f).Within(Eps),
                "the world boundary on the west clamps the same way");
        }

        [Test]
        public void TheHop_NeitherSideShowsVoid_AndTheArrivedViewIsAlreadySettled()
        {
            // The camera before the hop is clamped in west; CarryAcross shifts it by the crossing's local
            // shift and settles it into east's clamp. Both views must lie wholly inside their own region,
            // and the settled view must be the one the follow would rest at (the clamp of the mover).
            WorldFrame f = TwoSideBySide();
            f.TryGet("region.west", out RegionFrame w);
            f.TryGet("region.east", out RegionFrame e);
            var wb = new CameraBounds(w.LocalRect.center, w.LocalRect.size);
            var eb = new CameraBounds(e.LocalRect.center, e.LocalRect.size);
            var moverWest = new Vector2(50.3f, 0f);                                   // past the band, pre-hop
            Vector2 moverEast = WorldFrame.MapAcross(w, e, moverWest);
            Vector2 camWest = CameraBounds.Clamp(moverWest, wb, HalfW, HalfH);
            Vector2 carried = camWest + (moverEast - moverWest);                      // CarryAcross's shift
            Vector2 settled = CameraBounds.Clamp(carried, eb, HalfW, HalfH);          // ... then ApplyBounds

            AssertInside(camWest, w.LocalRect, "before the hop");
            AssertInside(settled, e.LocalRect, "after the hop");
            Assert.That(Vector2.Distance(settled, CameraBounds.Clamp(moverEast, eb, HalfW, HalfH)), Is.LessThan(Eps),
                "already where the follow would rest: no pan after the fade clears");
            Assert.That(carried.x - HalfW, Is.LessThan(e.LocalRect.xMin), "premise: carried but unsettled, it showed void");
        }

        static void AssertInside(Vector2 cam, Rect region, string when)
        {
            Assert.That(cam.x - HalfW, Is.GreaterThanOrEqualTo(region.xMin - Eps), $"{when}: left edge");
            Assert.That(cam.x + HalfW, Is.LessThanOrEqualTo(region.xMax + Eps), $"{when}: right edge");
            Assert.That(cam.y - HalfH, Is.GreaterThanOrEqualTo(region.yMin - Eps), $"{when}: bottom edge");
            Assert.That(cam.y + HalfH, Is.LessThanOrEqualTo(region.yMax + Eps), $"{when}: top edge");
        }

        // ---- when the seam fade starts (the trigger margin) -----------------------------------------

        const float FadeOut = 0.15f, FadeIn = 0.2f, MaxBlack = 1.5f, Band = 0.25f, MinSpeed = 0.1f;

        static bool WouldStart(WorldFrame f, Vector2 pos, Vector2 vel, float lead) =>
            SeamCrossingFade.TryLookAhead(pos, vel, FadeOut, lead, MinSpeed, out Vector2 ahead)
            && f.Resolve("region.west", ahead, Band).Kind == CrossingKind.Cross;

        [Test]
        public void FadeStartInset_IsTheLookAheadLessTheBand()
        {
            Assert.That(SeamCrossingFade.FadeStartInsetMetres(3f, FadeOut, 0.5f, Band), Is.EqualTo(0.7f).Within(Eps),
                "a walker at 3 m/s: 0.45 m of fade-out travel + 0.5 m lead - 0.25 m band");
            Assert.That(SeamCrossingFade.FadeStartInsetMetres(1.4f, FadeOut, 1f, Band), Is.EqualTo(0.96f).Within(Eps),
                "a dory rowed at 1.4 m/s with a 1 m lead");
            Assert.That(SeamCrossingFade.FadeStartInsetMetres(0f, FadeOut, 0f, Band), Is.EqualTo(-Band).Within(Eps),
                "no look-ahead: the old trigger, a band past the edge (off screen)");
        }

        [Test]
        public void LookAhead_StartsTheFadeAtTheInset_InsideTheEdge_NotPastIt()
        {
            // west's east edge is local x = 50. Heading east at 3 m/s with a 0.5 m lead the fade must start
            // with the mover 0.7 m INSIDE the edge (on screen: the view stops at x = 50), not before.
            WorldFrame f = TwoSideBySide();
            var v = new Vector2(3f, 0f);
            float inset = SeamCrossingFade.FadeStartInsetMetres(3f, FadeOut, 0.5f, Band);
            Assert.That(WouldStart(f, new Vector2(50f - inset + 0.01f, 0f), v, 0.5f), Is.True, "just inside the inset: starts");
            Assert.That(WouldStart(f, new Vector2(50f - inset - 0.01f, 0f), v, 0.5f), Is.False, "just short of it: not yet");
            Assert.That(50f - inset, Is.LessThan(50f), "the start point is inside the region, so inside the clamped view");
        }

        [Test]
        public void LookAhead_StandingStill_OrHeadingAway_OrAtAWorldBoundary_StartsNothing()
        {
            WorldFrame f = TwoSideBySide();
            Assert.That(WouldStart(f, new Vector2(49.9f, 0f), Vector2.zero, 0.5f), Is.False, "standing at the seam");
            Assert.That(WouldStart(f, new Vector2(49.9f, 0f), new Vector2(0.05f, 0f), 0.5f), Is.False, "creeping below the least speed");
            Assert.That(WouldStart(f, new Vector2(49.9f, 0f), new Vector2(-3f, 0f), 0.5f), Is.False, "heading back in");
            Assert.That(WouldStart(f, new Vector2(-49.9f, 0f), new Vector2(-3f, 0f), 0.5f), Is.False, "the west side is the world's edge");
            Assert.That(SeamCrossingFade.TryLookAhead(new Vector2(1f, 2f), Vector2.zero, FadeOut, 1f, MinSpeed, out Vector2 a), Is.False);
            Assert.That(a, Is.EqualTo(new Vector2(1f, 2f)), "no look-ahead leaves the position");
        }

        [Test]
        public void LookAhead_TheLeadRunsAlongTheHeading_WhateverTheSpeed()
        {
            Assert.That(SeamCrossingFade.TryLookAhead(Vector2.zero, new Vector2(0f, 2f), FadeOut, 1f, MinSpeed, out Vector2 a), Is.True);
            Assert.That(a.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(a.y, Is.EqualTo(2f * FadeOut + 1f).Within(Eps), "0.3 m of fade-out travel + the 1 m lead, northward");
        }

        // ---- the fade sequence (pure state machine) -------------------------------------------------

        static SeamCrossingFade NewFade()
        {
            var fade = new SeamCrossingFade();
            fade.Configure(FadeOut, FadeIn, MaxBlack);
            return fade;
        }

        [Test]
        public void Fade_FullSequence_OutBlackHopArriveIn_ThenIdle()
        {
            SeamCrossingFade fade = NewFade();
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.That(fade.Hopped(), Is.False, "no hop before the black");

            Assert.That(fade.Begin(), Is.True);
            fade.Tick(FadeOut * 0.5f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Out));
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps), "half-way out, half black");
            Assert.That(fade.ReadyToHop, Is.False);
            fade.Tick(FadeOut * 0.5f + Eps);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Black));
            Assert.That(fade.Alpha, Is.EqualTo(1f));
            Assert.That(fade.ReadyToHop, Is.True, "black: the hop may be made");

            Assert.That(fade.Hopped(), Is.True);
            fade.Tick(10f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Arriving), "held black, however long the scene takes");
            Assert.That(fade.Alpha, Is.EqualTo(1f));
            Assert.That(fade.Begin(), Is.False, "no new fade while arriving");

            Assert.That(fade.Arrived(), Is.True);
            fade.Tick(0.333f);                                                  // the hop frame's delta
            Assert.That(fade.Alpha, Is.EqualTo(1f), "the hop frame's delta is dropped: the fade-in is seen");
            fade.Tick(FadeIn * 0.5f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.In));
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps));
            fade.Tick(FadeIn * 0.5f + Eps);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f), "always ends clear");
        }

        [Test]
        public void Fade_CancelledMidRamp_ClearsFromWhereItGot_AndCanTurnRoundAgain()
        {
            SeamCrossingFade fade = NewFade();
            fade.Begin();
            fade.Tick(FadeOut * 0.4f);
            Assert.That(fade.Cancel(), Is.True, "the mover turned away");
            Assert.That(fade.Alpha, Is.EqualTo(0.4f).Within(Eps), "no jump on the turn");
            fade.Tick(FadeIn * 0.1f);
            Assert.That(fade.Alpha, Is.EqualTo(0.3f).Within(Eps), "clearing at the fade-in rate, no dropped step");
            Assert.That(fade.Begin(), Is.True, "heading out again turns the clear round");
            fade.Tick(FadeOut * 0.1f);
            Assert.That(fade.Alpha, Is.EqualTo(0.4f).Within(Eps), "from where it was, not from black or clear");
        }

        [Test]
        public void Fade_BlackTooLong_GivesUpAndClears()
        {
            SeamCrossingFade fade = NewFade();
            fade.Begin();
            fade.Tick(FadeOut + Eps);
            Assert.That(fade.ReadyToHop, Is.True);
            fade.Tick(MaxBlack * 0.9f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Black), "still waiting within the limit");
            fade.Tick(MaxBlack * 0.2f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.In), "past the limit: the mover stopped, clear");
            Assert.That(fade.TimedOut, Is.True);
            Assert.That(fade.ReadyToHop, Is.False, "and no hop once it has given up");
            fade.Tick(FadeIn + Eps);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Fade_OnlyLegalStepsTake_AndResetAlwaysClears()
        {
            SeamCrossingFade fade = NewFade();
            Assert.That(fade.Arrived(), Is.False, "nothing to arrive from");
            Assert.That(fade.Cancel(), Is.False, "nothing to cancel");
            fade.Begin();
            Assert.That(fade.Begin(), Is.False, "one fade at a time");
            Assert.That(fade.Arrived(), Is.False, "not hopped yet");
            fade.Tick(FadeOut + Eps);
            fade.Hopped();
            Assert.That(fade.Cancel(), Is.True, "a hop that could not be made clears from black");
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.In));
            fade.Reset();
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Fade_ZeroDurations_AreInstant_NeverDivideByZero()
        {
            var fade = new SeamCrossingFade();
            fade.Configure(0f, 0f, 0f);
            fade.Begin();
            Assert.That(fade.ReadyToHop, Is.True, "a zero fade-out is black at once");
            fade.Hopped();
            fade.Arrived();
            fade.Tick(0.016f);                                                  // the dropped step
            fade.Tick(0.016f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            fade.Configure(-1f, -1f, -1f);
            Assert.That(fade.Begin(), Is.True, "negative timings read as zero");
            Assert.That(fade.ReadyToHop, Is.True);
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
