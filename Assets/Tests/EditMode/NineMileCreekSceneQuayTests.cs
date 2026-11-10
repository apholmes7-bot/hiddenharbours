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
    /// the two caps on their faces, the faces' water and their joins, the wall with its brow mouths and pier openings, the
    /// breakwater on its arms, the design boat's track clear of every solid outline, the four brows and their floats, the nine
    /// ladders, the nineteen shore cleats, the north courses and the five wall berths.
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

        // ---- the wall: blockers along the faces, one mouth for each registered brow ------------------------

        static readonly long[] LiveBlockers = { 1770550000, 1770549994, 1770549993 };
        static readonly long[] RetiredBlockers = { 1770549995, 1770549996, 1770549997, 1770549998, 1770549999 };

        /// <summary>The H5 water side's own parts that carry <paramref name="script"/>, each with its GameObject.</summary>
        static List<(Doc go, Doc part)> WaterSideParts(string script) =>
            Children(At(NineMileCreekWharfWaterSide.RootName))
                .Select(go => (go, part: Scene.ComponentsOf(go).SingleOrDefault(d => d.IsScript(script))))
                .Where(p => p.part != null).ToList();

        /// <summary>The four registered brows (NMC H5), each with the float it lands on and the mouth H5 opened for
        /// it in the wall: the working float's on the crane face, the two finger brows' and the recreation float's
        /// on the north face. The old brow at x 105 and its float are retired. All four floats stay afloat through
        /// the whole range: the register lies the working float a metre east of where the bake drew her, clear of
        /// the basin's edge slope that grounded her west end (held below). The slopes are the brows' own: drop over
        /// run at spring low and spring high, and the step down onto the float at spring high.</summary>
        const string WorkingFloatId = "float.nmc_working";
        static readonly (string brow, long id, string landing, long landingId, float x, float faceY, bool agroundAtSpringLow,
                         float slopeLow, float slopeHigh, float stepDownHigh)[] Brows =
        {
            ("gangway.nmc_west_gangway", 1916874940L, WorkingFloatId, 482568552L, 54f, G.CraneFaceY, false, 0.3309f, 0.0482f, 0.75f),
            ("gangway.nmc_fleet_gangway0", 1822863346L, "float.nmc_finger_a", 963455038L, 109.7f, G.NorthFaceY, false, 0.3309f, 0.0482f, 0.75f),
            ("gangway.nmc_fleet_gangway1", 939760229L, "float.nmc_finger_b", 833421161L, 142.3f, G.NorthFaceY, false, 0.3309f, 0.0482f, 0.75f),
            ("gangway.nmc_east_gangway", 446611123L, "float.nmc_recreation", 1545836378L, 179.3f, G.NorthFaceY, false, 0.3310f, 0.0465f, 0.72f),
        };

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

        /// <summary>The wall stands along the crane face, the step and the north face, and is open in six places only:
        /// the four mouths H5 opened, one for each registered brow and as wide as that brow, and the two pier runways.
        /// The retired brow's mouth at x 105 is not one of them: H5 walled it again. The boundary's stored runs name
        /// the same six openings, each run ending a blocker's half thickness short of one.</summary>
        [Test]
        public void TheQuayIsWalledAlongItsFaces_OpenOnlyForTheFourBrowsAndTheTwoPierRunways()
        {
            Doc quay = At(Wharf(NineMileCreekWalls.RootName));
            Doc boundary = Script(quay, "HiddenHarbours.World.PropertyBoundary");
            float half = G.BlockerHalfThickness;
            var line = new[]
            {
                new Vector2(G.CraneFaceWestX, G.CraneFaceY), new Vector2(G.FaceStepX, G.CraneFaceY),
                new Vector2(G.FaceStepX, G.NorthFaceY), new Vector2(G.NorthFaceEastX, G.NorthFaceY),
            };
            List<Vector2[]> runs = Runs(boundary);
            Assert.That(runs.Count, Is.GreaterThanOrEqualTo(1), "the wall's runs");
            Near(runs[0][0], line[0], "the wall starts at the crane face's west end");
            Near(runs[runs.Count - 1].Last(), line[3], "the wall ends at the north face's east end");
            foreach (Vector2 v in runs.SelectMany(r => r))
                Assert.That(Enumerable.Range(0, line.Length - 1).Min(s => DistanceToSegment(v, line[s], line[s + 1])),
                    Is.LessThanOrEqualTo(1e-3f), $"run point ({Shown(v.x)}, {Shown(v.y)}) lies on the face line");
            Assert.That(Number(boundary, "_thicknessMetres"), Is.EqualTo(2f * half).Within(1e-6f));

            Doc walls = Scene.ChildNamed(Scene.TransformOf(quay), PropertyBoundary.CollidersChildName);
            List<Doc> blockers = Components(walls, PolygonCollider2DClass);
            Assert.That(blockers.Select(b => b.FileId), Is.EquivalentTo(LiveBlockers.Concat(RetiredBlockers)),
                "all eight blockers keep their ids");
            Assert.That(blockers.Where(Enabled).Select(b => b.FileId), Is.EquivalentTo(LiveBlockers),
                "the north, the step and the crane blockers stand; the five basin ghosts are retired");
            Assert.That(Live(walls), Is.True);
            Assert.That(Scene.TransformOf(walls).Field("m_LocalPosition"), Is.EqualTo("{x: 0, y: 0, z: 0}"));
            var paths = blockers.Where(Enabled).SelectMany(b =>
            {
                Assert.That(b.Field("m_Offset"), Is.EqualTo("{x: 0, y: 0}"));
                return Paths(b).Select(p => p.Select(v => ToWorld(walls, v)).ToArray());
            }).ToList();

            // Each registered brow hinges on its face, at its mouth; the mouth is as wide as the brow.
            var brows = WaterSideParts("HiddenHarbours.World.GangwayPlatform");
            Assert.That(brows.Count, Is.EqualTo(Brows.Length), "the four registered brows");
            var halfWidths = new float[Brows.Length];
            for (int b = 0; b < Brows.Length; b++)
            {
                Doc brow = brows.Single(g => g.part.Field("_id") == Brows[b].brow).part;
                Near(Vector(brow, "_shoreEnd"), new Vector2(Brows[b].x, Brows[b].faceY), Brows[b].brow + " hinges on its face at its mouth");
                halfWidths[b] = Number(brow, "_halfWidthMetres");
            }

            // Ruling 33 opened the two delivered pier runways; every other quarter metre stays walled.
            var runways = NineMileCreekWharfLandSide.YardRunwayOpenings();
            Assert.That(runways.Count, Is.EqualTo(2), "the two delivered piers have two runway openings");

            // The stored runs name exactly those six openings: along the face line, a run ends a blocker's half
            // thickness short of each mouth and each runway, and the next starts as far beyond it.
            var openings = new List<(float west, float east, float faceY)>();
            for (int b = 0; b < Brows.Length; b++)
                openings.Add((Brows[b].x - halfWidths[b], Brows[b].x + halfWidths[b], Brows[b].faceY));
            foreach (Vector2 runway in runways) openings.Add((runway.x, runway.y, G.NorthFaceY));
            var named = new List<Vector2[]>();
            var run = new List<Vector2> { line[0] };
            for (int face = 0; face < line.Length - 1; face++)
            {
                float y = line[face].y;
                if (Mathf.Approximately(y, line[face + 1].y))
                    foreach (var o in openings.Where(m => Mathf.Approximately(m.faceY, y)).OrderBy(m => m.west))
                    {
                        run.Add(new Vector2(o.west - half, y));
                        named.Add(run.ToArray());
                        run = new List<Vector2> { new Vector2(o.east + half, y) };
                    }
                run.Add(line[face + 1]);
            }
            named.Add(run.ToArray());
            Assert.That(runs.Count, Is.EqualTo(named.Count), "the wall's runs: one more than its six openings");
            for (int k = 0; k < named.Count; k++) Near(runs[k], named[k], $"run {k} of the wall");
            int walled = 0, open = 0;
            var mouthOpen = new int[Brows.Length];
            var runwayOpen = new int[runways.Count];
            var wrong = new List<string>();
            for (int s = 0; s < line.Length - 1; s++)
            {
                int n = Mathf.RoundToInt(Vector2.Distance(line[s], line[s + 1]) / 0.25f);
                for (int i = 0; i <= n; i++)
                {
                    Vector2 p = Vector2.Lerp(line[s], line[s + 1], i / (float)n);
                    bool onNorthFace = Mathf.Approximately(p.y, G.NorthFaceY);
                    bool inOpening = false;
                    for (int b = 0; b < Brows.Length; b++)
                        if (Mathf.Approximately(p.y, Brows[b].faceY) && Mathf.Abs(p.x - Brows[b].x) < halfWidths[b])
                        {
                            inOpening = true;
                            mouthOpen[b]++;
                        }
                    for (int r = 0; r < runways.Count; r++)
                        if (onNorthFace && p.x > runways[r].x && p.x < runways[r].y)
                        {
                            inOpening = true;
                            runwayOpen[r]++;
                        }
                    bool isWalled = paths.Any(path => Inside(path, p));
                    if (isWalled == inOpening)
                        wrong.Add($"({Shown(p.x)}, {Shown(p.y)}) must be {(inOpening ? "open for a brow or a pier runway" : "walled")}");
                    if (inOpening) open++;
                    else walled++;
                }
            }
            TestContext.WriteLine($"face line: {walled} quarter-metre points walled, {open} open: " +
                                  $"brow mouths {string.Join(", ", mouthOpen)}, pier runways {string.Join(", ", runwayOpen)}");
            Assert.That(wrong, Is.Empty, "the wall is closed everywhere except the four brow mouths and the two pier runways");
            Assert.That(mouthOpen, Is.All.GreaterThan(0), "each of the four brow mouths is open");
            Assert.That(runwayOpen, Is.All.GreaterThan(0), "each of the two pier runways stays open");

            // No blocker strays from the face it walls.
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

        // ---- row 4: the brows and their floats ---------------------------------------------------------------

        [Test]
        public void EveryBrowLandsOnItsFloat_AndRidesEveryTide()
        {
            var brows = WaterSideParts("HiddenHarbours.World.GangwayPlatform");
            var floats = WaterSideParts("HiddenHarbours.World.FloatingPlatform");
            Assert.That(brows.Select(b => b.part.Field("_id")), Is.EquivalentTo(Brows.Select(b => b.brow)), "the four registered brows");
            Assert.That(floats.Select(f => f.part.Field("_id")), Is.EquivalentTo(Brows.Select(b => b.landing)), "the four registered floats");
            foreach (var row in Brows)
            {
                var (browGo, brow) = brows.Single(b => b.part.Field("_id") == row.brow);
                var (floatGo, dock) = floats.Single(f => f.part.Field("_id") == row.landing);
                Assert.That((brow.FileId, dock.FileId), Is.EqualTo((row.id, row.landingId)), $"{row.brow} and {row.landing} keep their ids");
                Assert.That(Live(browGo) && Live(floatGo) && Enabled(brow) && Enabled(dock), Is.True, row.brow);
                Assert.That(brow.FieldRef("_landing"), Is.EqualTo(dock.FileId), $"{row.brow} lands on {row.landing}");

                Vector2 shore = Vector(brow, "_shoreEnd"), landing = Vector(brow, "_floatEnd");
                float abutment = Number(brow, "_abutmentElevation"), halfWidth = Number(brow, "_halfWidthMetres");
                Near(shore, new Vector2(row.x, row.faceY), $"{row.brow} hinges on its face");
                Assert.That(landing.x, Is.EqualTo(shore.x).Within(1e-4f), $"{row.brow} runs square off its face");
                Assert.That(abutment, Is.EqualTo(G.DeckElevation));
                Vector2 behind = shore + new Vector2(0f, 0.5f);
                Assert.That(Mathf.Abs(Ground(behind) - abutment), Is.LessThanOrEqualTo(ContactTolerance),
                    $"{row.brow}'s hinge stands on G3's deck behind the face (G3 {Shown(Ground(behind))})");

                Rect footprint = RectOf(dock, "_footprint");
                foreach (float dx in new[] { -halfWidth, 0f, halfWidth })
                    Assert.That(StandablePlatform.Contains(footprint, landing + new Vector2(dx, 0f)), Is.True,
                        $"{row.brow} lands its whole width on {row.landing} (offset {Shown(dx)})");

                // Her stored bed is G3's highest under her, so she grounds where G3 would ground her.
                float bed = Number(dock, "_bedElevation"), highest = Highest(footprint, 0.05f);
                TestContext.WriteLine($"{row.landing} bed: stored {bed:0.######}, G3 highest under her at 0.05 m {highest:0.######}");
                Assert.That(Mathf.Abs(bed - highest), Is.LessThanOrEqualTo(0.01f), $"{row.landing}'s stored bed is G3's highest under her");

                float draught = Number(dock, "_draughtMetres"), freeboard = Number(dock, "_freeboardMetres");
                Assert.That(FloatingPlatform.IsAground(Low, bed, draught), Is.EqualTo(row.agroundAtSpringLow),
                    $"{row.landing} at spring low: {(row.agroundAtSpringLow ? "on the bottom" : "afloat")} " +
                    $"(she takes the ground below {Shown(bed + draught)} m)");
                Assert.That(FloatingPlatform.IsAground(High, bed, draught), Is.False, $"{row.landing} is afloat at spring high");
                float run = Vector2.Distance(shore, landing);
                float deckLow = FloatingPlatform.DeckElevation(Low, bed, draught, freeboard);
                float deckHigh = FloatingPlatform.DeckElevation(High, bed, draught, freeboard);
                float slopeLow = (abutment - deckLow) / run, slopeHigh = (abutment - deckHigh) / run;
                TestContext.WriteLine($"{row.brow} run {Shown(run)} m; deck {Shown(deckLow)}..{Shown(deckHigh)}; slope {slopeLow:0.######} " +
                                      $"at spring low, {slopeHigh:0.######} at spring high");
                Assert.That(slopeLow, Is.EqualTo(row.slopeLow).Within(1e-3f), $"{row.brow}'s slope at spring low");
                Assert.That(slopeHigh, Is.EqualTo(row.slopeHigh).Within(1e-3f), $"{row.brow}'s slope at spring high");
                Assert.That(abutment - deckHigh, Is.EqualTo(row.stepDownHigh).Within(1e-3f),
                    $"you step DOWN {Shown(row.stepDownHigh)} m onto {row.landing} at spring high");
            }
        }

        // ---- the floats afloat ------------------------------------------------------------------------------
        // A float pinned by one corner of her footprint hangs level above the falling water, and whatever is berthed
        // at her hangs with her. The stored bed is the highest under her, so one high sample grounds the whole float.

        /// <summary>How many samples of <paramref name="footprint"/> stand on a bed too high to float her at spring
        /// low, by the game's own grounding rule and her own draught. The grid is the 0.05 m one her stored bed is
        /// held on.</summary>
        static int SamplesAground(Rect footprint, float draught, out int total, out float highest)
        {
            const float step = 0.05f;
            int nx = Mathf.CeilToInt(footprint.width / step), ny = Mathf.CeilToInt(footprint.height / step), aground = 0;
            total = 0; highest = float.NegativeInfinity;
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++)
                {
                    float bed = Ground(Mathf.Min(footprint.xMin + i * step, footprint.xMax), Mathf.Min(footprint.yMin + j * step, footprint.yMax));
                    total++; highest = Mathf.Max(highest, bed);
                    if (FloatingPlatform.IsAground(Low, bed, draught)) aground++;
                }
            return aground;
        }

        /// <summary>At the lowest water of the spring cycle every sample of every float's footprint has her draught of
        /// water under it. The four registered floats are the creek's only floats: the old one is retired.</summary>
        [Test]
        public void EveryFloatHasWaterUnderHerWholeFootprint_AtSpringLow()
        {
            var floats = WaterSideParts("HiddenHarbours.World.FloatingPlatform");
            Assert.That(floats, Has.Count.EqualTo(Brows.Length), "the four registered floats");
            foreach (var (_, dock) in floats)
            {
                Rect footprint = RectOf(dock, "_footprint");
                float draught = Number(dock, "_draughtMetres");
                int aground = SamplesAground(footprint, draught, out int total, out float highest);
                Debug.Log($"[NineMileCreekSceneQuayTests] {dock.Field("_id")} at spring low ({Shown(Low)} m): {aground} of {total} samples " +
                          $"aground; highest bed {highest:0.######} m, she needs {Low - draught:0.######} m or deeper");
                Assert.That(aground, Is.Zero,
                    $"{dock.Field("_id")}: {aground} of {total} samples of her footprint are aground at spring low (highest bed " +
                    $"{highest:0.######} m against the {Low - draught:0.######} m her {Shown(draught)} m draught needs). A float pinned " +
                    "by one corner hangs above the falling water, and her boats with her: move her by the register, not the ground.");
            }
        }

        /// <summary>The guard's red arm, and why the register and the bake differ. Where the rig's bake drew the working
        /// float her west end stands on the basin's edge slope and grounds her; the register lies her east of it, the
        /// same float (moved, not shortened), and the scene's float lies where the register says.</summary>
        [Test]
        public void TheRegisterLiesTheWorkingFloatClearOfTheBasinsEdge_WhereTheBakeWouldGroundHer()
        {
            Rect baked = NineMileCreekWharfWaterSide.BakedWorkingFloat(), registered = NineMileCreekWharfWaterSide.WorkingFloat();
            Doc dock = WaterSideParts("HiddenHarbours.World.FloatingPlatform").Single(f => f.part.Field("_id") == WorkingFloatId).part;
            Rect placed = RectOf(dock, "_footprint");
            float draught = Number(dock, "_draughtMetres");
            Assert.That(Vector2.Distance(placed.min, registered.min), Is.LessThan(1e-3f), "the scene's float lies where the register says");
            Assert.That(Vector2.Distance(placed.size, registered.size), Is.LessThan(1e-3f), "the scene's float is the register's size");
            Assert.That(Vector2.Distance(registered.size, baked.size), Is.LessThan(1e-3f), "moved, not shortened: her picture is the bake's");
            Assert.That(registered.yMin, Is.EqualTo(baked.yMin).Within(1e-3f), "moved along the apron only");
            int before = SamplesAground(baked, draught, out int bakedTotal, out float bakedHighest);
            int after = SamplesAground(registered, draught, out int total, out float highest);
            Debug.Log($"[NineMileCreekSceneQuayTests] the working float at spring low: at the bake's place (x {Shown(baked.xMin)} to " +
                      $"{Shown(baked.xMax)}) {before} of {bakedTotal} samples aground, highest bed {bakedHighest:0.######} m; at the register's " +
                      $"(x {Shown(registered.xMin)} to {Shown(registered.xMax)}) {after} of {total}, highest bed {highest:0.######} m; " +
                      $"she needs {Low - draught:0.######} m or deeper");
            Assert.That(before, Is.GreaterThan(0),
                "the bake's place floats her whole, so the register has no reason to differ from it: lie her where the bake drew her");
            Assert.That(after, Is.Zero, $"the register's place still grounds her: {after} of {total} samples, highest bed {highest:0.######} m");
        }

        // ---- row 5: the ladders and the shore cleats -------------------------------------------------------

        /// <summary>The stable id H5 bound to the player's berth ladder: the register's anchor for wall berth 0.</summary>
        public const string RegisteredBerthLadderId = "ladder.nmc_berth0";

        [Test]
        public void ThePlanNamesTheBerthLadderByItsRegisteredId() =>
            Assert.That(G.BerthLadderId, Is.EqualTo(RegisteredBerthLadderId));

        /// <summary>The nine registered ladders (NMC H5): one at each of the six wall berths and one east of them on
        /// the north face, two on the crane face for the unloading apron. Ladder_0 at x 139.25 is retired.</summary>
        static readonly (string id, float x, float faceY)[] RegisteredLadders =
        {
            (RegisteredBerthLadderId, G.BerthLadderPos.x, G.NorthFaceY), ("ladder.nmc_berth1", 101f, G.NorthFaceY),
            ("ladder.nmc_berth2", 118.8f, G.NorthFaceY), ("ladder.nmc_berth3", 133.6f, G.NorthFaceY),
            ("ladder.nmc_berth4", 149.3f, G.NorthFaceY), ("ladder.nmc_berth5", 160.3f, G.NorthFaceY),
            ("ladder.nmc_east", 180.2f, G.NorthFaceY),
            ("ladder.nmc_unload_w", 55.5f, G.CraneFaceY), ("ladder.nmc_unload_e", 76.6f, G.CraneFaceY),
        };

        [Test]
        public void TheLaddersClimbTheFace_OneAtTheBerth()
        {
            List<Doc> kept = Children(At(Wharf("Ladders")));
            Assert.That(kept.Select(Name), Is.EqualTo(new[] { G.BerthLadderName }), "the player's ladder stays; Ladder_0 is retired");
            var ladders = kept.Select(go => (go, part: Script(go, "HiddenHarbours.World.WharfLadder")))
                              .Concat(WaterSideParts("HiddenHarbours.World.WharfLadder")).ToList();
            Assert.That(ladders.Select(l => l.part.Field("_id")), Is.EquivalentTo(RegisteredLadders.Select(l => l.id)),
                "the nine registered ladders");
            foreach (var row in RegisteredLadders)
            {
                var (go, ladder) = ladders.Single(l => l.part.Field("_id") == row.id);
                var at = new Vector2(row.x, row.faceY);
                Assert.That(Live(go) && Enabled(ladder), Is.True, $"{row.id} is climbable");
                Near(WorldPosition(go), at, row.id);
                Assert.That(Number(ladder, "_topElevationMeters"), Is.EqualTo(G.DeckElevation), $"{row.id} tops out at the deck");
                Assert.That(Number(ladder, "_faceHeadingDegrees"), Is.EqualTo(180f), $"{row.id} faces the water");
                float foot = Ground(at.x, row.faceY - 0.25f), top = Ground(at.x, row.faceY + 0.25f);
                TestContext.WriteLine($"{row.id} at x {Shown(at.x)}: foot G3 {foot:0.######}, top contact G3 {top:0.######}");
                Assert.That(foot, Is.LessThan(Low), $"{row.id}'s foot is in water at spring low");
                Assert.That(Mathf.Abs(top - G.DeckElevation), Is.LessThanOrEqualTo(ContactTolerance), $"{row.id} tops onto the deck");
            }
            Assert.That(ladders.Count(l => WorldPosition(l.go).x >= G.BerthSlotWestX && WorldPosition(l.go).x <= G.BerthSlotEastX),
                Is.EqualTo(1), "exactly one ladder at the player's berth");
            Assert.That(Name(ladders.Single(l => l.part.Field("_id") == RegisteredBerthLadderId).go), Is.EqualTo(G.BerthLadderName),
                "the player's ladder carries the registered id");

            // Each of the five wall boats has one ladder on the north face at her berth's centre, where she lies.
            var wallBoats = Scene.Docs.Where(d => d.ClassId == 1 && Name(d).StartsWith("Moored_owner.", StringComparison.Ordinal)).ToList();
            Assert.That(wallBoats.Count, Is.EqualTo(5), "the five wall boats");
            foreach (Doc boat in wallBoats)
                Assert.That(ladders.Count(l => Mathf.Approximately(WorldPosition(l.go).y, G.NorthFaceY) &&
                                               Mathf.Abs(WorldPosition(l.go).x - WorldPosition(boat).x) <= 0.01f),
                    Is.EqualTo(1), $"{Name(boat)} has one ladder at her berth's centre");

            // The berth ladder is drawn where it stands.
            Doc picture = Children(At(Wharf("Fittings"))).Single(f => Name(f) == NineMileCreekGroundSteps.BerthLadderFittingName);
            Assert.That(Live(picture), Is.True);
            Near(WorldPosition(picture), G.BerthLadderPos, "the berth ladder's picture");
            StringAssert.Contains("guid:", Components(picture, SpriteRendererClass).Single().Field("m_Sprite"), "the berth ladder has its sprite");

            // The player steps off 1.35 m from it, on dry deck.
            Assert.That(Vector2.Distance(G.DisembarkPos, G.BerthLadderPos), Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(Ground(G.DisembarkPos), Is.GreaterThan(High), "the step-off point is dry at spring high");
        }

        /// <summary>How far inboard of its face a registered shore cleat stands (the register's anchors).</summary>
        const float RegisteredCleatInset = 0.3f;

        /// <summary>The nineteen registered shore cleats (NMC H5) replace the fifteen on the old cap row: sixteen along
        /// the north cap for the six wall berths and three on the crane apron, each 0.3 m inboard of its face and
        /// stored at the deck's height. They are drawn in the delivered wall art, so no fitting picture is asked of
        /// them here.</summary>
        [Test]
        public void TheShoreCleatsStandOnTheirCaps_AtTheDeckHeight()
        {
            var cleats = WaterSideParts("HiddenHarbours.World.ShoreCleat");
            Assert.That(cleats.Count, Is.EqualTo(19));
            Assert.That(cleats.Select(c => c.part.Field("_id")).Distinct().Count(), Is.EqualTo(19), "each has its own id");
            int north = 0, apron = 0;
            foreach (var (go, cleat) in cleats)
            {
                Vector2 at = WorldPosition(go);
                float stored = Number(cleat, "_elevationMeters");
                string id = cleat.Field("_id");
                Assert.That(Live(go) && Enabled(cleat), Is.True, id);
                StringAssert.StartsWith("cleat.nmc_", id);
                bool onNorth = Mathf.Abs(at.y - (G.NorthFaceY + RegisteredCleatInset)) <= 1e-4f &&
                               at.x >= G.FaceStepX && at.x <= G.NorthFaceEastX;
                bool onApron = Mathf.Abs(at.y - (G.CraneFaceY + RegisteredCleatInset)) <= 1e-4f &&
                               at.x >= G.CraneFaceWestX && at.x <= G.FaceStepX;
                Assert.That(onNorth || onApron, Is.True, $"{id} at ({Shown(at.x)}, {Shown(at.y)}) stands on the north cap or the crane apron");
                if (onNorth) north++;
                else apron++;
                Assert.That(stored, Is.EqualTo(G.DeckElevation), $"{id} is stored at the deck's height");
                Assert.That(Mathf.Abs(Ground(at) - stored), Is.LessThanOrEqualTo(ContactTolerance),
                    $"{id} stands on G3's deck (G3 {Shown(Ground(at))})");
                Assert.That(stored, Is.GreaterThan(High), $"{id} is dry at spring high");
            }
            Assert.That((north, apron), Is.EqualTo((16, 3)), "sixteen along the north cap, three on the crane apron");
            List<Doc> fittings = Children(At(Wharf("Fittings")));
            // A retired fitting's picture went with it: no bollard or cleat is drawn for the fifteen old cap-row
            // cleats, and no ladder is drawn where Ladder_0 stood. The berth ladder's own picture stays.
            Assert.That(fittings.Select(Name).Where(n => n == "Fitting_bollard" || n == "Fitting_cleat" || n == "Fitting_ladder"),
                        Is.Empty, "no picture is left of a fitting H5 retired");
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

        /// <summary>Each wall boat's keel clearance at spring low, measured over the G3 bed under her hull where she
        /// lies now: at the centre of the slot the wharf's register gives her (NMC H5). The scope's five numbers were
        /// measured on the packed line the boats left; these are the same measurement at the registered slots.</summary>
        static readonly Dictionary<long, float> WallBerthClearance = new Dictionary<long, float>
        {
            { 439298300, 0.761416f }, { 658256920, 0.824559f }, { 1807033261, 0.715770f },
            { 1930628943, 0.925303f }, { 1964796412, 0.962562f },
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
            // The wharf's two fuel pumps, kept on their ids (the owner's ruling).
            (1955470195, "HiddenHarbours.Economy.FuelPump", "fixture.nmc.wharf_pump_diesel"),
            (2073826116, "HiddenHarbours.Economy.FuelPump", "fixture.nmc.wharf_pump_gas"),
            // What H5 registered in the retired fittings' place: the eight ladders it placed.
            (672714673, "HiddenHarbours.World.WharfLadder", "ladder.nmc_berth1"),
            (2078310295, "HiddenHarbours.World.WharfLadder", "ladder.nmc_berth2"),
            (430371545, "HiddenHarbours.World.WharfLadder", "ladder.nmc_berth3"),
            (396678018, "HiddenHarbours.World.WharfLadder", "ladder.nmc_berth4"),
            (1680297167, "HiddenHarbours.World.WharfLadder", "ladder.nmc_berth5"),
            (1863683145, "HiddenHarbours.World.WharfLadder", "ladder.nmc_east"),
            (958240204, "HiddenHarbours.World.WharfLadder", "ladder.nmc_unload_e"),
            (1282779944, "HiddenHarbours.World.WharfLadder", "ladder.nmc_unload_w"),
        };

        /// <summary>The ids H5's fitting fates retired, by the PR's ledger: the old float, its brow and its eight
        /// cleats, Ladder_0, and the fifteen shore cleats of the old cap row. Their work is done by the registered
        /// floats, brows, ladders and cleats, held by their own tests above.</summary>
        static readonly long[] Retired =
        {
            197115774, 1838025610, 423332918, 1549994791, 1672905203, 941845350, 1309766030, 2131233888, 1658270516,
            1237254907, 1827630107, 475879148, 115453692, 650892435, 1413601334, 139615876, 1185731800, 89061061,
            999094374, 1197550133, 7895873, 295038245, 717937506, 1889992478, 1740556376, 1310633823,
        };

        /// <summary>Every id the scope still keeps names its object: the class, and the stable id where it has one; so
        /// do the wharf's two fuel pumps and the ladders H5 placed. The ids H5 retired are gone. The blockers, the
        /// waterlines, the wall berths, the brows and the floats are held by their own tests above.</summary>
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
            foreach (long id in Retired) Assert.That(Scene.Get(id), Is.Null, $"&{id} is retired with its fitting");
        }
    }
}
