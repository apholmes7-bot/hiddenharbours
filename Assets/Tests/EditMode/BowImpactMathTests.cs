using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The BOW as an IMPACT (owner eyeball 2026-08-27: <i>"the bow splash reads identical to the rear
    /// wake … not physics based or dynamic"</i>).
    ///
    /// <para>Each test below pins one of the four properties that separate a collision from a wake —
    /// driven by ENCOUNTER rather than speed, BURSTY arrival, HEAVY-TAILED size, and a death signature
    /// that is neither the foam's fade nor the bubbles' pop — plus the per-tick budget (rule 7), the
    /// determinism boundary (rule 5) and the A/B revert. Whether it LOOKS dynamic is still the owner's
    /// call; these prove the mechanism he asked for is actually the one running.</para>
    /// </summary>
    public class BowImpactMathTests
    {
        static BowImpactConfig Cfg() => BowImpactConfig.Default;

        // ==== property 1: the SEA drives it, not the speed alone ======================================

        [Test]
        public void TheSameBoatAtTheSameSpeed_ThrowsMoreInAHeadSea_ThanOnGlass()
        {
            // THE defect, stated as a measurement: with the shipped stream, these two were identical
            // numbers, because speed was the only input. That is why the bow could not look dynamic —
            // nothing about the water reached it.
            var cfg = Cfg();
            const float speed = 4f;

            float onGlass = BowImpactMath.Impact01(speed, 0f, in cfg);
            float inASea = BowImpactMath.Impact01(speed, cfg.SeaRateKnee, in cfg);

            Assert.Greater(onGlass, 0f, "a boat at 4 m/s must throw SOMETHING even on flat water");
            Assert.Greater(inASea, onGlass * 1.3f,
                $"A head sea must visibly multiply the splash (glass {onGlass:0.00} vs sea {inASea:0.00}). " +
                "If these are equal the bow is a speed ramp with extra steps and the owner's " +
                "'not physics based' verdict stands.");
        }

        [Test]
        public void TheEncounterRate_IsTheGapOpening_NotTheWholeBoatRising()
        {
            // A hull riding a long smooth swell lifts stem and centre together. If that counted as an
            // impact, every boat would throw spray continuously in any sea at all, which is the same
            // undifferentiated wall the shipped stream produced.
            float rising = BowImpactMath.EncounterRate(1.4f, 1.4f, 1.0f, 1.0f, 0.1f);
            Assert.AreEqual(0f, rising, 1e-6f,
                "a hull rising bodily on a swell must register NO impact — the stem is not burying.");

            // The stem going down into a face while the centre holds: that is the collision.
            float burying = BowImpactMath.EncounterRate(0.5f, 0f, 0.1f, 0f, 0.1f);
            Assert.Greater(burying, 0f, "the stem meeting a rising face must register an impact");
            Assert.AreEqual(4f, burying, 1e-4f, "…at the rate the gap is actually opening");
        }

        [Test]
        public void ComingOutOfATrough_ThrowsNothing()
        {
            // A symmetric signal would splash twice per wave — going in AND coming out — which reads as
            // a flicker rather than as a hull working.
            float rising = BowImpactMath.EncounterRate(0f, 0f, 0.5f, 0f, 0.1f);
            Assert.AreEqual(0f, rising, 1e-6f, "the bow coming UP out of a trough throws no water");
        }

        [Test]
        public void BelowTheSpeedThreshold_TheRoughestSeaThrowsNothing()
        {
            var cfg = Cfg();
            Assert.AreEqual(0f, BowImpactMath.Impact01(cfg.SpeedThreshold, 10f, in cfg), 1e-6f,
                "A moored boat in a chop SLAPS — that is the foam buffer's bob channel — but it does " +
                "not throw spray forward. Letting the sea alone drive the bow would splash at anchor.");
        }

        [Test]
        public void SeaGainZero_IsAPureSpeedRamp_BitExact()
        {
            var cfg = Cfg();
            cfg.SeaGain = 0f;
            float calm = BowImpactMath.Impact01(3.5f, 0f, in cfg);
            float rough = BowImpactMath.Impact01(3.5f, 99f, in cfg);
            Assert.AreEqual(calm, rough, 0f,
                "SeaGain 0 must restore the calm-water look everywhere, to the bit — a knob whose off " +
                "position is 'nearly the same' is not an A/B.");
        }

        [Test]
        public void Impact01_IsMonotoneAndBounded()
        {
            var cfg = Cfg();
            float previous = -1f;
            for (float speed = 0f; speed <= 12f; speed += 0.25f)
            {
                float v = BowImpactMath.Impact01(speed, 0.2f, in cfg);
                Assert.GreaterOrEqual(v, previous - 1e-6f, "driving harder must never throw less");
                Assert.LessOrEqual(v, 1f, "impact is a 0..1 quantity");
                previous = v;
            }
            Assert.AreEqual(1f, BowImpactMath.Impact01(20f, 20f, in cfg), 1e-6f, "it must saturate");
        }

        // ==== property 2: arrival is BURSTY, not a metronome ==========================================

        [Test]
        public void BurstCount_IsNotAMetronome()
        {
            var cfg = Cfg();
            const float dt = 1f / 30f;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (uint tick = 0; tick < 400; tick++)
                seen.Add(BowImpactMath.BurstCount(1f, false, in cfg, dt, tick));

            Assert.Greater(seen.Count, 2,
                "Every tick threw the same number of droplets. That is a metronome, and the shipped " +
                "carry-and-emit rate WAS one — a carried remainder is precisely a device for making " +
                "the output even. Churn throws clusters and then nothing.");
        }

        [Test]
        public void BurstCount_KeepsItsLongRunRate()
        {
            // Bursty must not mean WRONG: the mean over many ticks is the configured rate, so the owner's
            // ThrowPerSecond still means droplets per second.
            var cfg = Cfg();
            const float dt = 1f / 30f;
            const int ticks = 20000;
            int total = 0;
            for (uint tick = 0; tick < ticks; tick++)
                total += BowImpactMath.BurstCount(1f, false, in cfg, dt, tick);

            float perSecond = total / (ticks * dt);
            Assert.AreEqual(cfg.ThrowPerSecond, perSecond, cfg.ThrowPerSecond * 0.12f,
                $"the long-run throw rate drifted to {perSecond:0.0}/s against a configured " +
                $"{cfg.ThrowPerSecond:0.0}/s");
        }

        [Test]
        public void BurstCount_IsCappedByItsSlots_WhateverTheRate()
        {
            // The per-tick pool guard (rule 7): a frame hitch or an absurd rate must not be able to
            // empty the pool in one tick.
            var cfg = Cfg();
            cfg.ThrowPerSecond = 100000f;
            for (uint tick = 0; tick < 200; tick++)
                Assert.LessOrEqual(BowImpactMath.BurstCount(1f, false, in cfg, 1f, tick), cfg.BurstSlots,
                    "the burst must be bounded by its slot count by construction");
        }

        [Test]
        public void BurstCount_IsSilent_Aground_AndAtZeroImpact()
        {
            var cfg = Cfg();
            Assert.AreEqual(0, BowImpactMath.BurstCount(1f, true, in cfg, 1f / 30f, 7u), "aground");
            Assert.AreEqual(0, BowImpactMath.BurstCount(0f, false, in cfg, 1f / 30f, 7u), "no impact");
            Assert.AreEqual(0, BowImpactMath.BurstCount(1f, false, in cfg, 0f, 7u), "no time passed");
        }

        [Test]
        public void TheBowAndTheBubbles_DoNotBurstInLockstep()
        {
            // Two streams that arrive on the same ticks read as ONE event, which is the defect in its
            // purest form. The salts differ for exactly this reason; this proves they actually do.
            var bow = Cfg();
            var bubbles = WakeBubbleConfig.Default;
            const float dt = 1f / 30f;

            int agree = 0;
            for (uint tick = 0; tick < 600; tick++)
            {
                bool bowFired = BowImpactMath.BurstCount(0.5f, false, in bow, dt, tick) > 0;
                bool bubbleFired = WakeBubbleSystem.BurstCount(0.5f, false, in bubbles, dt, tick) > 0;
                if (bowFired == bubbleFired) agree++;
            }
            Assert.Less(agree, 580,
                "the bow's bursts and the bubbles' are firing on the same ticks — decorrelate the salts");
        }

        // ==== property 3: size is HEAVY-TAILED ========================================================

        [Test]
        public void SizeAt_IsBiasedSmall_SoAFewDropletsAreIndividuallyReadable()
        {
            var cfg = Cfg();
            float mid = (cfg.MinSize + cfg.MaxSize) * 0.5f;
            int big = 0;
            const int n = 2000;
            for (int i = 0; i < n; i++)
                if (BowImpactMath.SizeAt(i / (float)(n - 1), in cfg) > mid) big++;

            Assert.Less(big / (float)n, 0.35f,
                "Most of a throw must be fine spray with a few big gouts. A uniform distribution gives " +
                "a uniform field, which is the 'organized, shader-like' read the whole lane exists to " +
                "break — the same argument the bubbles' size bias carries.");
        }

        [Test]
        public void SizeAt_SpansItsKnobs_AndIsMonotone()
        {
            var cfg = Cfg();
            Assert.AreEqual(cfg.MinSize, BowImpactMath.SizeAt(0f, in cfg), 1e-5f);
            Assert.AreEqual(cfg.MaxSize, BowImpactMath.SizeAt(1f, in cfg), 1e-5f);
            float previous = -1f;
            for (float u = 0f; u <= 1.0001f; u += 0.01f)
            {
                float v = BowImpactMath.SizeAt(u, in cfg);
                Assert.GreaterOrEqual(v, previous, "the owner's two size knobs must still mean what they say");
                previous = v;
            }
        }

        // ==== property 4: it dies its OWN way =========================================================

        [Test]
        public void ADroplet_HoldsThenFallsBack_UnlikeTheFoamAndUnlikeABubble()
        {
            var cfg = Cfg();

            // It HOLDS: the foam beside it is already well faded at this age.
            Assert.AreEqual(1f, BowImpactMath.AlphaAt(0.5f, in cfg), 1e-5f,
                "half-way through its life a thrown droplet is still fully there");
            Assert.AreEqual(0f, BowImpactMath.AlphaAt(1f, in cfg), 1e-5f, "and gone at the end");

            // It SHRINKS — the opposite sign from a bubble's burst swell, which is what stops the two
            // streams reading as one.
            float birth = BowImpactMath.SizeOverLife(0.2f, 0f, in cfg);
            float dying = BowImpactMath.SizeOverLife(0.2f, 0.99f, in cfg);
            Assert.AreEqual(0.2f, birth, 1e-5f);
            Assert.Less(dying, birth * 0.6f, "thrown water comes back down and gets smaller doing it");

            var bubble = WakeBubbleConfig.Default;
            float bubbleDying = WakeBubbleSystem.SizeOverLife(0.2f, 0.99f, in bubble);
            Assert.Greater(bubbleDying, birth,
                "a bubble SWELLS as it bursts. If the droplet did the same, the bow and the stern " +
                "would be the same effect again — with the same authored kit, that is exactly how the " +
                "shipped pair read.");
        }

        [Test]
        public void AlphaAndSize_AreMonotone_AndNeverDegenerate()
        {
            var cfg = Cfg();
            float previousAlpha = 2f, previousSize = 999f;
            for (float t = 0f; t <= 1.0001f; t += 0.01f)
            {
                float a = BowImpactMath.AlphaAt(t, in cfg);
                float sz = BowImpactMath.SizeOverLife(0.2f, t, in cfg);
                Assert.LessOrEqual(a, previousAlpha + 1e-5f, "a droplet never brightens");
                Assert.LessOrEqual(sz, previousSize + 1e-5f, "…and never grows");
                Assert.Greater(sz, 0f, "…and never renders through a zero-size quad");
                previousAlpha = a;
                previousSize = sz;
            }
        }

        // ==== the launch, and the determinism boundary ================================================

        [Test]
        public void LaunchVelocity_ThrowsForward_AndHarderOnAHardImpact()
        {
            var cfg = Cfg();
            Vector2 bow = Vector2.up;

            Vector2 gentle = BowImpactMath.LaunchVelocity(bow, 4f, 0f, 0f, in cfg);
            Vector2 hard = BowImpactMath.LaunchVelocity(bow, 4f, 0f, 1f, in cfg);
            Assert.Greater(Vector2.Dot(hard, bow), 0f, "spray goes FORWARD off the cutwater");
            Assert.Greater(hard.magnitude, gentle.magnitude * 1.5f,
                "the same speed must throw water further when the stem is burying — that is the " +
                "difference between a splash and a decal");

            // The fan actually fans, and symmetrically.
            Vector2 left = BowImpactMath.LaunchVelocity(bow, 4f, -1f, 1f, in cfg);
            Vector2 right = BowImpactMath.LaunchVelocity(bow, 4f, +1f, 1f, in cfg);
            Assert.Greater(Vector2.Angle(left, right), cfg.FanHalfAngleDeg,
                "the fan half-angle must actually spread the throw");
            Assert.AreEqual(left.magnitude, right.magnitude, 1e-4f, "…symmetrically");
        }

        [Test]
        public void LaunchVelocity_SurvivesADegenerateBow()
        {
            var cfg = Cfg();
            Vector2 v = BowImpactMath.LaunchVelocity(Vector2.zero, 4f, 0.3f, 1f, in cfg);
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y), "a zero bow vector must not produce NaN");
        }

        [Test]
        public void EverythingIsDeterministic_FromTheTickIndexAlone()
        {
            // Rule 5: no System.Random anywhere in the stream. The same tick must throw the same burst
            // twice in a row, or a save-scrub or a replay would show a different sea.
            var cfg = Cfg();
            for (uint tick = 0; tick < 50; tick++)
                Assert.AreEqual(BowImpactMath.BurstCount(0.7f, false, in cfg, 1f / 30f, tick),
                                BowImpactMath.BurstCount(0.7f, false, in cfg, 1f / 30f, tick),
                                $"tick {tick} is not reproducible");
        }

        // ==== the A/B ================================================================================

        [Test]
        public void TheOffConfig_IsTheShippedMeteredStream()
        {
            Assert.IsFalse(BowImpactConfig.Off.Enabled,
                "Off must actually be off — it is how the owner A/Bs this against what he played.");
            Assert.IsTrue(BowImpactConfig.Default.Enabled,
                "…and the default must be ON, or this round ships a change he cannot see.");

            // Every other value is identical, so the A/B swaps ONE thing.
            var on = BowImpactConfig.Default;
            var off = BowImpactConfig.Off;
            Assert.AreEqual(on.ThrowPerSecond, off.ThrowPerSecond, 0f);
            Assert.AreEqual(on.SeaGain, off.SeaGain, 0f);
            Assert.AreEqual(on.FallFraction, off.FallFraction, 0f);
        }

        [Test]
        public void TheAuthoredSpraySheet_IsRetired_AndTheReplacementIsOn()
        {
            // The paired half of the same verdict item: the sheet was the last boat-attached authored
            // sprite in the wake, and the owner named it as reading "baked statically". Retiring it
            // without turning the live bow on would leave the stem with nothing at all.
            Assert.IsFalse(BowSprayGradeConfig.Default.SprayEnabled,
                "the authored bow-spray sheet must ship OFF (owner eyeball 2026-08-27)");
            Assert.IsTrue(BowImpactConfig.Default.Enabled,
                "…and the impact-driven droplets must ship ON in its place");
        }

        [Test]
        public void TheTransomPlume_WasAlreadyRetired_AndStaysThatWay()
        {
            // A record correction, kept as a test. The round-2 brief named the WakeSpriteLibrary plume
            // tiers as one of the sprites reading statically — but they have shipped OFF since
            // 2026-08-06, when the deposited wake wave replaced them. Anyone re-reading that brief and
            // "fixing" the plume would be turning a knob that was already in the right position; what
            // the owner was actually looking at astern is the wake WAVE's crests, which is why they —
            // not the plume — gained the age ramp and the per-crest variance this round.
            Assert.IsFalse(WakeGradeConfig.Default.PlumeEnabled,
                "the authored transom plume was retired 2026-08-06 and must stay retired");
            Assert.IsTrue(WakeWaveConfig.Default.Enabled,
                "…because the deposited wake wave is what draws the rear wake now");
            Assert.Greater(WakeWaveConfig.Default.AgeStrength, 0f,
                "…and it must AGE, which is the round-2 change it was missing");
        }
    }
}
