using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.App;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The three moments</b> (juice charter §4, owner ruling 2026-09-09): the landing frame, the sale,
    /// and the action timing that makes a dig and a cast land — plus the silent dig of #803.
    ///
    /// <para>Every curve, threshold and event the charter names is pinned here, on the pure math where
    /// there is math and on the presenters' public hooks where there is a presenter (EditMode never runs
    /// <c>OnEnable</c>, so each hook is driven the way the bus would drive it). The rig for the dig mirrors
    /// <see cref="CatchInHandTests"/> exactly — a fixture that left <c>GameServices.CatchHands</c> null
    /// would take the pail path and be green about the wrong game.</para>
    ///
    /// <para>What is NOT here: any editor-only observation. Frame cost of the splash and coin pools, the
    /// feel of a 90 ms hit-stop and the readability of the count-ups are the slot's to judge.</para>
    /// </summary>
    public class JuiceMomentsTests
    {
        // ---- the fixture services (mirrors CatchInHandTests, byte for byte where it matters) ------------

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 1.0f;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0.5f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class StampClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<JuiceMomentCue> _cues = new();
        private readonly List<string> _notices = new();
        private int _landed;
        private float _timeScaleWas;

        private void OnCue(JuiceMomentCue e) => _cues.Add(e);
        private void OnNotice(DevNotice e) => _notices.Add(e.Text);
        private void OnLanded(CatchLanded e) => _landed++;

        [SetUp]
        public void SetUp()
        {
            _cues.Clear(); _notices.Clear(); _landed = 0;
            _timeScaleWas = Time.timeScale;
            EventBus.Clear<JuiceMomentCue>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<FishCaught>();
            EventBus.Subscribe<JuiceMomentCue>(OnCue);
            EventBus.Subscribe<DevNotice>(OnNotice);
            EventBus.Subscribe<CatchLanded>(OnLanded);
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            IconRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<JuiceMomentCue>(OnCue);
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Unsubscribe<CatchLanded>(OnLanded);
            EventBus.Clear<JuiceMomentCue>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<FishCaught>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            IconRegistry.Reset();
            GameServices.Reset();
            Time.timeScale = _timeScaleWas;   // a hit-stop test must never leave the editor at 5 %
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private static readonly ActionTiming T = new ActionTiming(0.1f, 0.2f, 0.3f, 0.15f);

        private ActionTimingDef Def(float preHold, float strike, float follow, float settle, string id = "timing.test")
        {
            var d = ScriptableObject.CreateInstance<ActionTimingDef>();
            d.Id = id;
            d.PreHoldSeconds = preHold; d.StrikeSeconds = strike;
            d.FollowThroughSeconds = follow; d.SettleSeconds = settle;
            _spawned.Add(d);
            return d;
        }

        private static CatchItem Clam(float kg = 0.12f)
            => new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", FishCategory.Shellfish, kg, 2, 0.45f);

        private static CatchItem Cod(float kg = 1.23f)
            => new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, kg, 12, 0.6f);

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 8);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(s);
            return s;
        }

        private T AddOn<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        private NotebookPresenter StandABook()
        {
            var book = AddOn<NotebookPresenter>("book");
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore());
            book.OverrideJuice(JuiceSettings.Default);
            return book;
        }

        private static string Source(string relative)
            => File.ReadAllText(Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar)));

        // =============================================================================================
        // §4.3 ACTION TIMING — the four beats, pinned on the math
        // =============================================================================================

        [Test]
        public void FrameFor_holds_frame_0_through_the_pre_hold_sweeps_the_strike_and_holds_the_last()
        {
            Assert.AreEqual(0, ActionTimingMath.FrameFor(0.05f, in T, 6), "pre-hold: frame 0");
            Assert.AreEqual(1, ActionTimingMath.FrameFor(0.10f, in T, 6), "the strike begins on frame 1");
            Assert.AreEqual(3, ActionTimingMath.FrameFor(0.20f, in T, 6), "halfway through the strike");
            Assert.AreEqual(5, ActionTimingMath.FrameFor(0.30f, in T, 6), "the last frame at the strike's end");
            Assert.AreEqual(5, ActionTimingMath.FrameFor(0.60f, in T, 6), "…held through the follow-through");
            Assert.AreEqual(5, ActionTimingMath.FrameFor(9.00f, in T, 6), "…and forever after");
        }

        [Test]
        public void FrameFor_is_monotone_across_the_whole_action()
        {
            int last = -1;
            for (float s = 0f; s <= T.TotalSeconds + 0.1f; s += 0.005f)
            {
                int f = ActionTimingMath.FrameFor(s, in T, 8);
                Assert.GreaterOrEqual(f, last, $"the frame went backwards at {s:0.000}s");
                Assert.LessOrEqual(f, 7);
                last = f;
            }
        }

        [Test]
        public void FrameFor_degenerate_cases()
        {
            Assert.AreEqual(0, ActionTimingMath.FrameFor(0.5f, in T, 1), "one frame is frame 0");
            Assert.AreEqual(0, ActionTimingMath.FrameFor(0.5f, in T, 0), "no frames is frame 0");
            var cut = new ActionTiming(0.1f, 0f, 0.2f, 0f);
            Assert.AreEqual(0, ActionTimingMath.FrameFor(0.05f, in cut, 6), "zero strike: frame 0 in the pre-hold");
            Assert.AreEqual(5, ActionTimingMath.FrameFor(0.10f, in cut, 6), "zero strike: a hard cut to the last frame");
        }

        [Test]
        public void BeatAt_names_the_four_beats_and_Done()
        {
            Assert.AreEqual(ActionBeat.PreHold,       ActionTimingMath.BeatAt(0.05f, in T));
            Assert.AreEqual(ActionBeat.Strike,        ActionTimingMath.BeatAt(0.15f, in T));
            Assert.AreEqual(ActionBeat.FollowThrough, ActionTimingMath.BeatAt(0.40f, in T));
            Assert.AreEqual(ActionBeat.Settle,        ActionTimingMath.BeatAt(0.70f, in T));
            Assert.AreEqual(ActionBeat.Done,          ActionTimingMath.BeatAt(0.75f, in T));
            Assert.IsFalse(ActionTimingMath.IsDone(0.74f, in T));
            Assert.IsTrue(ActionTimingMath.IsDone(0.75f, in T));
        }

        [Test]
        public void Strike01_runs_0_to_1_across_the_strike_only()
        {
            Assert.AreEqual(0f, ActionTimingMath.Strike01(0.05f, in T), 1e-5f, "before the strike");
            Assert.AreEqual(0.5f, ActionTimingMath.Strike01(0.20f, in T), 1e-5f, "halfway");
            Assert.AreEqual(1f, ActionTimingMath.Strike01(0.50f, in T), 1e-5f, "after the strike");
        }

        [Test]
        public void The_three_timing_Defs_load_from_Resources_with_ids_and_non_zero_beats()
        {
            var cast = Resources.Load<ActionTimingDef>(PlayerFishingAnimator.CastTimingResource);
            var haul = Resources.Load<ActionTimingDef>(PlayerHaulAnimator.HaulTimingResource);
            var dig  = Resources.Load<ActionTimingDef>(LiftFeedback.DigTimingResource);
            Assert.IsNotNull(cast, "Resources/" + PlayerFishingAnimator.CastTimingResource);
            Assert.IsNotNull(haul, "Resources/" + PlayerHaulAnimator.HaulTimingResource);
            Assert.IsNotNull(dig,  "Resources/" + LiftFeedback.DigTimingResource);
            Assert.AreEqual("timing.cast", cast.Id);
            Assert.AreEqual("timing.haul", haul.Id);
            Assert.AreEqual("timing.dig",  dig.Id);
            Assert.Greater(cast.Timing.StrikeSeconds, 0f, "a cast has a strike");
            Assert.Greater(haul.Timing.FollowThroughSeconds + haul.Timing.SettleSeconds, 0f, "a haul has a tail");
            Assert.Greater(dig.Timing.PreHoldSeconds + dig.Timing.StrikeSeconds, 0f, "a dig has a beat before the lift");
        }

        // =============================================================================================
        // §4.1 THE LANDING FRAME — hit-stop
        // =============================================================================================

        [Test]
        public void HitStop_ignores_a_zero_duration_and_clamps_the_scale()
        {
            var h = new HitStop();
            h.Trigger(0.05f, 0f);
            Assert.IsFalse(h.Active, "a zero-second stop is no stop");
            Assert.AreEqual(1f, h.Scale, "inactive reads as full speed");
            h.Trigger(-3f, 0.1f);
            Assert.IsTrue(h.Active);
            Assert.AreEqual(0f, h.Scale, 1e-6f, "a negative scale clamps to a full freeze");
            h.Reset();
            h.Trigger(7f, 0.1f);
            Assert.AreEqual(1f, h.Scale, 1e-6f, "a scale above one clamps to no dip");
        }

        [Test]
        public void HitStop_overlap_keeps_the_longer_remaining_and_the_deeper_dip()
        {
            var h = new HitStop();
            h.Trigger(0.05f, 0.09f);
            h.Tick(0.04f);
            h.Trigger(0.5f, 0.02f);   // a shallower, shorter stop arrives mid-hold
            Assert.AreEqual(0.05f, h.Scale, 1e-6f, "the deeper dip stays");
            Assert.AreEqual(0.05f, h.Remaining, 1e-6f, "the longer remaining stays");
        }

        [Test]
        public void HitStop_returns_exactly_one_on_expiry_and_goes_inactive()
        {
            var h = new HitStop();
            h.Trigger(0.05f, 0.09f);
            Assert.AreEqual(0.05f, h.Tick(0.05f), 1e-6f, "still held");
            Assert.IsTrue(h.Active);
            float last = h.Tick(0.05f);
            Assert.AreEqual(1f, last, "the expiry frame hands back EXACTLY one — a 0.9999 here drifts the world forever");
            Assert.IsFalse(h.Active);
            Assert.AreEqual(1f, h.Scale);
        }

        [Test]
        public void HitStopHost_dips_the_time_scale_on_the_landing_and_puts_back_exactly_what_it_found()
        {
            var host = AddOn<HitStopHost>("hitstop");
            host.OverrideSettings(JuiceSettings.Default);
            Time.timeScale = 0.7f;   // whatever the world was running at

            host.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 2f));
            Assert.IsTrue(host.Holding);
            Assert.AreEqual(0.7f, host.Restore, 1e-6f, "the found value is what goes back");
            Assert.AreEqual(0.7f * JuiceSettings.Default.LandingHitStopScale, Time.timeScale, 1e-6f, "the dip multiplies the found scale");

            host.TickFrame(0.05f);
            Assert.IsTrue(host.Holding, "90 ms: still held at 50");
            host.TickFrame(0.05f);
            Assert.IsFalse(host.Holding, "…released at 100");
            Assert.AreEqual(0.7f, Time.timeScale, "put back EXACTLY — not 0.7 * 1.0000001");
        }

        [Test]
        public void HitStopHost_ignores_the_other_moments_and_a_disabled_charter()
        {
            var host = AddOn<HitStopHost>("hitstop");
            host.OverrideSettings(JuiceSettings.Default);
            Time.timeScale = 1f;
            host.OnMoment(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 0.1f));
            host.OnMoment(new JuiceMomentCue(JuiceMoment.CastEntry, Vector2.zero, 0f));
            host.OnMoment(new JuiceMomentCue(JuiceMoment.Sale, Vector2.zero, 48f));
            Assert.IsFalse(host.Holding, "only the landing stops the clock");
            Assert.AreEqual(1f, Time.timeScale);

            var off = JuiceSettings.Default; off.MomentsEnabled = false;
            host.OverrideSettings(off);
            host.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 2f));
            Assert.IsFalse(host.Holding, "MomentsEnabled=false is the whole lane's off switch");
            Assert.AreEqual(1f, Time.timeScale);
        }

        // =============================================================================================
        // §4.1 / §4.2 COUNT-UP — the notebook's weight readout and the purse
        // =============================================================================================

        [Test]
        public void CountUpMath_never_shows_the_target_early_and_eases_out()
        {
            Assert.AreEqual(0, CountUpMath.ValueAt(0, 120, 0f));
            Assert.AreEqual(120, CountUpMath.ValueAt(0, 120, 1f));
            for (float t = 0.01f; t < 1f; t += 0.01f)
                Assert.Less(CountUpMath.ValueAt(0, 120, t), 120, $"the target showed at t={t:0.00} — the pop would land on a number already seen");
            Assert.Greater(CountUpMath.ValueAt(0, 120, 0.5f), 60, "ease-out: more than half the climb in the first half");
            Assert.AreEqual(-7, CountUpMath.ValueAt(-7, -7, 0.3f), "a flat climb sits still");
        }

        [Test]
        public void CountUp_lands_exactly_once_and_reports_time_since()
        {
            var c = new CountUp();
            Assert.AreEqual(-1f, c.SinceLanded, "never landed");
            c.Start(0, 12, 0.6f);
            Assert.IsTrue(c.Running);
            Assert.AreEqual(0, c.Value, "the climb starts from the FROM value");
            c.Tick(0.3f);
            Assert.IsTrue(c.Running);
            Assert.Greater(c.Value, 0); Assert.Less(c.Value, 12);
            Assert.IsFalse(c.Landed);
            bool landedNow = c.Tick(0.35f);
            Assert.IsTrue(landedNow, "Tick returns true on the landing tick");
            Assert.IsTrue(c.Landed);
            Assert.AreEqual(12, c.Value);
            Assert.IsFalse(c.Running);
            c.Tick(0.1f);
            Assert.IsFalse(c.Landed, "Landed is ONE tick — the pop keys off it");
            Assert.AreEqual(0.1f, c.SinceLanded, 1e-4f);
        }

        [Test]
        public void Pop_is_one_at_the_edges_and_the_full_scale_at_its_peak()
        {
            Assert.AreEqual(1f, CountUpMath.Pop(-1f, 1.35f, 0.18f), 1e-6f, "never landed: no pop");
            Assert.AreEqual(1f, CountUpMath.Pop(0f, 1.35f, 0.18f), 1e-4f, "the landing tick starts at one");
            Assert.AreEqual(1.35f, CountUpMath.Pop(0.09f, 1.35f, 0.18f), 1e-4f, "the peak is the tunable");
            Assert.AreEqual(1f, CountUpMath.Pop(0.18f, 1.35f, 0.18f), 1e-6f, "back to one at the end");
            Assert.AreEqual(1f, CountUpMath.Pop(5f, 1.35f, 0.18f), 1e-6f, "and long after");
        }

        [Test]
        public void WriteFixed_writes_tenths_without_a_string_per_frame()
        {
            var sb = new StringBuilder();
            CountUpMath.WriteFixed(sb, 3, 1);    Assert.AreEqual("0.3", sb.ToString());  sb.Clear();
            CountUpMath.WriteFixed(sb, 120, 1);  Assert.AreEqual("12.0", sb.ToString()); sb.Clear();
            CountUpMath.WriteFixed(sb, -7, 0);   Assert.AreEqual("-7", sb.ToString());   sb.Clear();
            CountUpMath.WriteFixed(sb, 1234, 2); Assert.AreEqual("12.34", sb.ToString()); sb.Clear();
        }

        // =============================================================================================
        // §4.4 THE SILENT DIG (#803) — the lift line
        // =============================================================================================

        [Test]
        public void LiftLine_says_what_is_in_her_hand()
        {
            Assert.AreEqual("Lifted out a soft-shell clam — it's in your hand.", LiftLine.For(Clam()));
            Assert.AreEqual("Landed an atlantic cod — it's in your hand.", LiftLine.For(Cod()));
            var nameless = new CatchItem("fish.x", "", FishCategory.InshoreGroundfish, 1f, 1, 0.5f);
            Assert.AreEqual("Landed a catch — it's in your hand.", LiftLine.For(nameless));
        }

        [Test]
        public void A_dig_lands_ONE_CatchLanded_ONE_DigStrike_cue_at_the_hole_and_the_hand_DRAWS_the_clam()
        {
            // The #803 guard: CatchLanded has exactly one on-screen consequence per dig and the hand shows
            // the clam. The rig is CatchInHandTests' — shovel in hand, a pail to fall back on, a hole.
            var clock = new StampClock { TotalSeconds = 1000d };
            var save = SaveMigration.NewGame();
            save.OwnedGear.Add("gear.shovel");
            save.OwnedGear.Add("gear.bucket");
            GameServices.Clock = clock;
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Save = new FakeSaveService(save);

            var species = ScriptableObject.CreateInstance<FishSpeciesDef>();
            species.Id = "fish.soft_shell_clam"; species.DisplayName = "Soft-shell Clam";
            species.Category = FishCategory.Shellfish;
            species.MinWeightKg = 0.05f; species.MaxWeightKg = 0.2f; species.BaseValue = 2;
            species.SupplyElasticity = 0.45f; species.SpoilPerDay = 0.5f;
            _spawned.Add(species);

            var pail = AddOn<ClamBucket>("Pail");
            pail.Configure(20, requireOwnedBucket: false);

            var dig = AddOn<ClamDig>("ClamHole");
            dig.Configure(species, pail, dig.transform, "gear.shovel", seed: 7, reachRadius: 1.25f);
            dig.ConfigureInteract("fixture.clam_hole.test", ToolDef.ShovelId);

            IconRegistry.Register("fish.soft_shell_clam", MakeSprite());   // what the notebook/pail art registers in play
            CarryHands hands = ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            Assert.IsTrue(dig.TryDig());

            Assert.AreEqual(1, _landed, "CatchLanded fires ONCE per dig");
            Assert.AreEqual(0, pail.UsedUnits, "…and the pail is untouched (the in-hand ruling)");

            var strikes = _cues.FindAll(c => c.Kind == JuiceMoment.DigStrike);
            Assert.AreEqual(1, strikes.Count, "one DigStrike cue per dig — the sand chunks and the audio slot both key off it");
            Assert.AreEqual(dig.SpotPos.x, strikes[0].X, 1e-4f, "the cue is AT THE HOLE");
            Assert.AreEqual(dig.SpotPos.y, strikes[0].Y, 1e-4f);
            Assert.GreaterOrEqual(strikes[0].Strength, 0.05f, "strength is the clam's weight in kg");
            Assert.LessOrEqual(strikes[0].Strength, 0.2f);
            Assert.AreEqual(0, _cues.FindAll(c => c.Kind != JuiceMoment.DigStrike).Count, "a dig is not a landing, a cast or a sale");

            var carried = hands.Carried as CarriableCatch;
            Assert.IsNotNull(carried, "the clam is in her hand");
            var sr = carried.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(sr, "the carriable owns a renderer");
            Assert.IsNotNull(sr.sprite, "and it DRAWS — #803 was feedback, not a missing sprite");
        }

        [Test]
        public void LiftFeedback_speaks_ONCE_on_the_dig_Defs_strike_beat_and_never_again()
        {
            var lift = AddOn<LiftFeedback>("lift");
            lift.ConfigureTiming(Def(0.1f, 0.08f, 0.25f, 0.15f, "timing.dig.test"));
            var clam = Clam();

            lift.OnLanded(new CatchLanded(in clam));
            Assert.IsTrue(lift.Armed);
            Assert.AreEqual(0, _notices.Count, "nothing on the landing tick — the shovel is still coming up");
            lift.TickFrame(0.1f);
            Assert.AreEqual(0, _notices.Count, "the pre-hold is silent");
            lift.TickFrame(0.05f);
            Assert.AreEqual(0, _notices.Count, "0.15 < 0.18: mid-strike, still silent");
            lift.TickFrame(0.05f);
            Assert.AreEqual(1, _notices.Count, "the strike lands: ONE line");
            Assert.AreEqual(LiftLine.For(clam), _notices[0]);
            Assert.IsFalse(lift.Armed);
            lift.TickFrame(1f); lift.TickFrame(1f);
            Assert.AreEqual(1, _notices.Count, "and never a repeat");
        }

        [Test]
        public void LiftFeedback_with_a_zero_Def_speaks_on_the_landing_tick()
        {
            var lift = AddOn<LiftFeedback>("lift");
            lift.ConfigureTiming(Def(0f, 0f, 0f, 0f, "timing.zero"));
            var cod = Cod();
            lift.OnLanded(new CatchLanded(in cod));
            Assert.AreEqual(1, _notices.Count);
            Assert.AreEqual("Landed an atlantic cod — it's in your hand.", _notices[0]);
            Assert.IsFalse(lift.Armed);
        }

        // =============================================================================================
        // §4.1 / §4.3 THE BURSTS — pooled, sized by weight, dead on time
        // =============================================================================================

        [Test]
        public void MomentBurstMath_sizes_the_splash_by_weight_and_staggers_the_rings()
        {
            Assert.AreEqual(6,  MomentBurstMath.SplashDrops(0f, 6, 3f, 24), "the floor");
            Assert.AreEqual(12, MomentBurstMath.SplashDrops(2f, 6, 3f, 24), "6 + 3/kg");
            Assert.AreEqual(24, MomentBurstMath.SplashDrops(40f, 6, 3f, 24), "the cap");
            Assert.AreEqual(0,  MomentBurstMath.SplashDrops(2f, -9, 3f, 24), "never negative");
            Assert.AreEqual(0f, MomentBurstMath.RingDelay(0, 3, 0.6f, 0.5f), "the first ring is born now");
            Assert.AreEqual(0.15f, MomentBurstMath.RingDelay(1, 3, 0.6f, 0.5f), 1e-5f);
            Assert.AreEqual(0.30f, MomentBurstMath.RingDelay(2, 3, 0.6f, 0.5f), 1e-5f, "the last ring at lifetime*stagger");
            Assert.AreEqual(0f, MomentBurstMath.RingDelay(0, 1, 0.6f, 0.5f), "one ring has no stagger");
        }

        [Test]
        public void Emitter_spawns_the_charters_counts_per_moment_and_kills_them_on_time()
        {
            var em = AddOn<MomentBurstEmitter>("bursts");
            em.OverrideSettings(JuiceSettings.Default);
            var j = JuiceSettings.Default;

            em.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, new Vector2(3f, 4f), 2f));
            Assert.AreEqual(12, em.AliveCount, "a 2 kg landing: 6 + 3*2 drops");
            em.Tick(0.1f);
            Assert.AreEqual(12, em.AliveCount, "still falling");
            em.Tick(j.LandingSplashSeconds);
            Assert.AreEqual(0, em.AliveCount, "dead at the lifetime — nothing lingers");

            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 0.1f));
            Assert.AreEqual(j.SandChunkCount, em.AliveCount, "the sand chunks");
            em.Clear();
            Assert.AreEqual(0, em.AliveCount);

            em.OnMoment(new JuiceMomentCue(JuiceMoment.CastEntry, Vector2.zero, 0f));
            Assert.AreEqual(j.CastRingCount, em.AliveCount, "the rings (the staggered ones count as alive from birth)");
            em.Tick(j.CastRingSeconds * 2f);
            Assert.AreEqual(0, em.AliveCount, "every ring, staggered or not, is gone by twice the lifetime");

            em.OnMoment(new JuiceMomentCue(JuiceMoment.Sale, Vector2.zero, 48f));
            Assert.AreEqual(0, em.AliveCount, "the sale is the notebook's — nothing in the world");
        }

        [Test]
        public void Emitter_is_silent_when_the_charter_is_off_and_never_exceeds_its_pool()
        {
            var em = AddOn<MomentBurstEmitter>("bursts");
            var off = JuiceSettings.Default; off.MomentsEnabled = false;
            em.OverrideSettings(off);
            em.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 2f));
            Assert.AreEqual(0, em.AliveCount, "MomentsEnabled=false");

            em.OverrideSettings(JuiceSettings.Default);
            for (int i = 0; i < 6; i++) em.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 10f));
            Assert.LessOrEqual(em.AliveCount, MomentBurstConfig.Default.PoolSize, "six capped splashes recycle the oldest — the pool is the ceiling (rule 7)");
            Assert.Greater(em.AliveCount, 0);
        }

        // =============================================================================================
        // §4.3 THE FRAME PLAYERS — the cast's flick and the haul's follow-through
        // =============================================================================================

        [Test]
        public void The_cast_release_plays_frame_0_through_the_pre_hold_and_sweeps_the_strike()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();
            var anim = go.AddComponent<PlayerFishingAnimator>();
            anim.ConfigureCastTiming(Def(0.06f, 0.12f, 0.2f, 0.15f, "timing.cast.test"));

            Assert.AreEqual(0, anim.CastReleaseFrameAt(0.03f, 6), "the pre-hold holds frame 0");
            Assert.AreEqual(1, anim.CastReleaseFrameAt(0.06f, 6), "the strike begins on frame 1");
            Assert.AreEqual(5, anim.CastReleaseFrameAt(0.18f, 6), "the strike ends on the last frame");
            Assert.AreEqual(5, anim.CastReleaseFrameAt(0.40f, 6), "…held through the follow-through and settle");
        }

        [Test]
        public void The_haul_holds_its_last_frame_for_the_Defs_tail_then_hands_the_walk_sprite_back()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++) frames[i] = MakeSprite();
            Sprite walk = MakeSprite();
            sr.sprite = walk;
            var anim = go.AddComponent<PlayerHaulAnimator>();
            anim.Configure(frames);
            anim.ConfigureHaulTiming(Def(0f, 0f, 0.25f, 0.15f, "timing.haul.test"));

            anim.OnHaulStateChanged(new TrapHaulStateChanged(new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.6f, false, 0f, 0f)));
            Assert.AreNotSame(walk, sr.sprite, "the haul owns the renderer");
            Sprite heave = sr.sprite;

            anim.OnHaulStateChanged(new TrapHaulStateChanged(TrapHaulState.Idle));
            Assert.IsTrue(anim.Tailing, "the haul is over but the heave LINGERS");
            Assert.AreEqual(HaulPose.None, anim.Pose, "…while the pose already reads None (the haul is over)");
            Assert.AreSame(heave, sr.sprite, "the last frame holds");

            anim.TickTail(0.2f);
            Assert.IsTrue(anim.Tailing, "0.2 < 0.4: still held");
            Assert.AreSame(heave, sr.sprite);
            anim.TickTail(0.3f);
            Assert.IsFalse(anim.Tailing, "0.5 >= 0.4: released");
            Assert.AreSame(walk, sr.sprite, "the walk sprite gets the renderer back exactly");
        }

        [Test]
        public void The_haul_without_a_Def_hands_back_immediately_as_it_always_did()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++) frames[i] = MakeSprite();
            Sprite walk = MakeSprite();
            sr.sprite = walk;
            var anim = go.AddComponent<PlayerHaulAnimator>();
            anim.Configure(frames);   // no ConfigureHaulTiming: the shipped behaviour, no Resources fallback

            anim.OnHaulStateChanged(new TrapHaulStateChanged(new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.6f, false, 0f, 0f)));
            anim.OnHaulStateChanged(new TrapHaulStateChanged(TrapHaulState.Idle));
            Assert.IsFalse(anim.Tailing);
            Assert.AreSame(walk, sr.sprite, "unwired = the immediate hand-back the four haul suites pin");
        }

        // =============================================================================================
        // §4.1 / §4.2 / §4.4 THE NOTEBOOK — the register line, the weight readout, the sale
        // =============================================================================================

        [Test]
        public void The_notebook_registers_the_lift_line_on_CatchLanded()
        {
            var book = StandABook();
            var clam = Clam();
            book.OnCatchLanded(new CatchLanded(in clam));
            Assert.AreEqual(LiftLine.For(clam), book.RegisterLine, "shut or open, the line is kept for the next page paint");
            book.Open();
            var cod = Cod();
            book.OnCatchLanded(new CatchLanded(in cod));
            Assert.AreEqual(LiftLine.For(cod), book.RegisterLine);
        }

        [Test]
        public void The_weight_readout_sets_at_once_when_shut_and_counts_up_when_open()
        {
            var book = StandABook();
            book.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 1.23f));
            Assert.AreEqual(12, book.WeightTenths, "shut: nothing to animate, the tenths land at once (1.23 kg -> 12)");

            book.Open();
            book.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 1.23f));
            Assert.AreEqual(0, book.WeightTenths, "open: the count-up starts from zero");
            book.TickMoments(0.3f);
            Assert.Greater(book.WeightTenths, 0);
            Assert.Less(book.WeightTenths, 12, "halfway: not there yet — the pop must land on a new number");
            book.TickMoments(0.35f);
            Assert.AreEqual(12, book.WeightTenths, "landed at WeightCountUpSeconds");
        }

        [Test]
        public void The_weight_readout_ignores_a_cast_or_a_sale()
        {
            var book = StandABook();
            book.Open();
            book.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 0.5f));
            book.TickMoments(1f);
            Assert.AreEqual(5, book.WeightTenths);
            book.OnMoment(new JuiceMomentCue(JuiceMoment.CastEntry, Vector2.zero, 0f));
            book.OnMoment(new JuiceMomentCue(JuiceMoment.Sale, Vector2.zero, 48f));
            Assert.AreEqual(5, book.WeightTenths, "only a landing or a dig strike carries a weight");
        }

        [Test]
        public void A_sale_flies_the_coins_then_climbs_the_purse_to_the_new_balance()
        {
            var book = StandABook();
            book.Open();
            book.OnMoneyChanged(new MoneyChanged(148, 48));   // the wallet publishes BEFORE CatchSold (SellService)
            book.OnCatchSold(new CatchSold(48, 3));

            Assert.IsTrue(book.SaleAnimating);
            Assert.AreEqual(100, book.DisplayedMoney, "the purse shows the OLD balance until the first coin lands");

            book.TickMoments(0.5f);   // > CoinFlySeconds: the first coin has landed, the climb is armed
            Assert.GreaterOrEqual(book.DisplayedMoney, 100);
            Assert.Less(book.DisplayedMoney, 148, "the climb is under way, the target not yet shown");

            for (int i = 0; i < 6; i++) book.TickMoments(0.5f);   // 3.5 s total: coins, climb and pop all done
            Assert.AreEqual(148, book.DisplayedMoney, "the purse reads the wallet's balance");
            Assert.IsFalse(book.SaleAnimating, "and the sale is over");
        }

        [Test]
        public void A_sale_with_the_book_shut_or_the_charter_off_just_shows_the_balance()
        {
            var book = StandABook();
            book.OnMoneyChanged(new MoneyChanged(148, 48));
            book.OnCatchSold(new CatchSold(48, 3));
            Assert.IsFalse(book.SaleAnimating, "shut: nothing to fly");
            Assert.AreEqual(148, book.DisplayedMoney);

            var off = JuiceSettings.Default; off.MomentsEnabled = false;
            book.OverrideJuice(off);
            book.Open();
            book.OnMoneyChanged(new MoneyChanged(200, 52));
            book.OnCatchSold(new CatchSold(52, 4));
            Assert.IsFalse(book.SaleAnimating, "MomentsEnabled=false");
            Assert.AreEqual(200, book.DisplayedMoney);
        }

        // =============================================================================================
        // §4.5 THE EVENTS — every moment publishes the cue the audio slot names (source guards)
        // =============================================================================================

        [Test]
        public void Every_moment_publishes_its_cue_from_the_system_that_owns_it()
        {
            string fishing = Source("_Project/Code/Fishing/FishingController.cs");
            StringAssert.Contains("JuiceMoment.Landing", fishing, "the landing frame is the fishing controller's");
            StringAssert.Contains("JuiceMoment.CastEntry", fishing, "the cast entry is the fishing controller's");
            string dig = Source("_Project/Code/Fishing/ClamDig.cs");
            StringAssert.Contains("JuiceMoment.DigStrike", dig, "the dig strike is the dig's");
            string director = Source("_Project/Code/Audio/AudioDirector.cs");
            StringAssert.Contains("Subscribe<JuiceMomentCue>", director, "the audio director hears every cue");
            string manifest = Source("_Project/Audio/AUDIO-MANIFEST.md");
            foreach (string slot in new[] { "_landingHit", "_saleChime", "_digStrike", "_castEntry" })
                StringAssert.Contains(slot, manifest, "the manifest names the slot " + slot);
        }

        [Test]
        public void Every_feel_timer_runs_on_UNSCALED_time_so_the_hit_stop_cannot_freeze_its_own_feedback()
        {
            foreach (string file in new[]
            {
                "_Project/Code/App/HitStopHost.cs",
                "_Project/Code/Art/MomentBurstEmitter.cs",
                "_Project/Code/Player/LiftFeedback.cs",
                "_Project/Code/Player/PlayerHaulAnimator.cs",
                "_Project/Code/World/NotebookPresenter.cs",
            })
            {
                string src = Source(file);
                StringAssert.Contains("unscaledDeltaTime", src, file + " must tick on Time.unscaledDeltaTime");
            }
        }

        [Test]
        public void The_charters_defaults_are_the_numbers_it_wrote()
        {
            var j = JuiceSettings.Default;
            Assert.IsTrue(j.MomentsEnabled);
            Assert.GreaterOrEqual(j.LandingHitStopSeconds, 0.06f, "60–120 ms (charter §4.1)");
            Assert.LessOrEqual(j.LandingHitStopSeconds, 0.12f);
            Assert.Greater(j.LandingHitStopScale, 0f, "a stop, not a freeze — the world clock still ticks");
            Assert.Less(j.LandingHitStopScale, 0.2f);
            Assert.Greater(j.CoinFlyCount, 0);
            Assert.LessOrEqual(j.CoinFlyCount, 16, "capped (rule 7)");
            Assert.Greater(j.MomentTickHz, 0f);
        }
    }
}
