using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WatchDefContentTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string DigitalPath = DataRoot + "/Resources/Watches/HarbourDigital.asset";

        [Test]
        public void OnlyTheExistingDigitalWatchIsAuthored_AndItValidates()
        {
            var guids = AssetDatabase.FindAssets("t:WatchDef", new[] { DataRoot });
            Assert.That(guids, Has.Length.EqualTo(1), "New watch faces belong to later art slices.");
            Assert.That(AssetDatabase.GUIDToAssetPath(guids[0]), Is.EqualTo(DigitalPath));
            var def = AssetDatabase.LoadAssetAtPath<WatchDef>(DigitalPath);
            Assert.That(def, Is.Not.Null);
            Assert.That(def.TryValidate(out string error), Is.True, error);
            Assert.That(def.Id, Is.EqualTo("watch.harbour_digital"));
            Assert.That(def.DisplayName, Is.EqualTo("Harbour Digital Watch"));
            Assert.That(def.Face, Is.EqualTo(WatchFaceKind.HarbourDigital));
            Assert.That(def.Readouts, Is.EqualTo(WatchReadouts.Time | WatchReadouts.Weekday
                | WatchReadouts.Date | WatchReadouts.Season | WatchReadouts.Year
                | WatchReadouts.MarketDay | WatchReadouts.NightLight));
            Assert.That(def.Tier, Is.EqualTo(1));
            Assert.That(def.StartsOwned, Is.True, "Authored starting kit; runtime grant is a separate integration.");
            Assert.That(def.LegacyGearId, Is.EqualTo("gear.watch"));
            Assert.That(def.Price, Is.Zero);
            Assert.That(def.SellerId, Is.Null.Or.Empty, "The starting watch has no shop listing.");
        }

        [Test]
        public void DigitalWatchIsIncludedInTheRuntimeResourceCatalog()
        {
            var defs = Resources.LoadAll<WatchDef>("Watches");
            Assert.That(defs, Has.Length.EqualTo(1));
            Assert.That(defs[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<WatchDef>(DigitalPath)));
        }

        [Test]
        public void NewGameWearsTheAuthoredDigitalWatch_WithoutChangingTheSaveVersion()
        {
            SaveData save = SaveMigration.NewGame();

            AssertDigitalWatchIsWorn(save);
            Assert.That(save.OwnedGear, Is.EqualTo(new[] { "watch.harbour_digital" }));
            Assert.That(save.SchemaVersion, Is.EqualTo(SaveMigration.CurrentVersion));
        }

        [TestCase("[\"gear.rod\",\"gear.watch\"]")]
        [TestCase("[\"gear.rod\"]")]
        [TestCase("null")]
        public void PreWatchDefinitionSaveWearsTheAuthoredDigitalWatch_AndKeepsItsGear(string ownedGear)
        {
            // This is the existing v17 shape, before watches became data. No wearing field exists.
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":17,\"Money\":123,\"OwnedGear\":" + ownedGear + "}");
            Assert.That(save, Is.Not.Null);

            AssertDigitalWatchIsWorn(save);
            var expected = ownedGear == "null" ? new[] { "watch.harbour_digital" }
                : ownedGear.Contains("gear.watch") ? new[] { "gear.rod", "gear.watch", "watch.harbour_digital" }
                : new[] { "gear.rod", "watch.harbour_digital" };
            Assert.That(save.OwnedGear, Is.EqualTo(expected));
            Assert.That(save.SchemaVersion, Is.EqualTo(17));
            Assert.That(save.Money, Is.EqualTo(123));

            SaveData reloaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            AssertDigitalWatchIsWorn(reloaded);
            Assert.That(reloaded.OwnedGear, Is.EqualTo(expected), "A normal save round trip cannot double-grant.");
        }

        private static void AssertDigitalWatchIsWorn(SaveData save)
        {
            WatchDef worn = WatchEquipment.GetWorn(save);
            Assert.That(worn, Is.SameAs(AssetDatabase.LoadAssetAtPath<WatchDef>(DigitalPath)));
            Assert.That(worn.Id, Is.EqualTo("watch.harbour_digital"));
            Assert.That(worn.Face, Is.EqualTo(WatchFaceKind.HarbourDigital));
        }
    }
}
