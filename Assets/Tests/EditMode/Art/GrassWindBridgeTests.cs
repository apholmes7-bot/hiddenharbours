using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for the PURE math behind the grass wind + footstep bridges — the determinism contract for
    /// what <see cref="GrassWindBridge"/> publishes to <c>_WindWorld</c> and the falloff <see cref="GrassFootstep"/>
    /// mirrors into the shader. No Unity scene needed; these are the same functions the shader uses, so a drift
    /// between C# and HLSL (or a NaN at slack wind) is caught in CI.
    /// </summary>
    public class GrassWindBridgeTests
    {
        private const float Eps = 1e-4f;

        // ---- GrassWindBridge.WindToShaderVector ----------------------------------------------------------

        [Test]
        public void WindToShaderVector_ZeroWind_ReturnsZero()
        {
            Vector2 v = GrassWindBridge.WindToShaderVector(Vector2.zero, 12f);
            Assert.AreEqual(0f, v.magnitude, Eps, "Slack wind must publish a zero vector (shader falls back to idle).");
        }

        [Test]
        public void WindToShaderVector_BelowFullSway_ScalesLinearly()
        {
            // 3 m/s against a 12 m/s full-sway threshold => 0.25 of full amplitude, direction preserved (+X).
            Vector2 v = GrassWindBridge.WindToShaderVector(new Vector2(3f, 0f), 12f);
            Assert.AreEqual(0.25f, v.x, Eps);
            Assert.AreEqual(0f, v.y, Eps);
        }

        [Test]
        public void WindToShaderVector_AtOrAboveFullSway_SaturatesToUnitMagnitude()
        {
            Vector2 at = GrassWindBridge.WindToShaderVector(new Vector2(12f, 0f), 12f);
            Vector2 above = GrassWindBridge.WindToShaderVector(new Vector2(48f, 0f), 12f);
            Assert.AreEqual(1f, at.magnitude, Eps, "At the threshold the amplitude must be exactly full (1).");
            Assert.AreEqual(1f, above.magnitude, Eps, "Past the threshold the amplitude saturates at full (1).");
        }

        [Test]
        public void WindToShaderVector_PreservesDirection()
        {
            // An arbitrary direction (the sim wanders this over time); only the strength is normalized.
            var wind = new Vector2(-5f, 5f);     // 45deg up-left, ~7.07 m/s
            Vector2 v = GrassWindBridge.WindToShaderVector(wind, 12f);
            // direction matches the input direction
            Vector2 dir = v.normalized;
            Vector2 expected = wind.normalized;
            Assert.AreEqual(expected.x, dir.x, Eps);
            Assert.AreEqual(expected.y, dir.y, Eps);
            // magnitude is strength/full
            Assert.AreEqual(wind.magnitude / 12f, v.magnitude, Eps);
        }

        [Test]
        public void WindToShaderVector_MonotonicInStrength()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float speed = i; // 0..20 m/s
                float mag = GrassWindBridge.WindToShaderVector(new Vector2(speed, 0f), 12f).magnitude;
                Assert.GreaterOrEqual(mag + Eps, prev, "Sway amplitude must not decrease as wind strengthens.");
                prev = mag;
            }
        }

        [Test]
        public void WindToShaderVector_NegligibleWind_ReturnsZeroNoNaN()
        {
            Vector2 v = GrassWindBridge.WindToShaderVector(new Vector2(1e-7f, 0f), 12f);
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y), "Must not produce NaN at near-zero wind.");
            Assert.AreEqual(0f, v.magnitude, Eps);
        }

        // ---- GrassFootstep.FootstepFalloff ---------------------------------------------------------------

        [Test]
        public void FootstepFalloff_AtFeet_IsFull()
        {
            Assert.AreEqual(1f, GrassFootstep.FootstepFalloff(0f, 1.4f), Eps, "Right at the player the bend is full.");
        }

        [Test]
        public void FootstepFalloff_AtOrBeyondRadius_IsZero()
        {
            Assert.AreEqual(0f, GrassFootstep.FootstepFalloff(1.4f, 1.4f), Eps, "At the radius the bend is gone.");
            Assert.AreEqual(0f, GrassFootstep.FootstepFalloff(5f, 1.4f), Eps, "Past the radius the bend stays zero.");
        }

        [Test]
        public void FootstepFalloff_AtHalfRadius_IsHalf()
        {
            // smoothstep(0.5) = 0.5, so 1 - smoothstep = 0.5 at half the radius.
            Assert.AreEqual(0.5f, GrassFootstep.FootstepFalloff(0.7f, 1.4f), Eps);
        }

        [Test]
        public void FootstepFalloff_MonotonicNonIncreasing()
        {
            float prev = 2f;
            for (int i = 0; i <= 20; i++)
            {
                float d = i * 0.1f;   // 0..2 m
                float f = GrassFootstep.FootstepFalloff(d, 1.4f);
                Assert.LessOrEqual(f, prev + Eps, "Bend must not grow as you move away from the blade.");
                Assert.GreaterOrEqual(f, -Eps);
                Assert.LessOrEqual(f, 1f + Eps);
                prev = f;
            }
        }

        // ---- GrassFootstep.TrailStrength (the path spring-back fade) --------------------------------------

        [Test]
        public void TrailStrength_FreshPoint_IsFull()
        {
            Assert.AreEqual(1f, GrassFootstep.TrailStrength(0f, 1.2f), Eps, "A just-trodden point bends fully.");
            Assert.AreEqual(1f, GrassFootstep.TrailStrength(-5f, 1.2f), Eps, "A head point refreshed this frame is full.");
        }

        [Test]
        public void TrailStrength_AtOrPastLifetime_IsZero()
        {
            Assert.AreEqual(0f, GrassFootstep.TrailStrength(1.2f, 1.2f), Eps, "At the lifetime the grass has sprung back.");
            Assert.AreEqual(0f, GrassFootstep.TrailStrength(10f, 1.2f), Eps, "A stale point exerts no bend.");
            // A never-set point (huge negative born → huge positive age) must read 0, not bend the whole field.
            Assert.AreEqual(0f, GrassFootstep.TrailStrength(1e9f, 1.2f), Eps);
        }

        [Test]
        public void TrailStrength_HalfLifetime_IsHalf()
        {
            Assert.AreEqual(0.5f, GrassFootstep.TrailStrength(0.6f, 1.2f), Eps, "Linear spring-back: half-faded at half life.");
        }

        [Test]
        public void TrailStrength_MonotonicNonIncreasingInAge()
        {
            float prev = 2f;
            for (int i = 0; i <= 20; i++)
            {
                float age = i * 0.1f;   // 0..2 s
                float s = GrassFootstep.TrailStrength(age, 1.2f);
                Assert.LessOrEqual(s, prev + Eps, "A trail point must only fade, never strengthen, with age.");
                Assert.GreaterOrEqual(s, -Eps);
                Assert.LessOrEqual(s, 1f + Eps);
                prev = s;
            }
        }

        // ---- GrassFootstep.MovingFactor / SmoothToward / DirectionalGate (the behind-only gate) -----------

        [Test]
        public void MovingFactor_Saturates()
        {
            Assert.AreEqual(0f, GrassFootstep.MovingFactor(0f, 1.2f), Eps, "Standing still = symmetric gate.");
            Assert.AreEqual(0.5f, GrassFootstep.MovingFactor(0.6f, 1.2f), Eps);
            Assert.AreEqual(1f, GrassFootstep.MovingFactor(1.2f, 1.2f), Eps, "At the speed = fully directional.");
            Assert.AreEqual(1f, GrassFootstep.MovingFactor(10f, 1.2f), Eps, "Past it stays saturated.");
        }

        [Test]
        public void SmoothToward_EasesAndConverges()
        {
            // One step moves PART of the way; smoothing<=0 or dt<=0 snaps.
            float oneStep = GrassFootstep.SmoothToward(0f, 1f, 0.1f, 0.12f);
            Assert.Greater(oneStep, 0f);
            Assert.Less(oneStep, 1f);
            Assert.AreEqual(1f, GrassFootstep.SmoothToward(0f, 1f, 0.1f, 0f), Eps, "Zero smoothing snaps.");
            // Iterating converges to the target.
            float v = 0f;
            for (int i = 0; i < 200; i++) v = GrassFootstep.SmoothToward(v, 1f, 0.016f, 0.12f);
            Assert.AreEqual(1f, v, 1e-2f);
        }

        [Test]
        public void DirectionalGate_BendsBehindNotAhead_WhileMoving()
        {
            // moving = 1 (fully directional): blades BEHIND (ahead <= 0) bend; AHEAD (ahead > softness) are cut.
            Assert.AreEqual(1f, GrassFootstep.DirectionalGate(-0.5f, 0.12f, 1f), Eps, "Behind the foot: full bend.");
            Assert.AreEqual(0f, GrassFootstep.DirectionalGate(0.5f, 0.12f, 1f), Eps, "Ahead of the foot: no bend.");
            Assert.AreEqual(0.5f, GrassFootstep.DirectionalGate(0.06f, 0.12f, 1f), Eps, "Mid-softness ramps.");
        }

        [Test]
        public void DirectionalGate_Symmetric_WhenStill()
        {
            // moving = 0 (standing): the gate is 1 everywhere, so grass underfoot still parts symmetrically.
            Assert.AreEqual(1f, GrassFootstep.DirectionalGate(-0.5f, 0.12f, 0f), Eps);
            Assert.AreEqual(1f, GrassFootstep.DirectionalGate(0.5f, 0.12f, 0f), Eps, "Still player: ahead also bends.");
            // Half-moving blends the two.
            Assert.AreEqual(0.5f, GrassFootstep.DirectionalGate(0.5f, 0.12f, 0.5f), Eps, "Half speed = half of symmetric.");
        }
    }
}
