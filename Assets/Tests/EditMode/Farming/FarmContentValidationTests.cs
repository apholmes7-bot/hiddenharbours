using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the farm Defs (farm slice 1). The assets were hand-authored as YAML, so every
    /// check reads what Unity LOADS, never the file text: a bad indent fails silently to the field default,
    /// and only the loaded object tells.
    /// </summary>
    public class FarmContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string FarmRoot = "Assets/_Project/Data/Resources/" + FarmService.ResourcesFolder + "/";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private static readonly Regex CropId = new Regex("^crop\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex PlotId = new Regex("^plot\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex ProduceId = new Regex("^produce\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        private static int DaysPerSeason()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            return config != null ? Mathf.Max(1, config.DaysPerSeason) : GameConfig.DefaultDaysPerSeason;
        }

        private static void CheckIds<T>(List<T> all, Regex shape, System.Func<T, string> id) where T : Object
        {
            var seen = new HashSet<string>();
            foreach (T def in all)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsTrue(shape.IsMatch(id(def) ?? ""), path + ": bad id " + id(def));
                Assert.IsTrue(seen.Add(id(def)), path + ": duplicate id " + id(def));
                StringAssert.StartsWith(FarmRoot, path, "the farm service loads only Resources/" + FarmService.ResourcesFolder);
            }
        }

        [Test]
        public void TheSlice1Content_Ships_WithWellFormedUniqueIds()
        {
            List<CropDef> crops = LoadAll<CropDef>();
            List<FarmPlotDef> plots = LoadAll<FarmPlotDef>();
            List<ProduceDef> produce = LoadAll<ProduceDef>();
            CheckIds(crops, CropId, d => d.Id);
            CheckIds(plots, PlotId, d => d.Id);
            CheckIds(produce, ProduceId, d => d.Id);

            var cropIds = crops.ConvertAll(c => c.Id);
            foreach (string id in new[] { "crop.potato", "crop.turnip", "crop.cabbage", "crop.carrot" })
                CollectionAssert.Contains(cropIds, id);
            var plotIds = plots.ConvertAll(p => p.Id);
            CollectionAssert.Contains(plotIds, "plot.ginny_kitchen");
            CollectionAssert.Contains(plotIds, "plot.stp_homestead_field");
            foreach (CropDef c in crops) Assert.IsFalse(string.IsNullOrWhiteSpace(c.DisplayName), c.Id);
            foreach (FarmPlotDef p in plots) Assert.IsFalse(string.IsNullOrWhiteSpace(p.DisplayName), p.Id);
            foreach (ProduceDef p in produce) Assert.IsFalse(string.IsNullOrWhiteSpace(p.DisplayName), p.Id);
        }

        [Test]
        public void EveryCrop_FitsTheCalendar_AndEverySowingCanBeHarvestedInTime()
        {
            int len = DaysPerSeason();
            var produceIds = new HashSet<string>(LoadAll<ProduceDef>().ConvertAll(p => p.Id));
            foreach (CropDef c in LoadAll<CropDef>())
            {
                foreach (SeasonDay d in new[] { c.PlantFrom, c.PlantTo, c.HarvestFrom, c.HarvestTo })
                {
                    Assert.IsTrue(System.Enum.IsDefined(typeof(Season), d.Season), c.Id + ": unknown season");
                    Assert.That(d.Day, Is.InRange(1, len), c.Id + ": " + d + " is outside a " + len + "-day season");
                }
                int plantFrom = FarmMath.DayOfYear(c.PlantFrom, len);
                int plantTo = FarmMath.DayOfYear(c.PlantTo, len);
                int harvestFrom = FarmMath.DayOfYear(c.HarvestFrom, len);
                int harvestTo = FarmMath.DayOfYear(c.HarvestTo, len);
                Assert.LessOrEqual(plantFrom, plantTo, c.Id + ": the sowing window runs backwards (windows never wrap the year)");
                Assert.Less(harvestFrom, harvestTo, c.Id + ": the harvest window needs at least two days");
                Assert.Less(plantFrom, harvestFrom, c.Id + ": harvest opens before sowing does");
                Assert.GreaterOrEqual(plantFrom, c.TillLeadDays, c.Id + ": tilling would fall in the year before");
                Assert.LessOrEqual(plantTo + c.DaysToMature + 1, harvestTo,
                    c.Id + ": a late sowing could not stand ready a day before the window closes");
                Assert.That(c.SproutAt, Is.GreaterThan(0f).And.LessThan(c.GrowingAt), c.Id + ": 0 < SproutAt < GrowingAt");
                Assert.Less(c.GrowingAt, 1f, c.Id);
                Assert.Greater(c.YieldPerCell, 0, c.Id + ": a crop that yields nothing");
                Assert.IsTrue(produceIds.Contains(c.ProduceId ?? ""), c.Id + ": produce " + c.ProduceId + " is not a ProduceDef");
            }
        }

        [Test]
        public void EveryShippedCrop_WalksItsStages_AndIsReadyOnlyInItsWindow()
        {
            int len = DaysPerSeason();
            foreach (CropDef c in LoadAll<CropDef>())
            {
                int harvestFrom = FarmMath.DayOfYear(c.HarvestFrom, len);
                int harvestTo = FarmMath.DayOfYear(c.HarvestTo, len);
                for (int seed = 0; seed < 40; seed++)
                {
                    FarmPlotSchedule s = FarmMath.ScheduleFor("plot.check", c, seed, 1 + seed % 4, len);
                    FarmStage last = FarmStage.Fallow;
                    bool ready = false;
                    for (int doy = 0; doy < FarmMath.DaysPerYear(len); doy++)
                    {
                        FarmPlotState st = FarmMath.StateOn(s, c, 1, doy);
                        Assert.GreaterOrEqual((int)st.Stage, (int)last, c.Id + " seed " + seed + " day " + doy);
                        if (st.IsReady)
                        {
                            ready = true;
                            Assert.That(doy, Is.InRange(harvestFrom, harvestTo), c.Id + ": ready outside the window");
                        }
                        last = st.Stage;
                    }
                    Assert.IsTrue(ready, c.Id + " seed " + seed + ": never stood ready");
                    Assert.AreEqual(FarmStage.Stubble, last, c.Id + " seed " + seed + ": never harvested");
                }
            }
        }

        [Test]
        public void EveryProduceItem_EatsBetterPrepped_ThanRaw()
        {
            foreach (ProduceDef p in LoadAll<ProduceDef>())
            {
                Assert.GreaterOrEqual(p.BaseValue, 0, p.Id);
                Assert.Greater(p.PreppedHungerRelief, 0f, p.Id + ": prepped food must feed you");
                Assert.Less(p.RawHungerRelief, p.PreppedHungerRelief, p.Id + ": raw must feed less than prepped");
                if (p.EatenRaw) Assert.Greater(p.RawHungerRelief, 0f, p.Id + ": eaten raw but relieves nothing");
                else Assert.AreEqual(0f, p.RawHungerRelief, p.Id + ": not eaten raw, so raw relief is 0");
            }
        }

        [Test]
        public void EveryPlot_ResolvesItsCrops_AndItsTendersOrFlagsThemPlaceholder()
        {
            var cropIds = new HashSet<string>(LoadAll<CropDef>().ConvertAll(c => c.Id));
            var npcIds = new HashSet<string>(LoadAll<NpcDef>().ConvertAll(n => n.Id));
            var placeholders = new List<string>();
            foreach (FarmPlotDef p in LoadAll<FarmPlotDef>())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.RegionId), p.Id + ": no region");
                Assert.Greater(p.CellCount, 0, p.Id + ": no cells");
                Assert.IsNotNull(p.CropRotation, p.Id);
                Assert.IsNotEmpty(p.CropRotation, p.Id + ": no rotation");
                bool anyCrop = false;
                foreach (string id in p.CropRotation)
                {
                    if (string.IsNullOrEmpty(id)) continue;   // a rest year
                    anyCrop = true;
                    Assert.IsTrue(cropIds.Contains(id), p.Id + ": rotation names unknown crop " + id);
                }
                Assert.IsTrue(anyCrop, p.Id + ": a rotation of rest years only");

                if (p.AreaIsPlaceholder)
                {
                    Assert.AreEqual(0f, p.Area.width * p.Area.height, p.Id + ": a placeholder plot has no area yet");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.PlacementNote), p.Id + ": say where it is meant to go");
                }
                else Assert.Greater(p.Area.width * p.Area.height, 0f, p.Id + ": a placed plot needs an area");

                Assert.IsNotNull(p.TenderNpcIds, p.Id);
                Assert.IsNotEmpty(p.TenderNpcIds, p.Id + ": nobody tends it");
                foreach (string t in p.TenderNpcIds)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(t), p.Id + ": blank tender");
                    if (npcIds.Contains(t)) continue;
                    Assert.IsTrue(p.TendersArePlaceholder, p.Id + ": tender " + t + " is not an NpcDef and the plot is not flagged placeholder");
                    placeholders.Add(p.Id + " -> " + t);
                }

                Assert.IsTrue(System.Enum.IsDefined(typeof(FarmPlotAccess), p.Access), p.Id + ": unknown access");
                if (p.Access == FarmPlotAccess.SideJobByApproval)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.ApproverNpcId), p.Id + ": a side job needs an approver");
                if (p.Access == FarmPlotAccess.ForPurchase)
                    Assert.Greater(p.PurchasePrice, 0, p.Id + ": a plot for sale needs a price");
            }
            foreach (string line in placeholders) TestContext.WriteLine("placeholder tender: " + line);
        }

        [Test]
        public void GinnysPlot_IsHers_AndOpenToHelp()
        {
            FarmPlotDef ginny = LoadAll<FarmPlotDef>().Find(p => p.Id == "plot.ginny_kitchen");
            Assert.IsNotNull(ginny);
            Assert.AreEqual("region.st_peters", ginny.RegionId);
            CollectionAssert.Contains(ginny.TenderNpcIds, "npc.aunt_ginny");
            Assert.IsFalse(ginny.TendersArePlaceholder, "Ginny is a real NpcDef");
            Assert.AreEqual(FarmPlotAccess.OpenToHelp, ginny.Access, "owner ruling: the player starts by helping on Ginny's plot");
        }
    }
}
