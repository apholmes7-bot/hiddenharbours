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
    /// <item>how dark the seam fade is for how near the mover's leading edge is to the edge (<see cref="SeamCrossingFade.TryApproach"/>,
    /// <see cref="SeamCrossingFade.ApproachAlpha"/>, <see cref="SeamCrossingFade.FadeDistanceFor"/>) and its sequence (<see cref="SeamCrossingFade"/>);</item>
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
    ///
    /// <para><b>Why the fade law changed in the third pass.</b> A fixed-time fade started by a look-ahead was measured
    /// on the play look: a rowed dory had a third of her hull cut by the screen edge while the screen was still
    /// clear, then the black was held up to 0.95 s for her to get across. The darkness now follows the leading
    /// edge's gap to the edge, so it is black as the bow arrives at any speed; the hop is made at once, carrying
    /// the mover on so its trailing edge lands the band inside the neighbour. The look-ahead tests are gone; the
    /// tests below run slow and fast boats, a walker, a turn back and a stop up to the seam.</para>
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

        // ---- where the mover is against the seam (gap, carry, probe) ---------------------------------

        const float FadeOut = 0.15f, FadeIn = 0.2f, MaxBlack = 1.5f, Band = 0.25f, MinSpeed = 0.1f;
        const float FootFade = 0.45f, BoatFade = 1.5f, Dt = 0.02f;
        const string West = "region.west";

        static Rect WestRect(WorldFrame f)
        {
            f.TryGet(West, out RegionFrame w);
            return w.LocalRect;   // x -50..50, y -30..30; its east side is the seam
        }

        [Test]
        public void Approach_ReadsTheLeadingEdgesGap_TheCarry_AndAProbeInTheNeighbour()
        {
            WorldFrame f = TwoSideBySide();
            var body = Rect.MinMaxRect(45f, -1f, 49f, 1f);   // a 4 m hull, bow at x = 49
            Assert.That(SeamCrossingFade.TryApproach(WestRect(f), body, new Vector2(2f, 0.5f), MinSpeed, Band,
                out SeamApproach a), Is.True);
            Assert.That(a.Edge, Is.EqualTo(RegionEdge.East), "the edge it reaches first, not the one it drifts toward");
            Assert.That(a.GapMetres, Is.EqualTo(1f).Within(Eps), "from the BOW to the edge, not from the centre");
            Assert.That(a.CarryMetres, Is.EqualTo(5.25f).Within(Eps), "the stern lands the band past the edge");
            Assert.That(a.Outward, Is.EqualTo(Vector2.right));
            Assert.That(a.OutSpeed, Is.EqualTo(2f).Within(Eps));
            CrossingDecision ahead = f.Resolve(West, a.Probe, 0f);
            Assert.That(ahead.Kind, Is.EqualTo(CrossingKind.Cross), "the probe stands in the neighbour");
            Assert.That(ahead.To.RegionId, Is.EqualTo("region.east"));

            // Near the top, heading mostly north: the north edge is reached first, and it is the world's edge.
            var high = Rect.MinMaxRect(10f, 28f, 12f, 29.5f);
            Assert.That(SeamCrossingFade.TryApproach(WestRect(f), high, new Vector2(2f, 1f), MinSpeed, Band, out a), Is.True);
            Assert.That(a.Edge, Is.EqualTo(RegionEdge.North));
            Assert.That(f.Resolve(West, a.Probe, 0f).Kind, Is.Not.EqualTo(CrossingKind.Cross), "no seam there: no fade");
        }

        [Test]
        public void Approach_StandingStill_Creeping_OrHeadingForTheWorldsEdge_IsNoSeam()
        {
            WorldFrame f = TwoSideBySide();
            var body = Rect.MinMaxRect(48f, -0.5f, 49.5f, 0.5f);
            Assert.That(SeamCrossingFade.TryApproach(WestRect(f), body, Vector2.zero, MinSpeed, Band, out _), Is.False,
                "standing at the seam");
            Assert.That(SeamCrossingFade.TryApproach(WestRect(f), body, new Vector2(0.05f, 0f), MinSpeed, Band, out _), Is.False,
                "creeping below the least speed");
            Assert.That(SeamCrossingFade.TryApproach(WestRect(f), body, new Vector2(-3f, 0f), MinSpeed, Band, out SeamApproach a),
                Is.True, "heading back in reads the WEST edge ...");
            Assert.That(a.Edge, Is.EqualTo(RegionEdge.West));
            Assert.That(f.Resolve(West, a.Probe, 0f).Kind, Is.Not.EqualTo(CrossingKind.Cross), "... which is the world's edge: no fade");
        }

        [Test]
        public void ApproachAlpha_IsClearAtTheFadeDistance_BlackAtTheEdge_LinearBetween()
        {
            Assert.That(SeamCrossingFade.ApproachAlpha(BoatFade, BoatFade), Is.EqualTo(0f).Within(Eps));
            Assert.That(SeamCrossingFade.ApproachAlpha(BoatFade * 2f, BoatFade), Is.EqualTo(0f).Within(Eps));
            Assert.That(SeamCrossingFade.ApproachAlpha(BoatFade * 0.5f, BoatFade), Is.EqualTo(0.5f).Within(Eps));
            Assert.That(SeamCrossingFade.ApproachAlpha(0f, BoatFade), Is.EqualTo(1f).Within(Eps));
            Assert.That(SeamCrossingFade.ApproachAlpha(-0.3f, BoatFade), Is.EqualTo(1f).Within(Eps), "past the edge: black");
            Assert.That(SeamCrossingFade.ApproachAlpha(0.01f, 0f), Is.EqualTo(0f), "a zero distance is a step at the edge");
            Assert.That(SeamCrossingFade.ApproachAlpha(0f, 0f), Is.EqualTo(1f));
            Assert.That(SeamCrossingFade.FadeDistanceFor(BoatFade, 1.06f, FadeOut), Is.EqualTo(BoatFade).Within(Eps),
                "rowing: the tuned distance");
            Assert.That(SeamCrossingFade.FadeDistanceFor(BoatFade, 15f, FadeOut), Is.EqualTo(2.25f).Within(Eps),
                "too fast to cover it in the quickest fade: starts that much further out");
        }

        // ---- the distance-driven fade, run up to the seam step by step ------------------------------

        struct Run
        {
            public float GapAtBlack;        // the leading edge's gap when the screen went black (≤ 0: at/past)
            public float LeastGapWhileClear;// the smallest gap seen while the screen was not yet black
            public float FadeSeconds;       // from the first darkened step to the black one
            public float BlackBeforeHop;    // black held before the hop could be made (the hop is at once)
            public float TrailingAfterCarry;// the trailing edge after the carry (local x)
            public CrossingDecision Crossing;
        }

        // A mover of the given length heading due east along y = 0 at a steady speed, from well short of the
        // fade distance, the way the coordinator steps it: approach, tick, hop at black, move on.
        static Run RunToTheSeam(float speed, float length, float fadeDistance)
        {
            WorldFrame f = TwoSideBySide();
            Rect region = WestRect(f);
            SeamCrossingFade fade = NewFade();
            float bow = region.xMax - SeamCrossingFade.FadeDistanceFor(fadeDistance, speed, FadeOut) - 1f;
            var run = new Run { LeastGapWhileClear = float.MaxValue, FadeSeconds = -1f };
            float darkenedAt = -1f;
            for (int i = 0; i < 100000; i++)
            {
                var body = Rect.MinMaxRect(bow - length, -0.5f, bow, 0.5f);
                Assert.That(SeamCrossingFade.TryApproach(region, body, new Vector2(speed, 0f), MinSpeed, Band,
                    out SeamApproach a), Is.True, "heading out through the seam");
                Assert.That(f.Resolve(West, a.Probe, 0f).Kind, Is.EqualTo(CrossingKind.Cross));
                fade.Approach(SeamCrossingFade.ApproachAlpha(a.GapMetres,
                    SeamCrossingFade.FadeDistanceFor(fadeDistance, a.OutSpeed, FadeOut)));
                fade.Tick(Dt);
                if (fade.Alpha > 0f && darkenedAt < 0f) darkenedAt = i * Dt;
                if (fade.ReadyToHop)
                {
                    run.GapAtBlack = a.GapMetres;
                    run.FadeSeconds = i * Dt - darkenedAt;
                    run.BlackBeforeHop = 0f;              // the coordinator hops on the step it reads black
                    Assert.That(fade.Hopped(), Is.True);
                    run.TrailingAfterCarry = body.xMin + a.CarryMetres;
                    run.Crossing = f.Resolve(West, new Vector2(body.center.x + a.CarryMetres, 0f), Band);
                    return run;
                }
                run.LeastGapWhileClear = Mathf.Min(run.LeastGapWhileClear, a.GapMetres);
                bow += speed * Dt;
            }
            Assert.Fail("never went black");
            return run;
        }

        [Test]
        public void SlowBoat_IsBlackAsHerBowReachesTheEdge_NothingCutBefore_NoWaitingInTheBlack()
        {
            Run r = RunToTheSeam(1.06f, 5f, BoatFade);   // the measured rowing speed, a 5 m drawn dory
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f), "while the screen is not black her bow is short of the edge");
            Assert.That(r.GapAtBlack, Is.LessThanOrEqualTo(0f + Eps).And.GreaterThan(-1.06f * Dt - Eps),
                "black on the step her bow reaches the edge");
            Assert.That(r.FadeSeconds, Is.EqualTo(BoatFade / 1.06f).Within(2f * Dt), "the fade lasts as long as the last 1.5 m");
            Assert.That(r.BlackBeforeHop, Is.LessThan(0.3f), "the hop is made at once: no black held for her to get across");
            Assert.That(r.Crossing.Kind, Is.EqualTo(CrossingKind.Cross), "carried across, she is past the band");
            Assert.That(r.TrailingAfterCarry, Is.EqualTo(50f + Band).Within(Eps), "her stern lands the band inside the neighbour");
            TwoSideBySide().TryGet("region.east", out RegionFrame e);
            Assert.That(r.TrailingAfterCarry + r.Crossing.LocalShift.x, Is.EqualTo(e.LocalRect.xMin + Band).Within(Eps),
                "so the first clear frame on the far side shows all of her");
        }

        [Test]
        public void FastBoat_IsBlackAsHerBowReachesTheEdge_TooFastForTheDistance_StartsFurtherOut()
        {
            Run r = RunToTheSeam(5f, 5f, BoatFade);
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f));
            Assert.That(r.GapAtBlack, Is.LessThanOrEqualTo(Eps).And.GreaterThan(-5f * Dt - Eps));
            Assert.That(r.FadeSeconds, Is.GreaterThanOrEqualTo(FadeOut - Eps), "never quicker than the quickest fade");

            Run jet = RunToTheSeam(15f, 3f, BoatFade);   // past what 1.5 m can fade over in 0.15 s
            Assert.That(jet.LeastGapWhileClear, Is.GreaterThan(0f), "still nothing cut while clear");
            Assert.That(jet.GapAtBlack, Is.GreaterThan(-2f * 15f * Dt - Eps), "black within a step or so of the edge");
            Assert.That(jet.FadeSeconds, Is.EqualTo(FadeOut).Within(2f * Dt), "the quickest fade, started 2.25 m out");
        }

        [Test]
        public void OnFoot_AtAWalk_TheFadeTakesNoLongerThanTheTimedFadeDid()
        {
            Run r = RunToTheSeam(3f, 0.6f, FootFade);   // a walker at 3 m/s, figure + margin 0.6 m across
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f));
            Assert.That(r.GapAtBlack, Is.LessThanOrEqualTo(Eps).And.GreaterThan(-3f * Dt - Eps));
            Assert.That(r.FadeSeconds, Is.LessThanOrEqualTo(FadeOut + Dt + Eps), "0.15 s, as measured on the timed fade");
            Assert.That(r.BlackBeforeHop, Is.LessThanOrEqualTo(0f), "and no black held before the hop");
            Assert.That(r.TrailingAfterCarry, Is.EqualTo(50f + Band).Within(Eps));
        }

        [Test]
        public void TurningBackMidFade_ClearsSmoothlyFromWhereItGot_AndHeadingOutAgainResumes()
        {
            WorldFrame f = TwoSideBySide();
            Rect region = WestRect(f);
            SeamCrossingFade fade = NewFade();
            float gap = BoatFade * 0.5f;                  // half way through the boat's fade distance
            for (int i = 0; i < 200; i++) { fade.Approach(SeamCrossingFade.ApproachAlpha(gap, BoatFade)); fade.Tick(Dt); }
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps), "held half black while she holds there");
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Out));

            // She turns round: heading west, the seam is behind her and the edge ahead is the world's.
            var body = Rect.MinMaxRect(region.xMax - gap - 5f, -0.5f, region.xMax - gap, 0.5f);
            Assert.That(SeamCrossingFade.TryApproach(region, body, new Vector2(-1.06f, 0f), MinSpeed, Band, out SeamApproach a)
                && f.Resolve(West, a.Probe, 0f).Kind == CrossingKind.Cross, Is.False, "premise: no seam ahead now");
            fade.NoApproach();
            fade.Tick(Dt);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f - Dt / FadeIn).Within(Eps), "clearing at the fade-in rate, no jump");
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.In));
            fade.Approach(SeamCrossingFade.ApproachAlpha(gap, BoatFade));
            fade.Tick(Dt);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps), "heading out again picks up from where it was");
            fade.NoApproach();
            for (int i = 0; i < 20; i++) fade.Tick(Dt);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f), "always ends clear");
        }

        [Test]
        public void StationaryAtTheFadeDistance_OrStoppedInsideIt_TheScreenIsOrBecomesClear()
        {
            WorldFrame f = TwoSideBySide();
            Rect region = WestRect(f);
            var atDistance = Rect.MinMaxRect(region.xMax - BoatFade - 5f, -0.5f, region.xMax - BoatFade, 0.5f);
            Assert.That(SeamCrossingFade.TryApproach(region, atDistance, Vector2.zero, MinSpeed, Band, out _), Is.False,
                "standing: no approach");
            SeamCrossingFade fade = NewFade();
            for (int i = 0; i < 100; i++) { fade.NoApproach(); fade.Tick(Dt); }
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));

            // Moving, but exactly at the fade distance: still clear.
            Assert.That(SeamCrossingFade.TryApproach(region, atDistance, new Vector2(1.06f, 0f), MinSpeed, Band,
                out SeamApproach a), Is.True);
            fade.Approach(SeamCrossingFade.ApproachAlpha(a.GapMetres, BoatFade));
            fade.Tick(Dt);
            Assert.That(fade.Alpha, Is.EqualTo(0f), "at the fade distance the screen is still clear");
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));

            // Stopped part way in: it clears, at the fade-in rate, and stays clear.
            for (int i = 0; i < 200; i++) { fade.Approach(SeamCrossingFade.ApproachAlpha(BoatFade * 0.6f, BoatFade)); fade.Tick(Dt); }
            Assert.That(fade.Alpha, Is.EqualTo(0.4f).Within(Eps));
            fade.NoApproach();
            fade.Tick(Dt);
            Assert.That(fade.Alpha, Is.EqualTo(0.4f - Dt / FadeIn).Within(Eps));
            for (int i = 0; i < 100; i++) { fade.NoApproach(); fade.Tick(Dt); }
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
        }

        // ---- the fade sequence (pure state machine) -------------------------------------------------

        static SeamCrossingFade NewFade()
        {
            var fade = new SeamCrossingFade();
            fade.Configure(FadeOut, FadeIn, MaxBlack);
            return fade;
        }

        [Test]
        public void Fade_FullSequence_BlackHopArriveIn_ThenIdle()
        {
            SeamCrossingFade fade = NewFade();
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Hopped(), Is.False, "no hop before the black");

            fade.Approach(1f);                                                  // e.g. teleported past the band
            fade.Tick(FadeOut * 0.5f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Out));
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps), "never quicker than the quickest fade");
            fade.Tick(FadeOut * 0.5f + Eps);
            Assert.That(fade.ReadyToHop, Is.True, "black: the hop may be made");

            Assert.That(fade.Hopped(), Is.True);
            fade.Tick(10f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Arriving), "held black, however long the scene takes");
            Assert.That(fade.Alpha, Is.EqualTo(1f));

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
        public void Fade_AHopThatCannotBeMade_ClearsAndIgnoresTheApproachUntilItIsLostOnce()
        {
            SeamCrossingFade fade = NewFade();
            fade.Approach(1f);
            fade.Tick(FadeOut + Eps);
            Assert.That(fade.Hopped(), Is.True);
            Assert.That(fade.Cancel(), Is.True, "nothing to cross into: clear from black");
            Assert.That(fade.GaveUp, Is.True);
            fade.Approach(1f);                                                  // still pushing at the edge
            fade.Tick(FadeIn * 0.5f);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Eps), "clearing, not fading back to black");
            Assert.That(fade.Cancel(), Is.True, "a second give-up keeps it given up");
            Assert.That(fade.GaveUp, Is.True);
            fade.NoApproach();                                                  // let go once
            Assert.That(fade.GaveUp, Is.False);
            fade.Approach(1f);
            fade.Tick(Dt);
            Assert.That(fade.Alpha, Is.GreaterThan(0.5f), "a fresh approach fades again");
        }

        [Test]
        public void Fade_BlackTooLong_GivesUpAndClears()
        {
            SeamCrossingFade fade = NewFade();
            fade.Approach(1f);
            fade.Tick(FadeOut + Eps);
            Assert.That(fade.ReadyToHop, Is.True);
            fade.Tick(MaxBlack * 0.9f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Black), "still waiting within the limit");
            fade.Tick(MaxBlack * 0.2f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.In), "past the limit: clear");
            Assert.That(fade.TimedOut, Is.True);
            Assert.That(fade.GaveUp, Is.True);
            Assert.That(fade.ReadyToHop, Is.False, "and no hop once it has given up");
            fade.Approach(1f);
            fade.Tick(FadeIn + Eps);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Fade_OnlyLegalStepsTake_AndResetAlwaysClears()
        {
            SeamCrossingFade fade = NewFade();
            Assert.That(fade.Arrived(), Is.False, "nothing to arrive from");
            Assert.That(fade.Cancel(), Is.False, "nothing to cancel");
            fade.Approach(0.5f);
            fade.Tick(Dt);
            Assert.That(fade.Hopped(), Is.False, "not black yet");
            Assert.That(fade.Arrived(), Is.False, "not hopped yet");
            fade.Approach(1f);
            fade.Tick(FadeOut + Eps);
            fade.Hopped();
            fade.Reset();
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.That(fade.GaveUp, Is.False);
        }

        [Test]
        public void Fade_ZeroDurations_AreInstant_NeverDivideByZero()
        {
            var fade = new SeamCrossingFade();
            fade.Configure(0f, 0f, 0f);
            fade.Approach(1f);
            fade.Tick(0.016f);
            Assert.That(fade.ReadyToHop, Is.True, "a zero quickest fade follows the target at once");
            fade.Hopped();
            fade.Arrived();
            fade.Tick(0.016f);                                                  // the dropped step
            fade.Tick(0.016f);
            Assert.That(fade.Phase, Is.EqualTo(SeamFadePhase.Idle));
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            fade.Configure(-1f, -1f, -1f);
            fade.Approach(1f);
            fade.Tick(0f);
            Assert.That(fade.ReadyToHop, Is.True, "negative timings read as zero");
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
