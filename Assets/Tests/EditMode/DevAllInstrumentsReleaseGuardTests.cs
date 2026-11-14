using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>EditMode is compiled with UNITY_EDITOR. Pin the actual production preprocessor
    /// boundaries too, so setting the diagnostic flag cannot enable the path in a release build.</summary>
    public class DevAllInstrumentsReleaseGuardTests
    {
        [Test]
        public void ReleaseBuild_HasNeitherAnEnabledGateNorACallToTheDevFit()
        {
            string source = File.ReadAllText("Assets/_Project/Code/Boats/HelmControlRelay.cs");
            const string gatePattern = @"public bool DevAllInstrumentsEnabled\s*\{\s*get\s*\{\s*"
                + @"#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD\s*(.*?)#else\s*return false;\s*#endif\s*\}\s*\}";
            Match gate = Regex.Match(source, gatePattern, RegexOptions.Singleline);
            Assert.IsTrue(gate.Success, "release must compile the dev gate to an unconditional false");
            StringAssert.Contains("DevIgnoreEquipmentGating", gate.Groups[1].Value);
            StringAssert.Contains("GameServices.Config.DevAllHelmInstruments", gate.Groups[1].Value);

            int start = source.IndexOf("public HelmFit Fit", System.StringComparison.Ordinal);
            Assert.Greater(start, 0);
            int end = source.IndexOf("private", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string fit = source.Substring(start, end - start);
            Match dev = Regex.Match(fit, @"#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD\s*"
                + @"if \(DevAllInstrumentsEnabled\)\s*return DevInstrumentCycle.DevFit\(.*?;\s*#endif",
                RegexOptions.Singleline);
            Assert.IsTrue(dev.Success, "the override call itself must be absent from release IL");
            string releaseFit = fit.Remove(dev.Index, dev.Length);
            StringAssert.DoesNotContain("DevInstrumentCycle.DevFit", releaseFit);
            StringAssert.Contains("return BoatEquipment.EffectiveFit(console, _ownedScratch);", releaseFit);
        }
    }
}
