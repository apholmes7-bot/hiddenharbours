using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// THE RADAR'S GLASS — a live C# port of <c>docs/art/rigs/ui/radar/Art/radarRig.js</c> (ADR 0025 S5,
    /// Option A: live renderers inside the no-JS fence). The rig is a continuous-state instrument — a
    /// rotating sweep over echoes that move — so there is nothing to bake and every pixel is drawn from
    /// the state the host hands in, the same contract <see cref="DepthRigRender"/>,
    /// <see cref="FishRigRender"/> and <see cref="NavRigRender"/> keep.
    ///
    /// <para><b>Faithful to the rig, complete — including what the instrument does not yet drive.</b>
    /// The EBL, the VRM and the guard zone are all transcribed. Nothing in this slice ARMS the guard (no
    /// pusher exposes it, and its alarm is another lane's), but a transcribed path with no caller is
    /// dead code that no test can honestly exercise, so the renderer tests drive it directly. The split
    /// is the one this arc has used throughout: the RENDERER is the rig's port, the HOST is what is
    /// fitted.</para>
    ///
    /// <para><b>One bearing convention.</b> Every angle that arrives here is already the game's compass
    /// bearing (0 = N, clockwise) — see <see cref="RadarRigGeometry"/> for why the rig's own
    /// <c>dir()</c> agrees with it and why no flip happens anywhere in the port.</para>
    ///
    /// <para><b>Perf (rule 7).</b> One reused <see cref="DrawSurface"/> owned by the host, no allocation
    /// on the paint path, and the host repaints only on change-detection. The most expensive thing here
    /// is the sweep's 24-line additive fan; it is drawn only while the set is transmitting.</para>
    /// </summary>
    public static class RadarRigRender
    {
        /// <summary>Native sprite size — <c>radarRig.js:14</c>. PORTRAIT, the finder's exact footprint.</summary>
        public const int W = RadarRigGeometry.W, H = RadarRigGeometry.H;

        /// <summary>
        /// Speed (m/s) that maps to the rig's own authored trail factor of 1.0.
        ///
        /// <para>Calibrated FROM the rig rather than invented: <c>radarRig.js:141-142</c> puts working
        /// craft at <c>spd</c> 0.8 and 1.1, and an ambient punt's cruise is around 4 m/s, so 5 m/s puts a
        /// working boat at 0.8 — exactly where the rig's own scene draws one. A boat at rest has no
        /// trail at all, which is the honest picture of a hove-to hull.</para>
        /// </summary>
        public const float TrailReferenceSpeedMps = 5f;

        // ---- palettes (radarRig.js:18-48) --------------------------------------------------------
        private static readonly Color32[] CASE =
            RigDrawUtil.Ramp("#12171b", "#1b2228", "#28323a", "#3a4750", "#4f5e68", "#6b7c86");
        private static readonly Color32[] RESIN = RigDrawUtil.Ramp("#050708", "#0c1013", "#141b1f");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("#7fd6c9");
        private static readonly Color32 TEAK = RigDrawUtil.Hex("#c9a86a");
        private static readonly Color32 BLUE = RigDrawUtil.Hex("#5b93b8");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);
        private static readonly Color32 BLACK = new Color32(0, 0, 0, 255);

        /// <summary>One phosphor palette — day green scope or night amber, <c>radarRig.js:25-48</c>.
        /// A struct of colours rather than two parallel arrays so a field cannot be read off the wrong
        /// palette.</summary>
        private readonly struct Pal
        {
            public readonly Color32 Bg, Bg2, RingDim, RingMaj, Spoke;
            public readonly Color32 Tick, TickMaj, TickNum, Ink, Dim;
            public readonly Color32 Sweep, SweepEdge, Glow, Head, North;
            public readonly Color32 Ebl, Vrm, Guard, GuardFill, Land, LandDim, Rain, Flock;
            public readonly Color32 Strip, StripFg, StripDim, Plate;
            public readonly Color32 KeyPlate, KeyEdge, Legend, Mark;
            public readonly Color32[] Echo;
            // Alphas the sources carry as rgba(); kept beside their colours so a caller cannot pair the
            // wrong two.
            public readonly float RingDimA, RingMajA, SpokeA, GuardFillA, PlateA, RainA;

            public Pal(string bg, string bg2, Color32 ringDim, float ringDimA, Color32 ringMaj,
                       float ringMajA, Color32 spoke, float spokeA,
                       string tick, string tickMaj, string tickNum, string ink, string dim,
                       string sweep, string sweepEdge, string glow, string head, string north,
                       string ebl, string vrm, string guard, Color32 guardFill, float guardFillA,
                       string[] echo, string land, string landDim, Color32 rain, float rainA, string flock,
                       string strip, string stripFg, string stripDim, Color32 plate, float plateA,
                       string keyPlate, string keyEdge, string legend, string mark)
            {
                Bg = RigDrawUtil.Hex(bg); Bg2 = RigDrawUtil.Hex(bg2);
                RingDim = ringDim; RingDimA = ringDimA; RingMaj = ringMaj; RingMajA = ringMajA;
                Spoke = spoke; SpokeA = spokeA;
                Tick = RigDrawUtil.Hex(tick); TickMaj = RigDrawUtil.Hex(tickMaj);
                TickNum = RigDrawUtil.Hex(tickNum); Ink = RigDrawUtil.Hex(ink); Dim = RigDrawUtil.Hex(dim);
                Sweep = RigDrawUtil.Hex(sweep); SweepEdge = RigDrawUtil.Hex(sweepEdge);
                Glow = RigDrawUtil.Hex(glow); Head = RigDrawUtil.Hex(head); North = RigDrawUtil.Hex(north);
                Ebl = RigDrawUtil.Hex(ebl); Vrm = RigDrawUtil.Hex(vrm); Guard = RigDrawUtil.Hex(guard);
                GuardFill = guardFill; GuardFillA = guardFillA;
                Echo = RigDrawUtil.Ramp(echo);
                Land = RigDrawUtil.Hex(land); LandDim = RigDrawUtil.Hex(landDim);
                Rain = rain; RainA = rainA; Flock = RigDrawUtil.Hex(flock);
                Strip = RigDrawUtil.Hex(strip); StripFg = RigDrawUtil.Hex(stripFg);
                StripDim = RigDrawUtil.Hex(stripDim); Plate = plate; PlateA = plateA;
                KeyPlate = RigDrawUtil.Hex(keyPlate); KeyEdge = RigDrawUtil.Hex(keyEdge);
                Legend = RigDrawUtil.Hex(legend); Mark = RigDrawUtil.Hex(mark);
            }
        }

        // radarRig.js:25-36 — day: green phosphor scope.
        private static readonly Pal DAY = new Pal(
            "#05130d", "#020a07",
            new Color32(90, 200, 150, 255), 0.16f, new Color32(120, 220, 170, 255), 0.34f,
            new Color32(90, 200, 150, 255), 0.08f,
            "#3f9068", "#7fd6a8", "#9fe6c0", "#cfeede", "#5f8f78",
            "#6bf0a0", "#d6ffe6", "#6ff0a0", "#e6f4ec", "#e0554a",
            "#6fccef", "#e6b53f", "#e0554a", new Color32(224, 85, 74, 255), 0.12f,
            new[] { "#2f9e57", "#7bc24a", "#e6b53f", "#e0554a" }, "#c33b2b", "#7c2a20",
            new Color32(120, 196, 224, 255), 0.55f, "#8fe0b0",
            "#04120c", "#8fe0b0", "#3c6a54", new Color32(4, 14, 10, 255), 0.55f,
            "#245a4a", "#123528", "#dff3e6", "#bfe6cf");

        // radarRig.js:37-48 — night: warm amber (the fleet's night idiom).
        private static readonly Pal NIGHT = new Pal(
            "#0c0702", "#050200",
            new Color32(230, 170, 70, 255), 0.14f, new Color32(255, 190, 90, 255), 0.30f,
            new Color32(230, 170, 70, 255), 0.07f,
            "#7a5a1c", "#c79a3f", "#ffd07a", "#ffe0a0", "#8a6a30",
            "#ffcf6a", "#fff0c8", "#ffbe5a", "#ffe6bc", "#ff6f5c",
            "#7fd0ef", "#ffd77a", "#ff6f5c", new Color32(255, 111, 92, 255), 0.12f,
            new[] { "#7a7a24", "#b59a2c", "#e0902f", "#e0554a" }, "#c85030", "#7a2a16",
            new Color32(200, 170, 120, 255), 0.5f, "#ffd77a",
            "#150d02", "#ffd77a", "#6b501c", new Color32(10, 6, 2, 255), 0.6f,
            "#3a2c0c", "#201604", "#ffe0a0", "#ffd77a");

        /// <summary>Paint the standalone 480×660 sprite — <c>radarRig.js:442-446</c>.</summary>
        public static void RenderStandalone(DrawSurface s, in RadarRigState st, IReadOnlyList<RadarBlip> blips)
        {
            s.Clear();
            Render(s, RadarRigGeometry.InsetX, RadarRigGeometry.InsetY,
                   RadarRigGeometry.InsetW, RadarRigGeometry.InsetH, in st, blips);
        }

        /// <summary>
        /// The whole instrument fitted to a box — <c>radarRig.js:392-439</c> (<c>drawUnit</c>, which the
        /// dash rigs consume as <c>paintInto</c>).
        /// </summary>
        public static void Render(DrawSurface s, int x, int y, int ww, int hh,
                                  in RadarRigState st, IReadOnlyList<RadarBlip> blips)
        {
            Pal p = st.Night ? NIGHT : DAY;
            int fs = RadarRigGeometry.FontScale(hh);
            RadarRigLayout L = RadarRigGeometry.Layout(x, y, ww, hh);

            // ---- case body (graphite, shared with the fleet) — js:407-412 ------------------------
            int cr = System.Math.Max(4, DrawSurface.JsRound(hh * 0.09));
            RigDrawUtil.RRect(s, x, y, ww, hh, cr, RESIN[0]);
            RigDrawUtil.RRect(s, x + 1, y + 1, ww - 2, hh - 2, cr - 1, CASE[1]);
            RigDrawUtil.RRect(s, x + 2, y + 2, ww - 4, System.Math.Max(2, DrawSurface.JsRound(hh * 0.08)),
                              cr - 2, CASE[3]);
            RigDrawUtil.RRect(s, x + 2, y + hh - System.Math.Max(2, DrawSurface.JsRound(hh * 0.05)),
                              ww - 4, System.Math.Max(2, DrawSurface.JsRound(hh * 0.04)), 3, CASE[0]);

            // ---- recessed LCD — js:414-427 -------------------------------------------------------
            int lr = System.Math.Max(3, DrawSurface.JsRound(L.Lcd.H * 0.05));
            RigDrawUtil.RRect(s, L.Lcd.X - 3, L.Lcd.Y - 3, L.Lcd.W + 6, L.Lcd.H + 6, lr + 1, RESIN[0]);
            RigDrawUtil.RRect(s, L.Lcd.X - 2, L.Lcd.Y - 2, L.Lcd.W + 4, L.Lcd.H + 4, lr + 1, RESIN[2]);
            // The source clips the glass to a rounded path; we draw the rounded fill instead and rely on
            // every pane below staying inside it. The ONE deliberate deviation is the same one the
            // sounder documented: canvas anti-aliases that clip and we do not (the rigs' own no-AA rule).
            RigDrawUtil.RRect(s, L.Lcd.X, L.Lcd.Y, L.Lcd.W, L.Lcd.H, lr, p.Bg2);

            StatusStrip(s, in L, in st, in p, fs);
            ScopeView(s, in L, in st, blips, in p, fs);
            if (L.Data.H >= 5 * (fs >= 3 ? 2 : 1) + 6 && fs >= 2) DataPanel(s, in L, in st, blips, in p, fs);

            s.BlendRect(L.Lcd.X + 3, L.Lcd.Y + 2, L.Lcd.W - 6, 1, WHITE, 0.06f);   // glass glare, js:427

            // ---- side pushers — js:430-432 -------------------------------------------------------
            Key(s, L.Button0, "MODE", Glyph.Ring, in p, fs, st.Night);
            Key(s, L.Button1, "RANGE", Glyph.Up, in p, fs, st.Night);
            Key(s, L.Button2, "RANGE", Glyph.Down, in p, fs, st.Night);

            // ---- brand strip (original wordmark) — js:435-438 -------------------------------------
            int bs = System.Math.Max(1, fs - 1);
            int by = L.Brand.Y + DrawSurface.JsRound((L.Brand.H - 5.0 * bs) / 2.0);
            RigDrawUtil.Text(s, "RD-4", L.Brand.X + 1, by, bs, TEAL);
            RigDrawUtil.TextC(s, "HARBOUR", x + ww / 2.0, by, bs, TEAK);
            RigDrawUtil.Text(s, "RADAR", L.Brand.X + L.Brand.W - RigDrawUtil.TextW("RADAR", bs), by, bs, BLUE);
        }

        // ---- one pusher key (js:186-199) ---------------------------------------------------------

        private enum Glyph { Ring, Up, Down }

        private static void Key(DrawSurface s, RigRect b, string label, Glyph glyph,
                                in Pal p, int fs, bool lit)
        {
            RigDrawUtil.RRect(s, b.X, b.Y, b.W, b.H,
                              System.Math.Max(3, DrawSurface.JsRound(b.H * 0.24)), CASE[0]);
            int ix = b.X + 2, iy = b.Y + 2, iw = b.W - 4, ih = b.H - 4;
            int r = System.Math.Max(2, DrawSurface.JsRound(ih * 0.22));
            RigDrawUtil.RRect(s, ix, iy, iw, ih, r, p.KeyEdge);
            RigDrawUtil.RRect(s, ix, iy, iw, ih - 2, r, p.KeyPlate);
            RigDrawUtil.RRect(s, ix + 2, iy + 2, iw - 4,
                              System.Math.Max(1, DrawSurface.JsRound(ih * 0.22)), r - 1, WHITE, 0.14f);

            double cx = b.X + b.W / 2.0;
            int ls = System.Math.Max(1, fs - 1);
            RigDrawUtil.Text(s, label, DrawSurface.JsRound(cx - RigDrawUtil.TextW(label, ls) / 2.0),
                             DrawSurface.JsRound(iy + ih * 0.24), ls, p.Legend);

            int dcy = DrawSurface.JsRound(iy + ih * 0.70);
            int sz = System.Math.Max(2, ls);
            int icx = DrawSurface.JsRound(cx);
            if (glyph == Glyph.Up)
                for (int i = 0; i < sz + 1; i++) s.FillRect(icx - i, dcy - sz + i, i * 2 + 1, 1, p.Mark);
            else if (glyph == Glyph.Down)
                for (int i = 0; i < sz + 1; i++)
                    s.FillRect(icx - (sz - i), dcy - sz + i, (sz - i) * 2 + 1, 1, p.Mark);
            else
            {
                RigDrawUtil.Ring(s, icx, dcy, sz, sz - 1, p.Mark);
                s.FillRect(icx - 1, dcy - sz - 1, 2, 2, p.Mark);
            }

            // The rig lights the keys at night (js:198) — an amber wash over the plate.
            if (lit) RigDrawUtil.RRect(s, ix, iy, iw, ih - 2, r, new Color32(255, 190, 60, 255), 0.10f);
        }

        // ---- status strip (js:202-226) ------------------------------------------------------------

        private static int Bars(DrawSurface s, int x, int baseY, float v, in Pal p, int unit)
        {
            int n = DrawSurface.JsRound(4f * Mathf.Clamp01(v));
            for (int i = 0; i < 4; i++)
            {
                int bh = 2 + i * System.Math.Max(2, unit);
                s.FillRect(x + i * (unit + 1), baseY - bh, unit, bh, i < n ? p.StripFg : p.StripDim);
            }
            return x + 4 * (unit + 1);
        }

        private static void StatusStrip(DrawSurface s, in RadarRigLayout L, in RadarRigState st,
                                        in Pal p, int fs)
        {
            RigRect S = L.Status;
            s.FillRect(S.X, S.Y, S.W, S.H, p.Strip);
            s.BlendRect(S.X, S.Y, S.W, 1, WHITE, 0.05f);

            int midY = S.Y + DrawSurface.JsRound((S.H - 5.0 * fs) / 2.0);
            int baseY = S.Y + S.H - 3;
            int unit = System.Math.Max(1, fs);

            // range (left)
            string range = RadarRigGeometry.FormatNM(st.RangeNM) + "NM";
            int x = S.X + 4;
            RigDrawUtil.Text(s, range, x, midY, fs, p.StripFg);
            x += RigDrawUtil.TextW(range, fs) + 8;

            // orientation + TX (centre-ish)
            string ori = st.HeadUp ? "H-UP" : "N-UP";
            RigDrawUtil.Text(s, ori, x, midY, fs, p.StripFg);
            x += RigDrawUtil.TextW(ori, fs) + 6;
            Color32 txCol = st.Transmitting ? p.StripFg : p.StripDim;
            RigDrawUtil.Circle(s, x + 2, midY + 2 * fs, System.Math.Max(1, fs), txCol);
            if (fs >= 2) RigDrawUtil.Text(s, st.Transmitting ? "TX" : "STBY", x + 4 + fs, midY, fs, txCol);

            // gain / sea / rain (right)
            if (fs < 2) return;
            int rx = S.X + S.W - 4 - 4 * (unit + 1) * 3 - RigDrawUtil.TextW("G", fs) * 3 - 12;
            RigDrawUtil.Text(s, "G", rx, midY, fs, p.StripDim);
            rx = Bars(s, rx + RigDrawUtil.TextW("G", fs) + 2, baseY, st.Gain, in p, unit) + 6;
            RigDrawUtil.Text(s, "S", rx, midY, fs, p.StripDim);
            rx = Bars(s, rx + RigDrawUtil.TextW("S", fs) + 2, baseY, st.Sea, in p, unit) + 6;
            RigDrawUtil.Text(s, "R", rx, midY, fs, p.StripDim);
            Bars(s, rx + RigDrawUtil.TextW("R", fs) + 2, baseY, st.Rain, in p, unit);
        }

        // ---- the PPI scope (js:270-358) -----------------------------------------------------------

        private static void ScopeView(DrawSurface s, in RadarRigLayout L, in RadarRigState st,
                                      IReadOnlyList<RadarBlip> blips, in Pal p, int fs)
        {
            int cx = L.ScopeCx, cy = L.ScopeCy, R = L.ScopeR;
            if (R <= 0) return;

            // glass backdrop + radial deepen (js:273-275)
            RigDrawUtil.Circle(s, cx, cy, R, p.Bg);
            s.OverRadial(cx - R, cy - R, R * 2, R * 2, cx, cy, 2, R, R,
                         new[] { 0.0, 0.7, 1.0 }, new[] { WHITE, BLACK, BLACK },
                         new[] { 0.04f, 0f, 0.35f });

            // --- everything from here to the rim ticks is inside the rig's arc clip (js:277) -------

            // sea-clutter speckle near centre (js:280-282)
            if (st.Sea > 0.02f)
            {
                int n = DrawSurface.JsRound(st.Sea * 220f);
                for (int i = 0; i < n; i++)
                {
                    float a = 0.10f + 0.18f * (float)RadarRigGeometry.HashRnd(i * 1.7);
                    double rr = System.Math.Pow(RadarRigGeometry.HashRnd(i * 2.9), 1.7) * R * 0.5;
                    double aa = RadarRigGeometry.HashRnd(i * 4.3) * 360.0 * RigDrawUtil.DEG;
                    s.BlendRect(DrawSurface.JsRound(cx + System.Math.Sin(aa) * rr),
                                DrawSurface.JsRound(cy - System.Math.Cos(aa) * rr), 1, 1, p.Echo[0], a);
                }
            }
            // rain-clutter haze band (js:284-286)
            if (st.Rain > 0.02f)
            {
                float a = 0.08f + 0.14f * st.Rain;
                int n = DrawSurface.JsRound(st.Rain * 260f);
                for (int i = 0; i < n; i++)
                {
                    double rr = (0.3 + 0.7 * RadarRigGeometry.HashRnd(i * 3.3)) * R;
                    double aa = RadarRigGeometry.HashRnd(i * 6.1) * 360.0 * RigDrawUtil.DEG;
                    s.BlendRect(DrawSurface.JsRound(cx + System.Math.Sin(aa) * rr),
                                DrawSurface.JsRound(cy - System.Math.Cos(aa) * rr), 1, 1,
                                p.Rain, a * p.RainA);
                }
            }

            // range rings (js:289)
            for (int i = 1; i <= st.Rings; i++)
            {
                int rr = DrawSurface.JsRound((double)R * i / st.Rings);
                bool major = i == st.Rings;
                RingAlpha(s, cx, cy, rr, major ? p.RingMaj : p.RingDim, major ? p.RingMajA : p.RingDimA);
            }
            // bearing spokes every 30° (js:291-292)
            for (int a = 0; a < 360; a += 30)
            {
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                RigDrawUtil.ThickLine(s, cx + dx * 8, cy + dy * 8, cx + dx * (R - 2), cy + dy * (R - 2),
                                      1, p.Spoke, p.SpokeA);
            }

            // guard-zone sector (js:294-306)
            if (st.GuardOn) GuardZone(s, cx, cy, R, in st, in p);

            // sweep — phosphor trailing wedge + bright leading edge (js:308-318)
            if (st.Transmitting)
            {
                const int spanDeg = 64, steps = 24;
                for (int i = 0; i < steps; i++)
                {
                    double a = st.SweepDeg - spanDeg * ((double)i / steps);
                    float al = 0.16f * (1f - (float)i / steps);
                    RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                    RigDrawUtil.ThickLineAdd(s, cx, cy, cx + dx * R, cy + dy * R, 2, p.Glow, al);
                }
                RigDrawUtil.Dir(st.SweepDeg * RigDrawUtil.DEG, out double sx, out double sy);
                RigDrawUtil.ThickLine(s, cx, cy, cx + sx * R, cy + sy * R, 2, p.Sweep);
                RigDrawUtil.ThickLine(s, cx, cy, cx + sx * (R - 1), cy + sy * (R - 1), 1, p.SweepEdge);
            }

            // targets (js:320-322)
            bool guardHit = false;
            if (blips != null)
            {
                for (int i = 0; i < blips.Count; i++)
                {
                    RadarBlip b = blips[i];
                    Echo(s, cx, cy, R, in b, in st, in p);
                    if (st.GuardOn && !guardHit)
                        guardHit = RadarRigGeometry.InGuard(b.BearingTrue,
                                                            st.RangeNM > 0f ? b.RangeNM / st.RangeNM : 0f,
                                                            st.GuardFrom, st.GuardTo,
                                                            st.GuardNear, st.GuardFar);
                }
            }

            // guard alarm flash — any target inside the zone (js:324-327)
            if (st.GuardOn && st.Blink && guardHit) RingAlpha(s, cx, cy, R - 1, p.Guard, 0.5f);

            // EBL (js:329-331) + VRM (js:332-333)
            if (st.EblOn)
            {
                float sa = RadarRigGeometry.ScreenAngleOf(st.EblBearing, st.HeadUp, st.HeadingDeg);
                RigDrawUtil.Dir(sa * RigDrawUtil.DEG, out double dx, out double dy);
                for (int r = 6; r < R; r += 6)
                    s.FillRect(DrawSurface.JsRound(cx + dx * r), DrawSurface.JsRound(cy + dy * r), 2, 2, p.Ebl);
            }
            if (st.VrmOn && st.RangeNM > 0f)
            {
                double rr = Mathf.Clamp01(st.VrmNM / st.RangeNM) * R;
                for (int a = 0; a < 360; a += 7)
                {
                    RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                    s.FillRect(DrawSurface.JsRound(cx + dx * rr), DrawSurface.JsRound(cy + dy * rr), 1, 1, p.Vrm);
                }
            }

            // heading line + own ship (js:335-337)
            float hla = st.HeadUp ? 0f : st.HeadingDeg;
            RigDrawUtil.Dir(hla * RigDrawUtil.DEG, out double hx, out double hy);
            RigDrawUtil.ThickLine(s, cx, cy, cx + hx * R, cy + hy * R, 1, p.Head);

            // --- unclipped again (js:338) ----------------------------------------------------------

            // bearing scale ticks around the rim (js:341-344)
            for (int a = 0; a < 360; a += 5)
            {
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                bool major = a % 30 == 0;
                int rI = major ? R - 6 : R - 3;
                RigDrawUtil.ThickLine(s, cx + dx * rI, cy + dy * rI, cx + dx * R, cy + dy * R, 1,
                                      major ? p.TickMaj : p.Tick);
            }
            if (fs >= 2)
                for (int a = 0; a < 360; a += 30)
                {
                    RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                    int rr = R - 13;
                    RigDrawUtil.TextC(s, RadarRigGeometry.FormatDeg(a), cx + dx * rr,
                                      DrawSurface.JsRound(cy + dy * rr - 2), 1, p.TickNum);
                }

            // north marker on the rim (js:346-348)
            {
                float na = RadarRigGeometry.ScreenAngleOf(0f, st.HeadUp, st.HeadingDeg);
                RigDrawUtil.Dir(na * RigDrawUtil.DEG, out double dx, out double dy);
                RigDrawUtil.ThickLine(s, cx + dx * (R - 2), cy + dy * (R - 2),
                                      cx + dx * (R + 3), cy + dy * (R + 3), 3, p.North);
                if (fs >= 2)
                    RigDrawUtil.TextC(s, "N", cx + dx * (R + 8),
                                      DrawSurface.JsRound(cy + dy * (R + 8) - 2), 1, p.North);
            }

            // own-ship centre + heading pip (js:350-353)
            RigDrawUtil.Circle(s, cx, cy, System.Math.Max(2, DrawSurface.JsRound(R * 0.02)), p.Head);
            RigDrawUtil.ThickLine(s, cx, cy, cx + hx * System.Math.Max(6.0, R * 0.10),
                                  cy + hy * System.Math.Max(6.0, R * 0.10), 2, p.Head);

            // range tag at the bottom of the scope (js:355-357)
            if (fs >= 2)
            {
                string tag = RadarRigGeometry.FormatNM(st.RangeNM) + " NM";
                int tw = RigDrawUtil.TextW(tag, fs);
                s.BlendRect(DrawSurface.JsRound(cx - tw / 2.0 - 3), cy + R - 5 * fs - 6, tw + 6, 5 * fs + 4,
                            p.Plate, p.PlateA);
                RigDrawUtil.TextC(s, tag, cx, cy + R - 5 * fs - 4, fs, p.Ink);
            }
        }

        /// <summary>The guard sector's translucent fill and its four edges — <c>js:294-306</c>. The fill
        /// is a scanline polygon rather than the source's additive path fill: an additive wash over the
        /// near-black scope would barely register, and the sector's whole job is to be a visible box
        /// drawn on the water.</summary>
        private static void GuardZone(DrawSurface s, int cx, int cy, int R, in RadarRigState st, in Pal p)
        {
            float near = Mathf.Clamp01(st.GuardNear) * R, far = Mathf.Clamp01(st.GuardFar) * R;
            float a0 = st.GuardFrom, a1 = st.GuardTo;
            if (a1 < a0) a1 += 360f;

            int steps = System.Math.Max(2, (int)((a1 - a0) / 2f) + 1);
            int n = (steps + 1) * 2;
            var xs = new double[n];
            var ys = new double[n];
            int k = 0;
            for (int i = 0; i <= steps; i++)
            {
                double a = a0 + (a1 - a0) * ((double)i / steps);
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                xs[k] = cx + dx * far; ys[k] = cy + dy * far; k++;
            }
            for (int i = steps; i >= 0; i--)
            {
                double a = a0 + (a1 - a0) * ((double)i / steps);
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                xs[k] = cx + dx * near; ys[k] = cy + dy * near; k++;
            }
            FillPolyAlpha(s, xs, ys, k, p.GuardFill, p.GuardFillA);

            // edges (js:304-305)
            foreach (float a in new[] { st.GuardFrom, st.GuardTo })
            {
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                RigDrawUtil.ThickLine(s, cx + dx * near, cy + dy * near, cx + dx * far, cy + dy * far,
                                      1, p.Guard);
            }
            RingSeg(s, cx, cy, near, st.GuardFrom, st.GuardTo, p.Guard);
            RingSeg(s, cx, cy, far, st.GuardFrom, st.GuardTo, p.Guard);
        }

        /// <summary>js:359 — a stippled arc segment.</summary>
        private static void RingSeg(DrawSurface s, int cx, int cy, double rad, float a0, float a1, Color32 c)
        {
            float e = a1;
            if (e < a0) e += 360f;
            for (double a = a0; a <= e; a += 2)
            {
                RigDrawUtil.Dir(a * RigDrawUtil.DEG, out double dx, out double dy);
                s.FillRect(DrawSurface.JsRound(cx + dx * rad), DrawSurface.JsRound(cy + dy * rad), 1, 1, c);
            }
        }

        // ---- echo targets (js:228-267) ------------------------------------------------------------

        /// <summary>
        /// One return. The shapes are gameplay-defined by KIND, exactly as the rig's remarks say — this
        /// method IS <c>radarRig.js:231-267</c>.
        ///
        /// <para><b>Clipped to the scope's disc.</b> The rig draws inside a canvas <c>arc</c> clip; a
        /// blip sitting on the selected range would otherwise spill its half-extent past the rim and
        /// over the bearing scale. Only the echoes need it (everything else the scope draws is bounded
        /// by R by construction), which is why the disc-clipped primitives are local to this file
        /// rather than a fourth clip mode on <see cref="RigDrawUtil"/>.</para>
        /// </summary>
        private static void Echo(DrawSurface s, int cx, int cy, int R, in RadarBlip t,
                                 in RadarRigState st, in Pal p)
        {
            float frac = st.RangeNM > 0f ? t.RangeNM / st.RangeNM : 0f;
            RadarRigGeometry.BlipXY(cx, cy, R, t.BearingTrue, frac, st.HeadUp, st.HeadingDeg,
                                    out double gx, out double gy, out float sa, out bool vis);
            if (!vis) return;

            float a = RadarRigGeometry.SweepBrightness(sa, st.SweepDeg, st.Transmitting)
                    * (0.55f + 0.45f * Mathf.Clamp01(st.Gain));
            a = Mathf.Clamp01(a);
            double half = RadarRigGeometry.BlipHalf(R, t.Size);
            float size = t.Size <= 0f ? 1f : t.Size;

            switch (t.Kind)
            {
                case RadarEchoKind.Land:
                {
                    double w = System.Math.Max(2.0, half * 1.4);
                    EllipseDisc(s, gx, gy, w, System.Math.Max(2.0, half * 0.85), p.Land, a, cx, cy, R);
                    EllipseDisc(s, gx - half * 0.7, gy + half * 0.3, System.Math.Max(2.0, half * 0.8),
                                System.Math.Max(1.0, half * 0.55), p.Land, a, cx, cy, R);
                    EllipseDisc(s, gx + half * 0.75, gy - half * 0.25, System.Math.Max(2.0, half * 0.7),
                                System.Math.Max(1.0, half * 0.5), p.LandDim, a, cx, cy, R);
                    EllipseDisc(s, gx, gy, w + 2, System.Math.Max(2.0, half * 0.95), p.LandDim,
                                Mathf.Clamp01(a * 0.5f), cx, cy, R);
                    break;
                }
                case RadarEchoKind.Buoy:
                {
                    CircleDisc(s, gx, gy, System.Math.Max(1, DrawSurface.JsRound(half * 0.5)),
                               p.Echo[1], a, cx, cy, R);
                    RingDisc(s, gx, gy, System.Math.Max(2, DrawSurface.JsRound(half * 0.9)),
                             p.Echo[1], Mathf.Clamp01(a * 0.6f), cx, cy, R);
                    break;
                }
                case RadarEchoKind.Rain:
                    Stipple(s, gx, gy, DrawSurface.JsRound(10f + size * 18f), half * 1.9,
                            t, p.Rain, a * p.RainA, cx, cy, R);
                    break;
                case RadarEchoKind.Flock:
                    Stipple(s, gx, gy, DrawSurface.JsRound(6f + size * 10f), half * 1.2,
                            t, p.Flock, a, cx, cy, R);
                    break;
                default:   // vessel — solid strong return (js:253-262)
                {
                    Color32 col = p.Echo[RadarRigGeometry.StrengthIndex(size)];
                    EllipseDisc(s, gx, gy, System.Math.Max(2.0, half * 0.9),
                                System.Math.Max(1.0, half * 0.6), col, a, cx, cy, R);
                    PxDisc(s, DrawSurface.JsRound(gx), DrawSurface.JsRound(gy), p.SweepEdge, 1f, cx, cy, R);

                    if (st.Trails && t.HasTrail)
                    {
                        float ma = RadarRigGeometry.ScreenAngleOf(t.CourseDeg, st.HeadUp, st.HeadingDeg);
                        RigDrawUtil.Dir(ma * RigDrawUtil.DEG, out double mx, out double my);
                        double step = R * 0.05 * t.TrailScale;
                        for (int k = 1; k <= 5; k++)
                        {
                            float ta = Mathf.Clamp01(a * (1f - k / 6f) * 0.7f);
                            EllipseDisc(s, gx - mx * step * k, gy - my * step * k,
                                        System.Math.Max(1.0, half * 0.5), System.Math.Max(1.0, half * 0.35),
                                        col, ta, cx, cy, R);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>The rain/flock stipple clouds (js:245-252) — the rig's own deterministic hash, so
        /// the speckle is identical every repaint instead of crawling.</summary>
        private static void Stipple(DrawSurface s, double gx, double gy, int n, double rad,
                                    in RadarBlip t, Color32 c, float alpha, int cx, int cy, int R)
        {
            for (int i = 0; i < n; i++)
            {
                double rr = RadarRigGeometry.HashRnd(i * 3.1 + t.BearingTrue) * rad;
                double aa = RadarRigGeometry.HashRnd(i * 7.7 + t.RangeNM) * 360.0 * RigDrawUtil.DEG;
                PxDisc(s, DrawSurface.JsRound(gx + System.Math.Sin(aa) * rr),
                       DrawSurface.JsRound(gy - System.Math.Cos(aa) * rr), c, alpha, cx, cy, R);
            }
        }

        // ---- data panel (js:366-389) --------------------------------------------------------------

        private static void DataPanel(DrawSurface s, in RadarRigLayout L, in RadarRigState st,
                                      IReadOnlyList<RadarBlip> blips, in Pal p, int fs)
        {
            RigRect D = L.Data;
            s.FillRect(D.X, D.Y, D.W, D.H, p.Strip);
            s.FillRect(D.X, D.Y, D.W, 1, p.StripDim);

            int sc = fs >= 3 ? 2 : 1;
            int lh = System.Math.Max(9, 5 * sc + 4);
            double colW = D.W / 2.0;
            const int pad = 6;

            int count = blips?.Count ?? 0;
            bool alarm = false;
            if (st.GuardOn && blips != null)
                for (int i = 0; i < blips.Count && !alarm; i++)
                    alarm = RadarRigGeometry.InGuard(blips[i].BearingTrue,
                                                     st.RangeNM > 0f ? blips[i].RangeNM / st.RangeNM : 0f,
                                                     st.GuardFrom, st.GuardTo, st.GuardNear, st.GuardFar);

            // js:370-379 — eight label/value rows in two columns.
            var labels = new[] { "HDG", "EBL", "VRM", "GAIN", "SEA", "RAIN", "TGT", "GUARD" };
            var values = new[]
            {
                RadarRigGeometry.FormatDeg(st.HeadingDeg) + "° " + RadarRigGeometry.Cardinal8(st.HeadingDeg),
                st.EblOn ? RadarRigGeometry.FormatDeg(st.EblBearing) + "°" : "---",
                st.VrmOn ? RadarRigGeometry.FormatNM(st.VrmNM) + " NM" : "---",
                DrawSurface.JsRound(st.Gain * 100f) + "%",
                DrawSurface.JsRound(st.Sea * 100f) + "%",
                DrawSurface.JsRound(st.Rain * 100f) + "%",
                count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                st.GuardOn ? (alarm ? "ALARM" : "SET") : "OFF",
            };

            int y = D.Y + 4;
            for (int i = 0; i < labels.Length; i++)
            {
                int col = i % 2, row = i / 2;
                int x = D.X + pad + (int)(col * colW);
                int yy = y + row * lh;
                if (yy + 5 * sc > D.Y + D.H) break;
                RigDrawUtil.Text(s, labels[i], x, yy, sc, p.Dim);
                bool isAlarm = i == 7 && alarm;
                RigDrawUtil.Text(s, values[i], x + DrawSurface.JsRound(colW * 0.44), yy, sc,
                                 isAlarm ? p.Guard : p.Ink);
            }
        }

        // ---- primitives the radar needs that the shared layer does not carry ----------------------
        // Two families: an alpha-aware ring (the rig's ringOutline under a globalAlpha), and the
        // DISC-CLIPPED shapes the echo painter needs. Local rather than on RigDrawUtil because the disc
        // clip has exactly one caller — the echoes — and everything else the scope draws is bounded by
        // the scope's own radius by construction. The scanline maths is the shared layer's, unchanged.

        private static void RingAlpha(DrawSurface s, int cx, int cy, int rad, Color32 c, float alpha)
        {
            if (rad < 1) return;
            int rI = rad - 1;
            for (int dy = -rad; dy <= rad; dy++)
            {
                int xo = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                if (System.Math.Abs(dy) < rI)
                {
                    int xi = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rI * rI - dy * dy)));
                    s.BlendRect(cx - xo, cy + dy, xo - xi, 1, c, alpha);
                    s.BlendRect(cx + xi, cy + dy, xo - xi, 1, c, alpha);
                }
                else s.BlendRect(cx - xo, cy + dy, 2 * xo + 1, 1, c, alpha);
            }
        }

        /// <summary>A single pixel, dropped only if it is inside the scope's disc.</summary>
        private static void PxDisc(DrawSurface s, int x, int y, Color32 c, float alpha,
                                   int ccx, int ccy, int cr)
        {
            long dx = x - ccx, dy = y - ccy;
            if (dx * dx + dy * dy > (long)cr * cr) return;
            if (alpha >= 1f) s.FillRect(x, y, 1, 1, c);
            else s.BlendRect(x, y, 1, 1, c, alpha);
        }

        /// <summary>A horizontal span, trimmed to the scope's disc on this row.</summary>
        private static void SpanDisc(DrawSurface s, int x, int y, int w, Color32 c, float alpha,
                                     int ccx, int ccy, int cr)
        {
            if (w <= 0) return;
            int dy = y - ccy;
            long inside = (long)cr * cr - (long)dy * dy;
            if (inside < 0) return;                                   // this row misses the disc entirely
            int hx = (int)System.Math.Floor(System.Math.Sqrt(inside));
            int x0 = System.Math.Max(x, ccx - hx);
            int x1 = System.Math.Min(x + w, ccx + hx + 1);
            if (x1 <= x0) return;
            if (alpha >= 1f) s.FillRect(x0, y, x1 - x0, 1, c);
            else s.BlendRect(x0, y, x1 - x0, 1, c, alpha);
        }

        private static void EllipseDisc(DrawSurface s, double cxD, double cyD, double rxD, double ryD,
                                        Color32 c, float alpha, int ccx, int ccy, int cr)
        {
            int cx = DrawSurface.JsRound(cxD), cy = DrawSurface.JsRound(cyD);
            int rx = System.Math.Max(1, DrawSurface.JsRound(rxD)), ry = System.Math.Max(1, DrawSurface.JsRound(ryD));
            for (int dy = -ry; dy <= ry; dy++)
            {
                int dx = (int)System.Math.Floor(rx * System.Math.Sqrt(
                    System.Math.Max(0.0, 1.0 - (double)(dy * dy) / (ry * ry))));
                SpanDisc(s, cx - dx, cy + dy, 2 * dx + 1, c, alpha, ccx, ccy, cr);
            }
        }

        private static void CircleDisc(DrawSurface s, double cxD, double cyD, int rad,
                                       Color32 c, float alpha, int ccx, int ccy, int cr)
        {
            int cx = DrawSurface.JsRound(cxD), cy = DrawSurface.JsRound(cyD);
            for (int dy = -rad; dy <= rad; dy++)
            {
                int dx = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                SpanDisc(s, cx - dx, cy + dy, 2 * dx + 1, c, alpha, ccx, ccy, cr);
            }
        }

        private static void RingDisc(DrawSurface s, double cxD, double cyD, int rad,
                                     Color32 c, float alpha, int ccx, int ccy, int cr)
        {
            int cx = DrawSurface.JsRound(cxD), cy = DrawSurface.JsRound(cyD);
            int rI = System.Math.Max(0, rad - 1);
            for (int dy = -rad; dy <= rad; dy++)
            {
                int xo = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                if (System.Math.Abs(dy) < rI)
                {
                    int xi = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rI * rI - dy * dy)));
                    SpanDisc(s, cx - xo, cy + dy, xo - xi, c, alpha, ccx, ccy, cr);
                    SpanDisc(s, cx + xi, cy + dy, xo - xi, c, alpha, ccx, ccy, cr);
                }
                else SpanDisc(s, cx - xo, cy + dy, 2 * xo + 1, c, alpha, ccx, ccy, cr);
            }
        }

        /// <summary><see cref="RigDrawUtil.FillPoly"/> with a constant alpha — the guard sector's wash.</summary>
        private static void FillPolyAlpha(DrawSurface s, double[] xs, double[] ys, int n,
                                          Color32 c, float alpha)
        {
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
            }
            int y0 = DrawSurface.JsRound(minY), y1 = DrawSurface.JsRound(maxY);
            for (int y = y0; y <= y1; y++)
            {
                double yc = y + 0.5;
                double lo = double.MaxValue, hi = double.MinValue;
                for (int i = 0; i < n; i++)
                {
                    double ay = ys[i], byy = ys[(i + 1) % n];
                    if ((ay <= yc) != (byy <= yc))
                    {
                        double xx = xs[i] + (yc - ay) / (byy - ay) * (xs[(i + 1) % n] - xs[i]);
                        if (xx < lo) lo = xx;
                        if (xx > hi) hi = xx;
                    }
                }
                if (lo > hi) continue;
                int xa = DrawSurface.JsRound(lo), xb = DrawSurface.JsRound(hi);
                s.BlendRect(xa, y, System.Math.Max(1, xb - xa), 1, c, alpha);
            }
        }
    }
}
