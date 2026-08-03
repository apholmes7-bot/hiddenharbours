using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>The signals the sounder's glass draws from — the rig's <c>render(opts)</c> bag
    /// (<c>depthRig.js:205-207</c>), as a value so the host can change-detect it.</summary>
    public readonly struct DepthRigState
    {
        public readonly float Depth;     // metres (the transducer reading)
        public readonly bool Feet;       // display in feet
        public readonly bool Night;      // amber backlight vs day panel
        public readonly bool Armed;      // shallow alarm armed
        public readonly float Alarm;     // shallow set-point, metres
        public readonly float TempC;     // water temperature
        public readonly bool Blink;      // blink phase — flashes the SHALLOW flag when triggered

        public DepthRigState(float depth, bool feet, bool night, bool armed, float alarm, float tempC,
                             bool blink)
        {
            Depth = depth; Feet = feet; Night = night; Armed = armed; Alarm = alarm; TempC = tempC;
            Blink = blink;
        }

        /// <summary>The rig's own trigger — <c>armed &amp;&amp; depth &lt;= alarm</c> (depthRig.js:160).</summary>
        public bool Triggered => Armed && Depth <= Alarm;
    }

    /// <summary>
    /// Live C# port of the depth sounder's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/depth-finder/Art/depthRig.js</c> (ADR 0025, Option A). Method-by-method
    /// mirror of <c>paint</c>/<c>drawUnit</c>/<c>lcdContent</c> and their primitives; every constant
    /// cites its source line. Pure geometry + formatting live next door in
    /// <see cref="DepthRigGeometry"/> so the hit boxes and the LCD strings are testable without a
    /// canvas.
    ///
    /// <para><b>Box-parameterised, like the source.</b> <see cref="DrawUnit"/> renders the whole
    /// instrument crisply at ANY size, so the same code draws the standalone card now and the
    /// flush-mounted brow instrument when S2a/S6 composite a console dash around it. The sounder does
    /// NOT own the cutout forever — the fish finder (S3) drops into the same box.</para>
    ///
    /// <para><b>Two deliberate deviations from the source, both consequences of the no-AA rule</b>
    /// (<see cref="DrawSurface"/>): (1) the LCD's gradient follows the same integer row insets the
    /// rounded rect uses rather than an anti-aliased <c>Path2D</c> clip, and (2) the 7-segment bars are
    /// scan-converted by pixel-centre coverage instead of an anti-aliased polygon fill. Both keep the
    /// hard-edged pixel look the rigs are drawn in; neither moves a shape by more than the sub-pixel
    /// coverage canvas would have feathered.</para>
    /// </summary>
    public static class DepthRigRender
    {
        /// <summary>Standalone sprite size — depthRig.js:11.</summary>
        public const int W = DepthRigGeometry.W, H = DepthRigGeometry.H;

        // ---- palettes (depthRig.js:14-23) --------------------------------------------------------
        private static readonly Color32[] CASE = Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] RESIN = Ramp("050708", "0c1013", "141b1f");
        private static readonly Color32 TEAL = Hex("7fd6c9"), TEAK = Hex("c9a86a");
        private static readonly Color32[] RED = Ramp("7c2a20", "b83b2e", "e0554a");
        private static readonly Color32 BrandBlue = Hex("5b93b8");     // depthRig.js:256
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 BannerText = Hex("fff2ee");    // depthRig.js:192
        private static readonly Color32 Bloom = Hex("ffbe3d");         // depthRig.js:216-217

        /// <summary>One LCD palette — DAY is a positive grey-green panel, NIGHT an amber backlight
        /// (depthRig.js:20-23). <c>Off</c> carries its own alpha because the source states it as an
        /// <c>rgba()</c> string, and the un-lit segments really are a faint wash, not a solid.</summary>
        private readonly struct Pal
        {
            public readonly Color32 Bg1, Bg2, Seg, Off, Dim, KeyPlate, KeyEdge, Legend, Mark;
            public readonly float OffAlpha;
            public readonly bool HasBloom;

            public Pal(string bg1, string bg2, string seg, Color32 off, float offAlpha, string dim,
                       string keyPlate, string keyEdge, string legend, string mark, bool hasBloom)
            {
                Bg1 = Hex(bg1); Bg2 = Hex(bg2); Seg = Hex(seg); Off = off; OffAlpha = offAlpha;
                Dim = Hex(dim); KeyPlate = Hex(keyPlate); KeyEdge = Hex(keyEdge);
                Legend = Hex(legend); Mark = Hex(mark); HasBloom = hasBloom;
            }
        }

        private static readonly Pal DAY = new Pal("cbd2c4", "aeb6a6", "1c2119",
            new Color32(26, 32, 22, 255), 0.07f, "54604f", "2b5a76", "173648", "e2eef6", "bfe0f0", false);

        private static readonly Pal NIGHT = new Pal("4a3305", "2b1d04", "ffc247",
            new Color32(255, 182, 64, 255), 0.11f, "c7942f", "3a2c0c", "201604", "ffe0a0", "ffd77a", true);

        // ---- 3x5 bitmap font (depthRig.js:26-47) -------------------------------------------------
        private static readonly System.Collections.Generic.Dictionary<char, string[]> F = BuildFont();
        private static readonly string[] Blank = { "...", "...", "...", "...", "..." };

        // 7-segment map (depthRig.js:89-90)
        private static readonly System.Collections.Generic.Dictionary<char, string> SEG =
            new System.Collections.Generic.Dictionary<char, string>
            {
                { '0', "abcdef" }, { '1', "bc" },     { '2', "abged" },   { '3', "abgcd" },
                { '4', "fgbc" },   { '5', "afgcd" },  { '6', "afgedc" },  { '7', "abc" },
                { '8', "abcdefg" }, { '9', "abfgcd" }, { '-', "g" },      { ' ', "" },
            };

        // ---- the public entry points --------------------------------------------------------------

        /// <summary>Paint the standalone sprite (depthRig.js:260-264) into <paramref name="s"/>, which
        /// must be <see cref="W"/>×<see cref="H"/>.</summary>
        public static void Render(DrawSurface s, in DepthRigState o)
        {
            s.Clear();
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DrawUnit(s, mount.X, mount.Y, mount.W, mount.H, in o);
        }

        /// <summary>The whole instrument, fitted to a mount rect (depthRig.js:203-257). Public so a
        /// console dash can flush-mount it in its brow without going through the standalone sprite.</summary>
        public static void DrawUnit(DrawSurface s, int x, int y, int ww, int hh, in DepthRigState o)
        {
            Pal p = o.Night ? NIGHT : DAY;
            int fs = hh >= 200 ? 3 : hh >= 112 ? 2 : 1;                 // depthRig.js:209
            DepthRigLayout L = DepthRigGeometry.Layout(x, y, ww, hh);

            // night bloom behind the glass — depthRig.js:214-219
            if (p.HasBloom)
            {
                double gcx = L.Lcd.X + L.Lcd.W / 2.0, gcy = L.Lcd.Y + L.Lcd.H / 2.0;
                s.BlendRadial(x - 6, y - 6, ww + 12, hh + 12, gcx, gcy,
                              L.Lcd.H * 0.1, L.Lcd.W * 0.85, Bloom, 0.34f);
            }

            // case body — depthRig.js:222-226
            int cr = Max(4, DrawSurface.JsRound(hh * 0.11));
            RRect(s, x, y, ww, hh, cr, RESIN[0]);
            RRect(s, x + 1, y + 1, ww - 2, hh - 2, cr - 1, CASE[1]);
            RRect(s, x + 2, y + 2, ww - 4, Max(2, DrawSurface.JsRound(hh * 0.10)), cr - 2, CASE[3]);
            RRect(s, x + 2, y + hh - Max(2, DrawSurface.JsRound(hh * 0.06)), ww - 4,
                  Max(2, DrawSurface.JsRound(hh * 0.05)), 3, CASE[0]);

            // recessed LCD — depthRig.js:229-241
            int lr = Max(3, DrawSurface.JsRound(L.Lcd.H * 0.10));
            RRect(s, L.Lcd.X - 3, L.Lcd.Y - 3, L.Lcd.W + 6, L.Lcd.H + 6, lr + 1, RESIN[0]);
            RRect(s, L.Lcd.X - 2, L.Lcd.Y - 2, L.Lcd.W + 4, L.Lcd.H + 4, lr + 1, RESIN[2]);
            RRect(s, L.Lcd.X, L.Lcd.Y, L.Lcd.W, L.Lcd.H, lr, p.Bg1);
            // The gradient the source clips to a rounded Path2D — same rows, same insets, no AA.
            RRectVGradient(s, L.Lcd.X, L.Lcd.Y, L.Lcd.W, L.Lcd.H, lr, p.Bg1, p.Bg2);
            s.BlendRect(L.Lcd.X + 3, L.Lcd.Y + 2, L.Lcd.W - 6, 2, White, 0.10f);   // top glare

            LcdContent(s, in L.Lcd, in o, in p, fs);

            // alarm border flash across the whole glass — depthRig.js:244
            if (o.Triggered && o.Blink)
                RRectStroke(s, L.Lcd.X, L.Lcd.Y, L.Lcd.W, L.Lcd.H, lr, RED[2]);

            // side pushers — depthRig.js:247-250
            Key(s, in L.Button0, "SET", Glyph.Set, in p, fs, o.Night);
            Key(s, in L.Button1, "ALARM", Glyph.Up, in p, fs, o.Night);
            Key(s, in L.Button2, "ALARM", Glyph.Down, in p, fs, o.Night);

            // brand strip (original wordmark) — depthRig.js:253-256
            int bs = Max(1, fs - 1);
            int by = L.Brand.Y + DrawSurface.JsRound((L.Brand.H - 5 * bs) / 2.0);
            Text(s, "DS-2", L.Brand.X + 1, by, bs, TEAL, 1f);
            TextC(s, "HARBOUR", x + ww / 2.0, by, bs, TEAK);
            Text(s, "DEPTH", L.Brand.X + L.Brand.W - TextW("DEPTH", bs), by, bs, BrandBlue, 1f);
        }

        // ---- LCD content (depthRig.js:158-200) ----------------------------------------------------

        private static void LcdContent(DrawSurface s, in RigRect lcd, in DepthRigState o, in Pal p, int fs)
        {
            int m = Max(4, DrawSurface.JsRound(lcd.H * 0.08));
            bool triggered = o.Triggered;

            // top row: DEPTH label (left) + alarm indicator (right)
            Text(s, "DEPTH", lcd.X + m, lcd.Y + m, fs, p.Seg, 1f);

            string alTxt = fs >= 2 ? "AL " + DepthRigGeometry.FmtSet(o.Alarm, o.Feet) : "AL";
            int alScale = Max(1, fs - 1);
            int alW = TextW(alTxt, alScale);
            // depthRig.js:165-166 collapses to: the set-point text is P.dim except on a lit alarm flash.
            Color32 alColor = triggered && o.Blink ? RED[2] : p.Dim;
            Text(s, alTxt, lcd.X + lcd.W - m - alW - (fs + 2), lcd.Y + m, alScale, alColor, 1f);

            {   // the little alarm lamp — unarmed it is the faint OFF wash, not a solid (depthRig.js:167-168)
                int k = Max(2, fs);
                Color32 lampC = triggered ? (o.Blink ? RED[2] : RED[0]) : (o.Armed ? p.Dim : p.Off);
                float lampA = triggered || o.Armed ? 1f : p.OffAlpha;
                s.BlendRect(lcd.X + lcd.W - m - k, lcd.Y + m, k, k, lampC, lampA);
            }

            // main 7-seg depth number, centred in the upper body — depthRig.js:171-181
            string str = DepthRigGeometry.FmtDepth(o.Depth, o.Feet);
            int dh = DrawSurface.JsRound(lcd.H * 0.46);
            int dw = DrawSurface.JsRound(dh * 0.58);
            int gap = Max(3, DrawSurface.JsRound(dw * 0.30));
            int dpW = Max(2, DrawSurface.JsRound(dw * 0.26));
            int total = NumW(str, dw, gap, dpW);
            int nx = DrawSurface.JsRound(lcd.X + (lcd.W - total) / 2.0 - lcd.W * 0.02);
            int ny = DrawSurface.JsRound(lcd.Y + lcd.H * 0.30);
            Color32 numCol = triggered && o.Blink ? RED[2] : p.Seg;
            DrawNum(s, str, nx, ny, dw, dh, gap, dpW, numCol, p.Off, p.OffAlpha);

            // unit word (bottom-right) — depthRig.js:184-186
            string unit = o.Feet ? "FEET" : "METRES";
            int us = Max(1, fs - 1);
            Text(s, unit, lcd.X + lcd.W - m - TextW(unit, us), lcd.Y + lcd.H - m - 5 * us, us, p.Seg, 1f);

            // water temp (bottom-left) — or the SHALLOW banner when triggered — depthRig.js:189-199
            int ts = Max(1, fs - 1);
            int rowY = lcd.Y + lcd.H - m - 5 * ts;
            if (triggered)
            {
                int bw = TextW("SHALLOW", ts) + 8, bx = lcd.X + m - 2, by = rowY - 3;
                if (o.Blink)
                {
                    RRect(s, bx, by, bw, 5 * ts + 6, 2, RED[1]);
                    Text(s, "SHALLOW", bx + 4, by + 3, ts, BannerText, 1f);
                }
                else Text(s, "SHALLOW", bx + 4, by + 3, ts, RED[2], 1f);
            }
            else
            {
                string tv = fs >= 2
                    ? DepthRigGeometry.FixedOne(o.TempC)
                    : DepthRigGeometry.JsRoundToString(o.TempC);
                double tx = Text(s, tv, lcd.X + m, rowY, ts, p.Dim, 1f);
                int k = Max(2, ts);
                DegMark(s, tx + ts + 1, rowY, k, p.Dim, p.Bg2);
                Text(s, "C", tx + ts + 1 + k + ts, rowY, ts, p.Dim, 1f);
            }
        }

        // ---- one pusher key (depthRig.js:139-155) -------------------------------------------------

        private enum Glyph { Set, Up, Down }

        private static void Key(DrawSurface s, in RigRect b, string label, Glyph glyph, in Pal p,
                                int fs, bool lit)
        {
            RRect(s, b.X, b.Y, b.W, b.H, Max(3, DrawSurface.JsRound(b.H * 0.24)), CASE[0]);   // recess
            int ix = b.X + 2, iy = b.Y + 2, iw = b.W - 4, ih = b.H - 4;
            int r = Max(2, DrawSurface.JsRound(ih * 0.22));
            RRect(s, ix, iy, iw, ih, r, p.KeyEdge);
            RRect(s, ix, iy, iw, ih - 2, r, p.KeyPlate);
            RRectA(s, ix + 2, iy + 2, iw - 4, Max(1, DrawSurface.JsRound(ih * 0.22)), r - 1, White, 0.14f);

            double cx = b.X + b.W / 2.0;
            Text(s, label, DrawSurface.JsRound(cx - TextW(label, fs) / 2.0),
                 DrawSurface.JsRound(iy + ih * 0.20), fs, p.Legend, 1f);

            int dcy = DrawSurface.JsRound(iy + ih * 0.68);
            int sz = Max(2, fs);
            if (glyph == Glyph.Up)
                for (int i = 0; i < sz + 1; i++) FillRectF(s, cx - i, dcy - sz + i, i * 2 + 1, 1, p.Mark, 1f);
            else if (glyph == Glyph.Down)
                for (int i = 0; i < sz + 1; i++)
                    FillRectF(s, cx - (sz - i), dcy - sz + i, (sz - i) * 2 + 1, 1, p.Mark, 1f);
            else
            {
                int q = sz + 1;
                for (int i = -q; i <= q; i++)
                {
                    int wd = q - System.Math.Abs(i);
                    FillRectF(s, cx - wd, dcy + i, wd * 2 + 1, 1, p.Mark, 1f);
                }
            }

            // night: a warm wash over the key plate (depthRig.js:154 — the rrect is what actually draws)
            if (lit) RRectA(s, ix, iy, iw, ih - 2, r, new Color32(255, 190, 60, 255), 0.10f);
        }

        // ---- text (depthRig.js:48-58) -------------------------------------------------------------

        private static int TextW(string str, int s) => str.Length * (3 * s + s) - s;

        /// <summary>Draw a 3×5-bitmap string; returns the pen x AFTER the last glyph minus one gap
        /// unit, exactly as the source's <c>text()</c> does (its return feeds the °C layout).</summary>
        private static double Text(DrawSurface surface, string str, double x, double y, int s,
                                   Color32 col, float alpha)
        {
            double cx = x;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = char.ToUpperInvariant(str[i]);
                string[] g = F.TryGetValue(ch, out string[] found) ? found : Blank;
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 3; c++)
                        if (g[r][c] == '#') FillRectF(surface, cx + c * s, y + r * s, s, s, col, alpha);
                cx += 3 * s + s;
            }
            return cx - s;
        }

        private static void TextC(DrawSurface s, string str, double cx, double y, int scale, Color32 col)
            => Text(s, str, DrawSurface.JsRound(cx - TextW(str, scale) / 2.0), y, scale, col, 1f);

        /// <summary>The little superscript degree ring (depthRig.js:58).</summary>
        private static void DegMark(DrawSurface s, double x, double y, int k, Color32 col, Color32 bg)
        {
            FillRectF(s, x, y, k, k, col, 1f);
            FillRectF(s, x + 1, y + 1, Max(1, k - 2), Max(1, k - 2), bg, 1f);
        }

        // ---- crisp primitives (depthRig.js:62-86) -------------------------------------------------

        private static int RowInset(int v, int h, int r)
        {
            if (v < r)
            {
                int dy = r - v;
                return r - (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, r * r - dy * dy)));
            }
            if (v >= h - r)
            {
                int dy = v - (h - r) + 1;
                return r - (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, r * r - dy * dy)));
            }
            return 0;
        }

        private static void RRect(DrawSurface s, double x, double y, double w, double h, int r, Color32 fill)
            => RRectA(s, x, y, w, h, r, fill, 1f);

        private static void RRectA(DrawSurface s, double x, double y, double w, double h, int r,
                                   Color32 fill, float alpha)
        {
            int xi = DrawSurface.JsRound(x), yi = DrawSurface.JsRound(y);
            int wi = DrawSurface.JsRound(w), hi = DrawSurface.JsRound(h);
            r = Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(wi, hi) / 2.0)));
            for (int v = 0; v < hi; v++)
            {
                int i = RowInset(v, hi, r);
                s.BlendRect(xi + i, yi + v, wi - 2 * i, 1, fill, alpha);
            }
        }

        /// <summary>The rounded rect filled with a TOP→BOTTOM colour ramp — the source's linear gradient
        /// clipped to the glass path (depthRig.js:232-240), rasterised on the same row insets.</summary>
        private static void RRectVGradient(DrawSurface s, int x, int y, int w, int h, int r,
                                           Color32 top, Color32 bottom)
        {
            r = Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(w, h) / 2.0)));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                // canvas samples a gradient at the pixel centre
                double t = h > 0 ? (v + 0.5) / h : 0.0;
                s.FillRect(x + i, y + v, w - 2 * i, 1, DrawSurface.Lerp(top, bottom, t));
            }
        }

        /// <summary>1px rounded outline (depthRig.js:72-78).</summary>
        private static void RRectStroke(DrawSurface s, int x, int y, int w, int h, int r, Color32 fill)
        {
            r = Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(w, h) / 2.0)));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                if (v == 0 || v == h - 1) s.FillRect(x + i, y + v, w - 2 * i, 1, fill);
                else
                {
                    s.FillRect(x + i, y + v, 1, 1, fill);
                    s.FillRect(x + w - 1 - i, y + v, 1, 1, fill);
                }
            }
        }

        // ---- 7-segment numerals (depthRig.js:91-116) ----------------------------------------------

        /// <summary>Measure a numeric string that may carry a decimal point (depthRig.js:109).</summary>
        private static int NumW(string str, int dw, int gap, int dpW)
        {
            int w = 0;
            for (int i = 0; i < str.Length; i++) w += (str[i] == '.' ? dpW : dw) + gap;
            return Max(0, w - gap);
        }

        private static void DrawNum(DrawSurface s, string str, double x, double y, int dw, int dh,
                                    int gap, int dpW, Color32 onCol, Color32 offCol, float offAlpha)
        {
            double cx = x;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if (ch == '.')
                {
                    s.FillRect(DrawSurface.JsRound(cx), DrawSurface.JsRound(y + dh - dpW), dpW, dpW, onCol);
                    cx += dpW + gap;
                }
                else
                {
                    Seg7(s, DrawSurface.JsRound(cx), y, dw, dh, ch, onCol, offCol, offAlpha);
                    cx += dw + gap;
                }
            }
        }

        private static void Seg7(DrawSurface s, double x, double y, double w, double h, char ch,
                                 Color32 onCol, Color32 offCol, float offAlpha)
        {
            string on = SEG.TryGetValue(ch, out string found) ? found : "";
            double t = System.Math.Max(3.0, w * 0.19);
            double midY = y + h / 2.0, hlen = w - t * 0.7, vlen = h / 2.0 - t * 0.7;

            HBar(s, x + w / 2.0, y, hlen, t, on, 'a', onCol, offCol, offAlpha);
            HBar(s, x + w / 2.0, midY, hlen, t, on, 'g', onCol, offCol, offAlpha);
            HBar(s, x + w / 2.0, y + h, hlen, t, on, 'd', onCol, offCol, offAlpha);
            VBar(s, x, y + h / 4.0, vlen, t, on, 'f', onCol, offCol, offAlpha);
            VBar(s, x + w, y + h / 4.0, vlen, t, on, 'b', onCol, offCol, offAlpha);
            VBar(s, x, y + h * 3.0 / 4.0, vlen, t, on, 'e', onCol, offCol, offAlpha);
            VBar(s, x + w, y + h * 3.0 / 4.0, vlen, t, on, 'c', onCol, offCol, offAlpha);
        }

        /// <summary>The horizontal segment: a flat hexagon whose left/right edges slope 1:1 into the
        /// tips (depthRig.js:91-93). Scan-converted by pixel-centre coverage — no AA.</summary>
        private static void HBar(DrawSurface s, double cx, double cy, double len, double t, string on,
                                 char key, Color32 onCol, Color32 offCol, float offAlpha)
        {
            bool lit = on.IndexOf(key) >= 0;
            Color32 col = lit ? onCol : offCol;
            float alpha = lit ? 1f : offAlpha;
            double half = t / 2.0, l = len / 2.0;
            SpanBounds(cy - half, cy + half, out int y0, out int y1);
            for (int yy = y0; yy <= y1; yy++)
            {
                double dy = System.Math.Abs(yy + 0.5 - cy);
                double xl = cx - l + dy, xr = cx + l - dy;
                if (xr <= xl) continue;
                SpanBounds(xl, xr, out int x0, out int x1);
                if (x1 >= x0) s.BlendRect(x0, yy, x1 - x0 + 1, 1, col, alpha);
            }
        }

        /// <summary>The vertical segment: a tall hexagon, flat-sided in the middle, tapering to points
        /// (depthRig.js:94-96).</summary>
        private static void VBar(DrawSurface s, double cx, double cy, double len, double t, string on,
                                 char key, Color32 onCol, Color32 offCol, float offAlpha)
        {
            bool lit = on.IndexOf(key) >= 0;
            Color32 col = lit ? onCol : offCol;
            float alpha = lit ? 1f : offAlpha;
            double half = t / 2.0, l = len / 2.0;
            SpanBounds(cy - l, cy + l, out int y0, out int y1);
            for (int yy = y0; yy <= y1; yy++)
            {
                double dy = System.Math.Abs(yy + 0.5 - cy);
                double hw = System.Math.Min(half, l - dy);
                if (hw <= 0.0) continue;
                SpanBounds(cx - hw, cx + hw, out int x0, out int x1);
                if (x1 >= x0) s.BlendRect(x0, yy, x1 - x0 + 1, 1, col, alpha);
            }
        }

        /// <summary>A <c>fillRect</c> that may land on fractional coordinates (the pusher glyphs centre
        /// on a half-pixel when the button column is an odd width). Fills every pixel whose CENTRE lies
        /// in the half-open rect — canvas's coverage rule with the anti-aliasing switched off.</summary>
        private static void FillRectF(DrawSurface s, double x, double y, double w, double h,
                                      Color32 col, float alpha)
        {
            SpanBounds(x, x + w, out int x0, out int x1);
            SpanBounds(y, y + h, out int y0, out int y1);
            if (x1 < x0 || y1 < y0) return;
            s.BlendRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1, col, alpha);
        }

        /// <summary>Pixel indices whose centres lie in the half-open interval [lo, hi).</summary>
        private static void SpanBounds(double lo, double hi, out int i0, out int i1)
        {
            i0 = (int)System.Math.Ceiling(lo - 0.5);
            i1 = (int)System.Math.Ceiling(hi - 0.5) - 1;
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static int Max(int a, int b) => a > b ? a : b;

        private static Color32 Hex(string h)
            => new Color32((byte)System.Convert.ToInt32(h.Substring(0, 2), 16),
                           (byte)System.Convert.ToInt32(h.Substring(2, 2), 16),
                           (byte)System.Convert.ToInt32(h.Substring(4, 2), 16), 255);

        private static Color32[] Ramp(params string[] hex)
        {
            var r = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) r[i] = Hex(hex[i]);
            return r;
        }

        private static System.Collections.Generic.Dictionary<char, string[]> BuildFont()
        {
            var f = new System.Collections.Generic.Dictionary<char, string[]>(40);
            void Put(char c, string a, string b, string cc, string d, string e)
                => f[c] = new[] { a, b, cc, d, e };
            Put('A', ".#.", "#.#", "###", "#.#", "#.#"); Put('B', "##.", "#.#", "##.", "#.#", "##.");
            Put('C', ".##", "#..", "#..", "#..", ".##"); Put('D', "##.", "#.#", "#.#", "#.#", "##.");
            Put('E', "###", "#..", "##.", "#..", "###"); Put('F', "###", "#..", "##.", "#..", "#..");
            Put('G', ".##", "#..", "#.#", "#.#", ".##"); Put('H', "#.#", "#.#", "###", "#.#", "#.#");
            Put('I', "###", ".#.", ".#.", ".#.", "###"); Put('J', "..#", "..#", "..#", "#.#", ".#.");
            Put('K', "#.#", "#.#", "##.", "#.#", "#.#"); Put('L', "#..", "#..", "#..", "#..", "###");
            Put('M', "#.#", "###", "###", "#.#", "#.#"); Put('N', "#.#", "##.", "###", ".##", "#.#");
            Put('O', ".#.", "#.#", "#.#", "#.#", ".#."); Put('P', "##.", "#.#", "##.", "#..", "#..");
            Put('Q', ".#.", "#.#", "#.#", ".#.", "..#"); Put('R', "##.", "#.#", "##.", "#.#", "#.#");
            Put('S', ".##", "#..", ".#.", "..#", "##."); Put('T', "###", ".#.", ".#.", ".#.", ".#.");
            Put('U', "#.#", "#.#", "#.#", "#.#", ".#."); Put('V', "#.#", "#.#", "#.#", ".#.", ".#.");
            Put('W', "#.#", "#.#", "###", "###", "#.#"); Put('X', "#.#", "#.#", ".#.", "#.#", "#.#");
            Put('Y', "#.#", "#.#", ".#.", ".#.", ".#."); Put('Z', "###", "..#", ".#.", "#..", "###");
            Put('0', "###", "#.#", "#.#", "#.#", "###"); Put('1', ".#.", "##.", ".#.", ".#.", "###");
            Put('2', "##.", "..#", ".#.", "#..", "###"); Put('3', "##.", "..#", ".#.", "..#", "##.");
            Put('4', "#.#", "#.#", "###", "..#", "..#"); Put('5', "###", "#..", "##.", "..#", "##.");
            Put('6', ".##", "#..", "##.", "#.#", ".#."); Put('7', "###", "..#", ".#.", ".#.", ".#.");
            Put('8', ".#.", "#.#", ".#.", "#.#", ".#."); Put('9', ".#.", "#.#", ".##", "..#", "##.");
            Put('-', "...", "...", "###", "...", "..."); Put('.', "...", "...", "...", "...", ".#.");
            Put('/', "..#", "..#", ".#.", "#..", "#.."); Put(' ', "...", "...", "...", "...", "...");
            return f;
        }
    }
}
