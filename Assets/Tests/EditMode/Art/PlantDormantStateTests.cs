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
                    Is.EqualTo("fcd3bcb768176b59b59183118043e8f7c3e5f47e7e8350b90b6fb6932b197269"), "Dormant data changed; settle each state before changing this pin");
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
