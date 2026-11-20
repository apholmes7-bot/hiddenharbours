using System;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rice Point's wharf cribs meet the water along a straight face. The issued ground holds each
    /// crib as a hard step at half a metre a texel, and a face at a slant was a staircase: the water
    /// edge showed the same notch every 1.1 m down the face. The works now draw each face from the
    /// crib outline (<see cref="GroundFileIntake.HarbourFaces"/>). These tests hold the waterline
    /// straight at every tide of the year, prove the guard sees the old staircase, and keep the faces
    /// to their band so the deck, the berths and the rest of the ground are untouched.
    /// </summary>
    public sealed class RicePointCribFaceTests
    {
        static string WorksJson => File.ReadAllText(RicePointBuilder.GroundFolder + "/" + GroundFileIntake.RicePointWorksFile);
        static GroundFileIntake.HarbourWorks Works() => GroundFileIntake.ReadHarbourWorks(WorksJson);
        static GroundFileIntake.RicePointRaster Issued() => GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);

        /// <summary>
        /// The drawn faces, from the plan's outlines, the stretch of each that meets open water, and the
        /// tide levels that reach the wall there: the head crib's west face (the shore the camera on the
        /// inner berth looks along) at every tide of the year, the approach crib's south face from the
        /// mean up (below it the water stands off the wall on the mud).
        /// </summary>
        static readonly (string name, double ax, double ay, double bx, double by, double from, double to, double[] levels)[] Faces = {
            ("head crib, west face", -57.21, -86.25, -74.99, -119.84, 19.0, 35.5, new[] { -2.2, -1.0, 0.0, 1.0, 2.2 }),
            ("approach crib, south face", -98.28, -82.61, -64.7, -100.39, 22.0, 36.0, new[] { 0.5, 1.0, 2.2 }),
        };

        const double AlongStep = 0.05;

        static double Bilinear(GroundFileIntake.RicePointRaster r, double x, double y)
        {
            double fx = (x + r.Size[0] / 2) * r.PerMetre - 0.5, fy = (r.Size[1] / 2 - y) * r.PerMetre - 0.5;
            int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy);
            double u = fx - i, v = fy - j;
            double H(int c, int row) => r.Decode(r.Heights[row * r.Width + c]);
            return H(i, j) * (1 - u) * (1 - v) + H(i + 1, j) * u * (1 - v) + H(i, j + 1) * (1 - u) * v + H(i + 1, j + 1) * u * v;
        }

        /// <summary>
        /// Where the water stands against one face at one level: for each point down the face, the
        /// distance across it at which the ground (read between texels, as the water reads it) first
        /// rises to the level. Returns the peak-to-peak spread of that line about its best-fit straight line and its strongest repeat
        /// between 0.4 and 6 m (amplitude, metres).
        /// </summary>
        static (double spread, double repeat, double pitch) Waterline(GroundFileIntake.RicePointRaster r, (string name, double ax, double ay, double bx, double by, double from, double to, double[] levels) f, double level)
        {
            double ex = f.bx - f.ax, ey = f.by - f.ay, len = Math.Sqrt(ex * ex + ey * ey), vx = ex / len, vy = ey / len;
            double nx = -vy, ny = vx;
            double mx = f.ax + vx * (f.from + f.to) / 2, my = f.ay + vy * (f.from + f.to) / 2;
            if (Bilinear(r, mx + nx, my + ny) < Bilinear(r, mx - nx, my - ny)) { nx = -nx; ny = -ny; } // inward, toward the deck
            int n = (int)Math.Floor((f.to - f.from) / AlongStep);
            var offsets = new double[n];
            for (int k = 0; k < n; k++)
            {
                double s = f.from + k * AlongStep, px = f.ax + vx * s, py = f.ay + vy * s;
                double u = -2, prev = Bilinear(r, px + nx * u, py + ny * u), found = double.NaN;
                while (u < 2)
                {
                    double cur = Bilinear(r, px + nx * (u + 0.01), py + ny * (u + 0.01));
                    if (prev < level && level <= cur) { found = u + 0.01 * (level - prev) / (cur - prev); break; }
                    u += 0.01; prev = cur;
                }
                Assert.That(double.IsNaN(found), Is.False, $"{f.name}: no waterline at {level:F1} m, {s:F2} m along");
                offsets[k] = found;
            }
            // A straight line at a slight angle to the face is still straight: measure from the best-fit line.
            double km = (n - 1) / 2.0, om = offsets.Average(), sxy = 0, sxx = 0;
            for (int k = 0; k < n; k++) { sxy += (k - km) * (offsets[k] - om); sxx += (k - km) * (k - km); }
            for (int k = 0; k < n; k++) offsets[k] -= om + sxy / sxx * (k - km);
            double mean = 0;
            double best = 0, pitch = 0;
            for (double lambda = 0.4; lambda <= 6.0; lambda += 0.02)
            {
                double re = 0, im = 0;
                for (int k = 0; k < n; k++) { double a = 2 * Math.PI * k * AlongStep / lambda; re += (offsets[k] - mean) * Math.Cos(a); im -= (offsets[k] - mean) * Math.Sin(a); }
                double amp = 2.0 / n * Math.Sqrt(re * re + im * im);
                if (amp > best) { best = amp; pitch = lambda; }
            }
            return (offsets.Max() - offsets.Min(), best, pitch);
        }

        [Test]
        public void AtEveryTideOfTheYear_TheWaterlineAlongEachCribFace_IsStraight_WithNoRepeatingNotch()
        {
            var live = GroundFileIntake.ReadRicePointGround(RicePointBuilder.GroundFolder);
            foreach (var f in Faces)
                foreach (double level in f.levels)
                {
                    var (spread, repeat, pitch) = Waterline(live, f, level);
                    Assert.That(spread, Is.LessThan(0.12), $"{f.name} at {level:F1} m: the waterline wanders {spread:F3} m");
                    Assert.That(repeat, Is.LessThan(0.04), $"{f.name} at {level:F1} m: a {repeat:F3} m notch repeats every {pitch:F2} m");
                }
        }

        /// <summary>The guard sees what the owner saw: on the cut-only ground (no faces) every face is a staircase with a fixed pitch.</summary>
        [Test]
        public void TheSameGuard_OnTheGroundWithoutTheFaces_FindsTheStaircase()
        {
            var works = Works(); works.Faces = null;
            var stepped = GroundFileIntake.RicePointDredged(Issued(), works);
            foreach (var f in Faces)
                foreach (double level in f.levels)
                {
                    var (spread, repeat, pitch) = Waterline(stepped, f, level);
                    Assert.That(spread, Is.GreaterThan(0.3), $"{f.name} at {level:F1} m");
                    Assert.That(repeat, Is.GreaterThan(0.05), $"{f.name} at {level:F1} m");
                    Assert.That(pitch, Is.InRange(1.0, 1.2), $"{f.name} at {level:F1} m: the staircase's pitch, 1 m in y along a 2:1 slant");
                }
        }

        /// <summary>The faces move only the texels in their band beside a crib, never by more than the step that was there, and leave the deck as issued.</summary>
        [Test]
        public void TheFacesMoveOnlyTheirBand_WithinTheOldStep_AndTheDeckStands()
        {
            var issued = Issued(); var works = Works();
            var faced = GroundFileIntake.RicePointFaceTexels(issued, works);
            var withFaces = GroundFileIntake.RicePointDredged(issued, works);
            var bare = Works(); bare.Faces = null;
            var without = GroundFileIntake.RicePointDredged(issued, bare);
            int moved = 0;
            for (int row = 0; row < issued.Height; row++)
                for (int col = 0; col < issued.Width; col++)
                {
                    int i = row * issued.Width + col;
                    double x = -issued.Size[0] / 2 + (col + 0.5) / issued.PerMetre, y = issued.Size[1] / 2 - (row + 0.5) / issued.PerMetre;
                    if (withFaces.Heights[i] == without.Heights[i]) continue;
                    moved++;
                    Assert.That(faced[i], Is.True, $"texel {col},{row} moved but is not a face texel");
                    Assert.That(works.Faces.InBand(x, y), Is.True, $"texel {col},{row} is outside the face band");
                    Assert.That(withFaces.Still[i], Is.EqualTo(without.Still[i]));
                }
            Assert.That(moved, Is.GreaterThan(300), "both cribs' faces are drawn");
            Assert.That(faced.Count(t => t), Is.LessThan(issued.Heights.Length / 200), "the faces are a thin band");
            // Every face texel lies between the outer ground and the deck it joins, so nothing is raised above the deck or dug below the water.
            double lo = without.Decode(without.Heights.Min()), deck = 3.4, step = (works.Range[1] - works.Range[0]) / 65535d;
            for (int i = 0; i < faced.Length; i++)
                if (faced[i]) Assert.That(withFaces.Decode(withFaces.Heights[i]), Is.InRange(lo, 4.0 + step));
            // The deck itself: a point well inside the head crib keeps its issued height.
            var plan = RicePointBuilder.LoadPlan();
            Assert.That(plan.Ground.Field.ElevationAt(new UnityEngine.Vector2(-60.5f, -106f)), Is.EqualTo(deck).Within(0.01), "the head crib's deck");
        }

        [Test]
        public void HarbourFaces_AreRead_AndRefuseBadData()
        {
            var works = Works();
            Assert.That(works.Faces, Is.Not.Null);
            Assert.That(works.Faces.BlockIds, Is.EqualTo(new[] { "crib.rpt_approach", "crib.rpt_head" }));
            Assert.That(works.Faces.Run, Is.GreaterThan(works.Faces.Reach));
            string json = WorksJson;
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"reach\": 0.25", "\"reach\": 5")), "a face that reaches past its own run");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"minStep\": 1.0", "\"minStep\": 0")));
            Assert.That(works.Faces.Edges[1], Is.EqualTo(new[] { 3 }), "the head crib's trawler face stays as issued");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("{ \"id\": \"crib.rpt_approach\"", "{ \"id\": \"crib.rpt_head\"")), "a block named twice");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"edges\": [3]", "\"edges\": []")), "a block with no face");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace("\"edges\": [3]", "\"edges\": [1.5]")), "an edge that is not an index");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.ReadHarbourWorks(json.Replace(GroundFileIntake.HarbourWorksSchema, "hidden-harbours/harbour-works@2")));
            var issued = Issued();
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.RicePointDredged(issued,
                GroundFileIntake.ReadHarbourWorks(json.Replace("\"crib.rpt_approach\"", "\"crib.rpt_nowhere\""))), "a crib the plan does not have");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.RicePointDredged(issued,
                GroundFileIntake.ReadHarbourWorks(json.Replace("\"edges\": [3]", "\"edges\": [7]"))), "an edge the outline does not have");
            Assert.Throws<InvalidDataException>(() => GroundFileIntake.RicePointDredged(issued,
                GroundFileIntake.ReadHarbourWorks(json.Replace("\"grading\": \"cribs.rpt_wharf\"", "\"grading\": \"yard.rpt_wharf\""))), "a grading that is not cribs");
        }
    }
}
