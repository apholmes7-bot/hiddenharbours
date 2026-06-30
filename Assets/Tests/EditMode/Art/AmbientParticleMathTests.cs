using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for the PURE math behind the living-coast ambient particles (sea mist, chimney smoke,
    /// gulls, dust motes). No Unity scene needed — these are the deterministic spawn/drift/lifecycle/day-night
    /// curves the runtime shells call, so a regression in the feel-math (or a NaN at an edge) is caught in CI
    /// without opening Unity. Mirrors <c>GrassWindBridgeTests</c> / <c>FoamDensityLifecycleTests</c>.
    /// </summary>
    public class AmbientParticleMathTests
    {
        private const float Eps = 1e-4f;

        // ---- Hash01 (deterministic, no RNG — rule 5) -----------------------------------------------------

        [Test]
        public void Hash01_IsDeterministicAndInRange()
        {
            for (int i = 0; i < 64; i++)
            {
                float a = AmbientParticleMath.Hash01((uint)i);
                float b = AmbientParticleMath.Hash01((uint)i);
                Assert.AreEqual(a, b, "Same input must hash to the same value (determinism).");
                Assert.GreaterOrEqual(a, 0f);
                Assert.Less(a, 1f, "Hash must stay in [0,1).");
            }
        }

        [Test]
        public void Hash01_TwoArg_DecorrelatesBySalt()
        {
            // Different salts on the same index should (almost always) give different values.
            float a = AmbientParticleMath.Hash01(7, 11);
            float b = AmbientParticleMath.Hash01(7, 29);
            Assert.AreNotEqual(a, b, "Different salts must decorrelate per-particle dice.");
        }

        // ---- Life01 / LifeEnvelope -----------------------------------------------------------------------

        [Test]
        public void Life01_ClampsAndMapsLinearly()
        {
            Assert.AreEqual(0f, AmbientParticleMath.Life01(0f, 4f), Eps);
            Assert.AreEqual(0.5f, AmbientParticleMath.Life01(2f, 4f), Eps);
            Assert.AreEqual(1f, AmbientParticleMath.Life01(4f, 4f), Eps);
            Assert.AreEqual(1f, AmbientParticleMath.Life01(99f, 4f), Eps, "Past lifetime clamps to 1.");
            Assert.AreEqual(1f, AmbientParticleMath.Life01(1f, 0f), Eps, "Zero lifetime is treated as fully-aged.");
        }

        [Test]
        public void LifeEnvelope_ZeroAtBirthAndDeath_FullInMiddle()
        {
            Assert.AreEqual(0f, AmbientParticleMath.LifeEnvelope(0f, 0.3f, 0.3f), Eps, "Invisible exactly at birth.");
            Assert.AreEqual(0f, AmbientParticleMath.LifeEnvelope(1f, 0.3f, 0.3f), Eps, "Invisible exactly at death.");
            Assert.AreEqual(1f, AmbientParticleMath.LifeEnvelope(0.5f, 0.3f, 0.3f), Eps, "Full opacity mid-life.");
        }

        [Test]
        public void LifeEnvelope_StaysInUnitRange_NoNaN()
        {
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float e = AmbientParticleMath.LifeEnvelope(t, 0.25f, 0.4f);
                Assert.IsFalse(float.IsNaN(e));
                Assert.GreaterOrEqual(e, -Eps);
                Assert.LessOrEqual(e, 1f + Eps);
            }
        }

        [Test]
        public void LifeEnvelope_ZeroFades_IsHardOnExceptEndpoints()
        {
            // No fade-in/out → opacity is 1 everywhere except the clamped endpoints (still 0 there).
            Assert.AreEqual(1f, AmbientParticleMath.LifeEnvelope(0.5f, 0f, 0f), Eps);
            Assert.AreEqual(1f, AmbientParticleMath.LifeEnvelope(0.01f, 0f, 0f), Eps);
        }

        // ---- Drift ---------------------------------------------------------------------------------------

        [Test]
        public void Drift_AddsOwnVelocityAndScaledWind()
        {
            Vector2 pos = new Vector2(1f, 2f);
            Vector2 own = new Vector2(0.1f, 0f);
            Vector2 wind = new Vector2(1f, 0f);     // shared 0..1 wind
            Vector2 p = AmbientParticleMath.Drift(pos, own, wind, windResponse: 2f, dt: 1f);
            // pos += (own + wind*2)*1 = (1,2) + (0.1+2, 0) = (3.1, 2)
            Assert.AreEqual(3.1f, p.x, Eps);
            Assert.AreEqual(2f, p.y, Eps);
        }

        [Test]
        public void Drift_ZeroDt_DoesNotMove()
        {
            Vector2 pos = new Vector2(5f, 5f);
            Vector2 p = AmbientParticleMath.Drift(pos, new Vector2(9f, 9f), new Vector2(9f, 9f), 9f, 0f);
            Assert.AreEqual(pos, p);
        }

        // ---- MistIntensity (fog + sea-state thicken; baseline floor) -------------------------------------

        [Test]
        public void MistIntensity_ClearGlassyDay_IsBaseline()
        {
            float m = AmbientParticleMath.MistIntensity(1f, SeaState.Glass, baseline: 0.2f, fogWeight: 1f, seaStateWeight: 1f);
            Assert.AreEqual(0.2f, m, Eps, "Clear, glassy → only the baseline shimmer.");
        }

        [Test]
        public void MistIntensity_ThickFog_RaisesMist()
        {
            float clear = AmbientParticleMath.MistIntensity(1f, SeaState.Glass, 0.2f, 0.6f, 0.6f);
            float foggy = AmbientParticleMath.MistIntensity(0.0f, SeaState.Glass, 0.2f, 0.6f, 0.6f);
            Assert.Greater(foggy, clear, "Low visibility must thicken the mist.");
            Assert.AreEqual(0.8f, foggy, Eps, "baseline 0.2 + fog(1.0)*0.6 = 0.8.");
        }

        [Test]
        public void MistIntensity_HighSeaState_RaisesMist()
        {
            float calm = AmbientParticleMath.MistIntensity(1f, SeaState.Glass, 0.1f, 0.5f, 0.7f);
            float storm = AmbientParticleMath.MistIntensity(1f, SeaState.Storm, 0.1f, 0.5f, 0.7f);
            Assert.Greater(storm, calm, "A rough sea must kick up more spray/mist.");
            Assert.AreEqual(0.8f, storm, Eps, "baseline 0.1 + seastate(1.0)*0.7 = 0.8.");
        }

        [Test]
        public void MistIntensity_ClampsToOne()
        {
            float m = AmbientParticleMath.MistIntensity(0f, SeaState.Storm, 0.9f, 2f, 2f);
            Assert.AreEqual(1f, m, Eps, "Intensity saturates at 1.");
        }

        [Test]
        public void MistIntensity_MonotonicInFog()
        {
            float prev = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float vis = 1f - i / 10f;   // visibility falls → fog rises
                float m = AmbientParticleMath.MistIntensity(vis, SeaState.Calm, 0.1f, 0.8f, 0.4f);
                Assert.GreaterOrEqual(m + Eps, prev, "Mist must not decrease as fog thickens.");
                prev = m;
            }
        }

        // ---- DayNightBrightness / DayNightOpacity / MoonlightCatch ----------------------------------------

        [Test]
        public void DayNightBrightness_WhiteIsFull_BlackIsZero()
        {
            Assert.AreEqual(1f, AmbientParticleMath.DayNightBrightness(Color.white), Eps);
            Assert.AreEqual(0f, AmbientParticleMath.DayNightBrightness(Color.black), Eps);
        }

        [Test]
        public void DayNightOpacity_NoNightFade_AlwaysFull()
        {
            Assert.AreEqual(1f, AmbientParticleMath.DayNightOpacity(0f, nightFade: 0f), Eps, "nightFade 0 ignores the dark.");
            Assert.AreEqual(1f, AmbientParticleMath.DayNightOpacity(1f, nightFade: 0f), Eps);
        }

        [Test]
        public void DayNightOpacity_FullNightFade_TracksBrightness()
        {
            Assert.AreEqual(1f, AmbientParticleMath.DayNightOpacity(1f, nightFade: 1f), Eps, "Full day → full opacity.");
            Assert.AreEqual(0f, AmbientParticleMath.DayNightOpacity(0f, nightFade: 1f), Eps, "Full dark → gone (motes).");
            Assert.AreEqual(0.5f, AmbientParticleMath.DayNightOpacity(0.5f, nightFade: 1f), Eps);
        }

        [Test]
        public void MoonlightCatch_StrongestInDark_ZeroByDay()
        {
            Assert.AreEqual(0f, AmbientParticleMath.MoonlightCatch(1f, 0.2f), Eps, "By day there's no moon floor.");
            Assert.AreEqual(0.2f, AmbientParticleMath.MoonlightCatch(0f, 0.2f), Eps, "In full dark the floor is at max.");
            Assert.AreEqual(0.1f, AmbientParticleMath.MoonlightCatch(0.5f, 0.2f), Eps);
        }

        // ---- SmokePosition (rises + bends downwind, growing with age) -------------------------------------

        [Test]
        public void SmokePosition_NoWind_RisesStraight()
        {
            Vector2 origin = new Vector2(3f, 1f);
            Vector2 p = AmbientParticleMath.SmokePosition(origin, age: 2f, riseSpeed: 1.5f,
                wind: Vector2.zero, windResponse: 1f, swayAmp: 0f, swaySeed: 0f);
            Assert.AreEqual(origin.x, p.x, Eps, "No wind, no sway → no lateral drift.");
            Assert.AreEqual(origin.y + 3f, p.y, Eps, "Rises riseSpeed*age = 1.5*2 = 3.");
        }

        [Test]
        public void SmokePosition_BendsDownwind_MoreWithAge()
        {
            Vector2 origin = Vector2.zero;
            Vector2 wind = new Vector2(1f, 0f);     // blowing east
            Vector2 young = AmbientParticleMath.SmokePosition(origin, 1f, 1f, wind, 1f, 0f, 0f);
            Vector2 old = AmbientParticleMath.SmokePosition(origin, 3f, 1f, wind, 1f, 0f, 0f);
            Assert.Greater(young.x, 0f, "Wind bends the plume downwind.");
            Assert.Greater(old.x, young.x, "An older/higher puff has been carried further downwind (the column bends over).");
        }

        [Test]
        public void SmokePosition_BendGrowsQuadratically()
        {
            // bend = wind * windResponse * 0.5 * t^2 → at t=2 it's 4x the t=1 bend (with no sway).
            Vector2 wind = new Vector2(1f, 0f);
            float b1 = AmbientParticleMath.SmokePosition(Vector2.zero, 1f, 0f, wind, 1f, 0f, 0f).x;
            float b2 = AmbientParticleMath.SmokePosition(Vector2.zero, 2f, 0f, wind, 1f, 0f, 0f).x;
            Assert.AreEqual(0.5f, b1, Eps);
            Assert.AreEqual(2.0f, b2, Eps);
        }

        // ---- GullPosition / GullHeading ------------------------------------------------------------------

        [Test]
        public void GullPosition_LoopsBackAfterFullPhase()
        {
            Vector2 c = new Vector2(2f, 3f);
            Vector2 a = AmbientParticleMath.GullPosition(c, 5f, 3f, 0f, 0.25f, Vector2.zero, 0f);
            Vector2 b = AmbientParticleMath.GullPosition(c, 5f, 3f, 1f, 0.25f, Vector2.zero, 0f);
            Assert.AreEqual(a.x, b.x, 1e-3f, "Phase 0 and 1 are the same point on the loop.");
            Assert.AreEqual(a.y, b.y, 1e-3f);
        }

        [Test]
        public void GullPosition_StaysWithinRadiusOfCenter()
        {
            Vector2 c = new Vector2(-4f, 6f);
            float rx = 5f, ry = 3f;
            for (int i = 0; i <= 40; i++)
            {
                float ph = i / 40f;
                Vector2 p = AmbientParticleMath.GullPosition(c, rx, ry, ph, 0.5f, Vector2.zero, 0f);
                Assert.LessOrEqual(Mathf.Abs(p.x - c.x), rx + Eps, "x stays within the loop radius.");
                // y is a sum of two unit sinusoids scaled by ry → bounded by ry.
                Assert.LessOrEqual(Mathf.Abs(p.y - c.y), ry + Eps, "y stays within the loop radius.");
            }
        }

        [Test]
        public void GullPosition_RidesWindDownwind()
        {
            Vector2 c = Vector2.zero;
            Vector2 still = AmbientParticleMath.GullPosition(c, 4f, 2f, 0.3f, 0.1f, Vector2.zero, 0f);
            Vector2 windy = AmbientParticleMath.GullPosition(c, 4f, 2f, 0.3f, 0.1f, new Vector2(1f, 0f), 2f);
            Assert.AreEqual(still.x + 2f, windy.x, Eps, "The whole loop skews downwind by wind*windDrift.");
        }

        [Test]
        public void GullHeading_IsUnitAndNeverNaN()
        {
            for (int i = 0; i <= 20; i++)
            {
                float ph = i / 20f;
                Vector2 h = AmbientParticleMath.GullHeading(Vector2.zero, 5f, 3f, ph, 0.3f, Vector2.zero, 0f);
                Assert.IsFalse(float.IsNaN(h.x) || float.IsNaN(h.y));
                Assert.AreEqual(1f, h.magnitude, 1e-2f, "Heading is a unit tangent.");
            }
        }

        // ---- MoteBob -------------------------------------------------------------------------------------

        [Test]
        public void MoteBob_StaysWithinAmplitude()
        {
            for (int i = 0; i <= 40; i++)
            {
                float t = i * 0.25f;
                float b = AmbientParticleMath.MoteBob(t, bobAmp: 0.3f, seed: 0.4f, bobSpeed: 0.8f);
                Assert.LessOrEqual(Mathf.Abs(b), 0.3f + Eps, "Bob never exceeds its amplitude.");
            }
        }

        [Test]
        public void MoteBob_DifferentSeeds_Decorrelate()
        {
            // Seed 0 → sin(0) = 0; seed 0.25 → sin(π/2) = 1. Clearly different phases (no lockstep).
            float a = AmbientParticleMath.MoteBob(0f, 0.3f, 0.0f, 0.8f);
            float b = AmbientParticleMath.MoteBob(0f, 0.3f, 0.25f, 0.8f);
            Assert.AreEqual(0f, a, Eps, "Seed 0 starts at the bob centre.");
            Assert.AreEqual(0.3f, b, Eps, "Seed 0.25 starts at the bob crest — a clearly different phase.");
        }
    }
}
