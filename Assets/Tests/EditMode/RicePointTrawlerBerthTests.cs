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

        /// <summary>
        /// The dredge is reproducible from the committed inputs: re-run it from the issued base and
        /// the works file and it gives these rasters, by hash, and the committed live rasters are them.
        /// </summary>
        [Test]
        public void TheDredgeReRunFromTheCommittedInputs_GivesTheCommittedRasters_ByHash()
        {
            var rerun = GroundFileIntake.RicePointDredged(GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder), Works());
            Assert.That(GroundFileIntake.RasterCodeHash(rerun.Heights), Is.EqualTo("7a6ed2ebc68e8700f8acd4d97f2a9b462e2d40a80f5fba7db9c2454346f16a99"));
            Assert.That(GroundFileIntake.RasterCodeHash(rerun.Still), Is.EqualTo("409a0d8819000f217b7eafe7cb9d1dcaa22b4fef8fdc5390a9edbc3de45988eb"));
            foreach (var (file, codes) in new[] { ("RicePointSeabed_HeightTex_plan.png", rerun.Heights), ("RicePointSeabed_StillTex_plan.png", rerun.Still) })
                Assert.That(GroundFileIntake.RasterCodeHash(TerrainPlanPng.ReadFile(RicePointBuilder.GroundFolder + "/" + file).Channel(0)),
                    Is.EqualTo(GroundFileIntake.RasterCodeHash(codes)), file + " is not the dredge's output");
        }

        /// <summary>
        /// Widening the range moved nothing that matters: every texel that no dredge shape, and no
        /// blend at a shape's limit, asks to lower (the shore, the wharf, every arrival and door, the
        /// whole seam) keeps its issued height to within one step of the new encoding, and so does
        /// every still-water level.
        /// </summary>
        [Test]
        public void OutsideTheDredgeShapes_EveryTexelKeepsItsIssuedHeight_WithinOneEncodeStep()
        {
            var works = Works();
            var issued = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            var live = GroundFileIntake.ReadRicePointGround(RicePointBuilder.GroundFolder);
            double step = (works.Range[1] - works.Range[0]) / 65535d;
            int outside = 0;
            for (int row = 0; row < issued.Height; row++)
                for (int col = 0; col < issued.Width; col++)
                {
                    int i = row * issued.Width + col;
                    double before = issued.Decode(issued.Heights[i]);
                    double x = -issued.Size[0] / 2 + (col + 0.5) / issued.PerMetre, y = issued.Size[1] / 2 - (row + 0.5) / issued.PerMetre;
                    bool asked = works.Reaches(x, y, before);
                    if (asked) continue;
                    outside++;
                    if (Math.Abs(live.Decode(live.Heights[i]) - before) > step) Assert.Fail($"texel {col},{row} is outside every dredge shape and its blend margin, and moved");
                    if (issued.Still[i] != 0 && Math.Abs(live.Decode(live.Still[i]) - issued.Decode(issued.Still[i])) > step)
                        Assert.Fail($"the still water at texel {col},{row} changed level");
                    if ((issued.Still[i] == 0) != (live.Still[i] == 0)) Assert.Fail($"still water appeared or vanished at texel {col},{row}");
                }
            Assert.That(outside, Is.GreaterThan(issued.Heights.Length * 0.97), "the dredge is a small part of the region");
            // The places people and boats arrive keep their ground: the player's start on the wharf and every door.
            var plan = Plan();
            // The one door that is the dredge's own purpose lies in it: the channel mouth to the South Water,
            // whose shore point is the wharf deck that stands over the dredged basin.
            foreach (var door in plan.Doors.Where(d => d.OwnArrival != RicePointDoors.FromSouthWaterArrivalKey))
                foreach (var at in new[] { door.Centre, door.Arrival, door.Disembark })
                    Assert.That(works.TargetAt(at.x, at.y), Is.GreaterThan(works.KeepAbove), door.Id + " is clear of every cut");
        }

        /// <summary>
        /// A cut's side slope does not stop at a crease. Where it meets the old ground the two are
        /// rounded together over the cut's blend, so a tide's edge that falls there is a shore and
        /// not the stepped outline of a shape (the mud flat east of the basin at spring low). The
        /// blend is small and bounded: it reaches only where the slope and the old ground are within
        /// the blend height of each other, dips at most a quarter of that, and never digs below a bed.
        /// </summary>
        [Test]
        public void EachCutMeetsTheOldGroundThroughABlend_SmallBounded_AndNeverBelowItsBed()
        {
            var works = Works();
            foreach (var cut in works.Cuts)
                Assert.That(cut.BlendRun, Is.GreaterThan(0), cut.Id + " would meet the old ground at a crease");
            double widest = works.Cuts.Max(c => c.BlendHeight), deepestBed = works.Cuts.Min(c => c.Bed);
            var issued = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            var live = GroundFileIntake.RicePointDredged(issued, works);
            double step = (works.Range[1] - works.Range[0]) / 65535d;
            int blended = 0;
            for (int row = 0; row < issued.Height; row++)
                for (int col = 0; col < issued.Width; col++)
                {
                    int i = row * issued.Width + col;
                    double before = issued.Decode(issued.Heights[i]), after = live.Decode(live.Heights[i]);
                    double x = -issued.Size[0] / 2 + (col + 0.5) / issued.PerMetre, y = issued.Size[1] / 2 - (row + 0.5) / issued.PerMetre;
                    double target = works.TargetAt(x, y), hard = before < works.KeepAbove ? Math.Min(before, target) : before;
                    Assert.That(after, Is.LessThanOrEqualTo(hard + step), $"the blend raised ground at texel {col},{row}");
                    Assert.That(after, Is.GreaterThanOrEqualTo(hard - widest / 4 - step), $"the blend dug too deep at texel {col},{row}");
                    Assert.That(after, Is.GreaterThanOrEqualTo(Math.Min(before, deepestBed) - step), $"texel {col},{row} is below every bed");
                    if (hard - after <= step) continue;
                    blended++;
                    Assert.That(Math.Abs(before - target), Is.LessThan(widest), $"the blend reached texel {col},{row}, where slope and ground are far apart");
                }
            Assert.That(blended, Is.GreaterThan(0), "the blend does something");
            // Along the ground the blend reaches no farther than its run: the lowered ground at the south
            // edge is the hard cut's band widened by at most that on each side.
            var hardWorks = GroundFileIntake.ReadHarbourWorks(File.ReadAllText(RicePointBuilder.GroundFolder + "/" + GroundFileIntake.RicePointWorksFile));
            foreach (var cut in hardWorks.Cuts) cut.BlendRun = 0;
            var hardGround = GroundFileIntake.RicePointDredged(issued, hardWorks);
            int last = (issued.Height - 1) * issued.Width;
            var moved = Enumerable.Range(0, issued.Width).Where(c => Math.Abs(live.Decode(live.Heights[last + c]) - issued.Decode(issued.Heights[last + c])) > step).ToArray();
            var movedHard = Enumerable.Range(0, issued.Width).Where(c => Math.Abs(hardGround.Decode(hardGround.Heights[last + c]) - issued.Decode(issued.Heights[last + c])) > step).ToArray();
            double reach = works.Cuts.Max(c => c.BlendRun) * issued.PerMetre;
            Assert.That(movedHard.Min() - moved.Min(), Is.InRange(0, reach + 1));
            Assert.That(moved.Max() - movedHard.Max(), Is.InRange(0, reach + 1));
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
            // A cut must say how it meets the old ground, and not with a negative run.
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"blendRun\": 3", "\"blendRun\": -1")));
        }
    }
}
