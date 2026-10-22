using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersLadderContentTests
    {
        [Test]
        public void TheVisibleWharfLadder_HasItsRuntimeClimbAndMeasuredTop()
        {
            string scene = File.ReadAllText("Assets/_Project/Scenes/StPeters.unity");
            string meta = File.ReadAllText("Assets/_Project/Code/World/WharfLadder.cs.meta");
            string guid = Regex.Match(meta, @"(?m)^guid: (\w+)").Groups[1].Value;
            string[] docs = Regex.Split(scene, @"(?m)(?=^--- !u!)");
            string[] ladders = docs.Where(d => d.Contains("guid: " + guid + ",")).ToArray();
            Assert.AreEqual(StPetersWharf.Fittings().Count(f => f.Name == "ladder"), ladders.Length,
                "a painted ladder without the runtime component cannot offer a low-water climb");
            Assert.AreEqual(1, ladders.Length);
            StringAssert.Contains("  _id: wharf.st_peters.ladder_0", ladders[0]);
            string deck = docs.Single(d => d.Contains("\n  _id: wharf.st_peters\n"));
            string elevation = Regex.Match(deck, @"(?m)^  _deckElevation: (.+)$").Groups[1].Value.Trim();
            StringAssert.Contains("  _topElevationMeters: " + elevation, ladders[0]);
            StringAssert.Contains("  _faceHeadingDegrees: 180", ladders[0]);
            string goId = Regex.Match(ladders[0], @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            string transform = docs.Single(d => d.StartsWith("--- !u!4 ") &&
                d.Contains("m_GameObject: {fileID: " + goId + "}"));
            var fitting = StPetersWharf.LadderPosition();
            string pos = $"  m_LocalPosition: {{x: {fitting.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                         $"y: {fitting.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, z: 0}}";
            StringAssert.Contains(pos, transform, "runtime and visible fitting share one placement source");
        }
    }
}
