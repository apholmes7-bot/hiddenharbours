using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantSpeciesDefValidationTests
    {
        [Test] public void OneStableCodeAndIdPerSpecies()
        {
            var files = Directory.GetFiles("Assets/_Project/Data/Plants5", "Plant_*.asset");
            Assert.That(files.Length, Is.EqualTo(50));
            var ids = files.Select(p => Regex.Match(File.ReadAllText(p).Replace("\r\n", "\n"), @"(?m)^  Id: (.+)$").Groups[1].Value.Trim()).ToArray();
            var codes = files.Select(p => int.Parse(Regex.Match(File.ReadAllText(p).Replace("\r\n", "\n"), @"(?m)^  Code: (\d+)$").Groups[1].Value)).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(50)); Assert.That(codes, Is.EquivalentTo(Enumerable.Range(1, 50)));
            foreach (string old in Directory.GetFiles("Assets/_Project/Data/Decor/ShorePlants", "*.asset"))
            {
                string text = File.ReadAllText(old); string id = Regex.Match(text, @"(?m)^  Id: (.+)$").Groups[1].Value.Trim();
                Assert.That(ids, Does.Contain(id));
            }
            Assert.That(Directory.GetFiles("Assets/_Project/Data/Plants5/Recipes", "*.asset").Length, Is.EqualTo(5));
        }
        [Test] public void CodesArePinnedToKeys()
        {
            string[] keys = { "LowbushBlueberry", "SheepLaurel", "Rhodora", "BlackHuckleberry", "CommonJuniper", "Leatherleaf", "SweetGale", "WinterberryHolly", "SpeckledAlder", "PussyWillow", "RedOsierDogwood", "Meadowsweet", "Steeplebush", "Raspberry", "WildRose", "StaghornSumac", "Serviceberry", "BeakedHazelnut", "WildRaisin", "RedElderberry", "SugarKelp", "IrishMoss", "Eelgrass", "KnottedWrack", "Bladderwrack", "SeaLettuce", "Cordgrass", "Glasswort", "SaltmeadowHay", "BlackRush", "Cattail", "Threesquare", "MarramGrass", "Bayberry", "SweetFern", "BeachPea", "Lupin", "LadySlipper", "Fireweed", "QueenAnne", "Goldenrod", "OxeyeDaisy", "BlueFlag", "Buttercup", "MeadowGrass", "Timothy", "SoftRush", "TussockSedge", "OstrichFern", "MarshMarigold" };
            Assert.That(File.ReadAllText("Assets/_Project/Data/Plants5/Plant_OstrichFern.asset"), Does.Contain("Id: plant.ostrich_fern"));
            Assert.That(File.ReadAllText("Assets/_Project/Data/Plants5/Plant_MarshMarigold.asset"), Does.Contain("Id: plant.marsh_marigold"));
            for (int i = 0; i < keys.Length; i++)
            {
                string text = File.ReadAllText("Assets/_Project/Data/Plants5/Plant_" + keys[i] + ".asset").Replace("\r\n", "\n");
                Assert.That(text, Does.Contain("  Code: " + (i + 1) + "\n")); Assert.That(text, Does.Contain("  SpeciesKey: " + keys[i] + "\n"));
            }
        }
    }
}
