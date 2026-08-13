using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The lobster-boat variant generator</b> — one rig, eighteen hulls (fleet rig pack,
    /// 2026-08-12). This fixture holds the measurements the import was decided on, so they stay
    /// true rather than staying in a PR description.
    ///
    /// <para>Everything here runs on the CPU through the repo's own V8 host, so CI adjudicates it.
    /// </para>
    /// </summary>
    public class LobsterVariantRigTests
    {
        const string RigPath = "docs/art/rigs/lobsterBoatVariantsIsoRig.js";
        const string Global = "LobsterBoatVariantsIso";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static readonly string[] Sizes = { "inshore", "standard", "offshore" };
        static readonly string[] Styles = { "open", "hardtop" };
        static readonly string[] Regions = { "northumberland", "fundy", "newfoundland" };

        static IEnumerable<(string size, string style, string region)> Variants()
        {
            foreach (string sz in Sizes)
            foreach (string st in Styles)
            foreach (string rg in Regions)
                yield return (sz, st, rg);
        }

        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, RigPath)));
            return host;
        }

        static string Sha(byte[] b)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(b));
        }

        /// <summary>
        /// The three axis lists above are hard-coded, so they can go stale silently: a drop that adds
        /// a fourth region would leave this fixture sweeping the old eighteen, passing, and saying
        /// nothing about the new six. Check them against the rig's own tables first — the sweep is
        /// only worth its runtime if it is sweeping everything.
        /// </summary>
        static void AssertTheAxesAreStillTheOnesThisFixtureSweeps(IRigScriptHost host)
        {
            void Same(string table, string[] mine)
            {
                string theirs = host.EvaluateString($"{Global}.{table}.map(x=>x.id).join(',')");
                Assert.AreEqual(string.Join(",", mine), theirs,
                    $"{Global}.{table} is no longer what this fixture sweeps. The rig's option axes " +
                    "changed — update the lists here (and HullMeshFleet.NotHulls, and the follow-up's " +
                    "bake list) so the new variants are actually measured.");
            }

            Same("SIZES", Sizes);
            Same("STYLES", Styles);
            Same("REGIONS", Regions);
        }

        // ---- the sidecars she has not landed with yet ---------------------------------------------

        /// <summary>
        /// <b>D1, and it is a TRIPWIRE, not a complaint.</b> Her eighteen sidecars shipped with no
        /// <c>derivedFromRigSha256</c> at all — the field is absent, not wrong — which
        /// <see cref="DeckSidecarReader"/> treats exactly like a stale hash (<c>RigHashMatch.None</c>
        /// → "Not imported"). The pack README says sidecars carry one; hers do not.
        ///
        /// <para>The omission is in the owner's GENERATOR, not in the packaging, and this test is how
        /// that is established rather than assumed: it asks the rig itself. Per ADR 0021 a defect in
        /// the owner's file is recorded and pinned, never patched — so when her sidecars land they
        /// will land with the hash stamped on our side, and this test going RED is the signal that a
        /// drop has fixed it and the stamping step can be deleted.</para>
        ///
        /// <para>Her sidecars are deliberately NOT in this PR. A committed sidecar requires a deck
        /// Def (<c>EverySidecarHasACommittedDeckDef</c>), and a committed deck Def requires a visual
        /// that wears it (<c>EveryMeasuredHullsVisualPointsAtHerOwnDeck</c>) — which requires a
        /// picture, which for her requires the per-variant mesh extraction she is excluded for. The
        /// whole chain lands together.</para>
        /// </summary>
        [Test]
        public void TheRigsOwnGenerator_StillOmitsProvenance()
        {
            using IRigScriptHost host = Host();
            bool emits = host.EvaluateBool(
                $"'derivedFromRigSha256' in {Global}.gameplayGeometry(" +
                "{size:'standard',style:'hardtop',region:'fundy'})");

            Assert.IsFalse(emits,
                "lobsterBoatVariantsIsoRig.js now emits derivedFromRigSha256 itself. Good news: her " +
                "sidecars can be imported verbatim, with no stamping step on our side — and this " +
                "test has done its job and can go.");
        }

        // ---- the option axes ---------------------------------------------------------------------

        /// <summary>
        /// <b>All three option axes are geometry — 18 of 18 cells distinct.</b> Size, style AND
        /// region each change the boat, so each is a mesh and none is a colourway. Hashed at
        /// heading SE (the lit side) and NW (the shaded one), because a difference that hid on one
        /// would have to hide on both.
        ///
        /// <para>Worth pinning rather than assuming: a redundant axis is the one thing that turns
        /// eighteen bakes into nine, and the fuel kit and the shop counter both shipped axes that
        /// rendered identically. This one does not.</para>
        /// </summary>
        [Test]
        public void TheEighteenVariants_AreAllGeometricallyDistinct()
        {
            using IRigScriptHost host = Host();
            AssertTheAxesAreStillTheOnesThisFixtureSweeps(host);

            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var dupes = new List<string>();

            foreach (var (size, style, region) in Variants())
            {
                string id = $"{size}_{style}_{region}";
                var cells = new[] { 3, 7 }.Select(d => host.EvaluateBytes(
                    $"{Global}.render({d},{{size:'{size}',style:'{style}',region:'{region}',elev:40}})"))
                    .ToArray();

                // Vacuity guard: distinctness over eighteen EMPTY cells is also "all distinct" for a
                // hash that never sees a boat. Every cell must actually contain one.
                foreach (byte[] cell in cells)
                {
                    Assert.IsNotEmpty(cell, $"{id}: the rig returned no pixels at all.");
                    int opaque = 0;
                    for (int i = 3; i < cell.Length; i += 4) if (cell[i] != 0) opaque++;
                    Assert.Greater(opaque, 1000,
                                   $"{id}: only {opaque} opaque px — that is not a 8.6–14.6 m boat.");
                }

                string key = string.Join("|", cells.Select(Sha));
                if (seen.TryGetValue(key, out string other)) dupes.Add($"{id} === {other}");
                else seen[key] = id;
            }

            Assert.IsEmpty(dupes,
                "Two variant cells render identically, so one of them is not a hull — it is a " +
                "duplicate bake: " + string.Join(", ", dupes) + ".");
            Assert.AreEqual(18, seen.Count, "Expected 18 distinct variant cells.");
        }

        /// <summary>
        /// <b>Her twelve paints move no vertex.</b> Every scheme leaves the alpha channel untouched
        /// against the default, so the silhouette — and therefore the mesh — is paint-independent
        /// and ONE bake serves all twelve. The same property #497 established for the hero hull.
        /// </summary>
        [Test]
        public void HerTwelvePaints_MoveNoVertex()
        {
            using IRigScriptHost host = Host();
            const string V = "size:'standard',style:'hardtop',region:'fundy',elev:40";

            string[] paints = host.EvaluateString($"{Global}.PAINTS.map(p=>p.id).join(',')").Split(',');
            Assert.AreEqual(12, paints.Length, "Expected 12 paint schemes.");

            byte[] baseline = host.EvaluateBytes($"{Global}.render(3,{{{V}}})");
            var moved = new List<string>();
            var inert = new List<string>();

            foreach (string p in paints)
            {
                byte[] painted = host.EvaluateBytes($"{Global}.render(3,{{{V},paint:'{p}'}})");
                Assert.AreEqual(baseline.Length, painted.Length, $"{p}: cell size changed.");

                bool alphaMoved = false, rgbChanged = false;
                for (int i = 0; i < painted.Length; i += 4)
                {
                    if (painted[i + 3] != baseline[i + 3]) alphaMoved = true;
                    if (painted[i] != baseline[i] || painted[i + 1] != baseline[i + 1] ||
                        painted[i + 2] != baseline[i + 2]) rgbChanged = true;
                }

                if (alphaMoved) moved.Add(p);

                // ⚠️ The vacuity guard, and it is the point of the test rather than a nicety. "No
                // alpha moved" is also what you get from a `paint` option the rig silently ignores —
                // the fuel kit shipped exactly that, an axis whose values rendered identically. So
                // every scheme except the default must be SEEN to repaint the hull; if it is not,
                // this fixture is measuring nothing and would keep passing after paint broke.
                if (!rgbChanged && p != "gelcoat") inert.Add(p);
            }

            Assert.IsEmpty(moved,
                "These schemes changed the silhouette, so paint is NOT colour-only on this hull and " +
                "one mesh can no longer serve every scheme: " + string.Join(", ", moved) + ".");

            Assert.IsEmpty(inert,
                "These schemes changed NOTHING — the rig is ignoring the `paint` option, so the " +
                "alpha check above proved nothing: " + string.Join(", ", inert) + ".");
        }

        /// <summary>
        /// <b>Her paint table is the hero hull's, verbatim</b> — all twelve ids and all twelve
        /// resolved ramps. So the committed <c>paint.lobster_*</c> scheme Defs cover her too, and
        /// nobody needs to author a second table that could drift from the first.
        ///
        /// <para>This is the one claim the pack README makes about her that measurement confirms
        /// outright, which is exactly why it is worth a test: the day the two tables diverge, the
        /// shared Defs quietly stop meaning the same thing on the two hulls.</para>
        /// </summary>
        [Test]
        public void HerPaintTable_IsTheHeroHullsVerbatim()
        {
            using IRigScriptHost host = Host();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, "docs/art/rigs/lobsterBoatIsoRig.js")));

            Assert.AreEqual(host.EvaluateString("LobsterBoatIso.PAINTS.map(p=>p.id).join(',')"),
                            host.EvaluateString($"{Global}.PAINTS.map(p=>p.id).join(',')"),
                            "Her paint ids are no longer the hero hull's.");

            string mismatched = host.EvaluateString(
                "LobsterBoatIso.PAINTS.map(p=>p.id).filter(function(id){" +
                $"return JSON.stringify(LobsterBoatIso.paintRamps(id))!==JSON.stringify({Global}.paintRamps(id));" +
                "}).join(',')");

            Assert.IsEmpty(mismatched,
                $"These schemes resolve to different ramps on the two hulls: {mismatched}. They share " +
                "the paint.lobster_* Defs, so a yard painting both would no longer get one colour.");
        }
    }
}
