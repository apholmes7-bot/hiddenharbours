using System;
using System.Collections.Generic;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Part 2's south (terrain PR 5w; p2_lib's coast2, flats, plinths, creeks and paint): the sections from the Harbour
    /// Strand round the south to the bar's root, laid as paint. The ground is the ground file's (PR 5 B), so nothing here
    /// moves it: the coast step's heights only shape the flats' mask, as they did in part 2, and every paint rule reads
    /// the heights the player stands on (<see cref="Pz"/>). A toe section reads its walls' toe line, the live
    /// <see cref="CliffWallDef"/>s it lists.
    /// <para>
    /// The paint runs in part 2's order: the sections (each its recipe and its features), the flats, the forms' feet, then
    /// the bar (<see cref="BarPaint"/>), then part 2's ponds' and brooks' banks; the flats' still water and the reef's rock
    /// pools wait for the still water (<see cref="SouthStillPaint"/>). A bay a ground ask laid keeps its own paint.
    /// </para>
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        const int S2Strand = 0, S2Landing = 1, S2DeepCliff = 2, S2ToeReef = 3, S2GapCove = 4, S2Storm = 5, S2Flats = 6;

        static int SouthType(string t)
        {
            switch (t)
            {
                case "harbour_strand": return S2Strand;
                case "landing": return S2Landing;
                case "deep_cliff": return S2DeepCliff;
                case "toe_reef": return S2ToeReef;
                case "gap_cove": return S2GapCove;
                case "storm_beach": return S2Storm;
                case "sandy_flats": return S2Flats;
                default: throw new InvalidOperationException("[TerrainPlan] part 2 has no section type called '" + t + "'.");
            }
        }

        /// <summary>One of part 2's sections, read once.</summary>
        sealed class SouthSection
        {
            public CoastSectionDef Def;
            public int Type;
            public CoastSectionMode Mode;
            public RecipeTable Recipe;
            public double EdgeAmp, EdgeLam, SeaTail, SeaEnd, LandEnd, KeepAbove, MaxCut;
            public double[] Kx, Ky;
            public bool Spit;
            public PlanPoint SpitRoot, SpitDir;
            public double SpitLength, SpitHalf;
            public double[] SpitXp, SpitFp;
            public PlanLine Toe;
            public double Broken = 1.0;
            public double[] GullyAt, GullyLean, GullyWidth;
            public string Flats = "";
        }

        SouthSection[] _south;
        PlanLine _shore2;
        double[] _secU2;
        double _uEnd2;
        int[] _toeSecs;

        // the south's fields over the grid: the coast step's weights and distances, the flats' masks, the feet, the creeks
        double[] _secw2, _sd2, _sto, _rw, _fw, _fdt, _bank2, _peW;
        short[] _secidx2;
        bool[] _spm, _frun, _frib, _fmask, _chan2, _tidal2, _southFlats;
        /// <summary>What the south painted at a cell (Unpainted where it painted nothing), so the still water's paint only
        /// lands where nothing stronger painted since.</summary>
        byte[] _southZone;

        bool HasSouth => _plan.Sections2 != null && _plan.Sections2.Length > 0;

        // ---- the line, the sections and their toe lines ---------------------------------------------------------------

        void SouthLines()
        {
            var sl = _plan.ShoreLine2;
            if (sl == null || sl.Length < 4) throw new InvalidOperationException("[TerrainPlan] part 2's shore line needs four points.");
            var pts = new PlanPoint[sl.Length];
            var starts = new List<int>();
            for (int i = 0; i < sl.Length; i++)
            {
                pts[i] = Num(sl[i].At);
                if (!string.IsNullOrEmpty(sl[i].SectionId)) starts.Add(i);
            }
            var secs = _plan.Sections2;
            if (starts.Count != secs.Length)
                throw new InvalidOperationException("[TerrainPlan] part 2's shore line starts " + starts.Count + " sections; the plan lists " + secs.Length + ".");
            for (int k = 0; k < secs.Length; k++)
                if (secs[k] == null || sl[starts[k]].SectionId != secs[k].Id)
                    throw new InvalidOperationException("[TerrainPlan] part 2's section " + k + " is not the one its shore line starts there (" +
                                                        sl[starts[k]].SectionId + ").");
            _shore2 = RunOut(Catmull(pts, Num(_plan.ShoreStep)), (int)Num(_plan.ShoreRunOut));
            // SEC_U: each section's start by the raw bearing (part 2's line runs round the south, 61.6 to 285, no wrap)
            _secU2 = new double[starts.Count];
            for (int k = 0; k < starts.Count; k++) _secU2[k] = _src.Bearing(pts[starts[k]].X, pts[starts[k]].Y);
            _uEnd2 = _src.Bearing(pts[pts.Length - 1].X, pts[pts.Length - 1].Y);

            _south = new SouthSection[secs.Length];
            var toes = new List<int>();
            var reef = _plan.Reef ?? new TerrainPlanReef();
            for (int k = 0; k < secs.Length; k++)
            {
                var s = secs[k];
                var t = new SouthSection { Def = s, Type = SouthType(s.Type), Mode = s.Mode, Flats = s.FlatsCharacter ?? "" };
                if (s.Mode != CoastSectionMode.Keep) t.Recipe = new RecipeTable(s.Recipe);
                t.EdgeAmp = Num(s.Edge.x); t.EdgeLam = Num(s.Edge.y); t.SeaTail = Num(s.SeaTail);
                t.KeepAbove = Num(s.KeepAbove); t.MaxCut = Num(s.SpitMaxCut);
                var kn = new List<PlanPoint>();                                        // sorted(sec['knots'])
                foreach (var v in s.Knots ?? new UnityEngine.Vector2[0]) kn.Add(Num(v));
                kn.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
                t.Kx = new double[kn.Count]; t.Ky = new double[kn.Count];
                for (int j = 0; j < kn.Count; j++) { t.Kx[j] = kn[j].X; t.Ky[j] = kn[j].Y; }
                if (kn.Count > 0) { t.SeaEnd = t.Kx[0]; t.LandEnd = t.Kx[kn.Count - 1]; }
                if ((s.Mode == CoastSectionMode.Cut || s.Mode == CoastSectionMode.Fill) && kn.Count < 2)
                    throw new InvalidOperationException("[TerrainPlan] " + s.Id + " lays a profile with fewer than two knots.");
                if (s.HasSpit)
                {
                    t.Spit = true;
                    double b = Num(s.SpitBearing), tr = Radians(b);
                    double vx = Math.Sin(tr), vy = Math.Cos(tr) * _src.IslandRadiusY / _src.IslandRadiusX, vn = Hypot(vx, vy);
                    t.SpitDir = new PlanPoint(vx / vn, vy / vn);
                    // spit_root: the extended line's point whose bearing is nearest (numpy's argmin: the first)
                    int best = 0; double bd = double.PositiveInfinity;
                    for (int j = 0; j < _shore2.R.Length; j++)
                    {
                        double d = Math.Abs(_src.Bearing(_shore2.R[j].X, _shore2.R[j].Y) - b);
                        if (d < bd) { bd = d; best = j; }
                    }
                    t.SpitRoot = _shore2.R[best];
                    t.SpitLength = Num(s.SpitLength); t.SpitHalf = Num(s.SpitHalfWidth);
                    t.SpitXp = new[] { 0.0, t.SpitLength }; t.SpitFp = new[] { Num(s.SpitCrest.x), Num(s.SpitCrest.y) };
                }
                if (s.Mode == CoastSectionMode.Toe)
                {
                    t.Toe = ToeLine(s);
                    toes.Add(k);
                    if (s.Broken) t.Broken = Num(reef.BrokenScale);
                    if (t.Type == S2ToeReef) Gullies(t, k, reef);
                }
                _south[k] = t;
            }
            _toeSecs = toes.ToArray();
        }

        /// <summary>
        /// A toe section's toe line: its live walls' toes, clockwise, each turned so its brow lies to the right of travel
        /// (part 2's chain and _walls).
        /// </summary>
        static PlanLine ToeLine(CoastSectionDef s)
        {
            if (s.Walls == null || s.Walls.Length == 0) throw new InvalidOperationException("[TerrainPlan] " + s.Id + " is a toe section with no walls.");
            var T = new List<PlanPoint>();
            foreach (var w in s.Walls)
            {
                if (w == null || !w.IsLive) throw new InvalidOperationException("[TerrainPlan] " + s.Id + " lists a wall that does not stand.");
                var toe = Num(w.Toe); var brow = Num(w.Brow);
                if (toe.Length == 0 || brow.Length != toe.Length) throw new InvalidOperationException("[TerrainPlan] " + w.Id + "'s lines do not pair.");
                if (toe.Length > 1)
                {
                    var own = Exact(toe); int hint = -1;
                    var mid = brow[brow.Length / 2];
                    own.Query(mid.X, mid.Y, ref hint, out _, out double side, out _);
                    if (side < 0) Array.Reverse(toe);
                }
                T.AddRange(toe);
            }
            return Exact(T);
        }

        /// <summary>A toe reef's gullies across it: (along the toe (m), lean (tan), width (m)), every GullyEvery (part 2's gullies).</summary>
        void Gullies(SouthSection t, int k, TerrainPlanReef R)
        {
            double total = t.Toe.Length, br = t.Broken, e0 = Num(R.GullyEvery.x), e1 = Num(R.GullyEvery.y);
            double w0 = Num(R.GullyWidth.x), w1 = Num(R.GullyWidth.y);
            var at = new List<double>(); var lean = new List<double>(); var wid = new List<double>();
            double u = Rules.GullyFirst; int n = 0;
            while (true)
            {
                u += (e0 + (e1 - e0) * Hash01(n, k, _seed2 + Rules.SeedGullyEvery)) / br;
                if (u > total - Rules.GullyEndKeep) break;
                double l = Radians(-Rules.GullyLean + 2 * Rules.GullyLean * Hash01(n, k, _seed2 + Rules.SeedGullyLean));
                double w = (w0 + (w1 - w0) * Hash01(n, k, _seed2 + Rules.SeedGullyWidth)) * br;
                at.Add(u); lean.Add(Math.Tan(l)); wid.Add(w); n++;
            }
            t.GullyAt = at.ToArray(); t.GullyLean = lean.ToArray(); t.GullyWidth = wid.ToArray();
        }

        /// <summary>The live walls the south's toe sections list, in their order.</summary>
        IEnumerable<CliffWallDef> SouthWalls()
        {
            if (!HasSouth) yield break;
            foreach (var s in _plan.Sections2)
                if (s != null && s.Mode == CoastSectionMode.Toe && s.Walls != null)
                    foreach (var w in s.Walls) if (w != null && w.IsLive) yield return w;
        }

        static List<PlanPoint> Footprint(CliffWallDef w)
        {
            var o = new List<PlanPoint>(Num(w.Brow));
            var toe = Num(w.Toe);
            for (int j = toe.Length - 1; j >= 0; j--) o.Add(toe[j]);
            return o;
        }

        /// <summary>
        /// The walls in part 2's protected set (protection2's walls): each live wall's footprint holds the ground within
        /// WallKeep (<see cref="_peW"/>, which only shapes the flats' mask here) and keeps today's paint within WallPaintKeep.
        /// </summary>
        void SouthKeep(double[] pp)
        {
            _peW = new double[_n];
            if (!HasSouth) return;
            double kb = Num(_plan.WallKeep.x), kf = Num(_plan.WallKeep.y), pb = Num(_plan.WallPaintKeep.x), pf = Num(_plan.WallPaintKeep.y);
            foreach (var w in SouthWalls())
            {
                var fp = Footprint(w);
                StampPoly(_peW, fp, kb, kf);
                StampPoly(pp, fp, pb, pf);
            }
        }

        /// <summary>Part 2's ke: the protected set with the walls' footprints in it (protection2's), for the flats' mask only.</summary>
        double[] SouthKe(double frozenAt)
        {
            var ke = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                double p = Math.Max(_pe2[i], _peW[i]);
                ke[i] = p >= frozenAt ? 0.0 : 1 - Clip(p, 0, 1);
            }
            return ke;
        }

        // ---- the coast step (coast2): weights, distances, the spit and reef masks, and the heights the flats sit on ---

        double[] SouthCoast(double[] E1)
        {
            var secs = _south; int K = secs.Length;
            var Ec = (double[])E1.Clone();
            _secw2 = new double[_n]; _secidx2 = new short[_n]; _sd2 = Filled(double.PositiveInfinity); _sto = Filled(double.PositiveInfinity);
            _spm = new bool[_n]; _rw = new double[_n];
            for (int i = 0; i < _n; i++) _secidx2[i] = -1;
            double win = Num(_plan.ShoreWindow2);
            if (!Win(_shore2.MinX - win, _shore2.MinY - win, _shore2.MaxX + win, _shore2.MaxY + win, out int r0, out int r1, out int c0, out int c1)) return Ec;
            double wa = Num(_plan.Warp2.x), wl = Num(_plan.Warp2.y);
            double feather = Num(_plan.SectionFeather2), growth = Num(_plan.FeatherGrowth2), growthFrom = Num(_plan.FeatherGrowthFrom);
            double endF = Num(_plan.EndFeather2), landFade = Num(_plan.LandFade), landFeather = Num(_plan.LandFadeFeather), cutFade = Num(_plan.CutFade);
            var reef = _plan.Reef ?? new TerrainPlanReef();
            var W = new double[K];
            int hint = -1;
            var toeHint = new int[K];
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r], B = E1[i];
                double Xw = X + wa * SNoise(X, Y, _seed2 + Rules.SeedWarpX, wl), Yw = Y + wa * SNoise(X, Y, _seed2 + Rules.SeedWarpY, wl);
                _shore2.Query(Xw, Yw, ref hint, out double dist, out double sgn, out _);
                double sd0 = sgn * dist;
                double u = _src.Bearing(Xw, Yw);
                double F = feather + growth * Math.Max(0.0, -sd0 - growthFrom);
                for (int n = 0; n < K; n++)
                {
                    double u0 = _secU2[n], u1 = n + 1 < K ? _secU2[n + 1] : _uEnd2;
                    double lo = n > 0 ? u0 - F : -1e9, hi = n + 1 < K ? u1 + F : 1e9;
                    W[n] = Ss((u - lo) / (2 * F)) * Ss((hi - u) / (2 * F));
                }
                double sum = W[0];
                for (int n = 1; n < K; n++) sum += W[n];
                double norm = Math.Max(sum, 1e-9);
                double end = Ss((u - _secU2[0] + endF) / endF) * Ss((_uEnd2 + endF - u) / endF);
                double num = 0, wsum = 0, bestw = 0, sto = double.PositiveInfinity, rw = 0; int best = -1; bool spm = false;
                for (int k = 0; k < K; k++)
                {
                    double wk = W[k] / norm * end;
                    if (!(wk > 1e-4)) continue;
                    var t = secs[k];
                    double wb = 1.0, e = B;
                    if (t.Mode == CoastSectionMode.Cut || t.Mode == CoastSectionMode.Fill)
                    {
                        double sd = sd0 + t.EdgeAmp * SNoise(X, Y, _seed2 + Rules.SeedEdge + Rules.SeedEdgeStep * k, t.EdgeLam);
                        double P = Interp(sd, t.Kx, t.Ky);
                        if (t.Type == S2Storm || t.Type == S2Strand)
                            P = P + (P > Rules.StrandReliefAbove ? Rules.StrandRelief * SNoise(X, Y, _seed2 + Rules.SeedStrandRelief + k, Rules.StrandReliefLambda) : 0.0);
                        if (t.Type == S2GapCove)
                        {
                            P = Math.Max(P, B - t.MaxCut);
                            if (t.Spit)
                            {
                                double es = SpitAt(t, X, Y, P, out bool on);
                                P = Math.Max(P, es);
                                spm |= on;
                            }
                            double keep = 1 - Ss((B - t.KeepAbove) / Rules.CoveKeepFeather);
                            P = B + keep * (P - B);
                        }
                        bool cut = t.Mode == CoastSectionMode.Cut;
                        e = cut ? P : (sd > 0 ? Math.Max(P, B) : P);
                        wb = sd < t.SeaEnd ? 1 - Ss((t.SeaEnd - sd) / t.SeaTail) : 1.0;
                        wb = wb * (cut ? (sd > t.LandEnd ? 1 - Ss((sd - t.LandEnd) / cutFade) : 1.0) : (1 - Ss((sd - landFade) / landFeather)));
                    }
                    else if (t.Mode == CoastSectionMode.Toe)
                    {
                        t.Toe.Query(X, Y, ref toeHint[k], out double d, out double sg, out double along);   // unwarped
                        double s = sg < 0 ? d : -d;                                                       // + seaward of the toe
                        if (s > 0) sto = Math.Min(sto, s);
                        if (t.Type == S2ToeReef)
                        {
                            e = ReefAt(t, k, reef, X, Y, B, s, along, out double wr);
                            rw = Math.Max(rw, wr * wk);
                        }
                    }
                    double w = wk * wb;
                    num += w * (e - B); wsum += w;
                    if (w > bestw) { best = k; bestw = w; }
                }
                Ec[i] = B + num; _secw2[i] = wsum; _secidx2[i] = (short)best; _sd2[i] = sd0;
                _sto[i] = sto; _spm[i] = spm; _rw[i] = rw;
            }
            return Ec;
        }

        /// <summary>A gap cove's spit (part 2's spit): its crest's height where a cell is on it, and whether it stands proud of the floor.</summary>
        static double SpitAtCore(SouthSection t, double X, double Y, double floor, long seed2, out bool on)
        {
            double dx = X - t.SpitRoot.X, dy = Y - t.SpitRoot.Y;
            double a = dx * t.SpitDir.X + dy * t.SpitDir.Y, c = -dx * t.SpitDir.Y + dy * t.SpitDir.X;
            c = c + Rules.SpitWander * SNoise(X, Y, seed2 + Rules.SeedSpitWander, Rules.SpitWanderLambda);
            double crest = Interp(a, t.SpitXp, t.SpitFp) +
                           Rules.SpitCrestNoise * SNoise(X, Y, seed2 + Rules.SeedSpitCrest, Rules.SpitCrestLambda);
            double ce = a > t.SpitLength ? Hypot(c, (a - t.SpitLength) * Rules.SpitTipStretch) : Math.Abs(c);
            double q = ce / t.SpitHalf;
            double e = crest - (crest - floor) * (q * q);
            bool inside = a > -Rules.SpitRootBack && ce < t.SpitHalf;
            on = inside && e > floor;
            return inside ? e : -99.0;
        }

        double SpitAt(SouthSection t, double X, double Y, double floor, out bool on) => SpitAtCore(t, X, Y, floor, _seed2, out on);

        /// <summary>A toe reef's ground at a cell s metres seaward of its toe, t along it (part 2's reef), and its weight.</summary>
        double ReefAt(SouthSection t, int k, TerrainPlanReef R, double X, double Y, double b, double s, double along, out double w)
        {
            double br = t.Broken;
            double sn = s / (1 + Num(R.WidthWander) * SNoise(X, Y, _seed2 + Rules.SeedReefWidth + k, Num(R.WidthLambda)))
                        + Rules.ReefEdgeNoise * br * SNoise(X, Y, _seed2 + Rules.SeedReefEdge + k, Rules.ReefEdgeLambda);
            double zc = Num(R.CrestZ) + Num(R.CrestAmp) * SNoise(X, Y, _seed2 + Rules.SeedReefCrest + k, Rules.ReefCrestLambda)
                        + Rules.ReefRough * br * SNoise(X, Y, _seed2 + Rules.SeedReefRough + k, Rules.ReefRoughLambda);
            double rise0 = Num(R.Rise.x), rise1 = Num(R.Rise.y), fall0 = Num(R.Fall.x), fall1 = Num(R.Fall.y);
            w = Ss((sn - rise0) / (rise1 - rise0)) * (1 - Ss((sn - fall0) / (fall1 - fall0)));
            double e = zc, gz = Num(R.GullyZ), crest0 = Num(R.Crest.x);
            for (int j = 0; j < t.GullyAt.Length; j++)
            {
                double g = Math.Abs(along - t.GullyAt[j] - (sn - crest0) * t.GullyLean[j]) / (t.GullyWidth[j] / 2);
                if (g < Rules.GullyReach) e = Math.Min(e, gz + (e - gz) * Ss(g / Rules.GullyReach));
            }
            return Math.Max(b, b + w * (e - b));
        }

        /// <summary>Metres to the nearest toe of any toe section (unsigned), and that section's index (part 2's toe_distance).</summary>
        double ToeDistance(double X, double Y, int[] hints, out int which)
        {
            double d = double.PositiveInfinity; which = -1;
            for (int j = 0; j < _toeSecs.Length; j++)
            {
                int k = _toeSecs[j];
                _south[k].Toe.Query(X, Y, ref hints[j], out double dd, out _, out _);
                if (dd < d) { d = dd; which = k; }
            }
            return d;
        }

        // ---- the flats: where they lie, their drains and ribs (part 2's flats), on the coast step's heights ------------

        sealed class FlatsRunnel
        {
            public double At, Width, Depth, Amp, Q0, Reach, QMin;
            public int N;
            public double[] BAt, BLen, BTan, BCos;
        }

        List<FlatsRunnel> FlatsRunnels(FlatsDef F, double total)
        {
            var o = new List<FlatsRunnel>();
            double e0 = Num(F.RunnelEvery.x), e1 = Num(F.RunnelEvery.y), w0 = Num(F.RunnelWidth.x), w1 = Num(F.RunnelWidth.y);
            double d0 = Num(F.RunnelDepth.x), d1 = Num(F.RunnelDepth.y);
            double t = 0.0; int n = 0;
            while (true)
            {
                t += e0 + (e1 - e0) * Hash01(n, 5, _seed2 + Rules.SeedFlatsRunnelEvery);
                if (t > total) return o;
                var R = new FlatsRunnel
                {
                    At = t, N = n,
                    Width = w0 + (w1 - w0) * Hash01(n, 6, _seed2 + Rules.SeedFlatsRunnelWidth),
                    Depth = d0 + (d1 - d0) * Hash01(n, 7, _seed2 + Rules.SeedFlatsRunnelDepth),
                };
                long hs = _seed2 + Rules.SeedFlatsRunnel;
                R.Amp = Num(F.RunnelMeander.x) * (0.5 + Hash01(n, 1, hs));
                R.Q0 = Num(F.RunnelStart.x) + (Num(F.RunnelStart.y) - Num(F.RunnelStart.x)) * Hash01(n, 2, hs);
                int b0 = F.RunnelBranches.x, b1 = F.RunnelBranches.y;
                int nb = Math.Min((int)(b0 + (b1 + 1 - b0) * Hash01(n, 3, hs)), b1);
                R.BAt = new double[nb]; R.BLen = new double[nb]; R.BTan = new double[nb]; R.BCos = new double[nb];
                double reachB = 0.0, qmin = R.Q0;
                for (int j = 0; j < nb; j++)
                {
                    double qj = Num(F.BranchAt.x) + (Num(F.BranchAt.y) - Num(F.BranchAt.x)) * Hash01(n, 10 + j, hs);
                    double lq = Num(F.BranchLength.x) + (Num(F.BranchLength.y) - Num(F.BranchLength.x)) * Hash01(n, 20 + j, hs);
                    double th = Radians(Num(F.BranchAngle.x) + (Num(F.BranchAngle.y) - Num(F.BranchAngle.x)) * Hash01(n, 30 + j, hs));
                    double tn = Math.Tan(th) * (Hash01(n, 40 + j, hs) > 0.5 ? 1.0 : -1.0);
                    R.BAt[j] = qj; R.BLen[j] = lq; R.BTan[j] = tn; R.BCos[j] = Math.Cos(th);
                    reachB = Math.Max(reachB, Math.Abs(tn) * lq * Rules.BranchReachWidth);
                    qmin = Math.Min(qmin, qj - lq);
                }
                R.Reach = R.Amp + R.Width + Rules.RunnelReachPad + reachB;
                R.QMin = qmin - Rules.RunnelStartMargin;
                o.Add(R);
                n++;
            }
        }

        void SouthFlats(double[] B)
        {
            var F = _plan.Flats;
            _fw = new double[_n]; _fdt = Filled(double.PositiveInfinity); _frun = new bool[_n]; _frib = new bool[_n];
            if (F == null) return;
            double x0 = Num(F.Window.x), y0 = Num(F.Window.y), x1 = Num(F.Window.z), y1 = Num(F.Window.w);
            if (!Win(x0, y0, x1, y1, out int r0, out int r1, out int c0, out int c1)) return;
            var wk = Num(F.Width); var wu = new double[wk.Length]; var ww = new double[wk.Length];
            for (int j = 0; j < wk.Length; j++) { wu[j] = wk[j].X; ww[j] = wk[j].Y; }
            double wa = Num(_plan.Warp2.x), wl = Num(_plan.Warp2.y);
            double lobeA = Num(F.Lobes.x), lobeL = Num(F.Lobes.y), inner = Num(F.Inner), outer = Num(F.Outer);
            double ovA = Num(F.OuterVar.x), ovL = Num(F.OuterVar.y), swA = Num(F.Swell.x), swL = Num(F.Swell.y);
            double ra = Num(F.Ridges.x), rl = Num(F.Ridges.y), rpA = Num(F.Ripple.x), rpL = Num(F.Ripple.y);
            double ribS = Num(F.RibSpacing), ribW = Num(F.RibWidth), ribL = Num(F.RibLift), ribB = Num(F.RibBreakUp), ribWa = Num(F.RibWander);
            double m0 = Num(F.Moat.x), m1 = Num(F.Moat.y), rm0 = Num(F.ReefMoat.x), rm1 = Num(F.ReefMoat.y), front = Num(F.Front);
            double bw = Num(F.BranchWidth), bd = Num(F.BranchDepth), meL = Num(F.RunnelMeander.y);
            var runnels = FlatsRunnels(F, _shore2.Length);
            var ribs = new bool[_south.Length]; var reefs = new bool[_south.Length];
            for (int k = 0; k < _south.Length; k++) { ribs[k] = _south[k].Flats == "ribs"; reefs[k] = _south[k].Mode == CoastSectionMode.Toe && _south[k].Type == S2ToeReef; }
            int hint = -1; var hints = new int[_toeSecs.Length];
            for (int j = 0; j < hints.Length; j++) hints[j] = -1;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * _w + c;
                double X = _xs[c], Y = _ys[r], Bv = B[i];
                double Xw = X + wa * SNoise(X, Y, _seed2 + Rules.SeedWarpX, wl), Yw = Y + wa * SNoise(X, Y, _seed2 + Rules.SeedWarpY, wl);
                _shore2.Query(Xw, Yw, ref hint, out double dist, out double sgn, out double t);
                double s = -sgn * dist;                                                 // + seaward of the 0 m line
                double u = _src.Bearing(Xw, Yw);
                double W0 = Interp(u, wu, ww);
                double W = W0 + lobeA * SNoise(X, Y, _seed2 + Rules.SeedFlatsLobe, lobeL) * Ss(W0 / Rules.FlatsLobeRamp);
                W = Math.Max(W, 1e-3);
                double q = s / W;
                double zo = outer + ovA * SNoise(X, Y, _seed2 + Rules.SeedFlatsOuter, ovL);
                double z = inner + (zo - inner) * Clip(q, 0, 1);
                z = z + swA * SNoise(X, Y, _seed2 + Rules.SeedFlatsSwell, swL);
                double mid = Ss(q / Rules.FlatsRidgeIn) * (1 - Ss((q - Rules.FlatsRidgeOutFrom) / Rules.FlatsRidgeOut));
                double brk = Ss((SNoise(X, Y, _seed2 + Rules.SeedFlatsRidgeBreak, Rules.FlatsRidgeBreakLambda) + Rules.FlatsRidgeBreakShift) / Rules.FlatsRidgeBreakSpan);
                z = z + ra * Math.Cos(2 * Math.PI * s / rl + Rules.FlatsRidgePhase * SNoise(X, Y, _seed2 + Rules.SeedFlatsRidgePhase, Rules.FlatsRidgePhaseLambda)) * brk * mid;
                z = z + rpA * SNoise(X, Y, _seed2 + Rules.SeedFlatsRipple, rpL, 1);
                int ch = _secidx2[i];
                bool ribsec = ch >= 0 && ribs[ch];
                double ph = s / ribS + ribWa * SNoise(X, Y, _seed2 + Rules.SeedRibWander, Rules.RibWanderLambda);
                double off = Math.Abs(ph - RoundHalfEven(ph));
                bool rib = ribsec && off < ribW && Fbm(X, Y, _seed2 + Rules.SeedRibBreak, Rules.RibBreakLambda) > ribB && q > Rules.RibFrom && q < Rules.RibTo;
                if (rib) z = z + ribL * (1 - off / ribW);
                double cut = 0.0; bool run = false;
                foreach (var R in runnels)
                {
                    if (!(Math.Abs(t - R.At) < R.Reach && q > Math.Max(R.QMin, Rules.RunnelFirstQ) && q < Rules.RunnelLastQ)) continue;
                    double qc = Clip(q, 0, 1);
                    double d = Math.Abs(t - Meander(R, s, meL));
                    double hw = R.Width * (Rules.RunnelHalfFrom + Rules.RunnelHalfGrowth * qc) / 2;
                    double k = (1 - Ss(d / hw)) * R.Depth * (Rules.RunnelDepthFrom + Rules.RunnelDepthGrowth * qc) * Ss((q - R.Q0) / Rules.RunnelFadeIn);
                    bool on = d < hw && q > R.Q0;
                    for (int j = 0; j < R.BAt.Length; j++)
                    {
                        double sj = R.BAt[j] * W;
                        double tb = Meander(R, sj, meL) + R.BTan[j] * (sj - s)
                                    + Rules.BranchWander * SNoise(s, Rules.BranchLane * R.N + Rules.BranchLaneStep * j, _seed2 + Rules.SeedFlatsBranch, Rules.BranchWanderLambda);
                        double db = Math.Abs(t - tb) * R.BCos[j], hb = R.Width * bw / 2;
                        double al = (R.BAt[j] - q) / R.BLen[j];
                        bool inb = al >= 0 && al <= 1 && q > Rules.BranchFirstQ;
                        double kb = inb ? (1 - Ss(db / hb)) * R.Depth * bd * (Rules.BranchDepthFrom + Rules.BranchDepthGrowth * (1 - al)) * Ss((1 - al) / Rules.BranchFadeOut) : 0.0;
                        k = Math.Max(k, kb);
                        on |= inb && db < hb && al < Rules.BranchMarkTo;
                    }
                    cut = Math.Max(cut, k); run |= on;
                }
                z = z - cut;
                double dt = ToeDistance(X, Y, hints, out _);
                double wm = Ss((dt - m0) / (m1 - m0));
                double wr = ch >= 0 && reefs[ch] ? Ss((dt - rm0) / (rm1 - rm0)) : 1.0;
                double wf = 1 - Ss((s - W) / front);
                double w = wm * wr * wf * Ss(W0 / Rules.FlatsWidthRamp) * Ss((s + Rules.FlatsShoreIn) / Rules.FlatsShoreSpan);
                _fw[i] = w * (z > Bv ? 1.0 : 0.0);
                _frun[i] = run && w > Rules.RunnelMarked;
                _frib[i] = rib && w > Rules.RunnelMarked;
                _fdt[i] = dt;
            }
        }

        /// <summary>A runnel's centre (arc metres) at s metres seaward: it meanders on its own lane of noise.</summary>
        double Meander(FlatsRunnel R, double s, double lam) =>
            R.At + R.Amp * SNoise(s, Rules.RunnelMeanderLane * R.N + Rules.RunnelMeanderLaneOff, _seed2 + Rules.SeedFlatsMeander, lam);

        // ---- the forms' feet and the creeks' beds -------------------------------------------------------------------------

        void SouthFeet()
        {
            _fmask = new bool[_n];
            var forms = _plan.Forms2 ?? new FormDef[0];
            for (int n = 0; n < forms.Length; n++)
            {
                var f = forms[n];
                if (f == null || f.Feet == null) continue;
                for (int j = 0; j < f.Feet.Length; j++)
                {
                    double cx = Num(f.Feet[j].x), cy = Num(f.Feet[j].y), rr = Num(f.Feet[j].z), R = rr + Rules.FootReach;
                    if (!Win(cx - R, cy - R, cx + R, cy + R, out int r0, out int r1, out int c0, out int c1)) continue;
                    long seed = _seed2 + Rules.SeedFoot + Rules.SeedFootStep * n + j;
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        double X = _xs[c], Y = _ys[r];
                        double d = Hypot(X - cx, Y - cy) * (1 + Rules.FootWobble * SNoise(X, Y, seed, Rules.FootWobbleLambda));
                        if (d < rr) _fmask[r * _w + c] = true;
                    }
                }
            }
        }

        void SouthCreeks()
        {
            _chan2 = (bool[])_chan.Clone(); _tidal2 = (bool[])_tidal.Clone(); _bank2 = (double[])_bank.Clone();
            var creeks = _plan.Creeks2 ?? new TidalCreekDef[0];
            foreach (var cr in creeks)
            {
                if (cr == null) continue;
                var pts = Num(cr.Points); var widths = Num(cr.Widths);
                var g = Catmull(pts, _lineStep);
                double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                foreach (var p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
                double reach = Rules.CreekWindow;
                if (!Win(x0 - reach, y0 - reach, x1 + reach, y1 + reach, out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = -1;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    g.Query(_xs[c], _ys[r], ref hint, out double d, out _, out double u);
                    double hw = Interp(u, g.Knots, widths) / 2.0;
                    if (d <= hw + Rules.ChannelEdge) { _chan2[i] = true; _tidal2[i] = true; }
                    _bank2[i] = Math.Min(_bank2[i], Math.Max(d - hw, 0));
                }
            }
        }

        // ---- the paint ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// The south's masks and paint, into <paramref name="z"/> (part 1's paint): the sections (recipe, then features), the
        /// flats, the forms' feet. <paramref name="P"/> is the ground the paint reads; <paramref name="E1"/> part 1's.
        /// </summary>
        void South(byte[] z, double[] P, double[] E1, double[] ke)
        {
            _southZone = new byte[_n];
            for (int i = 0; i < _n; i++) _southZone[i] = TerrainPlanZones.Unpainted;
            _southFlats = new bool[_n];
            if (!HasSouth) return;
            SouthLines();
            var Ec = SouthCoast(E1);
            // the flats sit on part 1's ground with the coast step laid, where the protected set (and the walls) let it move
            var B = new double[_n];
            for (int i = 0; i < _n; i++) B[i] = E1[i] + ke[i] * (Ec[i] - E1[i]);
            SouthFlats(B);
            for (int i = 0; i < _n; i++) _fw[i] *= ke[i];
            SouthFeet();
            SouthCreeks();

            double dominance = Num(_plan.SectionDominance), floor = Num(_plan.SectionPaintFloor), ceiling = Num(_plan.SectionPaintCeiling);
            double moved = Num(_plan.SectionPaintMoved);
            var bays = _r.BayPaint;
            var F = _plan.Flats;
            RecipeTable sand = F != null ? new RecipeTable(F.SandRecipe) : null, mud = F != null ? new RecipeTable(F.MudRecipe) : null;
            RecipeTable ribR = F != null ? new RecipeTable(F.RibRecipe) : null;
            var mudSec = new bool[_south.Length]; var boulderSec = new bool[_south.Length];
            for (int k = 0; k < _south.Length; k++) { mudSec[k] = _south[k].Flats == "mud"; boulderSec[k] = _south[k].Flats == "boulder"; }
            var creekWin = CreekWindows();
            var footRecipe = FootRecipes(out var footOf);
            int sections = 0, flats = 0, feet = 0;
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                if (bays != null && bays[i] != TerrainPlanZones.Unpainted) continue;   // a ground ask's bay keeps its own bands
                double X = _xs[c], Y = _ys[r], e = P[i];
                byte zz = z[i]; bool painted = false;
                int dom = _secw2[i] > dominance ? _secidx2[i] : -1;
                if (dom >= 0 && _south[dom].Mode != CoastSectionMode.Keep)
                {
                    var t = _south[dom]; bool toe = t.Mode == CoastSectionMode.Toe;
                    bool m = e > (toe ? Rules.SouthToeFloor : floor) && (e < ceiling || Math.Abs(e - E1[i]) > moved);
                    if (toe) m = m && (_sto[i] < Rules.SouthToeReach || e > floor);
                    if (m)
                    {
                        zz = Features2(t.Type, RecipeZone(t.Recipe, e, X, Y, _seed2 + Rules.SeedSection + dom), e, X, Y, i, dom);
                        painted = true; sections++;
                    }
                }
                if (F != null && _fw[i] > Rules.FlatsMarked && e < Rules.FlatsPaintBelow && e > Rules.FlatsPaintAbove)
                {
                    zz = FlatsPaint(sand, mud, ribR, mudSec, boulderSec, creekWin, e, X, Y, i);
                    painted = true; flats++;
                    _southFlats[i] = true;
                }
                if (_fmask[i])
                {
                    zz = RecipeZone(footRecipe[footOf[i]], e, X, Y, _seed2 + Rules.SeedFormRecipe);
                    painted = true; feet++;
                    _southFlats[i] = false;
                }
                if (!painted) continue;
                z[i] = zz; _southZone[i] = zz;
            }
            _r.Note("south.section_cells", sections);
            _r.Note("south.flats_cells", flats);
            _r.Note("south.feet_cells", feet);
            _r.South = new TerrainPlanSouth
            {
                Secw = _secw2, SecIdx = _secidx2, Sd = _sd2, Sto = _sto, Spit = _spm, ReefW = _rw,
                FlatsW = _fw, FlatsRunnel = _frun, FlatsRib = _frib, FlatsToe = _fdt, Feet = _fmask,
                Chan = _chan2, Tidal = _tidal2, Bank = _bank2, Zone = _southZone,
            };
            _chan = _chan2; _tidal = _tidal2; _bank = _bank2;                     // part 2's creeks are the region's water from here on
            Log("south", "part 2's south (PR 5w): " + _south.Length + " sections, " + _toeSecs.Length + " on their walls' toes; " + sections +
                         " cells by section, " + flats + " on the flats, " + feet + " under the forms' feet");
        }

        /// <summary>Each cell's form foot recipe (the last foot to cover it wins, as part 2's mask is one recipe for all).</summary>
        RecipeTable[] FootRecipes(out int[] footOf)
        {
            footOf = new int[_n];
            var forms = _plan.Forms2 ?? new FormDef[0];
            var o = new RecipeTable[Math.Max(1, forms.Length)];
            for (int n = 0; n < forms.Length; n++) if (forms[n] != null && forms[n].Recipe != null) o[n] = new RecipeTable(forms[n].Recipe);
            if (forms.Length == 0) return o;
            for (int n = 0; n < forms.Length; n++)
            {
                if (o[n] == null) throw new InvalidOperationException("[TerrainPlan] " + forms[n]?.Id + " has no recipe for its feet.");
                if (n > 0 && !ReferenceEquals(forms[n].Recipe, forms[0].Recipe))
                    throw new InvalidOperationException("[TerrainPlan] part 2's forms paint their feet with one recipe; " + forms[n].Id + " names another.");
            }
            return o;
        }

        /// <summary>The creeks' paint windows: each creek's points' box plus CreekPaintReach (part 2's cr).</summary>
        bool[] CreekWindows()
        {
            var o = new bool[_n];
            foreach (var cr in _plan.Creeks2 ?? new TidalCreekDef[0])
            {
                if (cr == null) continue;
                var pts = Num(cr.Points);
                double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                foreach (var p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
                double R = Rules.CreekPaintReach;
                if (!Win(x0 - R, y0 - R, x1 + R, y1 + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++) for (int c = c0; c < c1; c++) o[r * _w + c] = true;
            }
            return o;
        }

        /// <summary>What a part 2 section type carries at map scale (part 2's features2), over its recipe's zone.</summary>
        byte Features2(int type, byte z, double E, double X, double Y, int i, int k)
        {
            double f1 = Fbm(X, Y, _seed2 + Rules.SeedFeature1 + k, Rules.Feature1Lambda);
            double f2 = Fbm(X, Y, _seed2 + Rules.SeedFeature2 + k, Rules.Feature2Lambda);
            double sto = _sto[i], rw = _rw[i];
            switch (type)
            {
                case S2Strand:
                {
                    bool run = Math.Abs(_sd2[i] + Rules.StrandRunnelOffset + Rules.StrandRunnelWander * SNoise(X, Y, _seed2 + Rules.SeedStrandRunnel, Rules.StrandRunnelLambda)) < Rules.StrandRunnelHalf;
                    if (run && E < Rules.StrandSiltTo && E > Rules.StrandSiltFrom) z = ZSilt;
                    if (E > Rules.StrandTalusFrom && E < Rules.StrandTalusTo && f2 > Rules.StrandTalusAbove) z = ZTalus;
                    break;
                }
                case S2Landing:
                    if (E > Rules.LandingWeedFrom && E < Rules.LandingWeedTo && f1 > Rules.LandingWeedAbove) z = ZRockweed;
                    if (E > Rules.LandingTalusFrom && E < Rules.LandingTalusTo && f2 > Rules.LandingTalusAbove) z = ZTalus;
                    break;
                case S2DeepCliff:
                {
                    bool apron = sto < Rules.ApronReach + Rules.ApronWander * SNoise(X, Y, _seed2 + Rules.SeedApron, Rules.ApronLambda);
                    if (E < Rules.ApronRippleBelow && !apron) z = ZRipple;
                    if (apron && E < Rules.ApronTalusBelow) z = ZTalus;
                    break;
                }
                case S2ToeReef:
                    if (sto < Rules.ReefToeTalus && E < Rules.ReefToeTalusBelow && f2 > Rules.ReefToeTalusAbove) z = ZTalus;
                    if (rw > Rules.ReefCrestTalusW && E < Rules.ReefCrestTalusTo && E > Rules.ReefCrestTalusFrom && f1 > Rules.ReefCrestTalusAbove) z = ZTalus;
                    if (rw < Rules.ReefEelgrassW && sto > Rules.ReefEelgrassOff && E < Rules.ReefEelgrassBelow) z = ZEelgrass;
                    break;
                case S2GapCove:
                    if (_spm[i] && E > Rules.SpitShingleFrom && E < Rules.SpitShingleTo) z = ZShingle;
                    if (_spm[i] && E > Rules.SpitWeedFrom && E <= Rules.SpitShingleFrom) z = ZRockweed;
                    if (E > Rules.CoveTalusFrom && E < Rules.CoveTalusTo && f2 > Rules.CoveTalusAbove && !_spm[i]) z = ZTalus;
                    break;
                case S2Storm:
                    if (E > Rules.StormWeedFrom && E < Rules.StormWeedTo && f1 > Rules.StormWeedAbove) z = ZRockweed;
                    if (E > Rules.StormTalusFrom && E < Rules.StormTalusTo && f2 > Rules.StormTalusAbove) z = ZTalus;
                    break;
            }
            return z;
        }

        /// <summary>The flats' paint at a cell their mask holds (part 2's flats_paint, less its still water: see <see cref="SouthStillPaint"/>).</summary>
        byte FlatsPaint(RecipeTable sand, RecipeTable mud, RecipeTable ribR, bool[] mudSec, bool[] boulderSec, bool[] creekWin,
                       double E, double X, double Y, int i)
        {
            var F = _plan.Flats;
            int ch = _secidx2[i];
            bool isMud = ch >= 0 && mudSec[ch], isBoulder = ch >= 0 && boulderSec[ch];
            byte zz = isMud ? RecipeZone(mud, E, X, Y, _seed2 + Rules.SeedFlatsMud) : RecipeZone(sand, E, X, Y, _seed2 + Rules.SeedFlatsSand);
            double f1 = Fbm(X, Y, _seed2 + Rules.SeedFlatsF1, Rules.FlatsF1Lambda), f2 = Fbm(X, Y, _seed2 + Rules.SeedFlatsF2, Rules.FlatsF2Lambda);
            double fm = Fbm(X, Y, _seed2 + Rules.SeedFlatsMussel, Num(F.MusselLambda));
            if (E > Num(F.MusselBand.x) && E < Num(F.MusselBand.y) && fm > Num(F.MusselCover) && f1 > Rules.FlatsMusselF1) zz = ZMusselbed;
            if (isMud && E < Rules.FlatsMudSiltTo && E > Rules.FlatsMudSiltFrom && f2 > Rules.FlatsMudSiltAbove) zz = ZSilt;
            if (_frun[i] && E < Rules.FlatsRunnelSiltBelow) zz = ZSilt;
            if (isBoulder && f2 > Rules.FlatsBoulderAbove && E > Rules.FlatsBoulderFrom) zz = ZTalus;
            if (_frib[i]) zz = RecipeZone(ribR, E, X, Y, _seed2 + Rules.SeedFlatsRib);
            if (_fdt[i] < Rules.FlatsToeTalus + Rules.FlatsToeTalusWander * SNoise(X, Y, _seed2 + Rules.SeedFlatsToeTalus, Rules.FlatsToeTalusLambda)
                && E > Rules.FlatsToeTalusFrom) zz = ZTalus;
            double bank = _bank2[i];
            if (creekWin[i] && !double.IsInfinity(bank) && !double.IsNaN(bank) && bank < Rules.CreekMudBank && !_chan2[i] && E > Rules.CreekMudFrom) zz = ZMud;
            if (creekWin[i] && _chan2[i]) zz = ZSilt;
            return zz;
        }

        /// <summary>
        /// Part 2's ponds' and brooks' own ground again, over the south's paint, inside their windows (part 2's fresh rule):
        /// mud at a fresh edge and sedge beyond, silt beds, tidal beds mud.
        /// </summary>
        void SouthFresh(byte[] z, double[] P)
        {
            if (!HasSouth) return;
            var nw = new bool[_n];
            foreach (var p in _plan.FreshPonds2 ?? new PondDef[0])
            {
                if (p == null) continue;
                var cc = Num(p.Centre);
                double R = Math.Max(Num(p.Radii.x), Num(p.Radii.y)) * Rules.PondReach + Num(p.Basin.x);
                if (!Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++) for (int c = c0; c < c1; c++) nw[r * _w + c] = true;
            }
            foreach (var st in _plan.FreshStreams2 ?? new StreamDef[0])
            {
                if (st == null) continue;
                var pts = Num(st.Points);
                double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                foreach (var q in pts) { x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
                double R = Rules.StreamWindow;
                if (!Win(x0 - R, y0 - R, x1 + R, y1 + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++) for (int c = c0; c < c1; c++) nw[r * _w + c] = true;
            }
            var bays = _r.BayPaint;
            int n = 0;
            for (int r = 0; r < _h; r++)
            for (int c = 0; c < _w; c++)
            {
                int i = r * _w + c;
                if (!nw[i] || (bays != null && bays[i] != TerrainPlanZones.Unpainted)) continue;
                double e = P[i], bank = _bank2[i];
                byte before = z[i];
                bool fresh = !double.IsInfinity(bank) && !double.IsNaN(bank) && e > Rules.FreshAbove && bank < Rules.FreshBankCap;
                if (fresh && bank < Rules.FreshMud) z[i] = ZMud;
                if (fresh && bank >= Rules.FreshMud && z[i] == ZGrass
                    && bank < Rules.FreshSedge + Rules.FreshSedgeNoise * Fbm(_xs[c], _ys[r], _seed2 + Rules.SeedFresh, Rules.FreshLambda)) z[i] = ZSedge;
                if (_chan2[i] && !_tidal2[i]) z[i] = ZSilt;
                if (_chan2[i] && _tidal2[i] && e > Rules.TidalMudAbove) z[i] = ZMud;
                if (z[i] != before) { _southZone[i] = z[i]; n++; }
            }
            _r.Note("south.fresh_cells", n);
        }

        /// <summary>
        /// The south's paint that stands on the still water (run once the water is laid): silt where water stands on the
        /// flats (part 2's flats_paint) and Irish moss in the reef's rock pools, both only where the south's own paint
        /// still holds. The reef's pools are its crest band's water, wherever the ground holds some (part 2 placed them
        /// by its own toe lines; the ground file carries their dishes).
        /// </summary>
        void SouthStillPaint()
        {
            if (!HasSouth || _southZone == null) return;
            var reef = _plan.Reef ?? new TerrainPlanReef();
            double c0 = Num(reef.Crest.x), c1 = Num(reef.Crest.y), dominance = Num(_plan.SectionDominance);
            int silt = 0, moss = 0;
            for (int i = 0; i < _n; i++)
            {
                if (_southZone[i] == TerrainPlanZones.Unpainted || _zone[i] != _southZone[i]) continue;
                double s = _still[i];
                if (double.IsNaN(s) || double.IsInfinity(s) || !(s > _E[i]) || _chan2[i]) continue;
                if (_southFlats[i]) { _zone[i] = ZSilt; silt++; continue; }
                int dom = _secw2[i] > dominance ? _secidx2[i] : -1;
                if (dom >= 0 && _south[dom].Type == S2ToeReef && _south[dom].Mode == CoastSectionMode.Toe && _sto[i] >= c0 && _sto[i] <= c1)
                {
                    _zone[i] = ZIrishmoss; moss++;
                }
            }
            _r.Note("south.flats_still_cells", silt);
            _r.Note("south.rock_pool_cells", moss);
        }
    }

    /// <summary>The south's fields (terrain PR 5w), for the guards and the plates' captions.</summary>
    public sealed class TerrainPlanSouth
    {
        public double[] Secw, Sd, Sto, ReefW, FlatsW, FlatsToe, Bank;
        public short[] SecIdx;
        public bool[] Spit, FlatsRunnel, FlatsRib, Feet, Chan, Tidal;
        /// <summary>What the south painted (TerrainPlanZones.Unpainted where it painted nothing).</summary>
        public byte[] Zone;
    }
}
