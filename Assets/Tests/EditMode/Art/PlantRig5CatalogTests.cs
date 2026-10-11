using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantRig5CatalogTests
    {
        [Test] public void CatalogKeysEqualThePinnedRig()
        {
            string dir = "docs/art/rigs/plants/";
            byte[] bytes = File.ReadAllBytes(dir + "plantIsoRig5.js");
            using (var sha = SHA256.Create()) Assert.That(string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2"))),
                Is.EqualTo("a80c485993a1a05930849298d688572864deaa873d6aa62435135bfc3ff0059b"));
            var keys = Regex.Matches(File.ReadAllText(dir + "catalog.json"), "\"key\": \"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            byte[] extra = File.ReadAllBytes(dir + "afPieces.js");
            using (var sha = SHA256.Create()) Assert.That(string.Concat(sha.ComputeHash(extra).Select(b => b.ToString("x2"))),
                Is.EqualTo("9f46015cd644965c6c358d5c61715746379bc69aa55edb0696ff80a1aeae5fe8"));
            var rig = Regex.Matches(System.Text.Encoding.UTF8.GetString(bytes) + System.Text.Encoding.UTF8.GetString(extra), "\\{ key: '([^']+)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.That(keys.Length, Is.EqualTo(50)); Assert.That(keys.Distinct().Count(), Is.EqualTo(50)); Assert.That(keys, Is.EquivalentTo(rig));
        }
        [Test] public void NoDimensionExceeds2048()
        {
            var dims = Regex.Matches(File.ReadAllText("docs/art/rigs/plants/catalog.json"), "\"[wh]\": (\\d+)");
            string catalog = File.ReadAllText("docs/art/rigs/plants/catalog.json");
            Assert.That(Regex.Matches(catalog, "\"stage\":").Count, Is.EqualTo(5472));
            Assert.That(Regex.Matches(catalog, "\"state\": \"DORMANT\"").Count, Is.EqualTo(64));
            Assert.That(dims.Count, Is.EqualTo((5408 + 50) * 2));
            foreach (Match m in dims) Assert.That(int.Parse(m.Groups[1].Value), Is.InRange(1, 2048));
        }
    }
}
