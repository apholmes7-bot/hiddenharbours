using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>SHE TRIMS TO HER SPEED — the law</b> (owner, 2026-09-21: <i>"Also i want trim added to the
    /// boats depending on speed and deacceleration"</i>; design <c>boats-and-navigation.md</c> §2.7.3).
    /// Pure <see cref="HullTrimMath"/>: no scene, no clock, no physics.
    ///
    /// <para>Every expected number is worked by hand from the formula in the design doc, never read
    /// back from the code. The test hull is chosen so that arithmetic is short: her length is 4/g
    /// metres, so √(g·L) is 2 m/s and her Froude number is half her speed. Her hump (Fn 0.40) is at
    /// 0.8 m/s and her planing settle (Fn 0.55) is complete at 1.1 m/s.</para>
    /// </summary>
    public class HullTrimMathTests
    {
        const float Tol = 1e-4f;
        // √(9.81 × 4/9.81) = 2 m/s, so Fn = u / 2.
        const float HalfFroudeLength = 4f / 9.81f;

        // Rise 6°, give 3° back over the hump, squat 1.5°/(m/s²), dip 2°/(m/s²), limits +8°/−4°, lag 0.5 s.
        static readonly HullTrimProfile P = new HullTrimProfile(
            humpDegrees: 6f, planingDropDegrees: 3f, accelDegreesPerMps2: 1.5f, decelDegreesPerMps2: 2f,
            maxBowUpDegrees: 8f, maxBowDownDegrees: 4f, responseSeconds: 0.5f,
            humpFroude: 0.4f, planingFroude: 0.55f, lengthMeters: HalfFroudeLength);

        static object _sink;

        static float Target(float waterSpeed, float heldSpeed, float acceleration)
            => HullTrimMath.TargetDegrees(P, waterSpeed, heldSpeed, acceleration);

        // ------------------------------------------------------------------ rest and the speed curve

        [Test]
        public void AtRest_SheSitsExactlyLevel()
        {
            Assert.AreEqual(0f, Target(0f, 0f, 0f), "at rest with no drive");
            Assert.AreEqual(0f, Target(0f, float.PositiveInfinity, 0f), "at rest with the throttle held");
        }

        // S(x) = x²(3 − 2x). Rise: 6·S(Fn/0.4). Settle: 3·S((Fn − 0.4)/0.15). Fn = u/2.
        [TestCase(0f, 0f)]
        [TestCase(0.4f, 3f)]         // Fn 0.2: halfway up the hump, S(0.5) = 0.5
        [TestCase(0.8f, 6f)]         // Fn 0.4: the hump, the full rise
        [TestCase(0.875f, 5.53125f)] // Fn 0.4375: a quarter over, S(0.25) = 0.15625 of the 3° drop
        [TestCase(0.95f, 4.5f)]      // Fn 0.475: halfway over, half the drop
        [TestCase(1.1f, 3f)]         // Fn 0.55: settled
        [TestCase(3f, 3f)]           // far past it: still settled, no further change
        public void TheSteadyBow_RisesToTheHumpAndAPlaningHullSettlesPastIt(float speed, float expected)
        {
            Assert.AreEqual(expected, Target(speed, float.PositiveInfinity, 0f), Tol,
                $"steady bow angle at {speed} m/s through the water");
        }

        [Test]
        public void ADisplacementHull_KeepsHerRiseAndNeverPlanes()
        {
            var displacement = new HullTrimProfile(6f, 0f, 1.5f, 2f, 8f, 4f, 0.5f, 0.4f, 0.55f, HalfFroudeLength);
            Assert.AreEqual(6f, HullTrimMath.TargetDegrees(displacement, 3f, float.PositiveInfinity, 0f), Tol,
                "no planing drop authored: well past the hump she still carries her full rise");
        }

        // ------------------------------------------------------------------ acceleration and deceleration

        [Test]
        public void UnderPower_TheBowSquatsUp_AtAnySpeed()
        {
            Assert.AreEqual(1.5f, HullTrimMath.DynamicDegrees(P, 0f, 1f), Tol, "from rest");
            Assert.AreEqual(1.5f, HullTrimMath.DynamicDegrees(P, 0.4f, 1f), Tol, "halfway up the hump");
            Assert.AreEqual(7.5f, Target(0.8f, float.PositiveInfinity, 1f), Tol,
                "at the hump: the full 6° rise plus 1.5° of squat");
        }

        [Test]
        public void AsSheSlows_TheNoseDips_WeightedByTheWaySheCarries()
        {
            Assert.AreEqual(-1f, HullTrimMath.DynamicDegrees(P, 0.4f, -1f), Tol, "half her hump: half the dip");
            Assert.AreEqual(-2f, HullTrimMath.DynamicDegrees(P, 0.8f, -1f), Tol, "at the hump: the full dip");
            Assert.AreEqual(-2f, HullTrimMath.DynamicDegrees(P, 2f, -1f), Tol, "past the hump: still the full dip");
            Assert.AreEqual(0f, HullTrimMath.DynamicDegrees(P, 0f, -1f), Tol, "no way on: nothing to dip with");
            Assert.AreEqual(0f, HullTrimMath.DynamicDegrees(P, -1f, -1f), Tol, "astern: no dip");
        }

        [Test]
        public void TheRise_IsOnlyWhatHerDriveHolds()
        {
            Assert.AreEqual(0f, Target(0.8f, 0f, 0f), Tol, "at the hump with no drive: no rise");
            Assert.AreEqual(3f, Target(0.8f, 0.4f, 0f), Tol, "a drive that holds 0.4 m/s holds that speed's rise");
            Assert.AreEqual(3f, Target(0.4f, 0.8f, 0f), Tol, "below the held speed, her own speed decides");
        }

        [Test]
        public void WhenTheDriveIsCut_TheRiseGoesAndTheDipShows()
        {
            // At the hump, braking at 1 m/s²: with the drive held she would read 6 − 2 = 4° (bow still up);
            // with the drive cut the rise goes, and the 2° dip is what she shows.
            Assert.AreEqual(-2f, Target(0.8f, 0f, -1f), Tol);
            Assert.Less(Target(0.8f, 0f, -1f), 0f, "a cut must read as bow DOWN");
        }

        // ------------------------------------------------------------------ limits

        [Test]
        public void TheTarget_StaysInsideHerLimits()
        {
            Assert.AreEqual(8f, Target(0.8f, float.PositiveInfinity, 3f), Tol, "6 + 4.5 = 10.5, held at +8");
            Assert.AreEqual(-4f, Target(0.8f, 0f, -10f), Tol, "−20, held at −4");
            Assert.AreEqual(8f, Target(0.8f, float.PositiveInfinity, float.PositiveInfinity), Tol);
            Assert.AreEqual(-4f, Target(0.8f, 0f, float.NegativeInfinity), Tol);
            Assert.AreEqual(0f, Target(0f, 0f, -10f), Tol, "a hard brake with no way on stays level");
        }

        [Test]
        public void ANotANumber_AsksForLevel()
        {
            Assert.AreEqual(0f, Target(0.8f, float.PositiveInfinity, float.NaN), "a NaN acceleration");
            Assert.AreEqual(0f, Target(float.NaN, float.PositiveInfinity, 0f), "a NaN speed");
            Assert.AreEqual(0f, Target(0f, 0f, float.NegativeInfinity), "−∞ braking with no way on (∞ · 0)");
        }

        // ------------------------------------------------------------------ reverse and the zero crossing

        [Test]
        public void Astern_SheSitsLevel()
        {
            Assert.AreEqual(0f, Target(-1f, 0f, 0f), Tol, "steady sternway");
            Assert.AreEqual(0f, Target(-1f, 0f, -1f), Tol, "gathering sternway: no dip");
            Assert.AreEqual(1.5f, Target(-1f, 0f, 1f), Tol,
                "losing sternway: the squat follows the along-keel acceleration, ungated");
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(1f)]
        public void NothingSpikes_ThroughTheStop(float acceleration)
        {
            float ahead = Target(1e-4f, float.PositiveInfinity, acceleration);
            float astern = Target(-1e-4f, float.PositiveInfinity, acceleration);
            Assert.Less(Mathf.Abs(ahead - astern), 1e-3f,
                $"a = {acceleration}: {ahead} just ahead vs {astern} just astern");
        }

        [TestCase(0f)]
        [TestCase(0.8f)]
        public void NothingSpikes_WhereTheDriveTurnsToBraking(float speed)
        {
            float pushing = Target(speed, float.PositiveInfinity, 1e-4f);
            float braking = Target(speed, float.PositiveInfinity, -1e-4f);
            Assert.Less(Mathf.Abs(pushing - braking), 1e-3f, $"at {speed} m/s: {pushing} vs {braking}");
        }

        [Test]
        public void TheCurve_HasNoJumpAnywhere_FromSternwayToPlaning()
        {
            // Steepest honest slope: the settle, 3 · 1.5 / 0.15 · ½ = 15°/(m/s), so a 1 mm/s step moves
            // the bow 0.015° at most. Anything near 0.05° is a corner or a spike.
            foreach (float a in new[] { -1f, 0f, 1f })
            {
                float last = Target(-2f, float.PositiveInfinity, a);
                for (int i = 1; i <= 4000; i++)
                {
                    float u = -2f + i * 1e-3f;
                    float now = Target(u, float.PositiveInfinity, a);
                    Assert.Less(Mathf.Abs(now - last), 0.05f, $"a = {a}: jump at {u} m/s ({last} → {now})");
                    last = now;
                }
            }
        }

        // ------------------------------------------------------------------ the lag (smoothing, variable dt)

        [Test]
        public void TheLag_IsOneExactExponential()
        {
            Assert.AreEqual(0.63212056f, HullTrimMath.Step(0f, 1f, 0.5f, 0.5f), 1e-6f, "one time constant: 1 − 1/e");
            Assert.AreEqual(0.81959198f, HullTrimMath.Step(2f, -1f, 0.25f, 0.5f), 1e-5f,
                "half a time constant from 2 toward −1: 2 − 3(1 − e^−½)");
        }

        [Test]
        public void TheLag_LandsInTheSamePlace_WhateverTheFrameTimes()
        {
            float halves = HullTrimMath.Step(HullTrimMath.Step(0f, 1f, 0.25f, 0.5f), 1f, 0.25f, 0.5f);
            Assert.AreEqual(HullTrimMath.Step(0f, 1f, 0.5f, 0.5f), halves, 1e-6f, "two half frames = one whole");

            float ragged = 0f;
            foreach (float dt in new[] { 0.013f, 0.021f, 0.0167f, 0.05f, 0.0993f })   // sums to 0.2 s
                ragged = HullTrimMath.Step(ragged, 1f, dt, 0.5f);
            Assert.AreEqual(0.32967995f, ragged, 1e-5f, "ragged frames over 0.2 s: 1 − e^−0.4");

            float at60 = 0f, at144 = 0f;
            for (int i = 0; i < 60; i++) at60 = HullTrimMath.Step(at60, 1f, 1f / 60f, 0.5f);
            for (int i = 0; i < 144; i++) at144 = HullTrimMath.Step(at144, 1f, 1f / 144f, 0.5f);
            Assert.AreEqual(0.86466472f, at60, 1e-4f, "one second at 60 fps: 1 − e^−2");
            Assert.AreEqual(0.86466472f, at144, 1e-4f, "one second at 144 fps: 1 − e^−2");
        }

        [Test]
        public void APausedOrBackwardFrame_Holds()
        {
            Assert.AreEqual(0.3f, HullTrimMath.Step(0.3f, 1f, 0f, 0.5f), "paused (dt 0)");
            Assert.AreEqual(0.3f, HullTrimMath.Step(0.3f, 1f, -0.1f, 0.5f), "clock stepped backwards");
            Assert.AreEqual(0.3f, HullTrimMath.Step(0.3f, 1f, float.NaN, 0.5f), "dt not a number");
        }

        [Test]
        public void NoLag_FollowsTheTargetExactly()
        {
            Assert.AreEqual(1f, HullTrimMath.Step(0.3f, 1f, 0.1f, 0f), "τ 0");
            Assert.AreEqual(1f, HullTrimMath.Step(0.3f, 1f, 0.1f, -1f), "τ negative");
            Assert.AreEqual(1f, HullTrimMath.Step(0.3f, 1f, 0.1f, float.NaN), "τ not a number");
        }

        [Test]
        public void ALongFrame_LandsOnTheTarget_WithoutOvershoot()
        {
            float up = HullTrimMath.Step(0f, 1f, 100f, 0.5f);
            float down = HullTrimMath.Step(0f, -1f, 100f, 0.5f);
            Assert.That(up, Is.InRange(0.999f, 1f));
            Assert.That(down, Is.InRange(-1f, -0.999f));
            foreach (float dt in new[] { 0.01f, 0.1f, 1f, 10f })
                Assert.That(HullTrimMath.Step(0f, 1f, dt, 0.5f), Is.InRange(0f, 1f), $"dt {dt}");
        }

        // ------------------------------------------------------------------ the pieces

        [Test]
        public void Froude_IsHeadwayOverRootGL()
        {
            Assert.AreEqual(1f, HullTrimMath.Froude(2f, HalfFroudeLength), 1e-5f);
            Assert.AreEqual(0f, HullTrimMath.Froude(-3f, 4.5f), "sternway reads no Froude number");
            Assert.AreEqual(1.009637f, HullTrimMath.Froude(1f, 0f), 1e-5f, "length floored at 0.1 m: 1/√0.981");
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.15625f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void Smooth01_IsTheClampedSmoothstep(float x, float expected)
        {
            Assert.AreEqual(expected, HullTrimMath.Smooth01(x), 1e-6f);
        }

        [Test]
        public void Smooth01_OfNotANumber_IsZero()
        {
            Assert.AreEqual(0f, HullTrimMath.Smooth01(float.NaN));
        }

        [Test]
        public void TheProfile_GuardsItsInputs()
        {
            var p = new HullTrimProfile(-1f, 10f, -1f, -1f, -1f, -1f, -1f, 0f, 0f, 0f);
            Assert.AreEqual(0f, p.HumpDegrees, "rise");
            Assert.AreEqual(0f, p.PlaningDropDegrees, "drop");
            Assert.AreEqual(0f, p.AccelDegreesPerMps2, "squat gain");
            Assert.AreEqual(0f, p.DecelDegreesPerMps2, "dip gain");
            Assert.AreEqual(0f, p.MaxBowUpDegrees, "up limit");
            Assert.AreEqual(0f, p.MaxBowDownDegrees, "down limit");
            Assert.AreEqual(0f, p.ResponseSeconds, "lag");
            Assert.AreEqual(1e-3f, p.HumpFroude, 1e-7f, "hump Froude floored");
            Assert.AreEqual(2e-3f, p.PlaningFroude, 1e-7f, "planing Froude kept above the hump's");
            Assert.AreEqual(0.1f, p.LengthMeters, 1e-7f, "length floored");
            Assert.IsTrue(p.IsNeutral);

            Assert.AreEqual(2f, new HullTrimProfile(2f, 5f, 0f, 0f, 0f, 0f, 0f, 0.4f, 0.55f, 4.5f).PlaningDropDegrees,
                "she can never give back more than she rose");
            Assert.AreEqual(0.501f, new HullTrimProfile(6f, 3f, 0f, 0f, 0f, 0f, 0f, 0.5f, 0.3f, 4.5f).PlaningFroude, 1e-6f,
                "a planing Froude below the hump's is lifted just above it");
        }

        [Test]
        public void ANeutralProfile_IsExactlyLevel_WhateverItIsAsked()
        {
            var zeros = new HullTrimProfile(0f, 0f, 0f, 0f, 8f, 4f, 0.5f, 0.4f, 0.55f, 4.5f);
            foreach (HullTrimProfile p in new[] { zeros, HullTrimProfile.Level })
            {
                Assert.AreEqual(0f, HullTrimMath.TargetDegrees(p, 5f, float.PositiveInfinity, 3f));
                Assert.AreEqual(0f, HullTrimMath.TargetDegrees(p, 0.8f, 0f, -10f));
                Assert.AreEqual(0f, HullTrimMath.TargetDegrees(p, float.NaN, float.NaN, float.NaN));
            }
        }

        // ------------------------------------------------------------------ resolving a hull

        [Test]
        public void Resolve_NoHullOrTheSwitchOff_IsLevel()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                hull.TrimHumpDegrees = 3f;
                hull.TrimDecelDegreesPerMps2 = 1.5f;
                Assert.IsTrue(HullTrimMath.Resolve(null, HullTrimSettings.Default).IsNeutral, "no hull");

                HullTrimSettings off = HullTrimSettings.Default;
                off.Enabled = false;
                Assert.IsTrue(HullTrimMath.Resolve(hull, off).IsNeutral, "the master switch off");
                Assert.IsTrue(HullTrimMath.Resolve(hull, default(HullTrimSettings)).IsNeutral,
                    "a GameConfig serialized before the block existed reads all-zero, i.e. off");
            }
            finally { Object.DestroyImmediate(hull); }
        }

        [Test]
        public void Resolve_TakesHerOwnValues_AndThePolicyForWhatSheLeavesAtZero()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                hull.TrimHumpDegrees = 3f;
                hull.TrimPlaningDropDegrees = 1f;
                hull.TrimAccelDegreesPerMps2 = 0.5f;
                hull.TrimDecelDegreesPerMps2 = 1.5f;
                hull.LengthMeters = 7f;
                var policy = new HullTrimSettings
                {
                    Enabled = true, HumpFroude = 0.35f, PlaningFroude = 0.6f,
                    DefaultMaxBowUpDegrees = 8f, DefaultMaxBowDownDegrees = 4f, DefaultResponseSeconds = 0.6f,
                };

                HullTrimProfile p = HullTrimMath.Resolve(hull, policy);
                Assert.AreEqual(3f, p.HumpDegrees);
                Assert.AreEqual(1f, p.PlaningDropDegrees);
                Assert.AreEqual(0.5f, p.AccelDegreesPerMps2);
                Assert.AreEqual(1.5f, p.DecelDegreesPerMps2);
                Assert.AreEqual(8f, p.MaxBowUpDegrees, "her 0 up-limit is the policy's");
                Assert.AreEqual(4f, p.MaxBowDownDegrees, "her 0 down-limit is the policy's");
                Assert.AreEqual(0.6f, p.ResponseSeconds, "her 0 lag is the policy's");
                Assert.AreEqual(0.35f, p.HumpFroude, "the hump is world policy");
                Assert.AreEqual(0.6f, p.PlaningFroude, "the settle is world policy");
                Assert.AreEqual(7f, p.LengthMeters, "her own length");

                hull.TrimMaxBowUpDegrees = 5f;
                hull.TrimMaxBowDownDegrees = 2f;
                hull.TrimResponseSeconds = 0.3f;
                p = HullTrimMath.Resolve(hull, policy);
                Assert.AreEqual(5f, p.MaxBowUpDegrees, "her own up-limit");
                Assert.AreEqual(2f, p.MaxBowDownDegrees, "her own down-limit");
                Assert.AreEqual(0.3f, p.ResponseSeconds, "her own lag");
            }
            finally { Object.DestroyImmediate(hull); }
        }

        // ------------------------------------------------------------------ the physics-side signals

        [Test]
        public void DriveAcceleration_IsForceOverMass_LessTheBodysDamping()
        {
            Assert.AreEqual(0.6f, HullTrimMath.DriveAcceleration(12f, 10f, 0.2f, 3f), 1e-6f, "12/10 − 0.2·3");
            Assert.AreEqual(0.4f, HullTrimMath.DriveAcceleration(0f, 10f, 0.2f, -2f), 1e-6f,
                "damping against sternway pushes her ahead");
            Assert.AreEqual(1.2f, HullTrimMath.DriveAcceleration(12f, 10f, -0.2f, 3f), 1e-6f,
                "a negative damping is no damping");
            Assert.AreEqual(0f, HullTrimMath.DriveAcceleration(12f, 0f, 0.2f, 3f), "no mass");
            Assert.AreEqual(0f, HullTrimMath.DriveAcceleration(12f, -1f, 0.2f, 3f), "negative mass");
            Assert.AreEqual(0f, HullTrimMath.DriveAcceleration(12f, float.NaN, 0.2f, 3f), "mass not a number");
        }

        [Test]
        public void HeldSpeed_IsDriveOverResistance()
        {
            Assert.AreEqual(2f, HullTrimMath.HeldSpeed(12f, 6f), 1e-6f);
            Assert.AreEqual(0f, HullTrimMath.HeldSpeed(0f, 6f), "no drive holds no speed");
            Assert.AreEqual(0f, HullTrimMath.HeldSpeed(-5f, 6f), "a drive astern holds no headway");
            Assert.AreEqual(float.PositiveInfinity, HullTrimMath.HeldSpeed(12f, 0f), "nothing resists");
            Assert.AreEqual(0f, HullTrimMath.HeldSpeed(float.NaN, 1f), "drive not a number");
        }

        // ------------------------------------------------------------------ rule 5, rule 7

        [Test]
        public void TheLaw_KeepsNoState()
        {
            float[] first = Sweep();
            // Anything in between — another hull, wild inputs — must not change what the same inputs give.
            var other = new HullTrimProfile(1f, 0f, 0.5f, 1.5f, 0f, 0f, 0.4f, 0.4f, 0.55f, 4.5f);
            HullTrimMath.TargetDegrees(other, 9f, 0f, -50f);
            HullTrimMath.TargetDegrees(P, float.NaN, float.NaN, float.NaN);
            HullTrimMath.Step(5f, -5f, 3f, 0.1f);
            float[] second = Sweep();
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], $"sample {i}");
        }

        static float[] Sweep()
        {
            var samples = new float[21 * 5];
            int n = 0;
            for (int i = 0; i <= 20; i++)
            for (int j = -2; j <= 2; j++)
                samples[n++] = HullTrimMath.Step(0.25f * j, Target(-1f + 0.15f * i, 1.2f, 0.7f * j), 1f / 60f, 0.5f);
            return samples;
        }

        [Test]
        public void ThePhysicsSideChain_AllocatesNothing()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                hull.TrimHumpDegrees = 3.5f;
                hull.TrimDecelDegreesPerMps2 = 3f;
                HullTrimSettings policy = HullTrimSettings.Default;
                float result = 0f;
                // A TestDelegate, not an Action: the constraint throws on any other delegate type.
                TestDelegate tick = () =>
                {
                    HullTrimProfile p = HullTrimMath.Resolve(hull, policy);
                    float a = HullTrimMath.DriveAcceleration(12f - 4f, 10f, 0.2f, 4f);
                    float r = SailDrive.LinearResistance(100f, 10f, 0.2f, 0.01f) * 0.01f;
                    float t = HullTrimMath.TargetDegrees(p, 4f, HullTrimMath.HeldSpeed(12f, r), a);
                    result = HullTrimMath.Step(result, t, 0.02f, p.ResponseSeconds);
                };
                tick();   // warm-up: JIT and type initialisation are not the tick
                Assert.That(() => { _sink = new object(); }, Is.AllocatingGCMemory(),
                    "positive control: the recorder must see an allocation when there is one");
                Assert.That(tick, Is.Not.AllocatingGCMemory());
            }
            finally { Object.DestroyImmediate(hull); }
        }
    }
}
