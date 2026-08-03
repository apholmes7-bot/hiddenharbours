using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the binnacle single-lever control's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/lever-rig/Art/leverRig.js</c> (ADR 0025, Option A: the rig's behaviour in
    /// the shipped game is pure C#; no JS ever runs in the build). Method-by-method mirror of
    /// <c>drawFrame</c> (leverRig.js:178-271): the swept-surface body (projected rings → lit quads,
    /// far-to-near), the contact shadow, the red neutral-lock trigger, and the dilated one-pixel
    /// silhouette. Every constant cites its source line; the pure geometry lives in
    /// <see cref="LeverRigGeometry"/> under numeric drift-goldens.
    ///
    /// <para><b>Perf (rule 7):</b> all scratch surfaces/lists are allocated once and reused; callers
    /// repaint only when <see cref="LeverRigGeometry.QuantizeKey"/> changes (the JS rig's own 1/48th
    /// frame cache granularity), so steady state is zero allocation and zero repaint.</para>
    /// </summary>
    public static class LeverRigRender
    {
        public const int W = LeverRigGeometry.W, H = LeverRigGeometry.H;
        public const int PX = LeverRigGeometry.PX, PY = LeverRigGeometry.PY;

        private const double DEG = System.Math.PI / 180.0;
        private const double PITCH = LeverRigGeometry.PITCH_DEG * DEG;   // leverRig.js:27
        private const double CAMD = LeverRigGeometry.CAMD;               // leverRig.js:28
        private const double GRIP_H = LeverRigGeometry.GRIP_H;           // leverRig.js:67

        private static readonly double CA = System.Math.Cos(PITCH), SA = System.Math.Sin(PITCH);

        // Key light, normalized (leverRig.js:34).
        private static readonly double[] L = Normalize(-0.50, 0.70, 0.51);

        private enum Mat { Arm, Collar, Grip, Cap }

        // ---- material ramps, dark → light (leverRig.js:71-75) -----------------------------------
        private static readonly Color32[] RampChrome = Ramp("12181c", "2f3a42", "586770", "8ea1a9", "c6d2d5", "f1f7f7");
        private static readonly Color32[] RampGraphite = Ramp("06090b", "11171b", "212b32", "36424b", "586771", "8b9ca4");
        private static readonly Color32[] RampRubber = Ramp("040608", "0c1013", "161c21", "222a30", "323c44", "4a575f");
        private static readonly Color32[] RampRed = Ramp("3f120d", "7c2a20", "b3372a", "e0554a", "f59183", "ffb7ac");

        // amb/gain shape the ramp response; rim/spec are the metal cues (leverRig.js:81-85)
        private readonly struct MatParams
        {
            public readonly double Amb, Gain, Rim, Spec, Tight;
            public MatParams(double amb, double gain, double rim, double spec, double tight)
            { Amb = amb; Gain = gain; Rim = rim; Spec = spec; Tight = tight; }
        }
        private static readonly MatParams MatMetal = new MatParams(0.10, 0.80, 0.14, 0.42, 34);
        private static readonly MatParams MatCap = new MatParams(0.00, 0.62, 0.09, 0.22, 48);
        private static readonly MatParams MatRubber = new MatParams(0.08, 0.60, 0.05, 0.10, 10);

        // ---- centreline nodes (leverRig.js:50-66): h, r, ell, bend, mat --------------------------
        private static readonly double[] NodeH = { 0, 14, 34, 58, 82, 100, 110, 117, 123, 133, 145, 156, 165, 173, 178 };
        private static readonly double[] NodeR = { 15.5, 14.4, 12.6, 11.2, 10.2, 9.6, 10.0, 11.8, 14.6, 16.8, 17.4, 16.6, 13.8, 10.0, 6.0 };
        private static readonly double[] NodeEll = { 1.00, 1.00, 1.02, 1.05, 1.08, 1.14, 1.26, 1.46, 1.62, 1.68, 1.68, 1.64, 1.52, 1.34, 1.14 };
        private static readonly double[] NodeBend = { 0, 0, 0, -1.0, -2.6, -3.8, -4.4, -4.8, -5.2, -5.6, -6.0, -6.2, -6.2, -6.2, -6.2 };
        private static readonly Mat[] NodeMat = { Mat.Arm, Mat.Arm, Mat.Arm, Mat.Arm, Mat.Arm, Mat.Arm, Mat.Collar, Mat.Collar, Mat.Grip, Mat.Grip, Mat.Grip, Mat.Grip, Mat.Grip, Mat.Cap, Mat.Cap };

        // ---- sampled centreline with slopes (leverRig.js:93-117) ---------------------------------
        private struct LinePt
        {
            public double H, R, Ell, Bend, Dr, Da, Db;
            public Mat M;
            public bool Rib;
        }
        private static readonly LinePt[] Line = BuildLine();

        private const int SEG = 22;   // cross-section vertices (leverRig.js:118)
        private static readonly double[] CU = new double[SEG];
        private static readonly double[] SU = new double[SEG];

        static LeverRigRender()
        {
            for (int j = 0; j < SEG; j++)
            {
                double u = (double)j / SEG * System.Math.PI * 2.0;
                CU[j] = System.Math.Cos(u);
                SU[j] = System.Math.Sin(u);
            }
        }

        private static LinePt[] BuildLine()
        {
            var s = new List<LinePt>(96);
            for (int i = 0; i < NodeH.Length - 1; i++)
            {
                int steps = System.Math.Max(2, DrawSurface.JsRound((NodeH[i + 1] - NodeH[i]) / 2.0));
                for (int k = (i == 0 ? 0 : 1); k <= steps; k++)
                {
                    double t = (double)k / steps;
                    s.Add(new LinePt
                    {
                        H = NodeH[i] + (NodeH[i + 1] - NodeH[i]) * t,
                        R = NodeR[i] + (NodeR[i + 1] - NodeR[i]) * t,
                        Ell = NodeEll[i] + (NodeEll[i + 1] - NodeEll[i]) * t,
                        Bend = NodeBend[i] + (NodeBend[i + 1] - NodeBend[i]) * t,
                        M = t < 0.5 ? NodeMat[i] : NodeMat[i + 1],
                    });
                }
            }
            var arr = s.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                LinePt p = arr[System.Math.Max(0, i - 1)];
                LinePt n = arr[System.Math.Min(arr.Length - 1, i + 1)];
                double dh = n.H - p.H;
                if (dh == 0.0) dh = 1.0;
                arr[i].Dr = (n.R - p.R) / dh;
                arr[i].Da = (n.R * n.Ell - p.R * p.Ell) / dh;
                arr[i].Db = (n.Bend - p.Bend) / dh;
                // one moulding groove round the rubber grip (leverRig.js:114)
                arr[i].Rib = arr[i].M == Mat.Grip && System.Math.Abs(arr[i].H - 138.0) < 1.1;
            }
            return arr;
        }

        // ---- projected vertex (leverRig.js:38-45) ------------------------------------------------
        private struct Pt { public double X, Y, Vx, Vy, Z, K; }

        private static void ToView(double x, double y, double z, double c, double s,
                                   out double vx, out double vy, out double vz)
        {
            double y0 = y * c - z * s, z0 = y * s + z * c;
            vx = x;
            vy = y0 * CA - z0 * SA;
            vz = y0 * SA + z0 * CA;
        }

        private static Pt ToScreen(double vx, double vy, double vz)
        {
            double k = CAMD / (CAMD - vz);
            return new Pt { X = PX + vx * k, Y = PY - vy * k, Vx = vx, Vy = vy, Z = vz, K = k };
        }

        // ---- reused scratch (rule 7: one buffer each, allocated once) ----------------------------
        private static DrawSurface _body, _shadow;
        private static Pt[][] _ringP;
        private static double[][] _ringN;   // 3 per vertex
        private struct Quad { public double Z; public int I, J; public bool Cap; public Color32 Col; }
        private static readonly List<Quad> _quads = new List<Quad>(2200);
        private static readonly double[] _polyX = new double[SEG];
        private static readonly double[] _polyY = new double[SEG];

        /// <summary>
        /// Draw one lever frame into <paramref name="target"/> (must be <see cref="W"/>×<see cref="H"/>).
        /// <paramref name="sig"/> is quantised to 1/48ths exactly as the rig's own cache key does
        /// (leverRig.js:283-289). Returns the pivot (constant — leverRig.js:270 returns PX/PY).
        /// </summary>
        public static void Render(DrawSurface target, float sig, HelmLeverFinish finish)
        {
            double s48 = DrawSurface.JsRound(Mathf.Clamp(sig, -1f, 1f) * 48.0) / 48.0;
            Color32[] armRamp = finish == HelmLeverFinish.Chrome ? RampChrome : RampGraphite;

            if (_body == null) { _body = new DrawSurface(W, H); _shadow = new DrawSurface(W, H); }
            if (_ringP == null)
            {
                _ringP = new Pt[Line.Length][];
                _ringN = new double[Line.Length][];
                for (int i = 0; i < Line.Length; i++) { _ringP[i] = new Pt[SEG]; _ringN[i] = new double[SEG * 3]; }
            }
            _body.Clear();
            _shadow.Clear();
            target.Clear();

            double th = (LeverRigGeometry.TH0 - s48 * LeverRigGeometry.THROW) * DEG;   // leverRig.js:31
            double c = System.Math.Cos(th), sn = System.Math.Sin(th);

            // rings of projected vertices + view-space normals (leverRig.js:184-195)
            for (int i = 0; i < Line.Length; i++)
            {
                LinePt n = Line[i];
                double a = n.R * n.Ell, b = n.R;
                for (int j = 0; j < SEG; j++)
                {
                    double cu = CU[j], su = SU[j];
                    ToView(a * cu, n.H, n.Bend + b * su, c, sn, out double vx, out double vy, out double vz);
                    _ringP[i][j] = ToScreen(vx, vy, vz);
                    ToView(cu, -(n.Da * cu * cu + n.Ell * su * (n.Dr * su + n.Db)), n.Ell * su, c, sn,
                           out double nx, out double ny, out double nz);
                    double m = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (m == 0.0) m = 1.0;
                    _ringN[i][j * 3] = nx / m; _ringN[i][j * 3 + 1] = ny / m; _ringN[i][j * 3 + 2] = nz / m;
                }
            }

            // contact shadow: the shaft dropped onto the console plane (leverRig.js:198-205)
            for (int i = 0; i < Line.Length; i++)
            {
                LinePt n = Line[i];
                if (n.H > 95.0) break;
                double y0 = n.H * c - n.Bend * sn, z0 = n.H * sn + n.Bend * c;
                ToView(0.30 * y0, 0.0, z0 + 0.34 * y0, 1.0, 0.0, out double vx, out double vy, out double vz);
                Pt p = ToScreen(vx, vy, vz);
                double f = 1.0 - n.H / 118.0;
                Disc(_shadow, p.X, p.Y, System.Math.Max(2.0, n.R * 0.8 * f + 1.5) * p.K, 1.5, new Color32(0x03, 0x06, 0x0a, 255));
            }

            // facets: cull backfaces, paint far → near (leverRig.js:208-237)
            _quads.Clear();
            for (int i = 0; i < Line.Length - 1; i++)
            {
                Mat mat = Line[i].M;
                Color32[] ramp; MatParams mp;
                if (mat == Mat.Grip) { ramp = RampRubber; mp = MatRubber; }                 // leverRig.js:86-90
                else if (mat == Mat.Arm) { ramp = armRamp; mp = MatMetal; }
                else { ramp = RampChrome; mp = MatCap; }                                    // collar + cap always chromed
                bool dark = Line[i].Rib;
                for (int j = 0; j < SEG; j++)
                {
                    int j2 = (j + 1) % SEG;
                    Pt p0 = _ringP[i][j], p1 = _ringP[i][j2], p2 = _ringP[i + 1][j2], p3 = _ringP[i + 1][j];
                    double nx = (_ringN[i][j * 3] + _ringN[i][j2 * 3] + _ringN[i + 1][j * 3] + _ringN[i + 1][j2 * 3]) / 4.0;
                    double ny = (_ringN[i][j * 3 + 1] + _ringN[i][j2 * 3 + 1] + _ringN[i + 1][j * 3 + 1] + _ringN[i + 1][j2 * 3 + 1]) / 4.0;
                    double nz = (_ringN[i][j * 3 + 2] + _ringN[i][j2 * 3 + 2] + _ringN[i + 1][j * 3 + 2] + _ringN[i + 1][j2 * 3 + 2]) / 4.0;
                    double nm = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nm == 0.0) nm = 1.0;
                    double cx = (p0.Vx + p1.Vx + p2.Vx + p3.Vx) / 4.0;
                    double cy = (p0.Vy + p1.Vy + p2.Vy + p3.Vy) / 4.0;
                    double cz = (p0.Z + p1.Z + p2.Z + p3.Z) / 4.0;
                    double vx = -cx, vy = -cy, vz = CAMD - cz;
                    double vm = System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
                    if (vm == 0.0) vm = 1.0;
                    vx /= vm; vy /= vm; vz /= vm;
                    if ((nx * vx + ny * vy + nz * vz) / nm <= 0.01) continue;   // backface
                    _quads.Add(new Quad
                    {
                        Z = cz, I = i, J = j, Cap = false,
                        Col = Shade(nx / nm, ny / nm, nz / nm, vx, vy, vz, ramp, mp, dark),
                    });
                }
            }
            // end cap — what turns to face you as the lever comes aft (leverRig.js:226-235)
            {
                int last = Line.Length - 1;
                ToView(0.0, 1.0, Line[last].Db, c, sn, out double ax, out double ay, out double az);
                double am = System.Math.Sqrt(ax * ax + ay * ay + az * az);
                if (am == 0.0) am = 1.0;
                double cz = 0.0;
                for (int j = 0; j < SEG; j++) cz += _ringP[last][j].Z;
                cz /= SEG;
                double vx = 0.0, vy = 0.0, vz = CAMD - cz;
                double vm = System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
                if (vm == 0.0) vm = 1.0;
                vx /= vm; vy /= vm; vz /= vm;
                if ((ax * vx + ay * vy + az * vz) / am > 0.0)
                {
                    _quads.Add(new Quad
                    {
                        Z = cz + 0.6, I = last, J = -1, Cap = true,
                        Col = Shade(ax / am, ay / am, az / am, vx, vy, vz, RampChrome, MatMetal, false),
                    });
                }
            }
            _quads.Sort((p, q) => p.Z.CompareTo(q.Z));
            foreach (Quad q in _quads)
            {
                if (q.Cap)
                {
                    for (int j = 0; j < SEG; j++) { _polyX[j] = _ringP[q.I][j].X; _polyY[j] = _ringP[q.I][j].Y; }
                    FillPoly(_body, _polyX, _polyY, SEG, q.Col);
                }
                else
                {
                    // swell: nudge corners outward from the centroid so facets never leave a gap (leverRig.js:149-152)
                    Pt p0 = _ringP[q.I][q.J], p1 = _ringP[q.I][(q.J + 1) % SEG];
                    Pt p2 = _ringP[q.I + 1][(q.J + 1) % SEG], p3 = _ringP[q.I + 1][q.J];
                    double cx = (p0.X + p1.X + p2.X + p3.X) / 4.0, cy = (p0.Y + p1.Y + p2.Y + p3.Y) / 4.0;
                    Swell(p0.X, p0.Y, cx, cy, out _polyX[0], out _polyY[0]);
                    Swell(p1.X, p1.Y, cx, cy, out _polyX[1], out _polyY[1]);
                    Swell(p2.X, p2.Y, cx, cy, out _polyX[2], out _polyY[2]);
                    Swell(p3.X, p3.Y, cx, cy, out _polyX[3], out _polyY[3]);
                    FillPoly(_body, _polyX, _polyY, 4, q.Col);
                }
            }

            // red neutral-lock trigger, on the starboard face of the grip (leverRig.js:240-251)
            {
                double u = 0.86, cu = System.Math.Cos(u), su = System.Math.Sin(u);
                int gi = -1;
                for (int i = 0; i < Line.Length; i++) if (Line[i].H >= GRIP_H) { gi = i; break; }
                if (gi >= 0)
                {
                    LinePt g = Line[gi];
                    double a = g.R * g.Ell, b = g.R;
                    ToView(cu, 0.0, g.Ell * su, c, sn, out double nx, out double ny, out double nz);
                    double nm2 = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nm2 == 0.0) nm2 = 1.0;
                    for (int t = -1; t <= 1; t++)
                    {
                        ToView(a * cu, g.H + t * 6.4, g.Bend + b * su, c, sn, out double vx0, out double vy0, out double vz0);
                        Pt p = ToScreen(vx0, vy0, vz0);
                        double vx = -p.Vx, vy = -p.Vy, vz = CAMD - p.Z;
                        double vm = System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
                        if (vm == 0.0) vm = 1.0;
                        if ((nx * vx + ny * vy + nz * vz) / (nm2 * vm) < 0.10) continue;   // rotated out of sight
                        Ball(_body, p.X, p.Y, (t == 0 ? 5.0 : 4.1) * p.K);
                    }
                }
            }

            // one unified silhouette: dilate the body's alpha (leverRig.js:254-268), composite order:
            // shadow at 0.24 → outline → body.
            target.Composite(_shadow, 0.24f);
            var lineCol = new Color32(5, 8, 11, 255);
            Color32[] bp = _body.Pixels, tp = target.Pixels;
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    int i = row + x;
                    if (bp[i].a > 8) continue;
                    bool near = (x > 0 && bp[i - 1].a > 8) || (x < W - 1 && bp[i + 1].a > 8)
                             || (y > 0 && bp[i - W].a > 8) || (y < H - 1 && bp[i + W].a > 8);
                    if (near) tp[i] = lineCol;
                }
            }
            target.Composite(_body, 1f);
        }

        // ---- lighting (leverRig.js:164-175) ------------------------------------------------------
        private static Color32 Shade(double nx, double ny, double nz, double vx, double vy, double vz,
                                     Color32[] ramp, MatParams m, bool dark)
        {
            double d = nx * L[0] + ny * L[1] + nz * L[2];
            double t = m.Amb + m.Gain * (0.5 + 0.5 * d);
            double nv = System.Math.Max(0.0, nx * vx + ny * vy + nz * vz);
            t += m.Rim * System.Math.Pow(1.0 - nv, 2.4);
            double hx = L[0] + vx, hy = L[1] + vy, hz = L[2] + vz;
            double hm = System.Math.Sqrt(hx * hx + hy * hy + hz * hz);
            if (hm == 0.0) hm = 1.0;
            double sp = System.Math.Max(0.0, (nx * hx + ny * hy + nz * hz) / hm);
            t += m.Spec * System.Math.Pow(sp, m.Tight);
            int i = DrawSurface.JsRound(t * (ramp.Length - 1));
            if (dark) i -= 1;
            return ramp[i < 0 ? 0 : (i > ramp.Length - 1 ? ramp.Length - 1 : i)];
        }

        // ---- crisp primitives (leverRig.js:124-161) ----------------------------------------------
        private static void Disc(DrawSurface s, double cx, double cy, double r, double wx, Color32 fill)
        {
            if (r < 0.5) return;
            int icx = DrawSurface.JsRound(cx), icy = DrawSurface.JsRound(cy);
            int ry = System.Math.Max(1, DrawSurface.JsRound(r));
            int rx = System.Math.Max(1, DrawSurface.JsRound(r * wx));
            for (int dy = -ry; dy <= ry; dy++)
            {
                double f = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - (double)(dy * dy) / (ry * ry)));
                int half = (int)System.Math.Floor(rx * f);
                s.FillRect(icx - half, icy + dy, 2 * half + 1, 1, fill);
            }
        }

        // shaded ball, for the small red trigger (leverRig.js:154-161)
        private static void Ball(DrawSurface s, double cx, double cy, double r)
        {
            Disc(s, cx, cy, r + 1.1, 1, new Color32(0x05, 0x08, 0x0b, 255));
            Disc(s, cx, cy, r, 1, RampRed[1]);
            Disc(s, cx + r * 0.16, cy - r * 0.18, r * 0.80, 1, RampRed[2]);
            Disc(s, cx + r * 0.30, cy - r * 0.34, r * 0.54, 1, RampRed[3]);
            Disc(s, cx + r * 0.40, cy - r * 0.46, r * 0.30, 1, RampRed[4]);
            Disc(s, cx + r * 0.46, cy - r * 0.54, System.Math.Max(1.0, r * 0.13), 1, RampRed[5]);
        }

        private static void Swell(double x, double y, double cx, double cy, out double ox, out double oy)
        {
            double dx = x - cx, dy = y - cy;
            double m = System.Math.Sqrt(dx * dx + dy * dy);
            if (m == 0.0) m = 1.0;
            ox = x + dx / m * 0.6;
            oy = y + dy / m * 0.6;
        }

        // scanline polygon fill, integer spans — no AA, this is pixel art (leverRig.js:131-147)
        private static void FillPoly(DrawSurface s, double[] xs, double[] ys, int n, Color32 color)
        {
            double minY = double.MaxValue, maxY = double.MinValue, minX = double.MaxValue, maxX = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
                if (xs[i] < minX) minX = xs[i];
                if (xs[i] > maxX) maxX = xs[i];
            }
            if (maxY < -2 || minY > H + 2 || maxX < -2 || minX > W + 2) return;
            int y0 = DrawSurface.JsRound(minY), y1 = DrawSurface.JsRound(maxY);
            for (int y = y0; y <= y1; y++)
            {
                double yc = y + 0.5;
                double lo = double.MaxValue, hi = double.MinValue;
                for (int i = 0; i < n; i++)
                {
                    double ay = ys[i], by = ys[(i + 1) % n];
                    if ((ay <= yc) != (by <= yc))
                    {
                        double x = xs[i] + (yc - ay) / (by - ay) * (xs[(i + 1) % n] - xs[i]);
                        if (x < lo) lo = x;
                        if (x > hi) hi = x;
                    }
                }
                if (lo > hi) { lo = minX; hi = maxX; }   // sub-pixel sliver: keep the seam closed
                int xa = DrawSurface.JsRound(lo), xb = DrawSurface.JsRound(hi);
                s.FillRect(xa, y, System.Math.Max(1, xb - xa), 1, color);
            }
        }

        private static double[] Normalize(double x, double y, double z)
        {
            double m = System.Math.Sqrt(x * x + y * y + z * z);
            return new[] { x / m, y / m, z / m };
        }

        private static Color32[] Ramp(params string[] hex)
        {
            var r = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++)
            {
                r[i] = new Color32(
                    (byte)System.Convert.ToInt32(hex[i].Substring(0, 2), 16),
                    (byte)System.Convert.ToInt32(hex[i].Substring(2, 2), 16),
                    (byte)System.Convert.ToInt32(hex[i].Substring(4, 2), 16),
                    255);
            }
            return r;
        }
    }
}
