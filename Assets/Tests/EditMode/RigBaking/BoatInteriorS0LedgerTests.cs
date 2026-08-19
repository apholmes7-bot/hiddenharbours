using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The S0 adjudication ledger: how it parses, how a derived stem inherits its family's verdict, and
    /// what the COMMITTED ledger currently says.
    ///
    /// <para>That last group is the unusual one and it is deliberate. The verdicts are the intake's
    /// answer about drop quality, arrived at by hashing every version of nine rigs across the whole
    /// repository history — work an editor session cannot redo. Pinning them here means flipping a
    /// verdict is a visible, deliberate edit to a test, and not something that happens by accident to a
    /// JSON file nobody reads.</para>
    /// </summary>
    public class BoatInteriorS0LedgerTests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static string Ledger(string hulls) =>
            "{\n  \"schema\": \"hidden-harbours/boat-interior-s0@1\",\n  \"hulls\": [\n" + hulls + "\n  ]\n}";

        // ---- parsing --------------------------------------------------------------------------------

        [Test]
        public void EachVerdictWordParsesToItsOwnValue()
        {
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"a\", \"verdict\": \"CLEAN\" },\n" +
                "    { \"hull_stem\": \"b\", \"verdict\": \"STALE-FATAL\" },\n" +
                "    { \"hull_stem\": \"c\", \"verdict\": \"REFUSED-PIN\" },\n" +
                "    { \"hull_stem\": \"e\", \"verdict\": \"FORKED-RIG\" },\n" +
                "    { \"hull_stem\": \"d\", \"verdict\": \"something new\" }"));

            Assert.AreEqual(BoatInteriorS0Verdict.Clean, map["a"].Verdict);
            Assert.AreEqual(BoatInteriorS0Verdict.StaleFatal, map["b"].Verdict);
            Assert.AreEqual(BoatInteriorS0Verdict.RefusedPin, map["c"].Verdict);
            Assert.AreEqual(BoatInteriorS0Verdict.ForkedRig, map["e"].Verdict);
            Assert.AreEqual(BoatInteriorS0Verdict.Unknown, map["d"].Verdict,
                            "a word this reader does not know is UNKNOWN, which does not build");
        }

        [Test]
        public void OnlyCleanIsBuildable()
        {
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"a\", \"verdict\": \"CLEAN\" },\n" +
                "    { \"hull_stem\": \"b\", \"verdict\": \"STALE-FATAL\" },\n" +
                "    { \"hull_stem\": \"c\", \"verdict\": \"REFUSED-PIN\" }"));

            Assert.IsTrue(map["a"].IsClean);
            Assert.IsFalse(map["b"].IsClean);
            Assert.IsFalse(map["c"].IsClean);
            Assert.IsFalse(BoatInteriorS0Ledger.For(map, "nobody").IsClean);
        }

        [Test]
        public void AHullWithNoRowIsUnknownAndThereforeRefused()
        {
            var map = BoatInteriorS0Ledger.Parse(Ledger("    { \"hull_stem\": \"a\", \"verdict\": \"CLEAN\" }"));
            BoatInteriorS0Entry entry = BoatInteriorS0Ledger.For(map, "neverAdjudicatedIsoRig");

            Assert.AreEqual(BoatInteriorS0Verdict.Unknown, entry.Verdict);
            Assert.IsFalse(entry.IsClean, "an unadjudicated hull is not a cleared one");
            StringAssert.Contains("UNKNOWN", BoatInteriorS0Ledger.RefusalMessage(entry));
        }

        [Test]
        public void ALedgerWithAnUnknownSchemaThrowsRatherThanDefaultingPermissive()
        {
            Assert.Throws<System.FormatException>(() => BoatInteriorS0Ledger.Parse(
                "{ \"schema\": \"something-else@9\", \"hulls\": [] }"));
        }

        [Test]
        public void ALedgerRowWithNoHullStemThrows()
        {
            Assert.Throws<System.FormatException>(() =>
                BoatInteriorS0Ledger.Parse(Ledger("    { \"verdict\": \"CLEAN\" }")));
        }

        // ---- family inheritance ------------------------------------------------------------------------

        [Test]
        public void ADerivedStemInheritsItsFamilysVerdict()
        {
            // The eighteen lobster variants have no row of their own — they inherit the generator rig's.
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"lobsterBoatVariantsIsoRig\", \"verdict\": \"REFUSED-PIN\",\n" +
                "      \"evidence\": \"the pin names a renderer that did not ship\" }"));

            BoatInteriorS0Entry v = BoatInteriorS0Ledger.For(map, "lobsterBoatVariantsIsoRig.standard_open_fundy");
            Assert.AreEqual(BoatInteriorS0Verdict.RefusedPin, v.Verdict);
            StringAssert.Contains("inherited from", v.Evidence);
        }

        [Test]
        public void AnExactRowBeatsTheFamilyFallback()
        {
            // Both sport fishers carry their own rows, and they must never be answered by a
            // `sportFisherIsoRig2` family row that does not exist.
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"sportFisherIsoRig2\", \"verdict\": \"STALE-FATAL\" },\n" +
                "    { \"hull_stem\": \"sportFisherIsoRig2.convertible\", \"verdict\": \"CLEAN\" }"));

            Assert.AreEqual(BoatInteriorS0Verdict.Clean,
                            BoatInteriorS0Ledger.For(map, "sportFisherIsoRig2.convertible").Verdict);
            Assert.AreEqual(BoatInteriorS0Verdict.StaleFatal,
                            BoatInteriorS0Ledger.For(map, "sportFisherIsoRig2.skybridge").Verdict,
                            "a stem with no row of its own still falls back to its family");
        }

        // ---- refusals are loud ---------------------------------------------------------------------------

        [Test]
        public void EveryRefusalNamesWhatUpstreamMustFix()
        {
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"a\", \"verdict\": \"STALE-FATAL\", \"evidence\": \"her loft moved\",\n" +
                "      \"upstream_ask\": \"re-measure against main\" }"));
            string message = BoatInteriorS0Ledger.RefusalMessage(BoatInteriorS0Ledger.For(map, "a"));

            StringAssert.Contains("STALE-FATAL", message);
            StringAssert.Contains("her loft moved", message);
            StringAssert.Contains("re-measure against main", message);
        }

        // ---- the committed ledger --------------------------------------------------------------------------

        static Dictionary<string, BoatInteriorS0Entry> Committed()
        {
            string path = Path.Combine(RepoRoot, BoatInteriorS0Ledger.LedgerPath);
            Assert.IsTrue(File.Exists(path), $"the committed S0 ledger is missing at {BoatInteriorS0Ledger.LedgerPath}");
            return BoatInteriorS0Ledger.Parse(File.ReadAllText(path));
        }

        [Test]
        public void TheCommittedLedgerCoversEveryHullFamilyInTheKit()
        {
            Assert.AreEqual(10, Committed().Count,
                            "nine rig families plus the second sport-fisher hull — the kit's whole coverage");
        }

        [Test]
        public void OnlyTheTwoSportFishersAreCleanToday()
        {
            // ⚠️ If upstream re-stamps the sidecars or re-measures the cape, this list GROWS and this
            // test is the thing that says so. Update it in the same PR as the re-adjudication.
            string[] clean = Committed().Values.Where(e => e.IsClean).Select(e => e.HullStem)
                                        .OrderBy(s => s, System.StringComparer.Ordinal).ToArray();

            CollectionAssert.AreEqual(
                new[] { "sportFisherIsoRig2.convertible", "sportFisherIsoRig2.skybridge" }, clean);
        }

        [Test]
        public void TheCapeIsForkedNotStaleAndAsksForAMergeNotAReMeasure()
        {
            // The distinction this pins cost a wrong verdict to learn. Her rooms are SOUND: every input
            // they are measured from is identical across both branches, and the washboards #247 moved
            // are not in the published loft at all. What is broken is that main's rig and the kit's are
            // two forks — main has the paint and the washboards but publishes no loft; the kit publishes
            // the loft but has neither. Calling that "stale" sends upstream to re-measure, which would
            // not help and cannot even be done: boatInteriorRig.js drops a hull whose rig has no loft.
            BoatInteriorS0Entry cape = BoatInteriorS0Ledger.For(Committed(), "capeIslanderIsoRig");

            Assert.AreEqual(BoatInteriorS0Verdict.ForkedRig, cape.Verdict);
            Assert.IsFalse(cape.IsClean, "forked still does not build — the rig cannot land as-is");
            StringAssert.Contains("merge", cape.UpstreamAsk.ToLowerInvariant(),
                                  "the ask is a rig merge, and saying 're-measure' here is the bug");
            Assert.IsFalse(cape.UpstreamAsk.ToLowerInvariant().Contains("re-measure against main"),
                           "main's rig publishes no loft — measuring against it deletes her from the kit");
        }

        [Test]
        public void TheForkedVerdictReadsDifferentlyFromTheStaleOne()
        {
            // Two refusals that need opposite upstream work must not print the same sentence.
            var map = BoatInteriorS0Ledger.Parse(Ledger(
                "    { \"hull_stem\": \"stale\", \"verdict\": \"STALE-FATAL\" },\n" +
                "    { \"hull_stem\": \"forked\", \"verdict\": \"FORKED-RIG\" }"));

            string stale = BoatInteriorS0Ledger.RefusalMessage(BoatInteriorS0Ledger.For(map, "stale"));
            string forked = BoatInteriorS0Ledger.RefusalMessage(BoatInteriorS0Ledger.For(map, "forked"));

            StringAssert.Contains("STALE-FATAL", stale);
            StringAssert.Contains("FORKED RIG", forked);
            StringAssert.Contains("NOT a re-measure", forked);
            Assert.AreNotEqual(stale, forked);
        }

        [Test]
        public void EveryNonCleanHullCarriesAnUpstreamAsk()
        {
            foreach (BoatInteriorS0Entry e in Committed().Values.Where(x => !x.IsClean))
            {
                Assert.IsNotEmpty(e.Evidence, $"{e.HullStem} is refused with no evidence");
                Assert.IsNotEmpty(e.UpstreamAsk, $"{e.HullStem} is refused with nothing for upstream to do");
            }
        }

        [Test]
        public void TheEighteenVariantsAllResolveThroughTheirFamily()
        {
            Dictionary<string, BoatInteriorS0Entry> ledger = Committed();
            string[] sizes = { "inshore", "standard", "offshore" };
            string[] styles = { "open", "hardtop" };
            string[] regions = { "fundy", "northumberland", "newfoundland" };

            int checkedCount = 0;
            foreach (string size in sizes)
            foreach (string style in styles)
            foreach (string region in regions)
            {
                string stem = $"lobsterBoatVariantsIsoRig.{size}_{style}_{region}";
                BoatInteriorS0Entry e = BoatInteriorS0Ledger.For(ledger, stem);
                Assert.AreNotEqual(BoatInteriorS0Verdict.Unknown, e.Verdict,
                                   $"{stem} fell through to UNKNOWN instead of inheriting its family");
                checkedCount++;
            }
            Assert.AreEqual(18, checkedCount);
        }
    }
}
