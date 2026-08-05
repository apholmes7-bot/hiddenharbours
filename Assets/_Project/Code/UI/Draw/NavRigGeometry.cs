using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chartplotter's LAYOUT and its world↔screen transform, lifted as data from
    /// <c>docs/art/rigs/ui/chartplotter/Art/navRig.js</c> (ADR 0025 S6). Pure and engine-light, so the
    /// whole geometry is EditMode-testable with no canvas — the S1/S2/S3b discipline.
    ///
    /// <para><b>Two faces, one layout function</b> (navRig.js:238 <c>layout</c>): a compact CONSOLE face
    /// (chart + slim data/route bars) and a MAX face that also carries a layer/tool rail, a waypoint and
    /// route manager column, and a depth-profile strip. Everything is a fraction of the box it is drawn
    /// into, so the same code serves the brow mount and the expanded card.</para>
    ///
    /// <para><b>The view transform is the rig's</b> (navRig.js:229-235) with ONE substitution: the rig
    /// scales by <c>chart.w / (rangeNM * PXNM)</c> because its world is measured in chart pixels at
    /// 50 px per nautical mile. This world is measured in METRES, so the same expression becomes
    /// <c>chart.w / rangeMetres</c> with the range converted through <see cref="NavMath"/>. The rotation,
    /// the centring and the inverse are unchanged.</para>
    /// </summary>
    public static class NavRigGeometry
    {
        /// <summary>Native size of the standalone CONSOLE face (navRig.js:15).</summary>
        public const int ConsoleW = 760, ConsoleH = 480;

        /// <summary>Native size of the standalone MAX face (navRig.js:15).</summary>
        public const int MaxW = 980, MaxH = 648;

        /// <summary>Which of the rig's two faces is being drawn.</summary>
        public enum Face { Console, Max }

        /// <summary>Chart orientation — north-up, or the boat's course to the top (navRig.js:230).</summary>
        public enum Orient { North, Head }

        // ---- layout (navRig.js:238-266) ---------------------------------------------------------------

        /// <summary>Every box the instrument draws into, for one face at one size. All in the caller's
        /// pixel space, so a mount rect and a full card differ only in what is passed in.</summary>
        public readonly struct Layout
        {
            public readonly RectInt Lcd, Col, KnobBox, Brand, TopBar, BotBar, Chart;
            public readonly RectInt Rail, Right, Profile;      // MAX only; zero-size on the console face
            public readonly RectInt Key0, Key1, Key2, Key3;
            public readonly int Pad, BrandH, ColW;
            public readonly bool HasMax;

            public Layout(RectInt lcd, RectInt col, RectInt knob, RectInt brand, RectInt top, RectInt bot,
                          RectInt chart, RectInt rail, RectInt right, RectInt profile,
                          RectInt k0, RectInt k1, RectInt k2, RectInt k3,
                          int pad, int brandH, int colW, bool hasMax)
            {
                Lcd = lcd; Col = col; KnobBox = knob; Brand = brand; TopBar = top; BotBar = bot;
                Chart = chart; Rail = rail; Right = right; Profile = profile;
                Key0 = k0; Key1 = k1; Key2 = k2; Key3 = k3;
                Pad = pad; BrandH = brandH; ColW = colW; HasMax = hasMax;
            }

            /// <summary>The four side pushers, in the rig's order (MAX/CNSL · MARK · IN · OUT).</summary>
            public RectInt Key(int i) => i switch { 0 => Key0, 1 => Key1, 2 => Key2, _ => Key3 };
        }

        /// <summary>
        /// Compute every box for a face drawn into (x, y, w, h) — the port of navRig.js:238. The rig's
        /// integer rounding is reproduced exactly (<see cref="DrawSurface.JsRound"/> for
        /// <c>Math.round</c>, floor for <c>Math.floor</c>), so a box lands on the same pixel it does in
        /// the preview.
        /// </summary>
        public static Layout ComputeLayout(int x, int y, int w, int h, Face face)
        {
            int pad = Mathf.Max(4, DrawSurface.JsRound(w * 0.016));
            int brandH = Mathf.Max(9, DrawSurface.JsRound(h * 0.058));
            int colW = Mathf.Max(30, DrawSurface.JsRound(w * 0.072));

            var lcd = new RectInt(x + pad, y + pad, w - pad * 3 - colW, h - pad * 2 - brandH);
            var col = new RectInt(x + w - pad - colW, y + pad, colW, lcd.height);

            const int nKeys = 4;
            int gap = Mathf.Max(2, DrawSurface.JsRound(col.height * 0.02));
            int knob = DrawSurface.JsRound(col.width * 0.9);
            int keyArea = col.height - knob - gap;
            int keyH = Mathf.FloorToInt((keyArea - gap * (nKeys - 1)) / (float)nKeys);
            var k0 = new RectInt(col.x, col.y + 0 * (keyH + gap), col.width, keyH);
            var k1 = new RectInt(col.x, col.y + 1 * (keyH + gap), col.width, keyH);
            var k2 = new RectInt(col.x, col.y + 2 * (keyH + gap), col.width, keyH);
            var k3 = new RectInt(col.x, col.y + 3 * (keyH + gap), col.width, keyH);
            var knobBox = new RectInt(col.x, col.y + col.height - knob, col.width, knob);

            var brand = new RectInt(x + pad, y + h - brandH - DrawSurface.JsRound(pad * 0.3),
                                    w - pad * 2, brandH);

            int topH = Mathf.Max(16, DrawSurface.JsRound(lcd.height * 0.11));
            int botH = Mathf.Max(13, DrawSurface.JsRound(lcd.height * 0.085));
            var topbar = new RectInt(lcd.x, lcd.y, lcd.width, topH);
            var botbar = new RectInt(lcd.x, lcd.y + lcd.height - botH, lcd.width, botH);

            int my = lcd.y + topH, mh = lcd.height - topH - botH;
            RectInt rail = default, right = default, profile = default, chart;
            bool hasMax = face == Face.Max;
            if (hasMax)
            {
                int railW = Mathf.Max(30, DrawSurface.JsRound(lcd.width * 0.072));
                int rightW = DrawSurface.JsRound(lcd.width * 0.25);
                int profH = DrawSurface.JsRound(mh * 0.24);
                rail = new RectInt(lcd.x, my, railW, mh);
                right = new RectInt(lcd.x + lcd.width - rightW, my, rightW, mh);
                chart = new RectInt(lcd.x + railW, my, lcd.width - railW - rightW, mh - profH);
                profile = new RectInt(lcd.x + railW, my + mh - profH, lcd.width - railW - rightW, profH);
            }
            else
            {
                chart = new RectInt(lcd.x, my, lcd.width, mh);
            }

            return new Layout(lcd, col, knobBox, brand, topbar, botbar, chart, rail, right, profile,
                              k0, k1, k2, k3, pad, brandH, colW, hasMax);
        }

        /// <summary>The rig's own font-scale rule for the chart body (navRig.js:320): labels only appear
        /// once the chart is wide enough to carry them.</summary>
        public static int ChartFontScale(int chartWidth) => chartWidth >= 360 ? 2 : 1;

        // ---- the view transform (navRig.js:229-235) ---------------------------------------------------

        /// <summary>
        /// World↔screen for one repaint: where the chart is centred, how many metres fit across it, and
        /// how far it is rotated. Built once per paint and passed down — the rig's <c>makeView</c>.
        /// </summary>
        public readonly struct View
        {
            public readonly double Scale;          // screen px per world METRE
            public readonly double Cos, Sin;
            public readonly double Cx, Cy;         // chart centre in world metres
            public readonly double Ox, Oy;         // chart centre in screen px

            public View(double scale, double phiRad, Vector2 centreWorld, double ox, double oy)
            {
                Scale = scale;
                Cos = System.Math.Cos(phiRad);
                Sin = System.Math.Sin(phiRad);
                Cx = centreWorld.x;
                Cy = centreWorld.y;
                Ox = ox;
                Oy = oy;
            }
        }

        /// <summary>
        /// Build the view for a chart box (navRig.js:229). <paramref name="rangeNM"/> is the width of the
        /// glass in nautical miles; <paramref name="headingDeg"/> only matters in
        /// <see cref="Orient.Head"/>.
        ///
        /// <para><b>Screen Y is DOWN and world Y is UP</b> — the one place that has to be said out loud.
        /// The rig never mentions it because a canvas has no other option; here the flip is explicit in
        /// <see cref="WorldToScreen"/> so nothing downstream has to remember it.</para>
        /// </summary>
        public static View MakeView(RectInt chart, Vector2 centreWorld, float rangeNM,
                                    Orient orient, float headingDeg)
        {
            float rangeMetres = NavMath.NMToMetres(rangeNM > 0f ? rangeNM : 0.05f);
            double scale = chart.width / (double)rangeMetres;
            double phi = (orient == Orient.Head ? -headingDeg : 0.0) * System.Math.PI / 180.0;
            return new View(scale, phi, centreWorld,
                            chart.x + chart.width / 2.0, chart.y + chart.height / 2.0);
        }

        /// <summary>World metres → screen pixels (navRig.js:234 <c>w2s</c>, with the Y flip made explicit).</summary>
        public static Vector2 WorldToScreen(in View v, Vector2 world)
        {
            double dx = world.x - v.Cx;
            double dy = v.Cy - world.y;                 // world +Y is north; screen +Y is down
            return new Vector2((float)(v.Ox + (dx * v.Cos - dy * v.Sin) * v.Scale),
                               (float)(v.Oy + (dx * v.Sin + dy * v.Cos) * v.Scale));
        }

        /// <summary>Screen pixels → world metres (navRig.js:235 <c>s2w</c>) — the exact inverse, which is
        /// what a chart tap needs.</summary>
        public static Vector2 ScreenToWorld(in View v, Vector2 screen)
        {
            double ux = (screen.x - v.Ox) / v.Scale;
            double uy = (screen.y - v.Oy) / v.Scale;
            double wx = v.Cx + (ux * v.Cos + uy * v.Sin);
            double wy = v.Cy - (-ux * v.Sin + uy * v.Cos);
            return new Vector2((float)wx, (float)wy);
        }

        // ---- formatters (navRig.js:542-549) -----------------------------------------------------------

        /// <summary>Range/distance in NM the rig's way: 2 decimals under 1, 1 decimal to 10, whole
        /// beyond. The sub-1 branch is what makes the re-scaled harbour ladder readable.</summary>
        public static string FmtNM(float nm)
        {
            nm = Mathf.Abs(nm);
            if (nm >= 10f) return Mathf.RoundToInt(nm).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (nm >= 1f) return nm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return nm.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A bearing as three zero-padded degrees (navRig.js:543).</summary>
        public static string FmtDeg(float deg)
        {
            int d = Mathf.RoundToInt(NavMath.Norm360(deg));
            if (d >= 360) d -= 360;                     // 359.7 rounds to 360; a compass has no 360
            return d < 10 ? "00" + d : d < 100 ? "0" + d : d.ToString();
        }

        private static readonly string[] Cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>Eight-point cardinal for a bearing (navRig.js:544).</summary>
        public static string Cardinal8(float deg)
            => Cardinals[Mathf.RoundToInt(NavMath.Norm360(deg) / 45f) % 8];

        /// <summary>
        /// Minutes as HH:MM, or "--:--" when there is no answer (navRig.js:545). A stopped boat has no
        /// ETA and the rig says so rather than printing a number.
        ///
        /// <para><b>The separator is the rig's COLON.</b> PR 2 shipped a dot here because the shared
        /// 3×5 font carried no <c>:</c> and a standing guard pinned that character as a loud unknown
        /// until a slice added it on purpose. This is that slice: the glyph is now transcribed from the
        /// rig's own table (navRig.js:71) into <see cref="RigDrawUtil"/>, so the clock prints the form
        /// the rig actually authors and the placeholder is <c>--:--</c> rather than <c>--.--</c>.</para>
        /// </summary>
        public static string FmtHM(float minutes)
        {
            if (float.IsNaN(minutes) || float.IsInfinity(minutes)) return "--:--";
            int m = Mathf.Max(0, Mathf.RoundToInt(minutes));
            int h = m / 60;
            m %= 60;
            return (h < 10 ? "0" + h : h.ToString()) + ":" + (m < 10 ? "0" + m : m.ToString());
        }

        /// <summary>
        /// The scale bar's round number: the largest step whose bar fits in ~100 px (navRig.js:383).
        /// The step table is re-scaled with the range ladder — the rig's smallest rung (0.1 NM) is
        /// bigger than a whole region here, so a bar drawn from it could never appear.
        /// </summary>
        /// <remarks>⚠ The table reaches DOWN to 0.002 NM (≈ 3.7 m), well below anything the rig needs.
        /// Its own table stops at 0.1 NM, and its loop falls back to the FIRST entry when nothing fits —
        /// so at this world's closest range every candidate overflows the 100 px budget and the bar
        /// draws too long. That is latent in the source and only surfaces at harbour scale; the fix is
        /// more rungs, not a different rule.</remarks>
        private static readonly float[] ScaleSteps =
            { 0.002f, 0.005f, 0.01f, 0.02f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f };

        /// <summary>Pick the scale-bar length in NM for a given px-per-NM: the largest round step whose
        /// bar still fits ~100 px (navRig.js:383).</summary>
        public static float ScaleBarNM(double pxPerNM)
        {
            float nm = ScaleSteps[0];
            foreach (float s in ScaleSteps)
                if (s * pxPerNM <= 100.0) nm = s;
            return nm;
        }
    }
}
