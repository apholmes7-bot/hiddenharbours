using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// FLICK-CAST maths (Rod Fishing v2 §2.2) — the pure gesture resolver, EditMode-tested like
    /// RodFightMath/TrapHaulMath. Covers: direction comes from the flick vector; DISTANCE tracks how far
    /// you wound back and the aim preview lands where it promised (the owner's 2026-07-23 playtest bug);
    /// the SNAP of the sweep decides how much of that range you deliver; where you release is deliberately
    /// no longer a factor; the cozy floor (a weak cast is a SHORT cast, never nothing); determinism
    /// (identical inputs → bit-identical results); and the degenerate gestures (null/empty/one-point/
    /// zero-length/backwards/NaN) that must resolve to a quiet NoCast, never a throw or a NaN.
    /// </summary>
    public class FlickCastMathTests
    {
        private static readonly Vector2 Anchor = Vector2.zero;

        private static FlickCastSettings S => FlickCastSettings.Default;

        // A clean flick toward +X: wind back 1.4 m, then sweep 2.4 m forward at a brisk pace.
        private static FlickSample[] CleanFlick() => new[]
        {
            new FlickSample(new Vector2(-0.2f, 0f), 0.00f),
            new FlickSample(new Vector2(-0.8f, 0f), 0.10f),
            new FlickSample(new Vector2(-1.4f, 0f), 0.20f),   // the wind-back apex
            new FlickSample(new Vector2(-0.6f, 0f), 0.25f),   // the forward sweep…
            new FlickSample(new Vector2( 0.2f, 0f), 0.30f),
            new FlickSample(new Vector2( 1.0f, 0f), 0.35f),   // release at the sweet point
        };

        private static FlickCastResult Eval(FlickSample[] g, float cap = 12f)
            => FlickCastMath.Evaluate(g, g?.Length ?? 0, Anchor, S, cap);

        // ---- direction ------------------------------------------------------------------------

        [Test]
        public void Direction_IsTheFlickVector()
        {
            var r = Eval(CleanFlick());
            Assert.IsTrue(r.IsCast, "a clean wind-back + sweep casts");
            Assert.AreEqual(1f, r.Direction.x, 1e-4f, "the flick ran +X, so the cast flies +X");
            Assert.AreEqual(0f, r.Direction.y, 1e-4f);
            Assert.Greater(r.LandingPoint.x, Anchor.x, "the line lands out along the flick");
            Assert.AreEqual(r.DistanceMetres, (r.LandingPoint - Anchor).magnitude, 1e-3f,
                "landing point = anchor + direction · distance");
        }

        [Test]
        public void Direction_FollowsACurvedGesture()
        {
            // Wind back to the south-west, sweep out north-east: direction = apex → release, normalised.
            var g = new[]
            {
                new FlickSample(new Vector2(-0.4f, -0.4f), 0.00f),
                new FlickSample(new Vector2(-1.0f, -1.0f), 0.10f),  // apex, 1.41 m behind
                new FlickSample(new Vector2( 0.0f,  0.0f), 0.20f),
                new FlickSample(new Vector2( 0.7f,  0.7f), 0.30f),  // release ~1 m past the anchor
            };
            var r = FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f);
            Assert.IsTrue(r.IsCast);
            Assert.AreEqual(r.Direction.x, r.Direction.y, 1e-4f, "a NE flick flies NE");
            Assert.AreEqual(1f, r.Direction.magnitude, 1e-4f, "direction is a unit vector");
        }

        // ---- range + the cap ------------------------------------------------------------------

        [Test]
        public void Range_IsCapped_AndDistanceNeverExceedsTheCap()
        {
            // An absurdly deep, absurdly fast flick: the range saturates at 1 and distance at the cap.
            var g = new[]
            {
                new FlickSample(new Vector2(-9f, 0f), 0.00f),
                new FlickSample(new Vector2(-4f, 0f), 0.02f),
                new FlickSample(new Vector2( 1f, 0f), 0.04f),   // 250 m/s, 10 m sweep — way past full power
            };
            var r = FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f);
            Assert.IsTrue(r.IsCast);
            Assert.AreEqual(1f, r.Range01, 1e-4f, "the range saturates at 1 however far you wind back");
            Assert.LessOrEqual(r.DistanceMetres, 12f + 1e-4f, "distance never exceeds the rod's cap");
            Assert.AreEqual(12f, r.DistanceMetres, 1e-3f, "a full wind-back, fully snapped, reaches the whole cap");
        }

        [Test]
        public void Distance_TracksHowFarYouWoundBack()
        {
            // THE owner-reported bug, pinned (playtest 2026-07-23: "casts should have distances based on
            // the cast"). The same brisk sweep from progressively deeper wind-backs must reach
            // progressively farther, right across the useful range — not all pile onto one distance.
            FlickSample[] From(float backX) => new[]
            {
                new FlickSample(new Vector2(-0.1f, 0f), 0.00f),
                new FlickSample(new Vector2(backX, 0f), 0.10f),   // the wind-back apex
                new FlickSample(new Vector2( 1.0f, 0f), 0.15f),   // one brisk sweep forward
            };

            float last = -1f;
            foreach (float back in new[] { -1f, -2f, -3f, -4f })
            {
                var r = Eval(From(back));
                Assert.IsTrue(r.IsCast, $"a {-back} m wind-back casts");
                Assert.Greater(r.DistanceMetres, last + 0.5f,
                    $"winding back to {back} m must reach clearly farther than the shallower draw before it");
                last = r.DistanceMetres;
            }
            Assert.AreEqual(12f, last, 1e-3f, "and the full wind-back reaches the cap");
        }

        [Test]
        public void TheAimPreview_IsWhereAFullySnappedCastLands()
        {
            // The wind-back preview used to promise a distance the release could not deliver. It must now
            // BE the promise: charge · cap is exactly what a properly snapped flick from that draw lands at.
            foreach (float back in new[] { 1f, 2f, 3.5f })
            {
                Vector2 pointer = new Vector2(-back, 0f);   // drawn back, so the cast will fly +X
                float charge = FlickCastMath.WindBackCharge01(pointer, Anchor, S.FullRangeWindBackMetres);
                Vector2 aim = FlickCastMath.WindBackAimOffset(pointer, Anchor,
                                                              S.FullRangeWindBackMetres, 12f);

                var r = Eval(new[]
                {
                    new FlickSample(new Vector2(-0.1f, 0f), 0.00f),
                    new FlickSample(pointer,                0.10f),
                    new FlickSample(new Vector2( 1.0f, 0f), 0.15f),   // fully snapped
                });

                Assert.AreEqual(charge * 12f, r.DistanceMetres, 1e-3f,
                    $"the {back} m wind-back previewed {charge * 12f:0.00} m — the cast must land there");
                Assert.AreEqual(aim.magnitude, r.DistanceMetres, 1e-3f, "…and the previewed SPOT is the spot");
            }
        }

        [Test]
        public void Snap_GrowsWithSweepSpeed()
        {
            // The same path swept twice — once lazily, once briskly. Same wind-back, different timestamps:
            // the brisk one delivers more of the range it aimed at and flies farther.
            FlickSample[] Path(float step) => new[]
            {
                new FlickSample(new Vector2(-1.2f, 0f), 0 * step),
                new FlickSample(new Vector2(-0.4f, 0f), 1 * step),
                new FlickSample(new Vector2( 0.4f, 0f), 2 * step),
                new FlickSample(new Vector2( 1.0f, 0f), 3 * step),
            };
            var lazy  = Eval(Path(0.30f));   // ~2.7 m/s peak
            var brisk = Eval(Path(0.05f));   // ~16 m/s peak
            Assert.IsTrue(lazy.IsCast && brisk.IsCast);
            Assert.Greater(brisk.Snap01, lazy.Snap01, "a faster sweep delivers more of the aimed range");
            Assert.Greater(brisk.DistanceMetres, lazy.DistanceMetres, "…and flies farther");
        }

        [Test]
        public void CapSeam_ADifferentCapRescalesTheSameGesture()
        {
            // The per-gear seam: the identical gesture against a longer rod flies proportionally farther.
            var small = Eval(CleanFlick(), cap: 6f);
            var big   = Eval(CleanFlick(), cap: 24f);
            Assert.IsTrue(small.IsCast && big.IsCast);
            Assert.AreEqual(small.Range01, big.Range01, 1e-5f, "the range is the gesture's, not the gear's");
            Assert.Greater(big.DistanceMetres, small.DistanceMetres, "a better rod throws the same flick farther");
            Assert.LessOrEqual(small.DistanceMetres, 6f + 1e-4f);
        }

        // ---- where you let go (deliberately NO LONGER a factor) --------------------------------

        // The same wind-back apex and the same brisk sweep, carried out to a varying RELEASE point:
        // let go just past the character, a metre or three later, or way out.
        private static FlickSample[] SweepTo(float releaseX) => new[]
        {
            new FlickSample(new Vector2(-0.2f, 0f), 0.00f),
            new FlickSample(new Vector2(-1.4f, 0f), 0.10f),   // the wind-back apex
            new FlickSample(new Vector2( 0.2f, 0f), 0.20f),
            new FlickSample(new Vector2(releaseX, 0f), 0.30f),
        };

        [Test]
        public void ReleasePoint_NoLongerChangesTheCast()
        {
            // THE REGRESSION for the owner's playtest bug. The retired model scored the release point in
            // world metres — full only within ~1 m of the angler, dead by ~3.8 m — so on a ~16 m-wide
            // screen every natural flick scored zero and collapsed onto the floor, whatever it was. Where
            // the hand happens to be at the instant the button comes up must not decide the cast.
            var near = Eval(SweepTo(1.0f));
            var mid  = Eval(SweepTo(3.0f));
            var far  = Eval(SweepTo(5.0f));
            Assert.IsTrue(near.IsCast && mid.IsCast && far.IsCast);

            Assert.AreEqual(near.DistanceMetres, mid.DistanceMetres, 1e-3f,
                "same draw, same snap — carrying the sweep further must not shorten the cast");
            Assert.AreEqual(near.DistanceMetres, far.DistanceMetres, 1e-3f);
            Assert.Greater(near.DistanceMetres, S.MinCastMetres + 1e-3f,
                "and none of them sit on the floor the old model pinned them to");
        }

        [Test]
        public void CozyFail_ALimpSweepIsAShortCast_NeverNothing()
        {
            // A good deep draw, then a dribbled sweep: the range was aimed but not delivered. Still a real
            // cast — short, in the water, reel in and try again. Never nothing, never a punishment.
            var limp = Eval(new[]
            {
                new FlickSample(new Vector2(-0.2f, 0f), 0.0f),
                new FlickSample(new Vector2(-4.0f, 0f), 0.4f),   // a full-range wind-back…
                new FlickSample(new Vector2( 0.5f, 0f), 2.0f),   // …pushed forward at ~2.8 m/s
            });
            var snapped = Eval(new[]
            {
                new FlickSample(new Vector2(-0.2f, 0f), 0.00f),
                new FlickSample(new Vector2(-4.0f, 0f), 0.10f),
                new FlickSample(new Vector2( 0.5f, 0f), 0.25f),   // the same draw, properly snapped
            });

            Assert.IsTrue(limp.IsCast, "a limp cast still casts (cozy fail)");
            Assert.Less(limp.Snap01, 0.5f, "the sweep was dribbled");
            Assert.GreaterOrEqual(limp.DistanceMetres, S.MinCastMetres - 1e-4f,
                "no successful cast lands under the floor");
            Assert.Less(limp.DistanceMetres, snapped.DistanceMetres,
                "…but it falls clearly short of what the same wind-back would have reached");
        }

        [Test]
        public void CozyFloor_AWeakFlickStillLandsAtLeastTheMinimum()
        {
            // Barely over the thresholds, swept slowly: raw distance would be under the floor — it clamps up.
            var g = new[]
            {
                new FlickSample(new Vector2(-0.65f, 0f), 0.0f),
                new FlickSample(new Vector2(-0.35f, 0f), 0.3f),
                new FlickSample(new Vector2(-0.05f, 0f), 0.6f),   // 0.6 m sweep at 1 m/s
            };
            var r = FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f);
            Assert.IsTrue(r.IsCast, "past the minimum wind-back + flick length it casts");
            Assert.AreEqual(S.MinCastMetres, r.DistanceMetres, 1e-3f, "a feeble flick lands at the floor");
        }

        // ---- degenerate gestures --------------------------------------------------------------

        [Test]
        public void Degenerate_NullEmptyOrOnePoint_IsNoCast()
        {
            Assert.IsFalse(FlickCastMath.Evaluate(null, 0, Anchor, S, 12f).IsCast, "null samples");
            Assert.IsFalse(FlickCastMath.Evaluate(new FlickSample[4], 0, Anchor, S, 12f).IsCast, "count 0");
            var one = new[] { new FlickSample(new Vector2(-2f, 0f), 0f) };
            Assert.IsFalse(FlickCastMath.Evaluate(one, 1, Anchor, S, 12f).IsCast, "one point is no gesture");
        }

        [Test]
        public void Degenerate_ZeroLengthGesture_IsNoCast()
        {
            var g = new[]
            {
                new FlickSample(new Vector2(-1f, 0f), 0.0f),
                new FlickSample(new Vector2(-1f, 0f), 0.1f),
                new FlickSample(new Vector2(-1f, 0f), 0.2f),   // never moved — a hold, not a flick
            };
            Assert.IsFalse(FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f).IsCast);
        }

        [Test]
        public void Degenerate_BackwardsDrag_IsNoCast()
        {
            // Dragged AWAY from the water and released still going backwards: its "sweep" points backwards
            // and its apex sits at the start (never behind the character along it) — the rod never loaded.
            var g = new[]
            {
                new FlickSample(new Vector2( 0.2f, 0f), 0.0f),
                new FlickSample(new Vector2(-0.6f, 0f), 0.1f),
                new FlickSample(new Vector2(-1.3f, 0f), 0.2f),
                new FlickSample(new Vector2(-2.0f, 0f), 0.3f),
            };
            Assert.IsFalse(FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f).IsCast);
        }

        [Test]
        public void Degenerate_TooShallowAWindBack_IsNoCast()
        {
            // A sweep that starts basically AT the character (no wind-back) mustn't fly.
            var g = new[]
            {
                new FlickSample(new Vector2(-0.1f, 0f), 0.0f),
                new FlickSample(new Vector2( 0.6f, 0f), 0.1f),
                new FlickSample(new Vector2( 1.4f, 0f), 0.2f),
            };
            Assert.IsFalse(FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f).IsCast,
                "under MinWindBackMetres the rod never loaded — no cast");
        }

        [Test]
        public void NaN_JunkSamplesAreSkipped_AndACleanGestureStillCasts()
        {
            var nan = new FlickSample(new Vector2(float.NaN, 0f), float.NaN);
            var g = new[]
            {
                new FlickSample(new Vector2(-0.2f, 0f), 0.00f),
                nan,
                new FlickSample(new Vector2(-1.4f, 0f), 0.20f),
                nan,
                new FlickSample(new Vector2( 0.2f, 0f), 0.30f),
                new FlickSample(new Vector2( 1.0f, 0f), 0.35f),
                nan,                                            // trailing junk mustn't hijack the release
            };
            var r = FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f);
            Assert.IsTrue(r.IsCast, "NaN samples are skipped; the surviving gesture casts");
            AssertFinite(r);
            Assert.AreEqual(1f, r.Direction.x, 1e-4f, "the release is the last USABLE sample");
        }

        [Test]
        public void NaN_AllJunk_IsNoCast_AndNaNInputsNeverLeakOut()
        {
            var nan = new FlickSample(new Vector2(float.NaN, float.NaN), float.NaN);
            var r = FlickCastMath.Evaluate(new[] { nan, nan, nan }, 3, Anchor, S, 12f);
            Assert.IsFalse(r.IsCast);

            // A NaN anchor / NaN cap against a clean gesture: sanitized, never NaN out.
            var g = CleanFlick();
            AssertFinite(FlickCastMath.Evaluate(g, g.Length, new Vector2(float.NaN, float.NaN), S, 12f));
            AssertFinite(FlickCastMath.Evaluate(g, g.Length, Anchor, S, float.NaN));
        }

        // ---- determinism ----------------------------------------------------------------------

        [Test]
        public void Determinism_IdenticalInputs_BitIdenticalResult()
        {
            var a = Eval(CleanFlick());
            var b = Eval(CleanFlick());
            Assert.AreEqual(a.IsCast, b.IsCast);
            Assert.AreEqual(a.Direction, b.Direction);
            Assert.AreEqual(a.Range01, b.Range01);
            Assert.AreEqual(a.Snap01, b.Snap01);
            Assert.AreEqual(a.DistanceMetres, b.DistanceMetres);
            Assert.AreEqual(a.LandingPoint, b.LandingPoint);
        }

        // ---- the shipped defaults stay coherent -----------------------------------------------

        [Test]
        public void Defaults_AreACoherentForgivingTuning()
        {
            var s = FlickCastSettings.Default;
            Assert.Greater(s.MaxCastDistanceMetres, s.MinCastMetres, "the cap clears the floor");
            Assert.Greater(s.LimpFlickFraction01, 0f, "a limp cast still flies (cozy fail, never zero)");
            Assert.Greater(s.FullRangeWindBackMetres, s.MinWindBackMetres,
                "the range has room to grow between 'that counts as a cast' and 'that's the full cast'");
            Assert.Greater(s.FullSnapFlickSpeed, 0f, "the snap reference is a real speed");
            Assert.Greater(s.LineFlightMetresPerSec, 0f);

            // The dial the owner actually feels: winding back the full amount, on a ~16 m-wide on-foot
            // screen, must be a comfortable draw — not a mouse-off-the-monitor demand.
            Assert.Less(s.FullRangeWindBackMetres, 8f,
                "a full-range wind-back has to fit on screen with room to sweep forward");
        }

        private static void AssertFinite(in FlickCastResult r)
        {
            Assert.IsFalse(float.IsNaN(r.Direction.x) || float.IsNaN(r.Direction.y), "direction is finite");
            Assert.IsFalse(float.IsNaN(r.Range01), "range is finite");
            Assert.IsFalse(float.IsNaN(r.Snap01), "snap is finite");
            Assert.IsFalse(float.IsNaN(r.DistanceMetres), "distance is finite");
            Assert.IsFalse(float.IsNaN(r.LandingPoint.x) || float.IsNaN(r.LandingPoint.y), "landing is finite");
        }
    }
}
