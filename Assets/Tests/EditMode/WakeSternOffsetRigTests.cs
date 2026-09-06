using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Where every hull actually sheds her churn, re-derived from HER OWN RIG rather than from the
    /// number in the def beside it.
    ///
    /// <para><b>Why this exists.</b> PR 11a authored <c>HullMeshDef.WakeSternOffsetMeters</c> at
    /// LOA/2 on all 34 hulls, and LOA/2 is exact <i>only</i> for a hull whose origin sits amidships.
    /// This test settles both halves of that: it scrapes each rig's own loft to check the origin IS
    /// amidships (every rig places station <c>u</c> at <c>y = −L/2 + u·L</c>, so the transom at
    /// <c>u = 0</c> is exactly <c>L/2</c> astern), and then compares the def against the rig's own
    /// <c>L</c>.</para>
    ///
    /// <para><b>It found three.</b> A def is only as good as the length it was written from, and
    /// three were written from a length that is not the loft's: the cape from her BoatDef's 12.9 m
    /// where the rig lofts 12.8, and both zodiacs from the label length over their TUBES (7.28 /
    /// 6.66) where the rig lofts the hull at 7.00 / 6.40. Small on the cape (5 cm), a seventh of a
    /// metre on the RHIBs — and all three are the kind of drift that only ever gets worse, because
    /// the def and the rig had no test between them until now.</para>
    ///
    /// <para><b>Two independent sources, on purpose.</b> The def is data the game reads; the rig is
    /// the artwork's own source of truth. Comparing a transcription against itself would prove
    /// nothing (<c>bit-equality-is-unattainable-between-two-transcriptions</c>), so this reads the rig
    /// text at test time.</para>
    /// </summary>
    public class WakeSternOffsetRigTests
    {
        const string DefRoot = "Assets/_Project/Data/Boats/HullMeshes";
        const string RigRoot = "docs/art/rigs";

        /// <summary>A hull's def, the rig that lofts her, and how to find that hull's loft length in
        /// it. Multi-hull rigs key off the variant's own id so a reordering cannot silently swap two
        /// boats' sterns.</summary>
        static readonly (string Def, string Rig, string Pattern)[] Fleet =
        {
            ("CapeIslanderIsoHullMesh",  "capeIslanderIsoRig.js",  @"\bconst L\s*=\s*([0-9.]+)"),
            ("CoastalPacketIsoHullMesh", "coastalPacketIsoRig.js", @"\bconst L\s*=\s*([0-9.]+)"),
            ("ConsoleIsoHullMesh",       "consoleIsoRig.js",       @"\bconst L\s*=\s*([0-9.]+)"),
            ("DoryIsoHullMesh",          "doryIsoRig.js",          @"\bconst L\s*=\s*([0-9.]+)"),
            ("LobsterBoatIsoHullMesh",   "lobsterBoatIsoRig.js",   @"\bconst L\s*=\s*([0-9.]+)"),
            ("PuntIsoHullMesh",          "puntIsoRig.js",          @"\bconst L\s*=\s*([0-9.]+)"),
            ("SideDraggerIsoHullMesh",   "sideDraggerIsoRig.js",   @"\bconst L\s*=\s*([0-9.]+)"),
            ("SportSkiffIsoHullMesh",    "sportSkiffIsoRig.js",    @"\bconst L\s*=\s*([0-9.]+)"),
            ("SportSkiffMk2IsoHullMesh", "sportSkiffMk2IsoRig.js", @"\bconst L\s*=\s*([0-9.]+)"),
            ("SternTrawlerIsoHullMesh",  "sternTrawlerIsoRig.js",  @"\bconst L\s*=\s*([0-9.]+)"),
            ("SternTrawlerMk2IsoHullMesh", "sternTrawlerMk2IsoRig.js", @"\bconst L\s*=\s*([0-9.]+)"),
            ("TankerIsoHullMesh",        "tankerIsoRig.js",        @"\bconst L\s*=\s*([0-9.]+)"),
            ("SportFisherConvertibleIsoHullMesh", "sportFisherIsoRig2.js",
                @"id:'convertible'[\s\S]{0,1500}?\bL:\s*([0-9.]+)"),
            ("SportFisherSkybridgeIsoHullMesh", "sportFisherIsoRig2.js",
                @"id:'skybridge'[\s\S]{0,1500}?\bL:\s*([0-9.]+)"),
            // ⚠️ The RHIBs carry two lengths: L is the LOFT (the hull the water sees) and loa is the
            // label length over the tubes. The wake is shed at the transom, so it is L.
            ("ZodiacHurricaneIsoHullMesh", "zodiacIsoRig.js", @"id:'hurricane'[\s\S]{0,200}?\bL:\s*([0-9.]+)"),
            ("ZodiacFrcIsoHullMesh",       "zodiacIsoRig.js", @"id:'frc'[\s\S]{0,200}?\bL:\s*([0-9.]+)"),
        };

        static IEnumerable<(string Def, string Rig, string Pattern)> AllHulls()
        {
            foreach (var row in Fleet) yield return row;
            // The lobster variants: one rig, three grades, and six defs each (hardtop/open x three
            // regions) that share a grade's loft. Their L is the grade's own loa (the rig assigns
            // const L = Z.loa), so the grade id is the key.
            foreach (var grade in new[] { ("Inshore", "inshore"), ("Standard", "standard"),
                                          ("Offshore", "offshore") })
                foreach (string top in new[] { "Hardtop", "Open" })
                    foreach (string region in new[] { "Fundy", "Newfoundland", "Northumberland" })
                        yield return ($"Lobster{grade.Item1}{top}{region}IsoHullMesh",
                                      "lobsterBoatVariantsIsoRig.js",
                                      $@"id:'{grade.Item2}'[\s\S]{{0,200}}?loa:\s*([0-9.]+)");
        }

        /// <summary>
        /// 🔴 <b>THE PREMISE.</b> <c>L/2</c> is the transom only because every hull rig lofts its
        /// stations about the ORIGIN — <c>y = −L/2 + u·L</c>, so <c>u = 0</c> is exactly half a hull
        /// astern. If a rig ever moves its origin, every offset derived from L/2 becomes wrong
        /// silently, so the premise is checked before the numbers are.
        /// </summary>
        [Test]
        public void EveryHullRig_LoftsHerStationsAboutTheOrigin_SoHalfHerLengthIsTheTransom()
        {
            var seen = new HashSet<string>();
            var failures = new StringBuilder();
            foreach (var hull in AllHulls())
            {
                if (!seen.Add(hull.Rig)) continue;
                string path = Path.Combine(RigRoot, hull.Rig);
                Assert.IsTrue(File.Exists(path), $"{hull.Rig} is missing — {hull.Def} has no rig.");
                string src = File.ReadAllText(path);
                // y : -L/2 + u*L   ·   y : (-0.5 + u) * B.L   ·   yOf = (u) => -L/2 + u*L
                bool amidships = Regex.IsMatch(
                    src, @"y\s*(?::|Of\s*=\s*\(u\)\s*=>)\s*\(?\s*-\s*(?:0\.5|[A-Za-z0-9_.]+\s*/\s*2)");
                if (!amidships)
                    failures.AppendLine(
                        $"  {hull.Rig}: no station law of the form y = -L/2 + u*L. If this rig has " +
                        "moved its origin off amidships, LOA/2 is no longer her transom and " +
                        "WakeSternOffsetMeters has to be measured from the loft instead.");
            }
            Assert.IsEmpty(failures.ToString(), "\n" + failures);
        }

        /// <summary>
        /// Every hull's <c>WakeSternOffsetMeters</c> is exactly half the length her own rig lofts.
        /// The whole fleet is reported in one message rather than failing on the first, because the
        /// interesting thing about a drift like this is always how many hulls share it.
        /// </summary>
        [Test]
        public void EveryHullsSternOffset_IsHalfTheLengthHerOwnRigLofts()
        {
            var report = new StringBuilder();
            var failures = new StringBuilder();
            int checkedHulls = 0;

            foreach (var hull in AllHulls())
            {
                string assetPath = $"{DefRoot}/{hull.Def}.asset";
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(assetPath);
                Assert.IsNotNull(def, $"{assetPath} is missing or is not a HullMeshDef.");

                string src = File.ReadAllText(Path.Combine(RigRoot, hull.Rig));
                Match m = Regex.Match(src, hull.Pattern);
                if (!m.Success)
                {
                    failures.AppendLine($"  {hull.Def}: could not find her loft length in {hull.Rig}.");
                    continue;
                }
                float loft = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                float want = loft * 0.5f;
                checkedHulls++;
                report.AppendLine($"  {hull.Def,-46} rig {loft,7:0.00} m  transom {want,6:0.00} m  " +
                                  $"def {def.WakeSternOffsetMeters,6:0.00} m");
                if (Mathf.Abs(def.WakeSternOffsetMeters - want) > 0.005f)
                    failures.AppendLine(
                        $"  {hull.Def}: sheds her churn {want:0.00} m astern ({hull.Rig} lofts her " +
                        $"at {loft:0.00} m) but the def says {def.WakeSternOffsetMeters:0.00} m — a " +
                        $"root error of {Mathf.Abs(def.WakeSternOffsetMeters - want):0.00} m on every " +
                        "frame she is under way.");
            }

            TestContext.WriteLine(report.ToString());
            Assert.AreEqual(34, checkedHulls, "The fleet is 34 hulls; this table has drifted from it.");
            Assert.IsEmpty(failures.ToString(), "\n" + failures);
        }

        /// <summary>Nothing in <c>HullMeshes</c> may sit outside the table above — a hull added
        /// without a rig mapping would keep whatever offset somebody typed, unchecked.</summary>
        [Test]
        public void NoHullDef_EscapesTheRigCheck()
        {
            var mapped = new HashSet<string>();
            foreach (var hull in AllHulls()) mapped.Add(hull.Def);

            var stray = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:HullMeshDef", new[] { DefRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!mapped.Contains(name))
                    stray.AppendLine($"  {name} has no rig mapped, so her stern offset is unchecked.");
            }
            Assert.IsEmpty(stray.ToString(), "\n" + stray);
        }
    }
}
