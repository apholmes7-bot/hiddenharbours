using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A villager fishing off the St Peters wharf head</b> (ADR 0053 §11, PR 2 of the angler seam). Needs a
    /// graphics device and SKIPS on CI's Null device.
    ///
    /// <para><b>The plate.</b> St Peters at 15:30 on day 1 with the region's own seed: Basil is at the wharf head,
    /// his routine block names the wharf rod, and the first slot that both may begin and lands a fish is played
    /// through. The plate proves, on the drawn objects, a line leaving his rod tip, a splash at touchdown, a fish at
    /// the surface in the fight and the fish in his hand on the landing beat; then that seeking away and back to the
    /// same instant gives the same state, bit for bit.</para>
    ///
    /// <para><b>The clip</b> (explicit only, never part of a suite run): the same session framed with the player
    /// standing beside him, stepped one frame per 1/fps of the clock and written as numbered frames. The player's
    /// fishing in the clip is a puppet: his fishing states are published from the same pure timeline under his own
    /// key, so it is a picture of what the two presenters draw, not of live input.</para>
    ///
    /// <para>The hour is pinned on the clock's own time scale and the clock is then moved by seeks only. St Peters is
    /// the start scene, so the shell is cleared with <see cref="ShellFlow.Reset"/>, never a new game, which would
    /// write the save every worktree shares.</para>
    /// </summary>
    public class VillagerAnglerPlatePlayTests
    {
        private const string PlateDir = "villager-angler-plates";
        private const string ClipDirEnv = "HH_CLIP_DIR";
        private const string ClipDirName = "villager-angler-clip";
        private const string StPetersScene = "StPeters";
        private const string BasilKey = "npc.basil_samson";
        private const string PlayerPuppetKey = "player.clip_puppet";

        private const float PlateHour = 15.5f;
        private const float ClipHour = 17.25f;
        private const int SettleFrames = 90;
        private const int MaxSlotsSearched = 60;
        private const float StepSeconds = 1f / 30f;
        private const int ClipFps = 15;
        private const int ClipJpegQuality = 90;
        private const float ClipLeadSeconds = 1.5f;
        private const float ClipTailSeconds = 2f;
        private const float PlayerLeadSeconds = 3f;

        /// <summary>Where the plate's camera parks: this far along his facing from his feet, so his cast lands in
        /// frame.</summary>
        private const float FrameAheadMetres = 3f;
        /// <summary>The player stands this far to Basil's right (south of an east-facing man) in the clip.</summary>
        private static readonly Vector2 PlayerBesideBasil = new Vector2(-0.6f, -1.6f);

        /// <summary>The line leaves within this of the rod tip the figure drew (m).</summary>
        private const float TipToleranceMetres = 0.15f;

        private static string s_floor;

        private WharfNightStage _stage;
        private bool _loadedAny;
        private bool _introOnEntry;
        private NpcFigureStand _basil;
        private NpcAnglerPresenter _angler;
        private CharacterFigurePresenter _figure;
        private IGameClock _clock;
        private PlayerWalkController _player;
        private IsoCharacterSprite _playerIso;
        private Rigidbody2D _playerBody;
        private bool _playerBodyWasSimulated;
        private bool _publishedPlayerState;
        private string _healed = "none";

        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(s_floor, "Floor for " + nameof(VillagerAnglerPlatePlayTests) + " at " +
                                       DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                                       "\nOnly plates NEWER than this file were shot by this run.\n",
                              new UTF8Encoding(false));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            Time.captureFramerate = 0;
            if (_publishedPlayerState) EventBus.Publish(new FishingStateChanged(FishingState.Idle));
            _publishedPlayerState = false;
            if (_playerIso != null) _playerIso.ReleaseHeading();
            if (_playerBody != null) _playerBody.simulated = _playerBodyWasSimulated;
            _player = null;
            _playerIso = null;
            _playerBody = null;
            _basil = null;
            _angler = null;
            _figure = null;
            _clock = null;
            if (_stage != null)
            {
                yield return _stage.TearDown();
                _stage = null;
            }
            if (!_loadedAny) yield break;
            GameServices.Reset();
            GameServices.OpeningCinematicRunning = _introOnEntry;
            ShellFlow.Reset();
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plate
        // =============================================================================================

        [UnityTest]
        public IEnumerator Plate_BasilFishesTheWharfHead_LineSplashFightAndFishInHand_StPeters1530()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(PlateHour);
            yield return FindBasil();
            Vector2 at = _basil.transform.position;
            float heading = Heading();
            yield return _stage.FrameOn(at + Compass(heading) * FrameAheadMetres);

            Session s = null;
            yield return FindASessionWithAFish(PlateHour, found => s = found);
            Assert.IsNotNull(s, "harness: no slot with a fish was found inside his block");
            Debug.Log($"[{PlateDir}] seed {s.Seed}, slot {s.Slot} at {s.Start:0.00}s, catch {s.Catch.FishId} " +
                      $"{s.Catch.WeightKg:0.###} kg; phases {s.Describe()}; services healed: {_healed}");

            // 1. The cast: his release is scrubbed once through, the line is out.
            yield return SeekAndDraw(s.CastStart + 0.6d * s.Script.CastSeconds);
            AssertInSession(FishingPhase.Cast);
            Shoot("1-cast");

            // 2. Touchdown: stepped across the edge one frame at a time, so the splash fires on the edge.
            yield return StepThrough(s.WaitStart - 0.2d, s.WaitStart + 0.15d);
            AssertInSession(FishingPhase.Waiting);
            Assert.IsTrue(_angler.SplashRenderer.enabled, "no splash at touchdown");
            AssertTheLineLeavesTheRodTip("touchdown");
            Shoot("2-touchdown");

            // 3. The bite.
            yield return SeekAndDraw(s.BiteStart + 0.5d);
            AssertInSession(FishingPhase.Bite);
            AssertTheLineLeavesTheRodTip("bite");
            Shoot("3-bite");

            // 4. The fight at the surface: she is drawn on the line.
            yield return StepThrough(s.SurfaceStart - 0.2d, s.SurfaceStart + 1.2d);
            AssertInSession(FishingPhase.FightSurface);
            Assert.IsTrue(_angler.FishRenderer.enabled, "no fish at the surface in the fight");
            AssertTheLineLeavesTheRodTip("fight");
            Shoot("4-fight");

            // 5. The landing: past the lift, the fish is in his hand.
            double land = s.LandStart + s.Script.LandLiftSeconds + 0.3d;
            yield return SeekAndDraw(land);
            AssertInSession(FishingPhase.Landed);
            Assert.IsNotNull(_angler.Land?.Mesh, "the landed fish's mesh was never made");
            Assert.IsTrue(_angler.Land.Mesh.Drawing, "the landed fish is not in his hand");
            Assert.AreEqual("land", _figure.AshoreFigure != null ? _figure.AshoreFigure.DrawnStateKey : null,
                            "his figure is not on the land beat");
            Shoot("5-land");
            string first = Fingerprint();

            // 6. Away and back to the same instant: the same state.
            yield return SeekAndDraw(s.Start - 5d);
            yield return SeekAndDraw(land);
            string second = Fingerprint();
            Assert.AreEqual(first, second, "the same instant drew a different state the second time");
            Debug.Log($"[{PlateDir}] fingerprint at the landing: {first}");
        }

        // =============================================================================================
        //  The clip (explicit: frames for a video, never a suite case)
        // =============================================================================================

        [UnityTest, Explicit("Writes a few hundred frames for a video; run by name only.")]
        public IEnumerator Clip_BasilAndThePlayerFishTheWharfHead_GoldenHour()
        {
            WharfNightStage.RequireAGraphicsDevice();
            string env = System.Environment.GetEnvironmentVariable(ClipDirEnv);
            string dir = !string.IsNullOrEmpty(env) ? env : Path.Combine(Application.temporaryCachePath, ClipDirName);
            Directory.CreateDirectory(dir);

            yield return Arrive(ClipHour);
            yield return FindBasil();
            yield return FindThePlayer();
            Vector2 at = _basil.transform.position;
            float heading = Heading();
            PutThePlayerAt(at + PlayerBesideBasil, heading);
            yield return _stage.FrameOn(at + Compass(heading) * FrameAheadMetres + PlayerBesideBasil * 0.5f);
            Time.timeScale = 1f;                  // the player's presenter keeps its clocks on engine time
            Time.captureFramerate = ClipFps;      // which this makes one 1/fps step per frame

            Session s = null;
            yield return FindASessionWithAFish(ClipHour, found => s = found);
            Assert.IsNotNull(s, "harness: no slot with a fish was found inside his block");

            // The player's puppet: the same script under his own key, starting a little before Basil.
            AnglerScript script = s.Script;
            uint playerKey = FigureKeyHash.Of(PlayerPuppetKey);
            long playerSlot = 0;
            ShoreCatch playerCatch = default;
            for (; playerSlot < MaxSlotsSearched; playerSlot++)
                if (GameServices.ShoreCatch.TryPick(_basilAngling.CatchSpotId, s.Seed, playerKey, playerSlot,
                        AnglerTimeline.SlotStartSeconds(in script, s.Seed, playerKey, playerSlot), out playerCatch))
                    break;
            double playerStart = AnglerTimeline.SlotStartSeconds(in script, s.Seed, playerKey, playerSlot);

            double from = s.Start - ClipLeadSeconds - PlayerLeadSeconds;
            double to = s.LandStart + script.LandSeconds + ClipTailSeconds;
            var log = new StringBuilder();
            log.AppendLine($"seed {s.Seed}; Basil slot {s.Slot} at {s.Start:0.00}s, catch {s.Catch.FishId} {s.Catch.WeightKg:0.###} kg");
            log.AppendLine($"player puppet slot {playerSlot}, catch {playerCatch.FishId} {playerCatch.WeightKg:0.###} kg, " +
                           $"{PlayerLeadSeconds:0.#}s ahead of Basil");
            log.AppendLine($"phases: {s.Describe()}");
            int frame = 0;
            for (double t = from; t <= to; t += 1d / ClipFps, frame++)
            {
                _clock.SeekTo(t);
                double pt = playerStart + (t - (s.Start - PlayerLeadSeconds));
                AnglerTimeline.Evaluate(in script, in playerCatch, playerCatch.FishId != null, s.Seed, playerKey,
                                        heading, pt, out AnglerBeat pb);
                EventBus.Publish(new FishingStateChanged(pb.InSession ? pb.State : FishingState.Idle));
                _publishedPlayerState = true;
                yield return null;   // never WaitForEndOfFrame: it does not fire in batchmode
                byte[] rgba = _stage.Capture();
                string name = $"frame_{frame:0000}.jpg";
                SaveJpeg(Path.Combine(dir, name), rgba);
                _basil.TryGetAnglerState(out FishingState bs, out _);
                log.AppendLine($"{name} t={t - s.Start:+0.00;-0.00}s basil={bs.Phase} " +
                               $"key={(_figure.AshoreFigure != null ? _figure.AshoreFigure.DrawnStateKey : "-")} " +
                               $"line={_angler.DrawsLine} splash={_angler.SplashRenderer.enabled} " +
                               $"fish={_angler.FishRenderer.enabled} held={(_angler.Land?.Mesh?.Drawing ?? false)} " +
                               $"player={(pb.InSession ? pb.Phase.ToString() : "Idle")}");
            }
            EventBus.Publish(new FishingStateChanged(FishingState.Idle));
            File.WriteAllText(Path.Combine(dir, "clip-log.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] clip: {frame} frames at {ClipFps} fps written to {dir}");
            Assert.Greater(frame, ClipFps * 5, "the clip is too short to be a session");
        }

        // =============================================================================================
        //  The rig
        // =============================================================================================

        private sealed class Session
        {
            public int Seed;
            public long Slot;
            public AnglerScript Script;
            public ShoreCatch Catch;
            public double Start, CastStart, WaitStart, BiteStart, DeepStart, SurfaceStart, LandStart;

            public string Describe() => string.Format(CultureInfo.InvariantCulture,
                "cast +{0:0.00}, wait +{1:0.00}, bite +{2:0.00}, deep +{3:0.00}, surface +{4:0.00}, land +{5:0.00}",
                CastStart - Start, WaitStart - Start, BiteStart - Start, DeepStart - Start, SurfaceStart - Start,
                LandStart - Start);
        }

        private NpcAnglingActivityDef _basilAngling;

        private IEnumerator Arrive(float hour)
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;
            _loadedAny = true;
            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();

            if (GameServices.Clock == null)
            {
                var live = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
                if (live != null) { GameServices.Clock = live; _healed = "the region's clock republished"; }
            }
            // A suite's earlier teardown resets the services; the shore spots are published once per domain.
            if (GameServices.ShoreCatch is EmptyShoreCatchFacts)
            {
                var lib = Resources.Load<ShoreSpotLibrary>(ShoreCatchRegistrar.ResourcesPath);
                Assert.IsNotNull(lib, "harness: no shore spot library in Resources");
                GameServices.ShoreCatch = lib.BuildFacts();
                _healed = _healed == "none" ? "shore catch republished" : _healed + "; shore catch republished";
            }

            _clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(_clock, "harness: the region registered no clock");
            Assert.IsNotNull(config, "harness: the region registered no GameConfig");
            _clock.SeekTo((1.0 + hour / 24.0) * config.SecondsPerDay);
            _clock.TimeScale = 0f;
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        private IEnumerator FindBasil()
        {
            for (int f = 0; f < 120 && _basil == null; f++)
            {
                foreach (NpcFigureStand stand in Object.FindObjectsByType<NpcFigureStand>(FindObjectsSortMode.None))
                    if (stand.FigureKey == BasilKey) { _basil = stand; break; }
                if (_basil == null) yield return null;
            }
            Assert.IsNotNull(_basil, $"harness: St Peters has no {BasilKey}");
            _figure = _basil.GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(_figure, "Basil has no mesh figure");
            var routine = _basil.GetComponent<VillagerRoutine>();
            Assert.IsNotNull(routine, "Basil has no routine");
            for (int f = 0; f < 120 && _basil.Angling == null; f++) yield return null;
            _basilAngling = _basil.Angling;
            Assert.IsNotNull(_basilAngling,
                $"Basil is not fishing at {_clock.HourOfDay:0.00} h: block {routine.Pose.BlockIndex}, walking " +
                $"{routine.Pose.Walking}, at {(Vector2)_basil.transform.position}, shelter {routine.Pose.Shelter}");
            for (int f = 0; f < 120 && _angler == null; f++)
            {
                _angler = _basil.GetComponent<NpcAnglerPresenter>();
                if (_angler == null) yield return null;
            }
            Assert.IsNotNull(_angler, "the angler drawing was never attached to Basil");
        }

        private float Heading()
        {
            var routine = _basil.GetComponent<VillagerRoutine>();
            RoutinePlan plan = routine.Today;
            int block = routine.Pose.BlockIndex;
            return plan != null && block >= 0 && block < plan.StandHeadingDegrees.Length
                ? plan.StandHeadingDegrees[block] : routine.Pose.HeadingDegrees;
        }

        /// <summary>The first slot from now that he plays (the stand says so) and that lands a fish, with its phase
        /// edges read off the same pure timeline.</summary>
        private IEnumerator FindASessionWithAFish(float hour, Action<Session> found)
        {
            AnglerScript script = _basilAngling.Script;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            uint key = FigureKeyHash.Of(BasilKey);
            double now = (1.0 + hour / 24.0) * GameServices.Config.SecondsPerDay;
            long first = AnglerTimeline.SlotIndex(in script, seed, key, now) + 1;
            for (long slot = first; slot < first + MaxSlotsSearched; slot++)
            {
                double start = AnglerTimeline.SlotStartSeconds(in script, seed, key, slot);
                if (!GameServices.ShoreCatch.TryPick(_basilAngling.CatchSpotId, seed, key, slot, start, out ShoreCatch c))
                    continue;
                yield return SeekAndDraw(start + 0.5d);
                if (_basil.Angling == null) break;   // past his block
                if (!_basil.TryGetAnglerState(out _, out _) || _basil.Beat.Slot != slot) continue;

                var s = new Session { Seed = seed, Slot = slot, Script = script, Catch = c, Start = start };
                FishingPhase last = FishingPhase.Idle;
                for (double t = start; t < start + script.SlotSeconds; t += 0.01d)
                {
                    AnglerTimeline.Evaluate(in script, in c, true, seed, key, 90f, t, out AnglerBeat b);
                    if (b.Phase == last) continue;
                    if (b.Phase == FishingPhase.Cast) s.CastStart = b.PhaseStartSeconds;
                    else if (b.Phase == FishingPhase.Waiting) s.WaitStart = b.PhaseStartSeconds;
                    else if (b.Phase == FishingPhase.Bite) s.BiteStart = b.PhaseStartSeconds;
                    else if (b.Phase == FishingPhase.FightDeep) s.DeepStart = b.PhaseStartSeconds;
                    else if (b.Phase == FishingPhase.FightSurface) s.SurfaceStart = b.PhaseStartSeconds;
                    else if (b.Phase == FishingPhase.Landed) s.LandStart = b.PhaseStartSeconds;
                    last = b.Phase;
                }
                Assert.Greater(s.LandStart, s.Start, "harness: the session with a fish never landed it");
                found(s);
                yield break;
            }
            found(null);
        }

        private IEnumerator SeekAndDraw(double t)
        {
            _clock.SeekTo(t);
            for (int i = 0; i < 3; i++) yield return null;
            yield return null;   // never WaitForEndOfFrame: it does not fire in batchmode
        }

        /// <summary>One frame per step of the clock from <paramref name="from"/> to <paramref name="to"/>, so each
        /// presenter sees consecutive frames and fires its edges.</summary>
        private IEnumerator StepThrough(double from, double to)
        {
            for (double t = from; t <= to; t += StepSeconds)
            {
                _clock.SeekTo(t);
                yield return null;
            }
            yield return null;   // never WaitForEndOfFrame: it does not fire in batchmode
        }

        private void AssertInSession(FishingPhase phase)
        {
            Assert.IsTrue(_basil.TryGetAnglerState(out FishingState s, out _), "Basil is not in a session");
            Assert.AreEqual(phase, s.Phase, "harness: sampled in the wrong phase");
            Assert.IsTrue(_figure.WorkPropDrawn, "his rod is not drawn");
        }

        private void AssertTheLineLeavesTheRodTip(string beat)
        {
            Assert.IsTrue(_angler.DrawsLine, $"{beat}: no line is drawn");
            LineRenderer line = _angler.Line;
            Assert.IsNotNull(line, $"{beat}: the line was never built");
            Assert.Greater(line.positionCount, 1, $"{beat}: the line has no points");
            Vector2 start = line.GetPosition(0);
            Vector2 tip = _figure.WorkPropTipWorld;
            Assert.LessOrEqual(Vector2.Distance(start, tip), TipToleranceMetres,
                $"{beat}: the line starts at {start}, not at the rod tip {tip}");
        }

        private string Fingerprint()
        {
            _basil.TryGetAnglerState(out FishingState s, out float seconds);
            AnglerBeat b = _basil.Beat;
            var sb = new StringBuilder();
            sb.Append(b.Slot).Append('|').Append(b.Phase).Append('|').Append(b.StateKey).Append('|')
              .Append(Bits(b.Scrub01)).Append('|').Append(Bits(seconds)).Append('|').Append(s.FishId).Append('|')
              .Append(Bits(s.WeightKg)).Append('|').Append(Bits(s.FishOffsetX)).Append('|').Append(Bits(s.FishOffsetY));
            sb.Append("|fig:").Append(_figure.AshoreFigure != null ? _figure.AshoreFigure.DrawnStateKey : "-")
              .Append(':').Append(_figure.AshoreFigure != null ? _figure.AshoreFigure.DrawnFrame : -1);
            sb.Append("|held:").Append(_angler.Land?.Mesh?.Drawing ?? false).Append(':').Append(_angler.Land?.Socket);
            Vector2 tip = _figure.WorkPropTipWorld;
            sb.Append("|tip:").Append(Bits(tip.x)).Append(',').Append(Bits(tip.y));
            return sb.ToString();
        }

        private static string Bits(float f) => BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture);

        private void Shoot(string name)
        {
            byte[] rgba = _stage.Capture();
            _stage.SavePlate($"{name}.png", rgba);
        }

        private void SaveJpeg(string path, byte[] rgba)
        {
            var tex = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToJPG(ClipJpegQuality));
            Object.DestroyImmediate(tex);
        }

        private IEnumerator FindThePlayer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                Transform published = GameServices.PlayerTransform;
                _player = published != null ? published.GetComponent<PlayerWalkController>() : null;
                if (_player == null) yield return null;
            }
            Assert.IsNotNull(_player, "harness: the region has no on-foot player");
            _playerIso = _player.GetComponent<IsoCharacterSprite>();
            _playerBody = _player.GetComponent<Rigidbody2D>();
        }

        private void PutThePlayerAt(Vector2 at, float headingDegrees)
        {
            if (_playerBody != null)
            {
                _playerBodyWasSimulated = _playerBody.simulated;
                _playerBody.linearVelocity = Vector2.zero;
                _playerBody.position = at;
                _playerBody.simulated = false;
            }
            Vector3 was = _player.transform.position;
            _player.transform.position = new Vector3(at.x, at.y, was.z);
            if (_playerIso != null) _playerIso.HoldHeading(headingDegrees);
        }

        private static Vector2 Compass(float headingDegrees)
        {
            float r = headingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }
    }
}
