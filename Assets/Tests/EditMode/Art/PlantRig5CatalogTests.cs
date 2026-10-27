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
                Is.EqualTo("1c71a9b2eecc28b395da4ce2a932ab150c2f96ec82880774a9afc66824483833"));
            var keys = Regex.Matches(File.ReadAllText(dir + "catalog.json"), "\"key\": \"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            byte[] extra = File.ReadAllBytes(dir + "afPieces.js");
            using (var sha = SHA256.Create()) Assert.That(string.Concat(sha.ComputeHash(extra).Select(b => b.ToString("x2"))),
                Is.EqualTo("9f46015cd644965c6c358d5c61715746379bc69aa55edb0696ff80a1aeae5fe8"));
            // Rig 5.1 owns all 50 keys; the pinned afPieces block now skips its two additions.
            var rig = Regex.Matches(System.Text.Encoding.UTF8.GetString(bytes), "\\{ key: '([^']+)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.That(rig.Length, Is.EqualTo(50)); Assert.That(rig.Distinct().Count(), Is.EqualTo(50));
            Assert.That(keys.Length, Is.EqualTo(50)); Assert.That(keys.Distinct().Count(), Is.EqualTo(50)); Assert.That(keys, Is.EquivalentTo(rig));
        }
        [Test] public void NoDimensionExceeds2048()
        {
            var dims = Regex.Matches(File.ReadAllText("docs/art/rigs/plants/catalog.json"), "\"[wh]\": (\\d+)");
            string catalog = File.ReadAllText("docs/art/rigs/plants/catalog.json");
            Assert.That(Regex.Matches(catalog, "\"stage\":").Count, Is.EqualTo(5472));
            Assert.That(Regex.Matches(catalog, "\"state\": \"DORMANT\"").Count, Is.EqualTo(24));
            Assert.That(dims.Count, Is.EqualTo((5448 + 50) * 2));
            foreach (Match m in dims) Assert.That(int.Parse(m.Groups[1].Value), Is.InRange(1, 2048));
        }
    }
}
