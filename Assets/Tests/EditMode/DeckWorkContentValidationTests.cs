using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the Build-7 <see cref="DeckWorkDef"/> assets (the ContentValidationTests
    /// pattern, over the ACTUAL Data/ content): unique non-empty ids, species rules that resolve to real
    /// fish with sane size windows, and — the loop-integrity rule — every shipped <see cref="TrapDef"/>
    /// that opts into deck work must have a rule for EVERY species it can land (an unruled species is an
    /// always-keeper by code convention, which is fine for tests but a data hole in shipped content).
    /// </summary>
    public class DeckWorkContentValidationTests
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
        public void DeckWorkDefs_HaveNonEmptyUniqueIds()
        {
            var defs = LoadAll<DeckWorkDef>();
            Assert.IsNotEmpty(defs, "Build 7 ships at least one DeckWorkDef (the pot ruleset)");

            var seen = new Dictionary<string, string>();
            foreach (var d in defs)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: empty id");
                Assert.IsFalse(seen.ContainsKey(d.Id), $"duplicate DeckWorkDef id '{d.Id}' in '{path}'");
                seen[d.Id] = path;
            }
        }

        [Test]
        public void DeckWorkSpeciesRules_ResolveToRealFish_WithSaneWindows()
        {
            var fishIds = new HashSet<string>();
            foreach (var f in LoadAll<FishSpeciesDef>())
                if (!string.IsNullOrWhiteSpace(f.Id)) fishIds.Add(f.Id);

            foreach (var d in LoadAll<DeckWorkDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsNotNull(d.SpeciesRules, $"{path}: SpeciesRules is null");
                Assert.IsNotEmpty(d.SpeciesRules, $"{path}: a deck ruleset with no species rules sorts nothing");

                var ruleIds = new HashSet<string>();
                foreach (var rule in d.SpeciesRules)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(rule.SpeciesId), $"{path}: a blank species id");
                    Assert.IsTrue(fishIds.Contains(rule.SpeciesId),
                        $"{path}: species id '{rule.SpeciesId}' resolves to no FishSpeciesDef");
                    Assert.IsTrue(ruleIds.Add(rule.SpeciesId),
                        $"{path}: duplicate rule for species '{rule.SpeciesId}'");

                    // A sane, honest window: sizes ordered, gauge inside the window so both shorts and
                    // keepers actually occur (a gauge outside the window makes the sort a foregone
                    // conclusion — legal, but almost certainly a data mistake).
                    Assert.Less(rule.SizeMinMm, rule.SizeMaxMm, $"{path}/{rule.SpeciesId}: size window inverted");
                    Assert.Greater(rule.SizeMinMm, 0f, $"{path}/{rule.SpeciesId}: sizes are millimetres, > 0");
                    Assert.Greater(rule.MinKeepSizeMm, rule.SizeMinMm,
                        $"{path}/{rule.SpeciesId}: the gauge is at/under the window floor — nothing would ever be short");
                    Assert.Less(rule.MinKeepSizeMm, rule.SizeMaxMm,
                        $"{path}/{rule.SpeciesId}: the gauge is at/over the window ceiling — nothing would ever keep");

                    if (rule.CanBeBerried)
                        Assert.Greater(rule.BerriedChance01, 0f,
                            $"{path}/{rule.SpeciesId}: berried allowed but chance 0 — pick one");
                    else
                        Assert.AreEqual(0f, rule.BerriedChance01,
                            $"{path}/{rule.SpeciesId}: berried chance set but CanBeBerried is off");
                }
            }
        }

        [Test]
        public void DeckWorkFeelNumbers_AreSane()
        {
            foreach (var d in LoadAll<DeckWorkDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.Greater(d.QuickGrabSeconds, 0f, $"{path}: QuickGrabSeconds must be positive");
                Assert.Greater(d.FullGrabSeconds, d.QuickGrabSeconds,
                    $"{path}: the care window needs FullGrabSeconds > QuickGrabSeconds");
                Assert.GreaterOrEqual(d.NipChanceRushed01, d.NipChanceCareful01,
                    $"{path}: care must never make a grab RISKIER (rushed ≥ careful)");
                Assert.Greater(d.BandSeconds, 0f, $"{path}: BandSeconds must be positive");
                Assert.Greater(d.BaitSeconds, 0f, $"{path}: BaitSeconds must be positive");
                Assert.Greater(d.WorkReachMeters, 0f, $"{path}: WorkReachMeters must be positive");
            }
        }

        [Test]
        public void EveryDeckWorkingTrap_HasARule_ForEverySpeciesItCanLand()
        {
            foreach (var t in LoadAll<TrapDef>())
            {
                if (t.DeckWork == null) continue;   // legacy instant-land traps opt out — fine
                string path = AssetDatabase.GetAssetPath(t);
                Assert.IsNotNull(t.AllowedCatchFishIds, $"{path}: AllowedCatchFishIds is null");
                foreach (var speciesId in t.AllowedCatchFishIds)
                {
                    Assert.IsTrue(t.DeckWork.TryGetRule(speciesId, out _),
                        $"{path}: deck-working trap can land '{speciesId}' but its DeckWorkDef " +
                        $"'{t.DeckWork.Id}' has no sort rule for it (it would be an unsortable always-keeper)");
                }
            }
        }

        [Test]
        public void TheShippedPots_OptIntoDeckWork()
        {
            // The owner's ask: the trap loop's pots work the deck. Both shipped pots must reference a
            // ruleset — a regression here silently reverts the loop to instant-land.
            foreach (var t in LoadAll<TrapDef>())
            {
                string path = AssetDatabase.GetAssetPath(t);
                Assert.IsNotNull(t.DeckWork, $"{path}: shipped pot must opt into the Build-7 deck work");
            }
        }
    }
}
