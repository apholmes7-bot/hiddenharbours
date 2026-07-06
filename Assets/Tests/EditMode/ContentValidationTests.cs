using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-30 — content validation. The single source of truth for "is the Data/ content well-formed":
    /// every <see cref="FishSpeciesDef"/>, <see cref="BoatHullDef"/>, <see cref="RegionDef"/>,
    /// <see cref="TrapDef"/>, and <see cref="BaitDef"/> must have a non-empty, unique id and references
    /// that actually resolve (a fish must be reachable by some region/gear/season AND its region ids must
    /// name a real region; a boat must have a name and a hold; a region must have a name + a scene to load;
    /// a trap's required bait must name a real BaitDef and its allowed catches must name real fish). It
    /// runs over the ACTUAL assets in Data/, so it catches data errors as content grows — a new Punt
    /// BoatHullDef, a copy-pasted id, a fish gated to a region that doesn't exist, an inverted weight range,
    /// a trap baited with a bait that doesn't exist. If tools-editor later adds an in-editor content
    /// validator, it should call THESE rules rather than re-deriving its own.
    /// </summary>
    public class ContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        // ---- reusable rules (the single source of truth) ------------------------------------

        /// <summary>Load every asset of type T under Data/.</summary>
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

        /// <summary>Add an id to the seen-set, failing if it is empty/blank or a duplicate.</summary>
        private static void RegisterUniqueId(Dictionary<string, string> seen, string id, string path, string kind)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{kind} '{path}' has an empty id");
            Assert.IsFalse(seen.ContainsKey(id),
                $"duplicate {kind} id '{id}' in '{path}' and '{(seen.TryGetValue(id, out var other) ? other : "?")}'");
            seen[id] = path;
        }

        // ---- fish ---------------------------------------------------------------------------

        [Test]
        public void FishSpecies_Exist_AndHaveNonEmptyUniqueIds()
        {
            var fish = LoadAll<FishSpeciesDef>();
            Assert.IsNotEmpty(fish, "the slice must ship at least one FishSpeciesDef in Data/");

            var seen = new Dictionary<string, string>();
            foreach (var f in fish)
                RegisterUniqueId(seen, f.Id, AssetDatabase.GetAssetPath(f), nameof(FishSpeciesDef));
        }

        [Test]
        public void FishSpecies_AreReachable_AndPricedSanely()
        {
            foreach (var f in LoadAll<FishSpeciesDef>())
            {
                string path = AssetDatabase.GetAssetPath(f);

                // Region references must resolve to at least one non-blank region — a fish gated to no
                // region (or a blank one) can never be caught.
                Assert.IsNotNull(f.RegionIds, $"{path}: RegionIds is null");
                Assert.IsNotEmpty(f.RegionIds, $"{path}: a fish with no region can never be caught");
                foreach (var r in f.RegionIds)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(r), $"{path}: has a blank region id");

                // Reachable by some gear and some season (an empty mask = catchable by nothing).
                Assert.AreNotEqual(0, (int)f.AllowedGear, $"{path}: AllowedGear is empty — no gear can land it");
                Assert.AreNotEqual(0, (int)f.Seasons, $"{path}: Seasons is empty — it bites in no season");

                // Sane catch + price.
                Assert.LessOrEqual(f.MinWeightKg, f.MaxWeightKg, $"{path}: MinWeightKg exceeds MaxWeightKg");
                Assert.Greater(f.MaxWeightKg, 0f, $"{path}: MaxWeightKg must be positive");
                Assert.GreaterOrEqual(f.BaseValue, 0, $"{path}: BaseValue must be non-negative");
            }
        }

        // ---- bait (trap arc Build 2) --------------------------------------------------------

        [Test]
        public void Bait_HaveNonEmptyUniqueIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (var b in LoadAll<BaitDef>())
                RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), nameof(BaitDef));
        }

        [Test]
        public void BaitFavorsSpeciesIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var b in LoadAll<BaitDef>())
            {
                string path = AssetDatabase.GetAssetPath(b);
                if (b.FavorsSpeciesIds == null) continue;
                foreach (var id in b.FavorsSpeciesIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank favored-species id");
                    Assert.IsTrue(fishIds.Contains(id),
                        $"{path}: favored-species id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- traps (trap arc Build 2) -------------------------------------------------------

        [Test]
        public void Traps_HaveNonEmptyUniqueIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (var t in LoadAll<TrapDef>())
                RegisterUniqueId(seen, t.Id, AssetDatabase.GetAssetPath(t), nameof(TrapDef));
        }

        [Test]
        public void TrapRequiredBaitIds_ResolveToRealBait()
        {
            var baitIds = new HashSet<string>();
            foreach (var b in LoadAll<BaitDef>())
                if (!string.IsNullOrWhiteSpace(b.Id)) baitIds.Add(b.Id);

            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                // A trap must be baited with a real bait — an unresolvable RequiredBaitId means the trap
                // can never be set (Build 3's resolver keys the catch weighting off the loaded bait).
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.RequiredBaitId),
                    $"{path}: RequiredBaitId is empty — the trap names no bait");
                Assert.IsTrue(baitIds.Contains(t.RequiredBaitId),
                    $"{path}: RequiredBaitId '{t.RequiredBaitId}' resolves to no BaitDef");
            }
        }

        [Test]
        public void TrapAllowedCatchFishIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                // A trap with no catchable species can never yield anything.
                Assert.IsNotNull(t.AllowedCatchFishIds, $"{path}: AllowedCatchFishIds is null");
                Assert.IsNotEmpty(t.AllowedCatchFishIds, $"{path}: a trap that can catch nothing is invalid");
                foreach (var id in t.AllowedCatchFishIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank allowed-catch fish id");
                    Assert.IsTrue(fishIds.Contains(id),
                        $"{path}: allowed-catch fish id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- boats --------------------------------------------------------------------------

        [Test]
        public void BoatHulls_Exist_AndHaveNonEmptyUniqueIds()
        {
            var boats = LoadAll<BoatHullDef>();
            Assert.IsNotEmpty(boats, "the slice must ship at least one BoatHullDef in Data/ (the Dory)");

            var seen = new Dictionary<string, string>();
            foreach (var b in boats)
                RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), nameof(BoatHullDef));
        }

        [Test]
        public void BoatHulls_HaveNameAndHold()
        {
            foreach (var b in LoadAll<BoatHullDef>())
            {
                string path = AssetDatabase.GetAssetPath(b);
                Assert.IsFalse(string.IsNullOrWhiteSpace(b.DisplayName), $"{path}: empty DisplayName");
                // Every hull on the Dory→Dynasty ladder hauls catch; a hold of zero breaks the loop.
                Assert.GreaterOrEqual(b.HoldUnits, 1, $"{path}: HoldUnits must be at least 1");
            }
        }

        // ---- regions (VS-22+) ---------------------------------------------------------------

        [Test]
        public void Regions_Exist_AndHaveNonEmptyUniqueIds()
        {
            var regions = LoadAll<RegionDef>();
            Assert.IsNotEmpty(regions, "the slice must ship at least one RegionDef in Data/Regions (the cove + Greywick)");

            var seen = new Dictionary<string, string>();
            foreach (var r in regions)
                RegisterUniqueId(seen, r.Id, AssetDatabase.GetAssetPath(r), nameof(RegionDef));
        }

        [Test]
        public void Regions_HaveNameAndAScene()
        {
            foreach (var r in LoadAll<RegionDef>())
            {
                string path = AssetDatabase.GetAssetPath(r);
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.DisplayName), $"{path}: empty DisplayName");
                // "Scene per region, loaded additively" (CLAUDE.md §3) — a region with no scene can't load.
                Assert.IsTrue(r.HasScene, $"{path}: no SceneName — the region can never be loaded");
                // Tide envelope is physical: amplitude can't be negative.
                Assert.GreaterOrEqual(r.TideAmplitude, 0f, $"{path}: negative TideAmplitude");
            }
        }

        [Test]
        public void RegionSpawnFishIds_ResolveToRealFish()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var r in LoadAll<RegionDef>())
            {
                string path = AssetDatabase.GetAssetPath(r);
                if (r.SpawnFishIds == null) continue;
                foreach (var id in r.SpawnFishIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"{path}: a blank spawn-fish id");
                    Assert.IsTrue(fishIds.Contains(id), $"{path}: spawn-fish id '{id}' resolves to no FishSpeciesDef");
                }
            }
        }

        // ---- cross-type ---------------------------------------------------------------------

        [Test]
        public void FishRegionIds_ResolveToRealRegions()
        {
            // Now that regions are authored as data, a fish's region ids must name an ACTUAL RegionDef —
            // not just be non-blank strings (which FishSpecies_AreReachable already checks). A fish gated
            // to a region that doesn't exist can never be caught.
            var regionIds = new HashSet<string>();
            foreach (var r in LoadAll<RegionDef>())
                if (!string.IsNullOrWhiteSpace(r.Id)) regionIds.Add(r.Id);
            Assert.IsNotEmpty(regionIds, "there must be at least one RegionDef for fish to reference");

            foreach (var f in LoadAll<FishSpeciesDef>())
            {
                string path = AssetDatabase.GetAssetPath(f);
                if (f.RegionIds == null) continue;
                foreach (var rid in f.RegionIds)
                    if (!string.IsNullOrWhiteSpace(rid))
                        Assert.IsTrue(regionIds.Contains(rid),
                            $"{path}: region id '{rid}' resolves to no RegionDef — the fish is gated to a region that doesn't exist");
            }
        }

        [Test]
        public void DefIds_AreGloballyUnique_AcrossAllDefTypes()
        {
            // Ids are append-only & stable and namespaced by type (fish.* / boat.* / region.* / trap.* /
            // bait.*), so they must not collide across the whole content set.
            var seen = new Dictionary<string, string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id))
                    RegisterUniqueId(seen, f.Id, AssetDatabase.GetAssetPath(f), "Def");
            foreach (var b in LoadAll<BoatHullDef>())
                if (!string.IsNullOrWhiteSpace(b.Id))
                    RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), "Def");
            foreach (var r in LoadAll<RegionDef>())
                if (!string.IsNullOrWhiteSpace(r.Id))
                    RegisterUniqueId(seen, r.Id, AssetDatabase.GetAssetPath(r), "Def");
            foreach (var t in LoadAll<TrapDef>())
                if (!string.IsNullOrWhiteSpace(t.Id))
                    RegisterUniqueId(seen, t.Id, AssetDatabase.GetAssetPath(t), "Def");
            foreach (var bait in LoadAll<BaitDef>())
                if (!string.IsNullOrWhiteSpace(bait.Id))
                    RegisterUniqueId(seen, bait.Id, AssetDatabase.GetAssetPath(bait), "Def");
        }
    }
}
