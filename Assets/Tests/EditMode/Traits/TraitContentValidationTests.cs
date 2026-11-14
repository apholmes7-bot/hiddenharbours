using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for <see cref="TraitDef"/> (traits slice 1). The three slice-1 assets were
    /// hand-authored as YAML, so every check reads what Unity LOADS, never the file text: a blank line or
    /// a bad indent fails silently to the field default, and only the loaded object tells.
    /// </summary>
    public class TraitContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private static readonly Regex IdShape = new Regex("^trait\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<TraitDef> LoadAll()
        {
            var list = new List<TraitDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TraitDef), new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<TraitDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        [Test]
        public void Traits_HaveUniqueWellFormedIds_NamesAndDescriptions()
        {
            List<TraitDef> all = LoadAll();
            Assert.IsNotEmpty(all, "slice 1 ships three TraitDefs under Data/Resources/Traits");
            var seen = new HashSet<string>();
            foreach (TraitDef def in all)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsTrue(IdShape.IsMatch(def.Id ?? ""), path + ": id must be trait.snake_case, got " + def.Id);
                Assert.IsTrue(seen.Add(def.Id), path + ": duplicate id " + def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), path + ": no display name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Description), path + ": no description");
                StringAssert.StartsWith("Assets/_Project/Data/Resources/" + TraitService.ResourcesFolder + "/", path,
                    "the service loads traits from Resources/" + TraitService.ResourcesFolder + " only");
            }
        }

        [Test]
        public void EveryModifier_IsABonus_NeverADrawback()
        {
            foreach (TraitDef def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsNotNull(def.Modifiers, path);
                Assert.IsNotEmpty(def.Modifiers, path + ": a trait with no modifier does nothing");
                foreach (TraitModifier m in def.Modifiers)
                {
                    Assert.IsTrue(System.Enum.IsDefined(typeof(TraitStat), m.Stat), path + ": unknown stat " + (int)m.Stat);
                    Assert.IsTrue(System.Enum.IsDefined(typeof(TraitModifierKind), m.Kind), path + ": unknown kind");
                    TraitDef single = Single(m);
                    float alone = TraitMath.FactorOf(single, m.Stat);
                    Object.DestroyImmediate(single);
                    Assert.IsTrue(TraitMath.IsBenefit(m.Stat, alone),
                        $"{path}: {m.Stat} x{alone} is not a bonus (owner ruling 2026-11-14: no drawbacks)");
                }
            }
        }

        [Test]
        public void TheSliceOneTraits_LoadWithTheirStartingNumbers()
        {
            var byId = new Dictionary<string, TraitDef>();
            foreach (TraitDef def in LoadAll()) byId[def.Id] = def;

            AssertOne(byId, "trait.speedwalker", "Speedwalker", TraitStat.WalkSpeed, 1.08f);
            AssertOne(byId, "trait.charismatic", "Charismatic", TraitStat.SellPrice, 1.05f);
            AssertOne(byId, "trait.patient_angler", "Patient Angler", TraitStat.BiteWait, 0.9f);
        }

        [Test]
        public void TheShippedCatalog_IsWhatTheDefaultServiceSees()
        {
            var service = new TraitService(() => null);
            var ids = new List<string>();
            foreach (TraitDef def in service.All) ids.Add(def.Id);
            CollectionAssert.IsSupersetOf(ids, new[] { "trait.charismatic", "trait.patient_angler", "trait.speedwalker" });
            Assert.AreEqual(LoadAll().Count, service.All.Count, "every TraitDef under Data/ is reachable at runtime");
        }

        [Test]
        public void ThePicksAllowed_IsTheOwnersTwo()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config);
            Assert.AreEqual(2, config.TraitPicksPerCharacter, "owner ruling 2026-11-14: players pick 2 traits");
        }

        private static void AssertOne(Dictionary<string, TraitDef> byId, string id, string name, TraitStat stat, float factor)
        {
            Assert.IsTrue(byId.TryGetValue(id, out TraitDef def), id + " is missing");
            Assert.AreEqual(name, def.DisplayName, id);
            Assert.AreEqual(factor, TraitMath.FactorOf(def, stat), 1e-5f, id + " loads its starting number");
            foreach (TraitStat other in System.Enum.GetValues(typeof(TraitStat)))
                if (other != stat) Assert.AreEqual(1f, TraitMath.FactorOf(def, other), id + " touches only " + stat);
        }

        private static TraitDef Single(TraitModifier m)
        {
            var def = ScriptableObject.CreateInstance<TraitDef>();
            def.Modifiers = new[] { m };
            def.hideFlags = HideFlags.HideAndDontSave;
            return def;
        }
    }
}
