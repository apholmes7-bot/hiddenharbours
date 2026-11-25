using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Save schema v18: character traits (traits slice 1). An older save loads with none.</summary>
    public class SaveMigrationV18Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastEighteen_BecauseTraitsShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 18);
        }

        [Test]
        public void Version17Json_WithoutTraits_LoadsWithNone()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":17,\"PlayerName\":\"Skipper\"}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.PlayerTraits);
            Assert.IsEmpty(save.PlayerTraits);
            Assert.AreEqual("Skipper", save.PlayerName, "nothing else is touched");
        }

        [Test]
        public void Version17Object_WithNullTraits_IsGivenAnEmptyList_AndKeepsTheRest()
        {
            var recipe = new CharacterRecipe { BuildId = "char_build.fisher", Hair = "black" };
            var old = new SaveData { SchemaVersion = 17, PlayerRecipe = recipe, PlayerName = "Skipper", PlayerTraits = null };
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsEmpty(save.PlayerTraits);
            Assert.IsTrue(save.PlayerRecipe.SameAs(recipe));
            Assert.AreEqual("Skipper", save.PlayerName);
        }

        [Test]
        public void AnOldSave_PlaysWithNoTraitBonus()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":16}");
            var service = new TraitService(() => new FixedSave(save), () => new TraitDef[0]);
            foreach (TraitStat stat in System.Enum.GetValues(typeof(TraitStat)))
                Assert.AreEqual(1f, service.PlayerMultiplier(stat));
        }

        [Test]
        public void Version18_TraitsSurviveRoundTrip_AndMigrationKeepsThem()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerTraits = new List<string> { "trait.charismatic", "trait.speedwalker" };
            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            CollectionAssert.AreEqual(save.PlayerTraits, back.PlayerTraits);
            CollectionAssert.AreEqual(save.PlayerTraits, SaveMigration.Migrate(back).PlayerTraits, "idempotent");
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
