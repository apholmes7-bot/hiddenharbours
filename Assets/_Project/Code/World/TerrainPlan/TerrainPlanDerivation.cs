using System;
using System.Collections.Generic;
using System.Globalization;
using static HiddenHarbours.World.TerrainPlanMath;

namespace HiddenHarbours.World
{
    /// <summary>How far a derivation runs: part 1 alone (pass 9's own maps), then the crossing, then the key scenes.</summary>
    public enum TerrainPlanStage
    {
        Part1 = 1,
        Crossing = 2,
        Final = 3,
    }

    /// <summary>
    /// <b>THE TERRAIN PLAN'S DERIVATION.</b> The region's plan (its Defs) laid over today's ground (the sources), on the
    /// seabed grid: the ground, the paint, the still water, the biomes. A pure function of the plan and the sources;
    /// no randomness but the plan's seed, so the same inputs give the same maps, bit for bit (rule 5).
    /// <para>
    /// The steps run in the handoff's order (terrain PR 5, §4.1), strongest last so nothing weaker overwrites it:
    /// part 1's plan (pass 9: the sections, the water, the paint), the crossing's kinds (part 2 §5, layout B), the key
    /// scenes' ground (the Head's pieces raised as max(plan, piece); the fall's cut), then tier 2 (Ginny's frozen
    /// pieces at part 1's heights) and tier 1 (pass 9's frozen mask at today's heights). <see cref="TerrainPlanResult.Order"/>
    /// records each step as it ran.
    /// </para>
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        readonly RegionTerrainPlanDef _plan;
        readonly TerrainPlanSources _src;
        readonly TerrainPlanGrid _g;
        readonly int _w, _h, _n;
        readonly double[] _xs, _ys;
        readonly long _seed, _seed2;
        readonly TerrainPlanKeep _keep;
        readonly TerrainPlanResult _r = new TerrainPlanResult();

