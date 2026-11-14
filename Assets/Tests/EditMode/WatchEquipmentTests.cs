using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WatchEquipmentTests
    {
        private readonly List<WatchDef> _defs = new List<WatchDef>();

        [TearDown]
        public void TearDown()
        {
            foreach (WatchDef def in _defs) Object.DestroyImmediate(def);
            _defs.Clear();
        }

        private WatchDef Watch(string id, int tier, bool startsOwned = false, string legacyId = null)
        {
            var def = ScriptableObject.CreateInstance<WatchDef>();
            def.Id = id;
            def.Tier = tier;
            def.StartsOwned = startsOwned;
            def.LegacyGearId = legacyId;
            _defs.Add(def);
            return def;
        }

        [Test]
        public void HighestOwnedTierWins_RegardlessOfCatalogOrAcquisitionOrder()
        {
            WatchDef low = Watch("watch.low_fixture", 1, startsOwned: true);
            WatchDef high = Watch("watch.high_fixture", 3);
            WatchDef unowned = Watch("watch.unowned_fixture", 9);
            var save = new SaveData { OwnedGear = new List<string> { high.Id, low.Id, "gear.rod" } };

            Assert.That(WatchEquipment.GetWorn(save, new[] { low, high, unowned }), Is.SameAs(high));
            save.OwnedGear.Reverse();
            Assert.That(WatchEquipment.GetWorn(save, new[] { unowned, high, low }), Is.SameAs(high));
            Assert.That(save.OwnedGear, Is.EquivalentTo(new[] { high.Id, low.Id, "gear.rod" }));
        }

        [Test]
        public void EqualTiersUseOrdinalIdOrder_RegardlessOfCatalogOrAcquisitionOrder()
        {
            WatchDef first = Watch("watch.a_fixture", 2);
            WatchDef last = Watch("watch.z_fixture", 2);
            var save = new SaveData { OwnedGear = new List<string> { last.Id, first.Id } };

            Assert.That(WatchEquipment.GetWorn(save, new[] { last, first }), Is.SameAs(first));
            save.OwnedGear.Reverse();
            Assert.That(WatchEquipment.GetWorn(save, new[] { first, last }), Is.SameAs(first));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAliasCountsAsOwnership_WithoutRewritingTheWallet(bool useAlias)
        {
            WatchDef def = Watch("watch.alias_fixture", 1, legacyId: "gear.old_watch_fixture");
            string ownedId = useAlias ? def.LegacyGearId : def.Id;
            var save = new SaveData { OwnedGear = new List<string> { ownedId, "gear.rod" } };

            Assert.That(WatchEquipment.SelectOwned(save, new[] { def }), Is.SameAs(def));
            Assert.That(save.OwnedGear, Is.EqualTo(new[] { ownedId, "gear.rod" }));
        }

        [Test]
        public void StartingGrantIsAdditiveAndIdempotent_AndDoesNotGrantPurchasedWatches()
        {
            WatchDef starter = Watch("watch.starting_fixture", 1, startsOwned: true);
            WatchDef purchase = Watch("watch.purchased_fixture", 3);
            var save = new SaveData { OwnedGear = new List<string> { "gear.shovel", "gear.rod" } };
            var catalog = new[] { purchase, starter };

            Assert.That(WatchEquipment.GetWorn(save, catalog), Is.SameAs(starter));
            Assert.That(WatchEquipment.GetWorn(save, catalog), Is.SameAs(starter));
            Assert.That(save.OwnedGear, Is.EqualTo(new[] { "gear.shovel", "gear.rod", starter.Id }));
        }

        [Test]
        public void MissingOwnedGearListCanReceiveTheStartingWatch()
        {
            WatchDef starter = Watch("watch.starting_fixture", 1, startsOwned: true);
            var save = new SaveData { OwnedGear = null };

            Assert.That(WatchEquipment.GetWorn(save, new[] { starter }), Is.SameAs(starter));
            Assert.That(save.OwnedGear, Is.EqualTo(new[] { starter.Id }));
        }

        [Test]
        public void NothingOwnedAndNoStartingWatchMeansNothingWorn()
        {
            WatchDef purchase = Watch("watch.purchased_fixture", 1);
            var save = new SaveData { OwnedGear = new List<string> { "gear.rod" } };

            Assert.That(WatchEquipment.GetWorn(save, new[] { purchase }), Is.Null);
            Assert.That(save.OwnedGear, Is.EqualTo(new[] { "gear.rod" }));
        }

        [Test]
        public void MissingSaveOrCatalogDoesNotGrantOrChooseAWatch()
        {
            WatchDef starter = Watch("watch.starting_fixture", 1, startsOwned: true);
            var save = new SaveData();

            Assert.That(WatchEquipment.GetWorn(null), Is.Null);
            Assert.That(WatchEquipment.GetWorn(null, new[] { starter }), Is.Null);
            Assert.That(WatchEquipment.GetWorn(save, null), Is.Null);
            Assert.That(WatchEquipment.SelectOwned(save, null), Is.Null);
            Assert.That(save.OwnedGear, Is.Empty);
        }
    }
}
