using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The crisp raster primitives the 2026-08-03 rig drop shares VERBATIM across its sources —
    /// <c>consoleRig.js:106-141</c>, <c>sportRig.js:67-102</c> and <c>compassRig.js:51-92</c> carry
    /// line-for-line identical <c>rowInset/rrect/circle/ring/thickLine/screw</c> implementations and
    /// the same 3×5 bitmap font idiom, so the C# ports share ONE faithful implementation instead of
    /// three copies (the JS duplicates only because each rig file must stand alone in a preview).
    /// Every method cites the console rig's line numbers as the canonical source.
    ///
    /// <para><b>Pixel semantics:</b> canvas <c>fillRect</c> with integer arguments — the only way the
    /// rigs call it — is an exact span fill, which is <see cref="DrawSurface.FillRect"/>. No AA
    /// anywhere (the rigs' own rule). <c>rgba(...)</c> fills map to the <c>alpha01</c> parameter
    /// (constant-alpha source-over, <see cref="DrawSurface.BlendRect"/>).</para>
    /// </summary>
    public static class RigDrawUtil
    {
        public const double DEG = System.Math.PI / 180.0;

        // ---- angle basis (consoleRig.js:141): angle from 12 o'clock, + = clockwise ----------------
        public static void Dir(double aRad, out double x, out double y)
        {
            x = System.Math.Sin(aRad);
            y = -System.Math.Cos(aRad);
        }

        // ---- rounded-rect row inset (consoleRig.js:108-112) ---------------------------------------
        public static int RowInset(int v, int h, int r)
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

        /// <summary>consoleRig.js:113-116 — scanline rounded rect, radius clamped to half the box.</summary>
        public static void RRect(DrawSurface s, int x, int y, int w, int h, int r, Color32 c, float alpha01 = 1f)
        {
            r = System.Math.Max(0, System.Math.Min(r, System.Math.Min(w, h) / 2));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                if (alpha01 >= 1f) s.FillRect(x + i, y + v, w - 2 * i, 1, c);
                else s.BlendRect(x + i, y + v, w - 2 * i, 1, c, alpha01);
            }
        }

        /// <summary>Rounded rect under the canvas <c>'lighter'</c> pass — the pilothouse deck/night
        /// washes, which follow the dash face's own corner radius (noviRig.js:428-437).</summary>
        public static void RRectAdd(DrawSurface s, int x, int y, int w, int h, int r, Color32 c, float alpha01)
        {
            r = System.Math.Max(0, System.Math.Min(r, System.Math.Min(w, h) / 2));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                s.AddRect(x + i, y + v, w - 2 * i, 1, c, alpha01);
            }
        }

        /// <summary>consoleRig.js:117-120 — scanline circle.</summary>
        public static void Circle(DrawSurface s, int cx, int cy, int rad, Color32 c, float alpha01 = 1f)
        {
            for (int dy = -rad; dy <= rad; dy++)
            {
                int dx = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                if (alpha01 >= 1f) s.FillRect(cx - dx, cy + dy, 2 * dx + 1, 1, c);
                else s.BlendRect(cx - dx, cy + dy, 2 * dx + 1, 1, c, alpha01);
            }
        }

        /// <summary>consoleRig.js:121-129 — scanline ring (annulus). The optional clip rect ports the
        /// rigs' <c>save/rect/clip</c> pattern (the "lit top half" bezel trick) without a clip stack:
        /// spans are intersected with the rect before filling.</summary>
        public static void Ring(DrawSurface s, int cx, int cy, int rO, int rI, Color32 c)
            => Ring(s, cx, cy, rO, rI, c, 0, 0, int.MaxValue, int.MaxValue, false);

        public static void Ring(DrawSurface s, int cx, int cy, int rO, int rI, Color32 c,
                                int clipX, int clipY, int clipW, int clipH, bool clip = true)
        {
            for (int dy = -rO; dy <= rO; dy++)
            {
                int xo = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rO * rO - dy * dy)));
                if (System.Math.Abs(dy) < rI)
                {
                    int xi = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rI * rI - dy * dy)));
                    ClippedFill(s, cx - xo, cy + dy, xo - xi, 1, c, clip, clipX, clipY, clipW, clipH);
                    ClippedFill(s, cx + xi, cy + dy, xo - xi, 1, c, clip, clipX, clipY, clipW, clipH);
                }
                else
                {
                    ClippedFill(s, cx - xo, cy + dy, 2 * xo + 1, 1, c, clip, clipX, clipY, clipW, clipH);
                }
            }
        }

        private static void ClippedFill(DrawSurface s, int x, int y, int w, int h, Color32 c,
                                        bool clip, int clipX, int clipY, int clipW, int clipH)
        {
            if (clip)
            {
                if (y < clipY || y >= clipY + clipH) return;
                int x1 = System.Math.Min(x + w, clipX + clipW);
                x = System.Math.Max(x, clipX);
                w = x1 - x;
                if (w <= 0) return;
            }
            s.FillRect(x, y, w, h, c);
        }

        /// <summary>Circle restricted to a clip rect — the rigs' <c>save/rect/clip</c> pattern
        /// (e.g. the sport ignition's lit top half, sportRig.js:279-280).</summary>
        public static void CircleClipped(DrawSurface s, int cx, int cy, int rad, Color32 c,
                                         int clipX, int clipY, int clipW, int clipH)
        {
            for (int dy = -rad; dy <= rad; dy++)
            {
                int dx = (int)System.Math.Floor(System.Math.Sqrt(System.Math.Max(0, rad * rad - dy * dy)));
                ClippedFill(s, cx - dx, cy + dy, 2 * dx + 1, 1, c, true, clipX, clipY, clipW, clipH);
            }
        }

        /// <summary>Rounded rect restricted to a clip rect (the sport binnacle's right-edge shade,
        /// sportRig.js:378-379).</summary>
        public static void RRectClipped(DrawSurface s, int x, int y, int w, int h, int r, Color32 c,
                                        int clipX, int clipY, int clipW, int clipH)
        {
            r = System.Math.Max(0, System.Math.Min(r, System.Math.Min(w, h) / 2));
            for (int v = 0; v < h; v++)
            {
                int i = RowInset(v, h, r);
                ClippedFill(s, x + i, y + v, w - 2 * i, 1, c, true, clipX, clipY, clipW, clipH);
            }
        }

        /// <summary>compassRig.js:74-77 — scanline axis-aligned ellipse.</summary>
        public static void Ellipse(DrawSurface s, int cx, int cy, int rx, int ry, Color32 c, float alpha01 = 1f)
        {
            rx = System.Math.Max(1, rx);
            ry = System.Math.Max(1, ry);
            for (int dy = -ry; dy <= ry; dy++)
            {
                int dx = (int)System.Math.Floor(rx * System.Math.Sqrt(System.Math.Max(0.0, 1.0 - (double)(dy * dy) / (ry * ry))));
                if (alpha01 >= 1f) s.FillRect(cx - dx, cy + dy, 2 * dx + 1, 1, c);
                else s.BlendRect(cx - dx, cy + dy, 2 * dx + 1, 1, c, alpha01);
            }
        }

        /// <summary>consoleRig.js:130-135 — stamped 1×1 thick line (n+1 samples, ±halfwidth across).</summary>
        public static void ThickLine(DrawSurface s, double x0, double y0, double x1, double y1,
                                     double w, Color32 c, float alpha01 = 1f)
        {
            double dx = x1 - x0, dy = y1 - y0;
            double len = System.Math.Max(1.0, System.Math.Sqrt(dx * dx + dy * dy));
            int n = (int)System.Math.Ceiling(len);
            double px = -dy / len, py = dx / len, hw = (w - 1.0) / 2.0;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n, X = x0 + dx * t, Y = y0 + dy * t;
                for (double j = -hw; j <= hw; j++)
                {
                    if (alpha01 >= 1f)
                        s.FillRect(DrawSurface.JsRound(X + px * j), DrawSurface.JsRound(Y + py * j), 1, 1, c);
                    else
                        s.BlendRect(DrawSurface.JsRound(X + px * j), DrawSurface.JsRound(Y + py * j), 1, 1, c, alpha01);
                }
            }
        }

        /// <summary>
        /// A thick line under the canvas <c>'lighter'</c> pass — <see cref="ThickLine"/>'s additive twin,
        /// grown here by S5 because the radar's phosphor sweep is a fan of two dozen additive lines
        /// (<c>radarRig.js:310-318</c>) and the guard-zone edges ride the same composite. The layer's own
        /// remarks reserved this ("the sounder/radar slices grow this layer then").
        ///
        /// <para>Additive is not a stylistic choice here: the sweep's trailing wedge OVERLAPS itself as
        /// the fan sweeps, and source-over would flatten twenty-four faint passes into one faint line
        /// instead of the glow that makes a scope look alive. Stamped 1×1 like its source-over twin, so
        /// the two agree pixel-for-pixel about which pixels a line covers.</para>
        /// </summary>
        public static void ThickLineAdd(DrawSurface s, double x0, double y0, double x1, double y1,
                                        double w, Color32 c, float alpha01)
        {
            if (alpha01 <= 0f) return;
            double dx = x1 - x0, dy = y1 - y0;
            double len = System.Math.Max(1.0, System.Math.Sqrt(dx * dx + dy * dy));
            int n = (int)System.Math.Ceiling(len);
            double px = -dy / len, py = dx / len, hw = (w - 1.0) / 2.0;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n, X = x0 + dx * t, Y = y0 + dy * t;
                for (double j = -hw; j <= hw; j++)
                    s.AddRect(DrawSurface.JsRound(X + px * j), DrawSurface.JsRound(Y + py * j), 1, 1,
                              c, alpha01);
            }
        }

        /// <summary>consoleRig.js:136-140 — a slotted screw head off a 5-step ramp.</summary>
        public static void Screw(DrawSurface s, int cx, int cy, int r, Color32[] ramp)
        {
            Circle(s, cx, cy, r, ramp[0]);
            Circle(s, cx, cy, r - 1, ramp[2]);
            Circle(s, cx - 1, cy - 1, System.Math.Max(1, r - 2), ramp[3]);
            s.FillRect(cx - r + 1, cy, 2 * r - 1, 1, ramp[0]);
        }

        // ---- 3×5 bitmap font (consoleRig.js:75-104; the compass subset :31-40 is glyph-identical) --
        // S4: completed to the sources' FULL A–Z. The earlier table omitted J/Q/X/Z because no ported
        // label was thought to use them, and the sources' own fallback — `F[ch] || F[' ']`,
        // consoleRig.js:99 — blanked an unknown char silently, so BOTH shipped skiff tachs,
        // ConsoleDashRender:315 and SportDashRender:234 (consoleRig.js:202, sportRig.js:164),
        // had been rendering their "X100" as " 100" since S2a. Novi's "X1000" (noviRig.js:186) and
        // Cape's "X100" (capeRig.js:175) need the same glyph, and the four are identical across every
        // rig source, so they land here rather than in a second font (rule: one shared helper).
        private static readonly string[] Chars =
        {
            "A", ".#.|#.#|###|#.#|#.#", "B", "##.|#.#|##.|#.#|##.",
            "C", ".##|#..|#..|#..|.##", "D", "##.|#.#|#.#|#.#|##.",
            "E", "###|#..|##.|#..|###", "F", "###|#..|##.|#..|#..",
            "G", ".##|#..|#.#|#.#|.##", "H", "#.#|#.#|###|#.#|#.#",
            "I", "###|.#.|.#.|.#.|###", "J", "..#|..#|..#|#.#|.#.",
            "K", "#.#|#.#|##.|#.#|#.#",
            "L", "#..|#..|#..|#..|###", "M", "#.#|###|###|#.#|#.#",
            "N", "#.#|##.|###|.##|#.#", "O", ".#.|#.#|#.#|#.#|.#.",
            "P", "##.|#.#|##.|#..|#..", "Q", ".#.|#.#|#.#|.#.|..#",
            "R", "##.|#.#|##.|#.#|#.#",
            "S", ".##|#..|.#.|..#|##.", "T", "###|.#.|.#.|.#.|.#.",
            "U", "#.#|#.#|#.#|#.#|.#.", "V", "#.#|#.#|#.#|.#.|.#.",
            "W", "#.#|#.#|###|###|#.#", "X", "#.#|#.#|.#.|#.#|#.#",
            "Y", "#.#|#.#|.#.|.#.|.#.", "Z", "###|..#|.#.|#..|###",
            "0", "###|#.#|#.#|#.#|###", "1", ".#.|##.|.#.|.#.|###",
            "2", "##.|..#|.#.|#..|###", "3", "##.|..#|.#.|..#|##.",
            "4", "#.#|#.#|###|..#|..#", "5", "###|#..|##.|..#|##.",
            "6", ".##|#..|##.|#.#|.#.", "7", "###|..#|.#.|.#.|.#.",
            "8", ".#.|#.#|.#.|#.#|.#.", "9", ".#.|#.#|.##|..#|##.",
            "/", "..#|..#|.#.|#..|#..", "-", "...|...|###|...|...",
            ".", "...|...|...|...|.#.", " ", "...|...|...|...|...",
            // U+00B0 DEGREE — authored by the chartplotter's own font table (navRig.js:71) and used on
            // every bearing it prints (COG, NEXT, the measure label). Unlike the '·' below this one has
            // real pixels in the source, so it is transcribed rather than blanked. Added by S6; the
            // compass and wind readouts may take it too rather than spelling out "DEG".
            "°", "##.|##.|...|...|...",
            // U+003A COLON — authored by the chartplotter's own font table (navRig.js:71) with real
            // pixels, and used by its TTG/ETA clock (navRig.js:545 `fmtHM`). Transcribed rather than
            // blanked, for the same reason '°' was. Until S6 this character was deliberately held OUT
            // of the table so it would render as loud tofu (RigFontUnknownGlyphTests) — this is the
            // slice that needs it, so it gets its pixels on purpose rather than by accident.
            ":", "...|.#.|...|.#.|...",
            // U+0025 PERCENT: the radar's data panel prints GAIN/SEA/RAIN as `round(v*100)+'%'`
            // (radarRig.js:374-376) and radarRig's own font table — like every rig's — carries no '%',
            // so the rig's preview RENDERS IT AS A GAP. Pinned here as an authored blank on the '·'
            // precedent below rather than left to the unknown-glyph fallback, which would draw a tofu
            // block AND log an error on every repaint. ⚠ This is an ART CALL held at the rig's own
            // answer, not a limitation: '%' is perfectly drawable in 3×5, and if the owner wants the
            // sign to print it gets its pixels on this row and nothing else changes.
            "%", "...|...|...|...|...",
            // U+00B7: both wheelhouse standby plates author a '·' separator (noviRig.js:359,
            // capeRig.js:348) that the sources' own font tables do not carry, so every preview
            // RENDERS it as a gap — that gap is the approved look, pinned here as an authored blank
            // rather than left to the unknown-character fallback. If the art call is ever that the
            // dot should actually print, it gets its pixels on this row.
            "·", "...|...|...|...|...",
        };

        private static System.Collections.Generic.Dictionary<char, string[]> _font;

        // The unknown-character fallback — the ONE deliberate deviation from the rig sources. The
        // rigs resolve a glyph as `F[ch] || F[' ']` (consoleRig.js:99), and porting that faithfully
        // is exactly what shipped the " 100" tachs: a missing glyph drew nothing and nothing failed.
        // An unknown character instead rasterises as this solid 3×5 block — no table glyph is a full
        // block, so it cannot be read as an authored label — and is reported once per distinct
        // character (Text). A bad label therefore stays on screen, obviously wrong, without ever
        // throwing out of the draw path mid-repaint. The table itself stays the sources' character
        // set: a glyph is added when a rig label needs it, never to quiet the report.
        private static readonly string[] Tofu = { "###", "###", "###", "###", "###" };

        // Characters already reported. The draw path runs per repaint, so the console gets ONE error
        // per offending character, not one per glyph per frame; steady-state cost for a tofu'd label
        // is a failed HashSet.Add, no allocation (rule 7).
        private static System.Collections.Generic.HashSet<char> _reportedUnknown;

        private static string[] Glyph(char ch)
        {
            if (_font == null)
            {
                _font = new System.Collections.Generic.Dictionary<char, string[]>(Chars.Length / 2);
                for (int i = 0; i < Chars.Length; i += 2)
                    _font[Chars[i][0]] = Chars[i + 1].Split('|');
            }
            return _font.TryGetValue(ch, out string[] g) ? g : Tofu;
        }

        private static void ReportUnknownGlyph(char ch, string label)
        {
            if (_reportedUnknown == null)
                _reportedUnknown = new System.Collections.Generic.HashSet<char>();
            if (!_reportedUnknown.Add(ch)) return;
            Debug.LogError($"RigDrawUtil: no 3×5 glyph for U+{(int)ch:X4} '{ch}' (label \"{label}\") — " +
                           "drawn as a solid tofu block. Add the glyph to the font table if the " +
                           "character is intended; reported once per character.");
        }

        /// <summary>Forget which unknown characters have been reported, re-arming the once-per-character
        /// error. The dedup set is plain static state and survives consecutive EditMode test runs in one
        /// editor session (no domain reload between runs), so the tests that pin the loud path reset it
        /// rather than depend on being a character's first sighting.</summary>
        public static void ResetUnknownGlyphReports() => _reportedUnknown?.Clear();

        /// <summary>consoleRig.js:95 — pixel width of a string at scale s.</summary>
        public static int TextW(string str, int s) => str.Length * (3 * s + s) - s;

        /// <summary>consoleRig.js:96-103 — draw upper-cased text, top-left anchored.</summary>
        public static void Text(DrawSurface surf, string str, int x, int y, int s, Color32 col)
        {
            str = str.ToUpperInvariant();
            int cx = x;
            foreach (char ch in str)
            {
                string[] g = Glyph(ch);
                if (ReferenceEquals(g, Tofu)) ReportUnknownGlyph(ch, str);
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 3; c++)
                        if (g[r][c] == '#')
                            surf.FillRect(cx + c * s, y + r * s, s, s, col);
                cx += 3 * s + s;
            }
        }

        /// <summary>consoleRig.js:104 — horizontally centred text (y stays the TOP edge).</summary>
        public static void TextC(DrawSurface surf, string str, double cx, int y, int s, Color32 col)
            => Text(surf, str, DrawSurface.JsRound(cx - TextW(str, s) / 2.0), y, s, col);

        /// <summary>compassRig.js:49 — centred BOTH axes (the compass card's own variant).</summary>
        public static void TextCC(DrawSurface surf, string str, double cx, double cy, int s, Color32 col)
            => Text(surf, str, DrawSurface.JsRound(cx - TextW(str, s) / 2.0),
                    DrawSurface.JsRound(cy - (5.0 * s) / 2.0), s, col);

        // ---- rotated nearest-neighbour blit (the canvas translate/rotate/drawImage idiom, ----------
        // consoleRig.js:325-328 `blit`, with imageSmoothingEnabled=false → nearest sampling) ---------

        /// <summary>
        /// Composite <paramref name="src"/> onto <paramref name="dst"/> with the source pivot
        /// (<paramref name="srcPx"/>,<paramref name="srcPy"/>) landing on
        /// (<paramref name="dstX"/>,<paramref name="dstY"/>), rotated by <paramref name="angDeg"/>
        /// (canvas convention: + = clockwise on a y-down screen). Inverse-mapped, nearest sample,
        /// straight source-over of the source's own alpha.
        /// </summary>
        public static void BlitRotated(DrawSurface dst, DrawSurface src, double srcPx, double srcPy,
                                       double dstX, double dstY, double angDeg)
        {
            double a = angDeg * DEG;
            double ca = System.Math.Cos(a), sa = System.Math.Sin(a);
            // Destination AABB of the rotated source.
            double maxR = System.Math.Sqrt((double)src.Width * src.Width + (double)src.Height * src.Height);
            int x0 = System.Math.Max(0, (int)System.Math.Floor(dstX - maxR));
            int x1 = System.Math.Min(dst.Width - 1, (int)System.Math.Ceiling(dstX + maxR));
            int y0 = System.Math.Max(0, (int)System.Math.Floor(dstY - maxR));
            int y1 = System.Math.Min(dst.Height - 1, (int)System.Math.Ceiling(dstY + maxR));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    // Inverse rotation: destination offset back into source space.
                    double ox = x + 0.5 - dstX, oy = y + 0.5 - dstY;
                    int sx = (int)System.Math.Floor(ox * ca + oy * sa + srcPx);
                    int sy = (int)System.Math.Floor(-ox * sa + oy * ca + srcPy);
                    if (sx < 0 || sx >= src.Width || sy < 0 || sy >= src.Height) continue;
                    Color32 s = src.Pixels[sy * src.Width + sx];
                    if (s.a == 0) continue;
                    int di = y * dst.Width + x;
                    dst.Pixels[di] = s.a == 255 ? s : OverPx(dst.Pixels[di], s);
                }
            }
        }

        private static Color32 OverPx(Color32 dst, Color32 src)
        {
            int a = src.a, ia = 255 - a;
            int outA = a + (dst.a * ia + 127) / 255;
            if (outA == 0) return new Color32(0, 0, 0, 0);
            int r = (src.r * a * 255 + dst.r * dst.a * ia + 127 * outA) / (outA * 255);
            int g = (src.g * a * 255 + dst.g * dst.a * ia + 127 * outA) / (outA * 255);
            int b = (src.b * a * 255 + dst.b * dst.a * ia + 127 * outA) / (outA * 255);
            return new Color32((byte)r, (byte)g, (byte)b, (byte)outA);
        }

        /// <summary>Composite <paramref name="src"/> onto <paramref name="dst"/> at an offset (the
        /// canvas <c>drawImage(img, dx, dy)</c>) — the dash-layer compositor's workhorse.</summary>
        public static void CompositeAt(DrawSurface dst, DrawSurface src, int dx, int dy)
        {
            int x0 = System.Math.Max(0, -dx), y0 = System.Math.Max(0, -dy);
            int x1 = System.Math.Min(src.Width, dst.Width - dx);
            int y1 = System.Math.Min(src.Height, dst.Height - dy);
            for (int y = y0; y < y1; y++)
            {
                int srow = y * src.Width, drow = (y + dy) * dst.Width + dx;
                for (int x = x0; x < x1; x++)
                {
                    Color32 s = src.Pixels[srow + x];
                    if (s.a == 0) continue;
                    dst.Pixels[drow + x] = s.a == 255 ? s : OverPx(dst.Pixels[drow + x], s);
                }
            }
        }

        /// <summary>Copy a rect of <paramref name="src"/> into the SAME rect of <paramref name="dst"/>
        /// (equal-sized surfaces) — restores the chrome under a moving instrument before redrawing it.</summary>
        public static void CopyRect(DrawSurface dst, DrawSurface src, int x, int y, int w, int h)
        {
            int x0 = System.Math.Max(0, x), y0 = System.Math.Max(0, y);
            int x1 = System.Math.Min(dst.Width, x + w), y1 = System.Math.Min(dst.Height, y + h);
            for (int yy = y0; yy < y1; yy++)
                System.Array.Copy(src.Pixels, yy * src.Width + x0, dst.Pixels, yy * dst.Width + x0, x1 - x0);
        }

        /// <summary>
        /// Scanline polygon fill, integer spans, no AA (the canvas <c>beginPath/fill</c> of the
        /// compass rose — compassRig.js:130-136 — under the rigs' own no-AA rule; same idiom as the
        /// lever's facet fill). Even-odd, y-centre sampled, span rounded with <see cref="DrawSurface.JsRound"/>.
        /// </summary>
        public static void FillPoly(DrawSurface s, double[] xs, double[] ys, int n, Color32 c)
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
                    double ay = ys[i], by = ys[(i + 1) % n];
                    if ((ay <= yc) != (by <= yc))
                    {
                        double x = xs[i] + (yc - ay) / (by - ay) * (xs[(i + 1) % n] - xs[i]);
                        if (x < lo) lo = x;
                        if (x > hi) hi = x;
                    }
                }
                if (lo > hi) continue;
                int xa = DrawSurface.JsRound(lo), xb = DrawSurface.JsRound(hi);
                s.FillRect(xa, y, System.Math.Max(1, xb - xa), 1, c);
            }
        }

        /// <summary>Parse a #rrggbb (or rrggbb) hex into an opaque Color32 — the ramp loader.</summary>
        public static Color32 Hex(string hex)
        {
            if (hex[0] == '#') hex = hex.Substring(1);
            return new Color32(
                (byte)System.Convert.ToInt32(hex.Substring(0, 2), 16),
                (byte)System.Convert.ToInt32(hex.Substring(2, 2), 16),
                (byte)System.Convert.ToInt32(hex.Substring(4, 2), 16), 255);
        }

        public static Color32[] Ramp(params string[] hex)
        {
            var r = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) r[i] = Hex(hex[i]);
            return r;
        }
    }
}
