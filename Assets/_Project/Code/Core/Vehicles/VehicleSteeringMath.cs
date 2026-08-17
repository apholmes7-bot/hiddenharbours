using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How a steered road vehicle turns — the pure arithmetic, in one place (ADR 0035).</b>
    /// Engine-light statics on plain floats, so every claim below is pinned by an EditMode test
    /// rather than felt for at the wheel.
    ///
    /// <para><b>The one thing this file exists to fix.</b> The Dually's rig models steering and yaw
    /// as INDEPENDENT axes, and says so in its own sidecar: <i>"STEERING moves the wheels only; YAW
    /// moves the machine. Nothing couples them — a game that turns the wheels without yawing (or the
    /// reverse) will look wrong, and the rig will not stop it."</i> That coupling is gameplay's job,
    /// and it is this file. The wheels are turned by <see cref="AckermannDegrees"/>; the machine is
    /// turned by <see cref="YawRateDegreesPerSecond"/>; both are driven from the SAME steer input
    /// through the SAME published lock angles, so they cannot disagree.</para>
    ///
    /// <para><b>The model is the kinematic bicycle</b>, referenced at the rear axle centre: no slip,
    /// no load transfer, no tyre model. That is the right altitude for a cozy trade RPG whose truck
    /// drives on gravel at walking-to-jogging pace — and it is exactly what the rig's own published
    /// turning radii are computed under ("geometric, at full lock… No slip, no load transfer"), so
    /// the picture and the physics are solving the same problem.</para>
    /// </summary>
    public static class VehicleSteeringMath
    {
        /// <summary>
        /// The two front-wheel angles for a steer input in <c>[−1, +1]</c>, Ackermann-split.
        ///
        /// <para><b>Sign, and it is measured rather than chosen:</b> <c>+1</c> is full LEFT lock —
        /// the nose swings toward rig −x — and the INNER wheel (the left one, in a left turn) takes
        /// the LARGER angle. Verified against the rig 2026-08-17 at every lock: with
        /// <c>steer = 1</c> her left corner rotates exactly +30.0000° and her right exactly
        /// +24.9372°, over all 666 vertices a side, about the published vertical axes. A controller
        /// that fed both wheels one angle would scrub; one that took the sign the other way would
        /// steer her into the ditch she was avoiding.</para>
        ///
        /// <para><b>Linear in the input, because the rig is.</b> The rig computes
        /// <c>inner = |v|·STEER_MAX</c> and derives the outer from it, so the inner angle is linear
        /// in <paramref name="steer"/> and the outer is NOT — it is the Ackermann partner of a linear
        /// inner. Interpolating the outer linearly instead is wrong by up to 1.4° mid-travel
        /// (measured: 13.546° against a linear 12.47° at half lock) and it un-couples the two front
        /// wheels from their shared turn centre everywhere except the stops.</para>
        /// </summary>
        /// <param name="steer">−1 (full right) … +1 (full left). Clamped.</param>
        /// <param name="maxInnerDegrees">The rig's peak inner lock (<c>steer.maxInnerDeg</c>).</param>
        /// <param name="wheelbaseMeters">Axle separation.</param>
        /// <param name="frontTrackMeters">Front track — the Ackermann split's other term.</param>
        /// <param name="leftDegrees">Street-side wheel angle, + = turned left.</param>
        /// <param name="rightDegrees">Curb-side wheel angle, + = turned left.</param>
        public static void AckermannDegrees(float steer, float maxInnerDegrees,
                                            float wheelbaseMeters, float frontTrackMeters,
                                            out float leftDegrees, out float rightDegrees)
        {
            steer = Mathf.Clamp(steer, -1f, 1f);
            if (Mathf.Approximately(steer, 0f) || maxInnerDegrees <= 0f)
            {
                leftDegrees = 0f;
                rightDegrees = 0f;
                return;
            }

            float inner = Mathf.Abs(steer) * maxInnerDegrees;
            float outer = OuterFromInnerDegrees(inner, wheelbaseMeters, frontTrackMeters);

            // + = left lock, and the INNER wheel is the one on the inside of the turn.
            if (steer > 0f) { leftDegrees = inner; rightDegrees = outer; }
            else { leftDegrees = -outer; rightDegrees = -inner; }
        }

        /// <summary>
        /// The outer front wheel's angle that shares a turn centre with an inner wheel at
        /// <paramref name="innerDegrees"/> — the rig's own formula,
        /// <c>outer = atan(1 / (1/tan(inner) + track/wheelbase))</c>.
        ///
        /// <para>Transcribed from <c>vehicleIsoRig.js</c>'s <c>steerAngles</c> and pinned against it:
        /// at the Dually's 30° inner, 4.30 m wheelbase and 1.80 m front track this returns 24.9372°,
        /// which is what the rig poses her outer wheel at and what her contract publishes as
        /// <c>maxOuterDeg</c> (24.94, rounded).</para>
        /// </summary>
        public static float OuterFromInnerDegrees(float innerDegrees, float wheelbaseMeters,
                                                  float frontTrackMeters)
        {
            if (innerDegrees <= 0f || wheelbaseMeters <= 0f) return 0f;
            float innerRad = innerDegrees * Mathf.Deg2Rad;
            float cot = 1f / Mathf.Tan(innerRad) + frontTrackMeters / wheelbaseMeters;
            return Mathf.Atan(1f / cot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The single "bicycle" steer angle δ equivalent to an Ackermann pair — the angle a
        /// one-front-wheel model would need to trace the same circle.
        ///
        /// <para><c>cot(δ) = (cot(inner) + cot(outer)) / 2</c>: the two front wheels sit half a track
        /// either side of the centreline, and their turn-centre offsets differ by exactly the track,
        /// so the centre's offset is their mean. Verified on the Dually: her inner and outer offsets
        /// are 7.4478 m and 9.2478 m, differing by precisely her 1.800 m front track.</para>
        /// </summary>
        public static float BicycleSteerDegrees(float innerDegrees, float outerDegrees)
        {
            if (innerDegrees <= 0f || outerDegrees <= 0f) return 0f;
            float cot = (1f / Mathf.Tan(innerDegrees * Mathf.Deg2Rad) +
                         1f / Mathf.Tan(outerDegrees * Mathf.Deg2Rad)) * 0.5f;
            return Mathf.Atan(1f / cot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The turn radius to the REAR AXLE CENTRE for a bicycle-model steer angle:
        /// <c>R = wheelbase / tan(δ)</c>. Straight ahead (δ = 0) returns
        /// <see cref="float.PositiveInfinity"/> — the honest answer, and one a caller cannot mistake
        /// for a tight turn the way a 0 could be.
        /// </summary>
        public static float TurnRadiusMeters(float bicycleSteerDegrees, float wheelbaseMeters)
        {
            if (Mathf.Approximately(bicycleSteerDegrees, 0f)) return float.PositiveInfinity;
            return wheelbaseMeters / Mathf.Tan(Mathf.Abs(bicycleSteerDegrees) * Mathf.Deg2Rad) *
                   Mathf.Sign(bicycleSteerDegrees);
        }

        /// <summary>
        /// <b>How fast the MACHINE turns</b>, degrees per second, for a speed and a steer input —
        /// the coupling the rig warns is nobody's job but ours.
        ///
        /// <para><c>ω = v · tan(δ) / wheelbase</c>, with δ the Ackermann pair's bicycle equivalent.
        /// Two consequences the shape gets right for free, and both are things a player feels:</para>
        /// <list type="bullet">
        ///   <item><b>A stopped truck does not turn.</b> v = 0 ⇒ ω = 0, however hard the wheel is
        ///   over. A vehicle that pivots on the spot at a standstill is the tell of an arcade model
        ///   bolted onto a machine that has front wheels.</item>
        ///   <item><b>Reversing steers the other way.</b> Negative v gives negative ω for the same
        ///   lock, which is what backing a truck actually does.</item>
        /// </list>
        /// </summary>
        /// <param name="speedMetersPerSecond">Signed speed along the nose; negative = reversing.</param>
        /// <param name="steer">−1 … +1, + = left.</param>
        public static float YawRateDegreesPerSecond(float speedMetersPerSecond, float steer,
                                                    float maxInnerDegrees, float wheelbaseMeters,
                                                    float frontTrackMeters)
        {
            if (wheelbaseMeters <= 0f) return 0f;
            AckermannDegrees(steer, maxInnerDegrees, wheelbaseMeters, frontTrackMeters,
                             out float left, out float right);

            // The INNER wheel is whichever took the larger angle — read off the pair rather than
            // re-deriving it from the sign of `steer`, so the two can never disagree about which
            // wheel is on the inside of the turn.
            float inner = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right));
            float outer = Mathf.Min(Mathf.Abs(left), Mathf.Abs(right));
            float delta = BicycleSteerDegrees(inner, outer);
            if (Mathf.Approximately(delta, 0f)) return 0f;

            float sign = steer >= 0f ? 1f : -1f;
            return sign * speedMetersPerSecond * Mathf.Tan(delta * Mathf.Deg2Rad) / wheelbaseMeters *
                   Mathf.Rad2Deg;
        }

        /// <summary>
        /// <b>How far a wheel has rolled, in REVOLUTIONS</b> — the rig's own unit for its
        /// <c>roll</c>/<c>wFL…wRR</c> axes, cyclic with period 1.
        ///
        /// <para>⚠️ <c>distance / (2π·r)</c>, <b>NOT</b> <c>distance / r</c>. The obvious reading —
        /// "roll is an angle, so it is v/r radians" — is 2π ≈ 6.3× too fast, and 6× is not a
        /// subtlety, it is a wheel that spins like a cartoon. The rig's own contract states the unit
        /// ("units: revolutions") and the arithmetic confirms it: at the Dually's 0.42 m radius one
        /// revolution covers 2.639 m, which is exactly the tread advance her contract publishes
        /// ("the tread advances 2.639 m over a 0.105 m stripe period").</para>
        ///
        /// <para>⚠️ <b>The period is also a measurement trap.</b> <c>roll:1</c> ties with
        /// <c>roll:0</c> exactly — the hub geometry returns after one turn — so a probe that tests
        /// the axis at 1 concludes it is dead. Test at a quarter.</para>
        /// </summary>
        public static float RollRevolutions(float distanceMeters, float wheelRadiusMeters)
        {
            if (wheelRadiusMeters <= 0f) return 0f;
            return distanceMeters / (2f * Mathf.PI * wheelRadiusMeters);
        }

        /// <summary>
        /// The rig's suspension drop at a point <paramref name="y"/> metres along the machine, in
        /// metres — <c>dz(y) = −(susF·travelF)·t − (susR·travelR)·(1−t)</c>,
        /// <c>t = (y − rearAxleY) / wheelbase</c>. Verbatim from <c>build()</c>.
        ///
        /// <para><b>Anything riding the body needs this, and the rig says so:</b> "every polygon in
        /// CAB, CARGO, THRESHOLD, STEP and TOW rides that dz. An occupant or a crate parented to the
        /// body must take the same correction". Note <c>t</c> deliberately EXTRAPOLATES past both
        /// axles — the bumpers pitch further than the travel — so this is not clamped.</para>
        /// </summary>
        public static float SuspensionDropMeters(float y, float susFront, float susRear,
                                                 float travelFrontMeters, float travelRearMeters,
                                                 float rearAxleY, float wheelbaseMeters)
        {
            if (wheelbaseMeters <= 0f) return 0f;
            float t = (y - rearAxleY) / wheelbaseMeters;
            return -(susFront * travelFrontMeters) * t - (susRear * travelRearMeters) * (1f - t);
        }
    }
}
