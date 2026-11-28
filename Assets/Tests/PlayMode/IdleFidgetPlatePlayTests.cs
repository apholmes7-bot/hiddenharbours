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
        private const int BeatFrames = 30;   // at least this many frames on a running clock per moment
        private const int SettleBeats = 2;   // and at least this many beats of the figure's clip
        private const float SettleCapSeconds = 3f;   // real seconds; a held clip fidget beats once and holds
        private const int NudgeEveryFrames = 10;     // frames without a beat before the clock is moved on
        private const double NudgeSeconds = 0.25;    // game seconds per nudge, about one idle frame
        private static int s_nudges;

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

            // The strip: one tile per fidget, cropped round his feet from the same game-zoom frame.
            Vector3 feet = _stage.Camera.WorldToScreenPoint(walk.transform.position);
            int tileW = Mathf.RoundToInt(_stage.Height * StripTileWidth), tileH = Mathf.RoundToInt(_stage.Height * StripTileHeight);
            int tileX = Mathf.Clamp(Mathf.RoundToInt(feet.x) - tileW / 2, 0, _stage.Width - tileW);
            int tileY = Mathf.Clamp(Mathf.RoundToInt(feet.y - tileH * StripFeetFromBottom), 0, _stage.Height - tileH);
            var tiles = new System.Collections.Generic.List<byte[]>();

            var log = new StringBuilder();
            log.AppendLine($"player at {walk.transform.position}, screen {feet}, tile {tileW}x{tileH} at ({tileX},{tileY})");
            int shot = 0;
            s_nudges = 0;
            yield return HoldAndSettle(clock, figure, lib.Fidgets[0], 0.0);
            tiles.Add(Crop(_stage.Capture(), _stage.Width, tileX, tileY, tileW, tileH));   // the control: idle, straight
            foreach (IdleFidgetDef d in lib.Fidgets)
            {
                if (d == null) continue;
                byte[] peak = null;
                if (d.Kind == IdleFidgetDef.Channel.Clip)
                {
                    if (!d.AvailableFor(figure.SkinDef))
                    {
                        log.AppendLine($"{d.Id}: SKIPPED, the player's skin carries no '{d.ClipKey}' clip");
                        continue;
                    }
                    foreach (float f in ClipMoments)
                    {
                        double at = d.DurationSeconds * f;
                        yield return HoldAndSettle(clock, figure, d, at);
                        byte[] frame = _stage.Capture();
                        _stage.SavePlate($"{d.Id}-{f * 100f:00}pc-at-{at:0.0}s.png", frame);
                        log.AppendLine($"{d.Id} at {at:0.0} s: drawn '{figure.DrawnStateKey}' frame {figure.DrawnFrame}");
                        Assert.AreEqual(d.ClipKey, figure.DrawnStateKey, $"{d.Id} at {at:0.0} s: the figure is not playing the fidget's clip");
                        if (Mathf.Approximately(f, ClipPeak)) peak = frame;
                        shot++;
                    }
                }
                else
                {
                    // The control: the same fidget at its first key, which is the figure's own straight look.
                    yield return HoldAndSettle(clock, figure, d, 0.0);
                    double straight = figure.DrawnLookYaw;
                    _stage.SavePlate($"{d.Id}-0-control.png", _stage.Capture());

                    float most = -1f;
                    for (int k = 1; k < d.Gaze.Length - 1; k++)
                    {
                        IdleFidgetDef.GazeKey key = d.Gaze[k];
                        yield return HoldAndSettle(clock, figure, d, key.AtSeconds);
                        double yaw = figure.DrawnLookYaw, pitch = figure.DrawnLookPitch;
                        byte[] frame = _stage.Capture();
                        _stage.SavePlate($"{d.Id}-{k}-at-{key.AtSeconds:0.0}s.png", frame);
                        log.AppendLine($"{d.Id} key {k} at {key.AtSeconds:0.0} s: asked yaw {key.YawDegrees:0} up {key.UpDegrees:0}; " +
                                       $"drawn yaw {yaw:0.0} pitch {pitch:0.0} (straight {straight:0.0})");
                        string why = $"state '{figure.DrawnStateKey}' frame {figure.RequestedFrame}, base '{presenter.FidgetBaseStateKey}', " +
                                     $"idle {presenter.FidgetIdle}, fidget {(presenter.Fidget.Active ? presenter.Fidget.Def.Id : "none")} " +
                                     $"at {presenter.Fidget.LocalSeconds:0.00} s, skin {(figure.SkinDef != null ? figure.SkinDef.Id + " bones " + (figure.SkinDef.Bones != null ? figure.SkinDef.Bones.Length : -1) + " look " + figure.SkinDef.LookChestBone + "/" + figure.SkinDef.LookNeckBone + "/" + figure.SkinDef.LookHeadBone + " " + figure.SkinDef.HasLook : "none")}, " +
                                     $"head look {GameServices.CharacterHeadLook}, input {IdleFidgetInput.ActedRecently}, ashore {presenter.DrawsAshore}";
                        {
                            var probe = new CharacterFigureLife();
                            IsoCharacterFigureRenderer.Life pl = probe.Step(figure.SkinDef, string.Empty, 0u, clock.TotalSeconds, figure, looks: false, idle: true);
                            bool sampled = d.TrySampleGaze(key.AtSeconds, out float sy, out float su);
                            why += $"; probe target {pl.HasTarget} {pl.Target} head {pl.HeadLook} active {probe.Fidget.Active}; sample {sampled} {sy:0} {su:0}; drawnFrame {figure.DrawnFrame}; lib {(IdleFidgetLibraryDef.Shipped != null ? IdleFidgetLibraryDef.Shipped.LookDistanceMetres : -1f)}";
                        }
                        log.AppendLine("   " + why);
                        if (Mathf.Abs(key.YawDegrees) >= 10f)
                            Assert.AreEqual(Mathf.Sign(key.YawDegrees), System.Math.Sign(yaw),
                                $"{d.Id} key {k}: the head did not turn the way the fidget asks ({why})\n{log}");
                        float size = Mathf.Abs(key.YawDegrees) + Mathf.Abs(key.UpDegrees);
                        if (size > most) { most = size; peak = frame; }
                        shot++;
                    }
                }
                if (peak != null) tiles.Add(Crop(peak, _stage.Width, tileX, tileY, tileW, tileH));
            }

            byte[] strip = Concat(tiles, tileW, tileH);
            SavePng("strip-game-zoom.png", strip, tileW * tiles.Count, tileH);
            SavePng("strip-3x.png", Scale(strip, tileW * tiles.Count, tileH, 3), tileW * tiles.Count * 3, tileH * 3);
            Debug.Log($"[{PlateDir}] {shot} fidget plates and a {tiles.Count}-tile strip (control first, library order) at the player's figure, {s_nudges} clock nudges:\n{log}");
            Assert.Greater(shot, 0, "no fidget was photographed");
        }

        /// <summary>Where in a Clip fidget the plates are taken (fractions of its duration); the strip uses
        /// <see cref="ClipPeak"/>, the middle, where each of the three clips is at its fullest.</summary>
        private static readonly float[] ClipMoments = { 0.25f, 0.5f, 0.75f };
        private const float ClipPeak = 0.5f;
        /// <summary>A strip tile as fractions of the plate height, his feet this far up the tile.</summary>
        private const float StripTileWidth = 0.14f, StripTileHeight = 0.22f, StripFeetFromBottom = 0.15f;

        private static byte[] Crop(byte[] rgba, int width, int x0, int y0, int w, int h)
        {
            var o = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                System.Buffer.BlockCopy(rgba, ((y0 + y) * width + x0) * 4, o, y * w * 4, w * 4);
            return o;
        }

        private static byte[] Concat(System.Collections.Generic.List<byte[]> tiles, int w, int h)
        {
            int n = tiles.Count, rowBytes = w * n * 4;
            var o = new byte[rowBytes * h];
            for (int t = 0; t < n; t++)
                for (int y = 0; y < h; y++)
                    System.Buffer.BlockCopy(tiles[t], y * w * 4, o, y * rowBytes + t * w * 4, w * 4);
            return o;
        }

        private static byte[] Scale(byte[] rgba, int w, int h, int k)
        {
            var o = new byte[w * k * h * k * 4];
            for (int y = 0; y < h * k; y++)
                for (int x = 0; x < w * k; x++)
                    System.Buffer.BlockCopy(rgba, ((y / k) * w + x / k) * 4, o, (y * w * k + x) * 4, 4);
            return o;
        }

        private static void SavePng(string name, byte[] rgbaBottomLeft, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = System.IO.Path.Combine(Application.temporaryCachePath, PlateDir);
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, name);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        /// <summary>Hold one moment, let the idle clip beat a few times on a running clock, then stop the
        /// clock and engine time again for the capture.</summary>
        private static IEnumerator HoldAndSettle(IGameClock clock, IsoCharacterFigureRenderer figure, IdleFidgetDef def, double local)
        {
            IdleFidgetPreview.Hold(def, local);
            Time.timeScale = 1f;
            clock.TimeScale = 1f;
            // The look is sampled only on a beat (a new clip or a new frame asked for), so a frame count alone
            // can end before the idle clip has beaten once at a high frame rate. Run until it has beaten
            // twice since the hold, or the cap.
            string key = figure.DrawnStateKey;
            int asked = figure.RequestedFrame, beats = 0;
            float until = Time.realtimeSinceStartup + SettleCapSeconds;
            for (int i = 0; i < BeatFrames || (beats < SettleBeats && Time.realtimeSinceStartup < until); i++)
            {
                yield return null;
                if (figure.DrawnStateKey != key || figure.RequestedFrame != asked)
                {
                    key = figure.DrawnStateKey;
                    asked = figure.RequestedFrame;
                    beats++;
                }
                else if (i % NudgeEveryFrames == NudgeEveryFrames - 1)
                {
                    // The idle clip has not stepped (in this harness the clock can stand still on a running
                    // scale): move the game clock on a little so the clip beats and the held look is sampled.
                    clock.SeekTo(clock.TotalSeconds + NudgeSeconds);
                    s_nudges++;
                }
            }
            clock.TimeScale = 0f;
            yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }
    }
}
