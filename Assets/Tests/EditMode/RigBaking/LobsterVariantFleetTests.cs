using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b><see cref="LobsterVariantFleet"/> is the one place the eighteen are named</b>, and this is
    /// what keeps that worth relying on.
    ///
    /// <para>Four lanes read that table — the mesh fleet, the deck-sidecar wiring, the sidecar
    /// generator and the two authored tables' keys. A table that quietly stopped being the whole
    /// list, or whose derived names stopped agreeing with the importer's own conventions, would fail
    /// in the least visible way available: eighteen plausible boats, one of them the wrong one.</para>
    ///
    /// <para>CPU-only through the repo's own V8 host plus the AssetDatabase, so CI adjudicates every
    /// claim here except the ones that read committed assets.</para>
    /// </summary>
    public class LobsterVariantFleetTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;

        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, LobsterVariantFleet.ScriptPath)));
            return host;
        }

        // ---- 1. the list is still the whole list -------------------------------------------------

        /// <summary>
        /// <b>The axes are the rig's own.</b> Three hard-coded string arrays can go stale silently: a
        /// drop that adds a fourth region would leave this repo baking the old eighteen, passing
        /// everything, and saying nothing about the new six.
        ///
        /// <para>Sabotage: drop a region from <see cref="LobsterVariantFleet.Regions"/> and this
        /// names the difference.</para>
        /// </summary>
        [Test]
        public void TheAxes_AreTheRigsOwn()
        {
            using IRigScriptHost host = Host();
            Assert.AreEqual(LobsterVariantFleet.ExpectedAxisSignature,
                            LobsterVariantFleet.AxisSignature(host),
                "The rig's option axes are no longer the ones this fleet enumerates. Update " +
                "LobsterVariantFleet, FleetFlotationTableTests.Authored and " +
                "FleetDeckOccupancyTableTests.Authored together — a new cell with no flotation row " +
                "is a hull the sea draws through, and a new cell with no bake is a hull nobody sees.");
        }

        [Test]
        public void TheFleetIsTheCrossProduct_AndEveryCellIsDistinct()
        {
            Assert.AreEqual(LobsterVariantFleet.Sizes.Length * LobsterVariantFleet.Styles.Length *
                            LobsterVariantFleet.Regions.Length,
                            LobsterVariantFleet.All.Count, "not the full cross product");

            CollectionAssert.AllItemsAreUnique(LobsterVariantFleet.All.Select(v => v.Key).ToList());
            CollectionAssert.AllItemsAreUnique(LobsterVariantFleet.All.Select(v => v.MeshId).ToList());
            CollectionAssert.AllItemsAreUnique(LobsterVariantFleet.All.Select(v => v.AssetName).ToList());
            CollectionAssert.AllItemsAreUnique(
                LobsterVariantFleet.All.Select(v => v.SidecarFileName).ToList());
        }

        // ---- 2. the derived names round-trip through the tools that consume them -----------------

        /// <summary>
        /// <b>The deck id the importer will mint must be the id this fleet says it is.</b>
        ///
        /// <para>The importer does not take an id — it derives one, by stripping a <c>Rig</c> suffix
        /// off the sidecar's file stem and snake-casing the result. So the stem is not cosmetic: it
        /// is the input to somebody else's naming function, and a stem that did not round-trip would
        /// mint <c>deck.lobster_standard_hardtop_fundy_iso</c> for one hull and something else for
        /// the next, with nothing to notice.</para>
        /// </summary>
        [Test]
        public void EveryDerivedName_RoundTripsThroughTheImportersOwnConventions()
        {
            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                string stem = DeckSidecarImporter.StripRigSuffix(
                    v.SidecarFileName.Replace(".gameplay.json", ""));

                Assert.AreEqual(v.SidecarStem, stem,
                    $"{v.Variant}: the importer strips a 'Rig' suffix; this stem does not survive it.");

                string assetName = char.ToUpperInvariant(stem[0]) + stem.Substring(1);
                Assert.AreEqual(v.AssetName, assetName,
                    $"{v.Variant}: the importer capitalises the stem to get the Def file name, and " +
                    "that must be the same file the mesh bake writes her visual beside.");

                Assert.AreEqual(v.DeckId, "deck." + DeckSidecarImporter.SnakeCase(stem),
                    $"{v.Variant}: the deck id the importer mints is not the one this fleet names.");

                Assert.AreEqual(v.MeshId, "hullmesh." + DeckSidecarImporter.SnakeCase(stem),
                    $"{v.Variant}: her mesh id and her deck id no longer share a body, so the two " +
                    "authored tables' keys stop lining up with the assets on disk.");
            }
        }

        [Test]
        public void EveryId_FollowsTheProjectConvention()
        {
            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                Assert.That(v.MeshId, Does.Match(@"^hullmesh\.[a-z0-9]+(_[a-z0-9]+)*$"), v.Variant);
                Assert.That(v.VisualId, Does.Match(@"^visual\.[a-z0-9]+(_[a-z0-9]+)*$"), v.Variant);
                Assert.That(v.DeckId, Does.Match(@"^deck\.[a-z0-9]+(_[a-z0-9]+)*$"), v.Variant);
            }
        }

        /// <summary>The ids this bake chose are the ones both authored tables are keyed by. The
        /// tables say the id is the bake's to pick; this records that the bake picked theirs, so
        /// nobody has to check by eye that eighteen strings match eighteen strings.</summary>
        [Test]
        public void TheChosenIds_AreTheOnesTheAuthoredTablesAreKeyedBy()
        {
            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                Assert.IsTrue(FleetFlotationTableTests.Authored.ContainsKey(v.MeshId),
                    $"{v.Variant}: no flotation row for {v.MeshId} — she would float with her deck " +
                    "under water, or not float at all.");
                Assert.IsTrue(FleetDeckOccupancyTableTests.Authored.ContainsKey(v.MeshId),
                    $"{v.Variant}: no deck-occupancy row for {v.MeshId}.");
            }
        }

        // ---- 3. the fleet rows are built from this table and nothing else ------------------------

        [Test]
        public void TheFleetCarriesExactlyTheseEighteen_WithTheirOwnExtractions()
        {
            // ⚠️ FILTERED BY RIG, not by IsVariant. She was the only generator when this was
            // written, so "every variant row" and "every lobster row" were the same set; the zodiac
            // and the sport fisher made them different on 2026-08-16 and this went red counting
            // twenty-two. The control being made is about HER eighteen, so it names her file.
            var byId = HullMeshFleet.Hulls
                .Where(h => h.IsVariant && h.ScriptPath == LobsterVariantFleet.ScriptPath)
                .ToDictionary(h => h.MeshId);
            Assert.AreEqual(LobsterVariantFleet.All.Count, byId.Count,
                "The fleet's lobster-variant rows are not the eighteen this table enumerates.");

            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                Assert.IsTrue(byId.TryGetValue(v.MeshId, out FleetHull hull), $"{v.Variant}: missing");
                Assert.AreEqual(LobsterVariantFleet.ScriptPath, hull.ScriptPath, v.Variant);
                Assert.AreEqual(LobsterVariantFleet.GlobalName, hull.GlobalName, v.Variant);
                Assert.AreEqual(v.Extraction.FaceExpression, hull.Extraction.FaceExpression,
                    $"{v.Variant}: the fleet row names a different cell than this table does — the " +
                    "bake would write a real boat under the wrong hull's id.");
                Assert.IsFalse(hull.HasBakedSheet,
                    $"{v.Variant}: no 32-facing sheet was ever baked for her, so claiming one would " +
                    "offer the owner a V-key A/B with nothing on the sprite side.");
                StringAssert.EndsWith($"/{v.AssetName}HullMesh.asset", hull.MeshAssetPath, v.Variant);
            }
        }

        /// <summary>The rig is baked now, so it must no longer be excluded — and the exclusion list
        /// is what the coverage test reads. Both directions, because a rig that is in BOTH would be
        /// baked and explained-away at once.</summary>
        [Test]
        public void TheVariantsRig_IsBakedAndNoLongerExcluded()
        {
            string file = Path.GetFileName(LobsterVariantFleet.ScriptPath);

            CollectionAssert.Contains(HullMeshFleet.BakedRigFileNames.ToList(), file,
                "The variants rig is not baked by anything in the fleet.");
            Assert.IsFalse(HullMeshFleet.NotHulls.ContainsKey(file),
                $"{file} is both baked and listed as not-a-hull. The exclusion entry's own " +
                "instruction was to delete it when the defs landed; they have.");
        }

        // ---- 4. the committed artefacts, once they exist ----------------------------------------

        /// <summary>
        /// <b>Every cell has all four of her committed artefacts</b> — sidecar, deck Def, visual and
        /// hull mesh. The chain <c>LobsterVariantRigTests</c> described has to land whole: a sidecar
        /// with no Def fails the parity fixture, a Def nobody wears fails the orphan check, and a
        /// visual with no mesh is a boat that cannot be drawn.
        /// </summary>
        [Test]
        public void EveryCell_HasHerSidecarDeckVisualAndMesh()
        {
            var missing = new List<string>();

            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                string sidecar = Path.Combine(RepoRoot, "docs/art/rigs/gameplay", v.SidecarFileName);
                if (!File.Exists(sidecar)) missing.Add($"{v.Variant}: sidecar {v.SidecarFileName}");

                var deck = AssetDatabase.LoadAssetAtPath<BoatDeckDef>(
                    $"Assets/_Project/Data/Boats/Decks/{v.AssetName}.asset");
                if (deck == null) missing.Add($"{v.Variant}: deck Def");
                else if (deck.Id != v.DeckId) missing.Add($"{v.Variant}: deck id is '{deck.Id}'");

                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    $"Assets/_Project/Data/Boats/Visuals/{v.AssetName}.asset");
                if (visual == null) missing.Add($"{v.Variant}: visual");
                else
                {
                    if (visual.Id != v.VisualId) missing.Add($"{v.Variant}: visual id is '{visual.Id}'");
                    if (visual.Deck != deck) missing.Add($"{v.Variant}: visual wears the wrong deck");
                }

                var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{v.AssetName}HullMesh.asset");
                if (mesh == null) missing.Add($"{v.Variant}: hull mesh");
                else if (mesh.Id != v.MeshId) missing.Add($"{v.Variant}: mesh id is '{mesh.Id}'");
            }

            CollectionAssert.IsEmpty(missing,
                "The lobster variants' chain is incomplete. Run, in order: Hidden Harbours ▸ Dev ▸ " +
                "Generate the 18 lobster-variant deck sidecars; ▸ 3D Hulls ▸ Bake the 18 lobster " +
                "variant hull meshes; ▸ Import Deck Sidecars.\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// <b>Eighteen meshes off one rig must be eighteen different meshes.</b>
        ///
        /// <para>The distinctness of the EXTRACTION is measured elsewhere
        /// (<c>RigMeshVariantExtractionTests</c>, over the face fingerprint). This asks the question
        /// one step later, of the assets that actually ship: two defs whose baked meshes are
        /// identical would mean the bake ran the same descriptor twice — the exact failure the rig's
        /// silent unknown-id fallback makes possible, and one that a count check cannot see because
        /// two variants legitimately share a face count.</para>
        /// </summary>
        [Test]
        public void TheEighteenCommittedMeshes_AreEighteenDifferentBoats()
        {
            var byPrint = new Dictionary<string, string>(StringComparer.Ordinal);
            var clashes = new List<string>();

            foreach (LobsterVariant v in LobsterVariantFleet.All)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                    $"Assets/_Project/Data/Boats/HullMeshes/{v.AssetName}HullMesh.asset");
                Assert.IsNotNull(def, $"{v.Variant}: no committed def");
                Assert.IsNotNull(def.Mesh, $"{v.Variant}: no mesh sub-asset");

                Vector3[] verts = def.Mesh.vertices;
                Assert.Greater(verts.Length, 0, $"{v.Variant}: empty mesh");

                var sb = new System.Text.StringBuilder();
                foreach (Vector3 p in verts)
                    sb.Append(p.x.ToString("F5")).Append(',')
                      .Append(p.y.ToString("F5")).Append(',')
                      .Append(p.z.ToString("F5")).Append(';');
                string print = sb.ToString();

                if (byPrint.TryGetValue(print, out string other))
                    clashes.Add($"{v.Variant} === {other}");
                else byPrint[print] = v.Variant;

                Assert.AreEqual(def.SourceFaceBuilder,
                    $"{LobsterVariantFleet.GlobalName}.{v.Extraction.FaceExpression}",
                    $"{v.Variant}: the committed def records a different cell than her id claims.");
            }

            CollectionAssert.IsEmpty(clashes,
                "Two committed variant meshes are geometrically identical, so the bake ran one " +
                "descriptor twice and one of these hulls is not the boat her id says she is. The " +
                "rig resolves an UNKNOWN axis id to its DEFAULT silently, so a typo does not fail " +
                "the bake — it ships the standard boat under someone else's name: " +
                string.Join(", ", clashes));
        }
    }
}
