using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The pure, headless-testable MODEL for the WEATHER-DRIVEN water palette (ADR 0017): a realistic 2-axis
    /// blend that turns the deterministic <see cref="EnvironmentSample"/> (the sea's mood) into a set of
    /// 0..1 weights across the four anchor preset MOODS — a region BASE, a CALM mood, a STORM mood, and a FOG
    /// mood — so the sea's colour/character EASES through the preset library as the weather shifts (P1 "the
    /// sea has moods"). <see cref="WaterSurface"/> consumes the weights to lerp the MOOD/COLOUR shader
    /// properties from the anchor materials and pushes the blended set through its MaterialPropertyBlock.
    ///
    /// <para><b>The realistic model (two axes, combined).</b>
    /// <list type="number">
    /// <item><description><b>Sea-state axis</b> — the normalised sea-state (Glass..Storm, see
    /// <see cref="SeaStateAxis01"/>) shaped by a tunable threshold/curve drives a CALM↔STORM lerp: a serene
    /// clear sea at low sea-state, lerping toward the greyer/choppier/desaturated STORM mood as it
    /// rises.</description></item>
    /// <item><description><b>Fog axis</b> — <c>(1 − Visibility)</c> shaped by a tunable threshold drives a pull
    /// toward the FOG mood (pale, desaturated, low-contrast, soft).</description></item>
    /// <item><description><b>Combine</b> — the sea-state lerp produces a calm↔storm base mood; the fog amount
    /// then pulls THAT toward the fog mood. So a foggy storm reads mostly fog (the smother dominates the look)
    /// while a foggy calm reads pale-serene — exactly the realistic ordering.</description></item>
    /// </list>
    /// The weights returned are over {base, calm, storm, fog} and sum to 1, so the caller does one weighted
    /// blend of the four anchor materials' mood props.</para>
    ///
    /// <para><b>Determinism (CLAUDE.md rule 5).</b> Every method here is a pure function of its arguments — no
    /// time, no global state, no randomness. The smoothing (<see cref="EaseWeights"/>) is a frame-rate-
    /// independent exponential ease (the same form as <see cref="WaterSurface.SmoothVectorToward"/>) applied
    /// only to PRESENTATION (the visible mood), never to the sim; it saves nothing. The whole feature is a pure
    /// function of the deterministic <see cref="EnvironmentSample"/> + the (presentation-only) smoothing
    /// state.</para>
    ///
    /// <para><b>No magic numbers (rule 6).</b> The thresholds/curve/response time are passed in by the caller
    /// from serialized tunables; nothing is hard-coded here beyond NaN-safe epsilons.</para>
    /// </summary>
    public static class WeatherWaterPalette
    {
        /// <summary>The four anchor MOODS the blend mixes, in a fixed order (the weight array's indices).</summary>
        public enum Anchor
        {
            /// <summary>The region/base preset — the look at fair, clear, calm-ish weather (e.g. North Atlantic).</summary>
            Base = 0,
            /// <summary>The serene/glassy CALM mood — strongest at the lowest sea-state (e.g. Glassy Calm).</summary>
            Calm = 1,
            /// <summary>The grey, choppy, desaturated STORM mood — strongest at high sea-state (e.g. Storm Grey).</summary>
            Storm = 2,
            /// <summary>The pale, low-contrast, soft FOG mood — strongest at low visibility (e.g. Foggy Smother).</summary>
            Fog = 3,
        }

        /// <summary>The number of anchor moods (the length of every weight array).</summary>
        public const int AnchorCount = 4;

        /// <summary>
        /// Normalise a <see cref="SeaState"/> (Glass=0 .. Storm=7) into 0..1 — the raw sea-state axis BEFORE
        /// the threshold/curve shaping. Pure; <c>Glass → 0</c>, <c>Storm → 1</c>, linear across the canon range.
        /// </summary>
        public static float SeaStateAxis01(SeaState seaState)
        {
            int max = (int)SeaState.Storm;   // 7
            return max > 0 ? Mathf.Clamp01((int)seaState / (float)max) : 0f;
        }

        /// <summary>
        /// Shape a raw 0..1 axis value through a tunable threshold + curve into a 0..1 blend amount. Below
        /// <paramref name="threshold"/> the amount is 0 (the mood doesn't engage until conditions cross the
        /// threshold); above it the remainder is remapped to 0..1 and raised to <paramref name="curve"/>
        /// (curve &gt; 1 = a slow start that ramps late — the storm/fog only really bites near the top;
        /// curve = 1 = linear). Monotonic non-decreasing in <paramref name="raw"/>; deterministic.
        /// </summary>
        /// <param name="raw">The raw axis value (0..1), e.g. <see cref="SeaStateAxis01"/> or <c>1 − Visibility</c>.</param>
        /// <param name="threshold">The axis value (0..1) at/below which the amount is 0. Clamped to [0, ~1).</param>
        /// <param name="curve">The shaping exponent (≥ a small floor). 1 = linear; &gt;1 = late-ramping.</param>
        public static float ShapeAxis(float raw, float threshold, float curve)
        {
            float r = Mathf.Clamp01(raw);
            float t = Mathf.Clamp(threshold, 0f, 0.999f);
            float remapped = Mathf.Clamp01((r - t) / Mathf.Max(1f - t, 1e-3f));
            float c = Mathf.Max(curve, 1e-3f);
            return Mathf.Pow(remapped, c);
        }

        /// <summary>
        /// The realistic 2-axis blend: compute the four anchor weights (over {Base, Calm, Storm, Fog}, summing
        /// to 1) for a given sea-state + visibility. PURE — a deterministic function of the sample + tunables.
        ///
        /// <para>The model (see the class summary): the sea-state axis lerps a calm↔storm base mood, then the
        /// fog amount pulls the whole thing toward fog. Concretely:
        /// <list type="bullet">
        /// <item><description><c>seaAmt = ShapeAxis(SeaStateAxis01, seaThreshold, seaCurve)</c> — 0 at the
        /// lowest sea-state, 1 at storm.</description></item>
        /// <item><description><c>fogAmt = ShapeAxis(1 − visibility, fogThreshold, fogCurve)</c> — 0 in clear
        /// air, 1 in a thick smother.</description></item>
        /// <item><description>The non-fog portion <c>(1 − fogAmt)</c> splits between the calm↔storm lerp:
        /// <c>calm = (1 − fogAmt)·(1 − seaAmt)·calmReach</c>, <c>storm = (1 − fogAmt)·seaAmt</c>, the base
        /// taking the rest — so a clear, mid sea reads as a calm/base mix, a clear gale reads storm, and any
        /// fog steals weight toward the fog mood on top.</description></item>
        /// </list>
        /// <paramref name="calmReach"/> (0..1) dials how strongly the lowest sea-state pulls toward the pure
        /// CALM mood vs sitting on the region BASE: 0 = the base IS the calm look (calm anchor unused), 1 =
        /// glassy water reads fully as the Calm preset. The base always backfills so the weights sum to 1.</para>
        /// </summary>
        /// <param name="seaState">The deterministic sea-state (Glass..Storm).</param>
        /// <param name="visibility">The deterministic visibility (1 = clear .. 0 = thick fog).</param>
        /// <param name="seaThreshold">Sea-state axis threshold below which no storm pull (0..1).</param>
        /// <param name="seaCurve">Sea-state axis shaping exponent (1 = linear; &gt;1 = storm bites late).</param>
        /// <param name="fogThreshold">Fog axis threshold below which no fog pull (0..1 on 1−visibility).</param>
        /// <param name="fogCurve">Fog axis shaping exponent (1 = linear; &gt;1 = fog bites late).</param>
        /// <param name="calmReach">How far the lowest sea-state pulls toward the pure Calm mood (0..1).</param>
        /// <returns>A length-<see cref="AnchorCount"/> array of 0..1 weights summing to 1
        /// (indexed by <see cref="Anchor"/>).</returns>
        public static float[] BlendWeights(SeaState seaState, float visibility,
                                           float seaThreshold, float seaCurve,
                                           float fogThreshold, float fogCurve,
                                           float calmReach)
        {
            var w = new float[AnchorCount];
            BlendWeightsNonAlloc(w, seaState, visibility,
                                 seaThreshold, seaCurve, fogThreshold, fogCurve, calmReach);
            return w;
        }

        /// <summary>
        /// Allocation-free twin of <see cref="BlendWeights"/> — writes the four weights into a caller-owned
        /// buffer (length ≥ <see cref="AnchorCount"/>) so the per-tick blend in <see cref="WaterSurface"/>
        /// never allocates (CLAUDE.md rule 7). Same pure model and guarantees (weights ≥ 0, sum to 1).
        /// </summary>
        public static void BlendWeightsNonAlloc(float[] weights, SeaState seaState, float visibility,
                                                float seaThreshold, float seaCurve,
                                                float fogThreshold, float fogCurve,
                                                float calmReach)
        {
            // The two shaped axes.
            float seaAmt = ShapeAxis(SeaStateAxis01(seaState), seaThreshold, seaCurve);
            float fogAmt = ShapeAxis(1f - Mathf.Clamp01(visibility), fogThreshold, fogCurve);
            float reach = Mathf.Clamp01(calmReach);

            // Non-fog portion splits calm↔storm by the sea-state; fog steals weight on top.
            float nonFog = 1f - fogAmt;
            float storm = nonFog * seaAmt;
            float calm = nonFog * (1f - seaAmt) * reach;
            float fog = fogAmt;
            // The region base backfills whatever the three moods didn't claim (clamped ≥ 0 for safety).
            float baseW = Mathf.Max(0f, nonFog - storm - calm);

            weights[(int)Anchor.Base] = baseW;
            weights[(int)Anchor.Calm] = calm;
            weights[(int)Anchor.Storm] = storm;
            weights[(int)Anchor.Fog] = fog;
            NormalizeInPlace(weights);
        }

        /// <summary>
        /// Ease a SMOOTHED weight set one step toward a TARGET weight set — the presentation-only mood ease so
        /// the sea's palette never POPS as the weather shifts; it slides between moods. Frame-rate-independent
        /// exponential smoothing per component (the same form as <see cref="WaterSurface.SmoothVectorToward"/>):
        /// <c>smoothed += (target − smoothed)·(1 − exp(−dt/τ))</c>. <paramref name="responseTime"/> (τ, seconds)
        /// ≤ 0 snaps to the target (no ease). After easing the result is re-normalised so it stays a valid
        /// weight set (sums to 1). Writes into <paramref name="smoothed"/> in place (no alloc). Deterministic;
        /// drives no sim, saves nothing (rule 5).
        /// </summary>
        /// <param name="smoothed">The persistent smoothed weights (read + written in place).</param>
        /// <param name="target">The freshly computed target weights (from <see cref="BlendWeights"/>).</param>
        /// <param name="responseTime">The ease time constant (seconds); ≤ 0 = instant snap.</param>
        /// <param name="dt">The elapsed time since the last ease (seconds; the push cadence).</param>
        public static void EaseWeights(float[] smoothed, float[] target, float responseTime, float dt)
        {
            if (smoothed == null || target == null) return;
            int n = Mathf.Min(smoothed.Length, target.Length);
            if (responseTime <= 0f || dt < 0f)
            {
                for (int i = 0; i < n; i++) smoothed[i] = target[i];
                NormalizeInPlace(smoothed);
                return;
            }
            float alpha = 1f - Mathf.Exp(-dt / responseTime);   // 0 (no move) .. 1 (full move) — fps-independent
            for (int i = 0; i < n; i++)
                smoothed[i] += (target[i] - smoothed[i]) * alpha;
            NormalizeInPlace(smoothed);
        }

        /// <summary>
        /// Normalise a weight array so its components are ≥ 0 and sum to 1 (a degenerate all-zero/negative set
        /// falls back to all-weight-on-Base, so a blend is always well-defined). In place; no alloc. Pure.
        /// </summary>
        public static void NormalizeInPlace(float[] weights)
        {
            if (weights == null || weights.Length == 0) return;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] < 0f) weights[i] = 0f;
                sum += weights[i];
            }
            if (sum <= 1e-6f)
            {
                // Degenerate — put all the weight on the base mood so the caller still has a valid blend.
                for (int i = 0; i < weights.Length; i++) weights[i] = 0f;
                weights[(int)Anchor.Base] = 1f;
                return;
            }
            float inv = 1f / sum;
            for (int i = 0; i < weights.Length; i++) weights[i] *= inv;
        }

        /// <summary>
        /// Apply a master STRENGTH to a weight set: lerp the weights from the IDENTITY (all weight on the base
        /// mood = today's static look) toward the live <paramref name="weights"/> by <paramref name="strength"/>
        /// (0 = base only = the pre-feature look; 1 = the full weather-driven blend). Writes in place; the
        /// result stays a valid weight set (sums to 1). This is the opt-in / revertible dial — at strength 0 the
        /// blend collapses to the base anchor, so the caller blends to exactly the base preset (and the surface
        /// reads as it does today when the base anchor is the live <c>Water.mat</c> look). Pure; no alloc.
        /// </summary>
        public static void ApplyStrengthInPlace(float[] weights, float strength)
        {
            if (weights == null || weights.Length == 0) return;
            float s = Mathf.Clamp01(strength);
            int baseIdx = (int)Anchor.Base;
            for (int i = 0; i < weights.Length; i++)
            {
                float identity = i == baseIdx ? 1f : 0f;
                weights[i] = Mathf.Lerp(identity, weights[i], s);
            }
            NormalizeInPlace(weights);
        }
    }
}
