using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v23 -> v24: THE CATCH RECORD, GIFT DAYS, COOK JOBS AND LORAN NUMBERS, on top of the food stores' v23.
    /// Pins the three obligations of a schema bump: an older save survives and starts with every new list
    /// empty (nothing invented), a missing or damaged list is repaired, and full rows come back from the
    /// real JSON exactly. Plus the one wired consumer: the catch record writes through to the save and a
    /// reload shows the same Notebook rows.
    /// </summary>
    public class SaveMigrationV24Tests
    {
        const string Cod = "fish.atlantic_cod";
        const string Wolffish = "fish.atlantic_wolffish";

        static CatchItem Catch(string id, float kg, string name)
            => new CatchItem(id, name, FishCategory.InshoreGroundfish, kg, 0, 0f);

        [Test]
        public void TheSchemaIsAtLeast24()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 24);
        }

        [Test]
        public void AV23Save_StartsWithEveryNewListEmpty()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":23,\"GameTimeSeconds\":4321.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            AssertEmptyV24(save);
            Assert.AreEqual(4321d, save.GameTimeSeconds, "nothing else moved");
        }

        [Test]
        public void AV20Save_WalksTheWholeChain()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":20,\"GameTimeSeconds\":4321.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            AssertEmptyV24(save);
            Assert.IsNotNull(save.PlayerBody, "the body's v21 step still ran");
            Assert.IsNotNull(save.PlayerCleanliness, "cleanliness's v22 step still ran");
            Assert.IsNotNull(save.KitchenStores, "the food stores' v23 step still ran");
        }

        [Test]
        public void AV23ObjectWithNullLists_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 23, CaughtSpecies = null, NpcGiftDays = null, CookJobs = null, HullLoranWaypoints = null,
            });
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            AssertEmptyV24(save);
        }

        [Test]
        public void ACurrentSaveWithNullLists_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                CaughtSpecies = null, NpcGiftDays = null, CookJobs = null, HullLoranWaypoints = null,
            });
            AssertEmptyV24(save);
        }

        [Test]
        public void ANewGameStartsWithEveryNewListEmpty()
        {
            AssertEmptyV24(SaveMigration.NewGame());
        }

        [Test]
        public void DamagedRowsAreDropped_AndGoodOnesKept()
        {
            SaveData save = SaveMigration.NewGame();
            save.CaughtSpecies = new List<CaughtSpeciesDto>
            {
                null,
                new CaughtSpeciesDto { SpeciesId = "", Count = 2 },
                new CaughtSpeciesDto { SpeciesId = Cod, Count = 0 },
                new CaughtSpeciesDto { SpeciesId = Cod, DisplayName = null, Count = 3, ReleasedCount = 9, RecordWeightKg = float.NaN },
                new CaughtSpeciesDto { SpeciesId = Cod, Count = 5 },
                new CaughtSpeciesDto { SpeciesId = Wolffish, Count = 1, ReleasedCount = -1, RecordWeightKg = 4.5f },
            };
            save.NpcGiftDays = new List<NpcGiftDayDto>
            {
                null,
                new NpcGiftDayDto { NpcId = "", DayIndex = 3, Count = 1 },
                new NpcGiftDayDto { NpcId = "npc.aunt_ginny", DayIndex = 3, Count = 0 },
                new NpcGiftDayDto { NpcId = "npc.aunt_ginny", DayIndex = 3, Count = 1 },
            };
            save.CookJobs = new List<CookJobDto>
            {
                null,
                new CookJobDto { StationKey = "", RecipeId = "recipe.a", ReadyAtGameSeconds = 1d },
                new CookJobDto { StationKey = "stove.1", RecipeId = "", ReadyAtGameSeconds = 1d },
                new CookJobDto { StationKey = "stove.1", RecipeId = "recipe.a", ReadyAtGameSeconds = double.NaN },
                new CookJobDto { StationKey = "stove.1", StationId = null, RecipeId = "recipe.a", ReadyAtGameSeconds = 7d },
            };
            save.HullLoranWaypoints = new List<LoranWaypointDto>
            {
                null,
                new LoranWaypointDto { HullId = "", Number = 1, ChainId = "chain.a", SecondaryA = 0, SecondaryB = 1 },
                new LoranWaypointDto { HullId = "boat.cape_islander", Number = 0, ChainId = "chain.a", SecondaryA = 0, SecondaryB = 1 },
                new LoranWaypointDto { HullId = "boat.cape_islander", Number = 1, ChainId = "chain.a", SecondaryA = 1, SecondaryB = 1 },
                new LoranWaypointDto { HullId = "boat.cape_islander", Number = 2, ChainId = "chain.a", SecondaryA = 0, SecondaryB = 1, TdA = double.NaN },
                new LoranWaypointDto { HullId = "boat.cape_islander", Number = 3, ChainId = "chain.a", SecondaryA = 0, SecondaryB = 1, TdA = 1d, TdB = 2d },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(2, save.CaughtSpecies.Count, "nameless, countless and second rows for a species are dropped");
            Assert.AreEqual(Cod, save.CaughtSpecies[0].SpeciesId);
            Assert.AreEqual(3, save.CaughtSpecies[0].Count, "the first row for a species is the one kept");
            Assert.AreEqual(3, save.CaughtSpecies[0].ReleasedCount, "released is never more than caught");
            Assert.AreEqual(0f, save.CaughtSpecies[0].RecordWeightKg, "an unreadable weight reads as unknown");
            Assert.AreEqual("", save.CaughtSpecies[0].DisplayName);
            Assert.AreEqual(0, save.CaughtSpecies[1].ReleasedCount);
            Assert.AreEqual(4.5f, save.CaughtSpecies[1].RecordWeightKg);

            Assert.AreEqual(1, save.NpcGiftDays.Count);
            Assert.AreEqual(1, save.CookJobs.Count);
            Assert.AreEqual("", save.CookJobs[0].StationId);
            Assert.AreEqual(1, save.HullLoranWaypoints.Count);
            Assert.AreEqual(3, save.HullLoranWaypoints[0].Number);

            SaveMigration.Migrate(save);
            Assert.AreEqual(2, save.CaughtSpecies.Count, "idempotent");
            Assert.AreEqual(1, save.HullLoranWaypoints.Count, "idempotent");
        }

        [Test]
        public void EveryNewRow_SurvivesTheRealJson()
        {
            SaveData save = SaveMigration.NewGame();
            save.CaughtSpecies.Add(new CaughtSpeciesDto
            {
                SpeciesId = Cod, DisplayName = "Atlantic cod", Count = 4, ReleasedCount = 1, RecordWeightKg = 7.25f,
            });
            save.NpcGiftDays.Add(new NpcGiftDayDto { NpcId = "npc.aunt_ginny", DayIndex = 12, Count = 1 });
            save.CookJobs.Add(new CookJobDto
            {
                StationKey = "fixture.boat.boat.cape_islander/stove", StationId = "cooking_station.stove",
                RecipeId = "recipe.heated_beans", ReadyAtGameSeconds = 98765.25d,
            });
            save.HullLoranWaypoints.Add(LoranWaypointDto.From("boat.cape_islander",
                new LoranWaypoint(2, "loran_chain.northeast", 1, 3, 13456.75d, 44123.125d)));

            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));

            CaughtSpeciesDto c = back.CaughtSpecies[0];
            Assert.AreEqual(Cod, c.SpeciesId);
            Assert.AreEqual("Atlantic cod", c.DisplayName);
            Assert.AreEqual(4, c.Count);
            Assert.AreEqual(1, c.ReleasedCount);
            Assert.AreEqual(7.25f, c.RecordWeightKg);

            NpcGiftDayDto g = back.NpcGiftDays[0];
            Assert.AreEqual("npc.aunt_ginny", g.NpcId);
            Assert.AreEqual(12, g.DayIndex);
            Assert.AreEqual(1, g.Count);

            CookJobDto j = back.CookJobs[0];
            Assert.AreEqual("fixture.boat.boat.cape_islander/stove", j.StationKey);
            Assert.AreEqual("cooking_station.stove", j.StationId);
            Assert.AreEqual("recipe.heated_beans", j.RecipeId);
            Assert.AreEqual(98765.25d, j.ReadyAtGameSeconds);

            Assert.AreEqual("boat.cape_islander", back.HullLoranWaypoints[0].HullId);
            LoranWaypoint w = back.HullLoranWaypoints[0].ToWaypoint();
            Assert.AreEqual(2, w.Number);
            Assert.AreEqual("loran_chain.northeast", w.ChainId);
            Assert.AreEqual(1, w.SecondaryA);
            Assert.AreEqual(3, w.SecondaryB);
            Assert.AreEqual(13456.75d, w.TdA);
            Assert.AreEqual(44123.125d, w.TdB);
        }

        [Test]
        public void TheCatchRecord_WritesThroughToTheSave_AndAReloadShowsTheSameRows()
        {
            SaveData save = SaveMigration.NewGame();
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 3.2f, "Atlantic cod"), 1, save);
            log.RecordEnteredHold(Catch(Cod, 7.5f, "Atlantic cod"), 2, save);
            log.RecordReleased(Catch(Wolffish, 4f, "Atlantic wolffish"), save);

            Assert.AreEqual(2, save.CaughtSpecies.Count, "every counted catch is already on the save");
            Assert.AreEqual(2, save.CaughtSpecies[0].Count);
            Assert.AreEqual(7.5f, save.CaughtSpecies[0].RecordWeightKg);
            Assert.AreEqual(1, save.CaughtSpecies[1].ReleasedCount);

            SaveData reloaded = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            var fresh = new CatchRecordLog();
            IReadOnlyList<CaughtSpeciesEntry> rows = fresh.EntriesFor(reloaded);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(Cod, rows[0].SpeciesId, "first-caught order survives");
            Assert.AreEqual("Atlantic cod", rows[0].DisplayName);
            Assert.AreEqual(2, rows[0].Count);
            Assert.AreEqual(7.5f, rows[0].RecordWeightKg);
            Assert.AreEqual(Wolffish, rows[1].SpeciesId);
            Assert.AreEqual(1, rows[1].ReleasedCount);

            fresh.RecordEnteredHold(Catch(Cod, 9f, "Atlantic cod"), 3, reloaded);
            Assert.AreEqual(3, reloaded.CaughtSpecies[0].Count, "a catch after the reload adds to the saved row");
            Assert.AreEqual(9f, reloaded.CaughtSpecies[0].RecordWeightKg);
            Assert.AreEqual(2, reloaded.CaughtSpecies.Count, "and never a second row for the species");
        }

        [Test]
        public void ADifferentSave_ShowsOnlyItsOwnRecord()
        {
            SaveData a = SaveMigration.NewGame();
            SaveData b = SaveMigration.NewGame();
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 1f, "Atlantic cod"), 1, a);

            Assert.IsEmpty(log.EntriesFor(b), "another save never shows this one's catches");
            Assert.IsEmpty(b.CaughtSpecies, "and none were written into it");
            Assert.AreEqual(1, log.EntriesFor(a).Count, "coming back to the first save reads its own rows again");
        }

        [Test]
        public void ASessionThatIsNotASave_KeepsTheRecordInMemoryOnly()
        {
            var log = new CatchRecordLog();
            log.RecordEnteredHold(Catch(Cod, 1f, "Atlantic cod"), 1, new object());
            Assert.AreEqual(1, log.SpeciesCount);
        }

        static void AssertEmptyV24(SaveData save)
        {
            Assert.IsNotNull(save.CaughtSpecies);
            Assert.IsNotNull(save.NpcGiftDays);
            Assert.IsNotNull(save.CookJobs);
            Assert.IsNotNull(save.HullLoranWaypoints);
            Assert.AreEqual(0, save.CaughtSpecies.Count);
            Assert.AreEqual(0, save.NpcGiftDays.Count);
            Assert.AreEqual(0, save.CookJobs.Count);
            Assert.AreEqual(0, save.HullLoranWaypoints.Count);
        }
    }
}
