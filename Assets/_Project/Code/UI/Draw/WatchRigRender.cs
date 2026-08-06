using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The signals the watch face draws from — the rig's <c>render(opts)</c> bag
    /// (<c>watchRig.js:27</c>), as a value so the host can change-detect it.
    ///
    /// <para>The clock-derived half of this comes straight off <see cref="WatchFaceState"/> (Core), which
    /// is the ONE mapper from <see cref="IGameClock"/>. The two display flags —
    /// <see cref="Use24"/> and <see cref="Light"/> — are not clock-derived (the rig's own README says so),
    /// so the presenter supplies them.</para>
    /// </summary>
    public readonly struct WatchRigState
    {
        public readonly WatchFaceState Face;   // hour/minute/second/weekday/date/season/year/market/night
        public readonly bool Use24;            // 24-hour vs 12-hour + AM/PM (watchRig.js:147-149)
        public readonly bool Light;            // momentary LIGHT pusher — backlight independent of night
        public readonly bool ShowSeconds;      // draw the seconds cells, or leave them unlit (see remarks)

        public WatchRigState(in WatchFaceState face, bool use24, bool light, bool showSeconds)
        {
            Face = face; Use24 = use24; Light = light; ShowSeconds = showSeconds;
        }

        /// <summary>The rig's own backlight test — <c>night || light</c> (watchRig.js:198).</summary>
        public bool Backlit => Face.IsNight || Light;
    }

    /// <summary>
    /// Live C# port of the digital watch face's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/watch-face/Art/watchRig.js</c> (ADR 0025, Option A). Method-by-method mirror
    /// of <c>paint</c>/<c>lcd</c> and their primitives; every constant cites its source line. Pure
    /// geometry + the LCD strings live next door in <see cref="WatchRigGeometry"/> so the cells and the
    /// formatters are testable without a canvas.
    ///
    /// <para><b>Live, not baked.</b> There is nothing finite here to bake: the face's state space is
    /// every minute of every day of a 112-day year, times the season/weekday/market/backlight flags. So
    /// this rig never enters <c>RigCatalog</c> and produces no PNG — it is the ADR's Option A case, the
    /// same as the sounder and the compass.</para>
    ///
    /// <para><b>Three deliberate deviations from the source, all consequences of the no-AA rule</b>
    /// (<see cref="DrawSurface"/>): (1) <c>rrectGrad</c>'s anti-aliased <c>arcTo</c> path becomes the
    /// same integer row insets the scanline rounded rect uses, with the gradient interpolated per row;
    /// (2) the 7-segment bars are scan-converted by pixel-centre coverage instead of an anti-aliased
    /// polygon fill; and (3) the colon's pips, which the source places at a FRACTIONAL y
    /// (<c>dh*0.62</c> = 37.2), land on the pixel grid. The sounder's port made (1) and (2) before this
    /// one and the reasoning is unchanged; none moves a shape by more than the sub-pixel coverage
    /// canvas would have feathered.</para>
    ///
    /// <para><b>Perf (rule 7):</b> a full repaint is ~1.8k span fills over 340×356. That is a repaint
    /// cost, not a frame cost — <see cref="HudController"/> repaints only when the DISPLAYED minute,
    /// day or season changes (~0.8 Hz at the shipped <c>SecondsPerDay</c>), never per frame. See
    /// <see cref="WatchRigState.ShowSeconds"/> for why the seconds cells are dark by default.</para>
    /// </summary>
    public static class WatchRigRender
    {
        /// <summary>Standalone sprite size — watchRig.js:13.</summary>
        public const int W = WatchRigGeometry.W, H = WatchRigGeometry.H;

        // ---- palettes (watchRig.js:16-22) -----------------------------------------------------------
        private static readonly Color32[] RESIN =
            RigDrawUtil.Ramp("050607", "0c0f11", "15191c", "20262a", "2c343a");
        private static readonly Color32[] BEZEL =
            RigDrawUtil.Ramp("5b636a", "767f86", "8b949b", "a3acb2", "c2c9cd");
        private static readonly Color32[] STEEL =
            RigDrawUtil.Ramp("3a434a", "5c676f", "828d94", "a7b2b8", "cfd7db");
        private static readonly Color32 INK = RigDrawUtil.Hex("0a0c0d");
        private static readonly Color32 Illuminator = RigDrawUtil.Hex("b7bec2");   // watchRig.js:208
        private static readonly Color32 WaterResist = RigDrawUtil.Hex("aeb6ba");   // watchRig.js:225
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        // watchRig.js:231 — the bloom is stated as rgba(120,240,150,0.45); the alpha rides the call.
        private static readonly Color32 Bloom = RigDrawUtil.Hex("78f096");

        /// <summary>One LCD palette — DAY is a positive grey-green panel, NIGHT the green EL backlight
        /// (watchRig.js:21-22). <c>Off</c> carries its own alpha because the source states it as an
        /// <c>rgba()</c> string: the un-lit segments are a faint ghost, not a solid.</summary>
        private readonly struct Pal
        {
            public readonly Color32 Bg1, Bg2, Seg, Off, Dim;
            public readonly float OffAlpha;
            public readonly bool HasBloom;

            public Pal(string bg1, string bg2, string seg, Color32 off, float offAlpha, string dim,
                       bool hasBloom)
            {
                Bg1 = RigDrawUtil.Hex(bg1); Bg2 = RigDrawUtil.Hex(bg2); Seg = RigDrawUtil.Hex(seg);
                Off = off; OffAlpha = offAlpha;
                Dim = RigDrawUtil.Hex(dim); HasBloom = hasBloom;
            }
        }

        // watchRig.js:21 — off = rgba(26,32,22,0.07)
        private static readonly Pal DAY = new Pal("cbd2c4", "aeb6a6", "1c2119",
            new Color32(26, 32, 22, 255), 0.07f, "4a5346", false);

        // watchRig.js:22 — off = rgba(9,42,18,0.12)
        private static readonly Pal NIGHT = new Pal("43c162", "2c9a46", "0b3415",
            new Color32(9, 42, 18, 255), 0.12f, "0f5222", true);

        // 7-segment map — watchRig.js:99-102. Identical to the sounder's (depthRig.js:89-90); kept
        // private here rather than promoted, because folding the three copies (this, DepthRigRender,
        // FishRigRender) into RigDrawUtil would touch the shipped instruments' goldens, which this
        // slice is fenced out of. Worth doing as its own change.
        private static readonly System.Collections.Generic.Dictionary<char, string> SEG =
            new System.Collections.Generic.Dictionary<char, string>
            {
                { '0', "abcdef" },  { '1', "bc" },      { '2', "abged" },  { '3', "abgcd" },
                { '4', "fgbc" },    { '5', "afgcd" },   { '6', "afgedc" }, { '7', "abc" },
                { '8', "abcdefg" }, { '9', "abfgcd" },  { '-', "g" },      { ' ', "" },
            };

        // ---- the public entry point -----------------------------------------------------------------

        /// <summary>Paint the whole watch (watchRig.js:189-238) into <paramref name="s"/>, which must be
        /// <see cref="W"/>×<see cref="H"/>.</summary>
        public static void Render(DrawSurface s, in WatchRigState o)
        {
            Pal p = o.Backlit ? NIGHT : DAY;
            s.Clear();                                                   // watchRig.js:200

            // strap lugs, cropped top & bottom — watchRig.js:203
            RigDrawUtil.RRect(s, 118, 0, 104, 16, 6, RESIN[0]);
            RigDrawUtil.RRect(s, 118, H - 16, 104, 16, 6, RESIN[0]);

            // case body — watchRig.js:205-206
            RRectGrad(s, 10, 8, 320, 340, 50, RESIN[3], RESIN[1]);
            RigDrawUtil.RRect(s, 16, 14, 308, 328, 44, RESIN[1]);

            // top illuminator label — watchRig.js:208
            RigDrawUtil.TextC(s, "ILLUMINATOR", W / 2.0, 36, 2, Illuminator);

            // side pushers + guards — watchRig.js:210
            Pusher(s, 16, 118, -1); Pusher(s, 16, 214, -1);
            Pusher(s, 310, 118, 1); Pusher(s, 310, 214, 1);

            // grey bezel face — watchRig.js:213-215
            RRectGrad(s, 40, 60, 260, 236, 24, BEZEL[3], BEZEL[1]);
            RigDrawUtil.RRect(s, 44, 64, 252, 228, 20, BEZEL[2]);
            Screw(s, 58, 80); Screw(s, 282, 80); Screw(s, 58, 278); Screw(s, 282, 278);

            // brand wordmark (original) + model no. — watchRig.js:217-218
            RigDrawUtil.TextC(s, "HARBOUR", W / 2.0, 70, 4, INK);
            RigDrawUtil.Text(s, "H-108", 224, 78, 2, BEZEL[0]);

            // pusher function labels, inset clear of the bezel screws, each flagged with a chevron
            // notch pointing at its button — watchRig.js:220-224 (NEW in the 2026-08-06 drop).
            const int ls = 2, yT = 98, yB = 274, xL = 80, xR = 260;
            RigDrawUtil.Text(s, "START/STOP", xL, yT, ls, INK);   BtnChevron(s, 68, yT, -1, INK);
            RigDrawUtil.Text(s, "MODE", xL, yB, ls, INK);         BtnChevron(s, 68, yB, -1, INK);
            RigDrawUtil.Text(s, "ALARM CHRONO", xR - RigDrawUtil.TextW("ALARM CHRONO", ls), yT, ls, INK);
            BtnChevron(s, 272, yT, 1, INK);
            RigDrawUtil.Text(s, "LIGHT", xR - RigDrawUtil.TextW("LIGHT", ls), yB, ls, INK);
            BtnChevron(s, 272, yB, 1, INK);

            RigDrawUtil.TextC(s, "WATER RESIST", W / 2.0, 300, 2, WaterResist);

            // LCD window (recessed) — watchRig.js:228-237
            RigRect g = WatchRigGeometry.Glass();
            if (p.HasBloom)
            {
                // green bloom over the bezel — watchRig.js:229-233
                s.BlendRadial(28, 88, 284, 210,
                              g.X + g.W / 2.0, g.Y + g.H / 2.0, 20, g.W * 0.8, Bloom, 0.45f);
            }
            RigDrawUtil.RRect(s, g.X - 4, g.Y - 4, g.W + 8, g.H + 8, 12, RESIN[0]);            // dark surround
            RRectGrad(s, g.X, g.Y, g.W, g.H, 8, p.Bg1, p.Bg2);                     // glass
            s.BlendRect(g.X + 4, g.Y + 3, g.W - 8, 2, White, 0.10f);               // top glare
            Lcd(s, g.X, g.Y, g.W, g.H, in o, in p);
        }

        // ---- LCD content (watchRig.js:145-186) -------------------------------------------------------

        private static void Lcd(DrawSurface s, int x, int y, int ww, int hh,
                                in WatchRigState o, in Pal p)
        {
            WatchFaceState f = o.Face;
            WatchLcdLayout L = WatchRigGeometry.Layout(x, y, ww, hh);

            string hh2 = WatchRigGeometry.HourText(f.Hour, o.Use24);          // watchRig.js:147-149
            string mm = WatchRigGeometry.Pad2(f.Minute);
            string ss = o.ShowSeconds ? WatchRigGeometry.Pad2(f.Second) : "  ";
            string date = WatchRigGeometry.Pad2(f.DayOfSeason);

            // --- top strip: indicators (left) + weekday & date (right) — watchRig.js:152-161
            int topY = L.TopRowY;
            RigDrawUtil.Text(s, WatchRigGeometry.MarketTag(f.IsMarketDay), x + 6, topY + 1, 2, p.Dim);
            for (int i = 0; i < 3; i++)                                        // oscillator ticks :157
                s.FillRect(x + 30 + i * 4, topY + 2 + (2 - i), 2, 3 + i * 2, p.Dim);

            Seg7(s, L.Date0, date[0], p.Seg, p.Off, p.OffAlpha, true);
            Seg7(s, L.Date1, date[1], p.Seg, p.Off, p.OffAlpha, true);

            // The weekday sits immediately left of the date cells' own left edge — watchRig.js:161.
            string dow = WatchRigGeometry.WeekdayAbbr(f.Weekday);
            RigDrawUtil.Text(s, dow, L.Date0.X - RigDrawUtil.TextW(dow, 3) - 8, topY + 1, 3, p.Seg);

            // --- AM/PM, left of the main group (12-hour only) — watchRig.js:166
            int mainY = L.Hour0.Y;
            if (!o.Use24) RigDrawUtil.Text(s, WatchRigGeometry.AmPm(f.Hour), x + 6, mainY + 6, 3, p.Seg);

            // --- main HH:MM — watchRig.js:171-179. A blank leading hour cell (12-hour, 1..9) draws
            // NOTHING, not even its off-ghosts: the source passes a null colour there, and canvas
            // ignores a null fillStyle, so the whole cell is skipped.
            bool lead = hh2[0] != ' ';
            Seg7(s, L.Hour0, hh2[0], p.Seg, p.Off, p.OffAlpha, lead);
            Seg7(s, L.Hour1, hh2[1], p.Seg, p.Off, p.OffAlpha, true);

            s.FillRect(L.ColonTop.X, L.ColonTop.Y, L.ColonTop.W, L.ColonTop.H, p.Seg);
            s.FillRect(L.ColonBottom.X, L.ColonBottom.Y, L.ColonBottom.W, L.ColonBottom.H, p.Seg);

            Seg7(s, L.Minute0, mm[0], p.Seg, p.Off, p.OffAlpha, true);
            Seg7(s, L.Minute1, mm[1], p.Seg, p.Off, p.OffAlpha, true);

            // --- bottom: season + year (left), seconds (right) — watchRig.js:182-185
            int botY = L.BottomRowY;
            string season = WatchRigGeometry.SeasonAbbr(f.Season);
            RigDrawUtil.Text(s, season, x + 8, botY + 4, 3, p.Seg);
            RigDrawUtil.Text(s, WatchRigGeometry.YearText(f.Year),
                             x + 8 + RigDrawUtil.TextW(season, 3) + 8, botY + 4, 3, p.Dim);

            // Seconds. With ShowSeconds off the cells still draw their off-ghosts (a blank ' ' lights no
            // segment but keeps the ghost pass) — an LCD with a dark seconds field, which is what an
            // unlit watch cell looks like, rather than a field frozen on a stale number.
            Seg7(s, L.Second0, ss[0], p.Seg, p.Off, p.OffAlpha, true);
            Seg7(s, L.Second1, ss[1], p.Seg, p.Off, p.OffAlpha, true);
        }

        // ---- primitives the watch does NOT share with RigDrawUtil -------------------------------------

        /// <summary>
        /// watchRig.js:69-77 — a rounded rect under a vertical linear gradient. The source clips an
        /// anti-aliased <c>arcTo</c> path; under the no-AA rule this walks the SAME integer row insets
        /// the scanline rounded rect uses and interpolates the gradient per row (deviation 1; the
        /// sounder's port made the identical call, DepthRigRender's remarks).
        /// </summary>
        private static void RRectGrad(DrawSurface s, int x, int y, int w, int h, int r,
                                      Color32 c1, Color32 c2)
        {
            r = System.Math.Max(0, System.Math.Min(r, System.Math.Min(w, h) / 2));
            for (int v = 0; v < h; v++)
            {
                int i = RigDrawUtil.RowInset(v, h, r);
                // The canvas gradient runs y → y+h, so the stop at row v is v/h (h > 0 always here).
                s.FillRect(x + i, y + v, w - 2 * i, 1, DrawSurface.Lerp(c1, c2, (double)v / h));
            }
        }

        /// <summary>watchRig.js:82-85 — the bezel screw. NOT <see cref="RigDrawUtil.Screw"/>: the three
        /// discs agree, but this rig cuts a 6×2 slot at <c>cy-1</c> where the console rigs cut a
        /// 1-pixel one, so a shared call would visibly change the head.</summary>
        private static void Screw(DrawSurface s, int cx, int cy)
        {
            RigDrawUtil.Circle(s, cx, cy, 5, STEEL[0]);
            RigDrawUtil.Circle(s, cx, cy, 4, STEEL[2]);
            RigDrawUtil.Circle(s, cx - 1, cy - 1, 3, STEEL[3]);
            s.FillRect(cx - 3, cy - 1, 6, 2, STEEL[0]);
        }

        /// <summary>watchRig.js:86-90 — the pusher legend's chevron notch, a 2-px arrow whose tip is at
        /// <paramref name="x"/> and which points OUTWARD (<paramref name="dir"/> −1 = left, +1 = right).
        /// New in the 2026-08-06 drop.</summary>
        private static void BtnChevron(DrawSurface s, int x, int y, int dir, Color32 col)
        {
            Pip(s, x, y, dir, col, 0, 2); Pip(s, x, y, dir, col, 1, 1); Pip(s, x, y, dir, col, 1, 3);
            Pip(s, x, y, dir, col, 2, 0); Pip(s, x, y, dir, col, 2, 4);
        }

        private static void Pip(DrawSurface s, int x, int y, int dir, Color32 col, int dx, int dy)
            => s.FillRect(x + (dir < 0 ? dx : -dx) * 2, y + dy * 2, 2, 2, col);

        /// <summary>watchRig.js:91-96 — a side pusher and its steel cap. The source's <c>bx</c> local and
        /// its <c>14 * side > 0 ? 14 : 14</c> both evaluate to a constant 14, so only the effective
        /// geometry is ported.</summary>
        private static void Pusher(DrawSurface s, int x, int y, int side)
        {
            RigDrawUtil.RRect(s, x, y, 14, 22, 4, RESIN[1]);
            int capX = side < 0 ? x - 9 : x + 13;
            RigDrawUtil.RRect(s, capX, y + 5, 10, 12, 3, STEEL[1]);
            RigDrawUtil.RRect(s, capX, y + 5, 10, 4, 2, STEEL[3]);
        }

        // ---- 7-segment numerals (watchRig.js:103-135) --------------------------------------------------

        /// <summary>One digit in its cell. <paramref name="draw"/> false skips the cell entirely — the
        /// source's null-colour branch (watchRig.js:119-120), which is how a 12-hour face blanks its
        /// leading cell instead of ghosting a zero there.</summary>
        private static void Seg7(DrawSurface s, in RigRect cell, char ch, Color32 onCol, Color32 offCol,
                                 float offAlpha, bool draw)
        {
            if (!draw) return;
            string on = SEG.TryGetValue(ch, out string found) ? found : "";
            double x = cell.X, y = cell.Y, w = cell.W, h = cell.H;
            double t = System.Math.Max(3.0, w * 0.19);                 // watchRig.js:117
            double midY = y + h / 2.0, hlen = w - t * 0.7, vlen = h / 2.0 - t * 0.7;

            HBar(s, x + w / 2.0, y,        hlen, t, on, 'a', onCol, offCol, offAlpha);
            HBar(s, x + w / 2.0, midY,     hlen, t, on, 'g', onCol, offCol, offAlpha);
            HBar(s, x + w / 2.0, y + h,    hlen, t, on, 'd', onCol, offCol, offAlpha);
            VBar(s, x,           y + h / 4.0,       vlen, t, on, 'f', onCol, offCol, offAlpha);
            VBar(s, x + w,       y + h / 4.0,       vlen, t, on, 'b', onCol, offCol, offAlpha);
            VBar(s, x,           y + h * 3.0 / 4.0, vlen, t, on, 'e', onCol, offCol, offAlpha);
            VBar(s, x + w,       y + h * 3.0 / 4.0, vlen, t, on, 'c', onCol, offCol, offAlpha);
        }

        /// <summary>The horizontal segment: a flat hexagon whose ends slope 1:1 into the tips
        /// (watchRig.js:103-108). Scan-converted by pixel-centre coverage — no AA (deviation 2).</summary>
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
        /// (watchRig.js:109-114).</summary>
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
                // Flat-sided until the taper starts, then the edge slopes 1:1 into the tip.
                double hw = System.Math.Min(half, l - dy);
                if (hw <= 0.0) continue;
                SpanBounds(cx - hw, cx + hw, out int x0, out int x1);
                if (x1 >= x0) s.BlendRect(x0, yy, x1 - x0 + 1, 1, col, alpha);
            }
        }

        /// <summary>Pixel centres covered by the half-open span [<paramref name="lo"/>,
        /// <paramref name="hi"/>) — the no-AA scan conversion the two bar shapes share.</summary>
        private static void SpanBounds(double lo, double hi, out int i0, out int i1)
        {
            i0 = (int)System.Math.Ceiling(lo - 0.5);
            i1 = (int)System.Math.Ceiling(hi - 0.5) - 1;
        }
    }
}
