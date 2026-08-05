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

        // ---- the MAX face's rail (navRig.js:258-260) ---------------------------------------------------

        /// <summary>
        /// The rail's twelve slots, in the rig's order. Slot 7 is the rig's <c>'|'</c> — a SPACER that
        /// gets no button, which is what divides the seven layer switches from the four tools. It stays
        /// in the array rather than being compacted out because the rig's own button pitch is
        /// <c>floor(rail.h / ids.length)</c> over all twelve: drop the separator and every button below
        /// it moves.
        /// </summary>
        public static readonly string[] RailIds =
            { "LAND", "ROCK", "BUOY", "POI", "TRK", "DPTH", "TRFC", "|", "PAN", "MRK", "RTE", "MSR" };

        /// <summary>The slot index of the rig's spacer — the one slot with no button.</summary>
        public const int RailSeparatorSlot = 7;

        /// <summary>Slots at and past the separator are TOOLS rather than layer switches
        /// (navRig.js:260 <c>tool:i&gt;=8</c>).</summary>
        public static bool RailSlotIsTool(int slot) => slot > RailSeparatorSlot;

        /// <summary>The tool a rail slot selects. Total over the four tool slots; anything else is
        /// <see cref="NavChartTool.Pan"/>, which is also the rig's fallback (js:509 treats a missing
        /// tool as PAN).</summary>
        public static NavChartTool RailTool(int slot) => slot switch
        {
            9 => NavChartTool.Mark,
            10 => NavChartTool.Route,
            11 => NavChartTool.Measure,
            _ => NavChartTool.Pan,
        };

        /// <summary>The layer a rail slot switches (navRig.js:531 <c>layerOn</c>'s map). Layer slots
        /// only — a tool slot maps to <see cref="NavChartLayers.None"/>.</summary>
        public static NavChartLayers RailLayer(int slot) => slot switch
        {
            0 => NavChartLayers.Land,
            1 => NavChartLayers.Rocks,
            2 => NavChartLayers.Buoys,
            3 => NavChartLayers.Poi,
            4 => NavChartLayers.Track,
            5 => NavChartLayers.Depth,
            6 => NavChartLayers.Traffic,
            _ => NavChartLayers.None,
        };

        /// <summary>
        /// The button rect for one rail slot, or false for the separator / an out-of-range slot / a
        /// console face (which has no rail at all). navRig.js:259-260 exactly:
        /// <c>bh = floor(rail.h / 12)</c>, then <c>{x: rail.x+2, y: rail.y + i*bh, w: rail.w-4, h: bh-1}</c>.
        /// </summary>
        public static bool TryRailButton(in Layout layout, int slot, out RectInt rect)
        {
            rect = default;
            if (!layout.HasMax || slot < 0 || slot >= RailIds.Length) return false;
            if (slot == RailSeparatorSlot) return false;
            RectInt rail = layout.Rail;
            if (rail.width <= 4 || rail.height <= 0) return false;

            int bh = rail.height / RailIds.Length;
            if (bh <= 1) return false;
            rect = new RectInt(rail.x + 2, rail.y + slot * bh, rail.width - 4, bh - 1);
            return true;
        }

        // ---- the MAX face's manager column (navRig.js:422-445) -----------------------------------------

        /// <summary>Most waypoint rows the manager column lists (navRig.js:426 <c>slice(0,6)</c>).</summary>
        public const int ManagerWaypointRows = 6;

        /// <summary>Most route legs it lists (navRig.js:434 <c>i&lt;=5</c>).</summary>
        public const int ManagerRouteRows = 5;

        /// <summary>Pixel pitch of one row in the column — the rig's <c>y+=9</c> throughout
        /// <c>rightCol</c>.</summary>
        public const int ManagerRowH = 9;

        /// <summary>
        /// Where every element of the manager column lands, for a given amount of content.
        ///
        /// <para><b>Computed once and shared by the renderer and the hit map</b>, for the reason
        /// <see cref="ChartplotterOverlayLayout"/> gives for having one screen→rig mapper: the column's
        /// geometry is a running cursor, not a grid, so two independent walks of it would agree until
        /// the day a route got long enough to push the rows apart.</para>
        /// </summary>
        public readonly struct Manager
        {
            /// <summary>The column box itself.</summary>
            public readonly RectInt Right;

            /// <summary>Text origin — the rig's <c>x = R.x + 6</c>.</summary>
            public readonly int TextX;

            /// <summary>Top of the WAYPOINTS header row.</summary>
            public readonly int WaypointHeaderY;

            /// <summary>Top of the first waypoint row, and how many are actually listed.</summary>
            public readonly int WaypointRowsY, WaypointRowCount;

            /// <summary>Top of the ROUTE header row, and of its first leg row / how many.</summary>
            public readonly int RouteHeaderY, RouteRowsY, RouteRowCount;

            /// <summary>Top of the CURSOR header row (the measure readout).</summary>
            public readonly int CursorHeaderY;

            /// <summary>Top of the TIDE &amp; SET header row.</summary>
            public readonly int TideHeaderY;

            /// <summary>
            /// The NAME / DEL action pair, live only while a waypoint row is selected.
            ///
            /// <para><b>The one affordance the rig does not author</b>, and it is placed so that stays
            /// true of everything that does: the rig's column cursor runs out well above the bottom of
            /// <see cref="Right"/> (about 210 px of content in a 438 px column at native MAX size), so
            /// these two buttons sit in the space it leaves empty and shift nothing above them. They
            /// are drawn with the rail's own button primitive and colours, because inventing a second
            /// button idiom would be the bigger liberty. See the PR body — the acceptance criteria ask
            /// for rename and delete FROM THE COLUMN, and the rig gives them no home.</para>
            /// </summary>
            public readonly RectInt NameButton, DeleteButton;

            public Manager(RectInt right, int textX, int wptHeaderY, int wptRowsY, int wptRowCount,
                           int routeHeaderY, int routeRowsY, int routeRowCount, int cursorHeaderY,
                           int tideHeaderY, RectInt nameButton, RectInt deleteButton)
            {
                Right = right; TextX = textX;
                WaypointHeaderY = wptHeaderY; WaypointRowsY = wptRowsY; WaypointRowCount = wptRowCount;
                RouteHeaderY = routeHeaderY; RouteRowsY = routeRowsY; RouteRowCount = routeRowCount;
                CursorHeaderY = cursorHeaderY; TideHeaderY = tideHeaderY;
                NameButton = nameButton; DeleteButton = deleteButton;
            }

            /// <summary>The full-width band a waypoint row occupies — the rig's own selection highlight
            /// rect (navRig.js:427 <c>fillRect(R.x+2, y-1, R.w-4, 9)</c>), which is therefore also the
            /// honest hit box: you click the thing that lights up.</summary>
            public RectInt WaypointRow(int i)
                => new RectInt(Right.x + 2, WaypointRowsY + i * ManagerRowH - 1, Right.width - 4,
                               ManagerRowH);
        }

        /// <summary>
        /// Walk the rig's <c>rightCol</c> cursor (navRig.js:424-444) and record where everything lands.
        /// <paramref name="waypointCount"/> and <paramref name="routeCount"/> are the region's real
        /// counts; the column lists at most <see cref="ManagerWaypointRows"/> / <see cref="ManagerRouteRows"/>
        /// of each, exactly as the source does.
        /// </summary>
        public static Manager ComputeManager(in Layout layout, int waypointCount, int routeCount,
                                             bool selected)
        {
            RectInt r = layout.Right;
            int x = r.x + 6;
            int y = r.y + 6;

            int wptHeader = y; y += ManagerRowH;                             // "WAYPOINTS" + count
            int wptRows = y;
            int wptShown = Mathf.Clamp(waypointCount, 0, ManagerWaypointRows);
            y += wptShown * ManagerRowH;

            y += 4; y += 6;                                                  // rule + gap (js:431)
            int routeHeader = y; y += ManagerRowH;                           // "ROUTE" + total
            int routeRows = y;
            // N points is N−1 legs, and the rig lists at most five of them (js:434 `i<=5`).
            int legs = Mathf.Clamp(routeCount - 1, 0, ManagerRouteRows);
            y += legs * ManagerRowH;

            y += 4; y += 6;                                                  // rule + gap (js:436)
            int cursorHeader = y; y += ManagerRowH;                          // "CURSOR"
            y += 2 * ManagerRowH;                                            // two rows either way (js:438-440)

            y += 4; y += 6;                                                  // rule + gap (js:441)
            int tideHeader = y; y += 3 * ManagerRowH;                        // header + HT + SET

            // The action pair, in the space below everything the rig authors. Sized from the column so
            // it scales with the face rather than from a pixel count that only works at native size.
            RectInt nameBtn = default, delBtn = default;
            if (selected)
            {
                int bw = (r.width - 12) / 2;
                int bh = Mathf.Max(9, ManagerRowH + 3);
                int by = y + 6;
                if (bw > 8 && by + bh <= r.y + r.height)
                {
                    nameBtn = new RectInt(r.x + 4, by, bw, bh);
                    delBtn = new RectInt(r.x + 8 + bw, by, bw, bh);
                }
            }

            return new Manager(r, x, wptHeader, wptRows, wptShown, routeHeader, routeRows, legs,
                               cursorHeader, tideHeader, nameBtn, delBtn);
        }

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
