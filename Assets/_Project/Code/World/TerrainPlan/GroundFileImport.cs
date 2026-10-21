using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using static HiddenHarbours.World.TerrainPlanMath;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE GROUND FILE, IMPORTED.</b> A region's heights from its <see cref="GroundFileDef"/>: the base decoded at its
    /// texel centres, each patch ask laid in order ("E(x, y) is the last ask whose patch covers (x, y), bilinear between
    /// its samples; elsewhere the base"), then the game's own asks, and the result encoded to the R16 height map's codes
    /// at the plan's range (<see cref="TerrainPlanMaps.Codes"/>, the paint tool's rule).
    ///
    /// <para>Pure, so a headless run imports exactly what the editor imports: no texture, no engine call. The base's
    /// samples are checked against the file's <c>pixelsSha256</c> (big-endian, rows top-down) and each patch's against
    /// its own hash; a mismatch STOPs the import. The base is decoded in the file's order of operations
    /// (lo + (hi − lo) × code / 65535), and a patch is read as the file's evaluator reads it (CD's <c>sampleP</c>): the
    /// fraction is clamped into the last cell, so a point on the box's north or east edge reads the edge sample.</para>
    ///
    /// <para>The game's asks: <see cref="GroundAskKind.ShoreBlend"/> raises a beach's toe to the flats' level along its
    /// shore, never lowering (fix 2); <see cref="GroundAskKind.ChannelHold"/> holds a channel's bed below the spring low
    /// along a wall's toe, never raising (fix 3); <see cref="GroundAskKind.HollowFill"/> fills a closed hollow to its spill,
    /// never cutting (fix 4). A <see cref="GroundAskKind.Rule"/> lays nothing: its guard holds it. Each ask's record keeps its
    /// reach, and a game ask's keeps the cells it changed with their heights before it, for the guards.</para>
    /// </summary>
    public static class GroundFileImport
    {
        /// <summary>A 16-bit map's largest code.</summary>
        public const int CodeCount = 65535;

        /// <summary>How far a patch's box may sit off its step's grid (units) before the import refuses it.</summary>
        const double BoxTolerance = 1e-9;

        /// <summary>The base: a 16-bit height texture over a rect, as the file holds it.</summary>
        public sealed class BaseMap
        {
            public int Width, Height;
            /// <summary>The range (m): height = Lo + (Hi − Lo) × code / 65535.</summary>
            public double Lo, Hi;
            /// <summary>The rect (world units): west, south, east, north.</summary>
            public double X0, Y0, X1, Y1;
            /// <summary>The samples, the file's order: row 0 at the rect's north edge.</summary>
            public ushort[] Codes;
            /// <summary>The file's <c>pixelsSha256</c>.</summary>
            public string PixelsSha256 = "";
        }

        /// <summary>One live ask in the import's numbers: a <see cref="GroundAskDef"/> read through <see cref="AsksOf"/>.</summary>
        public sealed class Ask
        {
            public string Id = "";
            public GroundAskSource Source;
            public GroundAskKind Kind;

            // a patch
            public double BoxX0, BoxY0, BoxX1, BoxY1, Step, Lo, Hi;
            public int Width, Height;
            /// <summary>The samples: row 0 at the box's south edge, column 0 at its west edge.</summary>
            public ushort[] Codes;
            public string PatchSha256 = "";

            // a line: the shore (a blend), a wall's toe (a hold)
            public PlanPoint[] Line = new PlanPoint[0];

            // a shore blend
            public double WinX0, WinY0, WinX1, WinY1, YScaleDeg, SampleStep, SampleRunOut, Softness, Sigma, KernelSigmas;
            public double AlongIn, AlongInOver, AlongOut, AlongOutOver, SeaFade, SeaFadeOver, LandFade, LandFadeOver, SeaZ, SeaTail;

            // a channel hold
            public double Margin, FootprintStep;

            // a rule's circle; a hollow fill's seed, its level, and the circle it keeps out of (reach −1: none)
            public double CentreX, CentreY, Reach;
            public double Level, LevelTolerance;
            public double KeepX, KeepY, KeepReach = -1;
        }

        /// <summary>What one ask did: the cells it laid (a patch) or changed (the game's), and its largest raise and cut.</summary>
        public sealed class AskRecord
        {
            public string Id = "";
            public GroundAskKind Kind;
            public int Cells;
            public double MaxRaise, MaxCut;
            public int MaxRaiseAt = -1, MaxCutAt = -1;
            /// <summary>A hollow fill: its basin's cells inside the kept circle, left as they were, and the level it filled to.</summary>
            public int Held;
            public double Level = double.NaN;
            /// <summary>
            /// Its reach (world units): a patch's box, a blend's or a fill's window, a hold's line and the texel its read
            /// touches each side, a rule's circle's square. Every cell it lays or changes lies inside it.
            /// </summary>
            public double X0, Y0, X1, Y1;
            /// <summary>A game ask's changed cells (plan order, sorted) and each one's height just before it (m).</summary>
            public int[] Changed = new int[0];
            public double[] Before = new double[0];
        }

        /// <summary>An import: the heights at the texel centres (plan order, row 0 north), who laid each, and the codes.</summary>
        public sealed class Result
        {
            public TerrainPlanGrid Grid;
            /// <summary>The base's rect (world units): the frame of <see cref="BaseAt"/>.</summary>
            public double X0, Y0, X1, Y1;
            public double[] Base;
            public double[] E;
            /// <summary>The index (into the asks) of the ask that laid or last changed a cell; −1 the base.</summary>
            public int[] Owner;
            public readonly List<AskRecord> Asks = new List<AskRecord>();
            public string BasePixelsSha256 = "";
            /// <summary>The R16 codes, Unity order (row 0 south), and their SHA-256 as little-endian bytes.</summary>
            public ushort[] Codes;
            public string CodesSha256 = "";

            /// <summary>The base read bilinearly at a point, as the file reads it (clamped at the rect's edges).</summary>
            public double BaseAt(double x, double y) => Bilinear(Base, x, y);

            /// <summary>A plan-order raster on the base's grid read bilinearly at a point (PaintedHeightField's frame, clamped).</summary>
            public double Bilinear(double[] a, double x, double y)
            {
                int w = Grid.W, h = Grid.H;
                double fx = (x - X0) / (X1 - X0) * w - 0.5, fy = (Y1 - y) / (Y1 - Y0) * h - 0.5;
                int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy);
                double tx = fx - i, ty = fy - j;
                int i0 = Math.Min(Math.Max(i, 0), w - 1), i1 = Math.Min(Math.Max(i + 1, 0), w - 1);
                int j0 = Math.Min(Math.Max(j, 0), h - 1), j1 = Math.Min(Math.Max(j + 1, 0), h - 1);
                double p = a[j0 * w + i0] + (a[j0 * w + i1] - a[j0 * w + i0]) * tx;
                double q = a[j1 * w + i0] + (a[j1 * w + i1] - a[j1 * w + i0]) * tx;
                return p + (q - p) * ty;
            }
        }

        // ---- the Defs, read -------------------------------------------------------------------------------------------

        /// <summary>The file's base, its samples read by the caller (file order, row 0 north).</summary>
        public static BaseMap BaseOf(GroundFileDef f, ushort[] codes, int width, int height)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            return new BaseMap
            {
                Width = width, Height = height, Codes = codes, PixelsSha256 = f.BasePixelsSha256 ?? "",
                Lo = Num(f.BaseRange.x), Hi = Num(f.BaseRange.y),
                X0 = Num(f.RectMin.x), Y0 = Num(f.RectMin.y), X1 = Num(f.RectMax.x), Y1 = Num(f.RectMax.y),
            };
        }

        /// <summary>
        /// The file's live asks in order, each patch's samples read by <paramref name="patchOf"/> (row 0 at the box's south
        /// edge). A retired ask is skipped; a line named by <see cref="GroundAskDef.LineOf"/> is read from that ask, and the
        /// circle named by <see cref="GroundAskDef.KeepOutOf"/> from that rule.
        /// </summary>
        public static List<Ask> AsksOf(GroundFileDef f, Func<GroundAskDef, ushort[]> patchOf)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            var byId = new Dictionary<string, GroundAskDef>(StringComparer.Ordinal);
            foreach (var d in f.Asks ?? new GroundAskDef[0])
            {
                if (d == null) throw new InvalidDataException("[GroundFileImport] " + f.Id + " lists an empty ask.");
                if (byId.ContainsKey(d.Id)) throw new InvalidDataException("[GroundFileImport] " + f.Id + " lists " + d.Id + " twice.");
                byId[d.Id] = d;
            }
            var o = new List<Ask>();
            foreach (var d in f.Asks ?? new GroundAskDef[0])
            {
                if (d.Retired) continue;
                var line = d.Line;
                if (!string.IsNullOrEmpty(d.LineOf))
                {
                    if (!byId.TryGetValue(d.LineOf, out var of)) throw new InvalidDataException("[GroundFileImport] " + d.Id + " reads the line of " + d.LineOf + ", which the file does not list.");
                    line = of.Line;
                }
                GroundAskDef keep = null;
                if (!string.IsNullOrEmpty(d.KeepOutOf))
                {
                    if (!byId.TryGetValue(d.KeepOutOf, out keep)) throw new InvalidDataException("[GroundFileImport] " + d.Id + " keeps out of " + d.KeepOutOf + ", which the file does not list.");
                    if (keep.Kind != GroundAskKind.Rule || !(keep.Reach > 0)) throw new InvalidDataException("[GroundFileImport] " + d.Id + " keeps out of " + d.KeepOutOf + ", which is not a rule with a circle.");
                }
                o.Add(AskOf(d, d.Kind == GroundAskKind.Patch ? patchOf(d) : null, line, keep));
            }
            return o;
        }

        /// <summary>One ask in the import's numbers: each float as the decimal it was authored as.</summary>
        public static Ask AskOf(GroundAskDef d, ushort[] patch, Vector2[] line, GroundAskDef keep = null)
        {
            var a = new Ask
            {
                Id = d.Id, Source = d.Source, Kind = d.Kind,
                BoxX0 = Num(d.BoxMin.x), BoxY0 = Num(d.BoxMin.y), BoxX1 = Num(d.BoxMax.x), BoxY1 = Num(d.BoxMax.y), Step = Num(d.Step),
                Lo = Num(d.PatchRange.x), Hi = Num(d.PatchRange.y), Width = d.Width, Height = d.Height, Codes = patch, PatchSha256 = d.PatchSha256 ?? "",
                WinX0 = Num(d.WindowMin.x), WinY0 = Num(d.WindowMin.y), WinX1 = Num(d.WindowMax.x), WinY1 = Num(d.WindowMax.y),
                YScaleDeg = Num(d.ShoreYScaleDeg), SampleStep = Num(d.SampleStep), SampleRunOut = Num(d.SampleRunOut), Softness = Num(d.Softness),
                Sigma = Num(d.Sigma), KernelSigmas = Num(d.KernelSigmas),
                AlongIn = Num(d.AlongIn.x), AlongInOver = Num(d.AlongIn.y), AlongOut = Num(d.AlongOut.x), AlongOutOver = Num(d.AlongOut.y),
                SeaFade = Num(d.SeaFade.x), SeaFadeOver = Num(d.SeaFade.y), LandFade = Num(d.LandFade.x), LandFadeOver = Num(d.LandFade.y),
                SeaZ = Num(d.SeaZ), SeaTail = Num(d.SeaTail), Margin = Num(d.Margin), FootprintStep = Num(d.FootprintStep),
                CentreX = Num(d.Centre.x), CentreY = Num(d.Centre.y), Reach = Num(d.Reach), Level = Num(d.Level), LevelTolerance = Num(d.LevelTolerance),
            };
            if (keep != null) { a.KeepX = Num(keep.Centre.x); a.KeepY = Num(keep.Centre.y); a.KeepReach = Num(keep.Reach); }
            line = line ?? new Vector2[0];
            a.Line = new PlanPoint[line.Length];
            for (int k = 0; k < line.Length; k++) a.Line[k] = Num(line[k]);
            return a;
        }

        // ---- the import ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Import the file: the base, then each ask in order. <paramref name="springLow"/> is the level a channel hold keeps
        /// its bed below (−the plan's SpringM); <paramref name="min"/> and <paramref name="max"/> are the height map's range.
        /// </summary>
        public static Result Run(BaseMap b, IList<Ask> asks, double springLow, float min, float max)
        {
            if (b == null || b.Codes == null || b.Width < 2 || b.Height < 2 || b.Codes.Length != b.Width * b.Height)
                throw new InvalidDataException("[GroundFileImport] the base's samples do not fill its size.");
            string pix = Sha256BigEndian(b.Codes);
            if (!string.Equals(pix, b.PixelsSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("[GroundFileImport] STOP: the base's pixels hash " + pix + "; the file says " + b.PixelsSha256 + ".");
            var g = GridOf(b);
            var r = new Result { Grid = g, X0 = b.X0, Y0 = b.Y0, X1 = b.X1, Y1 = b.Y1, BasePixelsSha256 = pix };
            r.Base = DecodeBase(b);
            r.E = (double[])r.Base.Clone();
            r.Owner = new int[g.Count];
            for (int i = 0; i < g.Count; i++) r.Owner[i] = -1;
            bool game = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < asks.Count; k++)
            {
                var a = asks[k];
                if (!seen.Add(a.Id)) throw new InvalidDataException("[GroundFileImport] " + a.Id + " is laid twice.");
                if (a.Source == GroundAskSource.Game) game = true;
                else if (game) throw new InvalidDataException("[GroundFileImport] " + a.Id + ": a package's ask after the game's own (the game's come last).");
                var rec = new AskRecord { Id = a.Id, Kind = a.Kind };
                var before = a.Source == GroundAskSource.Game ? (double[])r.E.Clone() : null;
                switch (a.Kind)
                {
                    case GroundAskKind.Patch: LayPatch(r, a, k, rec); Reach(rec, a.BoxX0, a.BoxY0, a.BoxX1, a.BoxY1); break;
                    case GroundAskKind.ShoreBlend: ShoreBlend(r, a, k, rec); Reach(rec, a.WinX0, a.WinY0, a.WinX1, a.WinY1); break;
                    case GroundAskKind.ChannelHold: ChannelHold(r, a, k, springLow, rec); ReachOfLine(rec, a.Line, g.Mpp); break;
                    case GroundAskKind.HollowFill: HollowFill(r, a, k, rec); Reach(rec, a.WinX0, a.WinY0, a.WinX1, a.WinY1); break;
                    case GroundAskKind.Rule: Reach(rec, a.CentreX - a.Reach, a.CentreY - a.Reach, a.CentreX + a.Reach, a.CentreY + a.Reach); break;
                    default: throw new InvalidDataException("[GroundFileImport] " + a.Id + ": no such kind " + a.Kind + ".");
                }
                if (before != null)
                {
                    var changed = new List<int>();
                    for (int i = 0; i < g.Count; i++) if (r.E[i] != before[i]) changed.Add(i);
                    rec.Changed = changed.ToArray();
                    rec.Before = new double[changed.Count];
                    for (int j = 0; j < changed.Count; j++) rec.Before[j] = before[changed[j]];
                }
                r.Asks.Add(rec);
            }
            r.Codes = TerrainPlanMaps.Codes(TerrainPlanMaps.HeightR01(g, r.E, min, max));
            r.CodesSha256 = TerrainPlanMaps.Sha256(r.Codes);
            return r;
        }

        /// <summary>The base's grid: its size and rect, texels square (the plan's grid when they agree).</summary>
        public static TerrainPlanGrid GridOf(BaseMap b)
        {
            double mx = (b.X1 - b.X0) / b.Width, my = (b.Y1 - b.Y0) / b.Height;
            if (!(mx > 0) || mx != my) throw new InvalidDataException("[GroundFileImport] the base's texels are not square (" + mx + " × " + my + " units).");
            return new TerrainPlanGrid(b.Width, b.Height, mx, b.X0, b.Y1);
        }

        /// <summary>The base's heights at its texel centres, plan order: lo + (hi − lo) × code / 65535, in that order.</summary>
        public static double[] DecodeBase(BaseMap b)
        {
            var e = new double[b.Codes.Length];
            for (int i = 0; i < e.Length; i++) e[i] = b.Lo + (b.Hi - b.Lo) * b.Codes[i] / 65535.0;
            return e;
        }

        /// <summary>A patch's heights, row 0 at the box's south edge: lo + (hi − lo) × code / 65535.</summary>
        public static double[] DecodePatch(Ask a)
        {
            var v = new double[a.Codes.Length];
            for (int i = 0; i < v.Length; i++) v[i] = a.Lo + (a.Hi - a.Lo) * a.Codes[i] / 65535.0;
            return v;
        }

        /// <summary>
        /// A patch at a point, as the file's evaluator reads it (CD's sampleP): false off the box; else the samples'
        /// bilinear, the cell clamped into the box so its north and east edges read their own samples.
        /// </summary>
        public static bool PatchAt(Ask a, double[] v, double x, double y, out double z)
        {
            double fx = (x - a.BoxX0) / a.Step, fy = (y - a.BoxY0) / a.Step;
            z = 0;
            if (!(fx >= 0 && fy >= 0 && fx <= a.Width - 1 && fy <= a.Height - 1)) return false;
            int ic = Math.Min(Math.Max((int)Math.Floor(fx), 0), a.Width - 2), jc = Math.Min(Math.Max((int)Math.Floor(fy), 0), a.Height - 2);
            double tx = fx - ic, ty = fy - jc;
            int i = jc * a.Width + ic;
            double v00 = v[i], v10 = v[i + 1], v01 = v[i + a.Width], v11 = v[i + a.Width + 1];
            double p = v00 + (v10 - v00) * tx, q = v01 + (v11 - v01) * tx;
            z = p + (q - p) * ty;
            return true;
        }

        static void LayPatch(Result r, Ask a, int k, AskRecord rec)
        {
            if (a.Codes == null || a.Width < 2 || a.Height < 2 || a.Codes.Length != a.Width * a.Height)
                throw new InvalidDataException("[GroundFileImport] " + a.Id + ": the patch's samples do not fill " + a.Width + " × " + a.Height + ".");
            if (!(a.Step > 0) || Math.Abs((a.BoxX1 - a.BoxX0) / a.Step + 1 - a.Width) > BoxTolerance || Math.Abs((a.BoxY1 - a.BoxY0) / a.Step + 1 - a.Height) > BoxTolerance)
                throw new InvalidDataException("[GroundFileImport] " + a.Id + ": its box and step do not make " + a.Width + " × " + a.Height + " samples.");
            if (!string.IsNullOrEmpty(a.PatchSha256))
            {
                string h = Sha256BigEndian(a.Codes);
                if (!string.Equals(h, a.PatchSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("[GroundFileImport] STOP: " + a.Id + "'s samples hash " + h + "; the ask says " + a.PatchSha256 + ".");
            }
            var v = DecodePatch(a);
            var g = r.Grid;
            if (!g.Win(a.BoxX0, a.BoxY0, a.BoxX1, a.BoxY1, out int r0, out int r1, out int c0, out int c1)) return;
            for (int row = r0; row < r1; row++)
            {
                double y = g.YG(row);
                for (int c = c0; c < c1; c++)
                {
                    if (!PatchAt(a, v, g.XG(c), y, out double z)) continue;
                    int i = row * g.W + c;
                    Track(rec, z - r.E[i], i);
                    r.E[i] = z;
                    r.Owner[i] = k;
                    rec.Cells++;
                }
            }
        }

        static void Reach(AskRecord rec, double x0, double y0, double x1, double y1)
        {
            rec.X0 = x0; rec.Y0 = y0; rec.X1 = x1; rec.Y1 = y1;
        }

        /// <summary>A line's reach: its box, and the texel its bilinear read touches each side.</summary>
        static void ReachOfLine(AskRecord rec, PlanPoint[] line, double mpp)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in line) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            Reach(rec, x0 - mpp, y0 - mpp, x1 + mpp, y1 + mpp);
        }

        static void Track(AskRecord rec, double d, int i)
        {
            if (d > rec.MaxRaise) { rec.MaxRaise = d; rec.MaxRaiseAt = i; }
            if (-d > rec.MaxCut) { rec.MaxCut = -d; rec.MaxCutAt = i; }
        }

        // ---- fix 2: the toe held at the flats' level ---------------------------------------------------------------------

        /// <summary>
        /// The beach's toe raised to the flats' level along its shore, in a band: the level is smax(0, the base, Softness)
        /// at each sample along the line, zero past its ends, smoothed along the line by a Gaussian; landward of the line the
        /// target is that level at the cell's nearest point, seaward it is the beach's own sea rule. The weight fades in and
        /// out along the line and across it; a cell past the line's ends is never touched. E ← E + w × max(0, target − E).
        /// </summary>
        static void ShoreBlend(Result r, Ask a, int k, AskRecord rec)
        {
            if (a.Line == null || a.Line.Length < 2) throw new InvalidDataException("[GroundFileImport] " + a.Id + ": a shore blend needs a line of two points or more.");
            if (!(a.SampleStep > 0) || !(a.Sigma > 0) || !(a.Softness > 0) || !(a.AlongInOver > 0) || !(a.AlongOutOver > 0) || !(a.SeaFadeOver > 0) || !(a.LandFadeOver > 0) || !(a.SeaTail > 0))
                throw new InvalidDataException("[GroundFileImport] " + a.Id + ": a shore blend's steps, sigma, softness, fades and tail must be positive.");
            var sh = new ShoreLine(a.Line, Math.Sin(a.YScaleDeg * Math.PI / 180.0));
            var level = new AlongLevel(r, sh, a);
            var g = r.Grid;
            int c0 = 0, c1 = 0, r0 = 0, r1 = 0;
            while (c0 < g.W && g.XG(c0) < a.WinX0) c0++;
            c1 = c0; while (c1 < g.W && g.XG(c1) < a.WinX1) c1++;
            while (r0 < g.H && g.YG(r0) > a.WinY1) r0++;
            r1 = r0; while (r1 < g.H && g.YG(r1) > a.WinY0) r1++;
            for (int row = r0; row < r1; row++)
            {
                double y = g.YG(row);
                for (int c = c0; c < c1; c++)
                {
                    int i = row * g.W + c;
                    sh.Near(g.XG(c), y, out double s, out double d, out double past);
                    double sm = s * sh.Length;
                    double w = Ss((sm - a.AlongIn) / a.AlongInOver) * (1 - Ss((sm - a.AlongOut) / a.AlongOutOver))
                               * Ss((d - a.SeaFade) / a.SeaFadeOver) * (1 - Ss((d - a.LandFade) / a.LandFadeOver));
                    if (past > 0) w = 0;
                    if (!(w > 0)) continue;
                    double tail = a.SeaZ * (1 - Math.Exp(Math.Min(d, 0) / a.SeaTail));
                    double t = d < 0 ? Smax(tail, r.Base[i], a.Softness) : level.At(sm);
                    double e = r.E[i], en = e + w * Math.Max(0, t - e);
                    if (en == e) continue;
                    Track(rec, en - e, i);
                    r.E[i] = en;
                    r.Owner[i] = k;
                    rec.Cells++;
                }
            }
        }

        /// <summary>The smooth maximum (CD's smax): max(a, b) rounded over a width of k.</summary>
        public static double Smax(double a, double b, double k)
        {
            double h = Clip(0.5 + 0.5 * (b - a) / k, 0, 1);
            return a + (b - a) * h + k * h * (1 - h);
        }

        /// <summary>
        /// A shore line in the beach's metric (CD's nearShore): y divided by sin 40°, so lengths along and across it are
        /// ground metres. The along fraction runs 0 to 1 by length; the distance is signed, inland positive.
        /// </summary>
        sealed class ShoreLine
        {
            readonly PlanPoint[] _p;
            readonly double[] _mx, _my, _l;
            readonly double _se;
            public readonly double[] SL;

            public ShoreLine(PlanPoint[] p, double se)
            {
                _p = p; _se = se;
                int n = p.Length;
                _mx = new double[n]; _my = new double[n]; _l = new double[n - 1]; SL = new double[n];
                for (int k = 0; k < n; k++) { _mx[k] = p[k].X; _my[k] = p[k].Y / se; }
                for (int k = 0; k + 1 < n; k++) { _l[k] = Hypot(_mx[k + 1] - _mx[k], _my[k + 1] - _my[k]); SL[k + 1] = SL[k] + _l[k]; }
            }

            public double Length => SL[SL.Length - 1];

            /// <summary>The line's point (world units) at an arc length, clamped to the line.</summary>
            public PlanPoint At(double s)
            {
                double sc = Clip(s, 0, Length);
                int k = 0;
                while (k + 1 < SL.Length - 1 && SL[k + 1] <= sc) k++;
                double u = (sc - SL[k]) / _l[k];
                return new PlanPoint(_p[k].X + (_p[k + 1].X - _p[k].X) * u, _p[k].Y + (_p[k + 1].Y - _p[k].Y) * u);
            }

            /// <summary>The nearest point's along fraction, the signed distance (m, inland +), and how far past an end (m).</summary>
            public void Near(double x, double y, out double s, out double d, out double past)
            {
                double px = x, py = y / _se, best = double.PositiveInfinity;
                int n = _mx.Length - 1;
                s = 0; d = 0; past = 0;
                for (int k = 0; k < n; k++)
                {
                    double ax = _mx[k], ay = _my[k], vx = _mx[k + 1] - ax, vy = _my[k + 1] - ay;
                    double l2 = vx * vx + vy * vy, l = Math.Sqrt(l2);
                    double t = ((px - ax) * vx + (py - ay) * vy) / l2, tc = Clip(t, 0, 1);
                    double qx = ax + vx * tc, qy = ay + vy * tc, dd = Hypot(px - qx, py - qy);
                    if (!(dd < best)) continue;
                    double cr = vx * (py - ay) - vy * (px - ax);
                    best = dd;
                    d = cr > 0 ? dd : -dd;
                    s = (SL[k] + tc * l) / Length;
                    past = k == 0 && t < 0 ? -t * l : (k == n - 1 && t > 1 ? (t - 1) * l : 0.0);
                }
            }
        }

        /// <summary>The flats' level along a shore: sampled (numpy's arange), smoothed (a centred Gaussian), read linearly.</summary>
        sealed class AlongLevel
        {
            readonly double _s0, _ds;
            readonly double[] _s, _v;

            public AlongLevel(Result r, ShoreLine sh, Ask a)
            {
                _s0 = -a.SampleRunOut;
                _ds = (_s0 + a.SampleStep) - _s0;                                  // numpy's arange: start + i × (start + step − start)
                int n = (int)Math.Floor((sh.Length + 2 * a.SampleRunOut) / a.SampleStep) + 1;
                _s = new double[n];
                var raw = new double[n];
                for (int j = 0; j < n; j++)
                {
                    double s = _s0 + j * _ds;
                    _s[j] = s;
                    if (s < 0 || s > sh.Length) continue;
                    var p = sh.At(s);
                    raw[j] = Smax(0, r.BaseAt(p.X, p.Y), a.Softness);
                }
                int half = (int)Math.Round(a.KernelSigmas * a.Sigma / a.SampleStep), m = 2 * half + 1;
                double k0 = -(a.KernelSigmas * a.Sigma), kd = (k0 + a.SampleStep) - k0;
                var g = new double[m];
                for (int j = 0; j < m; j++) { double u = (k0 + j * kd) / a.Sigma; g[j] = Math.Exp(-0.5 * (u * u)); }
                double sum = PairwiseSum(g, 0, m);
                for (int j = 0; j < m; j++) g[j] /= sum;
                _v = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double acc = 0;
                    for (int j = 0; j < m; j++)
                    {
                        int q = i + j - half;
                        if (q >= 0 && q < n) acc += raw[q] * g[m - 1 - j];
                    }
                    _v[i] = acc;
                }
            }

            /// <summary>The level at an arc length (numpy's interp on the samples; the ends hold).</summary>
            public double At(double s)
            {
                int n = _s.Length;
                if (s <= _s[0]) return _v[0];
                if (s >= _s[n - 1]) return _v[n - 1];
                int j = Math.Min(Math.Max((int)Math.Floor((s - _s0) / _ds), 0), n - 2);
                while (j > 0 && _s[j] > s) j--;
                while (j < n - 2 && _s[j + 1] <= s) j++;
                double slope = (_v[j + 1] - _v[j]) / (_s[j + 1] - _s[j]);
                return slope * (s - _s[j]) + _v[j];
            }
        }

        /// <summary>numpy's pairwise summation (blocks of 128, unrolled by 8), so a normalising sum matches the prototype's.</summary>
        static double PairwiseSum(double[] a, int at, int n)
        {
            if (n < 8)
            {
                double res = 0;
                for (int i = 0; i < n; i++) res += a[at + i];
                return res;
            }
            if (n <= 128)
            {
                double r0 = a[at], r1 = a[at + 1], r2 = a[at + 2], r3 = a[at + 3], r4 = a[at + 4], r5 = a[at + 5], r6 = a[at + 6], r7 = a[at + 7];
                int i = 8;
                for (; i < n - n % 8; i += 8)
                {
                    r0 += a[at + i]; r1 += a[at + i + 1]; r2 += a[at + i + 2]; r3 += a[at + i + 3];
                    r4 += a[at + i + 4]; r5 += a[at + i + 5]; r6 += a[at + i + 6]; r7 += a[at + i + 7];
                }
                double res = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
                for (; i < n; i++) res += a[at + i];
                return res;
            }
            int n2 = n / 2;
            n2 -= n2 % 8;
            return PairwiseSum(a, at, n2) + PairwiseSum(a, at + n2, n - n2);
        }

        // ---- fix 3: a channel's bed below the spring low ------------------------------------------------------------------

        /// <summary>
        /// A channel held along a line: at every texel the line's bilinear read touches, where the base holds water at a
        /// spring low, E ← min(E, max(the base, spring low − Margin)). It never raises, and never cuts below the base.
        /// </summary>
        static void ChannelHold(Result r, Ask a, int k, double springLow, AskRecord rec)
        {
            if (a.Line == null || a.Line.Length < 2) throw new InvalidDataException("[GroundFileImport] " + a.Id + ": a channel hold needs a line of two points or more.");
            if (!(a.FootprintStep > 0) || !(a.Margin >= 0)) throw new InvalidDataException("[GroundFileImport] " + a.Id + ": a channel hold's step must be positive and its margin not negative.");
            double held = springLow - a.Margin;
            foreach (int i in Footprint(r.Grid, a.Line, a.FootprintStep))
            {
                if (!(r.Base[i] < springLow)) continue;
                double e = r.E[i], en = Math.Min(e, Math.Max(r.Base[i], held));
                if (en == e) continue;
                Track(rec, en - e, i);
                r.E[i] = en;
                r.Owner[i] = k;
                rec.Cells++;
            }
        }

        // ---- fix 4: a closed hollow filled to its spill ------------------------------------------------------------------

        /// <summary>
        /// A closed hollow filled to its own spill: the window's cells are flooded from its border, the seed's basin is
        /// found, and each of its cells outside the kept circle is raised to the basin's spill; a cell inside the circle is
        /// held as it is. It stops when the seed is dry, when the basin reaches the window's border (it is not closed in the
        /// window), or when its spill is not the ask's level within its tolerance. It never cuts.
        /// </summary>
        static void HollowFill(Result r, Ask a, int k, AskRecord rec)
        {
            var g = r.Grid;
            if (!g.Win(a.WinX0, a.WinY0, a.WinX1, a.WinY1, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidDataException("[GroundFileImport] " + a.Id + ": the window misses the map.");
            int w = c1 - c0, h = r1 - r0;
            var z = new double[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) z[y * w + x] = r.E[(r0 + y) * g.W + c0 + x];
            var lvl = TerrainPlanFlood.SpillLevels(z, w, h, double.NegativeInfinity);
            var basins = TerrainPlanFlood.Basins(z, lvl, w, h, double.NegativeInfinity, TerrainPlanFlood.WetEps);
            int sr = g.RowOf(a.CentreY) - r0, sc = g.ColOf(a.CentreX) - c0;
            if (sr < 0 || sr >= h || sc < 0 || sc >= w) throw new InvalidDataException("[GroundFileImport] " + a.Id + ": the seed lies outside the window.");
            var b = TerrainPlanFlood.BasinAt(basins, sr * w + sc);
            if (b == null) throw new InvalidDataException("[GroundFileImport] STOP: " + a.Id + ": the ground at the seed holds no water.");
            foreach (int j in b.Cells)
            {
                int y = j / w, x = j - y * w;
                if (y == 0 || y == h - 1 || x == 0 || x == w - 1) throw new InvalidDataException("[GroundFileImport] STOP: " + a.Id + ": the hollow reaches the window's edge.");
            }
            if (!(Math.Abs(b.Spill - a.Level) <= a.LevelTolerance))
                throw new InvalidDataException("[GroundFileImport] STOP: " + a.Id + ": the hollow spills at " + b.Spill.ToString("R", CultureInfo.InvariantCulture) + " m; the ask says " + a.Level.ToString("R", CultureInfo.InvariantCulture) + ".");
            rec.Level = b.Spill;
            foreach (int j in b.Cells)
            {
                int row = r0 + j / w, col = c0 + j % w, i = row * g.W + col;
                if (a.KeepReach >= 0)
                {
                    double dx = g.XG(col) - a.KeepX, dy = g.YG(row) - a.KeepY;
                    if (Math.Sqrt(dx * dx + dy * dy) <= a.KeepReach) { rec.Held++; continue; }
                }
                double e = r.E[i], en = Math.Max(e, b.Spill);
                if (en == e) continue;
                Track(rec, en - e, i);
                r.E[i] = en;
                r.Owner[i] = k;
                rec.Cells++;
            }
        }

        /// <summary>
        /// The texels a line's bilinear read touches: the line walked at <paramref name="step"/> (numpy's linspace per
        /// segment), and each point's 2 × 2 cells. Plan order, sorted.
        /// </summary>
        public static SortedSet<int> Footprint(TerrainPlanGrid g, PlanPoint[] line, double step)
        {
            var o = new SortedSet<int>();
            for (int s = 0; s + 1 < line.Length; s++)
            {
                double ax = line[s].X, ay = line[s].Y, bx = line[s + 1].X, by = line[s + 1].Y;
                int n = Math.Max(2, (int)Math.Ceiling(Hypot(bx - ax, by - ay) / step) + 1);
                double dt = 1.0 / (n - 1);
                for (int j = 0; j < n; j++)
                {
                    double t = j == n - 1 ? 1.0 : j * dt;
                    double x = ax + (bx - ax) * t, y = ay + (by - ay) * t;
                    int i0 = (int)Math.Floor((x - g.X0) / g.Mpp - 0.5), j0 = (int)Math.Floor((g.Y1 - y) / g.Mpp - 0.5);
                    for (int jj = j0; jj <= j0 + 1; jj++)
                    for (int ii = i0; ii <= i0 + 1; ii++)
                        if (ii >= 0 && ii < g.W && jj >= 0 && jj < g.H) o.Add(jj * g.W + ii);
                }
            }
            return o;
        }

        // ---- the losses and the hashes ----------------------------------------------------------------------------------

        /// <summary>One patch's loss: its own samples where it wins, against the R16 read back bilinearly at them.</summary>
        public sealed class Loss
        {
            public string Id = "";
            public int Samples, Wins;
            public double Max, Sample, Read;
            public PlanPoint At;
            public int[] Over;
        }

        /// <summary>
        /// Each patch's loss on an import's codes: at each of its samples that no later patch covers, the sample against the
        /// R16 decoded (PaintedHeightField.DecodeElevation) and read bilinearly there; counts past each of <paramref name="over"/> (m).
        /// </summary>
        public static List<Loss> Losses(Result r, IList<Ask> asks, float min, float max, double[] over)
        {
            var g = r.Grid;
            var dec = new double[g.Count];
            for (int i = 0; i < g.Count; i++) dec[i] = TerrainPlanMaps.Decode(r.Codes[TerrainPlanMaps.Pixel(g, i)], min, max);
            var o = new List<Loss>();
            var values = new Dictionary<int, double[]>();
            for (int k = 0; k < asks.Count; k++) if (asks[k].Kind == GroundAskKind.Patch) values[k] = DecodePatch(asks[k]);
            for (int k = 0; k < asks.Count; k++)
            {
                var a = asks[k];
                if (a.Kind != GroundAskKind.Patch) continue;
                var v = values[k];
                var L = new Loss { Id = a.Id, Samples = v.Length, Over = new int[over.Length] };
                for (int jy = 0; jy < a.Height; jy++)
                for (int ix = 0; ix < a.Width; ix++)
                {
                    double x = a.BoxX0 + ix * a.Step, y = a.BoxY0 + jy * a.Step;
                    bool later = false;
                    for (int k2 = k + 1; k2 < asks.Count && !later; k2++)
                        if (asks[k2].Kind == GroundAskKind.Patch && PatchAt(asks[k2], values[k2], x, y, out _)) later = true;
                    if (later) continue;
                    L.Wins++;
                    double s = v[jy * a.Width + ix], rb = r.Bilinear(dec, x, y), d = Math.Abs(rb - s);
                    for (int t = 0; t < over.Length; t++) if (d > over[t]) L.Over[t]++;
                    if (d > L.Max) { L.Max = d; L.At = new PlanPoint(x, y); L.Sample = s; L.Read = rb; }
                }
                o.Add(L);
            }
            return o;
        }

        /// <summary>SHA-256 of 16-bit samples as big-endian bytes, in the order given (the file's pixelsSha256 and a patch's hash).</summary>
        public static string Sha256BigEndian(ushort[] codes)
        {
            var b = new byte[codes.Length * 2];
            for (int i = 0; i < codes.Length; i++) { b[2 * i] = (byte)(codes[i] >> 8); b[2 * i + 1] = (byte)codes[i]; }
            using (var h = SHA256.Create()) return TerrainPlanResult.Hex(h.ComputeHash(b));
        }
    }
}
