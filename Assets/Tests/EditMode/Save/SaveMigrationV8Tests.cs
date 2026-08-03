using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SaveData v7 → v8 (ADR 0025 S2 / ADR 0030 — helm instruments become purchasable PER HULL). Pins the
    /// migration contract (an older save gets two empty sparse lists and NOTHING else changes — never a
    /// crash), the sparse-absence semantics that keep the default fit authoritative, the
    /// <see cref="InstrumentLocker"/> read/write rules, and the whole thing through the REAL
    /// serialization path.
    ///
    /// <para><b>The load-bearing assertion is the absence one:</b> a v7 save must come out of the
    /// migration owning NO instruments on ANY hull, because that is exactly what "the console's authored
    /// default is what you have" means. If the migration ever invented a fitment, every pre-v8 boat would
    /// silently gain an instrument the player never bought.</para>
    /// </summary>
    public class SaveMigrationV8Tests
    {
        private static SaveData V7Blob()
        {
            var d = new SaveData
            {
                SchemaVersion = 7,
                WorldSeed = 4242,
                GameTimeSeconds = 9001.5,
                Money = 175,
                ActiveHullId = "boat.skiff",
            };
            d.OwnedBoats.Add("boat.dory");
            d.OwnedBoats.Add("boat.skiff");
            d.OwnedGear.Add("gear.rod");
            // A true v7 blob never had the lists — JsonUtility leaves absent reference fields null.
            d.HullInstruments = null;
            d.HullSounderPrefs = null;
            return d;
        }

        // ---- the migration -------------------------------------------------------------------------

        [Test]
        public void V7_MigratesToCurrent_WithNoInstrumentsOwned_AndNothingElseTouched()
        {
            SaveData d = SaveMigration.Migrate(V7Blob());

            // VERSION-AGNOSTIC on the counter (the SaveMigrationV3Tests rule): what this test owns is the
            // v8 CONTRACT, not the value of a counter every later lane bumps.
            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion,
                "a v7 blob climbs the whole chain to the current schema");
            Assert.GreaterOrEqual(d.SchemaVersion, 8, "and v8's per-hull instrument lists exist from that climb");

            Assert.IsNotNull(d.HullInstruments); Assert.IsEmpty(d.HullInstruments);
            Assert.IsNotNull(d.HullSounderPrefs); Assert.IsEmpty(d.HullSounderPrefs);

            // THE point of the step: every pre-v8 hull keeps exactly its console's authored fit.
            Assert.IsFalse(InstrumentLocker.Owns(d, "boat.skiff", "instrument.depth_sounder"),
                "a save from before instruments were purchasable owns none of them");
            Assert.IsFalse(InstrumentLocker.Owns(d, "boat.dory", "instrument.depth_sounder"));

            Assert.AreEqual(4242, d.WorldSeed);
            Assert.AreEqual(9001.5, d.GameTimeSeconds, 1e-9);
            Assert.AreEqual(175, d.Money);
            Assert.AreEqual("boat.skiff", d.ActiveHullId);
            CollectionAssert.AreEqual(new[] { "boat.dory", "boat.skiff" }, d.OwnedBoats);
            CollectionAssert.AreEqual(new[] { "gear.rod" }, d.OwnedGear);
        }

        [Test]
        public void AnOlderBlob_ClimbsTheWholeChain_AndStillLandsWithEmptyInstruments()
        {
            var v2 = new SaveData { SchemaVersion = 2, Money = 10, ActiveHullId = "boat.dory" };
            v2.OwnedBoats.Add("boat.dory");
            SaveData d = SaveMigration.Migrate(v2);

            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion);
            Assert.IsNotNull(d.HullInstruments); Assert.IsEmpty(d.HullInstruments);
            Assert.IsNotNull(d.HullSounderPrefs); Assert.IsEmpty(d.HullSounderPrefs);
            Assert.AreEqual(10, d.Money);
        }

        [Test]
        public void V7Json_LoadsThroughTheRealPath_NeverACrash()
        {
            string json = SaveSerialization.ToJson(V7Blob());
            SaveData d = SaveMigration.Migrate(SaveSerialization.FromJson(json));

            Assert.IsNotNull(d);
            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion);
            Assert.IsNotNull(d.HullInstruments);
            Assert.IsNotNull(d.HullSounderPrefs);
        }

        [Test]
        public void ANewGame_StartsAtTheCurrentVersion_WithTheInstrumentListsReady()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 8,
                "v8 (per-hull instruments) has shipped, and the counter only ever goes up");
            SaveData fresh = SaveMigration.NewGame();
            Assert.AreEqual(SaveMigration.CurrentVersion, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.HullInstruments);
            Assert.IsNotNull(fresh.HullSounderPrefs);
        }

        // ---- the locker ----------------------------------------------------------------------------

        [Test]
        public void Ownership_IsPerHull_AndIdempotent()
        {
            SaveData d = SaveMigration.NewGame();
            const string sounder = "instrument.depth_sounder";

            Assert.IsTrue(InstrumentLocker.Add(d, "boat.skiff", sounder), "a new fitment is recorded");
            Assert.IsFalse(InstrumentLocker.Add(d, "boat.skiff", sounder), "fitting it twice is a no-op");
            Assert.AreEqual(1, d.HullInstruments.Count);

            Assert.IsTrue(InstrumentLocker.Owns(d, "boat.skiff", sounder));
            Assert.IsFalse(InstrumentLocker.Owns(d, "boat.punt", sounder),
                "an instrument is bolted into ONE dash — the next boat needs its own");

            Assert.IsTrue(InstrumentLocker.Add(d, "boat.punt", sounder), "and can be bought again for it");
            Assert.AreEqual(2, d.HullInstruments.Count);
        }

        [Test]
        public void OwnedFor_FillsTheCallersList_ForOneHullOnly()
        {
            SaveData d = SaveMigration.NewGame();
            InstrumentLocker.Add(d, "boat.skiff", "instrument.depth_sounder");
            InstrumentLocker.Add(d, "boat.skiff", "instrument.gps");
            InstrumentLocker.Add(d, "boat.punt", "instrument.radar");

            var into = new System.Collections.Generic.List<string> { "stale" };
            InstrumentLocker.OwnedFor(d, "boat.skiff", into);
            CollectionAssert.AreEquivalent(new[] { "instrument.depth_sounder", "instrument.gps" }, into,
                "the list is cleared first and carries only this hull's fitments");

            InstrumentLocker.OwnedFor(d, "boat.dory", into);
            Assert.IsEmpty(into, "a hull with no deviations reads as empty — its console default stands");
        }

        [Test]
        public void NullSave_ReadsAsNothingOwned_RatherThanThrowing()
        {
            Assert.IsFalse(InstrumentLocker.Owns(null, "boat.skiff", "instrument.depth_sounder"));
            Assert.IsFalse(InstrumentLocker.Add(null, "boat.skiff", "instrument.depth_sounder"));
            Assert.IsEmpty(InstrumentLocker.OwnedFor(null, "boat.skiff", null));
            var fallback = new SounderPrefs(7f, true, false, false);
            Assert.AreEqual(7f, InstrumentLocker.PrefsFor(null, "boat.skiff", in fallback).AlarmMetres);
            Assert.IsFalse(InstrumentLocker.SetPrefs(null, "boat.skiff", in fallback));
        }

        // ---- preferences ---------------------------------------------------------------------------

        [Test]
        public void SounderPrefs_AreSparse_PerHull_AndSurviveTheRealSavePath()
        {
            SaveData d = SaveMigration.NewGame();
            var defaults = SounderPrefs.FromDefaults(DepthSounderSettings.Default);

            Assert.AreEqual(defaults.AlarmMetres,
                InstrumentLocker.PrefsFor(d, "boat.skiff", in defaults).AlarmMetres,
                "an untouched hull reads the owner's defaults, and stores nothing");
            Assert.IsEmpty(d.HullSounderPrefs);

            var set = new SounderPrefs(4.5f, true, true, true);
            Assert.IsTrue(InstrumentLocker.SetPrefs(d, "boat.skiff", in set));
            Assert.IsFalse(InstrumentLocker.SetPrefs(d, "boat.skiff", in set),
                "writing the same preferences again reports no change — no needless disk write");
            Assert.AreEqual(1, d.HullSounderPrefs.Count);

            SaveData reloaded = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(d)));
            SounderPrefs back = InstrumentLocker.PrefsFor(reloaded, "boat.skiff", in defaults);
            Assert.AreEqual(4.5f, back.AlarmMetres, 1e-6f, "the set-point a fisherman dialled survives a reload");
            Assert.IsTrue(back.Armed);
            Assert.IsTrue(back.Feet);
            Assert.IsTrue(back.Night);

            Assert.AreEqual(defaults.AlarmMetres,
                InstrumentLocker.PrefsFor(reloaded, "boat.punt", in defaults).AlarmMetres,
                "and the OTHER boat's glass is untouched");
        }

        [Test]
        public void TheSaveNeverCarriesADepth()
        {
            // Rule 5, stated as a test: a sounding is recomputed from the height map every tick. If a
            // depth field ever appears on SaveData this assertion is the thing that has to be argued with.
            foreach (var f in typeof(SaveData).GetFields())
            {
                Assert.That(f.Name, Does.Not.Contain("Depth"),
                    $"SaveData.{f.Name} — a sounding is recomputed from the one height map every tick " +
                    "(rule 5). A saved depth is the exact bug that rule exists to prevent.");
                Assert.That(f.Name, Does.Not.Contain("WaterLevel"), $"SaveData.{f.Name} — likewise the tide.");
            }
            foreach (var f in typeof(SounderPrefsDto).GetFields())
                Assert.That(f.Name, Does.Not.Contain("Depth"),
                    $"SounderPrefsDto.{f.Name} — the sounder persists PREFERENCES, never a reading.");
        }
    }
}
