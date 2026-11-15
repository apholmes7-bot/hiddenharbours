using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Save schema v19: hunger and tiredness (needs slice 1). An older save loads fed and rested.</summary>
    public class SaveMigrationV19Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastNineteen_BecauseNeedsShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 19);
        }

        [Test]
        public void Version18Json_WithoutNeeds_LoadsFedAndRested_AtItsOwnTime()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":18,\"GameTimeSeconds\":5000.0,\"PlayerName\":\"Skipper\"}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.PlayerNeeds);
            Assert.AreEqual(0f, save.PlayerNeeds.Hunger.Level);
            Assert.AreEqual(0f, save.PlayerNeeds.Tiredness.Level);
            Assert.AreEqual(5000d, save.PlayerNeeds.Hunger.SinceGameSeconds, "anchored at the save's own instant");
            Assert.AreEqual(5000d, save.PlayerNeeds.Tiredness.SinceGameSeconds);
            Assert.AreEqual("Skipper", save.PlayerName, "nothing else is touched");
        }

        [Test]
        public void Version18Object_WithNullNeeds_IsMigrated_AndKeepsTheRest()
        {
            var old = new SaveData { SchemaVersion = 18, GameTimeSeconds = 1200d, PlayerName = "Skipper", PlayerNeeds = null };
            old.PlayerTraits.Add("trait.speedwalker");
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(1200d, save.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual(0f, save.PlayerNeeds.Hunger.Level);
            CollectionAssert.AreEqual(new[] { "trait.speedwalker" }, save.PlayerTraits);
        }

        [Test]
        public void ANewGame_StartsFedRestedAndUnanchored()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsNotNull(save.PlayerNeeds);
            Assert.IsFalse(save.PlayerNeeds.Hunger.IsStamped, "the first slow tick anchors it");
            Assert.IsFalse(save.PlayerNeeds.Tiredness.IsStamped);
            Assert.AreEqual(0f, save.PlayerNeeds.Hunger.Level);
        }

        [Test]
        public void Version19_NeedsSurviveRoundTrip_AndMigrationKeepsThem()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(0.625f, 98765.5d),
                Tiredness = new NeedStateDto(0.25f, 12345.25d),
            };
            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(0.625f, back.PlayerNeeds.Hunger.Level);
            Assert.AreEqual(98765.5d, back.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual(0.25f, back.PlayerNeeds.Tiredness.Level);
            Assert.AreEqual(12345.25d, back.PlayerNeeds.Tiredness.SinceGameSeconds);

            SaveData again = SaveMigration.Migrate(back);
            Assert.AreEqual(0.625f, again.PlayerNeeds.Hunger.Level, "idempotent: a current save is not re-fed");
            Assert.AreEqual(98765.5d, again.PlayerNeeds.Hunger.SinceGameSeconds);
        }

        [Test]
        public void AnOldSave_PlaysWithNoNeedsPenalty()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":17,\"GameTimeSeconds\":900.0}");
            Assert.IsNotNull(save, "the v17 blob parses");
            Assert.AreEqual(900d, save.PlayerNeeds.Hunger.SinceGameSeconds);
            var service = new NeedsService(() => new FixedSave(save), () => null, () => NeedsSettings.Default,
                                           () => null, () => 75d);
            foreach (TraitStat stat in System.Enum.GetValues(typeof(TraitStat)))
                Assert.AreEqual(1f, service.PlayerMultiplier(stat));
        }

        private sealed class FixedSave : ISaveService
        {
            public FixedSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }
    }
}
