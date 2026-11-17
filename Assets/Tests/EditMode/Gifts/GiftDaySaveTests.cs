using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The gift ledger saved with the game (v24 <see cref="SaveData.NpcGiftDays"/>): today's gifts survive a
    /// save and a load through the real JSON, a new day clears the limit and the old rows, the migration's
    /// repair of a damaged row leaves gifting working, and a v23 save starts with no gift history.
    /// </summary>
    public class GiftDaySaveTests
    {
        private const string Ginny = "npc.aunt_ginny";
        private const string Basil = "npc.basil_samson";
        private const int PerDay = 1;

        private static GiftItem Cod() =>
            GiftItem.FromCatch(new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 1f, 1, 0f), null);

        private static SaveData RoundTrip(SaveData save) =>
            SaveSerialization.FromJson(SaveSerialization.ToJson(save));

        [Test]
        public void TodaysGifts_SurviveASaveAndALoad()
        {
            SaveData save = SaveMigration.NewGame();
            var ledger = new GiftLedger();
            ledger.Count(Ginny, 7, save);

            Assert.AreEqual(1, save.NpcGiftDays.Count, "the gift was written to the live save");
            Assert.AreEqual(Ginny, save.NpcGiftDays[0].NpcId);
            Assert.AreEqual(7, save.NpcGiftDays[0].DayIndex);
            Assert.AreEqual(1, save.NpcGiftDays[0].Count);

            SaveData loaded = RoundTrip(save);
            var afterLoad = new GiftLedger();   // a fresh launch
            GiftItem cod = Cod();
            Assert.AreEqual(1, afterLoad.GiftsToday(Ginny, 7, loaded));
            Assert.AreEqual(GiftReaction.AlreadyGiftedToday,
                GiftRule.Evaluate(null, in cod, afterLoad.GiftsToday(Ginny, 7, loaded), PerDay),
                "reloading on the same day no longer lets her take a second gift");
            Assert.AreEqual(0, afterLoad.GiftsToday(Basil, 7, loaded), "per person");
        }

        [Test]
        public void TheSharedLedger_FollowsTheSessionItIsGiven()
        {
            SaveData first = SaveMigration.NewGame();
            SaveData second = SaveMigration.NewGame();
            var ledger = new GiftLedger();
            ledger.Count(Ginny, 3, first);

            Assert.AreEqual(0, ledger.GiftsToday(Ginny, 3, second), "another save starts from its own rows");
            Assert.IsEmpty(second.NpcGiftDays, "and is not written by a read");
            Assert.AreEqual(1, ledger.GiftsToday(Ginny, 3, first), "back on the first save, its rows load again");
        }

        [Test]
        public void ANewDay_ClearsTheLimit_AndOnlyTodaysRowsAreKept()
        {
            SaveData save = SaveMigration.NewGame();
            var ledger = new GiftLedger();
            ledger.Count(Ginny, 4, save);
            ledger.Count(Basil, 4, save);
            Assert.AreEqual(2, save.NpcGiftDays.Count);

            SaveData loaded = RoundTrip(save);
            var next = new GiftLedger();
            GiftItem cod = Cod();
            Assert.AreEqual(0, next.GiftsToday(Ginny, 5, loaded), "the next morning she takes one again");
            Assert.AreEqual(GiftReaction.Neutral, GiftRule.Evaluate(null, in cod, next.GiftsToday(Ginny, 5, loaded), PerDay));
            Assert.IsEmpty(loaded.NpcGiftDays, "yesterday's rows are dropped");

            next.Count(Ginny, 5, loaded);
            Assert.AreEqual(1, loaded.NpcGiftDays.Count);
            Assert.AreEqual(5, loaded.NpcGiftDays[0].DayIndex);

            next.Count(Ginny, 5, loaded);
            Assert.AreEqual(1, loaded.NpcGiftDays.Count, "one row per person");
            Assert.AreEqual(2, loaded.NpcGiftDays[0].Count);
        }

        [Test]
        public void TheRepairDroppingBadRows_LeavesGiftingWorking()
        {
            SaveData save = SaveMigration.NewGame();
            save.NpcGiftDays = new List<NpcGiftDayDto>
            {
                null,
                new NpcGiftDayDto { NpcId = "", DayIndex = 9, Count = 1 },
                new NpcGiftDayDto { NpcId = Basil, DayIndex = 9, Count = 0 },
                new NpcGiftDayDto { NpcId = Ginny, DayIndex = 9, Count = 1 },
            };
            SaveData repaired = SaveMigration.Migrate(save);
            Assert.AreEqual(1, repaired.NpcGiftDays.Count, "the repair kept only the good row");

            var ledger = new GiftLedger();
            Assert.AreEqual(1, ledger.GiftsToday(Ginny, 9, repaired));
            Assert.AreEqual(0, ledger.GiftsToday(Basil, 9, repaired), "a dropped row reads as no gift");
            Assert.DoesNotThrow(() => ledger.Count(Basil, 9, repaired));
            Assert.AreEqual(1, ledger.GiftsToday(Basil, 9, repaired));
        }

        [Test]
        public void BadRowsTheRepairHasNotSeen_AreSkippedNotFatal()
        {
            var save = new SaveData
            {
                NpcGiftDays = new List<NpcGiftDayDto>
                {
                    null,
                    new NpcGiftDayDto { NpcId = Ginny, DayIndex = 2, Count = -4 },
                    new NpcGiftDayDto { NpcId = Basil, DayIndex = 2, Count = 1 },
                    new NpcGiftDayDto { NpcId = Basil, DayIndex = 2, Count = 3 },
                },
            };
            var ledger = new GiftLedger();
            Assert.DoesNotThrow(() => ledger.GiftsToday(Ginny, 2, save));
            Assert.AreEqual(0, ledger.GiftsToday(Ginny, 2, save));
            Assert.AreEqual(3, ledger.GiftsToday(Basil, 2, save), "a duplicate keeps the larger count");

            var none = new SaveData { NpcGiftDays = null };
            Assert.AreEqual(0, new GiftLedger().GiftsToday(Ginny, 2, none));
            Assert.DoesNotThrow(() => new GiftLedger().Count(Ginny, 2, none));
            Assert.AreEqual(1, none.NpcGiftDays.Count, "a missing list is made on the first write");
        }

        [Test]
        public void AV23Save_LoadsWithNoGiftHistory()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":23,\"GameTimeSeconds\":4321.0}");
            Assert.IsNotNull(save.NpcGiftDays);
            Assert.IsEmpty(save.NpcGiftDays);

            var ledger = new GiftLedger();
            GiftItem cod = Cod();
            Assert.AreEqual(0, ledger.GiftsToday(Ginny, 0, save));
            Assert.AreEqual(GiftReaction.Neutral, GiftRule.Evaluate(null, in cod, ledger.GiftsToday(Ginny, 0, save), PerDay));
        }

        [Test]
        public void WithoutASave_TheLedgerStaysInMemory()
        {
            var ledger = new GiftLedger();
            ledger.Count(Ginny, 1);
            Assert.AreEqual(1, ledger.GiftsToday(Ginny, 1));
            Assert.AreEqual(0, ledger.GiftsToday(Ginny, 1, new object()), "a non-save session is a fresh ledger");
        }
    }
}
