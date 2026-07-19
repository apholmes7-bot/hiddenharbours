// SPIKE / THROWAWAY (spike/js-rig-baking) — does ClearScript/V8 load and run inside Unity's Mono
// editor, and how fast? This is the escape-hatch check behind ADR 0021. Delete with the spike.
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigSpike
{
    public class V8RigInterpreterSpikeTests
    {
        static readonly (string Name, string Path, string Global)[] Rigs =
        {
            ("Punt hull",     "docs/art/punt-iso-rig/puntIsoRig.js",           "PuntIso"),
            ("Console skiff", "docs/art/skiff-fleet-rigs/consoleIsoRig.js",    "ConsoleIso"),
            ("Sport skiff",   "docs/art/skiff-fleet-rigs/sportSkiffIsoRig.js", "SportSkiffIso"),
        };

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void V8_Runs_The_Rigs_Inside_Unity()
        {
            // ClearScript probes for its native lib next to the managed assembly; under Unity the
            // managed DLL is loaded from a shadow location, so point it at the plugin folder.
            HostSettings.AuxiliarySearchPath =
                Path.Combine(Application.dataPath, "_Project/Plugins/Editor/JsEngineSpike");

            foreach (var (name, rel, glob) in Rigs)
            {
                string src = File.ReadAllText(Path.Combine(RepoRoot, rel));

                var swInit = Stopwatch.StartNew();
                using var e = new V8ScriptEngine();
                e.Execute(src);          // rig source unmodified, no shims
                swInit.Stop();

                e.Evaluate($"{glob}.render(0,{{}})"); // warm-up

                var sw1 = Stopwatch.StartNew();
                e.Evaluate($"{glob}.render(0,{{}})");
                sw1.Stop();

                var sw8 = Stopwatch.StartNew();
                e.Evaluate($"(function(){{for(var d=0;d<8;d++){glob}.render(d,{{}});}})()");
                sw8.Stop();

                var sw64 = Stopwatch.StartNew();
                e.Evaluate($@"(function(){{for(var d=0;d<8;d++)for(var f=0;f<8;f++){{
                    var r={glob}.rock(f);{glob}.render(d,{{roll:r.roll,pitch:r.pitch,heave:r.heave}});}}}})()");
                sw64.Stop();

                int opaque = System.Convert.ToInt32(e.Evaluate($@"(function(){{
                    var a={glob}.render(0,{{}}),n=0;
                    for(var i=0;i<a.length;i+=4) if(a[i+3]>0) n++; return n;}})()"));

                Debug.Log($"[rig-spike-v8] {name}: install {swInit.Elapsed.TotalMilliseconds:F0} ms " +
                          $"| 1 facing {sw1.Elapsed.TotalMilliseconds:F1} ms " +
                          $"| 8 facings {sw8.Elapsed.TotalSeconds:F2} s " +
                          $"| 64 cells {sw64.Elapsed.TotalSeconds:F2} s | opaque {opaque} px");

                Assert.Greater(opaque, 1000, $"{name} rendered nothing under V8");
            }
        }
    }
}
