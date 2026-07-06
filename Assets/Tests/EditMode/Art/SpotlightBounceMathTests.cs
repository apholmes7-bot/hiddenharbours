using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Determinism + correctness guard for the BOAT-SPOTLIGHT BOUNCE maths (the lamp rocks with the hull;
    /// ADR 0018 B2 read, CLAUDE.md rule 5). These run headless — no scene, no GPU — and pin the pure
    /// <see cref="LightMath.BounceLampYOffset"/> (bob → lamp Y offset) and <see cref="LightMath.BounceSwayDir"/>
    /// (roll → a subtle beam-direction sway). The <see cref="BoatSpotlight"/> component reads the hull's live bob
    /// + roll off the visual child and feeds these, so guarding the maths guards the feel. The KEY invariant the
    /// owner asked for: glass calm (zero rock) ⇒ zero offset AND the beam direction returned EXACTLY unchanged
    /// (the cone sits still). Mirrors <see cref="LightMathTests"/>'s style.
    /// </summary>
    public class SpotlightBounceMathTests
    {
        private const float Eps = 1e-4f;

        // ---- BounceLampYOffset (bob → the lamp rides up/down with the deck) -------------------------------

        [Test]
        public void BobOffset_ZeroRock_IsZero()
        {
            // Glass calm: no bob ⇒ the lamp sits exactly still (no vertical nudge), for any scale.
            Assert.AreEqual(0f, LightMath.BounceLampYOffset(0f, 1f), Eps, "zero bob -> zero offset");
            Assert.AreEqual(0f, LightMath.BounceLampYOffset(0f, 2.5f), Eps, "zero bob -> zero offset (any scale)");
            // Zero scale (bob share disabled) ⇒ zero offset even with a live bob.
            Assert.AreEqual(0f, LightMath.BounceLampYOffset(0.3f, 0f), Eps, "zero scale -> zero offset");
        }

        [Test]
        public void BobOffset_ScalesLinearlyAndTracksSign()
        {
            // The lamp shares the bob by the scale — linear, sign-preserving (up crest lifts, down trough drops).
            Assert.AreEqual(0.2f, LightMath.BounceLampYOffset(0.2f, 1f), Eps, "unit scale -> exactly the bob");
            Assert.AreEqual(0.1f, LightMath.BounceLampYOffset(0.2f, 0.5f), Eps, "half scale -> half the bob");
            Assert.AreEqual(-0.15f, LightMath.BounceLampYOffset(-0.15f, 1f), Eps, "a trough drops the lamp");
        }

        [Test]
        public void BobOffset_IsMonotonicInBob()
        {
            // As the crest lifts the hull higher, the lamp offset must never decrease (a monotone read).
            float prev = float.NegativeInfinity;
            for (float bob = -0.5f; bob <= 0.5f + Eps; bob += 0.05f)
            {
                float off = LightMath.BounceLampYOffset(bob, 0.8f);
                Assert.GreaterOrEqual(off + Eps, prev, $"offset dropped as bob rose at bob={bob}");
                prev = off;
            }
        }

        // ---- BounceSwayDir (roll → a subtle cone lean) ----------------------------------------------------

        [Test]
        public void Sway_ZeroRoll_ReturnsDirectionExactlyUnchanged()
        {
            // The owner's invariant: glass calm (zero roll) ⇒ the cone points EXACTLY where it aimed — no drift.
            Vector2 dir = new Vector2(0f, 1f);
            Vector2 swayed = LightMath.BounceSwayDir(dir, 0f, 0.5f);
            Assert.AreEqual(dir.x, swayed.x, Eps, "zero roll must not move the beam x");
            Assert.AreEqual(dir.y, swayed.y, Eps, "zero roll must not move the beam y");

            // Zero gain (sway disabled) ⇒ also exactly unchanged, even with a live roll.
            Vector2 swayed2 = LightMath.BounceSwayDir(dir, 12f, 0f);
            Assert.AreEqual(dir.x, swayed2.x, Eps, "zero gain must not move the beam x");
            Assert.AreEqual(dir.y, swayed2.y, Eps, "zero gain must not move the beam y");
        }

        [Test]
        public void Sway_DegenerateDir_ReturnedAsIs()
        {
            // No aim to sway (a zero vector) ⇒ returned untouched, never NaN.
            Vector2 zero = Vector2.zero;
            Vector2 swayed = LightMath.BounceSwayDir(zero, 20f, 0.5f);
            Assert.AreEqual(0f, swayed.x, Eps);
            Assert.AreEqual(0f, swayed.y, Eps);
        }

        [Test]
        public void Sway_RotatesByRollTimesGain_PreservingMagnitude()
        {
            // Beam pointing +Y (up). A +10° roll at gain 0.5 ⇒ a +5° CCW rotation of the axis.
            Vector2 dir = new Vector2(0f, 1f);
            Vector2 swayed = LightMath.BounceSwayDir(dir, 10f, 0.5f);

            float expectedDeg = 5f;
            float rad = expectedDeg * Mathf.Deg2Rad;
            // +CCW rotation of (0,1): x = -sin, y = cos.
            Assert.AreEqual(-Mathf.Sin(rad), swayed.x, Eps, "swayed x = -sin(5deg)");
            Assert.AreEqual(Mathf.Cos(rad), swayed.y, Eps, "swayed y = cos(5deg)");

            // Pure rotation preserves magnitude (a mounted lamp leans, it doesn't stretch the beam).
            Assert.AreEqual(dir.magnitude, swayed.magnitude, Eps, "sway must preserve |beamDir|");

            // The swayed axis is off the original by exactly the swayed angle.
            float angleBetween = Vector2.Angle(dir, swayed);
            Assert.AreEqual(expectedDeg, angleBetween, 1e-2f, "the beam leant by roll x gain degrees");
        }

        [Test]
        public void Sway_SignAndMonotonicity()
        {
            // Opposite rolls lean the cone opposite ways (symmetric), and a bigger roll leans it further.
            Vector2 dir = new Vector2(0f, 1f);
            Vector2 pos = LightMath.BounceSwayDir(dir, 8f, 0.5f);
            Vector2 neg = LightMath.BounceSwayDir(dir, -8f, 0.5f);
            Assert.AreEqual(-pos.x, neg.x, Eps, "opposite roll leans the cone the opposite way (x mirror)");
            Assert.AreEqual(pos.y, neg.y, Eps, "symmetric roll keeps the same forward component");

            // Monotonic: as |roll| grows the lean angle off the aim never decreases.
            float prevAngle = -1f;
            for (float roll = 0f; roll <= 20f + Eps; roll += 2f)
            {
                Vector2 s = LightMath.BounceSwayDir(dir, roll, 0.5f);
                float a = Vector2.Angle(dir, s);
                Assert.GreaterOrEqual(a + Eps, prevAngle, $"lean shrank as roll grew at roll={roll}");
                prevAngle = a;
            }
        }

        [Test]
        public void Sway_IsDeterministic()
        {
            // Same inputs ⇒ same output, every call (pure, no scene/RNG — rule 5).
            Vector2 dir = new Vector2(0.3f, 0.95f);
            Vector2 a = LightMath.BounceSwayDir(dir, 7.5f, 0.5f);
            Vector2 b = LightMath.BounceSwayDir(dir, 7.5f, 0.5f);
            Assert.AreEqual(a.x, b.x, 0f, "deterministic x");
            Assert.AreEqual(a.y, b.y, 0f, "deterministic y");
        }
    }
}
