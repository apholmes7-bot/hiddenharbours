using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE RIG 9 EXPORT (characterIsoRig9.js rev 9.2, drop of 2026-09-23), RE-RUN.</b>
    ///
    /// <para>The kit ships what the rig produced when Claude Design exported it: a build per cast
    /// preset (<c>builds/&lt;preset&gt;.v9.json</c>, with its gameplay sidecar), the rig's own
    /// checks (<c>golden-report.json</c>) and the 1x render strips (<c>renders/manifest.json</c>).
    /// Every one of them is re-made here from the rig in the kit, in its own host, and held to the
    /// committed copy: numbers within the rig's tolerance, sidecars and strips byte for byte, and
    /// every golden check passing again. A rig and an export that drift apart red here, before a
    /// bake reads either.</para>
    /// </summary>
    public partial class CharacterSkinnedExportTests
    {
        IRigScriptHost _v9Host;

        IRigScriptHost V9Host
        {
            get
            {
                if (_v9Host == null)
                {
                    _v9Host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_v9Host);
                    CharacterSkinExtractor.Load9Checks(_v9Host);
                }
                return _v9Host;
            }
        }

        [OneTimeTearDown]
        public void DisposeV9()
        {
            _v9Host?.Dispose();
            _v9Host = null;
        }

        static string V9Kit => CharacterSkinExtractor.V9KitRoot;

        /// <summary>
        /// Every cast preset's build, re-exported by the rig, matches the committed build within
        /// <see cref="CharacterSkinExtractor.V9Tolerance"/> on every number (the 9.2 drop's worst is
        /// 6.2e-15), with nothing added, missing or renamed.
        /// </summary>
        [Test]
        public void V9_EveryPresetExportsWithinTheRigsToleranceOfItsCommittedBuild()
        {
            double tol = CharacterSkinExtractor.V9Tolerance;
            var report = new StringBuilder();
            var problems = new List<string>();

            foreach (string preset in CharacterSkinExtractor.Presets9(V9Host))
            {
                ExportDrift9 d = CharacterSkinExtractor.CompareBuild9(V9Host, V9Kit, preset, tol);
                report.Append($"\n  {preset}: {d.Numbers:N0} numbers, worst " +
                              $"{d.Worst.ToString("0.00E+0", CultureInfo.InvariantCulture)} at {d.WorstAt}");
                foreach (string p in d.Problems) problems.Add($"{preset}: {p}");
                if (d.Numbers <= 0) problems.Add($"{preset}: no number was compared.");
                if (d.Worst > tol) problems.Add($"{preset}: {d.Worst:E2} off at {d.WorstAt}, over {tol}.");
            }

            Debug.Log($"[CharacterSkinnedExportTests] v9 builds re-exported against the kit:{report}");
            Assert.IsEmpty(problems, "The rig no longer exports the committed builds:\n  " +
                                     string.Join("\n  ", problems));
        }

        /// <summary>
        /// Every preset's gameplay sidecar (the part of the build the game reads outside the mesh),
        /// re-exported by the rig, is the committed one byte for byte.
        /// </summary>
        [Test]
        public void V9_EveryGameplaySidecarIsTheRigsOwnByteForByte()
        {
            var problems = new List<string>();
            foreach (string preset in CharacterSkinExtractor.Presets9(V9Host))
            {
                string diff = CharacterSkinExtractor.GameplaySidecarDiff9(V9Host, V9Kit, preset);
                if (diff != null) problems.Add($"{preset}: {diff}");
            }
            Assert.IsEmpty(problems, "Gameplay sidecars the rig no longer exports as committed:\n  " +
                                     string.Join("\n  ", problems));
        }

        /// <summary>
        /// Every 1x strip the manifest lists is the rig's own render today, pixel for pixel, and the
        /// manifest lists one for every cast preset, clip and facing it names, so a preset whose
        /// strips were dropped from the kit cannot pass by not being checked.
        /// </summary>
        [Test]
        public void V9_TheCommittedStripsAreTheRigsOwnRender()
        {
            List<string> misses = CharacterSkinExtractor.RenderMisses9(V9Host, V9Kit, out int strips);
            int presets = CharacterSkinExtractor.Presets9(V9Host).Length;
            int perPreset = (int)V9Host.EvaluateNumber(
                "(function(M){return M.clips.length*Object.keys(M.facings).length;})(" +
                CharacterSkinExtractor.ReadKitText9(V9Kit, CharacterSkinExtractor.V9ManifestFile) + ")");

            Assert.AreEqual(presets * perPreset, strips,
                $"The manifest's 1x strips should cover {presets} presets x {perPreset} clip facings; " +
                $"{strips} were checked.");
            Assert.IsEmpty(misses, "Strips the rig no longer renders as committed:\n  " + string.Join("\n  ", misses));
        }

        /// <summary>
        /// The rig's own checks (<c>runChecks</c>), run on every preset, reproduce the committed
        /// golden report: every check passes and none has moved.
        /// </summary>
        [Test]
        public void V9_TheRigsOwnChecksReproduceTheGoldenReport()
        {
            var problems = new List<string>();
            int passedTotal = 0, ofTotal = 0;
            foreach (string preset in CharacterSkinExtractor.Presets9(V9Host))
            {
                List<string> moved = CharacterSkinExtractor.GoldenDrift9(V9Host, V9Kit, preset,
                                                                         out int passed, out int of);
                foreach (string m in moved) problems.Add($"{preset}: {m}");
                if (of <= 0) problems.Add($"{preset}: the rig ran no check.");
                if (passed != of) problems.Add($"{preset}: {passed} of {of} checks pass.");
                passedTotal += passed; ofTotal += of;
            }

            Debug.Log($"[CharacterSkinnedExportTests] v9 golden checks: {passedTotal}/{ofTotal}");
            Assert.IsEmpty(problems, "The rig's checks no longer reproduce the golden report:\n  " +
                                     string.Join("\n  ", problems));
        }
    }
}
