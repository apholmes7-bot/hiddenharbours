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
    /// On-water integration for the ADR 0025 S1 helm-control seam: the self-installing
    /// <see cref="HelmControlRelay"/> on a LIVE engine boat — mouse drag-to-set flowing
    /// <c>DragDrive → BoatController.SetControl → EngineThrust → forward way</c>, detent steps that
    /// HOLD across frames (the owner's stepped-and-held directive: real elapsed frames, not a mocked
    /// tick), the neutral snap window on release, and the dev F-cycle contract (the style follows a
    /// live hull swap instantly, tiller ↔ lever).
    ///
    /// Environment is left null (GameServices.Reset) so wind/current/tide are zero and the motion
    /// under test is pure helm + engine. GameConfig is unwired → every read falls back to
    /// <see cref="HelmThrottleSettings.Default"/> (4 ahead / 2 astern / snap 0.08), which is what the
    /// assertions pin.
    /// </summary>
    public class HelmControlPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef NewEngineHull(string id, bool withConsole)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Test Engine Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            if (withConsole)
            {
                var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
                console.Id = "helm.test_console";
                console.Lever = HelmLeverFinish.Chrome;
                _spawned.Add(console);
                h.Helm = console;
            }
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, BoatController boat, Rigidbody2D rb) NewBoat()
        {
            var go = new GameObject("HelmTestBoat");
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D etc.; Awake self-adds HelmControlRelay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            // ⭐ THE PLAYER IS AT THIS BOAT'S HELM. The Core slot is arbitrated by OCCUPANCY now
            // (HelmSlot) rather than granted to whichever relay enabled last, so the one boat in
            // this fixture has to be DECLARED hers — which is what the fixture always meant.
            GameServices.Helm.SetPilotedHull(boat);
            return (go, boat, go.GetComponent<Rigidbody2D>());
        }

        // ---- the seam self-installs and reports the right control ---------------------------------

        [UnityTest]
        public IEnumerator Relay_SelfInstalls_AndTheStyleFollowsTheLiveHull_TheDevFCycleContract()
        {
            var (go, boat, _) = NewBoat();
            yield return null;   // Awake + OnEnable have run

            Assert.That(go.GetComponent<HelmControlRelay>(), Is.Not.Null,
                        "BoatController.Awake self-adds the relay (no builder re-run needed)");
            Assert.That(GameServices.HelmControl, Is.Not.Null, "the relay registered the Core seam");

            // No hull yet → no helm.
            Assert.That(GameServices.HelmControl.HasHelm, Is.False);

            // A bare motor (no console) shows the TILLER…
            boat.SetHull(NewEngineHull("boat.test_tiller", withConsole: false));
            Assert.That(GameServices.HelmControl.HasHelm, Is.True);
            Assert.That(GameServices.HelmControl.Style, Is.EqualTo(HelmControlStyle.Tiller));

            // …and the dev F-cycle's in-place hull swap flips the control INSTANTLY (owner addition
            // 2026-08-03: each cycled hull shows its appropriate control) — same frame, no re-wire.
            boat.SetHull(NewEngineHull("boat.test_lever", withConsole: true));
            Assert.That(GameServices.HelmControl.Style, Is.EqualTo(HelmControlStyle.Lever));
            Assert.That(GameServices.HelmControl.LeverFinish, Is.EqualTo(HelmLeverFinish.Chrome),
                        "the console's authored finish rides the seam");
        }

        // ---- mouse drag-to-set → SetControl → EngineThrust → the boat makes way -------------------

        [UnityTest]
        public IEnumerator DragDrive_FlowsThroughSetControl_IntoEngineThrust_AndTheBoatMakesWay()
        {
            var (_, boat, rb) = NewBoat();
            yield return null;
            boat.SetHull(NewEngineHull("boat.test_drag", withConsole: true));
            IHelmControl helm = GameServices.HelmControl;

            helm.DragDrive(0.6f);
            Assert.That(boat.Throttle, Is.EqualTo(0.6f).Within(1e-5f),
                        "the drag writes the controller's own throttle — one state, one owner");
            Assert.That(helm.Drive, Is.EqualTo(0.6f).Within(1e-5f),
                        "and the seam reads the same value back for the picture");

            // The thrust the physics will apply is the EngineThrust of exactly that value.
            float expected = BoatController.EngineThrust(0.6f, 650f, BoatController.DefaultAsternFactor);
            Assert.That(expected, Is.EqualTo(0.6f * 650f).Within(1e-3f));

            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            float way = Vector2.Dot(rb.linearVelocity, (Vector2)boat.transform.up);
            Assert.That(way, Is.GreaterThan(0.2f), "a held 0.6 drive makes real way through the water");
        }

        [UnityTest]
        public IEnumerator EndDrag_InsideTheNeutralWindow_SnapsToNeutral_OutsideHolds()
        {
            var (_, boat, _) = NewBoat();
            yield return null;
            boat.SetHull(NewEngineHull("boat.test_snap", withConsole: true));
            IHelmControl helm = GameServices.HelmControl;

            helm.DragDrive(0.05f);   // inside the default 0.08 window
            helm.EndDrag();
            Assert.That(boat.Throttle, Is.EqualTo(0f), "released inside the centre gate → neutral");

            helm.DragDrive(0.31f);   // a real lever stays where you left it
            helm.EndDrag();
            Assert.That(boat.Throttle, Is.EqualTo(0.31f).Within(1e-5f));
        }

        // ---- key steps HOLD across real frames -----------------------------------------------------

        [UnityTest]
        public IEnumerator StepAhead_HoldsTheDetentAcrossFrames_AndAccumulatesPerPress()
        {
            var (go, boat, rb) = NewBoat();
            go.AddComponent<DevBoatInput>();   // the live input layer must PRESERVE the held drive
            yield return null;
            boat.SetHull(NewEngineHull("boat.test_hold", withConsole: false));
            IHelmControl helm = GameServices.HelmControl;

            helm.StepAhead();      // one press = one detent (default ladder: 4 ahead)
            Assert.That(boat.Throttle, Is.EqualTo(0.25f).Within(1e-5f));

            // The drive must HOLD between presses across REAL frames with the input layer running —
            // this is the owner's core ask (the old binary key zeroed it every frame it wasn't held).
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(boat.Throttle, Is.EqualTo(0.25f).Within(1e-5f),
                        "the detent held with no key down");

            helm.StepAhead();
            Assert.That(boat.Throttle, Is.EqualTo(0.5f).Within(1e-5f), "presses accumulate");

            helm.StepAstern();
            Assert.That(boat.Throttle, Is.EqualTo(0.25f).Within(1e-5f));

            helm.SetNeutral();
            Assert.That(boat.Throttle, Is.EqualTo(0f), "the dedicated neutral key chops to 0");

            // And the held detent actually drives the hull (hold at one detent, gain way).
            helm.StepAhead();
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            float way = Vector2.Dot(rb.linearVelocity, (Vector2)boat.transform.up);
            Assert.That(way, Is.GreaterThan(0.05f), "a held detent keeps thrusting with no key down");
        }

        // ---- oars are untouched (the directive is motorised hulls ONLY) ----------------------------

        [UnityTest]
        public IEnumerator OarsHull_ShowsNoHelmControl()
        {
            var (_, boat, _) = NewBoat();
            yield return null;
            var oars = ScriptableObject.CreateInstance<BoatHullDef>();
            oars.Id = "boat.test_oars";
            oars.Propulsion = PropulsionType.Oars;
            _spawned.Add(oars);
            boat.SetHull(oars);

            Assert.That(GameServices.HelmControl.HasHelm, Is.False,
                        "a rowed hull has no engine helm — oar input is untouched by this arc");
            Assert.That(GameServices.HelmControl.Style, Is.EqualTo(HelmControlStyle.None));
        }
    }
}
