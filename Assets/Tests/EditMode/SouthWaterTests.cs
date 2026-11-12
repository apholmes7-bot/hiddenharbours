using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The South Water's region, bed and depths (region PR 1). Its seams arrive with their own tests.
    /// </summary>
    public class SouthWaterTests
    {
        static readonly string[] TrawlerHullAssets =
        {
            "Assets/_Project/Data/Boats/SternTrawler.asset",
            "Assets/_Project/Data/Boats/SternTrawlerMk2.asset",
            "Assets/_Project/Data/Boats/SideDragger.asset",
        };

        GameObject _bedGo;
        RectTidalTerrain _bed;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _bedGo = new GameObject("SouthWater_TestBed");
            _bed = _bedGo.AddComponent<RectTidalTerrain>();
            SouthWaterPlan.Plan.ConfigureTerrain(_bed);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bedGo);
            GameServices.Reset();
        }

        static Vector2 NorthEdgeAt(float x) => new Vector2(x, SouthWaterPlan.NorthEdgeY);
        static Vector2 DoorLineAt(float x) =>
            new Vector2(x, SouthWaterPlan.NorthEdgeY - SouthWaterPlan.DoorInsetMetres);

        [Test]
        public void TheRegionIsOneRectangle2400By1200Metres()
        {
            Assert.AreEqual(Vector2.zero, SouthWaterPlan.Plan.WorldCenter);
            Assert.AreEqual(new Vector2(2400f, 1200f), SouthWaterPlan.Plan.WorldSize);
            Assert.AreEqual("region.south_water", SouthWaterPlan.RegionId);
        }

        [Test]
        public void TheTideIsTheIslandsTide()
        {
            Assert.AreEqual(StPetersBuilder.TideMean, SouthWaterPlan.Plan.TideMean);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, SouthWaterPlan.Plan.TideAmplitude);
            Assert.AreEqual(StPetersBuilder.TidePhaseHours, SouthWaterPlan.Plan.TidePhaseHours);
        }

        [Test]
        public void TheNorthEdgeIsAboutTwentyMetresDeepBetweenTheApproaches()
        {
            float clear = SouthWaterPlan.ApproachHalfSize.x + SouthWaterPlan.ApproachFalloffMetres;
            float[] approaches =
            {
                SouthWaterPlan.RicePointApproachX, SouthWaterPlan.StPetersApproachX,
                SouthWaterPlan.EastWaterApproachX,
            };
            int sampled = 0;
            var bounds = SouthWaterPlan.Plan.WorldBounds;
            for (float x = bounds.xMin; x <= bounds.xMax; x += 10f)
            {
                if (approaches.Any(a => Mathf.Abs(x - a) <= clear)) continue;
                Assert.AreEqual(SouthWaterPlan.ShelfElevation, _bed.ElevationAt(NorthEdgeAt(x)), 1e-4f, $"x {x}");
                Assert.AreEqual(SouthWaterPlan.ShelfElevation, _bed.ElevationAt(DoorLineAt(x)), 1e-4f, $"x {x}");
                sampled++;
            }
            Assert.Greater(sampled, 100, "the north edge is shelf wherever no neighbour meets it");
            Assert.AreEqual(-20f, SouthWaterPlan.ShelfElevation);
        }

        [Test]
        public void TheWaterDeepensSouthwardToTrawlingDepth()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset);
            var d = config.DepthDrop;
            float x = 500f;
            float last = _bed.ElevationAt(NorthEdgeAt(x));
            for (float y = SouthWaterPlan.NorthEdgeY; y >= SouthWaterPlan.Plan.WorldBounds.yMin; y -= 10f)
            {
                float e = _bed.ElevationAt(new Vector2(x, y));
                Assert.LessOrEqual(e, last + 1e-4f, $"the bed never rises going south at y {y}");
                last = e;
            }
            Assert.AreEqual(SouthWaterPlan.DeepFloorElevation, last, 1e-4f);
            Assert.AreEqual(FishDepthBand.Deep, DepthDropMath.ZoneForDepth(-SouthWaterPlan.DeepFloorElevation,
                d.TidepoolMaxMeters, d.ShallowsMaxMeters, d.InshoreMaxMeters, d.MidwaterMaxMeters, d.DeepMaxMeters));
            Assert.AreEqual(FishDepthBand.Midwater, DepthDropMath.ZoneForDepth(-SouthWaterPlan.ShelfElevation,
                d.TidepoolMaxMeters, d.ShallowsMaxMeters, d.InshoreMaxMeters, d.MidwaterMaxMeters, d.DeepMaxMeters));
        }

        [Test]
        public void MostOfTheRegionIsTheDeepFloor()
        {
            var bounds = SouthWaterPlan.Plan.WorldBounds;
            int deep = 0, all = 0;
            for (float x = bounds.xMin + 10f; x < bounds.xMax; x += 20f)
            for (float y = bounds.yMin + 10f; y < bounds.yMax; y += 20f)
            {
                all++;
                if (_bed.ElevationAt(new Vector2(x, y)) <= SouthWaterPlan.DeepFloorElevation + 0.5f) deep++;
            }
            Assert.Greater(deep / (float)all, 0.5f);
        }

        [Test]
        public void TheBankStandsUpOffTheDeepFloor()
        {
            Assert.AreEqual(SouthWaterPlan.BankElevation, _bed.ElevationAt(SouthWaterPlan.BankCenter), 1e-4f);
            Assert.Greater(SouthWaterPlan.BankElevation, SouthWaterPlan.DeepFloorElevation);
            Assert.Less(SouthWaterPlan.BankElevation, SouthWaterPlan.ShelfElevation);
            var beyond = SouthWaterPlan.BankCenter + new Vector2(
                SouthWaterPlan.BankHalfSize.x + SouthWaterPlan.BankFalloffMetres + 1f, 0f);
            Assert.AreEqual(SouthWaterPlan.DeepFloorElevation, _bed.ElevationAt(beyond), 1e-4f);
        }

        [Test]
        public void EachApproachReadsItsNeighboursOwnFloorAtTheEdgeAndTheDoorLine()
        {
            var cases = new (float X, float Floor, string Name)[]
            {
                (SouthWaterPlan.RicePointApproachX, SouthWaterPlan.RicePointChannelBedElevation, "Rice Point"),
                (SouthWaterPlan.StPetersApproachX, StPetersBuilder.DeepHarbourElevation, "St Peters"),
                (SouthWaterPlan.EastWaterApproachX, EastWaterPlan.BayFloorElevation, "East Water"),
            };
            foreach (var c in cases)
            {
                for (float dx = -SouthWaterPlan.ApproachHalfSize.x; dx <= SouthWaterPlan.ApproachHalfSize.x; dx += 10f)
                {
                    Assert.AreEqual(c.Floor, _bed.ElevationAt(NorthEdgeAt(c.X + dx)), 1e-4f, $"{c.Name} edge {dx}");
                    Assert.AreEqual(c.Floor, _bed.ElevationAt(DoorLineAt(c.X + dx)), 1e-4f, $"{c.Name} door {dx}");
                }
            }
            Assert.AreEqual(-7f, SouthWaterPlan.RicePointChannelBedElevation,
                "the bed of the channel Rice Point dredges to its south edge");
        }

        [Test]
        public void TheApproachesDoNotOverlap()
        {
            float reach = SouthWaterPlan.ApproachHalfSize.x + SouthWaterPlan.ApproachFalloffMetres;
            Assert.Greater(SouthWaterPlan.StPetersApproachX - SouthWaterPlan.RicePointApproachX, 2f * reach);
            Assert.Greater(SouthWaterPlan.EastWaterApproachX - SouthWaterPlan.StPetersApproachX, 2f * reach);
            var bounds = SouthWaterPlan.Plan.WorldBounds;
            Assert.Greater(SouthWaterPlan.RicePointApproachX - reach, bounds.xMin);
            Assert.Less(SouthWaterPlan.EastWaterApproachX + reach, bounds.xMax);
        }

        [Test]
        public void FortyMetresInsideEachDoorTheWaterIsAlreadyDeep()
        {
            float y = SouthWaterPlan.NorthEdgeY - SouthWaterPlan.DoorInsetMetres - SouthWaterPlan.ArrivalClearanceMetres;
            foreach (float x in new[] { SouthWaterPlan.RicePointApproachX, SouthWaterPlan.StPetersApproachX,
                                        SouthWaterPlan.EastWaterApproachX })
                Assert.That(SouthWaterPlan.SpringLowDepthAt(new Vector2(x, y)), Is.GreaterThan(10f), $"x {x}");
        }

        [Test]
        public void NothingBaresAndTheCapeFloatsEverywhereAtSpringLow()
        {
            float need = StPetersBuilder.ArrivalHullDraughtMetres + StPetersBuilder.BerthUnderKeelClearance;
            var bounds = SouthWaterPlan.Plan.WorldBounds;
            for (float x = bounds.xMin; x <= bounds.xMax; x += 20f)
            for (float y = bounds.yMin; y <= bounds.yMax; y += 20f)
                Assert.That(SouthWaterPlan.SpringLowWater - _bed.ElevationAt(new Vector2(x, y)),
                    Is.GreaterThanOrEqualTo(need - 1e-4f), $"at ({x}, {y})");
            Assert.That(SouthWaterPlan.SpringLowDepthAt(SouthWaterPlan.DefaultArrivalPos), Is.GreaterThan(10f));
        }

        [Test]
        public void ATrawlerFloatsInTheRicePointApproachAtSpringLow()
        {
            float deepest = 0f;
            foreach (string path in TrawlerHullAssets)
            {
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                Assert.IsNotNull(hull, path);
                deepest = Mathf.Max(deepest, hull.DraughtMeters);
            }
            Assert.Greater(deepest, 3f, "the trawlers are the deep hulls this approach is cut for");
            float water = SouthWaterPlan.SpringLowDepthAt(DoorLineAt(SouthWaterPlan.RicePointApproachX));
            Assert.That(water, Is.GreaterThanOrEqualTo(deepest + StPetersBuilder.BerthUnderKeelClearance),
                $"{water} m of water for a hull drawing {deepest} m");
        }

        [Test]
        public void ATrawlerFloatsOverEveryGroundAtSpringLow()
        {
            float deepest = TrawlerHullAssets.Max(p => AssetDatabase.LoadAssetAtPath<BoatHullDef>(p).DraughtMeters);
            foreach (var ground in FishingGroundContentTests.Grounds()
                         .Where(g => g.RegionId == SouthWaterPlan.RegionId))
                foreach (var p in FishingGroundContentTests.SamplePoints(ground))
                    Assert.That(SouthWaterPlan.SpringLowDepthAt(p), Is.GreaterThan(deepest * 2f), $"{ground.Id} at {p}");
        }

        [Test]
        public void TheBedIsDeterministic_TwoBuildsAgreeToTheBit()
        {
            var otherGo = new GameObject("SouthWater_SecondBuild");
            try
            {
                var other = otherGo.AddComponent<RectTidalTerrain>();
                SouthWaterPlan.Plan.ConfigureTerrain(other);
                var bounds = SouthWaterPlan.Plan.WorldBounds;
                for (int x = 0; x <= 60; x++)
                for (int y = 0; y <= 30; y++)
                {
                    var p = new Vector2(bounds.xMin + bounds.width * x / 60f,
                                        bounds.yMin + bounds.height * y / 30f);
                    Assert.AreEqual(_bed.ElevationAt(p), other.ElevationAt(p));
                    Assert.AreEqual(_bed.ElevationAt(p), SouthWaterPlan.ElevationAt(p));
                }
            }
            finally { Object.DestroyImmediate(otherGo); }
        }

        [Test]
        public void TheCommittedRegionMatchesThePlan()
        {
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(SouthWaterPlan.RegionAssetName));
            Assert.IsNotNull(def);
            Assert.AreEqual(SouthWaterPlan.RegionId, def.Id);
            Assert.AreEqual(SouthWaterPlan.SceneName, def.SceneName);
            Assert.AreEqual(SouthWaterPlan.Plan.WorldSize, def.WorldSizeMeters);
            Assert.AreEqual(SouthWaterPlan.Plan.WorldCenter, def.WorldCenter);
            Assert.AreEqual(SouthWaterPlan.SeabedPixelsPerMetre, def.SeabedPixelsPerMetre);
            Assert.AreEqual(20f, def.HarbourDepthMeters);
            Assert.AreEqual(SouthWaterPlan.TideAmplitude, def.TideAmplitude);
            Assert.IsEmpty(def.SpawnFishIds);
            Assert.IsEmpty(def.UnlockFlag ?? "", "no gate: nothing in this PR locks the region");
            StringAssert.Contains("working name", def.DisplayName);
        }

        [Test]
        public void TheCommittedSceneIsEnabledInTheBuildList()
        {
            string path = WaterSceneTemplate.ScenePathFor(SouthWaterPlan.SceneName);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            Assert.AreEqual(1, EditorBuildSettings.scenes.Count(s => s.path == path && s.enabled));
        }
    }
}
