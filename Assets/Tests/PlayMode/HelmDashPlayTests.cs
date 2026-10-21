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
    /// On-water integration for the console helms' wheel: the overlay host raises the BAND (H2, which
    /// retired ADR 0025's composed dash) the same frame a console-rig hull goes live (the dev F-cycle
    /// contract), the band's wheel MIRRORS <c>BoatController.Steer</c> (one state, one owner), a wheel
    /// steer session writes steer through <c>DragSteer</c> and HOLDS against the key layer's per-frame
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
            // ⭐ THE PLAYER IS AT THIS BOAT'S HELM. The Core slot is arbitrated by OCCUPANCY now
            // (HelmSlot) rather than granted to whichever relay enabled last, so the one boat in
            // this fixture has to be DECLARED hers — which is what the fixture always meant.
            GameServices.Helm.SetPilotedHull(boat);
            return (go, boat);
        }

        /// <summary>
        /// Register a <see cref="GameConfig"/> whose key-steer ease winds in the given time (S4.5).
        ///
        /// <para>The arbitration tests below want the ease ON — it is the shipped path and they run
        /// through it — but they assert about OWNERSHIP, not duration, and a quarter-second wind would
        /// turn every one of them into a frame-count-as-time bet (which is exactly the thing this file
        /// says at the top it never makes). A wind time far shorter than a frame settles the helm on
        /// the first Update, so the real eased code runs and the assertions stay about the handover.
        /// <see cref="KeySteer_WindsToTheTarget_AndSettlesExactly"/> is where the wind itself is
        /// pinned, and it does so without asserting any duration.</para>
        /// </summary>
        private GameConfig ConfigWithSteerWind(float seconds)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            HelmWheelSettings wheel = cfg.HelmWheel;
            wheel.SteerEaseSeconds = seconds;
            cfg.HelmWheel = wheel;
            _spawned.Add(cfg);
            GameServices.Config = cfg;
            return cfg;
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

        // ---- the band appears with the hull, same frame --------------------------------------------

        [UnityTest]
        public IEnumerator EveryConsoleRigHull_ShowsItsBand()
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
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band),
                        "a console-rig hull earns the band");
            Assert.That(host.BandController.Rig, Is.EqualTo(ConsoleRigKind.Console), "…its own piece list");

            boat.SetHull(NewConsoleHull("boat.test_dash_sport", ConsoleRigKind.Sport, HelmWheelRim.Steel));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "sport too");
            Assert.That(host.BandController.Rig, Is.EqualTo(ConsoleRigKind.Sport));
            Assert.That(GameServices.HelmControl.WheelRim, Is.EqualTo(HelmWheelRim.Steel));

            // The two wheelhouses' bands carry other pieces than the skiffs' (their piece lists are
            // their own), so the swap also exercises the controller's per-piece re-allocation.
            boat.SetHull(NewConsoleHull("boat.test_dash_novi", ConsoleRigKind.Novi, HelmWheelRim.Rubber));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "the Novi has its band");
            Assert.That(host.BandController.Rig, Is.EqualTo(ConsoleRigKind.Novi));

            boat.SetHull(NewConsoleHull("boat.test_dash_cape", ConsoleRigKind.Cape, HelmWheelRim.Steel));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band),
                        "and so does the Cape Islander");
            Assert.That(host.BandController.Rig, Is.EqualTo(ConsoleRigKind.Cape));

            // Back to a skiff — the piece list changes back without a stale texture or a stale key.
            boat.SetHull(NewConsoleHull("boat.test_dash_back", ConsoleRigKind.Console, HelmWheelRim.Rubber));
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band));
            Assert.That(host.BandController.Rig, Is.EqualTo(ConsoleRigKind.Console));
        }

        [UnityTest]
        public IEnumerator AWheelhouseHull_SteersThroughItsOwnWheelMount()
        {
            // A wheelhouse's band is its own piece list (H2): the press must find the wheel in THAT
            // list, laid out at the live screen, or a wheel grab on these hulls would miss.
            HelmOverlayHost host = Host();
            var (_, boat) = NewBoat();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_novisteer", ConsoleRigKind.Novi, HelmWheelRim.Rubber));
            yield return null;

            boat.SetControl(0f, 0.4f);
            yield return null;
            double expected = WheelRigGeometry.DegFromSteer(0.4, GameServices.HelmWheel.Turns);
            HelmBandController band = host.BandController;
            Assert.That(band.WheelDeg, Is.EqualTo(expected).Within(1e-4),
                        "the wheelhouse wheel mirrors the one steer owner too");

            // The rim's top, from the hub on the band's bottom edge (the band's own geometry).
            IHelmControl helm = GameServices.HelmControl;
            Rect wheel = band.PieceRect(HelmBandGeometry.WheelIndex(ConsoleRigKind.Novi));
            int k = band.K;
            var rimTop = new Vector2(wheel.xMin + HelmBandGeometry.HubX * k,
                                     wheel.yMax - HelmBandGeometry.HubY * k + (float)(WheelRigGeometry.R * k));
            Assert.That(band.PressControls(helm, rimTop, GameServices.HelmOverlay), Is.True,
                        "the wheelhouse wheel's rim takes the press");
            Assert.That(band.SteerSession, Is.True, "…and opens the steer session");
            band.Deactivate(helm);
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
            Assert.That(host.BandController.WheelDeg, Is.EqualTo(expected).Within(1e-4),
                        "the wheel renders BoatController.Steer — a mirror, not a copy");
            Assert.That(host.BandController.SteerSession, Is.False, "no session while keys steer");
        }

        // ---- the steer session: DragSteer writes, holds against the zeroing key layer -------------

        [UnityTest]
        public IEnumerator DragSteer_Writes_AndHoldsAgainstTheKeyLayersZeroWrites()
        {
            ConfigWithSteerWind(0.0001f);                // S4.5: ease on, but settling within a frame
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
            ConfigWithSteerWind(0.0001f);                // S4.5: ease on, but settling within a frame
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

        // ---- S4.5: the key steer WINDS to its target instead of slamming there --------------------

        [UnityTest]
        public IEnumerator KeySteer_WindsToTheTarget_AndSettlesExactly()
        {
            // The owner's ask ("the steering wheel needs to follow the turning from the arrow keys —
            // gradual and smooth"), as an integration pin: the eased COMMAND is what reaches
            // BoatController, so the dash wheel's mirror is gradual because the rudder is.
            //
            // Deliberately asserts NO duration — headless frames are not a clock (a settle test that
            // counted frames would be a bet on batchmode's deltaTime). It asserts the two things that
            // are true whatever the frame rate: it does not arrive in one Update, and it arrives
            // EXACTLY rather than asymptotically.
            ConfigWithSteerWind(0.25f);                  // the shipped default wind
            var (go, boat) = NewBoat();
            go.AddComponent<DevBoatInput>();
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
            boat.SetHull(NewConsoleHull("boat.test_dash_ease", ConsoleRigKind.Console, HelmWheelRim.Rubber));

            boat.SetControl(0f, 1f);                     // hard over, as if a key had been held
            yield return null;                           // one DevBoatInput.Update with no key down

            Assert.That(boat.Steer, Is.LessThan(1f), "the key layer began winding it back…");
            Assert.That(boat.Steer, Is.GreaterThan(0f),
                        "…but did NOT slam it to centre in a single frame — that was the defect");

            // Pump until it settles. The cap is absurdly generous (10 s at 60 fps against a 0.25 s
            // wind): it is a hang guard, not a timing assertion.
            const int Cap = 600;
            int frames = 0;
            while (boat.Steer != 0f && frames < Cap) { frames++; yield return null; }
            Assert.That(boat.Steer, Is.EqualTo(0f),
                        $"the ease must settle BIT-EXACTLY on centre (still {boat.Steer} after " +
                        $"{frames} frames) — an asymptotic approach would leave a sliver of rudder " +
                        "on forever and repaint the wheel every frame");

            // And it holds there: a settled helm is a still picture, not a jitter.
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(boat.Steer, Is.EqualTo(0f));
        }

        // ---- the lever still steps detents on a band's hull ---------------------------------------

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
