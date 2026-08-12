using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The character rides the boat</b> — the pure pose maths behind it (<see cref="DeckRideMath"/>),
    /// and the two things it must agree with to be a RIDE rather than an independent wobble: the hull's own
    /// baked rock cycle, and the oar overlays that already ride it.
    ///
    /// <para>The load-bearing assertions here are the AGREEMENT ones. A rider that leans by the right
    /// amount at the wrong moment is worse than one that doesn't lean at all, so the tests pin the phase
    /// convention (crest 90° → frame 2, trough 270° → frame 6) against <c>DoryOarMath</c>'s own — the two
    /// live in different assemblies and cannot share a constant, which is exactly why they are pinned.</para>
    /// </summary>
    public class DeckRideMathTests
    {
        // The shipped iso dory's baked cycle, as DoryOarLayer's defaults state it.
        const int Frames = 8;
        const float DeckRoll = 5f;
        const float HeavePixels = 1.6f;
        const float PitchLift = 0.02f;
        const float Ppu = 32f;

        static DeckRidePose RideAt(float phase, float footing = 1f, float strength = 1f)
            => DeckRideMath.Ride(phase, DeckRoll, HeavePixels, PitchLift, Ppu, footing, strength);

        // ---- the level pose: a still deck never gets a swaying passenger -----------------------------

        [Test]
        public void LevelDeck_LeavesTheBodySquare()
        {
            foreach (float phase in new[] { DeckRideMath.LevelPhaseDegrees, -1f, -0.001f, -360f })
            {
                var pose = RideAt(phase);
                Assert.AreEqual(0f, pose.RollDegrees, 1e-6f, $"phase {phase} must not lean");
                Assert.AreEqual(0f, pose.LiftMeters, 1e-6f, $"phase {phase} must not lift");
            }
        }

        [Test]
        public void ANonFinitePhase_ParksTheRider_RatherThanLaunchingThem()
        {
            // A NaN reaching a transform is a sprite that disappears for the rest of the session.
            Assert.IsFalse(DeckRideMath.IsRocking(float.NaN));
            Assert.IsFalse(DeckRideMath.IsRocking(float.PositiveInfinity));
            Assert.AreEqual(0f, RideAt(float.NaN).RollDegrees, 1e-6f);
            Assert.AreEqual(0f, RideAt(float.PositiveInfinity).LiftMeters, 1e-6f);
        }

        [Test]
        public void StrengthZero_RestoresTheOldBoltUprightRead_Exactly()
        {
            // The owner's A/B: the whole feature off is not "nearly level", it is level.
            for (int f = 0; f < Frames; f++)
            {
                var pose = RideAt(DeckRideMath.PhaseForRockFrame(f, Frames), footing: 1f, strength: 0f);
                Assert.AreEqual(0f, pose.RollDegrees, 0f, $"frame {f}");
                Assert.AreEqual(0f, pose.LiftMeters, 0f, $"frame {f}");
            }
        }

        // ---- the cycle sits where the hull's does ---------------------------------------------------

        [Test]
        public void FrameToPhase_MatchesTheHullsOwnBakedCycle_CrestOnFrame2_TroughOnFrame6()
        {
            // Core may not reference Boats (rule 4), so DeckRideMath restates DoryOarMath's frame→phase
            // rule. This is the pin that stops the two drifting: same frame, same phase, every frame.
            for (int f = 0; f < Frames; f++)
                Assert.AreEqual(DoryOarMath.RockPhaseDegrees(f, Frames),
                                DeckRideMath.PhaseForRockFrame(f, Frames), 1e-4f, $"frame {f}");

            Assert.AreEqual(90f, DeckRideMath.PhaseForRockFrame(2, Frames), 1e-4f, "crest");
            Assert.AreEqual(270f, DeckRideMath.PhaseForRockFrame(6, Frames), 1e-4f, "trough");
        }

        [Test]
        public void FrameToPhase_IsLevelForTheCalmFrame_AndWrapsSafely()
        {
            Assert.AreEqual(DeckRideMath.LevelPhaseDegrees, DeckRideMath.PhaseForRockFrame(-1, Frames));
            Assert.AreEqual(0f, DeckRideMath.PhaseForRockFrame(Frames, Frames), 1e-4f, "a full cycle wraps to 0");
            Assert.AreEqual(45f, DeckRideMath.PhaseForRockFrame(Frames + 1, Frames), 1e-4f);
            Assert.AreEqual(0f, DeckRideMath.PhaseForRockFrame(0, 0), 1e-4f, "a degenerate count never divides by 0");
        }

        [Test]
        public void TheRider_LeansTheSAMEWayTheOarsDo_AtEveryFrameOfTheCycle()
        {
            // The oars are the shipping proof that this cycle is right — they have leaned on the hull
            // since #202. A rider out of step with them is out of step with the hull.
            for (int f = 0; f < Frames; f++)
            {
                var oars = DoryOarMath.RockPose(f, Frames, DeckRoll, PitchLift, HeavePixels, Ppu, 1f);
                var body = RideAt(DeckRideMath.PhaseForRockFrame(f, Frames), footing: 1f);

                Assert.AreEqual(oars.RollDegrees, body.RollDegrees, 1e-4f,
                                $"frame {f}: a fully-committed rider leans exactly as the oars do");
                Assert.AreEqual(oars.OffsetY, body.LiftMeters, 1e-4f,
                                $"frame {f}: and rises exactly as far");
            }
        }

        // ---- roll is braced, lift is exact -----------------------------------------------------------

        [Test]
        public void Footing_ScalesTheLEANOnly_NeverTheLIFT()
        {
            // The two channels are deliberately different: a fisher braces against the roll, but their
            // feet are ON the deck — gaining the heave down would sink them through the planking.
            const float phase = 90f;                       // the crest: sin = 1, so the lean is at its peak
            var bolted = RideAt(phase, footing: 1f);
            var braced = RideAt(phase, footing: 0.6f);
            var upright = RideAt(phase, footing: 0f);

            Assert.AreEqual(DeckRoll, bolted.RollDegrees, 1e-4f, "bolted to the deck takes the whole roll");
            Assert.AreEqual(DeckRoll * 0.6f, braced.RollDegrees, 1e-4f, "braced takes its share");
            Assert.AreEqual(0f, upright.RollDegrees, 1e-4f, "footing 0 IS the old bug, on purpose");

            Assert.AreEqual(bolted.LiftMeters, braced.LiftMeters, 1e-6f, "the deck lifts them the same");
            Assert.AreEqual(bolted.LiftMeters, upright.LiftMeters, 1e-6f, "…even standing perfectly upright");
        }

        [Test]
        public void Footing_IsClamped_SoAnOverdrivenAssetCannotThrowTheFisherOffTheBoat()
        {
            Assert.AreEqual(RideAt(90f, footing: 1f).RollDegrees, RideAt(90f, footing: 4f).RollDegrees, 1e-4f);
            Assert.AreEqual(0f, RideAt(90f, footing: -2f).RollDegrees, 1e-4f);
        }

        [Test]
        public void HeaveIsReproducedInMetres_AtTheSheetsOwnPixelScale()
        {
            // 1.6 px at PPU 32 = 0.05 m of screen lift at the crest, plus nothing from pitch (cos 90 = 0).
            var crest = RideAt(90f);
            Assert.AreEqual(HeavePixels / Ppu, crest.LiftMeters, 1e-5f);

            var trough = RideAt(270f);
            Assert.AreEqual(-HeavePixels / Ppu, trough.LiftMeters, 1e-5f, "the trough drops them as far");
        }

        [Test]
        public void APixelsPerUnitOfZero_IsGuarded_NotDividedBy()
        {
            var pose = DeckRideMath.Ride(90f, DeckRoll, HeavePixels, PitchLift, 0f, 1f, 1f);
            Assert.IsFalse(float.IsNaN(pose.LiftMeters), "no NaN");
            Assert.IsFalse(float.IsInfinity(pose.LiftMeters), "no infinity");
        }

        // ---- the shape of the cycle ------------------------------------------------------------------

        /// <summary>Where the LIFT actually peaks, in degrees of wave phase. The lift is
        /// <c>heave·sin θ + pitch·cos θ</c> — one sinusoid of amplitude <c>√(heave² + pitch²)</c> shifted
        /// by <c>φ = atan2(pitch, heave)</c> — so its maximum is at <c>90° − φ</c>, NOT at the crest.</summary>
        static float LiftPeakPhaseDegrees()
        {
            float heaveMeters = HeavePixels / Ppu;
            float phi = Mathf.Atan2(PitchLift, heaveMeters) * Mathf.Rad2Deg;
            return 90f - phi;
        }

        [Test]
        public void TheRide_IsONECleanCycle_WithExactlyOnePeakAndOneTrough()
        {
            // What must be true for the figure not to "hunt" on a smoothly rocking hull: the lift turns
            // round exactly TWICE per cycle. Stated as turning points rather than as "monotone up to 90°",
            // because the peak is NOT at 90° — see the test below.
            const float step = 0.5f;
            int turns = 0;
            float previous = RideAt(0f).LiftMeters;
            float next = RideAt(step).LiftMeters;
            int direction = System.Math.Sign(next - previous);

            for (float phase = step; phase < 360f; phase += step)
            {
                float lift = RideAt(phase).LiftMeters;
                int d = System.Math.Sign(lift - previous);
                // Ignore flat samples at the turning point itself — float noise there is not a wiggle.
                if (d != 0 && d != direction) { turns++; direction = d; }
                previous = lift;
            }

            Assert.AreEqual(2, turns,
                "one rise and one fall per cycle — any more is the fisher bobbing where the sea is not");
        }

        [Test]
        public void TheLiftPEAKSJustBEFORETheCrest_BecausePitchLeadsHeave()
        {
            // A finding worth pinning rather than a rule being restated. Heave peaks AT the crest (90°) and
            // the pitch term a quarter-cycle earlier, so their sum tops out ~22° ahead of it at the shipped
            // amplitudes — the hull riding UP the face before she tops it. This is not a quirk of the rider:
            // DoryOarMath.RockPose sums the same two terms the same way, so the oars have always led the
            // crest by the same margin and the owner's approved rowing feel already contains it. If it ever
            // reads wrong, the fix is the amplitudes, not the phase.
            float peak = LiftPeakPhaseDegrees();
            Assert.Less(peak, 90f, "the lift tops out before the crest");
            Assert.Greater(peak, 60f, "…but only just — this is a lead, not a different wave");

            float atPeak = RideAt(peak).LiftMeters;
            for (float phase = 0f; phase < 360f; phase += 1f)
                Assert.LessOrEqual(RideAt(phase).LiftMeters, atPeak + 1e-6f,
                                   $"nothing in the cycle rises above the algebraic peak (at {phase}°)");

            // And the peak is the closed form the algebra predicts, so the amplitude is bounded exactly.
            float heaveMeters = HeavePixels / Ppu;
            Assert.AreEqual(Mathf.Sqrt(heaveMeters * heaveMeters + PitchLift * PitchLift), atPeak, 1e-5f);
        }

        [Test]
        public void TheROLLPeaksExactlyAtTheCrest_BecauseItIsAPureSine()
        {
            // The roll channel has no second term, so unlike the lift it is not shifted at all: peak lean at
            // the crest, peak counter-lean in the trough, level at both zero-crossings.
            Assert.AreEqual(DeckRoll, RideAt(90f).RollDegrees, 1e-4f, "crest");
            Assert.AreEqual(-DeckRoll, RideAt(270f).RollDegrees, 1e-4f, "trough");
            Assert.AreEqual(0f, RideAt(0f).RollDegrees, 1e-4f, "rising zero-crossing");
            Assert.AreEqual(0f, RideAt(180f).RollDegrees, 1e-4f, "falling zero-crossing");
        }

        [Test]
        public void TheRide_IsPeriodic_SoALongVoyageNeverDrifts()
        {
            for (float phase = 0f; phase < 360f; phase += 17f)
            {
                var a = RideAt(phase);
                var b = RideAt(phase + 360f);
                Assert.AreEqual(a.RollDegrees, b.RollDegrees, 1e-3f, $"{phase}°");
                Assert.AreEqual(a.LiftMeters, b.LiftMeters, 1e-4f, $"{phase}°");
            }
        }

        [Test]
        public void TheRideStaysSmall_AtTheShippedAmplitudes_BecauseReadableBeatsBig()
        {
            // A guard on the FEEL, not a restatement of the formula: at 64×92 px and PPU 32 a lean past a
            // few degrees or a lift past a few pixels reads as the fisher falling over, not riding.
            for (float phase = 0f; phase < 360f; phase += 5f)
            {
                var pose = RideAt(phase, footing: 0.6f);
                Assert.LessOrEqual(Mathf.Abs(pose.RollDegrees), DeckRoll * 0.6f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(pose.LiftMeters) * Ppu, 3f,
                                   "under three pixels of travel at the shipped amplitudes");
            }
        }

        // ---- the DISPLACED RIDE composed on top (owner, 2026-08-11) --------------------------------
        //
        // Everything above is the rock CYCLE — what the hull's art draws in place, a couple of pixels of
        // it. On a displaced sea the whole boat is ALSO translated bodily by the water's lift, metres of
        // it, and that was missing from the read entirely: the fisher held her physics-root altitude
        // while the drawn deck bobbed past her ("not fixed to the same point on the bobbing boat deck").
        // RidingHull is that composition, and its whole job is to be boring — add to the lift, touch
        // nothing else.

        [Test]
        public void TheHullsRide_AddsToTheLift_AndLeavesTheLeanAlone()
        {
            // A world translation moves a deck without tilting it. If the ride ever reached the roll
            // channel, a heaving boat would lay her crew over on flat water.
            var rock = RideAt(37f);
            var ridden = DeckRideMath.RidingHull(rock, 0.8f, 1f);

            Assert.AreEqual(rock.RollDegrees, ridden.RollDegrees, 0f,
                            "the lean is the DECK's tilt and nothing else — exactly unchanged");
            Assert.AreEqual(rock.LiftMeters + 0.8f, ridden.LiftMeters, 1e-6f,
                            "and the ride adds to the lift, whole: their boots are on the planking");
        }

        [Test]
        public void TheHullsRide_IsNOTBraced_BecauseBracingIsAboutTiltNotAltitude()
        {
            // Footing scales the LEAN (a fisher braces against a tilting deck). It must not touch the
            // ride: no amount of bracing keeps a body at one altitude while the deck under it rises a
            // metre — that IS the defect, not the fix.
            var braced = DeckRideMath.RidingHull(RideAt(90f, footing: 0f), 1.3f, 1f);
            var rigid = DeckRideMath.RidingHull(RideAt(90f, footing: 1f), 1.3f, 1f);

            Assert.AreEqual(RideAt(90f, footing: 0f).LiftMeters + 1.3f, braced.LiftMeters, 1e-6f);
            Assert.AreEqual(RideAt(90f, footing: 1f).LiftMeters + 1.3f, rigid.LiftMeters, 1e-6f);
            Assert.AreEqual(0f, braced.RollDegrees, 1e-6f, "footing 0 still stands perfectly upright");
        }

        [Test]
        public void TheHullsRide_ScalesWithItsOwnStrength_AndZeroIsTheExactOldPicture()
        {
            var rock = RideAt(210f);
            Assert.AreEqual(rock.LiftMeters, DeckRideMath.RidingHull(rock, 2.5f, 0f).LiftMeters, 0f,
                            "strength 0 is the owner's A/B for THIS term: bit-identical to before it existed");
            Assert.AreEqual(rock.LiftMeters, DeckRideMath.RidingHull(rock, 2.5f, -1f).LiftMeters, 0f,
                            "a negative strength is OFF, never inverted");
            Assert.AreEqual(rock.LiftMeters + 1.25f,
                            DeckRideMath.RidingHull(rock, 2.5f, 0.5f).LiftMeters, 1e-6f);
        }

        [Test]
        public void ANonFiniteHullRide_ParksTheRider_RatherThanLaunchingThem()
        {
            // Same discipline as the phase guard above. A NaN reaching a transform does not merely look
            // wrong — Unity refuses the write and the figure is stuck there for the rest of the voyage.
            var rock = RideAt(120f);
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var pose = DeckRideMath.RidingHull(rock, bad, 1f);
                Assert.AreEqual(rock.LiftMeters, pose.LiftMeters, 0f, $"{bad} must leave the pose alone");
                Assert.AreEqual(rock.RollDegrees, pose.RollDegrees, 0f);
            }
        }

        [Test]
        public void ARideOnALevelDeck_IsStillARide()
        {
            // A hull can be riding a metre of swell while drawing NO rock cycle at all: glass-calm
            // amplitudes, a hull with no rock grid, a hull sunk to her waterline on a still sea. The lift
            // must survive the level pose — an early-out on "not rocking" would drop exactly the term
            // that matters most.
            var pose = DeckRideMath.RidingHull(DeckRidePose.Level, 0.9f, 1f);
            Assert.AreEqual(0.9f, pose.LiftMeters, 1e-6f);
            Assert.AreEqual(0f, pose.RollDegrees, 0f, "…and still perfectly level under their boots");
        }
    }
}
