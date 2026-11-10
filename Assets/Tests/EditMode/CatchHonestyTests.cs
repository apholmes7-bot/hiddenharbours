using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;   // ToolDef (the shovel in hand), BucketStrings (the pail's own sentence)

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// CATCH HONESTY: two places the catch loop was not straight with the player.
    ///
    /// <para><b>The clam flat.</b> A dug hole used to be remembered only by the hole itself, so it never
    /// came back in a sitting and the whole flat refilled on a reload. It is now spent for ONE TIDE: it
    /// stays dug until the flood has covered the flat again, and that survives a save and load. What is
    /// saved is when a hole was spent; which tide that is, is recomputed (rule 5).</para>
    ///
    /// <para><b>A full pail or hold.</b> The refusals were either silent or sent the player to a place
    /// with no working buyer. They are now said on the game's one feedback channel, before anything is
    /// caught and lost, and the pail sentence names a counter that buys.</para>
    ///
    /// <para>Driven without a scene: the real <see cref="TideModel"/> served through the Core
    /// environment contract, a flat terrain, a clock the test moves, and the real save serializer.</para>
    /// </summary>
    public class CatchHonestyTests
    {
        // ---- fakes --------------------------------------------------------------------------------------

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (_items.Count >= Capacity) return false;
                _items.Add(item);
                return true;
            }
            public void Clear() => _items.Clear();
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        /// <summary>The REAL tide, served through the Core contract: height and cycle both come from the
        /// pure <see cref="TideModel"/>, exactly as the shipped environment service answers.</summary>
        private sealed class TideEnv : IEnvironmentService
        {
            public GameConfig Config;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => TideModel.Height(totalSeconds, ActiveTideProfile, Config);
            public long TideCycleIndexAt(double totalSeconds)
                => TideModel.CycleIndex(totalSeconds, ActiveTideProfile, Config);
        }

        /// <summary>An environment that predates the tide-cycle read: it implements only what the
        /// interface required before, and so inherits the default.</summary>
        private sealed class OldEnv : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => -1f;
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

        // ---- the world under test -----------------------------------------------------------------------

        private const string ShovelGear = "gear.shovel";
        private const string HoleA = "fixture.clam_hole.3.00_4.00";

        private readonly List<Object> _spawned = new();
        private readonly List<string> _said = new();
        private int _caught;
        private void OnNotice(DevNotice e) => _said.Add(e.Text);
        private void OnCaught(FishCaught e) => _caught++;

        private GameConfig _config;
        private TideEnv _env;
        private StampClock _clock;
        private SaveData _save;
        private FishSpeciesDef _clam;

        /// <summary>One tide cycle, in game seconds (high water to high water).</summary>
        private double Period => _config.TidalPeriodHours * (_config.SecondsPerDay / 24.0);

        // The profile under test has no phase offset, so the carrier tops out a quarter of the way round
        // and bottoms out three quarters round. These are the model's own turning points, not tunables.
        private double HighWater(int n) => (0.25 + n) * Period;
        private double LowWater(int n) => (0.75 + n) * Period;

        [SetUp]
        public void SetUp()
        {
            _said.Clear();
            _caught = 0;
            EventBus.Clear<DevNotice>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<DevNotice>(OnNotice);
            EventBus.Subscribe<FishCaught>(OnCaught);
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            _env = new TideEnv { Config = _config };
            _clock = new StampClock { TotalSeconds = LowWater(0) };
            _clam = MakeClam();

            InstallWorld(NewSave());
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);   // digging needs the shovel in hand
            _said.Clear();                                    // whatever building the world said is not the subject
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Clear<DevNotice>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static SaveData NewSave()
        {
            SaveData save = SaveMigration.NewGame();
            save.OwnedGear.Add(ShovelGear);
            return save;
        }

        /// <summary>Publish the world the holes read: a flat at mean sea level (bared on the ebb, covered
        /// on the flood), the real tide, the test's clock, and the given save.</summary>
        private void InstallWorld(SaveData save)
        {
            _save = save;
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0f };
            GameServices.Environment = _env;
            GameServices.Clock = _clock;
            GameServices.Save = new FakeSaveService(save);
        }

        private FishSpeciesDef MakeClam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>A hole with an AUTHORED id, the way the region builder writes them. landInHand is
        /// false so the clam goes to the pail and the assertions can count it there (the fallback path
        /// that ships wherever no hands are published; see ClamDigTests' note).</summary>
        private ClamDig MakeHole(IHold pail, string id, Vector2 at = default)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = at;
            var d = go.AddComponent<ClamDig>();
            d.Configure(_clam, pail, go.transform, ShovelGear, seed: 7);
            d.ConfigureInteract(id, ToolDef.ShovelId, landInHand: false);
            return d;
        }

        /// <summary>Save, then load: the real serializer both ways, a new save service over the loaded
        /// blob. Returns the loaded blob. Everything the old world held in memory is the caller's to
        /// throw away, as a real reload throws it away.</summary>
        private SaveData SaveAndLoad()
        {
            string json = SaveSerialization.ToJson(_save);
            SaveData loaded = SaveSerialization.FromJson(json);
            Assert.IsNotNull(loaded, "the save round-trips through the real serializer");
            InstallWorld(loaded);
            return loaded;
        }

        // ================================================================================================
        // THE TIDE CYCLE: deterministic, recomputed, stepping exactly at high water
        // ================================================================================================

        [Test]
        public void TideCycle_StepsByOne_AtEachHighWater_AndNowhereElse()
        {
            TideProfile p = TideProfile.CoddleCove;
            for (int n = 0; n < 6; n++)
            {
                double hw = HighWater(n);
                Assert.AreEqual(n - 1, TideModel.CycleIndex(hw - 1.0, p, _config), $"just before high water {n}");
                Assert.AreEqual(n, TideModel.CycleIndex(hw + 1.0, p, _config), $"just after high water {n}");

                // Constant through the whole ebb and the whole flood that follow.
                for (int k = 1; k < 20; k++)
                    Assert.AreEqual(n, TideModel.CycleIndex(hw + Period * (k / 20.0), p, _config),
                                    $"cycle {n} is one cycle from high water to high water");

                // The step is AT the top of the tide the height model draws, not merely somewhere regular.
                float top = TideModel.Height(hw, p, _config);
                Assert.Greater(top, TideModel.Height(hw - Period / 8.0, p, _config), "high water is a maximum");
                Assert.Greater(top, TideModel.Height(hw + Period / 8.0, p, _config), "high water is a maximum");
            }
        }

        [Test]
        public void TideCycle_IsAPureFunctionOfTimeAndProfile()
        {
            TideProfile cove = TideProfile.CoddleCove;
            for (double t = -5000.0; t < 50000.0; t += 137.0)
                Assert.AreEqual(TideModel.CycleIndex(t, cove, _config), TideModel.CycleIndex(t, cove, _config));

            // A region whose tide runs early turns its cycle early by exactly its phase, like its height.
            TideProfile rips = TideProfile.FundyRips;
            double phaseSeconds = rips.PhaseHours * (_config.SecondsPerDay / 24.0);
            Assert.Greater(phaseSeconds, 0.0, "the comparison needs a profile with a real phase offset");
            for (int n = 0; n < 4; n++)
            {
                double hw = HighWater(n) - phaseSeconds;
                Assert.AreEqual(n - 1, TideModel.CycleIndex(hw - 1.0, rips, _config));
                Assert.AreEqual(n, TideModel.CycleIndex(hw + 1.0, rips, _config));
            }
        }

        [Test]
        public void TideCycle_WithNoUsableConfig_IsOneEndlessTide()
        {
            Assert.AreEqual(0L, TideModel.CycleIndex(12345.0, TideProfile.CoddleCove, null));

            var broken = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(broken);
            broken.TidalPeriodHours = 0f;
            Assert.AreEqual(0L, TideModel.CycleIndex(12345.0, TideProfile.CoddleCove, broken));
        }

        [Test]
        public void TheShippedEnvironmentService_AnswersWithTheTideModel()
        {
            var go = new GameObject("Environment");
            _spawned.Add(go);
            var service = go.AddComponent<EnvironmentService>();
            typeof(EnvironmentService)
                .GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(service, _config);

            // Through the INTERFACE: the default member must not shadow the real one.
            IEnvironmentService asCore = service;
            for (double t = 0.0; t < 6.0 * Period; t += Period / 7.0)
                Assert.AreEqual(TideModel.CycleIndex(t, service.ActiveTideProfile, _config),
                                asCore.TideCycleIndexAt(t), $"t = {t}");
            Assert.AreNotEqual(asCore.TideCycleIndexAt(LowWater(0)), asCore.TideCycleIndexAt(LowWater(1)),
                               "two low waters are two tides");
        }

        [Test]
        public void AnEnvironmentThatPredatesTheRead_IsOneEndlessTide_SoASpentHoleStaysSpent()
        {
            IEnvironmentService old = new OldEnv();
            Assert.AreEqual(0L, old.TideCycleIndexAt(0.0));
            Assert.AreEqual(0L, old.TideCycleIndexAt(1e9));

            GameServices.Environment = old;          // water at -1 m: the flat is always bared
            var pail = new FakeHold();
            ClamDig hole = MakeHole(pail, HoleA);
            Assert.IsTrue(hole.TryDig());
            _clock.TotalSeconds = LowWater(40);
            Assert.IsTrue(hole.Consumed, "with no tide to turn, a spent hole behaves as it always did");
        }

        // ================================================================================================
        // BREAK A: the clam flat
        // ================================================================================================

        [Test]
        public void ADugHole_IsStillDug_AfterSaveAndLoad_InTheSameTide()
        {
            var pail = new FakeHold();
            ClamDig before = MakeHole(pail, HoleA);
            Assert.IsTrue(before.TryDig(), "dug on the ebb");
            Assert.IsTrue(before.Consumed);

            // A little later on the same ebb: save, quit, load.
            _clock.TotalSeconds = LowWater(0) + Period / 8.0;
            SaveData loaded = SaveAndLoad();
            Assert.AreEqual(1, loaded.SpentClamHoles.Count, "the dug hole is in the save");
            Assert.AreEqual(HoleA, loaded.SpentClamHoles[0].HoleId);
            Assert.AreEqual(LowWater(0), loaded.SpentClamHoles[0].SpentAtGameSeconds, 1e-6,
                            "what is saved is WHEN it was dug");

            // The reload builds the scene's hole afresh: a new instance that remembers nothing.
            ClamDig after = MakeHole(pail, HoleA);
            Assert.IsFalse(after.Consumed, "a fresh instance knows nothing by itself (the old bug, in one line)");

            after.OnGameLoaded(default);
            Assert.IsTrue(after.Consumed, "the loaded game says this hole was dug on this tide");
            Assert.IsTrue(after.IsExposedNow(), "the flat is still bared, so only the spent state refuses");
            Assert.IsFalse(after.IsAvailable, "a spent hole is not offered to the interact verb");
            Assert.IsFalse(after.TryDig(), "and it does not yield a second clam");
            Assert.AreEqual(1, pail.UsedUnits, "one hole, one clam, across the reload");
            Assert.AreEqual(1, _caught);
        }

        [Test]
        public void ADugHole_IsBack_OnceTheFloodHasCoveredTheFlat()
        {
            var pail = new FakeHold();
            ClamDig hole = MakeHole(pail, HoleA);
            Assert.IsTrue(hole.TryDig());

            // Right up to the top of the flood it is still the tide the hole was dug on.
            _clock.TotalSeconds = HighWater(1) - 1.0;
            Assert.IsTrue(hole.Consumed, "still spent until high water");

            // The premise, checked rather than assumed: the water really did come over this ground.
            float ground = GameServices.TidalTerrain.ElevationAt(hole.SpotPos);
            Assert.Greater(GameServices.Environment.WaterLevelAt(HighWater(1)), ground,
                           "the flood covered the flat");

            _clock.TotalSeconds = HighWater(1) + 1.0;
            Assert.IsFalse(hole.Consumed, "the tide has turned: the hole is fresh");
            Assert.IsFalse(hole.IsExposedNow(), "but under water, so not diggable yet");
            Assert.IsFalse(hole.TryDig(), "you cannot dig a covered flat");

            _clock.TotalSeconds = LowWater(1);
            Assert.IsTrue(hole.IsExposedNow(), "the next ebb bares it again");
            Assert.IsTrue(hole.IsAvailable, "and it is a work-site again");
            Assert.IsTrue(hole.TryDig(), "a new tide, a new clam");
            Assert.AreEqual(2, pail.UsedUnits);
            Assert.IsTrue(hole.Consumed, "and spent again for this tide");
        }

        [Test]
        public void ADugHole_IsBack_AfterTheFlood_AcrossASaveAndLoadToo()
        {
            var pail = new FakeHold();
            Assert.IsTrue(MakeHole(pail, HoleA).TryDig());

            // Saved on the ebb, loaded on the NEXT ebb (the clock is restored before GameLoaded).
            SaveData loaded = SaveAndLoad();
            _clock.TotalSeconds = LowWater(1);

            ClamDig after = MakeHole(pail, HoleA);
            after.OnGameLoaded(default);
            Assert.AreEqual(1, loaded.SpentClamHoles.Count, "the old row is still in the file…");
            Assert.IsFalse(after.Consumed, "…and says nothing about this tide: the hole is fresh");
            Assert.IsTrue(after.TryDig());
            Assert.AreEqual(1, loaded.SpentClamHoles.Count, "one row per hole");
            Assert.AreEqual(LowWater(1), loaded.SpentClamHoles[0].SpentAtGameSeconds, 1e-6,
                            "the row now carries the new dig");
        }

        [Test]
        public void TheSameSeedAndTime_GiveTheSameFlat_SavedAndLoadedOrNot()
        {
            string straight = RunFlat(reloadMidway: false);
            string again = RunFlat(reloadMidway: false);
            string reloaded = RunFlat(reloadMidway: true);

            Assert.AreEqual(straight, again, "the flat is a function of what was dug and when, nothing hidden");
            Assert.AreEqual(straight, reloaded, "a flat that was saved and loaded reads exactly as one that was not");
            Assert.AreEqual("101010|101010|000000|010000|010000|000000", straight,
                            "spent on the tide it was dug or frightened on, fresh after each high water");
        }

        /// <summary>Six holes; dig two and frighten one on the first ebb, dig another on the second.
        /// Returns the flat's spent pattern at six moments. Optionally save and load partway through the
        /// first ebb, rebuilding every hole from nothing the way a reload does.</summary>
        private string RunFlat(bool reloadMidway)
        {
            GameServices.Reset();
            InstallWorld(NewSave());
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);
            var pail = new FakeHold();
            ClamDig[] flat = BuildFlat(pail);

            _clock.TotalSeconds = LowWater(0);
            Assert.IsTrue(flat[0].TryDig());
            Assert.IsTrue(flat[2].TryDig());
            _clock.TotalSeconds = LowWater(0) + 30.0;
            flat[4].MarkConsumed();                       // the skittish clam burrowed away

            if (reloadMidway)
            {
                SaveAndLoad();
                flat = BuildFlat(pail);
                foreach (ClamDig hole in flat) hole.OnGameLoaded(default);
            }

            var sb = new StringBuilder();
            Sample(flat, LowWater(0) + 60.0, sb).Append('|');
            Sample(flat, HighWater(1) - 1.0, sb).Append('|');
            Sample(flat, HighWater(1) + 1.0, sb).Append('|');

            _clock.TotalSeconds = LowWater(1);
            Assert.IsTrue(flat[1].TryDig());
            Sample(flat, LowWater(1) + 10.0, sb).Append('|');
            Sample(flat, HighWater(2) - 1.0, sb).Append('|');
            Sample(flat, HighWater(2) + 1.0, sb);
            return sb.ToString();
        }

        private ClamDig[] BuildFlat(IHold pail)
        {
            var flat = new ClamDig[6];
            for (int i = 0; i < flat.Length; i++)
                flat[i] = MakeHole(pail, $"fixture.clam_hole.{i}.00_0.00", new Vector2(i, 0f));
            return flat;
        }

        private StringBuilder Sample(ClamDig[] flat, double atSeconds, StringBuilder into)
        {
            _clock.TotalSeconds = atSeconds;
            foreach (ClamDig hole in flat) into.Append(hole.Consumed ? '1' : '0');
            return into;
        }

        [Test]
        public void AFrightenedClam_IsRecorded_LikeADugOne_AndOnlyOnce()
        {
            ClamDig hole = MakeHole(new FakeHold(), HoleA);
            hole.MarkConsumed();
            Assert.IsTrue(hole.Consumed);
            Assert.AreEqual(1, _save.SpentClamHoles.Count, "an escape is saved, or a reload would hand the clam back");

            _clock.TotalSeconds = LowWater(0) + 50.0;
            hole.MarkConsumed();                           // idempotent: the visual may call it again
            Assert.AreEqual(1, _save.SpentClamHoles.Count);
            Assert.AreEqual(LowWater(0), _save.SpentClamHoles[0].SpentAtGameSeconds, 1e-6,
                            "already spent on this tide: the first time stands");
            Assert.AreEqual(LowWater(0), hole.SpentAtSeconds, 1e-6);
        }

        [Test]
        public void AHoleWithNoAuthoredId_IsSpentInMemory_ButNeverWritten()
        {
            var pail = new FakeHold();
            ClamDig hole = MakeHole(pail, id: "");
            Assert.IsTrue(hole.TryDig());
            Assert.IsTrue(hole.Consumed, "still one clam a tide");
            Assert.AreEqual(0, _save.SpentClamHoles.Count,
                            "its fallback id does not survive a reload, so a row for it could never be read back");
        }

        [Test]
        public void TheLedger_KeepsOneRowAHole_AndOnlyTheRunningTide()
        {
            var save = new SaveData();
            ClamHoleLedger.MarkSpent(save, "a", LowWater(0), _env);
            ClamHoleLedger.MarkSpent(save, "b", LowWater(0) + 5.0, _env);
            ClamHoleLedger.MarkSpent(save, "a", LowWater(0) + 9.0, _env);
            Assert.AreEqual(2, save.SpentClamHoles.Count, "one row per hole");
            Assert.IsTrue(ClamHoleLedger.TryGetSpentAt(save, "a", out double a));
            Assert.AreEqual(LowWater(0) + 9.0, a, 1e-6);

            ClamHoleLedger.MarkSpent(save, "c", LowWater(1), _env);
            Assert.AreEqual(1, save.SpentClamHoles.Count, "last tide's rows say nothing and are dropped");
            Assert.IsFalse(ClamHoleLedger.TryGetSpentAt(save, "a", out _));
            Assert.IsTrue(ClamHoleLedger.TryGetSpentAt(save, "c", out _));

            // Tolerant of the absences a hand-edited or partial save can have.
            ClamHoleLedger.MarkSpent(null, "a", 0.0, _env);
            ClamHoleLedger.MarkSpent(save, "", 0.0, _env);
            Assert.IsFalse(ClamHoleLedger.TryGetSpentAt(null, "a", out _));
            save.SpentClamHoles = null;
            Assert.IsFalse(ClamHoleLedger.TryGetSpentAt(save, "a", out _));
            ClamHoleLedger.MarkSpent(save, "d", LowWater(1), null);
            Assert.AreEqual(1, save.SpentClamHoles.Count, "a missing list is made, not thrown on");
        }

        [Test]
        public void NothingAboutTheTide_IsSaved_OnlyWhichHoleAndWhen()
        {
            var names = new List<string>();
            foreach (FieldInfo f in typeof(SpentClamHoleDto).GetFields(BindingFlags.Public | BindingFlags.Instance))
                names.Add(f.Name);
            CollectionAssert.AreEquivalent(new[] { "HoleId", "SpentAtGameSeconds" }, names,
                "the row is a fact (which hole, when). The tide and its cycle are recomputed, never stored (rule 5)");
        }

        [Test]
        public void AnOldSave_WithoutTheSpentHoles_Loads_AndTheFlatIsFresh()
        {
            // A save exactly as the build before this one wrote it: every field but the new one.
            string current = SaveSerialization.ToJson(NewSave());
            string old = Regex.Replace(current, @",\s*""SpentClamHoles""\s*:\s*\[\s*\]", "");
            StringAssert.Contains("SpentClamHoles", current, "the current format writes the list");
            StringAssert.DoesNotContain("SpentClamHoles", old, "the fixture really is a save without the field");

            SaveData loaded = SaveSerialization.FromJson(old);
            Assert.IsNotNull(loaded, "an old save loads");
            Assert.AreEqual(SaveMigration.CurrentVersion, loaded.SchemaVersion, "no version was spent on this");
            Assert.IsNotNull(loaded.SpentClamHoles);
            Assert.AreEqual(0, loaded.SpentClamHoles.Count, "no hole had been dug as far as an old save knows");
            CollectionAssert.Contains(loaded.OwnedGear, ShovelGear, "and the rest of it came through");

            // Older still, and as bare as a save can be.
            SaveData bare = SaveSerialization.FromJson("{\"SchemaVersion\":16}");
            Assert.IsNotNull(bare);
            Assert.IsNotNull(bare.SpentClamHoles);
            Assert.AreEqual(0, bare.SpentClamHoles.Count);

            InstallWorld(loaded);
            var pail = new FakeHold();
            ClamDig hole = MakeHole(pail, HoleA);
            hole.OnGameLoaded(default);
            Assert.IsFalse(hole.Consumed);
            Assert.IsTrue(hole.TryDig(), "the flat an old save loads into digs as normal");
            Assert.AreEqual(1, loaded.SpentClamHoles.Count, "and from here on it is remembered");
        }

        [Test]
        public void TheHoleOnTheSand_ComesBack_WhenTheTideHasTurned()
        {
            var pail = new FakeHold();
            ClamDig hole = MakeHole(pail, HoleA);
            var visual = hole.gameObject.AddComponent<ClamHoleVisual>();
            var tex = new Texture2D(2, 2);
            _spawned.Add(tex);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
            _spawned.Add(sprite);
            visual.Configure(hole, sprite, null);

            visual.Tick(0.2f);
            Assert.IsTrue(visual.HoleRenderer.enabled, "drawn while bared");

            Assert.IsTrue(hole.TryDig());
            visual.Tick(0.2f);
            Assert.IsTrue(visual.IsHidden, "a dug hole is gone");
            Assert.IsFalse(visual.HoleRenderer.enabled);

            _clock.TotalSeconds = HighWater(1) - 1.0;
            visual.Tick(0.2f);
            Assert.IsTrue(visual.IsHidden, "and stays gone for the rest of that tide");

            _clock.TotalSeconds = HighWater(1) + 1.0;
            visual.Tick(0.2f);
            Assert.IsFalse(visual.IsHidden, "the tide turned: no longer a spent hole");
            Assert.IsFalse(visual.HoleRenderer.enabled, "but not drawn under water");

            _clock.TotalSeconds = LowWater(1);
            visual.Tick(0.2f);
            Assert.IsTrue(visual.HoleRenderer.enabled, "the next ebb shows the two holes again");
            Assert.IsFalse(visual.IsSinking);
        }

        // ================================================================================================
        // BREAK B: a full pail or hold says so, before the catch is lost
        // ================================================================================================

        [Test]
        public void AFullPail_RefusesTheDig_AndSaysSo_Once()
        {
            var pail = new FakeHold { Capacity = 1 };
            Assert.IsTrue(MakeHole(pail, "fixture.clam_hole.0.00_0.00").TryDig(), "the pail takes its one clam");
            _said.Clear();
            int caughtBefore = _caught;

            ClamDig next = MakeHole(pail, HoleA);
            Assert.IsFalse(next.TryDig(), "a full pail refuses the dig");

            Assert.AreEqual(1, _said.Count, "one press, one sentence");
            Assert.AreEqual(FishingStrings.PailFull, _said[0]);
            Assert.AreEqual(1, pail.UsedUnits, "nothing was dug");
            Assert.AreEqual(caughtBefore, _caught, "no catch was raised");
            Assert.IsFalse(next.Consumed, "refused BEFORE the clam was lifted: the hole still has it");
            Assert.IsFalse(ClamHoleLedger.TryGetSpentAt(_save, HoleA, out _), "and nothing was written");
        }

        [Test]
        public void ThePailSentence_NamesABuyerThatBuys()
        {
            StringAssert.DoesNotContain("Nine Mile Creek", FishingStrings.PailFull,
                "nothing at Nine Mile Creek buys a pail today");
            Assert.AreEqual(FishingStrings.PailFull, BucketStrings.Full,
                "the dig and the pail say one sentence (two modules that cannot see each other)");

            // The buyer the sentence names, pinned to the data that makes it one: the storekeeper's own
            // conversation carries a sell row at her counter. If this fails the buyer has moved, and the
            // sentence must move with it.
            StringAssert.Contains("Marguerite", FishingStrings.PailFull);
            StringAssert.Contains("LeBlanc's", FishingStrings.PailFull);
            string dialogue = File.ReadAllText(
                Path.Combine(Application.dataPath, "_Project/Data/NPCs/Dialogue/MargueriteFirst.asset"));
            StringAssert.Contains("SellAtSellerId: seller.leblancs", dialogue,
                "Marguerite's conversation offers the sale at the LeBlanc's counter");
        }

        [Test]
        public void TheHoldSentences_NameNoBuyer()
        {
            foreach (string s in new[] { FishingStrings.HoldFull, FishingStrings.LandedNoRoom })
            {
                StringAssert.DoesNotContain("Nine Mile Creek", s);
                StringAssert.DoesNotContain("sell", s, "nothing buys a boat's hold yet, so nothing is promised");
                StringAssert.DoesNotContain("LeBlanc", s);
            }
        }

        private FishSpeciesDef MakeCod()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.cod"; f.DisplayName = "fish.cod"; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeFisher(IHold hold, int seed)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { MakeCod() }, "region.coddle_cove", Gear.Handline, seed);
            return c;
        }

        private int Said(string sentence)
        {
            int n = 0;
            foreach (string s in _said) if (s == sentence) n++;
            return n;
        }

        [Test]
        public void AFullHold_RefusesTheCast_AndSaysSo_OnceAPress()
        {
            // The controller under test needs none of the clam world; give it the bare services the
            // existing controller suite runs under.
            GameServices.Reset();
            var hold = new FakeHold { Capacity = 0 };   // already full
            FishingController c = MakeFisher(hold, seed: 1);
            _said.Clear();

            c.Tick(0.05f, true, new Vector2(0f, -1f), true);          // the press
            Assert.AreEqual(FishingPhase.Idle, c.Phase, "a full hold never starts a cast");
            Assert.AreEqual(1, _said.Count, "the refusal is said, not only logged");
            Assert.AreEqual(FishingStrings.HoldFull, _said[0]);

            for (int i = 0; i < 20; i++) c.Tick(0.05f, true, new Vector2(0f, -1f), true);   // still held
            Assert.AreEqual(1, _said.Count, "holding the button is one press: one sentence, not one a frame");

            c.Tick(0.05f, false);                                      // release
            FlickGestures.CastLine(c);                                 // a whole new gesture
            Assert.AreEqual(FishingPhase.Idle, c.Phase);
            Assert.AreEqual(2, Said(FishingStrings.HoldFull), "a second press earns the sentence again");
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(0, _caught, "nothing was hooked, so nothing was lost");
        }

        [Test]
        public void AHoldWithRoom_CastsInSilence()
        {
            GameServices.Reset();
            FishingController c = MakeFisher(new FakeHold(), seed: 1);
            FlickGestures.CastLine(c);
            Assert.AreNotEqual(FishingPhase.Idle, c.Phase, "the line is out");
            Assert.AreEqual(0, Said(FishingStrings.HoldFull), "the sentence belongs to the refusal only");
        }

        [Test]
        public void AFishLanded_IntoAHoldThatFilledMidFight_IsNotLostInSilence()
        {
            // The one loss the cast gate cannot prevent: there was room at the cast and none at the
            // landing. Same seed and pacing as FishingControllerTests.PacedFight_Lands_AddsExactlyOneItem.
            GameServices.Reset();
            var hold = new FakeHold();
            FishingController c = MakeFisher(hold, seed: 999);

            FlickGestures.CastLine(c);
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < 30f)
            {
                c.Tick(0.05f, false);
                t += 0.05f;
            }
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "a fish is on");

            hold.Capacity = 0;                                         // the hold fills while she fights it

            t = 0f;
            while (c.Phase == FishingPhase.Fighting && t < 120f)
            {
                c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f);
                t += 0.05f;
            }
            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the fight was won");
            Assert.AreEqual(0, hold.UsedUnits, "and there was nowhere to put her");
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(1, Said(FishingStrings.LandedNoRoom), "so the player is told, once");
        }
    }
}
