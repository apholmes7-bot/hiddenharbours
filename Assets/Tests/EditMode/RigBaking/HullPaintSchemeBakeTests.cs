using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The paint kit's registration probe: does what was baked still agree with the rig?</b>
    ///
    /// <para>Every claim here is checked against the RIG, live, through the same host and the same JS
    /// the baker uses — not against a table copied into this file. That is deliberate: a fixture that
    /// hard-codes the colours it expects passes forever after the art director changes them, which
    /// is the failure this whole pipeline is built to avoid.</para>
    ///
    /// <para><b>The load-bearing one is <see cref="DefaultSchemeIsTheHullsOwnTable"/>.</b> "Unset
    /// scheme = today's boat" is the contract the entire kit rests on, and it is only true while the
    /// rig's default paint resolves to exactly the material table the committed hull def already
    /// holds. Measured on the drop (2026-08-12) it does, entry for entry, including the negative
    /// offsets on <c>blk</c>/<c>dark</c>. If a future drop retunes gelcoat, this goes red and says
    /// so instead of quietly repainting every lobster boat in the game.</para>
    /// </summary>
    public class HullPaintSchemeBakeTests
    {
        const string HullMeshAssetPath =
            "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";

        static HullMeshDef LoadHull()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshAssetPath);
            Assert.IsNotNull(def, $"No hull mesh def at {HullMeshAssetPath}.");
            return def;
        }

        static HullPaintSchemeDef[] LoadSchemes()
        {
            var found = AssetDatabase
                .FindAssets($"t:{nameof(HullPaintSchemeDef)}", new[] { HullPaintSchemeBaker.SchemeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>)
                .Where(s => s != null)
                .OrderBy(s => s.Id)
                .ToArray();
            Assert.IsNotEmpty(found,
                $"No paint schemes under {HullPaintSchemeBaker.SchemeFolder}. Run " +
                "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake hull PAINT SCHEMES…");
            return found;
        }

        /// <summary>The hull under test — read from the baker's own fleet table, so a fixture can
        /// never drift from what the baker actually bakes.</summary>
        static HullPaintSchemeBaker.PaintedHull Hull =>
            HullPaintSchemeBaker.Fleet.First(h => h.RigKey == "lobsterBoat");

        /// <summary>Opens the rig shimmed exactly as the baker shims it.</summary>
        static IRigScriptHost OpenRig(out string global)
        {
            var hull = Hull;
            var entry = RigCatalog.Get(hull.RigKey);
            global = entry.GlobalName;
            var host = RigScriptHostFactory.Create();
            string source = RigCatalog.ReadSource(entry);
            host.Execute(hull.ShimSymbols.Length == 0
                ? source
                : RigMeshExtractor.WidenExportedLiteral(
                      source, entry.GlobalName, hull.ShimSymbols, entry.ScriptPath));
            return host;
        }

        static string DefaultSchemeOf(IRigScriptHost host, string g) =>
            host.EvaluateString($"String({g}.{Hull.DefaultSchemeExpr} || '')");

        // ---- the A/B contract ------------------------------------------------------------------

        [Test]
        public void DefaultSchemeIsTheHullsOwnTable()
        {
            var hull = LoadHull();
            using IRigScriptHost host = OpenRig(out string g);

            string defaultPaint = DefaultSchemeOf(host, g);
            Assert.IsNotEmpty(defaultPaint, "The rig declares no defaultPaint.");

            var table = HullPaintSchemeBaker.ReadMaterialTable(host, g, Hull, defaultPaint);

            Assert.AreEqual(hull.Ramps.Length, table.Length,
                $"The rig's default paint '{defaultPaint}' has {table.Length} materials but the " +
                $"committed hull def has {hull.Ramps.Length}. Ramps are matched BY INDEX — a " +
                "mismatch here shifts every material onto its neighbour's colours. Re-bake the hull.");

            for (int m = 0; m < table.Length; m++)
            {
                Assert.AreEqual(hull.Ramps[m].Offset, table[m].Offset,
                    $"material {m}: offset drifted from the hull def.");
                CollectionAssert.AreEqual(hull.Ramps[m].Colors, table[m].Colors,
                    $"material {m}: the rig's default paint no longer matches the committed hull " +
                    "def's ramp. 'Unset scheme = today's boat' has stopped being true — either " +
                    "re-bake the hull or the drop retuned the default.");
            }
        }

        [Test]
        public void EveryBakedSchemeMatchesTheRig()
        {
            using IRigScriptHost host = OpenRig(out string g);
            var byRigId = LoadSchemes().ToDictionary(s => s.RigPaintId, s => s);
            var listed = HullPaintSchemeBaker.ReadPaintList(host, g, Hull);

            Assert.AreEqual(listed.Count, byRigId.Count,
                $"The rig declares {listed.Count} schemes but {byRigId.Count} are baked. Re-bake.");

            foreach (var p in listed)
            {
                Assert.IsTrue(byRigId.TryGetValue(p.Id, out var scheme),
                    $"The rig declares '{p.Id}' but no baked asset carries it.");

                Assert.AreEqual(p.Label, scheme.Label, $"'{p.Id}': label drifted from the rig.");
                Assert.AreEqual(p.Note, scheme.Note, $"'{p.Id}': note drifted from the rig.");

                var table = HullPaintSchemeBaker.ReadMaterialTable(host, g, Hull, p.Id);
                Assert.AreEqual(table.Length, scheme.Ramps.Length,
                    $"'{p.Id}': baked {scheme.Ramps.Length} ramps, the rig now yields {table.Length}.");
                for (int m = 0; m < table.Length; m++)
                {
                    Assert.AreEqual(table[m].Offset, scheme.Ramps[m].Offset, $"'{p.Id}' material {m}: offset.");
                    CollectionAssert.AreEqual(table[m].Colors, scheme.Ramps[m].Colors,
                        $"'{p.Id}' material {m}: colours drifted from the rig. Re-bake the schemes.");
                }
            }
        }

        /// <summary>
        /// The order the baker writes is the order the extractor reads, so index n means the same
        /// material in a scheme table and in the hull def. Both sides enumerate the rig's own MATS
        /// object; this asserts they still agree rather than assuming JS key order is stable across
        /// two different expressions.
        /// </summary>
        [Test]
        public void SchemeMaterialOrderMatchesTheHullsOwn()
        {
            using IRigScriptHost host = OpenRig(out string g);
            string defaultPaint = DefaultSchemeOf(host, g);
            string[] reference = HullPaintSchemeBaker.ReadMaterialNames(host, g, Hull, defaultPaint);

            Assert.Contains("hull", reference, "The rig's material table has no 'hull'.");
            foreach (var p in HullPaintSchemeBaker.ReadPaintList(host, g, Hull))
                CollectionAssert.AreEqual(reference, HullPaintSchemeBaker.ReadMaterialNames(host, g, Hull, p.Id),
                    $"'{p.Id}' enumerates its materials in a different order from the default scheme. " +
                    "Ramps are positional — a reordered table repaints the wrong materials.");
        }

        // ---- the assets themselves --------------------------------------------------------------

        [Test]
        public void EverySchemeIsUsableForItsHull()
        {
            var hull = LoadHull();
            foreach (var s in LoadSchemes())
                Assert.IsTrue(s.IsUsableFor(hull),
                    $"'{s.Id}' cannot repaint '{hull.Id}': {s.ExplainUnusableFor(hull)}");
        }

        [Test]
        public void SchemeIdsAreUniqueStableAndHullQualified()
        {
            var seen = new Dictionary<string, string>();
            foreach (var s in LoadSchemes())
            {
                Assert.IsTrue(s.Id.StartsWith("paint."),
                    $"'{s.Id}' is not a paint id (CLAUDE.md §5: type.snake_case).");
                Assert.AreEqual(s.Id, s.Id.ToLowerInvariant(), $"'{s.Id}' is not snake_case.");
                Assert.IsFalse(seen.TryGetValue(s.Id, out string other),
                    $"'{s.Id}' is used by two scheme assets ({other} and {AssetDatabase.GetAssetPath(s)}). " +
                    "Ids are append-only and stable.");
                seen[s.Id] = AssetDatabase.GetAssetPath(s);

                Assert.IsNotEmpty(s.HullMeshId, $"'{s.Id}' does not say which hull it repaints.");
                Assert.IsNotEmpty(s.RigPaintId, $"'{s.Id}' does not record the rig's own scheme id.");
                Assert.IsNotEmpty(s.SourceRigPath, $"'{s.Id}' has no provenance.");
            }
        }

        /// <summary>
        /// The count the coordinator carries to the owner's register ruling: how many distinct hull
        /// schemes the kit actually yields. Asserted against the RIG so it cannot go stale, and
        /// logged so the number appears in the run rather than in a PR body someone typed.
        /// </summary>
        [Test]
        public void ReportSchemeCount()
        {
            using IRigScriptHost host = OpenRig(out string g);
            var listed = HullPaintSchemeBaker.ReadPaintList(host, g, Hull);
            var sb = new StringBuilder($"[paint kit] {listed.Count} distinct hull schemes: ");
            sb.Append(string.Join(", ", listed.Select(p => p.Id)));
            Debug.Log(sb.ToString());
            Assert.GreaterOrEqual(listed.Count, 2, "A paint kit with one scheme is not a paint kit.");
        }
    }
}
