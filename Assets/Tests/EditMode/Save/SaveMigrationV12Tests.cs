using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v11 → v12: what the player is WEARING. One appended string, and the usual two obligations of a
    /// schema bump — an older save survives untouched, and the new field means the right thing when it
    /// is absent.
    ///
    /// <para><b>The third obligation is the one worth writing down.</b> Every migration step in this
    /// file's siblings HEALS what it adds — nav trims over-cap lists, the sounder repairs a zero range
    /// — so healing the outfit id would be the natural thing to reach for, and it would be wrong. An id
    /// that fails to resolve because a wardrobe asset did not load is not a corrupt save; erasing it on
    /// the next write would delete the player's choice for good. Resolution belongs where the fisher can
    /// fall back to the baked look for one session and say so. These tests are what should stop a heal
    /// landing here in good faith later.</para>
    /// </summary>
    public class SaveMigrationV12Tests
    {
        static SaveData V11(string json = null) =>
            json == null
                ? new SaveData { SchemaVersion = 11 }
                : JsonUtility.FromJson<SaveData>(json);

        [Test]
        public void CurrentVersion_IsTwelve()
        {
            Assert.AreEqual(12, SaveMigration.CurrentVersion,
                "The wardrobe appended SaveData.WornOutfitId, so the schema moved.");
        }

        [Test]
        public void ANewGame_WearsTheBakedLook()
        {
            var data = SaveMigration.NewGame();

            Assert.AreEqual(12, data.SchemaVersion);
            Assert.AreEqual("", data.WornOutfitId,
                "An empty id reads as 'the outfit the sheets were baked in'. A new game must NOT name " +
                "a preset: the shipped art is the look, and naming an id would make a new player's " +
                "first frame a recolour of it.");
        }

        [Test]
        public void AV11Save_GainsAnEmptyOutfit_AndIsStampedV12()
        {
            var data = SaveMigration.Migrate(V11());

            Assert.AreEqual(12, data.SchemaVersion);
            Assert.AreEqual("", data.WornOutfitId,
                "A v11 save was written before there was a wardrobe, so its player wore the baked " +
                "outfit. Nothing is invented for them.");
        }

        [Test]
        public void AV11Save_KeepsEverythingElseItHad()
        {
            var before = new SaveData
            {
                SchemaVersion = 11,
                WorldSeed = 4242,
                GameTimeSeconds = 98765.5,
                Money = 913,
                DayIndex = 7,
                ActiveHullId = "boat.punt",
            };
            before.OwnedBoats.Add("boat.dory");
            before.OwnedBoats.Add("boat.punt");
            before.OwnedLicenses.Add("license.cod");

            var after = SaveMigration.Migrate(before);

            Assert.AreEqual(4242, after.WorldSeed);
            Assert.AreEqual(98765.5, after.GameTimeSeconds, 1e-9);
            Assert.AreEqual(913, after.Money);
            Assert.AreEqual(7, after.DayIndex);
            Assert.AreEqual("boat.punt", after.ActiveHullId);
            CollectionAssert.AreEqual(new[] { "boat.dory", "boat.punt" }, after.OwnedBoats);
            CollectionAssert.AreEqual(new[] { "license.cod" }, after.OwnedLicenses);
        }

        [Test]
        public void AV12Save_KeepsTheOutfitItWasWearing()
        {
            var data = SaveMigration.Migrate(new SaveData { SchemaVersion = 12, WornOutfitId = "outfit.oilskins" });

            Assert.AreEqual("outfit.oilskins", data.WornOutfitId,
                "Migrating a current save must not re-dress the player.");
        }

        /// <summary>
        /// The one a well-meaning later change would break: an id the shipped wardrobe cannot resolve
        /// must SURVIVE the loader. The content may be back next build — a mod, a re-import, an asset
        /// that failed to load once — and a save that quietly forgot has thrown the choice away.
        /// </summary>
        [Test]
        public void AnUnresolvableOutfitId_IsNotHealedAway()
        {
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 11,
                WornOutfitId = "outfit.something_a_later_drop_removed",
            });

            Assert.AreEqual("outfit.something_a_later_drop_removed", data.WornOutfitId,
                "The loader has no business deciding a retired outfit means 'wear the default'. " +
                "PlayerOutfitVisual falls back for the session and logs; the save keeps the choice.");
        }

        [Test]
        public void AJsonBlobWithNoOutfitField_ReadsAsTheBakedLook()
        {
            // Exactly what a v11 file on disk looks like: the field simply is not there.
            var data = SaveMigration.Migrate(V11("{\"SchemaVersion\":11,\"Money\":50}"));

            Assert.AreEqual(12, data.SchemaVersion);
            Assert.AreEqual(50, data.Money);
            Assert.IsNotNull(data.WornOutfitId, "An absent field must never surface as null.");
            Assert.AreEqual("", data.WornOutfitId);
        }

        [Test]
        public void ItRoundTripsThroughJson()
        {
            var before = SaveMigration.NewGame();
            before.WornOutfitId = "outfit.rust_and_cream";

            var after = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(before));

            Assert.AreEqual("outfit.rust_and_cream", after.WornOutfitId);
            Assert.AreEqual(12, after.SchemaVersion);
        }

        // ---- the locker over the top of it ------------------------------------------------------

        [Test]
        public void TheLocker_ReportsAnEmptyIdForABareSave()
        {
            Assert.AreEqual("", OutfitLocker.WornOutfitId(new SaveData()));
            Assert.AreEqual("", OutfitLocker.WornOutfitId((SaveData)null),
                "No save is the baked look, not a null the caller has to guard.");
        }

        [Test]
        public void TheLocker_ReportsWhetherSomethingActuallyChanged()
        {
            var save = SaveMigration.NewGame();

            Assert.IsTrue(OutfitLocker.Wear(save, "outfit.spruce"));
            Assert.AreEqual("outfit.spruce", OutfitLocker.WornOutfitId(save));

            Assert.IsFalse(OutfitLocker.Wear(save, "outfit.spruce"),
                "Re-picking what is already on is not a change — so nothing is published, nothing is " +
                "re-skinned, and no file is written.");

            Assert.IsTrue(OutfitLocker.Wear(save, null), "Taking an outfit off is a change.");
            Assert.AreEqual("", OutfitLocker.WornOutfitId(save));
        }
    }
}
