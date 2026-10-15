using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantAddedSpeciesTests
    {
        [Test] public void BothAddedSpeciesDrawEveryStateWithoutPieceLighting()
        {
            var start = new ProcessStartInfo("node", "\"" + Path.GetFullPath("docs/art/rigs/plants/checkNewSpecies.cjs") + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.That(process.WaitForExit(120000), Is.True, "Added plant check timed out");
                Assert.That(process.ExitCode, Is.Zero, output.Result + error.Result);
            }
        }
    }
}
