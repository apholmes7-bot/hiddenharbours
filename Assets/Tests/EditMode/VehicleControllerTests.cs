using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>How the truck drives (ADR 0035)</b> — the pedals and the wheel, pinned as arithmetic so
    /// the feel is a decision on the record rather than something felt for once and forgotten.
    ///
    /// <para>The def is built in code rather than loaded, on purpose: these assert the MODEL, and a
    /// fixture that read the shipped asset would go red every time the owner tuned her, which is
    /// exactly what he is supposed to be able to do without asking anyone.</para>
    /// </summary>
    public class VehicleControllerTests
    {
        VehicleDef _def;
        VehicleMeshDef _mesh;

        [SetUp]
        public void SetUp()
        {
            _mesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _mesh.WheelbaseMeters = 4.30f;
            _mesh.FrontTrackMeters = 1.80f;
            _mesh.WheelRadiusMeters = 0.42f;
            _mesh.MaxInnerSteerDegrees = 30f;
            _mesh.MaxOuterSteerDegrees = 24.9372f;

            _def = ScriptableObject.CreateInstance<VehicleDef>();
            _def.Mesh = _mesh;
            _def.MaxSpeedMetersPerSecond = 11f;
            _def.MaxReverseSpeedMetersPerSecond = 4f;
            _def.AccelerationMetersPerSecondSquared = 4.5f;
            _def.BrakingMetersPerSecondSquared = 8f;
            _def.CoastDecelerationMetersPerSecondSquared = 2.2f;
            _def.SteerRateFullLocksPerSecond = 2f;
            _def.SteerReturnFullLocksPerSecond = 3f;
            _def.SteerFalloffHalfSpeedMetersPerSecond = 9f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_def);
            Object.DestroyImmediate(_mesh);
        }

        // =============================================================================================
        //  THE PEDALS
        // =============================================================================================

        /// <summary>Full throttle reaches the def's top speed and STOPS there — <c>MoveTowards</c>
        /// lands exactly on the target rather than asymptoting past or short of it.</summary>
        [Test]
        public void FullThrottleReachesTopSpeedAndHoldsIt()
        {
            float v = 0f;
            for (int i = 0; i < 600; i++) v = VehicleController.StepSpeed(v, 1f, false, _def, 0.02f);
            Assert.That(v, Is.EqualTo(_def.MaxSpeedMetersPerSecond).Within(1e-4f));

            v = VehicleController.StepSpeed(v, 1f, false, _def, 0.02f);
            Assert.That(v, Is.EqualTo(_def.MaxSpeedMetersPerSecond).Within(1e-4f),
                "she kept accelerating past her top speed.");
        }

        /// <summary>Reverse is its own, lower ceiling — a real gearbox, not a mirrored throttle.</summary>
        [Test]
        public void ReverseHasItsOwnLowerCeiling()
        {
            float v = 0f;
            for (int i = 0; i < 600; i++) v = VehicleController.StepSpeed(v, -1f, false, _def, 0.02f);
            Assert.That(v, Is.EqualTo(-_def.MaxReverseSpeedMetersPerSecond).Within(1e-4f));
        }

        /// <summary>
        /// ⭐ <b>The brake stops her AT zero, it does not drive her through it.</b> Standing on both
        /// pedals holds a truck still; a brake implemented as a negative throttle would back her out
        /// of the parking space.
        /// </summary>
        [Test]
        public void TheBrakeStopsHerDeadAndNeverReverses()
        {
            float v = 10f;
            for (int i = 0; i < 200; i++) v = VehicleController.StepSpeed(v, 1f, brake: true, _def, 0.02f);
            Assert.That(v, Is.EqualTo(0f).Within(1e-5f),
                "the brake must win over the throttle and settle at a dead stop.");

            v = VehicleController.StepSpeed(0f, 1f, brake: true, _def, 0.02f);
            Assert.That(v, Is.EqualTo(0f), "braking from rest must not push her backwards.");
        }

        /// <summary>She coasts down to rest with no pedals — slowly, as a laden truck does, but she
        /// does arrive. The rate is the def's, so the owner can tune the roll-on.</summary>
        [Test]
        public void SheCoastsToRestWithNoPedals()
        {
            float v = 11f;
            float afterOneSecond = v;
            for (int i = 0; i < 50; i++)
                afterOneSecond = VehicleController.StepSpeed(afterOneSecond, 0f, false, _def, 0.02f);
            Assert.That(afterOneSecond, Is.EqualTo(11f - _def.CoastDecelerationMetersPerSecondSquared)
                                          .Within(1e-3f));

            for (int i = 0; i < 1000; i++) v = VehicleController.StepSpeed(v, 0f, false, _def, 0.02f);
            Assert.That(v, Is.EqualTo(0f).Within(1e-5f));
        }

        // =============================================================================================
        //  THE WHEEL
        // =============================================================================================

        /// <summary>The wheel takes time to reach the lock — that delay IS the difference between a
        /// truck and a go-kart, and it is the def's number.</summary>
        [Test]
        public void TheWheelTakesTheDefsTimeToReachFullLock()
        {
            float s = 0f;
            for (int i = 0; i < 25; i++) s = VehicleController.StepSteer(s, 1f, _def, 0.02f);
            Assert.That(s, Is.EqualTo(1f).Within(1e-4f),
                "half a second at 2 full-locks/s should be exactly full lock.");

            s = 0f;
            for (int i = 0; i < 12; i++) s = VehicleController.StepSteer(s, 1f, _def, 0.02f);
            Assert.That(s, Is.LessThan(1f), "she must not snap to lock in a couple of ticks.");
        }

        /// <summary>
        /// ⭐ <b>Released, the wheel lands EXACTLY on centre.</b> A wheel that asymptotes toward zero
        /// never stops turning the truck, and over a long straight that is a drift the player cannot
        /// correct — the input is already neutral.
        /// </summary>
        [Test]
        public void ReleasingTheWheelSelfCentresExactly()
        {
            float s = 1f;
            for (int i = 0; i < 200; i++) s = VehicleController.StepSteer(s, 0f, _def, 0.02f);
            Assert.That(s, Is.EqualTo(0f), "self-centring must reach exact zero, not merely approach it.");
        }

        /// <summary>Self-centring is faster than the driver's own input, as a castering front end
        /// is.</summary>
        [Test]
        public void SelfCentringIsFasterThanSteeringIn()
        {
            float inbound = VehicleController.StepSteer(0f, 1f, _def, 0.02f);
            float outbound = 1f - VehicleController.StepSteer(1f, 0f, _def, 0.02f);
            Assert.That(outbound, Is.GreaterThan(inbound));
        }

        // =============================================================================================
        //  THE COUPLING, THROUGH THE COMPONENT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Speed-sensitive steering reduces the WHEEL, not the yaw.</b> Softening the yaw alone
        /// would leave the front wheels drawn hard over while the truck barely turned — precisely the
        /// disagreement the rig's sidecar warns nobody but us can prevent. So the falloff must be
        /// visible on <c>EffectiveSteer</c>, which is the single number both the picture and the
        /// physics read.
        /// </summary>
        [Test]
        public void SpeedSensitiveSteeringSoftensTheWHEEL_SoThePictureAndThePhysicsStayAgreed()
        {
            var go = new GameObject("truck", typeof(Rigidbody2D), typeof(VehicleController));
            try
            {
                var c = go.GetComponent<VehicleController>();
                c.SetVehicle(_def);

                // Wind the wheel to full lock at a standstill: no falloff at rest.
                c.SteerDemand = 1f;
                for (int i = 0; i < 40; i++) c.StepPhysics(0.02f);
                Assert.That(c.Steer, Is.EqualTo(1f).Within(1e-3f));
                Assert.That(c.EffectiveSteer, Is.EqualTo(1f).Within(1e-3f),
                    "at rest there is nothing to soften.");

                // Now bring her up to the half-speed: the geometry must see half the lock.
                c.Throttle = 1f;
                for (int i = 0; i < 2000 && c.SpeedMetersPerSecond < 9f; i++) c.StepPhysics(0.02f);
                Assert.That(c.SpeedMetersPerSecond, Is.GreaterThanOrEqualTo(9f),
                    "she never reached the falloff's half-speed, so the rest of this proves nothing.");
                Assert.That(c.Steer, Is.EqualTo(1f).Within(1e-3f),
                    "the DRIVER's wheel position has not changed — only what the geometry does with it.");
                Assert.That(c.EffectiveSteer, Is.EqualTo(0.5f).Within(0.02f),
                    "at the def's half-speed the geometry must see half the lock.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The odometer is signed and is the ONE thing the wheels' roll reads, so reversing
        /// unwinds them instead of spinning them on.</summary>
        [Test]
        public void TheOdometerIsSignedSoReversingUnwindsTheWheels()
        {
            var go = new GameObject("truck", typeof(Rigidbody2D), typeof(VehicleController));
            try
            {
                var c = go.GetComponent<VehicleController>();
                c.SetVehicle(_def);

                c.Throttle = 1f;
                for (int i = 0; i < 100; i++) c.StepPhysics(0.02f);
                float forward = c.OdometerMeters;
                Assert.That(forward, Is.GreaterThan(0f));

                c.Throttle = -1f;
                for (int i = 0; i < 400; i++) c.StepPhysics(0.02f);
                Assert.That(c.OdometerMeters, Is.LessThan(forward),
                    "backing up must unwind the odometer, and with it the wheels.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>A controller with no vehicle is inert rather than throwing — a scene-serialised
        /// instance the skinner has not reached yet must idle harmlessly.</summary>
        [Test]
        public void AControllerWithNoVehicleIsInert()
        {
            var go = new GameObject("truck", typeof(Rigidbody2D), typeof(VehicleController));
            try
            {
                var c = go.GetComponent<VehicleController>();
                c.Throttle = 1f;
                c.SteerDemand = 1f;
                Assert.DoesNotThrow(() => c.StepPhysics(0.02f));
                Assert.That(c.SpeedMetersPerSecond, Is.EqualTo(0f));
                Assert.That(c.YawRateDegreesPerSecond, Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
