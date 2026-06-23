using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// On-water integration for the ENGINE helm (the Punt — PR #55 propulsion branch), built from a
    /// minimal in-code harness (no greybox-scene dependency). The pure thrust/rudder math is covered
    /// headless in EditMode (BoatHandlingTests); this proves the LIVE rigidbody actually responds to the
    /// throttle/helm the way the owner expects when driving the boat:
    ///   (1) STEER — full ahead + full helm turns the bow a clearly-perceptible amount while making way
    ///       (the bug was an outboard that wouldn't steer at all);
    ///   (2) NO ZERO-SPEED PIVOT — helm hard over with no throttle barely moves the heading (an outboard
    ///       can't pivot dead in the water — the seamanship fantasy, boats-and-navigation.md §2);
    ///   (3) REVERSE — astern throttle drives the boat backwards relative to its bow.
    /// Environment is left null (GameServices.Reset), so wind/current/tide are zero and the motion under
    /// test is pure helm + engine.
    /// </summary>
    public class OutboardIntegrationPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();   // null environment → zero wind/current/tide

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // An engine hull mirroring the greybox Punt's stats (ApplyPuntStats) so the test reflects the
        // boat the owner actually drives: mass 700 kg, EnginePower 650, RudderAuthority 600.
        private BoatHullDef NewPuntHull(string id)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id; h.DisplayName = "Test Punt Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f; h.RudderAuthority = 600f;
            h.ForwardDrag = 140f; h.LateralDrag = 360f; h.WindExposure = 0f;
            h.DraughtMeters = 0.5f; h.HoldUnits = 14; h.CrewSlots = 1;
            h.MaxSafeSeaState = SeaState.Lively;
            h.CameraWorldHeightMeters = 17f;
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, BoatController boat, Rigidbody2D rb) NewBoat(Vector3 pos)
        {
            var go = new GameObject("Punt");
            go.transform.position = pos;
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + CapsuleCollider2D
            // Mirror the greybox builder's hull collider so the auto-computed rotational INERTIA matches
            // the boat the owner actually drives (a 4 m hull, not the unit-default capsule) — this is what
            // makes the rudder torque vs inertia balance realistic. (GreyboxBuilder line ~277.)
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            col.offset = Vector2.zero;
            _spawned.Add(go);
            return (go, boat, go.GetComponent<Rigidbody2D>());
        }

        // ---- (1) STEER: full ahead + full helm turns the bow perceptibly while making way ----

        [UnityTest]
        public IEnumerator Outboard_FullAheadFullHelm_TurnsTheBowWhileMakingWay()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            var hull = NewPuntHull("boat.punt.steer");
            yield return null;                          // Awake caches the rigidbody
            boat.SetHull(hull);

            // Drive full ahead to build way, then hold full ahead + full helm to starboard. Enough fixed
            // steps that an outboard that steers at all should swing the bow clearly. (Before the rudder
            // feel-scale fix this same run turned the bow ~0.8° — imperceptible: "the Punt won't steer".)
            for (int i = 0; i < 60; i++)
            {
                boat.SetControl(1f, 0f);                // build way first (no helm)
                yield return new WaitForFixedUpdate();
            }
            float headingAfterStraight = rb.rotation;

            for (int i = 0; i < 90; i++)
            {
                boat.SetControl(1f, 1f);                // full ahead + helm hard to starboard
                yield return new WaitForFixedUpdate();
            }
            float headingAfterHelm = rb.rotation;

            // The boat is making way ahead (positive Y is the bow at heading 0).
            Assert.Greater(rb.linearVelocity.magnitude, 0.5f, "the Punt should be making way under full throttle");
            // Helm to starboard turns the bow right. In Unity 2D, rotation INCREASES counter-clockwise, so a
            // clockwise (starboard) turn DECREASES rb.rotation — i.e. negative torque (matches RudderTorque).
            // A clearly-perceptible swing (≥10°): the bug was an imperceptible <1° at full helm underway.
            float turned = headingAfterHelm - headingAfterStraight;
            Assert.Less(turned, -10f,
                $"full helm while making way must turn the bow clearly (got {turned:F2}°). An outboard that " +
                "won't steer underway is the bug.");
        }

        // ---- (2) NO ZERO-SPEED PIVOT: helm hard over at rest barely moves the heading ---------

        [UnityTest]
        public IEnumerator Outboard_HelmAtRest_DoesNotPivot()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            var hull = NewPuntHull("boat.punt.pivot");
            yield return null;
            boat.SetHull(hull);

            float startHeading = rb.rotation;
            // Helm hard over with NO throttle: an outboard has ~no rudder authority dead in the water.
            for (int i = 0; i < 60; i++)
            {
                boat.SetControl(0f, 1f);
                yield return new WaitForFixedUpdate();
            }
            float turned = Mathf.Abs(rb.rotation - startHeading);
            Assert.Less(turned, 2f,
                $"the outboard must NOT pivot dead in the water (turned {turned:F2}°) — seamanship fantasy, §2");
        }

        // ---- (3) REVERSE: astern throttle drives the boat backwards relative to the bow ------

        [UnityTest]
        public IEnumerator Outboard_AsternThrottle_DrivesTheBoatBackwards()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            var hull = NewPuntHull("boat.punt.reverse");
            yield return null;
            boat.SetHull(hull);

            Vector2 bow = go.transform.up;              // heading 0 → bow points +Y
            Vector2 startPos = rb.position;

            for (int i = 0; i < 40; i++)
            {
                boat.SetControl(-1f, 0f);               // full astern, helm amidships
                yield return new WaitForFixedUpdate();
            }

            Vector2 disp = rb.position - startPos;
            Assert.Greater(disp.magnitude, 0.05f, "astern throttle must actually move the boat");
            Assert.Less(Vector2.Dot(disp, bow), 0f,
                "astern throttle must drive the boat backwards (opposite the bow) — S drives the Punt astern");
        }
    }
}
