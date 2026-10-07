using System;
using System.Collections.Generic;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Part 1 (terrain pass 9's plan9_lib.derive): the protected set, the coast's sections, the water, the paths and the
    /// paint, over today's ground. Ported step for step; each step's comment names the prototype's line it follows.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        // ---- part 1's maps ---------------------------------------------------------------------------------------------
        double[] _peCommon, _ppCommon, _pe1, _pp1, _peA;
        double[] _peSouth, _peCliff, _peCliffOut;                             // the keep's parts: the frozen map's reasons
        double[] _treesD, _cliffD;
        double[] _E, _secw, _sd;
        short[] _secidx;
        double[] _still, _bank;
        bool[] _chan, _tidal;
        byte[] _zone, _biome;
        double[] _pd, _ph;
        short[] _pw;

        // ---- the lines -------------------------------------------------------------------------------------------------
        PlanLine _shore;
        double[] _secU;
        double _uEnd;
        PlanLine[] _pathLines, _streamLines, _creekLines;

        // ---- what the fall needs from part 1: the run's cut (m, + = lowered) and the still cells of the pool it drains -
        int _fallStream = -1, _fallPond = -1;
        double[] _runCut;
        bool[] _pondWet;

        // ---- the plan's numbers, read once --------------------------------------------------------------------------------
        double _spring, _lineStep, _pathDipM, _plateau, _woodsFloor;

        /// <summary>
        /// The cliffs the barren rule measures from: today's walls (the frozen file's, the ones still standing), and with
        /// part 2's south its walls as they stand, each at its first brow, where the scene parks it (PR 5w). The barren is
        /// cliff-top heath, so it follows the walls a patch moves or adds. The paint keep reads today's walls alone: a
        /// wall the plan moves or adds holds no paint of today's.
        /// </summary>
        List<PlanPoint> BarrenCliffs()
        {
            var o = new List<PlanPoint>(_src.ItemsOf(_keep.CliffRoot));
            foreach (var w in SouthWalls()) o.Add(new PlanPoint(w.Brow[0].x, w.Brow[0].y));
            return o;
        }

        void Part1()
        {
            _spring = Num(_plan.SpringM); _lineStep = Num(_plan.LineStep); _pathDipM = Num(_plan.PathDip);
            _plateau = Num(_plan.PlateauM); _woodsFloor = Num(_plan.WoodsFloorM);
            Lines();
            Protection();
            _treesD = DistRaster(_src.ItemsOf(_keep.WoodsRoot), Rules.DistanceCap);
            _cliffD = DistRaster(BarrenCliffs(), Rules.DistanceCap);

            var b = _src.Base;
            var ke = new double[_n];
            for (int i = 0; i < _n; i++) ke[i] = 1 - Clip(_pe1[i], 0, 1);
            Coast();                                                           // E, secw, secidx, sd = coast(base)
            for (int i = 0; i < _n; i++) _E[i] = b[i] + ke[i] * (_E[i] - b[i]);   // the coast never moves what is protected
            _biome = BiomeOf(Pz(_E), _secidx, _secw);
            Water(_E);
            for (int i = 0; i < _n; i++) _E[i] = b[i] + ke[i] * (_E[i] - b[i]);   // nor does the water
            for (int i = 0; i < _n; i++)
                if (!(_pe1[i] < 0.5 && _E[i] < _still[i])) _still[i] = double.NaN;
            PathFields(out _pd, out _ph, out _pw, part1: true);
            Paint1(ke);
            Log("part 1", "pass 9's plan: " + _plan.Sections.Length + " sections, " + _plan.Ponds.Length + " ponds, " + _plan.Streams.Length +
                          " streams, " + _plan.Creeks.Length + " creeks, " + _plan.Pans.Length + " pans, " + Part1PathCount() + " paths");

            _r.E1 = (double[])_E.Clone();
            _r.Zone1 = (byte[])_zone.Clone();
            _r.SectionOf = _secidx; _r.SectionWeight = _secw;
            _r.Pe1 = _pe1; _r.Pp1 = _pp1;
        }

        void FinishPart1()
        {
            _r.E = _E; _r.Zone = _zone; _r.Still = _still; _r.Biome = _biome;
            _r.Chan = _chan; _r.Tidal = _tidal; _r.Bank = _bank;
            _r.PathD = _pd; _r.PathH = _ph; _r.PathW = ToInt(_pw);
            _r.PathWeight = new double[_n];
            _r.Frozen = new byte[_n];
            _r.KeyPaint = new bool[_n];
        }

        static int[] ToInt(short[] a)
        {
            var o = new int[a.Length];
            for (int i = 0; i < a.Length; i++) o[i] = a[i];
            return o;
        }

        int Part1PathCount()
        {
            int k = 0;
            foreach (var p in _plan.Paths) if (!p.PaintOnly) k++;
            return k;
        }

        // ---- the lines: the shore reference line (extended), the channels and the paths --------------------------------

        /// <summary>A resampled line with straight run-outs, 1 m apart, past both ends (pass 9's _extend; part 2's line too).</summary>
        static PlanLine RunOut(PlanLine line0, int ext)
        {
            var R0 = line0.R; int m = R0.Length;
            double ax = R0[0].X - R0[3].X, ay = R0[0].Y - R0[3].Y, an = Math.Sqrt(ax * ax + ay * ay);
            double bx = R0[m - 1].X - R0[m - 4].X, by = R0[m - 1].Y - R0[m - 4].Y, bn = Math.Sqrt(bx * bx + by * by);
            ax /= an; ay /= an; bx /= bn; by /= bn;
            var R = new PlanPoint[m + 2 * ext];
            for (int j = 0; j < ext; j++)
            {
                double k0 = ext - j, k1 = j + 1;                                 // np.arange(n, 0, -1); k[::-1]
                R[j] = new PlanPoint(R0[0].X + ax * k0, R0[0].Y + ay * k0);
                R[ext + m + j] = new PlanPoint(R0[m - 1].X + bx * k1, R0[m - 1].Y + by * k1);
            }
            Array.Copy(R0, 0, R, ext, m);
            var S = new double[R.Length];
            for (int j = 1; j < R.Length; j++) S[j] = S[j - 1] + Hypot(R[j].X - R[j - 1].X, R[j].Y - R[j - 1].Y);
            var ks = new double[line0.Knots.Length];
            for (int j = 0; j < ks.Length; j++) ks[j] = line0.Knots[j] + ext;
            return new PlanLine(R, S, ks);
        }

        void Lines()
        {
            var sl = _plan.ShoreLine;
            if (sl == null || sl.Length < 4) throw new InvalidOperationException("[TerrainPlan] the shore line needs four points.");
            var pts = new PlanPoint[sl.Length];
            var starts = new List<int>();
            for (int i = 0; i < sl.Length; i++)
            {
                pts[i] = Num(sl[i].At);
                if (!string.IsNullOrEmpty(sl[i].SectionId)) starts.Add(i);
            }
            var secs = _plan.Sections;
            if (starts.Count != secs.Length)
                throw new InvalidOperationException("[TerrainPlan] the shore line starts " + starts.Count + " sections; the plan lists " + secs.Length + ".");
            for (int k = 0; k < secs.Length; k++)
                if (secs[k] == null || sl[starts[k]].SectionId != secs[k].Id)
                    throw new InvalidOperationException("[TerrainPlan] section " + k + " is not the one the shore line starts there (" + sl[starts[k]].SectionId + ").");

            // pass 9: _R0 = catmull(SHORE, 1.0); SHORE_R = _extend(_R0): straight run-outs, 1 m apart, past both ends
            _shore = RunOut(Catmull(pts, Num(_plan.ShoreStep)), (int)Num(_plan.ShoreRunOut));

            // SEC_U: each section's start as an along-shore bearing; U_END: the line's last point
            _secU = new double[starts.Count];
            for (int k = 0; k < starts.Count; k++) _secU[k] = Along(pts[starts[k]].X, pts[starts[k]].Y);
            _uEnd = Along(pts[pts.Length - 1].X, pts[pts.Length - 1].Y);

            _pathLines = new PlanLine[_plan.Paths.Length];
            for (int n = 0; n < _pathLines.Length; n++) _pathLines[n] = PathLine(_plan.Paths[n]);
            _streamLines = new PlanLine[_plan.Streams.Length];
            for (int n = 0; n < _streamLines.Length; n++) _streamLines[n] = Catmull(Num(_plan.Streams[n].Points), _lineStep);
            _creekLines = new PlanLine[_plan.Creeks.Length];
            for (int n = 0; n < _creekLines.Length; n++) _creekLines[n] = Catmull(Num(_plan.Creeks[n].Points), _lineStep);

            // the fall's stream and the pond it drains: part 1 keeps their carve and water for the fall (§4.1 tier 3)
            foreach (var f in _plan.Falls)
            {
                if (f == null || f.Stream == null) continue;
                for (int n = 0; n < _plan.Streams.Length; n++) if (ReferenceEquals(_plan.Streams[n], f.Stream)) _fallStream = n;
                if (f.Stream.Source != null)
                    for (int n = 0; n < _plan.Ponds.Length; n++) if (ReferenceEquals(_plan.Ponds[n], f.Stream.Source)) _fallPond = n;
            }
        }

        /// <summary>The along-shore coordinate: the bearing about the island centre, turned so the north coast runs west to east.</summary>
        double Along(double x, double y) => Mod(_src.Bearing(x, y) - 180.0, 360.0);

        // ---- the protected set (pass 9's protection): max-accumulated, so the order of the shapes does not matter ------

        void Protection()
        {
            var k = _keep;
            _peCommon = new double[_n]; _ppCommon = new double[_n];
            var peBar = new double[_n]; var peSouth = new double[_n]; var peCliff = new double[_n]; var ppCliff = new double[_n];
            double pfe = Num(k.PaintFeather);
            var large = new HashSet<string>(k.LargeBuildings ?? new string[0], StringComparer.Ordinal);
            foreach (var b in _src.Buildings)                                       // buildings: 8 m, the cannery 12 m
            {
                double r = large.Contains(b.Id) ? Num(k.LargeBuildingRadius) : Num(k.BuildingRadius);
                StampPoint(_peCommon, b.At, r, Num(k.BuildingFeather));
                StampPoint(_ppCommon, b.At, r - Num(k.BuildingPaintInset), pfe);
            }
            foreach (var hull in YardHulls())                                       // yards: hull + 3 m
            {
                StampPoly(_peCommon, hull, Num(k.YardBuffer), Num(k.YardFeather));
                StampPoly(_ppCommon, hull, Num(k.YardPaintBuffer), pfe);
            }
            foreach (var root in k.BuiltRoots ?? new string[0])
                foreach (var p in _src.ItemsOf(root))
                {
                    StampPoint(_peCommon, p, Num(k.BuiltBuffer), Num(k.BuiltFeather));
                    StampPoint(_ppCommon, p, Num(k.BuiltPaintBuffer), pfe);
                }
            foreach (var path in _plan.Paths)                                       // the roads: ground holds 3 m either side
            {
                if (string.IsNullOrEmpty(path.LineSource)) continue;
                var pts = PathControlPoints(path);
                for (int j = 0; j + 1 < pts.Length; j++) StampCapsule(_peCommon, pts[j], pts[j + 1], Num(k.RoadBuffer), Num(k.RoadFeather));
            }
            StampRect(_peCommon, _src.WharfMin.X, _src.WharfMin.Y, _src.WharfMax.X, _src.WharfMax.Y, Num(k.WharfBuffer), Num(k.WharfFeather));
            StampRect(_ppCommon, _src.WharfMin.X, _src.WharfMin.Y, _src.WharfMax.X, _src.WharfMax.Y, Num(k.WharfPaintBuffer), pfe);
            foreach (var p in _src.ItemsOf(k.WharfRoot)) StampPoint(_peCommon, p, Num(k.WharfItemBuffer), Num(k.WharfItemFeather));
            foreach (var cap in new[] { _src.BerthSlip, _src.ApproachCut, _src.Pocket })
                StampCapsule(_peCommon, cap.A, cap.B, cap.HalfWidth + Num(k.BerthExtra), Num(k.BerthFeather));
            foreach (var p in _src.DockPoints) StampPoint(_peCommon, p, Num(k.DockPointBuffer), Num(k.DockPointFeather));
            for (int j = 0; j + 1 < _src.Entrance.Length; j++)
                StampCapsule(_peCommon, _src.Entrance[j], _src.Entrance[j + 1], _src.EntranceHalfWidth + Num(k.EntranceExtra), Num(k.EntranceFeather));
            StampCapsule(peBar, _src.Sandbar.A, _src.Sandbar.B, Num(k.BarBuffer), Num(k.BarFeather));
            StampCapsule(_peCommon, _src.BarGut.A, _src.BarGut.B, _src.BarGut.HalfWidth + Num(k.GutExtra), Num(k.GutFeather));
            foreach (var p in _src.Passages) StampPoint(_peCommon, p, Num(k.PassageBuffer), Num(k.PassageFeather));
            double eb = Num(k.EdgeBuffer), ef = Num(k.EdgeFeather);
            double xr = _g.X0 + _w * _g.Mpp, yb = _g.Y1 - _h * _g.Mpp;
            double s0 = Num(k.SouthSector.x), s1 = Num(k.SouthSector.y), sf = Num(k.SouthFeatherDeg);
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r];
                double edge = Math.Min(Math.Min(Math.Min(X - _g.X0, xr - X), _g.Y1 - Y), Y - yb);
                double v = 1 - Ss((edge - eb) / ef);
                if (v > _peCommon[i]) _peCommon[i] = v;
                double bb = _src.Bearing(X, Y);                                    // the south half: every cliff (PR 5w re-derives it)
                double o = bb >= s0 && bb <= s1 ? 0.0 : Math.Min(Math.Abs(bb - s0), Math.Abs(bb - s1));
                peSouth[i] = 1 - Ss(o / sf);
            }
            foreach (var p in _src.ItemsOf(k.CliffRoot))
            {
                StampPoint(peCliff, p, Num(k.CliffBuffer), Num(k.CliffFeather));
                StampPoint(ppCliff, p, Num(k.CliffPaintBuffer), pfe);
            }
            _peCliffOut = new double[_n];                                       // the cliffs outside the south sector (a ground file owns the south)
            foreach (var p in _src.ItemsOf(k.CliffRoot))
            {
                double bb = _src.Bearing(p.X, p.Y);
                if (!(bb >= s0 && bb <= s1)) StampPoint(_peCliffOut, p, Num(k.CliffBuffer), Num(k.CliffFeather));
            }
            _peSouth = peSouth; _peCliff = peCliff;
            foreach (var p in _src.ItemsOf(k.WoodsRoot)) StampPoint(_peCommon, p, Num(k.WoodsBuffer), Num(k.WoodsFeather));
            if (_src.StampFleet)
                StampRect(_peCommon, _src.FleetMin.X, _src.FleetMin.Y, _src.FleetMax.X, _src.FleetMax.Y, Num(k.FleetBuffer), Num(k.FleetFeather));

            _pe1 = new double[_n]; _pp1 = new double[_n]; _peA = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                double a = Math.Max(Math.Max(_peCommon[i], peSouth[i]), peCliff[i]);
                _peA[i] = a;
                _pe1[i] = Math.Max(a, peBar[i]);
                _pp1[i] = Math.Max(_ppCommon[i], ppCliff[i]);
            }
        }

        /// <summary>The yards' hulls: clusters of the yard's items (single link), four points or more (pass 9).</summary>
        List<List<PlanPoint>> YardHulls()
        {
            var o = new List<List<PlanPoint>>();
            foreach (var c in Clusters(_src.ItemsOf(_keep.YardsRoot), Num(_keep.YardLink)))
                if (c.Count >= _keep.YardMinPoints) o.Add(Hull(c));
            return o;
        }

        // ---- the coast (pass 9's coast): each section's cross-shore profile, feathered along the shore ------------------

        sealed class Section
        {
            public int Type;
            public bool Cut;
            public double EdgeAmp, EdgeLam, SeaEnd, LandEnd, SeaTail, RockEdge;
            public bool HasRockEdge;
            public double[] Kx, Ky;
            public RecipeTable Recipe;
        }

        const int TSand = 0, TLedge = 1, TShingle = 2, TMud = 3, TMarsh = 4, TFlats = 5, TBar = 6;

        static int TypeCode(string t)
        {
            switch (t)
            {
                case "sand_beach": return TSand;
                case "ledge_platform": return TLedge;
                case "shingle_cobble": return TShingle;
                case "mud_flat": return TMud;
                case "salt_marsh": return TMarsh;
                case "sandy_flats": return TFlats;
                case "the_bar": return TBar;
                default: throw new InvalidOperationException("[TerrainPlan] no section type is called '" + t + "'.");
            }
        }

        Section[] _secTab;

        Section Tab(CoastSectionDef s)
        {
            var t = new Section { Type = TypeCode(s.Type), Cut = s.Mode == CoastSectionMode.Cut, Recipe = new RecipeTable(s.Recipe) };
            t.EdgeAmp = Num(s.Edge.x); t.EdgeLam = Num(s.Edge.y); t.SeaTail = Num(s.SeaTail);
            t.HasRockEdge = s.HasRockEdge; t.RockEdge = Num(s.RockEdge);
            var kn = new List<PlanPoint>();                                          // sorted(sec['knots']): by distance, then height
            foreach (var v in s.Knots ?? new UnityEngine.Vector2[0]) kn.Add(Num(v));
            kn.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            t.Kx = new double[kn.Count]; t.Ky = new double[kn.Count];
            for (int j = 0; j < kn.Count; j++) { t.Kx[j] = kn[j].X; t.Ky[j] = kn[j].Y; }
            if (kn.Count > 0) { t.SeaEnd = t.Kx[0]; t.LandEnd = t.Kx[kn.Count - 1]; }
            return t;
        }

        void Coast()
        {
            var secs = _plan.Sections; int K = secs.Length;
            _secTab = new Section[K];
            for (int k = 0; k < K; k++) _secTab[k] = Tab(secs[k]);
            _E = (double[])_src.Base.Clone();
            _secw = new double[_n]; _secidx = new short[_n]; _sd = Filled(double.PositiveInfinity);
            for (int i = 0; i < _n; i++) _secidx[i] = -1;
            double win = Num(_plan.ShoreWindow);
            if (!Win(_shore.MinX - win, _shore.MinY - win, _shore.MaxX + win, _shore.MaxY + win, out int r0, out int r1, out int c0, out int c1)) return;
            double wa = Num(_plan.Warp.x), wl = Num(_plan.Warp.y);
            double feather = Num(_plan.SectionFeather), growth = Num(_plan.FeatherGrowth), growthFrom = Num(_plan.FeatherGrowthFrom);
            double endF = Num(_plan.EndFeather), landFade = Num(_plan.LandFade), landFeather = Num(_plan.LandFadeFeather), cutFade = Num(_plan.CutFade);
            var W = new double[K];
            var b = _src.Base;
            int hint = -1;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r], B = b[i];
                double Xw = X + wa * SNoise(X, Y, _seed + Rules.SeedWarpX, wl), Yw = Y + wa * SNoise(X, Y, _seed + Rules.SeedWarpY, wl);
                _shore.Query(Xw, Yw, ref hint, out double dist, out double sgn, out _);
                double sd0 = sgn * dist;
                double u = Along(Xw, Yw);
                // section_weights(u, sd0)
                double F = feather + growth * Math.Max(0.0, -sd0 - growthFrom);
                for (int n = 0; n < K; n++)
                {
                    double u0 = _secU[n], u1 = n + 1 < K ? _secU[n + 1] : _uEnd;
                    double lo = n > 0 ? u0 - F : -1e9, hi = n + 1 < K ? u1 + F : 1e9;
                    W[n] = Ss((u - lo) / (2 * F)) * Ss((hi - u) / (2 * F));
                }
                double sum = W[0];
                for (int n = 1; n < K; n++) sum += W[n];
                double norm = Math.Max(sum, 1e-9);
                double end = Ss((u - _secU[0] + endF) / endF) * Ss((_uEnd + endF - u) / endF);
                double num = 0, wsum = 0, bestw = 0; int best = -1;
                for (int k = 0; k < K; k++)
                {
                    double wk = W[k] / norm * end;
                    if (!(wk > 1e-4)) continue;
                    var t = _secTab[k];
                    double sd = sd0 + t.EdgeAmp * SNoise(X, Y, _seed + Rules.SeedEdge + Rules.SeedEdgeStep * k, t.EdgeLam);
                    double P = Interp(sd, t.Kx, t.Ky);
                    if (t.Type == TLedge)                       // rock: relief and benches on the platform, a rough bluff
                    {
                        double rough = Rules.LedgeRough * SNoise(X, Y, _seed + Rules.SeedLedge + k, Rules.LedgeRoughLambda)
                                       + Rules.LedgeRough2 * SNoise(X, Y, _seed + Rules.SeedLedge2 + k, Rules.LedgeRough2Lambda);
                        double bench = Math.Floor(Fbm(X, Y, _seed + Rules.SeedBench + k, Rules.LedgeBenchLambda) * Rules.LedgeBenchSteps)
                                       * Rules.LedgeBenchStep - Rules.LedgeBenchDrop;
                        bool on = P > Rules.LedgeOnFrom && P < Rules.LedgeOnTo;
                        P = P + (on ? rough + bench : rough * Rules.LedgeBluff * (P >= Rules.LedgeOnTo ? 1.0 : 0.0));
                    }
                    else if (t.Type == TMarsh)
                        P = P + (P > Rules.MarshReliefAbove ? Rules.MarshRelief * SNoise(X, Y, _seed + Rules.SeedMarsh, Rules.MarshReliefLambda) : 0.0);
                    else if (t.Type == TShingle)
                        P = P + (P > Rules.ShingleReliefAbove ? Rules.ShingleRelief * SNoise(X, Y, _seed + Rules.SeedShingle, Rules.ShingleReliefLambda) : 0.0);
                    double e = t.Cut ? P : (sd > 0 ? Math.Max(P, B) : P);
                    double wb = sd < t.SeaEnd ? 1 - Ss((t.SeaEnd - sd) / t.SeaTail) : 1.0;
                    wb = wb * (t.Cut ? (sd > t.LandEnd ? 1 - Ss((sd - t.LandEnd) / cutFade) : 1.0) : (1 - Ss((sd - landFade) / landFeather)));
                    double w = wk * wb;
                    num += w * (e - B); wsum += w;
                    if (w > bestw) { best = k; bestw = w; }
                }
                _E[i] = B + num; _secw[i] = wsum; _secidx[i] = (short)best; _sd[i] = sd0;
            }
        }

        // ---- biomes (pass 9's biome_of) ----------------------------------------------------------------------------------

        int _bShore, _bMarsh, _bBarren, _bMeadow, _bSwale, _bWoods;
        PlanPoint[] _swalePoly, _barrenPoly;
        double _marshLo, _marshHi, _shoreLo, _barrenTop, _barrenCliff;
        int[] _marshSecs;

        void Biomes()
        {
            if (_bWoods != 0) return;
            var bs = _plan.Biomes;
            for (int n = 0; n < bs.Length; n++)
            {
                var b = bs[n];
                switch (b.Kind)
                {
                    case BiomeKind.Coast: _bShore = n + 1; _shoreLo = Num(b.Band.x); break;
                    case BiomeKind.Marsh:
                        _bMarsh = n + 1; _marshLo = Num(b.Band.x); _marshHi = Num(b.Band.y);
                        var ks = new List<int>();
                        foreach (var id in b.SectionIds ?? new string[0])
                            for (int k = 0; k < _plan.Sections.Length; k++) if (_plan.Sections[k].Id == id) ks.Add(k);
                        _marshSecs = ks.ToArray();
                        break;
                    case BiomeKind.Barren: _bBarren = n + 1; _barrenPoly = Num(b.Poly); _barrenTop = Num(b.CliffTopMinM); _barrenCliff = Num(b.CliffDistanceM); break;
                    case BiomeKind.Meadow: _bMeadow = n + 1; break;
                    case BiomeKind.Swale: _bSwale = n + 1; _swalePoly = Num(b.Poly); break;
                }
            }
            _bWoods = bs.Length + 1;
            if (_bShore == 0 || _bMarsh == 0 || _bBarren == 0 || _bMeadow == 0 || _bSwale == 0)
                throw new InvalidOperationException("[TerrainPlan] the plan needs one biome of each kind.");
        }

        byte[] BiomeOf(double[] E, short[] secidx, double[] secw)
        {
            Biomes();
            var o = new byte[_n];
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r], e = E[i];
                bool plateau = e >= _plateau;
                bool sw = plateau && PointInPoly(X, Y, _swalePoly);
                bool ba = (plateau && !sw && PointInPoly(X, Y, _barrenPoly)) || (plateau && e >= _barrenTop && _cliffD[i] < _barrenCliff);
                int b = plateau ? _bMeadow : 0;
                if (ba) b = _bBarren;
                if (sw) b = _bSwale;
                int dom = secw[i] > Rules.SectionDominant ? secidx[i] : -1;
                bool marshy = false;
                foreach (int k in _marshSecs) if (dom == k) marshy = true;
                marshy = marshy && e > _marshLo && e < _marshHi;
                if (!plateau && e > _shoreLo && !marshy) b = _bShore;
                if (marshy) b = _bMarsh;
                if (plateau && _treesD[i] < _woodsFloor) b = _bWoods;
                o[i] = (byte)b;
            }
            return o;
        }

        // ---- water (pass 9's water): ponds, streams, creeks and pans carved into E, in place --------------------------

        static double PondR(double X, double Y, PlanPoint c, double ra, double rb, double cr, double sr, long seed, double wob, double lam) =>
            EllipseRadius(X, Y, c, ra, rb, cr, sr, seed, wob, lam);

        void Water(double[] E)
        {
            _still = Filled(double.NaN); _chan = new bool[_n]; _tidal = new bool[_n]; _bank = Filled(double.PositiveInfinity);
            if (_fallStream >= 0) _runCut = new double[_n];
            if (_fallPond >= 0) _pondWet = new bool[_n];
            var ponds = _plan.Ponds;
            for (int n = 0; n < ponds.Length; n++)
            {
                var p = ponds[n];
                var cc = Num(p.Centre); double ra = Num(p.Radii.x), rb = Num(p.Radii.y), br = Num(p.Basin.x), bd = Num(p.Basin.y);
                double R = Math.Max(ra, rb) * Rules.PondReach + br, surface = Num(p.Surface), bed = Num(p.Bed);
                double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot), rm = (ra + rb) / 2;
                long seed = _seed + Rules.SeedPond + n;
                if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double rr = PondR(_xs[c], _ys[r], cc, ra, rb, cr, sr, seed, Rules.PondWobble, Rules.PondWobbleLambda);
                    double e = E[i];
                    e = e - bd * (1 - Ss(Math.Max(rr - 1, 0) * rm / br));
                    double bowl = rr < 1 ? bed + (surface - bed) * (rr * rr) : surface + (rr - 1) * rm * Rules.PondBankSlope;
                    if (rr < Rules.PondBowlReach) e = Math.Min(e, bowl);
                    E[i] = e;
                    if (rr < Rules.PondStillReach && e < surface)
                    {
                        _still[i] = surface;
                        if (n == _fallPond) _pondWet[i] = true;
                    }
                    _bank[i] = Math.Min(_bank[i], Math.Max(rr - 1, 0) * rm);
                }
            }
            var streams = _plan.Streams;
            for (int n = 0; n < streams.Length; n++)
            {
                var st = streams[n]; var g = _streamLines[n];
                var z = Num(st.BedZ); double depth = Num(st.Depth);
                int pond = -1;
                if (st.Source != null) for (int j = 0; j < ponds.Length; j++) if (ReferenceEquals(ponds[j], st.Source)) pond = j;
                if (pond >= 0) z[0] = Num(ponds[pond].Surface) - depth;   // the pond stands at its outlet's level
                var prof = BedProfile(g, z, E, false, pond >= 0);
                Channel(E, g, prof, Num(st.Widths), Num(st.Bank.x), Num(st.Bank.y), Rules.StreamSmooth, Num(st.Points), Rules.StreamWindow,
                        depth, n == _fallStream);
            }
            var creeks = _plan.Creeks;
            for (int n = 0; n < creeks.Length; n++)
            {
                var cr = creeks[n]; var g = _creekLines[n];
                var prof = BedProfile(g, Num(cr.BedZ), E, true, false);
                Channel(E, g, prof, Num(cr.Widths), Num(cr.Bank), double.NaN, Rules.CreekSmooth, Num(cr.Points), Rules.CreekWindow,
                        double.NaN, false);
            }
            var pans = _plan.Pans;
            double panDepth = Num(_plan.PanDepth);
            for (int n = 0; n < pans.Length; n++)
            {
                var p = pans[n];
                var cc = Num(p.Centre); double ra = Num(p.Radii.x), rb = Num(p.Radii.y), R = Math.Max(ra, rb) * Rules.PanReach;
                double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
                long seed = _seed + Rules.SeedPan + n;
                if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                int ww = c1 - c0;
                var rv = new double[(r1 - r0) * ww];
                double lvl = double.PositiveInfinity;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    double rr = PondR(_xs[c], _ys[r], cc, ra, rb, cr, sr, seed, Rules.PondWobble, Rules.PondWobbleLambda);
                    rv[(r - r0) * ww + (c - c0)] = rr;
                    if (rr >= Rules.PanRimFrom && rr < Rules.PanRimTo) lvl = Math.Min(lvl, E[r * _w + c]);
                }
                if (double.IsPositiveInfinity(lvl)) throw new InvalidOperationException("[TerrainPlan] " + p.Id + " has no rim on the grid.");
                lvl -= Rules.PanRimDrop;
                double bed = lvl - panDepth;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c; double rr = rv[(r - r0) * ww + (c - c0)], e = E[i];
                    double e2 = rr < 1 ? Math.Min(e, bed + (e - bed) * (rr * rr)) : e;
                    E[i] = e2;
                    if (rr < 1 && e2 < lvl && double.IsNaN(_still[i])) _still[i] = lvl;
                }
            }
        }

        /// <summary>
        /// A channel's bed along its line (pass 9's bed_profile): the designed profile pulled down to the ground less the
        /// incision. A stream never climbs downstream; a tidal creek follows the ground; an outlet or a creek's incision
        /// ramps in from its start.
        /// </summary>
        double[] BedProfile(PlanLine g, double[] z, double[] E, bool tidalCreek, bool sill)
        {
            double incise = Num(_plan.Incise), ramp = Num(_plan.JoinRamp);
            int m = g.R.Length; var zb = new double[m];
            for (int j = 0; j < m; j++)
            {
                double inc = tidalCreek || sill ? incise * Clip(g.S[j] / ramp, 0, 1) : incise;
                zb[j] = Math.Min(Interp(g.S[j], g.Knots, z), _g.Sample(E, g.R[j].X, g.R[j].Y) - inc);
            }
            if (!tidalCreek) for (int j = 1; j < m; j++) zb[j] = Math.Min(zb[j - 1], zb[j]);
            return zb;
        }

        /// <summary>
        /// Carve a channel into E (pass 9's _channel, per stream or creek). A stream has two bank slopes (fresh, tidal) and
        /// still water in its bed; a creek (<paramref name="tidalSlope"/> NaN) is tidal throughout and holds none.
        /// </summary>
        void Channel(double[] E, PlanLine g, double[] prof, double[] widths, double slope, double tidalSlope, double k,
                     PlanPoint[] ctrl, double reach, double depth, bool recordCut)
        {
            bool creek = double.IsNaN(tidalSlope);
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in ctrl) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            // the window is the drawn points' box (pass 9), not the resampled line's
            if (!Win(x0 - reach, y0 - reach, x1 + reach, y1 + reach, out int r0, out int r1, out int c0, out int c1)) return;
            int hint = -1;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                g.Query(_xs[c], _ys[r], ref hint, out double d, out _, out double u);
                double zb = Interp(u, g.S, prof), hw = Interp(u, g.Knots, widths) / 2.0;
                double e = E[i];
                double q = d / Math.Max(hw, Rules.ChannelMinHalf);
                double vf = d <= hw ? zb - Rules.ChannelDish * (1 - q * q) : zb + (d - hw) * slope;
                bool cc = d <= hw + Rules.ChannelEdge;
                if (creek)
                {
                    E[i] = Smin(e, vf, k);
                    if (cc) { _chan[i] = true; _tidal[i] = true; }
                    _bank[i] = Math.Min(_bank[i], Math.Max(d - hw, 0));
                    continue;
                }
                double vt = d <= hw ? vf : zb + (d - hw) * tidalSlope;
                double ef = Smin(e, vf, k), et = Smin(e, vt, k);
                bool tid = zb < _spring;
                E[i] = tid ? et : ef;
                if (recordCut) _runCut[i] = e - E[i];
                if (cc) { _chan[i] = true; if (tid) _tidal[i] = true; }
                _bank[i] = Math.Min(_bank[i], Math.Max(d - hw, 0));
                double lvl = zb + (tid ? depth * Rules.TidalDepthShare : depth);
                if (cc && double.IsNaN(_still[i])) _still[i] = lvl;
            }
        }

        // ---- paths (pass 9's path_fields): the nearest painted path's centre, its half width and its index -----------

        /// <summary>
        /// The path field over part 1's paths (<paramref name="part1"/>) or the key scenes' painted paths. A tidal crossing
        /// is never painted. Each path's edge wanders by its own salt (Seed + 600 + its index in the plan's list).
        /// </summary>
        void PathFields(out double[] pd, out double[] ph, out short[] pw, bool part1)
        {
            pd = Filled(double.PositiveInfinity); ph = new double[_n]; pw = new short[_n];
            for (int i = 0; i < _n; i++) pw[i] = -1;
            double edgeAmp = Num(_plan.PathEdge.x), edgeLam = Num(_plan.PathEdge.y);
            for (int n = 0; n < _plan.Paths.Length; n++)
            {
                var p = _plan.Paths[n];
                if (p.Kind == PathKind.TidalCrossing || p.PaintOnly == part1) continue;
                var g = _pathLines[n];
                double half = Num(p.Width) / 2;
                if (!Win(g.MinX - Rules.PathWindow, g.MinY - Rules.PathWindow, g.MaxX + Rules.PathWindow, g.MaxY + Rules.PathWindow,
                        out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = -1;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    g.Query(_xs[c], _ys[r], ref hint, out double d, out _, out _);
                    double h = half + edgeAmp * SNoise(_xs[c], _ys[r], _seed + Rules.SeedPath + n, edgeLam);
                    if (d - h < pd[i] - ph[i]) { pd[i] = d; ph[i] = h; pw[i] = (short)n; }
                }
            }
        }

        /// <summary>Distance to the lower 40 % of each stream, its tidal mouth (pass 9's mouth_dist).</summary>
        double[] MouthDist()
        {
            var Dm = Filled(Rules.MouthReach);
            foreach (var g in _streamLines)
            {
                double s1 = g.S[g.S.Length - 1] * Rules.MouthShare;
                var T = new List<PlanPoint>();
                for (int j = 0; j < g.R.Length; j++) if (g.S[j] >= s1) T.Add(g.R[j]);
                if (T.Count < 2) continue;
                var t = Exact(T);
                double rm = Rules.MouthReach;
                if (!Win(t.MinX - rm, t.MinY - rm, t.MaxX + rm, t.MaxY + rm, out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = -1;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    t.Query(_xs[c], _ys[r], ref hint, out double d, out _, out _);
                    if (d < Dm[i]) Dm[i] = d;
                }
            }
            return Dm;
        }

        // ---- the paint (pass 9's derive, from z = today): one pass, each cell's rules in the prototype's order ----------

        /// <summary>What a section type carries at map scale (pass 9's features_zone): boulders, runnels, talus, weed, mussels.</summary>
        byte FeaturesZone(int type, byte z, double E, double X, double Y, double sd, int k, double mouth, Section sec)
        {
            switch (type)
            {
                case TSand:
                {
                    bool run = Math.Abs(sd + Rules.SandRunnelOffset + Rules.SandRunnelWander * SNoise(X, Y, _seed + Rules.SeedSandRunnel, Rules.SandRunnelLambda))
                               < Rules.SandRunnelHalf;
                    if (run && E < Rules.SandRunnelTo && E > Rules.SandRunnelFrom) z = ZSilt;
                    if (E > Rules.SandTalusFrom && E < Rules.SandTalusTo && F2(X, Y, k) > Rules.SandTalusAbove) z = ZTalus;
                    return z;
                }
                case TLedge:
                {
                    if (E > Rules.LedgeTalusHighFrom && E < Rules.LedgeTalusHighTo && F2(X, Y, k) > Rules.LedgeTalusHighAbove) z = ZTalus;
                    if (E > Rules.LedgeTalusLowFrom && E < Rules.LedgeTalusLowTo && F1(X, Y, k) > Rules.LedgeTalusLowAbove) z = ZTalus;
                    if (sec != null && sec.HasRockEdge)                       // off the rock's own edge the bed is sand and eelgrass
                    {
                        bool off = sd < sec.RockEdge + Rules.RockEdgeWander * SNoise(X, Y, _seed + Rules.SeedRockEdge + k, Rules.RockEdgeLambda);
                        if (off) z = E < Rules.RockEdgeEelgrassBelow ? ZEelgrass : ZRipple;
                    }
                    return z;
                }
                case TShingle:
                    if (E > Rules.ShingleWeedFrom && E < Rules.ShingleWeedTo && F1(X, Y, k) > Rules.ShingleWeedAbove) z = ZRockweed;
                    if (E > Rules.ShingleTalusFrom && E < Rules.ShingleTalusTo && F2(X, Y, k) > Rules.ShingleTalusAbove) z = ZTalus;
                    return z;
                case TMud:
                    if (E > Rules.MudMusselFrom && E < Rules.MudMusselTo && F1(X, Y, k) > Rules.SectionMusselAbove && Bed(X, Y, mouth)) z = ZMusselbed;
                    if (E > Rules.MudSiltFrom && E < Rules.MudSiltTo && Math.Abs(SNoise(X, Y, _seed + Rules.SeedMudSilt, Rules.MudSiltLambda)) < Rules.MudSiltHalf)
                        z = ZSilt;
                    return z;
                case TMarsh:
                    if (E > Rules.MarshMusselFrom && E < Rules.MarshMusselTo && F1(X, Y, k) > Rules.SectionMusselAbove && Bed(X, Y, mouth)) z = ZMusselbed;
                    return z;
                case TFlats:
                    if (E > Rules.FlatsSiltFrom && E < Rules.FlatsSiltTo && Math.Abs(SNoise(X, Y, _seed + Rules.SeedFlatsSilt, Rules.FlatsSiltLambda)) < Rules.FlatsSiltHalf)
                        z = ZSilt;
                    return z;
                default:
                    return z;
            }
        }

        double F1(double X, double Y, int k) => Fbm(X, Y, _seed + Rules.SeedFeature1 + k, Rules.Feature1Lambda);
        double F2(double X, double Y, int k) => Fbm(X, Y, _seed + Rules.SeedFeature2 + k, Rules.Feature2Lambda);

        /// <summary>A mussel bed keeps near a stream's mouth (NaN mouth: anywhere).</summary>
        bool Bed(double X, double Y, double mouth) =>
            double.IsNaN(mouth) || mouth < Rules.MouthBed + Rules.MouthBedNoise * Fbm(X, Y, _seed + Rules.SeedMouth, Rules.MouthBedLambda);

        void Paint1(double[] ke)
        {
            var b = _src.Base; var today = _src.Today;
            _zone = new byte[_n];
            var mouth = MouthDist();
            var bays = BayZones();
            _r.BayPaint = bays;
            // the paint-only sections: a polygon, or a bearing sector below a height
            var po = _plan.PaintOnly; int P = po.Length;
            var poType = new int[P]; var poPoly = new PlanPoint[P][]; var poRec = new RecipeTable[P];
            var poS0 = new double[P]; var poS1 = new double[P]; var poBelow = new double[P];
            for (int j = 0; j < P; j++)
            {
                poType[j] = TypeCode(po[j].Type); poRec[j] = new RecipeTable(po[j].Recipe);
                poPoly[j] = po[j].Poly != null && po[j].Poly.Length > 0 ? Num(po[j].Poly) : null;
                poS0[j] = Num(po[j].Sector.x); poS1[j] = Num(po[j].Sector.y); poBelow[j] = Num(po[j].Below);
            }
            double dominance = Num(_plan.SectionDominance), floor = Num(_plan.SectionPaintFloor), ceiling = Num(_plan.SectionPaintCeiling);
            double moved = Num(_plan.SectionPaintMoved), keepAbove = Num(_keep.KeepPaintAbove);
            var isNew = new bool[_plan.Paths.Length]; var isRoad = new bool[_plan.Paths.Length];
            for (int n = 0; n < isNew.Length; n++)
            {
                var p = _plan.Paths[n];
                if (p.PaintOnly || p.Kind == PathKind.TidalCrossing) continue;
                if (string.IsNullOrEmpty(p.LineSource)) isNew[n] = true; else isRoad[n] = true;
            }
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r], E = _E[i];
                double pe = _P != null ? _P[i] : E;                                    // the height the paint reads (§4.6)
                byte z = today[i] != TerrainPlanZones.Unpainted ? today[i] : BandZone(pe, X, Y);
                for (int j = 0; j < P; j++)
                {
                    bool m;
                    if (poPoly[j] != null) m = PointInPoly(X, Y, poPoly[j]);
                    else
                    {
                        m = pe < poBelow[j] && pe > Rules.PaintOnlyDeep;
                        if (m)
                        {
                            double bb = _src.Bearing(X, Y);
                            m = bb >= poS0[j] && bb <= poS1[j] && _src.SectorClass(bb) != Rules.PaintOnlySkipsClass;
                        }
                    }
                    if (m) z = FeaturesZone(poType[j], RecipeZone(poRec[j], pe, X, Y, _seed + Rules.SeedPaintOnly), pe, X, Y, _sd[i],
                                            Rules.PaintOnlyK, double.NaN, null);
                }
                int dom = _secw[i] > dominance ? _secidx[i] : -1;
                if (dom >= 0 && pe > floor && (pe < ceiling || Math.Abs(pe - b[i]) > moved))
                {
                    var t = _secTab[dom];
                    z = FeaturesZone(t.Type, RecipeZone(t.Recipe, pe, X, Y, _seed + Rules.SeedSection + dom), pe, X, Y, _sd[i], dom, mouth[i], t);
                }
                // the barren's granite: outcrops (raised), a talus rim, bare dirt; never in water, on a bank or near a path
                bool nochan = !_chan[i], dry = double.IsNaN(_still[i]);
                if (_biome[i] == _bBarren && z == ZGrass && nochan && dry && _bank[i] > Rules.BarrenBank && _pd[i] - _ph[i] > Rules.BarrenPathGap)
                {
                    double g = Fbm(X, Y, _seed + Rules.SeedGranite, Rules.BarrenGraniteLambda)
                               + Rules.BarrenGranite2 * (Fbm(X, Y, _seed + Rules.SeedGranite2, Rules.BarrenGranite2Lambda) - 0.5);
                    if (g > Rules.BarrenShelf)
                    {
                        z = ZShelf;
                        E = E + (Rules.BarrenRise + Rules.BarrenRiseMore * Ss((g - Rules.BarrenShelf) / Rules.BarrenRiseSpan)) * ke[i];
                        if (_P == null) pe = E;                                         // the import holds its own outcrops
                    }
                    else if (g > Rules.BarrenTalus) z = ZTalus;
                    else if (Fbm(X, Y, _seed + Rules.SeedDirt, Rules.BarrenDirtLambda) > Rules.BarrenDirt) z = ZDirt;
                }
                // the swale: sedge and mud round its water
                if (_biome[i] == _bSwale)
                {
                    if (z == ZGrass && _bank[i] < Rules.SwaleBank + Rules.SwaleBankNoise * Fbm(X, Y, _seed + Rules.SeedSwale, Rules.SwaleLambda)) z = ZSedge;
                    if (z == ZSedge && Fbm(X, Y, _seed + Rules.SeedSwaleMud, Rules.SwaleMudLambda) > Rules.SwaleMud) z = ZMud;
                }
                // water's own ground: mud at a fresh edge, sedge beyond; silt beds; tidal creeks run mud
                if (!double.IsInfinity(_bank[i]) && !double.IsNaN(_bank[i]) && pe > Rules.FreshAbove)
                {
                    if (_bank[i] < Rules.FreshMud) z = ZMud;
                    if (_bank[i] >= Rules.FreshMud && z == ZGrass
                        && _bank[i] < Rules.FreshSedge + Rules.FreshSedgeNoise * Fbm(X, Y, _seed + Rules.SeedFresh, Rules.FreshLambda)) z = ZSedge;
                }
                if (_chan[i] && !_tidal[i]) z = ZSilt;
                if (_chan[i] && _tidal[i] && pe > Rules.TidalMudAbove) z = ZMud;
                if (_tidal[i] && !_chan[i] && _bank[i] < Rules.TidalSiltBank && pe > Rules.TidalSiltFrom && pe < Rules.TidalSiltTo) z = ZSilt;
                if (!dry && nochan) z = ZSilt;
                // a ground file's bay: its own bands on the import (amendment 2 §4.5); paths and what is built still win
                if (bays != null && bays[i] != TerrainPlanZones.Unpainted) z = bays[i];
                // new paths (the roads are painted after the keep); stepping stones where a path fords
                bool onpath = _pd[i] < _ph[i];
                if (onpath && _pw[i] >= 0 && isNew[_pw[i]])
                {
                    if (nochan && dry) z = ZPath;
                    if (!nochan) z = ZTalus;
                    if (nochan) E = E - _pathDipM * ke[i];
                }
                // what is built keeps today's paint (or the bands it stands on); the roads are repainted on their own line
                if (_pp1[i] > keepAbove) z = today[i] != TerrainPlanZones.Unpainted ? today[i] : BandZone(b[i], X, Y);
                if (onpath && _pw[i] >= 0 && isRoad[_pw[i]]) z = ZPath;
                _zone[i] = z;
                _E[i] = E;
            }
        }
    }
}
