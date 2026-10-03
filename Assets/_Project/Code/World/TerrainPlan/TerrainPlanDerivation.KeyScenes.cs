using System;
using System.Collections.Generic;
using System.Globalization;
using HiddenHarbours.Core;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The key scenes' steps (terrain PR 5, §4.1), after part 1 and the crossing, weakest first so the stronger overwrites:
    /// <list type="number">
    /// <item>tier 3, the key scenes' ground: the Head's pieces in the key scene's own order (the neck, the top, the platform,
    /// the bar, the plinth), each laid as max(plan, piece), never lowering; the platform's rock pools; the fall's cut. Each
    /// paints where it moved the ground;</item>
    /// <item>tier 2: Ginny's frozen pieces, her dooryard, her path and her track at part 1's plan heights and paint;</item>
    /// <item>the key scenes' lots and paths, painted after the plan's paths;</item>
    /// <item>tier 1: pass 9's frozen mask at today's heights (part 1's to the bit on the crossing's own keep), today's paint
    /// where the keep holds it, the builder's roads repainted on their own line.</item>
    /// </list>
    /// The Head's ground is the key scene's rules without its board relief (the value-noise terms of R2 pass 3's
    /// neGround.js): the plan's ground carries no board noise.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        byte[] _owner;
        readonly List<RecipeTable> _ownerRecipe = new List<RecipeTable> { null };
        readonly List<long> _ownerSeed = new List<long> { 0 };
        double[] _f2, _pathWeight;
        bool[] _held1, _keyPaint;
        byte[] _platformOwner;

        byte NewOwner(string id, CoastRecipeDef recipe = null, long seed = 0)
        {
            if (_r.KeyOwners.Count >= 255) throw new InvalidOperationException("[TerrainPlan] more than 254 key-scene pieces.");
            _r.KeyOwners.Add(id);
            _ownerRecipe.Add(recipe != null ? new RecipeTable(recipe) : null);
            _ownerSeed.Add(seed);
            return (byte)(_r.KeyOwners.Count - 1);
        }

        void KeyScenes()
        {
            _r.E2 = (double[])_E.Clone();
            _owner = new byte[_n];
            _pathWeight = new double[_n];
            _keyPaint = new bool[_n];
            Held1();
            Head(_E, _zone);
            PlatformPools(_E, _still, _zone);
            Falls(_E, _still, _zone, _owner);
            Tier2(_E, _zone);
            Lots(_zone);
            KeyPaths(_zone, _still);
            Tier1(_E, _zone, _still);
        }

        /// <summary>Tier 1's cells: the crossing's frozen set, part 1's keep less its bar (the south, the cliffs), the far west.</summary>
        void Held1()
        {
            double frozenAt = Num(_keep.FrozenAt), west = Num(_plan.Crossing.XRange.x);
            _held1 = new bool[_n];
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                _held1[i] = _pe2[i] >= frozenAt || _peA[i] >= frozenAt || _xs[c] < west;
            }
        }

        // ---- tier 3: the Head (R2 pass 3's groundAt) -------------------------------------------------------------------------

        /// <summary>A headland's frame: its brow (closed by the root line, its last edge), the root line's axes.</summary>
        sealed class Root
        {
            public readonly PlanPoint[] Brow;
            public readonly double Ax, Ay, Tx, Ty, Nx, Ny, L, MinX, MinY, MaxX, MaxY;

            public Root(FormDef f)
            {
                Brow = Num(f.Brow);
                if (Brow.Length < 3) throw new InvalidOperationException("[TerrainPlan] " + f.Id + " has no brow.");
                PlanPoint a = Brow[Brow.Length - 1], b = Brow[0];
                Ax = a.X; Ay = a.Y;
                L = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                Tx = (b.X - a.X) / L; Ty = (b.Y - a.Y) / L; Nx = -Ty; Ny = Tx;
                MinX = MinY = double.PositiveInfinity; MaxX = MaxY = double.NegativeInfinity;
                foreach (var p in Brow) { MinX = Math.Min(MinX, p.X); MinY = Math.Min(MinY, p.Y); MaxX = Math.Max(MaxX, p.X); MaxY = Math.Max(MaxY, p.Y); }
            }

            /// <summary>Metres seaward of the root line.</summary>
            public double U(double x, double y) => (x - Ax) * Nx + (y - Ay) * Ny;

            /// <summary>Metres along the root line from its north corner (the brow's last point).</summary>
            public double W(double x, double y) => (x - Ax) * Tx + (y - Ay) * Ty;

            /// <summary>The signed distance to the brow (negative on the Head): the key scene's sdPoly.</summary>
            public double Sd(double x, double y)
            {
                double d = 1e9; bool inside = false;
                for (int i = 0, j = Brow.Length - 1; i < Brow.Length; j = i++)
                {
                    double ax = Brow[j].X, ay = Brow[j].Y, bx = Brow[i].X, by = Brow[i].Y;
                    double e = SegD(x, y, ax, ay, bx, by);
                    if (e < d) d = e;
                    if ((ay > y) != (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) inside = !inside;
                }
                return inside ? -d : d;
            }
        }

        /// <summary>The key scene's segment distance (a degenerate segment is its start point).</summary>
        static double SegD(double px, double py, double ax, double ay, double bx, double by)
        {
            double vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            double t = l2 != 0 ? ((px - ax) * vx + (py - ay) * vy) / l2 : 0;
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return Hypot(px - ax - vx * t, py - ay - vy * t);
        }

        /// <summary>Raise a cell to <paramref name="z"/> (never lower it); the piece owns it where it moved it more than the rule.</summary>
        void Raise(double[] E, int i, double z, byte me, ref int moved)
        {
            if (!(z > E[i])) return;
            if (z - E[i] > Rules.KeySceneMoved) { _owner[i] = me; moved++; }
            E[i] = z;
        }

        void Head(double[] E, byte[] zone)
        {
            double emin = double.PositiveInfinity;
            for (int i = 0; i < _n; i++) emin = Math.Min(emin, E[i]);
            var said = new List<string>();
            int step = Rules.KeyScenePieceSeedStep;
            for (int k = 0; k < _plan.Ramps.Length; k++)
                if (_plan.Ramps[k] != null) said.Add(Ramp(E, _plan.Ramps[k], _seed + Rules.SeedHeadNeck + step * k, emin));
            for (int k = 0; k < _plan.Forms.Length; k++)
                if (_plan.Forms[k] != null && _plan.Forms[k].Brow != null && _plan.Forms[k].Brow.Length >= 3)
                    said.Add(Top(E, _plan.Forms[k], _seed + Rules.SeedHeadTop + step * k));
            _platformOwner = new byte[_plan.Platforms.Length];
            for (int k = 0; k < _plan.Platforms.Length; k++)
                if (_plan.Platforms[k] != null) said.Add(Platform(E, _plan.Platforms[k], k, _seed + Rules.SeedHeadPlatform + step * k));
            for (int k = 0; k < _plan.Bars.Length; k++)
                if (_plan.Bars[k] != null) said.Add(Bar(E, _plan.Bars[k], _seed + Rules.SeedHeadBar + step * k));
            for (int k = 0; k < _plan.Forms.Length; k++)
                if (_plan.Forms[k] != null && _plan.Forms[k].PlinthRadii.x > 0 && _plan.Forms[k].PlinthRadii.y > 0)
                    said.Add(Plinth(E, _plan.Forms[k], _seed + Rules.SeedHeadPlinth + step * k));
            // the key scene's paint: each piece's recipe where it moved the ground, by the height the paint reads
            int painted = 0, cells = 0;
            var pe = Pz(E);
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c; byte o = _owner[i];
                if (o == 0) continue;
                cells++;
                var rec = _ownerRecipe[o];
                if (rec == null) continue;
                zone[i] = RecipeZone(rec, pe[i], _xs[c], _ys[r], _ownerSeed[o]);
                painted++;
            }
            _r.Note("head.cells_moved", cells);
            _r.Note("head.new_ground_m2", cells * _g.Mpp * _g.Mpp);
            Log("key scenes", "the Head (tier 3, max(plan, piece)): " + string.Join("; ", said) + "; " + painted + " cells painted by their recipes");
        }

        string Ramp(double[] E, GroundRampDef rp, long seed, double emin)
        {
            if (rp.Form == null) throw new InvalidOperationException("[TerrainPlan] " + rp.Id + " names no form.");
            var R = new Root(rp.Form);
            byte me = NewOwner(rp.Id, rp.Recipe, seed);
            double inland = Num(rp.Inland), from = Num(rp.From), to = Num(rp.To), seaward = Num(rp.SeawardReach);
            double tailFall = Num(rp.TailFall), tailLen = Num(rp.TailLength), f1 = Num(rp.FlankFall.x), f2 = Num(rp.FlankFall.y);
            double gx = Num(rp.Guard.x), gy = Num(rp.Guard.y), gr = Num(rp.GuardRadius), gf = Num(rp.GuardFade);
            if (!(to > emin)) return rp.Id + ": nothing to raise";
            // beyond the root line's ends the flanks fall: past ex the ramp stands below the lowest ground anywhere
            double ex = f2 > 0 ? (-f1 + Math.Sqrt(f1 * f1 + 4 * f2 * (to - emin))) / (2 * f2) : (to - emin) / f1;
            double u0 = -inland - tailLen, u1 = seaward, w0 = -ex, w1 = R.L + ex;
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (double u in new[] { u0, u1 })
            foreach (double w in new[] { w0, w1 })
            {
                double X = R.Ax + R.Nx * u + R.Tx * w, Y = R.Ay + R.Ny * u + R.Ty * w;
                x0 = Math.Min(x0, X); y0 = Math.Min(y0, Y); x1 = Math.Max(x1, X); y1 = Math.Max(y1, Y);
            }
            int moved = 0; double nearGuard = double.PositiveInfinity;
            if (Win(x0, y0, x1, y1, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r], u = R.U(X, Y);
                    if (!(u <= seaward && u > u0)) continue;
                    double w = R.W(X, Y), exx = Math.Max(0, Math.Max(-w, w - R.L)), uu = Math.Min(u, 0);
                    double zr = uu >= -inland ? from + (to - from) * Smoothstep(-inland, 0, uu) : from - tailFall * (-inland - uu);
                    zr = zr - f1 * exx - f2 * exx * exx;
                    double e = E[i];
                    if (!(zr > e)) continue;
                    double dg = Hypot(X - gx, Y - gy);
                    double z = e + (zr - e) * Smoothstep(gr, gr + gf, dg);
                    int before = moved;
                    Raise(E, i, z, me, ref moved);
                    if (moved > before) nearGuard = Math.Min(nearGuard, dg);
                }
            _r.Note(rp.Id + ".cells_moved", moved);
            _r.Note(rp.Id + ".nearest_to_guard_m", nearGuard);
            return rp.Id + " " + moved + " cells (nearest " + F(nearGuard, "0.00") + " m from its guard, which keeps " + F(gr, "0.00") + ")";
        }

        string Top(double[] E, FormDef f, long seed)
        {
            var R = new Root(f);
            byte me = NewOwner(f.Id + " top", f.Recipe, seed);
            double top = Num(f.Top), inset = Num(f.TopInset);
            int moved = 0; double fill = 0;
            if (Win(R.MinX, R.MinY, R.MaxX, R.MaxY, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r];
                    if (!(R.Sd(X, Y) < 0 && R.U(X, Y) > -inset)) continue;
                    if (top > E[i]) fill += (top - E[i]) * _g.Mpp * _g.Mpp;
                    Raise(E, i, top, me, ref moved);
                }
            _r.Note(f.Id + ".top_cells", moved);
            _r.Note(f.Id + ".top_fill_m3", fill);
            return f.Id + "'s top at " + F(top) + " m: " + moved + " cells, " + F(fill, "0") + " m³ of fill";
        }

        string Platform(double[] E, GroundPlatformDef pl, int k, long seed)
        {
            if (pl.Form == null) throw new InvalidOperationException("[TerrainPlan] " + pl.Id + " names no form.");
            var R = new Root(pl.Form);
            byte me = NewOwner(pl.Id, pl.Recipe, seed);
            _platformOwner[k] = me;
            double foot = Num(pl.Foot), fall = Num(pl.Fall), width = Num(pl.Width), blend = Num(pl.Blend);
            double reach0 = Num(pl.Reach.x), reach1 = Num(pl.Reach.y), rb0 = Num(pl.RootBlend.x), rb1 = Num(pl.RootBlend.y);
            int moved = 0;
            if (Win(R.MinX - reach1, R.MinY - reach1, R.MaxX + reach1, R.MaxY + reach1, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r], u = R.U(X, Y);
                    if (!(u > rb0)) continue;
                    double sd = R.Sd(X, Y);
                    if (!(sd >= 0 && sd < reach1)) continue;
                    double e = E[i], p = foot - fall * sd / width;
                    if (sd > width) p = p + (e - p) * Smoothstep(width, width + blend, sd);
                    double t = Smoothstep(rb0, rb1, u) * (1 - Smoothstep(reach0, reach1, sd));
                    Raise(E, i, e + (Math.Max(e, p) - e) * t, me, ref moved);
                }
            _r.Note(pl.Id + ".cells_moved", moved);
            return pl.Id + " " + moved + " cells";
        }

        /// <summary>A bar's crest line: its points, their running length.</summary>
        sealed class Crest
        {
            public readonly PlanPoint[] P;
            public readonly double[] Cum;
            public readonly double Length;

            public Crest(PlanPoint[] p)
            {
                P = p; Cum = new double[p.Length];
                for (int k = 1; k < p.Length; k++) Cum[k] = Cum[k - 1] + Hypot(p[k].X - p[k - 1].X, p[k].Y - p[k - 1].Y);
                Length = Cum[p.Length - 1];
            }

            /// <summary>The key scene's barAt: s along the crest (extended past both ends), t across it (+ to the lee).</summary>
            public void At(double x, double y, out double s, out double t)
            {
                double best = 1e9; s = 0; t = 0;
                for (int k = 0; k + 1 < P.Length; k++)
                {
                    double ax = P[k].X, ay = P[k].Y, vx = P[k + 1].X - ax, vy = P[k + 1].Y - ay, l2 = vx * vx + vy * vy, ln = Math.Sqrt(l2);
                    double tt = ((x - ax) * vx + (y - ay) * vy) / l2, tc = tt < 0 ? 0 : (tt > 1 ? 1 : tt);
                    double d = Hypot(x - ax - vx * tc, y - ay - vy * tc);
                    if (!(d < best)) continue;
                    best = d;
                    s = Cum[k] + tc * ln + (k == 0 && tt < 0 ? tt * ln : 0) + (k == P.Length - 2 && tt > 1 ? (tt - 1) * ln : 0);
                    t = (x - ax) * vy - (y - ay) * vx >= 0 ? d : -d;
                }
            }
        }

        string Bar(double[] E, TidalBarDef b, long seed)
        {
            var pts = Num(b.Crest);
            if (pts.Length < 2) throw new InvalidOperationException("[TerrainPlan] " + b.Id + " has no crest.");
            var cr = new Crest(pts); double L = cr.Length;
            byte me = NewOwner(b.Id, b.Recipe, seed);
            double top = Num(b.CrestTop), fall = Num(b.CrestFall), expo = Num(b.CrestExponent), wa = Num(b.CrestWave.x), wf = Num(b.CrestWave.y);
            double tip = Num(b.TipSlope), hw0 = Num(b.HalfWidth.x), hw1 = Num(b.HalfWidth.y), flank = Num(b.FlankFall), steep = Num(b.SeawardSteepen);
            double wx0 = Num(b.WindowMin.x), wy0 = Num(b.WindowMin.y), wx1 = Num(b.WindowMax.x), wy1 = Num(b.WindowMax.y);
            double before = Num(b.Before), reach = Num(b.Reach);
            int moved = 0;
            if (Win(wx0, wy0, wx1, wy1, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r];
                    if (!(X > wx0 && Y > wy0 && X < wx1 && Y < wy1)) continue;
                    cr.At(X, Y, out double s, out double t);
                    if (!(s > before && Math.Abs(t) < reach)) continue;
                    double k = Clip(s / L, 0, 1);
                    double crest = top - fall * Math.Pow(k, expo) + wa * Math.Sin(s * wf) * k;
                    if (s > L) crest -= (s - L) * tip;
                    double hw = hw0 - (hw0 - hw1) * k, tt = t * (t < 0 ? steep : 1.0) / hw;
                    Raise(E, i, crest - flank * tt * tt, me, ref moved);
                }
            _r.Note(b.Id + ".cells_moved", moved);
            _r.Note(b.Id + ".crest_length_m", L);
            return b.Id + " " + moved + " cells (its crest " + F(L, "0.00") + " m)";
        }

        string Plinth(double[] E, FormDef f, long seed)
        {
            byte me = NewOwner(f.Id + " plinth", f.Recipe, seed);
            double cx = Num(f.Centre.x), cy = Num(f.Centre.y), rot = Radians(Num(f.RotationDeg)), co = Math.Cos(rot), si = Math.Sin(rot);
            double sx = Num(f.PlinthRadii.x), sy = Num(f.PlinthRadii.y), top = Num(f.PlinthTop), fall = Num(f.PlinthFall);
            double k0 = Num(f.PlinthSkirt.x), k1 = Num(f.PlinthSkirt.y), R = Math.Max(sx, sy) * k1;
            int moved = 0;
            if (Win(cx - R, cy - R, cx + R, cy + R, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double dx = _xs[c] - cx, dy = _ys[r] - cy, u = dx * co + dy * si, v = -dx * si + dy * co;
                    double q = Hypot(u / sx, v / sy);
                    if (q < k1) Raise(E, i, top - fall * Smoothstep(k0, k1, q), me, ref moved);
                }
            _r.Note(f.Id + ".plinth_cells", moved);
            return f.Id + "'s plinth at " + F(top) + " m: " + moved + " cells";
        }

        // ---- tier 3: the platform's rock pools (part 2 §7.5's rule, on the plan's seed) -------------------------------------

        void PlatformPools(double[] E, double[] still, byte[] zone)
        {
            int running = 0;
            for (int k = 0; k < _plan.Platforms.Length; k++)
            {
                var pl = _plan.Platforms[k];
                if (pl == null || pl.Form == null) continue;
                byte platform = _platformOwner[k];
                var only = new bool[_n];
                for (int i = 0; i < _n; i++) only[i] = _owner[i] == platform;     // a pool is cut only where the platform set the ground
                var line = Straight(Num(pl.Form.Brow), _lineStep);
                var R = line.R; var S = line.S; double L = S[S.Length - 1];
                Normals(R, out _, out var left);                                    // the brow runs counter-clockwise: the sea is on its right
                int count = (int)RoundHalfEven(Num(pl.PoolsPer100m) * L / 100.0);
                double ra0 = Num(pl.PoolRadius.x), ra1 = Num(pl.PoolRadius.y), d0 = Num(pl.PoolDepth.x), d1 = Num(pl.PoolDepth.y);
                double b0 = Num(pl.PoolBand.x), b1 = Num(pl.PoolBand.y), wob = Num(pl.PoolWobble), lip = Num(_plan.PoolLip);
                byte moss = Zi(pl.PoolZone), me = NewOwner(pl.Id + " rock pools");
                string stem = pl.Id.Substring(pl.Id.IndexOf('.') + 1);
                int wetCells = 0; double wetArea = 0;
                for (int j = 0; j < count; j++)
                {
                    double u = Rules.PlatformPoolEnds + (L - 2 * Rules.PlatformPoolEnds)
                               * (j + Rules.PlatformPoolJitter + Rules.PlatformPoolJitterSpan * Hash01(j, k, _seed + Rules.SeedPlatformPool)) / count;
                    int ii = Math.Min(SearchSorted(S, u), R.Length - 1);
                    double off = b0 + (b1 - b0) * Hash01(j, k, _seed + Rules.SeedPlatformPoolOff);
                    var cc = new PlanPoint(PyRound(R[ii].X - left[ii].X * off, 2), PyRound(R[ii].Y - left[ii].Y * off, 2));
                    double rr = ra0 + (ra1 - ra0) * Hash01(j, k, _seed + Rules.SeedPlatformPoolR);
                    double ra = PyRound(rr * Rules.PlatformPoolLong, 2), rb = PyRound(rr * Rules.PlatformPoolShort, 2);
                    double rot = RoundHalfEven(-Rules.PlatformPoolRot + 2 * Rules.PlatformPoolRot * Hash01(j, k, _seed + Rules.SeedPlatformPoolRot));
                    double depth = PyRound(d0 + (d1 - d0) * Hash01(j, k, _seed + Rules.SeedPlatformPoolDepth), 2);
                    string id = "pool." + stem + "_" + (j + 1).ToString("00", CultureInfo.InvariantCulture);
                    var wet = new List<int>();
                    var rec = PoolCut(E, still, id, pl.Id + " (part 2 §7.5's rule, the plan's seed)", cc, ra, rb, rot, depth,
                                      _seed + Rules.SeedPlatformPoolCut + running + j, wob, lip, only, wet);
                    foreach (int i in wet) { zone[i] = moss; _owner[i] = me; }
                    wetCells += wet.Count; wetArea += rec.AreaM2;
                    _r.Pools.Add(rec);
                }
                running += count;
                _r.Note(pl.Id + ".pools", count);
                _r.Note(pl.Id + ".pool_m2", wetArea);
                Log("key scenes", pl.Id + "'s rock pools (tier 3): " + count + " basins along its " + F(L, "0.0") + " m brow, " + F(wetArea, "0.0") +
                                  " m² of water, painted " + pl.PoolZone);
            }
        }

        // ---- tier 2: Ginny's frozen pieces, at part 1's heights and paint ---------------------------------------------------

        /// <summary>A tier-2 piece's weight at a cell: 1 inside, feathered to 0 (the plan's building rule for a point).</summary>
        delegate double PieceWeight(int r, int c, ref int hint);

        struct Tier2Piece
        {
            public string Id;
            public double X0, Y0, X1, Y1, CentreX, CentreY;
            public PieceWeight Weight;
        }

        List<Tier2Piece> Tier2Pieces()
        {
            var o = new List<Tier2Piece>();
            foreach (var p in _plan.FrozenPieces)
            {
                double x = Num(p.At.x), y = Num(p.At.y), rad = Num(p.Radius), fe = Num(p.Feather), R = rad + fe;
                o.Add(new Tier2Piece
                {
                    Id = p.Id, X0 = x - R, Y0 = y - R, X1 = x + R, Y1 = y + R, CentreX = x, CentreY = y,
                    Weight = (int r, int c, ref int h) => Wv(Hypot(_xs[c] - x, _ys[r] - y) - rad, fe),
                });
            }
            foreach (var b in _plan.FrozenBoxes)
            {
                double x0 = Num(b.Min.x), y0 = Num(b.Min.y), x1 = Num(b.Max.x), y1 = Num(b.Max.y), fe = Num(b.Feather);
                o.Add(new Tier2Piece
                {
                    Id = b.Id, X0 = x0 - fe, Y0 = y0 - fe, X1 = x1 + fe, Y1 = y1 + fe, CentreX = (x0 + x1) / 2, CentreY = (y0 + y1) / 2,
                    Weight = (int r, int c, ref int h) =>
                    {
                        double X = _xs[c], Y = _ys[r];
                        return Wv(Hypot(Math.Max(Math.Max(x0 - X, X - x1), 0), Math.Max(Math.Max(y0 - Y, Y - y1), 0)), fe);
                    },
                });
            }
            double margin = Num(_plan.FrozenPathMargin), pfe = Num(_plan.FrozenPathFeather);
            for (int n = 0; n < _plan.Paths.Length; n++)
            {
                var p = _plan.Paths[n];
                if (p == null || !p.Frozen) continue;
                var g = _pathLines[n];
                double hold = Num(p.Width) / 2 + margin, R = hold + pfe;
                var mid = g.R[SearchSorted(g.S, g.Length / 2)];
                o.Add(new Tier2Piece
                {
                    Id = p.Id, X0 = g.MinX - R, Y0 = g.MinY - R, X1 = g.MaxX + R, Y1 = g.MaxY + R, CentreX = mid.X, CentreY = mid.Y,
                    Weight = (int r, int c, ref int h) =>
                    {
                        g.Query(_xs[c], _ys[r], ref h, out double d, out _, out _);
                        return Wv(d - hold, pfe);
                    },
                });
            }
            return o;
        }

        void Tier2(double[] E, byte[] zone)
        {
            var E1 = _r.E1; var Z1 = _r.Zone1;
            double frozenAt = Num(_keep.FrozenAt), keepAbove = Num(_keep.KeepPaintAbove);
            _f2 = new double[_n];
            var pieces = Tier2Pieces();
            foreach (var p in pieces)
            {
                if (!Win(p.X0, p.Y0, p.X1, p.Y1, out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = -1;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double w = p.Weight(r, c, ref hint);
                    if (w > _f2[i]) _f2[i] = w;
                }
            }
            var before = (double[])E.Clone();
            int painted = 0;
            for (int i = 0; i < _n; i++)
            {
                double f = _f2[i];
                if (!(f > 0)) continue;
                E[i] = E[i] + f * (E1[i] - E[i]);
                if (f > keepAbove) { zone[i] = Z1[i]; painted++; }
            }
            int held = 0, core = 0;
            foreach (var p in pieces)
            {
                var rec = new TerrainPlanPieceRecord { Id = p.Id };
                if (Win(p.X0, p.Y0, p.X1, p.Y1, out int r0, out int r1, out int c0, out int c1))
                {
                    int hint = -1;
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        int i = r * _w + c;
                        double w = p.Weight(r, c, ref hint);
                        if (!(w > 0)) continue;
                        rec.MaxMove = Math.Max(rec.MaxMove, Math.Abs(E[i] - before[i]));
                        if (w < frozenAt) continue;
                        rec.Cells++;
                        if (_held1[i]) rec.AlreadyHeld++;
                    }
                }
                int cr = _g.RowOf(p.CentreY), ccol = _g.ColOf(p.CentreX);
                rec.CentreHeld = cr >= 0 && cr < _h && ccol >= 0 && ccol < _w && _held1[cr * _w + ccol];
                _r.Pieces.Add(rec);
                core += rec.Cells; held += rec.AlreadyHeld;
            }
            Log("key scenes", "tier 2 (the key scenes' frozen pieces, at part 1's plan heights and paint): " + pieces.Count + " pieces, " + core +
                              " core cells, " + held + " of them already in pass 9's frozen mask; " + painted + " cells take part 1's paint");
        }

        // ---- the key scenes' lots and paths, after the plan's paths -----------------------------------------------------------

        /// <summary>Paint a cell in a material at a weight: 1 takes the zone; a light path (0 &lt; weight &lt; 1) lies over it.</summary>
        void PaintAt(byte[] zone, int i, string material, double weight, string who)
        {
            byte m = Zi(material);
            if (weight >= 1) zone[i] = m;
            else if (m == ZPath) _pathWeight[i] = Math.Max(_pathWeight[i], weight);
            else throw new InvalidOperationException("[TerrainPlan] " + who + " paints " + material + " at " + weight + ": only the path slot takes a light weight.");
            _keyPaint[i] = true;
        }

        void Lots(byte[] zone)
        {
            int cells = 0;
            foreach (var p in _plan.Patches)
            {
                if (p == null) continue;
                double w = Num(p.Weight), x0 = Num(p.Min.x), y0 = Num(p.Min.y), x1 = Num(p.Max.x), y1 = Num(p.Max.y);
                if (!(w > 0) || !Win(x0, y0, x1, y1, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    double X = _xs[c], Y = _ys[r];
                    if (!(X >= x0 && X <= x1 && Y >= y0 && Y <= y1)) continue;
                    PaintAt(zone, r * _w + c, p.Material, w, p.Id);
                    cells++;
                }
            }
            Log("key scenes", "the lots: " + _plan.Patches.Length + " lots, " + cells + " cells");
        }

        void KeyPaths(byte[] zone, double[] still)
        {
            PathFields(out var kd, out var kh, out var kw, part1: false);
            var weight = new double[_plan.Paths.Length];
            for (int n = 0; n < weight.Length; n++) weight[n] = _plan.Paths[n] != null ? Num(_plan.Paths[n].Weight) : 0;
            int cells = 0, light = 0, wet = 0;
            for (int i = 0; i < _n; i++)
            {
                int n = kw[i];
                if (n < 0 || !(kd[i] < kh[i]) || !(weight[n] > 0)) continue;
                if (_chan[i] || !double.IsNaN(still[i])) { wet++; continue; }         // a key scene's path is never painted in water
                var p = _plan.Paths[n];
                PaintAt(zone, i, p.Material, weight[n], p.Id);
                if (weight[n] < 1) light++; else cells++;
            }
            int count = 0;
            foreach (var p in _plan.Paths) if (p != null && p.PaintOnly && p.Kind != PathKind.TidalCrossing) count++;
            Log("key scenes", "the key scenes' paths: " + count + " paths, " + cells + " cells painted, " + light + " cells of worn grass (path at a light weight), " +
                              wet + " cells left to the water");
        }

        // ---- tier 1: pass 9's frozen mask, today's paint where the keep holds it, the roads -------------------------------------

        void Tier1(double[] E, byte[] zone, double[] still)
        {
            double frozenAt = Num(_keep.FrozenAt), keepAbove = Num(_keep.KeepPaintAbove), west = Num(_plan.Crossing.XRange.x);
            var isRoad = new bool[_plan.Paths.Length];
            for (int n = 0; n < isRoad.Length; n++)
            {
                var p = _plan.Paths[n];
                isRoad[n] = p != null && !p.PaintOnly && p.Kind != PathKind.TidalCrossing && !string.IsNullOrEmpty(p.LineSource);
            }
            int held = 0, keyScenes = 0, overridden = 0, lightCleared = 0;
            double maxOff = 0;
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                bool westStrip = _xs[c] < west;
                if (_held1[i])
                {
                    held++;
                    if (_owner[i] != 0 || _f2[i] > 0) keyScenes++;
                    E[i] = _peA[i] >= frozenAt || westStrip ? _src.Base[i] : _r.E1[i];   // the crossing's own keep is part 1's to the bit
                    maxOff = Math.Max(maxOff, Math.Abs(E[i] - _src.Base[i]));
                    still[i] = _still1[i];                                                 // only the water part 1 had there
                }
                if (_pp1[i] > keepAbove || westStrip)
                {
                    byte t = TodayPaint(i, r, c);
                    if (_keyPaint[i] && zone[i] != t) overridden++;
                    if (_pathWeight[i] > 0) { _pathWeight[i] = 0; lightCleared++; }
                    zone[i] = t;
                }
                if (_pd[i] < _ph[i] && _pw[i] >= 0 && isRoad[_pw[i]]) zone[i] = ZPath;
            }
            _r.Note("tier1.cells", held);
            _r.Note("tier1.max_off_today_m", maxOff);
            _r.Note("tier1.key_scene_cells", keyScenes);
            _r.Note("tier1.key_paint_overridden", overridden + lightCleared);
            Log("key scenes", "tier 1 (pass 9's frozen mask at today's heights; the keep's paint; the roads): " + held + " cells held, at most " +
                              F(maxOff, "0.0000") + " m off today's ground; " + keyScenes + " of them under a key scene; the keep repaints " +
                              (overridden + lightCleared) + " cells of the key scenes' paths and lots");
        }

        // ---- the village's routes: V1's lanes and walks, over the keep as the roads are -------------------------------------

        /// <summary>
        /// The village plan's routes (amendment 2 §4.6), in one pass: each cell within half a route's width of its straight
        /// line, in ground metres as the village measures them (a unit north being 1 / GroundDepthScale m), its edge
        /// wandering by the plan's PathEdge, takes its class's slot. They are painted after the keep, as the roads are, so a
        /// walk reaches its door, and after the map's still water, which they never cross.
        /// </summary>
        void Routes(byte[] zone, double[] still)
        {
            if (_plan.Routes == null || _plan.Routes.Length == 0) return;
            double edgeAmp = Num(_plan.PathEdge.x), edgeLam = Num(_plan.PathEdge.y), keepAbove = Num(_keep.KeepPaintAbove);
            double sy = IsoGround.GroundDepthScale;
            var state = new byte[_n];                                               // 1 painted, 2 left to the water
            int cells = 0, overKeep = 0, wasSlot = 0, wet = 0;
            for (int n = 0; n < _plan.Routes.Length; n++)
            {
                var route = _plan.Routes[n];
                if (!TerrainPlanValidation.RouteClasses.TryGetValue(route.Class ?? "", out var label) || !TerrainPlanValidation.TryLabel(label, out var slot))
                    throw new InvalidOperationException("[TerrainPlan] " + route.Id + "'s class '" + route.Class + "' is not in the route table.");
                byte z = Zi(slot.Material);
                var pts = Num(route.Points);
                var ground = new PlanPoint[pts.Length];
                for (int k = 0; k < pts.Length; k++) ground[k] = new PlanPoint(pts[k].X, pts[k].Y / sy);
                var line = Exact(ground);
                double half = Num(route.WidthMetres) / 2, reach = half + edgeAmp;
                if (!Win(line.MinX - reach, (line.MinY - reach) * sy, line.MaxX + reach, (line.MaxY + reach) * sy,
                        out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = -1, own = 0;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    line.Query(_xs[c], _ys[r] / sy, ref hint, out double d, out _, out _);
                    if (!(d < half + edgeAmp * SNoise(_xs[c], _ys[r], _seed + Rules.SeedRoute + n, edgeLam))) continue;
                    if (_chan[i] || !double.IsNaN(still[i])) { if (state[i] == 0) { state[i] = 2; wet++; } continue; }
                    own++;
                    if (state[i] == 1) continue;
                    state[i] = 1; cells++;
                    if (_pp1[i] > keepAbove) overKeep++;
                    if (zone[i] == z) wasSlot++;
                    zone[i] = z;
                }
                _r.Note(route.Id + ".cells", own);
            }
            _r.Note("routes.cells", cells);
            _r.Note("routes.over_keep", overKeep);
            _r.Note("routes.wet", wet);
            Log("key scenes", "the village's routes: " + _plan.Routes.Length + " routes, " + cells + " cells, " + overKeep +
                              " of them over the keep's paint and " + wasSlot + " already their slot; " + wet + " cells left to the water");
        }

        void Finish()
        {
            if (_P != null)
            {
                // the ground file's import is the map's ground; the plan's own becomes the check, and its water stands on the import
                _r.EDerived = _E;
                _r.GroundCodes = _src.Import?.Codes;
                _E = (double[])_P.Clone();
                StillOnGround();
            }
            int dropped = 0;
            for (int i = 0; i < _n; i++)
            {
                double s = _still[i];
                if (double.IsNaN(s)) continue;
                if (double.IsInfinity(s) || !(_E[i] < s)) { _still[i] = double.NaN; dropped++; }
            }
            Routes(_zone, _still);
            _biome = BiomeOf(_E, _secidx, _secw);
            double frozenAt = Num(_keep.FrozenAt);
            var frozen = new byte[_n];
            int t1 = 0, t2 = 0;
            for (int i = 0; i < _n; i++)
            {
                if (_held1[i]) { frozen[i] = 1; t1++; }
                else if (_f2[i] >= frozenAt) { frozen[i] = 2; t2++; }
            }
            _r.E = _E; _r.Zone = _zone; _r.Still = _still; _r.Biome = _biome;
            _r.Chan = _chan; _r.Tidal = _tidal; _r.Bank = _bank;
            _r.PathD = _pd; _r.PathH = _ph; _r.PathW = ToInt(_pw);
            _r.PathWeight = _pathWeight; _r.KeyPaint = _keyPaint; _r.KeyOwner = _owner;
            _r.Frozen = frozen;
            double west = Num(_plan.Crossing.XRange.x);
            var why = new byte[_n];
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c, b = 0;
                if (_pe2[i] >= frozenAt) b |= TerrainPlanResult.WhyCrossing;
                if (_peA[i] >= frozenAt) b |= TerrainPlanResult.WhyPart1;
                if (_xs[c] < west) b |= TerrainPlanResult.WhyWest;
                if (_f2[i] >= frozenAt) b |= TerrainPlanResult.WhyTier2;
                if (_peCommon[i] >= frozenAt) b |= TerrainPlanResult.WhyCommon;
                if (_peSouth[i] >= frozenAt) b |= TerrainPlanResult.WhySouth;
                if (_peCliff[i] >= frozenAt) b |= TerrainPlanResult.WhyCliffs;
                if (_peCliffOut[i] >= frozenAt) b |= TerrainPlanResult.WhyCliffsOut;
                why[i] = (byte)b;
            }
            _r.FrozenWhy = why;
            _r.Note("frozen.tier1_cells", t1);
            _r.Note("frozen.tier2_cells", t2);
            Log("finish", "still water where it stands above the ground (" + dropped + " cells dropped); biomes on the final ground; " + t1 +
                          " tier-1 and " + t2 + " tier-2 cells in the frozen map");
        }
    }
}
