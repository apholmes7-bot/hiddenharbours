using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The still water on the ground file's import (terrain PR 5 B; ADR 0046; amendment 1 §4.3, amendment 2 §4.2). The import
    /// is the ground everywhere, so every body the plan's Defs name stands again on it, by its own rule:
    /// <list type="bullet">
    /// <item>a pond: a flood from its centre over the ground under its surface, its outlet's gully left out. A pond whose
    /// flood leaks lays no water, and its guard fails;</item>
    /// <item>a key scene's pool (the plan's StillPools) and a fall's pool: the same flood at its level, dropped by whole
    /// height-map steps until it holds;</item>
    /// <item>a brook: its bed is the ground along its line where that stands under the designed bed, with a pond or pool on
    /// the line read as its water (the ground file's rule). A fall's own reach is the fall's. The water is the bed plus the
    /// brook's full depth, down to the sea (CD's rule: no tidal share);</item>
    /// <item>a pan and the crossing's pools: their own rule on the import's rim, with no carve (the import holds the dish);</item>
    /// <item>a fall's other reaches (its glide, slide, sill and chute) by CD's rule: the glide, slide and sill at their levels
    /// for the flow, the chute at the lower of the pool's level and the ground on its line plus its water;</item>
    /// <item>the hollows (part 2 §7.4, the ground file's rule): a priority flood from the open sea at a spring low; a closed
    /// basin deep and large enough keeps its water at its spill, the ponds' and pools' own basins excepted.</item>
    /// </list>
    /// Where two stand in a cell the higher wins (ADR 0046's max). Finish's drop rule then keeps water only where it stands
    /// over the ground.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        static void Lay(double[] still, int i, double lvl) => still[i] = double.IsNaN(still[i]) ? lvl : Math.Max(still[i], lvl);

        void StillOnGround()
        {
            var G = _E;
            _pondWet = new bool[_n];
            var still = Filled(double.NaN);
            double step = (Num(_plan.HeightRange.y) - Num(_plan.HeightRange.x)) / TerrainPlanMaps.CodeCount;
            double cellM2 = _g.Mpp * _g.Mpp / IsoGround.GroundDepthScale;
            var said = new List<string>();
            var own = new List<int>();                                              // the cells the ponds' and pools' basins hold

            // the ponds, then a key scene's pools: a flood at the surface
            foreach (var p in _plan.Ponds) said.Add(PondOnGround(G, still, p, p.AdjustToSpill ? null : Outlet(p), p.AdjustToSpill ? Rules.StillStepsDown : 0, step, cellM2, own));
            if (_plan.StillPools != null)
                foreach (var p in _plan.StillPools) said.Add(PondOnGround(G, still, p, null, Rules.StillStepsDown, step, cellM2, own));

            // the brooks, then the pans and the crossing's pools
            int brookCells = 0;
            for (int n = 0; n < _plan.Streams.Length; n++) brookCells += BrookOnGround(G, still, n);
            said.Add(_plan.Streams.Length + " brooks " + brookCells + " cells");
            int panCells = 0;
            for (int n = 0; n < _plan.Pans.Length; n++)
            {
                var p = _plan.Pans[n];
                panCells += RimPool(G, still, p.Id, Num(p.Centre), Num(p.Radii.x), Num(p.Radii.y), Num(p.RotationDeg), Rules.PanReach,
                                    _seed + Rules.SeedPan + n, Rules.PondWobble, Rules.PondWobbleLambda);
            }
            said.Add(_plan.Pans.Length + " pans " + panCells + " cells");
            int poolCells = 0;
            double wob = Num(_plan.Crossing.PoolWobble);
            for (int n = 0; n < _plan.Pools.Length; n++)
            {
                var p = _plan.Pools[n];
                poolCells += RimPool(G, still, p.Id, Num(p.Centre), Num(p.Radii.x), Num(p.Radii.y), Num(p.RotationDeg), Rules.PoolReach,
                                     _seed2 + Rules.SeedBarPool + n, wob, Rules.PoolEllipseLambda);
            }
            said.Add(_plan.Pools.Length + " crossing pools " + poolCells + " cells");

            // the falls: their reach is theirs; the pool by flood, the other reaches at their levels
            if (_plan.Falls != null)
                foreach (var f in _plan.Falls)
                    if (f != null) said.Add(FallOnGround(G, still, f, step, cellM2, own));

            // the hollows: part 2 §7.4's closed basins above the spring low, the ponds' and pools' own excepted
            said.Add(Hollows(G, still, cellM2, own));

            // where a body's water does not stand over the import (Finish drops it): how many, and the first few
            var dry = new List<string>();
            int dryCells = 0;
            for (int i = 0; i < _n; i++)
            {
                if (double.IsNaN(still[i]) || G[i] < still[i]) continue;
                if (dryCells++ < 6) dry.Add("(" + F(_xs[i % _w], "0.00") + ", " + F(_ys[i / _w], "0.00") + ")");
            }
            _r.Note("still.on_ground_dry_cells", dryCells);
            said.Add(dryCells + " cells at or under the ground" + (dryCells > 0 ? ", e.g. " + string.Join(" ", dry) : ""));

            _still = still;
            Log("finish", "the ground file's import is the map's ground; the still water stands on it: " + string.Join("; ", said));
        }

        /// <summary>The stream a pond spills into, as the derivation laid its line, or null.</summary>
        PlanLine Outlet(PondDef p)
        {
            if (p == null || p.Outlet == null) return null;
            for (int k = 0; k < _plan.Streams.Length; k++) if (ReferenceEquals(_plan.Streams[k], p.Outlet)) return _streamLines[k];
            return Catmull(Num(p.Outlet.Points), _lineStep);
        }

        /// <summary>
        /// A pond or pool by flood at its surface. A pond's outlet gully is left out. With <paramref name="steps"/> &gt; 0 a flood
        /// that leaks drops by whole height-map steps, at most that many, until it holds; with none, a leak lays no water.
        /// The cell under its centre joins <paramref name="own"/>: its basin is the pond's, not a hollow.
        /// </summary>
        string PondOnGround(double[] G, double[] still, PondDef p, PlanLine outlet, int steps, double step, double cellM2, List<int> own)
        {
            if (p == null) return "a missing pond";
            var cc = Num(p.Centre);
            double ra = Num(p.Radii.x), rb = Num(p.Radii.y), surface = Num(p.Surface);
            double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
            double R = Math.Max(ra, rb) * Num(_plan.FloodWindow) + Num(p.Basin.x), leakQ = Num(_plan.FloodLeak);
            double gullyHalf = Num(_plan.OutletGully.x), gullyFrom = Num(_plan.OutletGully.y);
            if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException("[TerrainPlan] " + p.Id + " is off the grid.");
            own.Add(_g.RowOf(cc.Y) * _w + _g.ColOf(cc.X));
            int ww = c1 - c0;
            var gully = new bool[(r1 - r0) * ww];
            if (outlet != null)
            {
                int hint = -1;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    outlet.Query(_xs[c], _ys[r], ref hint, out double d, out _, out double u);
                    gully[(r - r0) * ww + (c - c0)] = d <= gullyHalf && u > gullyFrom;
                }
            }
            double lvl = surface;
            List<int> cells = null;
            bool leaks = true;
            int k = 0;
            for (; k <= steps; k++)
            {
                lvl = surface - k * step;
                double at = lvl;
                cells = FloodFrom(cc, r0, r1, c0, c1, i => G[i] < at && !gully[(i / _w - r0) * ww + (i % _w - c0)],
                                  i => EllipseRadius(_xs[i % _w], _ys[i / _w], cc, ra, rb, cr, sr, 0, 0.0, 1.0), leakQ, out leaks);
                if (!leaks) break;
            }
            if (leaks)
            {
                _r.Note(p.Id + ".ground_leaks", 1L);
                return p.Id + " LEAKS at " + F(lvl, "0.0000") + ": no water";
            }
            double deepest = 0;
            bool fallSource = false;
            foreach (var fall in _plan.Falls ?? new WaterfallDef[0])
                if (fall != null && fall.Stream != null && ReferenceEquals(fall.Stream.Source, p)) fallSource = true;
            foreach (int i in cells)
            {
                deepest = Math.Max(deepest, lvl - G[i]); Lay(still, i, lvl);
                if (fallSource && _pondWet != null) _pondWet[i] = true;
                if (!string.IsNullOrEmpty(p.FloorZone) && _zone != null) _zone[i] = Zi(p.FloorZone);
            }
            _r.Note(p.Id + ".ground_level", lvl);
            _r.Note(p.Id + ".ground_cells", cells.Count);
            _r.Note(p.Id + ".ground_m2", cells.Count * cellM2);
            _r.Note(p.Id + ".ground_deepest", deepest);
            if (k > 0) _r.Note(p.Id + ".ground_steps_down", k);
            return p.Id + " at " + F(lvl, "0.0000") + (k > 0 ? " (" + k + " steps down)" : "") + ", " + cells.Count + " cells";
        }

        /// <summary>
        /// A 4-neighbour flood from the cell holding <paramref name="at"/> over the window's cells <paramref name="wet"/> admits:
        /// the cells, sorted. It leaks when it touches the window's edge or a cell past <paramref name="leakQ"/> of its ellipse.
        /// </summary>
        List<int> FloodFrom(PlanPoint at, int r0, int r1, int c0, int c1, Func<int, bool> wet, Func<int, double> q, double leakQ, out bool leaks)
        {
            leaks = false;
            var o = new List<int>();
            int sr = _g.RowOf(at.Y), sc = _g.ColOf(at.X);
            if (sr < r0 || sr >= r1 || sc < c0 || sc >= c1) return o;
            int s = sr * _w + sc;
            if (!wet(s)) return o;
            int ww = c1 - c0;
            var seen = new bool[(r1 - r0) * ww];
            var stack = new Stack<int>();
            seen[(sr - r0) * ww + (sc - c0)] = true;
            stack.Push(s);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                o.Add(i);
                int r = i / _w, c = i - r * _w;
                if (r == r0 || r == r1 - 1 || c == c0 || c == c1 - 1 || q(i) > leakQ) leaks = true;
                if (c > c0) Try(i - 1, r, c - 1);
                if (c < c1 - 1) Try(i + 1, r, c + 1);
                if (r > r0) Try(i - _w, r - 1, c);
                if (r < r1 - 1) Try(i + _w, r + 1, c);
            }
            o.Sort();
            return o;

            void Try(int j, int r, int c)
            {
                int k = (r - r0) * ww + (c - c0);
                if (seen[k] || !wet(j)) return;
                seen[k] = true;
                stack.Push(j);
            }
        }

        /// <summary>
        /// A brook on the import: the bed along its line is the ground where it stands under the designed bed (the ground file's
        /// rule; a pond or pool on the line read as its water); a fall's own reach is skipped. That the ground never climbs
        /// along it is its guard's (StreamsNeverClimb). Its water, the bed plus its full depth (CD's rule), fills its channel.
        /// Returns the cells laid.
        /// </summary>
        public static double BrookHeadLevel(StreamDef stream, double bed, int station) =>
            station == 0 && stream.Source != null ? Num(stream.Source.Surface) : bed + Num(stream.Depth);

        int BrookOnGround(double[] G, double[] still, int n)
        {
            var st = _plan.Streams[n];
            var g = _streamLines[n];
            var z = Num(st.BedZ);
            double depth = Num(st.Depth);
            foreach (var p in _plan.Ponds)
                if (st.Source != null && ReferenceEquals(p, st.Source)) z[0] = Num(p.Surface) - depth;      // its pond's level
            FallShape reach = null;
            if (_plan.Falls != null)
                foreach (var f in _plan.Falls)
                    if (f != null && ReferenceEquals(f.Stream, st)) reach = new FallShape(f);
            int m = g.R.Length;
            var zb = new double[m];
            for (int j = 0; j < m; j++)
            {
                double x = g.R[j].X, y = g.R[j].Y;
                if (j > 0 && reach != null && reach.InBox(x, y) && y < reach.ChuteTo) { zb[j] = double.NaN; continue; }   // the fall's own water
                double e = _g.Sample(G, x, y);
                double body = BodyAt(x, y);
                if (!double.IsNaN(body)) e = Math.Max(e, body - depth);
                zb[j] = BrookHeadLevel(st, Math.Min(Interp(g.S[j], g.Knots, z), e), j) - depth;                   // the file's bed: no running minimum
            }
            var widths = Num(st.Widths);
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in Num(st.Points)) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            double reachM = Rules.StreamWindow;
            if (!Win(x0 - reachM, y0 - reachM, x1 + reachM, y1 + reachM, out int r0, out int r1, out int c0, out int c1)) return 0;
            int hint = -1, laid = 0;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                g.Query(_xs[c], _ys[r], ref hint, out double d, out _, out double u);
                double hw = Interp(u, g.Knots, widths) / 2.0;
                if (!(d <= hw + Rules.ChannelEdge)) continue;
                double b = Interp(u, g.S, zb);
                if (double.IsNaN(b)) continue;
                Lay(still, r * _w + c, b + depth);
                laid++;
            }
            _r.Note(st.Id + ".ground_cells", laid);
            return laid;
        }

        /// <summary>The surface of a pond or a key scene's pool whose ellipse (unwobbled) holds the point, else NaN.</summary>
        double BodyAt(double x, double y)
        {
            double s = double.NaN;
            void Try(PondDef p)
            {
                if (p == null) return;
                double rot = Radians(Num(p.RotationDeg));
                if (EllipseRadius(x, y, Num(p.Centre), Num(p.Radii.x), Num(p.Radii.y), Math.Cos(rot), Math.Sin(rot), 0, 0.0, 1.0) < 1)
                    s = double.IsNaN(s) ? Num(p.Surface) : Math.Max(s, Num(p.Surface));
            }
            foreach (var p in _plan.Ponds) Try(p);
            if (_plan.StillPools != null) foreach (var p in _plan.StillPools) Try(p);
            return s;
        }

        /// <summary>
        /// The pan rule on the import (a salt pan's, a crossing pool's): the water stands at the lowest ground on its rim, less
        /// the rim drop, inside its ellipse. Returns the cells laid.
        /// </summary>
        int RimPool(double[] G, double[] still, string id, PlanPoint cc, double ra, double rb, double rotDeg, double reach, long seed, double wob,
                    double lam)
        {
            double R = Math.Max(ra, rb) * reach, rot = Radians(rotDeg), cr = Math.Cos(rot), sr = Math.Sin(rot);
            if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) return 0;
            int ww = c1 - c0;
            var rv = new double[(r1 - r0) * ww];
            double lvl = double.PositiveInfinity;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                double rr = EllipseRadius(_xs[c], _ys[r], cc, ra, rb, cr, sr, seed, wob, lam);
                rv[(r - r0) * ww + (c - c0)] = rr;
                if (rr >= Rules.PanRimFrom && rr < Rules.PanRimTo) lvl = Math.Min(lvl, G[r * _w + c]);
            }
            if (double.IsPositiveInfinity(lvl)) return 0;
            lvl -= Rules.PanRimDrop;
            int laid = 0;
            double deepest = 0;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                if (!(rv[(r - r0) * ww + (c - c0)] < 1 && G[i] < lvl)) continue;
                deepest = Math.Max(deepest, lvl - G[i]);
                Lay(still, i, lvl);
                laid++;
            }
            _r.Note(id + ".ground_level", lvl);
            _r.Note(id + ".ground_cells", laid);
            _r.Note(id + ".ground_deepest", deepest);
            return laid;
        }

        /// <summary>
        /// A fall on the import: its reach cleared, its pool by flood at its level, its other reaches by CD's rule: the glide,
        /// the slide and the sill at their levels for the flow, the chute at the lower of the pool's level and the ground on
        /// its line plus its water, each where it stands over the ground.
        /// </summary>
        string FallOnGround(double[] G, double[] still, WaterfallDef f, double step, double cellM2, List<int> own)
        {
            var s = new FallShape(f);
            if (!Win(s.BoxX0, s.BoxY0, s.BoxX1, s.BoxY1, out int r0, out int r1, out int c0, out int c1)) return f.Id + " off the grid";
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
                if (s.InBox(_xs[c], _ys[r]) && _ys[r] < s.ChuteTo && (_pondWet == null || !_pondWet[r * _w + c])) still[r * _w + c] = double.NaN;
            // the pool: a flood at its level, inside its own window
            var pool = f.Pool;
            double lvl0 = s.Surf + s.PoolWater, R = Math.Max(s.Pra, s.Prb) * Num(_plan.FloodWindow) + Num(pool.Basin.x), leakQ = Num(_plan.FloodLeak);
            var cc = new PlanPoint(s.Pcx, s.Pcy);
            own.Add(_g.RowOf(cc.Y) * _w + _g.ColOf(cc.X));
            Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int pr0, out int pr1, out int pc0, out int pc1);
            double lvl = lvl0;
            List<int> cells = null;
            bool leaks = true;
            int k = 0;
            for (; k <= Rules.StillStepsDown; k++)
            {
                lvl = lvl0 - k * step;
                double at = lvl;
                cells = FloodFrom(cc, pr0, pr1, pc0, pc1, i => G[i] < at, i => s.PoolQ(_xs[i % _w], _ys[i / _w]), leakQ, out leaks);
                if (!leaks) break;
            }
            double deepest = 0;
            if (!leaks) foreach (int i in cells) { deepest = Math.Max(deepest, lvl - G[i]); Lay(still, i, lvl); }
            int poolCells = leaks ? 0 : cells.Count;
            _r.Note(pool.Id + ".ground_level", lvl);
            _r.Note(pool.Id + ".ground_cells", poolCells);
            _r.Note(pool.Id + ".ground_m2", poolCells * cellM2);
            _r.Note(pool.Id + ".ground_deepest", deepest);
            if (leaks) _r.Note(pool.Id + ".ground_leaks", 1L);
            if (k > 0) _r.Note(pool.Id + ".ground_steps_down", k);
            // the glide, the slide and the sill at their levels for the flow; the chute at the lower of the pool's level and
            // the ground on its line plus its water, out to where that meets its designed dish and ChuteWaterPast beyond; each
            // where it stands over the ground by MinWater (CD's rule, the package's tools/stpOnePaint.js)
            double poolLevel = leaks ? lvl0 : lvl, chuteHalf = Math.Min(s.WaterHalf, FallShape.SectionHalf(s.ChuteWater, s.Chute) + s.ChuteWaterPast);
            int reaches = 0, chuteCapped = 0;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r];
                if (!s.InBox(X, Y) || s.PoolQ(X, Y) < 1 || (_pondWet != null && _pondWet[i])) continue;
                double a = Math.Abs(X - s.CxAt(Y)), w;
                if (Y >= s.ApproachFrom && Y <= s.LipY && a < s.WaterHalf)
                    w = s.ZbUp(Y) + (Y < s.Step ? s.GlideWater : Y < s.Sill ? s.SlideWater : s.SillWater);
                else if (Y > s.ChuteFrom && Y < s.ChuteTo && a <= chuteHalf)
                {
                    double onLine = _g.Sample(G, s.CxAt(Y), Y) + s.ChuteWater;
                    if (poolLevel < onLine) chuteCapped++;
                    w = Math.Min(poolLevel, onLine);
                }
                else continue;
                if (!(w > G[i] + s.MinWater)) continue;
                Lay(still, i, w);
                reaches++;
            }
            _r.Note(f.Id + ".ground_reach_cells", reaches);
            _r.Note(f.Id + ".ground_chute_half", chuteHalf);
            _r.Note(f.Id + ".ground_chute_at_pool", chuteCapped);
            return f.Id + ": its pool " + (leaks ? "LEAKS" : "at " + F(lvl, "0.0000") + (k > 0 ? " (" + k + " steps down)" : "") + ", " + poolCells + " cells") +
                   ", its reaches " + reaches + " cells";
        }

        /// <summary>
        /// The hollows (part 2 §7.4): a priority flood from the map's edge at the spring low; a closed basin at least
        /// HollowMinDepth deep over at least HollowMinAreaM2 of ground keeps its water at its spill, unless it holds a pond's or
        /// a pool's centre (<paramref name="own"/>). Each is recorded under the ground file's name for it, by its centre.
        /// </summary>
        string Hollows(double[] G, double[] still, double cellM2, List<int> own)
        {
            double floor = -_spring, minDeep = Num(_plan.HollowMinDepth), minArea = Num(_plan.HollowMinAreaM2);
            var lvl = TerrainPlanFlood.SpillLevels(G, _w, _h, floor);
            var basins = TerrainPlanFlood.Basins(G, lvl, _w, _h, floor, TerrainPlanFlood.WetEps);
            var mine = new HashSet<int>(own);
            int found = 0, owned = 0, shared = 0;
            double area = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in basins)
            {
                if (!(b.Deepest >= minDeep && b.Cells.Length * cellM2 >= minArea)) continue;
                found++;
                bool pond = false, wet = false;
                foreach (int i in b.Cells) { pond |= mine.Contains(i); wet |= !double.IsNaN(still[i]); }
                if (pond) { owned++; continue; }
                if (wet) shared++;
                double sx = 0, sy = 0;
                foreach (int i in b.Cells) { Lay(still, i, lvl[i]); sx += _xs[i % _w]; sy += _ys[i / _w]; }
                double cx = sx / b.Cells.Length, cy = sy / b.Cells.Length;
                string stem = "hollow.stp_" + (cx < 0 ? "w" : "e") + F(RoundHalfEven(Math.Abs(cx)), "0") + "_" + (cy < 0 ? "s" : "n") +
                              F(RoundHalfEven(Math.Abs(cy)), "0");
                string id = stem;
                for (int k = 2; !ids.Add(id); k++) id = stem + "_" + k;
                area += b.Cells.Length * cellM2;
                _r.Hollows.Add(new TerrainPlanPoolRecord
                {
                    Id = id, Source = "part 2 §7.4", CentreX = cx, CentreY = cy,
                    Spill = b.Spill, Bed = b.Spill - b.Deepest, AreaM2 = b.Cells.Length * cellM2, MaxWater = b.Deepest,
                });
            }
            _r.Note("hollows.found", found);
            _r.Note("hollows.ponds_own", owned);
            _r.Note("hollows.kept", _r.Hollows.Count);
            _r.Note("hollows.kept_m2", area);
            _r.Note("hollows.kept_with_other_water", shared);
            return "hollows: " + found + " basins at least " + F(minDeep) + " m deep and " + F(minArea) + " m², " + owned + " a pond's or pool's own, " +
                   _r.Hollows.Count + " kept (" + F(area, "0.0") + " m²; " + shared + " with other water in them)";
        }
    }
}
