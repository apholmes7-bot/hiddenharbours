using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    // Extends the existing NPC asset harness. HomeListingTests owns resident identities;
    // StPetersRoutineContentTests and RoutineCrossRegionContentTests own day shape and crossings.
    // This closes their region-wide reference/placement gaps using committed Force Text only.
    public partial class NpcContentValidationTests
    {
        static readonly Dictionary<string, string> MissingStationReferencesBaseline = new(StringComparer.Ordinal)
        {
            ["home.st_peters_red_saltbox/ResidentMovesTo | region.st_peters | station.st_peters.home_post_office"] =
                "Rose MacIsaac's move above the post office is declared on her home but has no station yet."
        };

        static readonly Dictionary<string, string> NpcsWithoutRoutinesYet = new(StringComparer.Ordinal)
        {
            ["npc.angus_gillis"] = "Angus Gillis has a character Def but no authored day yet.",
            ["npc.claudette_boudreau"] = "Claudette Boudreau has neither a routine nor a committed Nine Mile Creek placement yet.",
            ["npc.dougie_macneil"] = "Dougie MacNeil has a character Def but no authored day yet.",
            ["npc.hector_bernard"] = "Hector Bernard is placed in Nine Mile Creek without an authored day.",
            ["npc.lucie_landry"] = "Lucie Landry has a character Def but no authored day yet.",
            ["npc.monique_landry"] = "Monique Landry has a character Def but no authored day yet.",
            ["npc.ned_letter"] = "Ned's letter is a readable object using the NPC dialogue schema, not a walking resident.",
            ["npc.nmc_harbourmaster"] = "The harbourmaster is a stationary Nine Mile Creek service contact without an authored day.",
            ["npc.nmc_yard"] = "The yard hand is a stationary Nine Mile Creek service contact without an authored day.",
            ["npc.paul_landry"] = "Paul Landry has a character Def but no authored day yet.",
            ["npc.rene_chiasson"] = "Rene Chiasson has a character Def but no authored day yet.",
            ["npc.rpt_wreck"] = "The Rice Point wreck is a readable object using the NPC dialogue schema, not a walking resident.",
            ["npc.sam_landry"] = "Sam Landry has a character Def but no authored day yet.",
            ["npc.theresa_gillis"] = "Theresa Gillis has a character Def but no authored day yet.",
            ["npc.wendell_arsenault"] = "Wendell Arsenault is placed in Nine Mile Creek without an authored day."
        };

        // Empty today: all routine owners are placed and all placed Interactables have valid Defs.
        static readonly Dictionary<string, string> MissingNpcPlacementsBaseline = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string> PlacedNpcsWithoutDefsBaseline = new(StringComparer.Ordinal);

        sealed class NpcSceneContent
        {
            public readonly HashSet<string> Stations = new(StringComparer.Ordinal);
            // Keepers carry their own stops on a KeeperRound, apart from the village station table.
            public readonly HashSet<string> KeeperStops = new(StringComparer.Ordinal);
            public readonly List<(string location, string npc)> Placements = new();
            public readonly List<(string location, string routine, string npc)> Walkers = new();
            public readonly HashSet<string> Prefabs = new(StringComparer.Ordinal);
        }

        Dictionary<string, NpcSceneContent> _npcScenes;
        readonly Dictionary<string, string> _npcScriptGuids = new(StringComparer.Ordinal);

        string NpcScriptGuid(string relativePath)
        {
            if (!_npcScriptGuids.TryGetValue(relativePath, out string guid))
            {
                guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Code/World/" + relativePath + ".cs");
                Assert.That(guid, Is.Not.Empty, "Missing script: " + relativePath);
                _npcScriptGuids.Add(relativePath, guid);
            }
            return guid;
        }

        static string NpcGuid(string reference) => Regex.Match(reference ?? "", @"\bguid: ([0-9a-f]{32})\b").Groups[1].Value;
        static string NpcReference(string reference) => NpcGuid(reference) + ":" + ParseRef(reference);
        bool NpcScript(Doc doc, string relativePath) => doc.ClassId == 114 &&
            NpcGuid(doc.Field("m_Script")) == NpcScriptGuid(relativePath);

        static string NpcAssetReference(UnityEngine.Object asset)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id), Is.True);
            return guid + ":" + id;
        }

        Dictionary<string, NpcSceneContent> NpcScenes()
        {
            if (_npcScenes != null) return _npcScenes;
            var scenes = new Dictionary<string, NpcSceneContent>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles("Assets/_Project/Scenes", "*.unity").OrderBy(p => p, StringComparer.Ordinal))
            {
                var yaml = SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
                var scene = new NpcSceneContent();
                foreach (Doc doc in yaml.Docs)
                {
                    if (doc.ClassId == 1001) scene.Prefabs.Add(NpcGuid(doc.Field("m_SourcePrefab")));
                    if (NpcScript(doc, "Routines/RoutineStations"))
                    {
                        Assert.That(doc.Field("_stations"), Is.Not.Null, path + ": station schema changed");
                        foreach (Match row in Regex.Matches(doc.Text, @"^  - Id: ([^\n]*)$", RegexOptions.Multiline))
                        {
                            string id = row.Groups[1].Value.Trim().Trim('"');
                            Assert.That(id, Does.StartWith("station."), path);
                            Assert.That(scene.Stations.Add(id), Is.True, path + ": duplicate station " + id);
                        }
                    }
                    if (NpcScript(doc, "Keepers/KeeperRound"))
                    {
                        Assert.That(doc.Field("_stops"), Is.Not.Null, path + ": keeper stop schema changed");
                        foreach (Match row in Regex.Matches(doc.Text, @"^  - Id: ([^\n]*)$", RegexOptions.Multiline))
                        {
                            string id = row.Groups[1].Value.Trim().Trim('"');
                            Assert.That(id, Does.StartWith("station."), path);
                            Assert.That(scene.Stations.Contains(id), Is.False, path + ": keeper stop also in the village table " + id);
                            scene.KeeperStops.Add(id);
                        }
                    }
                    if (!NpcScript(doc, "Interactable") && !NpcScript(doc, "Routines/VillagerRoutine")) continue;
                    Assert.That(doc.Stripped, Is.False, path + ": expand prefab NPC components before auditing them");
                    Doc go = yaml.Require(doc.FieldRef("m_GameObject"), "NPC component owner");
                    Assert.That(go.FieldRefs("m_Component"), Does.Contain(doc.FileId), path + ": orphan NPC component");
                    string location = Path.GetFileNameWithoutExtension(path) + "/" + go.Field("m_Name") + "/" + doc.FileId;
                    if (NpcScript(doc, "Interactable")) scene.Placements.Add((location, NpcReference(doc.Field("_npc"))));
                    else
                    {
                        var talk = yaml.ComponentsOf(go).Where(c => NpcScript(c, "Interactable")).ToArray();
                        Assert.That(talk.Length, Is.EqualTo(1), location + ": walker needs one NPC Interactable");
                        scene.Walkers.Add((location, NpcReference(doc.Field("_routine")), NpcReference(talk[0].Field("_npc"))));
                    }
                }
                scenes.Add(Path.GetFileNameWithoutExtension(path), scene);
            }
            Assert.That(scenes, Is.Not.Empty, "No committed scenes were inspected");
            _npcScenes = scenes;
            return scenes;
        }

        static void AssertNpcBaseline(IEnumerable<string> actual, Dictionary<string, string> baseline, string label)
        {
            foreach (var row in baseline) Assert.That(row.Value, Is.Not.Null.And.Not.Empty, row.Key + " needs a reason");
            string[] found = actual.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            string[] expected = baseline.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.That(found, Is.EqualTo(expected), label + ": new failures must be fixed or deliberately named; " +
                "resolved failures must be removed from the baseline.\nActual:\n" + string.Join("\n", found));
        }

        [Test]
        public void HomeRoutineAndScheduleStations_ResolveInTheirOwnRegion_ExceptNamedBaseline()
        {
            var regions = LoadAll<RegionDef>().ToDictionary(r => r.Id, StringComparer.Ordinal);
            var scenes = NpcScenes();
            var missing = new List<string>();
            void Check(string source, string regionId, string station)
            {
                Assert.That(regionId, Is.Not.Null.And.Not.Empty, source + ": missing region");
                Assert.That(regions.ContainsKey(regionId), Is.True, source + ": unknown region " + regionId);
                Assert.That(scenes.ContainsKey(regions[regionId].SceneName), Is.True, source + ": no committed region scene");
                var scene = scenes[regions[regionId].SceneName];
                if (!scene.Stations.Contains(station ?? "") && !scene.KeeperStops.Contains(station ?? ""))
                    missing.Add(source + " | " + regionId + " | " + station);
            }
            foreach (HomeDef home in LoadAll<HomeDef>())
                if ((home.ResidentMovesTo ?? "").StartsWith("station.", StringComparison.Ordinal))
                    Check(home.Id + "/ResidentMovesTo", home.RegionId, home.ResidentMovesTo);
            var routines = LoadAll<RoutineDef>();
            Assert.That(routines, Is.Not.Empty, "No routines inspected");
            foreach (RoutineDef routine in routines)
            {
                void Day(string label, RoutineEntry[] entries)
                {
                    Assert.That(entries, Is.Not.Null.And.Not.Empty, routine.Id + "/" + label);
                    for (int i = 0; i < entries.Length; i++)
                        Check(routine.Id + "/" + label + "[" + i + "]",
                            string.IsNullOrEmpty(entries[i].RegionId) ? routine.RegionId : entries[i].RegionId, entries[i].StationId);
                }
                Day("Entries", routine.Entries);
                for (int i = 0; i < (routine.WeeklyDays?.Length ?? 0); i++)
                    Day("WeeklyDays[" + i + ":" + routine.WeeklyDays[i].Weekday + "].Entries", routine.WeeklyDays[i].Entries);
            }
            foreach (ConversationDef conversation in LoadAll<ConversationDef>())
                Check(conversation.Id + "/StationId", conversation.RegionId, conversation.StationId);
            foreach (RegionCrossingDef crossing in LoadAll<RegionCrossingDef>())
            {
                Check(crossing.Id + "/FromStationId", crossing.FromRegionId, crossing.FromStationId);
                Check(crossing.Id + "/ToStationId", crossing.ToRegionId, crossing.ToStationId);
            }
            AssertNpcBaseline(missing, MissingStationReferencesBaseline, "Unresolved stations");
        }

        [Test]
        public void DeclaredRegionalStations_AreExactlyTheCommittedStations()
        {
            var regions = LoadAll<RegionDef>();
            Assert.That(regions, Is.Not.Empty);
            var declared = regions.ToDictionary(r => r.Id, _ => new HashSet<string>(StringComparer.Ordinal));
            declared["region.st_peters"].UnionWith(StPetersRoutines.AllStationIds);
            declared[HeronViewRoster.RegionId].UnionWith(CumberlandCourse.Read().Stations.Select(s => s.Id));
            foreach (var keeper in NmcWholesalerKeepers.All)
            {
                declared["region.nine_mile_creek"].Add(keeper.CounterStationId);
                declared["region.nine_mile_creek"].Add(keeper.HomeStationId);
            }
            declared["region.nine_mile_creek"].Add(NmcRoutineStationsPatch.BarLandingId);
            declared["region.nine_mile_creek"].Add(NmcRoutineStationsPatch.FuelStopId);
            var scenes = NpcScenes();
            foreach (var region in regions)
            {
                IEnumerable<string> placed = scenes.TryGetValue(region.SceneName, out var scene)
                    ? scene.Stations : Enumerable.Empty<string>();
                Assert.That(placed, Is.EquivalentTo(declared[region.Id]), region.Id +
                    ": declarations and committed stations must agree; routine references are checked against this same set");
            }
        }

        [Test]
        public void EveryRoutineOwner_IsPlacedInItsRegionScene()
        {
            var regions = LoadAll<RegionDef>().ToDictionary(r => r.Id, StringComparer.Ordinal);
            var scenes = NpcScenes();
            var missing = new List<string>();
            var routines = LoadAll<RoutineDef>();
            Assert.That(routines, Is.Not.Empty);
            var npcs = new HashSet<NpcDef>(LoadAll<NpcDef>());
            foreach (RoutineDef routine in routines)
            {
                Assert.That(routine.Npc, Is.Not.Null, routine.Id + ": missing NPC Def");
                Assert.That(npcs, Does.Contain(routine.Npc), routine.Id + ": NPC must be in the content inventory");
                Assert.That(regions.ContainsKey(routine.RegionId), Is.True, routine.Id + ": unknown region");
                string sceneName = regions[routine.RegionId].SceneName;
                if (!scenes.TryGetValue(sceneName, out var scene) ||
                    !scene.Placements.Any(p => p.npc == NpcAssetReference(routine.Npc)))
                    missing.Add(routine.Id + " | " + routine.Npc.Id + " | " + routine.RegionId);
            }
            AssertNpcBaseline(missing, MissingNpcPlacementsBaseline, "Unplaced routine owners");
        }

        [Test]
        public void EveryPlacedNpc_HasAResolvableDef_AndWalkersUseThatSameNpc()
        {
            var npcs = LoadAll<NpcDef>().ToDictionary(NpcAssetReference, n => n);
            var routines = LoadAll<RoutineDef>().ToDictionary(NpcAssetReference, r => r);
            var scenes = NpcScenes().Values.ToArray();
            Assert.That(scenes.SelectMany(s => s.Placements), Is.Not.Empty);
            AssertNpcBaseline(scenes.SelectMany(s => s.Placements).Where(p => !npcs.ContainsKey(p.npc))
                .Select(p => p.location + " | " + p.npc), PlacedNpcsWithoutDefsBaseline, "Placed NPCs without Defs");
            foreach (var walker in scenes.SelectMany(s => s.Walkers))
            {
                Assert.That(routines.ContainsKey(walker.routine), Is.True, walker.location + ": missing routine Def");
                Assert.That(routines[walker.routine].Npc, Is.Not.Null, walker.location);
                Assert.That(walker.npc, Is.EqualTo(NpcAssetReference(routines[walker.routine].Npc)),
                    walker.location + ": the walker and talk component represent different NPCs");
            }
        }

        [Test]
        public void EveryNpc_HasARoutine_OrAnExplicitReasonWithoutOne()
        {
            var npcs = LoadAll<NpcDef>();
            Assert.That(npcs, Is.Not.Empty);
            var owners = new HashSet<NpcDef>(LoadAll<RoutineDef>().Select(r => r.Npc));
            AssertNpcBaseline(npcs.Where(n => !owners.Contains(n)).Select(n => n.Id), NpcsWithoutRoutinesYet,
                "NPCs without routines");
        }

        [Test]
        public void ReferencedPrefabs_CannotHideNpcOrStationComponents()
        {
            // Same fail-closed approach as RegionSeamAuditTests: today's NPCs/stations are scene rows.
            // If that changes, require prefab expansion (including overrides) before claiming coverage.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>(NpcScenes().Values.SelectMany(s => s.Prefabs));
            while (pending.Count > 0)
            {
                string guid = pending.Dequeue();
                if (!seen.Add(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(path, Does.EndWith(".prefab"), "Unresolved scene prefab: " + guid);
                var yaml = SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
                foreach (Doc doc in yaml.Docs)
                {
                    Assert.That(NpcScript(doc, "Interactable") || NpcScript(doc, "Routines/VillagerRoutine") ||
                        NpcScript(doc, "Routines/RoutineStations"), Is.False,
                        path + ": expand prefab NPC/station content and overrides before accepting this audit");
                    if (doc.ClassId == 1001) pending.Enqueue(NpcGuid(doc.Field("m_SourcePrefab")));
                }
            }
        }
    }
}
