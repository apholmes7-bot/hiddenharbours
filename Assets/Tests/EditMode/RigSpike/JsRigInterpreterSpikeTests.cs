// SPIKE / THROWAWAY (spike/js-rig-baking) — measures embedded-JS interpreter speed on the art
// director's rig .js files, from a Unity EDITOR context, on Unity's own runtime.
// This is a measurement harness for ADR 0021, NOT a feature. Delete with the spike.
//
// INERT BY DEFAULT. The asmdef is gated on the HH_JS_RIG_SPIKE define, which nothing sets — no
// engine DLLs are committed, so this assembly is skipped and main stays green. To reproduce:
//   1. dotnet add package Jint            -> copy Jint.dll + Acornima.dll (netstandard2.0)
//      dotnet add package Microsoft.ClearScript.V8 + .Native.win-x64
//                                         -> copy ClearScript.Core/.V8/.V8.ICUData.dll
//                                            + ClearScriptV8.win-x64.dll
//      into Assets/_Project/Plugins/Editor/JsEngineSpike/, each .meta set editor-only
//      (Any: enabled 0, Editor: enabled 1) and validateReferences: 0 on the ClearScript ones.
//   2. Add HH_JS_RIG_SPIKE to Scripting Define Symbols.
//   3. Unity.exe -batchmode -nographics -runTests -testPlatform EditMode \
//        -testFilter HiddenHarbours.Tests.RigSpike   (do NOT pass -quit; it races -runTests)
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using Jint;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigSpike
{
    public class JsRigInterpreterSpikeTests
    {
        static readonly (string Name, string Path, string Global)[] Rigs =
        {
            ("Punt hull",     "docs/art/punt-iso-rig/puntIsoRig.js",           "PuntIso"),
            ("Console skiff", "docs/art/skiff-fleet-rigs/consoleIsoRig.js",    "ConsoleIso"),
            ("Sport skiff",   "docs/art/skiff-fleet-rigs/sportSkiffIsoRig.js", "SportSkiffIso"),
        };

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void Rigs_Run_Unmodified_And_We_Measure_Them()
        {
            Debug.Log($"[rig-spike] runtime = {System.Environment.Version} / " +
                      $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

            foreach (var (name, rel, glob) in Rigs)
            {
                string src = File.ReadAllText(Path.Combine(RepoRoot, rel));

                var swInit = Stopwatch.StartNew();
                var engine = new Engine();          // <-- NO SHIMS. Bare Jint engine.
                engine.Execute(src);                // <-- rig source byte-for-byte unmodified.
                swInit.Stop();

                var rig = engine.Evaluate($"globalThis.{glob}").AsObject();
                int w = (int)rig.Get("W").AsNumber(), h = (int)rig.Get("H").AsNumber();

                engine.Evaluate($"{glob}.render(0, {{}})"); // warm-up

                var sw1 = Stopwatch.StartNew();
                engine.Evaluate($"{glob}.render(0, {{}})");
                sw1.Stop();

                var sw8 = Stopwatch.StartNew();
                engine.Evaluate($"(function(){{ for (var d=0; d<8; d++) {glob}.render(d, {{}}); }})()");
                sw8.Stop();

                int opaque = (int)engine.Evaluate($@"(function(){{
                    var a = {glob}.render(0, {{}}), n = 0;
                    for (var i = 0; i < a.length; i += 4) if (a[i+3] > 0) n++;
                    return n; }})()").AsNumber();

                Debug.Log($"[rig-spike] {name}: cell {w}x{h} ({w * h} px) | install {swInit.Elapsed.TotalMilliseconds:F0} ms " +
                          $"| 1 facing {sw1.Elapsed.TotalMilliseconds:F0} ms | 8 facings {sw8.Elapsed.TotalSeconds:F2} s " +
                          $"| 64 cells (extrapolated) {sw8.Elapsed.TotalSeconds * 8:F1} s | opaque {opaque} px");

                Assert.Greater(opaque, 1000, $"{name} rendered nothing — the rig did not run.");
            }
        }

        [Test]
        public void Rig_Cells_Can_Be_Emitted_In_TRUE_Clockwise_Order()
        {
            // PRIZE 1: the rigs bake counter-clockwise (cell i depicts -45*i, labelled +45*i).
            // Because WE choose the dir argument, we can emit a TRUE clockwise sheet with no rig edit.
            string src = File.ReadAllText(Path.Combine(RepoRoot, "docs/art/punt-iso-rig/puntIsoRig.js"));
            var engine = new Engine();
            engine.Execute(src);

            // Cell k of a TRUE clockwise sheet == render((8 - k) % 8).
            for (int k = 0; k < 8; k++)
            {
                int trueDir = (8 - k) % 8;
                int opaque = (int)engine.Evaluate($@"(function(){{
                    var a = PuntIso.render({trueDir}, {{}}), n = 0;
                    for (var i = 0; i < a.length; i += 4) if (a[i+3] > 0) n++;
                    return n; }})()").AsNumber();
                Assert.Greater(opaque, 1000, $"cell {k} (dir {trueDir}) rendered nothing");
            }
        }

        [Test]
        public void Rigs_Expose_Anchors_As_Cell_Pixels()
        {
            // PRIZE 2: can a bake emit measured anchors straight into a Def?
            string src = File.ReadAllText(Path.Combine(RepoRoot, "docs/art/skiff-fleet-rigs/consoleIsoRig.js"));
            var engine = new Engine();
            engine.Execute(src);

            var motor = engine.Evaluate("JSON.stringify(ConsoleIso.motorMount(0,{}))").AsString();
            var helm = engine.Evaluate("JSON.stringify(ConsoleIso.helmSeat(0,{}))").AsString();
            var tubs = engine.Evaluate("JSON.stringify(ConsoleIso.tubMounts(0,{}))").AsString();
            Debug.Log($"[rig-spike] ConsoleIso anchors dir0: motorMount={motor} helmSeat={helm} tubMounts={tubs}");

            // and they must MOVE with the rock frame — i.e. they are live projections, not constants
            var rocked = engine.Evaluate(
                "JSON.stringify(ConsoleIso.motorMount(0, ConsoleIso.rock(2)))").AsString();
            Debug.Log($"[rig-spike] ConsoleIso motorMount dir0 @rock(2)={rocked}");
            Assert.AreNotEqual(motor, rocked, "anchors should ride the wave");
        }

        [Test]
        public void Jint_Plugins_Are_Editor_Only()
        {
            // Verifies nothing can leak into a player build.
            foreach (var dll in new[] { "Jint", "Acornima" })
            {
                var path = $"Assets/_Project/Plugins/Editor/JsEngineSpike/{dll}.dll";
                var imp = (PluginImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(imp, $"{path} not imported");
                Assert.IsFalse(imp.GetCompatibleWithAnyPlatform(), $"{dll} is compatible with ANY platform");
                Assert.IsTrue(imp.GetCompatibleWithEditor(), $"{dll} not editor-compatible");
                foreach (BuildTarget t in new[] { BuildTarget.StandaloneWindows64, BuildTarget.Android, BuildTarget.iOS })
                    Assert.IsFalse(imp.GetCompatibleWithPlatform(t), $"{dll} would ship to {t}");
                Debug.Log($"[rig-spike] {dll}.dll: editor-only OK (anyPlatform=false, editor=true)");
            }
        }
    }
}
