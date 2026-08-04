using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// On-water integration for the ADR 0025 S2a composed skiff dash: the overlay host swaps to the
    /// COMPOSED DASH the same frame a console-rig hull goes live (the dev F-cycle contract), the
    /// wheel MIRRORS <c>BoatController.Steer</c> (one state, one owner), a focused wheel steer
    /// session writes steer through <c>DragSteer</c> and HOLDS against the key layer's per-frame
    /// zero writes (the S2a arbitration: preserve on zero, keys win on a real press), and the S1
    /// lever detents still step on a console hull. Real frames, never a mocked tick — and never
    /// frame-count-as-time (only order/holding is asserted, no duration).
    /// </summary>
    public class HelmDashPlayTests
    {
        private readonly List<Object> _spawned = new();
        private Keyboard _testKeyboard;

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_testKeyboard != null)
            {
                InputSystem.RemoveDevice(_testKeyboard);
                _testKeyboard = null;
            }
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef NewConsoleHull(string id, ConsoleRigKind rig, HelmWheelRim rim)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + id;
            console.Rig = rig;
            console.Lever = rig == ConsoleRigKind.Sport ? HelmLeverFinish.Chrome : HelmLeverFinish.Graphite;
            console.Wheel = rim;
            console.DefaultCompass = CompassMount.Dome;   // the S2a authored default on the skiffs
            _spawned.Add(console);

            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Test Console Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            h.Helm = console;
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, BoatController boat) NewBoat()
        {
            var go = new GameObject("DashTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds HelmControlRelay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            return (go, boat);
        }

        private static HelmOverlayHost Host()
        {
            HelmOverlayHost host = HelmOverlayHost.Instance;
            if (host == null)
            {
                var go = new GameObject("HelmOverlayHost(test)");
                host = go.AddComponent<HelmOverlayHost>();
            }
            return host;
        }

        // ---- the composed dash appears with the hull, same frame ----------------------------------

        [UnityTest]
        public IEnumerator EveryConsoleRigHull_ShowsTheComposedDash()
        {
            HelmOverlayHost host = Host();
            var (_, boat) = NewBoat();
            yield return null;

            boat.SetHull(NewConsoleHull("boat.test_dash_console", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            Assert.That(GameServices.HelmControl.Fit.Rig, Is.EqualTo(ConsoleRigKind.Console),
                        "the fit seam reads the live hull the same frame (F-cycle contract)");
            Assert.That(GameServices.HelmControl.Fit.Compass, Is.EqualTo(CompassMount.Dome));
            Assert.That(GameServices.HelmControl.WheelRim, Is.EqualTo(HelmWheelRim.Rubber));
            yield return null;   // one host Update
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash),
                        "a console-rig hull earns the composed dash");

            boat.SetHull(NewConsoleHull("boat.test_dash_sport", ConsoleRigKind.Sport, HelmWheelRim.Steel));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash), "sport too");
            Assert.That(GameServices.HelmControl.WheelRim, Is.EqualTo(HelmWheelRim.Steel));

            // S4: the two wheelhouses were on the lone lever card until their renderers landed. They
            // cross a CANVAS boundary as well as a rig one (600×548 vs the skiffs' 600×510), so the
            // swap also exercises the compositor's surface re-allocation.
            boat.SetHull(NewConsoleHull("boat.test_dash_novi", ConsoleRigKind.Novi, HelmWheelRim.Rubber));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash),
                        "the Novi has its own dash now");

            boat.SetHull(NewConsoleHull("boat.test_dash_cape", ConsoleRigKind.Cape, HelmWheelRim.Steel));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash),
                        "and so does the Cape Islander");

            // Back to a skiff — the canvas shrinks again without a stale surface or a stale key.
            boat.SetHull(NewConsoleHull("boat.test_dash_back", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash));
        }

        [UnityTest]
        public IEnumerator AWheelhouseHull_SteersThroughItsOwnWheelMount()
        {
            // The pilothouse wheel hub sits 86 px lower on the card than the skiffs' (382 vs 296). If
            // the compositor hit-tested against the skiff table, a wheel grab on these hulls would miss.
            HelmOverlayHost host = Host();
            var (_, boat) = NewBoat();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_novisteer", ConsoleRigKind.Novi, HelmWheelRim.Rubber));
            yield return null;

            boat.SetControl(0f, 0.4f);
            yield return null;
            double expected = WheelRigGeometry.DegFromSteer(0.4, GameServices.HelmWheel.Turns);
            Assert.That(host.DashController.WheelDeg, Is.EqualTo(expected).Within(1e-4),
                        "the wheelhouse wheel mirrors the one steer owner too");

            IHelmControl helm = GameServices.HelmControl;
            host.DashController.Press(helm, new Vector2(HelmDashGeometry.PilotWheelCx,
                                                        HelmDashGeometry.PilotWheelCy
                                                        + HelmDashGeometry.PilotTOPPAD),
                                      GameServices.HelmOverlay);
            Assert.That(host.DashController.SteerSession, Is.True,
                        "a press on the wheelhouse hub opens the steer session");
            host.DashController.Deactivate(helm);
        }

        // ---- the wheel mirrors the one steer owner -------------------------------------------------

        [UnityTest]
        public IEnumerator KeySteer_IsMirroredByTheWheel_NeverASecondState()
        {
            HelmOverlayHost host = Host();
            var (_, boat) = NewBoat();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_mirror", ConsoleRigKind.Console, HelmWheelRim.Rubber));

            boat.SetControl(0f, 0.4f);   // what a held key writes
            yield return null;           // host Update mirrors
            double expected = WheelRigGeometry.DegFromSteer(0.4, GameServices.HelmWheel.Turns);
            Assert.That(host.DashController.WheelDeg, Is.EqualTo(expected).Within(1e-4),
                        "the wheel renders BoatController.Steer — a mirror, not a copy");
            Assert.That(host.DashController.SteerSession, Is.False, "no session while keys steer");
        }

        // ---- the steer session: DragSteer writes, holds against the zeroing key layer -------------

        [UnityTest]
        public IEnumerator DragSteer_Writes_AndHoldsAgainstTheKeyLayersZeroWrites()
        {
            var (go, boat) = NewBoat();
            go.AddComponent<DevBoatInput>();             // the live key layer that stomps steer
            _testKeyboard = InputSystem.AddDevice<Keyboard>();   // headless has no device otherwise
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_steer", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            IHelmControl helm = GameServices.HelmControl;

            // Control: WITHOUT a session, the momentary key layer returns steer to centre.
            boat.SetControl(0f, 0.5f);
            yield return null;
            yield return null;
            Assert.That(boat.Steer, Is.EqualTo(0f).Within(1e-5f),
                        "no keys held → the momentary layer centres the steer (S1 semantics, untouched)");

            // A live wheel session HOLDS: the key layer preserves the held steer on its zero reads.
            helm.DragSteer(-0.6f);
            Assert.That(boat.Steer, Is.EqualTo(-0.6f).Within(1e-5f), "DragSteer writes the one owner");
            Assert.That(helm.SteerDragActive, Is.True);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(boat.Steer, Is.EqualTo(-0.6f).Within(1e-5f),
                        "the session held across real frames of the key layer running");

            // Session over → the momentary layer resumes and centres it again.
            helm.EndSteerDrag();
            yield return null;
            yield return null;
            Assert.That(boat.Steer, Is.EqualTo(0f).Within(1e-5f), "keys resume ownership after the session");
        }

        [UnityTest]
        public IEnumerator ARealKeyPress_BreaksTheSteerSession_KeysWin()
        {
            var (go, boat) = NewBoat();
            go.AddComponent<DevBoatInput>();
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_keywin", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            IHelmControl helm = GameServices.HelmControl;

            helm.DragSteer(0.7f);
            Assert.That(helm.SteerDragActive, Is.True);

            // Direct state write (not a queued event): an unfocused batchmode app drops keyboard
            // EVENTS for non-background devices, but InputState.Change lands regardless.
            InputState.Change(_testKeyboard, new KeyboardState(Key.A));             // hard to port
            yield return null;
            if (!_testKeyboard.aKey.isPressed)
                Assert.Ignore("virtual key press not deliverable here (unfocused headless input) — " +
                              "the arbitration's hold path is covered by the DragSteer test above");
            yield return null;
            Assert.That(helm.SteerDragActive, Is.False,
                        "a real key press ends the wheel session — one decisive handover");
            Assert.That(boat.Steer, Is.EqualTo(-1f).Within(1e-5f), "and the key owns the channel");

            InputState.Change(_testKeyboard, new KeyboardState());                  // release
            yield return null;
            yield return null;
            Assert.That(boat.Steer, Is.EqualTo(0f).Within(1e-5f), "momentary key semantics untouched");
        }

        // ---- the lever still steps detents on the composed dash's hull ----------------------------

        [UnityTest]
        public IEnumerator LeverDetents_StillStep_OnAConsoleHull()
        {
            var (_, boat) = NewBoat();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_detent", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            IHelmControl helm = GameServices.HelmControl;

            helm.StepAhead();
            Assert.That(boat.Throttle, Is.EqualTo(0.25f).Within(1e-5f), "default 4-ahead ladder");
            helm.StepAhead();
            Assert.That(boat.Throttle, Is.EqualTo(0.5f).Within(1e-5f));
            helm.SetNeutral();
            Assert.That(boat.Throttle, Is.EqualTo(0f));
        }
    }
}
