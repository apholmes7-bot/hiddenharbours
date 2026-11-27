using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The idle fidgets at the game camera</b>: the player standing in St Peters at noon, photographed
    /// through the game's own camera at the key moments of every shipped fidget his skin can draw, beside a
    /// control of the same frame with the head straight.
    ///
    /// <para><b>Opt-in.</b> Runs only with <c>HH_IDLE_FIDGET_PLATES=1</c> in the editor's launch shell (and a
    /// graphics device). Otherwise every case is ignored before its first yield, so CI pays nothing.</para>
    ///
    /// <para><b>How a moment is held.</b> The fidget is pinned with <see cref="IdleFidgetPreview.Hold"/>, the
    /// game clock is let run for a few idle beats (the rig samples the look on the clip's beat, so a frozen
    /// clock would keep the last beat's head), then the clock and engine time are stopped and the frame is
    /// taken. The look is a real pose of the rig through the shipped path: nothing is drawn by the fixture.</para>
    ///
    /// <para>The hour is set with <c>SeekTo</c> and the clock's own time scale (the village plates' way), never
    /// <see cref="WharfNightStage.SetNight"/>. St Peters is the start scene, so the shell is cleared with
    /// <see cref="ShellFlow.Reset"/>, never <c>StartNewGame()</c>, which would write the save every worktree shares.
    /// Plates land in <c>temporaryCachePath/idle-fidget-plates/</c>.</para>
    /// </summary>
    public class IdleFidgetPlatePlayTests
    {
        private const string EnvGate = "HH_IDLE_FIDGET_PLATES";
        private const string PlateDir = "idle-fidget-plates";
        private const string StPetersScene = "StPeters";
        private const float NoonHour = 12f;
        private const int SettleFrames = 60;
        private const int BeatFrames = 30;   // about half a second of idle beats at 60 fps

        private WharfNightStage _stage;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            IdleFidgetPreview.Clear();
            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            ShellFlow.Reset();
            InteractionGate.Reset();
        }

        [UnityTest]
        public IEnumerator Plate_ThePlayerLooksAbout_StPetersNoon()
        {
            if (System.Environment.GetEnvironmentVariable(EnvGate) != "1")
                Assert.Ignore($"opt-in plate: set {EnvGate}=1 in the editor's launch shell to take it");
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();

            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "harness: St Peters registered no clock");
            Assert.IsNotNull(config, "harness: St Peters registered no GameConfig");
            clock.SeekTo((1.0 + NoonHour / 24.0) * config.SecondsPerDay);
            clock.TimeScale = 0f;
            for (int i = 0; i < SettleFrames; i++) yield return null;

            var walk = Object.FindFirstObjectByType<PlayerWalkController>();
            var presenter = Object.FindFirstObjectByType<DeckRiderMeshPresenter>();
            Assert.IsNotNull(walk, "harness: no player walking in St Peters");
            Assert.IsNotNull(presenter, "harness: the player has no mesh presenter");
            yield return _stage.FrameOn(walk.transform.position);

            IsoCharacterFigureRenderer figure = presenter.AshoreFigure;
            if (figure == null || !figure.isActiveAndEnabled)
                Assert.Inconclusive("the player is not drawn as a mesh ashore here, so no fidget can be shown");

            IdleFidgetLibraryDef lib = IdleFidgetLibraryDef.Shipped;
            Assert.IsNotNull(lib, "harness: no shipped fidget library");

            var log = new StringBuilder();
            int shot = 0;
            foreach (IdleFidgetDef d in lib.Fidgets)
            {
                if (d == null) continue;
                if (d.Kind != IdleFidgetDef.Channel.Look)
                {
                    log.AppendLine($"{d.Id}: SKIPPED, a clip fidget (clip '{d.ClipKey}') the current rig does not carry");
                    continue;
                }

                // The control: the same fidget at its first key, which is the figure's own straight look.
                yield return HoldAndSettle(clock, d, 0.0);
                double straight = figure.DrawnLookYaw;
                _stage.SavePlate($"{d.Id}-0-control.png", _stage.Capture());

                for (int k = 1; k < d.Gaze.Length - 1; k++)
                {
                    IdleFidgetDef.GazeKey key = d.Gaze[k];
                    yield return HoldAndSettle(clock, d, key.AtSeconds);
                    double yaw = figure.DrawnLookYaw, pitch = figure.DrawnLookPitch;
                    _stage.SavePlate($"{d.Id}-{k}-at-{key.AtSeconds:0.0}s.png", _stage.Capture());
                    log.AppendLine($"{d.Id} key {k} at {key.AtSeconds:0.0} s: asked yaw {key.YawDegrees:0} up {key.UpDegrees:0}; " +
                                   $"drawn yaw {yaw:0.0} pitch {pitch:0.0} (straight {straight:0.0})");
                    if (Mathf.Abs(key.YawDegrees) >= 10f)
                        Assert.AreEqual(Mathf.Sign(key.YawDegrees), System.Math.Sign(yaw),
                            $"{d.Id} key {k}: the head did not turn the way the fidget asks");
                    shot++;
                }
            }
            Debug.Log($"[{PlateDir}] {shot} fidget plates at the player's figure:\n{log}");
            Assert.Greater(shot, 0, "no Look fidget was photographed");
        }

        /// <summary>Hold one moment, let the idle clip beat a few times on a running clock, then stop the
        /// clock and engine time again for the capture.</summary>
        private static IEnumerator HoldAndSettle(IGameClock clock, IdleFidgetDef def, double local)
        {
            IdleFidgetPreview.Hold(def, local);
            Time.timeScale = 1f;
            clock.TimeScale = 1f;
            for (int i = 0; i < BeatFrames; i++) yield return null;
            clock.TimeScale = 0f;
            yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }
    }
}
