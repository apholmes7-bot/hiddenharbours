using System;
using static HiddenHarbours.World.TerrainPlanMath;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The fall (the key scene's WaterfallDef; R2 pass 6's alderFall.js, ported rule for rule): inside its box the ground is
    /// the fall's own cut into the plan's bank (part 1's ground less the run's carve): the approach above the lip, the plunge
    /// pool's bowl and the chute down to the run's designed bed, blended into the plan's ground at the box's edges. Its water
    /// stands at the reference flow; its paint is laid in its window. The Fen Pool's water stands, as the board keeps it.
    /// The board's value-noise wobble of the pool's outline (0.04) is not carried: the plan's ground has no board noise.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        /// <summary>A fall's numbers, read once, and its shapes (alderFall.js's cxAt, zbUp, upperBed, planBed, chuteBed, poolQ, poolBed).</summary>
        sealed class FallShape
        {
            public double[] Cx, Cy, Ry, Rz;
            public double LipX, LipY, LipZ, LipRock, ApproachFrom, GlideFrom, GlideBed0, GlideBed1, Step, SlideBed0, SlideBed1, SlideBow, Sill;
            public double FadeFrom, FadeTo, BossHeight, BossSpread, BossRise0, BossRise1, CarveHalf;
            public double[] Glide, Slide, Chute;                          // rise, scale, edge, bank
            public double Pcx, Pcy, Pra, Prb, Pcos, Psin, Surf, PoolBedZ;
            public double FootX, FootY, FootOffX, FootOffY, FootRx, FootRy, FootShare, BowlPower, RimLift, RimSlope;
            public double ChuteFrom, ChuteTo, ChuteCap, ChuteBelowPlan;
            public double BoxX0, BoxY0, BoxX1, BoxY1, BlendW, BlendE, BlendS, BlendN;
            public double Flow, PoolWater, GlideWater, SlideWater, SillWater, ChuteWater, WaterHalf, MinWater;   // water: m over the bed at Flow
            public double ChuteWaterPast;

            public FallShape(WaterfallDef f)
            {
                Flow = Num(f.ReferenceFlow);
                PoolWater = Num(f.PoolWater.x) + Num(f.PoolWater.y) * Flow;
                GlideWater = Num(f.GlideWater.x) + Num(f.GlideWater.y) * Flow;
                SlideWater = Num(f.SlideWater.x) + Num(f.SlideWater.y) * Flow;
                SillWater = Num(f.SillWater.x) + Num(f.SillWater.y) * Flow;
                ChuteWater = Num(f.ChuteWater.x) + Num(f.ChuteWater.y) * Flow;
                WaterHalf = Num(f.WaterHalfWidth); MinWater = Num(f.MinWater); ChuteWaterPast = Num(f.ChuteWaterPast);
                if (f.Stream == null || f.Pool == null) throw new InvalidOperationException("[TerrainPlan] " + f.Id + " names no stream or no pool.");
                var cl = Num(f.Centreline);
                if (cl.Length < 2) throw new InvalidOperationException("[TerrainPlan] " + f.Id + "'s centreline needs two points.");
                Cx = new double[cl.Length]; Cy = new double[cl.Length];
                for (int k = 0; k < cl.Length; k++) { Cx[k] = cl[k].X; Cy[k] = cl[k].Y; }
                var rp = Num(f.Stream.Points); var rz = Num(f.Stream.BedZ);
                if (rp.Length != rz.Length || rp.Length < 2) throw new InvalidOperationException("[TerrainPlan] " + f.Stream.Id + "'s bed does not match its points.");
                Ry = new double[rp.Length]; Rz = rz;
                for (int k = 0; k < rp.Length; k++) Ry[k] = rp[k].Y;
                LipX = Num(f.Lip.x); LipY = Num(f.Lip.y); LipZ = Num(f.LipZ); LipRock = Num(f.LipRock);
                ApproachFrom = Num(f.ApproachFrom); GlideFrom = Num(f.GlideFrom); GlideBed0 = Num(f.GlideBed.x); GlideBed1 = Num(f.GlideBed.y);
                Step = Num(f.Step); SlideBed0 = Num(f.SlideBed.x); SlideBed1 = Num(f.SlideBed.y); SlideBow = Num(f.SlideBow); Sill = Num(f.Sill);
                FadeFrom = Num(f.SectionFade.x); FadeTo = Num(f.SectionFade.y);
                BossHeight = Num(f.BossHeight); BossSpread = Num(f.BossSpread); BossRise0 = Num(f.BossRise.x); BossRise1 = Num(f.BossRise.y);
                CarveHalf = Num(f.CarveHalfWidth);
                Glide = Sec(f.GlideSection); Slide = Sec(f.SlideSection); Chute = Sec(f.ChuteSection);
                var p = f.Pool;
                Pcx = Num(p.Centre.x); Pcy = Num(p.Centre.y); Pra = Num(p.Radii.x); Prb = Num(p.Radii.y);
                double rot = Radians(Num(p.RotationDeg)); Pcos = Math.Cos(rot); Psin = Math.Sin(rot);
                Surf = Num(p.Surface); PoolBedZ = Num(p.Bed);
                FootX = Num(f.Foot.x); FootY = Num(f.Foot.y); FootOffX = Num(f.FootOffset.x); FootOffY = Num(f.FootOffset.y);
                FootRx = Num(f.FootRadii.x); FootRy = Num(f.FootRadii.y); FootShare = Num(f.FootShare); BowlPower = Num(f.BowlPower);
                RimLift = Num(f.RimLift); RimSlope = Num(f.RimSlope);
                ChuteFrom = Num(f.ChuteFrom); ChuteTo = Num(f.ChuteTo); ChuteCap = Num(f.ChuteCap); ChuteBelowPlan = Num(f.ChuteBelowPlan);
                BoxX0 = Num(f.BoxMin.x); BoxY0 = Num(f.BoxMin.y); BoxX1 = Num(f.BoxMax.x); BoxY1 = Num(f.BoxMax.y);
                BlendW = Num(f.BoxBlend.x); BlendE = Num(f.BoxBlend.y); BlendS = Num(f.BoxBlend.z); BlendN = Num(f.BoxBlend.w);
            }

            static double[] Sec(FallSection s) => new[] { Num(s.Rise), Num(s.Scale), Num(s.Edge), Num(s.Bank) };

            /// <summary>A channel's cross-section at <paramref name="a"/> metres from its line: a dish, then its walls.</summary>
            public static double Section(double a, double[] s)
            {
                if (a < s[2]) { double t = a / s[1]; return s[0] * t * t; }
                double e = s[2] / s[1];
                return s[0] * e * e + s[3] * (a - s[2]);
            }

            /// <summary>The inverse of <see cref="Section"/>: how far from its line water <paramref name="h"/> deep meets the dish.</summary>
            public static double SectionHalf(double h, double[] s)
            {
                double e = s[2] / s[1], atEdge = s[0] * e * e;
                return h <= atEdge ? s[1] * Math.Sqrt(Math.Max(0, h) / s[0]) : s[2] + (h - atEdge) / s[3];
            }

            /// <summary>The line's x at <paramref name="y"/>: linear between its points, held beyond its ends.</summary>
            public double CxAt(double y) => ByY(Cy, Cx, y);

            /// <summary>The run's designed bed at <paramref name="y"/> (the StreamDef's points and BedZ).</summary>
            public double PlanBed(double y) => ByY(Ry, Rz, y);

            static double ByY(double[] ys, double[] v, double y)
            {
                if (y <= ys[0]) return v[0];
                for (int k = 0; k + 1 < ys.Length; k++)
                    if (y <= ys[k + 1]) return v[k] + (v[k + 1] - v[k]) * ((y - ys[k]) / (ys[k + 1] - ys[k]));
                return v[v.Length - 1];
            }

            /// <summary>The run's bed from the outlet to the brink: the glide, the slide (bowed), the sill down to the lip.</summary>
            public double ZbUp(double y)
            {
                if (y < Step) return GlideBed0 + (GlideBed1 - GlideBed0) * Clip((y - GlideFrom) / (Step - GlideFrom), 0, 1);
                if (y < Sill)
                {
                    double t = (y - Step) / (Sill - Step);
                    return SlideBed0 - (SlideBed0 - SlideBed1) * (t + SlideBow * Math.Sin(t * Math.PI));
                }
                return SlideBed1 + (LipZ - SlideBed1) * Clip((y - Sill) / (LipY - Sill), 0, 1);
            }

            public double Boss(double n, double y) => BossHeight * Math.Exp(-(n * n) / BossSpread) * Smoothstep(BossRise0, BossRise1, y);

            public double UpperBed(double n, double y)
            {
                double a = Math.Abs(n), k = Smoothstep(FadeFrom, FadeTo, y);
                double cg = Section(a, Glide), cs = Section(a, Slide);
                return ZbUp(y) + cg + (cs - cg) * k + Boss(n, y);
            }

            public double ZbCh(double y) => Math.Min(ChuteCap, PlanBed(y) - ChuteBelowPlan);

            public double ChuteBed(double n, double y) => ZbCh(y) + Section(Math.Abs(n), Chute);

            public double PoolQ(double x, double y)
            {
                double dx = x - Pcx, dy = y - Pcy, u = dx * Pcos + dy * Psin, v = -dx * Psin + dy * Pcos;
                return Hypot(u / Pra, v / Prb);
            }

            public double PoolBed(double x, double y, double q)
            {
                double dq = Hypot((x - FootX - FootOffX) / FootRx, (y - FootY - FootOffY) / FootRy);
                return Surf - (Surf - PoolBedZ) * Math.Pow(Clip(1 - q * q, 0, 1), BowlPower) * ((1 - FootShare) + FootShare * Clip(1 - dq, 0, 1));
            }

            public double CarveAt(double x, double y, double eB)
            {
                double e = eB, n = x - CxAt(y), a = Math.Abs(n);
                if (y <= LipY && y >= ApproachFrom && a < CarveHalf) e = Math.Min(e, UpperBed(n, y));
                double q = PoolQ(x, y);
                bool lipRock = y <= LipY && Math.Abs(x - LipX) < LipRock;
                if (!lipRock) e = Math.Min(e, q < 1 ? PoolBed(x, y, q) : Surf + RimLift + RimSlope * (q - 1));
                if (y >= ChuteFrom && a < CarveHalf) e = Math.Min(e, ChuteBed(n, y));
                return e;
            }

            public bool InBox(double x, double y) => x >= BoxX0 && x <= BoxX1 && y >= BoxY0 && y <= BoxY1;

            public double BoxWeight(double x, double y) =>
                Smoothstep(BoxX0, BoxX0 + BlendW, x) * Smoothstep(BoxX1, BoxX1 - BlendE, x) * Smoothstep(BoxY0, BoxY0 + BlendS, y) * Smoothstep(BoxY1, BoxY1 - BlendN, y);
        }

        const int FallNone = 0, FallGlide = 1, FallSlide = 2, FallSill = 3, FallPool = 4, FallChute = 5;

        /// <summary>
        /// The fall's water at a cell (alderFall.js's waterStatic, at the reference flow): its reach, its bed, its level, and
        /// the pool's q; FallNone where it stands thinner than MinWater. The run's own water below the chute (the board's MUD
        /// reach) is part 1's and is not handled here.
        /// </summary>
        static int FallWater(FallShape s, double x, double y, out double lvl, out double q)
        {
            int sec = FallNone; double bed = double.NaN;
            lvl = double.NaN;
            q = s.PoolQ(x, y);
            if (q < 1) { bed = s.PoolBed(x, y, q); lvl = s.Surf + s.PoolWater; sec = FallPool; }
            else
            {
                double n = x - s.CxAt(y), a = Math.Abs(n);
                if (y >= s.ApproachFrom && y <= s.LipY && a < s.WaterHalf)
                {
                    bed = s.UpperBed(n, y);
                    if (y < s.Step) { lvl = s.ZbUp(y) + s.GlideWater; sec = FallGlide; }
                    else if (y < s.Sill) { lvl = s.ZbUp(y) + s.SlideWater; sec = FallSlide; }
                    else { lvl = s.ZbUp(y) + s.SillWater; sec = FallSill; }
                }
                else if (y > s.ChuteFrom && y < s.ChuteTo && a < s.WaterHalf) { bed = s.ChuteBed(n, y); lvl = s.ZbCh(y) + s.ChuteWater; sec = FallChute; }
            }
            return sec != FallNone && lvl > bed + s.MinWater ? sec : FallNone;
        }

        /// <summary>
        /// Each fall in turn (§4.1 tier 3): its cut in its box, its water, its paint. <paramref name="owner"/> takes the fall's
        /// index where its cut moved the ground by more than the key scenes' rule.
        /// </summary>
        void Falls(double[] E, double[] still, byte[] z, byte[] owner)
        {
            if (_plan.Falls == null) return;
            foreach (var f in _plan.Falls)
            {
                if (f == null) continue;
                var s = new FallShape(f);
                byte me = NewOwner(f.Id);
                double moved = TerrainPlanRules.KeySceneMoved;
                bool ownRun = f.Stream != null && _fallStream >= 0 && ReferenceEquals(_plan.Streams[_fallStream], f.Stream);
                // the ground: the plan's bank (part 1's ground with the run's carve undone), cut and blended in the box
                if (!Win(s.BoxX0, s.BoxY0, s.BoxX1, s.BoxY1, out int r0, out int r1, out int c0, out int c1)) continue;
                int cut = 0; double deepest = 0, highest = 0, wetArea = 0;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r];
                    if (!s.InBox(X, Y)) continue;
                    if (_pondWet != null && _pondWet[i]) continue;                       // the Fen Pool's surface stands
                    double g = E[i];
                    double eB = ownRun && _runCut != null ? g + (1 - Clip(_pe1[i], 0, 1)) * _runCut[i] : g;
                    double e = g + (s.CarveAt(X, Y, eB) - g) * s.BoxWeight(X, Y);
                    if (Math.Abs(e - g) > moved) { owner[i] = me; cut++; }
                    deepest = Math.Min(deepest, e - g); highest = Math.Max(highest, e - g);
                    E[i] = e;
                }
                // the water at the reference flow: the fall's reaches replace the run's inside the box, above its chute's end
                int[] secCells = new int[6];
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r];
                    if (!s.InBox(X, Y) || (_pondWet != null && _pondWet[i])) continue;
                    int sec = FallWater(s, X, Y, out double lvl, out _);
                    if (Y < s.ChuteTo) still[i] = double.NaN;
                    if (sec == FallNone) continue;
                    still[i] = lvl; secCells[sec]++; wetArea += _g.Mpp * _g.Mpp;
                }
                FallPaint(s, f, Pz(E), z);
                Log("key scenes", f.Id + " (tier 3, the fall): its cut in x " + F(s.BoxX0) + ".." + F(s.BoxX1) + ", y " + F(s.BoxY0) + ".." + F(s.BoxY1) +
                                  " moved " + cut + " cells (" + F(deepest, "0.00") + ".." + F(highest, "+0.00;-0.00") + " m); its water at flow " + F(s.Flow) +
                                  ": glide " + secCells[FallGlide] + ", slide " + secCells[FallSlide] + ", sill " + secCells[FallSill] + ", pool " +
                                  secCells[FallPool] + ", chute " + secCells[FallChute] + " cells");
                _r.Note(f.Id + ".cells_moved", cut);
                _r.Note(f.Id + ".deepest_cut_m", deepest);
                _r.Note(f.Id + ".water_m2", wetArea);
            }
        }

        /// <summary>The fall's paint in its window (alderFall.js's zone): its water's beds, the lip's rock, the foot, the pool's rim, the chute's bar.</summary>
        void FallPaint(FallShape s, WaterfallDef f, double[] E, byte[] z)
        {
            double px0 = Num(f.PaintMin.x), py0 = Num(f.PaintMin.y), px1 = Num(f.PaintMax.x), py1 = Num(f.PaintMax.y);
            if (!Win(px0, py0, px1, py1, out int r0, out int r1, out int c0, out int c1)) return;
            byte glide = Zi(f.GlideZone), rock = Zi(f.RockZone), mud = Zi(f.PoolZone), rim = Zi(f.PoolRimZone), talus = Zi(f.TalusZone);
            double mudQ = Num(f.PoolMudQ), lipFrom = Num(f.LipRockFrom), lipTop = Num(f.LipRockTop), lipHalf = Num(f.LipRockHalfWidth);
            double footR = Num(f.FootRockRadius), squash = Num(f.FootRockSquash), footAbove = Num(f.FootRockAbove), footBelowY = Num(f.FootRockBelowY);
            double footSlope = Num(f.FootRockSlope), rim0 = Num(f.RimBand.x), rim1 = Num(f.RimBand.y), rimBelow = Num(f.RimShingleBelow);
            double rimSlope = Num(f.RimTalusSlope), bar0 = Num(f.ChuteBarY.x), bar1 = Num(f.ChuteBarY.y), barHalf = Num(f.ChuteBarHalfWidth);
            double barAbove = Num(f.ChuteBarAbove), inv = 1.0 / (2 * _g.Mpp);
            for (int r = Math.Max(r0, 1); r < Math.Min(r1, _h - 1); r++)
            for (int c = Math.Max(c0, 1); c < Math.Min(c1, _w - 1); c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r];
                if (!(X >= px0 && X <= px1 && Y >= py0 && Y <= py1)) continue;
                if (_pondWet != null && _pondWet[i]) continue;                           // the Fen Pool keeps the plan's paint
                int sec = FallWater(s, X, Y, out _, out double q);
                if (sec != FallNone)
                {
                    z[i] = sec == FallGlide || sec == FallChute ? glide : sec == FallPool ? (q < mudQ ? mud : rim) : rock;
                    continue;
                }
                double e = E[i], a = Math.Abs(X - s.CxAt(Y));
                double sl = Hypot((E[i + 1] - E[i - 1]) * inv, (E[i - _w] - E[i + _w]) * inv);   // on the fall's own ground
                double dF = Hypot(X - s.FootX, (Y - s.FootY) * squash);
                if (Y > lipFrom && Y <= s.LipY + lipTop && a < lipHalf) { z[i] = rock; continue; }
                if (dF < footR && e > footAbove && Y < footBelowY) { z[i] = sl > footSlope ? rock : talus; continue; }
                if (q >= rim0 && q < rim1)
                {
                    if (e < rimBelow) { z[i] = rim; continue; }
                    if (sl > rimSlope) { z[i] = talus; continue; }
                }
                // the chute's bar: the board paints it shingle, the glide's bed
                if (Y > bar0 && Y < bar1 && a < barHalf && e < s.ZbCh(Y) + barAbove) z[i] = glide;
            }
        }
    }
}
