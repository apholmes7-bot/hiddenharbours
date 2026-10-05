using System;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The crossing's kinds (terrain pass 9 part 2 §5, layout B; p2_lib.derive2's bar steps): runnels and sand-wave lobes
    /// on the bar's flanks, the clam holes pulled back into their band, the 23 pools, and the bar's own paint. Part 2's
    /// other steps (its coast, the flats, the plinths, its streams, ponds and creeks, the reef's pools and its paths) are
    /// PR 5w's and do not run here.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        double[] _pe2, _pp2, _still1;

        void Crossing()
        {
            var bar = _plan.Crossing;
            if (bar == null) throw new InvalidOperationException("[TerrainPlan] the plan has no crossing.");
            var pools = _plan.Pools;
            Protection2(bar, pools);
            var E1 = _r.E1;
            var ke = new double[_n];
            double frozenAt = Num(_keep.FrozenAt);
            for (int i = 0; i < _n; i++) ke[i] = _pe2[i] >= frozenAt ? 0.0 : 1 - Clip(_pe2[i], 0, 1);   // frozen ground is part 1's to the bit

            // part 2's coast, flats and plinths are PR 5w's: the crossing starts from part 1's ground
            var E = (double[])E1.Clone();
            double x0 = Num(bar.XRange.x), x1 = Num(bar.XRange.y), west = Num(bar.WindowWest), gx = Num(bar.GutX), gk = Num(bar.GutKeep);
            double sp = 0.5 * (Num(bar.RunnelEvery.x) + Num(bar.RunnelEvery.y)), rw = Num(bar.RunnelWidth), rd = Num(bar.RunnelDepth);
            double bLo = Num(bar.RunnelBand.x), bHi = Num(bar.RunnelBand.y), aLo = Num(bar.RunnelAcross.x), aHi = Num(bar.RunnelAcross.y);
            double a0 = Num(bar.LobeBand.x), a1 = Num(bar.LobeBand.y), b0 = Num(bar.LobeBand.z), b1 = Num(bar.LobeBand.w);
            double amp = Num(bar.LobeAmp), lam = Num(bar.LobeLambda), floor = Num(_plan.HeightRange.x);
            long sRun = _seed2 + Rules.SeedRunnel, sLobe = _seed2 + Rules.SeedLobe;
            for (int c = 0; c < _w; c++)
            {
                double X = _xs[c];
                if (!(X < x1 && X > x0 - west && Math.Abs(X - gx) > gk)) continue;   // outside it neither kind moves the ground
                for (int r = 0; r < _h; r++)
                {
                    int i = r * _w + c;
                    double Y = _ys[r], ay = Math.Abs(Y), e = E[i];
                    // runnels on the flanks, RunnelDepth deep: a skewed comb of lines, RunnelEvery apart
                    bool run = ay > aLo && ay < aHi && e > bLo && e < bHi;
                    if (run)
                    {
                        double q = (X - Rules.RunnelSkew * ay + Rules.RunnelNoise * SNoise(X, Y, sRun, Rules.RunnelLambda)) / sp;
                        double dr = Math.Abs(q - Math.Round(q)) * sp;
                        e = e - ke[i] * rd * 1.0 * (1 - Ss(dr / (rw / 2)));
                    }
                    // sand-wave lobes: the flank rises or falls by up to LobeAmp, per side, never the crest
                    double lw = Ss((ay - a0) / (a1 - a0)) * (1 - Ss((ay - b0) / (b1 - b0)));
                    e = e + ke[i] * 1.0 * lw * amp * SNoise(X, Math.Sign(Y) * Rules.LobeSideOffset, sLobe, lam);
                    if (Math.Abs(e - E1[i]) > Rules.MovedBy) e = Math.Max(e, floor);   // never below the grid's floor
                    E[i] = e;
                }
            }
            int pulled = ClamPull(E, E1);
            // toe_bound (part 2's walls) is PR 5w's
            var still = (double[])_still.Clone();
            _still1 = _still;
            double lip = Num(_plan.PoolLip), wob = Num(bar.PoolWobble);
            for (int n = 0; n < pools.Length; n++) _r.Pools.Add(Pool(E, still, pools[n], _seed2 + Rules.SeedBarPool + n, wob, lip));
            for (int i = 0; i < _n; i++)
            {
                double e = E1[i] + ke[i] * (E[i] - E1[i]);
                if (Math.Abs(e - E1[i]) > Rules.MovedBy) e = Math.Max(e, floor);
                E[i] = e;
            }
            for (int i = 0; i < _n; i++)
            {
                double s = still[i];
                if (!(!double.IsNaN(s) && !double.IsInfinity(s) && (_pe2[i] < 0.5 || !double.IsNaN(_still1[i])) && E[i] < s)) still[i] = double.NaN;
            }
            var z = (byte[])_r.Zone1.Clone();
            BarPaint(bar, z, Pz(E), still);
            _biome = BiomeOf(Pz(E), _secidx, _secw);
            double keepAbove = Num(_keep.KeepPaintAbove);
            for (int i = 0; i < _n; i++)
            {
                if (_pp2[i] > keepAbove) z[i] = _r.Zone1[i];
                if (_pd[i] < _ph[i] && _pw[i] >= 0 && !string.IsNullOrEmpty(_plan.Paths[_pw[i]].LineSource)) z[i] = ZPath;
            }
            _E = E; _still = still; _zone = z;
            _r.Pe2 = _pe2; _r.Pp2 = _pp2;
            Log("crossing", "part 2 §5, layout " + LayoutsOf(pools) + ": runnels, lobes, " + pools.Length + " pools; " + pulled + " clam holes pulled back");
        }

        void FinishCrossing()
        {
            FinishPart1();
        }

        static string LayoutsOf(TidalPoolDef[] pools)
        {
            bool a = false, b = false;
            foreach (var p in pools)
            {
                if (p == null) continue;
                if (p.Layout.IndexOf('A') >= 0) a = true;
                if (p.Layout.IndexOf('B') >= 0) b = true;
            }
            return a && b ? "B (A's flank pools and B's crest pools)" : a ? "A" : b ? "B" : "none";
        }

        /// <summary>
        /// Part 2's protected set with its walls left to PR 5w (protection2, walls off): part 1's list less its bar capsule,
        /// its south blanket and its cliff walls, plus the bar's crest band and the bar-head flats, released inside the pools.
        /// </summary>
        void Protection2(CoastSectionDef bar, TidalPoolDef[] pools)
        {
            var pbar = new double[_n];
            StampCapsule(pbar, _src.Sandbar.A, _src.Sandbar.B, Num(bar.CrestKeep.x), Num(bar.CrestKeep.y));
            StampPoly(pbar, Num(bar.HeadKeep), 0.0, Num(bar.HeadKeepFeather));
            var rel = new double[_n];
            double at = Num(bar.PoolRelease.x), fe = Num(bar.PoolRelease.y);
            foreach (var p in pools)
            {
                var cc = Num(p.Centre); double ra = Num(p.Radii.x), rb = Num(p.Radii.y), R = Math.Max(ra, rb) * Rules.PoolReach;
                double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
                if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double v = 1 - Ss((EllipseR(_xs[c], _ys[r], cc, ra, rb, cr, sr, 0, 0.0) - at) / fe);
                    if (v > rel[i]) rel[i] = v;
                }
            }
            _pe2 = new double[_n];
            for (int i = 0; i < _n; i++) _pe2[i] = Math.Max(_peCommon[i], pbar[i] * (1 - rel[i]));
            _pp2 = _ppCommon;
        }

        /// <summary>A pool's radius in its own ellipse (p2_lib.ellipse_r): wobbled by snoise at λ 2 m unless the wobble is 0.</summary>
        static double EllipseR(double X, double Y, PlanPoint c, double ra, double rb, double cr, double sr, long seed, double wob) =>
            EllipseRadius(X, Y, c, ra, rb, cr, sr, seed, wob, Rules.PoolEllipseLambda);

        /// <summary>
        /// A pool (p2_lib.pan): it fills to its lowest rim point, so its water is flat; the dish is flat to PoolLip of its
        /// radius, then rises to the rim. Carved into E; its water joins <paramref name="still"/> where none stands.
        /// </summary>
        TerrainPlanPoolRecord Pool(double[] E, double[] still, TidalPoolDef p, long seed, double wob, double lip) =>
            PoolCut(E, still, p.Id, "part 2 §5, layout " + p.Layout, Num(p.Centre), Num(p.Radii.x), Num(p.Radii.y), Num(p.RotationDeg),
                    Num(p.Depth), seed, wob, lip, null, null);

        /// <summary>
        /// The pan rule for any pool: <paramref name="only"/>, when given, limits the dish and the water to those cells (the
        /// rim is read wherever it falls); <paramref name="wetCells"/>, when given, collects the cells that hold water.
        /// </summary>
        TerrainPlanPoolRecord PoolCut(double[] E, double[] still, string id, string source, PlanPoint cc, double ra, double rb, double rotDeg,
                                      double depth, long seed, double wob, double lip, bool[] only, System.Collections.Generic.List<int> wetCells)
        {
            double R = Math.Max(ra, rb) * Rules.PoolReach;
            double rot = Radians(rotDeg), cr = Math.Cos(rot), sr = Math.Sin(rot);
            var rec = new TerrainPlanPoolRecord
            {
                Id = id, Source = source, CentreX = cc.X, CentreY = cc.Y, RadiusA = ra, RadiusB = rb, RotationDeg = rotDeg, Depth = depth,
            };
            if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException("[TerrainPlan] " + id + " is off the grid.");
            int ww = c1 - c0; var rv = new double[(r1 - r0) * ww];
            double lvl = double.PositiveInfinity;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                double rr = EllipseR(_xs[c], _ys[r], cc, ra, rb, cr, sr, seed, wob);
                rv[(r - r0) * ww + (c - c0)] = rr;
                if (rr >= Rules.PanRimFrom && rr < Rules.PanRimTo) lvl = Math.Min(lvl, E[r * _w + c]);
            }
            if (double.IsPositiveInfinity(lvl)) throw new InvalidOperationException("[TerrainPlan] " + id + " has no rim on the grid.");
            lvl -= Rules.PanRimDrop;
            double bed = lvl - depth, maxWater = double.NegativeInfinity; int wet = 0;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c; double rr = rv[(r - r0) * ww + (c - c0)], e = E[i];
                if (only != null && !only[i]) continue;
                double e2 = rr < 1 ? Math.Min(e, bed + (e - bed) * Ss((rr - lip) / (1 - lip))) : e;
                E[i] = e2;
                if (rr < 1 && e2 < lvl)
                {
                    wet++;
                    maxWater = Math.Max(maxWater, lvl - e2);
                    if (double.IsNaN(still[i])) still[i] = lvl;
                    wetCells?.Add(i);
                }
            }
            rec.Spill = PyRound(lvl, 3); rec.Bed = PyRound(bed, 3);
            rec.AreaM2 = PyRound(wet * (_g.Mpp * _g.Mpp), 1);
            rec.MaxWater = wet > 0 ? PyRound(maxWater, 3) : 0.0;
            return rec;
        }

        /// <summary>A clam hole the kinds carried out of its band pulls back towards part 1's ground (part 2's CLAM_PULL).</summary>
        int ClamPull(double[] E, double[] E1)
        {
            double cr = Num(_plan.ClamPull.x), cf = Num(_plan.ClamPull.y), cm = Num(_plan.ClamPull.z);
            double lo = Num(_plan.ClamBand.x) + cm, hi = Num(_plan.ClamBand.y) - cm, R = cr + cf;
            int k = 0;
            foreach (var h in _src.ItemsOf(_keep.ClamRoot))
            {
                double s = _g.Sample(E, h.X, h.Y);
                if (lo <= s && s <= hi) continue;
                k++;
                if (!Win(h.X - R, h.Y - R, h.X + R, h.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double w = 1 - Ss((Hypot(_xs[c] - h.X, _ys[r] - h.Y) - cr) / cf);
                    E[i] = E[i] + w * (E1[i] - E[i]);
                }
            }
            return k;
        }

        /// <summary>The bar's paint (p2_lib.bar_paint): its recipe, silt in the runnels, mussels by the gut, silt under its pools.</summary>
        void BarPaint(CoastSectionDef bar, byte[] z, double[] E, double[] still)
        {
            var recipe = new RecipeTable(bar.Recipe);
            double x0 = Num(bar.XRange.x), x1 = Num(bar.XRange.y), west = Num(bar.WindowWest), half = Num(bar.HalfWidth), below = Num(bar.PaintBelow);
            double gx = Num(bar.GutX), gk = Num(bar.GutKeep), sp = 0.5 * (Num(bar.RunnelEvery.x) + Num(bar.RunnelEvery.y)), rw = Num(bar.RunnelWidth);
            double bLo = Num(bar.RunnelBand.x), bHi = Num(bar.RunnelBand.y), aLo = Num(bar.RunnelAcross.x);
            double near = Num(bar.MusselNearGut), mLo = Num(bar.MusselBand.x), mHi = Num(bar.MusselBand.y);
            long sRec = _seed2 + Rules.SeedBarRecipe, sRun = _seed2 + Rules.SeedRunnel, sMus = _seed2 + Rules.SeedMussel;
            for (int c = 0; c < _w; c++)
            {
                double X = _xs[c];
                if (!(X < x1 && X > x0 - west)) continue;
                for (int r = 0; r < _h; r++)
                {
                    int i = r * _w + c;
                    double Y = _ys[r], e = E[i];
                    if (!(Math.Abs(Y) < half && e < below)) continue;
                    byte zb = RecipeZone(recipe, e, X, Y, sRec);
                    double q = (X - Rules.RunnelSkew * Math.Abs(Y) + Rules.RunnelNoise * SNoise(X, Y, sRun, Rules.RunnelLambda)) / sp;
                    double dr = Math.Abs(q - Math.Round(q)) * sp;
                    if (dr < rw / 2 && Math.Abs(Y) > aLo && e > bLo && e < bHi && Math.Abs(X - gx) > gk) zb = ZSilt;
                    if (Math.Abs(X - gx) < near && e > mLo && e < mHi && Fbm(X, Y, sMus, Rules.MusselLambda) > Rules.MusselAbove) zb = ZMusselbed;
                    if (!double.IsNaN(still[i]) && !double.IsInfinity(still[i]) && still[i] > e) zb = ZSilt;
                    z[i] = zb;
                }
            }
        }
    }
}
