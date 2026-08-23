using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the NOVI lobster boat's dash CHROME — the immutable rig source is
    /// <c>docs/art/rigs/ui/novi-helm/Art/noviRig.js</c> (ADR 0025, Option A). The modern downeast
    /// pilothouse: a moulded WHITE gelcoat console with a graphite instrument fascia, two round
    /// BLACK-FACE gauges with ice-blue segmented arcs and ice needles, a bank of backlit rockers on
    /// a carbon-fibre strip, a black/stainless side-binnacle, and a brow of THREE equal edge-to-edge
    /// glass MFD mounts. Mirror of <c>paint()</c> (noviRig.js:408-505) MINUS the composited
    /// instruments — the same composition contract <see cref="ConsoleDashRender"/> carries: the brow
    /// glass is the sounder host's, the compass is <see cref="CompassRigRender"/>, the wheel the
    /// player grabs is <see cref="WheelRigRender"/> (the rig's own baked wheel is deliberately not
    /// ported), and the lever (<see cref="LeverRigRender"/>) composites last, on top. The compositor
    /// is <see cref="HelmDashController"/>; geometry lives in <see cref="HelmDashGeometry"/>'s
    /// <c>Pilot*</c> table, which the Cape Islander shares.
    ///
    /// <para><b>The two empty slots.</b> This is the first dash with mounts it cannot fill: the hull
    /// supports RADAR and GPS (<c>NoviHelm.asset</c>) and neither renderer exists yet (S5). The rig
    /// authors both states, and both are ported: <b>not fitted</b> — the shipped default — draws the
    /// flush black-glass blanking screen (noviRig.js:329-336), an honestly empty mount in the case's
    /// own idiom; <b>fitted</b> draws the rig's standby page (:338-362), which S5 replaces by
    /// compositing its own cell into <see cref="HelmDashGeometry.SlotBoxOnCard"/> exactly as the
    /// compass and wheel mount today. No hole, no stretched neighbour, and no layout for S5 to move.</para>
    ///
    /// <para><b>Perf (rule 7):</b> no allocation after the static ramps and the baked key; the caller
    /// repaints only on a state-key change (<see cref="HelmDashController"/>).</para>
    /// </summary>
    public static class NoviDashRender
    {
        private const double DEG = System.Math.PI / 180.0;
        private const int PAD = HelmDashGeometry.PilotTOPPAD;   // the dash face sits TOPPAD low (js:421)

        // ---- palettes (noviRig.js:19-32) -----------------------------------------------------------
        private static readonly Color32[] GEL = RigDrawUtil.Ramp("7f8b8d", "98a4a5", "b6c0bf", "cdd6d3", "e2e8e3", "f2f5ef");
        private static readonly Color32[] GRAPH = RigDrawUtil.Ramp("0d1216", "151d22", "212c33", "33414a", "4a5a64", "69808b");
        private static readonly Color32[] CHROME = RigDrawUtil.Ramp("1e242a", "3a454d", "647079", "9fb0b6", "cdd9db", "eff6f6");
        private static readonly Color32[] RUBBER = RigDrawUtil.Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47");
        private static readonly Color32[] ICE = RigDrawUtil.Ramp("0a2632", "0f4a63", "1d7fa8", "3aa8d4", "6fccef", "a9e4ff");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("3f120d", "7c2a20", "b3372a", "e0554a", "f59183", "ffb7ac");
        private static readonly Color32[] GREEN = RigDrawUtil.Ramp("1c6a3b", "2f9e57", "66d585");
        private static readonly Color32 GBLK = RigDrawUtil.Hex("0b0f12");
        private static readonly Color32 GBLK2 = RigDrawUtil.Hex("141b20");
        private static readonly Color32 TKW = RigDrawUtil.Hex("e9f0f2");
        private static readonly Color32 TKD = RigDrawUtil.Hex("5c6b73");
        private static readonly Color32 SEGOFF = RigDrawUtil.Hex("16242b");
        private static readonly Color32 SEGRED = RigDrawUtil.Hex("3a1712");     // unlit red-line segment
        private static readonly Color32 AMBER = RigDrawUtil.Hex("e6b53f");
        private static readonly Color32 GLASS = RigDrawUtil.Hex("050c0f");
        private static readonly Color32 INKDIM = RigDrawUtil.Hex("7d949e");
        private static readonly Color32 SHADOW = RigDrawUtil.Hex("04070a");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

        // The gauge night bloom's stops (noviRig.js:208) — static so the wash allocates nothing.
        private static readonly double[] NightStopT = { 0.0, 0.6, 1.0 };
        private static readonly Color32[] NightStopC =
            { RigDrawUtil.Hex("6fccef"), RigDrawUtil.Hex("3aa8d4"), RigDrawUtil.Hex("1d7fa8") };
        private static readonly float[] NightStopA = { 0.38f, 0.18f, 0.05f };

        // The spotlight wash (noviRig.js:431-432): one colour fading to nothing.
        private static readonly double[] SpotStopT = { 0.0, 1.0 };
        private static readonly Color32[] SpotStopC = { RigDrawUtil.Hex("f2f8ff"), RigDrawUtil.Hex("f2f8ff") };
        private static readonly float[] SpotStopA = { 1f, 0f };

        // The moulded gelcoat face's vertical grade (noviRig.js:384-386) — colour AND alpha per stop.
        private static readonly double[] FaceStopT = { 0.0, 0.34, 0.7, 1.0 };
        private static readonly Color32[] FaceStopC =
            { WHITE, WHITE, RigDrawUtil.Hex("1e2a30"), RigDrawUtil.Hex("141e22") };
        private static readonly float[] FaceStopA = { 0.24f, 0.04f, 0.05f, 0.18f };

        // Corner radii — the ONE place the Novi and the Cape differ in layout terms (noviRig.js:120-121).
        private const int ShellR = 22, FaceR = 14;
        private const int NameRy = 13;                                           // noviRig.js:125

        // ---- the baked ignition key (noviRig.js:286-296), built once -------------------------------
        private static DrawSurface _key;
        private static int _keyPx, _keyPy;

        private static void EnsureKey()
        {
            if (_key != null) return;
            const int bowW = 14, bowH = 18, shaft = 10, pad = 6;
            int w = bowW + pad * 2, h = bowH + shaft + pad * 2;
            _key = new DrawSurface(w, h);
            _keyPx = DrawSurface.JsRound(w / 2.0);
            _keyPy = h - 4;
            RigDrawUtil.ThickLine(_key, _keyPx, _keyPy, _keyPx, _keyPy - shaft, 4, CHROME[3]);
            RigDrawUtil.ThickLine(_key, _keyPx - 1, _keyPy, _keyPx - 1, _keyPy - shaft, 1, CHROME[5]);
            int by = _keyPy - shaft - bowH;
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0), by, bowW, bowH, 6, RUBBER[1]);
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0) + 1, by + 1, bowW - 2,
                              DrawSurface.JsRound(bowH * 0.5), 5, RUBBER[3]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 4, SHADOW);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 3, RUBBER[2]);
        }

        /// <summary>
        /// Paint the Novi dash chrome (everything but the composited instruments) into
        /// <paramref name="s"/> (must be <see cref="HelmDashGeometry.PilotW"/>×<see cref="HelmDashGeometry.PilotH"/>).
        /// <paramref name="fit"/> decides the brow: which mounts are drawn, which are blanked, and
        /// whether the sounder mount is the tall PORTRAIT box the colour finder needs.
        /// </summary>
        public static void Render(DrawSurface s, in HelmFit fit, bool running, float rpm01,
                                  float fuel01, bool night = false, bool blink = false,
                                  bool deck = false, bool spot = false, bool anchorDown = false)
        {
            EnsureKey();
            bool lowFuel = fuel01 < 0.13f;
            s.Clear();

            // ---- console: white gelcoat shell + moulded face (js:424-425) ----
            ConsoleShell(s);
            DashFace(s);

            // ---- deck / spot working-light washes + the faint ice night ambient (js:428-437) ----
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            if (deck) RigDrawUtil.RRectAdd(s, fx, fy, fw, fh, FaceR, RigDrawUtil.Hex("eaf6ff"), 0.09f);
            if (spot)
                s.AddRadial(fx, fy, fw, fh, HelmDashGeometry.PilotWheelCx, 150 + PAD, 20, 220, 220,
                            SpotStopT, SpotStopC, SpotStopA, 0.13f);
            // js:434-437 builds a linear gradient it never uses — rrect() sets its own fill, so the
            // night ambient is a FLAT ice wash. Ported as the source actually paints, not as it reads.
            if (night) RigDrawUtil.RRectAdd(s, fx, fy, fw, fh, FaceR, ICE[4], 0.07f);

            // ---- brow: three edge-to-edge glass mounts (js:443-457) ----
            Brow(s, in fit, night, blink);

            // (the compass — dome on the crown or flush in the face — is CompassRigRender's, mounted
            //  by the compositor at HelmDashGeometry.CompassBoxOnCard)

            // ---- gauges (js:466-468) ----
            int gr = HelmDashGeometry.PilotGaugeR;
            GaugeRpm(s, HelmDashGeometry.PilotRpmCx, HelmDashGeometry.PilotRpmCy + PAD, gr, rpm01);
            GaugeFuel(s, HelmDashGeometry.PilotFuelCx, HelmDashGeometry.PilotFuelCy + PAD, gr,
                      fuel01, lowFuel, blink);
            if (night)
            {
                GaugeNight(s, HelmDashGeometry.PilotRpmCx, HelmDashGeometry.PilotRpmCy + PAD, gr);
                GaugeNight(s, HelmDashGeometry.PilotFuelCx, HelmDashGeometry.PilotFuelCy + PAD, gr);
            }

            // ---- rocker switch bank, left (js:471) ----
            BreakerBank(s, deck, spot, anchorDown);

            // ---- side-binnacle housing, right (js:474-487) ----
            Binnacle(s);

            // ---- ignition key, by the throttle (js:490) ----
            DrawIgnition(s, HelmDashGeometry.PilotIgnCx, HelmDashGeometry.PilotIgnCy + PAD,
                         HelmDashGeometry.PilotIgnR, running);

            // ---- lever hub only — the lever composites on top (js:239-246, :493) ----
            int dpx = HelmDashGeometry.PilotDrivePx, dpy = HelmDashGeometry.PilotDrivePivotY + PAD;
            RigDrawUtil.Circle(s, dpx, dpy, 20, SHADOW);
            RigDrawUtil.Circle(s, dpx, dpy, 18, CHROME[2]);
            RigDrawUtil.Circle(s, dpx - 1, dpy - 2, 15, CHROME[4]);
            RigDrawUtil.Circle(s, dpx, dpy, 12, RUBBER[1]);
            RigDrawUtil.Ring(s, dpx, dpy, 20, 18, SHADOW);
            RigDrawUtil.Screw(s, dpx, dpy, 3, CHROME);

            // ---- steering column + boss (js:496-498; the WHEEL is WheelRigRender's) ----
            int wcx = HelmDashGeometry.PilotWheelCx, wcy = HelmDashGeometry.PilotWheelCy + PAD;
            RigDrawUtil.ThickLine(s, wcx, wcy, wcx, fy + fh - 6, 18, CHROME[1]);
            RigDrawUtil.ThickLine(s, wcx - 5, wcy + 10, wcx - 5, fy + fh - 6, 3, CHROME[3]);
            RigDrawUtil.Circle(s, wcx, wcy, 20, CHROME[1]);

            // ---- builder's plate, low-centre (js:502) ----
            Nameplate(s);
        }

        // ---- moulded gelcoat face + console shell (js:381-405) -------------------------------------

        private static void ConsoleShell(DrawSurface s)
        {
            int sx = HelmDashGeometry.PilotShellX, sy = HelmDashGeometry.PilotShellY + PAD;
            int sw = HelmDashGeometry.PilotShellW, sh = HelmDashGeometry.PilotShellH;
            RigDrawUtil.RRect(s, sx, sy, sw, sh, ShellR, GEL[1]);
            RigDrawUtil.RRect(s, sx + 1, sy + 1, sw - 2, sh - 2, ShellR - 1, GEL[3]);
            RigDrawUtil.RRect(s, sx + 2, sy + 2, sw - 4, sh - 4, ShellR - 2, GEL[4]);
            RigDrawUtil.RRect(s, sx + 3, sy + 3, sw - 6, 4, ShellR - 2, GEL[5]);       // top bevel highlight
            // graphite reveal groove around the inner face + the ice cove hairline
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            RigDrawUtil.RRect(s, fx - 4, fy - 4, fw + 8, fh + 8, FaceR + 3, GRAPH[1]);
            RigDrawUtil.RRect(s, fx - 3, fy - 3, fw + 6, fh + 6, FaceR + 2, GRAPH[3]);
            RigDrawUtil.RRect(s, fx - 3, fy - 3, fw + 6, 1, 0, ICE[3], 0.55f);
            RigDrawUtil.Screw(s, sx + 11, sy + 11, 2, CHROME);
            RigDrawUtil.Screw(s, sx + sw - 11, sy + 11, 2, CHROME);
            RigDrawUtil.Screw(s, sx + 11, sy + sh - 11, 2, CHROME);
            RigDrawUtil.Screw(s, sx + sw - 11, sy + sh - 11, 2, CHROME);
        }

        private static void DashFace(DrawSurface s)
        {
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            RigDrawUtil.RRect(s, fx, fy, fw, fh, FaceR, GEL[3]);

            // The face grade + the moulded reveal, all inside the rig's rounded-rect clip (js:383-392).
            // Colour AND alpha move between stops, so this walks rows rather than using DrawSurface.Lerp.
            for (int v = 0; v < fh; v++)
            {
                int inset = RigDrawUtil.RowInset(v, fh, FaceR);
                double t = (double)v / fh;
                int i = 0;
                while (i < FaceStopT.Length - 1 && t > FaceStopT[i + 1]) i++;
                double lo = FaceStopT[i], hi = FaceStopT[System.Math.Min(i + 1, FaceStopT.Length - 1)];
                double f = hi > lo ? (t - lo) / (hi - lo) : 0.0;
                if (f < 0.0) f = 0.0; else if (f > 1.0) f = 1.0;
                Color32 ca = FaceStopC[i], cb = FaceStopC[System.Math.Min(i + 1, FaceStopC.Length - 1)];
                float aa = FaceStopA[i], ab = FaceStopA[System.Math.Min(i + 1, FaceStopA.Length - 1)];
                s.BlendRect(fx + inset, fy + v, fw - 2 * inset, 1, DrawSurface.Lerp(ca, cb, f),
                            (float)(aa + (ab - aa) * f));
            }
            int brow = HelmDashGeometry.PilotBrowB + PAD;
            FaceClipped(s, fx, brow + 7, fw, 1, RigDrawUtil.Hex("141e22"), 0.12f);
            FaceClipped(s, fx, brow + 8, fw, 1, WHITE, 0.14f);
            FaceClipped(s, fx + 3, fy + 3, 2, fh - 6, WHITE, 0.05f);
        }

        /// <summary>A blend inside the dash face's rounded-rect clip (the rig's <c>save/clip</c>).</summary>
        private static void FaceClipped(DrawSurface s, int x, int y, int w, int h, Color32 c, float alpha)
        {
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            for (int yy = y; yy < y + h; yy++)
            {
                int v = yy - fy;
                if (v < 0 || v >= fh) continue;
                int inset = RigDrawUtil.RowInset(v, fh, FaceR);
                int x0 = System.Math.Max(x, fx + inset), x1 = System.Math.Min(x + w, fx + fw - inset);
                if (x1 > x0) s.BlendRect(x0, yy, x1 - x0, 1, c, alpha);
            }
        }

        // ---- the brow (js:443-457) -----------------------------------------------------------------

        private static void Brow(DrawSurface s, in HelmFit fit, bool night, bool blink)
        {
            for (int i = 0; i < HelmDashGeometry.PilotSlotX.Length; i++)
            {
                // The dome compass takes the crown and displaces the centre mount (js:453).
                if (HelmDashGeometry.SlotIsDisplacedByCompass(i, fit.Compass)) continue;

                if (i == HelmDashGeometry.PilotSounderSlot)
                {
                    bool fish = fit.Sounder == SounderKind.Fish;
                    HelmDashGeometry.SlotBoxOnCard(i, fish, out int bx, out int by, out int bw, out int bh);
                    if (fit.Sounder == SounderKind.None)
                    {
                        // The rig's preview always carries a sounder, so it authors no empty state for
                        // this mount. A bare bezel over the gelcoat would read as damage, so an unfitted
                        // sounder gets the SAME blanking screen the radar and gps mounts use — the
                        // case's own idiom for "reserved, nothing in it".
                        ScreenMount(s, bx, by, bw, bh, "SOUNDER", fitted: false, night);
                    }
                    else
                    {
                        // Bezel + glare + LED only; the GLASS belongs to the sounder host (S2/S3b),
                        // which composites DepthRig/FishRig into this very box. Never a fake reading.
                        GlassBezel(s, bx, by, bw, bh);
                        s.BlendRect(bx + 3, by + 3, bw - 6, 2, WHITE, 0.05f);
                        RigDrawUtil.Circle(s, bx + bw - 8, by + 7, 2, night ? ICE[3] : ICE[4]);
                    }
                }
                else if (i == HelmDashGeometry.PilotRadarSlot)
                {
                    HelmDashGeometry.SlotBoxOnCard(i, true, out int bx, out int by, out int bw, out int bh);
                    if (fit.Radar)
                    {
                        // S5: the GLASS belongs to the radar host, which composites the live PPI into
                        // this very box — the sounder's fitted branch above, for the same reason. Drawing
                        // the rig's STANDBY page under a fitted set would leave "STANDBY · UPGRADEABLE"
                        // showing around a portrait instrument that cannot fill a square slot, and would
                        // be the dash contradicting the instrument. No layout moves: same box, same
                        // bezel, only the fill differs.
                        GlassBezel(s, bx, by, bw, bh);
                        s.BlendRect(bx + 3, by + 3, bw - 6, 2, WHITE, 0.05f);
                        RigDrawUtil.Circle(s, bx + bw - 8, by + 7, 2, night ? ICE[3] : ICE[4]);
                    }
                    else ScreenMount(s, bx, by, bw, bh, "RADAR", fitted: false, night);
                }
                else
                {
                    HelmDashGeometry.SlotBoxOnCard(i, false, out int bx, out int by, out int bw, out int bh);
                    ScreenMount(s, bx, by, bw, bh, "GPS", fit.Gps, night);
                }
            }
        }

        /// <summary>The glossy black glass frame that IS the reserved space (js:321-326) — always
        /// drawn, fitted or not.</summary>
        private static void GlassBezel(DrawSurface s, int x, int y, int w, int h)
        {
            RigDrawUtil.RRect(s, x - 4, y - 4, w + 8, h + 8, 7, SHADOW);
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, h + 6, 6, RigDrawUtil.Hex("0a0e12"));
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, 3, 4, RigDrawUtil.Hex("1b2228"));
            s.BlendRect(x - 1, y - 2, 12, 1, RigDrawUtil.Hex("9fb0b6"), 0.20f);      // brand hairline
        }

        /// <summary>
        /// An MFD mount (js:327-363). <paramref name="fitted"/> false — the shipped Novi — is the flush
        /// black-glass BLANKING screen: honestly empty, in the console's own idiom. Fitted (nothing can
        /// fit one yet) is the rig's standby page; S5's radar/gps renderers composite over the glass box
        /// rather than replacing any of this, so their arrival moves no layout.
        /// </summary>
        private static void ScreenMount(DrawSurface s, int x, int y, int w, int h, string kind,
                                        bool fitted, bool night)
        {
            GlassBezel(s, x, y, w, h);
            int cx = x + w / 2, cy = y + h / 2;
            if (!fitted)
            {
                RigDrawUtil.RRect(s, x, y, w, h, 4, RigDrawUtil.Hex("06090c"));
                RigDrawUtil.RRect(s, x + 2, y + 2, w - 4, h - 4, 3, GLASS);
                s.BlendRect(x + 3, y + 3, w - 6, 2, WHITE, 0.03f);
                RigDrawUtil.TextC(s, kind, cx, cy - 9, 2, RigDrawUtil.Hex("27323b"));
                RigDrawUtil.TextC(s, "NO DISPLAY FITTED", cx, cy + 7, 1, RigDrawUtil.Hex("1e2c34"));
                RigDrawUtil.Circle(s, x + w - 8, y + 7, 2, RigDrawUtil.Hex("20303a"));
                return;
            }

            RigDrawUtil.RRect(s, x, y, w, h, 4, RigDrawUtil.Hex("02070a"));
            RigDrawUtil.RRect(s, x + 2, y + 2, w - 4, h - 4, 3, GLASS);
            Color32 baseCol = night ? RigDrawUtil.Hex("123a4e") : RigDrawUtil.Hex("0f3346");
            Color32 acc = night ? ICE[3] : ICE[4];
            if (kind == "RADAR")
            {
                for (int i = 1; i <= 3; i++)
                {
                    int rr = i * DrawSurface.JsRound(h * 0.16);
                    RigDrawUtil.Ring(s, cx, cy + 6, rr, rr - 1, baseCol);
                }
                // The sweep's phase is not animated here: nothing can fit a radar yet, so the standby
                // page is drawn at phase 0 and carries no repaint key of its own (rule 7).
                RigDrawUtil.Dir(0.0, out double dx, out double dy);
                RigDrawUtil.ThickLine(s, cx, cy + 6, cx + dx * h * 0.42, cy + 6 + dy * h * 0.42, 2, acc, 0.55f);
            }
            else
            {
                for (int gx = x + 10; gx < x + w - 8; gx += 14) s.FillRect(gx, y + 8, 1, h - 16, baseCol);
                for (int gy = y + 10; gy < y + h - 8; gy += 13) s.FillRect(x + 8, gy, w - 16, 1, baseCol);
                RigDrawUtil.ThickLine(s, cx - 14, cy + 8, cx + 2, cy - 6, 2, acc);
                RigDrawUtil.ThickLine(s, cx + 2, cy - 6, cx + 16, cy + 2, 2, acc);
                RigDrawUtil.Circle(s, cx + 16, cy + 2, 2, ICE[5]);
            }
            s.AddRect(x + 2, y + 2, w - 4, h - 4, night ? ICE[4] : ICE[3], night ? 0.09f : 0.08f);
            RigDrawUtil.TextC(s, kind, cx, y + 8, 2, ICE[5]);
            RigDrawUtil.TextC(s, "STANDBY · UPGRADEABLE", cx, y + h - 13, 1, night ? ICE[2] : ICE[1]);
            s.BlendRect(x + 3, y + 3, w - 6, 2, WHITE, 0.05f);
            RigDrawUtil.Circle(s, x + w - 8, y + 7, 2, night ? ICE[3] : ICE[4]);
        }

        // ---- gauges (js:148-210) -------------------------------------------------------------------

        private static void GaugePod(DrawSurface s, int gx, int gy, int r)
        {
            int w = r * 2 + 30, h = r * 2 + 30, x = gx - w / 2, y = gy - h / 2;
            RigDrawUtil.RRect(s, x + 2, y + 3, w, h, 15, SHADOW, 0.5f);          // drop shadow
            RigDrawUtil.RRect(s, x, y, w, h, 15, GRAPH[1]);
            RigDrawUtil.RRect(s, x + 1, y + 1, w - 2, h - 2, 14, GRAPH[2]);
            RigDrawUtil.RRect(s, x + 3, y + 3, w - 6, 5, 12, GRAPH[3]);          // top sheen
            RigDrawUtil.RRect(s, x + 3, y + h - 4, w - 6, 3, 2, GRAPH[0]);
            RigDrawUtil.Screw(s, x + 8, y + 8, 2, CHROME);
            RigDrawUtil.Screw(s, x + w - 8, y + 8, 2, CHROME);
            RigDrawUtil.Screw(s, x + 8, y + h - 8, 2, CHROME);
            RigDrawUtil.Screw(s, x + w - 8, y + h - 8, 2, CHROME);
        }

        private static void GaugeBezel(DrawSurface s, int gx, int gy, int r)
        {
            RigDrawUtil.Circle(s, gx, gy, r + 6, RUBBER[0]);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 2, CHROME[2]);                // slim brushed ring
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 2, CHROME[5],
                             gx - r - 6, gy - r - 6, (r + 6) * 2, r + 3);        // lit top half
            RigDrawUtil.Ring(s, gx, gy, r + 1, r, RUBBER[0]);                    // black inner lip
            RigDrawUtil.Circle(s, gx, gy, r, GBLK2);                             // black dial
            RigDrawUtil.Circle(s, gx, gy, r - 1, GBLK);
            s.BlendRect(gx - r + 5, gy - r + 6, r, 2, WHITE, 0.05f);
        }

        private static void RadialTick(DrawSurface s, int gx, int gy, double deg, double rOut, double rIn,
                                       double w, Color32 col)
        {
            RigDrawUtil.Dir(deg * DEG, out double dx, out double dy);
            RigDrawUtil.ThickLine(s, gx + dx * rIn, gy + dy * rIn, gx + dx * rOut, gy + dy * rOut, w, col);
        }

        /// <summary>The ice needle (js:171-178): black counterweight, ice shaft, hot core, chrome hub.</summary>
        private static void Needle(DrawSurface s, int r, double px, double py, double tx, double ty)
        {
            double dx = tx - px, dy = ty - py;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) len = 1.0;
            double ux = dx / len, uy = dy / len, tail = System.Math.Min(16.0, len * 0.32);
            RigDrawUtil.ThickLine(s, px, py, px - ux * tail, py - uy * tail, 4, RigDrawUtil.Hex("0a0d0f"));
            RigDrawUtil.ThickLine(s, px, py, tx, ty, 3, ICE[3]);
            RigDrawUtil.ThickLine(s, px, py, px + ux * (len - 2), py + uy * (len - 2), 1, RigDrawUtil.Hex("eaf7ff"));
            int hr = DrawSurface.JsRound(r * 0.15) + 1;
            int ipx = DrawSurface.JsRound(px), ipy = DrawSurface.JsRound(py);
            RigDrawUtil.Circle(s, ipx, ipy, hr, CHROME[1]);
            RigDrawUtil.Circle(s, ipx, ipy, hr - 1, CHROME[3]);
            RigDrawUtil.Circle(s, ipx - 1, ipy - 1, System.Math.Max(1, hr - 3), CHROME[5]);
        }

        private static void GaugeRpm(DrawSurface s, int gx, int gy, int r, float rpm01)
        {
            GaugePod(s, gx, gy, r);
            GaugeBezel(s, gx, gy, r);
            const int N = 32;
            for (int i = 0; i <= N; i++)
            {
                double v = (double)i / N;
                bool red = v >= 0.84, lit = v <= rpm01 + 0.001;
                RadialTick(s, gx, gy, -135.0 + 270.0 * v, r - 3, r - 7, 2,
                           lit ? (red ? RED[3] : ICE[4]) : (red ? SEGRED : SEGOFF));
            }
            for (int n = 0; n <= 6; n++)
            {
                RigDrawUtil.Dir((-135.0 + 270.0 * n / 6.0) * DEG, out double dx, out double dy);
                double rr = r - 20;
                RigDrawUtil.TextC(s, n.ToString(), gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 2), 1,
                                  n >= 5 ? RED[4] : TKW);
            }
            RigDrawUtil.TextC(s, "X1000", gx, gy - 8, 1, TKD);
            RigDrawUtil.TextC(s, "RPM", gx, gy + r - 18, 1, ICE[4]);
            RigDrawUtil.Dir(HelmDashGeometry.RpmAngleDeg(rpm01) * DEG, out double rdx, out double rdy);
            double len = r - 12;
            Needle(s, r, gx, gy, gx + rdx * len, gy + rdy * len);
        }

        private static void GaugeFuel(DrawSurface s, int gx, int gy, int r, float fuel01, bool low, bool blink)
        {
            GaugePod(s, gx, gy, r);
            GaugeBezel(s, gx, gy, r);
            int drop = DrawSurface.JsRound(r * 0.42), py = gy + drop, rimR = r - 6;
            const int N = 24;
            for (int i = 0; i <= N; i++)
            {
                double v = (double)i / N;
                bool lowseg = v <= 0.12, lit = v <= fuel01 + 0.001;
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(v), r - 3, r - 7, 2,
                           lit ? (lowseg ? RED[3] : ICE[4]) : (lowseg ? SEGRED : SEGOFF));
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "E", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 1, RED[4]);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(1) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "F", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 1, TKW);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0.5) * DEG, out double dx, out double dy);
                double rr = r - 15;
                s.FillRect(DrawSurface.JsRound(gx + dx * rr - 1), DrawSurface.JsRound(gy + dy * rr - 2), 2, 4, TKW);
            }
            RigDrawUtil.TextC(s, "FUEL", gx, gy + r - 18, 1, ICE[4]);
            RigDrawUtil.Circle(s, gx, gy - 13, 3, low && blink ? AMBER : RigDrawUtil.Hex("2a3942"));
            RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(fuel01) * DEG, out double fdx, out double fdy);
            Needle(s, r, gx, py, gx + fdx * rimR, gy + fdy * rimR);   // tip rides gy, pivot rides py (js:202)
        }

        /// <summary>The dial's night backlight (js:204-210) — an additive bloom inside the glass.</summary>
        private static void GaugeNight(DrawSurface s, int gx, int gy, int r)
            => s.AddRadial(gx - r, gy - r, r * 2, r * 2, gx, gy, 2, r, r - 1,
                           NightStopT, NightStopC, NightStopA);

        // ---- carbon-fibre twill + backlit rockers (js:249-283) -------------------------------------

        private static void Carbon(DrawSurface s, int x, int y, int w, int h, int r)
        {
            RigDrawUtil.RRect(s, x, y, w, h, r, RigDrawUtil.Hex("0a0d10"));
            Color32 cellA = RigDrawUtil.Hex("12181c"), cellB = RigDrawUtil.Hex("0b0f12");
            Color32 sheen = RigDrawUtil.Hex("96aab4");
            for (int yy = y; yy < y + h; yy += 4)
            {
                for (int xx = x; xx < x + w; xx += 4)
                {
                    bool cell = ((xx - x) / 4 + (yy - y) / 4) % 2 == 0;
                    ClipRRect(s, x, y, w, h, r, xx, yy, 4, 4, cell ? cellA : cellB, 1f);
                    if (cell) ClipRRect(s, x, y, w, h, r, xx, yy, 2, 2, sheen, 0.10f);
                    else ClipRRect(s, x, y, w, h, r, xx + 2, yy + 2, 2, 2, sheen, 0.10f);
                }
            }
        }

        /// <summary>A fill clipped to a rounded rect — the rigs' <c>Path2D</c> clip, done per row.</summary>
        private static void ClipRRect(DrawSurface s, int cx, int cy, int cw, int ch, int cr,
                                      int x, int y, int w, int h, Color32 col, float alpha)
        {
            int rr = System.Math.Max(0, System.Math.Min(cr, System.Math.Min(cw, ch) / 2));
            for (int yy = y; yy < y + h; yy++)
            {
                int v = yy - cy;
                if (v < 0 || v >= ch) continue;
                int inset = RigDrawUtil.RowInset(v, ch, rr);
                int x0 = System.Math.Max(x, cx + inset), x1 = System.Math.Min(x + w, cx + cw - inset);
                if (x1 <= x0) continue;
                if (alpha >= 1f) s.FillRect(x0, yy, x1 - x0, 1, col);
                else s.BlendRect(x0, yy, x1 - x0, 1, col, alpha);
            }
        }

        private static void Rocker(DrawSurface s, int x, int y, bool on, string label)
        {
            int rw = HelmDashGeometry.PilotRockW, rh = HelmDashGeometry.PilotRockH;
            RigDrawUtil.RRect(s, x - 2, y - 2, rw + 4, rh + 4, 4, SHADOW);                 // gasket
            RigDrawUtil.RRect(s, x, y, rw, rh, 3, RUBBER[0]);                              // well
            RigDrawUtil.RRect(s, x + 1, y + 1, rw - 2, rh - 2, 2,
                              on ? RigDrawUtil.Hex("122029") : RigDrawUtil.Hex("171c20")); // convex paddle
            int lensH = DrawSurface.JsRound(rh * 0.52);
            RigDrawUtil.RRect(s, x + 3, y + 2, rw - 6, lensH - 1, 1,
                              on ? ICE[2] : RigDrawUtil.Hex("0e161b"));                    // legend lens
            if (on)
            {
                RigDrawUtil.RRect(s, x + 4, y + 3, rw - 8, 1, 0, ICE[4]);
                s.BlendRect(x + rw - 9, y + 3, 3, lensH - 3, RigDrawUtil.Hex("b4e6ff"), 0.5f);
            }
            RigDrawUtil.TextC(s, label, x + rw / 2.0, y + 3, 1,
                              on ? RigDrawUtil.Hex("eaf7ff") : RigDrawUtil.Hex("4a5a62"));
            RigDrawUtil.RRect(s, x + 3, y + rh - 5, rw - 6, 3, 1, RigDrawUtil.Hex("0a0e11"));
        }

        private static readonly string[] ColA = { "DECK", "BILGE", "NAV", "ANCH", "PUMP", "HORN" };
        private static readonly string[] ColB = { "SPOT", "WIPE", "CABIN", "INST", "ACC", "VHF" };

        private static void BreakerBank(DrawSurface s, bool deck, bool spot, bool anchorDown)
        {
            int bx = HelmDashGeometry.PilotBankX, by = HelmDashGeometry.PilotBankY + PAD;
            int bw = HelmDashGeometry.PilotBankW, bh = HelmDashGeometry.PilotBankH;
            int br = HelmDashGeometry.PilotBankR;
            RigDrawUtil.RRect(s, bx - 3, by - 3, bw + 6, bh + 6, br + 2, SHADOW, 0.55f);
            Carbon(s, bx, by, bw, bh, br);
            RigDrawUtil.RRect(s, bx + 2, by + 2, bw - 4, 2, 1, CHROME[3]);         // stainless hairline
            RigDrawUtil.Screw(s, bx + 8, by + 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 8, by + 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + 8, by + bh - 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 8, by + bh - 8, 2, CHROME);
            for (int i = 0; i < HelmDashGeometry.PilotRockRows; i++)
            {
                int y = HelmDashGeometry.PilotRockRow0 + i * HelmDashGeometry.PilotRockDy + PAD;
                // Row 0 col A/B are the working lights; ColA[3] is the authored ANCH breaker — the
                // windlass switch, drawn dead since this dash shipped and now lit by the hook itself.
                bool onA = i == 0 ? deck : (i == HelmDashGeometry.PilotAnchorRow && anchorDown);
                Rocker(s, HelmDashGeometry.PilotRockColA, y, onA, ColA[i]);
                Rocker(s, HelmDashGeometry.PilotRockColB, y, i == 0 && spot, ColB[i]);
            }
        }

        // ---- side-binnacle housing (js:474-487) ----------------------------------------------------

        private static void Binnacle(DrawSurface s)
        {
            int bx = HelmDashGeometry.PilotBinnX, by = HelmDashGeometry.PilotBinnY + PAD;
            int bw = HelmDashGeometry.PilotBinnW, bh = HelmDashGeometry.PilotBinnH;
            int br = HelmDashGeometry.PilotBinnR;
            RigDrawUtil.RRect(s, bx - 2, by - 2, bw + 4, bh + 4, br + 1, SHADOW);
            RigDrawUtil.RRect(s, bx, by, bw, bh, br, GRAPH[1]);
            RigDrawUtil.RRect(s, bx + 1, by + 1, bw - 2, bh - 4, br - 1, GRAPH[2]);
            RigDrawUtil.RRect(s, bx + 3, by + 3, bw - 6, 5, br - 3, GRAPH[3]);
            RigDrawUtil.RRect(s, bx + 3, by + bh - 7, bw - 6, 4, 3, SHADOW);
            RigDrawUtil.Screw(s, bx + 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + 9, by + bh - 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + bh - 10, 2, CHROME);
            // stainless throttle guide slot the lever rides in
            int dpx = HelmDashGeometry.PilotDrivePx;
            RigDrawUtil.RRect(s, dpx - 6, by + 16, 12, bh - 34, 6, SHADOW);
            RigDrawUtil.RRect(s, dpx - 4, by + 18, 8, bh - 38, 4, GRAPH[0]);
            // F / N / R detents engraved on the housing — static legends, not gear tell-tales (js:485-487)
            RigDrawUtil.TextC(s, "F", 586, 316 + PAD, 1, ICE[4]);
            RigDrawUtil.TextC(s, "N", 586, 336 + PAD, 1, INKDIM);
            RigDrawUtil.TextC(s, "R", 586, 356 + PAD, 1, RED[3]);
        }

        // ---- ignition (js:297-309) -----------------------------------------------------------------

        private static void DrawIgnition(DrawSurface s, int cx, int cy, int r, bool running)
        {
            RigDrawUtil.Circle(s, cx, cy, r + 2, SHADOW);
            RigDrawUtil.Circle(s, cx, cy, r, CHROME[1]);
            RigDrawUtil.Circle(s, cx - 1, cy - 2, r - 3, CHROME[3]);
            RigDrawUtil.Ring(s, cx, cy, r, r - 2, CHROME[4]);
            RigDrawUtil.Circle(s, cx, cy, 5, RigDrawUtil.Hex("05080a"));
            RigDrawUtil.Dir(HelmDashGeometry.PilotIgnOffDeg * DEG, out double ox, out double oy);
            RigDrawUtil.Dir(HelmDashGeometry.PilotIgnRunDeg * DEG, out double rx, out double ry);
            s.FillRect(DrawSurface.JsRound(cx + ox * (r + 3) - 1), DrawSurface.JsRound(cy + oy * (r + 3) - 1),
                       2, 2, INKDIM);
            s.FillRect(DrawSurface.JsRound(cx + rx * (r + 3) - 1), DrawSurface.JsRound(cy + ry * (r + 3) - 1),
                       2, 2, running ? GREEN[2] : INKDIM);
            RigDrawUtil.BlitRotated(s, _key, _keyPx, _keyPy, cx, cy,
                                    running ? HelmDashGeometry.PilotIgnRunDeg : HelmDashGeometry.PilotIgnOffDeg);
            RigDrawUtil.TextC(s, "OFF", cx - r - 2, cy + r + 3, 1, INKDIM);
            RigDrawUtil.TextC(s, "RUN", cx + r + 3, cy + r + 3, 1, running ? GREEN[1] : INKDIM);
        }

        // ---- brushed stainless nameplate (js:366-378) -----------------------------------------------

        private static void Nameplate(DrawSurface s)
        {
            int ncx = HelmDashGeometry.PilotNameCx, ncy = HelmDashGeometry.PilotNameCy + PAD;
            int rx = HelmDashGeometry.PilotNameRx;
            int w = rx * 2, h = NameRy * 2 + 6, x = ncx - rx, y = ncy - NameRy - 3;
            RigDrawUtil.RRect(s, x - 1, y + 2, w + 2, h, 4, SHADOW, 0.5f);
            RigDrawUtil.RRect(s, x, y, w, h, 4, CHROME[2]);
            for (int i = 0; i < h; i += 2) ClipRRect(s, x, y, w, h, 4, x, y + i, w, 1, WHITE, 0.05f);
            ClipRRect(s, x, y, w, h, 4, x, y + 1, w, DrawSurface.JsRound(h * 0.34), WHITE, 0.22f);
            RigDrawUtil.RRect(s, x + 1, y + h - 3, w - 2, 2, 1, CHROME[0]);
            RigDrawUtil.TextC(s, "NOVI", ncx, ncy - 6, 2, RigDrawUtil.Hex("0f1519"));
            RigDrawUtil.TextC(s, "HARBOUR MARINE", ncx, ncy + 5, 1, RigDrawUtil.Hex("2a3138"));
            RigDrawUtil.Circle(s, x + 7, ncy, 2, ICE[3]);
            RigDrawUtil.Circle(s, x + w - 7, ncy, 2, ICE[3]);
        }
    }
}
