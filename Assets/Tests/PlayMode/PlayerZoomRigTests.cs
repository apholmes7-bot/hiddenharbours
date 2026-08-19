using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE WHEEL, IN A LIVE RIG (owner ruling 2026-08-19). <c>PlayerZoomTierTests</c> proves the tier
    /// arithmetic and the refusals as rules. It cannot prove that a real camera, ticking its own
    /// <c>LateUpdate</c> every frame, actually SETTLES on the tier the wheel asked for — the follow-cam
    /// re-runs the zoom policy, the framing tween and the bounds clamp on every frame, and any of the
    /// three could quietly pull the orthographic size back off the step. That is a rig fact, and this is
    /// where rig facts are checked (the <c>CameraBoundsRigTests</c> lesson).
    ///
    /// <para><b>Component-API driven, not device-driven.</b> The wheel is read by
    /// <see cref="CameraZoomInput"/> from <c>Mouse.current</c>, and synthesising a mouse would test
    /// Unity's input plumbing rather than this feature. So the tests drive the same entry point the
    /// reader does (<see cref="CameraFollow.NudgePlayerZoom"/>) and separately assert that the reader
    /// asks <see cref="CameraFollow.WheelIsLive"/> the questions that matter before it touches a
    /// device.</para>
    ///
    /// <para><b>No frame-count-as-time assertions.</b> A headless <c>yield return null</c> is nowhere
    /// near a real frame, so nothing here claims the ease finishes in N frames — the step ease is set to
    /// zero and what is asserted is where the camera LANDS.</para>
    /// </summary>
    public class PlayerZoomRigTests
    {
        private const int Ppu = 32;
        private const int ScreenH = 1080;
        private const float Aspect = 16f / 9f;

        private GameObject _camGo;
        private Camera _cam;
        private CameraFollow _follow;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            InteractionGate.Reset();
            GameServices.CurrentRegionBounds = default;

            // A config whose step ease is ZERO: in play mode a non-zero ease bridges frames, and this
            // suite asserts endpoints, not the bridge.
            _config = ScriptableObject.CreateInstance<GameConfig>();
            PlayerZoomSettings snap = PlayerZoomSettings.Default;
            snap.StepSeconds = 0f;
            _config.PlayerZoom = snap;
            GameServices.Config = _config;

            _camGo = new GameObject("TestCamera");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.aspect = Aspect;                  // pinned: batchmode's screen aspect is not the game's
            _follow = _camGo.AddComponent<CameraFollow>();
            _follow.Target = null;                 // isolate the zoom from the follow

            // ⚠️ AND THE FRAMING EASE TOO. A mode change (ashore ⇄ helm) eases over the camera's own
            // _framingTweenSeconds, which is a REAL-TIME duration — and a headless `yield return null`
            // is nowhere near a real frame, so "wait N frames for a 0.4 s ease" is exactly the
            // frame-count-as-time claim this suite refuses to make. Zeroed here so every assertion is
            // about where the camera LANDS. It is a serialized private dial with no setter (the owner
            // tunes it in the Inspector), hence the reflection.
            Zero(_follow, "_framingTweenSeconds");
            Zero(_follow, "_deckZoomTweenSeconds");
        }

        private static void Zero(CameraFollow follow, string fieldName)
        {
            FieldInfo f = typeof(CameraFollow).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"CameraFollow.{fieldName} was renamed — this rig's ease-zeroing is stale");
            f.SetValue(follow, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            // Fully qualified: `using System` is in scope for the stored delegates below, which makes a
            // bare `Object` ambiguous with System.Object.
            if (_camGo != null) UnityEngine.Object.DestroyImmediate(_camGo);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            GameServices.Config = null;
            GameServices.CurrentRegionBounds = default;
            InteractionGate.Reset();
            GameServices.Reset();
        }

        private static float Ortho(float worldHeightMeters)
            => CameraFollow.OrthoSizeForWorldHeight(worldHeightMeters);

        private static float StepHeight(int step)
            => CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);

        /// <summary>Put the rig on foot with the policy committed, the state the wheel acts from.
        /// The explicit tick time is far past the commit hold so the mode change lands at once, and
        /// the camera's own LateUpdate tick cannot re-commit while the mode is unchanged.</summary>
        private void StandOnFoot(double now = 100.0)
        {
            _follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            _follow.TickZoom(now);
        }

        // ===== the wheel lands on a step, and STAYS there =========================================

        [UnityTest]
        public IEnumerator AWheelNotch_LeavesTheLiveCameraOnTheNextIntegerTier()
        {
            StandOnFoot();
            yield return null;

            Assert.IsTrue(_follow.NudgePlayerZoom(+1));

            // Several frames of the camera doing everything it does per frame — policy tick, tween,
            // bounds clamp. The tier must survive all of it.
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.AreEqual(5, _follow.PlayerZoomStep, $"frame {frame}: the tier drifted");
                Assert.AreEqual(Ortho(StepHeight(5)), _cam.orthographicSize, 1e-4f,
                    $"frame {frame}: the live camera left the x5 pixel-perfect step");
            }
        }

        [UnityTest]
        public IEnumerator WheelingOutThenIn_WalksTheLadderOneStopAtATime()
        {
            StandOnFoot();
            yield return null;

            _follow.NudgePlayerZoom(-1);
            yield return null;
            Assert.AreEqual(3, _follow.PlayerZoomStep);
            Assert.AreEqual(Ortho(StepHeight(3)), _cam.orthographicSize, 1e-4f);

            _follow.NudgePlayerZoom(+3);
            yield return null;
            Assert.AreEqual(6, _follow.PlayerZoomStep);
            Assert.AreEqual(Ortho(StepHeight(6)), _cam.orthographicSize, 1e-4f,
                "three notches in from the widest stop is the closest stop — no stops skipped or invented");
        }

        // ===== boarding: the boat's ruled tier wins, and the walker's is given back ===============

        [UnityTest]
        public IEnumerator TakingTheHelm_RestoresTheBoatsRuledTier_OverAnythingTheWheelDid()
        {
            StandOnFoot();
            yield return null;
            _follow.NudgePlayerZoom(+2);            // she is zoomed right in when she boards
            yield return null;

            _follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
            _follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            _follow.TickZoom(200.0);
            yield return null;

            Assert.AreEqual(Ortho(17f), _cam.orthographicSize, 1e-4f,
                "the helm frames the HULL's data-driven height — the wheel's rung is not carried aboard");
            Assert.IsFalse(_follow.WheelIsLive, "and the wheel is dead while she is at the helm");
        }

        [UnityTest]
        public IEnumerator SteppingAshoreAfterATrip_PutsHerBackOnHerOwnRung()
        {
            StandOnFoot();
            yield return null;
            _follow.NudgePlayerZoom(+2);
            yield return null;

            _follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
            _follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            _follow.TickZoom(200.0);
            yield return null;

            _follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            _follow.TickZoom(300.0);
            yield return null;

            Assert.AreEqual(6, _follow.PlayerZoomStep);
            Assert.AreEqual(Ortho(StepHeight(6)), _cam.orthographicSize, 1e-4f,
                "ashore she gets the rung she left, not the standing default");
        }

        // ===== the reader ==========================================================================

        [UnityTest]
        public IEnumerator TheWheelReader_IsInertWhileAModalHoldsTheGate()
        {
            _camGo.AddComponent<CameraZoomInput>();   // finds the CameraFollow beside it, as the builders wire it
            StandOnFoot();
            yield return null;

            float before = _cam.orthographicSize;
            InteractionGate.IsBlocked = true;

            for (int frame = 0; frame < 3; frame++) yield return null;

            Assert.IsFalse(_follow.WheelIsLive);
            Assert.AreEqual(before, _cam.orthographicSize, 1e-6f,
                "with a modal up the reader must not move the view — nor bank scroll for when it closes");

            InteractionGate.IsBlocked = false;
            yield return null;
            Assert.IsTrue(_follow.WheelIsLive, "closing the modal hands the wheel straight back");
        }

        [UnityTest]
        public IEnumerator TheReaderRunsEveryFrameAndChangesNothingOnItsOwn()
        {
            // A reader with no device input must be a no-op — the standing framing after a hundred
            // frames of it is the framing that shipped.
            _camGo.AddComponent<CameraZoomInput>();
            StandOnFoot();

            for (int frame = 0; frame < 20; frame++) yield return null;

            Assert.AreEqual(Ortho(CameraFollow.OnFootWorldHeightMeters), _cam.orthographicSize, 1e-4f,
                "an untouched wheel leaves the authored on-foot framing exactly where it was");
        }

        // ===== rule 5: the zoom is presentation ====================================================

        [UnityTest]
        public IEnumerator TheZoom_SpeaksToNothing_ItIsPresentationOnly()
        {
            // RULE 5, from the outbound side. The camera may change what is on screen and nothing else:
            // if a zoom ever published on the bus, some system downstream could start behaving
            // differently depending on how the player framed the view, and the sim would stop being a
            // function of (worldSeed, gameTime) alone. The signals watched here are the ones a camera
            // could plausibly reach for; the save side of the same claim is PlayerZoomTierTests'
            // TheZoomIsNeverSaved.
            // Stored delegates, not local functions: Unsubscribe removes by delegate equality, and one
            // stored instance per channel makes that unarguable.
            int heard = 0;
            Action<ControlModeChanged> onMode = _ => heard++;
            Action<ActiveBoatChanged> onBoat = _ => heard++;
            Action<DevNotice> onNotice = _ => heard++;

            StandOnFoot();
            yield return null;

            Rect boundsBefore = GameServices.CurrentRegionBounds;
            EventBus.Subscribe(onMode);
            EventBus.Subscribe(onBoat);
            EventBus.Subscribe(onNotice);
            try
            {
                _follow.NudgePlayerZoom(+2);
                yield return null;
                _follow.NudgePlayerZoom(-1);
                yield return null;

                Assert.AreEqual(0, heard, "a zoom step must publish nothing — it is presentation, not sim");
                Assert.AreEqual(boundsBefore, GameServices.CurrentRegionBounds,
                    "…and must not touch the region seam the sim and the camera share");
                Assert.AreEqual(5, _follow.PlayerZoomStep, "…while still having actually zoomed");
            }
            finally
            {
                EventBus.Unsubscribe(onMode);
                EventBus.Unsubscribe(onBoat);
                EventBus.Unsubscribe(onNotice);
            }
        }
    }
}
