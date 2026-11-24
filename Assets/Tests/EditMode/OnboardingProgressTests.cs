using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // LicenseDef / GearOffer / ShipwrightOffer — the shipped Defs the ids are held to
using HiddenHarbours.UI;        // TidePanelInput — the binding the dig line names
using HiddenHarbours.World;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The quest note tells the truth about the next step, from state.</b>
    ///
    /// <para>Two faults, both in what the note decided from. The director kept its place in four
    /// private booleans set by Core signals and never saved, so after Continue the note fell back to an
    /// early beat of a game the player had already played past. And the licence boolean was set by ANY
    /// licence purchase, so the clam licence Aunt Ginny fronts counted as the cod licence and the note
    /// skipped the step that actually lets her fish.</para>
    ///
    /// <para>Every test here builds a director over a SAVE and asks it which beat is next — no signal
    /// replayed, no session to remember — which is exactly the position a Continue is in. The identity
    /// checks are by Def id against the shipped catalog assets.</para>
    /// </summary>
    public class OnboardingProgressTests
    {
        /// <summary>A save service over one blob, keeping flags IN the blob's own flag list the way the
        /// real service does — so a flag written through it survives the blob's round trip to disk.</summary>
        private sealed class BlobSave : ISaveService
        {
            public BlobSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int FlagWrites { get; private set; }

            public bool GetFlag(string key)
            {
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                if (string.IsNullOrEmpty(key)) return;
                FlagWrites++;
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }
        }

        private const string ClamLicenceId = "license.clam";

        private sealed class FactHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 10;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FactLicences : ILicenseService
        {
            private readonly HashSet<string> _ids = new();
            public bool IsLicensed(string id) => _ids.Contains(id);
            public void Grant(string id) => _ids.Add(id);
            public int Count => _ids.Count;
        }

        private sealed class FactInstruments : IHelmInstruments
        {
            public FactInstruments(string id) { HullId = id; }
            public string HullId { get; }
            public HelmFit Fit => HelmFit.None;
            public bool TryReadDepth(out float depth) { depth = 0f; return false; }
            public bool TryReadPosition(out Vector2 position) { position = default; return false; }
            public SounderPrefs SounderPrefs => SounderPrefs.FromDefaults(DepthSounderSettings.Default);
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        private static object StandOn(string hullId)
        {
            var hull = new object();
            GameServices.Helm.Register(hull, null, new FactInstruments(hullId));
            GameServices.Helm.SetPlayersBoat(hullId == "" ? null : hull);
            return hull;
        }

        private static CatchItem CodCatch() => new CatchItem("fish.atlantic_cod", "Cod",
            FishCategory.InshoreGroundfish, 2f, 10, 0f);

        private static IslandWeekRulesDef IslandRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<IslandWeekRulesDef>(
                "Assets/_Project/Data/Resources/IslandWeek.asset");
            Assert.NotNull(rules);
            Assert.IsNull(rules.ValidationError());
            return rules;
        }

        [Test]
        public void IslandWeek_SharedDataNamesTheShippedDoryLicenceAndFiveTaskKeys()
        {
            var rules = IslandRules();
            Assert.AreEqual("island_week.st_peters", rules.Id);
            Assert.AreEqual(OnboardingProgress.DoryBoatId, rules.DoryId);
            Assert.AreEqual(OnboardingProgress.CodLicenceId, rules.LicenceId);
            Assert.AreEqual(7, rules.EarliestDay);
            CollectionAssert.AreEqual(new[] { "tasks.st_peters.mend_dory", "tasks.st_peters.cod_licence",
                "tasks.st_peters.fish_from_dory", "tasks.st_peters.sell_shellfish", "tasks.st_peters.first_week" },
                new IslandWeekProgress(rules).ReadSaved(Game(), 1000d).MissingTextKeys);
            Assert.AreEqual("st_peters.fact.fish_from_dory", IslandWeekFactRecorder.FishFromDoryKey);
            Assert.AreEqual("st_peters.fact.shellfish_sold", IslandWeekFactRecorder.ShellfishSoldKey);
            CollectionAssert.Contains(rules.FinfishCategories, FishCategory.InshoreGroundfish);
            CollectionAssert.DoesNotContain(rules.FinfishCategories, FishCategory.Shellfish);
        }

        [Test]
        public void IslandWeek_EveryCombinationReportsExactlyItsFactsAndMissingKeys([Range(0, 31)] int mask)
        {
            var rules = IslandRules();
            var save = Game();
            if ((mask & 1) != 0) save.RepairedBoats.Add(rules.DoryId);
            if ((mask & 2) != 0) save.OwnedLicenses.Add(rules.LicenceId);
            if ((mask & 4) != 0) save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, true));
            if ((mask & 8) != 0) save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, true));
            save.GameTimeSeconds = (mask & 16) != 0 ? 6000d : 5000d;
            string before = SaveSerialization.ToJson(save);

            IIslandWeekProgress progress = new IslandWeekProgress(rules);
            IslandWeekSnapshot reading = progress.ReadSaved(save, 1000d);
            Assert.AreEqual((IslandWeekFact)mask, reading.Held);
            Assert.AreEqual(mask == 31, reading.AllHold);
            string[] keys = { rules.DoryMendedTextKey, rules.CodLicenceTextKey, rules.FishFromDoryTextKey,
                              rules.ShellfishSoldTextKey, rules.DayReachedTextKey };
            var expectedMissing = new List<string>();
            for (int i = 0; i < keys.Length; i++)
            {
                bool expected = (mask & (1 << i)) != 0;
                Assert.AreEqual(expected, reading.Holds((IslandWeekFact)(1 << i)));
                if (!expected) expectedMissing.Add(keys[i]);
            }
            CollectionAssert.AreEqual(expectedMissing, reading.MissingTextKeys);
            Assert.AreEqual(before, SaveSerialization.ToJson(save), "evaluation writes nothing");

            var reload = SaveSerialization.FromJson(before);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var again = progress.ReadSaved(reload, 1000d);
                Assert.AreEqual(reading.Held, again.Held);
                CollectionAssert.AreEqual(reading.MissingTextKeys, again.MissingTextKeys);
            }
            Assert.AreEqual(before, SaveSerialization.ToJson(save));
        }

        [TestCase(5000d, false)]
        [TestCase(5999.999d, false)]
        [TestCase(6000d, true)]
        [TestCase(7000d, true)]
        [TestCase(112000d, true)]
        public void IslandWeek_DaySevenUsesAbsoluteGameTime_NotSeasonOrSavedDayCache(double seconds, bool reached)
        {
            var save = Game();
            save.GameTimeSeconds = seconds;
            save.DayIndex = 999;
            var progress = new IslandWeekProgress(IslandRules());
            Assert.AreEqual(reached, progress.ReadSaved(save, 1000d).Holds(IslandWeekFact.DayReached));
            Assert.IsFalse(progress.Read(save, 0d, 1000d).Holds(IslandWeekFact.DayReached),
                "a supplied live clock is explicit, never replaced by the save's older clock");
        }

        [Test]
        public void IslandWeek_UnrelatedOldOpeningEvidence_DoesNotInventTheNewFacts()
        {
            var save = Game(OnboardingFlags.OnboardedKey, OnboardingFlags.LandedCatchKey,
                OnboardingFlags.BoughtDoryKey, "st_peters.weather_turned");
            save.OwnedBoats.Add(OnboardingProgress.DoryBoatId);
            save.ActiveHullId = OnboardingProgress.DoryBoatId;
            save.OwnedGear.Add(OnboardingProgress.RodGearId);
            save.OwnedLicenses.Add(ClamLicenceId);
            save.RepairedBoats.Add("boat.punt");
            save.Money = 999;
            save.BucketCatch.Add(new HeldCatchDto { SpeciesId = "fish.soft_shell_clam", Quantity = 1 });
            save.CaughtSpecies.Add(new CaughtSpeciesDto { SpeciesId = "fish.atlantic_cod", Count = 1, RecordWeightKg = 2f });
            save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, false));
            save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, false));
            var progress = new IslandWeekProgress(IslandRules());
            Assert.AreEqual(IslandWeekFact.None, progress.ReadSaved(save, 1000d).Held);
            Assert.AreEqual(IslandWeekFact.None, progress.ReadSaved(null, 1000d).Held);
            save.RepairedBoats = null; save.OwnedLicenses = null; save.OnboardingFlags = null;
            Assert.AreEqual(IslandWeekFact.None, progress.ReadSaved(save, 1000d).Held);
        }

        [Test]
        public void IslandWeek_TunablesAreReadFromData_AndAnExistingEvaluatorKeepsItsReadingRules()
        {
            var rules = Object.Instantiate(IslandRules());
            _spawned.Add(rules);
            rules.EarliestDay = 3;
            rules.LicenceId = "license.other";
            rules.DoryId = "boat.other";
            var save = Game();
            save.OwnedLicenses.Add("license.other");
            save.RepairedBoats.Add("boat.other");
            var progress = new IslandWeekProgress(rules);
            rules.EarliestDay = 20;
            rules.LicenceId = "license.changed";
            var snapshot = progress.Read(save, 2000d, 1000d);
            Assert.AreEqual(IslandWeekFact.DoryMended | IslandWeekFact.CodLicence | IslandWeekFact.DayReached, snapshot.Held);
            Assert.AreEqual(IslandWeekFact.DoryMended, new IslandWeekProgress(rules).Read(save, 2000d, 1000d).Held);
        }

        [TestCase(-1d, 1000d)]
        [TestCase(double.NaN, 1000d)]
        [TestCase(double.PositiveInfinity, 1000d)]
        [TestCase(6000d, 0d)]
        [TestCase(6000d, -1d)]
        [TestCase(6000d, double.NaN)]
        [TestCase(6000d, double.PositiveInfinity)]
        public void IslandWeek_InvalidClockCannotSatisfyTheDay(double time, double dayLength)
            => Assert.IsFalse(new IslandWeekProgress(IslandRules()).Read(Game(), time, dayLength)
                .Holds(IslandWeekFact.DayReached));

        [TestCase("day")]
        [TestCase("licence")]
        [TestCase("boat")]
        [TestCase("identity")]
        [TestCase("empty categories")]
        [TestCase("shellfish")]
        [TestCase("unknown category")]
        [TestCase("duplicate category")]
        [TestCase("missing key")]
        [TestCase("duplicate key")]
        public void IslandWeek_InvalidDataIsRejected(string broken)
        {
            var rules = Object.Instantiate(IslandRules());
            _spawned.Add(rules);
            switch (broken)
            {
                case "day": rules.EarliestDay = 0; break;
                case "licence": rules.LicenceId = ""; break;
                case "boat": rules.DoryId = ""; break;
                case "identity": rules.Id = ""; break;
                case "empty categories": rules.FinfishCategories = null; break;
                case "shellfish": rules.FinfishCategories = new[] { FishCategory.Shellfish }; break;
                case "unknown category": rules.FinfishCategories = new[] { (FishCategory)999 }; break;
                case "duplicate category": rules.FinfishCategories = new[] { FishCategory.Pelagic, FishCategory.Pelagic }; break;
                case "missing key": rules.CodLicenceTextKey = ""; break;
                case "duplicate key": rules.CodLicenceTextKey = rules.DoryMendedTextKey; break;
            }
            Assert.IsNotNull(rules.ValidationError());
            Assert.Throws<System.ArgumentException>(() => new IslandWeekProgress(rules));
            Assert.Throws<System.ArgumentException>(() => new IslandWeekFactRecorder(rules));
        }

        [TestCase("boat.dory", true, true)]
        [TestCase("boat.dory", false, true)]
        [TestCase("boat.punt", true, false)]
        [TestCase("boat.punt", false, false)]
        [TestCase("", true, false)]
        [TestCase("", false, false)]
        public void IslandWeek_CatchSignalsUseTheOccupiedHull_NotTheLastSavedHull(string hullId, bool inHand, bool counts)
        {
            var save = Game();
            save.ActiveHullId = "boat.dory";
            GameServices.Save = new BlobSave(save);
            StandOn(hullId);
            CatchRecords.Install();
            var catchItem = CodCatch();
            if (inHand) EventBus.Publish(new CatchLanded(catchItem));
            else EventBus.Publish(new FishCaught(catchItem));
            Assert.AreEqual(counts, GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            Assert.IsFalse(GameServices.Save.GetFlag("st_peters.weather_turned"));
        }

        [Test]
        public void IslandWeek_ShoreCatchStowedOnTheDoryDoesNotBecomeADoryCatch()
        {
            GameServices.Save = new BlobSave(Game());
            CatchRecords.Install();
            var cod = CodCatch();
            EventBus.Publish(new CatchLanded(cod));
            StandOn("boat.dory");
            EventBus.Publish(new CatchTakenFromHand(cod));
            EventBus.Publish(new FishCaught(cod));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            EventBus.Publish(new FishCaught(cod));
            Assert.IsTrue(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), "a separate fresh catch counts");
            Assert.AreEqual(2, CatchRecords.Current()[0].Count, "the existing handoff counted each catch once");
        }

        [Test]
        public void IslandWeek_ReleasedCatchAndShellfishCaughtAboardDoNotSupplyTheFishFact()
        {
            GameServices.Save = new BlobSave(Game());
            StandOn("boat.dory");
            CatchRecords.Install();
            EventBus.Publish(new CatchReleased(CodCatch()));
            EventBus.Publish(new FishCaught(new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 1f, 1, 0f)));
            EventBus.Publish(new FishCaught(default));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IslandWeek_TheActualLandingChokePointCountsOnlyTheLicensedKeptFish(bool licensed)
        {
            GameServices.Save = new BlobSave(Game());
            var licences = new FactLicences();
            if (licensed) licences.Grant("license.cod");
            GameServices.Licenses = licences;
            StandOn("boat.dory");
            CatchRecords.Install();
            var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/AtlanticCod.asset");
            var licence = AssetDatabase.LoadAssetAtPath<LicenseDef>(
                "Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset");
            Assert.NotNull(fish); Assert.NotNull(licence);
            var go = new GameObject("IslandLanding(test)");
            _spawned.Add(go);
            var controller = go.AddComponent<FishingController>();
            var hold = new FactHold();
            controller.Configure(hold, new[] { fish }, "region.st_peters", Gear.Handline, 1, new[] { licence });
            const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FishingController).GetField("_pendingFish", access).SetValue(controller, fish);
            typeof(FishingController).GetField("_pendingWeight", access).SetValue(controller, 2f);
            typeof(FishingController).GetMethod("OnLanded", access).Invoke(controller, null);
            Assert.AreEqual(licensed ? 1 : 0, hold.UsedUnits);
            Assert.AreEqual(licensed ? FishingPhase.Landed : FishingPhase.Snapped, controller.Phase);
            Assert.AreEqual(licensed, GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
        }

        [TestCase(1, 0, false)]
        [TestCase(1, 1, true)]
        [TestCase(3, 1, true)]
        [TestCase(0, 0, false)]
        [TestCase(1, -1, false)]
        [TestCase(1, 2, false)]
        public void IslandWeek_SaleRequiresAnActualShellfishUnit(int totalCount, int shellfishCount, bool counts)
        {
            GameServices.Save = new BlobSave(Game());
            CatchRecords.Install();
            EventBus.Publish(new CatchSold(1, totalCount, default, shellfishCount));
            Assert.AreEqual(counts, GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
        }

        [Test]
        public void IslandWeek_LegacySaleAndDumpCannotInventShellfish_AndUninstallStopsListening()
        {
            GameServices.Save = new BlobSave(Game());
            CatchRecords.Install();
            EventBus.Publish(new CatchSold(50, 3));
            EventBus.Publish(new CatchDumped(3));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
            CatchRecords.Uninstall();
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
        }

        [Test]
        public void IslandWeek_NewFactsPersistOnce_AndNeverLeakAcrossLoadedGames()
        {
            var save = Game("unrelated.flag");
            var service = new BlobSave(save);
            GameServices.Save = service;
            StandOn("boat.dory");
            CatchRecords.Install();
            CatchRecords.Install();
            EventBus.Publish(new FishCaught(CodCatch()));
            EventBus.Publish(new FishCaught(CodCatch()));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.AreEqual(3, save.OnboardingFlags.Count);
            Assert.AreEqual(2, service.FlagWrites, "each historical fact is persisted once");
            Assert.AreEqual(2, CatchRecords.Current()[0].Count, "installing twice does not duplicate handlers");
            var restored = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(SaveMigration.CurrentVersion, restored.SchemaVersion);
            Assert.AreEqual(IslandWeekFact.FishFromDory | IslandWeekFact.ShellfishSold,
                new IslandWeekProgress(IslandRules()).ReadSaved(restored, 1000d).Held);

            GameServices.Save = new BlobSave(Game());
            EventBus.Publish(new GameLoaded());
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.AreEqual(1, GameServices.Save.Current.OnboardingFlags.Count);
            Assert.AreEqual(3, save.OnboardingFlags.Count, "the previous save is untouched");
        }

        private readonly List<Object> _spawned = new();
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            CatchRecords.Uninstall();
            CatchRecords.Log.Clear();
            _dir = Path.Combine(Path.GetTempPath(), "hh_onboarding_progress_tests");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            CatchRecords.Uninstall();
            CatchRecords.Log.Clear();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private static SaveData Game(params string[] flags)
        {
            SaveData save = SaveMigration.NewGame();
            foreach (string key in flags) save.OnboardingFlags.Add(new SaveFlag(key, true));
            return save;
        }

        /// <summary>A director over this save, as a fresh session finds it: nothing heard on the bus.
        /// (An EditMode AddComponent runs neither Awake nor OnEnable; the step is askable regardless.)</summary>
        private OnboardingDirector DirectorOver(SaveData save)
        {
            GameServices.Save = save != null ? new BlobSave(save) : null;
            var go = new GameObject("Onboarding(test)");
            _spawned.Add(go);
            return go.AddComponent<OnboardingDirector>();
        }

        private SaveData RoundTrip(SaveData save, string name)
        {
            string path = Path.Combine(_dir, name + ".json");
            SaveStore.Write(save, path);
            SaveData reloaded = SaveStore.Read(path);
            Assert.NotNull(reloaded, "the save reads back");
            return reloaded;
        }

        /// <summary>Each beat's state, built from what a save HOLDS — in the order the opening walks.</summary>
        private static IEnumerable<(string name, SaveData save, OnboardingStep step)> EveryBeatFromState()
        {
            yield return ("fresh", Game(), OnboardingStep.TalkGinny);

            yield return ("met", Game(OnboardingFlags.MetGinnyKey), OnboardingStep.DigClams);

            SaveData clams = Game(OnboardingFlags.MetGinnyKey);
            clams.BucketCatch.Add(new HeldCatchDto { SpeciesId = "fish.soft_shell_clam", Quantity = 3 });
            yield return ("clams-in-the-pail", clams, OnboardingStep.BuyCodLicence);

            yield return ("clams-sold", Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey),
                          OnboardingStep.BuyCodLicence);

            SaveData licenced = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey);
            licenced.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            yield return ("cod-licence", licenced, OnboardingStep.BuyRod);

            SaveData rod = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey);
            rod.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            rod.OwnedGear.Add(OnboardingProgress.RodGearId);
            yield return ("rod", rod, OnboardingStep.BuyDory);

            SaveData bought = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey,
                                   OnboardingFlags.BoughtDoryKey);
            bought.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            bought.OwnedGear.Add(OnboardingProgress.RodGearId);
            yield return ("dory-bought", bought, OnboardingStep.RepairDory);

            SaveData repaired = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey,
                                     OnboardingFlags.BoughtDoryKey);
            repaired.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            repaired.OwnedGear.Add(OnboardingProgress.RodGearId);
            repaired.RepairedBoats.Add(OnboardingProgress.DoryBoatId);
            yield return ("dory-repaired", repaired, OnboardingStep.Done);
        }

        // ---- the two faults ------------------------------------------------------------------------

        [Test]
        public void TheRodAndAClamLicence_ReadAsTheCodLicenceStep()
        {
            SaveData save = Game(OnboardingFlags.MetGinnyKey);
            save.OwnedGear.Add(OnboardingProgress.RodGearId);
            save.OwnedLicenses.Add(ClamLicenceId);

            OnboardingDirector director = DirectorOver(save);

            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep,
                "a clam licence is not the cod licence — the note still asks for the cod licence");
            Assert.AreEqual(WorldStrings.OnboardBuyLicence, OnboardingProgress.LineFor(director.CurrentStep),
                "and the line on the note is the cod-licence line");
        }

        [Test]
        public void TheSameState_AfterASaveAndLoadRoundTrip_ReadsAsTheSameStep()
        {
            SaveData save = Game(OnboardingFlags.MetGinnyKey);
            save.OwnedGear.Add(OnboardingProgress.RodGearId);
            save.OwnedLicenses.Add(ClamLicenceId);
            Assert.AreEqual(OnboardingStep.BuyCodLicence, DirectorOver(save).CurrentStep, "before the save");

            SaveData reloaded = RoundTrip(save, "rod-and-clam-licence");

            // A NEW director: the session that made the save is gone, as it is after Continue.
            Assert.AreEqual(OnboardingStep.BuyCodLicence, DirectorOver(reloaded).CurrentStep,
                "after Continue the note is on the step the save describes");
        }

        [Test]
        public void EachStep_IsReachedFromStateAlone_AndSurvivesTheRoundTrip()
        {
            foreach ((string name, SaveData save, OnboardingStep step) in EveryBeatFromState())
            {
                Assert.AreEqual(step, DirectorOver(save).CurrentStep, name + ": from the state");
                Assert.AreEqual(step, DirectorOver(RoundTrip(save, name)).CurrentStep,
                    name + ": the same step after a save and a load");
            }
        }

        [Test]
        public void EveryStepButDone_HasALineOnTheNote()
        {
            foreach (OnboardingStep step in System.Enum.GetValues(typeof(OnboardingStep)))
            {
                string line = OnboardingProgress.LineFor(step);
                if (step == OnboardingStep.Done) Assert.IsNull(line, "done: the note comes down");
                else Assert.IsFalse(string.IsNullOrEmpty(line), step + " has a line");
            }
        }

        // ---- licence identity on the live signal -----------------------------------------------------

        [Test]
        public void AClamLicenceBoughtThisSession_DoesNotMoveTheNote_TheCodLicenceDoes()
        {
            // The save never gains the licence here (the fake's blob is not the licence service's), so
            // this is the session's own hearing of the signal — the path the fault lived on.
            OnboardingDirector director = DirectorOver(
                Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey));
            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep);

            director.OnLicence(new LicensePurchased(ClamLicenceId, 10));
            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep,
                "buying the CLAM licence leaves the note on the cod licence");

            director.OnLicence(new LicensePurchased(OnboardingProgress.CodLicenceId, 60));
            Assert.AreEqual(OnboardingStep.BuyRod, director.CurrentStep,
                "buying the COD licence moves it on");
        }

        // ---- the two beats that leave nothing to read -----------------------------------------------

        [Test]
        public void TheFirstCatch_AndTheDoryPurchase_AreWrittenDown_SoALoadStillKnows()
        {
            SaveData save = Game(OnboardingFlags.MetGinnyKey);
            save.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            save.OwnedGear.Add(OnboardingProgress.RodGearId);
            OnboardingDirector director = DirectorOver(save);

            director.OnCaught(default);
            director.OnBoatBought(new BoatPurchased(OnboardingProgress.DoryBoatId, 400));
            Assert.AreEqual(OnboardingStep.RepairDory, director.CurrentStep, "in the session that bought her");

            SaveData reloaded = RoundTrip(save, "bought-not-repaired");
            Assert.AreEqual(OnboardingStep.RepairDory, DirectorOver(reloaded).CurrentStep,
                "and after Continue: she is bought, the note asks for the repair, not for the purchase again");
        }

        [Test]
        public void ADoryInOwnedBoats_IsNotADoryBought()
        {
            // SaveService adds every hull the player takes the helm of to OwnedBoats — the free St Peters
            // dory among them. The list cannot say "bought", so the note must not read it as that.
            SaveData save = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey);
            save.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            save.OwnedGear.Add(OnboardingProgress.RodGearId);
            save.OwnedBoats.Add(OnboardingProgress.DoryBoatId);
            save.ActiveHullId = OnboardingProgress.DoryBoatId;

            Assert.AreEqual(OnboardingStep.BuyDory, DirectorOver(save).CurrentStep,
                "having rowed a dory is not having bought the damaged one");
        }

        [Test]
        public void LoadingAGame_ClearsWhatTheSessionHeard()
        {
            OnboardingDirector director = DirectorOver(
                Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey));
            director.OnLicence(new LicensePurchased(OnboardingProgress.CodLicenceId, 60));
            Assert.AreEqual(OnboardingStep.BuyRod, director.CurrentStep, "heard this session");

            director.OnGameLoaded(new GameLoaded());

            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep,
                "the loaded save holds no cod licence, so the note asks for it — whatever the last game did");
        }

        [Test]
        public void NoSaveService_ReadsAsAFreshGame()
        {
            Assert.AreEqual(OnboardingStep.TalkGinny, DirectorOver(null).CurrentStep);
        }

        // ---- the ids are the shipped Defs' ------------------------------------------------------------

        [Test]
        public void TheIdsTheNoteWaitsOn_AreTheShippedDefs()
        {
            const string catalog = "Assets/_Project/Data/Resources/Catalog/";

            var cod = AssetDatabase.LoadAssetAtPath<LicenseDef>(catalog + "Licenses/CodLicense.asset");
            var clam = AssetDatabase.LoadAssetAtPath<LicenseDef>(catalog + "Licenses/ClamLicense.asset");
            Assert.NotNull(cod, "the cod licence Def");
            Assert.NotNull(clam, "the clam licence Def");
            Assert.AreEqual(cod.Id, OnboardingProgress.CodLicenceId, "the note waits on the COD licence's own id");
            Assert.AreEqual(clam.Id, ClamLicenceId, "the fixture's clam licence is the shipped one");
            Assert.AreNotEqual(cod.Id, clam.Id, "two licences, two ids");

            var rod = AssetDatabase.LoadAssetAtPath<GearOffer>(catalog + "Gear/Rod.asset");
            Assert.NotNull(rod, "the rod offer");
            Assert.AreEqual(rod.Id, OnboardingProgress.RodGearId);

            var dory = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(catalog + "Shipwright/DamagedDoryOffer.asset");
            Assert.NotNull(dory, "the damaged dory offer");
            Assert.AreEqual(dory.BoatId, OnboardingProgress.DoryBoatId);
            Assert.IsTrue(dory.StartsDamaged, "bought damaged: buying and repairing are two steps");
        }

        // ---- the key the dig line names ---------------------------------------------------------------

        [Test]
        public void TheDigLine_NamesTheTideTablesShippedKey()
        {
            // World cannot see the UI module that owns the binding, so the line carries the letter and
            // this holds it to the default the component ships with (it installs itself by AddComponent,
            // so the code default IS the shipped binding).
            var go = new GameObject("[TidePanelInput(test)]");
            _spawned.Add(go);
            var input = go.AddComponent<TidePanelInput>();
            FieldInfo field = typeof(TidePanelInput).GetField(
                "_toggleKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "_toggleKey is TidePanelInput's serialized binding field");
            string key = ((Key)field.GetValue(input)).ToString();

            StringAssert.StartsWith(key + " shows the tide table.", WorldStrings.OnboardDigClams,
                "the note names the key that opens the tide table");
        }
    }
}
