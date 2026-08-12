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
    /// holds. Measured on all three painted hulls (2026-08-12) it does, entry for entry, including
    /// the negative offsets. If a future drop retunes a default, this goes red and says so instead
    /// of quietly repainting the fleet.</para>
    ///
    /// <para><b>⚠️ Every test here is driven off <see cref="HullPaintSchemeBaker.Fleet"/>, not off a
    /// hull named in this file.</b> That is what the small-craft drop changed: the fixture used to
    /// name the lobster boat, and adding a second hull would have needed a second fixture (and would
    /// have broken this one — <c>LoadSchemes</c> read the whole folder, so two hulls sharing a rig
    /// scheme id, as the punt and the console both do with <c>harbour-white</c>, collided). A fourth
    /// painted hull now needs one line in the baker and nothing here. The two paint APIs
    /// (<c>matsFor(id).MATS</c> and <c>palette({{scheme:id}}).mats</c>) are covered by the same
    /// assertions for the same reason.</para>
    /// </summary>
    public class HullPaintSchemeBakeTests
    {
        /// <summary>One case per painted hull, named by rig key so a failure says which boat.</summary>
        static IEnumerable<TestCaseData> Fleet() => HullPaintSchemeBaker.Fleet
            .Select(h => new TestCaseData(h.RigKey).SetName($"{{m}}({h.RigKey})"));

        static HullPaintSchemeBaker.PaintedHull HullOf(string rigKey) =>
            HullPaintSchemeBaker.Fleet.First(h => h.RigKey == rigKey);

        /// <summary>The committed hull mesh this hull's schemes repaint, found BY ID rather than by a
        /// path typed here — the baker writes that id onto every scheme, so resolving through it is
        /// the same lookup the game does.</summary>
        static HullMeshDef LoadHull(string rigKey)
        {
            string id = HullOf(rigKey).HullMeshId;
            var def = AssetDatabase
                .FindAssets($"t:{nameof(HullMeshDef)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<HullMeshDef>)
                .FirstOrDefault(d => d != null && d.Id == id);
            Assert.IsNotNull(def, $"No HullMeshDef with id '{id}' — the baker's fleet table names a " +
                                  "hull mesh that is not in the project. Bake the hull mesh first.");
            return def;
        }

        /// <summary>This hull's schemes, and only this hull's. Filtering by
        /// <see cref="HullPaintSchemeDef.HullMeshId"/> matters: the punt and the console skiff share
        /// six rig scheme ids verbatim (both call theirs 'harbour-white'), so a fixture that read the
        /// whole folder would key two different tables to one name.</summary>
        static HullPaintSchemeDef[] LoadSchemes(string rigKey)
        {
            string hullId = HullOf(rigKey).HullMeshId;
            var found = AssetDatabase
                .FindAssets($"t:{nameof(HullPaintSchemeDef)}", new[] { HullPaintSchemeBaker.SchemeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>)
                .Where(s => s != null && s.HullMeshId == hullId)
                .OrderBy(s => s.Id)
                .ToArray();
            Assert.IsNotEmpty(found,
                $"No paint schemes for '{hullId}' under {HullPaintSchemeBaker.SchemeFolder}. Run " +
                "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake hull PAINT SCHEMES…");
            return found;
        }

        /// <summary>Opens the rig shimmed exactly as the baker shims it — and resolved through the
        /// same <see cref="HullMeshFleet"/> entry, so the fixture cannot be reading one rig while the
        /// bake reads another.</summary>
        static IRigScriptHost OpenRig(string rigKey, out string global)
        {
            var hull = HullOf(rigKey);
            var fleet = HullPaintSchemeBaker.RigFor(hull);
            global = fleet.GlobalName;
            var host = RigScriptHostFactory.Create();
            string source = HullPaintSchemeBaker.ReadRigSource(fleet.ScriptPath);
            host.Execute(hull.ShimSymbols.Length == 0
                ? source
                : RigMeshExtractor.WidenExportedLiteral(
                      source, fleet.GlobalName, hull.ShimSymbols, fleet.ScriptPath));
            return host;
        }

        static string DefaultSchemeOf(IRigScriptHost host, string g, string rigKey) =>
            host.EvaluateString($"String({g}.{HullOf(rigKey).DefaultSchemeExpr} || '')");

        // ---- the A/B contract ------------------------------------------------------------------

        [TestCaseSource(nameof(Fleet))]
        public void DefaultSchemeIsTheHullsOwnTable(string rigKey)
        {
            var hull = LoadHull(rigKey);
            using IRigScriptHost host = OpenRig(rigKey, out string g);

            string defaultPaint = DefaultSchemeOf(host, g, rigKey);
            Assert.IsNotEmpty(defaultPaint, $"'{rigKey}' declares no default scheme.");

            var table = HullPaintSchemeBaker.ReadMaterialTable(host, g, HullOf(rigKey), defaultPaint);

            Assert.AreEqual(hull.Ramps.Length, table.Length,
                $"'{rigKey}': the rig's default paint '{defaultPaint}' has {table.Length} materials " +
                $"but the committed hull def has {hull.Ramps.Length}. Ramps are matched BY INDEX — a " +
                "mismatch here shifts every material onto its neighbour's colours. Re-bake the hull.");

            for (int m = 0; m < table.Length; m++)
            {
                Assert.AreEqual(hull.Ramps[m].Offset, table[m].Offset,
                    $"'{rigKey}' material {m}: offset drifted from the hull def.");
                CollectionAssert.AreEqual(hull.Ramps[m].Colors, table[m].Colors,
                    $"'{rigKey}' material {m}: the rig's default paint no longer matches the committed " +
                    "hull def's ramp. 'Unset scheme = today's boat' has stopped being true — either " +
                    "re-bake the hull or the drop retuned the default.");
            }
        }

        [TestCaseSource(nameof(Fleet))]
        public void EveryBakedSchemeMatchesTheRig(string rigKey)
        {
            using IRigScriptHost host = OpenRig(rigKey, out string g);
            var byRigId = LoadSchemes(rigKey).ToDictionary(s => s.RigPaintId, s => s);
            var listed = HullPaintSchemeBaker.ReadPaintList(host, g, HullOf(rigKey));

            Assert.AreEqual(listed.Count, byRigId.Count,
                $"'{rigKey}': the rig declares {listed.Count} schemes but {byRigId.Count} are baked. Re-bake.");

            foreach (var p in listed)
            {
                Assert.IsTrue(byRigId.TryGetValue(p.Id, out var scheme),
                    $"'{rigKey}': the rig declares '{p.Id}' but no baked asset carries it.");

                Assert.AreEqual(p.Label, scheme.Label, $"'{rigKey}'/'{p.Id}': label drifted from the rig.");
                Assert.AreEqual(p.Note, scheme.Note, $"'{rigKey}'/'{p.Id}': note drifted from the rig.");

                var table = HullPaintSchemeBaker.ReadMaterialTable(host, g, HullOf(rigKey), p.Id);
                Assert.AreEqual(table.Length, scheme.Ramps.Length,
                    $"'{rigKey}'/'{p.Id}': baked {scheme.Ramps.Length} ramps, the rig now yields {table.Length}.");
                for (int m = 0; m < table.Length; m++)
                {
                    Assert.AreEqual(table[m].Offset, scheme.Ramps[m].Offset,
                        $"'{rigKey}'/'{p.Id}' material {m}: offset.");
                    CollectionAssert.AreEqual(table[m].Colors, scheme.Ramps[m].Colors,
                        $"'{rigKey}'/'{p.Id}' material {m}: colours drifted from the rig. Re-bake the schemes.");
                }
            }
        }

        /// <summary>
        /// The order the baker writes is the order the extractor reads, so index n means the same
        /// material in a scheme table and in the hull def. Both sides enumerate the rig's own MATS
        /// object; this asserts they still agree rather than assuming JS key order is stable across
        /// two different expressions.
        ///
        /// <para>It deliberately does NOT look for a material called 'hull'. Index 0 is whatever the
        /// rig's first material happens to be — 'hull' on the lobster boat, 'paint' on both small
        /// craft — and asserting a name here would have been a lobster-shaped assumption dressed up
        /// as a contract. What must hold is that the ORDER is the same for every scheme of a hull,
        /// which is what makes the table positional.</para>
        /// </summary>
        [TestCaseSource(nameof(Fleet))]
        public void SchemeMaterialOrderMatchesTheHullsOwn(string rigKey)
        {
            using IRigScriptHost host = OpenRig(rigKey, out string g);
            var hull = HullOf(rigKey);
            string defaultPaint = DefaultSchemeOf(host, g, rigKey);
            string[] reference = HullPaintSchemeBaker.ReadMaterialNames(host, g, hull, defaultPaint);

            Assert.IsNotEmpty(reference, $"'{rigKey}' has an empty material table.");
            CollectionAssert.AllItemsAreNotNull(reference);
            CollectionAssert.AllItemsAreUnique(reference,
                $"'{rigKey}' names two materials the same — ramps are positional and the names are " +
                "how the face packer resolves them, so a duplicate silently sends faces to one index.");

            foreach (var p in HullPaintSchemeBaker.ReadPaintList(host, g, hull))
                CollectionAssert.AreEqual(reference,
                    HullPaintSchemeBaker.ReadMaterialNames(host, g, hull, p.Id),
                    $"'{rigKey}'/'{p.Id}' enumerates its materials in a different order from the " +
                    "default scheme. Ramps are positional — a reordered table repaints the wrong materials.");
        }

        // ---- the assets themselves --------------------------------------------------------------

        [TestCaseSource(nameof(Fleet))]
        public void EverySchemeIsUsableForItsHull(string rigKey)
        {
            var hull = LoadHull(rigKey);
            foreach (var s in LoadSchemes(rigKey))
                Assert.IsTrue(s.IsUsableFor(hull),
                    $"'{s.Id}' cannot repaint '{hull.Id}': {s.ExplainUnusableFor(hull)}");
        }

        /// <summary>
        /// ⚠️ The arity gate is NOT enough to keep two hulls' tables apart, and this pins that.
        /// The punt and the lobster boat both have exactly ELEVEN materials, so a punt scheme handed
        /// to the lobster passes every structural check <see cref="HullPaintSchemeDef.IsUsableFor"/>
        /// makes except the hull id — and would repaint her silently and wrongly. Measured, not
        /// assumed: if a future rig change made the counts differ this test would still pass, but for
        /// a weaker reason, so it asserts the REFUSAL rather than the coincidence.
        /// </summary>
        [Test]
        public void ASchemeIsRefusedByEveryHullButItsOwn()
        {
            var hulls = HullPaintSchemeBaker.Fleet.Select(h => LoadHull(h.RigKey)).ToArray();
            if (hulls.Length < 2) Assert.Ignore("Only one painted hull — nothing to cross.");

            foreach (var h in HullPaintSchemeBaker.Fleet)
            {
                var mine = LoadHull(h.RigKey);
                foreach (var scheme in LoadSchemes(h.RigKey))
                    foreach (var other in hulls.Where(x => x.Id != mine.Id))
                        Assert.IsFalse(scheme.IsUsableFor(other),
                            $"'{scheme.Id}' was baked for '{mine.Id}' but is accepted by '{other.Id}'. " +
                            "Ramps match BY INDEX, so this would repaint another hull's materials with " +
                            "this one's colours and look like a shader bug rather than a wrong asset.");
            }
        }

        [Test]
        public void SchemeIdsAreUniqueStableAndHullQualified()
        {
            var seen = new Dictionary<string, string>();
            foreach (var h in HullPaintSchemeBaker.Fleet)
                foreach (var s in LoadSchemes(h.RigKey))
                {
                    Assert.IsTrue(s.Id.StartsWith("paint."),
                        $"'{s.Id}' is not a paint id (CLAUDE.md §5: type.snake_case).");
                    Assert.AreEqual(s.Id, s.Id.ToLowerInvariant(), $"'{s.Id}' is not snake_case.");
                    Assert.IsTrue(s.Id.StartsWith(h.IdPrefix),
                        $"'{s.Id}' does not carry '{h.RigKey}'s id prefix '{h.IdPrefix}'. Ids are " +
                        "hull-qualified because the TABLE is hull-specific.");
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
        /// Two hulls MAY share a rig scheme id — the punt and the console skiff both call their
        /// default 'harbour-white', deliberately, so a harbour of mixed boats reads as one fleet.
        /// What must not collide is the ASSET id or the file, which is what the hull-qualified prefix
        /// buys. Stated as a test because the near-miss is invisible: the two tables differ (11
        /// materials against 13) while the rig's own name for them is identical.
        /// </summary>
        [Test]
        public void HullsMayShareARigSchemeNameButNeverAnAssetId()
        {
            var byRigId = new Dictionary<string, List<string>>();
            foreach (var h in HullPaintSchemeBaker.Fleet)
                foreach (var s in LoadSchemes(h.RigKey))
                {
                    if (!byRigId.TryGetValue(s.RigPaintId, out var l))
                        byRigId[s.RigPaintId] = l = new List<string>();
                    l.Add(s.Id);
                }

            foreach (var kv in byRigId)
                CollectionAssert.AllItemsAreUnique(kv.Value,
                    $"rig scheme '{kv.Key}' baked two assets with the same id: {string.Join(", ", kv.Value)}");
        }

        /// <summary>
        /// The count the coordinator carries to the owner's register ruling: how many distinct hull
        /// schemes the kit actually yields. Asserted against the RIG so it cannot go stale, and
        /// logged so the number appears in the run rather than in a PR body someone typed.
        /// </summary>
        [Test]
        public void ReportSchemeCount()
        {
            var sb = new StringBuilder("[paint kit] ");
            int total = 0;
            foreach (var h in HullPaintSchemeBaker.Fleet)
            {
                using IRigScriptHost host = OpenRig(h.RigKey, out string g);
                var listed = HullPaintSchemeBaker.ReadPaintList(host, g, h);
                Assert.GreaterOrEqual(listed.Count, 2,
                    $"'{h.RigKey}': a paint kit with one scheme is not a paint kit.");
                total += listed.Count;
                sb.Append($"\n  {h.RigKey,-14} {listed.Count,2} schemes: ")
                  .Append(string.Join(", ", listed.Select(p => p.Id)));
            }
            sb.Append($"\n  TOTAL {total} distinct hull schemes across " +
                      $"{HullPaintSchemeBaker.Fleet.Length} hulls.");
            Debug.Log(sb.ToString());
        }
    }
}
