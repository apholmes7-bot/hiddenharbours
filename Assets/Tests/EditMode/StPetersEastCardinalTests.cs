using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The light station's east cardinal off the north-east bar (aid.stp_ne_bar_east_cardinal): planned on the
    /// painted seabed the scene step places it from, afloat at a spring low, the bed deeper to its east, and dressed
    /// as the kit's east cardinal with the east's light, Q(3) 10s.
    /// </summary>
    public sealed class StPetersEastCardinalTests
    {
        const string SeabedPath = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";

        static NavCardinal East() => StPetersNavMarks.Cardinals.Single(c => c.Id == StPetersNavMarks.EastCardinalId);

        static PaintedHeightField Seabed()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(SeabedPath);
            Assert.IsNotNull(map, SeabedPath + " is missing");
            Assert.IsNotNull(map.Field, "the painted seabed did not decode");
            return map.Field;
        }

        [Test]
        public void TheEastCardinal_StandsOffItsKeySceneRow_WithTheTipDueWest()
        {
            NavCardinal c = East();
            Assert.AreEqual(NavCardinalQuadrant.East, c.Quadrant);
            // The row's x; two metres south of its y, where the bar's tip is due west of her.
            Assert.AreEqual(259.8f, c.At.x, 1e-3f);
            Assert.AreEqual(105.2f - StPetersNavMarks.EastCardinalSouthOfTheRowMetres, c.At.y, 1e-3f);
            Assert.LessOrEqual(StPetersNavMarks.EastCardinalSouthOfTheRowMetres, 2f, "the mark has wandered off its row");
        }

        [Test]
        public void OnThePaintedSeabed_ItIsPlacedAfloat_WithTheBedFallingAwayEast()
        {
            PaintedHeightField bed = Seabed();
            NavCardinal c = East();
            NavMarkPlanResult plan = NavMarkPlan.Plan(StPetersNavMarks.Channels, StPetersNavMarks.Cardinals, bed.ElevationAt,
                StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude, StPetersNavMarks.Tuning);
            bool agrees = NavMarkPlan.QuadrantAgreesWithSeabed(c, bed.ElevationAt, out float safe, out float danger);
            float springLow = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            float floor = bed.ElevationAt(c.At);
            TestContext.WriteLine($"painted bed at {c.At}: {floor:0.00} m; spring low {springLow:0.00} m, depth {springLow - floor:0.00} m; " +
                                  $"{c.ProbeMetres:0} m east {safe:0.00} m, west {danger:0.00} m");
            foreach (NavMarkRefusal r in plan.Refusals) TestContext.WriteLine("refused: " + r.Reason);
            // The bed round the mark, west (the bar) to east (the deep), for whoever re-sites it.
            var grid = new System.Text.StringBuilder("bed (m) by x across, y down:\n       ");
            for (float x = c.At.x - 12f; x <= c.At.x + 12.01f; x += 2f) grid.Append($"{x,7:0.0}");
            for (float y = c.At.y + 8f; y >= c.At.y - 8.01f; y -= 2f)
            {
                grid.Append($"\n{y,7:0.0}");
                for (float x = c.At.x - 12f; x <= c.At.x + 12.01f; x += 2f) grid.Append($"{bed.ElevationAt(new Vector2(x, y)),7:0.00}");
            }
            TestContext.WriteLine(grid.ToString());

            Assert.IsTrue(agrees, $"the bed {c.ProbeMetres:0} m east is {safe:0.00} m, west {danger:0.00} m: an east mark there sends a boat shallow");
            List<PlannedNavMark> marks = plan.Marks.Where(m => m.Id == c.Id).ToList();
            Assert.AreEqual(1, marks.Count, "the east cardinal is not planned on the painted seabed");
            PlannedNavMark mark = marks[0];
            Assert.AreEqual("CardinalE", mark.MarkType);
            Assert.Greater(mark.SpringLowDepthMetres, StPetersNavMarks.Tuning.MinDepthAtSpringLowMetres,
                           "the mark would touch bottom at a spring low");
            Assert.Greater(springLow - floor, 0f, "the mark stands on ground that bares");
        }

        [Test]
        public void ItsDef_IsTheKitsEastCardinal_WithTheEastsLight()
        {
            Dictionary<string, NavBuoyDef> defs = NavMarkPlacer.LoadDefsByMarkType(StPetersNavMarks.DefFolder);
            Assert.IsTrue(defs.TryGetValue("CardinalE", out NavBuoyDef def) && def != null, "no CardinalE def under " + StPetersNavMarks.DefFolder);
            Assert.AreEqual("buoy.cardinal_east", def.Id);
            Assert.AreEqual("Q3", def.LightCharacter);
            Assert.AreEqual("Q(3) 10s", def.LightText);
        }
    }
}
