using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SaveData v5 → v6 (M1 §7.3 — held catch persists; renumbered past #324's v5 rename step, so a v4 input here also exercises the CHAIN v4→v5→v6). Pins the migration contract (an older save
    /// gets empty hold lists and NOTHING else changes — never a crash), the codec's round-trip
    /// (grouping only ever merges EXACTLY-identical freshness), and the arc's exit criterion through
    /// the REAL serialization path: a settled FreshnessState written to JSON and read back yields the
    /// bit-same spoil at any later read — <c>FreshnessTests.SaveAndReload_RestoresExactly</c>, but
    /// through <see cref="SaveSerialization"/> + <see cref="HoldCatchCodec"/> instead of a copy.
    /// </summary>
    public class SaveMigrationV6Tests
    {
        private const float SecondsPerDay = GameConfig.DefaultSecondsPerDay;

        // A factory over two known species — what the Fishing-side registry factory does, minus Unity.
        private sealed class FakeFactory : ICatchItemFactory
        {
            public bool TryCreate(string speciesId, out CatchItem item)
            {
                switch (speciesId)
                {
                    case "fish.cod":
                        item = new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish,
                                             4f, 20, 0.3f, 1f, default);
                        return true;
                    case "fish.clam":
                        item = new CatchItem("fish.clam", "Clam", FishCategory.Shellfish,
                                             0.1f, 2, 0.45f, 0.5f, default);
                        return true;
                    default:
                        item = default;
                        return false;
                }
            }
        }

        // ---- the migration ---------------------------------------------------------------------

        private static SaveData V4Blob()
        {
            var d = new SaveData
            {
                SchemaVersion = 4,
                WorldSeed = 1234,
                GameTimeSeconds = 5678.25,
                Money = 321,
                ActiveHullId = "boat.dory",
            };
            d.OwnedBoats.Add("boat.dory");
            d.BoatHoldCatch = null;   // a true v4 blob never had the lists — JsonUtility leaves null
            d.BucketCatch = null;
            d.FreezerCatch = null;
            return d;
        }

        [Test]
        public void V4_MigratesTo5_WithEmptyHolds_AndNothingElseTouched()
        {
            SaveData d = SaveMigration.Migrate(V4Blob());

            Assert.AreEqual(6, d.SchemaVersion);
            Assert.IsNotNull(d.BoatHoldCatch); Assert.IsEmpty(d.BoatHoldCatch);
            Assert.IsNotNull(d.BucketCatch);   Assert.IsEmpty(d.BucketCatch);
            Assert.IsNotNull(d.FreezerCatch);  Assert.IsEmpty(d.FreezerCatch);

            Assert.AreEqual(1234, d.WorldSeed);
            Assert.AreEqual(5678.25, d.GameTimeSeconds, 1e-9);
            Assert.AreEqual(321, d.Money);
            Assert.AreEqual("boat.dory", d.ActiveHullId);
        }

        [Test]
        public void CurrentVersion_Is5_AndNewGameStartsThere()
        {
            Assert.AreEqual(6, SaveMigration.CurrentVersion);
            SaveData fresh = SaveMigration.NewGame();
            Assert.AreEqual(6, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.BoatHoldCatch);
        }

        [Test]
        public void V4Json_LoadsThroughTheRealPath_NeverACrash()
        {
            // The genuine on-disk shape: serialize a v4 blob (fields absent from the JSON entirely
            // would need a hand-written string; null lists serialize as empty — both land safely).
            string json = SaveSerialization.ToJson(V4Blob());
            SaveData d = SaveMigration.Migrate(SaveSerialization.FromJson(json));

            Assert.IsNotNull(d);
            Assert.AreEqual(6, d.SchemaVersion);
            Assert.IsNotNull(d.BoatHoldCatch);
        }

        // ---- the codec -------------------------------------------------------------------------

        [Test]
        public void Capture_GroupsOnlyExactlyIdenticalFreshness()
        {
            var sameClock = Freshness.Landed(100d);
            var items = new List<CatchItem>
            {
                new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f, 1f, sameClock),
                new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f, 1f, sameClock),
                new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f, 1f,
                              Freshness.Landed(200d)),   // a later landing — its own clock
            };
            var records = new List<HeldCatchDto>();
            HoldCatchCodec.Capture(items, records);

            Assert.AreEqual(2, records.Count, "same haul compresses; a different clock never merges");
            Assert.AreEqual(2, records[0].Quantity);
            Assert.AreEqual(1, records[1].Quantity);
            Assert.AreEqual(200d, records[1].LastSettleGameSeconds, 1e-9);
        }

        [Test]
        public void Restore_SkipsUnknownSpecies_NeverCrashes()
        {
            var records = new List<HeldCatchDto>
            {
                new HeldCatchDto("fish.cod", 2, 0.25f, 300d, (int)StorageMode.Frozen),
                new HeldCatchDto("fish.retired_species", 3, 0f, 0d, 0),
            };
            var items = new List<CatchItem>();
            int restored = HoldCatchCodec.Restore(records, new FakeFactory(), items);

            Assert.AreEqual(2, restored, "the unknown id is skipped, the rest lands");
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(StorageMode.Frozen, items[0].Freshness.Mode);
            Assert.AreEqual(0.25f, items[0].Freshness.SpoilAccrued, 1e-6f);
        }

        // ---- the exit criterion: freshness survives the REAL save path exactly ------------------

        [Test]
        public void FreshnessSurvivesSaveAndReload_Exactly_ThroughTheRealPath()
        {
            // A cod landed at t0, settled once at three-quarters of a day (the FreshnessTests case).
            FreshnessState before = Freshness.Settle(Freshness.Landed(0d), SecondsPerDay * 0.75d,
                                                     1f, SecondsPerDay, SpoilPolicy.Default);
            var held = new List<CatchItem>
            {
                new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f, 1f, before),
            };

            // Save: capture into the blob, serialize to JSON (the on-disk format), read back, migrate.
            var save = SaveMigration.NewGame();
            HoldCatchCodec.Capture(held, save.BoatHoldCatch);
            SaveData reloaded = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));

            // Load: rebuild the items and read spoil at a later instant on both sides.
            var restored = new List<CatchItem>();
            Assert.AreEqual(1, HoldCatchCodec.Restore(reloaded.BoatHoldCatch, new FakeFactory(), restored));

            double later = SecondsPerDay * 2d;
            float beforeSpoil = Freshness.SpoilAt(before, later, 1f, SecondsPerDay, SpoilPolicy.Default);
            float afterSpoil = Freshness.SpoilAt(restored[0].Freshness, later, 1f,
                                                 SecondsPerDay, SpoilPolicy.Default);

            Assert.AreEqual(beforeSpoil, afterSpoil, 1e-6f,
                            "a reload lands on exactly the number an unbroken session would");
            Assert.AreEqual(before.SpoilAccrued, restored[0].Freshness.SpoilAccrued,
                            "the banked spoil round-trips bit-exact through JSON");
            Assert.AreEqual(before.LastSettleGameSeconds, restored[0].Freshness.LastSettleGameSeconds,
                            "and so does the settle stamp (double precision)");
        }
    }
}
