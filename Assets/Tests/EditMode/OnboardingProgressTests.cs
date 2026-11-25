using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
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

        private sealed class FactWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class FactHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public bool AcceptsCatch = true;
            public int CapacityUnits => 10;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (!AcceptsCatch) return false;
                _items.Add(item);
                return true;
            }
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
                new IslandWeekProgress(rules).Read(Game(), 0).MissingTextKeys);
            Assert.AreEqual("st_peters.fact.fish_from_dory", IslandWeekFactRecorder.FishFromDoryKey);
            Assert.AreEqual("st_peters.fact.shellfish_sold", IslandWeekFactRecorder.ShellfishSoldKey);
            CollectionAssert.Contains(rules.FinfishCategories, FishCategory.InshoreGroundfish);
            CollectionAssert.DoesNotContain(rules.FinfishCategories, FishCategory.Shellfish);
        }

        [Test]
        public void IslandWeek_EveryCombinationReportsExactlyItsFactsAndMissingKeys([NUnit.Framework.Range(0, 31)] int mask)
        {
            var rules = IslandRules();
            var save = Game();
            if ((mask & 1) != 0) save.RepairedBoats.Add(rules.DoryId);
            if ((mask & 2) != 0) save.OwnedLicenses.Add(rules.LicenceId);
            if ((mask & 4) != 0) save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, true));
            if ((mask & 8) != 0) save.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, true));
            int dayIndex = (mask & 16) != 0 ? 6 : 5;
            string before = SaveSerialization.ToJson(save);

            IIslandWeekProgress progress = new IslandWeekProgress(rules);
            IslandWeekSnapshot reading = progress.Read(save, dayIndex);
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
                var again = progress.Read(reload, dayIndex);
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
        public void IslandWeek_DaySevenUsesCoreClockDayIndex_NotSeasonOrSavedDayCache(double seconds, bool reached)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            config.SecondsPerDay = 1000f;
            var go = new GameObject("IslandClock(test)");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(clock, config);
            GameServices.Clock = clock;
            var save = Game();
            save.GameTimeSeconds = seconds;
            save.DayIndex = 999;
            clock.SeekTo(save.GameTimeSeconds);
            var progress = new IslandWeekProgress(IslandRules());
            Assert.AreEqual(reached, progress.Read(save, GameServices.Clock.DayIndex).Holds(IslandWeekFact.DayReached));
            clock.SeekTo(0d);
            Assert.IsFalse(progress.Read(save, GameServices.Clock.DayIndex).Holds(IslandWeekFact.DayReached),
                "the restored Core clock owns the day; neither saved seconds nor the cached day overrides it");
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
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save, 0).Held);
            Assert.AreEqual(IslandWeekFact.None, progress.Read(null, 0).Held);
            save.RepairedBoats = null; save.OwnedLicenses = null; save.OnboardingFlags = null;
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save, 0).Held);
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
            var snapshot = progress.Read(save, 2);
            Assert.AreEqual(IslandWeekFact.DoryMended | IslandWeekFact.CodLicence | IslandWeekFact.DayReached, snapshot.Held);
            Assert.AreEqual(IslandWeekFact.DoryMended, new IslandWeekProgress(rules).Read(save, 2).Held);
        }

        [TestCase(int.MinValue, false)]
        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(5, false)]
        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(int.MaxValue, true)]
        public void IslandWeek_DayIndexUsesTheExactZeroBasedBoundary(int dayIndex, bool reached)
            => Assert.AreEqual(reached, new IslandWeekProgress(IslandRules()).Read(Game(), dayIndex)
                .Holds(IslandWeekFact.DayReached));

        [Test]
        public void IslandWeek_FirstDayRequirementStillRejectsAnInvalidDayIndex()
        {
            var rules = Object.Instantiate(IslandRules());
            _spawned.Add(rules);
            rules.EarliestDay = 1;
            var progress = new IslandWeekProgress(rules);
            Assert.IsFalse(progress.Read(Game(), -1).Holds(IslandWeekFact.DayReached));
            Assert.IsTrue(progress.Read(Game(), 0).Holds(IslandWeekFact.DayReached));
        }

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
        public void IslandWeek_LandingProvenanceNamesTheHull_DeliveryAloneCannotEarnTheFact(string hullId, bool inHand, bool counts)
        {
            var save = Game();
            save.ActiveHullId = "boat.dory";
            GameServices.Save = new BlobSave(save);
            StandOn(hullId);
            CatchRecords.Install();
            var catchItem = CodCatch();
            if (inHand) EventBus.Publish(new CatchLanded(catchItem));
            else EventBus.Publish(new FishCaught(catchItem));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey),
                "delivery signals alone carry no fresh landing provenance");
            EventBus.Publish(new FreshCatchKept(catchItem, hullId, true));
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
            EventBus.Publish(new FreshCatchKept(cod, "", false));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            StandOn("boat.dory");
            EventBus.Publish(new CatchTakenFromHand(cod));
            EventBus.Publish(new FishCaught(cod));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            EventBus.Publish(new FishCaught(cod));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey),
                "even a counted hold entry cannot prove where a catch was landed");
            EventBus.Publish(new FreshCatchKept(cod, "boat.dory", true));
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
            EventBus.Publish(new FreshCatchKept(new CatchItem("fish.soft_shell_clam", "Clam",
                FishCategory.Shellfish, 1f, 1, 0f), "boat.dory", true));
            EventBus.Publish(new FreshCatchKept(default, "boat.dory", true));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
        }

        [Test, Combinatorial]
        public void IslandWeek_TheActualLandingChokePointCountsOnlyTheLicensedKeptFish(
            [Values(false, true)] bool licensed, [Values(false, true)] bool inHand,
            [Values("shore", "pier", "boat.punt", "boat.dory")] string origin,
            [Values(false, true)] bool repaired)
        {
            GameServices.Save = new BlobSave(Game());
            string hullId = origin.StartsWith("boat.", System.StringComparison.Ordinal) ? origin : "";
            GameServices.Save.Current.ActiveHullId = "boat.dory";
            if (repaired) GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            var licences = new FactLicences();
            if (licensed) licences.Grant("license.cod");
            GameServices.Licenses = licences;
            StandOn(hullId);
            CatchRecords.Install();
            var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/AtlanticCod.asset");
            var licence = AssetDatabase.LoadAssetAtPath<LicenseDef>(
                "Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset");
            Assert.NotNull(fish); Assert.NotNull(licence);
            var go = new GameObject("IslandLanding(test)");
            _spawned.Add(go);
            var controller = go.AddComponent<FishingController>();
            var hands = go.AddComponent<CarryHands>();
            GameServices.CatchHands = hands;
            controller.OnControlModeChanged(new ControlModeChanged(inHand ? ControlMode.OnFoot : ControlMode.Aboard));
            var hold = new FactHold();
            controller.Configure(hold, new[] { fish }, "region.st_peters", Gear.Handline, 1, new[] { licence });
            const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FishingController).GetField("_pendingFish", access).SetValue(controller, fish);
            typeof(FishingController).GetField("_pendingWeight", access).SetValue(controller, 2f);
            int landed = 0, caught = 0, unlicensed = 0, documentedReleases = 0, fresh = 0;
            void OnFresh(FreshCatchKept e)
            {
                fresh++;
                Assert.AreEqual(fish.Id, e.Item.SpeciesId);
                Assert.AreEqual(hullId, e.LandingHullId);
                Assert.AreEqual(repaired && hullId == "boat.dory", e.HullWasRepaired);
            }
            void OnHand(CatchLanded _) => landed++;
            void OnHold(FishCaught _) => caught++;
            void OnRelease(UnlicensedCatchReleased e)
            {
                unlicensed++;
                Assert.AreEqual(fish.Id, e.Item.SpeciesId);
                Assert.AreEqual(licence.Id, e.RequiredLicenceId);
            }
            void OnDocumented(CatchReleased _) => documentedReleases++;
            EventBus.Subscribe<CatchLanded>(OnHand);
            EventBus.Subscribe<FishCaught>(OnHold);
            EventBus.Subscribe<UnlicensedCatchReleased>(OnRelease);
            EventBus.Subscribe<CatchReleased>(OnDocumented);
            EventBus.Subscribe<FreshCatchKept>(OnFresh);
            try
            {
                typeof(FishingController).GetMethod("OnLanded", access).Invoke(controller, null);
                Assert.AreEqual(licensed && !inHand ? 1 : 0, hold.UsedUnits);
                Assert.AreEqual(licensed && inHand, hands.TryPeekHand(out _));
                Assert.AreEqual(licensed && inHand ? 1 : 0, landed);
                Assert.AreEqual(licensed && !inHand ? 1 : 0, caught);
                Assert.AreEqual(licensed ? 0 : 1, unlicensed);
                Assert.AreEqual(0, documentedReleases);
                Assert.AreEqual(licensed ? 1 : 0, fresh);
                Assert.AreEqual(FishingPhase.Landed, controller.Phase, "a won fight lands even when the fish is let go");
                Assert.AreEqual(licensed ? 1 : 0, CatchRecords.Current().Count);
                Assert.AreEqual(licensed && repaired && hullId == "boat.dory",
                    GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
                Assert.IsFalse(GameServices.Save.GetFlag("st_peters.weather_turned"));
            }
            finally
            {
                EventBus.Unsubscribe<CatchLanded>(OnHand);
                EventBus.Unsubscribe<FishCaught>(OnHold);
                EventBus.Unsubscribe<UnlicensedCatchReleased>(OnRelease);
                EventBus.Unsubscribe<CatchReleased>(OnDocumented);
                EventBus.Unsubscribe<FreshCatchKept>(OnFresh);
            }
        }

        private FishingController LicensedRodLanding(bool inHand, FactHold hold, out CarryHands hands)
        {
            var licences = new FactLicences();
            licences.Grant("license.cod");
            GameServices.Licenses = licences;
            var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/AtlanticCod.asset");
            var licence = AssetDatabase.LoadAssetAtPath<LicenseDef>(
                "Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset");
            Assert.NotNull(fish); Assert.NotNull(licence);
            var go = new GameObject("LandingOrigin(test)");
            _spawned.Add(go);
            var controller = go.AddComponent<FishingController>();
            hands = go.AddComponent<CarryHands>();
            GameServices.CatchHands = hands;
            controller.OnControlModeChanged(new ControlModeChanged(inHand ? ControlMode.OnFoot : ControlMode.Aboard));
            controller.Configure(hold, new[] { fish }, "region.st_peters", Gear.Handline, 1, new[] { licence });
            return controller;
        }

        private static void LandCod(FishingController controller, float weight = 2f, FishSpeciesDef fish = null)
        {
            const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
            fish ??= AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/AtlanticCod.asset");
            typeof(FishingController).GetField("_pendingFish", access).SetValue(controller, fish);
            typeof(FishingController).GetField("_pendingWeight", access).SetValue(controller, weight);
            typeof(FishingController).GetMethod("OnLanded", access).Invoke(controller, null);
        }

        [TestCase("shore")]
        [TestCase("pier")]
        [TestCase("boat.punt")]
        [TestCase("boat.dory")]
        public void IslandWeek_ForgottenCatchPlacedLiftedAndStowedCannotAcquireDoryProvenance(string origin)
        {
            GameServices.Save = new BlobSave(Game());
            string hullId = origin.StartsWith("boat.", System.StringComparison.Ordinal) ? origin : "";
            // The dory case deliberately lands every old fish before she is mended.
            if (hullId != "boat.dory") GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            StandOn(hullId);
            CatchRecords.Install();
            var hold = new FactHold();
            var rod = LicensedRodLanding(true, hold, out CarryHands hands);
            ICarriable oldest = null;
            int fresh = 0;
            Listen<FreshCatchKept>(_ => fresh++);
            int landings = CatchRecordLog.MaxPendingHandCatches + 1;
            for (int i = 0; i < landings; i++)
            {
                LandCod(rod, 1f + i);
                Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), $"landing {i}");
                ICarriable placed = hands.Carried;
                Assert.NotNull(placed);
                _spawned.Add(placed.Transform.gameObject);
                if (i == 0) oldest = placed;
                Assert.AreEqual(CarryRefusal.None, hands.TryPlace(placed));
                Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), $"placing {i}");
            }
            Assert.AreEqual(landings, fresh);
            Assert.AreEqual(landings, CatchRecords.Current()[0].Count);
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(oldest));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), "lifting");
            if (hullId == "boat.dory") GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            StandOn("boat.dory");
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), "boarding after repair");
            Assert.IsTrue(hands.TryGiveCatchTo(hold));
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1f, hold.Items[0].WeightKg);
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey), "stowing the forgotten fish");
            Assert.AreEqual(landings, fresh, "a transfer is not a fresh landing");
            Assert.AreEqual(landings + 1, CatchRecords.Current()[0].Count,
                "the existing bounded notebook pin is preserved, including its recount of a forgotten fish");
            LandCod(rod);
            Assert.IsTrue(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            Assert.AreEqual(landings + 1, fresh);
            Assert.AreEqual(landings + 2, CatchRecords.Current()[0].Count);
            Assert.IsFalse(GameServices.Save.GetFlag("st_peters.weather_turned"));
        }

        [TestCase("", false, "boat.dory", true, false)]
        [TestCase("boat.dory", false, "boat.dory", true, false)]
        [TestCase("boat.dory", true, "", false, true)]
        public void IslandWeek_OriginAndRepairAreCapturedBeforeDeliveryCallbacks(
            string beforeHull, bool beforeRepair, string afterHull, bool afterRepair, bool counts)
        {
            GameServices.Save = new BlobSave(Game());
            if (beforeRepair) GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            StandOn(beforeHull);
            CatchRecords.Install();
            var rod = LicensedRodLanding(true, new FactHold(), out _);
            Listen<CatchLanded>(_ =>
            {
                StandOn(afterHull);
                GameServices.Save.Current.RepairedBoats.Clear();
                if (afterRepair) GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            });
            int fresh = 0;
            Listen<FreshCatchKept>(e =>
            {
                fresh++;
                Assert.AreEqual(beforeHull, e.LandingHullId);
                Assert.AreEqual(beforeRepair, e.HullWasRepaired);
            });
            LandCod(rod);
            Assert.AreEqual(1, fresh);
            Assert.AreEqual(counts, GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IslandWeek_ProtectedOrUndeliverableLandingPublishesNoKeptProvenance(bool protectedFish)
        {
            GameServices.Save = new BlobSave(Game());
            GameServices.Save.Current.RepairedBoats.Add("boat.dory");
            StandOn("boat.dory");
            CatchRecords.Install();
            var hold = new FactHold { AcceptsCatch = protectedFish };
            var rod = LicensedRodLanding(false, hold, out CarryHands hands);
            var fish = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(
                "Assets/_Project/Data/Fish/AtlanticCod.asset"));
            _spawned.Add(fish);
            fish.Protected = protectedFish;
            int fresh = 0, released = 0, caught = 0;
            Listen<FreshCatchKept>(_ => fresh++);
            Listen<CatchReleased>(_ => released++);
            Listen<FishCaught>(_ => caught++);
            LandCod(rod, fish: fish);
            Assert.AreEqual(0, fresh);
            Assert.AreEqual(protectedFish ? 1 : 0, released);
            Assert.AreEqual(0, caught);
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.IsFalse(hands.TryPeekHand(out _));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
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

        [TestCase(false)]
        [TestCase(true)]
        public void IslandWeek_TheIslandCounterRecordsOnlyTheShellfishItActuallyBuys(bool shellfish)
        {
            var save = new BlobSave(Game());
            GameServices.Save = save;
            CatchRecords.Install();
            var pail = new FactHold();
            var wallet = new FactWallet();
            var go = new GameObject("IslandCounter(test)");
            _spawned.Add(go);
            var buyer = go.AddComponent<FishBuyer>();
            var counter = go.AddComponent<WharfSellPoint>();
            const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WharfSellPoint).GetField("_buyer", access).SetValue(counter, buyer);
            typeof(WharfSellPoint).GetField("_hold", access).SetValue(counter, pail);
            typeof(WharfSellPoint).GetField("_wallet", access).SetValue(counter, wallet);
            Assert.IsFalse(counter.SellsWhatIsCarried, "the island counter still uses its wired pail provider");
            Assert.AreEqual(0, counter.SellOverTheCounter());
            Assert.AreEqual(0, save.FlagWrites);

            var item = shellfish
                ? new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 1f, 10, 0f)
                : CodCatch();
            Assert.IsTrue(pail.TryAdd(item));
            var sales = new List<CatchSold>();
            EventBus.Subscribe<CatchSold>(sales.Add);
            try
            {
                int paid = counter.SellOverTheCounter();
                Assert.AreEqual(10, paid);
                Assert.AreEqual(paid, wallet.Money);
                Assert.AreEqual(0, pail.UsedUnits);
                Assert.AreEqual(1, counter.LastUnitsSold);
                Assert.AreEqual(1, sales.Count);
                Assert.AreEqual(1, sales[0].Count);
                Assert.AreEqual(shellfish ? 1 : 0, sales[0].ShellfishCount);
                Assert.AreEqual(shellfish, save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
                Assert.AreEqual(shellfish ? 1 : 0, save.FlagWrites);
                Assert.AreEqual(shellfish ? IslandWeekFact.ShellfishSold : IslandWeekFact.None,
                    new IslandWeekProgress(IslandRules()).Read(save.Current, 0).Held);
                Assert.AreEqual(0, counter.SellOverTheCounter());
                Assert.AreEqual(0, counter.LastUnitsSold);
                Assert.AreEqual(1, sales.Count, "an emptied pail cannot sell a second time");
            }
            finally { EventBus.Unsubscribe<CatchSold>(sales.Add); }
        }

        [Test]
        public void IslandWeek_OnlyASuccessfulCodPurchaseThroughTheLicenceServiceSuppliesTheLicenceFact()
        {
            var save = new BlobSave(Game());
            GameServices.Save = save;
            var go = new GameObject("IslandLicence(test)");
            _spawned.Add(go);
            var service = go.AddComponent<LicenseService>();
            service.Register();
            var vendor = go.AddComponent<LicenseVendor>();
            const string catalog = "Assets/_Project/Data/Resources/Catalog/Licenses/";
            var cod = AssetDatabase.LoadAssetAtPath<LicenseDef>(catalog + "CodLicense.asset");
            var clam = AssetDatabase.LoadAssetAtPath<LicenseDef>(catalog + "ClamLicense.asset");
            Assert.NotNull(cod); Assert.NotNull(clam);
            var wallet = new FactWallet();
            var progress = new IslandWeekProgress(IslandRules());
            Assert.IsFalse(vendor.TryBuy(cod, wallet, service));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save.Current, 0).Held);
            wallet.Add(clam.Price + cod.Price);
            Assert.IsTrue(vendor.TryBuy(clam, wallet, service));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save.Current, 0).Held);
            Assert.IsTrue(vendor.TryBuy(cod, wallet, service));
            Assert.AreEqual(IslandWeekFact.CodLicence, progress.Read(save.Current, 0).Held);
            CollectionAssert.AreEquivalent(new[] { clam.Id, cod.Id }, save.Current.OwnedLicenses);
            Assert.AreEqual(0, wallet.Money);
            Assert.IsFalse(vendor.TryBuy(cod, wallet, service));
            Assert.AreEqual(2, save.Current.OwnedLicenses.Count);
            Assert.AreEqual(0, save.FlagWrites, "licences remain in their existing saved list");
            var restored = SaveSerialization.FromJson(SaveSerialization.ToJson(save.Current));
            Assert.AreEqual(IslandWeekFact.CodLicence, progress.Read(restored, 0).Held);
        }

        [Test]
        public void IslandWeek_MendingReadsTheRepairLedger_NotOwnershipOrARepairSignal()
        {
            var save = new BlobSave(Game());
            GameServices.Save = save;
            CatchRecords.Install();
            var rules = IslandRules();
            var progress = new IslandWeekProgress(rules);
            save.Current.OwnedBoats.Add(rules.DoryId);
            EventBus.Publish(new BoatRepaired(rules.DoryId, 0));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save.Current, 0).Held);
            Assert.IsTrue(RepairLedger.MarkRepaired(save.Current, "boat.punt"));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save.Current, 0).Held);
            Assert.IsTrue(RepairLedger.MarkRepaired(save.Current, rules.DoryId));
            Assert.IsFalse(RepairLedger.MarkRepaired(save.Current, rules.DoryId));
            Assert.AreEqual(IslandWeekFact.DoryMended, progress.Read(save.Current, 0).Held);
            Assert.AreEqual(2, save.Current.RepairedBoats.Count);
            Assert.AreEqual(0, save.FlagWrites, "mending remains in its existing saved list");
            var restored = SaveSerialization.FromJson(SaveSerialization.ToJson(save.Current));
            Assert.AreEqual(IslandWeekFact.DoryMended, progress.Read(restored, 0).Held);
        }

        [Test]
        public void IslandWeek_ShelfPurchasesOrdersAndBagsNeitherInventNorEraseHistoricalFacts()
        {
            var save = Game();
            SupplyLocker.Add(save, "supply.bread", 1);
            save.StorePurchases.Add(new StorePurchaseRowDto
            {
                ShopId = "shop.st_peters_general", ShelfLineId = "line.bread", Units = 1, Day = 0, Seconds = 1,
            });
            save.SpecialOrders.Add(new SpecialOrderDto
            {
                OrderId = "order.test.1", SellerId = "shop.st_peters_general", DeliverToId = "counter.test",
                Lines = { new SpecialOrderLineDto { ListingId = "specialorder.test", Count = 1, PaidPrice = 10 } },
                PaidPrice = 10, OrderedAtSeconds = 1, OrderedDay = 0, DueDay = 1,
            });
            save.NextSpecialOrderNumber = 2;
            save.ShoppingBags.Add(new ShoppingBagDto
            {
                BagDefId = "shoppingbag.paper", Held = true, Region = "region.st_peters",
                Items = { new BagItemDto { SupplyId = "supply.bread", Count = 1 } },
            });
            var progress = new IslandWeekProgress(IslandRules());
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save, 0).Held);
            GameServices.Save = new BlobSave(save);
            CatchRecords.Install();
            StandOn("boat.dory");
            EventBus.Publish(new FishCaught(CodCatch()));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save, 0).Held,
                "a hold entry does not establish a fresh landing from the repaired dory");
            EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", false));
            Assert.AreEqual(IslandWeekFact.None, progress.Read(save, 0).Held,
                "a landing before repair does not supply the historical fact");
            EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
            Assert.AreEqual(IslandWeekFact.FishFromDory, progress.Read(save, 0).Held);
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.AreEqual(IslandWeekFact.FishFromDory | IslandWeekFact.ShellfishSold,
                progress.Read(save, 0).Held, "both facts must exist before serialization");
            string json = SaveSerialization.ToJson(save);
            var restored = SaveSerialization.FromJson(json);
            Assert.AreEqual(26, restored.SchemaVersion);
            Assert.AreEqual(IslandWeekFact.FishFromDory | IslandWeekFact.ShellfishSold, progress.Read(restored, 0).Held);
            Assert.AreEqual(2, restored.OnboardingFlags.Count);
            Assert.AreEqual(1, restored.StorePurchases.Count);
            Assert.AreEqual(1, restored.SpecialOrders.Count);
            Assert.AreEqual(1, restored.ShoppingBags.Count);
            Assert.AreEqual(json, SaveSerialization.ToJson(restored), "all shopping rows and flag values survive exactly");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IslandWeek_MissingOrInvalidResourceWarnsOnceAcrossReinstalls(bool invalid)
        {
            GameServices.Save = new BlobSave(Game());
            StandOn("boat.dory");
            CatchRecords.Install();
            IslandWeekRulesDef broken = null;
            if (invalid)
            {
                broken = Object.Instantiate(IslandRules());
                _spawned.Add(broken);
                broken.EarliestDay = 0;
            }
            int loads = 0, warnings = 0;
            IslandWeekRulesDef Load(string path)
            {
                Assert.AreEqual("IslandWeek", path);
                Assert.AreEqual(CatchRecords.IslandWeekResourcesPath, path);
                loads++;
                return broken;
            }
            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Warning) warnings++;
            }
            string reason = invalid ? "The earliest day is one-based." : "Asset is missing.";
            LogAssert.Expect(LogType.Warning,
                $"[CatchRecords] Resources/IslandWeek: {reason} Island week facts will not be recorded.");
            Application.logMessageReceived += OnLog;
            try
            {
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    CatchRecords.Install(null, Load);
                    EventBus.Publish(new FishCaught(CodCatch()));
                    EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
                    EventBus.Publish(new CatchSold(10, 1, default, 1));
                    if (repeat == 1) CatchRecords.Uninstall();
                }
                Assert.AreEqual(3, loads);
                Assert.AreEqual(1, warnings, "re-arming listeners must not flood the log");
                Assert.AreEqual(3, CatchRecords.Current()[0].Count, "ordinary catch recording keeps working");
                Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
                Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));

                CatchRecords.Install();
                EventBus.Publish(new FishCaught(CodCatch()));
                EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
                EventBus.Publish(new CatchSold(10, 1, default, 1));
                Assert.AreEqual(4, CatchRecords.Current()[0].Count);
                Assert.IsTrue(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
                Assert.IsTrue(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
                Assert.AreEqual(1, warnings, "a valid resource recovers silently");
                Assert.IsFalse(GameServices.Save.GetFlag("st_peters.weather_turned"));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
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
            EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
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
            EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
            EventBus.Publish(new FreshCatchKept(CodCatch(), "boat.dory", true));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.AreEqual(3, save.OnboardingFlags.Count);
            Assert.AreEqual(2, service.FlagWrites, "each historical fact is persisted once");
            Assert.AreEqual(2, CatchRecords.Current()[0].Count, "installing twice does not duplicate handlers");
            var restored = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(SaveMigration.CurrentVersion, restored.SchemaVersion);
            Assert.AreEqual(IslandWeekFact.FishFromDory | IslandWeekFact.ShellfishSold,
                new IslandWeekProgress(IslandRules()).Read(restored, 0).Held);

            GameServices.Save = new BlobSave(Game());
            EventBus.Publish(new GameLoaded());
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.FishFromDoryKey));
            Assert.IsFalse(GameServices.Save.GetFlag(IslandWeekFactRecorder.ShellfishSoldKey));
            EventBus.Publish(new CatchSold(10, 1, default, 1));
            Assert.AreEqual(1, GameServices.Save.Current.OnboardingFlags.Count);
            Assert.AreEqual(3, save.OnboardingFlags.Count, "the previous save is untouched");
        }

        private readonly List<Object> _spawned = new();
        private readonly List<System.Action> _unsubscribe = new();
        private string _dir;

        private void Listen<T>(System.Action<T> handler)
        {
            EventBus.Subscribe(handler);
            _unsubscribe.Add(() => EventBus.Unsubscribe(handler));
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ResetCatchRecords();
            UnlicensedCatch.ResetCatalog();
            _dir = Path.Combine(Path.GetTempPath(), "hh_onboarding_progress_tests");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unsubscribe in _unsubscribe) unsubscribe();
            _unsubscribe.Clear();
            ResetCatchRecords();
            UnlicensedCatch.ResetCatalog();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private static void ResetCatchRecords()
            => typeof(CatchRecords).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

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
