using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>What the ▲/▼ pushers currently adjust — the owner's Ruling A control mapping (ADR 0025
    /// S3). <b>Transient UI state, deliberately NOT persisted</b>: it is not a preference, it is where the
    /// player's thumb is.</summary>
    public enum FishRigAdjust
    {
        Range = 0,
        Alarm = 1,
    }

    /// <summary>
    /// The signals the finder's glass draws from — the rig's <c>render(opts)</c> bag
    /// (<c>fishRig.js:314-321</c>) plus the two the owner's rulings add (the kept shallow alarm and which
    /// mode the ▲/▼ pushers are in). A value, and <see cref="IEquatable{T}"/>, so the host can
    /// change-detect a whole frame's worth of glass with one compare and no allocation (rule 7).
    ///
    /// <para><b>The fish MARKS are not in here.</b> They are a list, and a list cannot be compared by
    /// value; the host tracks them with its own revision counter and hands them to
    /// <see cref="FishRigRender.DrawUnit"/> separately.</para>
    ///
    /// <para><b><see cref="Triggered"/> is a FIELD, not a derived property.</b> The finder keeps the depth
    /// sounder's alarm <em>unchanged</em> (Ruling E), so the trigger is computed once by the host through
    /// <c>DepthSounder.ShallowAlarm</c> and carried here. Re-deriving it (<c>Armed &amp;&amp; depth &lt;=
    /// alarm</c>) would be a second alarm rule that could silently drift from the sounder's — exactly what
    /// the ruling forbids.</para>
    /// </summary>
    public readonly struct FishRigState : System.IEquatable<FishRigState>
    {
        public readonly float Depth;          // metres (the transducer reading)
        public readonly float TempC;          // water temperature (⚠ placeholder — see GameConfig)
        public readonly float Range;          // vertical scale, metres — the glass's denominator
        public readonly float AlarmMetres;    // shallow set-point, metres
        public readonly bool Feet;            // display in feet
        public readonly bool Night;           // amber backlight vs day colour
        public readonly bool FishId;          // draw the depth tag beside each mark
        public readonly bool Armed;           // shallow alarm armed
        public readonly bool Triggered;       // DepthSounder.ShallowAlarm — computed by the host, ONE rule
        public readonly bool Blink;           // alarm flash phase
        public readonly double Phase;         // free-running scan phase, seconds (QUANTIZED by the host)
        public readonly FishRigAdjust Adjust; // what ▲/▼ are stepping right now
        public readonly float Sens;           // ⚠ placeholder — status strip signal bars, 0..1
        public readonly bool Link;            // ⚠ placeholder — transducer link
        public readonly float Batt;           // ⚠ placeholder — battery fill, 0..1
        public readonly float Volts;          // ⚠ placeholder — supply volts

        public FishRigState(float depth, float tempC, float range, float alarmMetres, bool feet, bool night,
                            bool fishId, bool armed, bool triggered, bool blink, double phase,
                            FishRigAdjust adjust, float sens, bool link, float batt, float volts)
        {
            Depth = depth; TempC = tempC; Range = range; AlarmMetres = alarmMetres;
            Feet = feet; Night = night; FishId = fishId; Armed = armed; Triggered = triggered;
            Blink = blink; Phase = phase; Adjust = adjust;
            Sens = sens; Link = link; Batt = batt; Volts = volts;
        }

        public bool Equals(FishRigState o)
            => Depth.Equals(o.Depth) && TempC.Equals(o.TempC) && Range.Equals(o.Range)
               && AlarmMetres.Equals(o.AlarmMetres) && Feet == o.Feet && Night == o.Night
               && FishId == o.FishId && Armed == o.Armed && Triggered == o.Triggered && Blink == o.Blink
               && Phase.Equals(o.Phase) && Adjust == o.Adjust && Sens.Equals(o.Sens) && Link == o.Link
               && Batt.Equals(o.Batt) && Volts.Equals(o.Volts);

        public override bool Equals(object obj) => obj is FishRigState o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Depth.GetHashCode();
                h = h * 397 ^ Range.GetHashCode();
                h = h * 397 ^ Phase.GetHashCode();
                h = h * 397 ^ AlarmMetres.GetHashCode();
                h = h * 397 ^ ((Feet ? 1 : 0) | (Night ? 2 : 0) | (FishId ? 4 : 0) | (Armed ? 8 : 0)
                               | (Triggered ? 16 : 0) | (Blink ? 32 : 0) | (Link ? 64 : 0) | ((int)Adjust << 7));
                return h;
            }
        }
    }

    /// <summary>
    /// Live C# port of the FISH FINDER's renderer — the immutable rig source is
    /// <c>docs/art/rigs/ui/fish-finder/Art/fishRig.js</c> (ADR 0025 names this rig as the one that
    /// <em>must</em> be a live renderer: "a scrolling waterfall with marks anywhere"). Method-by-method
    /// mirror of <c>paint</c>/<c>drawUnit</c>/<c>statusStrip</c>/<c>sonarView</c>/<c>ruler</c>/
    /// <c>readoutOverlay</c> and their primitives; every constant cites its source line. Pure geometry
    /// lives next door in <see cref="FishRigGeometry"/> so the hit boxes and the contour are testable
    /// without a canvas.
    ///
    /// <para><b>Box-parameterised, like the source.</b> <see cref="DrawUnit"/> renders the whole
    /// instrument at ANY size, so the same code draws the small card, the focused card, and — when S6
    /// composites a console dash — a flush-mounted brow instrument. ⚠ "Fits any box" is a GEOMETRY claim,
    /// not an aesthetic one: this rig is PORTRAIT (480×660) and a wide brow squashes the sonar column
    /// hard. See the committed brow proof.</para>
    ///
    /// <para><b>Three deliberate deviations from the source, all consequences of the no-AA rule</b>
    /// (<see cref="DrawSurface"/>): (1) the glass's rounded <c>Path2D</c> clip is replaced by re-stamping
    /// the corner insets with the bezel colour after the contents are drawn — same rows, same insets, no
    /// anti-aliased clip; (2) the 7-segment bars are scan-converted by pixel-centre coverage instead of an
    /// anti-aliased polygon fill; (3) <c>degMark</c>'s <c>clearRect</c> (<c>fishRig.js:71</c>) would punch
    /// a genuinely TRANSPARENT hole through the instrument to the game world behind it — on the rig's own
    /// page that reads as a ring, over live water it reads as a bug — so the inner pixels are captured and
    /// restored instead, which is what the ring is meant to look like and is strictly more faithful than
    /// the sounder's fixed-background guess.</para>
    ///
    /// <para><b>Perf (rule 7).</b> <see cref="DrawUnit"/> is O(sonar width) in <c>fillRect</c> spans and
    /// costs a full texture upload; it is NOT frame-rate work. The host repaints on a quantized
    /// <c>WaterfallHz</c> cadence, never per frame — see <see cref="FishFinderOverlayHost"/>.</para>
    /// </summary>
    public static class FishRigRender
    {
        /// <summary>Standalone sprite size — <c>fishRig.js:11</c>.</summary>
        public const int W = FishRigGeometry.W, H = FishRigGeometry.H;

        // ---- palettes (fishRig.js:14-37) ------------------------------------------------------------
        private static readonly Color32[] CASE = RigDrawUtil.Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] RESIN = RigDrawUtil.Ramp("050708", "0c1013", "141b1f");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("7fd6c9");
        private static readonly Color32 TEAK = RigDrawUtil.Hex("c9a86a");
        private static readonly Color32 BLUE = RigDrawUtil.Hex("5b93b8");
        private static readonly Color32[] RED = RigDrawUtil.Ramp("7c2a20", "b83b2e", "e0554a");
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        /// <summary>One sonar palette — DAY is a blue-green water column, NIGHT an amber backlight
        /// (fishRig.js:21-37). The <c>rgba()</c> entries keep their alpha separately because the source
        /// states them as CSS strings and they really are washes, not solids.</summary>
        private readonly struct Pal
        {
            public readonly Color32 Water1, Water2, Surf, Grid, DepthFg, TempFg, Plate, Tick, TickNum;
            public readonly Color32 Strip, StripFg, StripDim, KeyPlate, KeyEdge, Legend, Mark, Hard;
            public readonly Color32 FishOut, FishA, FishB, FishEye, FishTag;
            public readonly Color32[] Band;
            public readonly float GridAlpha, PlateAlpha;
            public readonly bool HasBloom;

            public Pal(string water1, string water2, string surf, string grid, float gridAlpha,
                       string depthFg, string tempFg, string plate, float plateAlpha,
                       string tick, string tickNum, string strip, string stripFg, string stripDim,
                       string keyPlate, string keyEdge, string legend, string mark, bool hasBloom,
                       string hard, string[] band,
                       string fishOut, string fishA, string fishB, string fishEye, string fishTag)
            {
                Water1 = RigDrawUtil.Hex(water1); Water2 = RigDrawUtil.Hex(water2);
                Surf = RigDrawUtil.Hex(surf); Grid = RigDrawUtil.Hex(grid); GridAlpha = gridAlpha;
                DepthFg = RigDrawUtil.Hex(depthFg); TempFg = RigDrawUtil.Hex(tempFg);
                Plate = RigDrawUtil.Hex(plate); PlateAlpha = plateAlpha;
                Tick = RigDrawUtil.Hex(tick); TickNum = RigDrawUtil.Hex(tickNum);
                Strip = RigDrawUtil.Hex(strip); StripFg = RigDrawUtil.Hex(stripFg);
                StripDim = RigDrawUtil.Hex(stripDim);
                KeyPlate = RigDrawUtil.Hex(keyPlate); KeyEdge = RigDrawUtil.Hex(keyEdge);
                Legend = RigDrawUtil.Hex(legend); Mark = RigDrawUtil.Hex(mark); HasBloom = hasBloom;
                Hard = RigDrawUtil.Hex(hard); Band = RigDrawUtil.Ramp(band);
                FishOut = RigDrawUtil.Hex(fishOut); FishA = RigDrawUtil.Hex(fishA);
                FishB = RigDrawUtil.Hex(fishB); FishEye = RigDrawUtil.Hex(fishEye);
                FishTag = RigDrawUtil.Hex(fishTag);
            }
        }

        private static readonly Pal DAY = new Pal(
            "1a4b5a", "081019", "e0554a", "96c8d2", 0.09f,          // grid rgba(150,200,210,0.09)
            "8fe6d8", "ffd08a", "060c10", 0.52f,                    // plate rgba(6,12,16,0.52)
            "6fc0dc", "e0776b", "0a141a", "8fe6d8", "3c5560",
            "2b5a76", "173648", "e2eef6", "bfe0f0", false,
            "ffe6a6", new[] { "4e9a4a", "c9b53c", "e0902f", "d0552c", "8a2a1c", "2a0e08" },
            "5a1c14", "e0554a", "e8863c", "2a0f0b", "ffe0b0");

        private static readonly Pal NIGHT = new Pal(
            "241a06", "080502", "ffb03a", "ffbe5a", 0.08f,          // grid rgba(255,190,90,0.08)
            "ffd77a", "ffbe6a", "0a0602", 0.55f,                    // plate rgba(10,6,2,0.55)
            "c79a3f", "ffb877", "150d02", "ffd77a", "6b501c",
            "3a2c0c", "201604", "ffe0a0", "ffd77a", true,
            "fff0c0", new[] { "7a7a24", "b59a2c", "c97a24", "b8481f", "7a2416", "1c0a05" },
            "5a2410", "ffb24a", "ffcf7a", "2a1608", "ffe6bc");

        private static readonly Color32 Bloom = RigDrawUtil.Hex("ffbe3d");   // fishRig.js:329

        // 7-segment map (fishRig.js:102-103). The finder draws ONLY lit segments — unlike the sounder
        // there is no faint "off" wash, because the sonar glass behind it is not a blank LCD.
        private static readonly Dictionary<char, string> SEG = new Dictionary<char, string>
        {
            { '0', "abcdef" }, { '1', "bc" },      { '2', "abged" },  { '3', "abgcd" },
            { '4', "fgbc" },   { '5', "afgcd" },   { '6', "afgedc" }, { '7', "abc" },
            { '8', "abcdefg" }, { '9', "abfgcd" }, { '-', "g" },      { ' ', "" },
        };

        // Scratch for the degree ring's pixel restore — 4 px is the largest the rig can ask for.
        private static readonly Color32[] DegScratch = new Color32[16];

        private static readonly List<SonarMark> NoMarks = new List<SonarMark>(0);

        // ---- the public entry points ----------------------------------------------------------------

        /// <summary>Paint the standalone sprite (<c>fishRig.js:367-371</c>) into <paramref name="s"/>. The
        /// surface may be any size — the 6/11/88/78% inset is taken from ITS dimensions, so the card state
        /// renders a smaller instrument rather than downscaling a 480×660 texture.</summary>
        public static void Render(DrawSurface s, in FishRigState o, IReadOnlyList<SonarMark> marks)
        {
            s.Clear();
            RigRect mount = FishRigGeometry.StandaloneMount(s.Width, s.Height);
            DrawUnit(s, mount.X, mount.Y, mount.W, mount.H, in o, marks);
        }

        /// <summary>The whole instrument, fitted to a mount rect (<c>fishRig.js:314-364</c>). Public so a
        /// console dash can flush-mount it in its brow without going through the standalone sprite.</summary>
        public static void DrawUnit(DrawSurface s, int x, int y, int ww, int hh, in FishRigState o,
                                    IReadOnlyList<SonarMark> marks)
        {
            marks ??= NoMarks;
            Pal p = o.Night ? NIGHT : DAY;
            int fs = hh >= 200 ? 3 : hh >= 112 ? 2 : 1;                     // fishRig.js:323
            FishRigLayout L = FishRigGeometry.Layout(x, y, ww, hh);

            // night bloom behind the glass — fishRig.js:327-331 (stop alpha 0.30; the sounder's is 0.34)
            if (p.HasBloom)
            {
                double gcx = L.Lcd.X + L.Lcd.W / 2.0, gcy = L.Lcd.Y + L.Lcd.H / 2.0;
                s.BlendRadial(x - 6, y - 6, ww + 12, hh + 12, gcx, gcy,
                              L.Lcd.H * 0.1, L.Lcd.W * 0.85, Bloom, 0.30f);
            }

            // case body — fishRig.js:333-337
            int cr = Max(4, DrawSurface.JsRound(hh * 0.11));
            RigDrawUtil.RRect(s, x, y, ww, hh, cr, RESIN[0]);
            RigDrawUtil.RRect(s, x + 1, y + 1, ww - 2, hh - 2, cr - 1, CASE[1]);
            RigDrawUtil.RRect(s, x + 2, y + 2, ww - 4, Max(2, DrawSurface.JsRound(hh * 0.10)), cr - 2, CASE[3]);
            RigDrawUtil.RRect(s, x + 2, y + hh - Max(2, DrawSurface.JsRound(hh * 0.06)), ww - 4,
                              Max(2, DrawSurface.JsRound(hh * 0.05)), 3, CASE[0]);

            // recessed LCD — fishRig.js:339-352
            int lr = Max(3, DrawSurface.JsRound(L.Lcd.H * 0.06));
            RigDrawUtil.RRect(s, L.Lcd.X - 3, L.Lcd.Y - 3, L.Lcd.W + 6, L.Lcd.H + 6, lr + 1, RESIN[0]);
            RigDrawUtil.RRect(s, L.Lcd.X - 2, L.Lcd.Y - 2, L.Lcd.W + 4, L.Lcd.H + 4, lr + 1, RESIN[2]);

            StatusStrip(s, in L.Status, in o, in p, fs);
            SonarView(s, in L.Sonar, in o, in p, marks);
            Ruler(s, in L.Ruler, in o, in p, fs);
            ReadoutOverlay(s, in L.Sonar, in o, in p);
            ClipGlassCorners(s, in L.Lcd, lr, RESIN[2]);     // deviation (1) — see the type doc

            s.BlendRect(L.Lcd.X + 3, L.Lcd.Y + 2, L.Lcd.W - 6, 1, White, 0.08f);   // glass glare

            // side pushers — fishRig.js:355-357, LABELLED BY MODE (Ruling A). The rig draws MODE /
            // RANGE▲ / RANGE▼; the owner's mapping makes MODE cycle what ▲/▼ adjust, so the two arrow
            // keys carry the active mode's word. Same three keys, same three glyphs, no new art.
            string stepLabel = o.Adjust == FishRigAdjust.Alarm ? "ALARM" : "RANGE";
            Key(s, in L.Button0, "MODE", Glyph.Set, in p, fs, o.Night);
            Key(s, in L.Button1, stepLabel, Glyph.Up, in p, fs, o.Night);
            Key(s, in L.Button2, stepLabel, Glyph.Down, in p, fs, o.Night);

            // brand strip (original wordmark) — fishRig.js:360-363
            int bs = Max(1, fs - 1);
            int by = L.Brand.Y + DrawSurface.JsRound((L.Brand.H - 5 * bs) / 2.0);
            RigDrawUtil.Text(s, "FF-2", L.Brand.X + 1, by, bs, TEAL);
            RigDrawUtil.TextC(s, "HARBOUR", x + ww / 2.0, by, bs, TEAK);
            RigDrawUtil.Text(s, "SONAR", L.Brand.X + L.Brand.W - RigDrawUtil.TextW("SONAR", bs), by, bs, BLUE);
        }

        // ---- status strip (fishRig.js:200-219) ------------------------------------------------------

        private static void StatusStrip(DrawSurface s, in RigRect S, in FishRigState o, in Pal p, int fs)
        {
            s.FillRect(S.X, S.Y, S.W, S.H, p.Strip);
            s.BlendRect(S.X, S.Y, S.W, 1, White, 0.05f);

            int midY = S.Y + DrawSurface.JsRound((S.H - 5 * fs) / 2.0);
            int barBase = S.Y + S.H - 3;
            int unit = Max(1, fs);
            int x = S.X + 4;

            RigDrawUtil.Text(s, "S", x, midY, fs, p.StripFg);
            x += RigDrawUtil.TextW("S", fs) + 3;
            int sBars = DrawSurface.JsRound(4 * Clamp01(o.Sens));
            for (int i = 0; i < 4; i++)
            {
                int bh = 2 + i * Max(2, fs);
                s.FillRect(x + i * (unit + 1), barBase - bh, unit, bh, i < sBars ? p.StripFg : p.StripDim);
            }
            x += 4 * (unit + 1) + 7;

            RigDrawUtil.Text(s, "T", x, midY, fs, p.StripFg);       // transducer link
            x += RigDrawUtil.TextW("T", fs) + 3;
            int lBars = o.Link ? 3 : 0;
            for (int i = 0; i < 3; i++)
            {
                int bh = 2 + i * Max(2, fs);
                s.FillRect(x + i * (unit + 1), barBase - bh, unit, bh, i < lBars ? p.StripFg : p.StripDim);
            }

            // battery + volts (right)
            string volts = RigNumberFormat.FixedOne(o.Volts) + "V";
            int vw = RigDrawUtil.TextW(volts, fs);
            RigDrawUtil.Text(s, volts, DrawSurface.JsRound(S.X + S.W - 4 - vw), midY, fs, p.StripFg);
            int bw = Max(8, 6 * unit), bhh = Max(5, 4 * unit);
            int bx = DrawSurface.JsRound(S.X + S.W - 4 - vw - 6 - bw);
            int byy = S.Y + DrawSurface.JsRound((S.H - bhh) / 2.0);
            RRectStroke(s, bx, byy, bw, bhh, 1, p.StripFg);
            s.FillRect(bx + bw, byy + 1, Max(1, unit), bhh - 2, p.StripFg);
            int fill = DrawSurface.JsRound((bw - 2) * Clamp01(o.Batt));
            s.FillRect(bx + 1, byy + 1, fill, bhh - 2, o.Batt < 0.2f ? RED[2] : p.StripFg);
        }

        // ---- the bottom contour + suspended fish (fishRig.js:225-279) --------------------------------

        private static void SonarView(DrawSurface s, in RigRect S, in FishRigState o, in Pal p,
                                      IReadOnlyList<SonarMark> marks)
        {
            // water column gradient (a canvas linear gradient down the box, sampled at the pixel centre)
            for (int row = 0; row < S.H; row++)
            {
                double t = S.H > 0 ? (row + 0.5) / S.H : 0.0;
                s.FillRect(S.X, S.Y + row, S.W, 1, DrawSurface.Lerp(p.Water1, p.Water2, t));
            }
            // faint horizontal grid at the ruler divisions
            for (int i = 1; i < 5; i++)
                s.BlendRect(S.X, DrawSurface.JsRound(S.Y + S.H * i / 5.0), S.W, 1, p.Grid, p.GridAlpha);
            // surface echo line
            s.FillRect(S.X, S.Y + 1, S.W, 1, p.Surf);
            s.BlendRect(S.X, S.Y, S.W, 1, White, 0.10f);

            // bottom contour — the average sits at the measured depth on the scale. NOT a history buffer:
            // three sine trains around depth/range, recomputed every repaint (FishRigGeometry.BottomAt).
            double ph = o.Phase;
            double depthFrac = FishRigGeometry.DepthFrac(o.Depth, o.Range);
            double phFloor = System.Math.Floor(ph);
            Color32[] bands = p.Band;
            int floorY = S.Y + S.H;
            for (int px = 0; px < S.W; px++)
            {
                double u = (double)px / S.W;
                int cx = S.X + px;
                int by = DrawSurface.JsRound(S.Y + FishRigGeometry.BottomAt(u, ph, depthFrac) * S.H);
                int hgt = floorY - by;
                if (hgt <= 0) continue;

                // suspended green fuzz just above the return (weed / thermocline)
                if (FishRigGeometry.HashRnd(px * 2.3 + phFloor) > 0.86)
                    s.FillRect(cx, by - 1 - (int)System.Math.Floor(FishRigGeometry.HashRnd(px) * 3), 1, 1, bands[0]);

                // colour bands top(soft) → deep(hard), boundaries jittered per column
                double j = FishRigGeometry.HashRnd(px) - 0.5;
                double s0 = 0.0, s1 = 0.12 + 0.04 * j, s2 = 0.30 + 0.05 * j,
                       s3 = 0.52 + 0.05 * j, s4 = 0.74 + 0.04 * j, s5 = 1.0;
                Band(s, cx, by, hgt, s0, s1, bands[0]);
                Band(s, cx, by, hgt, s1, s2, bands[1]);
                Band(s, cx, by, hgt, s2, s3, bands[2]);
                Band(s, cx, by, hgt, s3, s4, bands[3]);
                Band(s, cx, by, hgt, s4, s5, bands[4]);
                // ⚠ The sixth band is a NO-OP in the source and faithfully stays one here: the loop runs
                // once per band (6) but there are only 6 stops, so the last pass spans stops[5]=1.0 to an
                // undefined stops[6] (⇒ 1.0) and draws zero rows. `bands[5]` — the near-black — never
                // reaches the glass. Ported as authored; a "fix" here would change the rig's look.
                Band(s, cx, by, hgt, s5, 1.0, bands[5]);

                s.FillRect(cx, by, 1, 1, p.Hard);             // hard-bottom top edge

                // sparse dark speckle for texture
                if (FishRigGeometry.HashRnd(px * 5.1 + 7) > 0.8)
                {
                    int sy = by + 2 + (int)System.Math.Floor(
                        FishRigGeometry.HashRnd(px * 1.7) * System.Math.Max(1, hgt - 3));
                    s.BlendRect(cx, sy, 1, 1, Black, 0.28f);
                }
            }
            // newest-ping brightening at the right edge
            s.BlendRect(S.X + S.W - 2, S.Y, 2, S.H, White, 0.05f);

            // ---- the shallow set-point, as a line across the water column -----------------------------
            // ⚠ NOT in the rig, and NOT in Ruling E (which covers the TRIGGERED display). Ruling A puts
            // the alarm on the ▲/▼ pushers, and without this the player is adjusting a number they cannot
            // see — the sounder shows it as "AL n.n", the finder has no such chrome. Drawn with the rig's
            // OWN grid-line idiom at the set-point's place on the same scale as the contour, so the
            // relationship reads at a glance. Flagged in the PR for the owner to veto.
            if (o.Armed && o.Range > 0f)
            {
                double al = o.AlarmMetres / o.Range;
                if (al > 0.0 && al < 1.0)
                    s.BlendRect(S.X, DrawSurface.JsRound(S.Y + al * S.H), S.W, 1, RED[2], 0.55f);
            }

            // ---- fish marks (fishRig.js:267-278) -----------------------------------------------------
            int tagScale = S.H >= 150 ? 2 : 1;
            int drawn = 0;
            for (int i = 0; i < marks.Count; i++)
            {
                SonarMark m = marks[i];
                // METRES → the rig's normalised column position, AT PAINT TIME (the seam never stores a
                // normalised depth). A school deeper than the current scale is simply off the picture —
                // which is the honest thing for a range change to do.
                double y01 = m.DepthMetres / o.Range;
                if (y01 < 0.0 || y01 > 1.0) continue;

                FishRigGeometry.FishGeom(in S, m.X01, y01, m.Size, out double fx, out double fy, out double half);
                int dir = (drawn % 2 == 0) ? 1 : -1;
                drawn++;
                Fish(s, fx, fy, half, dir, in p);

                if (o.FishId)
                {
                    // The tag prints the mark's OWN metres, not y·range: the rig round-trips through the
                    // normalised value because that is all it has, we have the seam's number and a
                    // round-trip can only lose a digit.
                    string tv = RigNumberFormat.FmtSet(m.DepthMetres, o.Feet);
                    int tw = RigDrawUtil.TextW(tv, tagScale);
                    int tx = DrawSurface.JsRound(fx - tw / 2.0);
                    int ty = DrawSurface.JsRound(fy - half * 0.55 - 6 * tagScale);
                    s.BlendRect(tx - 2, ty - 1, tw + 4, 5 * tagScale + 2, p.Plate, p.PlateAlpha);
                    RigDrawUtil.Text(s, tv, tx, ty, tagScale, p.FishTag);
                }
            }
        }

        /// <summary>One colour band of the bottom return — <c>fishRig.js:254-258</c>.</summary>
        private static void Band(DrawSurface s, int cx, int by, int hgt, double from, double to, Color32 c)
        {
            int y0 = DrawSurface.JsRound(by + hgt * FishRigGeometry.Clamp(from, 0, 1));
            int y1 = DrawSurface.JsRound(by + hgt * FishRigGeometry.Clamp(to, 0, 1));
            if (y1 > y0) s.FillRect(cx, y0, 1, y1 - y0, c);
        }

        // ---- a single fish mark (fishRig.js:179-197) -------------------------------------------------

        private static void Fish(DrawSurface s, double cxD, double cyD, double half, int dir, in Pal p)
        {
            int cx = DrawSurface.JsRound(cxD), cy = DrawSurface.JsRound(cyD);
            double rx = half, ry = System.Math.Max(2.0, half * 0.52);
            Color32 body = half > ry * 2.3 ? p.FishA : p.FishB;

            RigDrawUtil.Ellipse(s, cx, cy, DrawSurface.JsRound(rx + 1), DrawSurface.JsRound(ry + 1), p.FishOut);

            // tail (behind, on the −dir side)
            double tx = cx - dir * (rx - 1);
            double tl = System.Math.Max(2.0, half * 0.7), th = System.Math.Max(2.0, ry * 1.15);
            for (int i = 0; i < tl; i++)
            {
                int hh = DrawSurface.JsRound(th * (i / tl));
                FillRectF(s, tx - dir * i, cy - hh, 1, hh * 2 + 1, p.FishOut);
            }
            for (int i = 0; i < tl - 1; i++)
            {
                int hh = DrawSurface.JsRound((th - 1) * (i / tl));
                FillRectF(s, tx - dir * i, cy - hh, 1, hh * 2 + 1, body);
            }

            // body + belly sheen
            RigDrawUtil.Ellipse(s, cx, cy, DrawSurface.JsRound(rx), DrawSurface.JsRound(ry), body);
            if (ry >= 3.0)
                RigDrawUtil.Ellipse(s, cx, cy + DrawSurface.JsRound(ry * 0.45), DrawSurface.JsRound(rx * 0.7),
                                    Max(1, DrawSurface.JsRound(ry * 0.35)), p.FishB);

            // eye near the front
            int ex = cx + dir * DrawSurface.JsRound(rx * 0.55);
            if (ry >= 2.0)
            {
                s.FillRect(ex - 1, cy - 1, 2, 2, White);
                s.FillRect(ex, cy - 1, 1, 1, p.FishEye);
            }
        }

        // ---- right-edge depth ruler (fishRig.js:282-294) ---------------------------------------------

        private static void Ruler(DrawSurface s, in RigRect Rc, in FishRigState o, in Pal p, int fs)
        {
            s.BlendRect(Rc.X, Rc.Y, Rc.W, Rc.H, Black, 0.28f);
            s.BlendRect(Rc.X, Rc.Y, 1, Rc.H, p.Grid, p.GridAlpha);

            const int segs = 5;
            double unit = o.Feet ? o.Range * RigNumberFormat.M2FT : o.Range;
            for (int i = 0; i <= segs; i++)
            {
                int y = DrawSurface.JsRound(Rc.Y + Rc.H * (double)i / segs) - (i == segs ? 1 : 0);
                s.FillRect(Rc.X + 1, y, DrawSurface.JsRound(Rc.W * 0.45), 1, p.Tick);
                string lbl = RigNumberFormat.JsRoundToString(unit * i / segs);
                int ly = (int)FishRigGeometry.Clamp(
                    y - (i == 0 ? 0 : (i == segs ? 5 * fs : DrawSurface.JsRound(5 * fs / 2.0))),
                    Rc.Y, Rc.Y + Rc.H - 5 * fs);
                RigDrawUtil.Text(s, lbl, Rc.X + DrawSurface.JsRound(Rc.W * 0.5) + 1, ly, fs, p.TickNum);
                if (i < segs)
                {
                    int my = DrawSurface.JsRound(Rc.Y + Rc.H * (i + 0.5) / segs);
                    s.FillRect(Rc.X + 1, my, DrawSurface.JsRound(Rc.W * 0.28), 1, p.Tick);
                }
            }

            // the shallow set-point's tick, in the alarm's own red — the ruler half of the marker the
            // sonar draws (see the note in SonarView). Nothing is drawn when the alarm is disarmed.
            if (o.Armed && o.Range > 0f)
            {
                double al = o.AlarmMetres / o.Range;
                if (al > 0.0 && al < 1.0)
                    s.FillRect(Rc.X + 1, DrawSurface.JsRound(Rc.Y + al * Rc.H),
                               DrawSurface.JsRound(Rc.W * 0.45), 1, RED[2]);
            }
        }

        // ---- big depth + temp overlay (fishRig.js:297-311) -------------------------------------------

        private static void ReadoutOverlay(DrawSurface s, in RigRect S, in FishRigState o, in Pal p)
        {
            string str = RigNumberFormat.FmtDepth(o.Depth, o.Feet);
            string unit = o.Feet ? "FT" : "M";
            int dh = DrawSurface.JsRound(System.Math.Min(S.W, S.H) * 0.24);
            int dw = DrawSurface.JsRound(dh * 0.56);
            int gap = Max(2, DrawSurface.JsRound(dw * 0.3));
            int dpW = Max(2, DrawSurface.JsRound(dw * 0.26));
            int us = Max(1, S.H >= 150 ? 2 : 1);

            int total = NumW(str, dw, gap, dpW);
            int nx = S.X + Max(4, DrawSurface.JsRound(S.W * 0.03));
            int ny = S.Y + Max(4, DrawSurface.JsRound(S.W * 0.045));

            int uW = RigDrawUtil.TextW(unit, us);
            int plW = total + uW + us + 8, plH = dh + 5 * us + 10;
            RigDrawUtil.RRect(s, nx - 4, ny - 4, plW + 6, plH, 3, p.Plate, p.PlateAlpha);

            // Ruling E: a sounding alarm flashes the big readout in the rig's OWN red (fishRig.js:18) —
            // no invented banner, no new art. The blink phase is DepthSounder.BlinkPhase, the one the
            // sounder uses, so the two instruments can never flash out of step.
            Color32 numCol = o.Triggered && o.Blink ? RED[2] : p.DepthFg;
            DrawNum(s, str, nx, ny, dw, dh, gap, dpW, numCol);
            RigDrawUtil.Text(s, unit, nx + total + us, ny + 2, us, p.DepthFg);   // the WORD never flashes

            // temp under it
            string tv = RigNumberFormat.FixedOne(o.TempC);
            int ts = us;
            int tRow = ny + dh + 5;
            RigDrawUtil.Text(s, tv, nx, tRow, ts, p.TempFg);
            int tx = nx + RigDrawUtil.TextW(tv, ts);       // the source's text() returns exactly this
            int k = Max(2, ts);
            DegMark(s, tx + ts + 1, tRow, k, p.TempFg);
            RigDrawUtil.Text(s, "C", tx + ts + 1 + k + ts, tRow, ts, p.TempFg);
        }

        /// <summary>The little superscript degree ring (<c>fishRig.js:71</c>). Deviation (3): the source's
        /// <c>clearRect</c> would punch a transparent hole through the instrument; we capture and restore
        /// the pixels instead, which is what the ring is meant to look like.</summary>
        private static void DegMark(DrawSurface s, int x, int y, int k, Color32 col)
        {
            int iw = Max(1, k - 2), ih = Max(1, k - 2);
            int n = iw * ih;
            bool captured = n <= DegScratch.Length;
            if (captured)
            {
                int i = 0;
                for (int yy = 0; yy < ih; yy++)
                    for (int xx = 0; xx < iw; xx++)
                    {
                        int px = x + 1 + xx, py = y + 1 + yy;
                        DegScratch[i++] = (px >= 0 && px < s.Width && py >= 0 && py < s.Height)
                            ? s.Pixels[py * s.Width + px]
                            : default;
                    }
            }
            s.FillRect(x, y, k, k, col);
            if (!captured) return;
            {
                int i = 0;
                for (int yy = 0; yy < ih; yy++)
                    for (int xx = 0; xx < iw; xx++)
                    {
                        int px = x + 1 + xx, py = y + 1 + yy;
                        if (px >= 0 && px < s.Width && py >= 0 && py < s.Height)
                            s.Pixels[py * s.Width + px] = DegScratch[i];
                        i++;
                    }
            }
        }

        // ---- one pusher key (fishRig.js:163-176) -----------------------------------------------------

        private enum Glyph { Set, Up, Down }

        private static void Key(DrawSurface s, in RigRect b, string label, Glyph glyph, in Pal p,
                                int fs, bool lit)
        {
            RigDrawUtil.RRect(s, b.X, b.Y, b.W, b.H, Max(3, DrawSurface.JsRound(b.H * 0.24)), CASE[0]);
            int ix = b.X + 2, iy = b.Y + 2, iw = b.W - 4, ih = b.H - 4;
            int r = Max(2, DrawSurface.JsRound(ih * 0.22));
            RigDrawUtil.RRect(s, ix, iy, iw, ih, r, p.KeyEdge);
            RigDrawUtil.RRect(s, ix, iy, iw, ih - 2, r, p.KeyPlate);
            RigDrawUtil.RRect(s, ix + 2, iy + 2, iw - 4, Max(1, DrawSurface.JsRound(ih * 0.22)), r - 1,
                              White, 0.14f);

            double cx = b.X + b.W / 2.0;
            int ls = Max(1, fs - 1);                                        // fishRig.js:171
            RigDrawUtil.Text(s, label, DrawSurface.JsRound(cx - RigDrawUtil.TextW(label, ls) / 2.0),
                             DrawSurface.JsRound(iy + ih * 0.24), ls, p.Legend);

            int dcy = DrawSurface.JsRound(iy + ih * 0.70);
            int sz = Max(2, ls);
            if (glyph == Glyph.Up)
                for (int i = 0; i < sz + 1; i++) FillRectF(s, cx - i, dcy - sz + i, i * 2 + 1, 1, p.Mark);
            else if (glyph == Glyph.Down)
                for (int i = 0; i < sz + 1; i++)
                    FillRectF(s, cx - (sz - i), dcy - sz + i, (sz - i) * 2 + 1, 1, p.Mark);
            else
            {
                int q = sz + 1;
                for (int i = -q; i <= q; i++)
                {
                    int wd = q - System.Math.Abs(i);
                    FillRectF(s, cx - wd, dcy + i, wd * 2 + 1, 1, p.Mark);
                }
            }

            if (lit) RigDrawUtil.RRect(s, ix, iy, iw, ih - 2, r, new Color32(255, 190, 60, 255), 0.10f);
        }

        // ---- 7-segment numerals (fishRig.js:110-125) -------------------------------------------------

        private static int NumW(string str, int dw, int gap, int dpW)
        {
            int w = 0;
            for (int i = 0; i < str.Length; i++) w += (str[i] == '.' ? dpW : dw) + gap;
            return Max(0, w - gap);
        }

        private static void DrawNum(DrawSurface s, string str, double x, double y, int dw, int dh,
                                    int gap, int dpW, Color32 onCol)
        {
            double cx = x;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '.')
                {
                    s.FillRect(DrawSurface.JsRound(cx), DrawSurface.JsRound(y + dh - dpW), dpW, dpW, onCol);
                    cx += dpW + gap;
                }
                else
                {
                    Seg7(s, DrawSurface.JsRound(cx), y, dw, dh, str[i], onCol);
                    cx += dw + gap;
                }
            }
        }

        private static void Seg7(DrawSurface s, double x, double y, double w, double h, char ch, Color32 onCol)
        {
            string on = SEG.TryGetValue(ch, out string found) ? found : "";
            double t = System.Math.Max(2.0, w * 0.2);                       // fishRig.js:111 (the sounder's is 0.19)
            double midY = y + h / 2.0, hlen = w - t * 0.7, vlen = h / 2.0 - t * 0.7;

            if (on.IndexOf('a') >= 0) HBar(s, x + w / 2.0, y, hlen, t, onCol);
            if (on.IndexOf('g') >= 0) HBar(s, x + w / 2.0, midY, hlen, t, onCol);
            if (on.IndexOf('d') >= 0) HBar(s, x + w / 2.0, y + h, hlen, t, onCol);
            if (on.IndexOf('f') >= 0) VBar(s, x, y + h / 4.0, vlen, t, onCol);
            if (on.IndexOf('b') >= 0) VBar(s, x + w, y + h / 4.0, vlen, t, onCol);
            if (on.IndexOf('e') >= 0) VBar(s, x, y + h * 3.0 / 4.0, vlen, t, onCol);
            if (on.IndexOf('c') >= 0) VBar(s, x + w, y + h * 3.0 / 4.0, vlen, t, onCol);
        }

        /// <summary>The horizontal segment: a flat hexagon whose left/right edges slope 1:1 into the tips
        /// (fishRig.js:104-106). Scan-converted by pixel-centre coverage — deviation (2), no AA.</summary>
        private static void HBar(DrawSurface s, double cx, double cy, double len, double t, Color32 col)
        {
            double half = t / 2.0, l = len / 2.0;
            SpanBounds(cy - half, cy + half, out int y0, out int y1);
            for (int yy = y0; yy <= y1; yy++)
            {
                double dy = System.Math.Abs(yy + 0.5 - cy);
                double xl = cx - l + dy, xr = cx + l - dy;
                if (xr <= xl) continue;
                SpanBounds(xl, xr, out int x0, out int x1);
                if (x1 >= x0) s.FillRect(x0, yy, x1 - x0 + 1, 1, col);
            }
        }

        /// <summary>The vertical segment: a tall hexagon, flat-sided in the middle, tapering to points
        /// (fishRig.js:107-109).</summary>
        private static void VBar(DrawSurface s, double cx, double cy, double len, double t, Color32 col)
        {
            double half = t / 2.0, l = len / 2.0;
            SpanBounds(cy - l, cy + l, out int y0, out int y1);
            for (int yy = y0; yy <= y1; yy++)
            {
                double dy = System.Math.Abs(yy + 0.5 - cy);
                double hw = System.Math.Min(half, l - dy);
                if (hw <= 0.0) continue;
                SpanBounds(cx - hw, cx + hw, out int x0, out int x1);
                if (x1 >= x0) s.FillRect(x0, yy, x1 - x0 + 1, 1, col);
            }
        }

        // ---- primitives the shared util does not carry -----------------------------------------------

        /// <summary>1px rounded outline (<c>fishRig.js:85-91</c>) — the battery shell.</summary>
        private static void RRectStroke(DrawSurface s, int x, int y, int w, int h, int r, Color32 fill)
        {
            r = Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(w, h) / 2.0)));
            for (int v = 0; v < h; v++)
            {
                int i = RigDrawUtil.RowInset(v, h, r);
                if (v == 0 || v == h - 1) s.FillRect(x + i, y + v, w - 2 * i, 1, fill);
                else
                {
                    s.FillRect(x + i, y + v, 1, 1, fill);
                    s.FillRect(x + w - 1 - i, y + v, 1, 1, fill);
                }
            }
        }

        /// <summary>Deviation (1): the source clips the glass contents to a rounded <c>Path2D</c>
        /// (<c>fishRig.js:343-346</c>). With no anti-aliasing and no path rasteriser, the identical result
        /// is to draw the contents square and then re-stamp the corner insets with the bezel colour the
        /// clip would have left showing. Same rows, same insets, no AA.</summary>
        private static void ClipGlassCorners(DrawSurface s, in RigRect lcd, int r, Color32 bezel)
        {
            r = Max(0, System.Math.Min(r, (int)System.Math.Floor(System.Math.Min(lcd.W, lcd.H) / 2.0)));
            if (r <= 0) return;
            for (int v = 0; v < lcd.H; v++)
            {
                int i = RigDrawUtil.RowInset(v, lcd.H, r);
                if (i <= 0) continue;
                s.FillRect(lcd.X, lcd.Y + v, i, 1, bezel);
                s.FillRect(lcd.X + lcd.W - i, lcd.Y + v, i, 1, bezel);
            }
        }

        /// <summary>A <c>fillRect</c> that may land on fractional coordinates (the pusher glyphs centre on
        /// a half-pixel when the button column is an odd width). Fills every pixel whose CENTRE lies in the
        /// half-open rect — canvas's coverage rule with anti-aliasing switched off.</summary>
        private static void FillRectF(DrawSurface s, double x, double y, double w, double h, Color32 col)
        {
            SpanBounds(x, x + w, out int x0, out int x1);
            SpanBounds(y, y + h, out int y0, out int y1);
            if (x1 < x0 || y1 < y0) return;
            s.FillRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1, col);
        }

        /// <summary>Pixel indices whose centres lie in the half-open interval [lo, hi).</summary>
        private static void SpanBounds(double lo, double hi, out int i0, out int i1)
        {
            i0 = (int)System.Math.Ceiling(lo - 0.5);
            i1 = (int)System.Math.Ceiling(hi - 0.5) - 1;
        }

        private static int Max(int a, int b) => a > b ? a : b;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
