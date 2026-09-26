using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// One hull's trim character, resolved from her own <see cref="BoatHullDef"/> and the world
    /// policy (<see cref="HullTrimSettings"/>). A value, so the tick does arithmetic only. The
    /// constructor applies the guards (nothing negative, the planing drop never more than the rise,
    /// the planing Froude always above the hump's, a length that cannot divide by zero); the
    /// fallbacks from a hull's 0 to the policy's defaults are <see cref="HullTrimMath.Resolve"/>'s.
    /// </summary>
    public readonly struct HullTrimProfile
    {
        /// <summary>Bow-up at the hump, degrees (<see cref="BoatHullDef.TrimHumpDegrees"/>).</summary>
        public readonly float HumpDegrees;

        /// <summary>What a planing hull gives back over the hump, degrees, ≤ the rise.</summary>
        public readonly float PlaningDropDegrees;

        /// <summary>Bow-up per m/s² of acceleration along the keel.</summary>
        public readonly float AccelDegreesPerMps2;

        /// <summary>Bow-down per m/s² of deceleration, at full way.</summary>
        public readonly float DecelDegreesPerMps2;

        /// <summary>The most the bow may rise, degrees (≥ 0).</summary>
        public readonly float MaxBowUpDegrees;

        /// <summary>The most the bow may dip, degrees (≥ 0).</summary>
        public readonly float MaxBowDownDegrees;

        /// <summary>The drawn trim's lag, seconds (0 = none).</summary>
        public readonly float ResponseSeconds;

        /// <summary>The Froude number of the hump.</summary>
        public readonly float HumpFroude;

        /// <summary>The Froude number by which a planing hull has settled; always above the hump's.</summary>
        public readonly float PlaningFroude;

        /// <summary>Her length (m) — the length in her Froude number.</summary>
        public readonly float LengthMeters;

        public HullTrimProfile(float humpDegrees, float planingDropDegrees, float accelDegreesPerMps2,
                               float decelDegreesPerMps2, float maxBowUpDegrees, float maxBowDownDegrees,
                               float responseSeconds, float humpFroude, float planingFroude,
                               float lengthMeters)
        {
            HumpDegrees = Mathf.Max(0f, humpDegrees);
            PlaningDropDegrees = Mathf.Clamp(planingDropDegrees, 0f, HumpDegrees);
            AccelDegreesPerMps2 = Mathf.Max(0f, accelDegreesPerMps2);
            DecelDegreesPerMps2 = Mathf.Max(0f, decelDegreesPerMps2);
            MaxBowUpDegrees = Mathf.Max(0f, maxBowUpDegrees);
            MaxBowDownDegrees = Mathf.Max(0f, maxBowDownDegrees);
            ResponseSeconds = Mathf.Max(0f, responseSeconds);
            HumpFroude = Mathf.Max(HullTrimMath.MinHumpFroude, humpFroude);
            PlaningFroude = Mathf.Max(HumpFroude + HullTrimMath.MinPlaningFroudeSpan, planingFroude);
            LengthMeters = Mathf.Max(HullTrimMath.MinLengthMeters, lengthMeters);
        }

        /// <summary>True when nothing about this hull asks her to trim: her target is exactly 0 at
        /// every speed and every acceleration, and <see cref="HullTrimMath.TargetDegrees"/> returns
        /// that 0 without computing anything.</summary>
        public bool IsNeutral => HumpDegrees == 0f && PlaningDropDegrees == 0f
                                 && AccelDegreesPerMps2 == 0f && DecelDegreesPerMps2 == 0f;

        /// <summary>The profile of a hull that never trims — what a missing hull, a disabled policy
        /// or an all-zero hull resolves to.</summary>
        public static readonly HullTrimProfile Level = default;
    }

    /// <summary>
    /// <b>SHE TRIMS TO HER SPEED</b> — the pure, headless-testable core of the hull trim (owner,
    /// 2026-09-21: <i>"Also i want trim added to the boats depending on speed and deacceleration"</i>).
    /// Design: <c>docs/design/boats-and-navigation.md</c> §2.7.3.
    ///
    /// <para><b>The target is two terms, clamped to her limits.</b>
    /// <list type="number">
    /// <item><b>Steady (speed):</b> <c>hump · S(Fn / FnHump) − drop · S((Fn − FnHump) / (FnPlane − FnHump))</c>,
    /// with <c>S</c> the smoothstep and <c>Fn = u / √(g L)</c> her Froude number. Level at rest, the
    /// full rise at hull speed, and a planing hull gives some of it back once she is over. The speed it
    /// reads is capped at the speed her drive can HOLD (<see cref="HeldSpeed"/>): the bow-up is the
    /// bow wave her drive keeps her climbing, so when the drive is cut the rise goes with it and the
    /// dip below is not masked by a hump she is no longer pushing up.</item>
    /// <item><b>Dynamic (acceleration):</b> a squat under power (<c>+accel gain · a</c>) and a dip as
    /// she slows (<c>decel gain · a · S(Fn / FnHump)</c>, a &lt; 0). The dip is her stern wave
    /// catching her up, so it scales with the way she carried and fades to nothing as she stops:
    /// a boat creeping into her berth does not nod.</item>
    /// </list></para>
    ///
    /// <para><b>The signals, and what they leave out.</b> <c>u</c> is her speed along the keel
    /// THROUGH THE WATER (her velocity less the current), so a current under her does not trim her.
    /// <c>a</c> is the acceleration along the keel that her OWN drive and her own drag produce — the
    /// thrust, the hull drag, the oar brace, the grounded and shallows holds and the body's damping
    /// — computed from the forces as the controller applied them, not by differencing her velocity.
    /// So the wind, the sea's shove and the seakeeping damping (the wave forces), and anything else
    /// that pushes her, are not read as drive or braking; the wave pitch stays the rock channel's
    /// alone.</para>
    ///
    /// <para><b>The filter is the drawer's lag.</b> The target moves in steps when the throttle
    /// does; <see cref="Step"/> eases the drawn trim after it with one exact exponential per
    /// frame, so a variable frame time converges on the same curve and a paused game (dt 0) holds.
    /// Nothing here keeps hidden state or draws a random number (rule 5), and nothing is saved.</para>
    ///
    /// <para><b>Astern she sits level.</b> The Froude number reads only headway, so the steady term
    /// and the dip are 0 going astern, and every term passes through 0 continuously where she stops
    /// or reverses: no sign spike on a crash stop or at the zero crossing. The squat alone is not
    /// gated on headway — its sign is the along-keel acceleration's — so a boat losing her sternway
    /// lifts her bow a little as she slows, exactly as a boat gathering headway does.</para>
    ///
    /// <para>A target that is not a number (a force or a velocity that already is one) asks for
    /// level, so a physics blow-up never reaches the picture as a NaN pitch.</para>
    /// </summary>
    public static class HullTrimMath
    {
        /// <summary>Standard gravity (m/s²) — the g in the Froude number. A physical constant.</summary>
        public const float Gravity = 9.81f;

        /// <summary>Guard floor on the hump's Froude number, so a zeroed policy cannot divide by
        /// zero. A guard, not a tunable.</summary>
        public const float MinHumpFroude = 1e-3f;

        /// <summary>Guard floor on the gap between the hump's and the planing Froude numbers.</summary>
        public const float MinPlaningFroudeSpan = 1e-3f;

        /// <summary>Guard floor on a hull's length (m) in her Froude number.</summary>
        public const float MinLengthMeters = 0.1f;

        /// <summary>
        /// Her trim character: her own <c>Trim*</c> values, with each 0 limit or response taken
        /// from the policy's default. A null hull, or the policy switched off (which is how a
        /// <c>GameConfig</c> serialized before trim existed reads), is <see cref="HullTrimProfile.Level"/>.
        /// </summary>
        public static HullTrimProfile Resolve(BoatHullDef hull, in HullTrimSettings policy)
        {
            if (hull == null || !policy.Enabled) return HullTrimProfile.Level;
            return new HullTrimProfile(
                hull.TrimHumpDegrees, hull.TrimPlaningDropDegrees,
                hull.TrimAccelDegreesPerMps2, hull.TrimDecelDegreesPerMps2,
                hull.TrimMaxBowUpDegrees > 0f ? hull.TrimMaxBowUpDegrees : policy.DefaultMaxBowUpDegrees,
                hull.TrimMaxBowDownDegrees > 0f ? hull.TrimMaxBowDownDegrees : policy.DefaultMaxBowDownDegrees,
                hull.TrimResponseSeconds > 0f ? hull.TrimResponseSeconds : policy.DefaultResponseSeconds,
                policy.HumpFroude, policy.PlaningFroude, hull.LengthMeters);
        }

        /// <summary>Her Froude number for headway <paramref name="speed"/> (m/s): <c>u / √(g L)</c>.
        /// Astern (a negative speed) reads 0.</summary>
        public static float Froude(float speed, float lengthMeters)
            => Mathf.Max(0f, speed) / Mathf.Sqrt(Gravity * Mathf.Max(MinLengthMeters, lengthMeters));

        /// <summary>The smoothstep <c>x²(3 − 2x)</c> on [0, 1], clamped outside it: level slope at
        /// both ends, so nothing in the curve has a corner.</summary>
        public static float Smooth01(float x)
        {
            if (!(x > 0f)) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }

        /// <summary>How far up her hump she is at headway <paramref name="speed"/>, 0 (at rest or
        /// astern) to 1 (at the hump or beyond).</summary>
        public static float HumpFraction(in HullTrimProfile profile, float speed)
            => Smooth01(Froude(speed, profile.LengthMeters) / profile.HumpFroude);

        /// <summary>How far over her hump she is, 0 (at or below it) to 1 (settled at the planing
        /// Froude number or beyond).</summary>
        public static float PlaningFraction(in HullTrimProfile profile, float speed)
            => Smooth01((Froude(speed, profile.LengthMeters) - profile.HumpFroude)
                        / (profile.PlaningFroude - profile.HumpFroude));

        /// <summary>The steady bow angle (degrees, + = up) at headway <paramref name="speed"/> through
        /// the water: the rise to the hump, less a planing hull's settle beyond it.</summary>
        public static float SteadyDegrees(in HullTrimProfile profile, float speed)
            => profile.HumpDegrees * HumpFraction(profile, speed)
               - profile.PlaningDropDegrees * PlaningFraction(profile, speed);

        /// <summary>The transient (degrees, + = up) for the drive's acceleration along the keel
        /// <paramref name="acceleration"/> (m/s²): the squat under power, or the dip as she slows —
        /// the dip weighted by the way she carries (<see cref="HumpFraction"/> of
        /// <paramref name="waterSpeed"/>). Exactly 0 at zero acceleration from either side.</summary>
        public static float DynamicDegrees(in HullTrimProfile profile, float waterSpeed, float acceleration)
        {
            if (acceleration >= 0f) return profile.AccelDegreesPerMps2 * acceleration;
            return profile.DecelDegreesPerMps2 * acceleration * HumpFraction(profile, waterSpeed);
        }

        /// <summary>
        /// ⭐ <b>The bow angle she is asked for</b> (degrees, + = up), clamped to her limits:
        /// <see cref="SteadyDegrees"/> at her water speed capped by the speed her drive holds, plus
        /// <see cref="DynamicDegrees"/>. A neutral profile returns exactly 0, and so does a water
        /// speed that is not a number, or any input that makes the sum not one.
        /// </summary>
        public static float TargetDegrees(in HullTrimProfile profile, float waterSpeed, float heldSpeed,
                                          float acceleration)
        {
            // A NaN speed is checked on its own: Mathf.Min would hand the held speed through in its
            // place and the sum would come out a number — the rise at a speed she is not making.
            if (profile.IsNeutral || float.IsNaN(waterSpeed)) return 0f;
            float raw = SteadyDegrees(profile, Mathf.Min(waterSpeed, heldSpeed))
                        + DynamicDegrees(profile, waterSpeed, acceleration);
            if (float.IsNaN(raw)) return 0f;
            return Mathf.Clamp(raw, -profile.MaxBowDownDegrees, profile.MaxBowUpDegrees);
        }

        /// <summary>
        /// The drawn trim eased one frame toward its target: <c>current + (target − current) ·
        /// (1 − e^(−dt/τ))</c>. Exact for any frame time, so two half frames land where one whole
        /// frame does. A frame that did not advance (dt ≤ 0 — paused — or not a number) holds;
        /// τ ≤ 0 follows the target exactly.
        /// </summary>
        public static float Step(float current, float target, float dt, float responseSeconds)
        {
            if (!(dt > 0f)) return current;
            if (!(responseSeconds > 0f)) return target;
            return current + (target - current) * (1f - Mathf.Exp(-dt / responseSeconds));
        }

        /// <summary>
        /// Her acceleration along the keel (m/s²) from the along-keel force her drive and her drag
        /// applied (<paramref name="alongForce"/>, in the body's own force units) on a body of
        /// <paramref name="bodyMass"/> — less the body's own damping, <c>c · v</c>, which the physics
        /// engine applies against her velocity OVER THE GROUND. The continuous-time acceleration;
        /// 0 for a body with no mass.
        /// </summary>
        public static float DriveAcceleration(float alongForce, float bodyMass, float linearDamping,
                                              float groundSpeedAlong)
        {
            if (!(bodyMass > 0f)) return 0f;
            return alongForce / bodyMass - Mathf.Max(0f, linearDamping) * groundSpeedAlong;
        }

        /// <summary>
        /// The headway her drive can hold (m/s): the drive force over everything that resists it
        /// (<c>SailDrive.LinearResistance</c> in the same force units). No drive ahead holds no
        /// speed (0); a drive against no resistance holds any speed (+∞).
        /// </summary>
        public static float HeldSpeed(float driveForce, float resistance)
        {
            if (!(driveForce > 0f)) return 0f;
            return resistance > 0f ? driveForce / resistance : float.PositiveInfinity;
        }
    }
}
