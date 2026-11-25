using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// R1, realistic catches for the current regions (2026-11-15), pinned to the data.
    ///
    /// <para><b>Every fishable region has something to catch in every season.</b> A region is fishable
    /// when its <see cref="RegionDef.SceneName"/> is a scene in the build, so a region that gains a scene
    /// joins the check by itself. The floor is one catchable species per <see cref="Season"/> value, so N
    /// is read from the Season enum, not written here. "Catchable" means a playable method can land it:
    /// the rod (<see cref="Gear.Handline"/>, <see cref="Gear.Jig"/>) or a pot (a species some authored
    /// <see cref="TrapDef"/> lists), swept over every hour and tide through the shipped
    /// <see cref="CatchResolver.Matches"/>.</para>
    ///
    /// <para><b>No species is authored where its depth band cannot exist.</b> For every species that
    /// states <see cref="FishSpeciesDef.DepthBands"/>, each region it lists must hold water reaching at
    /// least one of those bands. A region's deepest water is its authored floor plus its high water
    /// (<see cref="RegionDef.TideMeanLevel"/> + <see cref="RegionDef.TideAmplitude"/>), turned into a
    /// zone by the shipped <see cref="DepthDropMath.ZoneForDepth"/> on the shipped GameConfig. A painted
    /// region's floor is its seabed's encoded range floor (<see cref="PaintedHeightMap.MinElevation"/>),
    /// which brackets the painted bed from below, so the check can only be lenient there, never
    /// falsely red.</para>
    /// </summary>
    public class RegionCatchRealismTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string TerrainDir = "Assets/_Project/Data/Terrain/";

        // A sweep wide enough to cover any tide a region can author and every hour of the day.
        private const float SweepLowestTide = -6f;
        private const float SweepHighestTide = 6f;
        private const float SweepTideStep = 0.25f;
        private const float SweepHourStep = 0.5f;

        private static readonly Gear[] RodGear = { Gear.Handline, Gear.Jig };

        /// <summary>Where each region's floor is authored. A region a depth-banded species lists must
        /// appear here, so a new region cannot carry banded fish without saying how deep it is.</summary>
        private static readonly Dictionary<string, Func<float>> FloorElevation =
            new Dictionary<string, Func<float>>(StringComparer.Ordinal)
            {
                ["region.st_peters"] = () => Painted("StPetersSeabed.asset"),
                ["region.nine_mile_creek"] = () => Painted("NineMileCreekSeabed.asset"),
                ["region.rice_point"] = () => Painted("RicePointGround/RicePointSeabed.asset"),
                ["region.cumberland"] = () => Painted("CumberlandGround/CumberlandSeabed.asset"),
                ["region.east_water"] = () => EastWaterPlan.BayFloorElevation,
                ["region.south_water"] = () => SouthWaterPlan.DeepFloorElevation,
                ["region.coddle_cove"] = () => GreyboxBuilder.CoveDeepElevation,
            };

        private static float Painted(string relative)
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(TerrainDir + relative);
            Assert.IsNotNull(map, TerrainDir + relative + " must exist");
            return map.MinElevation;
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static List<RegionDef> FishableRegions()
        {
            var built = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) built.Add(Path.GetFileNameWithoutExtension(scene.path));
            return LoadAll<RegionDef>().Where(r => r.HasScene && built.Contains(r.SceneName)).ToList();
        }

        private static bool MatchesSomewhenIn(FishSpeciesDef fish, string regionId, Gear gear, Season season)
        {
            for (float hour = 0f; hour < 24f; hour += SweepHourStep)
                for (float tide = SweepLowestTide; tide <= SweepHighestTide; tide += SweepTideStep)
                {
                    var ctx = new CatchContext(regionId, tide, hour, season, gear);
                    if (CatchResolver.Matches(fish, in ctx)) return true;
                }
            return false;
        }

        [Test]
        public void TheFishableRegions_AreTheOnesWithABuiltScene()
        {
            var ids = FishableRegions().Select(r => r.Id).ToList();
            Assert.IsNotEmpty(ids, "the sweep needs the authored regions and the build's scene list");
            CollectionAssert.Contains(ids, "region.st_peters", "the home region is always fishable");
            CollectionAssert.Contains(ids, "region.rice_point", "R1 gives Rice Point a real table");
            CollectionAssert.Contains(ids, "region.cumberland", "R1 gives Cumberland a real table");
        }

        [Test]
        public void EveryFishableRegion_HasACatchableSpecies_InEverySeason()
        {
            List<FishSpeciesDef> fish = LoadAll<FishSpeciesDef>();
            var potted = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrapDef trap in LoadAll<TrapDef>())
                if (trap.AllowedCatchFishIds != null)
                    foreach (string id in trap.AllowedCatchFishIds) potted.Add(id);

            var failures = new List<string>();
            foreach (RegionDef region in FishableRegions())
                foreach (Season season in Enum.GetValues(typeof(Season)))
                {
                    bool any = fish.Any(f =>
                        RodGear.Any(g => MatchesSomewhenIn(f, region.Id, g, season)) ||
                        (potted.Contains(f.Id) && MatchesSomewhenIn(f, region.Id, Gear.Trap, season)));
                    if (!any) failures.Add($"{region.Id} in {season}");
                }

            Assert.IsEmpty(failures, "no playable method (rod or pot) lands anything in: " +
                                     string.Join(", ", failures));
        }

        /// <summary>The R1 additions were chosen because a method the player has today can take them.
        /// Pinned so a later edit cannot quietly move one onto a method that does not exist yet.</summary>
        [TestCase("fish.rainbow_smelt", Gear.Handline | Gear.Jig)]
        [TestCase("fish.atlantic_tomcod", Gear.Handline | Gear.Jig)]
        [TestCase("fish.cunner", Gear.Handline | Gear.Jig)]
        [TestCase("fish.green_crab", Gear.Trap)]
        [TestCase("fish.northern_quahog", Gear.ClamFork)]
        [TestCase("fish.atlantic_surf_clam", Gear.ClamFork)]
        public void TheR1Species_AreTakenOnlyByAPlayableMethod(string id, Gear gear)
        {
            FishSpeciesDef fish = LoadAll<FishSpeciesDef>().SingleOrDefault(f => f.Id == id);
            Assert.IsNotNull(fish, id + " must be authored under Data/Fish");
            Assert.AreEqual(gear, fish.AllowedGear, id);

            bool inATrap = LoadAll<TrapDef>().Any(t => t.AllowedCatchFishIds != null &&
                                                       t.AllowedCatchFishIds.Contains(id));
            Assert.AreEqual((gear & Gear.Trap) != 0, inATrap,
                id + ": a pot species must be in a pot's catch list, and nothing else may be");
        }

        [Test]
        public void NoSpeciesDepthBand_LiesWhollyOutsideEveryRegionItIsListedIn()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.GameConfigAsset);
            Assert.IsNotNull(config, "the zone thresholds come from the shipped GameConfig");
            var dd = config.DepthDrop;
            var regions = LoadAll<RegionDef>().ToDictionary(r => r.Id, StringComparer.Ordinal);

            var failures = new List<string>();
            foreach (FishSpeciesDef fish in LoadAll<FishSpeciesDef>())
            {
                if (fish.DepthBands == FishDepthBand.None) continue;   // depth-neutral by design
                foreach (string regionId in fish.RegionIds)
                {
                    if (!regions.TryGetValue(regionId, out RegionDef region))
                    {
                        failures.Add($"{fish.Id}: lists {regionId}, which is no authored region");
                        continue;
                    }
                    if (!FloorElevation.TryGetValue(regionId, out Func<float> floor))
                    {
                        failures.Add($"{fish.Id}: lists {regionId}, which has no known depth source");
                        continue;
                    }

                    float deepest = -floor() + region.TideMeanLevel + region.TideAmplitude;
                    FishDepthBand deepestZone = DepthDropMath.ZoneForDepth(deepest, dd.TidepoolMaxMeters,
                        dd.ShallowsMaxMeters, dd.InshoreMaxMeters, dd.MidwaterMaxMeters, dd.DeepMaxMeters);
                    FishDepthBand reachable = FishDepthBand.None;
                    foreach (FishDepthBand band in Enum.GetValues(typeof(FishDepthBand)))
                        if (band != FishDepthBand.None && band <= deepestZone) reachable |= band;

                    if ((fish.DepthBands & reachable) == 0)
                        failures.Add($"{fish.Id} ({fish.DepthBands}) in {regionId}: its water is " +
                                     $"{deepest:0.0} m at most, which reaches only {reachable}");
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
