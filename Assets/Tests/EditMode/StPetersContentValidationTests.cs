using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
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

        // ---- the St Peters CATCH REGION (this PR: pots draw local shellfish, not Coddle Cove's) ------

        /// <summary>Map every real FishSpeciesDef in Data/ by its stable id (the EditMode stand-in for the
        /// runtime <see cref="FishSpeciesRegistry"/>, which isn't populated outside Play).</summary>
        private static Dictionary<string, FishSpeciesDef> FishById()
        {
            var map = new Dictionary<string, FishSpeciesDef>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id) && !map.ContainsKey(f.Id)) map[f.Id] = f;
            return map;
        }

        [Test]
        public void LobsterAndCrab_AreCatchable_AtStPeters_AndStillAtCoddleCove()
        {
            // The trap species must be region-tagged for St Peters (the catch gate the CatchResolver filters
            // on) so a St-Peters pot draws them — WITHOUT dropping Coddle Cove (region ids are append-only).
            var byId = FishById();
            foreach (var id in new[] { "fish.lobster", "fish.rock_crab" })
            {
                Assert.IsTrue(byId.TryGetValue(id, out var f), $"{id} must exist in Data/Fish");
                Assert.IsTrue(f.RegionAllowed("region.st_peters"),
                    $"{id}: must be catchable at St Peters (RegionIds must include region.st_peters)");
                Assert.IsTrue(f.RegionAllowed("region.coddle_cove"),
                    $"{id}: must STILL be catchable at Coddle Cove — region tagging is additive, no regression");
            }
        }

        [Test]
        public void StPetersPot_YieldsLocalSpecies_AndTheRegionGateIsWhatMakesItFish()
        {
            // Prove end-to-end at the resolver: a pot SET at St Peters (region.st_peters — where DevTrapInput
            // tags this scene's pots) lands a St Peters local, and the SAME pool at a region the species don't
            // belong to comes up empty — so it's genuinely the region membership doing the work, not an
            // unfiltered pool. Determinism is untouched: this only exercises the pool the resolver reads.
            var byId = FishById();
            var trapsById = new Dictionary<string, TrapDef>();
            foreach (var t in LoadAll<TrapDef>())
                if (!string.IsNullOrWhiteSpace(t.Id)) trapsById[t.Id] = t;

            foreach (var trapId in new[] { "trap.lobster", "trap.crab" })
            {
                Assert.IsTrue(trapsById.TryGetValue(trapId, out var trap), $"{trapId} must exist in Data/Traps");

                // The pool the trap runtime feeds the resolver (AllowedCatchFishIds → real FishSpeciesDefs).
                var pool = new List<FishSpeciesDef>();
                foreach (var fid in trap.AllowedCatchFishIds)
                    if (byId.TryGetValue(fid, out var f)) pool.Add(f);
                Assert.IsNotEmpty(pool, $"{trapId}: its AllowedCatchFishIds must resolve to real fish");

                // All-year / all-day / mid-tide so only the REGION gate is in question here.
                var atStPeters = new CatchContext("region.st_peters", tideHeight: 1f, hourOfDay: 12f,
                                                  Season.HighSummer, Gear.Trap);
                var landed = PlacedTrapCatch.Resolve(pool, in atStPeters, baitFavours: null,
                                                     favourMultiplier: 1, new System.Random(12345));
                Assert.IsTrue(landed.HasValue,
                    $"{trapId}: a St-Peters-set pot must land a local catch, not come up empty");
                Assert.IsTrue(byId.TryGetValue(landed.Value.SpeciesId, out var caught)
                              && caught.RegionAllowed("region.st_peters"),
                    $"{trapId}: the landed species '{landed.Value.SpeciesId}' must be a St Peters local");

                var elsewhere = new CatchContext("region.nowhere", 1f, 12f, Season.HighSummer, Gear.Trap);
                Assert.IsNull(PlacedTrapCatch.Resolve(pool, in elsewhere, baitFavours: null,
                                                      favourMultiplier: 1, new System.Random(12345)),
                    $"{trapId}: the pot must catch NOTHING where its species aren't region-tagged (gate proof)");
            }
        }
    }
}
