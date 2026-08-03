using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the console steering wheel's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/console-wheel/Art/wheelRig.js</c> (ADR 0025, Option A: no JS in the
    /// build). Line-by-line mirror of <c>bake(deg, o)</c> (wheelRig.js:68-161): the wheel is DRAWN
    /// AT ANGLE, NOT ROTATED — every pixel of the 256×256 cell is solved for what it belongs to
    /// (hub boss + turning screws → turned knobs → rim section with the gold king mark → tapering
    /// spokes) and shaded from a FIXED upper-left key with a cylinder cross-section term, so the
    /// light never orbits the boat. The spin maths lives in <see cref="WheelRigGeometry"/>.
    ///
    /// <para><b>Perf (rule 7):</b> zero allocation per frame — the caller owns the one 256×256
    /// <see cref="DrawSurface"/>; repaint only when <see cref="WheelRigGeometry.QuantizeKey"/>
    /// changes (the rig's own half-degree cache key). A repaint is one full per-pixel solve
    /// (~65k pixels); the profile numbers ride the PR body.</para>
    /// </summary>
    public static class WheelRigRender
    {
        public const int W = WheelRigGeometry.W, H = WheelRigGeometry.H;

        private const double DEG = System.Math.PI / 180.0;
        private const int PVX = WheelRigGeometry.PVX, PVY = WheelRigGeometry.PVY;
        private const int HUBR = WheelRigGeometry.HUBR, KNOBL = WheelRigGeometry.KNOBL;
        private const int SPOKES = WheelRigGeometry.SPOKES;

        // ---- palettes (wheelRig.js:34-38) ---------------------------------------------------------
        private static readonly Color32[] GRAPH = RigDrawUtil.Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] RUBBER = RigDrawUtil.Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47", "42505a");
        private static readonly Color32[] STEEL = RigDrawUtil.Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32[] TEAK = RigDrawUtil.Ramp("2b2016", "3f2f20", "57402a", "6d5134", "8a6a42", "a5834f");
        private static readonly Color32[] GOLD = RigDrawUtil.Ramp("7a5a1c", "a8842a", "e0b13a");   // the cove-gold king spoke

        // Key light, normalized in-plane + out-of-screen component (wheelRig.js:51-52).
        private static readonly double LX, LY;
        private const double LZ = 0.62;

        static WheelRigRender()
        {
            double vx = -0.60, vy = -0.72;
            double m = System.Math.Sqrt(vx * vx + vy * vy);
            LX = vx / m;
            LY = vy / m;
        }

        // Scratch spoke table (SPOKES entries, reused — rule 7).
        private static readonly double[] _spokeA = new double[SPOKES];
        private static readonly double[] _spokeAx = new double[SPOKES];
        private static readonly double[] _spokeAy = new double[SPOKES];

        private static void RimsFor(HelmWheelRim rim, out Color32[] rimRamp, out Color32[] knobRamp,
                                    out Color32[] spokeRamp)
        {
            // wheelRig.js:41-45 RIMS: rubber = stock (graphite knobs), teak = turned teak rim and
            // handles, steel = polished stainless. Spokes are STEEL on every finish.
            switch (rim)
            {
                case HelmWheelRim.Teak: rimRamp = TEAK; knobRamp = TEAK; break;
                case HelmWheelRim.Steel: rimRamp = STEEL; knobRamp = STEEL; break;
                default: rimRamp = RUBBER; knobRamp = GRAPH; break;
            }
            spokeRamp = STEEL;
        }

        private static Color32 Pick(Color32[] ramp, double t)
        {
            // pickRGB (wheelRig.js:57): ramp[clamp(round(clamp01(t)*(len-1)))]
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;
            int i = DrawSurface.JsRound(t * (ramp.Length - 1));
            if (i < 0) i = 0;
            else if (i > ramp.Length - 1) i = ramp.Length - 1;
            return ramp[i];
        }

        // cylinder shading (wheelRig.js:60-65): q across the section, (ax, ay) the barrel axis.
        private static double CylShade(double q, double ax, double ay)
        {
            double px = -ay, py = ax;
            double nz = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - q * q));
            double d = (px * q) * LX + (py * q) * LY + nz * LZ;
            if (d < 0.0) d = 0.0;
            else if (d > 1.0) d = 1.0;
            double t = 0.16 + 0.84 * d;
            return t > 1.0 ? 1.0 : t;
        }

        private static double AngDiff(double a, double b)
        {
            // wheelRig.js:81 — signed smallest angle, (a-b) wrapped into (-180, 180].
            double x = (a - b) % 360.0;
            if (x > 180.0) x -= 360.0;
            if (x < -180.0) x += 360.0;
            return x;
        }

        /// <summary>
        /// Solve one wheel frame at <paramref name="degDeg"/> into <paramref name="target"/> (must
        /// be <see cref="W"/>×<see cref="H"/>; cleared here). Mirror of bake() — wheelRig.js:68-161.
        /// </summary>
        public static void Render(DrawSurface target, double degDeg, HelmWheelRim rim)
        {
            RimsFor(rim, out Color32[] RIM, out Color32[] KN, out Color32[] SP);
            target.Clear();
            Color32[] px = target.Pixels;

            for (int k = 0; k < SPOKES; k++)
            {
                double a = (degDeg + k * (360.0 / SPOKES)) * DEG;
                _spokeA[k] = a;
                _spokeAx[k] = System.Math.Sin(a);
                _spokeAy[k] = -System.Math.Cos(a);
            }
            double kingA = _spokeA[0] * 180.0 / System.Math.PI;

            int rI = WheelRigGeometry.RI, rO = WheelRigGeometry.RO;
            double outer = rO + KNOBL + 3;

            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    double dx = x + 0.5 - PVX, dy = y + 0.5 - PVY;
                    double r = System.Math.Sqrt(dx * dx + dy * dy);
                    if (r > outer) continue;
                    int i = row + x;

                    // --- hub (top of the stack) — wheelRig.js:91-105 ---
                    if (r <= HUBR + 4)
                    {
                        double nx = dx / (r != 0.0 ? r : 1.0), ny = dy / (r != 0.0 ? r : 1.0);
                        double face = Clamp01(0.5 + 0.5 * (nx * LX + ny * LY));
                        Color32 rgb;
                        if (r > HUBR + 2) rgb = Pick(GRAPH, 0.02);
                        else if (r > HUBR - 1) rgb = Pick(GRAPH, 0.22 + 0.5 * face);
                        else if (r > HUBR * 0.55) rgb = Pick(GRAPH, 0.34 + 0.5 * face);
                        else if (r > HUBR * 0.42) rgb = Pick(GRAPH, 0.06);
                        else rgb = Pick(GRAPH, 0.40 + 0.45 * face);
                        // four screws on the boss, turning with the wheel
                        for (int k = 0; k < 4; k++)
                        {
                            double a = (degDeg + 45.0 + k * 90.0) * DEG;
                            double sx = System.Math.Sin(a) * HUBR * 0.74;
                            double sy = -System.Math.Cos(a) * HUBR * 0.74;
                            double q = System.Math.Sqrt((dx - sx) * (dx - sx) + (dy - sy) * (dy - sy));
                            if (q < 3.2) rgb = q < 1.6 ? Pick(STEEL, 0.8) : Pick(STEEL, 0.25);
                        }
                        px[i] = rgb;
                        continue;
                    }

                    // --- turned knobs (over the rim) — wheelRig.js:108-125 ---
                    bool done = false;
                    for (int k = 0; k < SPOKES; k++)
                    {
                        double ax = _spokeAx[k], ay = _spokeAy[k];
                        double t0 = rO - 4, t1 = rO + KNOBL;
                        double along = dx * ax + dy * ay;
                        if (along < t0 - 1 || along > t1 + 1) continue;
                        double across = dx * (-ay) + dy * ax;
                        double u = Clamp01((along - t0) / (t1 - t0));
                        double prof = 0.30 + 0.70 * System.Math.Sin(System.Math.PI * Clamp01((u + 0.10) / 1.10));
                        double hw = 1.9 + 3.9 * prof;
                        if (System.Math.Abs(across) > hw) continue;
                        double q = across / hw;
                        double t = CylShade(q, ax, ay);
                        if (u > 0.93) t *= 0.55;                    // end cap turns away
                        bool collar = u < 0.16;
                        Color32 rgb = collar && k == 0 ? Pick(GOLD, 0.25 + 0.7 * t) : Pick(KN, 0.08 + 0.80 * t);
                        if (System.Math.Abs(across) > hw - 1) rgb = Pick(KN, 0.04);   // 1px dark edge
                        px[i] = rgb;
                        done = true;
                        break;
                    }
                    if (done) continue;

                    // --- rim section — wheelRig.js:128-137 ---
                    if (r >= rI && r <= rO)
                    {
                        double s = (r - rI) / (double)(rO - rI);
                        double nx = dx / r, ny = dy / r;
                        double face = Clamp01(0.5 + 0.5 * (nx * LX + ny * LY));
                        double sec = 1.0 - System.Math.Abs(s - 0.40) * 1.55;
                        double t = Clamp01(0.10 + 0.62 * face + 0.30 * sec);
                        if (s < 0.07 || s > 0.93) t = 0.04;         // keyline edges
                        double aDeg = System.Math.Atan2(dx, -dy) * 180.0 / System.Math.PI;
                        px[i] = System.Math.Abs(AngDiff(aDeg, kingA)) < 7.5
                              ? Pick(GOLD, 0.15 + 0.85 * t)
                              : Pick(RIM, t);
                        continue;
                    }

                    // --- spokes (under the rim) — wheelRig.js:140-151 ---
                    for (int k = 0; k < SPOKES; k++)
                    {
                        double ax = _spokeAx[k], ay = _spokeAy[k];
                        double along = dx * ax + dy * ay;
                        if (along < HUBR - 2 || along > rI + 2) continue;
                        double across = dx * (-ay) + dy * ax;
                        double u = Clamp01((along - HUBR) / (double)(rI - HUBR));
                        double hw = 2.9 - 0.7 * u;                  // tapers outboard
                        if (System.Math.Abs(across) > hw) continue;
                        double q = across / hw;
                        double t = CylShade(q, ax, ay);
                        px[i] = System.Math.Abs(across) > hw - 0.9 ? Pick(SP, 0.05) : Pick(SP, 0.10 + 0.85 * t);
                        break;
                    }
                }
            }
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
