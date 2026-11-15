using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Economy;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Home buying, slice 1: the for-sale houses as data. Pins what the owner counted (St Peters 1,
    /// Nine Mile Creek 3, Cumberland 2, Rice Point 1), that every home is priced and placed in a real
    /// region, that a lived-in house always says where its people go (the owner's ruling: residents
    /// stay in the game and move), and that the camper's meaning did not change when HomeDef grew.
    ///
    /// <para>Top-level EditMode folder, like <c>QuestContentValidationTests</c>: it loads real
    /// <c>Data/</c> assets across Economy and World.</para>
    /// </summary>
    public class HomeListingTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string IdPrefix = "home.";

        /// <summary>The owner's counts of resident houses for sale, per region. The camper is the
        /// separate cheap first rung and is not one of these.</summary>
        private static readonly Dictionary<string, int> ExpectedForSaleByRegion = new Dictionary<string, int>
        {
            { "region.st_peters", 1 },
            { "region.nine_mile_creek", 3 },
            { "region.cumberland", 2 },
            { "region.rice_point", 1 },
        };

        /// <summary>St Peters buildings that are plot-critical and must never be sold: the school,
        /// Junior and Basil's sage cottage, and the white farmhouse (the likely farm homestead).
        /// Ginny's cottage, the store and the post office are not StPetersVillage sites at all, so the
        /// site-key check below already keeps them out.</summary>
        private static readonly string[] ProtectedStPetersSites = { "school", "sageCottage", "whiteFarmhouse" };

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static List<HomeDef> Houses() =>
            LoadAll<HomeDef>().Where(h => h.Id != StPetersCamperLot.FallbackHomeId).ToList();

        [Test]
        public void EveryHome_HasAUniquePrefixedId()
        {
            var homes = LoadAll<HomeDef>();
            Assert.That(homes, Is.Not.Empty, "no HomeDef assets under " + DataRoot);
            foreach (var h in homes)
                Assert.That(h.Id, Does.StartWith(IdPrefix), $"{h.name}: home ids are \"home.snake_case\"");
            var dupes = homes.GroupBy(h => h.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(dupes, Is.Empty, "a deed is saved under the id, so two homes cannot share one");
        }

        [Test]
        public void EveryHome_HasAPositivePrice()
        {
            foreach (var h in LoadAll<HomeDef>())
                if (h.Acquisition == HomeDef.Mode.Purchasable)
                    Assert.Greater(h.Price, 0, $"{h.Id}: a purchasable home with no price is free");
        }

        [Test]
        public void EveryHouse_StandsInAKnownRegion_AndNamesItsBuilding()
        {
            var regions = new HashSet<string>(LoadAll<RegionDef>().Select(r => r.Id));
            Assert.That(regions, Is.Not.Empty, "no RegionDef assets found");
            foreach (var h in Houses())
            {
                Assert.That(regions, Does.Contain(h.RegionId), $"{h.Id}: unknown region \"{h.RegionId}\"");
                Assert.That(h.BuildingKey, Is.Not.Empty, $"{h.Id}: which building is this home?");
            }
        }

        [Test]
        public void ForSaleHouses_MatchTheOwnersCountsPerRegion()
        {
            var counts = Houses().Where(h => h.ListedForSale)
                                 .GroupBy(h => h.RegionId)
                                 .ToDictionary(g => g.Key, g => g.Count());
            CollectionAssert.AreEquivalent(ExpectedForSaleByRegion, counts,
                "the owner counted St Peters 1, Nine Mile Creek 3, Cumberland 2, Rice Point 1");
        }

        [Test]
        public void NoTwoHouses_AreTheSameBuilding()
        {
            var dupes = Houses().GroupBy(h => h.RegionId + "/" + h.BuildingKey)
                                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(dupes, Is.Empty);
        }

        [Test]
        public void AHouseWithResidents_SaysWhereTheyMove_AndTheyAreRealNpcs()
        {
            var npcs = new HashSet<string>(LoadAll<NpcDef>().Select(n => n.Id));
            foreach (var h in LoadAll<HomeDef>())
            {
                var residents = h.ResidentNpcIds ?? new string[0];
                if (residents.Length == 0)
                {
                    Assert.AreEqual(HomeDef.ResidentMoveMode.None, h.ResidentMove,
                        $"{h.Id}: nobody lives here, so nobody moves");
                    continue;
                }
                Assert.AreNotEqual(HomeDef.ResidentMoveMode.None, h.ResidentMove,
                    $"{h.Id}: residents stay in the game and move; say how");
                Assert.That(h.ResidentMovesTo, Is.Not.Null.And.Not.Empty,
                    $"{h.Id}: residents stay in the game and move; say where");
                foreach (string id in residents)
                    Assert.That(npcs, Does.Contain(id), $"{h.Id}: resident \"{id}\" is not an NpcDef");
            }
        }

        [Test]
        public void StPetersHouses_AreRealVillageSites_AndNeverAProtectedOne()
        {
            var sites = new HashSet<string>(StPetersVillage.Sites.Select(s => s.Key));
            foreach (var h in Houses().Where(h => h.RegionId == "region.st_peters"))
            {
                Assert.That(sites, Does.Contain(h.BuildingKey),
                    $"{h.Id}: \"{h.BuildingKey}\" is not a StPetersVillage site key");
                Assert.That(ProtectedStPetersSites, Does.Not.Contain(h.BuildingKey),
                    $"{h.Id}: \"{h.BuildingKey}\" is plot-critical and not for sale");
            }
        }

        [Test]
        public void TheRedSaltbox_IsRosesHouse_AndSheMovesOverThePostOffice()
        {
            var saltbox = LoadAll<HomeDef>().SingleOrDefault(h => h.Id == "home.st_peters_red_saltbox");
            Assert.IsNotNull(saltbox, "the owner's one St Peters house is missing");
            Assert.AreEqual("redSaltbox", saltbox.BuildingKey);
            CollectionAssert.AreEqual(new[] { "npc.rose_macisaac" }, saltbox.ResidentNpcIds);
            Assert.AreEqual(HomeDef.ResidentMoveMode.Rehouse, saltbox.ResidentMove,
                "Rose stays on the island");
            Assert.AreEqual("station.st_peters.home_post_office", saltbox.ResidentMovesTo);
        }

        [Test]
        public void TheCamper_IsUnchangedByTheNewFields()
        {
            HomeDef camper = StPetersCamperLot.Home;
            Assert.IsNotNull(camper, "no camper HomeDef at " + StPetersCamperLot.HomeDefPath);
            Assert.AreEqual("home.ginny_lot_camper", camper.Id);
            Assert.AreEqual(HomeDef.Mode.Purchasable, camper.Acquisition);
            Assert.AreEqual(900, camper.Price, "the camper's price is the owner's to tune; update with him");
            Assert.AreEqual("clipper", camper.Variant);
            Assert.That(camper.ResidentNpcIds ?? new string[0], Is.Empty, "nobody lives in the camper");
            Assert.AreEqual(HomeDef.ResidentMoveMode.None, camper.ResidentMove);
            Assert.That(camper.ResidentMovesTo ?? "", Is.Empty);
            Assert.IsTrue(camper.ListedForSale, "the camper stays for sale");
        }
    }
}
