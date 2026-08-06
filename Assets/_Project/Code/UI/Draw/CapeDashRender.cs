using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the CAPE ISLANDER's dash CHROME — the immutable rig source is
    /// <c>docs/art/rigs/ui/cape-islander-helm/Art/capeRig.js</c> (ADR 0025, Option A). The 1982
    /// wheelhouse: a MAHOGANY-framed panel faced in buff CORK laminate, two chrome-bezel gauges on
    /// aged cream faces with black needles, stacked banks of red rocker breakers, a cream
    /// side-binnacle, and a brow of three flush mounts. Mirror of <c>paint()</c> (capeRig.js:407-496)
    /// MINUS the composited instruments — the same composition contract
    /// <see cref="ConsoleDashRender"/> carries, and the same <c>Pilot*</c> geometry as
    /// <see cref="NoviDashRender"/> (capeRig.js:121 == noviRig.js:117-119: the two wheelhouses are one
    /// box in two finishes, so the shared pointer-picking and the composited lever line up across
    /// both). The two files diverge ONLY in paint: this one's dial faces are cream where the Novi's
    /// are black, its breakers are red where the Novi's are ice, its blanking covers are cork where
    /// the Novi's are glass — which is exactly what the negative-control golden proves.
    ///
    /// <para><b>The two empty slots.</b> The Cape supports RADAR and GPS
    /// (<c>CapeIslanderHelm.asset</c>) and neither renderer exists yet (S5). Not fitted — the shipped
    /// default — draws the rig's CORK BLANKING PLATE (capeRig.js:314-323), screwed down over the
    /// cutout: honestly empty, in this case's own idiom. Fitted draws the standby page (:325-352).
    /// S5 composites into <see cref="HelmDashGeometry.SlotBoxOnCard"/>, moving no layout here.</para>
    /// </summary>
    public static class CapeDashRender
    {
        private const double DEG = System.Math.PI / 180.0;
        private const int PAD = HelmDashGeometry.PilotTOPPAD;   // the panel sits TOPPAD low (js:420)

        // ---- palettes (capeRig.js:20-37) -----------------------------------------------------------
        private static readonly Color32[] CORK = RigDrawUtil.Ramp("786a46", "94855a", "ac9c6c", "c0b082", "d2c393", "e0d3a8");
        private static readonly Color32[] WOOD = RigDrawUtil.Ramp("241a10", "38271a", "4d3722", "63472c", "7a5a3a", "916c46");
        private static readonly Color32 WOODHI = RigDrawUtil.Hex("a5804f");
        private static readonly Color32[] CHROME = RigDrawUtil.Ramp("12181c", "2f3a42", "586770", "8ea1a9", "c6d2d5", "f1f7f7");
        private static readonly Color32[] STEEL = RigDrawUtil.Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32[] RUBBER = RigDrawUtil.Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47");
        private static readonly Color32[] BRASS = RigDrawUtil.Ramp("5f4715", "8a6a22", "b98f2f", "dbb043", "efc85a");
        private static readonly Color32[] KNOB = RigDrawUtil.Ramp("4a2f16", "6f4823", "8f5f2e", "b57f3e", "d59f56");
        private static readonly Color32 FACE1 = RigDrawUtil.Hex("e9e2cf");
        private static readonly Color32 FACE2 = RigDrawUtil.Hex("d7cdb2");
        private static readonly Color32 INK = RigDrawUtil.Hex("232823");
        private static readonly Color32 INKDIM = RigDrawUtil.Hex("8a825f");
        private static readonly Color32 NEEDLE = RigDrawUtil.Hex("1f2422");
        private static readonly Color32 NEEDLE_HL = RigDrawUtil.Hex("3b433c");
        private static readonly Color32 NEEDLE_DK = RigDrawUtil.Hex("0c0f0d");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("3f120d", "7c2a20", "b3372a", "e0554a", "f59183", "ffb7ac");
        private static readonly Color32[] GREEN = RigDrawUtil.Ramp("1c6a3b", "2f9e57", "66d585");
        private static readonly Color32 AMBER = RigDrawUtil.Hex("e6b53f");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("7fd6c9");
        private static readonly Color32 GLASS = RigDrawUtil.Hex("070f11");
        private static readonly Color32 SPOT_ON = RigDrawUtil.Hex("ffe9c0");
        private static readonly Color32 DECK_ON = RigDrawUtil.Hex("ffdfa0");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

        // The gauge night bloom's stops (capeRig.js:198) — warm lamp, where the Novi's is ice.
        private static readonly double[] NightStopT = { 0.0, 0.6, 1.0 };
        private static readonly Color32[] NightStopC =
            { RigDrawUtil.Hex("ffac3c"), RigDrawUtil.Hex("e4922e"), RigDrawUtil.Hex("be7426") };
        private static readonly float[] NightStopA = { 0.40f, 0.20f, 0.06f };

        // The spotlight wash (capeRig.js:430-431).
        private static readonly double[] SpotStopT = { 0.0, 1.0 };
        private static readonly Color32[] SpotStopC = { SPOT_ON, SPOT_ON };
        private static readonly float[] SpotStopA = { 1f, 0f };

        // Corner radii + nameplate minor axis — the ONLY layout-shaped deltas from the Novi (js:122-127).
        private const int PanelR = 16, CorkR = 8;
        private const int NameRy = 14;

        // ---- the baked ignition key (capeRig.js:271-281), built once -------------------------------
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
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0), by, bowW, bowH, 6, KNOB[1]);
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0) + 1, by + 1, bowW - 2,
                              DrawSurface.JsRound(bowH * 0.5), 5, KNOB[3]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 4, KNOB[0]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 3, KNOB[2]);
        }

        /// <summary>
        /// Paint the Cape Islander dash chrome (everything but the composited instruments) into
        /// <paramref name="s"/> (must be <see cref="HelmDashGeometry.PilotW"/>×<see cref="HelmDashGeometry.PilotH"/>).
        /// </summary>
        public static void Render(DrawSurface s, in HelmFit fit, bool running, float rpm01,
                                  float fuel01, bool night = false, bool blink = false,
                                  bool deck = false, bool spot = false)
        {
            EnsureKey();
            bool lowFuel = fuel01 < 0.13f;
            s.Clear();

            // ---- panel: mahogany frame + cork face (js:423-424) ----
            WoodFrame(s);
            CorkFace(s);

            // ---- deck / spot working-light washes over the cork (js:427-432) ----
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            if (deck) RigDrawUtil.RRectAdd(s, fx, fy, fw, fh, CorkR, DECK_ON, 0.10f);
            if (spot)
                s.AddRadial(fx, fy, fw, fh, HelmDashGeometry.PilotWheelCx, 150 + PAD, 20, 220, 220,
                            SpotStopT, SpotStopC, SpotStopA, 0.14f);
            // (the Cape authors no night ambient over the cork — only its gauges light, js:461)

            // ---- brow: three flush mounts (js:437-450) ----
            Brow(s, in fit, night);

            // (the compass is CompassRigRender's, mounted by the compositor)

            // ---- gauges (js:459-461) ----
            int gr = HelmDashGeometry.PilotGaugeR;
            GaugeRpm(s, HelmDashGeometry.PilotRpmCx, HelmDashGeometry.PilotRpmCy + PAD, gr, rpm01);
            GaugeFuel(s, HelmDashGeometry.PilotFuelCx, HelmDashGeometry.PilotFuelCy + PAD, gr,
                      fuel01, lowFuel, blink);
            if (night)
            {
                GaugeNight(s, HelmDashGeometry.PilotRpmCx, HelmDashGeometry.PilotRpmCy + PAD, gr);
                GaugeNight(s, HelmDashGeometry.PilotFuelCx, HelmDashGeometry.PilotFuelCy + PAD, gr);
            }

            // ---- breaker banks, left (js:464) ----
            BreakerBank(s, deck, spot);

            // ---- side-binnacle housing, right (js:467-478) ----
            Binnacle(s);

            // ---- ignition key (js:481) ----
            DrawIgnition(s, HelmDashGeometry.PilotIgnCx, HelmDashGeometry.PilotIgnCy + PAD,
                         HelmDashGeometry.PilotIgnR, running);

            // ---- lever hub only — the lever composites on top (js:232-238, :484) ----
            int dpx = HelmDashGeometry.PilotDrivePx, dpy = HelmDashGeometry.PilotDrivePivotY + PAD;
            RigDrawUtil.Circle(s, dpx, dpy, 20, CHROME[0]);
            RigDrawUtil.Circle(s, dpx, dpy, 17, CHROME[2]);
            RigDrawUtil.Circle(s, dpx - 1, dpy - 2, 14, CHROME[4]);
            RigDrawUtil.Ring(s, dpx, dpy, 20, 18, RUBBER[0]);
            RigDrawUtil.Screw(s, dpx, dpy, 3, CHROME);

            // ---- steering column + boss (js:487-489; the WHEEL is WheelRigRender's) ----
            int wcx = HelmDashGeometry.PilotWheelCx, wcy = HelmDashGeometry.PilotWheelCy + PAD;
            RigDrawUtil.ThickLine(s, wcx, wcy, wcx, fy + fh - 6, 18, CHROME[1]);
            RigDrawUtil.ThickLine(s, wcx - 5, wcy + 10, wcx - 5, fy + fh - 6, 3, CHROME[3]);
            RigDrawUtil.Circle(s, wcx, wcy, 20, CHROME[1]);

            // ---- builder's plate, low-centre (js:493) ----
            Nameplate(s);
        }

        // ---- mahogany frame + speckled cork face (js:368-404) --------------------------------------

        /// <summary>capeRig.js:114 — the rig's own deterministic hash, standing in for Math.random so
        /// the cork speckle and the wood grain are the SAME every repaint (rule 5: no hidden
        /// randomness) and the same as the rig's preview.</summary>
        private static double HashRnd(double n)
        {
            double x = System.Math.Sin(n * 127.1 + 13.7) * 43758.5453;
            return x - System.Math.Floor(x);
        }

        private static void WoodFrame(DrawSurface s)
        {
            int px = HelmDashGeometry.PilotShellX, py = HelmDashGeometry.PilotShellY + PAD;
            int pw = HelmDashGeometry.PilotShellW, ph = HelmDashGeometry.PilotShellH;
            RigDrawUtil.RRect(s, px, py, pw, ph, PanelR, WOOD[0]);
            RigDrawUtil.RRect(s, px + 1, py + 1, pw - 2, ph - 2, PanelR - 1, WOOD[2]);
            RigDrawUtil.RRect(s, px + 3, py + 3, pw - 6, ph - 6, PanelR - 2, WOOD[3]);
            // grain streaks inside the frame's own rounded clip (js:391-396)
            for (int i = 0; i < 40; i++)
            {
                double yy = py + 4 + HashRnd(i * 2.3) * (ph - 8);
                Color32 col = HashRnd(i) > 0.5 ? WOODHI : WOOD[1];
                ClipRRect(s, px + 3, py + 3, pw - 6, ph - 6, PanelR - 2,
                          px + 4, (int)System.Math.Floor(yy), pw - 8, 1, col, 0.35f);
            }
            RigDrawUtil.RRect(s, px + 3, py + 3, pw - 6, 3, PanelR - 2, WOODHI);
            // brass cove pin around the cork edge
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW;
            RigDrawUtil.RRect(s, fx - 3, fy - 3, fw + 6, 2, 1, BRASS[2]);
            // corner bungs
            RigDrawUtil.Screw(s, px + 10, py + 10, 2, KNOB);
            RigDrawUtil.Screw(s, px + pw - 10, py + 10, 2, KNOB);
            RigDrawUtil.Screw(s, px + 10, py + ph - 10, 2, KNOB);
            RigDrawUtil.Screw(s, px + pw - 10, py + ph - 10, 2, KNOB);
        }

        private static void CorkFace(DrawSurface s)
        {
            int fx = HelmDashGeometry.PilotFaceX, fy = HelmDashGeometry.PilotFaceY + PAD;
            int fw = HelmDashGeometry.PilotFaceW, fh = HelmDashGeometry.PilotFaceH;
            RigDrawUtil.RRect(s, fx, fy, fw, fh, CorkR, CORK[3]);
            RigDrawUtil.RRect(s, fx + 1, fy + 1, fw - 2, fh - 3, CorkR, CORK[3]);
            // vignette top-light + footer shade, inside the face's clip (js:372-376)
            ClipRRect(s, fx, fy, fw, fh, CorkR, fx, fy, fw, 22, RigDrawUtil.Hex("fff8e0"), 0.16f);
            ClipRRect(s, fx, fy, fw, fh, CorkR, fx, fy + fh - 26, fw, 26, RigDrawUtil.Hex("1e160a"), 0.14f);
            // cork speckle — 1250 deterministic motes (js:378-383)
            const int n = 1250;
            for (int i = 0; i < n; i++)
            {
                double rx = HashRnd(i * 1.7), ry = HashRnd(i * 3.1 + 9), rv = HashRnd(i * 5.3 + 2);
                int sx = fx + (int)System.Math.Floor(rx * fw);
                int sy = fy + (int)System.Math.Floor(ry * fh);
                Color32 col = rv > 0.66 ? CORK[5] : (rv > 0.33 ? CORK[1] : CORK[2]);
                ClipRRect(s, fx, fy, fw, fh, CorkR, sx, sy, 1, 1, col, 1f);
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

        // ---- the brow (js:437-450) -----------------------------------------------------------------

        private static void Brow(DrawSurface s, in HelmFit fit, bool night)
        {
            for (int i = 0; i < HelmDashGeometry.PilotSlotX.Length; i++)
            {
                if (HelmDashGeometry.SlotIsDisplacedByCompass(i, fit.Compass)) continue;

                if (i == HelmDashGeometry.PilotSounderSlot)
                {
                    bool fish = fit.Sounder == SounderKind.Fish;
                    HelmDashGeometry.SlotBoxOnCard(i, fish, out int bx, out int by, out int bw, out int bh);
                    if (fit.Sounder == SounderKind.None)
                    {
                        // The rig's preview always carries a sounder, so it authors no empty state for
                        // this mount; an unfitted one gets the same cork blanking plate the radar and
                        // gps cutouts use — this case's own idiom for "reserved, nothing in it".
                        ScreenMount(s, bx, by, bw, bh, "SOUNDER", fitted: false, night);
                    }
                    else
                    {
                        // Gasket + chrome bezel only; the GLASS belongs to the sounder host (js:440-441).
                        RigDrawUtil.RRect(s, bx - 5, by - 4, bw + 10, bh + 9, 9, RUBBER[0]);
                        RigDrawUtil.RRect(s, bx - 4, by - 3, bw + 8, bh + 7, 8, CHROME[2]);
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
                        // gasket, only the fill differs.
                        RigDrawUtil.RRect(s, bx - 5, by - 4, bw + 10, bh + 9, 9, RUBBER[0]);
                        RigDrawUtil.RRect(s, bx - 4, by - 3, bw + 8, bh + 7, 8, CHROME[2]);
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

        /// <summary>
        /// A flush screen mount (js:306-353). The cutout + chrome bezel are ALWAYS drawn — that IS the
        /// reserved space. <paramref name="fitted"/> false — the shipped Cape — screws a CORK BLANKING
        /// PLATE over it. Fitted is the rig's standby page, which S5's renderers composite over.
        /// </summary>
        private static void ScreenMount(DrawSurface s, int x, int y, int w, int h, string kind,
                                        bool fitted, bool night)
        {
            RigDrawUtil.RRect(s, x - 5, y - 5, w + 10, h + 10, 9, RUBBER[0]);       // gasket
            RigDrawUtil.RRect(s, x - 4, y - 4, w + 8, h + 8, 8, CHROME[2]);         // chrome bezel
            RigDrawUtil.RRectClipped(s, x - 4, y - 4, w + 8, 8, 8, CHROME[5], x - 4, y - 4, w + 8, 5);
            RigDrawUtil.Screw(s, x - 1, y - 1, 2, CHROME);
            RigDrawUtil.Screw(s, x + w + 1, y - 1, 2, CHROME);
            RigDrawUtil.Screw(s, x - 1, y + h + 1, 2, CHROME);
            RigDrawUtil.Screw(s, x + w + 1, y + h + 1, 2, CHROME);

            int cx = x + w / 2, cy = y + h / 2;
            if (!fitted)
            {
                RigDrawUtil.RRect(s, x, y, w, h, 5, CORK[3]);                       // cork cover plate
                RigDrawUtil.RRect(s, x + 2, y + 2, w - 4, 4, 3, CORK[5]);
                RigDrawUtil.RRect(s, x + 2, y + h - 5, w - 4, 3, 2, CORK[1]);
                RigDrawUtil.Screw(s, x + 7, y + 7, 2, STEEL);
                RigDrawUtil.Screw(s, x + w - 7, y + 7, 2, STEEL);
                RigDrawUtil.Screw(s, x + 7, y + h - 7, 2, STEEL);
                RigDrawUtil.Screw(s, x + w - 7, y + h - 7, 2, STEEL);
                RigDrawUtil.TextC(s, kind, cx, cy - 9, 2, WOOD[2]);
                RigDrawUtil.TextC(s, "BLANKING PLATE", cx, cy + 6, 1, INKDIM);
                return;
            }

            RigDrawUtil.RRect(s, x, y, w, h, 5, RigDrawUtil.Hex("02090b"));         // recessed dark glass
            RigDrawUtil.RRect(s, x + 2, y + 2, w - 4, h - 4, 4, GLASS);
            Color32 baseCol = night ? RigDrawUtil.Hex("3a2c0c") : RigDrawUtil.Hex("123a36");
            Color32 acc = night ? AMBER : TEAL;
            if (kind == "RADAR")
            {
                for (int i = 1; i <= 3; i++)
                {
                    int rr = i * DrawSurface.JsRound(h * 0.16);
                    RigDrawUtil.Ring(s, cx, cy + 6, rr, rr - 1, baseCol);
                }
                // Phase 0: nothing can fit a radar yet, so the standby sweep is static and carries no
                // repaint key of its own (rule 7). S5 owns the live sweep.
                RigDrawUtil.Dir(0.0, out double dx, out double dy);
                RigDrawUtil.ThickLine(s, cx, cy + 6, cx + dx * h * 0.42, cy + 6 + dy * h * 0.42, 2, acc, 0.5f);
            }
            else
            {
                for (int gx = x + 10; gx < x + w - 8; gx += 14) s.FillRect(gx, y + 8, 1, h - 16, baseCol);
                for (int gy = y + 10; gy < y + h - 8; gy += 13) s.FillRect(x + 8, gy, w - 16, 1, baseCol);
                RigDrawUtil.ThickLine(s, cx - 14, cy + 8, cx + 2, cy - 6, 2, acc);
                RigDrawUtil.ThickLine(s, cx + 2, cy - 6, cx + 16, cy + 2, 2, acc);
                RigDrawUtil.Circle(s, cx + 16, cy + 2, 2,
                                   night ? RigDrawUtil.Hex("ffd77a") : RigDrawUtil.Hex("a9e7dd"));
            }
            s.AddRect(x + 2, y + 2, w - 4, h - 4,
                      night ? RigDrawUtil.Hex("ffbe3c") : RigDrawUtil.Hex("7fd6c9"), night ? 0.10f : 0.09f);
            RigDrawUtil.TextC(s, kind, cx, y + 8, 2,
                              night ? RigDrawUtil.Hex("ffd77a") : RigDrawUtil.Hex("a9e7dd"));
            RigDrawUtil.TextC(s, "STANDBY · OPTIONAL", cx, y + h - 13, 1,
                              night ? RigDrawUtil.Hex("8a6a2c") : RigDrawUtil.Hex("4e7d78"));
            s.BlendRect(x + 3, y + 3, w - 6, 2, WHITE, 0.06f);                      // glass glare
            RigDrawUtil.Circle(s, x + w - 8, y + 7, 2, night ? AMBER : GREEN[1]);   // standby LED
        }

        // ---- gauges (js:148-200) -------------------------------------------------------------------

        private static void GaugeBezel(DrawSurface s, int gx, int gy, int r)
        {
            RigDrawUtil.Circle(s, gx, gy, r + 6, RUBBER[0]);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 1, CHROME[2]);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 1, CHROME[5],
                             gx - r - 6, gy - r - 6, (r + 6) * 2, r + 3);        // lit top half
            RigDrawUtil.Circle(s, gx, gy, r, FACE1);                             // aged cream dial
            RigDrawUtil.Ring(s, gx, gy, r, r - 2, FACE2);
            s.BlendRect(gx - r + 4, gy - r + 5, r, 2, WHITE, 0.28f);
        }

        private static void RadialTick(DrawSurface s, int gx, int gy, double deg, double rOut, double rIn,
                                       double w, Color32 col)
        {
            RigDrawUtil.Dir(deg * DEG, out double dx, out double dy);
            RigDrawUtil.ThickLine(s, gx + dx * rIn, gy + dy * rIn, gx + dx * rOut, gy + dy * rOut, w, col);
        }

        /// <summary>The black instrument needle (js:160-167) — no ice, no hot core: this is 1982.</summary>
        private static void Needle(DrawSurface s, int r, double px, double py, double tx, double ty)
        {
            double dx = tx - px, dy = ty - py;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) len = 1.0;
            double ux = dx / len, uy = dy / len, tail = System.Math.Min(14.0, len * 0.3);
            RigDrawUtil.ThickLine(s, px, py, tx, ty, 3, NEEDLE);
            RigDrawUtil.ThickLine(s, px, py, px + ux * (len - 2), py + uy * (len - 2), 1, NEEDLE_HL);
            RigDrawUtil.ThickLine(s, px, py, px - ux * tail, py - uy * tail, 4, NEEDLE_DK);
            int hr = DrawSurface.JsRound(r * 0.15) + 1;
            int ipx = DrawSurface.JsRound(px), ipy = DrawSurface.JsRound(py);
            RigDrawUtil.Circle(s, ipx, ipy, hr, CHROME[1]);
            RigDrawUtil.Circle(s, ipx, ipy, hr - 1, CHROME[3]);
            RigDrawUtil.Circle(s, ipx - 1, ipy - 1, System.Math.Max(1, hr - 3), CHROME[5]);
        }

        private static void GaugeRpm(DrawSurface s, int gx, int gy, int r, float rpm01)
        {
            GaugeBezel(s, gx, gy, r);
            for (int i = 0; i <= 12; i++)
            {
                double v = i / 12.0;
                bool major = i % 2 == 0;
                RadialTick(s, gx, gy, -135.0 + 270.0 * v, r - 3, major ? r - 12 : r - 8,
                           major ? 2 : 1, major ? INK : INKDIM);
            }
            for (int i = 0; i <= 10; i++)                                  // painted red line (js:172)
            {
                double v = 0.84 + 0.16 * i / 10.0;
                RadialTick(s, gx, gy, -135.0 + 270.0 * v, r - 3, r - 7, 2, RED[2]);
            }
            for (int n = 0; n <= 6; n++)
            {
                RigDrawUtil.Dir((-135.0 + 270.0 * n / 6.0) * DEG, out double dx, out double dy);
                double rr = r - 21;
                RigDrawUtil.TextC(s, n.ToString(), gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 2), 1,
                                  n >= 5 ? RED[2] : INK);
            }
            RigDrawUtil.TextC(s, "X100", gx, gy - 6, 1, INKDIM);
            RigDrawUtil.TextC(s, "RPM", gx, gy + r - 19, 2, BRASS[1]);
            RigDrawUtil.Dir(HelmDashGeometry.RpmAngleDeg(rpm01) * DEG, out double rdx, out double rdy);
            double len = r - 13;
            Needle(s, r, gx, gy, gx + rdx * len, gy + rdy * len);
        }

        private static void GaugeFuel(DrawSurface s, int gx, int gy, int r, float fuel01, bool low, bool blink)
        {
            GaugeBezel(s, gx, gy, r);
            int drop = DrawSurface.JsRound(r * 0.42), py = gy + drop, rimR = r - 6;
            for (int i = 0; i <= 10; i++)
            {
                bool major = i % 2 == 0;
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(i / 10.0), r - 3,
                           major ? r - 12 : r - 8, major ? 2 : 1, major ? INK : INKDIM);
            }
            for (int i = 0; i <= 3; i++)
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(0.10 * i / 3.0), r - 3, r - 7, 2, RED[2]);
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "E", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, RED[2]);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(1) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "F", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, INK);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0.5) * DEG, out double dx, out double dy);
                double rr = r - 16;
                s.FillRect(DrawSurface.JsRound(gx + dx * rr - 1), DrawSurface.JsRound(gy + dy * rr - 2), 2, 4, INK);
            }
            RigDrawUtil.TextC(s, "FUEL", gx, gy + r - 17, 2, BRASS[1]);
            RigDrawUtil.Circle(s, gx, py - 15, 3, low && blink ? AMBER : RigDrawUtil.Hex("b9ad8e"));
            RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(fuel01) * DEG, out double fdx, out double fdy);
            Needle(s, r, gx, py, gx + fdx * rimR, gy + fdy * rimR);   // tip rides gy, pivot rides py (js:192)
        }

        private static void GaugeNight(DrawSurface s, int gx, int gy, int r)
            => s.AddRadial(gx - r, gy - r, r * 2, r * 2, gx, gy, 2, r, r - 1,
                           NightStopT, NightStopC, NightStopA);

        // ---- red rocker breakers (js:241-270) -------------------------------------------------------

        private static void Rocker(DrawSurface s, int x, int y, bool on, string label)
        {
            int rw = HelmDashGeometry.PilotRockW, rh = HelmDashGeometry.PilotRockH;
            RigDrawUtil.RRect(s, x - 2, y - 2, rw + 4, rh + 4, 4, RUBBER[0]);              // gasket
            RigDrawUtil.RRect(s, x, y, rw, rh, 3, RigDrawUtil.Hex("0a0d10"));              // well
            int half = DrawSurface.JsRound(rh / 2.0);
            // the paddle rocks toward the lit (bottom) half when on
            int litY = on ? y + half : y + 1, litH = on ? rh - half - 1 : half - 1;
            int dimY = on ? y + 1 : y + half, dimH = on ? half - 1 : rh - half - 1;
            RigDrawUtil.RRect(s, x + 1, dimY, rw - 2, dimH, 2, RED[0]);                    // recessed half
            RigDrawUtil.RRect(s, x + 1, litY, rw - 2, litH, 2, on ? RED[3] : RED[1]);
            if (on)
            {
                RigDrawUtil.RRect(s, x + 3, litY + 1, rw - 4, 2, 1, RED[4]);
                s.BlendRect(x + rw - 8, litY + 2, 3, 2, RigDrawUtil.Hex("ffdcc8"), 0.7f);
            }
            // engraved legend ABOVE the rocker (js:253) — the Cape labels its breakers on the panel
            RigDrawUtil.TextC(s, label, x + rw / 2.0, y - 8, 1,
                              on ? RigDrawUtil.Hex("d8ccae") : INKDIM);
        }

        private static readonly string[] ColA = { "DECK", "BILGE", "NAV", "ANCH", "PUMP", "HORN" };
        private static readonly string[] ColB = { "SPOT", "WIPE", "CABIN", "INST", "ACC", "VHF" };

        private static void BreakerBank(DrawSurface s, bool deck, bool spot)
        {
            int bx = HelmDashGeometry.PilotBankX, by = HelmDashGeometry.PilotBankY + PAD;
            int bw = HelmDashGeometry.PilotBankW, bh = HelmDashGeometry.PilotBankH;
            int br = HelmDashGeometry.PilotBankR;
            RigDrawUtil.RRect(s, bx - 2, by - 2, bw + 4, bh + 4, br + 1, RUBBER[0]);
            RigDrawUtil.RRect(s, bx, by, bw, bh, br, CHROME[1]);
            RigDrawUtil.RRect(s, bx + 2, by + 2, bw - 4, 5, br - 3, CHROME[3]);
            RigDrawUtil.RRect(s, bx + 3, by + bh - 6, bw - 6, 4, 3, RUBBER[0]);
            RigDrawUtil.Screw(s, bx + 8, by + 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 8, by + 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + 8, by + bh - 8, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 8, by + bh - 8, 2, CHROME);
            for (int i = 0; i < HelmDashGeometry.PilotRockRows; i++)
            {
                int y = HelmDashGeometry.PilotRockRow0 + i * HelmDashGeometry.PilotRockDy + PAD;
                Rocker(s, HelmDashGeometry.PilotRockColA, y, i == 0 && deck, ColA[i]);
                Rocker(s, HelmDashGeometry.PilotRockColB, y, i == 0 && spot, ColB[i]);
            }
        }

        // ---- cream side-binnacle (js:467-478) -------------------------------------------------------

        private static void Binnacle(DrawSurface s)
        {
            int bx = HelmDashGeometry.PilotBinnX, by = HelmDashGeometry.PilotBinnY + PAD;
            int bw = HelmDashGeometry.PilotBinnW, bh = HelmDashGeometry.PilotBinnH;
            int br = HelmDashGeometry.PilotBinnR;
            RigDrawUtil.RRect(s, bx - 2, by - 2, bw + 4, bh + 4, br + 1, RUBBER[0]);
            RigDrawUtil.RRect(s, bx, by, bw, bh, br, CORK[1]);
            RigDrawUtil.RRect(s, bx + 1, by + 1, bw - 2, bh - 4, br - 1, CORK[2]);
            RigDrawUtil.RRect(s, bx + 3, by + 3, bw - 6, 6, br - 3, CORK[4]);
            RigDrawUtil.RRect(s, bx + 3, by + bh - 8, bw - 6, 5, 4, CORK[0]);
            RigDrawUtil.Screw(s, bx + 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + 9, by + bh - 10, 2, CHROME);
            RigDrawUtil.Screw(s, bx + bw - 9, by + bh - 10, 2, CHROME);
            // F / N / R detents engraved on the housing — static legends (js:475-477)
            RigDrawUtil.TextC(s, "F", 586, 316 + PAD, 1, WOOD[0]);
            RigDrawUtil.TextC(s, "N", 586, 336 + PAD, 1, WOOD[1]);
            RigDrawUtil.TextC(s, "R", 586, 356 + PAD, 1, RED[2]);
            // emergency lanyard tab, lower-left (js:478)
            RigDrawUtil.RRect(s, bx + 8, by + bh - 30, 15, 12, 3, RUBBER[0]);
            RigDrawUtil.RRect(s, bx + 10, by + bh - 28, 11, 4, 2, RED[2]);
        }

        // ---- ignition (js:282-294) ------------------------------------------------------------------

        private static void DrawIgnition(DrawSurface s, int cx, int cy, int r, bool running)
        {
            RigDrawUtil.Circle(s, cx, cy, r + 2, RUBBER[0]);
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

        // ---- brass nameplate (js:356-365) ------------------------------------------------------------

        private static void Nameplate(DrawSurface s)
        {
            int ncx = HelmDashGeometry.PilotNameCx, ncy = HelmDashGeometry.PilotNameCy + PAD;
            int rx = HelmDashGeometry.PilotNameRx;
            RigDrawUtil.Ellipse(s, ncx, ncy + 1, rx + 2, NameRy + 2, RUBBER[0]);
            RigDrawUtil.Ellipse(s, ncx, ncy, rx, NameRy, BRASS[0]);
            RigDrawUtil.Ellipse(s, ncx, ncy, rx - 2, NameRy - 2, BRASS[1]);
            RigDrawUtil.Ellipse(s, ncx, ncy - 2, rx - 4, NameRy - 5, BRASS[2]);
            EllipseClipTop(s, ncx, ncy - 1, rx - 3, NameRy - 3, BRASS[3], ncy - NameRy, NameRy);
            RigDrawUtil.TextC(s, "CAPE ISLANDER", ncx, ncy - 6, 1, WOOD[0]);
            RigDrawUtil.TextC(s, "HARBOUR MARINE", ncx, ncy + 2, 1, WOOD[1]);
        }

        /// <summary>The nameplate's lit upper half (js:361-362) — an ellipse under a top-half clip.</summary>
        private static void EllipseClipTop(DrawSurface s, int cx, int cy, int rx, int ry, Color32 c,
                                           int clipY, int clipH)
        {
            rx = System.Math.Max(1, rx);
            ry = System.Math.Max(1, ry);
            for (int dy = -ry; dy <= ry; dy++)
            {
                int yy = cy + dy;
                if (yy < clipY || yy >= clipY + clipH) continue;
                int dx = (int)System.Math.Floor(rx * System.Math.Sqrt(
                    System.Math.Max(0.0, 1.0 - (double)(dy * dy) / (ry * ry))));
                s.FillRect(cx - dx, yy, 2 * dx + 1, 1, c);
            }
        }
    }
}
