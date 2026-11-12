using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rice Point's wharf takes the trawlers. The berths are data (<see cref="BerthDef"/>), the depth
    /// is the region's ground (the issued base, dredged as <c>harbour-works.json</c> says), and these
    /// tests hold the two together at the lowest tide of the year: every trawler's draught against
    /// every trawler berth and the whole way in from the south edge.
    /// </summary>
    public sealed class RicePointTrawlerBerthTests
    {
        const string BerthFolder = "Assets/_Project/Data/Berths";
        const float SampleStepMeters = 0.25f;

        static readonly string[] TrawlerPaths = {
            "Assets/_Project/Data/Boats/SternTrawler.asset",
            "Assets/_Project/Data/Boats/SternTrawlerMk2.asset",
            "Assets/_Project/Data/Boats/SideDragger.asset",
        };

        static readonly string[] BerthPaths = {
            BerthFolder + "/RicePointTrawlerFace.asset",
            BerthFolder + "/RicePointTrawlerOutside.asset",
        };

        static LandScenePlanDef Plan() { var plan = RicePointBuilder.LoadPlan(); Assert.That(plan, Is.Not.Null); return plan; }
        static BerthDef Berth(string path) { var b = AssetDatabase.LoadAssetAtPath<BerthDef>(path); Assert.That(b, Is.Not.Null, path); return b; }
        static BoatHullDef Hull(string path) { var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path); Assert.That(h, Is.Not.Null, path); return h; }

        /// <summary>The lowest water of the year: the tide's mean less its whole amplitude (the envelope peaks at one).</summary>
        static float LowestTide(LandScenePlanDef plan) => plan.Region.TideMeanLevel - plan.Region.TideAmplitude;

        static GroundFileIntake.HarbourWorks Works() => GroundFileIntake.ReadHarbourWorks(
            File.ReadAllText(RicePointBuilder.GroundFolder + "/" + GroundFileIntake.RicePointWorksFile));

        [Test]
        public void ThereAreAtLeastTwoTrawlerBerths_EachWithAStableIdInThisRegion()
        {
            var plan = Plan();
            var berths = AssetDatabase.FindAssets("t:BerthDef", new[] { BerthFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BerthDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b.RegionId == plan.Region.Id).ToArray();
            Assert.That(berths.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(berths.Select(b => b.Id).Distinct().Count(), Is.EqualTo(berths.Length));
            foreach (var b in berths) Assert.That(b.Id, Does.StartWith("berth.rpt_"));
            Assert.That(BerthPaths.Select(p => Berth(p).Id), Is.SubsetOf(berths.Select(b => b.Id)));
        }

        [Test]
        public void EachTrawlerFloatsInEachTrawlerBerth_AtTheLowestTideOfTheYear(
            [ValueSource(nameof(TrawlerPaths))] string hullPath, [ValueSource(nameof(BerthPaths))] string berthPath)
        {
            var plan = Plan(); var hull = Hull(hullPath); var berth = Berth(berthPath);
            float halfBeam = HullPresence.HalfBeamOf(hull);
            Assert.That(berth.Takes(hull.LengthMeters, halfBeam, hull.DraughtMeters), Is.True,
                $"{berth.Id} takes {berth.LengthMeters:F1} m by {berth.BeamMeters:F1} m drawing {berth.MaxDraughtMeters:F1} m; " +
                $"{hull.Id} is {hull.LengthMeters:F1} m by {halfBeam * 2f:F1} m drawing {hull.DraughtMeters:F1} m");
            float bed = berth.ShallowestBedInBerth(plan.Ground.Field.ElevationAt, SampleStepMeters);
            float water = LowestTide(plan) - bed;
            Assert.That(water, Is.GreaterThanOrEqualTo(hull.DraughtMeters + berth.KeelClearanceMeters),
                $"{hull.Id} draws {hull.DraughtMeters:F2} m; {berth.Id} has {water:F2} m at the lowest tide " +
                $"({LowestTide(plan):F2} m) over its shallowest spot ({bed:F2} m) and promises {berth.KeelClearanceMeters:F2} m under her");
        }

        [Test]
        public void EachTrawlerFloatsTheWholeWayIn_FromTheSouthEdge_AtTheLowestTideOfTheYear(
            [ValueSource(nameof(TrawlerPaths))] string hullPath, [ValueSource(nameof(BerthPaths))] string berthPath)
        {
            var plan = Plan(); var hull = Hull(hullPath); var berth = Berth(berthPath);
            Assert.That(berth.Approach.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(berth.ApproachHalfWidthMeters, Is.GreaterThanOrEqualTo(HullPresence.HalfBeamOf(hull)));
            float south = -plan.WorldSize.y * 0.5f;
            Assert.That(berth.Approach[0].y, Is.EqualTo(south).Within(2f), "the way in starts at the south edge, where the open water is");
            float bed = berth.ShallowestBedOnApproach(plan.Ground.Field.ElevationAt, SampleStepMeters);
            // The last strip to the edge itself, read off the edge row of the ground.
            for (float x = berth.Approach[0].x - berth.ApproachHalfWidthMeters; x <= berth.Approach[0].x + berth.ApproachHalfWidthMeters; x += SampleStepMeters)
                bed = Mathf.Max(bed, plan.Ground.Field.ElevationAt(new Vector2(x, south + 0.25f)));
            float water = LowestTide(plan) - bed;
            Assert.That(water, Is.GreaterThanOrEqualTo(hull.DraughtMeters + berth.KeelClearanceMeters),
                $"{hull.Id} draws {hull.DraughtMeters:F2} m; the way in to {berth.Id} has {water:F2} m at the lowest tide over its shallowest spot ({bed:F2} m)");
            // And the way in arrives: its last point is in water as deep, within a boat's length of the berth.
            Vector2 end = berth.Approach[berth.Approach.Length - 1];
            Assert.That(Mathf.Min(Vector2.Distance(end, berth.Stern), Vector2.Distance(end, berth.Bow)), Is.LessThan(hull.LengthMeters));
        }

        [Test]
        public void TheTwoBerthsLieSideBySide_WithoutSharingWater()
        {
            var face = Berth(BerthPaths[0]); var outside = Berth(BerthPaths[1]);
            Vector2 along = (face.Bow - face.Stern).normalized;
            Assert.That(Vector2.Dot(along, (outside.Bow - outside.Stern).normalized), Is.GreaterThan(0.999f), "the outside boat lies the same way");
            var across = new Vector2(along.y, -along.x);
            float apart = Mathf.Abs(Vector2.Dot(outside.Stern - face.Stern, across));
            Assert.That(apart, Is.GreaterThanOrEqualTo((face.BeamMeters + outside.BeamMeters) * 0.5f));
        }

        [Test]
        public void TheDepthIsTheRegionsData_TheLiveGroundIsTheIssuedBaseDredgedAsTheWorksSay()
        {
            var plan = Plan(); var works = Works();
            GroundFileIntake.RicePointRaster live = null;
            Assert.DoesNotThrow(() => live = GroundFileIntake.ReadRicePointGround(RicePointBuilder.GroundFolder));
            Assert.That(works.RegionId, Is.EqualTo(plan.Region.Id));
            Assert.That((double)plan.Ground.MinElevation, Is.EqualTo(works.Range[0]));
            Assert.That((double)plan.Ground.MaxElevation, Is.EqualTo(works.Range[1]));
            Assert.That((double)plan.PaintFloor, Is.EqualTo(works.Range[0]), "the paint reaches the floor the ground can now reach");
            // The field the game sails on is that raster, texel for texel.
            var field = plan.Ground.Field;
            double step = (works.Range[1] - works.Range[0]) / 65535d;
            for (int row = 0; row < live.Height; row += 3)
                for (int col = 0; col < live.Width; col += 3)
                    if (Math.Abs(field.ElevationAt(field.TexelToWorld(col, live.Height - 1 - row)) - live.Decode(live.Heights[row * live.Width + col])) > 0.001)
                        Assert.Fail($"the field and the dredged raster disagree at texel {col},{row}");
            // Outside the cuts nothing moved by more than the re-encoding's last digit; the wharf stands.
            var issued = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            int cut = 0;
            for (int i = 0; i < issued.Heights.Length; i++)
            {
                double before = issued.Decode(issued.Heights[i]), after = live.Decode(live.Heights[i]);
                if (Math.Abs(after - before) <= step) continue;
                cut++;
                Assert.That(after, Is.LessThan(before), "a cut only ever lowers ground");
                Assert.That(before, Is.LessThan(works.KeepAbove), "no cut touches the wharf or the land");
            }
            Assert.That(cut, Is.GreaterThan(0));
            Assert.That(field.ElevationAt(plan.PlayerStart), Is.GreaterThan(plan.Region.TideMeanLevel + plan.Region.TideAmplitude), "the wharf deck is still dry");
        }

        [Test]
        public void TheDredgedBedFitsInsideTheGroundsEncodingRange()
        {
            var works = Works();
            foreach (var cut in works.Cuts)
                Assert.That(cut.Bed, Is.GreaterThan(works.Range[0]),
                    $"{cut.Id} is dredged to {cut.Bed:F2} m, at or below the {works.Range[0]:F2} m floor the raster encodes; widen the range first");
        }

        [Test]
        public void TheSeamToNineMileCreekIsUntouched_TheCutsReachOnlyTheSouthEdge()
        {
            var works = Works();
            var issued = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            var live = GroundFileIntake.RicePointDredged(issued, works);
            double step = (works.Range[1] - works.Range[0]) / 65535d;
            var southCut = new List<double>();
            for (int row = 0; row < issued.Height; row++)
                for (int col = 0; col < issued.Width; col++)
                {
                    bool edge = row == 0 || col == 0 || col == issued.Width - 1 || row == issued.Height - 1;
                    if (!edge) continue;
                    int i = row * issued.Width + col;
                    bool moved = Math.Abs(live.Decode(live.Heights[i]) - issued.Decode(issued.Heights[i])) > step;
                    if (!moved) continue;
                    Assert.That(row, Is.EqualTo(issued.Height - 1), $"a cut reaches an edge that is not the south one, at texel {col},{row}");
                    southCut.Add(-issued.Size[0] / 2 + (col + 0.5) / issued.PerMetre);
                }
            Assert.That(southCut, Is.Not.Empty, "the way in reaches the south edge, where the open water is");
            // The exit the south water has to meet: where this edge was lowered.
            Assert.That(southCut.Min(), Is.GreaterThan(-70)); Assert.That(southCut.Max(), Is.LessThan(-15));
        }

        [Test]
        public void HarbourWorks_RefuseACutThroughStillWater_AndACutBelowTheRange()
        {
            var issued = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            string json = File.ReadAllText(RicePointBuilder.GroundFolder + "/" + GroundFileIntake.RicePointWorksFile);
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"range\": [-10, 6]", "\"range\": [-6.5, 6]")));
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.RicePointDredged(issued,
                GroundFileIntake.ReadHarbourWorks(json.Replace("\"region\": \"region.rice_point\"", "\"region\": \"region.elsewhere\""))));
            // A pool of still water stands somewhere inland; a cut laid across the whole region must find one and be refused.
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.RicePointDredged(issued,
                GroundFileIntake.ReadHarbourWorks(json.Replace("\"halfWidth\": 14", "\"halfWidth\": 400"))));
        }
    }
}
