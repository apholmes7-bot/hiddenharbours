using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the deck-container ladder (the ContentValidationTests pattern, over the
    /// ACTUAL Data/ assets): unique non-empty container ids, authored fill-state arrays with no holes
    /// and a sane order (≥ 2 states — a single sprite can't visibly change with contents, the owner's
    /// 'important'), and the ratified first slice actually wired — the playable small hulls (the dory
    /// and the fishing skiff) carry the fish tray with an authored deck anchor (a stale asset
    /// deserializes the offset as (0,0) = dead amidships; the anchor must be deliberate).
    /// </summary>
    public class DeckContainerContentValidationTests
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

        [Test]
        public void DeckContainers_Exist_AndHaveNonEmptyUniqueIds()
        {
            var defs = LoadAll<DeckContainerDef>();
            Assert.IsNotEmpty(defs, "the slice ships at least one DeckContainerDef (the fish tray)");

            var seen = new Dictionary<string, string>();
            foreach (var d in defs)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: empty id");
                Assert.IsFalse(seen.ContainsKey(d.Id), $"duplicate DeckContainerDef id '{d.Id}' in '{path}'");
                seen[d.Id] = path;
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.DisplayName), $"{path}: empty DisplayName");
            }
        }

        [Test]
        public void AuthoredFillStates_HaveNoHoles_AndAtLeastTwoStates()
        {
            // An EMPTY array is the deliberate greybox choice (code-built silhouettes). But once the
            // owner's painted states land, the set must be complete: no null holes (a hole would flash
            // greybox mid-ladder) and at least empty + brim (one sprite cannot visibly change).
            foreach (var d in LoadAll<DeckContainerDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                if (d.FillSprites == null || d.FillSprites.Length == 0) continue;   // greybox fallback — fine

                Assert.GreaterOrEqual(d.FillSprites.Length, 2,
                    $"{path}: one painted state can't read fullness — author at least empty + brim");
                for (int i = 0; i < d.FillSprites.Length; i++)
                    Assert.IsNotNull(d.FillSprites[i],
                        $"{path}: FillSprites[{i}] is a null hole — the tray would flash greybox mid-ladder");
            }
        }

        [Test]
        public void TheRatifiedSlice_SmallHullsCarryTheFishTray()
        {
            // Owner canon: small boats carry a TRAY. The committed playable hulls must be wired to the
            // committed container asset — the tray is the hold's only readout, so an unwired hull is a
            // boat whose catch is invisible.
            var mustCarry = new HashSet<string> { "boat.dory", "boat.fishing_skiff" };
            int found = 0;
            foreach (var h in LoadAll<BoatHullDef>())
            {
                if (!mustCarry.Contains(h.Id)) continue;
                found++;
                string path = AssetDatabase.GetAssetPath(h);
                Assert.IsNotNull(h.DeckContainer, $"{path}: a small hull with no deck container — the catch is invisible");
                Assert.AreEqual("container.fish_tray", h.DeckContainer.Id,
                    $"{path}: small hulls carry the fish tray (totes are M2 big-hull containers)");
                Assert.Greater(h.DeckContainerOffset.sqrMagnitude, 0f,
                    $"{path}: DeckContainerOffset is (0,0) — a stale asset serialized before the field " +
                    "existed reads dead-amidships; author the anchor explicitly");
                Assert.LessOrEqual(h.DeckContainerOffset.magnitude, h.LengthMeters,
                    $"{path}: the container anchor is off the boat (|offset| > hull length)");
            }
            Assert.AreEqual(mustCarry.Count, found, "both committed small hulls (dory + fishing skiff) must exist in Data/");
        }

        [Test]
        public void ContainerIds_DontCollideWithOtherDefTypes()
        {
            // Ids are globally unique across all Def types (the qa-test rule); containers are a new type,
            // so extend the guarantee here without editing qa-test's file.
            var otherIds = new HashSet<string>();
            foreach (var b in LoadAll<BoatHullDef>()) if (!string.IsNullOrWhiteSpace(b.Id)) otherIds.Add(b.Id);
            foreach (var d in LoadAll<DeckContainerDef>())
                Assert.IsFalse(otherIds.Contains(d.Id),
                    $"{AssetDatabase.GetAssetPath(d)}: container id '{d.Id}' collides with a BoatHullDef id");
        }
    }
}
