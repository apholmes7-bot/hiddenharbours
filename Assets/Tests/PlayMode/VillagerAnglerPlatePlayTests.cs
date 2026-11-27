using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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
        private const string StPetersScene = "StPeters";
        private const string BasilKey = "npc.basil_samson";
        private const string PlayerPuppetKey = "player.clip_puppet";

        private const float PlateHour = 15.5f;
        private const int SettleFrames = 90;
        private const int MaxSlotsSearched = 60;
        private const float StepSeconds = 1f / 30f;
        private const float ClipLeadSeconds = 1.5f;
        private const float ClipTailSeconds = 2f;
        private const float PlayerLeadSeconds = 3f;

        // ---- the clip (take 3): 18:30 on the clearest evening, close, at video rate ----
        /// <summary>The hour the region is entered at before the weather pick moves the clock.</summary>
        private const float ClipArriveHour = 18.5f;
        /// <summary>The hour asked for: inside the warm key (from about 18:04) and his wharf block (to 19:30).</summary>
        private static readonly float[] ClipHours = { 18.5f };
        private const int ClipDaysSearched = 60;
        private const int ClipCandidatesTried = 12;
        private const int ClipFps = 30;
        private const int ClipJpegQuality = 92;
        /// <summary>The clip writes ~25 s at 30 fps in full HD; the runner's 180 s default cuts it off.</summary>
        private const int ClipTimeoutMs = 1_200_000;
        /// <summary>The design screen: the game's closest walking stop lands on a whole pixel step here.</summary>
        private const int ClipWidthPx = 1920, ClipHeightPx = 1080;
        /// <summary>The player stands this far to Basil's right, on the same plank row at the edge, both facing his
        /// water (right: the barrel on the back curb stands to his left).</summary>
        private const float PlayerRightOfBasilMetres = 1.8f;
        /// <summary>The frame's centre is this far out over the water from the anglers' feet: the two of them low in
        /// frame, the water they fight their fish in above them.</summary>
        private const float ClipWaterAheadMetres = 1.8f;

        /// <summary>Where the plate's camera parks: this far along his facing from his feet, so his cast lands in
        /// frame.</summary>
        private const float FrameAheadMetres = 3f;

        /// <summary>He stands within this of the spot's authored stand (m): the hold may ease him the last step.</summary>
        private const float StandToleranceMetres = 0.05f;
        /// <summary>The fight is probed for "over the water" this often (s of the clock).</summary>
        private const double FightProbeSeconds = 0.25d;
        /// <summary>The edge profile is sampled this far north of the stand, this finely (m).</summary>
        private const float EdgeProfileMetres = 2f, EdgeProfileStepMetres = 0.05f;

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
            ReleaseTheClipTargets();
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

            // He fishes from the spot's own stand, on the planks, facing out over the water.
            Assert.IsTrue(_basilAngling.TryGetStand(out ShoreStand stand), "the wharf spot publishes no stand");
            Assert.LessOrEqual(Vector2.Distance(at, stand.Position), StandToleranceMetres,
                               $"Basil fishes from {at}, not the spot's stand {stand.Position}");
            Assert.AreEqual(stand.HeadingDegrees, heading, "he does not face the stand's heading");
            Assert.IsTrue(StandableSurfaces.TryGetDeckElevationNow(at, out _), $"the stand {at} is not on the deck");
            Assert.IsTrue(TidalWalkability.IsWalkableNow(at), $"the stand {at} is not walkable");
            LogTheEdgeProfile(at);

            Session s = null;
            yield return FindASessionWithAFish((1.0 + PlateHour / 24.0) * GameServices.Config.SecondsPerDay,
                                               found => s = found);
            Assert.IsNotNull(s, "harness: no slot with a fish was found inside his block");
            Debug.Log($"[{PlateDir}] seed {s.Seed}, slot {s.Slot} at {s.Start:0.00}s, catch {s.Catch.FishId} " +
                      $"{s.Catch.WeightKg:0.###} kg; phases {s.Describe()}; services healed: {_healed}");

            // The whole fight, off his own state: the fish is over the water at every step, never on the deck.
            for (double t = s.DeepStart; t < s.LandStart; t += FightProbeSeconds)
            {
                yield return SeekAndDraw(t);
                Assert.IsTrue(_basil.TryGetAnglerState(out FishingState fs, out _), "harness: not in a session mid-fight");
                AssertOverWater(at + new Vector2(fs.FishOffsetX, fs.FishOffsetY), $"fight +{t - s.Start:0.00}s");
            }

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

            // 2b. She comes in: over the last of his wait, one fish swims to the bait, under the water.
            // Shot on her way in, at the flank flash as she turns into the circle, mid-circle and as she hangs.
            AnglerApproachLook a = _basil.Look.Approach;
            float approach = a.Seconds;
            if (approach > 0f)
            {
                float tIn = AnglerApproachMath.SwimInSeconds(in a), circleEnd = tIn + a.CircleSeconds;
                var beats = new (string name, float tau)[]
                {
                    ("2b-approach-in", 0.6f * tIn), ("2b-approach-roll", tIn + 0.2f),
                    ("2b-approach-circle", tIn + 0.6f * a.CircleSeconds), ("2b-approach-hang", circleEnd + 0.7f),
                };
                foreach (var (name, tau) in beats)
                {
                    double t = s.BiteStart - approach + tau;
                    yield return StepThrough(t - 0.1d, t);
                    AssertInSession(FishingPhase.Waiting);
                    Assert.IsTrue(_angler.ApproachRenderer.enabled, $"no fish coming in to his bait ({s.Catch.FishId}, {name})");
                    AssertOverWater(_angler.ApproachRenderer.transform.position, $"the fish coming in ({name})");
                    Shoot(name);
                }
            }

            // 3. The bite.
            yield return SeekAndDraw(s.BiteStart + 0.5d);
            AssertInSession(FishingPhase.Bite);
            AssertTheLineLeavesTheRodTip("bite");
            AssertOverWater(_angler.Line.GetPosition(_angler.Line.positionCount - 1), "bite (the line's end)");
            if (approach > 0f) Assert.IsTrue(_angler.ApproachRenderer.enabled, "she left the bait before the strike");
            Shoot("3-bite");

            // 3b. The deep fight: she is drawn under the water where the line ends.
            if (approach > 0f)
            {
                yield return StepThrough(s.DeepStart, s.DeepStart + 0.15d);
                AssertInSession(FishingPhase.FightDeep);
                Assert.IsTrue(_angler.ApproachRenderer.enabled, "no fish under the water at the strike");
                Shoot("3a-strike");
                yield return StepThrough(s.DeepStart + 1.0d, s.DeepStart + 1.1d);
                AssertInSession(FishingPhase.FightDeep);
                Assert.IsTrue(_angler.ApproachRenderer.enabled, "no fish under the water in the deep fight");
                AssertOverWater(_angler.ApproachRenderer.transform.position, "the deep fight (the drawn fish)");
                Shoot("3b-deep");
            }

            // 4. The fight at the surface: she is drawn on the line, over the water (early, mid and late).
            yield return StepThrough(s.SurfaceStart - 0.2d, s.SurfaceStart + 0.3d);
            AssertInSession(FishingPhase.FightSurface);
            LogTheSurfacedFish("4a-fight-break");
            Shoot("4a-fight-break");
            yield return StepThrough(s.SurfaceStart + 0.3d, s.SurfaceStart + 1.2d);
            AssertInSession(FishingPhase.FightSurface);
            Assert.IsTrue(_angler.FishRenderer.enabled, "no fish at the surface in the fight");
            AssertTheLineLeavesTheRodTip("fight");
            AssertOverWater(_angler.FishRenderer.transform.position, "mid-fight (the drawn fish)");
            LogTheSurfacedFish("4-fight");
            Shoot("4-fight");
            double late = s.SurfaceStart + 0.8d * (s.LandStart - s.SurfaceStart);
            yield return StepThrough(late - 0.1d, late);
            AssertInSession(FishingPhase.FightSurface);
            LogTheSurfacedFish("4c-fight-late");
            Shoot("4c-fight-late");

            // 4b. The landing splash: stepped across the edge, so it fires where she leaves the water.
            //     Unless that point is drawn inside his figure: then no rings at all (never round his waist).
            yield return StepThrough(s.LandStart - 0.3d, s.LandStart - StepSeconds);
            AssertInSession(FishingPhase.FightSurface);
            Assert.IsTrue(_basil.TryGetAnglerState(out FishingState lastUp, out _), "harness: not in a session");
            Vector2 feet = _angler.transform.position;
            Vector2 waterPoint = feet + new Vector2(lastUp.FishOffsetX, lastUp.FishOffsetY);
            bool onHim = RodPresenterMath.InFisherFigure(waterPoint, feet, LandFeel().FisherFigureHalfWidthM,
                                                         LandFeel().FisherFigureHeightM);
            yield return StepThrough(s.LandStart - StepSeconds, s.LandStart + 0.1d);
            AssertInSession(FishingPhase.Landed);
            if (onHim) Assert.IsFalse(_angler.SplashRenderer.enabled, "landing rings drawn round his figure");
            else
            {
                Assert.IsTrue(_angler.SplashRenderer.enabled, "no splash where she is lifted out");
                AssertOverWater(_angler.SplashRenderer.transform.position, "the landing splash");
            }
            Debug.Log($"[{PlateDir}] landing water point {waterPoint} (feet {feet}): " +
                      (onHim ? "inside his figure, no rings" : "clear of him, rings"));

            // 4c. Out of the water she hangs on the line, whole and in front of him, before his hand takes her,
            //     beside his figure, never over it.
            yield return StepThrough(s.LandStart + 0.1d, s.LandStart + LandFeel().LandLiftTravelSeconds + 0.1d);
            AssertInSession(FishingPhase.Landed);
            Assert.IsTrue(_angler.FishRenderer.enabled, "she is not seen on the line as she hangs");
            Assert.Greater(_angler.FishRenderer.sortingOrder, _basil.GetComponent<SpriteRenderer>().sortingOrder,
                           "lifted, she hangs in front of him");
            Assert.Greater(Mathf.Abs(_angler.FishRenderer.transform.position.x - feet.x), LandFeel().FisherFigureHalfWidthM,
                           "she hangs over his figure");
            StringAssert.Contains("_gill", _angler.FishRenderer.sprite.name, "she does not hang head-up by the gill");
            Assert.IsFalse(_angler.Land?.Mesh != null && _angler.Land.Mesh.Drawing,
                           "she is in his hand while she still hangs on the line");

            // 5. The landing: past the lift and the hang, the fish is in his hand.
            double land = s.LandStart + LandedSeen(s) + 0.3d;
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
        //  The tide plates: he casts into water, never onto the bared bed
        // =============================================================================================

        /// <summary>Days searched for the three tides inside his wharf block.</summary>
        private const int TideDaysSearched = 120;
        /// <summary>How far out along his bearing the dead-low log looks for the water, and in what steps (m).</summary>
        private const float WaterProfileMetres = 60f, WaterProfileStepMetres = 0.5f;

        private static string WaterBeginsAlong(Vector2 at, Vector2 dir, AnglerWaterMath.DepthAt depth, double t)
        {
            for (float r = 0f; r <= WaterProfileMetres; r += WaterProfileStepMetres)
                if (depth(at + dir * r, t) > 0f) return $"{r:0.0} m out";
            return $"beyond {WaterProfileMetres:0} m";
        }

        /// <summary>The ground along his bearing against the drawn water level, logged beside each tide plate.</summary>
        private static string BedProfile(Vector2 at, Vector2 dir, float drawnLevel)
        {
            var sb = new StringBuilder();
            foreach (float m in new[] { 0f, 2f, 4f, 7f, 9f, 12f, 16f })
            {
                float bed = GameServices.TidalTerrain.ElevationAt(at + dir * m);
                sb.Append($"\n    {m,3:0} m out: bed {bed:0.00} m, drawn depth {drawnLevel - bed:0.00} m");
            }
            return sb.ToString();
        }

        /// <summary>How close the drawn sea level must come to the tide's before a tide plate is shot (m): the
        /// sea's own ease trails a moving tide by under 2 cm.</summary>
        private const float DrawnLevelToleranceMetres = 0.02f;
        /// <summary>The most real seconds the sea is given to catch up with a seek.</summary>
        private const float SeaCatchUpSeconds = 5f;

        /// <summary>The water level the region's sea is drawing now (its renderer's own block).</summary>
        private static float DrawnSeaLevel()
        {
            var sea = Object.FindFirstObjectByType<WaterSurface>();
            Assert.IsNotNull(sea, "harness: the region has no sea");
            var block = new MaterialPropertyBlock();
            sea.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetFloat("_WaterLevel");
        }

        /// <summary>Where the surfaced fish is drawn and with what, logged beside a fight plate.</summary>
        private void LogTheSurfacedFish(string name)
        {
            SpriteRenderer f = _angler.FishRenderer;
            Vector3 line = _angler.Line.GetPosition(_angler.Line.positionCount - 1);
            Debug.Log($"[{PlateDir}] {name}: fish drawn {f.enabled}, sprite {(f.sprite != null ? f.sprite.name : "none")}, " +
                      $"at {f.transform.position}, scale {f.transform.lossyScale}, bounds {f.bounds.size}, order {f.sortingOrder}, " +
                      $"colour {f.color}; line ends {line}; the fish under the water drawn {_angler.ApproachRenderer.enabled}");
        }
        /// <summary>Run engine time with the clock held on <paramref name="t"/> until the drawn sea is at the tide's
        /// level, then freeze again (the plate laws: engine time runs only between pictures).</summary>
        private IEnumerator LetTheSeaCatchUp(double t)
        {
            float target = GameServices.Environment.WaterLevelAt(t);
            float until = Time.realtimeSinceStartup + SeaCatchUpSeconds;
            Time.timeScale = 1f;
            int settled = 0;
            while (Time.realtimeSinceStartup < until && settled < 3)
            {
                _clock.SeekTo(t);
                yield return null;
                settled = Mathf.Abs(DrawnSeaLevel() - target) <= DrawnLevelToleranceMetres * 0.5f ? settled + 1 : 0;
            }
            Time.timeScale = 0f;
            _clock.SeekTo(t);
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Inside his wharf block (15:00 to 19:30), clear of his walk in and out.</summary>
        private const float TideBlockFromHour = 15.75f, TideBlockToHour = 19.0f;
        /// <summary>Into the wait: the bobber is down and still.</summary>
        private const double TideShotIntoWaitSeconds = 1.5d;
        /// <summary>Candidates tried per tide before the harness gives up (his routine or the weather may keep
        /// him from a slot the tide alone would allow).</summary>
        private const int TideCandidatesTried = 12;

        [UnityTest, Timeout(600000)]
        public IEnumerator Plate_BasilCastsIntoWater_HighWater_ALongCastAtMidWater_AndNoCastAtDeadLow()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(PlateHour);
            yield return FindBasil();
            Vector2 at = _basil.transform.position;
            float heading = Heading();
            AnglerScript script = _basilAngling.Script;
            Assert.IsTrue(_basilAngling.TryGetStand(out ShoreStand stand), "the wharf spot publishes no stand");
            ShoreEdge edge = stand.Edge.RelativeTo(stand.Position);
            IEnvironmentService env = GameServices.Environment;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(env, "harness: no environment service");
            Assert.IsNotNull(terrain, "harness: St Peters published no tidal ground");
            int seed = env.WorldSeed;
            uint key = FigureKeyHash.Of(BasilKey);
            AnglerWaterMath.DepthAt depth = (p, t) => TidalExposure.WaterDepth(env.WaterLevelAt(t), terrain.ElevationAt(p));

            // The three tides, picked off the pure plan over his block (slot starts on the plate's clock, which the
            // seeks keep equal to the tide's).
            double spd = GameServices.Config.SecondsPerDay;
            var highs = new List<(long slot, double start, float level)>();
            var mids = new List<(long slot, double start, float level)>();
            var lows = new List<(long slot, double start, float level)>();
            var log = new StringBuilder();
            for (int day = 1; day <= TideDaysSearched; day++)
            {
                double from = (day + TideBlockFromHour / 24.0) * spd, to = (day + TideBlockToHour / 24.0) * spd;
                for (long slot = AnglerTimeline.SlotIndex(in script, seed, key, from) + 1;
                     AnglerTimeline.SlotStartSeconds(in script, seed, key, slot) + script.SlotSeconds < to; slot++)
                {
                    double start = AnglerTimeline.SlotStartSeconds(in script, seed, key, slot);
                    if (env.SeaState01At(start) > _basilAngling.MaxSeaState01) continue;
                    AnglerWater w = AnglerWaterMath.Plan(in script, at,
                        AnglerTimeline.CastDirection(in script, seed, key, slot, heading), in edge, start, depth);
                    float level = env.WaterLevelAt(start);
                    bool stays = double.IsPositiveInfinity(w.DryAtSeconds);
                    if (w.Casts && !w.IsLongCast(in script) && stays) highs.Add((slot, start, level));
                    // A long cast the tide dries later in the session still shows its bobber down at the shot.
                    else if (w.IsLongCast(in script) && w.DryAtSeconds > script.CastBackSeconds + script.CastSeconds
                             + TideShotIntoWaitSeconds) mids.Add((slot, start, level));
                    else if (!w.Casts) lows.Add((slot, start, level));
                }
            }
            highs.Sort((a, b) => b.level.CompareTo(a.level));   // the highest water first
            lows.Sort((a, b) => a.level.CompareTo(b.level));    // the lowest first
            // The mid tide: the long casts nearest the middle of their range of levels.
            if (mids.Count > 0)
            {
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                foreach (var m in mids) { lo = Mathf.Min(lo, m.level); hi = Mathf.Max(hi, m.level); }
                float centre = 0.5f * (lo + hi);
                mids.Sort((a, b) => Mathf.Abs(a.level - centre).CompareTo(Mathf.Abs(b.level - centre)));
            }
            log.AppendLine($"candidates over {TideDaysSearched} days: {highs.Count} usual casts, {mids.Count} long casts, " +
                           $"{lows.Count} slots with no water in reach");

            bool shot = false;
            foreach (var (name, list, casts) in new[] { ("tide-1-high", highs, true), ("tide-2-mid-long-cast", mids, true),
                                                        ("tide-3-dead-low-no-cast", lows, false) })
            {
                shot = false;
                for (int i = 0; i < Mathf.Min(TideCandidatesTried, list.Count) && !shot; i++)
                    yield return ShootTheTide(name, list[i].slot, list[i].start, at, heading, depth, script, log, casts,
                                              done => shot = done);
                Assert.IsTrue(shot, $"harness: none of the first {TideCandidatesTried} of {list.Count} {name} slots was " +
                                    $"played by his stand\n{log}");
            }

            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            File.WriteAllText(Path.Combine(dir, "tide-log.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] tides:\n{log}");
        }

        private IEnumerator ShootTheTide(string name, long slot, double start, Vector2 at, float heading,
                                         AnglerWaterMath.DepthAt depth, AnglerScript script, StringBuilder log, bool casts,
                                         Action<bool> done)
        {
            double shot = start + script.CastBackSeconds + script.CastSeconds + TideShotIntoWaitSeconds;
            yield return SeekAndDraw(shot);
            // Only a slot his stand is playing (or standing down for want of water) is a plate of the tide.
            bool playing = _basil.Angling != null && _basil.Beat.Slot == slot
                           && (!casts || _basil.TryGetAnglerState(out _, out _));
            if (!playing)
            {
                log.AppendLine($"{name}: slot {slot} at hour {_clock.HourOfDay:0.00} skipped (angling " +
                               $"{_basil.Angling != null}, beat slot {_basil.Beat.Slot}, in session {_basil.TryGetAnglerState(out _, out _)})");
                done(false);
                yield break;
            }
            AnglerWater w = _basil.Water;
            Assert.IsTrue(w.IsSet, $"{name}: his stand read no water plan");
            Assert.AreEqual(casts, w.Casts, $"{name}: the stand's plan disagrees with the harness's pick");
            Vector2 dir = AnglerTimeline.CastDirection(in script, GameServices.Environment.WorldSeed,
                                                       FigureKeyHash.Of(BasilKey), slot, heading);
            float level = GameServices.Environment.WaterLevelAt(shot);
            if (casts)
            {
                AssertInSession(FishingPhase.Waiting);
                Assert.AreEqual(slot, _basil.Beat.Slot, $"{name}: sampled the wrong slot");
                _basil.TryGetAnglerState(out FishingState s, out _);
                Vector2 bobber = at + new Vector2(s.CastAimX, s.CastAimY);
                Vector2 lineEnd = _angler.Line.GetPosition(_angler.Line.positionCount - 1);
                float d = depth(bobber, shot);
                log.AppendLine($"{name}: slot {slot} at {start:0.0}s (hour {(_clock.HourOfDay):0.00}), level {level:0.00} m, " +
                               $"reach {w.ReachM:0.00} m (usual {script.CastReachM}, long {w.IsLongCast(in script)}), " +
                               $"bobber {bobber} depth {d:0.00} m at the shot " +
                               $"({depth(bobber, start + script.CastBackSeconds + script.CastSeconds):0.00} m at touchdown), line end {lineEnd} depth {depth(lineEnd, shot):0.00} m, " +
                               $"waterline {(w.Waterline.IsSet ? (w.Waterline.Point.magnitude.ToString("0.00") + " m out") : "none")}");
                double touchdown = start + script.CastBackSeconds + script.CastSeconds;
                Assert.Greater(depth(bobber, touchdown), script.MinBobberDepthM,
                               $"{name}: the bobber at {bobber} landed in water too shallow");
                Assert.Greater(d, 0f, $"{name}: the bobber at {bobber} is on dry ground at the shot");
                Assert.Greater(depth(lineEnd, shot), 0f, $"{name}: the drawn line ends on dry ground at {lineEnd}");
                AssertOverWater(bobber, name);
                yield return _stage.FrameOn(at + dir * (w.ReachM * 0.5f));
            }
            else
            {
                Assert.IsFalse(_basil.TryGetAnglerState(out _, out _), $"{name}: he is in a session with no water in reach");
                Assert.IsTrue(_basil.TryGetFigureBeat(out string beat, out _, out _, out _), $"{name}: he does not hold his rod");
                Assert.AreEqual(FishingBeatMath.StateKeyFor(FishingPose.Hold), beat, $"{name}: not standing down, rod up");
                Vector2 usual = at + edge(at, dir, script.CastReachM);
                Vector2 longest = at + edge(at, dir, script.LongCastReachM);
                log.AppendLine($"{name}: slot {slot} at {start:0.0}s (hour {(_clock.HourOfDay):0.00}), level {level:0.00} m, " +
                               $"no cast: depth at the usual reach {depth(usual, shot):0.00} m, at the long reach " +
                               $"{depth(longest, shot):0.00} m; he holds the rod; along his bearing the water begins " +
                               $"{WaterBeginsAlong(at, dir, depth, shot)}");
                Assert.IsFalse(_angler.DrawsLine, $"{name}: a line is drawn with no cast");
                yield return _stage.FrameOn(at + dir * (script.CastReachM * 0.5f));
            }
            yield return SeekAndDraw(shot);
            // The sea samples the tide on engine time, which the plate's freeze stops: run engine time on the held
            // instant until the drawn level is the tide's, or the picture shows the last plate's water.
            yield return LetTheSeaCatchUp(shot);
            float drawn = DrawnSeaLevel();
            log.AppendLine($"  drawn sea level {drawn:0.000} m against the tide's {level:0.000} m" +
                           $"{BedProfile(at, dir, drawn)}");
            Assert.AreEqual(level, drawn, DrawnLevelToleranceMetres, $"{name}: the sea is drawn at another level than the tide");
            // He casts if and only if the bobber lands on drawn water.
            Vector2 landsAt = at + edge(at, dir, casts ? w.ReachM : script.CastReachM);
            float drawnDepth = drawn - GameServices.TidalTerrain.ElevationAt(landsAt);
            if (casts) Assert.Greater(drawnDepth, 0f, $"{name}: he cast onto ground the sea is not drawn over");
            else
                foreach (float r in new[] { script.CastReachM, script.LongCastReachM })
                    Assert.LessOrEqual(drawn - GameServices.TidalTerrain.ElevationAt(at + edge(at, dir, r)), 0f,
                                       $"{name}: he stood down with drawn water at {r} m");
            Shoot(name);
            done(true);

            Vector2 edge(Vector2 origin, Vector2 direction, float reach)
                => _basilAngling.TryGetStand(out ShoreStand st) ? st.Edge.RelativeTo(st.Position).Clamp(direction * reach)
                                                                : direction * reach;
        }

        // =============================================================================================
        //  The fight in the light the media clip will use: clear, golden hour, a +2 m tide
        // =============================================================================================

        /// <summary>The tide the golden plates are shot at (m) and how far from it a slot may be.</summary>
        private const float GoldenTideMetres = 2f, GoldenTideToleranceMetres = 0.4f;
        /// <summary>The air counts as clear at this visibility or better.</summary>
        private const float ClearVisibility01 = 0.9f;
        /// <summary>Golden hour is short and the tide must be high in it: a longer search than the tide plate's. Ten
        /// 112-day years: in 360 days one warm-key mackerel slot passed every gate, on a day his routine had him
        /// elsewhere, so the clip's fish needs the longer look.</summary>
        private const int GoldenDaysSearched = 1120;
        /// <summary>The golden plates' names start with this, so they never overwrite the 15:30 plate's.</summary>
        private const string GoldenPrefix = "g-";
        /// <summary>When the warm key's hour cannot be read, golden hour is taken to start here.</summary>
        private const float GoldenFallbackFromHour = 18f;
        /// <summary>The search (and its logged table) opens here, before the warm key, so an earlier, bigger fish
        /// is seen beside the warm-light ones.</summary>
        private const float GoldenSearchFromHour = 17f;
        /// <summary>How many of the best slots the golden log lists.</summary>
        private const int GoldenSlotsListed = 10;
        /// <summary>The search closes here (19:20): the end of the warm light the clip wants, inside his
        /// 19:30 block end and before the sunset glow.</summary>
        private const float GoldenSearchToHour = 19f + 20f / 60f;
        /// <summary>The fish the clip wants in the warm key (coordinator 2026-11-26): ranked first.</summary>
        private const string GoldenTargetFishId = "fish.mackerel";
        /// <summary>How many target-fish slots the log names as the top.</summary>
        private const int GoldenTopListed = 3;
        /// <summary>The clip's sea: at or under this the water keeps its own colour (at 0.55 a third of the
        /// storm-grey preset is blended in, too dim for the clip). The pick takes a played target slot in the
        /// warm key at this sea first, then a played target slot from <see cref="GoldenClipFallbackFromHour"/>.</summary>
        private const float GoldenClipMaxSeaState01 = 0.45f;
        /// <summary>The clip's earliest hour when no warm-key slot at the clip's sea is played (17:20).</summary>
        private const float GoldenClipFallbackFromHour = 17f + 20f / 60f;
        /// <summary>How many target-fish slots in the warm key are each tried on his real stand and logged.</summary>
        private const int GoldenVerdictsListed = 24;
        /// <summary>An hour of his day outside his angling block (12:00 to 15:00 he is at the slip head with no
        /// rod). The search seeks here first on each slot's day, so his stand drops the last run's window as it
        /// does when he walks off the wharf at dusk: a seek from one day's angling block straight into another's
        /// keeps the old day's window, and no slot of the new day may begin in it.</summary>
        private const float GoldenOffBlockHour = 13f;
        private const string GoldenPlayed = "played";

        [UnityTest, Timeout(900000)]
        public IEnumerator Plate_TheSurfacedFightAndTheLanding_ClearGoldenHour_HighWater()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(ClipArriveHour);
            yield return FindBasil();
            Vector2 at = _basil.transform.position;
            float heading = Heading();
            var weather = GameServices.Environment as HiddenHarbours.Environment.EnvironmentService;
            Assert.IsNotNull(weather, "harness: the region registered no environment service");
            AnglerScript script = _basilAngling.Script;
            int seed = weather.WorldSeed;
            uint key = FigureKeyHash.Of(BasilKey);
            double spd = GameServices.Config.SecondsPerDay;
            float warm = WarmKeyHour();
            float warmFrom = float.IsNaN(warm) ? GoldenFallbackFromHour : warm;
            float fromHour = Mathf.Max(GoldenSearchFromHour, TideBlockFromHour);
            var log = new StringBuilder();

            // Why the target fish does or does not come up in the warm key: every slot of his in the warm
            // window, put down by the FIRST gate it fails, in the order the game applies them, off the same
            // data the roll reads (no data is changed here).
            var spotFacts = GameServices.ShoreCatch as ShoreCatchFacts;
            ShoreSpotDef spot = spotFacts?.Spot(_basilAngling.CatchSpotId);
            FishSpeciesDef target = FishSpeciesRegistry.Get(GoldenTargetFishId);
            var gates = new SortedDictionary<string, int>(StringComparer.Ordinal);
            void Gate(string why) => gates[why] = gates.TryGetValue(why, out int g) ? g + 1 : 1;
            var seasonsSeen = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int warmSlots = 0;
            double targetShareSum = 0d;
            int targetShareSlots = 0;

            // The pick, off the pure models: a slot in his block from the search hour, with a fish, clear air, a
            // sea he fishes in, and the tide within reach of +2 m through the fight. Ranked: the target fish in
            // the warm key first, then anything in the warm key, then the biggest fish (the one the camera
            // reads), then the tide nearest +2 m.
            var picks = new List<(long slot, double start, float vis, float sea, float level, float kg, string id, float hour, bool warmKey)>();
            for (int day = 1; day <= GoldenDaysSearched; day++)
            {
                double from = (day + fromHour / 24.0) * spd, to = (day + GoldenSearchToHour / 24.0) * spd;
                for (long slot = AnglerTimeline.SlotIndex(in script, seed, key, from) + 1;
                     AnglerTimeline.SlotStartSeconds(in script, seed, key, slot) + script.SlotSeconds < to; slot++)
                {
                    double start = AnglerTimeline.SlotStartSeconds(in script, seed, key, slot);
                    float hour = (float)((start / spd - System.Math.Floor(start / spd)) * 24d);
                    bool warmKey = hour >= warmFrom;
                    bool picked = GameServices.ShoreCatch.TryPick(_basilAngling.CatchSpotId, seed, key, slot, start,
                                                                  out ShoreCatch pickFish);
                    float sea = weather.SeaState01At(start), vis = weather.VisibilityAt(start);
                    float level = GameServices.Environment.WaterLevelAt(start + 0.5d * script.SlotSeconds);
                    bool weatherOk = sea <= _basilAngling.MaxSeaState01 && vis >= ClearVisibility01;
                    bool tideOk = Mathf.Abs(level - GoldenTideMetres) <= GoldenTideToleranceMetres;

                    if (warmKey && spot != null && target != null)
                    {
                        warmSlots++;
                        CatchContext ctx = ShoreCatchFacts.ContextAt(spot, start);
                        seasonsSeen[ctx.Season.ToString()] = seasonsSeen.TryGetValue(ctx.Season.ToString(), out int ns) ? ns + 1 : 1;
                        if (spot.SpeciesIds == null || System.Array.IndexOf(spot.SpeciesIds, GoldenTargetFishId) < 0) Gate("bite table: not in the spot's species list");
                        else if (!target.RegionAllowed(ctx.RegionId)) Gate($"bite table: region {ctx.RegionId} not allowed");
                        else if (!target.GearAllowed(ctx.Gear)) Gate($"bite table: gear {ctx.Gear} not allowed");
                        else if (!target.SeasonAllowed(ctx.Season)) Gate($"season: {ctx.Season}");
                        else if (!target.TimeAllowed(ctx.HourOfDay)) Gate("hour: outside her hours");
                        else if (!CatchResolver.Matches(target, in ctx)) Gate($"tide or moving water: tide {ctx.TideHeight:0.00} m");
                        else
                        {
                            // She can bite: her share of the roll among the species that also match here.
                            float total = 0f;
                            foreach (string id in spot.SpeciesIds)
                            {
                                FishSpeciesDef f = FishSpeciesRegistry.Get(id);
                                if (CatchResolver.Matches(f, in ctx)) total += f.SpawnWeight;
                            }
                            if (total > 0f) { targetShareSum += target.SpawnWeight / total; targetShareSlots++; }
                            if (!tideOk) Gate("tide: drawn level off +2 m");
                            else if (!weatherOk) Gate($"weather: not clear or too rough (sea max {_basilAngling.MaxSeaState01:0.00}, clear from {ClearVisibility01:0.00})");
                            else if (!picked) Gate($"bite chance: no bite (spot {spot.BiteChance01:0.00})");
                            else if (pickFish.FishId != GoldenTargetFishId) Gate($"the roll: {pickFish.FishId} came up");
                            else Gate("PASSED every gate");
                        }
                    }

                    if (!picked || !weatherOk || !tideOk) continue;
                    picks.Add((slot, start, vis, sea, level, pickFish.WeightKg, pickFish.FishId, hour, warmKey));
                }
            }
            // The clip's tiers: 0 the target in the warm key at the clip's sea; 1 the target from the fallback hour
            // at the clip's sea; 2 the target anywhere else in the warm key; 3 the rest.
            int Tier(bool warmKey, string id, float sea, float hour)
            {
                bool fish = id == GoldenTargetFishId, calm = sea <= GoldenClipMaxSeaState01;
                if (fish && warmKey && calm) return 0;
                if (fish && calm && hour >= GoldenClipFallbackFromHour) return 1;
                return fish && warmKey ? 2 : 3;
            }
            picks.Sort((a, b) =>
            {
                int c = Tier(a.warmKey, a.id, a.sea, a.hour).CompareTo(Tier(b.warmKey, b.id, b.sea, b.hour));
                if (c != 0) return c;
                c = b.warmKey.CompareTo(a.warmKey);
                if (c != 0) return c;
                c = b.kg.CompareTo(a.kg);
                return c != 0 ? c : Mathf.Abs(a.level - GoldenTideMetres).CompareTo(Mathf.Abs(b.level - GoldenTideMetres));
            });
            log.AppendLine($"{picks.Count} clear slots with a fish within {GoldenTideToleranceMetres} m of a {GoldenTideMetres} m " +
                           $"tide over {GoldenDaysSearched} days, {fromHour:0.00} h to {GoldenSearchToHour:0.00} h (warm key from {warmFrom:0.00} h)");
            var bySpecies = new Dictionary<string, int>();
            foreach (var p in picks) bySpecies[p.id] = bySpecies.TryGetValue(p.id, out int n) ? n + 1 : 1;
            foreach (var kv in bySpecies) log.AppendLine($"  {kv.Key}: {kv.Value} slots");

            // The target fish in the warm key: the top three, or which gate kept her out.
            log.AppendLine($"  {GoldenTargetFishId} in the warm key ({warmFrom:0.00} h to {GoldenSearchToHour:0.00} h), top {GoldenTopListed}:");
            int listed = 0;
            foreach (var p in picks)
            {
                if (!p.warmKey || p.id != GoldenTargetFishId) continue;
                if (listed++ >= GoldenTopListed) break;
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    TOP slot {0}: {1:0.###} kg, day {2}, hour {3:0.00}, tide {4:0.00} m, visibility {5:0.00}, sea {6:0.00}",
                    p.slot, p.kg, (int)(p.start / spd), p.hour, p.level, p.vis, p.sea));
            }
            if (listed == 0) log.AppendLine("    none");
            if (spot == null || target == null)
                log.AppendLine($"  gates not read: spot {(spot != null)}, {GoldenTargetFishId} def {(target != null)}");
            else
            {
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0} warm-key slots of his over {1} days, by the first gate each failed (spot {2}, bite chance {3:0.00}; " +
                    "{4}: seasons {5}, hours {6:0.##} to {7:0.##}, spawn weight {8:0.##}):",
                    warmSlots, GoldenDaysSearched, spot.Id, spot.BiteChance01, target.Id,
                    target.Seasons, target.StartHour, target.EndHour, target.SpawnWeight));
                foreach (var kv in gates) log.AppendLine($"    {kv.Key}: {kv.Value}");
                log.Append("    seasons of those slots:");
                foreach (var kv in seasonsSeen) log.Append($" {kv.Key} {kv.Value}");
                log.AppendLine();
                if (targetShareSlots > 0)
                    log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "    where she can bite, her mean share of the roll by spawn weight is {0:0.000} over {1} slots", targetShareSum / targetShareSlots, targetShareSlots));
            }
            var biggest = new List<(long slot, double start, float vis, float sea, float level, float kg, string id, float hour, bool warmKey)>(picks);
            biggest.Sort((a, b) => b.kg.CompareTo(a.kg));
            log.AppendLine("  the biggest fish, any hour in the window:");
            for (int i = 0; i < Mathf.Min(GoldenSlotsListed, biggest.Count); i++)
            {
                var p = biggest[i];
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    slot {0}: {1} {2:0.###} kg, day {3}, hour {4:0.00}{5}, tide {6:0.00} m, visibility {7:0.00}, sea {8:0.00}",
                    p.slot, p.id, p.kg, (int)(p.start / spd), p.hour, p.warmKey ? " (warm key)" : "", p.level, p.vis, p.sea));
            }

            // Every target slot in the warm key, each tried on his real stand (the clip must be a slot the game
            // plays): the clip's sea first, then the biggest.
            log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  every {0} slot in the warm key that passed the gates, as his stand plays it (sea at or under {1:0.00} first, up to {2}):",
                GoldenTargetFishId, GoldenClipMaxSeaState01, GoldenVerdictsListed));
            int verdicts = 0;
            foreach (var p in picks)
            {
                if (!p.warmKey || p.id != GoldenTargetFishId) continue;
                if (verdicts++ >= GoldenVerdictsListed) break;
                string verdict = null;
                yield return SeekAsHisStandPlays(p.start, p.slot, v => verdict = v);
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    slot {0}: {1:0.###} kg, day {2}, hour {3:0.00}, tide {4:0.00} m, visibility {5:0.00}, sea {6:0.00}{7}: {8}",
                    p.slot, p.kg, (int)(p.start / spd), p.hour, p.level, p.vis, p.sea,
                    p.sea <= GoldenClipMaxSeaState01 ? " (clip sea)" : "", verdict));
            }
            if (verdicts == 0) log.AppendLine("    none");

            Session s = null;
            for (int i = 0; i < Mathf.Min(TideCandidatesTried, picks.Count) && s == null; i++)
            {
                var p = picks[i];
                string verdict = null;
                yield return SeekAsHisStandPlays(p.start, p.slot, v => verdict = v);
                if (verdict != GoldenPlayed)
                {
                    log.AppendLine($"slot {p.slot} at hour {_clock.HourOfDay:0.00}: {verdict}");
                    continue;
                }
                GameServices.ShoreCatch.TryPick(_basilAngling.CatchSpotId, seed, key, p.slot, p.start, out ShoreCatch c);
                var candidate = new Session { Seed = seed, Slot = p.slot, Script = script, Catch = c, Start = p.start };
                ReadPhases(candidate, key);
                if (candidate.LandStart <= candidate.Start) { log.AppendLine($"slot {p.slot}: the fish is not landed"); continue; }
                s = candidate;
                log.AppendLine($"picked slot {p.slot}: day {(int)(p.start / spd)}, hour {_clock.HourOfDay:0.00}, visibility " +
                               $"{p.vis:0.000}, sea state {p.sea:0.000}, tide {p.level:0.00} m; catch {c.FishId} {c.WeightKg:0.###} kg; " +
                               $"phases {s.Describe()}");
            }
            // The search and its gates go to disk now, so they survive a plate that fails further on.
            string goldenDir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(goldenDir);
            File.WriteAllText(Path.Combine(goldenDir, "golden-search.txt"), log.ToString(), new UTF8Encoding(false));
            Assert.IsNotNull(s, $"harness: no clear golden-hour slot at a {GoldenTideMetres} m tide was played\n{log}");

            // The light settles on engine time at the fight's hour, then the game's own camera is parked.
            yield return SeekAndDraw(s.SurfaceStart);
            for (int f = 0; f < SettleFrames; f++) yield return null;
            // The tide plate's camera: halfway out along his cast.
            Vector2 castDir = AnglerTimeline.CastDirection(in script, seed, key, s.Slot, heading);
            yield return _stage.FrameOn(at + castDir * (script.CastReachM * 0.5f));
            LogWaterLook(log);
            Camera cam = _stage.Camera;
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "harness: the stage camera has no CameraFollow");
            float plateHeight = cam.orthographicSize * 2f;
            SpriteRenderer body = _basil.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(body, "harness: Basil has no body renderer to sort against");
            FishArtLibrary art = FishArtLibrary.Shared;
            int scale = Mathf.Max(1, _basil.Look.FishDrawScale);

            // The approach, at the game's walking zoom.
            AnglerApproachLook a = _basil.Look.Approach;
            if (a.Seconds > 0f)
            {
                yield return Reframe(follow, GameServices.Config.PlayerZoom.Sanitized().DefaultWorldHeightMeters);
                double t = s.BiteStart - a.Seconds + 0.6f * AnglerApproachMath.SwimInSeconds(in a);
                yield return StepThrough(t - 0.1d, t);
                AssertInSession(FishingPhase.Waiting);
                Assert.IsTrue(_angler.ApproachRenderer.enabled, "no fish coming in to his bait");
                yield return LetTheSeaCatchUp(t);
                ShootGolden("2b-approach-walking", t, cam, at, log);
                yield return Reframe(follow, plateHeight);
            }

            // The surfaced fight read frame by frame on the game's own drawer: the first thrash drawn cut at her
            // waterline, the first dart, the first jump (a fish that does not jump is logged, not failed).
            double cutAt = double.NaN, dartAt = double.NaN, jumpAt = double.NaN;
            SurfacedFishDrawer drawer = _angler.Fish;
            Assert.IsNotNull(drawer, "harness: his presenter has no surfaced-fish drawer");
            for (double t = s.SurfaceStart; t < s.LandStart; t += StepSeconds)
            {
                _clock.SeekTo(t);
                yield return null;
                if (!_angler.FishRenderer.enabled || drawer.DrawnFrame == null) continue;
                bool dart = drawer.Rung?.DartFrames != null && System.Array.IndexOf(drawer.Rung.DartFrames, drawer.DrawnFrame) >= 0;
                bool cut = drawer.UnderwaterRenderer != null && drawer.UnderwaterRenderer.enabled;
                if (cut && !dart && !drawer.FishAirborne && double.IsNaN(cutAt) && t >= s.SurfaceStart + 0.3d) cutAt = t;
                if (dart && double.IsNaN(dartAt)) dartAt = t;
                if (drawer.FishAirborne && double.IsNaN(jumpAt)) jumpAt = t;
            }
            log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "surfaced fight {0:0.00}s to {1:0.00}s read on his drawer: first cut thrash {2:0.00}, first dart {3:0.00}, " +
                "first jump {4:0.00} (jumps drawn {5})", s.SurfaceStart, s.LandStart, cutAt, dartAt, jumpAt, drawer.JumpsDrawn));

            // The surfaced fight (early, mid, late), the lift and the landing.
            var beatList = new List<(string name, double from, double to)>
            {
                ("4a-fight-break", s.SurfaceStart - 0.2d, s.SurfaceStart + 0.3d),
                ("4-fight", s.SurfaceStart + 0.3d, s.SurfaceStart + 1.2d),
                ("4c-fight-late", s.SurfaceStart + 0.8d * (s.LandStart - s.SurfaceStart) - 0.1d,
                                  s.SurfaceStart + 0.8d * (s.LandStart - s.SurfaceStart)),
                ("5a-land-lift", s.LandStart - 0.3d, s.LandStart + 0.5d * LandFeel().LandLiftTravelSeconds),
                ("5b-land-hang", s.LandStart + LandFeel().LandLiftTravelSeconds,
                                 s.LandStart + LandFeel().LandLiftTravelSeconds + 0.5d * LandFeel().LandHangSeconds),
                ("5-land", s.LandStart + LandedSeen(s), s.LandStart + LandedSeen(s) + 0.3d),
            };
            if (!double.IsNaN(cutAt)) beatList.Add(("4d-thrash-cut", cutAt - 0.3d, cutAt));
            if (!double.IsNaN(dartAt)) beatList.Add(("4e-dart", dartAt - 0.3d, dartAt));
            if (!double.IsNaN(jumpAt)) beatList.Add(("4f-jump", jumpAt - 0.3d, jumpAt + 0.06d));
            foreach (var (name, from, to) in beatList)
            {
                yield return StepThrough(from, to);
                Assert.IsTrue(_basil.TryGetAnglerState(out FishingState fs, out _), $"{name}: not in a session");
                SpriteRenderer fish = _angler.FishRenderer;
                if (fish.enabled && fs.Phase == FishingPhase.Landed)
                {
                    // Out of the water she hangs whole from the line's end by her grip, in front of him.
                    float gripM = LandFeel().HangGripRowsAbovePivot / fish.sprite.pixelsPerUnit * scale;
                    Assert.LessOrEqual(Vector2.Distance((Vector2)fish.transform.position + new Vector2(0f, gripM),
                                                        _angler.LineFarEnd), 1e-3f,
                                       $"{name}: she does not hang from the line's end {_angler.LineFarEnd}");
                    Assert.Greater(fish.sortingOrder, body.sortingOrder, $"{name}: lifted, she hangs behind him");
                    StringAssert.Contains("_gill", fish.sprite.name, $"{name}: she does not hang head-up by the gill");
                }
                else if (fish.enabled)
                {
                    // The line leaves the drawn frame's own mouth, at his scale.
                    Assert.IsNotNull(art, "harness: no fish art table");
                    Assert.IsTrue(art.TryMouthFor(fish.sprite, out Vector2 mouth), $"{name}: the frame is not the table's");
                    Vector2 want = (Vector2)fish.transform.position + mouth * scale;
                    Assert.LessOrEqual(Vector2.Distance(want, _angler.LineFarEnd), 1e-3f,
                                       $"{name}: the line ends at {_angler.LineFarEnd}, not her drawn mouth {want}");
                    if (fs.FishOffsetY >= 0f)
                        Assert.Less(fish.sortingOrder, body.sortingOrder, $"{name}: she draws over him from the water side");
                }
                yield return LetTheSeaCatchUp(to);
                ShootGolden(name, to, cam, at, log);
            }

            // The motion, not single frames: the contact strip from the bite to the landing.
            yield return WriteContactStrip(s, cam, at, log);

            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            File.WriteAllText(Path.Combine(dir, "golden-log.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] golden:\n{log}");
        }

        private static FishFeelSettings LandFeel() =>
            GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;

        /// <summary>How long into the landing she is on the line (the lift, then the hang) or being lifted by
        /// the activity, whichever is longer: past it she is in his hand.</summary>
        private static double LandedSeen(Session s) =>
            Mathf.Max(s.Script.LandLiftSeconds, LandFeel().LandLiftTravelSeconds + LandFeel().LandHangSeconds);

        /// <summary>The contact strip: every this many frames of the clock's step.</summary>
        private const int ContactStride = 4;
        /// <summary>One cell of the strip on the plate, in plate pixels (2 m by 3.5 m at the plate's zoom).</summary>
        private const int ContactCellW = 64, ContactCellH = 112;
        /// <summary>The strip is drawn at this whole-number scale of the plate (each plate pixel a square block).</summary>
        private const int ContactScale = 3;
        /// <summary>The label band over each cell, in unscaled pixels: a 5-row glyph with a row either side.</summary>
        private const int ContactLabelRows = 7;
        /// <summary>The gap between cells, in unscaled pixels.</summary>
        private const int ContactGap = 1;
        /// <summary>The widest PNG a texture can write.</summary>
        private const int ContactMaxWidthPx = 16384;
        /// <summary>Every cell is framed on this point above his feet (m): one fixed frame, so the motion reads and
        /// not the camera, tall enough for his figure and a fish surfaced a couple of metres off the wharf.</summary>
        private const float ContactCentreAboveFeetM = 1.75f;
        private const string ContactStripFile = "g-contact-strip.png";
        /// <summary>The lift and the landing on their own, at this whole-number scale of the plate.</summary>
        private const int ContactLandingScale = 4;
        private const string ContactLandingFile = "g-contact-landing-4x.png";

        /// <summary>
        /// Step his fight from the bite to the landing on consecutive frames and keep every
        /// <see cref="ContactStride"/>th: a cell of the plate framed on him (<see cref="ContactCentreAboveFeetM"/>), drawn
        /// at <see cref="ContactScale"/>x in one row, each labelled with its phase (the lift and the landing told
        /// apart) and banded by phase colour. One fixed frame on him for every cell. Written as <see cref="ContactStripFile"/>; the cells are logged.
        /// </summary>
        private IEnumerator WriteContactStrip(Session s, Camera cam, Vector2 at, StringBuilder log)
        {
            double from = s.BiteStart, to = s.LandStart + LandedSeen(s) + 0.3d;
            yield return StepThrough(from - 0.5d, from - StepSeconds);
            var cells = new List<(Color32[] crop, string label, Color32 band)>();
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(at.x, at.y + ContactCentreAboveFeetM, 0f));
            int step = 0;
            for (double t = from; t <= to; t += StepSeconds, step++)
            {
                _clock.SeekTo(t);
                yield return null;
                if (step % ContactStride != 0) continue;
                Assert.IsTrue(_basil.TryGetAnglerState(out FishingState fs, out _), $"contact strip at {t:0.00}s: not in a session");
                string label;
                Color32 band;
                switch (fs.Phase)
                {
                    case FishingPhase.Bite: label = "BITE"; band = new Color32(200, 170, 60, 255); break;
                    case FishingPhase.FightDeep: label = "DEEP"; band = new Color32(40, 90, 150, 255); break;
                    case FishingPhase.FightSurface: label = "SURFACE"; band = new Color32(40, 150, 140, 255); break;
                    case FishingPhase.Landed:
                        bool lift = t < s.LandStart + s.Script.LandLiftSeconds;
                        label = lift ? "LIFT" : "LANDED";
                        band = lift ? new Color32(190, 110, 50, 255) : new Color32(110, 160, 70, 255);
                        break;
                    default: label = fs.Phase.ToString().ToUpperInvariant(); band = new Color32(110, 110, 110, 255); break;
                }
                cells.Add((CropCell(_stage.Capture(), Mathf.RoundToInt(vp.x * _stage.Width), Mathf.RoundToInt(vp.y * _stage.Height)), label, band));
                SurfacedFishDrawer d = _angler.Fish;
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "contact {0}: t {1:0.00}s ({2:+0.00}s from the bite), {3}, fish {4}, cut {5}, dart held at the surface {6}, splash {7}",
                    cells.Count - 1, t, t - from, label,
                    _angler.FishRenderer.enabled && d?.DrawnFrame != null ? d.DrawnFrame.name : "off",
                    d?.UnderwaterRenderer != null && d.UnderwaterRenderer.enabled, d != null && d.DartHeldAtSurface,
                    SplashSpriteName()));
            }
            Assert.Greater(cells.Count, 0, "harness: the contact strip has no cells");

            log.AppendLine("contact strip: every " + ContactStride + " frames from the bite to the landing, " +
                           ComposeStrip(cells, ContactScale, ContactStripFile));
            var landing = cells.FindAll(c => c.label == "LIFT" || c.label == "LANDED");
            if (landing.Count > 0)
                log.AppendLine("contact landing: the lift and the landing alone, " +
                               ComposeStrip(landing, ContactLandingScale, ContactLandingFile));
        }

        /// <summary>Lay <paramref name="cells"/> out in one row at the whole-number <paramref name="scale"/>, each
        /// under its phase band and label, and write it to the plate folder as <paramref name="file"/>; a cell
        /// narrows around its middle only if the row would pass the widest texture.</summary>
        private static string ComposeStrip(List<(Color32[] crop, string label, Color32 band)> cells, int scale, string file)
        {
            int n = cells.Count;
            int cw = Mathf.Min(ContactCellW, ContactMaxWidthPx / scale / n - ContactGap);
            Assert.Greater(cw, 8, $"harness: {n} cells cannot fit one row at {scale}x");
            int ch = ContactCellH;
            int w = (n * (cw + ContactGap) - ContactGap) * scale, h = (ContactLabelRows + ch) * scale;
            var strip = new byte[w * h * 4];
            void Put(int x, int yTop, Color32 c)
            {
                if (x < 0 || x >= w || yTop < 0 || yTop >= h) return;
                int i = ((h - 1 - yTop) * w + x) * 4;
                strip[i] = c.r; strip[i + 1] = c.g; strip[i + 2] = c.b; strip[i + 3] = 255;
            }
            void Block(int ux, int uyTop, Color32 c)
            {
                for (int by = 0; by < scale; by++)
                    for (int bx = 0; bx < scale; bx++)
                        Put(ux * scale + bx, uyTop * scale + by, c);
            }
            var white = new Color32(250, 250, 245, 255);
            var gap = new Color32(16, 16, 20, 255);
            for (int ux = 0; ux < w / scale; ux++)
                for (int uy = 0; uy < h / scale; uy++) Block(ux, uy, gap);
            for (int c = 0; c < n; c++)
            {
                var cell = cells[c];
                int left = c * (cw + ContactGap);
                for (int ux = 0; ux < cw; ux++)
                    for (int uy = 0; uy < ContactLabelRows; uy++) Block(left + ux, uy, cell.band);
                DrawLabel(cell.label, left + 1, 1, cw - 2, (gx, gy) => Block(gx, gy, white));
                int skip = (ContactCellW - cw) / 2;                 // a narrowed cell keeps the crop's middle
                for (int uy = 0; uy < ch; uy++)
                    for (int ux = 0; ux < cw; ux++)
                        Block(left + ux, ContactLabelRows + uy, cell.crop[uy * ContactCellW + skip + ux]);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(strip);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"{n} cells, {cw}x{ch} plate px each at {scale}x, {w}x{h} px, written to {file}";
        }

        /// <summary>
        /// The <see cref="ContactCellW"/> x <see cref="ContactCellH"/> plate cell centred on plate pixel
        /// (<paramref name="cx"/>, <paramref name="cy"/>) of a bottom-up RGBA capture, as rows top to bottom
        /// (off-plate pixels dark), so the strip keeps a cell and not the whole frame.
        /// </summary>
        private Color32[] CropCell(byte[] rgba, int cx, int cy)
        {
            var crop = new Color32[ContactCellW * ContactCellH];
            for (int uy = 0; uy < ContactCellH; uy++)
            {
                int py = cy + ContactCellH / 2 - 1 - uy;            // plate rows count up from the bottom
                for (int ux = 0; ux < ContactCellW; ux++)
                {
                    int px = cx - ContactCellW / 2 + ux;
                    if (px < 0 || px >= _stage.Width || py < 0 || py >= _stage.Height)
                    {
                        crop[uy * ContactCellW + ux] = new Color32(16, 16, 20, 255);
                        continue;
                    }
                    int i = (py * _stage.Width + px) * 4;
                    crop[uy * ContactCellW + ux] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], 255);
                }
            }
            return crop;
        }

        /// <summary>The sprite his splash is drawing now, or "off".</summary>
        private string SplashSpriteName()
        {
            foreach (SpriteRenderer r in _angler.GetComponentsInChildren<SpriteRenderer>(false))
                if (r.enabled && r.sprite != null && r.sprite.name.StartsWith("SplashBurst", StringComparison.Ordinal))
                    return r.sprite.name;
            return "off";
        }

        /// <summary>A 3x5 capital for each letter the strip's labels use (rows top to bottom).</summary>
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".#.", "#.#", "###", "#.#", "#.#" }, ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
            ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" }, ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" }, ['F'] = new[] { "###", "#..", "##.", "#..", "#.." },
            ['G'] = new[] { ".##", "#..", "#.#", "#.#", ".##" }, ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" }, ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" },
            ['L'] = new[] { "#..", "#..", "#..", "#..", "###" }, ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" },
            ['N'] = new[] { "##.", "#.#", "#.#", "#.#", "#.#" }, ['O'] = new[] { ".#.", "#.#", "#.#", "#.#", ".#." },
            ['P'] = new[] { "##.", "#.#", "##.", "#..", "#.." }, ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
            ['S'] = new[] { ".##", "#..", ".#.", "..#", "##." }, ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" }, ['W'] = new[] { "#.#", "#.#", "###", "###", "#.#" },
            ['Y'] = new[] { "#.#", "#.#", ".#.", ".#.", ".#." },
        };

        /// <summary>Plot <paramref name="text"/> in <see cref="Glyphs"/> from (<paramref name="x"/>,
        /// <paramref name="yTop"/>), a column between letters, stopping at <paramref name="maxWidth"/>.</summary>
        private static void DrawLabel(string text, int x, int yTop, int maxWidth, Action<int, int> plot)
        {
            int cx = 0;
            foreach (char ch in text)
            {
                if (cx + 3 > maxWidth) break;
                if (Glyphs.TryGetValue(ch, out string[] g))
                    for (int gy = 0; gy < g.Length; gy++)
                        for (int gx = 0; gx < g[gy].Length; gx++)
                            if (g[gy][gx] == '#') plot(x + cx + gx, yTop + gy);
                cx += 4;
            }
        }

        /// <summary>The water's look at this hour, for why the sea reads as it does: each water surface's own
        /// material (the preset it was authored from), whether the weather mood blend is on and how strong, the
        /// four mood anchors with this frame's blend weights, and the colours the blend actually pushed (the
        /// renderer's property block, else the material's). Read by reflection: test-only, nothing is changed.</summary>
        private static void LogWaterLook(StringBuilder log)
        {
            const BindingFlags Field = BindingFlags.Instance | BindingFlags.NonPublic;
            string[] anchors = { "base", "calm", "storm", "fog" };
            string[] colours = { "_ShallowColor", "_DeepColor", "_DeepBlueColor" };
            var surfaces = Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            log.AppendLine($"water look: {surfaces.Length} water surface(s)");
            var block = new MaterialPropertyBlock();
            foreach (WaterSurface w in surfaces)
            {
                var r = w.GetComponent<Renderer>();
                Material mat = r != null ? r.sharedMaterial : null;
                bool on = (bool)(typeof(WaterSurface).GetField("_weatherPaletteEnabled", Field)?.GetValue(w) ?? false);
                float strength = (float)(typeof(WaterSurface).GetField("_weatherPaletteStrength", Field)?.GetValue(w) ?? float.NaN);
                var mats = typeof(WaterSurface).GetField("_anchorMaterials", Field)?.GetValue(w) as Material[];
                var weights = typeof(WaterSurface).GetField("_weatherBlendWeights", Field)?.GetValue(w) as float[];
                var sb = new StringBuilder();
                sb.Append(string.Format(CultureInfo.InvariantCulture, "  {0}: material {1}, weather mood {2} at strength {3:0.00}",
                                        w.gameObject.name, mat != null ? mat.name : "none", on ? "ON" : "off", strength));
                for (int i = 0; i < anchors.Length; i++)
                {
                    string name = mats != null && i < mats.Length && mats[i] != null ? mats[i].name : "none";
                    string weight = weights != null && i < weights.Length
                        ? weights[i].ToString("0.000", CultureInfo.InvariantCulture) : "?";
                    sb.Append($"; {anchors[i]} {name} x{weight}");
                }
                if (r != null)
                {
                    r.GetPropertyBlock(block);
                    foreach (string c in colours)
                    {
                        int id = Shader.PropertyToID(c);
                        bool pushed = !block.isEmpty && block.HasColor(id);
                        bool authored = mat != null && mat.HasProperty(id);
                        if (!pushed && !authored) continue;
                        Color col = pushed ? block.GetColor(id) : mat.GetColor(id);
                        sb.Append(string.Format(CultureInfo.InvariantCulture, "; {0} #{1} ({2})",
                                                c, ColorUtility.ToHtmlStringRGB(col), pushed ? "blended" : "material"));
                    }
                }
                log.AppendLine(sb.ToString());
            }
        }

        /// <summary>Snap the game's camera to a world height and let it land (engine time runs only while it does).</summary>
        private static IEnumerator Reframe(CameraFollow follow, float worldHeightMeters)
        {
            Time.timeScale = 1f;
            follow.SetFraming(worldHeightMeters, 0f);
            for (int f = 0; f < 12; f++) yield return null;
            Time.timeScale = 0f;
            for (int f = 0; f < 2; f++) yield return null;
        }

        /// <summary>Shoot a golden plate and log what is in it: the tide (asked and drawn), the zoom, where Basil
        /// stands in the picture (for the 3x crop), and the fish and line as drawn.</summary>
        private void ShootGolden(string name, double t, Camera cam, Vector2 at, StringBuilder log)
        {
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(at.x, at.y, 0f));
            SpriteRenderer fish = _angler.FishRenderer;
            log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0}{1}: t {2:0.00}s, hour {3:0.000}, tide {4:0.000} m (drawn {5:0.000}), world height {6:0.###} m, " +
                "plate {7}x{8}, Basil at px ({9:0},{10:0}) from top-left; fish {11} order {12} (body {13}) at {14}; " +
                "line ends {15}; held {16}; frame {17}, cut at her waterline {18}, airborne {19}",
                GoldenPrefix, name, t, _clock.HourOfDay, GameServices.Environment.WaterLevelAt(t), DrawnSeaLevel(),
                cam.orthographicSize * 2f, _stage.Width, _stage.Height, vp.x * _stage.Width, (1f - vp.y) * _stage.Height,
                fish.enabled ? (fish.sprite != null ? fish.sprite.name : "none") : "off", fish.sortingOrder,
                _basil.GetComponent<SpriteRenderer>().sortingOrder, (Vector2)fish.transform.position,
                _angler.LineFarEnd, _angler.Land?.Mesh?.Drawing ?? false,
                _angler.Fish?.DrawnFrame != null ? _angler.Fish.DrawnFrame.name : "none",
                _angler.Fish?.UnderwaterRenderer != null && _angler.Fish.UnderwaterRenderer.enabled,
                _angler.Fish != null && _angler.Fish.FishAirborne));
            Shoot(GoldenPrefix + name);
        }

        /// <summary>The phase edges of <paramref name="s"/>'s slot, read off the pure timeline.</summary>
        private static void ReadPhases(Session s, uint key)
        {
            AnglerScript script = s.Script;
            FishingPhase last = FishingPhase.Idle;
            for (double t = s.Start; t < s.Start + script.SlotSeconds; t += 0.01d)
            {
                AnglerTimeline.Evaluate(in script, in s.Catch, true, s.Seed, key, 90f, t, out AnglerBeat b);
                if (b.Phase == last) continue;
                if (b.Phase == FishingPhase.Cast) s.CastStart = b.PhaseStartSeconds;
                else if (b.Phase == FishingPhase.Waiting) s.WaitStart = b.PhaseStartSeconds;
                else if (b.Phase == FishingPhase.Bite) s.BiteStart = b.PhaseStartSeconds;
                else if (b.Phase == FishingPhase.FightDeep) s.DeepStart = b.PhaseStartSeconds;
                else if (b.Phase == FishingPhase.FightSurface) s.SurfaceStart = b.PhaseStartSeconds;
                else if (b.Phase == FishingPhase.Landed) s.LandStart = b.PhaseStartSeconds;
                last = b.Phase;
            }
        }

        // =============================================================================================
        //  The clip (explicit: frames for a video, never a suite case)
        // =============================================================================================
        [UnityTest, Timeout(ClipTimeoutMs), Explicit("Writes several hundred full-HD frames for a video; run by name only.")]
        public IEnumerator Clip_BasilAndThePlayerFishTheWharfHead_GoldenHour()
        {
            // Explicit is not enough: a class or namespace filter still selects it. With nowhere named to
            // film into it films nothing, so only a run that sets HH_CLIP_DIR on purpose can write frames.
            string dir = System.Environment.GetEnvironmentVariable(ClipDirEnv);
            if (string.IsNullOrEmpty(dir))
                Assert.Ignore($"films only into a folder named by {ClipDirEnv}; none is set, so no frames are written");
            WharfNightStage.RequireAGraphicsDevice();
            Directory.CreateDirectory(dir);

            yield return Arrive(ClipArriveHour);
            yield return FindBasil();
            yield return FindThePlayer();
            var log = new StringBuilder();

            // The light: the clearest late afternoon in his wharf block, picked off the pure weather model.
            var weather = GameServices.Environment as HiddenHarbours.Environment.EnvironmentService;
            Assert.IsNotNull(weather, "harness: the region registered no environment service");
            double spd = GameServices.Config.SecondsPerDay;
            var candidates = new List<(int day, float hour, float vis, float sea)>();
            for (int day = 1; day <= ClipDaysSearched; day++)
                foreach (float hour in ClipHours)
                {
                    double t0 = (day + hour / 24.0) * spd;
                    candidates.Add((day, hour, weather.VisibilityAt(t0), weather.SeaState01At(t0)));
                }
            candidates.Sort((a, b) =>
            {
                int c = (b.vis - b.sea).CompareTo(a.vis - a.sea);
                return c != 0 ? c : b.hour.CompareTo(a.hour);
            });

            Session s = null;
            (int day, float hour, float vis, float sea) pick = default;
            for (int i = 0; i < Mathf.Min(ClipCandidatesTried, candidates.Count) && s == null; i++)
            {
                pick = candidates[i];
                double now = (pick.day + pick.hour / 24.0) * spd;
                yield return SeekAndDraw(now);
                for (int f = 0; f < SettleFrames; f++) yield return null;   // the tint ticks on engine time
                if (_basil.Angling == null)
                {
                    log.AppendLine($"candidate day {pick.day} {pick.hour:0.00} h: Basil is not fishing then");
                    continue;
                }
                yield return FindASessionWithAFish(now, found => s = found);
                if (s == null) log.AppendLine($"candidate day {pick.day} {pick.hour:0.00} h: no slot with a fish left");
            }
            Assert.IsNotNull(s, "harness: no clear late-afternoon slot with a fish was found");
            log.Insert(0, $"light: day {pick.day} at {pick.hour:0.00} h, visibility {pick.vis:0.000}, sea state " +
                          $"{pick.sea:0.000} (best of {candidates.Count} day/hour candidates by visibility less sea " +
                          $"state); the warm key starts {WarmKeyHour():0.00} h and his wharf block now runs to 19:30\n");

            Vector2 at = _basil.transform.position;
            float heading = Heading();
            Vector2 fwd = Compass(heading), right = Compass(heading + 90f);
            Vector2 playerAt = at + right * PlayerRightOfBasilMetres;
            PutThePlayerAt(playerAt, heading);
            Vector2 landing = at + Compass(heading + s.Script.CastBearingDegrees) * s.Script.CastReachM;
            Vector2 centre = (at + playerAt) * 0.5f + fwd * ClipWaterAheadMetres;
            yield return _stage.FrameOn(centre);

            // Close: the game's own closest walking stop, on a design-screen target so it lands on a whole step.
            Camera cam = _stage.Camera;
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "harness: the stage camera has no CameraFollow");
            _clipRt = new RenderTexture(ClipWidthPx, ClipHeightPx, 24, RenderTextureFormat.ARGBHalf);
            _clipRt.Create();
            cam.targetTexture = _clipRt;
            Time.timeScale = 1f;                  // the player's presenter keeps its clocks on engine time
            float closest = GameServices.Config.PlayerZoom.Sanitized().ClosestWorldHeightMeters;
            follow.SetFraming(closest, 0f);
            for (int f = 0; f < 12; f++) yield return null;
            Time.captureFramerate = ClipFps;      // which this makes one 1/fps step per frame
            float upp = follow.WorldUnitsPerRenderedPixel;
            log.AppendLine($"camera: closest walking stop {closest:0.###} m asked, ortho {cam.orthographicSize:0.###} " +
                           $"(world height {cam.orthographicSize * 2f:0.###} m), {1f / Mathf.Max(1e-6f, upp):0.#} px/m " +
                           $"at {ClipWidthPx}x{ClipHeightPx}, centre {centre}");
            log.AppendLine($"staging: Basil at {at} facing {heading:0}°, player at {playerAt} " +
                           $"({PlayerRightOfBasilMetres} m to his right, side by side), cast lands " +
                           $"about {landing}");
            log.AppendLine("the player is a puppet: his fishing states are published from the same angler timeline " +
                           $"under the key {PlayerPuppetKey}, {PlayerLeadSeconds:0.#} s ahead of Basil, and the game's own " +
                           "presenter draws them; it is not live input");

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
            // The puppet fishes over the same edge, measured from where he stands.
            ShoreEdge playerEdge = _basilAngling.TryGetStand(out ShoreStand spotStand) ? spotStand.Edge.RelativeTo(playerAt) : default;

            double from = s.Start - ClipLeadSeconds - PlayerLeadSeconds;
            double to = s.LandStart + script.LandSeconds + ClipTailSeconds;
            log.AppendLine($"seed {s.Seed}; Basil slot {s.Slot} at {s.Start:0.00}s, catch {s.Catch.FishId} {s.Catch.WeightKg:0.###} kg");
            log.AppendLine($"player puppet slot {playerSlot}, catch {playerCatch.FishId} {playerCatch.WeightKg:0.###} kg, " +
                           $"{PlayerLeadSeconds:0.#}s ahead of Basil");
            log.AppendLine($"phases: {s.Describe()}");
            int frame = 0;
            bool loggedLine = false;
            for (double t = from; t <= to; t += 1d / ClipFps, frame++)
            {
                _clock.SeekTo(t);
                double pt = playerStart + (t - (s.Start - PlayerLeadSeconds));
                AnglerTimeline.Evaluate(in script, in playerCatch, playerCatch.FishId != null, s.Seed, playerKey,
                                        heading, pt, in playerEdge, out AnglerBeat pb);
                EventBus.Publish(new FishingStateChanged(pb.InSession ? pb.State : FishingState.Idle));
                _publishedPlayerState = true;
                yield return null;   // never WaitForEndOfFrame: it does not fire in batchmode
                string name = $"frame_{frame:0000}.jpg";
                SaveClipFrame(Path.Combine(dir, name));
                _basil.TryGetAnglerState(out FishingState bs, out _);
                if (!loggedLine && _angler.DrawsLine && _angler.Line != null && (loggedLine = true))
                    log.AppendLine($"line: width {_angler.Line.widthMultiplier * _angler.Line.startWidth:0.####} m = " +
                                   $"{_angler.Line.widthMultiplier * _angler.Line.startWidth / Mathf.Max(1e-6f, upp):0.##} px " +
                                   "at this zoom (as the game draws it; not thickened for the clip)");
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
            // A suite's earlier teardown resets the services; the shore spots are published once per domain, before
            // the first scene. Republished BEFORE the region loads, because the day's plans are built at load and
            // read the spot's stand through it.
            if (GameServices.ShoreCatch is EmptyShoreCatchFacts)
            {
                var lib = Resources.Load<ShoreSpotLibrary>(ShoreCatchRegistrar.ResourcesPath);
                Assert.IsNotNull(lib, "harness: no shore spot library in Resources");
                GameServices.ShoreCatch = lib.BuildFacts();
                _healed = "shore catch republished";
            }
            // The species seams likewise: registered once per domain, emptied by the same teardown, so a later
            // test's mackerel would fight flat (never jump) where the game's would jump. A repeat registration
            // republishes the seams.
            if (GameServices.FishBehaviour is EmptyFishBehaviourFacts)
            {
                var species = Resources.Load<FishSpeciesLibrary>(FishSpeciesRegistrar.ResourcesPath);
                Assert.IsNotNull(species, "harness: no fish species library in Resources");
                species.RegisterAll();
                Assert.IsFalse(GameServices.FishBehaviour is EmptyFishBehaviourFacts, "harness: the species seam stayed empty");
                _healed = _healed == "none" ? "fish species republished" : _healed + "; fish species republished";
            }
            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();

            if (GameServices.Clock == null)
            {
                var live = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
                if (live != null)
                {
                    GameServices.Clock = live;
                    _healed = _healed == "none" ? "the region's clock republished" : _healed + "; the region's clock republished";
                }
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
        private IEnumerator FindASessionWithAFish(double now, Action<Session> found)
        {
            _basilAngling = _basil.Angling;
            AnglerScript script = _basilAngling.Script;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            uint key = FigureKeyHash.Of(BasilKey);
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

        /// <summary>
        /// Seek to <paramref name="slotStart"/> the way the game reaches it: first to an hour of that day outside
        /// his angling block (<see cref="GoldenOffBlockHour"/>), so his stand drops the last run's window as it
        /// does when he walks off at dusk, then half a second into the slot. Hands back
        /// <see cref="GoldenPlayed"/> when his stand is fishing that very slot, else why not.
        /// </summary>
        private IEnumerator SeekAsHisStandPlays(double slotStart, long slot, System.Action<string> verdict)
        {
            double spd = GameServices.Config.SecondsPerDay;
            double day = System.Math.Floor(slotStart / spd);
            yield return SeekAndDraw((day + GoldenOffBlockHour / 24.0) * spd);
            yield return SeekAndDraw(slotStart + 0.5d);
            var routine = _basil.GetComponent<VillagerRoutine>();
            bool inSession = _basil.TryGetAnglerState(out _, out _);
            if (_basil.Angling == null)
                verdict($"not angling: block {routine.Pose.BlockIndex}, walking {routine.Pose.Walking}, " +
                        $"a varied day (beach outing) {routine.IsVariedToday}");
            else if (!inSession || _basil.Beat.Slot != slot)
                verdict($"angling, not this slot: beat slot {_basil.Beat.Slot}, in session {inSession}, " +
                        $"walking {routine.Pose.Walking}, water casts {_basil.Water.IsSet && _basil.Water.Casts}");
            else verdict(GoldenPlayed);
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

        /// <summary>Over the water: on no standable deck. The tide may bare the bed under the wharf's side at low
        /// water, so the ground's walkability is logged, not asserted; the deck is what the fish must never cross.</summary>
        private static void AssertOverWater(Vector2 p, string beat)
        {
            bool deck = StandableSurfaces.TryGetDeckElevationNow(p, out float elevation);
            Debug.Log($"[{PlateDir}] {beat}: at {p}, on deck {deck}, walkable {TidalWalkability.IsWalkableNow(p)}, " +
                      $"depth {TidalWalkability.DepthNow(p):0.00} m");
            Assert.IsFalse(deck, $"{beat}: at {p}, which is on the deck (elevation {elevation:0.00}), not over the water");
        }

        /// <summary>Where the deck ends straight out from his stand, logged so the authored edge can be checked
        /// against the built one.</summary>
        private static void LogTheEdgeProfile(Vector2 at)
        {
            float lastOnDeck = float.NaN;
            for (float d = 0f; d <= EdgeProfileMetres; d += EdgeProfileStepMetres)
                if (StandableSurfaces.TryGetDeckElevationNow(at + Vector2.up * d, out _)) lastOnDeck = at.y + d;
            Debug.Log($"[{PlateDir}] the deck north of the stand ends at y {lastOnDeck:0.00} (sampled every " +
                      $"{EdgeProfileStepMetres} m)");
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

        private RenderTexture _clipRt;
        private Texture2D _clipRead, _clipOut;
        private Color32[] _clipPixels;

        /// <summary>Render the game's camera into the clip's own target and write it as a JPEG, gamma-corrected
        /// exactly as the stage's plates are (the project is linear).</summary>
        private void SaveClipFrame(string path)
        {
            Camera cam = _stage.Camera;
            cam.Render();
            int w = _clipRt.width, h = _clipRt.height;
            if (_clipRead == null) _clipRead = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            if (_clipOut == null) _clipOut = new Texture2D(w, h, TextureFormat.RGBA32, false);
            if (_clipPixels == null) _clipPixels = new Color32[w * h];
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _clipRt;
            _clipRead.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _clipRead.Apply(false);
            RenderTexture.active = prev;
            Color[] px = _clipRead.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                c = c.gamma;
                _clipPixels[i] = new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f),
                                             (byte)Mathf.RoundToInt(c.b * 255f), 255);
            }
            _clipOut.SetPixels32(_clipPixels);
            _clipOut.Apply(false);
            File.WriteAllBytes(path, _clipOut.EncodeToJPG(ClipJpegQuality));
        }

        private void ReleaseTheClipTargets()
        {
            if (_clipRead != null) Object.DestroyImmediate(_clipRead);
            if (_clipOut != null) Object.DestroyImmediate(_clipOut);
            _clipRead = null; _clipOut = null; _clipPixels = null;
            if (_clipRt == null) return;
            if (_stage?.Camera != null && _stage.Camera.targetTexture == _clipRt) _stage.Camera.targetTexture = null;
            _clipRt.Release();
            Object.DestroyImmediate(_clipRt);
            _clipRt = null;
        }

        /// <summary>The hour the day/night warm key starts, read off the region's own profile.</summary>
        private static float WarmKeyHour()
        {
            var profile = Resources.Load<DayNightProfile>("DayNightProfile");
            return profile != null ? profile.SunsetHour - profile.WarmKeyBeforeSunsetHours : float.NaN;
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
