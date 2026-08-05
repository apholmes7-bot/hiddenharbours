using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The weight filter's state — the (offset, velocity) pair that chases the surface. Plain
    /// mutable struct owned by the consumer (<see cref="BoatWaveMotion"/> keeps one per boat),
    /// reset to <c>default</c> on wake so a re-enabled hull never chases a stale sea.
    /// </summary>
    public struct HeaveWeightState
    {
        /// <summary>The weighted ride (metres) — where the hull actually is.</summary>
        public float RideMeters;

        /// <summary>Its vertical rate (m/s).</summary>
        public float VelocityMetersPerSec;

        /// <summary>False until the first step seeds the state from the live surface. A default
        /// (unprimed) state never eases in from zero — it wakes ON the water.</summary>
        public bool Primed;
    }

    /// <summary>
    /// <b>The storm seakeeping read (ADR 0018 B2.5)</b> — the pure, headless-testable core that makes
    /// the hull's VISUAL response grow with the sea and carry weight (P1 "The Sea Has Moods", P5
    /// "Cozy but with Teeth"; owner 2026-08-05: <i>"is there steep enough front-to-back rocking to
    /// represent the storm waves? … It must stay smooth and obey gravity though"</i>).
    ///
    /// <para><b>Why the sea-state axis and not the sampled slope.</b> The wave field's dominant
    /// wavelength GROWS with the wind (λ = base + perWind·speed), so the surface slope — amplitude ×
    /// wave number — saturates by construction: between sea 0.7 and 1.0 the drawn slope grows ~10%
    /// while the wave height grows ~70%. Every angular read keyed off slope alone therefore stops
    /// answering the sea exactly where the owner said it did. <see cref="StormBlend01"/> re-injects
    /// the axis the slope loses, off the same deterministic <c>SeaState01</c> every other consumer
    /// scales with (rule 5 — a pure function of the deterministic wind).</para>
    ///
    /// <para><b>Calm is the negative control.</b> At or below the settings' storm start the blend is
    /// exactly 0 — multipliers exactly 1, extras exactly 0, the weight filter an exact passthrough —
    /// so the owner's tuned calm feel is byte-identical (a ×1f multiply is the identity).</para>
    ///
    /// <para><b>The weight filter (obey gravity).</b> <see cref="StepHeaveWeight"/> runs a
    /// spring-damper chase of the displaced ride with ONE physical guarantee: the chase's downward
    /// acceleration never exceeds g × the settings cap — crossing a sharpened crest the surface can
    /// plummet faster than gravity, and the hull must NOT be bolted to it; it unweights, falls at g,
    /// and lands. Upward is uncapped (buoyancy carries it). A hard band keeps it honest (never
    /// hovers, never submarines) and an epsilon snap settles it exactly (no eternal micro-motion).
    /// Stateful but presentation-only, deterministic per tick sequence, fps-independent by sub-step —
    /// the <see cref="WaveFieldAnimator"/> honesty contract exactly; nothing here feeds the sim, and
    /// B3's <see cref="SeakeepingForcesMath"/> keeps its own pure path untouched.</para>
    ///
    /// <para>Static, allocation-free, engine-light (Mathf only) — same discipline (and EditMode
    /// coverage) as <see cref="BoatWaveMotionMath"/> / <see cref="SeakeepingForcesMath"/>.</para>
    /// </summary>
    public static class StormRockMath
    {
        /// <summary>Floor on the per-hull response factor wherever it bends the chase stiffness, so
        /// a zeroed hull def cannot collapse the spring to a dead stop. A guard, not a tunable.</summary>
        public const float MinHullResponseFactor = 0.25f;

        /// <summary>Integrator sub-step ceiling (seconds). The chase runs semi-implicit Euler; at
        /// ω ≈ 7 rad/s a 20 ms step is far inside the stability bound (ω·h ≪ 2), so one visual
        /// frame at any playable fps integrates identically enough to read the same. A guard.</summary>
        public const float MaxSubStepSeconds = 0.02f;

        /// <summary>Ceiling on sub-steps per call so a huge dt (a sleep skip, a hitch) costs bounded
        /// work; the band clamp keeps the result sane regardless. A guard.</summary>
        public const int MaxSubSteps = 64;

        /// <summary>
        /// The storm blend (0..1): exactly 0 at/below the settings' storm start (and whenever the
        /// block is disabled), rising as <c>t^ResponseExponent</c> to 1 at sea state 1. The ONE
        /// curve every B2.5 consumer scales by — monotone in <paramref name="seaState01"/> by
        /// construction.
        /// </summary>
        public static float StormBlend01(float seaState01, in StormRockSettings settings)
        {
            if (!settings.Enabled) return 0f;
            float start = Mathf.Clamp01(settings.StormStartSeaState01);
            float sea = Mathf.Clamp01(seaState01);
            if (sea <= start) return 0f;
            float span = Mathf.Max(1e-4f, 1f - start);
            return Mathf.Pow(Mathf.Clamp01((sea - start) / span), Mathf.Max(0.01f, settings.ResponseExponent));
        }

        /// <summary>
        /// The gain/cap multiplier for a storm blend: 1 at blend 0 (exactly — the calm identity),
        /// <see cref="StormRockSettings.MaxResponseMultiplier"/> at blend 1. Scales the transform
        /// rock's gains AND caps together, and a mesh hull's def rock amplitudes, so the whole tuned
        /// shape grows rather than pinning against a calm-sized ceiling.
        /// </summary>
        public static float ResponseMultiplier(float stormBlend01, in StormRockSettings settings)
            => 1f + Mathf.Max(0f, settings.MaxResponseMultiplier - 1f) * Mathf.Clamp01(stormBlend01);

        /// <summary>
        /// The output-smoothing time constant for a storm blend: the calm constant at blend 0
        /// (byte-identical), tightened toward × <see cref="StormRockSettings.StormSmoothingFactor"/>
        /// at blend 1 — velvet is for calm; a storm is violent. Never negative.
        /// </summary>
        public static float TightenedSmoothingSeconds(float calmSeconds, float stormBlend01,
                                                      in StormRockSettings settings)
        {
            float factor = Mathf.Lerp(1f, Mathf.Clamp01(settings.StormSmoothingFactor),
                                      Mathf.Clamp01(stormBlend01));
            return Mathf.Max(0f, calmSeconds) * factor;
        }

        /// <summary>
        /// The per-hull factor the storm attitude extras scale by — the hull's seakeeping RESPONSE
        /// (<c>liveliness / mass factor</c>, the same <see cref="SeakeepingResponse"/> B3 resolves
        /// from <c>BoatHullDef</c>), clamped into [0, <see cref="StormRockSettings.MaxHullResponseScale"/>].
        /// A dory (response 1) takes the tuned read; a heavy trader (response &lt; 1) shrugs; a hull
        /// with no controller/def reads neutral 1 at the CALLER (this is math, not policy).
        /// </summary>
        public static float HullAttitudeScale(in SeakeepingResponse response, in StormRockSettings settings)
            => Mathf.Clamp(response.Response, 0f, Mathf.Max(1f, settings.MaxHullResponseScale));

        /// <summary>
        /// One weight-filter step: chase <paramref name="targetRideMeters"/> (the displaced ride the
        /// surface demands this tick) with a spring-damper whose downward acceleration is capped at
        /// <paramref name="gravity"/> × <see cref="StormRockSettings.MaxDownwardAccelInGs"/>, then
        /// hold the hard band and the settle snap, and return the ride to draw — the raw target
        /// blended toward the weighted chase by <paramref name="engage01"/>.
        ///
        /// <para><b>engage01 ≤ 0 is the exact passthrough</b>: the state is pinned onto the surface
        /// (so engagement later starts from truth, never a stale ease-in) and the target is returned
        /// bit-identically — the negative control the calm band rides on. Callers pass
        /// <c>StormBlend01 × HeaveWeight01</c>.</para>
        ///
        /// <para>fps-independent by sub-step (<see cref="MaxSubStepSeconds"/>), deterministic per
        /// tick sequence, allocation-free. <paramref name="dt"/> 0 (a paused clock) advances
        /// nothing and returns the same blend as the previous instant — the sea freezes, the hull
        /// freezes with it.</para>
        /// </summary>
        /// <param name="state">The per-boat filter state (caller-owned; reset to default on wake).</param>
        /// <param name="targetRideMeters">The surface's displaced ride under the hull (metres).</param>
        /// <param name="dt">Game-time step (seconds, clamped ≥ 0).</param>
        /// <param name="gravity">g (m/s²) — pass the wave field's own
        /// <see cref="WaveFieldSettings.Gravity"/>, the one place that constant lives.</param>
        /// <param name="engage01">How much weight to apply (0 = surface-bolted, 1 = full chase).</param>
        /// <param name="hullResponse">The hull's seakeeping character — response bends the chase
        /// stiffness per <see cref="StormRockSettings.HullStiffnessResponseExponent"/>.</param>
        /// <param name="settings">The B2.5 policy.</param>
        public static float StepHeaveWeight(ref HeaveWeightState state, float targetRideMeters, float dt,
                                            float gravity, float engage01,
                                            in SeakeepingResponse hullResponse,
                                            in StormRockSettings settings)
        {
            float engage = Mathf.Clamp01(engage01);
            if (engage <= 0f)
            {
                // The exact passthrough: output IS the surface, and the state rides pinned to it so
                // the moment the blend rises the chase begins from the truth.
                state.RideMeters = targetRideMeters;
                state.VelocityMetersPerSec = 0f;
                state.Primed = true;
                return targetRideMeters;
            }

            if (!state.Primed)
            {
                state.RideMeters = targetRideMeters;   // wake ON the water, never ease in from zero
                state.VelocityMetersPerSec = 0f;
                state.Primed = true;
            }

            float hullFactor = Mathf.Clamp(hullResponse.Response, MinHullResponseFactor,
                                           Mathf.Max(1f, settings.MaxHullResponseScale));
            float omega = Mathf.Max(0.1f, settings.HeaveChaseStiffnessRadPerSec)
                        * Mathf.Pow(hullFactor, settings.HullStiffnessResponseExponent);
            float zeta = Mathf.Max(0f, settings.HeaveDampingRatio);
            float capAccel = Mathf.Max(0f, gravity) * Mathf.Max(0f, settings.MaxDownwardAccelInGs);

            float x = state.RideMeters;
            float v = state.VelocityMetersPerSec;

            // Semi-implicit Euler with the free-fall cap on the DOWNWARD acceleration only: the
            // spring may snatch the hull up as hard as it likes (buoyancy), but it can never drag
            // it down faster than gravity — crossing a steep crest the hull unweights and FALLS,
            // it is not bolted to the plummeting surface.
            float remaining = Mathf.Max(0f, dt);
            for (int i = 0; i < MaxSubSteps && remaining > 0f; i++)
            {
                float h = Mathf.Min(remaining, MaxSubStepSeconds);
                remaining -= h;
                float accel = omega * omega * (targetRideMeters - x) - 2f * zeta * omega * v;
                if (accel < -capAccel) accel = -capAccel;
                v += accel * h;
                x += v * h;
            }

            // The honesty band: whatever the spring did, the hull is never further from the surface
            // than this — no hovering, no submarining. Velocity INTO the clamp is zeroed so the
            // state does not wind up against the wall.
            float band = Mathf.Max(0.01f, settings.SurfaceBandMeters);
            if (x > targetRideMeters + band) { x = targetRideMeters + band; if (v > 0f) v = 0f; }
            else if (x < targetRideMeters - band) { x = targetRideMeters - band; if (v < 0f) v = 0f; }

            // The settle snap: close and slow lands EXACTLY on the surface — a flattening sea ends
            // in true stillness, never an asymptotic shiver. The velocity threshold is ε·ω (the
            // spring's own velocity scale), not a second tunable.
            float eps = Mathf.Max(0f, settings.SettleEpsilonMeters);
            if (Mathf.Abs(x - targetRideMeters) <= eps && Mathf.Abs(v) <= eps * omega)
            {
                x = targetRideMeters;
                v = 0f;
            }

            state.RideMeters = x;
            state.VelocityMetersPerSec = v;
            return targetRideMeters + (x - targetRideMeters) * engage;
        }
    }
}