        TerrainPlanDerivation(RegionTerrainPlanDef plan, TerrainPlanSources src)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _src = src ?? throw new ArgumentNullException(nameof(src));
            _g = src.Grid;
            _w = _g.W; _h = _g.H; _n = _g.Count;
            if (src.Base == null || src.Base.Length != _n) throw new ArgumentException("[TerrainPlan] the sources' Base does not fill the grid.");
            if (src.Today == null || src.Today.Length != _n) throw new ArgumentException("[TerrainPlan] the sources' Today does not fill the grid.");
            _xs = new double[_w]; _ys = new double[_h];
            for (int c = 0; c < _w; c++) _xs[c] = _g.XG(c);
            for (int r = 0; r < _h; r++) _ys[r] = _g.YG(r);
            _seed = plan.Seed; _seed2 = plan.Part2Seed;
            _keep = plan.Keep ?? new TerrainPlanKeep();
            _jitAmp = Num(plan.RecipeJitter.x); _jitLam = Num(plan.RecipeJitter.y);
            _r.Grid = _g;
            _r.Base = src.Base;
        }

        /// <summary>Derive the plan's maps from its Defs and the sources, as far as <paramref name="last"/>.</summary>
        public static TerrainPlanResult Derive(RegionTerrainPlanDef plan, TerrainPlanSources src, TerrainPlanStage last = TerrainPlanStage.Final)
        {
            var d = new TerrainPlanDerivation(plan, src);
            d.Run(last);
            return d._r;
        }

        /// <summary>
        /// The ground file's import (the sources' <see cref="TerrainPlanSources.Ground"/>) when the whole plan runs, else null.
        /// With it, every paint step reads its heights, not the derived ones, and the still water stands on it (amendment 1
        /// §4.6): the paint follows the ground the player stands on. Part 1's and the crossing's own runs never read it, so
        /// they stay pass 9's to the bit.
        /// </summary>
        double[] _P;

        /// <summary>The heights a paint step reads: the import's when there is one, else the derived <paramref name="e"/>.</summary>
        double[] Pz(double[] e) => _P ?? e;

        /// <summary>The whole plan runs: the roads run on into their join points, and the village's routes are painted.</summary>
        bool _final;

        void Run(TerrainPlanStage last)
        {
            _final = last == TerrainPlanStage.Final;
            if (last == TerrainPlanStage.Final && _src.Ground != null)
            {
                if (_src.Ground.Length != _n) throw new ArgumentException("[TerrainPlan] the sources' Ground does not fill the grid.");
                _P = _src.Ground;
            }
            Part1();
            if (last == TerrainPlanStage.Part1) { FinishPart1(); return; }
            Crossing();
            if (last == TerrainPlanStage.Crossing) { FinishCrossing(); return; }
            KeyScenes();
            Finish();
        }

        void Log(string step, string what) => _r.Order.Add((_r.Order.Count + 1).ToString("00", CultureInfo.InvariantCulture) + " " + step + ": " + what);

        static string F(double v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);

        // ---- the grid ------------------------------------------------------------------------------------------------------

        bool Win(double x0, double y0, double x1, double y1, out int r0, out int r1, out int c0, out int c1) =>
            _g.Win(x0, y0, x1, y1, out r0, out r1, out c0, out c1);

        double[] Filled(double v)
        {
            var a = new double[_n];
            if (v != 0) for (int i = 0; i < _n; i++) a[i] = v;
            return a;
        }

        static double Wv(double d, double fe) => d <= 0 ? 1.0 : (fe > 0 ? 1 - Ss(d / fe) : 0.0);

        // ---- the protected set's shapes: max-accumulated, feathered (pass 9's stamp) ----------------------------------------

        void StampPoint(double[] p, PlanPoint at, double buf, double fe)
        {
            double rr = buf + fe;
            if (!Win(at.X - rr, at.Y - rr, at.X + rr, at.Y + rr, out int r0, out int r1, out int c0, out int c1)) return;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double wv = Wv(Hypot(_xs[c] - at.X, _ys[r] - at.Y) - buf, fe);
                if (wv > p[i]) p[i] = wv;
            }
        }

        void StampCapsule(double[] p, PlanPoint a, PlanPoint b, double buf, double fe)
        {
            double rr = buf + fe;
            if (!Win(Math.Min(a.X, b.X) - rr, Math.Min(a.Y, b.Y) - rr, Math.Max(a.X, b.X) + rr, Math.Max(a.Y, b.Y) + rr,
                    out int r0, out int r1, out int c0, out int c1)) return;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double wv = Wv(Seg(_xs[c], _ys[r], a, b) - buf, fe);
                if (wv > p[i]) p[i] = wv;
            }
        }

        void StampRect(double[] p, double x0, double y0, double x1, double y1, double buf, double fe)
        {
            double rr = buf + fe;
            if (!Win(x0 - rr, y0 - rr, x1 + rr, y1 + rr, out int r0, out int r1, out int c0, out int c1)) return;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r];
                double ox = Math.Max(Math.Max(x0 - X, X - x1), 0), oy = Math.Max(Math.Max(y0 - Y, Y - y1), 0);
                double wv = Wv(Hypot(ox, oy) - buf, fe);
                if (wv > p[i]) p[i] = wv;
            }
        }

        void StampPoly(double[] p, IList<PlanPoint> poly, double buf, double fe)
        {
            if (poly == null || poly.Count < 3) return;
            double rr = buf + fe, mnx = double.PositiveInfinity, mny = double.PositiveInfinity, mxx = double.NegativeInfinity, mxy = double.NegativeInfinity;
            foreach (var q in poly) { mnx = Math.Min(mnx, q.X); mny = Math.Min(mny, q.Y); mxx = Math.Max(mxx, q.X); mxy = Math.Max(mxy, q.Y); }
            if (!Win(mnx - rr, mny - rr, mxx + rr, mxy + rr, out int r0, out int r1, out int c0, out int c1)) return;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r];
                double d = (PointInPoly(X, Y, poly) ? -1.0 : PolyEdgeDist(X, Y, poly)) - buf;
                double wv = Wv(d, fe);
                if (wv > p[i]) p[i] = wv;
            }
        }

        /// <summary>Distance to the nearest of <paramref name="pts"/>, capped at <paramref name="rmax"/> (pass 9's dist_raster).</summary>
        double[] DistRaster(List<PlanPoint> pts, double rmax)
        {
            var d = Filled(rmax);
            foreach (var p in pts)
            {
                if (!Win(p.X - rmax, p.Y - rmax, p.X + rmax, p.Y + rmax, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double v = Hypot(_xs[c] - p.X, _ys[r] - p.Y);
                    if (v < d[i]) d[i] = v;
                }
            }
            return d;
        }

        // ---- lines ----------------------------------------------------------------------------------------------------------

        PlanLine PathLine(PathDef p) => PathLineOf(_plan, _src, p, _final);

        /// <summary>
        /// A path's line as the derivation lays it, every LineStep metres: a builder's line (LineSource) straight, run on
        /// into its join points where <paramref name="join"/> (the whole plan's run: part 1's and the crossing's own lay
        /// the builder's line whole, so they stay pass 9's), a tidal crossing straight, any other path Catmull-Rom through
        /// its points. The guards read the paths by it too.
        /// </summary>
        public static PlanLine PathLineOf(RegionTerrainPlanDef plan, TerrainPlanSources src, PathDef p, bool join = true)
        {
            if (!string.IsNullOrEmpty(p.LineSource))
            {
                if (!src.Lines.TryGetValue(p.LineSource, out var pts) || pts == null || pts.Length < 2)
                    throw new InvalidOperationException("[TerrainPlan] " + p.Id + " names the line " + p.LineSource + ", which the sources do not hold.");
                return Straight(join ? JoinedLine(p, pts) : pts, Num(plan.LineStep));
            }
            var own = Num(p.Points);
            return p.Kind == PathKind.TidalCrossing ? Straight(own, Num(plan.LineStep)) : Catmull(own, Num(plan.LineStep));
        }

        /// <summary>
        /// A builder's line run on into the path's join points: the line to where a leg first reaches the first join
        /// point's x (the point there, on that leg), then the join points. With none, the line as it is.
        /// </summary>
        public static PlanPoint[] JoinedLine(PathDef p, PlanPoint[] pts)
        {
            if (p.JoinPoints == null || p.JoinPoints.Length == 0) return pts;
            var join = Num(p.JoinPoints);
            double x = join[0].X;
            var o = new List<PlanPoint> { pts[0] };
            for (int k = 0; k + 1 < pts.Length; k++)
            {
                PlanPoint a = pts[k], b = pts[k + 1];
                if (a.X != b.X && (a.X - x) * (b.X - x) <= 0)
                {
                    double t = (x - a.X) / (b.X - a.X);
                    var at = new PlanPoint(x, a.Y + t * (b.Y - a.Y));
                    if (t > 0 && at.Y != join[0].Y) o.Add(at);
                    o.AddRange(join);
                    return o.ToArray();
                }
                o.Add(b);
            }
            throw new InvalidOperationException("[TerrainPlan] " + p.Id + "'s line never reaches its join's x (" + x.ToString(CultureInfo.InvariantCulture) + ").");
        }

        PlanPoint[] PathControlPoints(PathDef p) =>
            !string.IsNullOrEmpty(p.LineSource) && _src.Lines.TryGetValue(p.LineSource, out var pts) ? pts : Num(p.Points);

        // ---- zones ----------------------------------------------------------------------------------------------------------

        static byte Zi(string zone)
        {
            int i = TerrainPlanZones.IndexOf(zone);
            if (i < 0) throw new InvalidOperationException("[TerrainPlan] no ground zone is called '" + zone + "'.");
            return (byte)i;
        }

        static readonly byte ZGrass = Zi("grass"), ZMarram = Zi("marram"), ZSand = Zi("sand"), ZShingle = Zi("shingle"),
            ZRipple = Zi("ripple"), ZShelf = Zi("shelf"), ZSilt = Zi("silt"), ZDirt = Zi("dirt"), ZSedge = Zi("sedge"),
            ZTalus = Zi("talus"), ZLedge = Zi("ledge"), ZRockweed = Zi("rockweed"), ZMusselbed = Zi("musselbed"),
            ZEelgrass = Zi("eelgrass"), ZIrishmoss = Zi("irishmoss"), ZMud = Zi("mud"), ZPath = Zi("path");

        /// <summary>A coast recipe's bands as numbers, read once.</summary>
        sealed class RecipeTable
        {
            public readonly double[] Below;
            public readonly byte[] Zone;

            public RecipeTable(CoastRecipeDef recipe)
            {
                if (recipe == null || recipe.Bands == null || recipe.Bands.Length == 0)
                    throw new InvalidOperationException("[TerrainPlan] a section or form has no recipe bands.");
                Below = new double[recipe.Bands.Length]; Zone = new byte[recipe.Bands.Length];
                for (int k = 0; k < Below.Length; k++) { Below[k] = Num(recipe.Bands[k].Below); Zone[k] = Zi(recipe.Bands[k].Zone); }
            }
        }

        double _jitAmp, _jitLam;

        /// <summary>A coast recipe's material by height, jittered (pass 9's recipe_zone): the lowest band E + jitter sits under.</summary>
        byte RecipeZone(RecipeTable recipe, double e, double x, double y, long seed)
        {
            double jit = _jitAmp * SNoise(x, y, seed, _jitLam);
            byte z = ZGrass;
            for (int k = recipe.Below.Length - 1; k >= 0; k--)
                if (e + jit < recipe.Below[k]) z = recipe.Zone[k];
            return z;
        }

        // ---- the TerrainSplat shader's height bands (pass 9's band_zone) ---------------------------------------------------

        static double Band(double e, double floor, double blend) => Smoothstep(floor - blend, floor + blend, e);

        byte BandZone(double E, double X, double Y)
        {
            var b = _src.Bands;
            double e = Math.Max(b.PaintFloor, E);
            double bg = Band(e, b.GrassFloor, b.BandBlend), bm = Band(e, b.MarramFloor, b.BandBlend), bs = Band(e, b.SandFloor, b.BandBlend);
            double br = Band(e, b.RippleFloor, b.BandBlend), bh = Band(e, b.ShingleFloor, b.BandBlend);
            double dx = X - b.IslandCentre.X, dy = (Y - b.IslandCentre.Y) * b.IslandAspect;
            double n = Hypot(dx, dy) + 1e-6, brg = (dx * b.Weather.X + dy * b.Weather.Y) / n;
            double barW = 1 - Smoothstep(b.BarHalfWidth - b.BarEdge, b.BarHalfWidth + b.BarEdge, SegDist(X, Y, b.BarFrom, b.BarTo));
            double ws = Smoothstep(-b.SectorBlend, b.SectorBlend, brg) * (1 - barW);
            double w0 = bg, w1 = bm * (1 - bg) * (1 - ws), w2 = bs * (1 - bm) * (1 - bg) * (1 - ws), w3 = bh * (1 - bg) * ws;
            double w4 = br * (1 - bs) * (1 - bm) * (1 - bg) * (1 - ws);
            double w5 = (1 - br) * (1 - bs) * (1 - bm) * (1 - bg) * (1 - ws) + (1 - bh) * (1 - bg) * ws;
            int k = 0; double best = w0;                                          // numpy's argmax: the first largest
            if (w1 > best) { k = 1; best = w1; }
            if (w2 > best) { k = 2; best = w2; }
            if (w3 > best) { k = 3; best = w3; }
            if (w4 > best) { k = 4; best = w4; }
            if (w5 > best) { k = 5; }
            switch (k)
            {
                case 0: return ZGrass;
                case 1: return ZMarram;
                case 2: return ZSand;
                case 3: return ZShingle;
                case 4: return ZRipple;
                default: return ZShelf;
            }
        }

        /// <summary>The shader's segment distance (stp.seg_dist: ab·ab, and numpy's hypot).</summary>
        static double SegDist(double px, double py, PlanPoint a, PlanPoint b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, l2 = abx * abx + aby * aby;
            double t = Clip(((px - a.X) * abx + (py - a.Y) * aby) / l2, 0, 1);
            return Hypot(px - (a.X + t * abx), py - (a.Y + t * aby));
        }

        /// <summary>Today's paint where there is some, else the bands on today's ground: what a keep holds.</summary>
        byte TodayPaint(int i, int r, int c) =>
            _src.Today[i] != TerrainPlanZones.Unpainted ? _src.Today[i] : BandZone(_src.Base[i], _xs[c], _ys[r]);
    }
}
