using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static partial class GroundFileIntake
    {
        /// <summary>Height substrate plus mown course paint. No road, prop, float or face placement.</summary>
        public static byte[][] CumberlandStarterPaint(CumberlandRaster ground, string rulesJson)
        {
            // Reuse the land starter's height/still rules; course geometry stays in Cumberland's source.
            var maps = RicePointStarterPaint(new RicePointRaster {
                Width = ground.Width, Height = ground.Height, Size = ground.Size, Range = ground.Range,
                PerMetre = ground.PerMetre, Heights = ground.Heights, Still = ground.Still
            }, rulesJson);
            var rules = TerrainPlanSourcesJson.ParseDocument(rulesJson) as Dictionary<string, object>;
            var mowing = Obj(rules, "course");
            int lawn = PaintChannel(Num(mowing, "lawnChannel")), grass = PaintChannel(Num(mowing, "grassChannel"));
            int sand = PaintChannel(Num(mowing, "sandChannel"));
            var weights = Nums(mowing, "stripeWeights", 2);
            double stripe = Num(mowing, "stripeMetres"), collar = Num(mowing, "collarMetres");
            double tee = Num(mowing, "teeRadius"), collarWeight = Num(mowing, "collarWeight");
            if (lawn == grass || new[] { stripe, collar, tee }.Any(n => !Finite(n) || n <= 0) ||
                weights.Concat(new[] { collarWeight }).Any(n => !Finite(n) || n < 0 || n > byte.MaxValue || n != Math.Floor(n)))
                throw Bad("invalid course paint rules");
            var course = Obj(ground.Document, "course");
            var area = PaintPoints(Arr(course, "area"));
            double greenRadius = Num(Obj(course, "greens"), "r");
            var holes = Rows(course, "holes").Select(h => new MownHole(h)).ToArray();
            var bunkers = Arr(course, "bunkers").Select(b => ((List<object>)b).Cast<double>().ToArray()).ToArray();
            if (!Finite(greenRadius) || greenRadius <= 0 || bunkers.Any(b => b.Length != 3 || b.Any(n => !Finite(n)) || b[2] <= 0))
                throw Bad("invalid course ground geometry");
            for (int row = 0; row < ground.Height; row++)
                for (int col = 0; col < ground.Width; col++)
                {
                    int i = row * ground.Width + col;
                    if (ground.Still[i] != 0) continue;
                    double x = -ground.Size[0] / 2 + (col + 0.5) / ground.PerMetre;
                    double y = ground.Size[1] / 2 - (row + 0.5) / ground.PerMetre;
                    if (!InsidePaintArea(area, x, y)) continue;
                    int weight = -1;
                    foreach (var hole in holes)
                        if (hole.Fairway(x, y, out double along)) weight = (int)weights[(int)Math.Floor(along / stripe) % weights.Length];
                    foreach (var hole in holes)
                    {
                        if (NearPaintPoint(hole.Green, x, y, greenRadius + collar)) weight = (int)collarWeight;
                        if (NearPaintPoint(hole.Green, x, y, greenRadius) || NearPaintPoint(hole.Tee, x, y, tee)) weight = byte.MaxValue;
                    }
                    bool bunker = bunkers.Any(b => NearPaintPoint(b, x, y, b[2]));
                    if (weight < 0 && !bunker) continue;
                    foreach (var map in maps) Array.Clear(map, i * 4, 4);
                    if (bunker) maps[sand / 4][i * 4 + sand % 4] = byte.MaxValue;
                    else
                    {
                        maps[lawn / 4][i * 4 + lawn % 4] = (byte)weight;
                        maps[grass / 4][i * 4 + grass % 4] = (byte)(byte.MaxValue - weight);
                    }
                }
            return maps;
        }

        static double[][] PaintPoints(List<object> rows) => rows.Select(p => {
            var values = p as List<object>;
            if (values == null || values.Count != 2 || values.Any(n => !(n is double d) || !Finite(d)))
                throw Bad("invalid course paint point");
            return values.Cast<double>().ToArray();
        }).ToArray();

        static bool NearPaintPoint(double[] point, double x, double y, double radius) => point != null &&
            (x - point[0]) * (x - point[0]) + (y - point[1]) * (y - point[1]) <= radius * radius;

        static bool InsidePaintArea(double[][] polygon, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i][1] > y) != (polygon[j][1] > y) &&
                    x < (polygon[j][0] - polygon[i][0]) * (y - polygon[i][1]) / (polygon[j][1] - polygon[i][1]) + polygon[i][0])
                    inside = !inside;
            return inside;
        }

        sealed class MownHole
        {
            public readonly double[] Tee, Green;
            readonly double[][] _line;
            readonly double _halfWidth;
            public MownHole(Dictionary<string, object> source)
            {
                Tee = source["tee"] == null ? null : Nums(source, "tee", 2);
                Green = source["green"] == null ? null : Nums(source, "green", 2);
                _line = PaintPoints(Arr(source, "line"));
                _halfWidth = Num(source, "width") / 2;
                if (_line.Length < 2 || !Finite(_halfWidth) || _halfWidth <= 0) throw Bad("invalid course hole");
            }
            public bool Fairway(double x, double y, out double along)
            {
                along = 0;
                double walked = 0, nearest = double.PositiveInfinity;
                for (int i = 1; i < _line.Length; i++)
                {
                    var a = _line[i - 1]; var b = _line[i];
                    double dx = b[0] - a[0], dy = b[1] - a[1], length = Math.Sqrt(dx * dx + dy * dy);
                    if (length == 0) throw Bad("zero-length course segment");
                    double t = ((x - a[0]) * dx + (y - a[1]) * dy) / (length * length);
                    // The first and last caps are square at the source line's endpoints.
                    if ((i == 1 && t < 0) || (i == _line.Length - 1 && t > 1)) { walked += length; continue; }
                    t = Math.Max(0, Math.Min(1, t));
                    double ex = x - a[0] - t * dx, ey = y - a[1] - t * dy, distance = ex * ex + ey * ey;
                    if (distance < nearest) { nearest = distance; along = walked + t * length; }
                    walked += length;
                }
                return nearest <= _halfWidth * _halfWidth;
            }
        }
    }
}
