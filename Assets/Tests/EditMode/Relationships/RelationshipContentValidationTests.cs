using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the relationship data (relationships slice 1). The config and the seed ties
    /// were hand-authored as YAML, so every check reads what Unity LOADS, never the file text: a bad indent
    /// fails silently to the field default, and only the loaded object tells.
    /// </summary>
    public class RelationshipContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string ResourcesRoot = "Assets/_Project/Data/Resources/";
        private const string NpcPrefix = "npc.";
        private static readonly Regex TieIdShape = new Regex("^tie\\.[a-z0-9]+(_[a-z0-9]+)*__[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static RelationshipConfig TheConfig()
        {
            List<RelationshipConfig> all = LoadAll<RelationshipConfig>();
            Assert.AreEqual(1, all.Count, "exactly one RelationshipConfig ships");
            Assert.AreEqual(ResourcesRoot + RelationshipService.ConfigPath + ".asset", AssetDatabase.GetAssetPath(all[0]),
                "the service loads the config from Resources/" + RelationshipService.ConfigPath);
            return all[0];
        }

        [Test]
        public void TheConfig_HasOrderedThresholds()
        {
            RelationshipStageThresholds t = TheConfig().Thresholds;
            Assert.IsTrue(t.IsValid,
                $"thresholds out of order: min {t.MinStrength}, rival {t.RivalAtOrBelow}, strained {t.StrainedAtOrBelow}, " +
                $"acquaintance {t.AcquaintanceFrom}, friend {t.FriendFrom}, close {t.CloseFrom}, max {t.MaxStrength}");
        }

        [Test]
        public void TheConfig_PricesEveryEventKind_Once()
        {
            RelationshipConfig config = TheConfig();
            Assert.IsNotNull(config.Rules);
            var seen = new HashSet<RelationshipEventKind>();
            foreach (RelationshipRule rule in config.Rules)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RelationshipEventKind), rule.Kind), "unknown kind " + (int)rule.Kind);
                Assert.IsTrue(seen.Add(rule.Kind), "two rows for " + rule.Kind);
                Assert.IsTrue(rule.IsValid, rule.Kind + ": a rule needs a non-zero step and a positive cap");
            }
            foreach (RelationshipEventKind kind in Enum.GetValues(typeof(RelationshipEventKind)))
                Assert.IsTrue(seen.Contains(kind), "no rule row for " + kind + ": its events would move nothing");
        }

        [Test]
        public void TheConfig_LoadsWithItsStartingNumbers()
        {
            RelationshipConfig config = TheConfig();
            Assert.AreEqual(-40, config.Thresholds.RivalAtOrBelow);
            Assert.AreEqual(70, config.Thresholds.CloseFrom);
            var byKind = new Dictionary<RelationshipEventKind, RelationshipRule>();
            foreach (RelationshipRule rule in config.Rules) byKind[rule.Kind] = rule;
            Assert.AreEqual(-1, byKind[RelationshipEventKind.CustomDiverted].PointsPerUnit, "diverted custom cools");
            Assert.IsFalse(byKind[RelationshipEventKind.CustomDiverted].CanMakeRival);
            Assert.AreEqual(12, byKind[RelationshipEventKind.Introduced].CapPerPairPerDay);
        }

        [Test]
        public void EveryTie_IsWellFormed_NamesRealVillagers_AndIsUnique()
        {
            List<RelationshipTieDef> ties = LoadAll<RelationshipTieDef>();
            Assert.IsNotEmpty(ties, "slice 1 ships seed ties under Data/Resources/Relationships/Ties");

            var npcIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NpcDef npc in LoadAll<NpcDef>()) npcIds.Add(npc.Id);
            RelationshipStageThresholds bounds = TheConfig().Thresholds;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (RelationshipTieDef tie in ties)
            {
                string path = AssetDatabase.GetAssetPath(tie);
                StringAssert.StartsWith(ResourcesRoot + RelationshipService.TiesFolder + "/", path,
                    "the service loads ties from Resources/" + RelationshipService.TiesFolder + " only");
                Assert.IsTrue(TieIdShape.IsMatch(tie.Id ?? ""), path + ": id must be tie.<a>__<b>, got " + tie.Id);
                Assert.IsTrue(ids.Add(tie.Id), path + ": duplicate id " + tie.Id);
                Assert.IsTrue(npcIds.Contains(tie.NpcA ?? ""), path + ": no villager " + tie.NpcA);
                Assert.IsTrue(npcIds.Contains(tie.NpcB ?? ""), path + ": no villager " + tie.NpcB);
                Assert.Less(string.CompareOrdinal(tie.NpcA, tie.NpcB), 0, path + ": NpcA must sort before NpcB");
                Assert.AreEqual("tie." + Bare(tie.NpcA) + "__" + Bare(tie.NpcB), tie.Id, path + ": id must name its pair");
                Assert.IsTrue(pairs.Add(tie.NpcA + "|" + tie.NpcB), path + ": this pair is already tied");
                Assert.AreEqual(bounds.Clamp(tie.StartingStrength), tie.StartingStrength,
                    path + ": starting strength outside the config's bounds");
                Assert.IsFalse(string.IsNullOrWhiteSpace(tie.Note), path + ": say why they start this way");
            }
        }

        [Test]
        public void TheSliceOneTies_LoadWithTheirStartingStages()
        {
            var service = new RelationshipService();
            Assert.AreEqual(RelationshipStage.Friend, service.Stage("npc.aunt_ginny", "npc.basil_samson"));
            Assert.AreEqual(RelationshipStage.Acquaintance, service.Stage("npc.rose_macisaac", "npc.marguerite_leblanc"));
            Assert.AreEqual(RelationshipStage.Strained, service.Stage("npc.basil_samson", "npc.junior_poirier"));
            Assert.AreEqual(RelationshipStage.Stranger, service.Stage("npc.eileen_doiron", "npc.aunt_ginny"),
                "some villagers start with no ties at all");
            Assert.AreEqual(LoadAll<RelationshipTieDef>().Count, service.Graph.Snapshot().Count,
                "every tie under Data/ is reachable at runtime");
        }

        private static string Bare(string npcId)
            => npcId != null && npcId.StartsWith(NpcPrefix, StringComparison.Ordinal) ? npcId.Substring(NpcPrefix.Length) : npcId;
    }
}
