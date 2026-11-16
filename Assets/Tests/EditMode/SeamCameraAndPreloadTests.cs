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
    /// <item>which neighbour to preload (<c>WorldFrame.TryGetApproachingNeighbour</c>, gated on the heading);</item>
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
    /// edge's gap to the edge, so it is black as the bow arrives at any speed; the hop is made at once. The
    /// look-ahead tests are gone; the tests below run slow and fast boats, a walker, a turn back and a stop up to
    /// the seam.</para>
    ///
    /// <para><b>Why the carry went in the fifth pass</b> (play look at 62f3ef70f0). The hop used to carry the mover
    /// on under the black so its trailing edge landed the band inside the neighbour (the run tests pinned that):
    /// a dory jumped 5.2 m forward in one frame, a walker 1.1 m. The owner's ruling is that a crossing KEEPS
    /// position, so the carry is gone and the run tests now pin the pivot crossing from where it stands; the
    /// arrival latch and the render-time overlay fit are pinned at the end.</para>
    ///
    /// <para><b>Why the switch moved to the pivot in the sixth pass</b> (second play look at 0f40c43eb6). Switched as
    /// the leading edge met the seam, the unmoved pivot arrived up to 2.1 m outside the new region: a drift back
    /// crossed again, and the clamped view showed only a sliver of the hull. The fade now runs on until the pivot is
    /// at the seam, so the mover lands inside; the run tests pin that. The camera hold of the fifth pass is gone:
    /// the lurch it answered was the probe rendering a larger picture than the batchmode screen it was clamped
    /// for, and does not occur with the two the same size.</para>
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
            public float PivotGapAtBlack;   // the PIVOT's gap when the screen went black (≤ 0: at/past the seam)
            public float LeastGapWhileClear;// the smallest leading-edge gap seen while the cover was still fully off
            public float LeastGapUnderHalf; // the smallest leading-edge gap seen while the cover was under half
            public float FadeSeconds;       // from the first darkened step to the black one
            public float BlackBeforeHop;    // black held before the hop could be made (the hop is at once)
            public float PivotAtHop;        // where the mover's pivot stood when the hop was made (local x)
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
            var run = new Run { LeastGapWhileClear = float.MaxValue, LeastGapUnderHalf = float.MaxValue, FadeSeconds = -1f };
            float darkenedAt = -1f;
            for (int i = 0; i < 100000; i++)
            {
                var body = Rect.MinMaxRect(bow - length, -0.5f, bow, 0.5f);
                Assert.That(SeamCrossingFade.TryApproach(region, body, new Vector2(speed, 0f), MinSpeed, Band,
                    out SeamApproach a), Is.True, "heading out through the seam");
                Assert.That(f.Resolve(West, a.Probe, 0f).Kind, Is.EqualTo(CrossingKind.Cross));
                // As the coordinator drives it: by the PIVOT's distance, black when the pivot is at the seam.
                float pivotGap = SeamArrivalLatch.DepthInside(region, body.center, a.Edge);
                fade.Approach(SeamCrossingFade.PivotApproachAlpha(a.GapMetres, pivotGap,
                    SeamCrossingFade.FadeDistanceFor(fadeDistance, a.OutSpeed, FadeOut)));
                fade.Tick(Dt);
                if (fade.Alpha > 0f && darkenedAt < 0f) darkenedAt = i * Dt;
                if (fade.ReadyToHop)
                {
                    run.GapAtBlack = a.GapMetres;
                    run.PivotGapAtBlack = pivotGap;
                    run.FadeSeconds = i * Dt - darkenedAt;
                    run.BlackBeforeHop = 0f;              // the coordinator hops on the step it reads black
                    Assert.That(fade.Hopped(), Is.True);
                    // Nothing carries the mover on (a crossing KEEPS position): the coordinator crosses on the
                    // decision read ahead of the leading edge, from where the pivot stands.
                    run.PivotAtHop = body.center.x;
                    run.Crossing = f.Resolve(West, a.Probe, 0f);
                    return run;
                }
                if (fade.Alpha <= 0f) run.LeastGapWhileClear = Mathf.Min(run.LeastGapWhileClear, a.GapMetres);
                if (fade.Alpha < 0.5f) run.LeastGapUnderHalf = Mathf.Min(run.LeastGapUnderHalf, a.GapMetres);
                bow += speed * Dt;
            }
            Assert.Fail("never went black");
            return run;
        }

        [Test]
        public void SlowBoat_IsBlackAsHerPivotReachesTheSeam_AndArrivesInsideTheNewRegion()
        {
            // PR 1223 second play look: switched as her BOW met the seam, her pivot arrived 2.1 m outside the new
            // rect (B, a bounce back) and the clamped view showed a sliver of her (C). She now switches as her
            // PIVOT crosses: black by then, position kept, and she arrives inside by definition.
            Run r = RunToTheSeam(1.06f, 5f, BoatFade);   // the measured rowing speed, a 5 m drawn dory
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f), "the fade starts with her bow still short of the edge");
            Assert.That(r.PivotGapAtBlack, Is.LessThanOrEqualTo(Eps).And.GreaterThan(-1.06f * Dt - Eps),
                "black on the step her pivot reaches the seam");
            Assert.That(r.FadeSeconds, Is.EqualTo((BoatFade + 2.5f) / 1.06f).Within(2f * Dt),
                "the fade lasts from bow 1.5 m short to pivot at the seam");
            Assert.That(r.BlackBeforeHop, Is.LessThan(0.3f), "the hop is made at once: no black held for her to get across");
            Assert.That(r.Crossing.Kind, Is.EqualTo(CrossingKind.Cross));
            WorldFrame f = TwoSideBySide();
            f.TryGet("region.east", out RegionFrame e);
            var arrived = new Vector2(r.PivotAtHop + r.Crossing.LocalShift.x, 0f);
            Assert.That(arrived.x, Is.GreaterThanOrEqualTo(e.LocalRect.xMin - Eps),
                "she KEEPS position and her pivot is already inside the edge she came in by");
            Assert.That(f.Resolve("region.east", arrived, Band).Kind, Is.EqualTo(CrossingKind.Stay),
                "the crossing rule alone keeps her there: no bounce back");
        }

        [Test]
        public void FastBoat_IsBlackAsHerPivotReachesTheSeam_TooFastForTheDistance_StartsFurtherOut()
        {
            Run r = RunToTheSeam(5f, 5f, BoatFade);
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f));
            Assert.That(r.PivotGapAtBlack, Is.LessThanOrEqualTo(Eps).And.GreaterThan(-5f * Dt - Eps));
            Assert.That(r.FadeSeconds, Is.GreaterThanOrEqualTo(FadeOut - Eps), "never quicker than the quickest fade");

            Run jet = RunToTheSeam(15f, 3f, BoatFade);   // past what 1.5 m can fade over in 0.15 s
            Assert.That(jet.LeastGapWhileClear, Is.GreaterThan(0f), "still nothing cut while clear");
            Assert.That(jet.PivotGapAtBlack, Is.GreaterThan(-2f * 15f * Dt - Eps), "black within a step or so of the seam");
            Assert.That(jet.FadeSeconds, Is.EqualTo((2.25f + 1.5f) / 15f).Within(2f * Dt),
                "started 2.25 m out (the quickest fade's distance) and run on half her length");
        }

        [Test]
        public void OnFoot_AtAWalk_BlackAsHeReachesTheSeam_NothingCutBeforeHalfCover()
        {
            Run r = RunToTheSeam(3f, 0.6f, FootFade);   // a walker at 3 m/s, figure + margin 0.6 m across
            Assert.That(r.LeastGapWhileClear, Is.GreaterThan(0f));
            Assert.That(r.LeastGapUnderHalf, Is.GreaterThan(0f), "his half width is under the fade distance: not cut while visible");
            Assert.That(r.PivotGapAtBlack, Is.LessThanOrEqualTo(Eps).And.GreaterThan(-3f * Dt - Eps));
            Assert.That(r.FadeSeconds, Is.EqualTo((FootFade + 0.3f) / 3f).Within(2f * Dt), "a quarter second at a walk");
            Assert.That(r.BlackBeforeHop, Is.LessThanOrEqualTo(0f), "and no black held before the hop");
            Assert.That(r.PivotAtHop, Is.GreaterThanOrEqualTo(50f - Eps), "he crosses where he stands: at the seam");
        }

        [Test]
        public void PivotApproachAlpha_StartsWhereTheLeadingEdgeFadeDid_BlackAtThePivot()
        {
            // a 5 m hull: pivot 2.5 m behind the bow
            Assert.That(SeamCrossingFade.PivotApproachAlpha(BoatFade, BoatFade + 2.5f, BoatFade), Is.EqualTo(0f).Within(Eps),
                "clear with the bow at the fade distance, as before");
            Assert.That(SeamCrossingFade.PivotApproachAlpha(0f, 2.5f, BoatFade), Is.LessThan(1f), "bow at the edge: not yet black");
            Assert.That(SeamCrossingFade.PivotApproachAlpha(-2.5f, 0f, BoatFade), Is.EqualTo(1f).Within(Eps), "pivot at the seam: black");
            Assert.That(SeamCrossingFade.PivotApproachAlpha(-1.25f + BoatFade * 0.5f, 1.25f + BoatFade * 0.5f, BoatFade),
                Is.EqualTo(0.5f).Within(Eps), "linear between");
            Assert.That(SeamCrossingFade.PivotApproachAlpha(0.4f, 0.4f, BoatFade),
                Is.EqualTo(SeamCrossingFade.ApproachAlpha(0.4f, BoatFade)).Within(Eps), "a point mover: the old law");
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
        public void ApproachingNeighbour_WithAHeading_OnlyAMoverHeadingForTheSeamWarmsIt()
        {
            // CI on PR 1223: a player standing at a spawn within the preload distance of a seam loaded the whole
            // neighbour scene. Warming now needs the mover to be heading out through that edge.
            WorldFrame f = TwoSideBySide();
            var near = new Vector2(30f, 0f);   // 20 m from the seam
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, new Vector2(1f, 0f), MinSpeed, 40f,
                out RegionFrame n), Is.True, "heading east for it");
            Assert.That(n.RegionId, Is.EqualTo("region.east"));
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, Vector2.zero, MinSpeed, 40f, out _), Is.False,
                "standing");
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, new Vector2(0.05f, 0f), MinSpeed, 40f, out _), Is.False,
                "creeping below the least speed");
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, new Vector2(-1f, 0f), MinSpeed, 40f, out _), Is.False,
                "heading away");
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, new Vector2(0f, 2f), MinSpeed, 40f, out _), Is.False,
                "running along it");
            Assert.That(f.TryGetApproachingNeighbour("region.west", near, 40f, out _), Is.True,
                "the heading-free overload is unchanged");
        }

        const float PhysicsStep = 0.02f;   // the project's fixed step

        [Test]
        public void ATeleportBesideTheSeam_IsNotAHeading_AndWarmsNothing()
        {
            // The proven cause of the PR 1223 reds (run 43769153746): RicePointPlayablePlayTests.StageStrand puts the
            // walker straight from the spawn (-56, -108.4) on the strand door's mark (366, -96), 14 m inside the
            // seam with Nine Mile Creek. Read as one step's speed that jump "headed" east at ~21 km/s, and the
            // next step preloaded the creek. A jump faster than anything moves is a teleport: zero, and flagged.
            WorldFrame f = TwoSideBySide();
            var spawn = new Vector2(-372f, 0f);
            var staged = new Vector2(36f, 0f);   // 14 m from the seam, as on the strand
            float cap = GameConfig.DefaultRegionMoverMaxPlausibleSpeed;
            float least = GameConfig.DefaultRegionPreloadMinHeadingSpeed;

            Vector2 raw = (staged - spawn) / PhysicsStep;
            Assert.That(f.TryGetApproachingNeighbour("region.west", staged, raw, least, 40f, out _), Is.True,
                "read as speed, the teleport warms the neighbour (the bug)");

            Vector2 v = SeamCrossingFade.PlausibleVelocity(spawn, staged, PhysicsStep, cap, out bool jumped);
            Assert.That(jumped, Is.True);
            Assert.That(v, Is.EqualTo(Vector2.zero));
            Assert.That(f.TryGetApproachingNeighbour("region.west", staged, v, least, 40f, out _), Is.False,
                "a teleport is standing still");

            // Real motion passes untouched: a walk, and a boat at the cap.
            Vector2 walk = SeamCrossingFade.PlausibleVelocity(staged, staged + new Vector2(0.04f, 0f), PhysicsStep, cap, out jumped);
            Assert.That(jumped, Is.False);
            Assert.That(walk.x, Is.EqualTo(2f).Within(1e-4f));
            SeamCrossingFade.PlausibleVelocity(staged, staged + new Vector2(cap * PhysicsStep * 0.99f, 0f), PhysicsStep, cap, out jumped);
            Assert.That(jumped, Is.False, "just under the cap is speed");
            Assert.That(SeamCrossingFade.PlausibleVelocity(spawn, staged, 0f, cap, out jumped), Is.EqualTo(Vector2.zero));
            Assert.That(jumped, Is.False, "no step is no motion, not a teleport");
            Assert.That(SeamCrossingFade.PlausibleVelocity(spawn, staged, PhysicsStep, 0f, out jumped), Is.EqualTo(raw),
                "a cap of 0 keeps every step");
        }

        [Test]
        public void WarmGate_OnlyAHeadingThatHolds_Warms()
        {
            var gate = new NeighbourWarmGate();
            float hold = GameConfig.DefaultRegionPreloadHeadingSeconds;
            int steps = Mathf.CeilToInt(hold / PhysicsStep);

            Assert.That(gate.Step("region.east", PhysicsStep, hold), Is.False, "one step never warms");
            for (int i = 1; i < steps; ++i)
                Assert.That(gate.Step("region.east", PhysicsStep, hold), Is.False, "step " + i);
            Assert.That(gate.Step("region.east", PhysicsStep, hold), Is.True, "held for the heading time");

            gate.Step("region.south", PhysicsStep, hold);
            Assert.That(gate.HeadingSeconds, Is.EqualTo(0f), "another neighbour starts the count again");
            gate.Step("region.south", PhysicsStep, hold);
            gate.Interrupt();
            Assert.That(gate.Step("region.south", PhysicsStep, hold), Is.False, "a teleport starts it again too");
            gate.Step(null, PhysicsStep, hold);
            Assert.That(gate.Step("region.south", PhysicsStep, hold), Is.False, "so does one step of no heading");

            Assert.That(new NeighbourWarmGate().Step("region.east", PhysicsStep, 0f), Is.True, "a hold of 0 warms at once");
        }

        [Test]
        public void WarmGate_TheApproachEnds_OnlyAfterTheReleaseTimeWithNoHeading()
        {
            var gate = new NeighbourWarmGate();
            float release = GameConfig.DefaultRegionPreloadReleaseSeconds;
            int steps = Mathf.RoundToInt(release / PhysicsStep);

            gate.Step("region.east", PhysicsStep, 0f);
            Assert.That(gate.ApproachEnded(release), Is.False, "heading for it");
            for (int i = 0; i < steps - 5; ++i) gate.Step(null, PhysicsStep, 0f);
            Assert.That(gate.ApproachEnded(release), Is.False, "not yet");
            gate.Step("region.east", PhysicsStep, 0f);
            Assert.That(gate.IdleSeconds, Is.EqualTo(0f), "turning back for it restarts the wait");
            for (int i = 0; i < steps + 5; ++i) gate.Step(null, PhysicsStep, 0f);
            Assert.That(gate.ApproachEnded(release), Is.True);
            gate.Reset();
            Assert.That(gate.ApproachEnded(release), Is.False, "an arrival starts from nothing");
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

        // ---- play look at 62f3ef70f0: keep position, re-arm by the mover, hold the view, light the frame ----

        const string East = "region.east";

        static Rect EastRect(WorldFrame f)
        {
            f.TryGet(East, out RegionFrame e);
            return e.LocalRect;   // x -50..50, y -40..40; its west side is the seam
        }

        [Test]
        public void KeepPosition_ACurrentCarryingHerBack_IsHeldForHalfHerLength_OnlyATrueTurnBackCrossesAgain()
        {
            // PR 1223 second play look (noon west rows 4228-4313): she arrived, stopped rowing, and a current
            // carried her back toward the seam at 0.35 m/s; the 0.25 m band alone cannot hold that, and two fades
            // came in 3 s. She now arrives inside the new region (pivot switch), and the latch holds the edge she
            // came in by for the band plus half her length (RegionSeamReturnMoverLengths) before a drift back
            // reads as a turn back.
            WorldFrame f = TwoSideBySide();
            Run r = RunToTheSeam(1.06f, 5f, BoatFade);
            Rect east = EastRect(f);
            var arrived = new Vector2(r.PivotAtHop + r.Crossing.LocalShift.x, 0f);
            Assert.That(f.Resolve(East, arrived, Band).Kind, Is.EqualTo(CrossingKind.Stay), "she arrives inside");
            var drifted = new Vector2(east.xMin - 0.4f, 0f);   // 0.4 m back, as measured
            CrossingDecision back = f.Resolve(East, drifted, Band);
            Assert.That(back.Kind, Is.EqualTo(CrossingKind.Cross), "premise: the band alone sends a 0.4 m drift back across");
            Assert.That(back.Edge, Is.EqualTo(RegionEdge.West));

            const float length = 5f;
            float returnAllowance = length * GameConfig.DefaultRegionSeamReturnMoverLengths;
            var latch = new SeamArrivalLatch();
            Assert.That(SeamArrivalLatch.Opposite(r.Crossing.Edge), Is.EqualTo(RegionEdge.West), "out by east, in by west");
            latch.Arm(east, arrived, SeamArrivalLatch.Opposite(r.Crossing.Edge), 8f, Band, returnAllowance);
            Assert.That(latch.AllowanceMetres, Is.EqualTo(returnAllowance + Band).Within(Eps), "inside: the return allowance + band");
            Assert.That(latch.Step(east, arrived), Is.True);
            Assert.That(latch.HoldsBack(east, arrived, back.Edge), Is.True, "where she arrived is staying");
            Assert.That(latch.HoldsBack(east, drifted, back.Edge), Is.True, "and so is the current's 0.4 m drift back");
            Assert.That(latch.HoldsBack(east, new Vector2(east.xMin - returnAllowance - Band + 0.01f, 0f), back.Edge), Is.True,
                "held to half her length past the seam");
            Assert.That(latch.HoldsBack(east, new Vector2(east.xMin - returnAllowance - Band - 0.1f, 0f), back.Edge), Is.False,
                "a true turn back past that crosses again");
            Assert.That(latch.HoldsBack(east, drifted, RegionEdge.North), Is.False, "only the edge she came in by");

            // The four-argument Arm (no return allowance) keeps the old allowance: overhang + band.
            latch.Arm(east, new Vector2(east.xMin - 1f, 0f), RegionEdge.West, 8f, Band);
            Assert.That(latch.AllowanceMetres, Is.EqualTo(1f + Band).Within(Eps));
            latch.Arm(east, new Vector2(east.xMin - 4f, 0f), RegionEdge.West, 8f, Band, returnAllowance);
            Assert.That(latch.AllowanceMetres, Is.EqualTo(4f + Band).Within(Eps), "an overhang past the allowance is still staying");

            // A walker 0.44 m out, as measured: held the same way.
            var walker = new Vector2(east.xMin - 0.44f, 3f);
            Assert.That(f.Resolve(East, walker, Band).Kind, Is.EqualTo(CrossingKind.Cross));
            latch.Arm(east, walker, RegionEdge.West, 1f, Band);
            Assert.That(latch.HoldsBack(east, walker, RegionEdge.West), Is.True);
            latch.Release();
            Assert.That(latch.HoldsBack(east, walker, RegionEdge.West), Is.False, "released: the rule alone again");
        }

        [Test]
        public void Rearm_ScalesWithTheMover_AndABoatIdlingJustInsideASeamIsNeverUnderACover()
        {
            // Fix 3, the measured case: the dory sat 3.2 m inside Nine Mile Creek's west edge drifting back at
            // 0.16 m/s, and the cover came back on to 0.41. The 0.25 m band is foot scale.
            float lengths = GameConfig.DefaultRegionSeamRearmMoverLengths;
            float least = GameConfig.DefaultRegionSeamRearmMinMetres;
            float dory = 5f + 2f * GameConfig.DefaultRegionSeamBoatDrawnMarginMetres;
            float walker = 0.5f + 2f * GameConfig.DefaultRegionSeamFootDrawnMarginMetres;
            float doryRearm = SeamArrivalLatch.RearmDistance(dory, lengths, least);
            float walkerRearm = SeamArrivalLatch.RearmDistance(walker, lengths, least);
            Assert.That(doryRearm, Is.EqualTo(dory * lengths).Within(Eps), "a boat re-arms in her own lengths");
            Assert.That(doryRearm, Is.GreaterThan(3.2f + dory * 0.5f), "well past where she idled");
            Assert.That(walkerRearm, Is.LessThan(doryRearm), "a walker re-arms far sooner");
            Assert.That(SeamArrivalLatch.RearmDistance(0.1f, lengths, least), Is.EqualTo(least).Within(Eps), "never under the least");
            Assert.That(SeamArrivalLatch.RearmDistance(-1f, -1f, -1f), Is.EqualTo(0f), "negatives read as zero");

            WorldFrame f = TwoSideBySide();
            Rect east = EastRect(f);
            float pivot = east.xMin + 3.2f;
            var body = Rect.MinMaxRect(pivot - dory * 0.5f, -1f, pivot + dory * 0.5f, 1f);
            var drift = new Vector2(-0.16f, 0f);
            Assert.That(SeamCrossingFade.TryApproach(east, body, drift, MinSpeed, Band, out SeamApproach a)
                && f.Resolve(East, a.Probe, 0f).Kind == CrossingKind.Cross
                && SeamCrossingFade.ApproachAlpha(a.GapMetres, BoatFade) > 0f, Is.True,
                "premise: unlatched, the drift back reads as an approach and darkens the screen (the fault)");

            var latch = new SeamArrivalLatch();
            latch.Arm(east, new Vector2(east.xMin - 2.3f, 0f), RegionEdge.West, doryRearm, Band);
            Assert.That(latch.Step(east, new Vector2(pivot, 0f)), Is.True, "3.2 m in: still latched");
            Assert.That(SeamCrossingFade.TryApproach(east, body, drift, MinSpeed, Band, latch.Latched, latch.Edge, out _),
                Is.False, "latched, the edge she came in by drives no fade: the screen stays clear");
            Assert.That(SeamCrossingFade.TryApproach(east, body, new Vector2(0f, 0.5f), MinSpeed, Band, latch.Latched,
                latch.Edge, out a) && a.Edge == RegionEdge.North, Is.True, "every other edge still reads as before");

            Assert.That(latch.Step(east, new Vector2(east.xMin + doryRearm - 0.01f, 0f)), Is.True, "just short: latched");
            Assert.That(latch.Step(east, new Vector2(east.xMin + doryRearm, 0f)), Is.False, "her re-arm distance in: re-armed");
            Assert.That(latch.Step(east, new Vector2(pivot, 0f)), Is.False, "and coming back out does not re-latch it");
            Assert.That(SeamCrossingFade.TryApproach(east, body, drift, MinSpeed, Band, latch.Latched, latch.Edge, out _),
                Is.True, "re-armed, heading for that seam fades as any approach does");
        }

        [Test]
        public void TheNightOverlay_IsFittedToTheCameraForTheFrameItDraws_HoweverFarTheCameraJumped()
        {
            // Fix 4: the first frame of the new region rendered at full daylight under the cover (mean 78 vs 1.8):
            // the camera moved 760 m in the physics step and that frame was drawn before LateUpdate refitted the
            // multiply overlay. It is now refitted as the live camera begins to render.
            var host = new GameObject("c1223-daynight");
            var camGo = new GameObject("c1223-main-cam") { tag = "MainCamera" };
            try
            {
                var dn = host.AddComponent<HiddenHarbours.Art.DayNightController>();
                typeof(HiddenHarbours.Art.DayNightController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(dn, null);
                Assert.That(dn.Overlay, Is.Not.Null, "premise: the overlay quad exists");
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 7.5f;
                camGo.transform.position = new Vector3(365f, 0f, -10f);
                dn.FitOverlayTo(cam);

                camGo.transform.position = new Vector3(365f - 760f, -100f, -10f);   // the seam crossing's shift
                float halfW = cam.orthographicSize * cam.aspect * 4f;               // the shipped oversize
                Assert.That(Mathf.Abs(dn.Overlay.position.x - camGo.transform.position.x), Is.GreaterThan(halfW),
                    "premise: fitted a frame behind, the quad is nowhere near the view (the daylight frame)");

                MethodInfo onRender = typeof(HiddenHarbours.Art.DayNightController).GetMethod("OnBeginCameraRendering",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onRender, Is.Not.Null, "the render-time refit exists");
                Assume.That(Camera.main, Is.SameAs(cam), "this camera is the live one");
                onRender.Invoke(dn, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), cam });
                Assert.That(dn.Overlay.position.x, Is.EqualTo(camGo.transform.position.x).Within(Eps));
                Assert.That(dn.Overlay.position.y, Is.EqualTo(camGo.transform.position.y).Within(Eps),
                    "fitted for the frame being drawn: the night is over the whole view");

                var other = new GameObject("c1223-other-cam").AddComponent<Camera>();
                try
                {
                    other.transform.position = new Vector3(5000f, 0f, -10f);
                    onRender.Invoke(dn, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), other });
                    Assert.That(dn.Overlay.position.x, Is.EqualTo(camGo.transform.position.x).Within(Eps),
                        "another camera's render (a capture, a minimap) does not steal the overlay");
                }
                finally { Object.DestroyImmediate(other.gameObject); }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
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
