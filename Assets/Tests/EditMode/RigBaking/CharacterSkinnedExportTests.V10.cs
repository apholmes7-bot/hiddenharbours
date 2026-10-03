using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE RIG 10 EXPORT (characterIsoRig10.js rev 10.2, drop of 2026-10-01), RE-RUN.</b>
    ///
    /// <para>Rig 10's kit ships a build per preset (<c>builds/&lt;preset&gt;.v10.json</c>, every number
    /// written to seven decimals), a gameplay sidecar per preset
    /// (<c>gameplay/characterIsoRig10.&lt;preset&gt;.gameplay.json</c>, on one line) and the rig's own
    /// checks (<c>golden-report.json</c>). The presets the game bakes (<c>CAST10</c>; owner, 10-01) are
    /// re-made here from the rig in the kit, in a host of their own, and held to the committed copy:
    /// numbers within the rig's tolerance, sidecars byte for byte, and every golden check as committed,
    /// the one the kit records as failing included. Rig 10's render manifest lists its strips without
    /// hashes, so there is no strip test here: the intake's harness held all 60 to the rig by pixel at
    /// landing (<c>INTAKE.md</c>).</para>
    ///
    /// <para>And the reader's contract with rig 10: one script host reads one character rig, and the
    /// game bakes <c>CAST10</c> alone, refusing the twenty NPCs by name.</para>
    /// </summary>
    public partial class CharacterSkinnedExportTests
    {
        IRigScriptHost _v10Host;

        IRigScriptHost V10Host
        {
            get
            {
                if (_v10Host == null)
                {
                    _v10Host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_v10Host, CharacterRigKit.Rig10);
                    CharacterSkinExtractor.Load9Checks(_v10Host);
                }
                return _v10Host;
            }
        }

        [OneTimeTearDown]
        public void DisposeV10()
        {
            _v10Host?.Dispose();
            _v10Host = null;
        }

        static string V10Kit => CharacterRigKit.Rig10.KitRoot;

        /// <summary>
        /// The host holds rig 10, and refuses rig 9: every reader names the global of the kit its host
        /// holds, so one host with both would read whichever it named. A host nobody loaded reads as
        /// rig 9, as every host did before rig 10.
        /// </summary>
        [Test]
        public void V10_OneHostReadsOneCharacterRig()
        {
            IRigScriptHost host = V10Host;
            Assert.AreSame(CharacterRigKit.Rig10, CharacterSkinExtractor.KitOf(host));
            Assert.AreEqual(CharacterRigKit.Rig10.RigName, host.EvaluateString(CharacterRigKit.Rig10.GlobalName + ".rig"));
            Assert.AreEqual(CharacterRigKit.Rig10.Revision, host.EvaluateString(CharacterRigKit.Rig10.GlobalName + ".revision"));

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig9),
                "A host holding rig 10 took rig 9 as well.");
            StringAssert.Contains("host of its own", refused.Message);
            Assert.AreSame(CharacterRigKit.Rig10, CharacterSkinExtractor.KitOf(host),
                "The refused load changed the kit the host holds.");

            using IRigScriptHost fresh = RigScriptHostFactory.Create();
            Assert.AreSame(CharacterRigKit.Rig9, CharacterSkinExtractor.KitOf(fresh),
                "A host nobody loaded should read as rig 9.");
        }

        /// <summary>
        /// The presets the reader bakes are the rig's own <c>CAST10</c>, in its order. Every other name
        /// in its <c>CAST</c> (the twenty NPCs, owner 10-01: they come with the wardrobe) is refused by
        /// name, and so is a name the rig does not know, which rig 10's <c>buildOf</c> refuses too
        /// (<c>no preset "…"</c>, as 9.2's did).
        /// </summary>
        [Test]
        public void V10_TheGameBakesCast10AndRefusesTheNpcs()
        {
            IRigScriptHost host = V10Host;
            string g = CharacterRigKit.Rig10.GlobalName;
            string[] cast10 = host.EvaluateString(g + ".CAST10.join(',')").Split(',');
            CollectionAssert.AreEqual(cast10, CharacterSkinExtractor.Presets9(host),
                "The reader's presets are not rig 10's CAST10, in order.");

            string[] npcs = host.EvaluateString(
                "(function(G){return G.CAST.filter(function(p){return G.CAST10.indexOf(p)<0;}).join(',');})(" + g + ")")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.IsNotEmpty(npcs, "Rig 10's CAST names no build beyond CAST10.");
            Assert.IsTrue(host.EvaluateBool(
                    "(function(G){return G.CAST10.every(function(p){return G.CAST.indexOf(p)>=0;});})(" + g + ")"),
                "A CAST10 preset is missing from rig 10's CAST.");
            foreach (string npc in npcs)
            {
                ArgumentException e = Assert.Throws<ArgumentException>(
                    () => CharacterSkinExtractor.AssertPreset9(host, npc), $"'{npc}' was taken as a preset to bake.");
                StringAssert.Contains("NPC", e.Message);
            }

            const string nobody = "nobody_at_all";
            Assert.Throws<ArgumentException>(() => CharacterSkinExtractor.AssertPreset9(host, nobody));
            Assert.IsTrue(host.EvaluateBool(
                    "(function(){try{" + g + ".buildOf('" + nobody + "');return false;}catch(e){return true;}})()"),
                "Rig 10's buildOf answered for a preset it does not have.");
            Debug.Log($"[CharacterSkinnedExportTests] v10 presets: {cast10.Length} baked " +
                      $"({string.Join(", ", cast10)}), {npcs.Length} NPCs refused");
        }

        /// <summary>
        /// Every preset the game bakes, re-exported by the rig, matches the committed build within
        /// <see cref="CharacterSkinExtractor.V9Tolerance"/> on every number, with nothing added, missing
        /// or renamed. The committed numbers are written to seven decimals, so the worst sits near half
        /// a unit of the seventh (5e-8), a twentieth of the bar.
        /// </summary>
        [Test]
        public void V10_EveryPresetExportsWithinTheRigsToleranceOfItsCommittedBuild()
        {
            double tol = CharacterSkinExtractor.V9Tolerance;
            var report = new StringBuilder();
            var problems = new List<string>();

            foreach (string preset in CharacterSkinExtractor.Presets9(V10Host))
            {
                ExportDrift9 d = CharacterSkinExtractor.CompareBuild9(V10Host, V10Kit, preset, tol);
                report.Append($"\n  {preset}: {d.Numbers:N0} numbers, worst " +
                              $"{d.Worst.ToString("0.00E+0", CultureInfo.InvariantCulture)} at {d.WorstAt}");
                foreach (string p in d.Problems) problems.Add($"{preset}: {p}");
                if (d.Numbers <= 0) problems.Add($"{preset}: no number was compared.");
                if (d.Worst > tol) problems.Add($"{preset}: {d.Worst:E2} off at {d.WorstAt}, over {tol}.");
            }

            Debug.Log($"[CharacterSkinnedExportTests] v10 builds re-exported against the kit:{report}");
            Assert.IsEmpty(problems, "Rig 10 no longer exports the committed builds:\n  " +
                                     string.Join("\n  ", problems));
        }

        /// <summary>
        /// Every preset's gameplay sidecar, re-exported by the rig, is the committed one byte for byte.
        /// </summary>
        [Test]
        public void V10_EveryGameplaySidecarIsTheRigsOwnByteForByte()
        {
            var problems = new List<string>();
            foreach (string preset in CharacterSkinExtractor.Presets9(V10Host))
            {
                string diff = CharacterSkinExtractor.GameplaySidecarDiff9(V10Host, V10Kit, preset);
                if (diff != null) problems.Add($"{preset}: {diff}");
            }
            Assert.IsEmpty(problems, "Gameplay sidecars rig 10 no longer exports as committed:\n  " +
                                     string.Join("\n  ", problems));
        }

        /// <summary>
        /// The rig's own checks (<c>runChecks</c>), run on every preset the game bakes, reproduce the
        /// committed golden report row for row: every pass, every gate, every value, and the gated
        /// totals. The kit records one gated failure among the ten, the girl's <c>face</c> at 7 of 8
        /// facings (INTAKE.md, finding 10), so a check is held to the report, not to passing: one that
        /// starts failing, or stops, moves a row.
        ///
        /// <para>The report was written on Node 24 (INTAKE.md), and the game reads the rig in
        /// ClearScript's V8, which prints a few residuals near 1e-16 m otherwise (three rows among the
        /// ten, by under 4e-17 m). A value is held as printed or, with the same text, every number within
        /// the 1e-6 m the rig's own checks gate on (<see cref="CharacterSkinExtractor.V9Tolerance"/>):
        /// such a row is reprinted, not moved.</para>
        /// </summary>
        [Test]
        public void V10_TheRigsOwnChecksReproduceTheGoldenReport()
        {
            var problems = new List<string>();
            var failing = new List<string>();
            int passedTotal = 0, ofTotal = 0, reprintedTotal = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(V10Host))
            {
                List<string> moved = CharacterSkinExtractor.GoldenDrift9(V10Host, V10Kit, preset,
                                                                         out int passed, out int of,
                                                                         out int reprinted);
                foreach (string m in moved) problems.Add($"{preset}: {m}");
                if (of <= 0) problems.Add($"{preset}: the rig ran no check.");
                if (passed != of) failing.Add($"{preset} {passed}/{of}");
                passedTotal += passed; ofTotal += of; reprintedTotal += reprinted;
            }

            Debug.Log($"[CharacterSkinnedExportTests] v10 golden checks: {passedTotal}/{ofTotal} gated pass" +
                      (failing.Count > 0 ? $"; as the kit records, not all on {string.Join(", ", failing)}" : "") +
                      $"; {reprintedTotal} values reprinted within the rig's gate");
            Assert.IsEmpty(problems, "Rig 10's checks no longer reproduce the golden report:\n  " +
                                     string.Join("\n  ", problems));
        }
    }
}
