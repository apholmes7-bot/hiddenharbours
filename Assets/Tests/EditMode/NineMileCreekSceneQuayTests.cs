using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE QUAY NINE MILE CREEK SHIPS</b> (NMC H2, PR 2): the activated scene's wharf, held to G3, the ground
    /// it now stands on. Every fixture reads NineMileCreek.unity as N1 writes it (<see cref="NineMileCreekLiveGround.Scene"/>)
    /// and probes G3 through PR 1's seam (<see cref="NineMileCreekLiveGround.Plan"/>), so what passes is what ships:
    /// the two caps on their faces, the faces' water and their joins, the wall with the brow's one opening, the
    /// breakwater on its arms, the design boat's track clear of every solid outline, the brow and its float, the two
    /// ladders, the fifteen shore cleats, the north courses and the five wall berths.
    ///
    /// <para><b>Whose numbers.</b> The thresholds are the scope's (PR 2 rows 1 to 6), measured on G3 before the scene
    /// moved; each test prints what it measured. The builder's wharf on the plan's analytic premise is
    /// <c>NineMileCreekWharfTests</c>' and its neighbours', and none of them claims this one.</para>
    /// </summary>
    public class NineMileCreekSceneQuayTests
    {
        static float Low => NineMileCreekMainland.SpringLowWater;
        static float High => NineMileCreekMainland.SpringHighWater;
        static float Ground(float x, float y) => Plan.ElevationAt(new Vector2(x, y));
        static float Ground(Vector2 at) => Plan.ElevationAt(at);
        static string Wharf(string child) => NineMileCreekWharf.RootName + "/" + child;

        /// <summary>Where the north face's toe stops holding water: east of it the east arm's root rises under the
        /// face (−4.13 at x 201.5, +1.74 at x 208), the join row 2 says to test apart.</summary>
        const float NorthToeEastX = 200f;

        /// <summary>A contact line reads G3 within this of the deck: two centimetres, the scope's 0.018 m residual
        /// rounded up, a sixth of one 0.12 m texel step of the 16-bit code.</summary>
        const float ContactTolerance = 0.02f;

        static void Near(Vector2 actual, Vector2 expected, string what, float within = 1e-4f) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(within),
                        $"{what}: ({Shown(actual.x)}, {Shown(actual.y)}), expected ({Shown(expected.x)}, {Shown(expected.y)}).");

        static void Near(Vector2[] actual, Vector2[] expected, string what)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{what}: the number of points");
            for (int i = 0; i < actual.Length; i++) Near(actual[i], expected[i], $"{what}, point {i}");
        }

        /// <summary>The highest G3 sample along a line at 0.05 m, and where.</summary>
        static (float worst, float at) HighestAlong(bool alongX, float fixedAt, float from, float to)
        {
            float worst = float.MinValue, at = from;
            int n = Mathf.RoundToInt((to - from) / 0.05f);
            for (int i = 0; i <= n; i++)
            {
                float s = Mathf.Min(from + i * 0.05f, to);
                float e = alongX ? Ground(s, fixedAt) : Ground(fixedAt, s);
                if (e > worst) { worst = e; at = s; }
            }
            return (worst, at);
        }

        /// <summary>The largest |G3 − deck| along a line at 0.05 m, and where.</summary>
        static (float worst, float at) ResidualAlong(bool alongX, float fixedAt, float from, float to)
        {
            float worst = 0f, at = from;
            int n = Mathf.RoundToInt((to - from) / 0.05f);
            for (int i = 0; i <= n; i++)
            {
                float s = Mathf.Min(from + i * 0.05f, to);
                float r = Mathf.Abs((alongX ? Ground(s, fixedAt) : Ground(fixedAt, s)) - G.DeckElevation);
                if (r > worst) { worst = r; at = s; }
            }
            return (worst, at);
        }

        // ---- row 1: the caps --------------------------------------------------------------------------------

        [Test]
        public void TheTwoCapsStandOnTheirFaces_WhereG3MeetsTheDeck()
        {
            foreach (var (path, id, fileId, cap, scope) in new[]
                     {
                         ("NorthWall", "wharf.nine_mile_creek", 392040810L, G.NorthCap, 0.017991f),
                         ("WestWall", "wharf.nine_mile_creek.apron", 1824537357L, G.CraneCap, 0.018266f),
                     })
            {
                Doc go = At(Wharf(path));
                Doc deck = Script(go, "HiddenHarbours.World.StandablePlatform");
                Assert.That(deck.FileId, Is.EqualTo(fileId), $"{path} keeps its support's id.");
                Assert.That(Live(go) && Enabled(deck), Is.True, $"{path} stands and is standable.");
                Assert.That(deck.Field("_id"), Is.EqualTo(id));
                Assert.That(RectOf(deck, "_footprint"), Is.EqualTo(cap), $"{path} is the narrow cap on its face.");
                Assert.That(Number(deck, "_deckElevation"), Is.EqualTo(G.DeckElevation));
                Near(WorldPosition(go), cap.center, $"{path} stands at its cap's centre");

                // The landward contact, a quarter metre in from the face: the texel line where G3 IS the deck.
                var (worst, at) = ResidualAlong(true, cap.yMax, cap.xMin + 0.5f, cap.xMax - 0.5f);
                TestContext.WriteLine($"{path}: contact y {Shown(cap.yMax)} residual {worst:0.######} at x {Shown(at)}");
                Assert.That(worst, Is.LessThanOrEqualTo(scope + 1e-5f),
                    $"{path}'s cap must meet G3 along its contact within the scope's {scope} m.");
            }
        }

        // ---- row 2: the faces and their joins ------------------------------------------------------------------

        [Test]
        public void TheFacesDropToWaterAtEveryTide_AndTheirJoinsAreHeldApart()
        {
            float face = G.NorthFaceY, crane = G.CraneFaceY, step = G.FaceStepX;
            var log = new List<string>();
            void Below(string what, (float worst, float at) m, float than)
            {
                log.Add($"{what}: highest {m.worst:0.######} at {Shown(m.at)}");
                Assert.That(m.worst, Is.LessThan(than), $"{what} must hold water at spring low ({Shown(than)}).");
            }

            // The toes, a quarter metre seaward of each face, hold water at spring low along each face's run.
            Below("north toe y 85.75, x 78.25..200", HighestAlong(true, face - 0.25f, step + 0.25f, NorthToeEastX), Low);
            Below("crane toe y 75.75, x 51..77.75", HighestAlong(true, crane - 0.25f, G.CraneFaceWestX + 1f, step - 0.25f), Low);
            Below("step toe x 78.25, y 76.25..85.75", HighestAlong(false, step + 0.25f, crane + 0.25f, face - 0.25f), Low);

            // The step's deck side is the deck; it is the corner where the crane apron turns north.
            var stepDeck = ResidualAlong(false, step - 0.25f, crane + 0.25f, face - 0.25f);
            log.Add($"step deck side x 77.75: residual {stepDeck.worst:0.######} at y {Shown(stepDeck.at)}");
            Assert.That(stepDeck.worst, Is.LessThanOrEqualTo(ContactTolerance), "the step's deck side is the deck");

            // The corners, apart: each contact runs on through its inner corner as deck.
            var northCorner = ResidualAlong(true, face + 0.25f, step - 1f, step + 2f);
            var craneCorner = ResidualAlong(true, crane + 0.25f, step - 3f, step - 0.25f);
            log.Add($"north contact through the corner: {northCorner.worst:0.######}; crane contact to the corner: {craneCorner.worst:0.######}");
            Assert.That(northCorner.worst, Is.LessThanOrEqualTo(ContactTolerance), "the north contact through the inner corner");
            Assert.That(craneCorner.worst, Is.LessThanOrEqualTo(ContactTolerance), "the crane contact into the inner corner");

            // The crane face's west end meets land: deck behind it and dry ground in front, no slot of water.
            var craneWest = ResidualAlong(true, crane + 0.25f, G.CraneFaceWestX - 1f, G.CraneFaceWestX + 1f);
            float craneWestToe = Ground(G.CraneFaceWestX + 0.25f, crane - 0.25f);
            log.Add($"crane west end: contact {craneWest.worst:0.######}, toe at x 50.25 {craneWestToe:0.######}");
            Assert.That(craneWest.worst, Is.LessThanOrEqualTo(ContactTolerance), "the crane contact at its west end");
            Assert.That(craneWestToe, Is.GreaterThan(High), "the crane face's west end stands on land");

            // The north face's east end is the east arm's root: the deck within the scope's 0.08 m there.
            float eastEnd = Ground(G.NorthFaceEastX, face + 0.25f);
            log.Add($"north contact at the east arm (x 212): {eastEnd:0.######}");
            Assert.That(eastEnd, Is.GreaterThanOrEqualTo(G.DeckElevation - 0.08f), "the north face's east end meets the arm's crest");

            TestContext.WriteLine(string.Join("\n", log));
        }

        // ---- the wall: blockers along the faces, one opening for the brow ---------------------------------

        static readonly long[] LiveBlockers = { 1770550000, 1770549994, 1770549993 };
        static readonly long[] RetiredBlockers = { 1770549995, 1770549996, 1770549997, 1770549998, 1770549999 };

        static List<Vector2[]> Runs(Doc boundary)
        {
            var runs = new List<Vector2[]>();
            List<Vector2> cur = null;
            string[] lines = boundary.Lines;
            int at = Array.IndexOf(lines, "  _runs:");
            for (int i = at + 1; at >= 0 && i < lines.Length; i++)
            {
                string l = lines[i];
                if (l == "  - Points:")
                {
                    if (cur != null) runs.Add(cur.ToArray());
                    cur = new List<Vector2>();
                }
                else if (l.StartsWith("    - ", StringComparison.Ordinal) && cur != null) cur.Add(Vector(l.Substring(6)));
                else break;
            }
            if (cur != null) runs.Add(cur.ToArray());
            return runs;
        }

        [Test]
        public void TheQuayIsWalledAlongItsFaces_OpenOnlyForTheBrow()
        {
            Doc quay = At(Wharf(NineMileCreekWalls.RootName));
            Doc boundary = Script(quay, "HiddenHarbours.World.PropertyBoundary");
            float half = G.BlockerHalfThickness, gap = G.GangwayOpeningHalfWidth, brow = G.GangwayShoreEnd.x;
            var west = new[]
            {
                new Vector2(G.CraneFaceWestX, G.CraneFaceY), new Vector2(G.FaceStepX, G.CraneFaceY),
                new Vector2(G.FaceStepX, G.NorthFaceY), new Vector2(brow - gap - half, G.NorthFaceY),
            };
            var east = new[] { new Vector2(brow + gap + half, G.NorthFaceY), new Vector2(G.NorthFaceEastX, G.NorthFaceY) };
            List<Vector2[]> runs = Runs(boundary);
            Assert.That(runs.Count, Is.EqualTo(2), "the wall is two runs: crane, step and north to the brow; the brow east");
            Near(runs[0], west, "the west run");
            Near(runs[1], east, "the east run");
            Assert.That(Number(boundary, "_thicknessMetres"), Is.EqualTo(2f * half).Within(1e-6f));

            Doc walls = Scene.ChildNamed(Scene.TransformOf(quay), PropertyBoundary.CollidersChildName);
            List<Doc> blockers = Components(walls, PolygonCollider2DClass);
            Assert.That(blockers.Select(b => b.FileId), Is.EquivalentTo(LiveBlockers.Concat(RetiredBlockers)),
                "all eight blockers keep their ids");
            Assert.That(blockers.Where(Enabled).Select(b => b.FileId), Is.EquivalentTo(LiveBlockers),
                "the north (split at the brow), the step and the crane blockers stand; the five basin ghosts are retired");
            Assert.That(Live(walls), Is.True);
            Assert.That(Scene.TransformOf(walls).Field("m_LocalPosition"), Is.EqualTo("{x: 0, y: 0, z: 0}"));
            var paths = blockers.Where(Enabled).SelectMany(b =>
            {
                Assert.That(b.Field("m_Offset"), Is.EqualTo("{x: 0, y: 0}"));
                return Paths(b).Select(p => p.Select(v => ToWorld(walls, v)).ToArray());
            }).ToList();

            // Every quarter metre of the face line is walled, but for the brow's opening, which nothing walls.
            var line = new[] { west[0], west[1], west[2], new Vector2(G.NorthFaceEastX, G.NorthFaceY) };
            int walled = 0, open = 0;
            for (int s = 0; s < line.Length - 1; s++)
            {
                int n = Mathf.RoundToInt(Vector2.Distance(line[s], line[s + 1]) / 0.25f);
                for (int i = 0; i <= n; i++)
                {
                    Vector2 p = Vector2.Lerp(line[s], line[s + 1], i / (float)n);
                    bool inOpening = Mathf.Approximately(p.y, G.NorthFaceY) && Mathf.Abs(p.x - brow) < gap;
                    bool isWalled = paths.Any(path => Inside(path, p));
                    Assert.That(isWalled, Is.EqualTo(!inOpening),
                        $"({Shown(p.x)}, {Shown(p.y)}) must be {(inOpening ? "open for the brow" : "walled")}.");
                    if (inOpening) open++;
                    else walled++;
                }
            }
            TestContext.WriteLine($"face line: {walled} quarter-metre points walled, {open} open at the brow");
            Assert.That(open, Is.GreaterThan(0));

            // The opening is wider than the brow, and no blocker strays from the face it walls.
            Doc gangway = Script(At(Wharf("FloatingDock/Gangway")), "HiddenHarbours.World.GangwayPlatform");
            Assert.That(2f * gap, Is.GreaterThanOrEqualTo(2f * Number(gangway, "_halfWidthMetres") + 0.25f - 1e-4f),
                "the brow passes the opening with the scope's 0.25 m to spare");
            foreach (Vector2 v in paths.SelectMany(p => p))
            {
                float off = Enumerable.Range(0, line.Length - 1).Min(s => DistanceToSegment(v, line[s], line[s + 1]));
                Assert.That(off, Is.LessThanOrEqualTo(half * Mathf.Sqrt(2f) + 1e-3f),
                    $"blocker vertex ({Shown(v.x)}, {Shown(v.y)}) strays {Shown(off)} m from the face line.");
            }
        }

        // ---- row 3: the breakwater -----------------------------------------------------------------------------

        [Test]
        public void TheBreakwaterIsItsSourceArms_ItsCribsStandDown()
        {
            Doc breakwater = At(Wharf("Breakwater"));
            List<Doc> edges = Components(breakwater, EdgeCollider2DClass);
            Assert.That(edges.Count, Is.EqualTo(2), "the south arm and the east arm");
            Assert.That(edges[0].FileId, Is.EqualTo(1594378569L), "the south arm keeps the breakwater's edge id");
            Near(EdgePoints(edges[0]).Select(v => ToWorld(breakwater, v)).ToArray(), G.SouthArm, "the third issue's south arm");
            Near(EdgePoints(edges[1]).Select(v => ToWorld(breakwater, v)).ToArray(), G.EastArm, "the third issue's east arm");
            Assert.That(edges.All(Enabled) && Live(breakwater), Is.True);

            List<Doc> cribs = Children(breakwater);
            Assert.That(cribs.Count, Is.EqualTo(61));
            Assert.That(cribs.All(c => Name(c).StartsWith("Crib_", StringComparison.Ordinal) && !Active(c)), Is.True,
                "the old crib line across the basin stands down, ids kept");

            // Across the whole wharf, only the two arms and the three face blockers are solid.
            var solid = Scene.SubtreeOf(At(NineMileCreekWharf.RootName).FileId).Select(id => Scene.Get(id))
                             .Where(d => d != null && !d.Stripped && new[] { 58, 60, 61, 66, 68, 70 }.Contains(d.ClassId))
                             .Where(d => Enabled(d) && Live(Scene.Require(d.FieldRef("m_GameObject"), "a GameObject")))
                             .Select(d => d.FileId);
            Assert.That(solid, Is.EquivalentTo(LiveBlockers.Concat(edges.Select(e => e.FileId))),
                "no remnant collider crosses the basin");
        }

        // ---- row 3: the design boat's track, clear of every solid outline --------------------------------

        /// <summary>The distance from a segment to a box centred at <paramref name="c"/> along unit axes u (half
        /// <paramref name="hu"/>) and n (half <paramref name="hn"/>): zero if they meet (clipped Liang–Barsky),
        /// else the nearer of the segment's ends to the box and the box's corners to the segment.</summary>
        static double SegmentToBox(double[] a, double[] b, double[] c, double[] u, double[] n, double hu, double hn)
        {
            double ax = (a[0] - c[0]) * u[0] + (a[1] - c[1]) * u[1], ay = (a[0] - c[0]) * n[0] + (a[1] - c[1]) * n[1];
            double bx = (b[0] - c[0]) * u[0] + (b[1] - c[1]) * u[1], by = (b[0] - c[0]) * n[0] + (b[1] - c[1]) * n[1];
            double t0 = 0, t1 = 1, dx = bx - ax, dy = by - ay;
            bool Clip(double p, double q)
            {
                if (p == 0) return q >= 0;
                double r = q / p;
                if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            if (Clip(-dx, ax + hu) && Clip(dx, hu - ax) && Clip(-dy, ay + hn) && Clip(dy, hn - ay) && t0 <= t1) return 0;
            double ToBox(double x, double y)
            {
                double ex = Math.Max(Math.Abs(x) - hu, 0), ey = Math.Max(Math.Abs(y) - hn, 0);
                return Math.Sqrt(ex * ex + ey * ey);
            }
            double ToSeg(double x, double y)
            {
                double len = dx * dx + dy * dy;
                double t = len <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / len));
                double ex = x - (ax + dx * t), ey = y - (ay + dy * t);
                return Math.Sqrt(ex * ex + ey * ey);
            }
            return Math.Min(Math.Min(ToBox(ax, ay), ToBox(bx, by)),
                            Math.Min(Math.Min(ToSeg(-hu, -hn), ToSeg(hu, -hn)), Math.Min(ToSeg(hu, hn), ToSeg(-hu, hn))));
        }

        [Test]
        public void TheDesignBoatSailsHerTrackClearOfEverySolidOutline()
        {
            var (length, _, halfBeam) = Hull("Assets/_Project/Data/Boats/CapeIslander.asset");
            var outlines = SolidOutlines(out var polygons);
            double radius = Math.Sqrt(length * length / 4.0 + halfBeam * halfBeam), worst = double.MaxValue;
            string nearest = "";
            double[] P(Vector2 v) => new double[] { v.x, v.y };
            var segs = outlines.Select(o => (a: P(o.a), b: P(o.b), r: (double)o.radius, o.who)).ToList();
            foreach (double[][] points in NineMileCreekDesignBoatTests.Track)
                for (int s = 0; s < points.Length - 1; s++)
                {
                    // PR 1's Catmull-Rom enclosure, unchanged: each sample's hull, grown by the margin that covers
                    // the half-interval's translation and turn, stands for every pose between samples.
                    double[] a = points[Math.Max(0, s - 1)], b = points[s], c = points[s + 1], d = points[Math.Min(points.Length - 1, s + 2)];
                    double[] q1 = { (c[0] - a[0]) / 2, (c[1] - a[1]) / 2 };
                    double[] q2 = { (2 * a[0] - 5 * b[0] + 4 * c[0] - d[0]) / 2, (2 * a[1] - 5 * b[1] + 4 * c[1] - d[1]) / 2 };
                    double[] q3 = { (3 * b[0] - a[0] + d[0] - 3 * c[0]) / 2, (3 * b[1] - a[1] + d[1] - 3 * c[1]) / 2 };
                    double Norm(double[] v) => Math.Sqrt(v[0] * v[0] + v[1] * v[1]);
                    double speed = Norm(q1) + 2 * Norm(q2) + 3 * Norm(q3), accel = 2 * Norm(q2) + 6 * Norm(q3);
                    const int steps = 512;
                    const double dt = .5 / steps;
                    for (int j = 0; j < steps; j++)
                    {
                        double t = (j + .5) / steps;
                        double[] pos = { b[0] + q1[0] * t + q2[0] * t * t + q3[0] * t * t * t, b[1] + q1[1] * t + q2[1] * t * t + q3[1] * t * t * t };
                        double[] vel = { q1[0] + 2 * q2[0] * t + 3 * q3[0] * t * t, q1[1] + 2 * q2[1] * t + 3 * q3[1] * t * t };
                        double v = Norm(vel), lower = v - accel * dt;
                        Assert.That(lower, Is.GreaterThan(0), "a tangent reversal needs a different enclosure");
                        double margin = speed * dt + radius * accel * dt / lower + 1e-5;
                        double[] u = { vel[0] / v, vel[1] / v }, n = { -u[1], u[0] };
                        var centre = new Vector2((float)pos[0], (float)pos[1]);
                        Assert.That(polygons.Any(p => Inside(p.path, centre)), Is.False, "the hull's centre inside a solid outline");
                        foreach (var g in segs)
                        {
                            double clear = SegmentToBox(g.a, g.b, pos, u, n, length / 2.0, halfBeam) - g.r - margin;
                            if (clear < worst) { worst = clear; nearest = g.who; }
                        }
                    }
                }
            TestContext.WriteLine($"Cape Islander {Shown(length)} x {Shown(2f * halfBeam)} m on the track: {outlines.Count} solid " +
                                  $"segments; continuous clearance lower bound {worst:0.######} m, nearest {nearest}");
            Assert.That(worst, Is.GreaterThanOrEqualTo(1.0), $"the hull comes within 1 m of {nearest}");
        }

        // ---- row 4: the brow and its float -----------------------------------------------------------------

        [Test]
        public void TheBrowLandsOnTheFloat_AndRidesEveryTide()
        {
            Doc browGo = At(Wharf("FloatingDock/Gangway")), floatGo = At(Wharf("FloatingDock/Float"));
            Doc brow = Script(browGo, "HiddenHarbours.World.GangwayPlatform");
            Doc dock = Script(floatGo, "HiddenHarbours.World.FloatingPlatform");
            Assert.That((brow.FileId, dock.FileId), Is.EqualTo((1838025610L, 197115774L)), "the brow and the float keep their ids");
            Assert.That(brow.Field("_id"), Is.EqualTo("wharf.nine_mile_creek.gangway"));
            Assert.That(dock.Field("_id"), Is.EqualTo("wharf.nine_mile_creek.float"));
            Assert.That(Live(browGo) && Live(floatGo) && Enabled(brow) && Enabled(dock), Is.True);
            Assert.That(brow.FieldRef("_landing"), Is.EqualTo(dock.FileId), "the brow lands on the float");

            Vector2 shore = Vector(brow, "_shoreEnd"), landing = Vector(brow, "_floatEnd");
            float abutment = Number(brow, "_abutmentElevation"), halfWidth = Number(brow, "_halfWidthMetres");
            Assert.That(shore, Is.EqualTo(G.GangwayShoreEnd));
            Assert.That(landing, Is.EqualTo(G.GangwayFloatEnd));
            Assert.That(abutment, Is.EqualTo(G.DeckElevation));
            Near(WorldPosition(browGo), (shore + landing) / 2f, "the brow stands at its midpoint");
            Assert.That(Mathf.Abs(Ground(shore) - abutment), Is.LessThanOrEqualTo(ContactTolerance),
                $"the hinge stands on G3's deck behind the face (G3 {Shown(Ground(shore))})");

            Rect footprint = RectOf(dock, "_footprint");
            Near(WorldPosition(floatGo), footprint.center, "the float stands at her footprint's centre");
            foreach (float dx in new[] { -halfWidth, 0f, halfWidth })
                Assert.That(StandablePlatform.Contains(footprint, landing + new Vector2(dx, 0f)), Is.True,
                    $"the brow's landing lands its whole width on the float (offset {Shown(dx)})");

            // Her stored bed is G3's highest under her, so she grounds where G3 would ground her.
            float bed = Number(dock, "_bedElevation"), highest = Highest(footprint, 0.05f);
            TestContext.WriteLine($"float bed: stored {bed:0.######}, G3 highest under her at 0.05 m {highest:0.######}");
            Assert.That(Mathf.Abs(bed - highest), Is.LessThanOrEqualTo(0.01f));

            float draught = Number(dock, "_draughtMetres"), freeboard = Number(dock, "_freeboardMetres");
            Assert.That(FloatingPlatform.IsAground(Low, bed, draught), Is.False, "the float is afloat at spring low");
            float run = Vector2.Distance(shore, landing);
            float deckLow = FloatingPlatform.DeckElevation(Low, bed, draught, freeboard);
            float deckHigh = FloatingPlatform.DeckElevation(High, bed, draught, freeboard);
            float slopeLow = (abutment - deckLow) / run, slopeHigh = (abutment - deckHigh) / run;
            TestContext.WriteLine($"brow run {Shown(run)} m; deck {Shown(deckLow)}..{Shown(deckHigh)}; slope {slopeLow:0.######} " +
                                  $"at spring low, {slopeHigh:0.######} at spring high");
            Assert.That(slopeLow, Is.EqualTo(0.339869f).Within(1e-3f), "the scope's 33.9869 % at spring low");
            Assert.That(slopeHigh, Is.EqualTo(0.052288f).Within(1e-3f), "the scope's 5.2288 % at spring high");
            Assert.That(abutment - deckHigh, Is.EqualTo(0.8f).Within(1e-3f), "you step DOWN 0.8 m onto the float at spring high");
        }

        // ---- row 5: the ladders and the shore cleats -------------------------------------------------------

        [Test]
        public void TheLaddersClimbTheFace_OneAtTheBerth()
        {
            List<Doc> ladders = Children(At(Wharf("Ladders")));
            Assert.That(ladders.Select(Name), Is.EqualTo(new[] { "Ladder_0", G.BerthLadderName }));
            foreach (var (go, id, at) in new[]
                     {
                         (ladders[0], "wharf.nine_mile_creek.ladder_0", G.WharfLadderPos),
                         (ladders[1], G.BerthLadderId, G.BerthLadderPos),
                     })
            {
                Doc ladder = Script(go, "HiddenHarbours.World.WharfLadder");
                Assert.That(Live(go) && Enabled(ladder), Is.True, $"{Name(go)} is climbable");
                Assert.That(ladder.Field("_id"), Is.EqualTo(id));
                Near(WorldPosition(go), at, Name(go));
                Assert.That(Number(ladder, "_topElevationMeters"), Is.EqualTo(G.DeckElevation), $"{Name(go)} tops out at the deck");
                Assert.That(Number(ladder, "_faceHeadingDegrees"), Is.EqualTo(180f), $"{Name(go)} faces the water");
                float foot = Ground(at.x, G.NorthFaceY - 0.25f), top = Ground(at.x, G.NorthFaceY + 0.25f);
                TestContext.WriteLine($"{Name(go)} at x {Shown(at.x)}: foot G3 {foot:0.######}, top contact G3 {top:0.######}");
                Assert.That(foot, Is.LessThan(Low), $"{Name(go)}'s foot is in water at spring low");
                Assert.That(Mathf.Abs(top - G.DeckElevation), Is.LessThanOrEqualTo(ContactTolerance), $"{Name(go)} tops onto the deck");
            }
            Assert.That(Script(ladders[0], "HiddenHarbours.World.WharfLadder").FileId, Is.EqualTo(1827630107L), "the old ladder keeps its id");
            Assert.That(ladders.Count(l => WorldPosition(l).x >= G.BerthSlotWestX && WorldPosition(l).x <= G.BerthSlotEastX),
                Is.EqualTo(1), "exactly one ladder at the player's berth");

            // The berth ladder is drawn as the old one is, where it stands.
            List<Doc> fittings = Children(At(Wharf("Fittings")));
            Doc picture = fittings.Single(f => Name(f) == NineMileCreekGroundSteps.BerthLadderFittingName);
            Doc old = fittings.Single(f => Name(f) == "Fitting_ladder");
            Assert.That(Live(picture), Is.True);
            Near(WorldPosition(picture), G.BerthLadderPos, "the berth ladder's picture");
            Assert.That(Components(picture, SpriteRendererClass).Single().Field("m_Sprite"),
                Is.EqualTo(Components(old, SpriteRendererClass).Single().Field("m_Sprite")), "the same ladder sprite");

            // The player steps off 1.35 m from it, on dry deck.
            Assert.That(Vector2.Distance(G.DisembarkPos, G.BerthLadderPos), Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(Ground(G.DisembarkPos), Is.GreaterThan(High), "the step-off point is dry at spring high");
        }

        [Test]
        public void TheShoreCleatsStandOnTheCapRow_AtTheirG3Height()
        {
            List<Doc> cleats = Children(At(Wharf("MooringCleats")));
            List<Doc> fittings = Children(At(Wharf("Fittings")));
            Assert.That(cleats.Count, Is.EqualTo(15));
            foreach (Doc go in cleats)
            {
                Doc cleat = Script(go, "HiddenHarbours.World.ShoreCleat");
                Vector2 at = WorldPosition(go);
                float stored = Number(cleat, "_elevationMeters");
                Assert.That(Live(go) && Enabled(cleat), Is.True, Name(go));
                Assert.That(at.y, Is.EqualTo(G.CleatRowY), $"{Name(go)} stands on the cap row");
                Assert.That(at.x, Is.InRange(G.FaceStepX, G.NorthFaceEastX), $"{Name(go)} stands along the north cap");
                Assert.That(stored, Is.EqualTo(Ground(at)).Within(1e-4f), $"{Name(go)} stands at G3's height");
                Assert.That(stored, Is.GreaterThan(High), $"{Name(go)} is dry at spring high");
                string kind = Name(go).Split('_')[1];
                Assert.That(fittings.Any(f => Name(f) == "Fitting_" + kind && Vector2.Distance(WorldPosition(f), at) <= 1e-4f),
                    Is.True, $"{Name(go)} is drawn where it stands");
            }
            Near(WorldPosition(cleats.Single(c => Name(c) == "Cleat_cleat_14")), G.WestCleat, "the west cleat");
            Assert.That(fittings.Where(f => WorldPosition(f).y >= G.NorthFaceY + 0.75f && WorldPosition(f).y <= G.OldNorthFaceY + 0.5f)
                                .Select(Name), Is.Empty, "no fitting is left on the old face's rows");
        }

        // ---- row 2: the courses that draw the face ----------------------------------------------------------

        static readonly long[] FaceWaterlines =
            { 1031781710, 374340541, 1486511428, 585380344, 295766120, 331860810, 624322931, 1944909869, 1807664618 };

        [Test]
        public void TheNorthCoursesDrawTheFaceAtY86_TheGhostsStandDown()
        {
            List<Doc> courses = Children(At(NineMileCreekDressing.RootName + "/" + NineMileCreekDressing.FaceRootName));
            var north = courses.Where(c => Name(c).StartsWith(NineMileCreekDressing.NorthWallRun + "_", StringComparison.Ordinal)).ToList();
            Assert.That(north.Count, Is.EqualTo(9));
            var waterlines = new List<long>();
            foreach (Doc go in north)
            {
                Doc tide = Script(go, "HiddenHarbours.Art.TidalFaceWaterline");
                float x = WorldPosition(go).x;
                Assert.That(Live(go) && Enabled(tide), Is.True, Name(go));
                Assert.That(Number(tide, "_lipWorldY"), Is.EqualTo(G.NorthFaceY), $"{Name(go)} draws its lip on the face");
                Assert.That(Number(tide, "_lipElevation"),
                    Is.EqualTo(Ground(x, G.NorthFaceY + NineMileCreekQuayFace.FaceCourseWidthMetres * 0.5f)).Within(1e-5f),
                    $"{Name(go)}'s lip is G3 half a course inboard");
                Assert.That(Number(tide, "_lipElevation"), Is.GreaterThan(High));
                waterlines.Add(tide.FileId);
            }
            Assert.That(waterlines, Is.EquivalentTo(FaceWaterlines), "the nine courses keep their waterline ids");

            var ghosts = courses.Except(north).ToList();
            Assert.That(ghosts.Count, Is.EqualTo(29));
            Assert.That(ghosts.Where(Active).Select(Name), Is.Empty, "the west, apron, head and breakwater courses stand down");

            List<Doc> gear = Children(At(NineMileCreekDressing.RootName + "/" + NineMileCreekDressing.ApronRootName));
            Assert.That(gear.Count, Is.EqualTo(6));
            Assert.That(gear.Where(Active).Select(Name), Is.Empty, "the apron's gear stood over the basin: all of it stands down");
            Assert.That(gear.Select(Name), Has.Member("davitHoist_0"), "the davit's id stays reserved for the crane");
        }

        // ---- row 6: the five wall berths ---------------------------------------------------------------------

        static readonly Dictionary<long, float> WallBerthClearance = new Dictionary<long, float>
        {
            { 439298300, 0.783461f }, { 658256920, 0.815770f }, { 1807033261, 0.661387f },
            { 1930628943, 0.925303f }, { 1964796412, 0.999753f },
        };

        [Test]
        public void TheWallBoatsLieAlongTheG3Face_AfloatAtSpringLow()
        {
            var wall = new List<long>();
            foreach (Doc go in Children(At(NineMileCreekMooredFleet.RootName)))
            {
                Doc moored = Script(go, "HiddenHarbours.Boats.MooredBoat");
                string owner = File.ReadAllText(BoatData(RefGuid(moored.Field("_owner"))));
                if (Regex.IsMatch(owner, @"(?m)^  Moorage: [1-9]")) continue;          // a float berth: not the wall's
                wall.Add(moored.FileId);
                var (length, draught, halfBeam) = Hull(BoatData(GuidField(owner, "Boat")));
                Vector2 c = WorldPosition(go);
                Assert.That(Number(moored, "_headingDegrees"), Is.EqualTo(G.BerthHeadingDegrees), $"{Name(go)} lies along the face");
                Assert.That(c.y + halfBeam, Is.EqualTo(G.FenderLineY).Within(0.01f), $"{Name(go)} lies against the fender line");
                float bed = Highest(new Rect(c.x - length / 2f, c.y - halfBeam, length, 2f * halfBeam), 0.05f);
                float clearance = Low - draught - bed;
                TestContext.WriteLine($"{Name(go)}: {Shown(length)} x {Shown(2f * halfBeam)} m, draught {Shown(draught)}, " +
                                      $"highest bed {bed:0.######}, keel clearance at spring low {clearance:0.######}");
                Assert.That(WallBerthClearance.ContainsKey(moored.FileId), Is.True, $"{Name(go)} is not a wall berth the scope names");
                Assert.That(clearance, Is.GreaterThanOrEqualTo(WallBerthClearance[moored.FileId] - 0.005f),
                    $"{Name(go)} floats at spring low by the scope's margin");
            }
            Assert.That(wall, Is.EquivalentTo(WallBerthClearance.Keys), "the five wall berths, ids kept");
        }

        // ---- the identity the scope keeps -----------------------------------------------------------------

        static readonly (long id, string kind, string stableId)[] Kept =
        {
            (392040810, "HiddenHarbours.World.StandablePlatform", "wharf.nine_mile_creek"),
            (1824537357, "HiddenHarbours.World.StandablePlatform", "wharf.nine_mile_creek.apron"),
            (1594378569, "68", null),
            (197115774, "HiddenHarbours.World.FloatingPlatform", "wharf.nine_mile_creek.float"),
            (1838025610, "HiddenHarbours.World.GangwayPlatform", "wharf.nine_mile_creek.gangway"),
            (423332918, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_0"),
            (1549994791, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_1"),
            (1672905203, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_2"),
            (941845350, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_3"),
            (1309766030, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_4"),
            (2131233888, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_5"),
            (1658270516, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_6"),
            (1237254907, "HiddenHarbours.World.FloatCleat", "wharf.nine_mile_creek.float.cleat_7"),
            (1827630107, "HiddenHarbours.World.WharfLadder", "wharf.nine_mile_creek.ladder_0"),
            (475879148, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_0"),
            (115453692, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_1"),
            (650892435, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_2"),
            (1413601334, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_3"),
            (139615876, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_4"),
            (1185731800, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_5"),
            (89061061, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_6"),
            (999094374, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_7"),
            (1197550133, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_8"),
            (7895873, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_9"),
            (295038245, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_10"),
            (717937506, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_11"),
            (1889992478, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_12"),
            (1740556376, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.bollard_13"),
            (1310633823, "HiddenHarbours.World.ShoreCleat", "wharf.nine_mile_creek.cleat_14"),
            // The wharf's two fuel pumps, stood down in step 05 on their ids (the owner's ruling).
            (1955470195, "HiddenHarbours.Economy.FuelPump", "fixture.nmc.wharf_pump_diesel"),
            (2073826116, "HiddenHarbours.Economy.FuelPump", "fixture.nmc.wharf_pump_gas"),
        };

        /// <summary>Every id rows 1 to 6 keep still names its object: the class, and the stable id where it has one; so
        /// do the wharf's two fuel pumps, stood down on theirs. The blockers, the waterlines and the wall berths are held
        /// by their own tests above.</summary>
        [Test]
        public void EveryIdTheScopeKeeps_StillNamesItsObject()
        {
            foreach (var (id, kind, stableId) in Kept)
            {
                Doc d = Scene.Get(id);
                Assert.That(d, Is.Not.Null, $"&{id} is gone");
                Assert.That(int.TryParse(kind, out int cls) ? d.ClassId == cls : d.IsScript(kind), Is.True, $"&{id} is not a {kind}");
                if (stableId != null) Assert.That(d.Field("_id"), Is.EqualTo(stableId), $"&{id}'s stable id");
            }
        }
    }
}
