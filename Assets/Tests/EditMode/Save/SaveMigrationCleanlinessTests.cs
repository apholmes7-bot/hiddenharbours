using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v20 -> v21: CLEANLINESS (owner ask 2026-11-15). An older save loads clean, idle and anchored at its
    /// own saved time, so nothing changes until the player gets dirty; a new game starts clean and
    /// unanchored; the field round-trips through the real JSON; the step is idempotent.
    /// </summary>
    public class SaveMigrationCleanlinessTests
    {
        [Test]
        public void V20Save_LoadsClean_AnchoredAtItsOwnTime()
        {
            var old = new SaveData { SchemaVersion = 20, GameTimeSeconds = 4321d, PlayerName = "Skipper", PlayerCleanliness = null };
            SaveData save = SaveMigration.Migrate(old);
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 21);
            Assert.IsNotNull(save.PlayerCleanliness);
            Assert.AreEqual(0f, save.PlayerCleanliness.Dirt);
            Assert.AreEqual(4321d, save.PlayerCleanliness.SinceGameSeconds);
            Assert.AreEqual(0, save.PlayerCleanliness.Activity);
            Assert.AreEqual("", save.PlayerCleanliness.WashStationId);
            Assert.Less(save.PlayerCleanliness.StretchSinceGameSeconds, 0d);
            Assert.AreEqual("Skipper", save.PlayerName, "nothing else touched");
        }

        [Test]
        public void NewGame_StartsClean_Unanchored()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsNotNull(save.PlayerCleanliness);
            Assert.AreEqual(0f, save.PlayerCleanliness.Dirt);
            Assert.IsFalse(save.PlayerCleanliness.IsStamped, "the first slow tick anchors it");
        }

        [Test]
        public void CurrentSave_RoundTrips_AndIsNotReCleaned()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerCleanliness = new PlayerCleanlinessDto
            {
                Dirt = 0.625f, SinceGameSeconds = 98765.5d, Activity = (int)DirtActivity.Gym,
                WashStationId = "", StretchSinceGameSeconds = 98000d,
            };
            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(0.625f, back.PlayerCleanliness.Dirt);
            Assert.AreEqual(98765.5d, back.PlayerCleanliness.SinceGameSeconds);
            Assert.AreEqual((int)DirtActivity.Gym, back.PlayerCleanliness.Activity);
            Assert.AreEqual(98000d, back.PlayerCleanliness.StretchSinceGameSeconds);

            SaveData again = SaveMigration.Migrate(back);
            Assert.AreEqual(0.625f, again.PlayerCleanliness.Dirt, "idempotent: a current save is not washed");
        }

        [Test]
        public void MissingField_OnACurrentSave_IsRepaired()
        {
            var save = new SaveData { SchemaVersion = SaveMigration.CurrentVersion, PlayerCleanliness = null };
            Assert.IsNotNull(SaveMigration.Migrate(save).PlayerCleanliness);
        }
    }
}
