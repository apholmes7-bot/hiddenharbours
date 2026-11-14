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
    }
}
