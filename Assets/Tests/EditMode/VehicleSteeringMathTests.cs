using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The Dually's steering geometry, pinned against the rig that draws her (ADR 0035).</b>
    ///
    /// <para>Every number below was MEASURED out of <c>vehicleIsoRig.js</c> in the repo's own V8 on
    /// 2026-08-17 (and the rig-side halves are pinned independently by
    /// <c>DuallyIsoKitProbeTests</c>). This fixture exists because the rig models the wheels and the
    /// machine as INDEPENDENT axes and says so in its own sidecar — <i>"Nothing couples them — a
    /// game that turns the wheels without yawing (or the reverse) will look wrong, and the rig will
    /// not stop it."</i> The coupling is ours, so it is ours to prove.</para>
    /// </summary>
    public class VehicleSteeringMathTests
    {
        // The Dually's chassis, as the rig publishes it. Not tunables — art.
        const float Wheelbase = 4.30f;      // G.axF − G.axR
        const float FrontTrack = 1.80f;     // G.frontWX × 2
        const float WheelRadius = 0.42f;    // G.wheelR
        const float MaxInner = 30f;         // STEER_MAX

        // =============================================================================================
        //  ACKERMANN — the two front wheels share one turn centre
        // =============================================================================================

        /// <summary>
        /// The outer wheel's angle is the rig's own <c>steerAngles</c> arithmetic, not an
        /// approximation of it. 24.9372° at full lock is what the rig poses her at and what her
        /// contract rounds to 24.94.
        /// </summary>
        [Test]
        public void TheOuterWheelAngleMatchesTheRigsOwnAckermannFormula()
        {
            Assert.That(VehicleSteeringMath.OuterFromInnerDegrees(MaxInner, Wheelbase, FrontTrack),
                Is.EqualTo(24.9372f).Within(1e-3f),
                "the outer lock moved. Both front wheels must stay tangent to ONE turn centre; a " +
                "wrong outer angle scrubs the tyre and, worse, means the picture and the yaw rate " +
                "are solving different geometry.");
        }

        /// <summary>
        /// ⭐ <b>The Ackermann identity, checked the way it is actually defined:</b> the two front
        /// wheels' turn-centre offsets must differ by EXACTLY the front track. Measured against the
        /// rig 2026-08-17 — 7.4478 m and 9.2478 m, differing by 1.8000.
        ///
        /// <para>Stronger than asserting the two angles, because it is the property the angles exist
        /// to satisfy: a formula change that kept both angles plausible but broke the shared centre
        /// would pass an angle check and fail this one.</para>
        /// </summary>
        [Test]
        public void BothFrontWheelsShareOneTurnCentre_AtEveryLock()
        {
            for (float steer = 0.1f; steer <= 1.0001f; steer += 0.1f)
            {
                VehicleSteeringMath.AckermannDegrees(steer, MaxInner, Wheelbase, FrontTrack,
                                                     out float left, out float right);
                float inner = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right));
                float outer = Mathf.Min(Mathf.Abs(left), Mathf.Abs(right));

                float rInner = Wheelbase / Mathf.Tan(inner * Mathf.Deg2Rad);
                float rOuter = Wheelbase / Mathf.Tan(outer * Mathf.Deg2Rad);

                Assert.That(rOuter - rInner, Is.EqualTo(FrontTrack).Within(1e-3f),
                    $"at steer {steer:F1} the two front wheels do NOT share a turn centre: their " +
                    $"offsets differ by {rOuter - rInner:F4} m, but they sit {FrontTrack} m apart, " +
                    "so the difference must be exactly the track.");
            }
        }

        /// <summary>
        /// ⚠️ <b>+1 is full LEFT, and the INNER wheel turns FURTHER.</b> Measured against the rig at
        /// every lock: with <c>steer = 1</c> her left corner rotates exactly +30.0000° and her right
        /// exactly +24.9372°, over all 666 vertices a side.
        ///
        /// <para>A flipped sign here steers the truck into the ditch she was avoiding, and nothing
        /// else in the stack would notice — the wheels and the machine would agree with each other
        /// while both being wrong.</para>
        /// </summary>
        [Test]
        public void PlusOneIsFullLeftLock_AndTheInsideWheelTurnsFurther()
        {
            VehicleSteeringMath.AckermannDegrees(1f, MaxInner, Wheelbase, FrontTrack,
                                                 out float left, out float right);
            Assert.That(left, Is.EqualTo(30f).Within(1e-4f), "left lock: the LEFT wheel is the inner one.");
            Assert.That(right, Is.EqualTo(24.9372f).Within(1e-3f));
            Assert.That(Mathf.Abs(left), Is.GreaterThan(Mathf.Abs(right)),
                "in a LEFT turn the LEFT wheel is on the inside and must take the larger angle.");

            VehicleSteeringMath.AckermannDegrees(-1f, MaxInner, Wheelbase, FrontTrack,
                                                 out float rl, out float rr);
            Assert.That(rr, Is.EqualTo(-30f).Within(1e-4f), "right lock mirrors: the RIGHT wheel is inner.");
            Assert.That(rl, Is.EqualTo(-24.9372f).Within(1e-3f));
        }

        /// <summary>Centred means centred — a wheel that never quite reaches zero turns the truck
        /// forever on a straight road, and the player has no input left to correct it with.</summary>
        [Test]
        public void ZeroSteerTurnsNeitherWheel()
        {
            VehicleSteeringMath.AckermannDegrees(0f, MaxInner, Wheelbase, FrontTrack,
                                                 out float left, out float right);
            Assert.That(left, Is.EqualTo(0f));
            Assert.That(right, Is.EqualTo(0f));
        }

        // =============================================================================================
        //  THE COUPLING — wheels turned, machine turning
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A stopped truck does not turn, however hard the wheel is over.</b> The tell of an
        /// arcade model bolted onto a machine that has front wheels is that she pivots on the spot;
        /// this is the assertion that says she does not.
        /// </summary>
        [Test]
        public void AStoppedVehicleDoesNotYaw_AtAnyLock()
        {
            for (float steer = -1f; steer <= 1.0001f; steer += 0.25f)
                Assert.That(
                    VehicleSteeringMath.YawRateDegreesPerSecond(0f, steer, MaxInner, Wheelbase, FrontTrack),
                    Is.EqualTo(0f).Within(1e-6f),
                    $"she yawed at a standstill with the wheel at {steer:F2}. Yaw is v·tan(δ)/L, so " +
                    "it must vanish with v — a truck that pivots parked is a top-down shooter.");
        }

        /// <summary>Reversing steers the other way — same lock, opposite yaw. What backing a truck
        /// actually does, and it falls out of the model rather than being special-cased.</summary>
        [Test]
        public void ReversingReversesTheYaw()
        {
            float ahead = VehicleSteeringMath.YawRateDegreesPerSecond(5f, 1f, MaxInner, Wheelbase, FrontTrack);
            float astern = VehicleSteeringMath.YawRateDegreesPerSecond(-5f, 1f, MaxInner, Wheelbase, FrontTrack);
            Assert.That(ahead, Is.GreaterThan(0f), "+steer is a LEFT turn, which is +yaw in Unity 2D.");
            Assert.That(astern, Is.EqualTo(-ahead).Within(1e-4f));
        }

        /// <summary>
        /// ⭐ <b>The yaw rate and the drawn wheels describe the SAME circle.</b> The whole point of
        /// the coupling: drive at full lock for a second, and the radius she actually traced must be
        /// the radius her front wheels are pointing at.
        ///
        /// <para>Checked as <c>v/ω = R</c>, which is the definition of circular motion, against the
        /// radius derived from the Ackermann pair the renderer poses. Nothing here reuses the
        /// production code's own intermediate — the radius comes from the ANGLES, the rate from the
        /// yaw function — so the two can genuinely disagree.</para>
        /// </summary>
        [Test]
        public void TheYawRateTracesExactlyTheCircleTheFrontWheelsPointAt()
        {
            const float speed = 6f;
            foreach (float steer in new[] { 0.25f, 0.5f, 0.75f, 1f })
            {
                VehicleSteeringMath.AckermannDegrees(steer, MaxInner, Wheelbase, FrontTrack,
                                                     out float left, out float right);
                float rFromWheels = Wheelbase / Mathf.Tan(Mathf.Abs(left) * Mathf.Deg2Rad)
                                    + FrontTrack * 0.5f;

                float omega = VehicleSteeringMath.YawRateDegreesPerSecond(
                    speed, steer, MaxInner, Wheelbase, FrontTrack);
                float rFromYaw = speed / (omega * Mathf.Deg2Rad);

                Assert.That(rFromYaw, Is.EqualTo(rFromWheels).Within(rFromWheels * 1e-3f),
                    $"at steer {steer:F2} the truck traces a {rFromYaw:F3} m circle while her front " +
                    $"wheels point at a {rFromWheels:F3} m one. That is exactly the disagreement the " +
                    "rig's sidecar warns about, and this coupling exists to prevent it.");
            }
        }

        /// <summary>
        /// The full-lock turn radius, derived rather than transcribed: 8.348 m to the rear axle
        /// centre.
        ///
        /// <para>⚠️ <b>Her sidecar publishes 8.29.</b> The two disagree by 0.7% — a rounding artefact
        /// in a hand-transcription — and this repo takes the number the rig's own lock angles give,
        /// because that is the circle the drawn wheels are pointing at. Pinned here so the choice is
        /// a decision on the record rather than a discrepancy someone later "fixes" toward the
        /// sidecar.</para>
        /// </summary>
        [Test]
        public void TheFullLockTurnRadiusIsDerivedFromTheRigsAngles_NotFromTheSidecarsRounding()
        {
            float delta = VehicleSteeringMath.BicycleSteerDegrees(30f, 24.9372f);
            float radius = VehicleSteeringMath.TurnRadiusMeters(delta, Wheelbase);

            Assert.That(radius, Is.EqualTo(8.3478f).Within(1e-3f),
                "the derived full-lock radius moved.");
            Assert.That(radius, Is.Not.EqualTo(8.29f).Within(1e-2f),
                "this is deliberately NOT the sidecar's transcribed 8.29 — see the summary. If a " +
                "later drop makes the rig agree with the sidecar, update both this and the doc.");
        }

        /// <summary>Straight ahead is an infinite radius, not a zero one. A caller cannot mistake
        /// infinity for a tight turn; it could very easily mistake 0.</summary>
        [Test]
        public void StraightAheadIsAnInfiniteRadius()
        {
            Assert.That(VehicleSteeringMath.TurnRadiusMeters(0f, Wheelbase),
                Is.EqualTo(float.PositiveInfinity));
        }

        // =============================================================================================
        //  WHEEL ROLL — revolutions, not radians
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>The rate is <c>v/(2πr)</c>, NOT <c>v/r</c>.</b> The rig's roll axes are in
        /// REVOLUTIONS, cyclic with period 1; the "roll is an angle so it is v/r radians" reading is
        /// 2π ≈ 6.3× too fast, which is a wheel that spins like a cartoon.
        ///
        /// <para>Cross-checked against a number the ART side published independently: her contract
        /// states the tread advances <b>2.639 m</b> per revolution. One revolution of travel must
        /// therefore be exactly one revolution of roll.</para>
        /// </summary>
        [Test]
        public void WheelRollIsInRevolutions_AndOneTurnCoversTheRigsPublishedTread()
        {
            const float treadPerTurn = 2.639f;   // the contract's own number, not ours

            Assert.That(VehicleSteeringMath.RollRevolutions(treadPerTurn, WheelRadius),
                Is.EqualTo(1f).Within(1e-3f),
                "one tread-length of travel must be exactly one revolution. If this is out by 2π " +
                "someone has read the rig's roll as radians.");

            Assert.That(VehicleSteeringMath.RollRevolutions(2f * Mathf.PI * WheelRadius, WheelRadius),
                Is.EqualTo(1f).Within(1e-5f));

            // The sabotage this pins: the radian reading, which is 2π out.
            float radianReading = treadPerTurn / WheelRadius;
            Assert.That(radianReading, Is.EqualTo(2f * Mathf.PI).Within(1e-2f),
                "sanity: the WRONG formula would give 2π revolutions per turn instead of 1.");
        }

        /// <summary>Reversing unwinds the wheels rather than spinning them on. Signed, because the
        /// odometer that drives it is signed.</summary>
        [Test]
        public void RollingBackwardsUnwindsTheWheels()
        {
            Assert.That(VehicleSteeringMath.RollRevolutions(-2.639f, WheelRadius),
                Is.EqualTo(-1f).Within(1e-3f));
        }

        // =============================================================================================
        //  SUSPENSION
        // =============================================================================================

        /// <summary>
        /// The rig's <c>dz(y)</c>, verbatim — because anything riding the body must take the same
        /// correction, and the rig's own sidecar says so ("an occupant or a crate parented to the
        /// body must take the same correction").
        ///
        /// <para>Checked at both axles, where the interpolation collapses to one travel each, and
        /// then OUTSIDE them: <c>t</c> deliberately extrapolates, so the bumpers pitch further than
        /// the travel. A clamped implementation would pass the axle checks and fail the last one.</para>
        /// </summary>
        [Test]
        public void SuspensionDropIsTheRigsFormula_AndExtrapolatesPastTheAxles()
        {
            const float travelF = 0.09f, travelR = 0.11f, rearY = -2.12f;

            // At the FRONT axle t = 1: the whole drop is the front travel.
            Assert.That(
                VehicleSteeringMath.SuspensionDropMeters(2.18f, 1f, 0f, travelF, travelR, rearY, Wheelbase),
                Is.EqualTo(-travelF).Within(1e-5f));

            // At the REAR axle t = 0: the whole drop is the rear travel.
            Assert.That(
                VehicleSteeringMath.SuspensionDropMeters(rearY, 0f, 1f, travelF, travelR, rearY, Wheelbase),
                Is.EqualTo(-travelR).Within(1e-5f));

            // Level body, both ends compressed: the drop is between the two travels everywhere.
            float mid = VehicleSteeringMath.SuspensionDropMeters(
                0f, 1f, 1f, travelF, travelR, rearY, Wheelbase);
            Assert.That(mid, Is.LessThan(-travelF).And.GreaterThan(-travelR));

            // ⚠️ The front bumper is FORWARD of the front axle, so it must pitch FURTHER than the
            // front travel — the rig extrapolates and a clamp here would quietly flatten her nose-dive.
            float bumper = VehicleSteeringMath.SuspensionDropMeters(
                3.36f, 1f, 0f, travelF, travelR, rearY, Wheelbase);
            Assert.That(Mathf.Abs(bumper), Is.GreaterThan(travelF),
                "the front bumper stands 1.18 m ahead of the axle, so a nose-down pitch must move it " +
                "further than the axle's own travel. A clamped t would report exactly the travel.");
        }
    }
}
