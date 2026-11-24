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

            public bool GetFlag(string key)
            {
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                if (string.IsNullOrEmpty(key)) return;
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }
        }

        private const string ClamLicenceId = "license.clam";

        private readonly List<Object> _spawned = new();
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _dir = Path.Combine(Path.GetTempPath(), "hh_onboarding_progress_tests");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
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

            SaveData rowed = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.BoughtDoryKey);
            rowed.RepairedBoats.Add(OnboardingProgress.DoryBoatId);
            rowed.OwnedBoats.Add(OnboardingProgress.DoryBoatId);
            rowed.ActiveHullId = OnboardingProgress.DoryBoatId;
            yield return ("first-row-after-repair", rowed, OnboardingStep.Done);

            yield return ("arrival-only", Game("arrived_st_peters"), OnboardingStep.TalkGinny);
            yield return ("logbook-only", Game(OnboardingFlags.ReadLogbookKey), OnboardingStep.TalkGinny);
            yield return ("met-and-read", Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.ReadLogbookKey),
                          OnboardingStep.DigClams);
            yield return ("catch-before-ginny", Game(OnboardingFlags.LandedCatchKey), OnboardingStep.TalkGinny);
            yield return ("purchase-before-ginny", Game(OnboardingFlags.BoughtDoryKey), OnboardingStep.TalkGinny);
            yield return ("onboarded-with-no-other-evidence", Game(OnboardingFlags.OnboardedKey), OnboardingStep.Done);

            SaveData clamLicence = Game(OnboardingFlags.MetGinnyKey);
            clamLicence.OwnedLicenses.Add(ClamLicenceId);
            yield return ("clam-licence-before-dig", clamLicence, OnboardingStep.DigClams);

            SaveData firstSaleWithoutCatch = Game(OnboardingFlags.MetGinnyKey);
            firstSaleWithoutCatch.Money = 100;
            yield return ("sale-money-alone-without-catch-record", firstSaleWithoutCatch, OnboardingStep.DigClams);

            SaveData codBeforeCatch = Game(OnboardingFlags.MetGinnyKey);
            codBeforeCatch.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            yield return ("cod-licence-implies-catch", codBeforeCatch, OnboardingStep.BuyRod);

            SaveData rodBeforeCatch = Game(OnboardingFlags.MetGinnyKey);
            rodBeforeCatch.OwnedGear.Add(OnboardingProgress.RodGearId);
            yield return ("owned-rod-implies-catch-but-not-cod-licence", rodBeforeCatch, OnboardingStep.BuyCodLicence);

            yield return ("dory-purchase-implies-catch-but-not-licence",
                          Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.BoughtDoryKey), OnboardingStep.BuyCodLicence);
            SaveData boughtBeforeRod = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.BoughtDoryKey);
            boughtBeforeRod.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            yield return ("dory-bought-before-rod", boughtBeforeRod, OnboardingStep.BuyRod);

            SaveData repairOnly = Game();
            repairOnly.RepairedBoats.Add(OnboardingProgress.DoryBoatId);
            yield return ("repair-overrides-all-missing-earlier-steps", repairOnly, OnboardingStep.Done);

            SaveData borrowed = Game(OnboardingFlags.MetGinnyKey);
            borrowed.OwnedBoats.Add(OnboardingProgress.DoryBoatId);
            borrowed.ActiveHullId = OnboardingProgress.DoryBoatId;
            yield return ("first-row-in-borrowed-dory-is-not-catch-or-purchase", borrowed, OnboardingStep.DigClams);

            SaveData wrongIds = Game(OnboardingFlags.MetGinnyKey);
            wrongIds.OwnedLicenses.Add("license.clam");
            wrongIds.OwnedGear.Add("gear.shovel");
            wrongIds.RepairedBoats.Add("boat.punt");
            yield return ("other-licence-gear-and-repaired-hull", wrongIds, OnboardingStep.DigClams);

            SaveData storedRod = Game(OnboardingFlags.MetGinnyKey, OnboardingFlags.LandedCatchKey);
            storedRod.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            storedRod.StoredContainers.Add(new StoredContainerDto
            {
                Key = "storage.stpeters.ginny_rod_rack",
                Items = new List<StoredItemDto>
                {
                    new StoredItemDto { ItemId = "tool.rod", InstanceId = "tool.st_peters.rod" }
                }
            });
            yield return ("physical-starter-rod-is-not-owned-gear", storedRod, OnboardingStep.BuyRod);

            SaveData licenceBeforeGinny = Game();
            licenceBeforeGinny.OwnedLicenses.Add(OnboardingProgress.CodLicenceId);
            licenceBeforeGinny.OwnedGear.Add(OnboardingProgress.RodGearId);
            yield return ("licence-and-rod-before-ginny", licenceBeforeGinny, OnboardingStep.TalkGinny);

            foreach (string container in new[] { "pail", "freezer", "hold" })
            {
                SaveData held = Game(OnboardingFlags.MetGinnyKey);
                var item = new HeldCatchDto { SpeciesId = "fish.atlantic_cod", Quantity = 1 };
                if (container == "pail") held.BucketCatch.Add(item);
                if (container == "freezer") held.FreezerCatch.Add(item);
                if (container == "hold") held.BoatHoldCatch.Add(item);
                yield return ("fin-fish-in-" + container + "-also-counts-as-dig", held, OnboardingStep.BuyCodLicence);
            }

            SaveData emptyCatchRow = Game(OnboardingFlags.MetGinnyKey);
            emptyCatchRow.BucketCatch.Add(new HeldCatchDto { SpeciesId = "", Quantity = 0 });
            yield return ("even-an-empty-catch-row-counts", emptyCatchRow, OnboardingStep.BuyCodLicence);

            SaveData falseFlags = Game();
            falseFlags.OnboardingFlags.Add(new SaveFlag(OnboardingFlags.MetGinnyKey, false));
            falseFlags.OnboardingFlags.Add(new SaveFlag(OnboardingFlags.OnboardedKey, false));
            yield return ("false-flag-rows-are-not-completion", falseFlags, OnboardingStep.TalkGinny);

            SaveData lateDay = Game(OnboardingFlags.MetGinnyKey);
            lateDay.DayIndex = 20;
            lateDay.GameTimeSeconds = 100000;
            yield return ("elapsed-days-do-not-advance-opening", lateDay, OnboardingStep.DigClams);

            // Same v1 boundary as SaveMigrationV2Tests: old owned hulls become repaired.
            yield return ("v1-owned-dory-is-migrated-to-done", SaveSerialization.FromJson(
                "{\"SchemaVersion\":1,\"OwnedBoats\":[\"boat.dory\"]}"), OnboardingStep.Done);
            yield return ("v1-owned-punt-does-not-finish-dory-opening", SaveSerialization.FromJson(
                "{\"SchemaVersion\":1,\"OwnedBoats\":[\"boat.punt\"]}"), OnboardingStep.TalkGinny);
            yield return ("v1-without-boat", SaveSerialization.FromJson(
                "{\"SchemaVersion\":1,\"OwnedBoats\":[]}"), OnboardingStep.TalkGinny);
            yield return ("v2-owned-dory-is-not-migrated-as-repaired", SaveSerialization.FromJson(
                "{\"SchemaVersion\":2,\"OwnedBoats\":[\"boat.dory\"]}"), OnboardingStep.TalkGinny);
        }

        private static IEnumerable<TestCaseData> SavedStepCases()
        {
            foreach (var row in EveryBeatFromState())
                yield return new TestCaseData(row.name, row.save, row.step)
                    .SetName("SavedOpening_" + row.name);
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

        [TestCaseSource(nameof(SavedStepCases))]
        public void EachStep_IsReachedFromStateAlone_AndSurvivesTheRoundTrip(
            string name, SaveData save, OnboardingStep step)
        {
            Assert.AreEqual(step, DirectorOver(save).CurrentStep, name + ": from the state");
            Assert.AreEqual(step, DirectorOver(RoundTrip(save, name)).CurrentStep,
                name + ": the same step after a save and a load");
            var facts = OnboardingProgress.Read(save, new OnboardingFlags(new SaveFlagStore()));
            Assert.AreEqual(step, OnboardingProgress.StepFor(in facts), name + ": pure reader agrees");
            Assert.AreEqual(step == OnboardingStep.Done, facts.Onboarded || facts.DoryRepaired,
                name + ": only onboarded or repaired finishes the opening");
            string key = StepTextKey(step);
            string expected = key == null ? null : (string)typeof(WorldStrings).GetField(key).GetRawConstantValue();
            Assert.AreEqual(expected, OnboardingProgress.LineFor(step), name + ": exact text key " + key);
        }

        private static string StepTextKey(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.TalkGinny: return nameof(WorldStrings.OnboardTalkGinny);
                case OnboardingStep.DigClams: return nameof(WorldStrings.OnboardDigClams);
                case OnboardingStep.BuyCodLicence: return nameof(WorldStrings.OnboardBuyLicence);
                case OnboardingStep.BuyRod: return nameof(WorldStrings.OnboardBuyRod);
                case OnboardingStep.BuyDory: return nameof(WorldStrings.OnboardBuyDory);
                case OnboardingStep.RepairDory: return nameof(WorldStrings.OnboardRepairDory);
                case OnboardingStep.Done: return null;
                default: Assert.Fail("unmapped opening step " + step); return null;
            }
        }

        [Test]
        public void NullSaveAndNullLists_AreFreshFacts_ButFlagsStillApply()
        {
            foreach (SaveData save in new[] { null, new SaveData
                     { OwnedLicenses = null, OwnedGear = null, RepairedBoats = null,
                       BucketCatch = null, FreezerCatch = null, BoatHoldCatch = null } })
            {
                var facts = OnboardingProgress.Read(save, null);
                Assert.IsFalse(facts.MetGinny);
                Assert.IsFalse(facts.LandedCatch);
                Assert.IsFalse(facts.HasCodLicence);
                Assert.IsFalse(facts.HasRod);
                Assert.IsFalse(facts.BoughtDory);
                Assert.IsFalse(facts.DoryRepaired);
                Assert.IsFalse(facts.Onboarded);
                Assert.AreEqual(OnboardingStep.TalkGinny, OnboardingProgress.StepFor(in facts));
                var flags = new OnboardingFlags(new InMemoryFlagStore()) { MetGinny = true };
                facts = OnboardingProgress.Read(save, flags);
                Assert.AreEqual(OnboardingStep.DigClams, OnboardingProgress.StepFor(in facts));
                flags.Onboarded = true;
                facts = OnboardingProgress.Read(save, flags);
                Assert.AreEqual(OnboardingStep.Done, OnboardingProgress.StepFor(in facts));
            }
            Assert.IsNull(OnboardingProgress.LineFor((OnboardingStep)999));
        }

        [TestCase("clam")]
        [TestCase("fin-fish")]
        public void AnyCatchSignal_CompletesDig_EvenWithNoSavedCatch(string species)
        {
            var save = Game(OnboardingFlags.MetGinnyKey);
            var director = DirectorOver(save);
            director.OnCaught(new FishCaught(new CatchItem(species, species,
                species == "clam" ? FishCategory.Shellfish : FishCategory.InshoreGroundfish, 1f, 1, 0f)));
            Assert.IsTrue(GameServices.Save.GetFlag(OnboardingFlags.LandedCatchKey));
            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep);
        }

        [Test]
        public void UnrelatedGearPurchaseAndRepair_DoNotAdvanceTheOpening()
        {
            var director = DirectorOver(Game(OnboardingFlags.MetGinnyKey));
            director.OnGear(new GearPurchased("gear.shovel", 1));
            director.OnBoatBought(new BoatPurchased("boat.punt", 1));
            director.OnBoatRepaired(new BoatRepaired("boat.punt", 1));
            Assert.AreEqual(OnboardingStep.DigClams, director.CurrentStep);
            Assert.IsFalse(GameServices.Save.GetFlag(OnboardingFlags.BoughtDoryKey));
            Assert.IsFalse(GameServices.Save.GetFlag(OnboardingFlags.OnboardedKey));
            director.OnGear(new GearPurchased(OnboardingProgress.RodGearId, 1));
            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep);
            director.OnBoatRepaired(new BoatRepaired(OnboardingProgress.DoryBoatId, 1));
            Assert.AreEqual(OnboardingStep.Done, director.CurrentStep);
            Assert.IsTrue(GameServices.Save.GetFlag(OnboardingFlags.OnboardedKey));
            director.OnBoatRepaired(new BoatRepaired(OnboardingProgress.DoryBoatId, 1));
            Assert.AreEqual(OnboardingStep.Done, director.CurrentStep);
        }

        [Test]
        public void LoadingAnotherSave_ClearsCatchRodAndPurchaseHeardInTheSession()
        {
            var director = DirectorOver(Game(OnboardingFlags.MetGinnyKey));
            director.OnCaught(default);
            director.OnGear(new GearPurchased(OnboardingProgress.RodGearId, 1));
            director.OnBoatBought(new BoatPurchased(OnboardingProgress.DoryBoatId, 1));
            GameServices.Save = new BlobSave(Game(OnboardingFlags.MetGinnyKey));
            Assert.AreEqual(OnboardingStep.BuyCodLicence, director.CurrentStep);
            director.OnGameLoaded(new GameLoaded());
            Assert.AreEqual(OnboardingStep.DigClams, director.CurrentStep);
        }

        [TestCase(nameof(WorldStrings.OnboardTalkGinny), "Find Aunt Ginny on the island and press E — she'll set you straight.")]
        [TestCase(nameof(WorldStrings.OnboardDigClams), "Tab shows the tide table. First buy the Shellfish Licence at LeBlanc's (Ginny fronted the fee), then dig clams on the bared flats at low water (E by a hole).")]
        [TestCase(nameof(WorldStrings.OnboardCrossBar), "Walk the bared sandbar east to Nine Mile Creek before the tide floods it back.")]
        [TestCase(nameof(WorldStrings.OnboardBuyLicence), "At Nine Mile Creek: sell your clams, then buy a COD LICENCE to fish.")]
        [TestCase(nameof(WorldStrings.OnboardBuyRod), "Buy a fishing ROD from the shop — then you can hand-line cod.")]
        [TestCase(nameof(WorldStrings.OnboardBuyDory), "Save up, then BUY the damaged dory at the Nine Mile Creek shipwright.")]
        [TestCase(nameof(WorldStrings.OnboardRepairDory), "She's yours but broken — pay the shipwright to REPAIR her.")]
        [TestCase(nameof(WorldStrings.OnboardSailHome), "Seaworthy at last — sail your dory home to Coddle Cove.")]
        [TestCase(nameof(WorldStrings.OnboardDone), "You earned her, plank and rope. Fair winds, skipper!")]
        public void OpeningTextKeys_PinTheCurrentWording(string key, string text)
        {
            Assert.AreEqual(text, typeof(WorldStrings).GetField(key).GetRawConstantValue(), key);
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
