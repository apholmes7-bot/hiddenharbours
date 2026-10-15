using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantDormantStateTests
    {
        [Test] public void DormantListIsPinnedAndAllFourCallPathsSkipEveryCell()
        {
            const string dir = "docs/art/rigs/plants/";
            using (var sha = SHA256.Create())
                Assert.That(string.Concat(sha.ComputeHash(File.ReadAllBytes(dir + "dormant-states.json")).Select(b => b.ToString("x2"))),
                    Is.EqualTo("fe4c46b6124d8174d7a1f44e5a98ed40ef6e53cc94596372f003a7a5f86ec6f4"), "Dormant data changed; settle each state before changing this pin");
            var start = new ProcessStartInfo("node", "\"" + Path.GetFullPath(dir + "checkDormant.cjs") + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.That(process.WaitForExit(120000), Is.True, "Dormant check timed out");
                Assert.That(process.ExitCode, Is.Zero, output.Result + error.Result);
            }
        }
    }
}
