using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class SaveMigrationV17Tests
    {
        [Test]
        public void Version16WithoutRecipeOrName_LoadsAsBakedWithNoName()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":16}");
            Assert.AreEqual(17, save.SchemaVersion);
            Assert.IsTrue(save.PlayerRecipe.IsEmpty);
            Assert.AreEqual("", save.PlayerName);
        }

        [Test]
        public void Version16_KeepsExistingRecipeAndOutfit_AndAddsNoName()
        {
            var recipe = new CharacterRecipe { BuildId = "char_build.fisher", Hair = "black" };
            var old = new SaveData { SchemaVersion = 16, PlayerRecipe = recipe, WornOutfitId = "outfit.fixture" };
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(17, save.SchemaVersion);
            Assert.IsTrue(save.PlayerRecipe.SameAs(recipe));
            Assert.AreEqual("outfit.fixture", save.WornOutfitId);
            Assert.AreEqual("", save.PlayerName);
        }

        [Test]
        public void Version17_NameSurvivesRoundTrip()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerName = "Skipper";
            Assert.AreEqual("Skipper", SaveSerialization.FromJson(SaveSerialization.ToJson(save)).PlayerName);
        }

        [Test]
        public void CurrentVersionNullName_IsRepairedToNone()
        {
            var save = new SaveData { SchemaVersion = SaveMigration.CurrentVersion, PlayerName = null };
            Assert.AreEqual("", SaveMigration.Migrate(save).PlayerName);
        }
    }
}
