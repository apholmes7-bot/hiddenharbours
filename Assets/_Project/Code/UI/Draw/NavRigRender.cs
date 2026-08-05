using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>Everything the chartplotter draws in one repaint — a pure value, so the host's change
    /// key is a struct compare and the renderer never reaches for a service.</summary>
    public readonly struct NavRigState
    {
        public readonly Vector2 Boat;              // world metres
        public readonly bool HasBoat;
        public readonly float HeadingDeg, SpeedKnots;
        public readonly float RangeNM;
        public readonly NavRigGeometry.Orient Orient;
        public readonly bool Night;
        public readonly bool ShowTrack;
        public readonly float TideMetres;
        public readonly bool TideRising;

        public NavRigState(Vector2 boat, bool hasBoat, float headingDeg, float speedKnots, float rangeNM,
                           NavRigGeometry.Orient orient, bool night, bool showTrack,
                           float tideMetres, bool tideRising)
        {
            Boat = boat; HasBoat = hasBoat; HeadingDeg = headingDeg; SpeedKnots = speedKnots;
            RangeNM = rangeNM; Orient = orient; Night = night; ShowTrack = showTrack;
            TideMetres = tideMetres; TideRising = tideRising;
        }
    }

    /// <summary>
    /// Live C# port of the chartplotter's CONSOLE face — the immutable rig source is
    /// <c>docs/art/rigs/ui/chartplotter/Art/navRig.js</c> (ADR 0025 S6, Option A). Mirror of
    /// <c>drawUnit()</c> (navRig.js:482) in its <c>console</c> mode: graphite case, recessed glass, the
    /// chart body, the data strip and the route strip, four side pushers, the rotary, and the original
    /// wordmark.
    ///
    /// <para><b>The world is the game's, not the rig's.</b> Every polygon, buoy, rock, region label and
    /// AIS contact in the source is hand-authored demo content for a fictional 22 × 17 NM chart
    /// (navRig.js:126-175); none of it is portable. The depth shading and the coastline come from
    /// <see cref="NavChartSource"/> — the real painted seabed — and the waypoints, route and track come
    /// from the player's save. What is ported is the PRESENTATION: palettes, shading bands, symbols,
    /// layout, typography.</para>
    ///
    /// <para><b>Traffic ships EMPTY, on purpose.</b> No contacts seam exists (it is S5's blocker), so
    /// there is nothing honest to draw. Rather than fake vessels, the layer simply has no source — the
    /// <c>FishSchools</c> null-object precedent, where the UI runs against an empty set today and one
    /// assignment swaps in the real thing later.</para>
    ///
    /// <para><b>The MAX face is not here.</b> The rig's second face is not a bigger console — it is
    /// ADVANCED KIT (layer/tool rail, waypoint and route manager, measure line, depth-profile strip).
    /// Expanding this instrument shows the same face larger, exactly as the sounder and finder do, and
    /// the manager surface is its own slice. Nothing here forecloses it: the layout function already
    /// computes every MAX box (<see cref="NavRigGeometry.Layout.Rail"/> and friends).</para>
    /// </summary>
    public static class NavRigRender
    {
        private const double DEG = System.Math.PI / 180.0;

        // The graphite case shared with the rest of the instrument fleet (navRig.js:20-23).
        private static readonly Color32[] CASE = RigDrawUtil.Ramp("12171b", "1b2228", "28323a", "3a4750", "4f5e68", "6b7c86");
        private static readonly Color32[] RESIN = RigDrawUtil.Ramp("050708", "0c1013", "141b1f");
        private static readonly Color32[] STEEL = RigDrawUtil.Ramp("232a30", "39434b", "556069", "7a8892", "9db0b8");
        private static readonly Color32 TEAL = RigDrawUtil.Hex("7fd6c9");
        private static readonly Color32 TEAK = RigDrawUtil.Hex("c9a86a");
        private static readonly Color32 BLUE = RigDrawUtil.Hex("5b93b8");
        private static readonly Color32 WHITE = new Color32(255, 255, 255, 255);

        /// <summary>Native console size, for a host that wants to raster at 1:1 (navRig.js:15).</summary>
        public const int W = NavRigGeometry.ConsoleW, H = NavRigGeometry.ConsoleH;

        /// <summary>
        /// Paint the whole instrument into <paramref name="s"/>, fitted to (x, y, w, h) — the rig's
        /// box-parameterised <c>drawUnit</c>/<c>paintInto</c> contract, so the brow mount and the
        /// expanded card are the same code at two sizes.
        /// </summary>
        public static void Render(DrawSurface s, int x, int y, int w, int h, in NavRigState st,
                                  NavChartSource chart,
                                  IReadOnlyList<NavWaypoint> waypoints,
                                  IReadOnlyList<Vector2> route,
                                  IReadOnlyList<Vector2> track)
        {
            NavPalette p = NavPalette.For(st.Night);
            NavRigGeometry.Layout L = NavRigGeometry.ComputeLayout(x, y, w, h, NavRigGeometry.Face.Console);

            // ---- case body (navRig.js:491-495) ----
            int cr = Mathf.Max(4, DrawSurface.JsRound(h * 0.06));
            RigDrawUtil.RRect(s, x, y, w, h, cr, RESIN[0]);
            RigDrawUtil.RRect(s, x + 1, y + 1, w - 2, h - 2, cr - 1, CASE[1]);
            RigDrawUtil.RRect(s, x + 2, y + 2, w - 4, Mathf.Max(2, DrawSurface.JsRound(h * 0.06)), cr - 2, CASE[3]);
            RigDrawUtil.RRect(s, x + 2, y + h - Mathf.Max(2, DrawSurface.JsRound(h * 0.045)), w - 4,
                              Mathf.Max(2, DrawSurface.JsRound(h * 0.03)), 3, CASE[0]);
            RigDrawUtil.Screw(s, x + 7, y + 7, 3, STEEL);
            RigDrawUtil.Screw(s, x + w - 7, y + 7, 3, STEEL);
            RigDrawUtil.Screw(s, x + 7, y + h - 7, 3, STEEL);
            RigDrawUtil.Screw(s, x + w - 7, y + h - 7, 3, STEEL);

            // ---- recessed glass (navRig.js:498-504) ----
            int lr = Mathf.Max(3, DrawSurface.JsRound(L.Lcd.height * 0.04));
            RigDrawUtil.RRect(s, L.Lcd.x - 3, L.Lcd.y - 3, L.Lcd.width + 6, L.Lcd.height + 6, lr + 1, RESIN[0]);
            RigDrawUtil.RRect(s, L.Lcd.x - 2, L.Lcd.y - 2, L.Lcd.width + 4, L.Lcd.height + 4, lr + 1, RESIN[2]);
            RigDrawUtil.RRect(s, L.Lcd.x, L.Lcd.y, L.Lcd.width, L.Lcd.height, lr, p.Frame);

            DrawChart(s, in L, in st, p, chart, waypoints, route, track);
            TopBar(s, L.TopBar, in st, p, chart);
            BotBar(s, L.BotBar, in st, p, route);

            s.BlendRect(L.Lcd.x + 3, L.Lcd.y + 2, L.Lcd.width - 6, 1, WHITE, 0.05f);

            // ---- side keys + rotary (navRig.js:518-522) ----
            Key(s, L.Key(0), "MAX", KeyGlyph.Square, p, false);
            Key(s, L.Key(1), "MARK", KeyGlyph.Dot, p, false);
            Key(s, L.Key(2), "IN", KeyGlyph.Up, p, false);
            Key(s, L.Key(3), "OUT", KeyGlyph.Down, p, false);
            Knob(s, L.KnobBox);

            // ---- brand strip, original wordmark (navRig.js:525-528) ----
            int by = L.Brand.y + DrawSurface.JsRound((L.Brand.height - 5) / 2.0);
            RigDrawUtil.Text(s, "GN-12", L.Brand.x + 1, by, 1, TEAL);
            RigDrawUtil.TextC(s, "HARBOUR", x + w / 2.0, by, 1, TEAK);
            RigDrawUtil.Text(s, "CHARTPLOTTER",
                             L.Brand.x + L.Brand.width - RigDrawUtil.TextW("CHARTPLOTTER", 1), by, 1, BLUE);
        }

        // ---- the chart body (navRig.js:305-379) -------------------------------------------------------

        private static void DrawChart(DrawSurface s, in NavRigGeometry.Layout L, in NavRigState st,
                                      in NavPalette p, NavChartSource chart,
                                      IReadOnlyList<NavWaypoint> waypoints,
                                      IReadOnlyList<Vector2> route,
                                      IReadOnlyList<Vector2> track)
        {
            RectInt ch = L.Chart;
            if (ch.width <= 0 || ch.height <= 0) return;
            Vector2 centre = st.HasBoat ? st.Boat
                                        : (chart != null && chart.HasChart ? chart.Bounds.center : Vector2.zero);
            NavRigGeometry.View v = NavRigGeometry.MakeView(ch, centre, st.RangeNM, st.Orient, st.HeadingDeg);
            int fs = NavRigGeometry.ChartFontScale(ch.width);

            // The survey, resampled per destination pixel. The rig blits its baked base under a canvas
            // transform; with a Color32[] the honest equivalent is the INVERSE transform per pixel,
            // point-sampled — which is also what imageSmoothingEnabled=false means there.
            Color32 offChart = p.Water[4];
            for (int py = ch.y; py < ch.y + ch.height; py++)
            {
                int row = py * s.Width;
                for (int px = ch.x; px < ch.x + ch.width; px++)
                {
                    Vector2 world = NavRigGeometry.ScreenToWorld(in v, new Vector2(px + 0.5f, py + 0.5f));
                    s.Pixels[row + px] = chart != null ? chart.ColourAt(world, offChart) : offChart;
                }
            }

            // ---- the track breadcrumb (navRig.js:328) ----
            if (st.ShowTrack && track != null && track.Count > 1)
            {
                for (int i = 1; i < track.Count; i++)
                {
                    Vector2 a = NavRigGeometry.WorldToScreen(in v, track[i - 1]);
                    Vector2 b = NavRigGeometry.WorldToScreen(in v, track[i]);
                    DashLine(s, a, b, 1, p.Track, 3, 3, ch);
                }
            }

            // ---- the planned route, magenta (navRig.js:333-339) ----
            if (route != null && route.Count > 1)
            {
                for (int i = 1; i < route.Count; i++)
                {
                    Vector2 a = NavRigGeometry.WorldToScreen(in v, route[i - 1]);
                    Vector2 b = NavRigGeometry.WorldToScreen(in v, route[i]);
                    ClippedThickLine(s, a, b, 2, p.RouteDim, ch);
                    DashLine(s, a, b, 2, p.Route, 6, 4, ch);
                }
                for (int i = 0; i < route.Count; i++)
                {
                    Vector2 sp = NavRigGeometry.WorldToScreen(in v, route[i]);
                    if (!Inside(ch, sp, 6)) continue;
                    RingOutline(s, sp, 4, p.Route);
                    if (i > 0) RigDrawUtil.Circle(s, Mathf.RoundToInt(sp.x), Mathf.RoundToInt(sp.y), 1, p.Route);
                }
            }

            // ---- marked waypoints (navRig.js:359) ----
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    NavWaypoint wpt = waypoints[i];
                    Vector2 sp = NavRigGeometry.WorldToScreen(in v, wpt.Pos);
                    if (!Inside(ch, sp, 18)) continue;
                    WaypointGlyph(s, sp, in wpt, in p, fs, ch);
                }
            }

            // (traffic: the layer exists in the rig, its source does not exist in the sim — nothing
            //  is drawn rather than invented. See the class remarks.)

            // ---- own ship (navRig.js:370-372) ----
            if (st.HasBoat)
            {
                Vector2 os = NavRigGeometry.WorldToScreen(in v, st.Boat);
                float shipAng = st.Orient == NavRigGeometry.Orient.Head ? 0f : st.HeadingDeg;
                RingOutline(s, os, 8, p.Head);
                OwnShip(s, os, shipAng, in p, ch);
            }

            // ---- north arrow + scale bar, screen space (navRig.js:377) ----
            if (fs >= 2)
            {
                NorthArrow(s, ch.x + ch.width - 16, ch.y + 16, in st, in p);
                ScaleBar(s, ch.x + 10, ch.y + ch.height - 12, in v, in p);
            }
            // the glass's own hairline border
            s.FillRect(ch.x, ch.y, ch.width, 1, p.StripDim);
            s.FillRect(ch.x, ch.y + ch.height - 1, ch.width, 1, p.StripDim);
            s.FillRect(ch.x, ch.y, 1, ch.height, p.StripDim);
            s.FillRect(ch.x + ch.width - 1, ch.y, 1, ch.height, p.StripDim);
        }

        // ---- glyphs (navRig.js:269-302) ---------------------------------------------------------------

        private static void OwnShip(DrawSurface s, Vector2 c, float angDeg, in NavPalette p, RectInt clip)
        {
            const float r = 7f;
            double a = angDeg * DEG, ca = System.Math.Cos(a), sa = System.Math.Sin(a);
            Vector2 Pt(float fx, float fy) => new Vector2(
                (float)(c.x + (fx * ca - fy * sa)), (float)(c.y + (fx * sa + fy * ca)));

            Vector2[] hull =
            {
                Pt(0f, -r * 1.4f), Pt(r * 0.62f, r * 0.5f), Pt(r * 0.34f, r),
                Pt(-r * 0.34f, r), Pt(-r * 0.62f, r * 0.5f),
            };
            var xs = new double[hull.Length];
            var ys = new double[hull.Length];
            for (int i = 0; i < hull.Length; i++) { xs[i] = hull[i].x; ys[i] = hull[i].y; }
            RigDrawUtil.FillPoly(s, xs, ys, hull.Length, p.Ship);
            for (int i = 0; i < hull.Length; i++)
            {
                Vector2 a2 = hull[i], b2 = hull[(i + 1) % hull.Length];
                ClippedThickLine(s, a2, b2, 1, p.ShipEdge, clip);
            }
            ClippedThickLine(s, c, Pt(0f, -r * 1.4f), 1, p.Head, clip);
        }

        private static void WaypointGlyph(DrawSurface s, Vector2 c, in NavWaypoint w, in NavPalette p,
                                          int fs, RectInt clip)
        {
            Color32 col = p.WaypointColour(w.Kind);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
            switch (w.Kind)
            {
                case NavWaypointKind.Anchor:
                    RigDrawUtil.Circle(s, cx, cy, 2, col);
                    RingOutline(s, c, 5, col);
                    s.FillRect(cx, cy - 6, 1, 4, col);
                    s.FillRect(cx - 3, cy - 3, 7, 1, col);
                    break;
                case NavWaypointKind.Hazard:
                    RigDrawUtil.Circle(s, cx, cy, 3, col);
                    s.FillRect(cx - 1, cy - 1, 3, 1, p.ShipEdge);
                    s.FillRect(cx - 1, cy + 1, 3, 1, p.ShipEdge);
                    break;
                default:   // the flag (navRig.js:291)
                    ClippedThickLine(s, c, new Vector2(c.x, c.y - 11f), 1, col, clip);
                    s.FillRect(cx + 1, cy - 11, 7, 5, col);
                    RigDrawUtil.Circle(s, cx, cy, 2, col);
                    break;
            }
            if (fs >= 2 && !string.IsNullOrEmpty(w.Name))
            {
                int tw = RigDrawUtil.TextW(w.Name, 1);
                s.BlendRect(cx + 4, cy + 3, tw + 2, 7, p.Plate, p.PlateAlpha);
                RigDrawUtil.Text(s, w.Name, cx + 5, cy + 4, 1, p.Label);
            }
        }

        private static void NorthArrow(DrawSurface s, int x, int y, in NavRigState st, in NavPalette p)
        {
            double ang = (st.Orient == NavRigGeometry.Orient.Head ? -st.HeadingDeg : 0f) * DEG;
            double dx = System.Math.Sin(ang), dy = -System.Math.Cos(ang);
            RigDrawUtil.ThickLine(s, x, y + 7, x + dx * 7, y + 7 + dy * 7, 1, p.Label);
            s.FillRect(DrawSurface.JsRound(x + dx * 7 - 1), DrawSurface.JsRound(y + 7 + dy * 7 - 1), 2, 2, p.BuoyPort);
            RigDrawUtil.TextC(s, "N", x + dx * 11, DrawSurface.JsRound(y + 7 + dy * 11 - 2), 1, p.Label);
        }

        private static void ScaleBar(DrawSurface s, int x, int y, in NavRigGeometry.View v, in NavPalette p)
        {
            double pxPerNM = v.Scale * NavMath.MetresPerNauticalMile;
            float nm = NavRigGeometry.ScaleBarNM(pxPerNM);
            int w = DrawSurface.JsRound(nm * pxPerNM);
            if (w <= 0) return;
            s.FillRect(x, y, w, 1, p.Label);
            s.FillRect(x, y - 2, 1, 4, p.Label);
            s.FillRect(x + w, y - 2, 1, 4, p.Label);
            RigDrawUtil.Text(s, NavRigGeometry.FmtNM(nm) + "NM", x, y - 9, 1, p.Label);
        }

        // ---- the two data strips (navRig.js:388-416) --------------------------------------------------

        private static void TopBar(DrawSurface s, RectInt b, in NavRigState st, in NavPalette p,
                                   NavChartSource chart)
        {
            if (b.width <= 0 || b.height <= 0) return;
            s.FillRect(b.x, b.y, b.width, b.height, p.Strip);
            s.BlendRect(b.x, b.y, b.width, 1, WHITE, 0.05f);

            int y = b.y + DrawSurface.JsRound((b.height - 14) / 2.0);
            int x = b.x + 6;
            RigDrawUtil.Circle(s, x + 2, y + 7, 2, st.HasBoat ? p.StripHi : p.StripDim);   // fix pip
            x += 8;
            RigDrawUtil.Text(s, "GPS", x, y + 3, 1, st.HasBoat ? p.StripHi : p.StripDim);
            x += RigDrawUtil.TextW("GPS", 1) + 6;

            x = Field(s, x, y, "SOG", st.SpeedKnots.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "KN",
                      in p, 2, p.StripFg) + 12;
            x = Field(s, x, y, "COG", NavRigGeometry.FmtDeg(st.HeadingDeg) + "°", in p, 2, p.StripFg) + 12;

            // Depth on the CHART is datum depth under the boat — the survey's own number, so the strip
            // and the shading under it can never disagree. Live depth-under-the-hull is the sounder's.
            float d = chart != null && st.HasBoat ? chart.DepthAt(st.Boat) : float.NaN;
            string dTxt = float.IsNaN(d) ? "--" : Mathf.Max(0f, d).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "M";
            Color32 dCol = !float.IsNaN(d) && d < 3f ? p.BuoyPort : p.StripFg;
            Field(s, x, y, "DPTH", dTxt, in p, 2, dCol);

            string ori = st.Orient == NavRigGeometry.Orient.Head ? "HEAD-UP" : "NORTH-UP";
            TextR(s, ori, b.x + b.width - 6, y, 1, p.StripHi);
            TextR(s, NavRigGeometry.FmtNM(st.RangeNM) + "NM", b.x + b.width - 6, y + 7, 2, p.StripFg);
        }

        private static void BotBar(DrawSurface s, RectInt b, in NavRigState st, in NavPalette p,
                                   IReadOnlyList<Vector2> route)
        {
            if (b.width <= 0 || b.height <= 0) return;
            s.FillRect(b.x, b.y, b.width, b.height, p.Strip);
            s.FillRect(b.x, b.y, b.width, 1, p.StripDim);

            int y = b.y + DrawSurface.JsRound((b.height - 7) / 2.0);
            if (route == null || route.Count < 2)
            {
                // The rig writes em dashes here (js:415); its own 3x5 font has no U+2014, so the preview
                // renders them as gaps. Hyphens carry the same rule with pixels the shared font has.
                RigDrawUtil.TextC(s, "- NO ACTIVE ROUTE -", b.x + b.width / 2.0, y, 1, p.StripDim);
                return;
            }

            float total = NavMath.RouteLengthMetres(route);
            Vector2 from = st.HasBoat ? st.Boat : route[0];
            float brg = NavMath.BearingDegrees(from, route[1]);
            float dtg = NavMath.DistanceMetres(from, route[1]);
            float eta = NavMath.MinutesToGo(total, st.SpeedKnots);

            int x = b.x + 6;
            s.FillRect(x, y + 1, 4, 4, p.Route);
            x += 8;
            x = Run(s, "ROUTE " + (route.Count - 1) + " LEGS", x, y, 1, p.StripFg) + 12;
            x = Run(s, "NEXT " + NavRigGeometry.FmtDeg(brg) + "°", x, y, 1, p.StripHi) + 10;
            x = Run(s, "DTG " + NavRigGeometry.FmtNM(NavMath.MetresToNM(dtg)) + "NM", x, y, 1, p.StripFg) + 10;
            RigDrawUtil.Text(s, "TTG " + NavRigGeometry.FmtHM(eta), x, y, 1, p.StripFg);
            TextR(s, "DIST " + NavRigGeometry.FmtNM(NavMath.MetresToNM(total)) + "NM",
                  b.x + b.width - 6, y, 1, p.StripHi);
        }

        /// <summary>Draw a string and return the x the NEXT one starts at. The rig gets this from its own
        /// <c>text()</c>, which returns the advance; RigDrawUtil's returns nothing, so the advance is
        /// measured with the same <see cref="RigDrawUtil.TextW"/> the drawing uses.</summary>
        private static int Run(DrawSurface s, string str, int x, int y, int scale, Color32 col)
        {
            RigDrawUtil.Text(s, str, x, y, scale, col);
            return x + RigDrawUtil.TextW(str, scale);
        }

        /// <summary>A labelled value pair (navRig.js:387 <c>fmtField</c>): small label over a bigger
        /// value. Returns where the next field starts — the WIDER of the two rows, since either can
        /// be the long one.</summary>
        private static int Field(DrawSurface s, int x, int y, string label, string val, in NavPalette p,
                                 int fs, Color32 valCol)
        {
            RigDrawUtil.Text(s, label, x, y, 1, p.StripDim);
            RigDrawUtil.Text(s, val, x, y + 7, fs, valCol);
            return x + Mathf.Max(RigDrawUtil.TextW(label, 1), RigDrawUtil.TextW(val, fs));
        }

        // ---- pushers + rotary (navRig.js:464-479) -----------------------------------------------------

        private enum KeyGlyph { Up, Down, Square, Dot }

        private static void Key(DrawSurface s, RectInt b, string label, KeyGlyph glyph, in NavPalette p,
                                bool lit)
        {
            if (b.width <= 0 || b.height <= 0) return;
            RigDrawUtil.RRect(s, b.x, b.y, b.width, b.height, Mathf.Max(2, DrawSurface.JsRound(b.height * 0.2)), CASE[0]);
            int ix = b.x + 2, iy = b.y + 2, iw = b.width - 4, ih = b.height - 4;
            int r = Mathf.Max(2, DrawSurface.JsRound(ih * 0.2));
            RigDrawUtil.RRect(s, ix, iy, iw, ih, r, p.KeyEdge);
            RigDrawUtil.RRect(s, ix, iy, iw, ih - 1, r, p.KeyPlate);
            RigDrawUtil.RRect(s, ix + 2, iy + 2, iw - 4, Mathf.Max(1, DrawSurface.JsRound(ih * 0.24)),
                              Mathf.Max(0, r - 1), WHITE, 0.13f);
            RigDrawUtil.TextC(s, label, b.x + b.width / 2.0, iy + DrawSurface.JsRound(ih * 0.32), 1, p.Legend);

            int cx = b.x + b.width / 2, dcy = DrawSurface.JsRound(iy + ih * 0.7);
            const int sz = 2;
            switch (glyph)
            {
                case KeyGlyph.Up:
                    for (int i = 0; i <= sz; i++) s.FillRect(cx - i, dcy - sz + i, i * 2 + 1, 1, p.Mark);
                    break;
                case KeyGlyph.Down:
                    for (int i = 0; i <= sz; i++) s.FillRect(cx - (sz - i), dcy - sz + i, (sz - i) * 2 + 1, 1, p.Mark);
                    break;
                case KeyGlyph.Square:
                    RigDrawUtil.Ring(s, cx, dcy, 3, 2, p.Mark);
                    break;
                default:
                    RigDrawUtil.Circle(s, cx, dcy, 1, p.Mark);
                    break;
            }
            if (lit) RigDrawUtil.RRect(s, ix, iy, iw, ih - 1, r, RigDrawUtil.Hex("ffbe3c"), 0.10f);
        }

        private static void Knob(DrawSurface s, RectInt k)
        {
            if (k.width <= 0 || k.height <= 0) return;
            int cx = k.x + k.width / 2, cy = k.y + k.height / 2;
            int r = Mathf.Min(k.width, k.height) / 2 - 1;
            if (r < 2) return;
            RigDrawUtil.Circle(s, cx, cy, r, CASE[0]);
            RigDrawUtil.Circle(s, cx, cy, r - 1, CASE[3]);
            RigDrawUtil.Circle(s, cx - 1, cy - 1, Mathf.Max(1, r - 3), CASE[4]);
            s.FillRect(cx - 1, cy - r + 2, 2, Mathf.Max(3, DrawSurface.JsRound(r * 0.5)), CASE[0]);
        }

        // ---- small primitives the rig has and RigDrawUtil does not ------------------------------------

        private static bool Inside(RectInt r, Vector2 p, float pad)
            => p.x >= r.x - pad && p.x <= r.x + r.width + pad
            && p.y >= r.y - pad && p.y <= r.y + r.height + pad;

        /// <summary>A thick line clipped to the chart box — the rig gets this free from a canvas clip
        /// path; here the segment is trimmed against the box before it is rasterised, so a route leg
        /// running off the chart cannot draw over the data strips.</summary>
        private static void ClippedThickLine(DrawSurface s, Vector2 a, Vector2 b, double width,
                                             Color32 c, RectInt clip)
        {
            if (!SegmentClip(ref a, ref b, clip)) return;
            RigDrawUtil.ThickLine(s, a.x, a.y, b.x, b.y, width, c);
        }

        private static void DashLine(DrawSurface s, Vector2 a, Vector2 b, double width, Color32 c,
                                     float on, float off, RectInt clip)
        {
            if (!SegmentClip(ref a, ref b, clip)) return;
            float dx = b.x - a.x, dy = b.y - a.y;
            float len = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
            float ux = dx / len, uy = dy / len;
            for (float d = 0f; d < len; d += on + off)
            {
                float e = Mathf.Min(len, d + on);
                RigDrawUtil.ThickLine(s, a.x + ux * d, a.y + uy * d, a.x + ux * e, a.y + uy * e, width, c);
            }
        }

        /// <summary>Liang–Barsky against the chart box. Returns false when the segment misses entirely.</summary>
        private static bool SegmentClip(ref Vector2 a, ref Vector2 b, RectInt r)
        {
            float x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            float dx = x1 - x0, dy = y1 - y0;
            float t0 = 0f, t1 = 1f;
            float xMin = r.x, xMax = r.x + r.width - 1, yMin = r.y, yMax = r.y + r.height - 1;

            for (int edge = 0; edge < 4; edge++)
            {
                float pEdge = edge switch { 0 => -dx, 1 => dx, 2 => -dy, _ => dy };
                float qEdge = edge switch
                {
                    0 => x0 - xMin,
                    1 => xMax - x0,
                    2 => y0 - yMin,
                    _ => yMax - y0,
                };
                if (Mathf.Approximately(pEdge, 0f)) { if (qEdge < 0f) return false; continue; }
                float t = qEdge / pEdge;
                if (pEdge < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            a = new Vector2(x0 + t0 * dx, y0 + t0 * dy);
            b = new Vector2(x0 + t1 * dx, y0 + t1 * dy);
            return true;
        }

        private static void RingOutline(DrawSurface s, Vector2 c, int rad, Color32 col)
            => RigDrawUtil.Ring(s, Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), rad, rad - 1, col);

        /// <summary>Right-aligned text (the rig's <c>textR</c>; RigDrawUtil has left and centred only).</summary>
        private static void TextR(DrawSurface s, string str, int xRight, int y, int scale, Color32 col)
            => RigDrawUtil.Text(s, str, xRight - RigDrawUtil.TextW(str, scale), y, scale, col);
    }
}
