using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters opening — content validation for the NEW economy data (licences, gear offers, the clam,
    /// the damaged-dory offer). Mirrors <see cref="ContentValidationTests"/>'s rules over the real assets
    /// in Data/: unique non-empty ids, resolvable cross-references, sane prices. Catches a copy-pasted id,
    /// a licence that permits a species that doesn't exist, or a damaged offer with no repair cost — as
    /// the St Peters content grows.
    /// </summary>
    public class StPetersContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // ---- licences -------------------------------------------------------------------------

        [Test]
        public void Licenses_Exist_AndHaveNonEmptyUniqueIds()
        {
            var licenses = LoadAll<LicenseDef>();
            Assert.IsNotEmpty(licenses, "the St Peters opening must ship at least the cod licence");

            var seen = new Dictionary<string, string>();
            foreach (var l in licenses)
            {
                string path = AssetDatabase.GetAssetPath(l);
                Assert.IsFalse(string.IsNullOrWhiteSpace(l.Id), $"{path}: LicenseDef has an empty id");
                Assert.IsFalse(seen.ContainsKey(l.Id), $"duplicate LicenseDef id '{l.Id}' in '{path}' and '{(seen.TryGetValue(l.Id, out var o) ? o : "?")}'");
                seen[l.Id] = path;
                Assert.GreaterOrEqual(l.Price, 0, $"{path}: licence fee must be non-negative");
            }
        }

        [Test]
        public void CodLicense_Exists_AndPermitsCod()
        {
            LicenseDef cod = null;
            foreach (var l in LoadAll<LicenseDef>())
                if (l.Id == "license.cod") cod = l;

            Assert.IsNotNull(cod, "the cod licence (license.cod) must exist for the St Peters opening");
            CollectionAssert.Contains(cod.PermittedSpeciesIds, "fish.atlantic_cod",
                "the cod licence must permit Atlantic cod (the rod-fishes-cod gate)");
        }

        [Test]
        public void LicensePermittedSpecies_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var l in LoadAll<LicenseDef>())
            {
                string path = AssetDatabase.GetAssetPath(l);
                if (l.PermittedSpeciesIds == null) continue;
                foreach (var sid in l.PermittedSpeciesIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(sid), $"{path}: a blank permitted-species id");
                    Assert.IsTrue(fishIds.Contains(sid),
                        $"{path}: permitted species '{sid}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- gear offers ----------------------------------------------------------------------

        [Test]
        public void GearOffers_Exist_AndHaveNonEmptyUniqueIds_AndSanePrices()
        {
            var gear = LoadAll<GearOffer>();
            Assert.IsNotEmpty(gear, "the St Peters opening ships the rod/shovel/bucket gear offers");

            var seen = new Dictionary<string, string>();
            foreach (var g in gear)
            {
                string path = AssetDatabase.GetAssetPath(g);
                Assert.IsFalse(string.IsNullOrWhiteSpace(g.Id), $"{path}: GearOffer has an empty id");
                Assert.IsFalse(seen.ContainsKey(g.Id), $"duplicate GearOffer id '{g.Id}' in '{path}' and '{(seen.TryGetValue(g.Id, out var o) ? o : "?")}'");
                seen[g.Id] = path;
                Assert.GreaterOrEqual(g.Price, 0, $"{path}: gear price must be non-negative");
            }
        }

        [Test]
        public void RodShovelBucket_Exist()
        {
            var ids = new HashSet<string>();
            foreach (var g in LoadAll<GearOffer>()) if (!string.IsNullOrWhiteSpace(g.Id)) ids.Add(g.Id);

            Assert.IsTrue(ids.Contains("gear.rod"), "the rod (gear.rod) must exist");
            Assert.IsTrue(ids.Contains("gear.shovel"), "the shovel (gear.shovel) must exist");
            Assert.IsTrue(ids.Contains("gear.bucket"), "the clam bucket (gear.bucket) must exist");
        }

        // ---- the clam -------------------------------------------------------------------------

        [Test]
        public void SoftShellClam_Exists_AsShellfish_PricedSanely()
        {
            FishSpeciesDef clam = null;
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (f.Id == "fish.soft_shell_clam") clam = f;

            Assert.IsNotNull(clam, "the soft-shell clam (fish.soft_shell_clam) must exist for the dig-and-sell loop");
            Assert.AreEqual(HiddenHarbours.Core.FishCategory.Shellfish, clam.Category,
                "the clam is a Shellfish (drives its market category)");
            Assert.AreNotEqual(0, (int)clam.AllowedGear, "the clam must be catchable by some gear (hand-dig)");
            Assert.Greater(clam.BaseValue, 0, "the clam must have a positive base value to sell");
            Assert.LessOrEqual(clam.MinWeightKg, clam.MaxWeightKg, "clam weight range must be valid");
        }

        // ---- the damaged dory offer -----------------------------------------------------------

        [Test]
        public void DamagedDoryOffer_Exists_IsDamaged_WithPositiveRepairCost()
        {
            ShipwrightOffer damaged = null;
            foreach (var o in LoadAll<ShipwrightOffer>())
                if (o.StartsDamaged) damaged = o;

            Assert.IsNotNull(damaged, "the St Peters opening ships a damaged boat offer to buy + repair");
            Assert.AreEqual("boat.dory", damaged.BoatId, "the damaged St Peters boat is the dory");
            Assert.Greater(damaged.RepairCost, 0, "a damaged offer must cost something to repair");
            Assert.GreaterOrEqual(damaged.Price, 0, "the buy price must be non-negative");
        }
    }
}
