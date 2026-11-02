using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    public class EastWaterTests
    {
        GameObject _bedGo;
        RectTidalTerrain _bed;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _bedGo = new GameObject("EastWater_TestBed");
            _bed = _bedGo.AddComponent<RectTidalTerrain>();
            EastWaterPlan.Plan.ConfigureTerrain(_bed);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bedGo);
            GameServices.Reset();
        }

        [Test]
        public void TheRectangleIs760By520Metres()
        {
            Assert.AreEqual(Vector2.zero, EastWaterPlan.Plan.WorldCenter);
            Assert.AreEqual(new Vector2(760f, 520f), EastWaterPlan.Plan.WorldSize);
        }

        [Test]
        public void TheWestSeamReadsStPetersOwnFloor()
        {
            var islandGo = new GameObject("Island_SeamReference");
            try
            {
                var island = islandGo.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(island);
                Assert.AreEqual(island.ElevationAt((Vector2)StPetersBuilder.ToEastWaterPassagePos),
                    _bed.ElevationAt((Vector2)EastWaterPlan.ToStPetersPassagePos), 1e-4f);
                Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, EastWaterPlan.BayFloorElevation);
            }
            finally { Object.DestroyImmediate(islandGo); }
        }

        [Test]
        public void TheTideIsTheIslandsTide()
        {
            Assert.AreEqual(StPetersBuilder.TideMean, EastWaterPlan.Plan.TideMean);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, EastWaterPlan.Plan.TideAmplitude);
            Assert.AreEqual(StPetersBuilder.TidePhaseHours, EastWaterPlan.Plan.TidePhaseHours);
        }

        [Test]
        public void TheWholeBedIsFlatAtMinusFourMetres()
        {
            Assert.IsEmpty(EastWaterPlan.Zones, "Decision 8 permits no shelf or deep step");
            var bounds = EastWaterPlan.Plan.WorldBounds;
            for (int x = 0; x <= 76; x++)
            for (int y = 0; y <= 52; y++)
            {
                var p = new Vector2(bounds.xMin + x * 10f, bounds.yMin + y * 10f);
                Assert.AreEqual(-4f, _bed.ElevationAt(p), 1e-4f, $"bed at {p}");
                Assert.AreEqual(_bed.ElevationAt(p), EastWaterPlan.ElevationAt(p));
            }
        }

        [Test]
        public void NothingBaresAndTheCapeFloatsAtSpringLow()
        {
            Assert.That(EastWaterPlan.SpringLowWater - _bed.ElevationAt(Vector2.zero),
                Is.GreaterThanOrEqualTo(StPetersBuilder.ArrivalHullDraughtMetres +
                                       StPetersBuilder.BerthUnderKeelClearance - 1e-4f));
        }

        [Test]
        public void TheDoorHasTheIslandsInsetAndFitsItsWestWall()
        {
            var bounds = EastWaterPlan.Plan.WorldBounds;
            var door = EastWaterPlan.ToStPetersPassagePos;
            var band = EastWaterPlan.Plan.EdgeBandSize;
            Assert.AreEqual(StPetersBuilder.SeaDoorInsetMetres, door.x - bounds.xMin, 1e-4f);
            Assert.AreEqual(520f, band.y);
            Assert.That(band.x, Is.GreaterThanOrEqualTo(6f));
            Assert.That(door.x - band.x * 0.5f, Is.GreaterThan(bounds.xMin));
            Assert.AreEqual(bounds.yMin, door.y - band.y * 0.5f);
            Assert.AreEqual(bounds.yMax, door.y + band.y * 0.5f);
        }

        [Test]
        public void TheArrivalIsFortyMetresInsideAndClearOfTheDoor()
        {
            float clearance = EastWaterPlan.FromStPetersArrivalPos.x - EastWaterPlan.ToStPetersPassagePos.x;
            Assert.AreEqual(40f, clearance, 1e-4f);
            Assert.That(clearance, Is.GreaterThan(EastWaterPlan.Plan.EdgeBandSize.x * 0.5f));
            Assert.AreEqual(EastWaterPlan.FromStPetersArrivalPos, EastWaterPlan.DefaultArrivalPos);
        }

        [Test]
        public void AllAuthoredPointsFitAndTheOpeningLiesInTheWestHalf()
        {
            Rect bounds = EastWaterPlan.Plan.WorldBounds;
            foreach (var p in new[] { EastWaterPlan.ToStPetersPassagePos, EastWaterPlan.DefaultArrivalPos,
                                     EastWaterPlan.OpeningLiePos })
                Assert.IsTrue(bounds.Contains((Vector2)p), $"outside rectangle: {p}");
            Assert.That(EastWaterPlan.OpeningLiePos.x, Is.LessThan(bounds.center.x));
            Assert.That(EastWaterPlan.OpeningLiePos.x - EastWaterPlan.ToStPetersPassagePos.x,
                        Is.GreaterThan(EastWaterPlan.ArrivalClearanceMetres));
        }

        [Test]
        public void TheCommittedRegionMatchesThePlanAndKeepsTheDeadSpawnListEmpty()
        {
            var def = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName));
            Assert.IsNotNull(def);
            Assert.AreEqual(EastWaterPlan.RegionId, def.Id);
            Assert.AreEqual(EastWaterPlan.SceneName, def.SceneName);
            Assert.AreEqual(EastWaterPlan.Plan.WorldSize, def.WorldSizeMeters);
            Assert.AreEqual(EastWaterPlan.Plan.WorldCenter, def.WorldCenter);
            Assert.AreEqual(4f, def.HarbourDepthMeters);
            Assert.IsFalse(def.IsDeepHarbour);
            Assert.IsEmpty(def.SpawnFishIds);
            StringAssert.Contains("working name", def.DisplayName);
        }

        [Test]
        public void TheBedIsDeterministic_TwoBuildsAgreeToTheBit()
        {
            var otherGo = new GameObject("EastWater_SecondBuild");
            try
            {
                var other = otherGo.AddComponent<RectTidalTerrain>();
                EastWaterPlan.Plan.ConfigureTerrain(other);
                var bounds = EastWaterPlan.Plan.WorldBounds;
                for (int x = 0; x <= 40; x++)
                for (int y = 0; y <= 28; y++)
                {
                    var p = new Vector2(bounds.xMin + bounds.width * x / 40f,
                                        bounds.yMin + bounds.height * y / 28f);
                    Assert.AreEqual(_bed.ElevationAt(p), other.ElevationAt(p));
                }
            }
            finally { Object.DestroyImmediate(otherGo); }
        }

        [Test]
        public void TheCommittedSceneIsEnabledInTheBuildList()
        {
            string path = WaterSceneTemplate.ScenePathFor(EastWaterPlan.SceneName);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            Assert.AreEqual(1, EditorBuildSettings.scenes.Count(s => s.path == path && s.enabled));
        }

        [Test]
        public void TheCommittedDoorsSendTheKeysTheirDestinationsRead()
        {
            var original = SceneManager.GetActiveScene();
            foreach (string name in new[] { "StPeters", EastWaterPlan.SceneName })
            {
                string path = WaterSceneTemplate.ScenePathFor(name);
                var scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var doors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RegionPassage>(true));
                    string target = name == "StPeters" ? EastWaterPlan.RegionId : "region.st_peters";
                    var door = doors.Single(p => p.Target != null && p.Target.Id == target);
                    Assert.AreEqual(name == "StPeters" ? EastWaterPlan.FromStPetersArrivalKey :
                        StPetersBuilder.FromEastWaterArrivalKey, door.ArrivalKey);
                    if (name == EastWaterPlan.SceneName)
                    {
                        Assert.AreEqual(EastWaterPlan.ToStPetersPassagePos, door.transform.position);
                        Assert.AreEqual(EastWaterPlan.Plan.EdgeBandSize, door.GetComponent<BoxCollider2D>().size);
                        var anchor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RegionAnchor>(true)).Single();
                        Assert.AreEqual(EastWaterPlan.FromStPetersArrivalPos,
                            anchor.ArrivalPointFor(EastWaterPlan.FromStPetersArrivalKey).position);
                        Assert.IsNull(anchor.DockZone);
                        Assert.IsNull(anchor.DisembarkPoint);
                    }
                }
                finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            if (original.IsValid()) SceneManager.SetActiveScene(original);
        }

        static FishSpeciesDef[] FishAssets() => AssetDatabase.FindAssets("t:FishSpeciesDef",
                new[] { "Assets/_Project/Data/Fish" })
            .Select(g => AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        [Test]
        public void ThePoolIsExactlyStPetersSpeciesWhoseBandsIncludeFourMetres()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset);
            var depth = config.DepthDrop;
            var band = DepthDropMath.ZoneForDepth(4f, depth.TidepoolMaxMeters, depth.ShallowsMaxMeters,
                depth.InshoreMaxMeters, depth.MidwaterMaxMeters, depth.DeepMaxMeters);
            Assert.AreEqual(FishDepthBand.Inshore, band);
            var all = FishAssets();
            var expected = all.Where(f => f.RegionAllowed("region.st_peters") &&
                (f.DepthBands == FishDepthBand.None || (f.DepthBands & band) != 0)).ToArray();
            Assert.AreEqual(9, expected.Length, "review the authored species set if it changes");
            foreach (var fish in all)
                Assert.AreEqual(expected.Contains(fish), fish.RegionAllowed(EastWaterPlan.RegionId),
                    AssetDatabase.GetAssetPath(fish));
        }

        sealed class FourMetreWorld : IFishSchoolWorld
        {
            public int WorldSeed => 0;
            public string RegionId => EastWaterPlan.RegionId;
            public float WaterColumnAt(Vector2 p, double seconds) => -EastWaterPlan.ElevationAt(p);
            public float SeaState01At(double seconds) => 0f;
            public Season SeasonAt(double seconds) => Season.EarlySpring;
            public float HourOfDayAt(double seconds) => 6f;
            public float TideRateMetresPerHourAt(double seconds) => 0f;
        }

        [Test]
        public void TheOrdinarySchoolModelCanPlaceTheAuthoredFishOnThisBedAtDawn()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset);
            var pool = FishAssets().Where(f => f.RegionAllowed(EastWaterPlan.RegionId)).ToArray();
            var model = new FishSchoolModel(new FourMetreWorld(), pool, config.FishSchools,
                config.DepthDrop, config.SecondsPerHour);
            var schools = new System.Collections.Generic.List<FishSchool>();
            int found = 0;
            var bounds = EastWaterPlan.Plan.WorldBounds;
            for (float x = bounds.xMin; x <= bounds.xMax; x += 11f)
            for (float y = bounds.yMin; y <= bounds.yMax; y += 11f)
            {
                model.SchoolsAt(new Vector2(x, y), config.SecondsPerHour * 6.0, schools);
                foreach (var school in schools)
                {
                    Assert.That(school.DepthMetres, Is.InRange(0f, 4f));
                    Assert.IsNotEmpty(school.SpeciesIds);
                    foreach (string id in school.SpeciesIds)
                        Assert.IsTrue(pool.Any(f => f.Id == id && !f.IsShellfish), id);
                    found++;
                }
            }
            Assert.That(found, Is.GreaterThan(0), "a flat 4 m bed must support the live school model");
        }
    }
}
