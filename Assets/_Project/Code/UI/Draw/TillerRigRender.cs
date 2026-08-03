using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Live C# port of the outboard tiller's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/outboard-tiller/Art/tillerRig.js</c> (ADR 0025, Option A). Method-by-method
    /// mirror of <c>paint</c> (tillerRig.js:130-188) and its primitives; every constant cites its
    /// source line. The sprite is drawn STRAIGHT UP and the CALLER rotates it about
    /// <see cref="PivotX"/>/<see cref="PivotY"/> by <c>steer × MaxSteerDeg</c> — steering is not a
    /// render parameter (the rig's own contract, tillerRig.js:37-45), so the overlay rotates the
    /// RectTransform instead of re-rasterising.
    ///
    /// <para><b>Pinned geometry</b> (guarded in <c>LeverTillerGeometryGoldenTests</c>): cell
    /// <see cref="W"/>×<see cref="H"/>, clamp pivot (tillerRig.js:8), <see cref="MaxSteerDeg"/> and the
    /// rig-local hit targets (tillerRig.js:195-199).</para>
    /// </summary>
    public static class TillerRigRender
    {
        // cell + clamp pivot (near base) — tillerRig.js:8
        public const int W = 120, H = 244;
        public const int PivotX = 60, PivotY = 214;
        // degrees at full lock — tillerRig.js:196
        public const float MaxSteerDeg = 45f;
        // rig-local hit targets — tillerRig.js:198
        public const int HitButtonX = 58, HitButtonY = 50, HitButtonR = 16;
        public const int HitShiftX = 47, HitShiftY = 84, HitShiftW = 22, HitShiftH = 34;

        // ---- palettes (tillerRig.js:11-19) -------------------------------------------------------
        private static readonly Color32[] GRAPH = Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] RUBBER = Ramp("0b0e11", "11161a", "1a2126", "252e35", "333f47");
        private static readonly Color32[] STEEL = Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32[] GREEN = Ramp("1c6a3b", "2f9e57", "66d585");
        private static readonly Color32[] RED = Ramp("7c2a20", "b83b2e", "e0554a");
        private static readonly Color32 REDOFF = Hex("241012");
        private static readonly Color32 AMBER = Hex("e6b53f");
        private static readonly Color32 TEAL = Hex("7fd6c9");
        private static readonly Color32 LAMPOFF = Hex("16202a");
        private static readonly Color32 SEGOFF = Hex("141c22");
        private static readonly Color32[] KILL = Ramp("c23a2d", "ef6a5b");
        private static readonly Color32[] CORD = Ramp("8f2b22", "c23a2d");

        // 3x5 pixel glyphs for the shift rocker (tillerRig.js:22-25)
        private static readonly string[] GlyphF = { "###", "#..", "##.", "#..", "#.." };
        private static readonly string[] GlyphR = { "##.", "#.#", "##.", "#.#", "#.#" };

        /// <summary>
        /// Paint the full handle, local space, pointing up (tillerRig.js:130-188) into
        /// <paramref name="s"/> (must be <see cref="W"/>×<see cref="H"/>). <paramref name="throttle"/>
        /// 0..1 (the overlay feeds <c>|drive|</c>), <paramref name="forward"/> = the F/R shift rocker
        /// (the overlay feeds <c>drive ≥ 0</c>).
        /// </summary>
        public static void Render(DrawSurface s, float throttle, bool running, bool press,
                                  bool forward, bool warn, bool blink)
        {
            float th = Mathf.Clamp01(throttle);
            s.Clear();

            // cowl (powerhead the tiller clamps to) — tillerRig.js:138-143
            RRect(s, 30, 200, 60, 32, 12, GRAPH[1]);
            RRect(s, 33, 203, 54, 8, 4, GRAPH[3]);                     // moulded top vents
            for (int i = 0; i < 4; i++) s.FillRect(38 + i * 12, 204, 1, 6, GRAPH[0]);
            RRect(s, 36, 227, 48, 4, 2, GRAPH[0]);                     // bottom lip
            Screw(s, 40, 226, 2);
            Screw(s, 80, 226, 2);
            Circle(s, 72, 214, 3, TEAL);                               // maker nub
            Circle(s, 72, 214, 2, Hex("0e2b2a"));

            // clamp collar + swivel bracket at the steering axis — tillerRig.js:146-149
            RRect(s, 45, 189, 30, 16, 4, GRAPH[4]);
            RRect(s, 43, 191, 4, 12, 1, GRAPH[3]);
            RRect(s, 73, 191, 4, 12, 1, GRAPH[3]);                     // side plates
            RRect(s, 48, 190, 24, 3, 1, GRAPH[5]);
            Circle(s, 60, 197, 3, GRAPH[5]);                           // pivot bolt
            Circle(s, 60, 197, 2, GRAPH[2]);

            // arm shaft with a ribbed rubber boot near the base — tillerRig.js:152-153
            Cylinder(s, 46, 162, 28, 34, 9, GRAPH, 0.34, false, 0, 0, 0);
            for (int yy = 176; yy <= 192; yy += 4)
            {
                s.FillRect(48, yy, 24, 1, GRAPH[0]);
                s.FillRect(48, yy - 1, 24, 1, GRAPH[4]);
            }

            // kill-cord: clip plugged onto a red kill button, cord coiled down to the collar — tillerRig.js:156-163
            Cord(s, 42, 182, 36, 200, 7, 3);
            Circle(s, 36, 201, 3, CORD[0]);                            // terminal loop at the collar
            RRect(s, 34, 170, 17, 15, 3, GRAPH[0]);                    // kill-switch housing
            RRect(s, 35, 171, 15, 3, 1, GRAPH[3]);
            Circle(s, 42, 179, 4, KILL[0]);                            // red kill button
            Circle(s, 41, 178, 2, KILL[1]);
            RRect(s, 37, 175, 11, 6, 2, KILL[0]);                      // lanyard clip clamped over it
            RRect(s, 38, 176, 9, 2, 1, KILL[1]);
            s.FillRect(41, 177, 3, 2, Hex("3a0f0b"));                  // clip eye the cord threads

            // telltale pod at the base of the grip — tillerRig.js:166-170
            RRect(s, 39, 150, 42, 15, 4, GRAPH[1]);
            RRect(s, 40, 151, 40, 2, 1, GRAPH[3]);
            Circle(s, 50, 158, 3, running ? GREEN[2] : LAMPOFF);
            Circle(s, 61, 158, 3, running ? TEAL : LAMPOFF);
            TriUp(s, 72, 155, 6, (warn && blink) ? AMBER : LAMPOFF);

            // grip body (throttle twist body), ribbed only on the lower shaft-hand section — tillerRig.js:173-174
            Cylinder(s, 36, 32, 48, 122, 16, RUBBER, 0.36, true, 7, 124, 152);
            s.BlendRect(59, 40, 1, 92, new Color32(0, 0, 0, 255), 0.22f);   // moulding seam

            // trim rocker (subtle, left flank) — tillerRig.js:177-179
            RRect(s, 39, 66, 10, 26, 3, RUBBER[0]);
            RRect(s, 40, 67, 8, 11, 2, STEEL[2]);
            RRect(s, 40, 80, 8, 11, 2, STEEL[0]);
            s.FillRect(43, 70, 2, 2, STEEL[4]);
            s.FillRect(43, 86, 2, 1, STEEL[3]);

            ThrottleBand(s, 71, 60, 10, 90, th);   // right shoulder — tillerRig.js:181
            ShiftRocker(s, 47, 84, 22, 34, forward);   // centre column, below the button — tillerRig.js:182
            Button(s, 58, 50, 10, running, press);     // thumb rest, top of grip — tillerRig.js:183

            // grip end cap — tillerRig.js:186-187
            RRect(s, 40, 18, 40, 16, 7, RUBBER[1]);
            RRect(s, 44, 19, 32, 5, 3, RUBBER[4]);
        }

        // ---- crisp primitives (tillerRig.js:28-72) -----------------------------------------------
        private static int RowInset(int v, int h, int r)
        {
            if (v < r) { int dy = r - v; return r - (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, r * r - dy * dy))); }
            if (v >= h - r) { int dy = v - (h - r) + 1; return r - (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, r * r - dy * dy))); }
            return 0;
        }

        private static void RRect(DrawSurface s, int x, int y, int w, int h, int r, Color32 fill)
        {
            r = System.Math.Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(w, h) / 2.0)));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                s.FillRect(x + i, y + v, w - 2 * i, 1, fill);
            }
        }

        private static void Circle(DrawSurface s, int cx, int cy, int rad, Color32 fill)
        {
            for (int dy = -rad; dy <= rad; dy++)
            {
                int dx = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                s.FillRect(cx - dx, cy + dy, 2 * dx + 1, 1, fill);
            }
        }

        private static void TriUp(DrawSurface s, int cx, int yTop, int size, Color32 fill)
        {
            for (int i = 0; i < size; i++) s.FillRect(cx - i, yTop + i, 2 * i + 1, 1, fill);
        }

        private static void Glyph(DrawSurface s, string[] map, int x, int y, int scale, Color32 color)
        {
            for (int r = 0; r < map.Length; r++)
                for (int c = 0; c < map[r].Length; c++)
                    if (map[r][c] == '#') s.FillRect(x + c * scale, y + r * scale, scale, scale, color);
        }

        private static void Screw(DrawSurface s, int cx, int cy, int r)
        {
            Circle(s, cx, cy, r, GRAPH[0]);
            Circle(s, cx, cy, r - 1, GRAPH[3]);
            Circle(s, cx - 1, cy - 1, System.Math.Max(1, r - 2), GRAPH[4]);
            s.FillRect(cx - r + 1, cy, 2 * r - 1, 1, GRAPH[0]);   // slot
        }

        // shaded cylinder: vertical highlight streak + optional grip ribs (tillerRig.js:51-64)
        private static void Cylinder(DrawSurface s, int x, int y, int w, int h, int r, Color32[] ramp,
                                     double hl, bool rib, int ribStep, int ribFrom, int ribTo)
        {
            int n = ramp.Length;
            double hlU = w * hl, span = w * 0.62;
            for (int v = 0; v < h; v++)
            {
                int ins = RowInset(v, h, r);
                bool ribRow = rib && (y + v) >= ribFrom && (y + v) <= ribTo && ribStep > 0 && (v % ribStep == 0);
                for (int u = ins; u < w - ins; u++)
                {
                    double t = 1.0 - System.Math.Min(1.0, System.Math.Abs(u - hlU) / span);
                    int idx = DrawSurface.JsRound(t * (n - 1));
                    if (v < 2) idx = System.Math.Min(n - 1, idx + 1);
                    if (ribRow) idx = System.Math.Max(0, idx - 2);
                    s.FillRect(x + u, y + v, 1, 1, ramp[idx]);
                }
            }
        }

        // coiled cord: overlapping loops walked from a→b, so it reads connected (tillerRig.js:66-72)
        private static void Cord(DrawSurface s, int ax, int ay, int bx, int by, int loops, int rad)
        {
            for (int i = 0; i <= loops; i++)
            {
                double t = (double)i / loops;
                int cx = DrawSurface.JsRound(ax + (bx - ax) * t), cy = DrawSurface.JsRound(ay + (by - ay) * t);
                Circle(s, cx, cy, rad, CORD[0]);
                s.FillRect(cx - rad + 1, cy - 1, 1, 1, CORD[1]);   // per-loop sheen
            }
        }

        // ---- feature parts (tillerRig.js:74-127) -------------------------------------------------
        // throttle: segmented band recessed into a moulded rubber channel (tillerRig.js:76-89)
        private static void ThrottleBand(DrawSurface s, int x, int y, int w, int h, float throttle)
        {
            RRect(s, x - 2, y - 3, w + 4, h + 6, 4, RUBBER[0]);        // rubber surround (part of grip)
            RRect(s, x - 2, y - 3, w + 4, 2, 2, RUBBER[3]);            // top lip catch-light
            RRect(s, x - 1, y - 1, w + 2, h + 2, 2, Hex("05080a"));    // inner shadow
            const int N = 10;
            const int gap = 1;
            int segH = (int)System.Math.Floor((h - (N - 1) * gap) / (double)N);
            int lit = DrawSurface.JsRound(throttle * N);
            for (int i = 0; i < N; i++)
            {
                int segY = y + h - segH - i * (segH + gap);
                bool on = i < lit;
                Color32 col = SEGOFF;
                if (on) col = i < 6 ? GREEN[1] : (i < 9 ? Hex("c99a34") : RED[1]);   // muted, integrated
                RRect(s, x, segY, w, segH, 1, col);
                if (on) s.BlendRect(x + 1, segY, w - 2, 1, new Color32(255, 255, 255, 255), 0.20f);
            }
            s.BlendRect(x, y, System.Math.Max(1, (int)System.Math.Floor(w / 3.0)), h,
                        new Color32(120, 150, 160, 255), 0.10f);       // faint glass reflection
        }

        // start/stop: low dome flush-set into a moulded dish in the rubber (tillerRig.js:91-108)
        private static void Button(DrawSurface s, int cx, int cy, int r, bool running, bool press)
        {
            Circle(s, cx, cy, r + 4, RUBBER[0]);                       // recess shadow ring
            Circle(s, cx, cy, r + 3, RUBBER[2]);                       // moulded bezel
            Circle(s, cx - 1, cy - 2, r + 3, RUBBER[4]);               // bezel upper sheen
            Circle(s, cx, cy, r + 1, Hex("05080a"));                   // dish shadow
            int oy = press ? 1 : 0;
            Color32[] c = running ? RED : GREEN;
            Circle(s, cx, cy + oy, r, c[0]);
            Circle(s, cx, cy + oy, r - 1, c[1]);
            Circle(s, cx - 2, cy - 2 + oy, System.Math.Max(2, r - 3), c[2]);   // upper-left sheen
            if (running)
            {                                                          // stop square
                int sq = System.Math.Max(4, DrawSurface.JsRound(r * 0.72));
                int s2 = (int)System.Math.Floor(sq / 2.0);
                s.FillRect(cx - s2, cy - s2 + oy, sq, sq, Hex("f2e7e4"));
            }
            else
            {                                                          // play triangle
                int size = System.Math.Max(5, DrawSurface.JsRound(r * 0.95));
                int x0 = cx - (int)System.Math.Floor(size * 0.32);
                for (int col = 0; col < size; col++)
                {
                    int half = DrawSurface.JsRound(((size - col) / 2.0) * 0.9);
                    s.FillRect(x0 + col, cy - half + oy, 1, 2 * half + 1, Hex("eafaf0"));
                }
            }
        }

        // red FORWARD/REVERSE shift rocker — active gear half lights red, letter brightens (tillerRig.js:110-127)
        private static void ShiftRocker(DrawSurface s, int x, int y, int w, int h, bool forward)
        {
            RRect(s, x - 3, y - 3, w + 6, h + 6, 5, RUBBER[0]);        // recess in rubber
            RRect(s, x - 3, y - 3, w + 6, 2, 2, RUBBER[3]);            // top catch-light
            RRect(s, x - 1, y - 1, w + 2, h + 2, 4, Hex("06090b"));    // slot shadow
            int half = (int)System.Math.Floor((h - 3) / 2.0);
            int topY = y + 1, botY = y + h - half - 1, midX = x + (int)System.Math.Floor(w / 2.0);
            // top half = FORWARD
            RRect(s, x + 1, topY, w - 2, half, 3, forward ? RED[0] : REDOFF);
            if (forward)
            {
                RRect(s, x + 2, topY + 1, w - 4, (int)System.Math.Floor(half * 0.42), 2, RED[1]);
                RRect(s, x + 3, topY + 1, w - 6, 1, 1, RED[2]);
            }
            // bottom half = REVERSE
            RRect(s, x + 1, botY, w - 2, half, 3, !forward ? RED[0] : REDOFF);
            if (!forward)
            {
                RRect(s, x + 2, botY, w - 4, (int)System.Math.Floor(half * 0.42), 2, RED[1]);
                RRect(s, x + 3, botY + 1, w - 6, 1, 1, RED[2]);
            }
            // centre pivot ridge
            s.FillRect(x, y + (int)System.Math.Floor(h / 2.0) - 1, w, 2, Hex("05080a"));
            s.FillRect(x, y + (int)System.Math.Floor(h / 2.0) - 1, w, 1, STEEL[2]);
            // engraved letters
            Glyph(s, GlyphF, midX - 3, topY + (int)System.Math.Floor(half / 2.0) - 5, 2,
                  forward ? Hex("f4dede") : Hex("5a3335"));
            Glyph(s, GlyphR, midX - 3, botY + (int)System.Math.Floor(half / 2.0) - 5, 2,
                  !forward ? Hex("f4dede") : Hex("5a3335"));
        }

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
    }
}
