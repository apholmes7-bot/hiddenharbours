using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Integration proof for the active-boat heading seam (ADR 0007): in play mode the probe's
    /// OnEnable self-registers into <see cref="GameServices.ActiveBoat"/>, and a live rigidbody's
    /// velocity flows through <see cref="IActiveBoatService.Sample"/> as course-over-ground + SOG.
    /// (The bearing math + gating are covered headless in EditMode; this covers the runtime wiring
    /// the editor can't — Awake-cached rigidbody + the enable/disable registration lifecycle.)
    /// </summary>
    public class ActiveBoatProbePlayTests
    {
        private GameObject _go;
        private ActiveBoatProbe _probe;

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_go != null) Object.Destroy(_go);
        }

        private IEnumerator BuildActiveBoat()
        {
            _go = new GameObject("Boat");
            var ctrl = _go.AddComponent<BoatController>(); // RequireComponent adds the Rigidbody2D
            yield return null;                             // let Awake cache the rigidbody
            _probe = _go.AddComponent<ActiveBoatProbe>();  // OnEnable registers into GameServices
            _probe.Configure(ctrl);
        }

        [UnityTest]
        public IEnumerator Probe_SelfRegisters_AndReportsLiveHeadingAndVelocity()
        {
            yield return BuildActiveBoat();

            Assert.AreSame(_probe, GameServices.ActiveBoat, "the probe self-registers as the active-boat service");
            Assert.IsTrue(_probe.HasActiveBoat, "an enabled controller is the active boat");

            // Orient the bow East and give the hull a real course-over-ground; sample with no further
            // physics step so damping can't nibble the velocity before we read it.
            _go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // bow (transform.up) → +X (East)
            var rb = _go.GetComponent<Rigidbody2D>();
            rb.linearVelocity = new Vector2(3f, 4f);                   // |v| = 5 m/s, course ENE-ish

            var k = GameServices.ActiveBoat.Sample();
            Assert.IsTrue(k.HasBoat);
            Assert.AreEqual(90f, k.HeadingDegrees, 1f, "heading reflects the live bow facing");
            Assert.Less((k.Velocity - new Vector2(3f, 4f)).magnitude, 0.05f, "the live rigidbody velocity flows through");
            Assert.AreEqual(5f, k.SpeedOverGround, 0.05f, "SOG is the speed over ground");
        }

        [UnityTest]
        public IEnumerator Probe_ClearsTheServiceSlot_WhenDisabled()
        {
            yield return BuildActiveBoat();
            Assert.AreSame(_probe, GameServices.ActiveBoat, "registered while enabled");

            _probe.enabled = false;
            yield return null; // OnDisable runs

            Assert.IsNull(GameServices.ActiveBoat, "disabling the probe clears the Core slot (no dangling ref)");
        }
    }
}
