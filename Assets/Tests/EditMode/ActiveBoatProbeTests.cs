using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ActiveBoatProbe's pull-side contract (ADR 0007): it gates on the controller's enabled state
    /// and faithfully forwards the bow heading + velocity as a Core <see cref="BoatKinematics"/>. The
    /// bearing comes off the live transform (no physics step needed); the self-registration into
    /// GameServices and the live rigidbody velocity are the play-mode concern (ActiveBoatProbePlayTests).
    /// </summary>
    public class ActiveBoatProbeTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private (ActiveBoatProbe probe, BoatController boat) MakeProbe()
        {
            var go = new GameObject("Boat");           // BoatController auto-adds Rigidbody2D + collider
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            var probe = go.AddComponent<ActiveBoatProbe>();
            probe.Configure(boat);
            return (probe, boat);
        }

        [Test]
        public void NoBoat_ReportsInactive_AndNoneSnapshot()
        {
            var go = new GameObject("Bare");
            _spawned.Add(go);
            var probe = go.AddComponent<ActiveBoatProbe>(); // never Configured

            Assert.IsFalse(probe.HasActiveBoat, "no wired boat → not active");
            Assert.IsFalse(probe.Sample().HasBoat, "no wired boat → None snapshot");
        }

        [Test]
        public void EnabledBoat_IsActive_AndReportsBowHeading()
        {
            var (probe, boat) = MakeProbe();
            boat.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // bow (transform.up) swings to +X (East)

            Assert.IsTrue(probe.HasActiveBoat, "an enabled controller is the active boat");
            var k = probe.Sample();
            Assert.IsTrue(k.HasBoat);
            Assert.AreEqual(90f, k.HeadingDegrees, 1e-2f, "bow pointing East → heading 90");

            boat.transform.rotation = Quaternion.Euler(0f, 0f, 90f);  // bow swings to -X (West)
            Assert.AreEqual(270f, probe.Sample().HeadingDegrees, 1e-2f, "bow pointing West → heading 270");
        }

        [Test]
        public void DisabledController_ReadsInactive_AshoreOrMoored()
        {
            var (probe, boat) = MakeProbe();
            boat.enabled = false; // ControlSwitcher disables the controller when moored / on foot

            Assert.IsFalse(probe.HasActiveBoat, "a disabled controller is not the active boat");
            Assert.IsFalse(probe.Sample().HasBoat, "moored / on foot → None snapshot (no stale heading)");
        }

        [Test]
        public void Sample_ForwardsTheControllersVelocityVerbatim()
        {
            var (probe, boat) = MakeProbe();
            // The probe must pass through exactly what the controller reports (it never invents motion).
            Assert.AreEqual(boat.Velocity, probe.Sample().Velocity, "velocity is forwarded, not synthesised");
            Assert.AreEqual(boat.Velocity.magnitude, probe.Sample().SpeedOverGround, 1e-4f, "SOG = |forwarded velocity|");
        }
    }
}
