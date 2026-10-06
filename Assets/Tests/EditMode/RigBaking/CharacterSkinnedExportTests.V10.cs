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
    /// <b>THE RIG 10 EXPORT (characterIsoRig10.js rev 10.3, landed 2026-10-03 over 10.2's drop of 2026-10-01), RE-RUN.</b>
    ///
    /// <para>Rig 10's kit ships a build per preset (<c>builds/&lt;preset&gt;.v10.json</c>, every number
    /// written to seven decimals), a gameplay sidecar per preset
    /// (<c>gameplay/characterIsoRig10.&lt;preset&gt;.gameplay.json</c>, on one line) and the rig's own
    /// checks (<c>golden-report.json</c>). The presets the game bakes (<c>CAST10</c>; owner, 10-01) are
    /// re-made here from the rig in the kit, in a host of their own, and held to the committed copy:
    /// numbers within the rig's tolerance, sidecars byte for byte, and every golden check as committed.
    /// Since 10.3 the render manifest lists each of its 241 images with the sha256 of its pixels
    /// (<c>rgbaSha256</c>), so the strips are held here too: the owner ruled they come with the 10.3
    /// intake's Phase B.</para>
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
        /// the rig's gate tolerance (<see cref="CharacterSkinExtractor.GateTolerance9"/>: <c>TOL.gate_m</c>
        /// since 10.3) on every number, with nothing added, missing
        /// or renamed. The committed numbers are written to seven decimals, so the worst sits near half
        /// a unit of the seventh (5e-8), a twentieth of the bar.
        /// </summary>
        [Test]
        public void V10_EveryPresetExportsWithinTheRigsToleranceOfItsCommittedBuild()
        {
            double tol = CharacterSkinExtractor.GateTolerance9(V10Host);
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
        /// Every 1x strip the render manifest lists (idle at the 8 facings, the walk at S, the blink and
        /// the look, for every build in the rig's <c>CAST</c>) is the rig's own render today, pixel for
        /// pixel: drawn here in V8 through the kit's own plan (<c>tools/kit.js</c>, <c>renderPlan</c>)
        /// and hashed against the manifest's <c>rgbaSha256</c>. The plan and the manifest must list the
        /// same strips, so a strip dropped from either cannot pass by not being checked.
        /// </summary>
        [Test]
        public void V10_TheCommittedStripsAreTheRigsOwnRender()
        {
            List<string> misses = CharacterSkinExtractor.RenderMisses10(V10Host, V10Kit, out int strips, out int planned);
            Debug.Log($"[CharacterSkinnedExportTests] v10 strips: {strips} of the kit's {planned} 1x strips drawn, " +
                      $"{misses.Count} miss(es)");
            Assert.Greater(planned, 0, "The kit's plan draws no 1x strip.");
            Assert.AreEqual(planned, strips, $"The kit's plan draws {planned} 1x strips; {strips} were checked.");
            Assert.IsEmpty(misses, "Strips rig 10 no longer renders as committed:\n  " + string.Join("\n  ", misses));
        }

        /// <summary>
        /// The rig's own checks (<c>runChecks</c>), run on every preset the game bakes, reproduce the
        /// committed golden report row for row: every pass, every gate, every value, and the gated
        /// totals. Since 10.3 every gated check passes on the ten (the girl's <c>face</c>, 7 of 8 facings
        /// at 10.2, is 8 of 8), but a check is still held to the report, not to passing: one that starts
        /// failing, or stops, moves a row.
        ///
        /// <para>The report was written on Node 24 (INTAKE.md), and the game reads the rig in
        /// ClearScript's V8, which prints a few residuals near 1e-16 m otherwise (at 10.2, three rows
        /// among the ten, by under 4e-17 m). A value is held as printed or, with the same text, every
        /// number within the tolerance the rig's own checks gate on (<c>TOL.gate_m</c> since 10.3,
        /// <see cref="CharacterSkinExtractor.GateTolerance9"/>): such a row is reprinted, not moved.</para>
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

        /// <summary>
        /// The girl passes the rig's <c>face</c> gate at every facing: its own check, run alone, counts the
        /// eyes it paints at each of the rig's facings (<c>order</c>: two from the front and the front
        /// diagonals, one in profile, none from behind) and passes, every facing's row ok. At 10.2 she
        /// showed 7 of 8 (INTAKE.md); 10.3 draws her at all 8. Held to passing, not to the report, so the
        /// gate cannot fall back with the report.
        /// </summary>
        [Test]
        public void V10_TheGirlPassesTheFaceGateAtEveryFacing()
        {
            string g = CharacterRigKit.Rig10.GlobalName;
            Assert.Contains("girl", CharacterSkinExtractor.Presets9(V10Host), "The game bakes no girl from rig 10.");
            string[] r = V10Host.EvaluateString(
                "(function(){var G=" + g + ",c=G.CHECKS.filter(function(x){return x.id==='face';});" +
                "if(c.length!==1)return 'checks '+c.length;var it=c[0].run(G.buildOf('girl')),s=it.next();while(!s.done)s=it.next();" +
                "var v=s.value;return [String(v.pass),String(G.order.length),v.rows.map(function(w){return w.label+' '+(w.ok?'ok':'NOT ok')+' ('+w.value+'; '+w.note+')';}).join('\\n')].join('|');})()")
                .Split('|');
            Assert.AreEqual(3, r.Length, $"Rig 10's face check read as: {string.Join("|", r)}");
            string[] rows = r[2].Split('\n');
            string seen = string.Join("\n  ", rows);
            Debug.Log($"[CharacterSkinnedExportTests] the girl's face gate, rig 10: pass {r[0]}\n  {seen}");
            Assert.AreEqual(int.Parse(r[1], CultureInfo.InvariantCulture), rows.Length,
                $"The face check counted {rows.Length} facings of the rig's {r[1]}:\n  {seen}");
            foreach (string row in rows)
                StringAssert.DoesNotContain("NOT ok", row, $"The girl's face fails at a facing:\n  {seen}");
            Assert.AreEqual("true", r[0], $"The girl's face gate does not pass:\n  {seen}");
        }
    }
}
