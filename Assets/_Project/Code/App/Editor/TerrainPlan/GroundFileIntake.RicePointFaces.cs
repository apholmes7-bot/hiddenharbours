using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Rice Point's crib faces drawn true. The issued ground holds each wharf crib as a hard step
    /// (the deck beside the water) rasterized at half a metre, so a face that runs at a slant is a
    /// staircase of texels. The water is clipped where the ground meets it, read between texels, and
    /// that staircase was the shoreline: the same notch every 1.1 m down the face, which reads as a
    /// repeating pattern and not as a wall. Here each texel near a drawn crib face (the outline edges
    /// the works name) is set from its true distance to that face: the outer ground up to
    /// <see cref="Reach"/> metres outside, rising straight to the deck over <see cref="Run"/> metres.
    /// The step is the same height as before, but its contours now lie along the face, so the
    /// waterline runs straight down it. Pure and deterministic, so paint = sail still holds.
    /// </summary>
    public static partial class GroundFileIntake
    {
        public sealed class HarbourFaces
        {
            public string GradingId;
            public string[] BlockIds;
            /// <summary>Per block, the outline edges (edge i runs from point i to point i + 1) whose face is drawn.</summary>
            public int[][] Edges;
            public double Reach, Run, MinStep;
            /// <summary>Each crib outline, read from the plan's grading.</summary>
            public double[][][] Outlines;

            /// <summary>The signed distance from a point to the nearest crib face (positive inside a crib) and that face's inward normal.</summary>
            public double SignedDistance(double x, double y, out double nx, out double ny) => SignedDistance(x, y, out nx, out ny, out _);

            /// <summary>As above, and whether that nearest face is one the works draw.</summary>
            public double SignedDistance(double x, double y, out double nx, out double ny, out bool drawn)
            {
                double best = double.PositiveInfinity, sign = -1; nx = 0; ny = 0; drawn = false;
                for (int p = 0; p < Outlines.Length; p++)
                {
                    var poly = Outlines[p];
                    bool inside = false;
                    double area = 0;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        if ((poly[i][1] > y) != (poly[j][1] > y) &&
                            x < (poly[j][0] - poly[i][0]) * (y - poly[i][1]) / (poly[j][1] - poly[i][1]) + poly[i][0])
                            inside = !inside;
                        area += poly[j][0] * poly[i][1] - poly[i][0] * poly[j][1];
                    }
                    double turn = area > 0 ? 1 : -1; // counter-clockwise: the inside is to the left of each edge
                    for (int i = 0; i < poly.Length; i++)
                    {
                        var a = poly[i]; var b = poly[(i + 1) % poly.Length];
                        double ex = b[0] - a[0], ey = b[1] - a[1], len2 = ex * ex + ey * ey;
                        double t = Math.Max(0, Math.Min(1, ((x - a[0]) * ex + (y - a[1]) * ey) / len2));
                        double dx = x - (a[0] + ex * t), dy = y - (a[1] + ey * t);
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d < best)
                        {
                            double len = Math.Sqrt(len2);
                            best = d; sign = inside ? 1 : -1;
                            nx = -ey / len * turn; ny = ex / len * turn;
                            drawn = Array.IndexOf(Edges[p], i) >= 0;
                        }
                    }
                }
                return sign * best;
            }

            /// <summary>Is this point in the band a drawn face sets (from <see cref="Reach"/> outside to the deck)?</summary>
            public bool InBand(double x, double y)
            {
                double sd = SignedDistance(x, y, out _, out _, out bool drawn);
                return drawn && sd >= -Reach && sd < Run - Reach;
            }
        }

        static HarbourFaces ReadHarbourFaces(Dictionary<string, object> works)
        {
            if (!works.TryGetValue("faces", out var raw)) return null;
            var o = raw as Dictionary<string, object> ?? throw Bad("\"faces\" is not an object");
            var blocks = Arr(o, "blocks").Select(b => b as Dictionary<string, object> ?? throw Bad("a face block is not an object")).ToArray();
            var faces = new HarbourFaces {
                GradingId = Str(o, "grading"),
                BlockIds = blocks.Select(b => Str(b, "id")).ToArray(),
                Edges = blocks.Select(b => Arr(b, "edges").Select(e => e is double d && d >= 0 && d == Math.Floor(d) ? (int)d : throw Bad("a face edge is not an index")).ToArray()).ToArray(),
                Reach = Num(o, "reach"), Run = Num(o, "run"), MinStep = Num(o, "minStep")
            };
            if (string.IsNullOrEmpty(faces.GradingId) || faces.BlockIds.Length == 0 || faces.BlockIds.Distinct().Count() != faces.BlockIds.Length ||
                faces.Edges.Any(e => e.Length == 0 || e.Distinct().Count() != e.Length) ||
                !(faces.Reach >= 0) || !(faces.Run > faces.Reach) || !Finite(faces.Run) || !(faces.MinStep > 0) || !Finite(faces.MinStep))
                throw Bad("invalid harbour faces");
            return faces;
        }

        /// <summary>The faces' crib outlines, read from the ground's plan: the grading entry and the blocks the works name.</summary>
        public static double[][][] RicePointCribOutlines(RicePointRaster ground, HarbourFaces faces)
        {
            var grading = Arr(ground.Document, "grading").OfType<Dictionary<string, object>>()
                .FirstOrDefault(g => g.TryGetValue("id", out var id) && (id as string) == faces.GradingId)
                ?? throw Bad("the plan has no grading " + faces.GradingId);
            if (Str(grading, "kind") != "cribs") throw Bad(faces.GradingId + " is not cribs");
            var blocks = Rows(grading, "blocks");
            return faces.BlockIds.Select((id, n) => {
                var block = blocks.FirstOrDefault(b => b.TryGetValue("id", out var v) && (v as string) == id) ?? throw Bad("the plan has no crib " + id);
                var outline = Points(Get(block, "outline"), id + " outline");
                if (outline.Length < 3) throw Bad(id + " outline is not a polygon");
                if (faces.Edges[n].Any(e => e >= outline.Length)) throw Bad(id + " has no such edge");
                return outline.Select(p => new double[] { p.x, p.y }).ToArray();
            }).ToArray();
        }

        /// <summary>
        /// Sets the drawn crib faces in an already dredged ground. Each texel within the band takes the
        /// outer ground (sampled half a metre beyond the band outside) rising straight to the deck
        /// (sampled half a metre beyond the band inside) by its distance to the face. Where the two are
        /// less than <see cref="HarbourFaces.MinStep"/> apart there is no face to draw (the crib meets
        /// the land) and the texel is left. Returns the texels it set.
        /// </summary>
        static bool[] SetCribFaces(RicePointRaster ground, double[] heights, HarbourFaces faces)
        {
            var source = (double[])heights.Clone();
            var set = new bool[heights.Length];
            double half = ground.Size[0] / 2, top = ground.Size[1] / 2, texel = 1 / ground.PerMetre;
            double Sample(double x, double y)
            {
                int col = Math.Max(0, Math.Min(ground.Width - 1, (int)Math.Floor((x + half) * ground.PerMetre)));
                int row = Math.Max(0, Math.Min(ground.Height - 1, (int)Math.Floor((top - y) * ground.PerMetre)));
                return source[row * ground.Width + col];
            }
            double inner = faces.Run - faces.Reach + texel, outer = -faces.Reach - texel;
            for (int row = 0; row < ground.Height; row++)
                for (int col = 0; col < ground.Width; col++)
                {
                    double x = -half + (col + 0.5) * texel, y = top - (row + 0.5) * texel;
                    double sd = faces.SignedDistance(x, y, out double nx, out double ny, out bool drawn);
                    if (!drawn || sd < -faces.Reach || sd >= faces.Run - faces.Reach) continue;
                    double hi = Sample(x + nx * (inner - sd), y + ny * (inner - sd));
                    double ho = Sample(x + nx * (outer - sd), y + ny * (outer - sd));
                    if (!(hi - ho >= faces.MinStep)) continue;
                    double t = Math.Max(0, Math.Min(1, (sd + faces.Reach) / faces.Run));
                    int i = row * ground.Width + col;
                    heights[i] = ho + (hi - ho) * t;
                    set[i] = true;
                }
            return set;
        }

        /// <summary>The texels the works' faces set in this ground (all false when the works have no faces).</summary>
        public static bool[] RicePointFaceTexels(RicePointRaster ground, HarbourWorks works)
        {
            var heights = new double[ground.Heights.Length];
            for (int row = 0; row < ground.Height; row++)
                for (int col = 0; col < ground.Width; col++)
                {
                    int i = row * ground.Width + col;
                    double x = -ground.Size[0] / 2 + (col + 0.5) / ground.PerMetre, y = ground.Size[1] / 2 - (row + 0.5) / ground.PerMetre;
                    heights[i] = works.DredgedHeight(x, y, ground.Decode(ground.Heights[i]));
                }
            if (works.Faces == null) return new bool[heights.Length];
            works.Faces.Outlines = RicePointCribOutlines(ground, works.Faces);
            return SetCribFaces(ground, heights, works.Faces);
        }
    }
}
