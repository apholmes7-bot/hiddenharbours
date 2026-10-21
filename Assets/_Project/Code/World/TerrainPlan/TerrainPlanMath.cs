using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>A point in world metres, in double precision: the terrain plan's derivation frame.</summary>
    [Serializable]
    public struct PlanPoint
    {
        public double X;
        public double Y;

        public PlanPoint(double x, double y)
        {
            X = x; Y = y;
        }

        public override string ToString() =>
            "(" + X.ToString("R", CultureInfo.InvariantCulture) + ", " + Y.ToString("R", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// The seabed grid the plan derives on: <see cref="W"/> × <see cref="H"/> cells of <see cref="Mpp"/> metres,
    /// row 0 at the north edge (<see cref="Y1"/>), column 0 at the west edge (<see cref="X0"/>). The same grid the
    /// painted height, splat and still-water maps hold (PaintedHeightMap's frame).
    /// </summary>
    public readonly struct TerrainPlanGrid
    {
        public readonly int W;
        public readonly int H;
        public readonly double Mpp;
        public readonly double X0;
        public readonly double Y1;

        public TerrainPlanGrid(int w, int h, double mpp, double x0, double y1)
        {
            W = w; H = h; Mpp = mpp; X0 = x0; Y1 = y1;
        }

        public int Count => W * H;
        public double XG(int c) => X0 + (c + 0.5) * Mpp;
        public double YG(int r) => Y1 - (r + 0.5) * Mpp;

        /// <summary>The column and row a world point falls in (either may be off the map).</summary>
        public int ColOf(double x) => (int)Math.Floor((x - X0) / Mpp);
        public int RowOf(double y) => (int)Math.Floor((Y1 - y) / Mpp);

        /// <summary>The rows and columns covering a world rectangle, or false when it misses the map.</summary>
        public bool Win(double x0, double y0, double x1, double y1, out int r0, out int r1, out int c0, out int c1)
        {
            c0 = Math.Max(0, (int)Math.Floor((x0 - X0) / Mpp));
            c1 = Math.Min(W, (int)Math.Ceiling((x1 - X0) / Mpp) + 1);
            r0 = Math.Max(0, (int)Math.Floor((Y1 - y1) / Mpp));
            r1 = Math.Min(H, (int)Math.Ceiling((Y1 - y0) / Mpp) + 1);
            return c0 < c1 && r0 < r1;
        }

        /// <summary>Bilinear sample of a grid raster at a world point (terrain pass 9's sample_grid).</summary>
        public double Sample(double[] a, double x, double y)
        {
            double c = (x - X0) / Mpp - 0.5, r = (Y1 - y) / Mpp - 0.5;
            int c0 = Math.Min(Math.Max((int)Math.Floor(c), 0), W - 2);
            int r0 = Math.Min(Math.Max((int)Math.Floor(r), 0), H - 2);
            double fc = TerrainPlanMath.Clip(c - c0, 0, 1), fr = TerrainPlanMath.Clip(r - r0, 0, 1);
            int i = r0 * W + c0;
            return a[i] * (1 - fc) * (1 - fr) + a[i + 1] * fc * (1 - fr) + a[i + W] * (1 - fc) * fr + a[i + W + 1] * fc * fr;
        }
    }

    /// <summary>
    /// A polyline resampled by arc length (<see cref="R"/> at arc lengths <see cref="S"/>), with its control
    /// points' arc lengths (<see cref="Knots"/>), and the closest-point query the derivation runs on it.
    /// </summary>
    public sealed class PlanLine
    {
        public readonly PlanPoint[] R;
        public readonly double[] S;
        public readonly double[] Knots;
        readonly double[] _ax, _ay, _bx, _by, _l2, _l, _minX, _minY, _maxX, _maxY;

        public PlanLine(PlanPoint[] r, double[] s, double[] knots)
        {
            R = r; S = s; Knots = knots;
            int n = Math.Max(0, r.Length - 1);
            _ax = new double[n]; _ay = new double[n]; _bx = new double[n]; _by = new double[n];
            _l2 = new double[n]; _l = new double[n];
            _minX = new double[n]; _minY = new double[n]; _maxX = new double[n]; _maxY = new double[n];
            for (int k = 0; k < n; k++)
            {
                _ax[k] = r[k].X; _ay[k] = r[k].Y;
                _bx[k] = r[k + 1].X - r[k].X; _by[k] = r[k + 1].Y - r[k].Y;
                _l2[k] = Math.Max(_bx[k] * _bx[k] + _by[k] * _by[k], 1e-12);
                _l[k] = Math.Sqrt(_l2[k]);
                _minX[k] = Math.Min(r[k].X, r[k + 1].X); _maxX[k] = Math.Max(r[k].X, r[k + 1].X);
                _minY[k] = Math.Min(r[k].Y, r[k + 1].Y); _maxY[k] = Math.Max(r[k].Y, r[k + 1].Y);
            }
        }

        public int Segments => _ax.Length;
        public double Length => S[S.Length - 1];

        public double MinX { get { double v = double.PositiveInfinity; foreach (var p in R) v = Math.Min(v, p.X); return v; } }
        public double MinY { get { double v = double.PositiveInfinity; foreach (var p in R) v = Math.Min(v, p.Y); return v; } }
        public double MaxX { get { double v = double.NegativeInfinity; foreach (var p in R) v = Math.Max(v, p.X); return v; } }
        public double MaxY { get { double v = double.NegativeInfinity; foreach (var p in R) v = Math.Max(v, p.Y); return v; } }

        double D2(int k, double px, double py, out double t, out double ex, out double ey)
        {
            double dx = px - _ax[k], dy = py - _ay[k];
            t = (dx * _bx[k] + dy * _by[k]) / _l2[k];
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            ex = dx - t * _bx[k]; ey = dy - t * _by[k];
            return ex * ex + ey * ey;
        }

        /// <summary>
        /// The distance to the line, the side (+1 = right of travel) and the arc length of the closest point
        /// (terrain pass 9's poly_query). The first closest segment wins, as numpy's argmin does. <paramref name="hint"/>
        /// (a segment index, e.g. the neighbouring cell's) only bounds the search: it never changes the answer.
        /// </summary>
        public void Query(double px, double py, ref int hint, out double dist, out double side, out double along)
        {
            int n = _ax.Length;
            double bound = double.PositiveInfinity;
            if (hint >= 0 && hint < n) bound = D2(hint, px, py, out _, out _, out _);
            double bound2 = bound * (1 + 1e-9) + 1e-12;
            int best = -1; double bestD2 = double.PositiveInfinity, bestT = 0, bestEx = 0, bestEy = 0;
            for (int k = 0; k < n; k++)
            {
                double gx = _minX[k] - px; if (px - _maxX[k] > gx) gx = px - _maxX[k]; if (gx < 0) gx = 0;
                double gy = _minY[k] - py; if (py - _maxY[k] > gy) gy = py - _maxY[k]; if (gy < 0) gy = 0;
                if (gx * gx + gy * gy > bound2) continue;
                double d2 = D2(k, px, py, out double t, out double ex, out double ey);
                if (d2 < bestD2) { best = k; bestD2 = d2; bestT = t; bestEx = ex; bestEy = ey; }
                if (d2 < bound) { bound = d2; bound2 = bound * (1 + 1e-9) + 1e-12; }
            }
            hint = best;
            dist = Math.Sqrt(bestD2);
            double cross = _bx[best] * bestEy - _by[best] * bestEx;
            side = cross < 0 ? 1.0 : -1.0;
            along = S[best] + bestT * _l[best];
        }
    }

    /// <summary>
    /// <b>THE TERRAIN PLAN'S ARITHMETIC.</b> Pure functions of their arguments, in double precision: the hashed
    /// value noise, the smoothstep, the polylines and the shapes terrain pass 9's derivation (plan9_lib.py) runs on,
    /// ported line for line so the same plan gives the same maps, bit for bit, on every machine.
    /// </summary>
    public static class TerrainPlanMath
    {
        // ---- numbers ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// A Def's float as the decimal number it was authored as (96.6f → 96.6, not 96.59999847): the double nearest the
        /// decimal with the fewest places that reads back as the same float, which is what .NET 8's "R" string parses to.
        /// IEEE arithmetic on exact powers of ten, never a string: Unity's Mono writes "R" with nine digits where eight
        /// suffice (-36.600315f is "-36.6003151" there), so a string here would give each runtime its own map. Zero is
        /// +0; a magnitude past 1e±22 is the float itself.
        /// </summary>
        public static double Num(float f)
        {
            double a = f;
            if (a == 0) return 0.0;
            if (double.IsNaN(a) || double.IsInfinity(a)) return a;
            double m = Math.Abs(a);
            float fm = Math.Abs(f);
            int q = 0;                                                // decimal places: start one short of the leading digit
            while (q > -22 && m >= Pow10[1 - q]) q--;
            while (q >= 0 && q < 22 && m * Pow10[q] < 1) q++;
            for (q -= 1; q <= 22; q++)
            {
                if (q < -22) continue;
                double s = q >= 0 ? m * Pow10[q] : m / Pow10[-q];
                if (s >= 1e10) break;                                 // nine digits always read back; this is past them
                double k = Math.Round(s, MidpointRounding.ToEven);
                if (k == 0) continue;
                // k and 10^|q| are exact doubles, so one IEEE operation is the correctly rounded value of the decimal.
                double d = q >= 0 ? k / Pow10[q] : k * Pow10[-q];
                if ((float)d == fm) return a < 0 ? -d : d;
            }
            return a;
        }

        public static PlanPoint Num(Vector2 v) => new PlanPoint(Num(v.x), Num(v.y));

        public static PlanPoint[] Num(Vector2[] v)
        {
            var o = new PlanPoint[v == null ? 0 : v.Length];
            for (int i = 0; i < o.Length; i++) o[i] = Num(v[i]);
            return o;
        }

        public static double[] Num(float[] v)
        {
            var o = new double[v == null ? 0 : v.Length];
            for (int i = 0; i < o.Length; i++) o[i] = Num(v[i]);
            return o;
        }

        public static double Clip(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>numpy's clip then the smoothstep: u² (3 − 2u) on [0, 1].</summary>
        public static double Ss(double u)
        {
            u = u < 0 ? 0 : (u > 1 ? 1 : u);
            return u * u * (3 - 2 * u);
        }

        /// <summary>The shader's smoothstep(a, b, x).</summary>
        public static double Smoothstep(double a, double b, double x) => Ss((x - a) / (b - a));

        public static double Smin(double a, double b, double k)
        {
            double h = Clip(0.5 + 0.5 * (b - a) / k, 0, 1);
            return b + (a - b) * h - k * h * (1 - h);
        }

        public static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        /// <summary>Python's float modulo: the result takes the divisor's sign.</summary>
        public static double Mod(double a, double b)
        {
            double m = a % b;
            if (m != 0) { if ((b < 0) != (m < 0)) m += b; }
            else m = b < 0 ? -0.0 : 0.0;
            return m;
        }

        public static double Degrees(double radians) => radians * (180.0 / Math.PI);

        /// <summary>Python's math.radians: degrees × (π / 180).</summary>
        public static double Radians(double degrees) => degrees * (Math.PI / 180.0);

        /// <summary>
        /// Python's round(x, ndigits) on a float, exactly: the binary value rounded to <paramref name="ndigits"/>
        /// decimals, ties to even, returned as the double nearest that decimal. A record's rounding (a pool's
        /// centre, a scene item's position) is part of the data, so it must not drift with the runtime.
        /// </summary>
        public static double PyRound(double x, int ndigits)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || x == 0) return x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long frac = bits & 0xFFFFFFFFFFFFFL;
            long mant = exp == 0 ? frac : frac | (1L << 52);
            int e2 = (exp == 0 ? 1 : exp) - 1075;                    // x = mant · 2^e2
            var num = new System.Numerics.BigInteger(mant);
            var den = System.Numerics.BigInteger.One;
            if (e2 >= 0) num <<= e2; else den <<= -e2;
            var p10 = System.Numerics.BigInteger.Pow(10, Math.Abs(ndigits));
            if (ndigits >= 0) num *= p10; else den *= p10;
            var q = System.Numerics.BigInteger.DivRem(num, den, out var rem);
            int c = (rem * 2).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            // q and 10^n are exact doubles here (q < 2^53, n ≤ 22), so one IEEE division is the correctly rounded quotient.
            double r = ndigits > 22 ? (double)q / Math.Pow(10, ndigits)
                : ndigits >= 0 ? (double)q / Pow10[ndigits] : (double)q * Math.Pow(10, -ndigits);
            return neg ? -r : r;
        }

        static readonly double[] Pow10 =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
            1e20, 1e21, 1e22,
        };

        /// <summary>numpy's round on one value: to the nearest integer, ties to even.</summary>
        public static double RoundHalfEven(double x) => Math.Round(x, MidpointRounding.ToEven);

        /// <summary>numpy's interp: piecewise linear through (xp, fp), held flat past both ends.</summary>
        public static double Interp(double x, double[] xp, double[] fp)
        {
            int n = xp.Length;
            if (x > xp[n - 1]) return fp[n - 1];
            if (x < xp[0]) return fp[0];
            int lo = 0, hi = n;                                  // the last j with xp[j] <= x
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (xp[mid] <= x) lo = mid; else hi = mid; }
            int j = lo;
            if (j == n - 1) return fp[j];
            if (xp[j] == x) return fp[j];
            double slope = (fp[j + 1] - fp[j]) / (xp[j + 1] - xp[j]);
            return slope * (x - xp[j]) + fp[j];
        }

        // ---- noise: a hashed value lattice, smooth-interpolated; a pure function of (seed, x, y) ---------------------

        public static double Hash01(double ix, double iy, long seed)
        {
            ulong x = (ulong)((long)ix & 0xffffffffL), y = (ulong)((long)iy & 0xffffffffL);
            ulong h = unchecked(x * 0x27d4eb2dUL + y * 0x165667b1UL + (ulong)(seed & 0xffffffffL) * 0x9e3779b1UL) & 0xffffffffUL;
            h ^= h >> 15; h = unchecked(h * 0x85ebca6bUL) & 0xffffffffUL;
            h ^= h >> 13; h = unchecked(h * 0xc2b2ae35UL) & 0xffffffffUL;
            h ^= h >> 16;
            return h / 4294967295.0;
        }

        public static double VNoise(double x, double y, long seed)
        {
            double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy;
            double u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            double a = Hash01(ix, iy, seed), b = Hash01(ix + 1, iy, seed);
            double c = Hash01(ix, iy + 1, seed), d = Hash01(ix + 1, iy + 1, seed);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static double Fbm(double x, double y, long seed, double lam = 1.0, int octaves = 3)
        {
            x /= lam; y /= lam;
            double s = 0.0, amp = 1.0, norm = 0.0;
            for (int k = 0; k < octaves; k++)
            {
                double m = 1 << k;
                s = s + amp * VNoise(x * m + 13.7 * k, y * m + 7.1 * k, seed + 101 * k);
                norm += amp; amp *= 0.5;
            }
            return s / norm;
        }

        /// <summary>fbm recentred and scaled to roughly [−1, 1].</summary>
        public static double SNoise(double x, double y, long seed, double lam, int octaves = 3) =>
            (Fbm(x, y, seed, lam, octaves) - 0.5) * 2.6;

        /// <summary>
        /// A point's radius in a turned ellipse (1 on its rim; <paramref name="cr"/>, <paramref name="sr"/> the turn's cosine
        /// and sine), wobbled by snoise at <paramref name="lam"/> unless the wobble is 0: a pond's shape (plan9_lib.pond_r),
        /// a pool's (p2_lib.ellipse_r), and the guards' reading of both.
        /// </summary>
        public static double EllipseRadius(double X, double Y, PlanPoint c, double ra, double rb, double cr, double sr, long seed, double wob,
                                           double lam)
        {
            double dx = X - c.X, dy = Y - c.Y;
            double u = (dx * cr + dy * sr) / ra, v = (-dx * sr + dy * cr) / rb;
            double h = Hypot(u, v);
            return wob == 0 ? h : h * (1 + wob * SNoise(X, Y, seed, lam));
        }

        // ---- the builder's roads (StPetersStarterSplat.BentPath's hash, StPetersShoreMap.Hash01) ----------------------

        public static double Fnv01(int x, int y, int salt)
        {
            ulong h = 2166136261;
            foreach (int v in new[] { x, y, salt })
                h = unchecked((h ^ ((ulong)(long)v & 0xffffffffUL)) * 16777619UL) & 0xffffffffUL;
            h ^= h >> 15; h = unchecked(h * 2246822519UL) & 0xffffffffUL; h ^= h >> 13;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }

        // ---- polylines -------------------------------------------------------------------------------------------------

        /// <summary>Uniform Catmull-Rom through the control points, resampled every <paramref name="step"/> metres.</summary>
        public static PlanLine Catmull(IList<PlanPoint> pts, double step)
        {
            int n = pts.Count;
            if (n < 2) throw new ArgumentException("[TerrainPlan] a line needs two points.");
            var q = new PlanPoint[n + 2];
            q[0] = new PlanPoint(pts[0].X * 2 - pts[1].X, pts[0].Y * 2 - pts[1].Y);
            for (int i = 0; i < n; i++) q[i + 1] = pts[i];
            q[n + 1] = new PlanPoint(pts[n - 1].X * 2 - pts[n - 2].X, pts[n - 1].Y * 2 - pts[n - 2].Y);
            var cx = new List<double>(); var cy = new List<double>(); var starts = new List<int>();
            for (int i = 1; i < q.Length - 2; i++)
            {
                PlanPoint p0 = q[i - 1], p1 = q[i], p2 = q[i + 1], p3 = q[i + 2];
                int m = Math.Max(4, (int)Math.Ceiling(Hypot(p2.X - p1.X, p2.Y - p1.Y) / step * 2));
                double dt = 1.0 / m;
                starts.Add(cx.Count);
                for (int j = 0; j < m; j++)
                {
                    double t = j * dt, t2 = t * t, t3 = Math.Pow(t, 3);
                    cx.Add(0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3));
                    cy.Add(0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
                }
            }
            starts.Add(cx.Count);
            cx.Add(q[n].X); cy.Add(q[n].Y);
            var d = new double[cx.Count];
            for (int i = 1; i < d.Length; i++) d[i] = d[i - 1] + Hypot(cx[i] - cx[i - 1], cy[i] - cy[i - 1]);
            var knots = new double[starts.Count];
            for (int i = 0; i < knots.Length; i++) knots[i] = d[starts[i]];
            return Resample(cx.ToArray(), cy.ToArray(), d, step, knots);
        }

        /// <summary>A polyline of straight legs, resampled every <paramref name="step"/> metres.</summary>
        public static PlanLine Straight(IList<PlanPoint> pts, double step)
        {
            int n = pts.Count;
            var px = new double[n]; var py = new double[n]; var d = new double[n];
            for (int i = 0; i < n; i++) { px[i] = pts[i].X; py[i] = pts[i].Y; }
            for (int i = 1; i < n; i++) d[i] = d[i - 1] + Hypot(px[i] - px[i - 1], py[i] - py[i - 1]);
            return Resample(px, py, d, step, (double[])d.Clone());
        }

        /// <summary>A polyline as given, not resampled: its points, their running arc length, and each point a knot.</summary>
        public static PlanLine Exact(IList<PlanPoint> pts)
        {
            int n = pts.Count;
            var r = new PlanPoint[n]; var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                r[i] = pts[i];
                if (i > 0) s[i] = s[i - 1] + Hypot(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y);
            }
            return new PlanLine(r, s, (double[])s.Clone());
        }

        /// <summary>
        /// Each point's unit tangent by numpy's gradient (central differences, one-sided at the ends), and its normal
        /// to the left of travel (−ty, tx): terrain pass 9 part 2's chain().
        /// </summary>
        public static void Normals(PlanPoint[] r, out PlanPoint[] tangent, out PlanPoint[] leftNormal)
        {
            int n = r.Length;
            tangent = new PlanPoint[n]; leftNormal = new PlanPoint[n];
            for (int i = 0; i < n; i++)
            {
                double gx, gy;
                if (n < 2) { gx = 0; gy = 0; }
                else if (i == 0) { gx = r[1].X - r[0].X; gy = r[1].Y - r[0].Y; }
                else if (i == n - 1) { gx = r[n - 1].X - r[n - 2].X; gy = r[n - 1].Y - r[n - 2].Y; }
                else { gx = (r[i + 1].X - r[i - 1].X) / 2.0; gy = (r[i + 1].Y - r[i - 1].Y) / 2.0; }
                double l = Math.Max(Hypot(gx, gy), 1e-9);
                tangent[i] = new PlanPoint(gx / l, gy / l);
                leftNormal[i] = new PlanPoint(-gy / l, gx / l);
            }
        }

        /// <summary>numpy's searchsorted (side left): the first index whose value is not below <paramref name="v"/>.</summary>
        public static int SearchSorted(double[] a, double v)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (a[mid] < v) lo = mid + 1; else hi = mid; }
            return lo;
        }

        static PlanLine Resample(double[] cx, double[] cy, double[] d, double step, double[] knots)
        {
            double total = d[d.Length - 1];
            int m = Math.Max(2, (int)Math.Round(total / step) + 1);
            double ds = total / (m - 1);
            var s = new double[m]; var r = new PlanPoint[m];
            for (int i = 0; i < m; i++) s[i] = i * ds;
            s[m - 1] = total;
            for (int i = 0; i < m; i++) r[i] = new PlanPoint(Interp(s[i], d, cx), Interp(s[i], d, cy));
            return new PlanLine(r, s, knots);
        }

        public static bool PointInPoly(double px, double py, IList<PlanPoint> poly)
        {
            bool inside = false;
            double x0 = poly[poly.Count - 1].X, y0 = poly[poly.Count - 1].Y;
            for (int i = 0; i < poly.Count; i++)
            {
                double x1 = poly[i].X, y1 = poly[i].Y;
                if (((y1 > py) != (y0 > py)) && (px < (x0 - x1) * (py - y1) / ((y0 - y1) + 1e-12) + x1)) inside = !inside;
                x0 = x1; y0 = y1;
            }
            return inside;
        }

        /// <summary>The distance to a segment (analytic.seg: a degenerate segment is its start point).</summary>
        public static double Seg(double px, double py, PlanPoint a, PlanPoint b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, l2 = abx * abx + aby * aby;
            double t = l2 > 1e-6 ? Clip(((px - a.X) * abx + (py - a.Y) * aby) / l2, 0, 1) : 0;
            return Hypot(px - (a.X + t * abx), py - (a.Y + t * aby));
        }

        public static double PolyEdgeDist(double px, double py, IList<PlanPoint> poly)
        {
            double d = double.PositiveInfinity;
            for (int i = 0; i < poly.Count; i++)
                d = Math.Min(d, Seg(px, py, poly[i], poly[(i + 1) % poly.Count]));
            return d;
        }

        /// <summary>The convex hull, counter-clockwise (Andrew's monotone chain), of the distinct points.</summary>
        public static List<PlanPoint> Hull(IEnumerable<PlanPoint> pts)
        {
            var p = new List<PlanPoint>();
            foreach (var q in pts)
            {
                bool seen = false;
                foreach (var r in p) if (r.X == q.X && r.Y == q.Y) { seen = true; break; }
                if (!seen) p.Add(q);
            }
            p.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            if (p.Count < 3) return p;
            double Cross(PlanPoint o, PlanPoint a, PlanPoint b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            var lo = new List<PlanPoint>(); var up = new List<PlanPoint>();
            foreach (var q in p)
            {
                while (lo.Count >= 2 && Cross(lo[lo.Count - 2], lo[lo.Count - 1], q) <= 0) lo.RemoveAt(lo.Count - 1);
                lo.Add(q);
            }
            for (int i = p.Count - 1; i >= 0; i--)
            {
                while (up.Count >= 2 && Cross(up[up.Count - 2], up[up.Count - 1], p[i]) <= 0) up.RemoveAt(up.Count - 1);
                up.Add(p[i]);
            }
            lo.RemoveAt(lo.Count - 1); up.RemoveAt(up.Count - 1);
            lo.AddRange(up);
            return lo;
        }

        /// <summary>Single-link clusters: points nearer than <paramref name="link"/> share one.</summary>
        public static List<List<PlanPoint>> Clusters(IList<PlanPoint> pts, double link)
        {
            int n = pts.Count;
            var par = new int[n];
            for (int i = 0; i < n; i++) par[i] = i;
            int F(int i) { while (par[i] != i) { par[i] = par[par[i]]; i = par[i]; } return i; }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Hypot(pts[j].X - pts[i].X, pts[j].Y - pts[i].Y) < link) par[F(i)] = F(j);
            var order = new List<int>(); var groups = new Dictionary<int, List<PlanPoint>>();
            for (int i = 0; i < n; i++)
            {
                int r = F(i);
                if (!groups.TryGetValue(r, out var g)) { g = new List<PlanPoint>(); groups[r] = g; order.Add(r); }
                g.Add(pts[i]);
            }
            var o = new List<List<PlanPoint>>();
            foreach (int r in order) o.Add(groups[r]);
            return o;
        }
    }
}
