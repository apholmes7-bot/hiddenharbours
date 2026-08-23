using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the SPORT skiff's dash CHROME — the immutable rig source is
    /// <c>docs/art/rigs/ui/sport-skiff-helm/Art/sportRig.js</c> (ADR 0025, Option A). The console
    /// skiff's polished sister: IDENTICAL geometry (sportRig.js:113 — one
    /// <see cref="HelmDashGeometry"/> serves both), re-skinned in polished stainless — chrome
    /// bezels over WHITE dials with dark needles and a sporty red counterweight, chrome switchgear
    /// and ignition barrel, a brushed stainless binnacle with a chrome compass-rose lever hub.
    /// Mirror of <c>paint()</c> (sportRig.js:299-408) minus the four composited instruments — the
    /// same composition contract as <see cref="ConsoleDashRender"/> (which carries the shared
    /// rationale; this file cites only the sport deltas).
    /// </summary>
    public static class SportDashRender
    {
        private const double DEG = System.Math.PI / 180.0;
        private const int PAD = HelmDashGeometry.TOPPAD;

        // ---- palettes (sportRig.js:15-33) ---------------------------------------------------------
        private static readonly Color32[] GRAPH = RigDrawUtil.Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] STEEL = RigDrawUtil.Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32[] RUBBER = RigDrawUtil.Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47");
        private static readonly Color32[] CHROME = RigDrawUtil.Ramp("1e242a", "3a454d", "647079", "9fb0b6", "cdd9db", "eff6f6");
        private static readonly Color32[] PAINT = RigDrawUtil.Ramp("828f90", "9aa8a8", "b7c2bf", "d3dbd4", "e7ece5", "f5f7f1");
        private static readonly Color32[] TRIM = RigDrawUtil.Ramp("0d3f3c", "14554e", "1c7367", "2ba39a", "49b8aa");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("7fd6c9");
        private static readonly Color32 GFACE = RigDrawUtil.Hex("edefe8");
        private static readonly Color32 GFACE2 = RigDrawUtil.Hex("dbe0d8");
        private static readonly Color32 GTICK = RigDrawUtil.Hex("1b2427");
        private static readonly Color32 GTICKD = RigDrawUtil.Hex("5f7073");
        private static readonly Color32 GLABEL = RigDrawUtil.Hex("1c7367");
        private static readonly Color32 NEEDLE = RigDrawUtil.Hex("141a1d");
        private static readonly Color32 NEEDLE_HL = RigDrawUtil.Hex("39454a");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("7c2a20", "b83b2e", "e0554a");
        private static readonly Color32[] GREEN = RigDrawUtil.Ramp("1c6a3b", "2f9e57", "66d585");
        private static readonly Color32 AMBER = RigDrawUtil.Hex("e6b53f");
        private static readonly Color32 LAMPOFF = RigDrawUtil.Hex("16202a");
        private static readonly Color32 SPOT_ON = RigDrawUtil.Hex("eef3ef");
        private static readonly Color32 SPOT_OFF = RigDrawUtil.Hex("20323a");
        private static readonly Color32 BINTX = RigDrawUtil.Hex("123a36");
        private static readonly Color32 BINTX_R = RigDrawUtil.Hex("7c2a20");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

        // The gauge backlight's two gradients (sportRig.js:188-194) — static so the wash allocates
        // nothing. Warmer and far heavier at the rim than the console's: it has to carry an amber
        // reading over WHITE enamel, where the console's only has to light black glass.
        private static readonly double[] NightCoreT = { 0.0, 0.65, 1.0 };
        private static readonly Color32[] NightCoreC =
            { RigDrawUtil.Hex("ffb648"), RigDrawUtil.Hex("f09e36"), RigDrawUtil.Hex("d6842e") };
        private static readonly float[] NightCoreA = { 0.52f, 0.40f, 0.30f };
        private static readonly double[] NightBloomT = { 0.0, 1.0 };
        private static readonly Color32[] NightBloomC =
            { RigDrawUtil.Hex("ffb84a"), RigDrawUtil.Hex("ffb84a") };
        private static readonly float[] NightBloomA = { 0.20f, 0f };

        // ---- the baked chrome-shaft ignition key (sportRig.js:263-275), built once ----------------
        private static DrawSurface _key;
        private static int _keyPx, _keyPy;

        private static void EnsureKey()
        {
            if (_key != null) return;
            const int bowW = 15, bowH = 20, shaft = 11, pad = 6;
            int w = bowW + pad * 2, h = bowH + shaft + pad * 2;
            _key = new DrawSurface(w, h);
            _keyPx = DrawSurface.JsRound(w / 2.0);
            _keyPy = h - 4;
            RigDrawUtil.ThickLine(_key, _keyPx, _keyPy, _keyPx, _keyPy - shaft, 4, CHROME[2]);
            RigDrawUtil.ThickLine(_key, _keyPx - 1, _keyPy, _keyPx - 1, _keyPy - shaft, 1, CHROME[5]);
            int by = _keyPy - shaft - bowH;
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0), by, bowW, bowH, 6, RUBBER[1]);
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0) + 1, by + 1, bowW - 2,
                              DrawSurface.JsRound(bowH * 0.5), 5, RUBBER[3]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 4, RUBBER[0]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 3, CHROME[2]);
        }

        /// <summary>Paint the sport dash chrome (minus the composited instruments) into
        /// <paramref name="s"/> (<see cref="HelmDashGeometry.W"/>×<see cref="HelmDashGeometry.H"/>).
        ///
        /// <para><paramref name="night"/> lights the dials and NOTHING else — the whole of what
        /// sportRig.js authors for night on the chrome (js:360). Her sister drops the entire panel a
        /// palette step (consoleRig.js:58); this hull's gelcoat and stainless are given no night ramp
        /// at all, so none is invented here. What sells her night face is the composited instruments,
        /// which take their own.</para></summary>
        public static void Render(DrawSurface s, bool running, float drive, float rpm01, float fuel01,
                                  bool night = false, bool blink = false,
                                  bool anchorFitted = false, bool anchorDown = false)
        {
            EnsureKey();
            bool lowFuel = fuel01 < 0.13f;
            s.Clear();

            // ---- windscreen + spotlight can (sportRig.js:315-323) ----
            RigDrawUtil.RRect(s, 175, 26 + PAD, 250, 22, 5, RigDrawUtil.Hex("3a7680"), 0.30f);
            RigDrawUtil.RRect(s, 175, 26 + PAD, 250, 3, 2, RigDrawUtil.Hex("8fc9c4"), 0.35f);
            RigDrawUtil.ThickLine(s, 175, 27 + PAD, 425, 27 + PAD, 2, CHROME[3]);
            RigDrawUtil.ThickLine(s, 175, 47 + PAD, 425, 47 + PAD, 2, CHROME[1]);
            RigDrawUtil.RRect(s, HelmDashGeometry.SpotcanX, HelmDashGeometry.SpotcanY + PAD,
                              HelmDashGeometry.SpotcanW, HelmDashGeometry.SpotcanH, 5, CHROME[2]);
            RigDrawUtil.RRect(s, HelmDashGeometry.SpotcanX, HelmDashGeometry.SpotcanY + PAD,
                              HelmDashGeometry.SpotcanW, 3, 2, CHROME[5]);
            int scx = HelmDashGeometry.SpotcanX + HelmDashGeometry.SpotcanW - 4;
            int scy = HelmDashGeometry.SpotcanY + PAD + HelmDashGeometry.SpotcanH / 2;
            RigDrawUtil.Circle(s, scx, scy, 6, CHROME[0]);
            RigDrawUtil.Circle(s, scx, scy, 5, SPOT_OFF);

            // ---- console body: white gelcoat, teal cove, stainless pin (sportRig.js:326-335) ----
            int cx0 = HelmDashGeometry.ConsoleX, cy0 = HelmDashGeometry.ConsoleY + PAD;
            int cw = HelmDashGeometry.ConsoleW, chh = HelmDashGeometry.ConsoleH, cr = HelmDashGeometry.ConsoleR;
            RigDrawUtil.RRect(s, cx0, cy0, cw, chh, cr, PAINT[0]);
            RigDrawUtil.RRect(s, cx0 + 1, cy0 + 1, cw - 2, chh - 4, cr, PAINT[1]);
            RigDrawUtil.RRect(s, cx0 + 3, cy0 + 3, cw - 6, 10, cr - 4, PAINT[3]);
            RigDrawUtil.RRect(s, cx0 + 3, cy0 + 3, cw - 6, 4, cr - 4, PAINT[4]);
            RigDrawUtil.RRect(s, cx0 + 3, cy0 + chh - 9, cw - 6, 6, 4, PAINT[0]);
            RigDrawUtil.RRect(s, cx0 + 8, cy0 + 56, cw - 16, 5, 2, TRIM[3]);
            s.FillRect(cx0 + 8, cy0 + 62, cw - 16, 1, CHROME[5]);
            s.FillRect(cx0 + 8, cy0 + 63, cw - 16, 1, CHROME[2]);
            RigDrawUtil.Screw(s, cx0 + 11, cy0 + 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + cw - 11, cy0 + 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + 11, cy0 + chh - 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + cw - 11, cy0 + chh - 11, 2, STEEL);

            // (brow sounder cutout: S2's DepthRig mount — nothing drawn, never a fake)

            // ---- gauges: WHITE dials under chrome bezels (sportRig.js:357-359) ----
            GaugeRpm(s, HelmDashGeometry.RpmCx, HelmDashGeometry.RpmCy + PAD, HelmDashGeometry.GaugeR, rpm01);
            GaugeFuel(s, HelmDashGeometry.FuelCx, HelmDashGeometry.FuelCy + PAD, HelmDashGeometry.GaugeR,
                      fuel01, lowFuel, blink);
            if (night)   // js:360 — over the finished dials, never under them
            {
                GaugeNight(s, HelmDashGeometry.RpmCx, HelmDashGeometry.RpmCy + PAD, HelmDashGeometry.GaugeR);
                GaugeNight(s, HelmDashGeometry.FuelCx, HelmDashGeometry.FuelCy + PAD, HelmDashGeometry.GaugeR);
            }

            // ---- switch panel: dark so the chrome bats pop (sportRig.js:363-371) ----
            int swx = HelmDashGeometry.SwX, swy = HelmDashGeometry.SwY + PAD;
            int sww = HelmDashGeometry.SwW, swh = HelmDashGeometry.SwH, swr = HelmDashGeometry.SwR;
            RigDrawUtil.RRect(s, swx - 2, swy - 2, sww + 4, swh + 4, swr + 1, RUBBER[0]);
            RigDrawUtil.RRect(s, swx, swy, sww, swh, swr, GRAPH[1]);
            RigDrawUtil.RRect(s, swx + 2, swy + 2, sww - 4, 6, swr - 3, GRAPH[3]);
            RigDrawUtil.Screw(s, swx + 8, swy + 8, 2, GRAPH);
            RigDrawUtil.Screw(s, swx + sww - 8, swy + 8, 2, GRAPH);
            DrawIgnition(s, HelmDashGeometry.StartCx, HelmDashGeometry.StartCy + PAD, HelmDashGeometry.StartR, running);
            Toggle(s, HelmDashGeometry.DeckX, HelmDashGeometry.DeckY + PAD, HelmDashGeometry.DeckW,
                   HelmDashGeometry.DeckH, HelmDashGeometry.DeckCx, HelmDashGeometry.DeckLampY + PAD, false, TEAL);
            Toggle(s, HelmDashGeometry.SpotX, HelmDashGeometry.SpotY + PAD, HelmDashGeometry.SpotW,
                   HelmDashGeometry.SpotH, HelmDashGeometry.SpotCx, HelmDashGeometry.SpotLampY + PAD, false, SPOT_ON);
            RigDrawUtil.TextC(s, "DECK", HelmDashGeometry.DeckCx, HelmDashGeometry.DeckLampY + PAD + 7, 1, GTICKD);
            RigDrawUtil.TextC(s, "SPOT", HelmDashGeometry.SpotCx, HelmDashGeometry.SpotLampY + PAD + 7, 1, GTICKD);
            // The GROUND TACKLE's bat, midway between the two working lights — the console dash's own
            // addition, in this hull's chrome switchgear. Drawn ONLY where there is a hook to work: a
            // switch the boat cannot answer is the diegetic version of a readout you have not earned,
            // and the same rule refuses it (ADR 0039).
            if (anchorFitted)
            {
                Toggle(s, HelmDashGeometry.AnchX, HelmDashGeometry.AnchY + PAD, HelmDashGeometry.AnchW,
                       HelmDashGeometry.AnchH, HelmDashGeometry.AnchCx, HelmDashGeometry.AnchLampY + PAD,
                       anchorDown, AMBER);
                RigDrawUtil.TextC(s, "ANCH", HelmDashGeometry.AnchCx,
                                  HelmDashGeometry.AnchLampY + PAD + 7, 1, anchorDown ? AMBER : GTICKD);
            }

            // ---- binnacle casing: brushed stainless (sportRig.js:374-386) ----
            int bx = HelmDashGeometry.BinnX, by = HelmDashGeometry.BinnY + PAD;
            int bw = HelmDashGeometry.BinnW, bh = HelmDashGeometry.BinnH, br = HelmDashGeometry.BinnR;
            RigDrawUtil.RRect(s, bx - 2, by - 2, bw + 4, bh + 4, br + 1, CHROME[0]);
            RigDrawUtil.RRect(s, bx, by, bw, bh, br, CHROME[2]);
            RigDrawUtil.RRect(s, bx + 2, by + 2, bw - 4, 8, br - 3, CHROME[5]);
            RigDrawUtil.RRect(s, bx + 3, by + bh - 8, bw - 6, 5, 4, CHROME[0]);
            RigDrawUtil.RRectClipped(s, bx, by, bw, bh, br, CHROME[1], bx + bw - 30, by, 34, bh);
            RigDrawUtil.Screw(s, bx + 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + 9, by + bh - 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + bh - 10, 2, CHROME);
            // F / N / R engraved on the housing (sportRig.js:383-385 — engraved, no lamps here)
            RigDrawUtil.TextC(s, "F", 570, 300 + PAD, 1, BINTX);
            RigDrawUtil.TextC(s, "N", 570, 318 + PAD, 1, BINTX);
            RigDrawUtil.TextC(s, "R", 570, 336 + PAD, 1, BINTX_R);
            RigDrawUtil.RRect(s, bx + 8, by + bh - 30, 15, 12, 3, GRAPH[0]);
            RigDrawUtil.RRect(s, bx + 10, by + bh - 28, 11, 4, 2, RED[1]);

            // ---- the chrome compass-rose lever hub (sportRig.js:235-246) ----
            int dpx = HelmDashGeometry.DrivePx, dpy = HelmDashGeometry.DrivePivotY + PAD;
            RigDrawUtil.Circle(s, dpx, dpy, 22, CHROME[0]);
            RigDrawUtil.Circle(s, dpx, dpy, 20, CHROME[2]);
            RigDrawUtil.CircleClipped(s, dpx, dpy, 20, CHROME[4], dpx - 22, dpy - 22, 44, 22);
            RigDrawUtil.Circle(s, dpx - 1, dpy - 2, 16, CHROME[3]);
            RigDrawUtil.Ring(s, dpx, dpy, 22, 20, CHROME[0]);
            RigDrawUtil.Circle(s, dpx, dpy, 12, CHROME[1]);
            for (int k = 0; k < 8; k++)
            {
                RigDrawUtil.Dir(k * 45 * DEG, out double dx, out double dy);
                bool longRay = k % 2 == 0;
                double rO = longRay ? 11 : 6;
                RigDrawUtil.ThickLine(s, dpx, dpy, dpx + dx * rO, dpy + dy * rO, longRay ? 2 : 1,
                                      longRay ? CHROME[5] : CHROME[3]);
            }
            RigDrawUtil.Circle(s, dpx, dpy, 2, CHROME[0]);

            // ---- steering column + boss (sportRig.js:392-395; the wheel is WheelRigRender's) ----
            int wcx = HelmDashGeometry.WheelCx, wcy = HelmDashGeometry.WheelCy + PAD;
            RigDrawUtil.ThickLine(s, wcx, wcy, wcx, cy0 + chh - 6, 18, CHROME[1]);
            RigDrawUtil.ThickLine(s, wcx - 5, wcy + 10, wcx - 5, cy0 + chh - 6, 3, CHROME[4]);
            RigDrawUtil.ThickLine(s, wcx + 7, wcy + 10, wcx + 7, cy0 + chh - 6, 2, CHROME[0]);
            RigDrawUtil.Circle(s, wcx, wcy, 20, CHROME[0]);
        }

        // ---- gauges: chrome bezels, white dials (sportRig.js:132-182) -----------------------------
        private static void Bezel(DrawSurface s, int gx, int gy, int r)
        {
            RigDrawUtil.Circle(s, gx, gy, r + 7, CHROME[0]);
            RigDrawUtil.Ring(s, gx, gy, r + 6, r + 1, CHROME[2]);
            RigDrawUtil.Ring(s, gx, gy, r + 6, r + 1, CHROME[5], gx - r - 7, gy - r - 7, (r + 7) * 2, r + 3);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 2, CHROME[1], gx - 1, gy, r + 9, r + 9);
            RigDrawUtil.Ring(s, gx, gy, r + 6, r + 5, CHROME[0]);
            RigDrawUtil.Circle(s, gx, gy, r, GFACE);
            RigDrawUtil.Ring(s, gx, gy, r, r - 3, GFACE2);
            RigDrawUtil.Ring(s, gx, gy, r, r - 1, RigDrawUtil.Hex("c2ccc4"));
            s.BlendRect(gx - r + 5, gy - r + 6, r - 2, 2, WHITE, 0.40f);
        }

        private static void RadialTick(DrawSurface s, int gx, int gy, double deg, double rOut, double rIn,
                                       double w, Color32 col)
        {
            RigDrawUtil.Dir(deg * DEG, out double dx, out double dy);
            RigDrawUtil.ThickLine(s, gx + dx * rIn, gy + dy * rIn, gx + dx * rOut, gy + dy * rOut, w, col);
        }

        private static void Needle(DrawSurface s, int r, double px, double py, double tx, double ty)
        {
            // dark needle, sporty red counterweight, chrome hub (sportRig.js:149-155)
            double dx = tx - px, dy = ty - py;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) len = 1.0;
            double ux = dx / len, uy = dy / len, tail = System.Math.Min(15.0, len * 0.3);
            RigDrawUtil.ThickLine(s, px, py, tx, ty, 3, NEEDLE);
            RigDrawUtil.ThickLine(s, px, py, px + ux * (len - 2), py + uy * (len - 2), 1, NEEDLE_HL);
            RigDrawUtil.ThickLine(s, px, py, px - ux * tail, py - uy * tail, 3, RED[2]);
            int hr = DrawSurface.JsRound(r * 0.14) + 1;
            int ipx = DrawSurface.JsRound(px), ipy = DrawSurface.JsRound(py);
            RigDrawUtil.Circle(s, ipx, ipy, hr, CHROME[1]);
            RigDrawUtil.Circle(s, ipx, ipy, hr - 1, CHROME[3]);
            RigDrawUtil.Circle(s, ipx - 1, ipy - 1, System.Math.Max(1, hr - 3), CHROME[5]);
        }

        private static void GaugeRpm(DrawSurface s, int gx, int gy, int r, float rpm01)
        {
            Bezel(s, gx, gy, r);
            for (int i = 0; i <= 12; i++)
            {
                double v = i / 12.0, deg = -135.0 + 270.0 * v;
                bool major = i % 2 == 0;
                RadialTick(s, gx, gy, deg, r - 3, major ? r - 12 : r - 8, major ? 2 : 1, major ? GTICK : GTICKD);
            }
            for (int i = 0; i <= 10; i++)
            {
                double v = 0.84 + 0.16 * i / 10.0;
                RadialTick(s, gx, gy, -135.0 + 270.0 * v, r - 3, r - 7, 2, RED[1]);
            }
            for (int n = 0; n <= 6; n++)
            {
                RigDrawUtil.Dir((-135.0 + 270.0 * n / 6.0) * DEG, out double dx, out double dy);
                double rr = r - 22;
                RigDrawUtil.TextC(s, n.ToString(), gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 2), 1,
                                  n >= 5 ? RED[1] : GTICK);
            }
            RigDrawUtil.TextC(s, "X100", gx, gy - 4, 1, GTICKD);
            RigDrawUtil.TextC(s, "RPM", gx, gy + r - 18, 2, GLABEL);
            RigDrawUtil.Dir(HelmDashGeometry.RpmAngleDeg(rpm01) * DEG, out double rdx, out double rdy);
            double lenN = r - 13;
            Needle(s, r, gx, gy, gx + rdx * lenN, gy + rdy * lenN);
        }

        private static void GaugeFuel(DrawSurface s, int gx, int gy, int r, float fuel01, bool low, bool blink)
        {
            Bezel(s, gx, gy, r);
            int drop = DrawSurface.JsRound(r * 0.42), py = gy + drop, rimR = r - 6;
            for (int i = 0; i <= 10; i++)
            {
                bool major = i % 2 == 0;
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(i / 10.0), r - 3,
                           major ? r - 12 : r - 8, major ? 2 : 1, major ? GTICK : GTICKD);
            }
            for (int i = 0; i <= 3; i++)
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(0.10 * i / 3.0), r - 3, r - 7, 2, RED[1]);
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "E", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, RED[1]);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(1) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "F", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, GTICK);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0.5) * DEG, out double dx, out double dy);
                double rr = r - 16;
                s.FillRect(DrawSurface.JsRound(gx + dx * rr - 1), DrawSurface.JsRound(gy + dy * rr - 2), 2, 4, GTICK);
            }
            RigDrawUtil.TextC(s, "FUEL", gx, gy + r - 16, 2, GLABEL);
            RigDrawUtil.Circle(s, gx, py - 14, 3, low && blink ? AMBER : RigDrawUtil.Hex("c7d0c8"));
            RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(fuel01) * DEG, out double fdx, out double fdy);
            Needle(s, r, gx, py, gx + fdx * rimR, gy + fdy * rimR);
        }

        /// <summary>
        /// The dial's amber backlight (sportRig.js:185-197). The core pass is SOURCE-OVER, not
        /// additive — the one real divergence from her sister's identical-looking helper, and a
        /// deliberate one: adding light to a white enamel dial only bleaches it, so the amber has to
        /// composite normally to read as a backlight at all (consoleRig.js:227 lights BLACK glass and
        /// does use <c>lighter</c>). The outer bloom, spilling onto the surrounding chrome, is
        /// additive in both.
        /// </summary>
        private static void GaugeNight(DrawSurface s, int gx, int gy, int r)
        {
            s.OverRadial(gx - r, gy - r, r * 2, r * 2, gx, gy, 2, r, r - 1,
                         NightCoreT, NightCoreC, NightCoreA);
            int br = r + 18;                       // the source's fill rect; the gradient dies at r+16
            s.AddRadial(gx - br, gy - br, br * 2, br * 2, gx, gy, r * 0.5, r + 16, r + 16,
                        NightBloomT, NightBloomC, NightBloomA);
        }

        // ---- switches: chrome bats on dark housings (sportRig.js:249-291) -------------------------
        // sportRig.js:368-369 calls toggle() with NO night argument — this hull's switchgear has no
        // authored halo, unlike the console's. Nothing invented here.
        private static void Toggle(DrawSurface s, int x, int y, int w, int h, int lampCx, int lampY,
                                   bool on, Color32 lampCol)
        {
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, h + 6, 5, GRAPH[0]);
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, 2, 2, GRAPH[3]);
            RigDrawUtil.Screw(s, x - 1, y - 1, 2, GRAPH);
            RigDrawUtil.Screw(s, x + w + 1, y + h + 1, 2, GRAPH);
            RigDrawUtil.RRect(s, x, y, w, h, 4, RigDrawUtil.Hex("05080a"));
            int midY = y + h / 2, cx = x + w / 2;
            int ty = on ? y + 3 : midY + 1, by = on ? midY - 1 : y + h - 3;
            RigDrawUtil.RRect(s, cx - 4, ty, 8, by - ty, 3, CHROME[1]);
            RigDrawUtil.RRect(s, cx - 4, on ? ty : by - 6, 8, 6, 3, CHROME[3]);
            s.FillRect(cx - 2, on ? ty + 1 : by - 4, 3, 2, CHROME[5]);
            RigDrawUtil.Circle(s, cx, midY, 3, CHROME[5]);
            RigDrawUtil.Circle(s, lampCx, lampY, 3, on ? lampCol : LAMPOFF);
            if (on) s.BlendRect(lampCx - 1, lampY - 1, 2, 2, WHITE, 0.55f);
        }

        private static void DrawIgnition(DrawSurface s, int cx, int cy, int r, bool running)
        {
            RigDrawUtil.Circle(s, cx, cy, r, CHROME[0]);
            RigDrawUtil.Circle(s, cx, cy, r - 1, CHROME[2]);
            RigDrawUtil.CircleClipped(s, cx, cy, r - 1, CHROME[4], cx - r, cy - r, r * 2, r);
            RigDrawUtil.Circle(s, cx - 1, cy - 2, r - 4, CHROME[3]);
            RigDrawUtil.Ring(s, cx, cy, r, r - 2, CHROME[0]);
            RigDrawUtil.Circle(s, cx, cy, 5, RigDrawUtil.Hex("05080a"));
            RigDrawUtil.Dir(HelmDashGeometry.StartOffDeg * DEG, out double ox, out double oy);
            RigDrawUtil.Dir(HelmDashGeometry.StartRunDeg * DEG, out double rx, out double ry);
            s.FillRect(DrawSurface.JsRound(cx + ox * (r + 3) - 1), DrawSurface.JsRound(cy + oy * (r + 3) - 1), 2, 2, GTICKD);
            s.FillRect(DrawSurface.JsRound(cx + rx * (r + 3) - 1), DrawSurface.JsRound(cy + ry * (r + 3) - 1), 2, 2,
                       running ? GREEN[2] : GTICKD);
            RigDrawUtil.BlitRotated(s, _key, _keyPx, _keyPy, cx, cy,
                                    running ? HelmDashGeometry.StartRunDeg : HelmDashGeometry.StartOffDeg);
            RigDrawUtil.Circle(s, cx, cy - r - 6, 3, running ? GREEN[2] : LAMPOFF);
            RigDrawUtil.TextC(s, "OFF", cx - r - 1, cy + r + 2, 1, GTICKD);
            RigDrawUtil.TextC(s, "RUN", cx + r + 2, cy + r + 2, 1, running ? GREEN[2] : GTICKD);
        }
    }
}
