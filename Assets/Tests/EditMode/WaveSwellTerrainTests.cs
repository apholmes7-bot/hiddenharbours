using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellTerrainTests
    {
        [TestCase("StPeters", 0f, 140f, false, TestName = "StPetersShelteredCove_TheSwellIsExactlyZero_AtEveryTide")]
        [TestCase("NineMileCreek", 132f, 80f, false, TestName = "NineMileCreekHarbour_TheSwellIsExactlyZero_AtEveryTide")]
        [TestCase("NineMileCreek", 80f, 30f, false, TestName = "NineMileCreekShelteredCove_TheSwellIsExactlyZero_AtEveryTide")]
        [TestCase("StPeters", 300f, -180f, true, TestName = "StPetersOpenCoast_CarriesTheClimateSwell_AtEveryTide")]
        [TestCase("NineMileCreek", 150f, -40f, true, TestName = "NineMileCreekOpenCoast_CarriesTheClimateSwell_AtEveryTide")]
        [TestCase("Cumberland", 200f, -166f, false, TestName = "CumberlandMarina_TheSwellIsExactlyZero_AtEveryTide")]
        [TestCase("Cumberland", 300f, -240f, true, TestName = "CumberlandSouthShore_CarriesTheClimateSwell_AtEveryTide")]
        public void ThePaintedCoast_RespectsSwellShelter(string regionName, float x, float y, bool open)
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/" + regionName + ".asset");
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/" +
                (regionName == "StPeters" ? "StPetersSeabed.asset" :
                 regionName == "Cumberland" ? "CumberlandGround/CumberlandSeabed.asset" :
                 "NineMileCreekGround/NineMileCreekGround.asset"));
            Assert.IsNotNull(region); Assert.IsNotNull(map); Assert.IsNotNull(region.SwellClimate);
            Verify(new PaintedBed(map.Field), region.SwellClimate.Climate, region.SwellShelter, new Vector2(x, y), open);
        }

        sealed class PaintedBed : ITidalTerrain
        {
            readonly PaintedHeightField field;
            public PaintedBed(PaintedHeightField field) { this.field = field; }
            public float ElevationAt(Vector2 p) => field.ElevationAt(p);
        }

        [Test]
        public void StPetersExposedPier_CanCarryTheOceanSwell()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/StPetersSeabed.asset");
            Assert.IsNotNull(region); Assert.IsNotNull(map); Assert.IsNotNull(region.SwellClimate);
            VerifyExposedPier(new PaintedBed(map.Field), region.SwellClimate.Climate, region.SwellShelter);
        }

        public static void VerifyExposedPier(ITidalTerrain bed, SwellClimateSettings climate, SwellShelterSettings basins)
        {
            var at = new Vector2(211f, -6f);
            var direction = SwellMath.Direction(in climate, 12345, 0.0, 1800f);
            Assert.AreEqual(1f, basins.TransmissionAt(at));
            foreach (float tide in new[] { 0f, 2.2f })
                Assert.AreEqual(1f, SwellMath.ShelterAt(at, direction, tide, bed, in climate, in basins));
        }

        public static void Verify(ITidalTerrain bed, SwellClimateSettings climate, SwellShelterSettings basins,
                                  Vector2 at, bool open)
        {
            Assert.Less(bed.ElevationAt(at), -2.2f, "the site remains water at spring low");
            var settings = WaveSwellTests.Field();
            var glass = WaveMath.TrainsFrom(Vector2.zero, 0f, in settings);
            for (int day = 0; day <= 24; day++)
            for (int tide = -22; tide <= 22; tide++)
            {
                double time = day * 900.0;
                var sea = SwellMath.Compose(in glass, in climate, 12345, time, 1800f, settings.Gravity);
                var direction = SwellMath.Direction(in climate, 12345, time, 1800f);
                float shelter = SwellMath.ShelterAt(at, direction, tide * 0.1f, bed, in climate, in basins);
                Assert.AreEqual(open ? 1f : 0f, shelter);
                var fetch = WaveFetchSample.Open.WithSwell(shelter);
                if (open)
                    Assert.AreEqual(SwellMath.Height(in climate, 12345, time, 1800f) * 0.5f,
                        sea.TotalAmplitude, 0.00000002f);
                else
                {
                    for (int i = 0; i < 8; i++) Assert.AreEqual(0f, sea[i].Amplitude * fetch.ForTrain(i, sea[i].Wavelength));
                    var surface = WaveMath.Sample(at, time, in sea, fetch);
                    Assert.AreEqual(0f, surface.Height); Assert.AreEqual(Vector2.zero, surface.Slope);
                    Assert.AreEqual(0f, surface.CrestFactor);
                }
            }
        }
    }
}
