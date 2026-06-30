using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the LIVING-MOON math (<see cref="MoonMath"/>) that drives the water's reflected
    /// moon — its nightly ARC, its PHASES, and the tie to the spring/neap tide cycle. The in-shader moon
    /// reflection (disc + glitter path) is GPU maths and can't run headless, but the deterministic state that
    /// POSITIONS + SHAPES it is pure and locked here (CLAUDE.md rule 5 — deterministic, saves nothing):
    ///
    ///   • PHASE cycles 0..1 over the lunar month (0 new, 0.5 full), derived from the SAME period as the tide
    ///     envelope so full moon ~ spring tide;
    ///   • ARC rises → peaks → sets monotonically across the moon's up-window;
    ///   • brightness/illumination: a thin crescent (new) gives far less light than a full moon.
    ///
    /// Plus <see cref="MoonCycle.ComputeState"/>'s packing into the published globals.
    /// </summary>
    public class MoonMathTests
    {
        private const float LunarDays = 28f;     // canon GameConfig.LunarMonthDays
        private const float SecPerDay = 1200f;   // canon GameConfig.SecondsPerDay
        private static double LunarSeconds => (double)LunarDays * SecPerDay;

        // ===== PHASE: cycles 0..1 over the lunar month, tied to the tide cycle ============================

        [Test]
        public void Phase_IsNewAtCycleStart_FullAtHalf_AndWrapsAtMonthEnd()
        {
            // phase 0 (new) at t=0; 0.5 (full) at half a lunar month; ~0 again (wraps) at a full month.
            Assert.AreEqual(0f, MoonMath.Phase01(0.0, LunarDays, SecPerDay, 0f), 1e-4f,
                "the cycle starts on a NEW moon (phase 0)");
            Assert.AreEqual(0.5f, MoonMath.Phase01(LunarSeconds * 0.5, LunarDays, SecPerDay, 0f), 1e-4f,
                "half a lunar month in is the FULL moon (phase 0.5)");
            float wrapped = MoonMath.Phase01(LunarSeconds, LunarDays, SecPerDay, 0f);
            Assert.AreEqual(0f, wrapped, 1e-4f, "a whole lunar month wraps back to a new moon (phase ~0)");
        }

        [Test]
        public void Phase_StaysInUnitInterval_OverManyMonths()
        {
            for (double t = 0; t < LunarSeconds * 5; t += LunarSeconds / 37.0)
            {
                float p = MoonMath.Phase01(t, LunarDays, SecPerDay, 0f);
                Assert.That(p, Is.InRange(0f, 1f), "phase always wraps into [0,1)");
            }
        }

        [Test]
        public void Phase_TiesToTheSpringNeapTideCycle_FullMoonIsSpringTide()
        {
            // The tide envelope (TideModel): env = 0.5 + 0.5*cos(2π * tHours / (lunarHours/2)), 1 at spring.
            // Assert that at FULL moon (phase 0.5) and NEW moon (phase 0) the tide is at SPRING (env ~ 1),
            // proving the moon phase and the tide envelope share one cycle (full moon ~ spring tide).
            double secondsPerHour = SecPerDay / 24.0;
            double lunarHours = LunarDays * 24.0;

            double tFull = LunarSeconds * 0.5;   // full moon
            double tNew = 0.0;                    // new moon
            float envFull = TideEnv(tFull, secondsPerHour, lunarHours);
            float envNew = TideEnv(tNew, secondsPerHour, lunarHours);

            Assert.AreEqual(0.5f, MoonMath.Phase01(tFull, LunarDays, SecPerDay, 0f), 1e-4f);
            Assert.AreEqual(1f, envFull, 1e-3f, "FULL moon lands on a SPRING tide (envelope at 1)");
            Assert.AreEqual(1f, envNew, 1e-3f, "NEW moon lands on the other SPRING tide (envelope at 1)");

            // and a QUARTER moon (phase 0.25) lands on a NEAP tide (envelope at its minimum).
            double tQuarter = LunarSeconds * 0.25;
            float envQuarter = TideEnv(tQuarter, secondsPerHour, lunarHours);
            Assert.AreEqual(0f, envQuarter, 1e-3f, "a QUARTER moon lands on a NEAP tide (envelope at 0)");
        }

        // mirror of TideModel's spring/neap envelope (kept local so this test doesn't reach into Environment).
        private static float TideEnv(double totalSeconds, double secondsPerHour, double lunarHours)
        {
            double tHours = totalSeconds / secondsPerHour;
            return (float)(0.5 + 0.5 * System.Math.Cos(System.Math.PI * 2.0 * tHours / (lunarHours / 2.0)));
        }

        // ===== ILLUMINATION / BRIGHTNESS: full moon bright, new moon dark ==================================

        [Test]
        public void Illumination_IsZeroAtNew_OneAtFull_AndRisesMonotonicallyBetween()
        {
            Assert.AreEqual(0f, MoonMath.IlluminatedFraction(0f), 1e-4f, "new moon is unlit (0)");
            Assert.AreEqual(1f, MoonMath.IlluminatedFraction(0.5f), 1e-4f, "full moon is fully lit (1)");
            // monotonic non-decreasing across the WAXING half (new -> full).
            float prev = -1f;
            for (float p = 0f; p <= 0.5f + 1e-4f; p += 0.02f)
            {
                float il = MoonMath.IlluminatedFraction(p);
                Assert.That(il, Is.InRange(0f, 1f), "illumination stays a 0..1 fraction");
                Assert.GreaterOrEqual(il + 1e-4f, prev, "illumination rises new -> full");
                prev = il;
            }
        }

        [Test]
        public void Terminator_IsFullDiscAtFull_NoDiscAtNew_HalfAtQuarters()
        {
            // cos(2π·phase): +1 at new (terminator across the whole disc -> dark), -1 at full (lit), 0 at quarters.
            Assert.AreEqual(1f, MoonMath.TerminatorSigned(0f), 1e-4f, "new moon: terminator +1 (dark disc)");
            Assert.AreEqual(-1f, MoonMath.TerminatorSigned(0.5f), 1e-4f, "full moon: terminator -1 (lit disc)");
            Assert.AreEqual(0f, MoonMath.TerminatorSigned(0.25f), 1e-4f, "first quarter: terminator 0 (half disc)");
            Assert.AreEqual(0f, MoonMath.TerminatorSigned(0.75f), 1e-4f, "last quarter: terminator 0 (half disc)");
        }

        // ===== ARC: the moon rises, peaks, and sets across the night =======================================

        [Test]
        public void Arc_RisesPeaksAndSets_AcrossTheNight()
        {
            // Default up-window: rise 0.78 (dusk) -> set 0.30 (after dawn), wrapping midnight. Sample across it.
            // Just after rise: low above the horizon, on the EAST side (+X).
            MoonMath.MoonArc(0.80f, out Vector2 dRise, out float hRise);
            // Middle of the night (midnight ~ 0.0/1.0): high above the horizon.
            MoonMath.MoonArc(0.04f, out Vector2 dMid, out float hMid);
            // Just before set: low again, on the WEST side (−X).
            MoonMath.MoonArc(0.28f, out Vector2 dSet, out float hSet);

            Assert.Greater(hMid, hRise, "the moon is higher in the middle of the night than just after rising");
            Assert.Greater(hMid, hSet, "the moon is higher in the middle of the night than just before setting");
            Assert.Greater(dRise.x, 0f, "just after moonrise the moon is on the EAST side (+X)");
            Assert.Less(dSet.x, 0f, "just before moonset the moon is on the WEST side (−X)");
        }

        [Test]
        public void Arc_IsBelowHorizon_DuringTheDay()
        {
            // Midday (0.5) is well inside the down-window (set 0.30 .. rise 0.78) -> the moon is down.
            MoonMath.MoonArc(0.5f, out Vector2 _, out float h);
            Assert.AreEqual(0f, h, 1e-4f, "by day the moon is below the horizon (no reflection)");
        }

        [Test]
        public void Arc_DirectionIsAlwaysUnitLength()
        {
            for (float f = 0f; f <= 1f; f += 0.02f)
            {
                MoonMath.MoonArc(f, out Vector2 d, out float _);
                Assert.AreEqual(1f, d.magnitude, 1e-3f, "the moon direction is always normalized (no NaN axis)");
            }
        }

        [Test]
        public void NightProgress_WrapsMidnight_AndIsZeroByDay()
        {
            // up-window rise 0.78 -> set 0.30 wraps past midnight.
            Assert.AreEqual(0f, MoonMath.NightProgress(0.5f, 0.78f, 0.30f), 1e-4f, "daytime -> not up (0)");
            float justAfterRise = MoonMath.NightProgress(0.79f, 0.78f, 0.30f);
            float midnight = MoonMath.NightProgress(0.0f, 0.78f, 0.30f);
            float justBeforeSet = MoonMath.NightProgress(0.29f, 0.78f, 0.30f);
            Assert.Greater(justAfterRise, 0f, "just after rise the moon is up");
            Assert.Greater(midnight, justAfterRise, "progress advances across midnight");
            Assert.Greater(justBeforeSet, midnight, "progress keeps advancing toward set");
            Assert.That(justBeforeSet, Is.LessThan(1f), "progress is still < 1 just before set");
        }

        // ===== ComputeState: the packing into the published globals ========================================

        [Test]
        public void ComputeState_PacksPhaseTerminatorBrightnessAndPresence()
        {
            // Full moon, middle of the night: bright, present, terminator -1 (full disc), a valid direction.
            double tFull = LunarSeconds * 0.5;
            MoonCycle.ComputeState(tFull, /*dayFraction midnight*/0.02f,
                LunarDays, SecPerDay, /*offset*/0f, 0.78f, 0.30f, /*phaseDrivesPresence*/0.6f,
                out Vector2 dir, out Vector4 state);

            Assert.AreEqual(0.5f, state.x, 1e-3f, "x = phase (full)");
            Assert.AreEqual(-1f, state.y, 1e-3f, "y = signed terminator (-1 = full disc)");
            Assert.Greater(state.z, 0.5f, "z = brightness is high for a full moon at night");
            Assert.Greater(state.w, 0f, "w = above-horizon presence > 0 (the moon is up)");
            Assert.AreEqual(1f, dir.magnitude, 1e-3f, "the moon direction is unit length while up");
        }

        [Test]
        public void ComputeState_NewMoonIsDimmerThanFull_AtTheSameNightTime()
        {
            double tNew = 0.0;                  // new moon
            double tFull = LunarSeconds * 0.5;  // full moon
            MoonCycle.ComputeState(tNew, 0.02f, LunarDays, SecPerDay, 0f, 0.78f, 0.30f, 0.6f,
                out Vector2 _, out Vector4 newState);
            MoonCycle.ComputeState(tFull, 0.02f, LunarDays, SecPerDay, 0f, 0.78f, 0.30f, 0.6f,
                out Vector2 _, out Vector4 fullState);

            Assert.Less(newState.z, fullState.z,
                "a new moon is dimmer than a full moon at the same time of night (you need the boat light)");
            Assert.AreEqual(0f, newState.z, 1e-3f, "a new moon gives ~no reflected light");
        }

        [Test]
        public void ComputeState_DropsTheMoonDirection_WhenDown()
        {
            // Midday: the moon is down -> direction zeroed so the shader cleanly drops the reflection.
            MoonCycle.ComputeState(LunarSeconds * 0.5, /*midday*/0.5f, LunarDays, SecPerDay, 0f,
                0.78f, 0.30f, 0.6f, out Vector2 dir, out Vector4 state);
            Assert.AreEqual(Vector2.zero, dir, "the moon direction is zero when the moon is below the horizon");
            Assert.AreEqual(0f, state.w, 1e-4f, "above-horizon presence is 0 by day");
        }

        [Test]
        public void Phase_IsDeterministic_SameInputsSameResult()
        {
            // Rule 5: the moon is a pure function of the clock — same time, same phase, every time.
            double t = LunarSeconds * 0.37 + 123.0;
            float a = MoonMath.Phase01(t, LunarDays, SecPerDay, 3f);
            float b = MoonMath.Phase01(t, LunarDays, SecPerDay, 3f);
            Assert.AreEqual(a, b, 0f, "deterministic: identical inputs give an identical phase");
        }
    }
}
