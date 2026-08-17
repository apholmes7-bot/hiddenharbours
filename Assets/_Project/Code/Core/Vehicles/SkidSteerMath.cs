using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How a SKID-STEERED machine turns — the pure arithmetic, in one place (ADR 0035).</b> The
    /// sibling of <see cref="VehicleSteeringMath"/> for a machine that has no steering axle at all,
    /// and a separate file for the reason that one is separate from the boats': <b>skid steer is not
    /// Ackermann</b>, and pushing it through the truck's math would be wrong in the two places a
    /// player would notice first.
    ///
    /// <para><b>Why it cannot be the truck's model.</b> The Otter's own rig publishes
    /// <c>_excluded.steering_geometry</c>: <i>"no steer axle, no kingpin, no wheel yaw — correct for
    /// the class. Her steering is YAW, the whole machine, tied to the side split"</i>, and her probe
    /// pins it (<c>resolve({}).steer</c> is undefined; there is no such axis to pose). Two
    /// consequences follow immediately, and both are the OPPOSITE of the Dually's:</para>
    /// <list type="bullet">
    ///   <item><b>She turns at a standstill.</b> <c>ω = v·tan(δ)/wheelbase</c> is identically zero at
    ///   <c>v = 0</c> — the truck's headline property, and the tell of an honest front-wheel model.
    ///   For a skid machine the same standstill is a SPIN ON THE SPOT: the two sides counter-rotate
    ///   and the hull turns about its own centre. Feeding her through the bicycle model would give a
    ///   machine that cannot turn until she is already moving, which is not merely wrong, it is
    ///   unusable in a yard.</item>
    ///   <item><b>Reversing does NOT mirror the stick.</b> A truck backs the other way for the same
    ///   lock because <c>ω</c> carries the sign of <c>v</c>. A differential does not: the sides split
    ///   the same way whichever way the machine is travelling, so left stick yaws her left going
    ///   forward AND going astern. That is what backing a skid machine actually does.</item>
    /// </list>
    ///
    /// <para><b>The model is the differential drive</b>, referenced at the hull centre:
    /// <c>v = (vL + vR)/2</c> and <c>ω = (vR − vL)/track</c>. No slip, no track shear, no soil model —
    /// the same altitude the Dually's kinematic bicycle sits at, and the same altitude the rig's own
    /// published <c>YAW.skid_arithmetic</c> is derived under.</para>
    ///
    /// <para><b>Sign, and it is the fleet's one convention.</b> <c>+</c> yaw is LEFT (counter-clockwise,
    /// the nose swinging toward rig −x, and <see cref="Rigidbody2D.angularVelocity"/>'s own positive
    /// sense). A left turn therefore runs the RIGHT side faster, which is what
    /// <see cref="TrackSpeeds"/> produces and what <see cref="YawRateDegreesPerSecond"/> reads back.</para>
    ///
    /// <para>Engine-light statics on plain floats, so every claim above is pinned by an EditMode
    /// assertion rather than felt for at the stick.</para>
    /// </summary>
    public static class SkidSteerMath
    {
        /// <summary>
        /// How far one wheel revolution carries a track: <c>2π·r</c>.
        ///
        /// <para>Verified against the Otter's own sidecar 2026-08-17: at her published
        /// <c>WHEELS.radius_m</c> of 0.32 this is 2.0106 m, and her <c>roll.distance_per_rev_m</c>
        /// publishes 2.011. Same number, rounded — and the same <c>2π·r</c> trap
        /// <see cref="VehicleSteeringMath.RollRevolutions"/> documents: <c>v/r</c> would be 2π ≈ 6.3×
        /// too fast.</para>
        /// </summary>
        public static float DistancePerRevolutionMeters(float wheelRadiusMeters)
            => wheelRadiusMeters > 0f ? 2f * Mathf.PI * wheelRadiusMeters : 0f;

        /// <summary>
        /// <b>The rig's own skid arithmetic, forward</b>: the heading change (degrees) bought by a
        /// SPLIT of <paramref name="splitRevolutions"/> — one side turning that many revolutions
        /// forward while the other turns the same number back.
        ///
        /// <para>Verbatim from the sidecar's <c>YAW.skid_arithmetic.formula</c>,
        /// <c>yaw_rad = 2 · split_rev · distance_per_rev / track</c>, with the distance per
        /// revolution taken from her wheel radius rather than from the sidecar's rounded copy of it
        /// — see <see cref="SplitRevolutionsForYawDegrees"/> for why the rounding matters.</para>
        /// </summary>
        public static float YawDegreesForSplitRevolutions(float splitRevolutions,
                                                          float trackWidthMeters,
                                                          float wheelRadiusMeters)
        {
            if (trackWidthMeters <= 0f) return 0f;
            float circumference = DistancePerRevolutionMeters(wheelRadiusMeters);
            return 2f * splitRevolutions * circumference / trackWidthMeters * Mathf.Rad2Deg;
        }

        /// <summary>
        /// ⭐ <b>The inverse — the number the whole model is solved against.</b> How many revolutions
        /// of SPLIT one turn of <paramref name="yawDegrees"/> costs:
        /// <c>split = yaw_rad · track / (2 · 2π·r)</c>.
        ///
        /// <para><b>DERIVED, not transcribed, and pinned to the rig's published figure</b> — exactly
        /// as <see cref="VehicleMeshDef.FullLockTurnRadiusMeters"/> is derived from the Dually's
        /// published locks rather than from her sidecar's rounded 8.29 m. At the Otter's 1.26 m track
        /// and 0.32 m wheels this returns <b>0.24609375</b> for a 45° turn — the π cancels exactly and
        /// the whole thing collapses to <c>track / (16·r) = 1.26 / 5.12</c> — against her rig's own
        /// <c>G.revPerYaw45 = 0.2461</c>. Same number to the four decimals she publishes it at.</para>
        ///
        /// <para>⚠️ <b>Her sidecar's <c>deg_per_rev_of_split: 182.89</c> is 0.03° off</b> the same
        /// geometry's 182.857 (<c>45 / 0.24609375</c>, i.e. <c>2·2πr/track</c> in degrees) — a
        /// rounding artefact in a hand-transcription, harmless in itself and exactly the kind of thing
        /// that stops being harmless the moment somebody types it into a call site. Nothing here reads
        /// it; the derivation is the source and the published <c>revPerYaw45</c> is the pin.</para>
        /// </summary>
        public static float SplitRevolutionsForYawDegrees(float yawDegrees, float trackWidthMeters,
                                                          float wheelRadiusMeters)
        {
            float circumference = DistancePerRevolutionMeters(wheelRadiusMeters);
            if (circumference <= 0f || trackWidthMeters <= 0f) return 0f;
            return yawDegrees * Mathf.Deg2Rad * trackWidthMeters / (2f * circumference);
        }

        /// <summary>
        /// <b>How fast the MACHINE turns</b>, degrees per second, for a pair of track speeds —
        /// <c>ω = (vR − vL) / track</c>, the differential's defining identity.
        ///
        /// <para>The exact inverse of <see cref="TrackSpeeds"/>, which is what lets the drawn wheels
        /// and the physics yaw be solved from ONE number: the driver's stick sets the yaw, the yaw
        /// sets the pair, and the pair spins the tyres. The rig warns that nothing couples them for
        /// us — <i>"a game that yaws her without splitting the sides (or splits without yawing) will
        /// read wrong; the rig will not stop it"</i> — and this pair of functions is that
        /// coupling.</para>
        /// </summary>
        public static float YawRateDegreesPerSecond(float leftTrackSpeedMetersPerSecond,
                                                    float rightTrackSpeedMetersPerSecond,
                                                    float trackWidthMeters)
        {
            if (trackWidthMeters <= 0f) return 0f;
            return (rightTrackSpeedMetersPerSecond - leftTrackSpeedMetersPerSecond) /
                   trackWidthMeters * Mathf.Rad2Deg;
        }

        /// <summary>The body's speed along the nose for a pair of track speeds — their mean.</summary>
        public static float BodySpeedMetersPerSecond(float leftTrackSpeedMetersPerSecond,
                                                     float rightTrackSpeedMetersPerSecond)
            => 0.5f * (leftTrackSpeedMetersPerSecond + rightTrackSpeedMetersPerSecond);

        /// <summary>
        /// The two track speeds that produce a body speed and a yaw rate together:
        /// <c>vL = v − ω·track/2</c>, <c>vR = v + ω·track/2</c>.
        ///
        /// <para>At <c>v = 0</c> this is a pure counter-rotation — the spin on the spot the rig's own
        /// note describes (<i>"rollL = +t, rollR = −t is a spin on the spot"</i>) — and at
        /// <c>ω = 0</c> both sides run together and she tracks straight.</para>
        /// </summary>
        public static void TrackSpeeds(float bodySpeedMetersPerSecond, float yawRateDegreesPerSecond,
                                       float trackWidthMeters, out float leftMetersPerSecond,
                                       out float rightMetersPerSecond)
        {
            float halfDifferential = 0.5f * Mathf.Max(0f, trackWidthMeters) *
                                     yawRateDegreesPerSecond * Mathf.Deg2Rad;
            leftMetersPerSecond = bodySpeedMetersPerSecond - halfDifferential;
            rightMetersPerSecond = bodySpeedMetersPerSecond + halfDifferential;
        }

        /// <summary>
        /// The hardest she can possibly turn — both sides at full speed in opposite directions,
        /// <c>ω = 2·v_track / track</c>. The ceiling every yaw demand is clamped to, so a tuned spin
        /// rate can never ask for a differential the transmission does not have.
        /// </summary>
        public static float MaxYawRateDegreesPerSecond(float maxTrackSpeedMetersPerSecond,
                                                       float trackWidthMeters)
        {
            if (trackWidthMeters <= 0f) return 0f;
            return 2f * Mathf.Abs(maxTrackSpeedMetersPerSecond) / trackWidthMeters * Mathf.Rad2Deg;
        }

        /// <summary>
        /// <b>What the stick actually asks for</b>: <paramref name="steer"/> in <c>[−1, +1]</c> as a
        /// fraction of the machine's tuned spin rate, bounded by
        /// <see cref="MaxYawRateDegreesPerSecond"/>.
        ///
        /// <para><b>Not scaled by speed, and that is the point.</b> The Dually reduces her STEER at
        /// speed (<c>VehicleDef.SteerFalloffHalfSpeedMetersPerSecond</c>) because the pure geometric
        /// bicycle model turns TIGHTER the faster you go, which is backwards from how a vehicle feels.
        /// A differential has no such pathology — <c>ω</c> is set by the split alone and is already
        /// independent of <c>v</c> — so there is nothing to correct, and correcting it anyway would
        /// invent a falloff the machine does not have. A skid machine's speed limit on turning falls
        /// out of the track ceiling instead: see
        /// <see cref="MaxBodySpeedMetersPerSecond"/>.</para>
        /// </summary>
        public static float YawRateDemandDegreesPerSecond(float steer, float spinRateDegreesPerSecond,
                                                          float maxTrackSpeedMetersPerSecond,
                                                          float trackWidthMeters)
        {
            float demand = Mathf.Clamp(steer, -1f, 1f) * Mathf.Max(0f, spinRateDegreesPerSecond);
            float cap = MaxYawRateDegreesPerSecond(maxTrackSpeedMetersPerSecond, trackWidthMeters);
            return cap > 0f ? Mathf.Clamp(demand, -cap, cap) : demand;
        }

        /// <summary>
        /// ⭐ <b>Turning costs forward speed, and by exactly the differential it spends.</b> The
        /// fastest the BODY may go while yawing at <paramref name="yawRateDegreesPerSecond"/>:
        /// <c>v_track − |ω|·track/2</c>, floored at 0.
        ///
        /// <para>The outer track is the one that saturates — it runs at <c>v + ω·track/2</c> — so the
        /// honest ceiling on <c>v</c> is whatever is left of the transmission after the turn has taken
        /// its half. At the Otter's tuning that is a 0.8 m/s give-back at full stick, which the driver
        /// reads as a machine that leans into a turn and slows a little, exactly like the real class.
        /// Zeroing her instead — or letting the outer track quietly exceed its own ceiling — would be
        /// the two ways to get this wrong, and they look like a stutter and like cheating
        /// respectively.</para>
        /// </summary>
        public static float MaxBodySpeedMetersPerSecond(float yawRateDegreesPerSecond,
                                                        float trackWidthMeters,
                                                        float maxTrackSpeedMetersPerSecond)
        {
            float differential = 0.5f * Mathf.Max(0f, trackWidthMeters) *
                                 Mathf.Abs(yawRateDegreesPerSecond) * Mathf.Deg2Rad;
            return Mathf.Max(0f, Mathf.Abs(maxTrackSpeedMetersPerSecond) - differential);
        }

        /// <summary>
        /// The two sides' roll phase, in REVOLUTIONS, from the two sides' own travelled distances —
        /// the rig's <c>rollL</c> / <c>rollR</c> axes (<c>WHEELS.roll.params</c>, <c>units:
        /// revolutions</c>).
        ///
        /// <para>Deliberately <see cref="VehicleSteeringMath.RollRevolutions"/> twice rather than a
        /// second copy of <c>d/(2π·r)</c>: it is the same arithmetic, it is already pinned by test,
        /// and the one thing that must never happen is the two vehicles' wheels disagreeing about what
        /// a revolution is.</para>
        /// </summary>
        public static void TrackRollRevolutions(float leftDistanceMeters, float rightDistanceMeters,
                                                float wheelRadiusMeters, out float rollLeft,
                                                out float rollRight)
        {
            rollLeft = VehicleSteeringMath.RollRevolutions(leftDistanceMeters, wheelRadiusMeters);
            rollRight = VehicleSteeringMath.RollRevolutions(rightDistanceMeters, wheelRadiusMeters);
        }
    }
}
