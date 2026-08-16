using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0022 phase 8 — one rig, many hulls.</b>
    ///
    /// <para>Every hull baked before 2026-08-13 is one boat per rig file, and its geometry is the
    /// static <c>F</c> array the rig builds once at load. The 2026-08-13 fleet pack brought two
    /// GENERATORS — <c>lobsterBoatVariantsIsoRig.js</c> (18 hulls) and <c>zodiacIsoRig.js</c>
    /// (2 builds) — whose faces come from a private function of a variant descriptor, with no
    /// <c>F</c> anywhere. <see cref="RigHullExtraction"/> is how a fleet entry says WHICH hull it
    /// means; this fixture is what keeps that honest.</para>
    ///
    /// <para><b>Two obligations, and the second is the one with teeth.</b> The new path must reach
    /// every variant — but far more importantly, the OLD path must be untouched, because eleven
    /// committed hulls go through it. Both are asserted below, and the second is asserted for all
    /// eleven rather than for a sample.</para>
    ///
    /// <para>Pure CPU through the repo's own V8 host, so CI adjudicates all of it.</para>
    /// </summary>
    public class RigMeshVariantExtractionTests
    {
        const string VariantRig = "docs/art/rigs/lobsterBoatVariantsIsoRig.js";
        const string VariantGlobal = "LobsterBoatVariantsIso";

        /// <summary>The symbol <see cref="RigMeshSymbols.Reconstructions"/> shims in to reach the
        /// generator's private builder. Deliberately NOT named after any rig private — see
        /// <see cref="TheShimNameIsNotOneTheRigsAlreadyUse"/>.</summary>
        const string ShimName = "variantFaces";

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

        static string FaceExpr(string size, string style, string region) =>
            $"{ShimName}({{size:'{size}',style:'{style}',region:'{region}'}})";

        static RigHullExtraction Extraction(string size, string style, string region) =>
            new RigHullExtraction
            {
                FaceExpression = FaceExpr(size, style, region),
                ExtraSymbols = new[] { ShimName },
            };

        /// <summary>
        /// A stable fingerprint of a face list: material, both biases and every vertex, in order.
        ///
        /// <para>⚠️ <b>Face COUNT is not an oracle and this is not a hypothetical.</b> Measured
        /// 2026-08-13: <c>inshore_hardtop_northumberland</c> and <c>inshore_hardtop_fundy</c> are
        /// both 637 faces and are different boats. A distinctness check that counted would have
        /// passed while the bake shipped one hull twice.</para>
        /// </summary>
        static string Fingerprint(RigMeshData data)
        {
            var sb = new StringBuilder();
            foreach (RigFace f in data.Faces)
            {
                // Mat is an INDEX into data.Materials, so the material NAME is what travels — an
                // index would compare equal across two rigs whose tables happen to be ordered alike.
                sb.Append(data.Materials[f.Mat].Name).Append('|')
                  .Append(f.B.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append(f.Db.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                foreach (Vector3d v in f.V)
                    sb.Append(v.X.ToString("F9", CultureInfo.InvariantCulture)).Append(',')
                      .Append(v.Y.ToString("F9", CultureInfo.InvariantCulture)).Append(',')
                      .Append(v.Z.ToString("F9", CultureInfo.InvariantCulture)).Append(';');
            }
            return sb.ToString();
        }

        // ================= the obligation with teeth: the old path is untouched =================

        /// <summary>
        /// <b>Every hull that was baking on 2026-08-12 still extracts to the identical face list.</b>
        ///
        /// <para>The change added a parameter to the method all eleven go through. This asserts, per
        /// hull, that passing no extraction and passing a NON-variant extraction produce the same
        /// geometry — i.e. that the new arm is genuinely unreachable unless a variant is named, and
        /// that the default arm is the code path it always was.</para>
        ///
        /// <para>This is the cheap half of the control. The other half is the committed
        /// <c>HullMeshDef</c> field-compare after a real re-bake, which is what actually adjudicates
        /// the shipped assets — a committed SHEET is not admissible (the small-craft sheets are known
        /// stale pass-1 bakes).</para>
        /// </summary>
        [Test]
        public void EveryHullBakedBeforeGeneratorsExisted_TakesTheIdenticalPath()
        {
            // ⚠️ NARROWED DELIBERATELY, which is what the message below used to ask for. When the 18
            // lobster variants joined the fleet this swept HullMeshFleet.Hulls and asserted every
            // entry's Extraction was null — so it went red on the bake, as designed. The control it
            // is actually making is about the ELEVEN that were baked before generators existed, and
            // HullMeshFleet.OneHullPerRig is exactly that set, named. Widening the sweep back to
            // Hulls would only mean asserting that a variant takes the static path, which is the
            // opposite of true.
            var report = new StringBuilder();
            foreach (FleetHull hull in HullMeshFleet.OneHullPerRig)
            {
                Assert.IsNull(hull.Extraction,
                    $"{hull.Key} carries a hull extraction. Every hull in the table on 2026-08-12 " +
                    "took the static F array; if this one is now a generator variant, it belongs in " +
                    "the variant block rather than in OneHullPerRig — move it deliberately.");

                string before, after;
                using (IRigScriptHost h = RigScriptHostFactory.Create())
                    before = Fingerprint(RigMeshExtractor.ExtractFrom(h, hull.ScriptPath, hull.GlobalName));
                using (IRigScriptHost h = RigScriptHostFactory.Create())
                    after = Fingerprint(RigMeshExtractor.ExtractFrom(
                        h, hull.ScriptPath, hull.GlobalName, hull: new RigHullExtraction()));

                Assert.AreEqual(before, after,
                    $"{hull.Key} ({hull.ScriptPath}) extracts differently when handed an empty " +
                    "RigHullExtraction. An extraction that names no variant must be exactly the same " +
                    "as none at all — otherwise the eleven committed hulls are not on the path they " +
                    "were baked from.");

                report.Append(hull.Key).Append(' ');
            }

            // ⚠️ THE CONTROL IS THE ELEVEN, NOT THE COUNT — and writing it as a count is what made
            // this red on a correct change.
            //
            // It used to read `Assert.AreEqual(11, OneHullPerRig.Count)`, and its own message asked
            // the next person to "re-read the field-compare gate before landing a twelfth". A
            // twelfth landed on 2026-08-16: the reshaped sport skiff, one hull per rig exactly like
            // the eleven, but baked long AFTER generators existed, so she is not part of the control
            // this test is making. A bare count cannot tell her arrival apart from one of the
            // original eleven going MISSING — which is the failure this exists to catch. Naming them
            // can, and the gate the old message pointed at is met: her committed def is compared
            // field-for-field against a fresh extraction by
            // HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig.
            //
            // The per-hull identity check above still sweeps the WHOLE set, newcomer included — that
            // an extraction naming no variant is exactly the same as none at all is a real check
            // whenever the hull was baked.
            string[] bakingOn20260812 =
            {
                "dory", "punt", "consoleSkiff", "sportSkiff", "capeIslander", "lobsterBoat",
                "sideDragger", "sternTrawler", "sternTrawlerMk2", "coastalPacket", "tanker",
            };
            CollectionAssert.IsSubsetOf(bakingOn20260812,
                HullMeshFleet.OneHullPerRig.Select(h => h.Key).ToList(),
                $"One of the eleven hulls this control was measured against has left " +
                $"OneHullPerRig ({report}). A hull leaving the static-F set is either a deliberate " +
                "re-categorisation — say so — or this control silently shrinking, which is how the " +
                "eleven quietly stop being proven unchanged.");

            CollectionAssert.IsSupersetOf(HullMeshFleet.Hulls.Select(h => h.Key).ToList(),
                HullMeshFleet.OneHullPerRig.Select(h => h.Key).ToList(),
                "OneHullPerRig has drifted out of Hulls, so this control set is no longer a subset " +
                "of the fleet it is meant to be controlling.");
        }

        /// <summary>
        /// Every hull that IS a generator cell names a variant — the mirror of the control above,
        /// and the thing that makes it a partition rather than two lists that happen not to overlap.
        ///
        /// <para>Worth its own assert because the failure it catches is silent: a variant row whose
        /// <c>Extraction</c> went null would bake through the static-F path, and since the rig has no
        /// <c>F</c> the run reports FAILED — but a row that kept a NON-NULL extraction naming the
        /// wrong cell bakes a real, plausible boat under another hull's id. The distinctness sweep
        /// below is what catches the second; this catches the first at the table rather than at the
        /// bake.</para>
        /// </summary>
        [Test]
        public void EveryGeneratorCellInTheFleet_NamesTheVariantItMeans()
        {
            var variants = HullMeshFleet.Hulls.Where(h => h.IsVariant).ToList();
            Assert.AreEqual(HullMeshFleet.Hulls.Count - HullMeshFleet.OneHullPerRig.Count,
                            variants.Count,
                            "Every hull is either one-per-rig or a generator cell; something is neither.");

            foreach (FleetHull hull in variants)
            {
                Assert.IsNotEmpty(hull.Extraction.FaceExpression, $"{hull.Key}: empty face expression.");
                CollectionAssert.Contains(hull.Extraction.ExtraSymbols, ShimName,
                    $"{hull.Key}: her face expression calls {ShimName}() but does not ask for it to " +
                    "be shimmed, so the extractor would evaluate an undefined function.");
            }
        }

        /// <summary>A hull that names no variant records no variant — so the eleven committed defs
        /// keep an empty <c>SourceFaceBuilder</c> and their bytes do not churn.</summary>
        [Test]
        public void AStaticHull_RecordsNoVariantProvenance()
        {
            FleetHull dory = HullMeshFleet.Get("dory");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, dory.ScriptPath, dory.GlobalName);

            Assert.IsEmpty(data.SourceFaceExpression,
                "A static-F hull must record an empty face expression. HullMeshDef.SourceFaceBuilder " +
                "is written from this, and a non-empty value would rewrite eleven committed assets.");
        }

        // ===================== the new path reaches every variant ==============================

        /// <summary>
        /// All eighteen lobster variants extract, and all eighteen are DIFFERENT BOATS.
        ///
        /// <para>Distinctness is over the full face fingerprint, not the count — see
        /// <see cref="Fingerprint"/> for the measured pair that makes counting inadmissible.</para>
        /// </summary>
        [Test]
        public void EveryLobsterVariant_ExtractsItsOwnGeometry()
        {
            var byPrint = new Dictionary<string, string>(StringComparer.Ordinal);
            var counts = new List<string>();

            foreach ((string sz, string st, string rg) in Variants())
            {
                string key = $"{sz}_{st}_{rg}";
                using IRigScriptHost host = RigScriptHostFactory.Create();
                var data = RigMeshExtractor.ExtractFrom(
                    host, VariantRig, VariantGlobal, hull: Extraction(sz, st, rg));

                Assert.Greater(data.Faces.Count, 0, $"{key} extracted no faces.");
                counts.Add($"{key}={data.Faces.Count}");

                string print = Fingerprint(data);
                byPrint.TryGetValue(print, out string clash);
                Assert.IsFalse(byPrint.ContainsKey(print),
                    $"{key} extracted the SAME geometry as {clash}. " +
                    "Either the variant descriptor did not reach the generator (the rigs resolve an " +
                    "unknown id to their DEFAULT rather than failing — see " +
                    $"{nameof(AnUnknownVariantId_FallsBackToTheDefaultHullSilently)}), or two of the " +
                    "eighteen genuinely collapse and the bake list should say so.");
                byPrint[print] = key;
            }

            Assert.AreEqual(18, byPrint.Count,
                $"Expected 18 distinct hulls, got {byPrint.Count}. Counts: {string.Join(", ", counts)}");
        }

        /// <summary>A variant hull records WHICH variant it is — the only thing that tells eighteen
        /// defs sharing one rig path apart.</summary>
        [Test]
        public void AVariantHull_RecordsWhichVariantItIs()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(
                host, VariantRig, VariantGlobal, hull: Extraction("offshore", "hardtop", "fundy"));

            Assert.AreEqual($"{VariantGlobal}.{FaceExpr("offshore", "hardtop", "fundy")}",
                            data.SourceFaceExpression,
                "The face expression must survive onto the data, because HullMeshDef.SourceFaceBuilder " +
                "is written from it and is the ONLY field distinguishing eighteen defs that share a " +
                "SourceRigPath.");
        }

        // ============================ the traps, pinned ========================================

        /// <summary>
        /// ⚠️ <b>The trap that makes a count-based oracle dangerous.</b> Both pack generators resolve
        /// an unrecognised axis id to their DEFAULT rather than throwing, so a typo in a variant
        /// descriptor does not fail the bake — it silently bakes the default hull under another
        /// hull's id. Measured, not assumed; this is why
        /// <see cref="EveryLobsterVariant_ExtractsItsOwnGeometry"/> hashes geometry.
        /// </summary>
        [Test]
        public void AnUnknownVariantId_FallsBackToTheDefaultHullSilently()
        {
            string defaults, typo;
            using (IRigScriptHost h = RigScriptHostFactory.Create())
                defaults = Fingerprint(RigMeshExtractor.ExtractFrom(
                    h, VariantRig, VariantGlobal, hull: Extraction("standard", "hardtop", "northumberland")));
            using (IRigScriptHost h = RigScriptHostFactory.Create())
                typo = Fingerprint(RigMeshExtractor.ExtractFrom(
                    h, VariantRig, VariantGlobal, hull: Extraction("standrad", "hardtop", "northumberlnad")));

            Assert.AreEqual(defaults, typo,
                "This rig used to swallow a misspelt variant id and hand back its default hull. If it " +
                "now FAILS instead, that is strictly better — delete this test and say so in the " +
                "distinctness test's message, which currently cites it as the reason for hashing.");
        }

        /// <summary>
        /// The shim name must not be a name the rigs already use.
        ///
        /// <para>The widening adds a PROPERTY to the exported literal while the expression is
        /// evaluated in the rig's own closure, so a shim named after a rig private happens to resolve
        /// — <c>zodiacIsoRig.js</c> has a private <c>facesOf</c> and it would work. It is still two
        /// bindings with one name in one file, which is a trap laid for the next reader.</para>
        /// </summary>
        [Test]
        public void TheShimNameIsNotOneTheRigsAlreadyUse()
        {
            foreach (var kv in RigMeshSymbols.Reconstructions)
            {
                if (!kv.Value.ContainsKey(ShimName)) continue;

                string full = Path.Combine(RepoRoot, "docs", "art", "rigs", kv.Key);
                Assert.IsTrue(File.Exists(full), $"{kv.Key} has a {ShimName} reconstruction but is not " +
                                                 "committed under docs/art/rigs/.");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(full));
                string global = GlobalOf(kv.Key);

                Assert.IsFalse(
                    host.EvaluateBool($"typeof {global} === 'object' && {global} !== null && " +
                                      $"typeof {global}.{ShimName} !== 'undefined'"),
                    $"{kv.Key} already exports `{ShimName}`. The shim would then be shadowing a real " +
                    "export rather than supplying a missing one — pick another name, or (far better) " +
                    "drop the reconstruction and read the rig's own.");

                Assert.IsFalse(File.ReadAllText(full).Contains($"function {ShimName}"),
                    $"{kv.Key} declares a private `function {ShimName}`. The widening would still " +
                    "resolve, but one name would mean two things in one file. Rename the shim.");
            }
        }

        static string GlobalOf(string rigFileName)
        {
            foreach (FleetHull h in HullMeshFleet.Hulls)
                if (Path.GetFileName(h.ScriptPath) == rigFileName) return h.GlobalName;
            // Generators are not in the fleet table until their bake lands, so fall back to the
            // catalog, which registers a rig the moment its source is committed.
            foreach (var kv in RigCatalog.Entries)
                if (Path.GetFileName(kv.Value.ScriptPath) == rigFileName) return kv.Value.GlobalName;
            throw new AssertionException(
                $"No global name known for {rigFileName}. A rig with a reconstruction should be in " +
                "RigCatalog at least — that is where an imported rig is declared.");
        }

        /// <summary>A fitting and a hull variant are alternative face sources; asking for both is a
        /// mistake, and a silent precedence rule would make the bake mean something nobody chose.
        /// </summary>
        [Test]
        public void AFittingAndAVariant_CannotBothBeAskedFor()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var ex = Assert.Throws<ArgumentException>(() => RigMeshExtractor.ExtractFrom(
                host, VariantRig, VariantGlobal,
                prop: new RigPropExtraction { FaceBuilderCall = "nope()" },
                hull: Extraction("standard", "hardtop", "northumberland")));

            StringAssert.Contains("exactly one", ex.Message);
        }
    }
}
