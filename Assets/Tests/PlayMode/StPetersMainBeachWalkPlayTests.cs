#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE MAIN BEACH IS A BEACH (terrain fidelity, piece 2).</b> St Peters' main beach, West Gap, walked by the
    /// shipped fisher on a held intent: the village path (<c>path.stp_cliff_walk</c>) to the junction where
    /// <c>path.stp_west_gap_path</c> leaves it, down that path past the beach steps' head and foot
    /// (<c>prop.stp_beach_steps</c> in <c>keyscene.stp_main_beach</c>) to the pocket beach, south to the water's edge,
    /// along the bay both ways, and back up the same way.
    ///
    /// <para><b>No teleport.</b> The one placement is the start: she is stood once on the village path, 10 m east of
    /// the junction. Every metre after that is the shipped <see cref="PlayerWalkController"/>'s, which reads a
    /// <see cref="HeldWalkIntents"/> in its own Update and moves its rigidbody in its own FixedUpdate. Nothing here
    /// writes a velocity or a position.</para>
    ///
    /// <para><b>Where the walk stops.</b> A leg that makes less than <see cref="StallMetres"/> of progress in
    /// <see cref="StallWindowSeconds"/> is a STOP. The stop records her body position, the ground and water there
    /// (the depth the walker itself reads), and every solid collider her foot circle touches or would hit in the
    /// direction she was walking. It takes a plate, and the walk goes on to the next waypoint from where she
    /// stands. The case fails at the end, naming every stop, so one run shows them all. The survey legs along the
    /// bay record where the walkable shore ends; they do not fail the case.</para>
    ///
    /// <para><b>The board's framing.</b> The second case shoots the bay at noon in three on-foot frames down the
    /// board's axis (<c>beach-b.png</c> of the 10-02 St Peters review: the path and the steps above, the sand to
    /// the water below, the bay to the east). Every caption records the framed height, as the cliff plates do.</para>
    ///
    /// <para>A plate needs a GPU: without a graphics device both cases skip by name (CI). The shared save is never
    /// opened: each case asserts the run's own save before it walks or shoots.</para>
    /// </summary>
    public class StPetersMainBeachWalkPlayTests
    {
        const string PlateDir = "MainBeachPlates";
        const string StPeters = "StPeters";
        const string CleanupSceneName = "MainBeachWalkCleanup";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        const float LeadEaseOutSeconds = 6f;
        const float OnTheCentreMetres = 0.001f;
        const float NoonHours = 12f;

        // ---- the walk ------------------------------------------------------------------------------------------
        const float ArriveMetres = 0.4f;
        const float StallWindowSeconds = 2f, StallMetres = 0.25f;
        const float LegSlackSeconds = 15f, LegMetresPerSecondFloor = 1.5f;   // a leg's budget: its length at 1.5 m/s + 15 s
        const float WaterEdgeDepth = 0.02f;      // her body stands in water: the water's edge
        const float ToTheWaterMaxMetres = 45f;
        const float EdgeTargetDepth = -0.1f;      // along the bay she keeps to ground 0.1 m over the water
        const float EdgeSteerGain = 4f;
        const float EastReachX = 40f, WestReachX = -6f;
        const float FootProbeSlack = 0.15f, AheadProbeMetres = 0.6f;

        /// <summary>The way down, from the data: <c>path.stp_cliff_walk</c>'s points east of the junction, then
        /// <c>path.stp_west_gap_path</c>'s five (its second-last pair are the steps' head and foot, as
        /// <c>keyscene.stp_main_beach</c> places <c>prop.stp_beach_steps</c>). The way up is the same, reversed.</summary>
        static readonly (string Name, Vector2 At)[] TheWayDown =
        {
            ("village-path-start", new Vector2(22.5f, -57.1f)),
            ("village-path", new Vector2(17.8f, -56.9f)),
            ("the-junction", new Vector2(12.5f, -55.3f)),
            ("west-gap-path", new Vector2(11.6f, -57.6f)),
            ("the-steps-head", new Vector2(11f, -59f)),
            ("the-steps-foot", new Vector2(10.4f, -63.8f)),
            ("the-pocket-beach", new Vector2(10f, -66.5f)),
        };

        /// <summary>The board's framing, as three on-foot frames down its axis.</summary>
        static readonly (string Name, Vector2 Centre)[] TheBoardsFrames =
        {
            ("1-the-path-and-the-steps", new Vector2(10.5f, -60f)),
            ("2-the-sand-to-the-water", new Vector2(10f, -72f)),
            ("3-the-bay-east", new Vector2(26f, -70f)),
        };

        sealed class Leg
        {
            public string Name;
            public bool Survey, Reached;
            public Vector2 From, To, End, Aim;
            public float Metres, Seconds;
            public string Why, Blockers;
            public override string ToString() =>
                $"{Name}: {(Reached ? "REACHED" : "STOPPED")} from ({From.x:0.##}, {From.y:0.##}) toward ({To.x:0.##}, {To.y:0.##}), " +
                $"ended at ({End.x:0.###}, {End.y:0.###}), {Metres:0.##} m in {Seconds:0.##} s" +
                (Why != null ? $", {Why}" : "") + (Blockers != null ? $"; {Blockers}" : "");
        }

        readonly List<Object> _spawned = new List<Object>();
        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Leg> _legs = new List<Leg>();
        DateTime _runStartedUtc;
        bool _loadedAny, _introOnEntry, _enteredWorld;
        Vector2 _askedCentre;
        string _healed = "none", _pinned = "(not pinned)", _saveLine = "(not read)", _eased = "(not framed)";
        int _plateIndex;

        CameraFollow _follow;
        Camera _cam;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        RenderTexture _rt;
        int _w, _h, _askedGridPx;
        float _askedHeight;

        PlayerWalkController _player;
        Rigidbody2D _body;
        Collider2D _foot;
        HeldWalkIntents _held;
        bool _tideGated;

        [OneTimeSetUp]
        public void MarkTheRun() => _runStartedUtc = DateTime.UtcNow;

        // =============================================================================================
        //  Set-up and teardown: leave nothing loaded (#764)
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            _held?.Release();
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // a STATIC: left at 0 it stops every test that follows

            if (_follow != null && _followCaptured)
            {
                _follow.Target = _followTargetOnEntry;
                _follow.Smooth = _followSmoothOnEntry;
            }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            _legs.Clear();

            _follow = null; _cam = null; _followCaptured = false; _followTargetOnEntry = null; _anchor = null;
            _player = null; _body = null; _foot = null; _held = null;
            _pinned = "(not pinned)"; _saveLine = "(not read)"; _eased = "(not framed)"; _healed = "none";

            if (!_loadedAny) yield break;   // a case that skipped (every case on CI) loaded nothing

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            if (_enteredWorld) ShellFlow.Reset();   // the slate wipe a case that entered the world owes the shell
            _enteredWorld = false;
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The cases
        // =============================================================================================

        /// <summary>2.1: the village path, down the West Gap path past the steps onto the sand, to the water's
        /// edge, along the bay both ways, and back up. Fails naming every stop on the way down, to the water and
        /// back up.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TheMainBeach_SheWalksTheVillagePathDownTheStepsToTheWaterAndBack()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            AssertTheSaveIsTheRunsOwn();
            yield return EnterTheWorldAshore();
            yield return PinTheHour(NoonHours);
            yield return FindHer();
            TakeTheWalker();

            // ---- the one placement: the start of the village path ----------------------------------------------
            Vector2 start = TheWayDown[0].At;
            _body.position = start;
            _player.transform.position = new Vector3(start.x, start.y, _player.transform.position.z);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.LessOrEqual(Vector2.Distance(Body(), start), 0.5f,
                $"she did not stay where the walk starts ({start}); she is at {Body()}. Something else moves her.");
            yield return Plate("start", null);

            // ---- the way down --------------------------------------------------------------------------------
            for (int i = 1; i < TheWayDown.Length; i++)
                yield return WalkLeg("down: " + TheWayDown[i].Name, TheWayDown[i].At, survey: false);

            // ---- to the water's edge --------------------------------------------------------------------------
            var water = new Leg { Name = "to the water's edge", From = Body(), To = Body() + Vector2.down * ToTheWaterMaxMetres };
            yield return ToTheWater(water);
            yield return Plate(water.Reached ? "the-waters-edge" : "stop-before-the-water", water);
            Vector2 edge = Body();

            // ---- along the bay, east then west (survey: where the walkable shore ends) --------------------------
            var east = new Leg { Name = "along the bay east", Survey = true, From = Body(), To = new Vector2(EastReachX, Body().y) };
            yield return AlongTheEdge(east, +1f, EastReachX);
            yield return Plate(east.Reached ? "the-bay-east-end" : "the-bay-east-stop", east);
            var west = new Leg { Name = "along the bay west", Survey = true, From = Body(), To = new Vector2(WestReachX, Body().y) };
            yield return AlongTheEdge(west, -1f, WestReachX);
            yield return Plate(west.Reached ? "the-bay-west-end" : "the-bay-west-stop", west);

            // ---- back to the water's edge below the path, and up the way she came -------------------------------
            yield return WalkLeg("back: the water's edge below the path", edge, survey: false);
            for (int i = TheWayDown.Length - 1; i >= 2; i--)
                yield return WalkLeg("up: " + TheWayDown[i].Name, TheWayDown[i].At, survey: false);

            WriteTheWalkLog();
            List<Leg> stops = _legs.Where(l => !l.Survey && !l.Reached).ToList();
            Assert.That(stops, Is.Empty,
                $"the walk stopped {stops.Count} time(s) on the way down, to the water or back up:\n  " +
                string.Join("\n  ", stops.Select(s => s.ToString())) +
                $"\nthe whole walk:\n  {string.Join("\n  ", _legs.Select(l => l.ToString()))}");
        }

        /// <summary>2.3: the bay at noon from the board's framing, three on-foot frames down its axis.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheMainBeach_FromTheBoardsFraming_AtNoon()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            AssertTheSaveIsTheRunsOwn();
            yield return PinTheHour(NoonHours);
            foreach (var f in TheBoardsFrames)
            {
                yield return FrameOn(f.Centre);
                string name = "beach-board-" + f.Name;
                SavePlate(name + ".png", Capture(), _w);
                SaveCaption(name + ".txt", Caption(name, "the board's framing (beach-b.png): " + f.Name, f.Centre, null));
            }
        }

        // =============================================================================================
        //  The walker
        // =============================================================================================

        Vector2 Body() => _body.position;
        static float Depth(Vector2 at) => TidalWalkability.DepthNow(at);
        static float Water() => GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);

        IEnumerator FindHer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                foreach (PlayerWalkController p in Object.FindObjectsByType<PlayerWalkController>())
                {
                    var iso = p.GetComponent<IsoCharacterSprite>();
                    if (iso == null || iso.Visual == null || iso.Visual.Skin == null) continue;
                    _player = p;
                    break;
                }
                if (_player == null) yield return null;
            }
            Assert.IsNotNull(_player, "no on-foot player with a character skin after 240 frames: there is no fisher to walk.");
            Assert.IsNull(_player.GetComponentInParent<IsoFacetHullRenderer>(), "the player is ABOARD a hull; the walk is ashore.");
            Assert.IsTrue(_player.isActiveAndEnabled, "the shipped walk controller is disabled: nothing would read the held intent.");
            _body = _player.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(_body, "the fisher has no Rigidbody2D.");
            _foot = _player.GetComponents<Collider2D>().FirstOrDefault(c => c != null && !c.isTrigger);
            Assert.IsNotNull(_foot, "the fisher has no solid foot collider, so nothing could ever block her.");
        }

        void TakeTheWalker()
        {
            _held = new HeldWalkIntents();
            _player.ConfigureWalkInput(_held);
            _body.simulated = true;
            FieldInfo gate = typeof(PlayerWalkController).GetField("_tideGatedWalk", BindingFlags.Instance | BindingFlags.NonPublic);
            _tideGated = gate != null && (bool)gate.GetValue(_player);
        }

        IEnumerator WalkLeg(string name, Vector2 to, bool survey)
        {
            var leg = new Leg { Name = name, Survey = survey, From = Body(), To = to };
            float budget = Vector2.Distance(Body(), to) / LegMetresPerSecondFloor + LegSlackSeconds;
            yield return Pursue(leg, budget, at => Vector2.Distance(at, to) <= ArriveMetres, at => (to - at).normalized);
            string label = name.Replace(": ", "-").Replace(' ', '-').Replace("'", "");
            yield return Plate(leg.Reached ? label : "stop-" + label, leg);
        }

        IEnumerator ToTheWater(Leg leg)
        {
            float budget = ToTheWaterMaxMetres / LegMetresPerSecondFloor + LegSlackSeconds;
            Vector2 from = Body();
            yield return Pursue(leg, budget, at => Depth(at) >= WaterEdgeDepth, at => Vector2.down);
            if (!leg.Reached && Vector2.Distance(from, Body()) >= ToTheWaterMaxMetres - 1f)
                leg.Why = (leg.Why ?? "") + $" (still dry after {ToTheWaterMaxMetres} m south)";
        }

        IEnumerator AlongTheEdge(Leg leg, float sign, float untilX)
        {
            float budget = Mathf.Abs(untilX - Body().x) / LegMetresPerSecondFloor + LegSlackSeconds;
            yield return Pursue(leg, budget, at => (at.x - untilX) * sign >= 0f, at =>
            {
                float d = Depth(at);
                float steer = float.IsNegativeInfinity(d) ? -1f : Mathf.Clamp((d - EdgeTargetDepth) * EdgeSteerGain, -1f, 1f);
                return new Vector2(sign, steer).normalized;   // wetter than the mark: turn up the beach (+y)
            });
        }

        /// <summary>Hold an intent toward <paramref name="aim"/> until <paramref name="arrived"/>, a stall or the
        /// budget. The intent lands in the controller's Update, so every step sets it, then yields a frame.</summary>
        IEnumerator Pursue(Leg leg, float budget, Func<Vector2, bool> arrived, Func<Vector2, Vector2> aim)
        {
            Time.timeScale = 1f;
            float spent = 0f, sinceCheck = 0f;
            Vector2 checkpoint = Body(), was = Body();
            while (true)
            {
                Vector2 at = Body();
                if (arrived(at)) { leg.Reached = true; break; }
                leg.Aim = aim(at);
                _held.Walk(leg.Aim, true);
                yield return null;

                float dt = Time.deltaTime;
                spent += dt;
                sinceCheck += dt;
                Vector2 now = Body();
                leg.Metres += Vector2.Distance(was, now);
                was = now;
                if (sinceCheck >= StallWindowSeconds)
                {
                    if (Vector2.Distance(now, checkpoint) < StallMetres) { leg.Why = "stalled"; break; }
                    checkpoint = now;
                    sinceCheck = 0f;
                }
                if (spent > budget) { leg.Why = $"out of time ({budget:0.#} s)"; break; }
            }
            _held.Release();
            yield return null;
            leg.End = Body();
            leg.Seconds = spent;
            if (!leg.Reached) leg.Blockers = WhatStopsHer(leg.Aim);
            _legs.Add(leg);
            Debug.Log($"[{PlateDir}] {leg}");
        }

        /// <summary>Every solid collider her foot touches, and the first she would hit walking on: by path, type
        /// and layer, with the ground and water where she stands.</summary>
        string WhatStopsHer(Vector2 aim)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(_player.gameObject.layer));
            Vector2 centre = _foot.bounds.center;
            float radius = Mathf.Max(_foot.bounds.extents.x, _foot.bounds.extents.y);
            var touching = new List<Collider2D>();
            Physics2D.OverlapCircle(centre, radius + FootProbeSlack, filter, touching);
            var ahead = new List<RaycastHit2D>();
            Physics2D.CircleCast(centre, radius, aim, filter, ahead, AheadProbeMetres);
            string Name(Collider2D c) => $"{PathOf(c.transform)} ({c.GetType().Name}, layer {LayerMask.LayerToName(c.gameObject.layer)})";
            var touch = touching.Where(c => c != null && c.attachedRigidbody != _body).Select(Name).Distinct().ToList();
            var hit = ahead.Where(h => h.collider != null && h.collider.attachedRigidbody != _body)
                           .Select(h => $"{Name(h.collider)} at {h.distance:0.##} m").Distinct().ToList();
            float d = Depth(Body());
            return $"foot touches [{string.Join("; ", touch)}]; ahead ({aim.x:0.##}, {aim.y:0.##}) hits [{string.Join("; ", hit)}]; " +
                   $"depth {d:0.###} m, ground {Water() - d:0.###} m, water {Water():0.###} m";
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        void WriteTheWalkLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"the main beach walk, run started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z; {_saveLine}");
            sb.AppendLine($"hour, sun and tide: {_pinned}");
            sb.AppendLine($"the walker: tide-gated {_tideGated}; foot {_foot.GetType().Name} {_foot.bounds.size.x:0.###} x {_foot.bounds.size.y:0.###} m; " +
                          $"layer {LayerMask.LayerToName(_player.gameObject.layer)}");
            foreach (Leg l in _legs) sb.AppendLine((l.Survey ? "  survey " : "  route  ") + l);
            SaveCaption("beach-walk-log.txt", sb.ToString());
        }

        // =============================================================================================
        //  The region, the hour, the save and the frame
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered and nothing " +
                              "was walked. Expected on CI; the beach's plates need a GPU.");
        }

        IEnumerator LoadStPeters()
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;
            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the region logs decor complaints of its own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0) _healed = $"{StPeters}: re-published {string.Join(", ", healed)}";

            int frames = 0;
            while (frames < 240 && FindPersistentFollow() == null) { yield return null; frames++; }
            Assert.IsNotNull(FindPersistentFollow(), $"{StPeters}: no CameraFollow on the persistent core's camera after 240 frames.");
            Assert.IsNotNull(GameServices.Environment, "St Peters registered no environment, so there is no tide to read the water's edge by.");
        }

        /// <summary>The run's own save is the one open; the shared save is not.</summary>
        void AssertTheSaveIsTheRunsOwn()
        {
            string active = SaveStore.ActivePath;
            Assert.That(active, Is.Not.Null.And.Not.Empty, $"no {SaveStore.TestRunSaveVariable} redirect: this case never opens the shared save.");
            Assert.That(Path.GetFullPath(active), Is.Not.EqualTo(Path.GetFullPath(SaveStore.DefaultPath)).IgnoreCase,
                "the active save IS the shared save; the case stops before it walks.");
            SaveService save = Object.FindAnyObjectByType<SaveService>();
            if (save != null)
                Assert.That(Path.GetFullPath(save.SavePath), Is.EqualTo(Path.GetFullPath(active)).IgnoreCase,
                    "the running save service holds another file than the run's redirect.");
            _saveLine = $"save: the run's own ({active}); the shared save is not open";
        }

        /// <summary>A case that loads St Peters straight sits at the title, and the title holds the world's input
        /// (<see cref="ShellFlow.WorldInputBlocked"/>), so the walker is parked. The run's OWN save (asserted
        /// just before) is marked arrived, so entering the world does not play the arrival opening; then the world
        /// is entered the way Continue enters it.</summary>
        IEnumerator EnterTheWorldAshore()
        {
            if (GameServices.Save == null)
            {
                // The save service is made once, at play start (BeforeSceneLoad); a case before this one ended in
                // GameServices.Reset(), which unpublishes it but leaves it alive. Re-publish it, its file checked first.
                SaveService live = Object.FindAnyObjectByType<SaveService>();
                Assert.IsNotNull(live, "no save service is alive, so the arrival cannot be marked done.");
                Assert.That(Path.GetFullPath(live.SavePath), Is.EqualTo(Path.GetFullPath(SaveStore.ActivePath)).IgnoreCase,
                    "the live save service holds another file than the run's redirect.");
                GameServices.Save = live;
                _healed = (_healed == "none" ? "" : _healed + "; ") + "re-published the run's save service";
            }
            GameServices.Save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
            _enteredWorld = ShellFlow.AtTitle;
            if (_enteredWorld) ShellFlow.ContinueGame();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(ShellFlow.WorldInputBlocked, "the shell still holds the world's input after Continue.");
            ArrivalOpening opening = Object.FindAnyObjectByType<ArrivalOpening>();
            Assert.IsTrue(opening == null || !opening.IsRunning, "the arrival opening is running: she is on its boat, not ashore.");
        }

        IEnumerator PinTheHour(float hours)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");
            double t = (1.0 + hours / 24.0) * config.SecondsPerDay;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0, $"the clock did not land on {t:0.0} s.");
            float waited = 0f;
            while (waited < 1f) { yield return null; waited += Time.deltaTime; }
            float hour = clock.HourOfDay;
            _pinned = $"day {clock.DayIndex} {hour:0.000} h (TotalSeconds {clock.TotalSeconds:0.0}); water {Water():+0.000;-0.000} m; " +
                      $"_SunElevation {Shader.GetGlobalFloat("_SunElevation"):0.000}";
        }

        /// <summary>The play camera's own follow, on a still anchor at the on-foot stop, the 1080 px target attached
        /// before the follow is asked its height (the stop's metres depend on the grid it is asked on).</summary>
        IEnumerator FrameOn(Vector2 at)
        {
            if (_follow == null)
            {
                _follow = FindPersistentFollow();
                Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
                _cam = _follow.GetComponent<Camera>();
                Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            }
            if (!_followCaptured)
            {
                _followTargetOnEntry = _follow.Target;
                _followSmoothOnEntry = _follow.Smooth;
                _followCaptured = true;
            }

            Time.timeScale = 0f;
            if (_anchor == null) { _anchor = new GameObject("MainBeachPlateAnchor"); _spawned.Add(_anchor); }
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            if (_rt == null)
            {
                _w = PlateWidthPx;
                _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            _askedGridPx = _cam.targetTexture.height;
            _askedHeight = _follow.WorldHeightFor(CameraFraming.OnFoot);
            _follow.SetFraming(_askedHeight, 0f);
            for (int i = 0; i < 2; i++) yield return null;

            // The camera rests on its own pixel grid (CameraFollow.ApplyPixelGrid: PixelGrid.Snap with the follow's
            // WorldUnitsPerRenderedPixel, under GameServices.PixelGridSnap). So the ASKED centre is snapped to that
            // same grid, here where the plate's target and stop are set and the grid is the plate's, before the
            // camera is asked to sit on it. The bar below stays 0.001 m.
            float grid = _follow.WorldUnitsPerRenderedPixel;
            if (grid > 0f && GameServices.PixelGridSnap) at = PixelGrid.Snap(at, grid);
            _askedCentre = at;
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s for the look-ahead to ease out";
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on ({at.x:0.###}, {at.y:0.###}), after {_eased}.");
            Assert.That(worldHeight, Is.EqualTo(_askedHeight).Within(0.5f),
                $"the camera frames {worldHeight:0.###} m tall, not the on-foot stop its follow was asked for ({_askedHeight:0.###} m).");
        }

        /// <summary>A plate of where she stands now, then the camera goes back to her and the world runs.</summary>
        IEnumerator Plate(string label, Leg leg)
        {
            Vector2 at = Body();
            yield return FrameOn(at);
            string name = $"beach-walk-{++_plateIndex:00}-{label}";
            SavePlate(name + ".png", Capture(), _w);
            float d = Depth(at);
            string her = $"her: body ({at.x:0.###}, {at.y:0.###}); the plate's centre is her body snapped to the camera's grid, ({_askedCentre.x:0.#####}, {_askedCentre.y:0.#####}); depth {d:0.###} m, ground {Water() - d:0.###} m, water {Water():0.###} m; tide-gated {_tideGated}";
            SaveCaption(name + ".txt", Caption(name, label, _askedCentre, her + (leg != null ? "\nleg: " + leg : "")));
            _follow.Target = _player.transform;
            _follow.Smooth = _followSmoothOnEntry;
            Time.timeScale = 1f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f || Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never came to rest.");
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>()))
                    return f;
            return null;
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;
            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        string Caption(string plate, string subject, Vector2 centre, string extra)
        {
            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            var sb = new StringBuilder();
            sb.AppendLine(plate);
            sb.AppendLine(subject);
            sb.AppendLine($"run: started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, " +
                          $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; services healed: {_healed}");
            sb.AppendLine(_saveLine);
            sb.AppendLine($"asked centre ({centre.x:0.###}, {centre.y:0.###}); camera at ({p.x:0.####}, {p.y:0.####}, {p.z:0.###}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.#####} -> {worldHeight * _cam.aspect:0.###} x {worldHeight:0.####} m, render texture {_w}x{_h} px");
            sb.AppendLine($"framing: asked {_askedHeight:0.###} m (WorldHeightFor OnFoot on a {_askedGridPx} px grid), framed {worldHeight:0.###} m; committed {_follow.Framing} (known {_follow.FramingKnown})");
            sb.AppendLine($"hour, sun and tide: {_pinned}");
            sb.AppendLine($"OpeningCinematicRunning {GameServices.OpeningCinematicRunning}; Time.timeScale {Time.timeScale}");
            if (extra != null) sb.AppendLine(extra);
            return sb.ToString();
        }

        static string PlatePath()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void SavePlate(string name, byte[] rgbaBottomLeft, int width)
        {
            var tex = new Texture2D(width, rgbaBottomLeft.Length / (width * 4), TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static void SaveCaption(string name, string text) =>
            File.WriteAllText(Path.Combine(PlatePath(), name), text, new UTF8Encoding(false));

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

        static List<GameObject> PersistentRoots()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
#endif
