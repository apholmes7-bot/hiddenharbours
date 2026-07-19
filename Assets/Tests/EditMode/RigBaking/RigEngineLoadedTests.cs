using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⚠️ ASSERT THAT THE ENGINE LOADED AND RENDERED. NEVER INFER IT FROM A GREEN RUN. ⚠️
    ///
    /// The failure mode this guards is genuinely nasty: if a native RID or the ICU data assembly is
    /// missing, Unity does not raise a compile error. It logs "Unable to resolve reference
    /// 'ClearScript.V8.ICUData'", SKIPS THE WHOLE ASSEMBLY, and the run reports total=0 and exits 0.
    /// A CI job that only checks the exit code sees a pass. A CI job that only checks failed="0"
    /// sees a pass too. The only defence is a test that does real work with the engine and asserts
    /// on the result — which is what this fixture is.
    ///
    /// (Related trap, recorded in ADR 0021: never pass -quit alongside -runTests. It races the test
    /// runner and produces the same false green from the other direction.)
    /// </summary>
    public class RigEngineLoadedTests
    {
        [Test]
        public void V8_Loads_AndReportsItself()
        {
            using var host = RigScriptHostFactory.Create();
            Assert.IsNotEmpty(host.EngineName, "The host did not report an engine name.");
            Debug.Log($"[rig-baker] engine: {host.EngineName}");

            // Real arithmetic through the engine, not a property read.
            Assert.AreEqual(499500.0,
                host.EvaluateNumber("(function(){var s=0;for(var i=0;i<1000;i++)s+=i;return s;})()"),
                1e-9, "V8 did not evaluate a trivial loop — the engine is not actually running.");
        }

        [Test]
        public void V8_RunsTheRigsUnmodified_AndActuallyRendersPixels(
            [Values("punt", "lobsterBoat")] string rigKey)
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(rigKey);
            var geo = RigCatalog.Install(host, entry);

            Assert.Greater(geo.Width, 0);
            Assert.Greater(geo.Height, 0);
            Debug.Log($"[rig-baker] {rigKey}: {geo}");

            byte[] rgba = host.EvaluateBytes($"{entry.GlobalName}.render(0,{{}})");
            Assert.AreEqual(geo.Width * geo.Height * 4, rgba.Length,
                "The buffer size does not match the rig's own W×H — either the bulk readback is " +
                "wrong or the geometry read is.");

            int opaque = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) opaque++;

            // THE LOAD-BEARING ASSERT: pixels, not a green run.
            Assert.Greater(opaque, 1000,
                $"{rigKey} rendered {opaque} opaque pixels. The engine loaded but drew nothing — " +
                "do not treat this suite as passing.");
            Debug.Log($"[rig-baker] {rigKey}: {opaque} opaque px at dir 0");
        }

        /// <summary>
        /// The bulk <c>ReadBytes</c> path is a hard requirement of ADR 0021 (measured: render+
        /// readback 7.61 ms vs 7.94 ms render-only — free). This does not prove the API used, but it
        /// does prove readback is not costing more than the render, which is what a per-element
        /// marshal would immediately show.
        /// </summary>
        [Test]
        public void PixelReadback_IsNotTheBottleneck()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("punt");
            RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            host.EvaluateBytes($"{g}.render(0,{{}})"); // warm

            var renderOnly = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 8; i++)
                host.EvaluateNumber($"({g}.render({i.ToString(CultureInfo.InvariantCulture)},{{}}), 1)");
            renderOnly.Stop();

            var withReadback = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 8; i++)
                host.EvaluateBytes($"{g}.render({i.ToString(CultureInfo.InvariantCulture)},{{}})");
            withReadback.Stop();

            double r = renderOnly.Elapsed.TotalMilliseconds;
            double rb = withReadback.Elapsed.TotalMilliseconds;
            Debug.Log($"[rig-baker] punt ×8 — render only {r:F1} ms, render+readback {rb:F1} ms " +
                      $"({rb / r:F2}×)");

            Assert.Less(rb, r * 3.0 + 50.0,
                $"Readback took {rb:F1} ms against {r:F1} ms for render alone. The bulk " +
                "ITypedArray<byte>.ReadBytes path is the one implementation constraint ADR 0021 " +
                "names; a per-element marshal here would erase the entire engine advantage.");
        }
    }
}
