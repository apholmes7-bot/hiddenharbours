using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE-logic guard for the WAKE WAVE — the owner's 2026-08-06 ask: <i>"a real WAKE WAVE should
    /// replace the old wake sprite"</i>, i.e. displaced water responding to the hull rather than an
    /// authored Kelvin-V decal pinned to the transom.
    ///
    /// <para>Every claim is exercised on the side-effect-free <see cref="WakeWaveMath"/> headless — no
    /// scene, no sprites. What is pinned here is what makes the wave a WAVE rather than a repaint:
    /// it is born from the hull's grade, it LIFTS through the sea's own shared exaggeration, it
    /// COLLAPSES to exactly nothing by the end of its life, and each crest's shading faces the way its
    /// water is travelling.</para>
    /// </summary>
    public class WakeWaveMathTests
    {
        private static WakeWaveConfig Cfg() => WakeWaveConfig.Default;

        // ==== birth: the hull's own grade decides how much water it moves ============================

        [Test]
        public void NoWaveAtRest_AndNoneBelowTheOnset()
        {
            var c = Cfg();
            Assert.AreEqual(0f, WakeWaveMath.BirthAmplitudeMeters(1f, 0f, in c), 1e-6f,
                "a boat lying still displaces nothing.");
            Assert.AreEqual(0f, WakeWaveMath.BirthAmplitudeMeters(1f, c.SpeedOnset, in c), 1e-6f);
        }

        [Test]
        public void Amplitude_RisesWithSpeed_AndWithTheHullGrade()
        {
            var c = Cfg();
            float slow = WakeWaveMath.BirthAmplitudeMeters(0.5f, c.SpeedOnset + 0.5f, in c);
            float fast = WakeWaveMath.BirthAmplitudeMeters(0.5f, c.SpeedOnset + c.SpeedOnsetRange, in c);
            Assert.Greater(fast, slow, "the same hull driven harder must raise a bigger wave.");

            float small = WakeWaveMath.BirthAmplitudeMeters(0f, 6f, in c);
            float big = WakeWaveMath.BirthAmplitudeMeters(1f, 6f, in c);
            Assert.Greater(big, small, "a bigger/heavier hull must raise a bigger wave at the same speed.");
            Assert.AreEqual(c.AmplitudeAtMagnitude1, big, 1e-4f,
                "at full grade and full onset the wave is the configured maximum — no hidden scaling.");
        }

        [Test]
        public void TheDoryStaysGentle_ItIsTheBottomOfTheRamp()
        {
            // The dory's rowed top speed is around 1.4 m/s and her grade is near the floor: she should
            // push a low roll, not a standing crest. The number is the CONFIG's, so this pins the shape
            // (a small fraction of the maximum), never a tuned constant that will move.
            var c = Cfg();
            float dory = WakeWaveMath.BirthAmplitudeMeters(0.12f, 1.4f, in c);
            Assert.Greater(dory, 0f, "a rowed dory does move water.");
            Assert.Less(dory, c.AmplitudeAtMagnitude1 * 0.35f,
                "the dory must sit at the bottom of the ramp, not wear a big hull's wave.");
        }

        // ==== life: the wave collapses, it does not merely fade ======================================

        [Test]
        public void Amplitude_CollapsesToExactlyZero_ByEndOfLife()
        {
            var c = Cfg();
            Assert.AreEqual(0.25f, WakeWaveMath.AmplitudeAt(0.25f, 0f, in c), 1e-6f, "full height at birth.");
            Assert.AreEqual(0f, WakeWaveMath.AmplitudeAt(0.25f, 1f, in c), 1e-6f,
                "a crest still standing when its sprite vanished would POP.");
        }

        [Test]
        public void Amplitude_NeverGrowsWithAge()
        {
            var c = Cfg();
            float previous = float.MaxValue;
            for (int i = 0; i <= 100; i++)
            {
                float a = WakeWaveMath.AmplitudeAt(0.3f, i / 100f, in c);
                Assert.LessOrEqual(a, previous + 1e-6f, "a laid crest must never rise again.");
                previous = a;
            }
        }

        [Test]
        public void CollapsePowerAboveOne_HoldsTheCrestNearTheBoat()
        {
            // The shape claim behind the default: >1 means the crest keeps most of its height early and
            // lets go late, which is how a wake behaves. A linear fall would look like a dissolve.
            var c = Cfg();
            Assert.Greater(c.CollapsePower, 1f);
            float half = WakeWaveMath.AmplitudeAt(1f, 0.5f, in c);
            Assert.Less(half, 0.5f, "with CollapsePower > 1 the crest is BELOW half height at half life.");

            var linear = c; linear.CollapsePower = 1f;
            Assert.AreEqual(0.5f, WakeWaveMath.AmplitudeAt(1f, 0.5f, in linear), 1e-5f);
        }

        // ==== lift: the wave stands on the SAME sea the hull rides ===================================

        [Test]
        public void ScreenLift_RidesTheSeasOwnExaggeration_AndIsHonestMetresWithoutIt()
        {
            // The whole reason the exaggeration is borrowed rather than re-declared: retune the
            // displaced sea and the hull's wave must follow it, never sit at a cached scale.
            Assert.AreEqual(0.2f, WakeWaveMath.ScreenLiftMeters(0.2f, 1f), 1e-6f,
                "with no displaced sea the wave draws at its honest metres.");
            Assert.AreEqual(0.3f, WakeWaveMath.ScreenLiftMeters(0.2f, 1.5f), 1e-6f,
                "with the shipped x1.5 the wave is exaggerated exactly as the swell is.");
            Assert.AreEqual(0f, WakeWaveMath.ScreenLiftMeters(0f, 1.5f), 1e-6f,
                "a collapsed crest lies flat on the water.");
        }

        [Test]
        public void CrestFootprint_GrowsWithAmplitude_AndNeverCollapsesToZero()
        {
            var c = Cfg();
            Assert.Greater(WakeWaveMath.CrestHeightMeters(0.3f, in c),
                           WakeWaveMath.CrestHeightMeters(0.1f, in c),
                           "a bigger wave is a broader one.");
            Assert.AreEqual(c.MinCrestHeightMeters, WakeWaveMath.CrestHeightMeters(0f, in c), 1e-6f,
                "a dying crest thins to a ripple; a zero-height quad would flicker through a texel.");
        }

        // ==== orientation: the shading has to face the way the water is going ========================

        [Test]
        public void ArmCrests_PointTheirShadingOutward_OnBothSides()
        {
            // The crest sprite is not symmetric across its short axis: it carries a lit crest on one
            // side of its waterline and a hollow on the other. Point that inward and the arm reads as a
            // wave rolling INTO the boat's own track.
            var headings = new[] { Vector2.up, Vector2.right, new Vector2(1f, 1f).normalized,
                                   new Vector2(-0.3f, -0.9f).normalized };
            foreach (Vector2 track in headings)
            foreach (int side in new[] { -1, +1 })
            {
                float deg = WakeWaveMath.ArmCrestOrientDeg(track, side, 19f);
                float rad = deg * Mathf.Deg2Rad;
                Vector2 longAxis = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 spriteUp = new Vector2(-longAxis.y, longAxis.x);     // the sprite's own +Y
                Vector2 outward = WakeTrailMath.Lateral(track, side);

                Assert.Greater(Vector2.Dot(spriteUp, outward), 0f,
                    $"arm crest on side {side} of heading {track} faces its lit crest INWARD.");

                // …and it is still laid along the arm locus, not turned into some new direction.
                Vector2 arm = WakeTrailMath.ArmDir(track, side, 19f);
                Assert.Greater(Mathf.Abs(Vector2.Dot(longAxis, arm)), 0.999f,
                    "the ribbon must still run down the emergent arm — only its FACING may flip.");
            }
        }

        [Test]
        public void SternRoll_RunsAcrossTheTrack_AndFacesAstern()
        {
            foreach (Vector2 track in new[] { Vector2.up, Vector2.right, new Vector2(0.6f, -0.8f) })
            {
                Vector2 t = track.normalized;
                float deg = WakeWaveMath.TransomOrientDeg(t);
                float rad = deg * Mathf.Deg2Rad;
                Vector2 longAxis = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 spriteUp = new Vector2(-longAxis.y, longAxis.x);

                Assert.AreEqual(0f, Vector2.Dot(longAxis, t), 1e-4f,
                    "the stern roll is laid ACROSS the track, not along it.");
                Assert.Greater(Vector2.Dot(spriteUp, -t), 0.999f,
                    "the roll travels ASTERN, so that is the side its lit crest belongs on.");
            }
        }

        [Test]
        public void DegenerateInputs_ReadZero_NeverNaN()
        {
            Assert.AreEqual(0f, WakeWaveMath.TransomOrientDeg(Vector2.zero), 1e-6f);
            Assert.IsFalse(float.IsNaN(WakeWaveMath.ArmCrestOrientDeg(Vector2.zero, +1, 19f)));
        }

        // ==== the A/B and the pool budget ============================================================

        // ==== PER-CREST VARIANCE + AGEING (owner eyeball 2026-08-27, defect 1) ======================
        //
        // "the old static rear wakes ... read as a sprite baked staticly, are never manipulated". The
        // crests WERE the rear wake and WERE the one stream with no variation of any kind: same sprite,
        // same baked angle, same length, and — uniquely — a tint that never moved at all, because #665
        // excluded them from the age ramp to protect the sprite's own crest/hollow profile.

        [Test]
        public void CrestLength_VariesPerCrest_SoARunOfThemIsNotAStamp()
        {
            var cfg = WakeWaveConfig.Default;
            Assert.Greater(cfg.LengthJitter, 0f, "the shipped wave must actually vary its crests");

            var seen = new System.Collections.Generic.HashSet<float>();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 200; i++)
            {
                float seed = i / 199f;
                float len = WakeWaveMath.CrestLengthMeters(4f, seed, in cfg);
                seen.Add(Mathf.Round(len * 1000f) / 1000f);
                min = Mathf.Min(min, len);
                max = Mathf.Max(max, len);
            }
            Assert.Greater(seen.Count, 100, "every crest came out the same length — that is the stamp");
            Assert.GreaterOrEqual(min, 4f * (1f - cfg.LengthJitter) - 1e-4f, "jitter escaped its bound (low)");
            Assert.LessOrEqual(max, 4f * (1f + cfg.LengthJitter) + 1e-4f, "jitter escaped its bound (high)");
        }

        [Test]
        public void CrestOrientation_WobblesSmall_SoTheVStillReadsAsAV()
        {
            var cfg = WakeWaveConfig.Default;
            Assert.Greater(cfg.OrientJitterDeg, 0f, "…and must actually wobble");
            Assert.LessOrEqual(cfg.OrientJitterDeg, 8f,
                "The baked analytic angle IS the emergent V, and orienting crests by anything looser " +
                "was the 'horizontal dashes' defect. This is a wobble, not a scatter.");

            for (int i = 0; i < 200; i++)
            {
                float got = WakeWaveMath.CrestOrientDeg(37f, i / 199f, in cfg);
                Assert.LessOrEqual(Mathf.Abs(got - 37f), cfg.OrientJitterDeg + 1e-4f,
                    "the wobble escaped its configured bound");
            }
        }

        [Test]
        public void BothCrestJitters_AreBitExactPassthroughsAtZero()
        {
            var cfg = WakeWaveConfig.Default;
            cfg.LengthJitter = 0f;
            cfg.OrientJitterDeg = 0f;
            for (int i = 0; i < 50; i++)
            {
                float seed = i / 49f;
                Assert.AreEqual(4.25f, WakeWaveMath.CrestLengthMeters(4.25f, seed, in cfg), 0f,
                    "length jitter 0 must return the shared length to the bit");
                Assert.AreEqual(-113.5f, WakeWaveMath.CrestOrientDeg(-113.5f, seed, in cfg), 0f,
                    "orient jitter 0 must return the baked angle to the bit");
            }
        }

        [Test]
        public void TheCrestJitters_AreDeterministic_AndDecorrelated()
        {
            var cfg = WakeWaveConfig.Default;
            // Rule 5: same seed, same crest, every run.
            Assert.AreEqual(WakeWaveMath.CrestLengthMeters(3f, 0.31f, in cfg),
                            WakeWaveMath.CrestLengthMeters(3f, 0.31f, in cfg), 0f);

            // …and a crest that happens to be long must not also be systematically rotated one way, or
            // the two "variations" are one variation wearing two hats.
            int longAndLeft = 0, longAndRight = 0;
            for (int i = 0; i < 400; i++)
            {
                float seed = i / 399f;
                if (WakeWaveMath.CrestLengthMeters(3f, seed, in cfg) <= 3f) continue;
                if (WakeWaveMath.CrestOrientDeg(0f, seed, in cfg) < 0f) longAndLeft++; else longAndRight++;
            }
            int total = longAndLeft + longAndRight;
            Assert.Greater(total, 50, "sanity: some crests must come out long");
            Assert.Greater(Mathf.Min(longAndLeft, longAndRight), total * 0.3f,
                "length and orientation are correlated — they must be independent draws off the seed");
        }

        [Test]
        public void TheCrests_ShipAging_AndZeroIsTheOldFlatWhite()
        {
            Assert.Greater(WakeWaveConfig.Default.AgeStrength, 0f,
                "The crests were the ONE wake stream that never changed colour for its whole life — " +
                "which is exactly the 'baked statically, never manipulated' the owner reported. " +
                "AgeStrength 0 is the look he already rejected and cannot be the shipped default.");
            Assert.LessOrEqual(WakeWaveConfig.Default.AgeStrength, 1f, "it is a 0..1 dial");
        }

        [Test]
        public void TheDefaultRetiresThePlumeDecal_AndStandsTheWaveInItsPlace()
        {
            Assert.IsFalse(WakeGradeConfig.Default.PlumeEnabled,
                "the authored Kelvin-V decal is what the wake wave replaced (owner, 2026-08-06).");
            Assert.IsTrue(WakeWaveConfig.Default.Enabled, "…so something has to be drawing the wake.");
            Assert.IsTrue(WakeWaveConfig.Default.TransomCrest,
                "the stern roll is the piece the decal was really drawing; without it the wake has a " +
                "hole right behind the transom.");
        }

        [Test]
        public void TheParticleBudget_CountsTheSternRoll_EvenWhenItIsSwitchedOff()
        {
            // A budget that only holds while a toggle is off is not a budget. The default pools are
            // 96 foam + 48 crests + 24 stern rolls; the worst case must stay comfortably under them.
            var trail = WakeTrailConfig.Default;
            int worst = WakeTrailMath.MaxParticlesPerTick(in trail);
            Assert.AreEqual(trail.MaxDepositsPerTick * (2 + 1 + 1 + WakeTrailMath.ChurnPuffCount(in trail)),
                            worst, "the budget must include the stern-roll crest.");
            Assert.Less(worst, 96, "one tick must never be able to fill the foam pool.");
        }
    }
}
