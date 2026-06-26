using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Pure-logic tests for the art-rendering components (VS-24). These cover the deterministic mapping
    /// functions only — the MonoBehaviours read the live sim through GameServices at runtime.
    /// </summary>
    public class ArtRenderingTests
    {
        [Test]
        public void Waterline_RecedesAtLow_FloodsAtHigh()
        {
            var dir = Vector2.up; // beach faces north: rising tide pushes the waterline +Y onto land
            var low  = TideShoreline.WaterlineOffset(-1.6f, 3f, dir);
            var mid  = TideShoreline.WaterlineOffset(0f,    3f, dir);
            var high = TideShoreline.WaterlineOffset(1.6f,  3f, dir);

            Assert.AreEqual(0f, mid.magnitude, 1e-4f, "mean tide sits at the anchor");
            Assert.Greater(high.y, 0f, "high tide floods along the flood direction");
            Assert.Less(low.y, 0f, "low tide recedes opposite the flood direction");
            Assert.Greater(high.y, low.y, "higher tide => waterline further onto land");
            Assert.AreEqual(1.6f * 3f, high.y, 1e-3f, "offset == tideHeight * slope along the flood axis");
        }

        [Test]
        public void Waterline_FollowsFloodDirection()
        {
            var off = TideShoreline.WaterlineOffset(2f, 1f, new Vector2(1f, 0f)); // east-facing beach
            Assert.AreEqual(2f, off.x, 1e-4f);
            Assert.AreEqual(0f, off.y, 1e-4f);
        }

        [TestCase(0f, true)]
        [TestCase(5.9f, true)]
        [TestCase(6f, false)]
        [TestCase(12f, false)]
        [TestCase(18.99f, false)]
        [TestCase(19f, true)]
        [TestCase(23.5f, true)]
        public void IsNight_WrapsMidnight(float hour, bool expectedNight)
        {
            Assert.AreEqual(expectedNight, CottageDayNight.IsNight(hour, dawnHour: 6f, duskHour: 19f));
        }

        // ===== WaterSurface: the SIM → shader-uniform mappings (the immersion key) =======================
        // These prove the shader's surface reads the deterministic environment the same way the physics does:
        // current → flow, wind → roughness, sea-state → chop, level passes through unchanged.

        [Test]
        public void Flow_RisesWithCurrent_AboveTheBaseFloor()
        {
            const float baseFlow = 0.06f, scale = 0.12f, full = 1.2f;
            float slack = WaterSurface.FlowSpeed(Vector2.zero, baseFlow, scale, full);
            float mid   = WaterSurface.FlowSpeed(new Vector2(0.6f, 0f), baseFlow, scale, full); // half of full
            float fast  = WaterSurface.FlowSpeed(new Vector2(1.2f, 0f), baseFlow, scale, full); // at full
            float over  = WaterSurface.FlowSpeed(new Vector2(5f, 0f), baseFlow, scale, full);   // saturates

            Assert.AreEqual(baseFlow, slack, 1e-5f, "slack water drifts at the material's base flow (never frozen)");
            Assert.Greater(mid, slack, "more current => faster scroll");
            Assert.Greater(fast, mid, "monotonic in current speed");
            Assert.AreEqual(baseFlow + scale, fast, 1e-5f, "at full current the live add reaches the scale");
            Assert.AreEqual(fast, over, 1e-5f, "current beyond full saturates (clamped 0..1)");
        }

        [Test]
        public void FlowDirection_FollowsTheCurrentSet_AndIsNormalized()
        {
            var dir = WaterSurface.FlowDirection(new Vector2(3f, 4f));   // 3-4-5 → unit (0.6, 0.8)
            Assert.AreEqual(0.6f, dir.x, 1e-4f);
            Assert.AreEqual(0.8f, dir.y, 1e-4f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f, "scroll dir is a unit vector");

            var slack = WaterSurface.FlowDirection(Vector2.zero);       // no NaN on slack water
            Assert.AreEqual(Vector2.right, slack, "near-zero current falls back to +x, never NaN");
        }

        [Test]
        public void WindDirection_FollowsTheWind_NormalizesAndFallsBackOnSlackWind()
        {
            // EnvironmentSample.WindVector is direction × strength; WindDirection normalizes it (strength is
            // dropped here — it drives _Roughness separately). The shader scrolls its wind-chop octave along
            // this, so the surface follows the (time-varying) sim wind, not only the fixed current axis.
            var dir = WaterSurface.WindDirection(new Vector2(3f, 4f));   // 3-4-5 → unit (0.6, 0.8)
            Assert.AreEqual(0.6f, dir.x, 1e-4f);
            Assert.AreEqual(0.8f, dir.y, 1e-4f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f, "wind dir is a unit vector regardless of wind strength");

            // Strength must NOT change the direction: a stronger wind on the same bearing normalizes equal.
            var weak   = WaterSurface.WindDirection(new Vector2(0f, 2f));
            var strong = WaterSurface.WindDirection(new Vector2(0f, 18f));
            Assert.AreEqual(weak, strong, "direction is independent of wind strength (both unit +Y)");

            // Slack wind (near-zero) falls back to +Y — matching the shader's _WindDir default — never NaN.
            var slack = WaterSurface.WindDirection(Vector2.zero);
            Assert.AreEqual(Vector2.up, slack, "near-zero wind falls back to +y, never NaN");
            Assert.IsFalse(float.IsNaN(slack.x) || float.IsNaN(slack.y), "slack-wind fallback is NaN-safe");
        }

        // ===== WaterSurface: the COHESION-PASS direction twins (rolling swell + foam-drift blend) ==========
        // These guard the C# mirrors of the shader's SwellDir()/FoamDriftDir(): the large-scale ocean swell
        // keys off the (wandering) WIND by default so the cohesion bands REORIENT as the weather shifts (P1
        // "sea has moods"); an explicit override wins; and the foam drift follows a tunable BLEND of wind AND
        // the tidal current. NONE of this is pushed to the material (the shader derives it live from the
        // already-pushed _WindDir/_FlowDir) — these are the headless determinism guards for the LOGIC.

        [Test]
        public void SwellDirection_DefaultsToWind_OverrideWins_NormalizedAndNaNSafe()
        {
            // Auto (zero override): the swell axis follows the wind, normalized. Wind generates swell, so as
            // the sim wanders _WindDir the swell bands reorient — the cohesion is sim-true, not a fixed angle.
            var auto = WaterSurface.SwellDirection(new Vector2(3f, 4f), Vector2.zero);   // 3-4-5 → (0.6,0.8)
            Assert.AreEqual(0.6f, auto.x, 1e-4f);
            Assert.AreEqual(0.8f, auto.y, 1e-4f);
            Assert.AreEqual(1f, auto.magnitude, 1e-4f, "swell dir is a unit vector");

            // A non-zero explicit override wins over the wind (the _OceanSwellDir art-direction lever).
            var overridden = WaterSurface.SwellDirection(new Vector2(0f, 9f), new Vector2(1f, 0f));
            Assert.AreEqual(Vector2.right, overridden, "explicit _OceanSwellDir override beats auto-from-wind");

            // Slack wind AND zero override → +Y fallback (matching the shader default), never NaN.
            var slackSwell = WaterSurface.SwellDirection(Vector2.zero, Vector2.zero);
            Assert.AreEqual(Vector2.up, slackSwell, "no wind, no override → +Y fallback (shader default)");
            Assert.IsFalse(float.IsNaN(slackSwell.x) || float.IsNaN(slackSwell.y), "fallback is NaN-safe");
        }

        [Test]
        public void FoamDriftDirection_BlendsWindAndCurrent_DialsBetweenThem()
        {
            var wind    = new Vector2(0f, 1f);   // +Y
            var current = new Vector2(1f, 0f);   // +X

            // windVsCurrent = 0 → pure current-led; = 1 → pure wind-led (the two ends of the blend).
            var currentLed = WaterSurface.FoamDriftDirection(wind, current, 0f);
            var windLed     = WaterSurface.FoamDriftDirection(wind, current, 1f);
            Assert.AreEqual(Vector2.right, currentLed, "0 = pure current-led drift");
            Assert.AreEqual(Vector2.up, windLed, "1 = pure wind-led drift");

            // A mid blend points BETWEEN the two axes (45° here) and stays a unit vector.
            var mid = WaterSurface.FoamDriftDirection(wind, current, 0.5f);
            Assert.AreEqual(1f, mid.magnitude, 1e-4f, "blended drift is normalized");
            Assert.Greater(mid.x, 0f, "the mix leans partly toward the current axis");
            Assert.Greater(mid.y, 0f, "and partly toward the wind axis");
        }

        [Test]
        public void FoamDriftDirection_IsNaNSafe_OnSlackWindAndCurrent()
        {
            // Both forces slack: wind falls back to +Y, current to +X, so the blend is the normalized mix of
            // the two fallback axes — a FINITE UNIT vector, never NaN (the drift never freezes). The exact
            // heading depends on windVsCurrent, but the guarantee that matters is finite + unit-length.
            var slackDrift = WaterSurface.FoamDriftDirection(Vector2.zero, Vector2.zero, 0.6f);
            Assert.IsFalse(float.IsNaN(slackDrift.x) || float.IsNaN(slackDrift.y), "slack inputs stay NaN-safe");
            Assert.AreEqual(1f, slackDrift.magnitude, 1e-4f, "slack fallback is still a unit vector (never frozen/NaN)");
            Assert.Greater(slackDrift.x, 0f, "leans toward the +X current fallback");
            Assert.Greater(slackDrift.y, 0f, "and toward the +Y wind fallback (a blended, finite heading)");

            // Slack wind but a live current, fully wind-led: the wind axis fell back to +Y, so the wind-led
            // blend still resolves to a finite unit vector (no NaN from normalizing a zero wind vector).
            var windLedNoWind = WaterSurface.FoamDriftDirection(Vector2.zero, new Vector2(2f, 0f), 1f);
            Assert.AreEqual(1f, windLedNoWind.magnitude, 1e-4f, "wind-led with slack wind still unit (via +Y fallback)");
        }

        [Test]
        public void Roughness_RisesWithWind_AndSaturates()
        {
            const float full = 12f;
            Assert.AreEqual(0f, WaterSurface.Roughness(Vector2.zero, full), 1e-5f, "calm => glassy (0 roughness)");
            Assert.AreEqual(0.5f, WaterSurface.Roughness(new Vector2(6f, 0f), full), 1e-4f, "half wind => half roughness");
            Assert.AreEqual(1f, WaterSurface.Roughness(new Vector2(12f, 0f), full), 1e-5f, "full-wind => full whitecaps");
            Assert.AreEqual(1f, WaterSurface.Roughness(new Vector2(40f, 0f), full), 1e-5f, "a gale saturates at 1");
        }

        [Test]
        public void Choppiness_SpansGlassToStorm_Monotonic()
        {
            Assert.AreEqual(0f, WaterSurface.Choppiness(SeaState.Glass), 1e-5f, "glass is flat");
            Assert.AreEqual(1f, WaterSurface.Choppiness(SeaState.Storm), 1e-5f, "a storm is fully choppy");
            float calm = WaterSurface.Choppiness(SeaState.Calm);
            float rough = WaterSurface.Choppiness(SeaState.Rough);
            Assert.Greater(rough, calm, "rougher seas => more chop");
            Assert.That(WaterSurface.Choppiness(SeaState.Moderate), Is.InRange(0f, 1f), "stays in the 0..1 uniform range");
        }

        // ===== WaterSurface: the distance-to-land DEPTH estimate (the no-height-map shore gradient) =======
        // These prove the fallback depth curve: ~0 (shallow + foam) right at the shore, deepening smoothly to
        // a max depth offshore and saturating there — the tunable drop-off the owner adjusts in any scene.

        [Test]
        public void DistanceToDepth_ShallowAtShore_DeepOffshore()
        {
            const float dropoff = 14f, maxDepth = 3.5f;

            float atShore = WaterSurface.DistanceToDepth(0f, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);
            float offshore = WaterSurface.DistanceToDepth(dropoff, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);
            float farOut = WaterSurface.DistanceToDepth(dropoff * 4f, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);

            Assert.AreEqual(0f, atShore, 1e-5f, "depth is ~0 at the waterline (shallow, foam sits here)");
            Assert.AreEqual(maxDepth, offshore, 1e-4f, "depth reaches the max by the drop-off distance");
            Assert.AreEqual(maxDepth, farOut, 1e-5f, "depth saturates at the max past the drop-off (no runaway)");
        }

        [Test]
        public void DistanceToDepth_IsMonotonicNonDecreasing_BothCurves()
        {
            const float dropoff = 10f, maxDepth = 4f;
            foreach (var curve in new[] { WaterSurface.DropoffCurve.Linear, WaterSurface.DropoffCurve.Smooth })
            {
                float prev = WaterSurface.DistanceToDepth(0f, dropoff, maxDepth, curve);
                for (float d = 0.5f; d <= dropoff; d += 0.5f)
                {
                    float depth = WaterSurface.DistanceToDepth(d, dropoff, maxDepth, curve);
                    Assert.GreaterOrEqual(depth + 1e-5f, prev, $"{curve}: depth never decreases going offshore");
                    Assert.That(depth, Is.InRange(0f, maxDepth + 1e-4f), $"{curve}: stays within [0, maxDepth]");
                    prev = depth;
                }
            }
        }

        [Test]
        public void DistanceToDepth_LinearVsSmooth_DifferShape_AgreeAtEnds()
        {
            const float dropoff = 12f, maxDepth = 3f;
            float lin0  = WaterSurface.DistanceToDepth(0f, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);
            float smo0  = WaterSurface.DistanceToDepth(0f, dropoff, maxDepth, WaterSurface.DropoffCurve.Smooth);
            float linEnd = WaterSurface.DistanceToDepth(dropoff, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);
            float smoEnd = WaterSurface.DistanceToDepth(dropoff, dropoff, maxDepth, WaterSurface.DropoffCurve.Smooth);
            Assert.AreEqual(lin0, smo0, 1e-5f, "both start at 0 depth at the shore");
            Assert.AreEqual(linEnd, smoEnd, 1e-4f, "both reach maxDepth at the drop-off");

            // Mid-shelf: smoothstep eases in, so it sits SHALLOWER than the straight line near the shore.
            float mid = dropoff * 0.25f;
            float linMid = WaterSurface.DistanceToDepth(mid, dropoff, maxDepth, WaterSurface.DropoffCurve.Linear);
            float smoMid = WaterSurface.DistanceToDepth(mid, dropoff, maxDepth, WaterSurface.DropoffCurve.Smooth);
            Assert.Less(smoMid, linMid, "the smooth curve keeps a gentler shelf near the shore than linear");
        }

        [Test]
        public void DistanceToDepth_GuardsZeroDropoff_NoDivideByZero()
        {
            float d = WaterSurface.DistanceToDepth(5f, 0f, 3f, WaterSurface.DropoffCurve.Linear);
            Assert.AreEqual(3f, d, 1e-4f, "a zero drop-off saturates to maxDepth immediately rather than NaN");
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        public void Smoothstep01_HitsEndpointsAndMidpoint(float t, float expected)
        {
            Assert.AreEqual(expected, WaterSurface.Smoothstep01(t), 1e-5f);
        }

        // ===== WaterSurface: the ALWAYS-ON beach swash (cosmetic shoreline wash) ==========================
        // These guard the C# twin of the shader's swash math: it oscillates over time (waves in/out), stays
        // within the amplitude, and — the P1 integrity invariant — is CONFINED to the foam band by the gate
        // (zero in deep water), so the cosmetic wash can never move deep water or the gameplay waterline.

        [Test]
        public void SwashOffset_OscillatesInTime_WithinAmplitude()
        {
            const float speed = 0.5f, amp = 0.3f, alongShore = 0f;
            float minV = float.MaxValue, maxV = float.MinValue;
            // Sample a full beat (the slow octave has period 1/(speed*0.5) = 4s at speed 0.5).
            for (float tm = 0f; tm <= 4f; tm += 0.05f)
            {
                float v = WaterSurface.SwashOffset(tm, speed, amp, alongShore);
                Assert.LessOrEqual(Mathf.Abs(v), amp + 1e-4f, "swash never exceeds its amplitude");
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
            }
            Assert.Greater(maxV, 0.05f, "the wash runs UP the beach (positive offset) within a beat");
            Assert.Less(minV, -0.05f, "the wash runs BACK (negative offset) within a beat — it moves in AND out");
        }

        [Test]
        public void SwashOffset_IsContinuousAndTimeDriven_NotFrozen()
        {
            // Two distinct times give distinct offsets => it's animated, not a static fringe.
            float a = WaterSurface.SwashOffset(0.3f, 0.5f, 0.3f, 1.2f);
            float b = WaterSurface.SwashOffset(0.9f, 0.5f, 0.3f, 1.2f);
            Assert.AreNotEqual(a, b, "swash advances with time (the always-on motion the tide alone lacked)");
        }

        [Test]
        public void SwashBandGate_FullAtShore_ZeroInDeepWater()
        {
            const float foamWidth = 0.45f, amp = 0.3f;
            float atEdge = WaterSurface.SwashBandGate(0f, foamWidth, amp);
            Assert.AreEqual(1f, atEdge, 1e-4f, "the wash is strongest right at the wet edge (depth 0)");

            // reach = foamWidth*2 + amp = 1.2 m. Anything at/beyond reach must read EXACTLY zero — the
            // invariant that keeps the cosmetic wash out of deep water (P1 integrity, rule 5).
            float reach = foamWidth * 2f + amp;
            Assert.AreEqual(0f, WaterSurface.SwashBandGate(reach, foamWidth, amp), 1e-4f,
                "the wash is fully gone by the band reach");
            Assert.AreEqual(0f, WaterSurface.SwashBandGate(10f, foamWidth, amp), 1e-6f,
                "deep water gets ZERO swash — it cannot move the gameplay waterline");
        }

        [Test]
        public void SwashBandGate_IsMonotonicNonIncreasing_FromShoreToDeep()
        {
            const float foamWidth = 0.4f, amp = 0.25f;
            float prev = WaterSurface.SwashBandGate(0f, foamWidth, amp);
            for (float d = 0.05f; d <= 2f; d += 0.05f)
            {
                float g = WaterSurface.SwashBandGate(d, foamWidth, amp);
                Assert.LessOrEqual(g, prev + 1e-5f, "the gate only fades going offshore (never re-strengthens)");
                Assert.That(g, Is.InRange(0f, 1f), "the gate stays a 0..1 weight");
                prev = g;
            }
        }

        [Test]
        public void PointInPolygon_InsideAndOutsideASquare()
        {
            var square = new[]
            {
                new Vector2(-2f, -2f), new Vector2(2f, -2f), new Vector2(2f, 2f), new Vector2(-2f, 2f),
            };
            Assert.IsTrue(WaterSurface.PointInPolygon(new Vector2(0f, 0f), square), "centre is inside");
            Assert.IsFalse(WaterSurface.PointInPolygon(new Vector2(5f, 0f), square), "well outside reads outside");
            Assert.IsFalse(WaterSurface.PointInPolygon(Vector2.zero, null), "null polygon is never inside (safe)");
        }
    }
}
