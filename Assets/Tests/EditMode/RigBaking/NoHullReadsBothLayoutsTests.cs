using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class NoHullReadsBothLayoutsTests
    {
        [Test]
        public void CommittedFleet_ReadsExactlyOneLayoutPerHull()
        {
            string root = RigCatalog.RepoRoot;
            var walks = HullWalkSidecarImporter.Files(root).Select(f => HullLayoutJson.Object(DeckSidecarJson.Parse(File.ReadAllText(f)), f)).ToArray();
            var decks = Directory.GetFiles(Path.Combine(root, HullWalkSidecarImporter.SidecarFolder), "*.gameplay.json")
                .Select(f => new { File = f, Json = HullLayoutJson.Object(DeckSidecarJson.Parse(File.ReadAllText(f)), f) }).ToArray();
            var sheetRoot = HullLayoutJson.Object(DeckSidecarJson.Parse(File.ReadAllText(Path.Combine(root,
                "Assets/_Project/Art/Boats/Interiors/BoatInteriors.json"))), "sheets");
            var sheets = HullLayoutJson.Array(sheetRoot, "sheets").Select(s => HullLayoutJson.Object(s, "sheet")).ToArray();
            foreach (var walk in walks)
            {
                string hull = HullLayoutJson.Text(walk, "hull"), rig = HullLayoutJson.Text(walk, "rig");
                bool overlap = decks.Any(d => (DeckSidecarJson.String(DeckSidecarJson.Member(d.Json, "rig")) ?? "") == rig &&
                    (DeckSidecarJson.Member(d.Json, "variant") == null ||
                     DeckSidecarJson.String(DeckSidecarJson.Member(DeckSidecarJson.Member(d.Json, "variant"), "hull")) == hull));
                Assert.That(HullLayoutGuard.Check(hull, HullLayoutJson.Text(walk, "schema"), "", overlap, false, false), Is.Empty);
            }
            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.That(def, Is.Not.Null, hull.MeshId);
                if (string.IsNullOrEmpty(def.LayoutSchema)) continue;
                var matches = walks.Where(w => HullLayoutJson.Text(w, "rig") == Path.GetFileName(hull.ScriptPath) &&
                    (hull.Extraction == null || hull.Extraction.ScopeOr(hull.GlobalName).Contains("'" + HullLayoutJson.Text(w, "hull") + "'"))).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), hull.MeshId + ": walk must resolve once");
                string stem = Path.GetFileName(hull.MeshAssetPath).Replace("HullMesh.asset", "");
                bool sheet = sheets.Any(s => string.Equals(DeckSidecarJson.String(DeckSidecarJson.Member(s, "hullFileStem")), stem, StringComparison.OrdinalIgnoreCase));
                Assert.That(HullLayoutGuard.Check(hull.MeshId, HullLayoutJson.Text(matches[0], "schema"), def.LayoutSchema,
                    false, RigMeshAssetBaker.MeshInteriorHulls.Contains(hull.GlobalName), sheet), Is.Empty);
            }
        }

        [TestCase(true, false, false, "both deck and walk")]
        [TestCase(false, true, false, "MeshInteriorHulls")]
        [TestCase(false, false, true, "BoatInteriors.json")]
        public void PlantedOverlap_IsRefusedByHull(bool deck, bool kit, bool sheet, string cause)
        {
            var errors = HullLayoutGuard.Check("box_walk", HullWalkSidecarReader.Schema, HullWalkSidecarReader.Schema, deck, kit, sheet);
            StringAssert.Contains("box_walk", errors.Single());
            StringAssert.Contains(cause, errors.Single());
        }

        [Test]
        public void SwitchedDefWithoutMatchingWalk_IsRefused()
        {
            Assert.That(HullLayoutGuard.Check("box_walk", null, HullWalkSidecarReader.Schema, false, false, false), Has.Length.EqualTo(1));
            Assert.That(HullLayoutGuard.Check("box_walk", "unknown", HullWalkSidecarReader.Schema, false, false, false), Has.Length.EqualTo(1));
        }
    }
}
