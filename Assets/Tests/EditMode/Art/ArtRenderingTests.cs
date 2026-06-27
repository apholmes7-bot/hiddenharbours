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

        // ===== WaterSurface: the FLOW-MOMENTUM smoothing (the water has MASS) ==============================
        // These guard SmoothVectorToward — the frame-rate-independent exponential ease the surface uses so the
        // VISUAL flow LAGS the live sim instead of SNAPPING when the wind/current heading wanders. The four
        // properties the momentum feel rests on: it eases toward a steady target; on a heading REVERSAL the
        // smoothed magnitude DIPS below both endpoints mid-turn (the "slows through the turn, then speeds back
        // up" feel — because we smooth the VECTOR, not heading+magnitude apart); it is frame-rate independent
        // (sub-stepping reaches the same end state); and it is deterministic. Presentation only (rule 5).

        [Test]
        public void SmoothFlow_EasesTowardASteadyTarget_MonotonicAndConverges()
        {
            var target = new Vector2(2f, 0f);
            var v = Vector2.zero;               // start at rest; the sim flow is steady at the target
            const float tau = 3f, dt = 1f / 8f; // a 3 s response at the 8 Hz default push cadence

            float prevDist = (target - v).magnitude;
            float prevMag = v.magnitude;
            // Run for ~10 response-times (30 s at τ=3): the residual gap is 2·exp(−30/3) ≈ 9e-5, so "reached".
            for (int i = 0; i < 8 * 30; i++)    // 30 s of pushes
            {
                v = WaterSurface.SmoothVectorToward(v, target, tau, dt);
                float dist = (target - v).magnitude;
                Assert.LessOrEqual(dist, prevDist + 1e-6f, "each step moves no further from the target (eases in)");
                Assert.GreaterOrEqual(v.magnitude + 1e-6f, prevMag, "toward a co-linear target the speed only rises (accelerates out)");
                Assert.LessOrEqual(v.x, target.x + 1e-4f, "never overshoots the target (no spring ringing)");
                prevDist = dist;
                prevMag = v.magnitude;
            }
            Assert.AreEqual(target.x, v.x, 2e-3f, "after many response-times the smoothed flow has reached the live sim");
            Assert.AreEqual(target.y, v.y, 1e-4f);

            // One step covers exactly the analytic fraction 1 − exp(−dt/τ) of the gap (the smoothing law).
            float oneStepX = WaterSurface.SmoothVectorToward(Vector2.zero, target, tau, dt).x;
            float expectedFrac = 1f - Mathf.Exp(-dt / tau);
            Assert.AreEqual(target.x * expectedFrac, oneStepX, 1e-5f, "one step = the exact exponential fraction of the gap");
        }

        [Test]
        public void SmoothFlow_OnReversal_MagnitudeDipsBelowBothEndpoints_MidTurn()
        {
            // The headline momentum property. The sim flow REVERSES heading (+X → −X) at full magnitude; because
            // we smooth the VECTOR, the smoothed point travels through the origin region as it rotates/reverses,
            // so its MAGNITUDE dips well below BOTH endpoints mid-turn — the surface visibly SLOWS through the
            // turn, then speeds back up. (Smoothing heading+magnitude separately would hold the speed flat — the
            // exact snap we're avoiding.)
            var from = new Vector2(1.5f, 0f);
            var to   = new Vector2(-1.5f, 0f);
            float endpointMag = from.magnitude;   // == to.magnitude == 1.5
            const float tau = 3f, dt = 1f / 8f;

            var v = from;                         // already tracking the old flow, then it flips
            float minMag = endpointMag;
            for (int i = 0; i < 8 * 30; i++)      // run well past the turn
            {
                v = WaterSurface.SmoothVectorToward(v, to, tau, dt);
                minMag = Mathf.Min(minMag, v.magnitude);
            }
            Assert.Less(minMag, endpointMag - 0.25f, "mid-reversal the smoothed speed DIPS clearly below both endpoints (slows through the turn)");
            Assert.Less(minMag, 0.4f, "and dips close to slack as the vector passes through the reversal (a real deceleration, not a graze)");

            // It recovers: by the end it has accelerated back out toward the new full-magnitude heading.
            Assert.AreEqual(to.x, v.x, 1e-2f, "after the turn the flow speeds back up to the new heading (accelerates out)");
            Assert.Less(v.x, 0f, "and is now pointing the new way (the reversal completed)");
        }

        [Test]
        public void SmoothFlow_IsFrameRateIndependent_SubSteppingReachesSameEndState()
        {
            // Smoothing once over a big dt must reach ~the same place as many small sub-steps over the same total
            // time toward a FIXED target — so the eased look doesn't change with the refresh rate. (Exact under
            // the exponential law: the (1 − exp) factors compose multiplicatively.)
            var target = new Vector2(-1f, 2f);
            const float tau = 2.5f, total = 1f;

            var coarse = WaterSurface.SmoothVectorToward(Vector2.zero, target, tau, total);          // 1 step of 1.0 s

            var fine = Vector2.zero;
            const int n = 64;
            for (int i = 0; i < n; i++)                                                              // 64 steps of 1/64 s
                fine = WaterSurface.SmoothVectorToward(fine, target, tau, total / n);

            Assert.AreEqual(coarse.x, fine.x, 1e-4f, "coarse vs fine sub-stepping converge to the same X (fps-independent)");
            Assert.AreEqual(coarse.y, fine.y, 1e-4f, "…and the same Y");
        }

        [Test]
        public void SmoothFlow_IsDeterministic_AndSnapsWhenResponseTimeIsZero()
        {
            var smoothed = new Vector2(0.3f, -0.7f);
            var target   = new Vector2(2f, 1f);

            // Deterministic: identical inputs give bit-identical outputs (the determinism guard, rule 5).
            var a = WaterSurface.SmoothVectorToward(smoothed, target, 3f, 1f / 8f);
            var b = WaterSurface.SmoothVectorToward(smoothed, target, 3f, 1f / 8f);
            Assert.AreEqual(a, b, "same inputs => same output (no hidden state / randomness)");

            // responseTime <= 0 snaps to the target (the "no inertia" / instant-snap escape — the old behaviour).
            var snap = WaterSurface.SmoothVectorToward(smoothed, target, 0f, 1f / 8f);
            Assert.AreEqual(target, snap, "zero response time = instant snap (no momentum), exactly the live sim");

            // A negative dt is guarded (returns the target rather than moving the wrong way / producing NaN).
            var guarded = WaterSurface.SmoothVectorToward(smoothed, target, 3f, -1f);
            Assert.AreEqual(target, guarded, "a negative dt is guarded — no backwards step, no NaN");
            Assert.IsFalse(float.IsNaN(guarded.x) || float.IsNaN(guarded.y), "guarded result is finite");
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

        // ===== WaterSurface: the LIVING-FOAM soft-threshold (metaball merge / separate) ====================
        // These guard the C# twin of the shader's foam mask: the foam is built from a SMOOTHSTEP around a
        // threshold on an evolving field, NOT a hard step. That soft band is what makes blobs MERGE (a rising
        // valley between two maxima crosses thr−soft), SEPARATE (the field dips back below), and fade in/out.
        // The evolving FIELD is GPU value-noise (not unit-testable headless); the THRESHOLD math — the part
        // that produces the merge/separate behaviour — is pure and mirrored here. Visual-only (P1, rule 5).

        [Test]
        public void Smoothstep_HitsEdges_AndEasesBetween()
        {
            // Below edge0 → 0, above edge1 → 1, midpoint → 0.5, with an ease-in-out shape between.
            Assert.AreEqual(0f, WaterSurface.Smoothstep(0.4f, 0.7f, 0.2f), 1e-5f, "below edge0 is fully off");
            Assert.AreEqual(1f, WaterSurface.Smoothstep(0.4f, 0.7f, 0.9f), 1e-5f, "above edge1 is fully on");
            Assert.AreEqual(0.5f, WaterSurface.Smoothstep(0.4f, 0.7f, 0.55f), 1e-5f, "the midpoint is half coverage");

            // Degenerate (equal) edges fall back to a hard step at the edge — no NaN / divide-by-zero.
            Assert.AreEqual(0f, WaterSurface.Smoothstep(0.5f, 0.5f, 0.49f), 1e-6f, "equal edges: just-below is 0");
            Assert.AreEqual(1f, WaterSurface.Smoothstep(0.5f, 0.5f, 0.50f), 1e-6f, "equal edges: at/above is 1 (no NaN)");
        }

        [Test]
        public void FoamSoftThreshold_IsSoft_NotAHardStep_AndMonotonic()
        {
            const float thr = 0.55f, soft = 0.18f;
            // A SOFT band: a field value inside (thr−soft, thr+soft) is PARTIAL coverage — the thing a hard
            // step() could never produce, and exactly what lets blob edges fade rather than pop.
            float partial = WaterSurface.FoamSoftThreshold(thr, thr, soft);
            Assert.AreEqual(0.5f, partial, 1e-4f, "a field exactly at the threshold is half-coverage (soft, not 0/1)");
            float justInside = WaterSurface.FoamSoftThreshold(thr + soft * 0.5f, thr, soft);
            Assert.That(justInside, Is.InRange(0.5f, 1f), "inside the band reads as a fractional, growing coverage");

            // Fully below the band → no foam; fully above → solid foam (the blob's bright core).
            Assert.AreEqual(0f, WaterSurface.FoamSoftThreshold(thr - soft - 0.01f, thr, soft), 1e-4f, "below the band: no foam");
            Assert.AreEqual(1f, WaterSurface.FoamSoftThreshold(thr + soft + 0.01f, thr, soft), 1e-4f, "above the band: solid foam");

            // Monotonic non-decreasing in the field value (a rising field only ever adds coverage — the basis
            // for "a maximum rising through the band fades the blob IN; falling back fades it OUT").
            float prev = WaterSurface.FoamSoftThreshold(0f, thr, soft);
            for (float f = 0.02f; f <= 1f; f += 0.02f)
            {
                float c = WaterSurface.FoamSoftThreshold(f, thr, soft);
                Assert.GreaterOrEqual(c + 1e-5f, prev, "coverage never decreases as the field rises (fades in, not flickers)");
                Assert.That(c, Is.InRange(0f, 1f), "coverage stays a 0..1 weight");
                prev = c;
            }
        }

        [Test]
        public void FoamSoftThreshold_TwoMaxima_MergeWhenValleyRises_SeparateWhenItDips()
        {
            // The metaball mechanism in one assertion set. Model two foam maxima as the field; the VALLEY between
            // them is the field's value at the midpoint. With a fixed threshold:
            const float thr = 0.5f, soft = 0.2f;   // band = [0.3, 0.7]

            // Two SEPARATE blobs: each maximum is above the band (foam), but the valley between them sits BELOW
            // thr−soft → zero coverage in the gap → the blobs read as two separate patches.
            float maxCoverage = WaterSurface.FoamSoftThreshold(0.9f, thr, soft);
            float valleyLow   = WaterSurface.FoamSoftThreshold(0.25f, thr, soft);   // valley below the band
            Assert.AreEqual(1f, maxCoverage, 1e-4f, "each maximum is solid foam");
            Assert.AreEqual(0f, valleyLow, 1e-4f, "a LOW valley between them is bare water — the blobs are SEPARATE");

            // Now the maxima grow toward each other and the VALLEY rises above thr−soft: the gap fills with foam
            // → the two blobs MERGE into one connected patch. Same threshold, only the field changed (it evolved).
            float valleyRisen = WaterSurface.FoamSoftThreshold(0.45f, thr, soft);   // valley risen into the band
            Assert.Greater(valleyRisen, 0f, "a RISEN valley now carries foam — the two blobs have MERGED");
            Assert.Greater(valleyRisen, valleyLow, "merging is monotonic: the higher the valley, the more it fills in");
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
