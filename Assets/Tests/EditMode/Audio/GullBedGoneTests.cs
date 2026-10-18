using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// <b>The gull sound no longer runs under everything.</b> The director used to loop a gull bed on
    /// its own source from the first frame to the last, whether a gull was near or not. Now the only
    /// gull sound is the call player's, which calls for the birds near the view. Reads the director's
    /// source, so a looping gull source cannot come back by accident.
    /// </summary>
    public class GullBedGoneTests
    {
        private const string DirectorPath = "Assets/_Project/Code/Audio/AudioDirector.cs";

        [Test]
        public void TheAlwaysOnGullBedIsGone()
        {
            Assert.IsTrue(File.Exists(DirectorPath), DirectorPath + " is not where the test looks");
            string code = File.ReadAllText(DirectorPath);

            var failures = new List<string>();
            if (code.Contains("MakeSource(\"Gulls\"")) failures.Add("it still makes a looping \"Gulls\" source");
            if (Regex.IsMatch(code, @"\b_gull\b")) failures.Add("it still holds a _gull source");
            if (code.Contains("_gull.volume")) failures.Add("it still sets a gull source's volume every tick");
            if (code.Contains("gull AMBIENT BED")) failures.Add("its summary still promises a gull bed");

            // The controls: the file is the director, and the calls have a player.
            if (!code.Contains("MakeSource(\"Bed\"")) failures.Add("no sea bed either: this is not the director the test knows");
            if (!code.Contains("GullCallPlayer")) failures.Add("no gull call player: the gulls went silent instead");

            if (failures.Count > 0) Assert.Fail("the always-on gull bed is back:\n  " + string.Join("\n  ", failures));
        }
    }
}
