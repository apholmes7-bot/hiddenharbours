using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-30 — content validation. The single source of truth for "is the Data/ content well-formed":
    /// every <see cref="FishSpeciesDef"/> and <see cref="BoatHullDef"/> must have a non-empty, unique
    /// id and references that actually resolve (a fish must be reachable by some region/gear/season, a
    /// boat must have a name and a hold). It runs over the ACTUAL assets in Data/, so it catches data
    /// errors as content grows — a new Punt BoatHullDef, a copy-pasted id, a fish gated so tightly it
    /// can never bite, an inverted weight range. If tools-editor later adds an in-editor content
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

        // ---- cross-type ---------------------------------------------------------------------

        [Test]
        public void DefIds_AreGloballyUnique_AcrossFishAndBoats()
        {
            // Ids are append-only & stable and namespaced by type (fish.* / boat.*), so they must not
            // collide across the whole content set.
            var seen = new Dictionary<string, string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id))
                    RegisterUniqueId(seen, f.Id, AssetDatabase.GetAssetPath(f), "Def");
            foreach (var b in LoadAll<BoatHullDef>())
                if (!string.IsNullOrWhiteSpace(b.Id))
                    RegisterUniqueId(seen, b.Id, AssetDatabase.GetAssetPath(b), "Def");
        }
    }
}
