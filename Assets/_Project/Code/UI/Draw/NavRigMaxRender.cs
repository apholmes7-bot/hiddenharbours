using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chartplotter's MAX-face panes — the layer/tool RAIL (navRig.js:417-421), the waypoint and
    /// route MANAGER column (js:422-445) and the depth-PROFILE strip (js:446-461). Split out of
    /// <see cref="NavRigRender"/> because those are the three boxes the console face does not have;
    /// everything they share with it — the case, the glass, the chart body, the two data strips — stays
    /// in the one renderer, drawn by the same code at both faces.
    ///
    /// <para><b>Every pane reads the SAME state</b> the console face reads: one
    /// <see cref="NavChartSource"/> survey, one <see cref="NavLocker"/>-filled waypoint list, one route.
    /// No pane owns a copy, so the manager's bearing to a mark and the mark drawn on the chart cannot
    /// drift apart.</para>
    ///
    /// <para><b>Two of the source's strings have no glyph in the source's own font</b>, and are handled
    /// the way <c>NavRigRender.BotBar</c> already handles its em dashes — printed with pixels the shared
    /// font has rather than as a gap or a tofu block. They are called out at their call sites.</para>
    /// </summary>
    internal static class NavRigMaxRender
    {
        // ---- the layer / tool rail (navRig.js:417-421, 509-510) ---------------------------------------

        /// <summary>
        /// Draw every rail button and the rail's own right-hand edge.
        ///
        /// <para>Lit state is the rig's (js:509): a TOOL button lights when it is the selected tool
        /// (with PAN lighting for "no tool"), a LAYER button lights when its layer is on. The small
        /// square pip in the corner is drawn for a lit LAYER only — the source's own way of telling the
        /// two halves of the rail apart at a glance (js:420).</para>
        /// </summary>
        internal static void Rail(DrawSurface s, in NavRigGeometry.Layout layout, in NavMaxState mx,
                                  in NavPalette p)
        {
            RectInt rail = layout.Rail;
            if (rail.width <= 0 || rail.height <= 0) return;

            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                if (!NavRigGeometry.TryRailButton(in layout, slot, out RectInt b)) continue;
                bool tool = NavRigGeometry.RailSlotIsTool(slot);
                bool on = tool ? mx.Tool == NavRigGeometry.RailTool(slot)
                               : mx.LayerOn(NavRigGeometry.RailLayer(slot));
                RailButton(s, b, NavRigGeometry.RailIds[slot], on, tool, in p);
            }

            s.FillRect(rail.x + rail.width - 1, rail.y, 1, rail.height, p.RailEdge);
        }

        private static void RailButton(DrawSurface s, RectInt b, string id, bool on, bool tool,
                                       in NavPalette p)
        {
            if (b.width <= 0 || b.height <= 0) return;
            RigDrawUtil.RRect(s, b.x, b.y, b.width, b.height, 2, on ? p.RailLit : p.RailPlate);
            s.FillRect(b.x, b.y, b.width, 1, p.RailEdge);

            Color32 col = on ? p.RailTxt : (tool ? p.StripHi : p.RailDim);
            RigDrawUtil.TextC(s, id, b.x + b.width / 2.0, b.y + DrawSurface.JsRound((b.height - 5) / 2.0),
                              1, col);
            if (on && !tool) s.FillRect(b.x + 2, b.y + 2, 2, 2, p.RailTxt);
        }

        // ---- the waypoint & route manager (navRig.js:422-445) -----------------------------------------

        /// <summary>
        /// Draw the manager column: the waypoint list with its selection, the route's legs, the
        /// measure cursor's range and bearing, and the tide and set.
        ///
        /// <para>Bearings and distances are taken FROM THE BOAT (the rig measures from its own-ship
        /// constant, js:426). With no fix there is nothing to measure from, so those two columns print
        /// the survey's own "--" rather than a bearing from the origin.</para>
        /// </summary>
        internal static void Manager(DrawSurface s, in NavRigGeometry.Manager m, in NavRigState st,
                                     in NavMaxState mx, in NavPalette p,
                                     IReadOnlyList<NavWaypoint> waypoints, IReadOnlyList<Vector2> route)
        {
            RectInt r = m.Right;
            if (r.width <= 0 || r.height <= 0) return;
            s.FillRect(r.x, r.y, r.width, r.height, p.Strip);
            s.FillRect(r.x, r.y, 1, r.height, p.RailEdge);

            int x = m.TextX;
            int right = r.x + r.width - 6;
            int rightMid = r.x + r.width - 30;
            int count = waypoints?.Count ?? 0;

            // ---- waypoints (js:425-430) ----
            RigDrawUtil.Text(s, "WAYPOINTS", x, m.WaypointHeaderY, 1, p.StripHi);
            NavRigRender.TextR(s, count.ToString(), right, m.WaypointHeaderY, 1, p.StripDim);

            for (int i = 0; i < m.WaypointRowCount; i++)
            {
                NavWaypoint w = waypoints[i];
                int y = m.WaypointRowsY + i * NavRigGeometry.ManagerRowH;
                bool sel = mx.SelectedWaypoint == i;
                if (sel)
                {
                    RectInt band = m.WaypointRow(i);
                    s.FillRect(band.x, band.y, band.width, band.height, p.RailLit);
                }
                s.FillRect(x, y + 1, 3, 3, p.WaypointColour(w.Kind));
                RigDrawUtil.Text(s, Shorten(w.Name, 7), x + 6, y, 1, sel ? p.StripFg : p.StripDim);

                if (st.HasBoat)
                {
                    NavRigRender.TextR(s, NavRigGeometry.FmtDeg(NavMath.BearingDegrees(st.Boat, w.Pos)) + "°",
                                       rightMid, y, 1, p.StripDim);
                    NavRigRender.TextR(s, NavRigGeometry.FmtNM(NavMath.DistanceNM(st.Boat, w.Pos)),
                                       right, y, 1, p.StripFg);
                }
                else
                {
                    NavRigRender.TextR(s, "--", right, y, 1, p.StripDim);
                }
            }

            Rule(s, r, m.RouteHeaderY, in p);

            // ---- the route (js:432-435) ----
            int points = route?.Count ?? 0;
            RigDrawUtil.Text(s, "ROUTE", x, m.RouteHeaderY, 1, p.StripHi);
            // js:433 writes an em dash for "no route"; the shared font has no U+2014 (nor does the
            // source's own), so a hyphen carries the same rule with pixels that exist — the BotBar
            // precedent, unchanged.
            NavRigRender.TextR(s, points > 1
                                  ? NavRigGeometry.FmtNM(NavMath.MetresToNM(NavMath.RouteLengthMetres(route))) + "NM"
                                  : "-",
                               right, m.RouteHeaderY, 1, p.StripFg);

            for (int i = 0; i < m.RouteRowCount; i++)
            {
                int y = m.RouteRowsY + i * NavRigGeometry.ManagerRowH;
                Vector2 a = route[i], b = route[i + 1];
                RigDrawUtil.Text(s, "LEG " + (i + 1), x, y, 1, p.StripDim);
                NavRigRender.TextR(s, NavRigGeometry.FmtDeg(NavMath.BearingDegrees(a, b)) + "°",
                                   rightMid, y, 1, p.StripDim);
                NavRigRender.TextR(s, NavRigGeometry.FmtNM(NavMath.DistanceNM(a, b)), right, y, 1,
                                   p.StripFg);
            }

            Rule(s, r, m.CursorHeaderY, in p);

            // ---- the measure cursor (js:437-440) ----
            RigDrawUtil.Text(s, "CURSOR", x, m.CursorHeaderY, 1, p.StripHi);
            int cy = m.CursorHeaderY + NavRigGeometry.ManagerRowH;
            if (mx.Measure == NavMeasureState.Complete)
            {
                RigDrawUtil.Text(s, "RNG", x, cy, 1, p.StripDim);
                NavRigRender.TextR(s, NavRigGeometry.FmtNM(NavMath.DistanceNM(mx.MeasureFrom, mx.MeasureTo)) + "NM",
                                   right, cy, 1, p.StripFg);
                cy += NavRigGeometry.ManagerRowH;
                RigDrawUtil.Text(s, "BRG", x, cy, 1, p.StripDim);
                NavRigRender.TextR(s, NavRigGeometry.FmtDeg(NavMath.BearingDegrees(mx.MeasureFrom, mx.MeasureTo)) + "°",
                                   right, cy, 1, p.StripFg);
            }
            else
            {
                RigDrawUtil.Text(s, "MEASURE TOOL", x, cy, 1, p.StripDim);
                // The source says TAP TWO POINTS (js:440) for a touch plotter. This one is dragged:
                // press on the chart, pull, release — the natural gesture on the platform this ships on.
                RigDrawUtil.Text(s, "DRAG THE CHART", x, cy + NavRigGeometry.ManagerRowH, 1, p.StripDim);
            }

            Rule(s, r, m.TideHeaderY, in p);

            // ---- tide & set (js:442-444) ----
            // The source's header is "TIDE & SET"; neither its font nor the shared one carries '&', so
            // the source itself draws a gap there. A slash carries the same "two readings, one pane"
            // sense with pixels the font has — the same substitution rule as the em dash above.
            RigDrawUtil.Text(s, "TIDE / SET", x, m.TideHeaderY, 1, p.StripHi);
            int ty = m.TideHeaderY + NavRigGeometry.ManagerRowH;

            RigDrawUtil.Text(s, "HT", x, ty, 1, p.StripDim);
            NavRigRender.TextR(s, float.IsNaN(st.TideMetres)
                                  ? "--"
                                  : Fixed1(st.TideMetres) + "M " + (st.TideRising ? "RIS" : "FALL"),
                               right, ty, 1, p.StripFg);

            ty += NavRigGeometry.ManagerRowH;
            RigDrawUtil.Text(s, "SET", x, ty, 1, p.StripDim);
            NavRigRender.TextR(s, mx.HasSet
                                  ? NavRigGeometry.FmtDeg(mx.SetDeg) + "° " + Fixed1(mx.DriftKnots) + "KN"
                                  : "--",
                               right, ty, 1, p.StripFg);

            ManagerActions(s, in m, in mx, in p, waypoints);
        }

        /// <summary>
        /// The NAME / DEL pair, and the name editor's field while it is open.
        ///
        /// <para><b>The one affordance the rig does not author</b> — see
        /// <see cref="NavRigGeometry.Manager.NameButton"/> for where it is placed and why that shifts
        /// nothing the source does author. Drawn with the rail's button primitive and colours so it
        /// belongs to the same instrument.</para>
        /// </summary>
        private static void ManagerActions(DrawSurface s, in NavRigGeometry.Manager m,
                                           in NavMaxState mx, in NavPalette p,
                                           IReadOnlyList<NavWaypoint> waypoints)
        {
            RectInt name = m.NameButton, del = m.DeleteButton;
            if (name.width <= 0 || del.width <= 0) return;

            if (mx.Editing)
            {
                // The field takes the whole action row while it is open: there is nothing else to press.
                RectInt field = new RectInt(name.x, name.y, del.x + del.width - name.x, name.height);
                RigDrawUtil.RRect(s, field.x, field.y, field.width, field.height, 2, p.RailLit);
                s.FillRect(field.x, field.y, field.width, 1, p.RailEdge);

                int tx = field.x + 3, ty = field.y + DrawSurface.JsRound((field.height - 5) / 2.0);
                RigDrawUtil.Text(s, mx.EditText, tx, ty, 1, p.StripFg);
                // A caret drawn as a RECT rather than a glyph: every full block in this font's shape
                // vocabulary is the unknown-character tofu, and a cursor must never be readable as one.
                int caretX = tx + RigDrawUtil.TextW(mx.EditText, 1) + (mx.EditText.Length > 0 ? 1 : 0);
                if (caretX < field.x + field.width - 2) s.FillRect(caretX, ty, 1, 5, p.StripHi);
                return;
            }

            int selected = mx.SelectedWaypoint;
            bool live = waypoints != null && selected >= 0 && selected < waypoints.Count;
            if (!live) return;

            RailButton(s, name, "NAME", false, true, in p);
            RailButton(s, del, "DEL", false, true, in p);
        }

        private static void Rule(DrawSurface s, RectInt r, int headerY, in NavPalette p)
        {
            // The rig draws its rule 10 px above the header that follows it (js:431 `y+=4; rule; y+=6`).
            s.FillRect(r.x + 4, headerY - 6, r.width - 8, 1, p.StripDim);
        }

        private static string Shorten(string name, int max)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= max ? name : name.Substring(0, max);
        }

        private static string Fixed1(float v)
            => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        // ---- the depth-profile strip (navRig.js:446-461) ----------------------------------------------

        /// <summary>
        /// Draw the DEPTH AHEAD strip from an already-sampled <see cref="NavDepthProfile"/>.
        ///
        /// <para>The seabed is filled column by column rather than as one polygon, which is what makes
        /// the honesty case free: a sample that landed off the survey is <see cref="float.NaN"/> and its
        /// column is simply not drawn, so the strip shows a GAP over unsurveyed water instead of a
        /// confident seabed at zero metres.</para>
        /// </summary>
        internal static void Profile(DrawSurface s, RectInt pr, NavDepthProfile profile,
                                     in NavPalette p)
        {
            if (pr.width <= 0 || pr.height <= 0) return;
            s.FillRect(pr.x, pr.y, pr.width, pr.height, p.ProfSky);
            s.FillRect(pr.x, pr.y, pr.width, 1, p.RailEdge);

            int gy = pr.y + 9, gh = pr.height - 13, gx = pr.x + 2, gw = pr.width - 4;
            int bottom = pr.y + pr.height;
            RigDrawUtil.Text(s, "DEPTH AHEAD", pr.x + 4, pr.y + 2, 1, p.ProfTxt);

            int n = profile?.Count ?? 0;
            if (profile == null || n < 2 || !profile.HasProfile || gh <= 0 || gw <= 0)
            {
                // No survey under the line — the chart's own honesty rule, said in words rather than
                // drawn as a flat bottom at zero.
                RigDrawUtil.TextC(s, "NO SURVEY", pr.x + pr.width / 2.0,
                                  pr.y + pr.height / 2 - 2, 1, p.StripDim);
                return;
            }

            float maxD = Mathf.Max(1f, profile.MaxDepth);
            float[] ds = profile.Depths;
            int prevX = int.MinValue, prevY = 0;

            for (int i = 0; i < n; i++)
            {
                float d = ds[i];
                if (float.IsNaN(d)) { prevX = int.MinValue; continue; }

                int px = gx + Mathf.RoundToInt(i / (float)(n - 1) * gw);
                int py = gy + Mathf.RoundToInt(Mathf.Clamp01(d / maxD) * gh);
                if (py < pr.y + 1) py = pr.y + 1;
                if (py > bottom - 1) py = bottom - 1;

                // The water column above the bed stays ProfSky (already filled); the bed is everything
                // below it.
                if (py < bottom) s.FillRect(px, py, 1, bottom - py, p.ProfBed);
                if (prevX != int.MinValue)
                    RigDrawUtil.ThickLine(s, prevX, prevY, px, py, 1, p.ProfBedEdge);
                else
                    s.FillRect(px, py, 1, 1, p.ProfBedEdge);
                prevX = px;
                prevY = py;
            }

            NavRigRender.TextR(s, "0M", pr.x + pr.width - 4, gy - 6, 1, p.StripDim);
            NavRigRender.TextR(s, Mathf.RoundToInt(maxD) + "M", pr.x + pr.width - 4, bottom - 8, 1,
                               p.StripDim);
        }
    }
}
