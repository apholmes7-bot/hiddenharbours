using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the notebook kit.
    ///
    /// <para>Menu priority 77 continues the unbroken Art-menu bake run (…75 taken, 76 dialogue
    /// bubbles). Unity draws a separator at any priority gap greater than 10, so an item placed
    /// carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class NotebookKitBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Notebook Kit (stocks · boxes · tabs · marks · selection)",
                  priority = 77)]
        public static void BakeAll()
        {
            try
            {
                var report = NotebookKitBaker.BakeAll();
                AssetDatabase.Refresh();
                Debug.Log(Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Notebook kit bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Bake, import, then verify, in one editor invocation — the <c>-executeMethod</c> entry
        /// point.
        ///
        /// <para>The marker on the last line is what a run is verified by — <b>a Unity exit code does
        /// not tell you whether <c>-executeMethod</c> fired</b>, and a first run in a fresh worktree
        /// can import only and exit 0 with the method never called. Run it twice.</para>
        /// </summary>
        public static void BakeAndImportHeadless()
        {
            BakeAll();
            int changed = NotebookKitImporter.ImportAll(out int missing);
            Debug.Log($"[notebook-bake] import: {changed} piece(s) changed, {missing} missing");

            if (!NotebookKitImporter.Verify(logEachPass: true))
            {
                Debug.LogError("[notebook-bake] VERIFY FAILED — see mismatches above.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[notebook-bake] BAKE + IMPORT + VERIFY COMPLETE");
        }

        /// <summary>Import + verify only, for when the PNGs are already on disk (a fresh clone of a
        /// branch that carries them, where nothing needs re-rendering).</summary>
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Set Notebook Import Settings",
                  priority = 208)]
        public static void ImportOnly()
        {
            int changed = NotebookKitImporter.ImportAll(out int missing);
            bool ok = NotebookKitImporter.Verify(logEachPass: false);
            Debug.Log($"[NotebookKitImporter] {changed} piece(s) changed, {missing} missing — " +
                      (ok ? "VERIFY PASSED" : "VERIFY FAILED (see errors above)"));
        }

        /// <summary>
        /// Runs the kit's own four probes and logs the verdicts. The kit's README is explicit that
        /// these are not decoration — "run these in the import PR and fail on them" —
        /// so <c>NotebookKitProbeTests</c> runs the same four and fails on them.
        ///
        /// <list type="bullet">
        ///   <item><c>probeType</c> — the notebook's cell and glyph set ARE the bubble kit's, by
        ///   reference. The one that would catch two faces drifting apart.</item>
        ///   <item><c>probeHand</c> — the roughened mode (shipped OFF) differs pixel by pixel while
        ///   every cell origin stays exactly <c>x + i·5·s</c>.</item>
        ///   <item><c>probeWrite</c> — ink at fill k is a subset of ink at fill k+1; one caret, and no
        ///   strike or tick on text not yet written.</item>
        ///   <item><c>probeFlow</c> — blocks kept whole across the gutter, nothing drawn past the last
        ///   ruled line, and <c>placed + overflow = given</c> so the pencil "n more" is a fact.</item>
        /// </list>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Probe Notebook Kit (type · hand · write · flow)", priority = 153)]
        public static void ProbeKit()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(NotebookKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(NotebookKitBaker.RigKey));

            bool allPass = true;
            var sb = new StringBuilder("[notebook] probes\n");

            foreach (string probe in NotebookProbes)
            {
                host.Execute($"globalThis.__hhNbProbe = NotebookKit.{probe}();");
                bool pass = host.EvaluateBool("!!globalThis.__hhNbProbe.pass");
                string claim = host.EvaluateString("String(globalThis.__hhNbProbe.claim || '')");
                allPass &= pass;
                sb.AppendLine($"  {probe,-11} {(pass ? "PASS" : "FAIL")} — {claim}");
            }

            if (allPass) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }

        /// <summary>The kit's four probes, named once so the menu and the tests cannot drift.</summary>
        public static readonly string[] NotebookProbes =
            { "probeType", "probeHand", "probeWrite", "probeFlow" };

        public static string Summarise(NotebookKitBakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Notebook kit baked — {r.Pieces.Count} pieces, " +
                          $"{r.RewrittenCount} written / {r.Pieces.Count - r.RewrittenCount} unchanged " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s total, " +
                          $"{r.RenderMilliseconds:F0} ms rendering, {r.EngineName})");

            foreach (var p in r.Pieces) sb.AppendLine("  " + p);

            sb.AppendLine("  ⚠ Nothing here was copied — the kit ships no PNGs. Every piece is " +
                          "live-drawn from notebookRig3.js through the V8 host.");
            sb.AppendLine("  ⚠ Furniture only: the type specimens belong to the font asset, and the " +
                          "page/turn composites carry SAMPLE game text and are never baked.");
            return sb.ToString();
        }
    }
}
