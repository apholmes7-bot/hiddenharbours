using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the diegetic compass — the immutable rig source is
    /// <c>docs/art/rigs/ui/compass/Art/compassRig.js</c> (ADR 0025, Option A). Two real units off one
    /// shared procedural card: the black bracket <b>dome</b> that sits on the crown (the skiffs'
    /// unit) and the white flush Ritchie that sinks into a dash (Novi/Cape). The card is baked ONCE
    /// per face (compassRig.js:105-139) and blitted rotated by <c>-heading</c>, nearest-neighbour,
    /// so the card holds north while the case and its red lubber line turn with the hull.
    ///
    /// <para><b>Heading convention</b> (compassRig.js:3-4): 0 = N, degrees CLOCKWISE — the SAME
    /// canon as <see cref="BoatKinematics.BearingDegrees"/>, read off the PHYSICS ROOT's bow
    /// (never the counter-rotating visual child). Pinned by <c>CompassHeadingGoldenTests</c>.</para>
    ///
    /// <para><b>Raster notes.</b> The canvas 'lighter' passes (glass sheen, night flood) are ported
    /// as coverage-deduplicated ADDITIVE fills — stroke stamps mark a scratch coverage mask first so
    /// overlapping samples add exactly once, which is what one canvas stroke does. Two knowingly
    /// approximate spots, both sub-pixel polish under the rigs' own no-AA rule: the squashed arc
    /// stroke's anisotropic width, and transform-order rounding on the glint. Geometry (the dome
    /// layout chain, the card mark angles) is exact and golden-pinned.</para>
    ///
    /// <para><b>Perf (rule 7):</b> the two face cards bake once per session; a repaint of a dome
    /// cell touches only its cell surface; callers repaint on quantised-heading change only.</para>
    /// </summary>
    public static class CompassRigRender
    {
        private const double DEG = System.Math.PI / 180.0;

        /// <summary>Baked card radius (compassRig.js:142).</summary>
        public const int CARD_R = 96;

        /// <summary>The dome's orthographic tilt (compassRig.js:208 <c>fore=0.72</c>).</summary>
        public const double DomeFore = 0.72;

        // ---- palettes (compassRig.js:23-28) -------------------------------------------------------
        private static readonly Color32[] BLK = RigDrawUtil.Ramp("05080a", "0b1013", "141b20", "212b31", "33414a", "4a5a64");
        private static readonly Color32[] CHR = RigDrawUtil.Ramp("1e242a", "3a454d", "647079", "9fb0b6", "cdd9db", "eff6f6");
        private static readonly Color32[] WHT = RigDrawUtil.Ramp("8f9a99", "aab4b2", "c6cfcb", "dde4de", "eef2ec", "fbfdfa");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("2a0b08", "6e1f16", "b3372a", "e0554a", "ff6f5c", "ffb2a6");
        private static readonly Color32 SHADOW = RigDrawUtil.Hex("04070a");

        // Face colour sets (compassRig.js:27-28).
        private readonly struct Face
        {
            public readonly Color32 FaceCol, Ring, Ink, Dim, N, Rose;
            public Face(string face, string ring, string ink, string dim, string n, string rose)
            {
                FaceCol = RigDrawUtil.Hex(face); Ring = RigDrawUtil.Hex(ring); Ink = RigDrawUtil.Hex(ink);
                Dim = RigDrawUtil.Hex(dim); N = RigDrawUtil.Hex(n); Rose = RigDrawUtil.Hex(rose);
            }
        }
        private static readonly Face DARKF = new Face("0a0f14", "04070a", "e9f0f2", "7d949e", "e0554a", "cfe0e6");
        private static readonly Face LITEF = new Face("e7ece3", "c2cabf", "1a2226", "5c6b6a", "c23a2b", "2a343a");

        // ---- heading helpers (compassRig.js:94-98; canon 0=N clockwise) ---------------------------
        public static readonly string[] C8 = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public static double Norm(double h) => ((h % 360.0) + 360.0) % 360.0;

        /// <summary>Nearest 8-point index — <c>Math.round(norm(h)/45) % 8</c> (compassRig.js:97).</summary>
        public static int Cardinal8Index(double h) => DrawSurface.JsRound(Norm(h) / 45.0) % 8;

        public static string Cardinal8(double h) => C8[Cardinal8Index(h)];

        /// <summary>3-digit bearing string (compassRig.js:98) — faithfully keeps the rig's own
        /// quirk that 359.6 reads "360" (rounded before wrapping).</summary>
        public static string FmtDeg(double h)
        {
            int d = DrawSurface.JsRound(Norm(h));
            return (d < 10 ? "00" : d < 100 ? "0" : "") + d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---- dome layout (compassRig.js:207-221), pure + parameterised for the goldens ------------
        public struct DomeLayout
        {
            public int Cx, Gr, Ry, BaseH, CapB, UnitH, Top, Gy, GBot, BaseTop, FootY, Rt, Rb, CapT, ArmR;
        }

        public static DomeLayout ComputeDomeLayout(double x, double y, double w, double h)
            => ComputeDomeLayoutWith(x, y, w, h, DomeFore, 0.42, 0.40);

        /// <summary>Parameterised core so the golden tests can negative-control the chain.</summary>
        public static DomeLayout ComputeDomeLayoutWith(double x, double y, double w, double h,
                                                       double fore, double wFrac, double hFrac)
        {
            var L = new DomeLayout();
            L.Cx = DrawSurface.JsRound(x + w / 2.0);
            L.Gr = (int)System.Math.Floor(System.Math.Min(w * wFrac, h * hFrac));
            L.Ry = DrawSurface.JsRound(L.Gr * fore);
            L.BaseH = DrawSurface.JsRound(L.Ry * 1.4);
            L.CapB = DrawSurface.JsRound(L.Gr * 0.5 * fore);
            L.UnitH = 10 + 2 * L.Ry + L.BaseH + L.CapB;
            L.Top = DrawSurface.JsRound(y + System.Math.Max(0.0, (h - L.UnitH) / 2.0));
            L.Gy = L.Top + 10 + L.Ry;
            L.GBot = L.Gy + L.Ry;
            L.BaseTop = L.GBot - 4;
            L.FootY = L.BaseTop + L.BaseH;
            L.Rt = DrawSurface.JsRound(L.Gr * 0.58);
            L.Rb = DrawSurface.JsRound(L.Gr * 0.98);
            L.CapT = DrawSurface.JsRound(L.Rt * fore);
            L.ArmR = L.Gr + 4;
            return L;
        }

        /// <summary>
        /// Where a card mark authored at card-direction <paramref name="dDeg"/> lands on screen
        /// (offset from the glass centre, y-down px) after the card rotates by −heading — the pure
        /// mapping the goldens pin: the mark's screen angle is (d − heading), so the current heading
        /// always reads at the top lubber (compassRig.js:100-104).
        /// </summary>
        public static void MarkScreenOffset(double dDeg, double headingDeg, double radius,
                                            out double sx, out double sy)
        {
            // Card-local (N up): dir(d) = (sin, -cos), scaled to the mark radius…
            double lx = System.Math.Sin(dDeg * DEG) * radius;
            double ly = -System.Math.Cos(dDeg * DEG) * radius;
            // …then the canvas rotate(-heading) — y-down clockwise-positive rotation matrix.
            double a = -headingDeg * DEG;
            double ca = System.Math.Cos(a), sa = System.Math.Sin(a);
            sx = lx * ca - ly * sa;
            sy = lx * sa + ly * ca;
        }

        // ---- the rotating card, baked once per face (compassRig.js:105-139) -----------------------
        private static DrawSurface _cardDark, _cardLite;

        private static DrawSurface Card(bool lite)
        {
            if (lite && _cardLite != null) return _cardLite;
            if (!lite && _cardDark != null) return _cardDark;
            DrawSurface c = BuildCard(CARD_R, lite ? LITEF : DARKF);
            if (lite) _cardLite = c; else _cardDark = c;
            return c;
        }

        private static readonly double[] _polyX = new double[4];
        private static readonly double[] _polyY = new double[4];

        private static DrawSurface BuildCard(int r, Face fc)
        {
            int S = (r + 2) * 2;
            var g = new DrawSurface(S, S);
            int cx = S / 2, cy = S / 2;
            RigDrawUtil.Circle(g, cx, cy, r, fc.Ring);
            RigDrawUtil.Circle(g, cx, cy, r - 1, fc.FaceCol);
            // degree graduations (compassRig.js:110-115)
            for (int d = 0; d < 360; d += 5)
            {
                RigDrawUtil.Dir(d * DEG, out double ox, out double oy);
                bool major = d % 30 == 0, mid = d % 15 == 0;
                double rO = r - 3, rI = major ? r - 11 : mid ? r - 8 : r - 6;
                RigDrawUtil.ThickLine(g, cx + ox * rI, cy + oy * rI, cx + ox * rO, cy + oy * rO,
                                      major ? 2 : 1, major ? fc.Ink : fc.Dim);
            }
            // "by tens" numbers between the letters (compassRig.js:117-119)
            int[] tensD = { 30, 60, 120, 150, 210, 240, 300, 330 };
            string[] tensL = { "3", "6", "12", "15", "21", "24", "30", "33" };
            for (int i = 0; i < tensD.Length; i++)
            {
                RigDrawUtil.Dir(tensD[i] * DEG, out double ox, out double oy);
                double rr = r - 20;
                RigDrawUtil.TextCC(g, tensL[i], cx + ox * rr, cy + oy * rr, 2, fc.Ink);
            }
            // intercardinal letters (compassRig.js:121-123)
            int[] icD = { 45, 135, 225, 315 };
            string[] icL = { "NE", "SE", "SW", "NW" };
            for (int i = 0; i < icD.Length; i++)
            {
                RigDrawUtil.Dir(icD[i] * DEG, out double ox, out double oy);
                double rr = r - 19;
                RigDrawUtil.TextCC(g, icL[i], cx + ox * rr, cy + oy * rr, 2, fc.Dim);
            }
            // cardinal letters, N red (compassRig.js:125-127)
            for (int i = 0; i < 4; i++)
            {
                int d = i * 90;
                RigDrawUtil.Dir(d * DEG, out double ox, out double oy);
                double rr = r - 21;
                RigDrawUtil.TextCC(g, C8[i * 2], cx + ox * rr, cy + oy * rr, 3, i == 0 ? fc.N : fc.Ink);
            }
            // compass rose — long N/S needle, short E/W, N half red (compassRig.js:129-137)
            double rrr = r - 30;
            _polyX[0] = cx - rrr * 0.5; _polyY[0] = cy;            // W tip
            _polyX[1] = cx; _polyY[1] = cy - 4;
            _polyX[2] = cx + rrr * 0.5; _polyY[2] = cy;            // E tip
            _polyX[3] = cx; _polyY[3] = cy + 4;
            RigDrawUtil.FillPoly(g, _polyX, _polyY, 4, fc.Rose);
            _polyX[0] = cx; _polyY[0] = cy + rrr;                  // S half (neutral)
            _polyX[1] = cx - 5; _polyY[1] = cy;
            _polyX[2] = cx + 5; _polyY[2] = cy;
            RigDrawUtil.FillPoly(g, _polyX, _polyY, 3, fc.Rose);
            _polyX[0] = cx; _polyY[0] = cy - rrr;                  // N half (red)
            RigDrawUtil.FillPoly(g, _polyX, _polyY, 3, fc.N);
            RigDrawUtil.Circle(g, cx, cy, 3, fc.Ink);
            RigDrawUtil.Circle(g, cx, cy, 2, fc.FaceCol);
            return g;
        }

        // ---- additive helpers ('lighter') with coverage dedup --------------------------------------
        private static byte[] _cover;

        private static void EnsureCover(DrawSurface s)
        {
            if (_cover == null || _cover.Length < s.Pixels.Length)
                _cover = new byte[s.Pixels.Length];
            System.Array.Clear(_cover, 0, s.Pixels.Length);
        }

        private static void AddPx(DrawSurface s, int x, int y, double rr, double gg, double bb)
        {
            if (x < 0 || x >= s.Width || y < 0 || y >= s.Height) return;
            int i = y * s.Width + x;
            Color32 d = s.Pixels[i];
            if (d.a == 0) return;   // 'lighter' over nothing stays invisible once composited opaque-under
            int r = d.r + (int)(rr + 0.5); if (r > 255) r = 255;
            int g = d.g + (int)(gg + 0.5); if (g > 255) g = 255;
            int b = d.b + (int)(bb + 0.5); if (b > 255) b = 255;
            s.Pixels[i] = new Color32((byte)r, (byte)g, (byte)b, d.a);
        }

        // ---- the live card in its bezel (compassRig.js:147-182) -----------------------------------
        private static void LiveCard(DrawSurface s, int cx, int cy, int r, double heading, bool night,
                                     bool liteFace, double fore = 1.0)
        {
            DrawSurface card = Card(liteFace);
            double sc = (double)r / CARD_R;
            double h = heading * DEG;
            double ch = System.Math.Cos(h), sh = System.Math.Sin(h);
            int ryPx = (int)System.Math.Ceiling(r * fore) + 1;

            // Rotate-under-clip: for each device pixel inside the (squashed) glass circle, un-squash,
            // inverse-rotate by +heading, nearest-sample the baked card (canvas rotate(-h) + clip arc).
            for (int y = cy - ryPx; y <= cy + ryPx; y++)
            {
                if (y < 0 || y >= s.Height) continue;
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    if (x < 0 || x >= s.Width) continue;
                    double ux = x + 0.5 - cx;
                    double uy = (y + 0.5 - cy) / fore;
                    if (ux * ux + uy * uy > (double)r * r) continue;
                    // c = M(+h)·d / sc + pivot (inverse of rotate(-h)·scale(sc))
                    int sx = (int)System.Math.Floor((ch * ux - sh * uy) / sc + card.Width / 2.0);
                    int sy = (int)System.Math.Floor((sh * ux + ch * uy) / sc + card.Height / 2.0);
                    if (sx < 0 || sx >= card.Width || sy < 0 || sy >= card.Height) continue;
                    Color32 c = card.Pixels[sy * card.Width + sx];
                    if (c.a == 0) continue;
                    s.Pixels[y * s.Width + x] = c;
                }
            }

            if (night)
            {
                // red bulb flood, radial 'lighter' (compassRig.js:160-165), in un-squashed card space
                for (int y = cy - ryPx; y <= cy + ryPx; y++)
                {
                    if (y < 0 || y >= s.Height) continue;
                    for (int x = cx - r - 1; x <= cx + r + 1; x++)
                    {
                        if (x < 0 || x >= s.Width) continue;
                        double ux = x + 0.5 - cx;
                        double uy = (y + 0.5 - cy) / fore;
                        if (ux * ux + uy * uy > (double)r * r) continue;
                        double dist = System.Math.Sqrt(ux * ux + (uy - r * 0.15) * (uy - r * 0.15));
                        double t = (dist - 2.0) / (r * 1.05 - 2.0);
                        if (t < 0) t = 0; else if (t > 1) t = 1;
                        double a;
                        double cr, cg, cb;
                        if (t < 0.55)
                        {
                            double u = t / 0.55;
                            a = 0.55 + (0.30 - 0.55) * u;
                            cr = 255 + (224 - 255) * u; cg = 80 + (64 - 80) * u; cb = 60 + (44 - 60) * u;
                        }
                        else
                        {
                            double u = (t - 0.55) / 0.45;
                            a = 0.30 + (0.06 - 0.30) * u;
                            cr = 224 + (150 - 224) * u; cg = 64 + (26 - 64) * u; cb = 44 + (18 - 44) * u;
                        }
                        AddPx(s, x, y, cr * a, cg * a, cb * a);
                    }
                }
            }

            // ---- domed glass (compassRig.js:167-176): sheen gradient + two rim arcs + hard glint ----
            for (int y = cy - ryPx; y <= cy + ryPx; y++)
            {
                if (y < 0 || y >= s.Height) continue;
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    if (x < 0 || x >= s.Width) continue;
                    double ux = x + 0.5 - cx;
                    double uy = (y + 0.5 - cy) / fore;
                    if (ux * ux + uy * uy > (double)r * r) continue;
                    double gx = ux + r * 0.34, gy = uy + r * 0.44;
                    double t = System.Math.Sqrt(gx * gx + gy * gy) / r;
                    if (t >= 1.0) continue;
                    double a = t < 0.5 ? 0.20 + (0.05 - 0.20) * (t / 0.5) : 0.05 * (1.0 - (t - 0.5) / 0.5);
                    AddPx(s, x, y, 255 * a, 255 * a, 255 * a);
                }
            }
            ArcAdd(s, cx, cy, r * 0.9, fore, System.Math.PI * 0.86, System.Math.PI * 1.42,
                   System.Math.Max(1.5, r * 0.055), 255, 255, 255, 0.26);
            if (night) ArcAdd(s, cx, cy, r * 0.9, fore, System.Math.PI * -0.12, System.Math.PI * 0.30,
                              System.Math.Max(1.0, r * 0.04), 255, 150, 120, 0.16);
            else ArcAdd(s, cx, cy, r * 0.9, fore, System.Math.PI * -0.12, System.Math.PI * 0.30,
                        System.Math.Max(1.0, r * 0.04), 170, 205, 225, 0.12);
            // hard glint (squash-approximated centre; sub-pixel polish)
            RigDrawUtil.Ellipse(s, DrawSurface.JsRound(cx - r * 0.38),
                                DrawSurface.JsRound(cy - r * 0.42 * fore),
                                System.Math.Max(1, DrawSurface.JsRound(r * 0.08)),
                                System.Math.Max(1, DrawSurface.JsRound(r * 0.05 * fore)),
                                new Color32(255, 255, 255, 255), 0.5f);

            // fixed lubber line at 12 o'clock, drawn OUTSIDE the squash (compassRig.js:179-181)
            double ry = r * fore;
            Color32 lub = night ? RED[4] : RED[3];
            if (night)
                RigDrawUtil.ThickLine(s, cx, cy - ry + 2, cx, cy - ry * 0.34, 3, RED[1]);
            else
                RigDrawUtil.ThickLine(s, cx, cy - ry + 2, cx, cy - ry * 0.34, 3,
                                      new Color32(120, 20, 14, 255), 0.6f);
            RigDrawUtil.ThickLine(s, cx, cy - ry + 2, cx, cy - ry * 0.34, 1, lub);
        }

        /// <summary>Additive elliptical arc stroke with coverage dedup (one canvas stroke adds once).</summary>
        private static void ArcAdd(DrawSurface s, int cx, int cy, double rad, double fore,
                                   double a0, double a1, double w, double cr, double cg, double cb, double alpha)
        {
            EnsureCover(s);
            int n = (int)System.Math.Ceiling(rad * (a1 - a0) / 0.5);
            if (n < 2) n = 2;
            double hw = w / 2.0;
            int ihw = (int)System.Math.Ceiling(hw);
            for (int i = 0; i <= n; i++)
            {
                double t = a0 + (a1 - a0) * i / n;
                double pxd = cx + rad * System.Math.Cos(t);
                double pyd = cy + rad * System.Math.Sin(t) * fore;
                int px0 = (int)System.Math.Floor(pxd), py0 = (int)System.Math.Floor(pyd);
                for (int dy = -ihw; dy <= ihw; dy++)
                {
                    for (int dx = -ihw; dx <= ihw; dx++)
                    {
                        if (dx * dx + dy * dy > hw * hw) continue;
                        int x = px0 + dx, y = py0 + dy;
                        if (x < 0 || x >= s.Width || y < 0 || y >= s.Height) continue;
                        int ci = y * s.Width + x;
                        if (_cover[ci] != 0) continue;
                        _cover[ci] = 1;
                        AddPx(s, x, y, cr * alpha, cg * alpha, cb * alpha);
                    }
                }
            }
        }

        // ---- FLUSH unit (compassRig.js:186-203) ----------------------------------------------------
        public static void PaintFlush(DrawSurface s, int x, int y, int w, int h, double heading, bool night)
        {
            heading = Norm(heading);
            int cx = DrawSurface.JsRound(x + w / 2.0), cy = DrawSurface.JsRound(y + h / 2.0);
            int R = System.Math.Min(w, h) / 2 - 2;
            RigDrawUtil.Circle(s, cx, cy, R + 3, SHADOW);                          // cutout shadow
            RigDrawUtil.Ring(s, cx, cy, R + 2, R - 6, WHT[2]);                     // gelcoat bezel
            RigDrawUtil.Ring(s, cx, cy, R + 2, R - 6, WHT[4],
                             cx - R - 2, cy - R - 2, (R + 2) * 2, R + 2);          // lit top half
            RigDrawUtil.Ring(s, cx, cy, R + 2, R + 1, WHT[0]);                     // outer lip
            RigDrawUtil.Ring(s, cx, cy, R - 6, R - 7, BLK[1]);                     // inner black lip
            int[] screwD = { 45, 135, 225, 315 };
            foreach (int d in screwD)
            {
                RigDrawUtil.Dir(d * DEG, out double ox, out double oy);
                RigDrawUtil.Screw(s, DrawSurface.JsRound(cx + ox * (R - 2)),
                                  DrawSurface.JsRound(cy + oy * (R - 2)), 2, CHR);
            }
            RigDrawUtil.ThickLine(s, cx, cy - R - 1, cx, cy - R + 4, 3, night ? RED[4] : RED[3]);
            LiveCard(s, cx, cy, R - 7, heading, night, liteFace: true);
        }

        // ---- DOME unit (compassRig.js:207-264) -----------------------------------------------------
        public static void PaintDome(DrawSurface s, int x, int y, int w, int h, double heading, bool night)
        {
            heading = Norm(heading);
            DomeLayout L = ComputeDomeLayout(x, y, w, h);
            int cx = L.Cx, gy = L.Gy, gr = L.Gr, ry = L.Ry;
            int baseTop = L.BaseTop, bodyH = L.BaseH, footY = L.FootY;
            int rt = L.Rt, rb = L.Rb, capB = L.CapB, capT = L.CapT, armR = L.ArmR;

            // ---- binnacle base: a truncated cone at the same forward slope ----
            RigDrawUtil.Ellipse(s, cx, footY + 3, rb + 3, capB + 3, SHADOW, 0.5f);          // deck shadow
            RigDrawUtil.Ellipse(s, cx, footY, rb, capB, BLK[0]);                            // foot disc
            RigDrawUtil.Ellipse(s, cx, footY - 1, rb - 2, System.Math.Max(1, capB - 1), BLK[1]);
            for (int i = 0; i < bodyH; i++)
            {
                double t = (double)i / bodyH;
                double rr = rt + (rb - rt) * (t * t * 0.40 + t * 0.60);                     // cone radius per row
                int cxl = DrawSurface.JsRound(cx - rr), ww = DrawSurface.JsRound(rr * 2);
                int lw = System.Math.Max(2, DrawSurface.JsRound(ww * 0.16));
                int rw = System.Math.Max(2, DrawSurface.JsRound(ww * 0.12));
                s.FillRect(cxl, baseTop + i, ww, 1, BLK[2]);
                s.FillRect(cxl, baseTop + i, lw, 1, BLK[3]);                                // left key light
                s.FillRect(cxl + ww - rw, baseTop + i, rw, 1, BLK[0]);                      // right shade
            }
            RigDrawUtil.Ellipse(s, cx, baseTop, rt, capT, BLK[3]);                          // tilted top surface
            RigDrawUtil.Ellipse(s, cx, baseTop - 1, System.Math.Max(1, rt - 2), System.Math.Max(1, capT - 1), BLK[4]);
            int plaqueY = baseTop + DrawSurface.JsRound(bodyH * 0.55);
            RigDrawUtil.RRect(s, cx - 18, plaqueY - 4, 36, 9, 2, BLK[1]);                   // brand plaque
            s.FillRect(cx - 12, plaqueY, 24, 1, BLK[4]);
            RigDrawUtil.Screw(s, cx - DrawSurface.JsRound(rb * 0.66), footY - DrawSurface.JsRound(capB * 0.1), 2, CHR);
            RigDrawUtil.Screw(s, cx + DrawSurface.JsRound(rb * 0.66), footY - DrawSurface.JsRound(capB * 0.1), 2, CHR);

            // ---- gimbal yoke arms ----
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                RigDrawUtil.ThickLine(s, cx + sgn * (rt - 3), baseTop + 2, cx + sgn * armR, gy, 7, BLK[2]);
                RigDrawUtil.ThickLine(s, cx + sgn * (rt - 3), baseTop + 2, cx + sgn * armR, gy, 2, BLK[4]);
            }

            // ---- tilted globe housing ----
            RigDrawUtil.Ellipse(s, cx, gy + 2, gr + 4, ry + 4, SHADOW);                     // seat shadow
            RigDrawUtil.Ellipse(s, cx, gy, gr + 3, ry + 3, BLK[3]);                         // black rim
            RigDrawUtil.Ellipse(s, cx, gy, gr + 2, ry + 2, CHR[2]);                         // stainless ring
            EllipseClipTop(s, cx, gy, gr + 2, ry + 2, CHR[5], gy - ry - 3, ry + 3);         // lit upper half
            RigDrawUtil.Ellipse(s, cx, gy, gr, ry, SHADOW);                                 // inner lip
            RigDrawUtil.ThickLine(s, cx, gy - ry - 3, cx, gy - ry + 3, 3, night ? RED[4] : RED[3]);   // north index
            LiveCard(s, cx, gy, gr - 2, heading, night, liteFace: false, fore: DomeFore);

            // ---- thumb-screw knobs on the pivots ----
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                RigDrawUtil.Circle(s, cx + sgn * (armR + 1), gy, 8, SHADOW);
                RigDrawUtil.Circle(s, cx + sgn * (armR + 1), gy, 7, CHR[1]);
                RigDrawUtil.Circle(s, cx + sgn * armR, gy - 1, 5, CHR[3]);
                for (int k = 0; k < 8; k++)
                {
                    RigDrawUtil.Dir(k * 45 * DEG, out double ox, out double oy);
                    s.FillRect(DrawSurface.JsRound(cx + sgn * (armR + 1) + ox * 7) - 1,
                               DrawSurface.JsRound(gy + oy * 7) - 1, 2, 2, CHR[0]);
                }
                RigDrawUtil.Circle(s, cx + sgn * (armR + 1), gy, 2, CHR[5]);
            }

            if (night)
            {
                // outer red bloom (compassRig.js:258-263) — additive over the already-painted body
                for (int yy = (int)(gy - gr * 1.6); yy <= (int)(gy + gr * 1.6); yy++)
                {
                    for (int xx = (int)(cx - gr * 1.6); xx <= (int)(cx + gr * 1.6); xx++)
                    {
                        if (xx < 0 || xx >= s.Width || yy < 0 || yy >= s.Height) continue;
                        double dx = xx + 0.5 - cx, dy = yy + 0.5 - gy;
                        double dist = System.Math.Sqrt(dx * dx + dy * dy);
                        double t = (dist - gr * 0.5) / (gr * 1.6 - gr * 0.5);
                        if (t < 0) t = 0; else if (t >= 1) continue;
                        double a = 0.15 * (1.0 - t);
                        AddPx(s, xx, yy, 255 * a, 70 * a, 50 * a);
                    }
                }
            }
        }

        private static void EllipseClipTop(DrawSurface s, int cx, int cy, int rx, int ry, Color32 c,
                                           int clipY, int clipH)
        {
            for (int dy = -ry; dy <= ry; dy++)
            {
                int yy = cy + dy;
                if (yy < clipY || yy >= clipY + clipH) continue;
                int dx = (int)System.Math.Floor(rx * System.Math.Sqrt(System.Math.Max(0.0, 1.0 - (double)(dy * dy) / (ry * ry))));
                s.FillRect(cx - dx, yy, 2 * dx + 1, 1, c);
            }
        }

        /// <summary>Unified entry (compassRig.js:268-270): route on the fitted mount.</summary>
        public static void PaintInto(DrawSurface s, int x, int y, int w, int h, CompassMount form,
                                     double heading, bool night)
        {
            if (form == CompassMount.Dome) PaintDome(s, x, y, w, h, heading, night);
            else if (form == CompassMount.Flush) PaintFlush(s, x, y, w, h, heading, night);
        }
    }
}
