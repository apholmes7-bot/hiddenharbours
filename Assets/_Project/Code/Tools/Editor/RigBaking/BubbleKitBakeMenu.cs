using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the dialogue bubble kit.
    ///
    /// <para>Menu priority 76 continues the unbroken Art-menu bake run (…73 shop fixtures, 74 camper,
    /// 75 taken). Unity draws a separator at any priority gap greater than 10, so an item placed
    /// carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class BubbleKitBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Dialogue Bubble Kit (panel · 6 tails · chip · cursors)",
                  priority = 76)]
        public static void BakeAll()
        {
            try
            {
                var report = BubbleKitBaker.BakeAll();
                AssetDatabase.Refresh();
                Debug.Log(Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Bubble kit bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Bake, import, then verify, in one editor invocation — the <c>-executeMethod</c> entry
        /// point.
        ///
        /// <para>Deliberately NOT a menu item: the owner's Art menu already has the bake, and a
        /// second entry that just runs all three would be clutter. This exists because a headless run
        /// takes one method, and doing it as three batch invocations pays the editor's start-up
        /// three times for nothing.</para>
        ///
        /// <para>The marker on the last line is what a run is verified by — <b>a Unity exit code does
        /// not tell you whether <c>-executeMethod</c> fired</b>, and a first run in a fresh worktree
        /// can import only and exit 0 with the method never called. Run it twice.</para>
        /// </summary>
        public static void BakeAndImportHeadless()
        {
            BakeAll();
            int changed = BubbleKitImporter.ImportAll(out int missing);
            Debug.Log($"[bubble-bake] import: {changed} piece(s) changed, {missing} missing");

            if (!BubbleKitImporter.Verify(logEachPass: true))
            {
                Debug.LogError("[bubble-bake] VERIFY FAILED — see mismatches above.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[bubble-bake] BAKE + IMPORT + VERIFY COMPLETE");
        }

        /// <summary>Import + verify only, for when the PNGs are already on disk (a fresh clone of a
        /// branch that carries them, where nothing needs re-rendering).</summary>
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Set Dialogue Bubble Import Settings",
                  priority = 207)]
        public static void ImportOnly()
        {
            int changed = BubbleKitImporter.ImportAll(out int missing);
            bool ok = BubbleKitImporter.Verify(logEachPass: false);
            Debug.Log($"[BubbleKitImporter] {changed} piece(s) changed, {missing} missing — " +
                      (ok ? "VERIFY PASSED" : "VERIFY FAILED (see errors above)"));
        }

        /// <summary>
        /// Runs the kit's one behavioural claim as a probe and logs the verdict:
        /// <c>CharacterIso6.render(dir,{t,talk:true})</c> really does reach the head rig's mouth
        /// cadence. The kit calls this "a rename, not a feature" — this is what turns that into
        /// something checked. <c>BubbleKitMouthProbeTests</c> runs the same call and fails on it.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Probe Talk-Cue Mouth Channel", priority = 152)]
        public static void ProbeMouth()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(BubbleKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.TalkCueRigKey));

            host.Execute("globalThis.__hhProbe = TalkCue.probeMouth(CharacterIso6);");
            bool pass = host.EvaluateBool("!!globalThis.__hhProbe.pass");
            double px = host.EvaluateNumber("globalThis.__hhProbe.px");
            string note = host.EvaluateString("String(globalThis.__hhProbe.note || '')");

            string message = $"[talk-cue] mouth probe {(pass ? "PASS" : "FAIL")} — {px} px changed. {note}";
            if (pass) Debug.Log(message);
            else Debug.LogError(message);
        }

        public static string Summarise(BubbleKitBakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Bubble kit baked — {r.Pieces.Count} pieces, " +
                          $"{r.RewrittenCount} written / {r.Pieces.Count - r.RewrittenCount} unchanged " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s total, " +
                          $"{r.RenderMilliseconds:F0} ms rendering, {r.EngineName})");

            foreach (var p in r.Pieces) sb.AppendLine("  " + p);

            sb.AppendLine("  ⚠ Nothing here was copied — the kit ships no PNGs. Every piece is " +
                          "live-drawn from dialogueBubbleRig.js through the V8 host.");
            return sb.ToString();
        }
    }
}
