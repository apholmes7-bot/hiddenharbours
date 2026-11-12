using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Every named fishing ground is honest data: a unique id, a region that exists, a rectangle
    /// inside that region, fish that exist and can live there, and (where the region's bed is a
    /// plan this suite can read) an authored depth range the real bed lies inside.
    /// </summary>
    public class FishingGroundContentTests
    {
        const string GroundsRoot = "Assets/_Project/Data/FishingGrounds";
        const float DepthToleranceMetres = 0.05f;

        public static FishingGroundDef[] Grounds() =>
            AssetDatabase.FindAssets("t:FishingGroundDef", new[] { GroundsRoot })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<FishingGroundDef>).Where(g => g != null).ToArray();

        /// <summary>A nine-by-nine lattice over the ground, corners included.</summary>
        public static IEnumerable<Vector2> SamplePoints(FishingGroundDef g)
        {
            for (int i = 0; i <= 8; i++)
            for (int j = 0; j <= 8; j++)
                yield return g.Center + new Vector2(g.HalfSize.x * (i / 4f - 1f), g.HalfSize.y * (j / 4f - 1f));
        }

        static RegionDef[] Regions() =>
            AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data/Regions" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<RegionDef>)
                .Where(r => r != null).ToArray();

        static FishSpeciesDef[] Fish() =>
            AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FishSpeciesDef>)
                .Where(f => f != null).ToArray();

        static FishDepthBand BandFor(float depthMetres)
        {
            var d = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset).DepthDrop;
            return DepthDropMath.ZoneForDepth(depthMetres, d.TidepoolMaxMeters, d.ShallowsMaxMeters,
                d.InshoreMaxMeters, d.MidwaterMaxMeters, d.DeepMaxMeters);
        }

        [Test]
        public void TheSouthWaterHasThreeNamedGrounds()
        {
            var ids = Grounds().Where(g => g.RegionId == SouthWaterPlan.RegionId).Select(g => g.Id).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "fishing_ground.south_water_shelf", "fishing_ground.south_water_bank",
                "fishing_ground.south_water_deep_mud",
            }, ids);
        }

        [Test]
        public void EveryGroundHasAUniquePrefixedIdAndAName()
        {
            var grounds = Grounds();
            Assert.IsNotEmpty(grounds);
            foreach (var g in grounds)
            {
                StringAssert.StartsWith("fishing_ground.", g.Id, AssetDatabase.GetAssetPath(g));
                Assert.That(g.Id, Does.Match("^fishing_ground\\.[a-z0-9_]+$"), AssetDatabase.GetAssetPath(g));
                Assert.IsNotEmpty(g.DisplayName, g.Id);
                Assert.IsNotEmpty(g.Description, g.Id);
            }
            Assert.AreEqual(grounds.Length, grounds.Select(g => g.Id).Distinct().Count());
        }

        [Test]
        public void EveryGroundLiesWhollyInsideARegionThatExists()
        {
            var regions = Regions();
            foreach (var g in Grounds())
            {
                var region = regions.SingleOrDefault(r => r.Id == g.RegionId);
                Assert.IsNotNull(region, $"{g.Id}: no region {g.RegionId}");
                var bounds = new Rect(region.WorldCenter - region.WorldSizeMeters * 0.5f, region.WorldSizeMeters);
                Assert.Greater(g.HalfSize.x, 0f, g.Id);
                Assert.Greater(g.HalfSize.y, 0f, g.Id);
                Assert.IsTrue(bounds.Contains(g.WorldRect.min) && bounds.Contains(g.WorldRect.max),
                    $"{g.Id}: {g.WorldRect} leaves {bounds}");
                Assert.IsTrue(g.Contains(g.Center));
                Assert.IsFalse(g.Contains(g.Center + g.HalfSize * 1.01f));
            }
        }

        [Test]
        public void GroundsInOneRegionDoNotOverlap()
        {
            var grounds = Grounds();
            for (int i = 0; i < grounds.Length; i++)
            for (int j = i + 1; j < grounds.Length; j++)
                if (grounds[i].RegionId == grounds[j].RegionId)
                    Assert.IsFalse(grounds[i].WorldRect.Overlaps(grounds[j].WorldRect),
                        $"{grounds[i].Id} overlaps {grounds[j].Id}");
        }

        [Test]
        public void EveryGroundSaysWhatItsBottomIsAndADepthRangeInOrder()
        {
            foreach (var g in Grounds())
            {
                Assert.AreNotEqual(EcologyHabitat.None, g.Bottom, g.Id);
                Assert.Greater(g.MinDepthMetres, 0f, g.Id);
                Assert.GreaterOrEqual(g.MaxDepthMetres, g.MinDepthMetres, g.Id);
            }
        }

        [Test]
        public void EveryGroundSaysHowHardItIsAndRockIsHarderThanMud()
        {
            const EcologyHabitat hard = EcologyHabitat.Rock | EcologyHabitat.Gravel;
            var grounds = Grounds();
            foreach (var g in grounds)
                Assert.That(g.Hardness01, Is.InRange(0f, 1f), g.Id);
            foreach (var rocky in grounds.Where(g => (g.Bottom & EcologyHabitat.Rock) != 0))
            foreach (var soft in grounds.Where(g => (g.Bottom & hard) == 0))
                Assert.Greater(rocky.Hardness01, soft.Hardness01, $"{rocky.Id} against {soft.Id}");
            Assert.IsTrue(grounds.Any(g => (g.Bottom & EcologyHabitat.Rock) != 0) &&
                          grounds.Any(g => (g.Bottom & hard) == 0), "one hard ground and one soft, at least");
        }

        [Test]
        public void TheSouthWatersRealBedLiesInsideEachGroundsAuthoredDepths()
        {
            foreach (var g in Grounds().Where(g => g.RegionId == SouthWaterPlan.RegionId))
                foreach (var p in SamplePoints(g))
                {
                    float depth = SouthWaterPlan.TideMean - SouthWaterPlan.ElevationAt(p);
                    Assert.That(depth, Is.InRange(g.MinDepthMetres - DepthToleranceMetres,
                        g.MaxDepthMetres + DepthToleranceMetres), $"{g.Id} at {p}");
                }
        }

        [Test]
        public void EveryFishAGroundHoldsExistsListsTheRegionAndLivesAtThatDepth()
        {
            var fish = Fish();
            foreach (var g in Grounds())
            {
                Assert.IsNotEmpty(g.FishIds, g.Id);
                Assert.AreEqual(g.FishIds.Length, g.FishIds.Distinct().Count(), g.Id);
                var bands = BandFor(g.MinDepthMetres) | BandFor(g.MaxDepthMetres);
                foreach (string id in g.FishIds)
                {
                    var f = fish.SingleOrDefault(x => x.Id == id);
                    Assert.IsNotNull(f, $"{g.Id}: no fish {id}");
                    Assert.IsTrue(f.RegionAllowed(g.RegionId), $"{g.Id}: {id} does not list {g.RegionId}");
                    Assert.IsTrue(f.DepthBands == FishDepthBand.None || (f.DepthBands & bands) != 0,
                        $"{g.Id}: {id} lives in {f.DepthBands}, the ground is {bands}");
                    Assert.IsTrue(g.Holds(id));
                }
                Assert.IsFalse(g.Holds("fish.not_a_fish"));
            }
        }

        [Test]
        public void EveryFishThatListsTheSouthWaterIsOnOneOfItsGrounds()
        {
            var held = Grounds().Where(g => g.RegionId == SouthWaterPlan.RegionId)
                .SelectMany(g => g.FishIds).Distinct().ToArray();
            foreach (var f in Fish().Where(f => f.RegionAllowed(SouthWaterPlan.RegionId)))
                CollectionAssert.Contains(held, f.Id);
        }
    }
}
