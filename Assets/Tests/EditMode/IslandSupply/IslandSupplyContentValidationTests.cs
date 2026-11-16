using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.Tests.EditMode.IslandSupplyTestData;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the island supply Defs (slice S1). The assets were hand-authored as YAML, so every
    /// check reads what Unity LOADS: a bad indent falls silently to the field default and only the loaded
    /// object tells.
    /// </summary>
    public class IslandSupplyContentValidationTests
    {
        private static readonly Regex GoodId = new Regex("^islandgood\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex CarrierId = new Regex("^carrier\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex RoleId = new Regex("^supplyrole\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex BulkId = new Regex("^bulk\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static void CheckIds<T>(List<T> all, Regex shape, System.Func<T, string> id) where T : Object
        {
            Assert.That(all, Is.Not.Empty, typeof(T).Name + ": none shipped");
            var seen = new HashSet<string>();
            foreach (T def in all)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsTrue(shape.IsMatch(id(def) ?? ""), path + ": bad id " + id(def));
                Assert.IsTrue(seen.Add(id(def)), path + ": duplicate id " + id(def));
                StringAssert.StartsWith(SupplyRoot, path, "island supply content lives under Resources/IslandSupply");
            }
        }

        [Test]
        public void TheS1Content_Ships_WithWellFormedUniqueIds()
        {
            CheckIds(LoadAll<IslandGoodDef>(), GoodId, d => d.Id);
            CheckIds(LoadAll<CarrierDef>(), CarrierId, d => d.Id);
            CheckIds(LoadAll<SupplyRoleDef>(), RoleId, d => d.Id);
            CheckIds(LoadAll<BulkMaterialDef>(), BulkId, d => d.Id);

            var carrierIds = LoadAll<CarrierDef>().ConvertAll(c => c.Id);
            foreach (string id in new[] { "carrier.shopping_bag", "carrier.hand_pail", "carrier.hand_cart",
                                          "carrier.garden_wagon", "carrier.atv_front_rack", "carrier.atv_rear_rack",
                                          "carrier.atv_trailer", "carrier.pickup_bed", "carrier.tractor_forks",
                                          "carrier.tractor_bucket", "carrier.jerry_can_20l", "carrier.drum_205l" })
                CollectionAssert.Contains(carrierIds, id);
            Assert.That(LoadAll<IslandGoodDef>().Count, Is.EqualTo(9), "the nine goods of the plan's table (h)");
        }

        [Test]
        public void Goods_HaveRealFiguresAndNames()
        {
            foreach (IslandGoodDef g in LoadAll<IslandGoodDef>())
            {
                Assert.That(string.IsNullOrWhiteSpace(g.DisplayName), Is.False, g.Id);
                Assert.That(string.IsNullOrWhiteSpace(g.Unit), Is.False, g.Id + ": unit");
                Assert.That(g.DailyUnits, Is.GreaterThan(0f), g.Id);
                Assert.That(g.DailyMassKg, Is.GreaterThan(0f), g.Id);
                Assert.That(g.DailyVolumeLitres, Is.GreaterThan(0f), g.Id);
                Assert.That(g.WinterMultiplier, Is.GreaterThanOrEqualTo(1f), g.Id);
                Assert.That(string.IsNullOrWhiteSpace(g.Basis), Is.False, g.Id + ": say where the numbers came from");
            }
        }

        [Test]
        public void Carriers_HaveBothLimits_AndAPracticalLoadNoMoreThanTheRating()
        {
            foreach (CarrierDef c in LoadAll<CarrierDef>())
            {
                Assert.That(c.CapacityKg, Is.GreaterThan(0f), c.Id + ": a mass limit");
                Assert.That(c.CapacityLitres, Is.GreaterThan(0f), c.Id + ": a volume limit");
                Assert.That(c.RatedKg, Is.GreaterThanOrEqualTo(c.CapacityKg), c.Id + ": practical load above the rating");
                Assert.That(string.IsNullOrWhiteSpace(c.Basis), Is.False, c.Id);
            }
        }

        [Test]
        public void Roles_NameRealCarriersAndRealPeople()
        {
            var carriers = CarriersById();
            var npcIds = new HashSet<string>(LoadAll<NpcDef>().ConvertAll(n => n.Id));
            foreach (SupplyRoleDef r in LoadAll<SupplyRoleDef>())
            {
                Assert.That(r.CarrierIds, Is.Not.Empty, r.Id);
                foreach (string id in r.CarrierIds)
                    Assert.IsTrue(carriers.ContainsKey(id), r.Id + ": unknown carrier " + id);
                foreach (string npc in r.CrewNpcIds)
                    Assert.IsTrue(npcIds.Contains(npc), r.Id + ": unknown NPC " + npc);
                if (r.Mode != SupplyTripMode.MainlandOnly)
                {
                    Assert.That(r.CrewNpcIds.Count, Is.EqualTo(r.People), r.Id + ": one NPC per person crossing");
                    Assert.That(r.CadenceDays, Is.GreaterThan(0), r.Id + ": a bar run needs a cadence");
                    Assert.That(r.MaxWadeDepthMetres, Is.GreaterThan(0f), r.Id);
                }
            }
        }

        [Test]
        public void TheRouteConfig_LoadsFromResources_AndPointsAtARealRegion()
        {
            IslandSupplyConfig route = Route();
            RegionDef region = Region(route.IslandRegionId);
            Assert.That(region, Is.Not.Null);
            Assert.That(route.RunDayEndHour, Is.GreaterThan(route.RunDayStartHour));
            Assert.That(route.GutCentreFromIslandMetres, Is.LessThan(route.BarLengthMetres));
        }

        /// <summary>The config's bar must be the bar the builder lays. The crest and gut bed are the same numbers
        /// as <c>StPetersBuilder.cs:458-461</c>; the gut sits <c>ChannelAlong</c> of the way from the island end
        /// of the sandbar (<c>StPetersBuilder.cs:439-440</c>), and is <c>2 x ChannelHalfWidth</c> wide.</summary>
        [Test]
        public void TheRouteBar_MatchesTheTerrainTheBuilderLays()
        {
            IslandSupplyConfig route = Route();
            Assert.That(route.BarCrestElevationMetres, Is.EqualTo(StPetersBuilder.SandbarCrestElevation).Within(1e-5f));
            Assert.That(route.GutBedElevationMetres, Is.EqualTo(StPetersBuilder.ChannelBedElevation).Within(1e-5f));
            float sandbar = Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
            Assert.That(route.GutCentreFromIslandMetres, Is.EqualTo(StPetersBuilder.ChannelAlong * sandbar).Within(1f));
            Assert.That(route.GutWidthMetres, Is.EqualTo(2f * StPetersBuilder.ChannelHalfWidth).Within(1e-3f));
        }

        /// <summary>The jerry can and drum carriers are the fuel containers already on main, by the numbers.</summary>
        [Test]
        public void FuelCarriers_MatchTheFuelContainerDefs()
        {
            var carriers = CarriersById();
            var fuel = LoadAll<FuelContainerDef>();
            void Same(string carrierId, string fuelId)
            {
                CarrierDef c = carriers[carrierId];
                FuelContainerDef f = ById(fuel, d => d.Id, fuelId);
                Assert.That(c.CapacityLitres, Is.EqualTo(f.CapacityLitres).Within(1e-3f), carrierId + " litres");
                Assert.That(c.TareKg, Is.EqualTo(f.TareKg).Within(1e-3f), carrierId + " tare");
                Assert.That(c.CapacityKg + c.TareKg, Is.EqualTo(f.FullKg).Within(1e-3f), carrierId + " full");
            }
            Same("carrier.jerry_can_20l", "fuelstore.gas_jerry_s20");
            Same("carrier.drum_205l", "fuelstore.diesel_drum_s205");
        }
    }
}
