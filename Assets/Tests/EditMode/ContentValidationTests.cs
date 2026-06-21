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
    /// id and no missing (broken) object references. It runs over the ACTUAL assets in Data/ so it
    /// catches data errors as content grows — a new Punt BoatHullDef, a new ref field (e.g. a
    /// PropulsionType), a deleted sprite, a copy-pasted id. If tools-editor later adds an in-editor
    /// content validator, it should call THESE rules rather than re-deriving its own.
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

        /// <summary>Property paths of any assigned-but-unresolvable object reference on the asset.
        /// A broken ref reads as null value with a non-zero recorded instance id.</summary>
        private static List<string> MissingRefs(Object asset)
        {
            var bad = new List<string>();
            var so = new SerializedObject(asset);
            SerializedProperty p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                    bad.Add(p.propertyPath);
            }
            return bad;
        }

        /// <summary>Add an id to the seen-set, failing if it is empty or a duplicate.</summary>
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
        public void FishSpecies_HaveNoMissingReferences()
        {
            foreach (var f in LoadAll<FishSpeciesDef>())
            {
                var bad = MissingRefs(f);
                Assert.IsEmpty(bad,
                    $"FishSpeciesDef '{AssetDatabase.GetAssetPath(f)}' has missing refs: {string.Join(", ", bad)}");
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
        public void BoatHulls_HaveNoMissingReferences()
        {
            foreach (var b in LoadAll<BoatHullDef>())
            {
                var bad = MissingRefs(b);
                Assert.IsEmpty(bad,
                    $"BoatHullDef '{AssetDatabase.GetAssetPath(b)}' has missing refs: {string.Join(", ", bad)}");
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
