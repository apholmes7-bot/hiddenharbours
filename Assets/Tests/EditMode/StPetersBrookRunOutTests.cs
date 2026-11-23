using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using static HiddenHarbours.World.TerrainPlanMath;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The creek rule (owner, 11-23): fresh water that runs into the sea keeps a stream at a low tide; a channel that
    /// only fills with seawater is a dry bed at a low tide.
    ///
    /// <para><b>A fresh brook</b> carries its water past its last point down its <see cref="StreamDef.RunOut"/> to below
    /// the spring low, in a shallow bed cut by a <see cref="GroundAskKind.BrookBed"/> ask: level from bank to bank, so the
    /// thread's edge is the ground's own smooth contour, not the map's texel edge. A higher tide covers its lower part.</para>
    ///
    /// <para><b>A sea-filled channel</b> (no brook feeds it) is filled to its spill by a hollow fill, so a spring low leaves
    /// damp ground there, never a stranded sheet of still water.</para>
    ///
    /// <para>Everything is read at fixed tide levels (the spring low, mean tide, the spring high): the tide is recomputed
    /// from the seed and the clock, never saved, so these levels are the whole of what the water can do.</para>
    /// </summary>
    public class StPetersBrookRunOutTests
    {
        // ---- the bed's rule, on a made-up beach ----------------------------------------------------------------------

        const int N = 80;               // 80 × 80 texels of 0.25 m: a 20 m square
        const double Mpp = 0.25, Lo = -4, Hi = 7;

        // A beach falling to the south and, across the brook, to the west: the case that drew a blocky waterline.
        static double Beach(double x, double y) => 0.10 * x + 0.05 * y - 1.0;

        static GroundFileImport.BaseMap BeachBase()
        {
            var codes = new ushort[N * N];
            for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
            {
                double x = (c + 0.5) * Mpp, y = 20 - (r + 0.5) * Mpp;
                codes[r * N + c] = (ushort)Math.Round((Beach(x, y) - Lo) / (Hi - Lo) * 65535.0);
            }
            return new GroundFileImport.BaseMap
            {
                Width = N, Height = N, Lo = Lo, Hi = Hi, X0 = 0, Y0 = 0, X1 = 20, Y1 = 20, Codes = codes,
                PixelsSha256 = GroundFileImport.Sha256BigEndian(codes),
            };
        }

        static GroundFileImport.Ask Bed(double wander)
        {
            return new GroundFileImport.Ask
            {
                Id = "ground.test_brook_bed", Source = GroundAskSource.Game, Kind = GroundAskKind.BrookBed,
                Line = new[] { new PlanPoint(10, 18), new PlanPoint(10, 10), new PlanPoint(10, 2) },
                SampleStep = 0.25, Sigma = 1.0, HalfWidthFrom = 1.5, HalfWidthTo = 1.5, BedDepth = 0.22, BankWidth = 2.0,
                Wander = wander, WanderLambda = 6.0, FadeIn = 0, FadeOut = 0, Seed = 7,
            };
        }

        static GroundFileImport.Result Run(GroundFileImport.Ask a) =>
            GroundFileImport.Run(BeachBase(), new List<GroundFileImport.Ask> { a }, -2.2, -4f, 12f);

        [Test]
        public void ABrooksBed_IsLevelFromBankToBank_AcrossASlopingBeach()
        {
            var r = Run(Bed(0));
            var g = r.Grid;
            int row = g.RowOf(10.0);
            double y = g.YG(row);
            int mid = g.ColOf(10.0);   // the column just east of the line (centre 10.125)
            for (int k = 0; k < 6; k++)
            {
                double west = r.E[row * N + (mid - 1 - k)], east = r.E[row * N + (mid + k)];
                Assert.AreEqual(west, east, 1e-9, $"{(k + 0.5) * Mpp} m each side of the line at y {y}: the bed is level across, though the beach falls west");
            }
            Assert.Less(r.E[row * N + mid], r.E[row * N + mid + 4], "the bed's middle is its lowest");
            double bank = Beach(10, y);
            Assert.AreEqual(bank - 0.22, r.E[row * N + mid], 0.01, "the middle sits BedDepth under the bank (the ground on the line)");
            Assert.Greater(r.Asks[0].Cells, 0, "the bed changed the ground");
        }

        [Test]
        public void ABrooksBed_NeverTouchesTheGroundPastItsEnds_AndIsTheSameEveryRun()
        {
            var a = Run(Bed(0.3));
            var b = Run(Bed(0.3));
            CollectionAssert.AreEqual(a.Codes, b.Codes, "two runs, one bed");
            var g = a.Grid;
            var plain = GroundFileImport.DecodeBase(BeachBase());
            for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
            {
                double y = g.YG(r);
                if (y > 18 || y < 2) Assert.AreEqual(plain[r * N + c], a.E[r * N + c], 0.0, $"({g.XG(c)}, {y}) lies past the bed's ends");
                if (Math.Abs(g.XG(c) - 10) > 1.5 * 1.3 + 2.0 + Mpp) Assert.AreEqual(plain[r * N + c], a.E[r * N + c], 0.0, $"({g.XG(c)}, {y}) lies past the bank");
            }
        }

        [Test]
        public void ABrooksBed_NeverCutsDrySandUnderItsLevel()
        {
            const double dry = 0.5;                       // on the line the beach runs 0.1 .. 0.9 m
            var a = Bed(0.3);
            a.DryAbove = dry;
            var r = Run(a);
            var plain = GroundFileImport.DecodeBase(BeachBase());
            int kept = 0, cut = 0;
            for (int i = 0; i < N * N; i++)
            {
                if (plain[i] >= dry) { Assert.GreaterOrEqual(r.E[i], dry, $"cell {i} stood at {plain[i]:0.000} m, over the dry level"); kept++; }
                else if (r.E[i] < plain[i] - 0.05) cut++;
            }
            Assert.Greater(kept, 0, "some of the bed crosses dry sand");
            Assert.Greater(cut, 0, "and under it the bed is cut");
        }

        [Test]
        public void ABrooksBed_FillsADipOnItsLine_ToTheLipBelowIt()
        {
            // a round hollow 0.6 m deep on the line at y 10: the water spills over its lower lip and on down the beach
            static double Dipped(double x, double y) => Beach(x, y) - 0.6 * Math.Max(0, 1 - ((x - 10) * (x - 10) + (y - 10) * (y - 10)) / 4.0);
            var codes = new ushort[N * N];
            for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
                codes[r * N + c] = (ushort)Math.Round((Dipped((c + 0.5) * Mpp, 20 - (r + 0.5) * Mpp) - Lo) / (Hi - Lo) * 65535.0);
            var b = BeachBase();
            b.Codes = codes;
            b.PixelsSha256 = GroundFileImport.Sha256BigEndian(codes);
            var res = GroundFileImport.Run(b, new List<GroundFileImport.Ask> { Bed(0) }, -2.2, -4f, 12f);
            var bare = GroundFileImport.Run(b, new List<GroundFileImport.Ask>(), -2.2, -4f, 12f);
            var bank = GroundFileImport.BankLevels(bare, Bed(0), GroundFileImport.BedLine(Bed(0)));
            for (int j = 1; j < bank.Length; j++) Assert.LessOrEqual(bank[j], bank[j - 1], $"the bank never climbs downstream (sample {j})");
            double floor = Dipped(10, 10);
            Assert.Greater(res.Bilinear(res.E, 10, 10), floor + 0.1, "the dip's floor is filled toward its lip, not kept as the bank");
        }

        [Test]
        public void ABrooksWater_MeetsItsBedInASmoothEdge_WhereverItsBanksWander()
        {
            const double water = 0.1;
            var r = Run(Bed(0.3));
            var g = r.Grid;
            double prevW = double.NaN, prevE = double.NaN;
            for (double y = 16; y >= 4; y -= 0.25)
            {
                double level = r.Bilinear(r.E, 10, y) + water;
                double west = Edge(-1), east = Edge(+1);
                Assert.Greater(west, 0.2, $"y {y}: the thread has water west of its line");
                Assert.Greater(east, 0.2, $"y {y}: and east of it");
                Assert.Less(west + east, 2 * 1.5 * 1.3, $"y {y}: a thread, not a sheet");
                if (!double.IsNaN(prevW))
                {
                    Assert.Less(Math.Abs(west - prevW), 0.2, $"y {y}: the west edge does not jog");
                    Assert.Less(Math.Abs(east - prevE), 0.2, $"y {y}: the east edge does not jog");
                }
                prevW = west; prevE = east;

                double Edge(int side)
                {
                    double d = 0;
                    while (d < 4 && r.Bilinear(r.E, 10 + side * d, y) < level) d += 0.02;
                    return d;
                }
            }
        }

        // ---- St Peters ------------------------------------------------------------------------------------------------

        static RegionTerrainPlanDef _plan;
        static PaintedHeightMap _map;
        static PaintedStillWater _still;

        [OneTimeSetUp]
        public void LoadStPeters()
        {
            _plan = StPetersTerrainPlan.LoadPlan();
            _map = StPetersTerrainPlan.LoadSeabed();
            _still = _map.StillWater;   // as the game reads it: smoothed and sealed
            Assert.IsTrue(_still.IsBound, "the committed still map");
        }

        static IEnumerable<StreamDef> Brooks() => _plan.Streams.Where(s => s != null && s.RunOut != null && s.RunOut.Length > 0);

        static float Ground(Vector2 p) => _map.Field.ElevationAt(p);

        /// <summary>
        /// The run-out drawn as the derivation draws it, sampled every half metre from where its water starts: the brook's
        /// cut bed's first station, else its last point.
        /// </summary>
        static List<Vector2> RunOutSamples(StreamDef s, bool fromBed = true)
        {
            var line = Catmull(Num(s.LineWithRunOut(0)), Num(_plan.LineStep));
            var bed = fromBed ? BedOf(s) : null;
            double from = line.Knots[bed != null ? Math.Min(bed.FromStation, s.Points.Length - 1) : s.Points.Length - 1];
            var o = new List<Vector2>();
            int j = 0;
            for (double u = from; u <= line.Length; u += 0.5)
            {
                while (j < line.S.Length - 2 && line.S[j + 1] < u) j++;
                double t = (u - line.S[j]) / Math.Max(1e-9, line.S[j + 1] - line.S[j]);
                o.Add(new Vector2((float)(line.R[j].X + (line.R[j + 1].X - line.R[j].X) * t), (float)(line.R[j].Y + (line.R[j + 1].Y - line.R[j].Y) * t)));
            }
            return o;
        }

        static GroundAskDef BedOf(StreamDef s) =>
            _plan.Ground.Asks.FirstOrDefault(a => a != null && !a.Retired && a.Kind == GroundAskKind.BrookBed && a.Stream == s);

        [Test]
        public void EveryFreshBrook_HasARunOutToBelowTheSpringLow_AndABedAlongIt()
        {
            var brooks = Brooks().ToList();
            Assert.GreaterOrEqual(brooks.Count, 6, "St Peters' six brooks all reach the sea");
            foreach (var s in brooks)
            {
                var end = s.RunOut[s.RunOut.Length - 1];
                Assert.Less(Ground(end), -_plan.SpringM, $"{s.Id}'s run-out ends below the spring low, at the sea");
            }
            // The Storm Brook's run-out follows Storm Creek's own channel through the bar head's held ground: no bed is cut there.
            Assert.GreaterOrEqual(brooks.Count(s => BedOf(s) != null), 5, "the brooks crossing open beach have a bed cut down it (a BrookBed ask)");
        }

        [Test]
        public void AtASpringLow_EachBrooksThreadReachesTheSea_ShallowAndNarrow()
        {
            float low = -_plan.SpringM;
            foreach (var s in Brooks())
            {
                var bed = BedOf(s);
                float widest = bed != null ? Mathf.Max(bed.BedHalfWidth.x, bed.BedHalfWidth.y) * (1 + bed.BedWander.x) : s.RunOutWidth / 2;
                int samples = 0, dry = 0;
                string firstDry = "";
                foreach (var p in RunOutSamples(s))
                {
                    float e = Ground(p);
                    if (e < low) break;   // the sea from here on
                    samples++;
                    float level = _still.StillLevelAt(p);
                    if (!(level > e)) { if (dry++ == 0) firstDry = $" first at ({p.x:0.00}, {p.y:0.00}) ground {e:0.000}"; continue; }
                    Assert.LessOrEqual(level - e, s.Depth + 0.06f, $"{s.Id} at ({p.x:0.00}, {p.y:0.00}): a wadeable thread, not a pool");
                    Assert.Less(SeaReachMath.UnderStill01(low, level, 0.05f), 1f, $"{s.Id} at ({p.x:0.00}, {p.y:0.00}): above a spring low it is the brook's water");
                    Vector2 across = Across(s, p);
                    Assert.Less(WetReach(p, across) + WetReach(p, -across), 2 * widest + 0.5f,
                                $"{s.Id} at ({p.x:0.00}, {p.y:0.00}): a narrow thread, not a sheet");
                }
                Assert.Greater(samples, 0, $"{s.Id}: its run-out crosses the beach");
                Assert.AreEqual(0, dry, $"{s.Id}: {dry} of {samples} run-out samples above the spring low are dry{firstDry}: the stream breaks before the sea");
            }
        }

        [Test]
        public void AtMeanTide_TheSeaCoversTheThreadsLowerPart_AndAtASpringHighAllOfIt()
        {
            foreach (var s in Brooks())
            {
                int covered = 0, shown = 0;
                // The run-out proper, from the brook's drawn end: its bed upstream climbs above the high-water line.
                foreach (var p in RunOutSamples(s, fromBed: false))
                {
                    float level = _still.StillLevelAt(p);
                    if (float.IsNegativeInfinity(level)) continue;
                    if (SeaReachMath.UnderStill01(0f, level, 0.05f) >= 1f) covered++; else shown++;
                    Assert.AreEqual(1f, SeaReachMath.UnderStill01(_plan.SpringM, level, 0.05f), $"{s.Id} at ({p.x:0.00}, {p.y:0.00}): a spring high covers it");
                }
                Assert.Greater(covered, 0, $"{s.Id}: mean tide covers the run-out's lower part");
            }
        }

        [Test]
        public void ASeaFilledChannel_IsADryBedAtASpringLow()
        {
            var fills = _plan.Ground.Asks.Where(a => a != null && !a.Retired && a.Source == GroundAskSource.Game && a.Kind == GroundAskKind.HollowFill &&
                                                     a.Id != "ground.stp_ne_neck_hollow_fill").ToList();
            Assert.GreaterOrEqual(fills.Count, 2, "the sea-filled channels by the creek mouths are filled");
            foreach (var a in fills)
            {
                var c = a.Centre;
                Assert.GreaterOrEqual(Ground(c), a.Level - 0.01f, $"{a.Id}: its bed stands at its spill");
                Assert.Greater(Ground(c), -_plan.SpringM, $"{a.Id}: above the spring low, so a spring low leaves it dry");
                float level = _still.StillLevelAt(c);
                Assert.IsFalse(level > Ground(c), $"{a.Id}: no still water stands in it ({level} over ground {Ground(c)})");
            }
        }

        static Vector2 Across(StreamDef s, Vector2 p)
        {
            var pts = s.LineWithRunOut(0);
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k + 1 < pts.Length; k++)
            {
                float d = DistToSegment(p, pts[k], pts[k + 1]);
                if (d < bd) { bd = d; best = k; }
            }
            Vector2 t = (pts[best + 1] - pts[best]).normalized;
            return new Vector2(-t.y, t.x);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static float WetReach(Vector2 p, Vector2 dir)
        {
            float d = 0;
            while (d < 8f)
            {
                var q = p + dir * d;
                if (!(_still.StillLevelAt(q) > Ground(q))) break;
                d += 0.05f;
            }
            return d;
        }
    }
}
