using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The BUBBLE stream's laws (owner ask 2026-08-27: "i want to see bubbles form and drift but they
    /// arent entirely noticeable, everything looks very organized and shader-like and not particle like").
    ///
    /// <para>Each test below pins one of the four properties that buy "particle-like" — bursty arrival,
    /// heavy-tailed size, a pop that is NOT the foam's fade, and per-bubble clocks — plus the pool budget
    /// (rule 7) and the determinism boundary (rule 5). They are the properties an eyeball would check, made
    /// checkable; the look itself is still the owner's call.</para>
    /// </summary>
    public class WakeBubbleSystemTests
    {
        static WakeBubbleConfig Cfg() => WakeBubbleConfig.Default;

        // ==== property 1: arrival is BURSTY, not a metronome ==========================================

        [Test]
        public void BurstCount_IsNotAMetronome()
        {
            var cfg = Cfg();
            const float dt = 1f / 30f;

            var seen = new System.Collections.Generic.HashSet<int>();
            for (uint t = 0; t < 400; t++)
                seen.Add(WakeBubbleSystem.BurstCount(1f, false, in cfg, dt, t));

            Assert.Greater(seen.Count, 2,
                "Every tick produced (near enough) the same number of bubbles. A steady rate is a " +
                "metronome and the eye reads a metronome as machinery — which is exactly the " +
                "'organized and shader-like' the owner reported. Churn throws clusters, then nothing.");
        }

        [Test]
        public void BurstCount_LongRunRate_MatchesTheConfiguredRate()
        {
            var cfg = Cfg();
            const float dt = 1f / 30f;
            const int ticks = 20000;

            long total = 0;
            for (uint t = 0; t < ticks; t++) total += WakeBubbleSystem.BurstCount(1f, false, in cfg, dt, t);

            // Bursty must not mean WRONG. Per tick this is Binomial(slots, lambda/slots), so over N ticks
            // the mean is N*lambda and the standard deviation is sqrt(N * slots * p * (1-p)) — a DERIVED
            // tolerance, not a tuned one. Three sigma.
            float lambda = cfg.FormPerSecond * dt;
            float p = lambda / cfg.BurstSlots;
            double expected = ticks * (double)lambda;
            double sigma = System.Math.Sqrt(ticks * (double)cfg.BurstSlots * p * (1f - p));

            Assert.AreEqual(expected, total, 3.0 * sigma,
                "The long-run bubble rate drifted off the configured FormPerSecond. Uneven arrival is the " +
                "point; a different average is a bug, and it would quietly re-tune the owner's knob.");
        }

        [Test]
        public void BurstCount_NeverExceedsItsSlots()
        {
            var cfg = Cfg();
            // A dt far past any real frame, so lambda/slots saturates at 1 and every slot fires.
            for (uint t = 0; t < 200; t++)
            {
                int n = WakeBubbleSystem.BurstCount(1f, false, in cfg, 10f, t);
                Assert.LessOrEqual(n, cfg.BurstSlots,
                    "The per-tick count must be bounded by the slot count BY CONSTRUCTION — that bound is " +
                    "the pool's guard against a long frame emptying the whole pool in one tick (rule 7).");
            }
        }

        [Test]
        public void BurstCount_IsZero_Aground_AtRest_AndWithNoTime()
        {
            var cfg = Cfg();
            Assert.AreEqual(0, WakeBubbleSystem.BurstCount(1f, true, in cfg, 1f / 30f, 7u),
                "A boat aground is not working water.");
            Assert.AreEqual(0, WakeBubbleSystem.BurstCount(0f, false, in cfg, 1f / 30f, 7u),
                "Zero vigour is a drifting hull — bubbles form where the hull WORKS the water.");
            Assert.AreEqual(0, WakeBubbleSystem.BurstCount(1f, false, in cfg, 0f, 7u));
        }

        [Test]
        public void Vigour01_IsZeroAtRest_SaturatesUnderway_AndIsLiftedByTheGrade()
        {
            var cfg = Cfg();

            Assert.AreEqual(0f, WakeBubbleSystem.Vigour01(0f, 1f, in cfg), 1e-6f);
            Assert.AreEqual(0f, WakeBubbleSystem.Vigour01(cfg.SpeedThreshold, 1f, in cfg), 1e-6f);
            Assert.AreEqual(1f, WakeBubbleSystem.Vigour01(cfg.FullVigourSpeed, 1f, in cfg), 1e-5f);
            Assert.AreEqual(1f, WakeBubbleSystem.Vigour01(cfg.FullVigourSpeed * 3f, 1f, in cfg), 1e-5f);

            // The grade LIFTS rather than gates: the dory still fizzes.
            float dory = WakeBubbleSystem.Vigour01(cfg.FullVigourSpeed, 0f, in cfg);
            float dragger = WakeBubbleSystem.Vigour01(cfg.FullVigourSpeed, 1f, in cfg);
            Assert.Greater(dory, 0f,
                "A small hull at speed must still bubble. A grade that GATED would leave the starter boat " +
                "with no bubbles at all, which is the boat the owner sails most.");
            Assert.Greater(dragger, dory,
                "A dragger must boil where a dory fizzes — the size x weight x speed grading of the " +
                "original 2026-07-23 ask, applied to the bubbles.");
        }

        // ==== property 2: size is heavy-tailed, so a few are individually readable =====================

        [Test]
        public void SizeAt_SpansItsRange_Exactly()
        {
            var cfg = Cfg();
            Assert.AreEqual(cfg.MinSize, WakeBubbleSystem.SizeAt(0f, in cfg), 1e-6f,
                "The owner's two size knobs must mean what they say at both ends.");
            Assert.AreEqual(cfg.MaxSize, WakeBubbleSystem.SizeAt(1f, in cfg), 1e-6f);

            float prev = -1f;
            for (int i = 0; i <= 200; i++)
            {
                float v = WakeBubbleSystem.SizeAt(i / 200f, in cfg);
                Assert.GreaterOrEqual(v, prev - 1e-6f, "Size must be monotone in the draw.");
                prev = v;
            }
        }

        [Test]
        public void SizeAt_IsBiasedSmall_SoAFewBubblesAreReadable()
        {
            var cfg = Cfg();
            float mid = (cfg.MinSize + cfg.MaxSize) * 0.5f;
            float median = WakeBubbleSystem.SizeAt(0.5f, in cfg);

            Assert.Less(median, mid,
                "Half the bubbles must be smaller than the midpoint. A uniform size distribution gives a " +
                "uniform field of identical dots; the owner asked to be able to PICK OUT bubbles, and " +
                "that needs a haze of small ones with a scatter of big ones.");

            // How rare is "big"? Fewer than a quarter of draws should reach halfway up the range.
            int big = 0;
            const int n = 1000;
            for (int i = 0; i < n; i++)
                if (WakeBubbleSystem.SizeAt(i / (float)n, in cfg) > mid) big++;
            Assert.Less(big, n * 0.25f,
                "Big bubbles stopped being rare, so nothing stands out from anything else.");
        }

        // ==== property 3: it POPS, and that is a different death from the foam's ======================

        [Test]
        public void AlphaAt_Holds_ThenGoes()
        {
            var cfg = Cfg();
            float holdUntil = 1f - cfg.PopFraction;

            Assert.AreEqual(1f, WakeBubbleSystem.AlphaAt(0f, in cfg), 1e-6f);
            Assert.AreEqual(1f, WakeBubbleSystem.AlphaAt(holdUntil, in cfg), 1e-6f,
                "A bubble on the water does not gradually become transparent — it is there, and then it " +
                "is not.");
            Assert.AreEqual(0f, WakeBubbleSystem.AlphaAt(1f, in cfg), 1e-6f);

            float prev = 2f;
            for (int i = 0; i <= 200; i++)
            {
                float v = WakeBubbleSystem.AlphaAt(i / 200f, in cfg);
                Assert.LessOrEqual(v, prev + 1e-6f, "A bubble must never brighten.");
                prev = v;
            }
        }

        [Test]
        public void ABubbleDies_DifferentlyFromFoam()
        {
            var cfg = Cfg();

            // THE POINT of this test: two streams that die the same way read as one stream. At the moment
            // a bubble is still at full opacity, the foam it is sitting in has already faded a long way —
            // and that difference is what lets the eye separate them.
            var foam = WakeConfig.Default;
            float t = 1f - cfg.PopFraction;      // the last instant of the bubble's hold

            float bubbleAlpha = WakeBubbleSystem.AlphaAt(t, in cfg);
            float foamAlpha = WakeParticleSystem.LifeFade(t, in foam) / Mathf.Max(1e-4f, foam.StartAlpha);

            Assert.AreEqual(1f, bubbleAlpha, 1e-6f);
            Assert.Less(foamAlpha, 0.5f,
                "The foam curve must have given up most of its opacity by the time a bubble is still " +
                "whole. If the two curves converge, the bubbles read as more foam and the stream buys " +
                "nothing.");
        }

        [Test]
        public void SizeOverLife_HoldsThenSwellsIntoThePop()
        {
            var cfg = Cfg();
            const float baseSize = 0.2f;
            float holdUntil = 1f - cfg.PopFraction;

            Assert.AreEqual(baseSize, WakeBubbleSystem.SizeOverLife(baseSize, 0f, in cfg), 1e-6f);
            Assert.AreEqual(baseSize, WakeBubbleSystem.SizeOverLife(baseSize, holdUntil, in cfg), 1e-6f,
                "A bubble keeps its size while it is whole — the swell is the film letting go, not a " +
                "slow inflation over its whole life.");
            Assert.AreEqual(baseSize * cfg.PopSwell, WakeBubbleSystem.SizeOverLife(baseSize, 1f, in cfg), 1e-5f);

            float prev = -1f;
            for (int i = 0; i <= 200; i++)
            {
                float v = WakeBubbleSystem.SizeOverLife(baseSize, i / 200f, in cfg);
                Assert.GreaterOrEqual(v, prev - 1e-6f, "A bubble must never shrink.");
                prev = v;
            }
        }

        // ==== property 4: their own clocks ============================================================

        [Test]
        public void EveryBubble_GetsItsOwnLifeAndSize()
        {
            var cfg = Cfg();
            var sys = new WakeBubbleSystem(64);
            for (int i = 0; i < 40; i++) sys.Form(Vector2.zero, Vector2.zero, 0.5f, 1f, in cfg);

            var lives = new System.Collections.Generic.HashSet<float>();
            var sizes = new System.Collections.Generic.HashSet<float>();
            float loLife = float.MaxValue, hiLife = float.MinValue;
            foreach (var b in sys.Pool)
            {
                if (!b.Alive) continue;
                lives.Add(b.Lifetime);
                sizes.Add(b.BaseSize);
                loLife = Mathf.Min(loLife, b.Lifetime);
                hiLife = Mathf.Max(hiLife, b.Lifetime);
            }

            Assert.Greater(lives.Count, 30, "Bubbles must not share a clock.");
            Assert.Greater(sizes.Count, 30, "Bubbles must not share a size.");

            // DERIVED: lifetimes are cfg.Lifetime * (1 +/- LifetimeJitter), so over 40 draws the observed
            // span should cover most of that interval. Ask for half of it — enough to prove the jitter is
            // wired without pinning the exact draw of 40 hashes.
            float fullSpan = 2f * cfg.Lifetime * cfg.LifetimeJitter;
            Assert.Greater(hiLife - loLife, fullSpan * 0.5f,
                "The lifetime jitter is not reaching the pool, so a cluster pops all at once instead of " +
                "each bubble going on its own clock.");
        }

        [Test]
        public void AClusterScatters_InsteadOfStacking()
        {
            var cfg = Cfg();
            var sys = new WakeBubbleSystem(64);
            var at = new Vector2(10f, -4f);
            const float radius = 0.5f;

            for (int i = 0; i < 40; i++) sys.Form(at, Vector2.zero, radius, 1f, in cfg);

            int distinct = 0;
            float maxR = 0f;
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            foreach (var b in sys.Pool)
            {
                if (!b.Alive) continue;
                float r = (b.Pos - at).magnitude;
                maxR = Mathf.Max(maxR, r);
                Assert.LessOrEqual(r, radius + 1e-4f,
                    "A bubble landed outside the cluster it formed in.");
                if (seen.Add((Mathf.RoundToInt(b.Pos.x * 100f), Mathf.RoundToInt(b.Pos.y * 100f)))) distinct++;
            }

            Assert.Greater(distinct, 30, "The cluster stacked instead of scattering.");
            Assert.Greater(maxR, radius * 0.5f,
                "The scatter never reached the outside of its radius. The area-uniform draw exists so a " +
                "cluster fills its patch instead of piling up at the centre.");
        }

        // ==== the pool budget (rule 7) ================================================================

        [Test]
        public void ThePool_NeverGrows_AndRecyclesInPlace()
        {
            var cfg = Cfg();
            const int capacity = 16;
            var sys = new WakeBubbleSystem(capacity);

            for (int i = 0; i < capacity * 10; i++)
                sys.Form(new Vector2(i, 0f), Vector2.zero, 0.1f, 1f, in cfg);

            Assert.AreEqual(capacity, sys.Capacity, "The pool must be fixed.");
            Assert.AreEqual(capacity, sys.Pool.Length);
            Assert.LessOrEqual(sys.AliveCount, capacity,
                "Emission can never exceed the pool — a full pool recycles its oldest slot rather than " +
                "allocating (rule 7).");
        }

        [Test]
        public void TheShippedPoolCovers_TheShippedSteadyState()
        {
            var cfg = Cfg();
            // The default pool on BoatWakeEmitter is 64. The steady-state population of a stream forming
            // at FormPerSecond with mean lifetime Lifetime is their product (Little's law) — so the
            // shipped pool has to carry that with margin, or the oldest bubbles start being recycled
            // early and the stream visibly thins under its own budget.
            float steadyState = cfg.FormPerSecond * cfg.Lifetime;
            Assert.Less(steadyState, 64,
                $"The shipped stream needs ~{steadyState:0} live bubbles but the shipped pool is 64. " +
                "Either raise _bubblePoolPerBoat or lower FormPerSecond — the two are one budget.");
        }

        // ==== drift ===================================================================================

        [Test]
        public void Advect_RidesTheWater_AndLosesItsOwnPush()
        {
            var drift = new Vector2(0.4f, -0.1f);
            var b = new WakeBubbleSystem.Bubble
            {
                Alive = true,
                Pos = Vector2.zero,
                Vel = new Vector2(2f, 0f),
                Lifetime = 100f,
            };

            var stepped = WakeBubbleSystem.Advect(b, drift, 0.5f, 1f);
            Assert.AreEqual(2.4f, stepped.Pos.x, 1e-4f,
                "A bubble moves on its own push PLUS the water it sits in.");
            Assert.AreEqual(-0.1f, stepped.Pos.y, 1e-4f);
            Assert.AreEqual(1f, stepped.Vel.x, 1e-4f,
                "Its own push must decay so that far from the boat only the water's drift remains.");
            Assert.AreEqual(1f, stepped.Age, 1e-4f);
        }

        [Test]
        public void Advect_DiesAtTheEndOfItsLifetime()
        {
            var b = new WakeBubbleSystem.Bubble { Alive = true, Lifetime = 0.5f };
            b = WakeBubbleSystem.Advect(b, Vector2.zero, 1f, 0.4f);
            Assert.IsTrue(b.Alive);
            b = WakeBubbleSystem.Advect(b, Vector2.zero, 1f, 0.2f);
            Assert.IsFalse(b.Alive, "A bubble that has outlived its lifetime must free its slot.");
        }

        [Test]
        public void Bubbles_TakeALargerShareOfTheWind_ThanFoamDoes()
        {
            var cfg = Cfg();
            var current = new Vector2(0.2f, 0f);
            var wind = new Vector2(4f, 0f);

            Vector2 d = WakeBubbleSystem.DriftVelocity(current, wind, cfg.WindDriftFraction);
            Assert.AreEqual(current.x + wind.x * cfg.WindDriftFraction, d.x, 1e-5f);

            Assert.Greater(cfg.WindDriftFraction, WakeTrailConfig.Default.FoamWindDriftFraction,
                "A bubble stands proud of the surface where the air actually reaches it, where a foam " +
                "raft lies in it. That difference in coupling is what makes a bubble visibly LEAVE the " +
                "trail it formed in — 'form and drift', not 'form and ride along'.");
        }

        // ==== determinism (rule 5) ====================================================================

        [Test]
        public void TheStreamIsDeterministic_NotRandom()
        {
            var cfg = Cfg();
            var a = new WakeBubbleSystem(32);
            var b = new WakeBubbleSystem(32);

            for (int i = 0; i < 20; i++)
            {
                a.Form(new Vector2(i, 0f), Vector2.one, 0.3f, 0.8f, in cfg);
                b.Form(new Vector2(i, 0f), Vector2.one, 0.3f, 0.8f, in cfg);
            }

            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(a.Pool[i].Pos, b.Pool[i].Pos,
                    "Identical inputs must reproduce identical bubbles — the variation is a stable hash, " +
                    "not System.Random (rule 5).");
                Assert.AreEqual(a.Pool[i].Lifetime, b.Pool[i].Lifetime);
                Assert.AreEqual(a.Pool[i].BaseSize, b.Pool[i].BaseSize);
            }
        }
    }
}
