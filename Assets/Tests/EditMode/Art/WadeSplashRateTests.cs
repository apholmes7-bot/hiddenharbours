using NUnit.Framework;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for <see cref="WadeSplashMath.SplashRate"/> — the ongoing feet-splash SPAWN RATE the
    /// wade/swim splash emitter scales its emission by, as a pure function of the player's on-foot water band
    /// (<see cref="OnFootWaterState"/>) and this-tick move speed. The whole point is that the effect is GATED
    /// (nothing on dry ground), rises with movement (little/none when still), and is capped for the frame
    /// budget (rule 7). No Unity scene needed — pure, deterministic feel-math, so a regression (or a NaN at an
    /// edge) is caught in CI without opening Unity. Mirrors <see cref="SprayIntensityTests"/>.
    /// </summary>
    public class WadeSplashRateTests
    {
        private const float Eps = 1e-4f;

        // Representative tunables mirroring WadeSplashConfig.Default.
        private const float Idle = 1.5f;
        private const float WadePer = 14f;
        private const float SwimPer = 7f;
        private const float SpeedFull = 2.0f;
        private const float Max = 24f;

        private static float Rate(OnFootWaterState state, float speed)
            => WadeSplashMath.SplashRate(state, speed, Idle, WadePer, SwimPer, SpeedFull, Max);

        // ---- GATED: nothing on dry ground, whatever the speed --------------------------------------------

        [Test]
        public void Dry_IsAlwaysZero_EvenSprinting()
        {
            Assert.AreEqual(0f, Rate(OnFootWaterState.Dry, 0f), Eps, "Standing on dry ground → no splash.");
            Assert.AreEqual(0f, Rate(OnFootWaterState.Dry, 5f), Eps, "Running on dry ground → still no splash (gated).");
        }

        // ---- standing still IN the water: a faint idle disturbance, not zero -----------------------------

        [Test]
        public void Wade_StandingStill_IsIdleRate()
        {
            Assert.AreEqual(Idle, Rate(OnFootWaterState.Wade, 0f), Eps,
                "Standing still while wading → only the faint idle disturbance.");
        }

        [Test]
        public void Swim_StandingStill_IsIdleRate()
        {
            Assert.AreEqual(Idle, Rate(OnFootWaterState.Swim, 0f), Eps,
                "Treading water still → only the faint idle disturbance.");
        }

        // ---- rises with speed (little when slow, more when fast) -----------------------------------------

        [Test]
        public void Wade_RisesWithSpeed()
        {
            float slow = Rate(OnFootWaterState.Wade, 0.3f);
            float fast = Rate(OnFootWaterState.Wade, 1.8f);
            Assert.Greater(fast, slow, "Wading faster kicks up more splashes.");
            Assert.Greater(slow, Idle - Eps, "Even a slow wade is at least the idle floor.");
        }

        [Test]
        public void Wade_MonotonicNonDecreasingInSpeed()
        {
            float prev = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float speed = i / 40f * (SpeedFull * 1.5f);   // walk past the saturation point too
                float r = Rate(OnFootWaterState.Wade, speed);
                Assert.GreaterOrEqual(r + Eps, prev, "Splash rate must not decrease as the player moves faster.");
                prev = r;
            }
        }

        [Test]
        public void SpeedTerm_SaturatesAtSpeedForFull()
        {
            // At and beyond SpeedForFull the speed term is fully on; going faster adds nothing (before the cap).
            float atFull = Rate(OnFootWaterState.Wade, SpeedFull);
            float beyond = Rate(OnFootWaterState.Wade, SpeedFull * 3f);
            Assert.AreEqual(atFull, beyond, Eps, "The speed term saturates at SpeedForFull (then the cap holds).");
            Assert.AreEqual(Idle + WadePer, atFull, Eps, "At full speed the wade rate is idle + the per-speed weight.");
        }

        // ---- swimming is gentler than wading at the same speed -------------------------------------------

        [Test]
        public void Swim_IsGentlerThanWade_AtSameSpeed()
        {
            float wade = Rate(OnFootWaterState.Wade, SpeedFull);
            float swim = Rate(OnFootWaterState.Swim, SpeedFull);
            Assert.Less(swim, wade, "Swimming throws up less than a brisk wade at the same speed (heavier water).");
        }

        // ---- capped for the frame budget (rule 7) --------------------------------------------------------

        [Test]
        public void Rate_IsCappedAtMax()
        {
            // A big per-speed weight + high speed would exceed the cap; the cap holds.
            float r = WadeSplashMath.SplashRate(OnFootWaterState.Wade, 10f,
                idleRate: 5f, wadeRatePerSpeed: 100f, swimRatePerSpeed: 100f,
                speedForFull: 1f, maxRate: Max);
            Assert.AreEqual(Max, r, Eps, "The ongoing rate never exceeds the pool-protecting cap.");
        }

        // ---- robust: clamps garbage inputs, never NaN/negative -------------------------------------------

        [Test]
        public void NegativeSpeed_IsClampedToIdle()
        {
            float r = Rate(OnFootWaterState.Wade, -3f);
            Assert.AreEqual(Idle, r, Eps, "A negative speed clamps to the idle floor, never below zero.");
        }

        [Test]
        public void NeverNegative_NeverNaN_AcrossTheGrid()
        {
            OnFootWaterState[] states = { OnFootWaterState.Dry, OnFootWaterState.Wade, OnFootWaterState.Swim };
            foreach (var st in states)
            for (int i = -5; i <= 40; i++)
            {
                float speed = i / 10f;
                float r = Rate(st, speed);
                Assert.IsFalse(float.IsNaN(r), "Splash rate is never NaN.");
                Assert.GreaterOrEqual(r, -Eps, "Splash rate is never negative.");
                Assert.LessOrEqual(r, Max + Eps, "Splash rate never exceeds the cap.");
            }
        }

        [Test]
        public void ZeroSpeedForFull_DoesNotDivideByZero()
        {
            // A degenerate SpeedForFull of 0 must not NaN — the helper guards the divide.
            float r = WadeSplashMath.SplashRate(OnFootWaterState.Wade, 1f, Idle, WadePer, SwimPer, 0f, Max);
            Assert.IsFalse(float.IsNaN(r), "A zero SpeedForFull must not divide by zero.");
            Assert.LessOrEqual(r, Max + Eps, "Still capped.");
        }

        // ---- deterministic (rule 5) ----------------------------------------------------------------------

        [Test]
        public void IsDeterministic()
        {
            float a = Rate(OnFootWaterState.Wade, 1.3f);
            float b = Rate(OnFootWaterState.Wade, 1.3f);
            Assert.AreEqual(a, b, 0f, "Same inputs must produce the exact same output (no RNG, rule 5).");
        }
    }
}
