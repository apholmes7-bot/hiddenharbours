using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellContentTests
    {
        const string Asset = "Assets/_Project/Data/Swell/HomeBaySwell.asset";

        [Test]
        public void TheHomeBay_HasOneStableSwellClimate_AcrossEveryRegion()
        {
            string guid = Regex.Match(File.ReadAllText(Asset + ".meta"), @"guid: (\w+)").Groups[1].Value;
            Assert.AreEqual(32, guid.Length);
            StringAssert.Contains("  Id: swell.home_bay", File.ReadAllText(Asset));
            var regions = Directory.GetFiles("Assets/_Project/Data/Regions", "*.asset");
            Assert.GreaterOrEqual(regions.Length, 2);
            foreach (var region in regions)
                StringAssert.Contains("  SwellClimate: {fileID: 11400000, guid: " + guid + ", type: 2}", File.ReadAllText(region), region);
        }

        [Test]
        public void TheSwellAsset_SerializesEveryTunable_AtItsDocumentedDefault()
        {
            string yaml = File.ReadAllText(Asset);
            object defaults = SwellClimateSettings.Default;
            foreach (var field in typeof(SwellClimateSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var match = Regex.Match(yaml, @"(?m)^    " + field.Name + @": ([^\r\n]+)");
                Assert.IsTrue(match.Success, field.Name);
                Assert.AreEqual((float)field.GetValue(defaults), float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), field.Name);
            }
        }

        [Test]
        public void TheRuntime_ComposesAfterWindMemory_AndResolvesRegionDataThroughCore()
        {
            string animator = File.ReadAllText("Assets/_Project/Code/Core/Environment/WaveFieldAnimator.cs");
            StringAssert.Contains("GameServices.WithSwell(in targets, hourSeconds, gravity)", animator);
            string registrar = File.ReadAllText("Assets/_Project/Code/World/RegionSceneLoader.cs");
            StringAssert.Contains("GameServices.RegisterSwellClimate(r.Id, r.SwellClimate, r.SwellShelter)", registrar);
            string services = File.ReadAllText("Assets/_Project/Code/Core/Services/GameServices.cs");
            StringAssert.Contains("fetch.ForTrain(trains.DominantIndex, trains.Dominant.Wavelength)", services);
            StringAssert.Contains("wind.WithSwell(SwellMath.ShelterAt", services);
        }

        [Test]
        public void CalmSwell_HasAVisibleRead_WithoutAddingTheLegacyNoiseSwell()
        {
            string water = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            StringAssert.Contains("* waveFetchFactors.high.z / max(_WaveFieldParams.z, 1e-6)", water);
            Assert.AreEqual(1, Regex.Matches(water, @"swellCrest = SwellField\(worldXY, depth, t\)").Count);
            StringAssert.Contains("bool trainsLive = _WaveFieldParams.x >= 0.5;", water);
            StringAssert.Contains("if (_WaveSwell.x > 0.0) f.high.zw = swellShelter.xx;", water);
        }
    }
}
