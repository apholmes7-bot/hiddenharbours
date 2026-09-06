using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The wind a sailing hull is in, resolved into her own frame — true and apparent, in the
    /// conventions the art rig uses so the physics and the picture cannot describe different boats.
    ///
    /// <para><b>Angles are SIGNED, ±180, positive = the wind is over the STARBOARD bow</b> (so the
    /// sails are set to port). That is <c>sloopIsoRig.sailPose</c>'s own convention, verbatim:
    /// <c>side = awa &gt;= 0 ? -1 : 1</c>. 0° is dead into the wind; ±180° is dead downwind.</para>
    ///
    /// <para><b>Speeds are KNOTS</b>, because that is the unit the polar and the whole sailing sidecar
    /// are stated in, and converting once at the boundary beats converting at every read.</para>
    /// </summary>
    public readonly struct SailWind
    {
        /// <summary>True wind angle off the bow, signed, ±180. Independent of boat speed.</summary>
        public readonly float TrueAngleDeg;

        /// <summary>True wind speed, knots.</summary>
        public readonly float TrueKn;

        /// <summary>Apparent wind angle off the bow, signed, ±180 — what the SAILS see.</summary>
        public readonly float ApparentAngleDeg;

        /// <summary>Apparent wind speed, knots.</summary>
        public readonly float ApparentKn;

        public SailWind(float trueAngleDeg, float trueKn, float apparentAngleDeg, float apparentKn)
        {
            TrueAngleDeg = trueAngleDeg;
            TrueKn = trueKn;
            ApparentAngleDeg = apparentAngleDeg;
            ApparentKn = apparentKn;
        }
    }

    /// <summary>
    /// <b>The first hull whose speed IS the wind</b> (P1). Pure, static, engine-light maths: true wind
    /// and the boat's own motion in, a target speed and the thrust that holds it out. No clock read, no
    /// RNG, no state — so it is EditMode-testable and cannot drift between the controller and a test.
    ///
    /// <para><b>⚠️ The wind vector points DOWNWIND.</b> <c>EnvironmentSample.WindVector</c> is the
    /// direction the wind BLOWS — <c>BoatController</c> shoves the hull along it, and
    /// <c>TelltaleMath</c> streams the burgee along it. A wind ANGLE is measured to where the wind
    /// comes FROM, so every angle here is taken off <c>-wind</c>. Getting that backwards puts the boat
    /// on the opposite tack and is invisible in a symmetric test — which is why the guard suite has a
    /// sabotage arm that rotates the wind 180° and requires the speed guard to FAIL.</para>
    ///
    /// <para><b>Apparent wind comes from Core's existing maths</b>
    /// (<c>TelltaleMath.ApparentWind</c> = <c>trueWind − boatVelocity</c>). There is one wind in this
    /// game and this file does not add a second.</para>
    /// </summary>
    public static class SailDrive
    {
        /// <summary>One knot in metres per second.</summary>
        public const float MetresPerSecondPerKnot = 0.5144444f;

        public static float ToKnots(float metresPerSecond) => metresPerSecond / MetresPerSecondPerKnot;
        public static float ToMetresPerSecond(float knots) => knots * MetresPerSecondPerKnot;

        /// <summary>
        /// Resolve the wind into the boat's frame.
        /// </summary>
        /// <param name="trueWindMs">The sim's wind vector, m/s, pointing DOWNWIND.</param>
        /// <param name="bowForward">The boat's heading (need not be normalised).</param>
        /// <param name="boatVelocityMs">Course-over-ground velocity, m/s.</param>
        public static SailWind ResolveWind(Vector2 trueWindMs, Vector2 bowForward, Vector2 boatVelocityMs)
        {
            float bow = BoatKinematics.BearingDegrees(bowForward);

            // Where the wind comes FROM is the bearing of -wind. A zero wind has no direction; the
            // angle is then meaningless and the speed is what gates every consumer (becalmed).
            float trueFrom = BoatKinematics.BearingDegrees(-trueWindMs);
            Vector2 apparentMs = TelltaleMath.ApparentWind(trueWindMs, boatVelocityMs);
            float apparentFrom = BoatKinematics.BearingDegrees(-apparentMs);

            return new SailWind(
                BoatKinematics.RelativeBearingDegrees(bow, trueFrom), ToKnots(trueWindMs.magnitude),
                BoatKinematics.RelativeBearingDegrees(bow, apparentFrom), ToKnots(apparentMs.magnitude));
        }

        /// <summary>
        /// Boat speed the polar promises at this true wind, knots — bilinear over the grid, clamped at
        /// its edges, and ZERO inside the no-go.
        ///
        /// <para><b>⚠️ The no-go is gated on the TRUE angle, never the apparent one, and that is not a
        /// preference.</b> Apparent wind angle is a function of boat speed, so a gate on it is a gate
        /// on its own output: at twa 40 in 12 kn the sloop 88 sits at awa 23.6° when moving and 40°
        /// when stopped, so an apparent-angle gate stops her, which reopens the angle, which starts
        /// her — a limit cycle, at the exact angle a player pinching to windward sails. The true angle
        /// does not move with her speed, so it can be a law. The apparent band stays what the RIG uses
        /// to decide whether to draw flogging sails.</para>
        ///
        /// <para>Off the grid's edges the value is CLAMPED, not extrapolated: a polar is a measurement
        /// over a stated range and a linear run-out past 25 kn of breeze would invent planing the model
        /// explicitly says it does not have (<c>POLAR_REFERENCE.surfing_planing</c>).</para>
        /// </summary>
        public static float TargetSpeedKn(SailPolarDef polar, float trueAngleDeg, float trueWindKn,
                                          float noGoTrueDeg)
        {
            if (polar == null || !polar.IsUsable()) return 0f;
            if (trueWindKn <= 0f) return 0f;

            float twa = Mathf.Abs(Mathf.Repeat(trueAngleDeg + 180f, 360f) - 180f);
            if (twa < noGoTrueDeg) return 0f;

            return Sample(polar.TrueWindAngleDeg, polar.TrueWindKn, polar.BoatSpeedKn, twa, trueWindKn);
        }

        /// <summary>Apparent wind angle the polar itself recorded at this cell, degrees (magnitude) —
        /// the art side's own number, so a consumer can ask "would the sprite flog here?" without
        /// re-deriving the true→apparent glue and disagreeing with the file by rounding.</summary>
        public static float PolarApparentAngleDeg(SailPolarDef polar, float trueAngleDeg, float trueWindKn)
        {
            if (polar == null || !polar.IsUsable()) return 0f;
            if (polar.ApparentWindAngleDeg == null || polar.ApparentWindAngleDeg.Length == 0) return 0f;
            float twa = Mathf.Abs(Mathf.Repeat(trueAngleDeg + 180f, 360f) - 180f);
            return Sample(polar.TrueWindAngleDeg, polar.TrueWindKn, polar.ApparentWindAngleDeg, twa, trueWindKn);
        }

        /// <summary>
        /// <b>The thrust that holds a target speed — and the reason there is no ramp anywhere here.</b>
        ///
        /// <para>Ask for the force that balances the hull's resistance AT the target and she arrives
        /// there on HER OWN time constant (τ = m/k) — which is what a boat gathering way actually feels
        /// like. A ramp on top of that would be a second lag in series with a lag the hull already has,
        /// and this project has already paid for that lesson once: 190 m of glide that no tuning fixes
        /// (<c>boat-hull-time-constant-forbids-throttle-ramps</c>). Set the target; let her settle.</para>
        ///
        /// <para><b>⚠️⚠️ <paramref name="linearResistance"/> is the TOTAL, not <c>ForwardDrag</c>.</b>
        /// A hull is slowed by two velocity-proportional terms, not one: the hull drag
        /// <c>BoatController</c> applies itself (<c>ForwardDrag × ForceFeelScale × v</c>) AND the
        /// <c>Rigidbody2D.linearDamping</c> the body carries (0.2, against a mass of
        /// <c>MassKg / 100</c>). Only the first is a hull field, which is exactly why the second gets
        /// forgotten — and forgetting it does not scale a boat's speed a little, it scales it by
        /// <c>ForwardDrag / (ForwardDrag + 0.2·MassKg)</c>, which for the sloop 30 is <b>17 %</b>. She
        /// would make 0.8 kn on a broad reach her own polar puts at 4.7 and read as becalmed in a
        /// working breeze. Use <see cref="LinearResistance"/> and let the caller read the body it
        /// actually has, rather than re-deriving Unity's damping here.</para>
        /// </summary>
        public static float ThrustFor(float targetSpeedMs, float linearResistance)
            => targetSpeedMs <= 0f || linearResistance <= 0f ? 0f : targetSpeedMs * linearResistance;

        /// <summary>
        /// <b>Everything that resists her, as one coefficient in the hull's own design units</b> — what
        /// <see cref="ThrustFor"/> must be given.
        ///
        /// <para>The body's damping is a force <c>damping × mass × v</c> in NEWTONS, while
        /// <paramref name="forwardDrag"/> is a design-unit number the controller multiplies by
        /// <paramref name="forceFeelScale"/> before applying. Dividing the first by that scale puts both
        /// on one footing, so the sum is the number the thrust ask is stated in.</para>
        ///
        /// <para>Read <paramref name="bodyMass"/> and <paramref name="linearDamping"/> off the live
        /// <c>Rigidbody2D</c> rather than recomputing them from the def: the mass rule
        /// (<c>MassKg / 100</c>) and the damping constant live in the controller, and a drive that
        /// re-derives them silently disagrees with the boat the moment either is tuned.</para>
        /// </summary>
        public static float LinearResistance(float forwardDrag, float bodyMass, float linearDamping,
                                             float forceFeelScale)
        {
            if (forceFeelScale <= 0f) return Mathf.Max(0f, forwardDrag);
            return Mathf.Max(0f, forwardDrag) + Mathf.Max(0f, linearDamping * bodyMass) / forceFeelScale;
        }

        /// <summary>
        /// <b>AUTO_TRIM — the builder pages' law, verbatim.</b> The sheets that hold 18° of attack on
        /// the main and 16° on the headsail until the 86°/85° limits cap them.
        ///
        /// <para>This is the DEFAULT so that a player who never touches a sheet still sails
        /// (<c>AUTO_TRIM</c> in the sailing sidecar). ⚠️ Measured consequence, worth knowing before
        /// tuning feel: under this law the rig NEVER draws luffing sails outside irons — it holds the
        /// angle of attack well clear of the 8° luff threshold, so the sprite reads
        /// drawing → stalled → in irons and nothing between.</para>
        /// </summary>
        public static void AutoTrim(float apparentAngleDeg, out float main, out float jib)
        {
            float a = Mathf.Abs(Mathf.Repeat(apparentAngleDeg + 180f, 360f) - 180f);
            float boomOffset = Mathf.Clamp(a - 18f, 0f, 86f);
            float jibOffset = Mathf.Clamp(a - 16f, 0f, 85f);
            main = Mathf.Clamp01(1f - (boomOffset - 4f) / 82f);
            jib = Mathf.Clamp01(1f - (jibOffset - 9f) / 76f);
        }

        /// <summary>
        /// ⭐⭐ <b>IN IRONS — the ONE publisher (owner ruling 2026-09-06).</b> Is she to be DRAWN with
        /// her sails flogging? The physics and the picture must never answer this separately, so they
        /// do not each own a threshold: this is the union of both, and it is the only thing anything
        /// should ask.
        ///
        /// <para><b>Why a union and not the sprite's band alone.</b> The rig flogs below
        /// <paramref name="spriteIronsApparentDeg"/> of APPARENT wind (25° on both sloops). Apparent
        /// angle is a function of boat speed — and inside the no-go the drive takes her speed to zero,
        /// at which point the apparent wind IS the true wind and <c>awa == twa</c>. So an
        /// apparent-only test draws a boat pinching at twa 30° with her sails DRAWING while she sits
        /// dead in the water: measured, not hypothetical — 25° &lt; twa &lt; 45° is exactly the band
        /// where the no-go has stopped her but her own apparent angle is still outside the rig's
        /// flogging threshold. Taking the no-go as well closes it: if the drive says she makes no way,
        /// she is drawn making no way.</para>
        ///
        /// <para><b>The other direction is already closed by the no-go's VALUE, not by this code.</b>
        /// A boat the polar sails while the rig flogs her would be just as wrong, and
        /// <c>NoGoTrueWindDeg = 45</c> was chosen because it leaves ZERO such cells on both hulls'
        /// shipped grids (at 40 there are 1 and 6; at 35, 4 and 14). At the boundary itself — twa 45 in
        /// 12 kn at the polar's 4.97 kn — the apparent angle is <b>32.2°</b>, comfortably outside the
        /// 25° band, so the boat that is only just sailing is drawn sailing.</para>
        ///
        /// <para>⚠️ <paramref name="apparentAngleDeg"/> must come from the drive's own
        /// <see cref="ResolveWind"/> (i.e. Core's <c>TelltaleMath.ApparentWind</c>), never from a
        /// second derivation of the apparent wind, and <paramref name="noGoTrueDeg"/> is a TRUE-wind
        /// angle — feeding the sprite's 25° in as a true-angle threshold is the sabotage the guard
        /// suite pins.</para>
        /// </summary>
        public static bool IsInIrons(float trueAngleDeg, float apparentAngleDeg, float noGoTrueDeg,
                                     float spriteIronsApparentDeg)
        {
            float twa = Mathf.Abs(Mathf.Repeat(trueAngleDeg + 180f, 360f) - 180f);
            float awa = Mathf.Abs(Mathf.Repeat(apparentAngleDeg + 180f, 360f) - 180f);
            return twa < noGoTrueDeg || awa < spriteIronsApparentDeg;
        }

        // ---- the grid ---------------------------------------------------------------------------

        /// <summary>
        /// Bilinear sample of a row-major (angle × wind) table, clamped at both edges. Shared by every
        /// column so the speed, the apparent angle and anything added later cannot disagree about
        /// where a cell is.
        /// </summary>
        static float Sample(float[] angles, float[] winds, float[] cells, float angle, float wind)
        {
            Bracket(angles, angle, out int a0, out int a1, out float at);
            Bracket(winds, wind, out int w0, out int w1, out float wt);

            int n = winds.Length;
            float lo = Mathf.Lerp(cells[a0 * n + w0], cells[a0 * n + w1], wt);
            float hi = Mathf.Lerp(cells[a1 * n + w0], cells[a1 * n + w1], wt);
            return Mathf.Lerp(lo, hi, at);
        }

        /// <summary>Which two ascending axis entries a value falls between, and how far. Outside the
        /// axis both indices collapse onto the edge and <paramref name="t"/> is 0 — a clamp, not an
        /// extrapolation.</summary>
        static void Bracket(float[] axis, float value, out int i0, out int i1, out float t)
        {
            if (value <= axis[0]) { i0 = i1 = 0; t = 0f; return; }
            int last = axis.Length - 1;
            if (value >= axis[last]) { i0 = i1 = last; t = 0f; return; }

            for (int i = 0; i < last; i++)
            {
                if (value > axis[i + 1]) continue;
                i0 = i; i1 = i + 1;
                float span = axis[i + 1] - axis[i];
                t = span > 0f ? (value - axis[i]) / span : 0f;
                return;
            }
            i0 = i1 = last; t = 0f;
        }
    }
}
