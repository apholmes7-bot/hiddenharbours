using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// FLICK-CAST maths (Rod Fishing v2 §2.2) — the pure gesture resolver, EditMode-tested like
    /// RodFightMath/TrapHaulMath. Covers: direction comes from the flick vector; power is earned by sweep
    /// speed + length and CAPPED; release timing is the quality curve (sweet band → falloff → piled);
    /// the cozy floor (a botched cast is a SHORT cast, never nothing); determinism (identical inputs →
    /// bit-identical results); and the degenerate gestures (null/empty/one-point/zero-length/backwards/
    /// NaN) that must resolve to a quiet NoCast, never a throw or a NaN.
    /// </summary>
    public class FlickCastMathTests
    {
        private static readonly Vector2 Anchor = Vector2.zero;

        private static FlickCastSettings S => FlickCastSettings.Default;

        // A clean, sweet-released flick toward +X: wind back 1.4 m, sweep 2.4 m, release 1 m past the
        // anchor (= the default SweetReleaseMetres) at a brisk pace.
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

        // ---- power + the cap ------------------------------------------------------------------

        [Test]
        public void Power_IsCapped_AndDistanceNeverExceedsTheCap()
        {
            // An absurdly long, absurdly fast sweep: power saturates at 1 and distance at the cap.
            var g = new[]
            {
                new FlickSample(new Vector2(-9f, 0f), 0.00f),
                new FlickSample(new Vector2(-4f, 0f), 0.02f),
                new FlickSample(new Vector2( 1f, 0f), 0.04f),   // 250 m/s, 10 m sweep — way past full power
            };
            var r = FlickCastMath.Evaluate(g, g.Length, Anchor, S, 12f);
            Assert.IsTrue(r.IsCast);
            Assert.AreEqual(1f, r.Power01, 1e-4f, "power saturates at 1 however hard you throw");
            Assert.LessOrEqual(r.DistanceMetres, 12f + 1e-4f, "distance never exceeds the rod's cap");
            Assert.AreEqual(12f, r.DistanceMetres, 1e-3f, "a max-power sweet release reaches the whole cap");
        }

        [Test]
        public void Power_GrowsWithSweepSpeed()
        {
            // The same path swept twice — once lazily, once briskly. Same geometry, different timestamps:
            // the brisk one earns more power and flies farther.
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
            Assert.Greater(brisk.Power01, lazy.Power01, "a faster sweep earns more power");
            Assert.Greater(brisk.DistanceMetres, lazy.DistanceMetres, "…and flies farther");
        }

        [Test]
        public void CapSeam_ADifferentCapRescalesTheSameGesture()
        {
            // The per-gear seam: the identical gesture against a longer rod flies proportionally farther.
            var small = Eval(CleanFlick(), cap: 6f);
            var big   = Eval(CleanFlick(), cap: 24f);
            Assert.IsTrue(small.IsCast && big.IsCast);
            Assert.AreEqual(small.Power01, big.Power01, 1e-5f, "power is the gesture's, not the gear's");
            Assert.Greater(big.DistanceMetres, small.DistanceMetres, "a better rod throws the same flick farther");
            Assert.LessOrEqual(small.DistanceMetres, 6f + 1e-4f);
        }

        // ---- release timing (the skill beat) --------------------------------------------------

        // The same wind-back apex swept out to a varying RELEASE point: releasing at the sweet point,
        // late-ish (3 m past), and way late (5 m past — beyond the falloff).
        private static FlickSample[] SweepTo(float releaseX) => new[]
        {
            new FlickSample(new Vector2(-0.2f, 0f), 0.00f),
            new FlickSample(new Vector2(-1.4f, 0f), 0.10f),   // the wind-back apex
            new FlickSample(new Vector2( 0.2f, 0f), 0.20f),
            new FlickSample(new Vector2(releaseX, 0f), 0.30f),
        };

        [Test]
        public void Quality_IsFull_AtTheSweetRelease_AndFallsOffAwayFromIt()
        {
            var sweet   = Eval(SweepTo(1.0f));   // released at the sweet point
            var late    = Eval(SweepTo(3.0f));   // hung on too long
            var wayLate = Eval(SweepTo(5.0f));   // way past the falloff
            Assert.IsTrue(sweet.IsCast && late.IsCast && wayLate.IsCast);

            Assert.AreEqual(1f, sweet.Quality01, 1e-4f, "released at the sweet point = full quality");
            Assert.Less(late.Quality01, sweet.Quality01, "off the sweet band, quality falls");
            Assert.Less(wayLate.Quality01, late.Quality01, "…and keeps falling");
            Assert.AreEqual(0f, wayLate.Quality01, 1e-4f, "far past the falloff the line piles (quality 0)");

            // The late sweeps are LONGER (≥ power), yet they fly SHORTER — the timing beat dominates.
            Assert.GreaterOrEqual(late.Power01, sweet.Power01);
            Assert.Greater(sweet.DistanceMetres, late.DistanceMetres, "a mistimed release casts shorter");
            Assert.Greater(late.DistanceMetres, wayLate.DistanceMetres);
        }

        [Test]
        public void CozyFail_AMistimedCastIsAShortCast_NeverNothing()
        {
            // Fully piled (quality 0) but still a real cast: it flies the piled fraction of its power
            // and never lands closer than the floor — the bobber is in the water, reel in and recast.
            var piled = Eval(SweepTo(5.0f));
            Assert.IsTrue(piled.IsCast, "a botched cast still casts (cozy fail)");
            Assert.AreEqual(0f, piled.Quality01, 1e-4f);
            Assert.GreaterOrEqual(piled.DistanceMetres, S.MinCastMetres - 1e-4f,
                "no successful cast lands under the floor");
            Assert.Less(piled.DistanceMetres, Eval(SweepTo(1.0f)).DistanceMetres, "…but it is clearly SHORT");
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
            Assert.AreEqual(a.Power01, b.Power01);
            Assert.AreEqual(a.Quality01, b.Quality01);
            Assert.AreEqual(a.DistanceMetres, b.DistanceMetres);
            Assert.AreEqual(a.LandingPoint, b.LandingPoint);
        }

        // ---- the shipped defaults stay coherent -----------------------------------------------

        [Test]
        public void Defaults_AreACoherentForgivingTuning()
        {
            var s = FlickCastSettings.Default;
            Assert.Greater(s.MaxCastDistanceMetres, s.MinCastMetres, "the cap clears the floor");
            Assert.Greater(s.PiledCastFraction01, 0f, "a piled cast still flies (cozy fail, never zero)");
            Assert.Greater(s.QualityFalloffMetres, 0f, "quality fades, it doesn't cliff");
            Assert.Greater(s.FullPowerFlickMetres, s.MinFlickLengthMetres, "power has room to grow");
            Assert.Greater(s.LineFlightMetresPerSec, 0f);
            Assert.That(s.SpeedWeight01, Is.InRange(0f, 1f));
        }

        private static void AssertFinite(in FlickCastResult r)
        {
            Assert.IsFalse(float.IsNaN(r.Direction.x) || float.IsNaN(r.Direction.y), "direction is finite");
            Assert.IsFalse(float.IsNaN(r.Power01), "power is finite");
            Assert.IsFalse(float.IsNaN(r.Quality01), "quality is finite");
            Assert.IsFalse(float.IsNaN(r.DistanceMetres), "distance is finite");
            Assert.IsFalse(float.IsNaN(r.LandingPoint.x) || float.IsNaN(r.LandingPoint.y), "landing is finite");
        }
    }
}
