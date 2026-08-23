using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the centre-console skiff's dash CHROME — the immutable rig source is
    /// <c>docs/art/rigs/ui/console-helm/Art/consoleRig.js</c> (ADR 0025, Option A). Mirror of
    /// <c>paint()</c> (consoleRig.js:331-466) MINUS the four composited instruments, exactly the
    /// composition contract the rig itself uses: the brow sounder (S2's DepthRig port mounts there),
    /// the dome compass (<see cref="CompassRigRender"/>), the wheel (the dash's own baked wheel is
    /// deliberately NOT ported — in game the wheel the player grabs is <see cref="WheelRigRender"/>,
    /// per console-wheel/README.md "the wheel the player grabs is its own rig"), and the lever
    /// (<see cref="LeverRigRender"/>, composited last, on top). The compositor is
    /// <c>HelmDashController</c>; geometry constants live in <see cref="HelmDashGeometry"/>.
    ///
    /// <para><b>Paint scheme:</b> the JS asks <c>ConsoleIso.palette()</c> for the boat's colourway;
    /// without ConsoleIso loaded it falls back to the harbour-white fleet ramps
    /// (consoleRig.js:25-27, :49) — the game's one shipped scheme, so THAT path is what this port
    /// carries. Per-boat paint schemes ride with the ConsoleIso OKLCH mixer in a later slice.</para>
    ///
    /// <para><b>Perf (rule 7):</b> no allocation after the static ramps; the caller repaints only on
    /// a state-key change (running/gear/rpm/fuel quantised — <c>HelmDashController</c>).</para>
    /// </summary>
    public static class ConsoleDashRender
    {
        private const double DEG = System.Math.PI / 180.0;
        private const int PAD = HelmDashGeometry.TOPPAD;   // the dash face sits TOPPAD low (js:345)

        // ---- palettes (consoleRig.js:22-36) -------------------------------------------------------
        private static readonly Color32[] GRAPH = RigDrawUtil.Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] STEEL = RigDrawUtil.Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32[] RUBBER = RigDrawUtil.Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47");
        private static readonly Color32[] PAINT = RigDrawUtil.Ramp("5d6a70", "7e8c90", "a3b0b1", "c2cdca", "dde5df", "eef0ea", "f7f8f3");
        private static readonly Color32[] TRIM = RigDrawUtil.Ramp("0d3f3c", "14554e", "1c7367", "2ba39a", "49b8aa");
        private static readonly Color32[] COVE = RigDrawUtil.Ramp("7a5a1c", "a8842a", "e0b13a");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("7fd6c9");
        private static readonly Color32 FACE = RigDrawUtil.Hex("080c0f");
        private static readonly Color32 TICK = RigDrawUtil.Hex("c6d0ce");
        private static readonly Color32 TICKDIM = RigDrawUtil.Hex("586a6d");
        private static readonly Color32 NEEDLE = RigDrawUtil.Hex("e8863c");
        private static readonly Color32 NEEDLE_HL = RigDrawUtil.Hex("f6b268");
        private static readonly Color32 NEEDLE_DK = RigDrawUtil.Hex("7d4a28");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("7c2a20", "b83b2e", "e0554a");
        private static readonly Color32[] GREEN = RigDrawUtil.Ramp("1c6a3b", "2f9e57", "66d585");
        private static readonly Color32 AMBER = RigDrawUtil.Hex("e6b53f");
        private static readonly Color32 LAMPOFF = RigDrawUtil.Hex("16202a");
        private static readonly Color32 SPOT_ON = RigDrawUtil.Hex("eef3ef");
        private static readonly Color32 SPOT_OFF = RigDrawUtil.Hex("20323a");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

        // The gauge backlight's two gradients (consoleRig.js:228-235) — static so the wash allocates
        // nothing. Amber instrument lighting: a bright core inside the glass, then a soft spill.
        private static readonly double[] NightCoreT = { 0.0, 0.6, 1.0 };
        private static readonly Color32[] NightCoreC =
            { RigDrawUtil.Hex("ffac3c"), RigDrawUtil.Hex("e4922e"), RigDrawUtil.Hex("be7426") };
        private static readonly float[] NightCoreA = { 0.46f, 0.24f, 0.08f };
        private static readonly double[] NightBloomT = { 0.0, 1.0 };
        private static readonly Color32[] NightBloomC =
            { RigDrawUtil.Hex("ffac3c"), RigDrawUtil.Hex("ffac3c") };
        private static readonly float[] NightBloomA = { 0.18f, 0f };

        // chips where boots and buckets rub — console-local, deterministic (consoleRig.js:72)
        private static readonly int[,] SCUFFS =
        {
            { 6, 150, 2, 1, 0 }, { 5, 167, 1, 2, 1 }, { 563, 196, 2, 1, 0 }, { 566, 213, 1, 2, 1 },
            { 247, 384, 3, 1, 0 }, { 318, 389, 2, 1, 1 }, { 9, 332, 1, 3, 1 },
        };

        // ---- harbour-white face paint (facePaint's no-Iso fallback, consoleRig.js:42-70, day) -----
        private static Color32 At(Color32[] r, double t)
        {
            int i = DrawSurface.JsRound((r.Length - 1) * t);
            if (i < 0) i = 0;
            else if (i > r.Length - 1) i = r.Length - 1;
            return r[i];
        }

        private static double Lum(Color32 c)
        {
            double V(byte b) { double v = b / 255.0; return v <= 0.04045 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4); }
            return 0.2126 * V(c.r) + 0.7152 * V(c.g) + 0.0722 * V(c.b);
        }

        private struct FacePaint
        {
            public Color32 Edge, Dk, Sh, Mid, Body, Hi, Top, CoveDk, Cove, CoveHi, Pin, PinDk, Scuff, Scuff2;
        }

        private static FacePaint Paint(bool night)
        {
            double d = night ? -0.17 : 0.0;
            Color32 F(double t) => At(PAINT, t + d);
            var f = new FacePaint
            {
                Edge = At(PAINT, 0), Dk = F(0.20), Sh = F(0.42), Mid = F(0.62), Body = F(0.78),
                Hi = F(0.92), Top = F(1),
                CoveDk = At(TRIM, night ? 0.10 : 0.25), Cove = At(TRIM, night ? 0.55 : 0.75),
                CoveHi = At(TRIM, night ? 0.80 : 1), Pin = At(COVE, night ? 0.55 : 1), PinDk = At(COVE, 0),
            };
            bool light = Lum(f.Body) > 0.34;
            f.Scuff = light ? f.Dk : f.Hi;      // wear = undercoat on light paint, bare primer on dark
            f.Scuff2 = light ? f.Sh : f.Mid;
            return f;
        }

        // ---- the baked ignition key (consoleRig.js:297-309), built once ---------------------------
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
            RigDrawUtil.ThickLine(_key, _keyPx, _keyPy, _keyPx, _keyPy - shaft, 4, STEEL[2]);
            RigDrawUtil.ThickLine(_key, _keyPx - 1, _keyPy, _keyPx - 1, _keyPy - shaft, 1, STEEL[4]);
            int by = _keyPy - shaft - bowH;
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0), by, bowW, bowH, 6, RUBBER[1]);
            RigDrawUtil.RRect(_key, _keyPx - DrawSurface.JsRound(bowW / 2.0) + 1, by + 1, bowW - 2,
                              DrawSurface.JsRound(bowH * 0.5), 5, RUBBER[3]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 4, RUBBER[0]);
            RigDrawUtil.Circle(_key, _keyPx, by + DrawSurface.JsRound(bowH * 0.5), 3, GRAPH[2]);
        }

        /// <summary>
        /// Paint the console dash chrome (everything but the four composited instruments) into
        /// <paramref name="s"/> (must be <see cref="HelmDashGeometry.W"/>×<see cref="HelmDashGeometry.H"/>).
        /// <paramref name="night"/> paints the backlit face (the whole-panel palette drop, the amber
        /// wash, the gauge backlights and the lamp haloes); deck/spot are the rig's working lights and
        /// stay off until a slice wires them. <paramref name="anchorFitted"/>/<paramref name="anchorDown"/>
        /// are the ground tackle's own bat — drawn only on a hull that carries a hook, lit amber while
        /// it is over the side.
        /// </summary>
        public static void Render(DrawSurface s, bool running, float drive, float rpm01, float fuel01,
                                  bool night = false, bool blink = false,
                                  bool anchorFitted = false, bool anchorDown = false)
        {
            EnsureKey();
            FacePaint f = Paint(night);
            bool lowFuel = fuel01 < 0.13f;
            s.Clear();

            // ---- windscreen + spotlight can on the rail (consoleRig.js:348-356) ----
            RigDrawUtil.RRect(s, 175, 26 + PAD, 250, 22, 5, RigDrawUtil.Hex("3a7680"), 0.30f);
            RigDrawUtil.RRect(s, 175, 26 + PAD, 250, 3, 2, RigDrawUtil.Hex("8fc9c4"), 0.35f);
            RigDrawUtil.ThickLine(s, 175, 27 + PAD, 425, 27 + PAD, 2, STEEL[2]);
            RigDrawUtil.ThickLine(s, 175, 47 + PAD, 425, 47 + PAD, 2, STEEL[1]);
            RigDrawUtil.RRect(s, HelmDashGeometry.SpotcanX, HelmDashGeometry.SpotcanY + PAD,
                              HelmDashGeometry.SpotcanW, HelmDashGeometry.SpotcanH, 5, GRAPH[1]);
            RigDrawUtil.RRect(s, HelmDashGeometry.SpotcanX, HelmDashGeometry.SpotcanY + PAD,
                              HelmDashGeometry.SpotcanW, 3, 2, GRAPH[3]);
            int scx = HelmDashGeometry.SpotcanX + HelmDashGeometry.SpotcanW - 4;
            int scy = HelmDashGeometry.SpotcanY + PAD + HelmDashGeometry.SpotcanH / 2;
            RigDrawUtil.Circle(s, scx, scy, 6, GRAPH[0]);
            RigDrawUtil.Circle(s, scx, scy, 5, SPOT_OFF);

            // ---- console body: she wears the boat's paint (consoleRig.js:361-384) ----
            int cx0 = HelmDashGeometry.ConsoleX, cy0 = HelmDashGeometry.ConsoleY + PAD;
            int cw = HelmDashGeometry.ConsoleW, chh = HelmDashGeometry.ConsoleH, cr = HelmDashGeometry.ConsoleR;
            RigDrawUtil.RRect(s, cx0 - 1, cy0 + 3, cw + 2, chh + 2, cr + 1, RigDrawUtil.Hex("060a0c"), 0.32f);
            RigDrawUtil.RRect(s, cx0, cy0, cw, chh, cr, f.Edge);
            RigDrawUtil.RRect(s, cx0 + 1, cy0 + 1, cw - 2, chh - 3, cr, f.Body);
            s.FillRect(cx0 + 3, cy0 + 64, cw - 6, chh - 78, f.Mid);            // lower face turns from the sky
            RigDrawUtil.RRect(s, cx0 + 2, cy0 + 2, cw - 4, 10, cr - 3, f.Hi);  // brow lip in the key light
            RigDrawUtil.RRect(s, cx0 + 2, cy0 + 2, cw - 4, 4, cr - 3, f.Top);
            RigDrawUtil.RRect(s, cx0 + 4, cy0 + 13, cw - 8, 3, 2, f.Sh);       // AO under the screen rail
            s.FillRect(cx0 + 4, cy0 + 16, cw - 8, 1, f.Mid);
            RigDrawUtil.RRect(s, cx0 + 2, cy0 + chh - 42, cw - 4, 30, 10, f.Sh);
            RigDrawUtil.RRect(s, cx0 + 3, cy0 + chh - 16, cw - 6, 12, 7, f.Dk);
            s.FillRect(cx0 + 4, cy0 + chh - 3, cw - 8, 1, f.Edge);
            {   // sheer band + gold cove pin (consoleRig.js:373-378)
                int bandY = cy0 + 55;
                s.FillRect(cx0 + 8, bandY - 1, cw - 16, 1, f.CoveDk);
                RigDrawUtil.RRect(s, cx0 + 8, bandY, cw - 16, 6, 2, f.Cove);
                s.FillRect(cx0 + 9, bandY + 1, cw - 18, 1, f.CoveHi);
                s.FillRect(cx0 + 8, bandY + 8, cw - 16, 1, f.Pin);
                s.FillRect(cx0 + 8, bandY + 9, cw - 16, 1, f.PinDk);
            }
            RigDrawUtil.Screw(s, cx0 + 11, cy0 + 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + cw - 11, cy0 + 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + 11, cy0 + chh - 11, 2, STEEL);
            RigDrawUtil.Screw(s, cx0 + cw - 11, cy0 + chh - 11, 2, STEEL);
            for (int i = 0; i < SCUFFS.GetLength(0); i++)
                s.FillRect(cx0 + SCUFFS[i, 0], cy0 + SCUFFS[i, 1], SCUFFS[i, 2], SCUFFS[i, 3],
                           SCUFFS[i, 4] != 0 ? f.Scuff2 : f.Scuff);
            if (night)
                RigDrawUtil.RRect(s, cx0 + 1, cy0 + 1, cw - 2, chh - 3, cr, RigDrawUtil.Hex("ffb055"), 0.055f);

            // (brow sounder cutout: S2's DepthRig mount — S2a draws nothing there, never a fake)

            // ---- gauges flanking the wheel (consoleRig.js:405-407) ----
            GaugeRpm(s, HelmDashGeometry.RpmCx, HelmDashGeometry.RpmCy + PAD, HelmDashGeometry.GaugeR, rpm01);
            GaugeFuel(s, HelmDashGeometry.FuelCx, HelmDashGeometry.FuelCy + PAD, HelmDashGeometry.GaugeR,
                      fuel01, lowFuel, blink);
            if (night)   // js:407 — the backlight goes on OVER the finished dials, never under them
            {
                GaugeNight(s, HelmDashGeometry.RpmCx, HelmDashGeometry.RpmCy + PAD, HelmDashGeometry.GaugeR);
                GaugeNight(s, HelmDashGeometry.FuelCx, HelmDashGeometry.FuelCy + PAD, HelmDashGeometry.GaugeR);
            }

            // ---- switch panel, left (consoleRig.js:410-419) ----
            int swx = HelmDashGeometry.SwX, swy = HelmDashGeometry.SwY + PAD;
            int sww = HelmDashGeometry.SwW, swh = HelmDashGeometry.SwH, swr = HelmDashGeometry.SwR;
            RigDrawUtil.RRect(s, swx - 3, swy + 1, sww + 6, swh + 6, swr + 2, RigDrawUtil.Hex("070b0d"), 0.26f);
            RigDrawUtil.RRect(s, swx - 2, swy - 2, sww + 4, swh + 4, swr + 1, RUBBER[0]);
            RigDrawUtil.RRect(s, swx, swy, sww, swh, swr, GRAPH[1]);
            RigDrawUtil.RRect(s, swx + 2, swy + 2, sww - 4, 6, swr - 3, GRAPH[3]);
            RigDrawUtil.Screw(s, swx + 8, swy + 8, 2, GRAPH);
            RigDrawUtil.Screw(s, swx + sww - 8, swy + 8, 2, GRAPH);
            DrawIgnition(s, HelmDashGeometry.StartCx, HelmDashGeometry.StartCy + PAD, HelmDashGeometry.StartR, running);
            Toggle(s, HelmDashGeometry.DeckX, HelmDashGeometry.DeckY + PAD, HelmDashGeometry.DeckW,
                   HelmDashGeometry.DeckH, HelmDashGeometry.DeckCx, HelmDashGeometry.DeckLampY + PAD,
                   false, TEAL, night);
            Toggle(s, HelmDashGeometry.SpotX, HelmDashGeometry.SpotY + PAD, HelmDashGeometry.SpotW,
                   HelmDashGeometry.SpotH, HelmDashGeometry.SpotCx, HelmDashGeometry.SpotLampY + PAD,
                   false, SPOT_ON, night);
            RigDrawUtil.TextC(s, "DECK", HelmDashGeometry.DeckCx, HelmDashGeometry.DeckLampY + PAD + 7, 1, TICKDIM);
            RigDrawUtil.TextC(s, "SPOT", HelmDashGeometry.SpotCx, HelmDashGeometry.SpotLampY + PAD + 7, 1, TICKDIM);
            // The GROUND TACKLE's own bat, midway between the two working lights — drawn ONLY on a
            // hull that actually carries a hook. A switch the boat cannot answer is the diegetic
            // version of a readout you have not earned, and the same rule refuses it (ADR 0039).
            if (anchorFitted)
            {
                Toggle(s, HelmDashGeometry.AnchX, HelmDashGeometry.AnchY + PAD, HelmDashGeometry.AnchW,
                       HelmDashGeometry.AnchH, HelmDashGeometry.AnchCx, HelmDashGeometry.AnchLampY + PAD,
                       anchorDown, AMBER, night);
                RigDrawUtil.TextC(s, "ANCH", HelmDashGeometry.AnchCx,
                                  HelmDashGeometry.AnchLampY + PAD + 7, 1, anchorDown ? AMBER : TICKDIM);
            }

            // ---- binnacle casing, right (consoleRig.js:422-439) ----
            int bx = HelmDashGeometry.BinnX, by = HelmDashGeometry.BinnY + PAD;
            int bw = HelmDashGeometry.BinnW, bh = HelmDashGeometry.BinnH, br = HelmDashGeometry.BinnR;
            RigDrawUtil.RRect(s, bx - 3, by + 1, bw + 6, bh + 6, br + 2, RigDrawUtil.Hex("070b0d"), 0.26f);
            RigDrawUtil.RRect(s, bx - 2, by - 2, bw + 4, bh + 4, br + 1, RUBBER[0]);
            RigDrawUtil.RRect(s, bx, by, bw, bh, br, GRAPH[1]);
            RigDrawUtil.RRect(s, bx + 2, by + 2, bw - 4, 8, br - 3, GRAPH[3]);
            RigDrawUtil.RRect(s, bx + 3, by + bh - 8, bw - 6, 5, 4, GRAPH[0]);
            RigDrawUtil.Screw(s, bx + 9, by + 10, 2, GRAPH);
            RigDrawUtil.Screw(s, bx + bw - 9, by + 10, 2, GRAPH);
            RigDrawUtil.Screw(s, bx + 9, by + bh - 10, 2, GRAPH);
            RigDrawUtil.Screw(s, bx + bw - 9, by + bh - 10, 2, GRAPH);
            {   // F / N / R detents, each with its own tell-tale (consoleRig.js:430-437)
                char gz = Mathf.Abs(drive) < 0.04f ? 'N' : (drive > 0f ? 'F' : 'R');
                void Lamp(int y, bool on, Color32 col)
                {
                    RigDrawUtil.Circle(s, 559, y + 2 + PAD, 3, GRAPH[0]);
                    RigDrawUtil.Circle(s, 559, y + 2 + PAD, 2, on ? col : LAMPOFF);
                    if (!on) return;
                    s.BlendRect(558, y + 1 + PAD, 1, 1, WHITE, 0.5f);
                    // the lit detent haloes in the dark (js:433) — only the ONE that is on
                    if (night) RigDrawUtil.Circle(s, 559, y + 2 + PAD, 6, col, 0.18f);
                }
                Lamp(300, gz == 'F', TEAL);
                Lamp(318, gz == 'N', GREEN[2]);
                Lamp(336, gz == 'R', RED[2]);
                RigDrawUtil.TextC(s, "F", 570, 300 + PAD, 1, gz == 'F' ? TEAL : TICKDIM);
                RigDrawUtil.TextC(s, "N", 570, 318 + PAD, 1, gz == 'N' ? RigDrawUtil.Hex("cfd9d6") : TICKDIM);
                RigDrawUtil.TextC(s, "R", 570, 336 + PAD, 1, gz == 'R' ? RED[2] : TICKDIM);
            }
            // emergency lanyard tab, lower-left (consoleRig.js:439)
            RigDrawUtil.RRect(s, bx + 8, by + bh - 30, 15, 12, 3, GRAPH[0]);
            RigDrawUtil.RRect(s, bx + 10, by + bh - 28, 11, 4, 2, RED[1]);

            // ---- the lever hub only — the lever composites on top (consoleRig.js:272-278, 442) ----
            int dpx = HelmDashGeometry.DrivePx, dpy = HelmDashGeometry.DrivePivotY + PAD;
            RigDrawUtil.Circle(s, dpx, dpy, 20, GRAPH[0]);
            RigDrawUtil.Circle(s, dpx, dpy, 17, GRAPH[2]);
            RigDrawUtil.Circle(s, dpx - 1, dpy - 2, 14, GRAPH[3]);
            RigDrawUtil.Ring(s, dpx, dpy, 20, 18, RUBBER[0]);
            RigDrawUtil.Screw(s, dpx, dpy, 3, GRAPH);

            // ---- steering column + boss (consoleRig.js:445-447; the WHEEL is WheelRigRender's) ----
            int wcx = HelmDashGeometry.WheelCx, wcy = HelmDashGeometry.WheelCy + PAD;
            RigDrawUtil.ThickLine(s, wcx, wcy, wcx, cy0 + chh - 6, 18, GRAPH[2]);
            RigDrawUtil.ThickLine(s, wcx - 5, wcy + 10, wcx - 5, cy0 + chh - 6, 3, GRAPH[4]);
            RigDrawUtil.Circle(s, wcx, wcy, 20, GRAPH[1]);
        }

        /// <summary>The painted centre cap — the one piece of helm hardware in her sheer colour,
        /// drawn OVER the wheel (consoleRig.js:449-454). The compositor calls this after the wheel.</summary>
        public static void PaintWheelCap(DrawSurface s, bool night = false)
        {
            FacePaint f = Paint(night);
            int wcx = HelmDashGeometry.WheelCx, wcy = HelmDashGeometry.WheelCy + PAD;
            RigDrawUtil.Circle(s, wcx, wcy, 8, f.CoveDk);
            RigDrawUtil.Circle(s, wcx, wcy, 7, f.Cove);
            RigDrawUtil.Circle(s, wcx - 2, wcy - 3, 3, f.CoveHi);
            RigDrawUtil.Circle(s, wcx, wcy, 2, STEEL[1]);
            s.FillRect(wcx - 6, wcy + 5, 12, 1, f.CoveDk);
        }

        // ---- gauges (consoleRig.js:172-221) --------------------------------------------------------
        private static void Bezel(DrawSurface s, int gx, int gy, int r)
        {
            RigDrawUtil.Circle(s, gx, gy + 2, r + 8, RigDrawUtil.Hex("070b0d"), 0.24f);   // contact shadow
            RigDrawUtil.Circle(s, gx, gy, r + 6, RUBBER[0]);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 1, GRAPH[3]);
            RigDrawUtil.Ring(s, gx, gy, r + 5, r + 1, GRAPH[5], gx - r - 6, gy - r - 6, (r + 6) * 2, r + 3);
            RigDrawUtil.Circle(s, gx, gy, r, FACE);
            RigDrawUtil.Ring(s, gx, gy, r, r - 2, RigDrawUtil.Hex("101a1d"));
            s.BlendRect(gx - r + 3, gy - r + 5, r, 2, RigDrawUtil.Hex("96b4b9"), 0.05f);
        }

        private static void RadialTick(DrawSurface s, int gx, int gy, double deg, double rOut, double rIn,
                                       double w, Color32 col)
        {
            RigDrawUtil.Dir(deg * DEG, out double dx, out double dy);
            RigDrawUtil.ThickLine(s, gx + dx * rIn, gy + dy * rIn, gx + dx * rOut, gy + dy * rOut, w, col);
        }

        private static void Needle(DrawSurface s, int r, double px, double py, double tx, double ty)
        {
            double dx = tx - px, dy = ty - py;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) len = 1.0;
            double ux = dx / len, uy = dy / len, tail = System.Math.Min(15.0, len * 0.3);
            RigDrawUtil.ThickLine(s, px, py, tx, ty, 3, NEEDLE);
            RigDrawUtil.ThickLine(s, px, py, px + ux * (len - 2), py + uy * (len - 2), 1, NEEDLE_HL);
            RigDrawUtil.ThickLine(s, px, py, px - ux * tail, py - uy * tail, 4, NEEDLE_DK);
            int hr = DrawSurface.JsRound(r * 0.14) + 1;
            int ipx = DrawSurface.JsRound(px), ipy = DrawSurface.JsRound(py);
            RigDrawUtil.Circle(s, ipx, ipy, hr, STEEL[1]);
            RigDrawUtil.Circle(s, ipx, ipy, hr - 1, STEEL[3]);
            RigDrawUtil.Circle(s, ipx - 1, ipy - 1, System.Math.Max(1, hr - 3), STEEL[4]);
        }

        private static void GaugeRpm(DrawSurface s, int gx, int gy, int r, float rpm01)
        {
            Bezel(s, gx, gy, r);
            for (int i = 0; i <= 12; i++)
            {
                double v = i / 12.0, deg = -135.0 + 270.0 * v;
                bool major = i % 2 == 0;
                RadialTick(s, gx, gy, deg, r - 3, major ? r - 12 : r - 8, major ? 2 : 1, major ? TICK : TICKDIM);
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
                                  n >= 5 ? RED[2] : TICK);
            }
            RigDrawUtil.TextC(s, "X100", gx, gy - 4, 1, TICKDIM);
            RigDrawUtil.TextC(s, "RPM", gx, gy + r - 18, 2, TEAL);
            RigDrawUtil.Circle(s, gx, gy + DrawSurface.JsRound(r * 0.42), 3, rpm01 > 0.86f ? RED[2] : LAMPOFF);
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
                bool major = i % 2 == 0;   // the JS's (i%1e9===0)||(i%2===0) reduces to this
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(i / 10.0), r - 3,
                           major ? r - 12 : r - 8, major ? 2 : 1, major ? TICK : TICKDIM);
            }
            for (int i = 0; i <= 3; i++)
                RadialTick(s, gx, gy, HelmDashGeometry.FuelPhiDeg(0.10 * i / 3.0), r - 3, r - 7, 2, RED[1]);
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "E", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, RED[2]);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(1) * DEG, out double dx, out double dy);
                double rr = r - 15;
                RigDrawUtil.TextC(s, "F", gx + dx * rr, DrawSurface.JsRound(gy + dy * rr - 3), 2, TICK);
            }
            {
                RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(0.5) * DEG, out double dx, out double dy);
                double rr = r - 16;
                s.FillRect(DrawSurface.JsRound(gx + dx * rr - 1), DrawSurface.JsRound(gy + dy * rr - 2), 2, 4, TICK);
            }
            RigDrawUtil.TextC(s, "FUEL", gx, gy + r - 16, 2, TEAL);
            RigDrawUtil.Circle(s, gx, py - 14, 3, low && blink ? AMBER : LAMPOFF);
            RigDrawUtil.Dir(HelmDashGeometry.FuelPhiDeg(fuel01) * DEG, out double fdx, out double fdy);
            Needle(s, r, gx, py, gx + fdx * rimR, gy + fdy * rimR);   // tip rides gy, pivot rides py (js:220)
        }

        /// <summary>The dial's amber backlight (consoleRig.js:223-237). TWO passes, as authored: a core
        /// clipped inside the glass, then an unclipped bloom that spills onto the panel around the
        /// bezel. Both additive — these dials are black glass, so light ADDS to them.</summary>
        private static void GaugeNight(DrawSurface s, int gx, int gy, int r)
        {
            s.AddRadial(gx - r, gy - r, r * 2, r * 2, gx, gy, 2, r, r - 1,
                        NightCoreT, NightCoreC, NightCoreA);
            int br = r + 18;                       // the source's fill rect; the gradient dies at r+16
            s.AddRadial(gx - br, gy - br, br * 2, br * 2, gx, gy, r * 0.55, r + 16, r + 16,
                        NightBloomT, NightBloomC, NightBloomA);
        }

        // ---- switches (consoleRig.js:281-323) ------------------------------------------------------
        // The night halo is authored per-lamp and only fires for a lamp that is ON (js:294-295). Deck
        // and spot are hard-off until a slice wires the working lights, so today it never draws — it
        // is here so wiring them is a one-line change and not a rediscovery of the rig source.
        private static void Toggle(DrawSurface s, int x, int y, int w, int h, int lampCx, int lampY,
                                   bool on, Color32 lampCol, bool night = false)
        {
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, h + 6, 5, GRAPH[0]);
            RigDrawUtil.RRect(s, x - 3, y - 3, w + 6, 2, 2, GRAPH[3]);
            RigDrawUtil.Screw(s, x - 1, y - 1, 2, GRAPH);
            RigDrawUtil.Screw(s, x + w + 1, y + h + 1, 2, GRAPH);
            RigDrawUtil.RRect(s, x, y, w, h, 4, RigDrawUtil.Hex("05080a"));
            int midY = y + h / 2, cx = x + w / 2;
            int ty = on ? y + 3 : midY + 1, by = on ? midY - 1 : y + h - 3;
            RigDrawUtil.RRect(s, cx - 4, ty, 8, by - ty, 3, STEEL[1]);
            RigDrawUtil.RRect(s, cx - 4, on ? ty : by - 6, 8, 6, 3, STEEL[3]);
            s.FillRect(cx - 2, on ? ty + 1 : by - 4, 3, 2, STEEL[4]);
            RigDrawUtil.Circle(s, cx, midY, 3, GRAPH[5]);
            RigDrawUtil.Circle(s, lampCx, lampY, 3, on ? lampCol : LAMPOFF);
            if (!on) return;
            s.BlendRect(lampCx - 1, lampY - 1, 2, 2, WHITE, 0.55f);
            if (night)
            {
                RigDrawUtil.Circle(s, lampCx, lampY, 6, lampCol, 0.20f);
                RigDrawUtil.Circle(s, lampCx, lampY, 10, lampCol, 0.09f);
            }
        }

        private static void DrawIgnition(DrawSurface s, int cx, int cy, int r, bool running)
        {
            RigDrawUtil.Circle(s, cx, cy, r, GRAPH[0]);
            RigDrawUtil.Circle(s, cx, cy, r - 2, GRAPH[3]);
            RigDrawUtil.Circle(s, cx - 1, cy - 2, r - 4, GRAPH[4]);
            RigDrawUtil.Ring(s, cx, cy, r - 1, r - 3, STEEL[3]);
            RigDrawUtil.Circle(s, cx, cy, 5, RigDrawUtil.Hex("05080a"));
            RigDrawUtil.Dir(HelmDashGeometry.StartOffDeg * DEG, out double ox, out double oy);
            RigDrawUtil.Dir(HelmDashGeometry.StartRunDeg * DEG, out double rx, out double ry);
            s.FillRect(DrawSurface.JsRound(cx + ox * (r + 3) - 1), DrawSurface.JsRound(cy + oy * (r + 3) - 1), 2, 2, TICKDIM);
            s.FillRect(DrawSurface.JsRound(cx + rx * (r + 3) - 1), DrawSurface.JsRound(cy + ry * (r + 3) - 1), 2, 2,
                       running ? GREEN[2] : TICKDIM);
            RigDrawUtil.BlitRotated(s, _key, _keyPx, _keyPy, cx, cy,
                                    running ? HelmDashGeometry.StartRunDeg : HelmDashGeometry.StartOffDeg);
            RigDrawUtil.Circle(s, cx, cy - r - 6, 3, running ? GREEN[2] : LAMPOFF);
            RigDrawUtil.TextC(s, "OFF", cx - r - 1, cy + r + 2, 1, TICKDIM);
            RigDrawUtil.TextC(s, "RUN", cx + r + 2, cy + r + 2, 1, running ? GREEN[2] : TICKDIM);
        }
    }
}
