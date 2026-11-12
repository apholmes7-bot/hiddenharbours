using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Rice Point's harbour works: the dredging that makes the wharf take the trawlers. The creative
    /// director's second-issue ground stays as issued (<see cref="RicePointBaseFolder"/>, its hashes in
    /// plan.json); the ground the game sails on is that base with the cuts of <c>harbour-works.json</c>
    /// applied, re-encoded to the wider range the cuts need. One pure function, so the committed
    /// rasters can always be proved to be exactly "the base, dredged as the data says" (paint = sail).
    /// </summary>
    public static partial class GroundFileIntake
    {
        public const string HarbourWorksSchema = "hidden-harbours/harbour-works@1";
        public const string RicePointBaseFolder = "Issue2Base";
        public const string RicePointWorksFile = "harbour-works.json";

        /// <summary>One dredged cut: a line with a flat bed out to a half width, then a batter up to the old ground.</summary>
        public sealed class HarbourCut
        {
            public string Id;
            public double Bed, HalfWidth, SideRun;
            public double[][] Line;

            /// <summary>The bed this cut asks for at a point: its bed inside, rising one metre per <see cref="SideRun"/> metres outside.</summary>
            public double TargetAt(double x, double y)
            {
                double best = double.PositiveInfinity;
                for (int i = 0; i + 1 < Line.Length; i++)
                {
                    double ax = Line[i][0], ay = Line[i][1], bx = Line[i + 1][0], by = Line[i + 1][1];
                    double ex = bx - ax, ey = by - ay;
                    double t = ((x - ax) * ex + (y - ay) * ey) / (ex * ex + ey * ey);
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    double dx = x - (ax + ex * t), dy = y - (ay + ey * t);
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < best) best = d;
                }
                double outside = best - HalfWidth;
                return outside <= 0 ? Bed : Bed + outside / SideRun;
            }
        }

        public sealed class HarbourWorks
        {
            public string Id, RegionId;
            public double[] Range;
            public double KeepAbove;
            public HarbourCut[] Cuts;

            /// <summary>The lowest bed any cut asks for here, or +infinity where no cut reaches.</summary>
            public double TargetAt(double x, double y)
            {
                double target = double.PositiveInfinity;
                foreach (var cut in Cuts) target = Math.Min(target, cut.TargetAt(x, y));
                return target;
            }
        }

        public static HarbourWorks ReadHarbourWorks(string json)
        {
            var doc = HiddenHarbours.World.TerrainPlanSourcesJson.ParseDocument(json) as Dictionary<string, object>;
            if (doc == null || Str(doc, "schema") != HarbourWorksSchema) throw Bad("not a harbour works file");
            var works = new HarbourWorks {
                Id = Str(doc, "id"), RegionId = Str(doc, "region"),
                Range = Nums(doc, "range", 2), KeepAbove = Num(doc, "keepAbove"),
                Cuts = Rows(doc, "cuts").Select(c => new HarbourCut {
                    Id = Str(c, "id"), Bed = Num(c, "bed"), HalfWidth = Num(c, "halfWidth"), SideRun = Num(c, "sideRun"),
                    Line = Arr(c, "line").Select(p => ((List<object>)p).Select(n => (double)n).ToArray()).ToArray()
                }).ToArray()
            };
            if (works.Range.Any(n => !Finite(n)) || works.Range[0] >= works.Range[1] || !Finite(works.KeepAbove) || works.Cuts.Length == 0)
                throw Bad("invalid harbour works frame");
            foreach (var cut in works.Cuts)
                if (string.IsNullOrEmpty(cut.Id) || !Finite(cut.Bed) || !(cut.HalfWidth > 0) || !(cut.SideRun > 0) ||
                    cut.Line.Length < 2 || cut.Line.Any(p => p.Length != 2 || p.Any(n => !Finite(n))) ||
                    cut.Bed < works.Range[0] || cut.Bed >= works.KeepAbove)
                    throw Bad("invalid harbour cut " + cut.Id);
            if (works.Cuts.Select(c => c.Id).Distinct().Count() != works.Cuts.Length) throw Bad("harbour cut ids repeat");
            return works;
        }

        /// <summary>
        /// The base ground with the works applied: every texel is the lower of the base and what the
        /// cuts ask, except ground at or above <see cref="HarbourWorks.KeepAbove"/> (the wharf and its
        /// wall), which no cut touches. Heights and still-water levels are re-encoded to the works'
        /// range. A cut that would reach a still-water pool is refused.
        /// </summary>
        public static RicePointRaster RicePointDredged(RicePointRaster ground, HarbourWorks works)
        {
            if (works.RegionId != ground.RegionId) throw Bad("harbour works are for another region");
            if (works.Range[0] > ground.Range[0] || works.Range[1] < ground.Range[1]) throw Bad("harbour works range loses base ground");
            var result = new RicePointRaster {
                Id = ground.Id, RegionId = ground.RegionId, Name = ground.Name, Width = ground.Width, Height = ground.Height,
                Centre = ground.Centre, Size = ground.Size, Range = works.Range, PerMetre = ground.PerMetre,
                Mean = ground.Mean, Amplitude = ground.Amplitude, PhaseHours = ground.PhaseHours, Document = ground.Document,
                Heights = new ushort[ground.Heights.Length], Still = new ushort[ground.Still.Length]
            };
            double lo = works.Range[0], span = works.Range[1] - works.Range[0];
            for (int row = 0; row < ground.Height; row++)
                for (int col = 0; col < ground.Width; col++)
                {
                    int i = row * ground.Width + col;
                    double height = ground.Decode(ground.Heights[i]);
                    double x = -ground.Size[0] / 2 + (col + 0.5) / ground.PerMetre;
                    double y = ground.Size[1] / 2 - (row + 0.5) / ground.PerMetre;
                    double target = works.TargetAt(x, y);
                    bool cut = height < works.KeepAbove && target < height;
                    if (cut) height = target;
                    result.Heights[i] = (ushort)Math.Round((height - lo) * 65535d / span);
                    ushort still = ground.Still[i];
                    if (still == 0) continue;
                    if (cut) throw Bad("a harbour cut reaches still water at texel " + i);
                    result.Still[i] = (ushort)Math.Round((ground.Decode(still) - lo) * 65535d / span);
                    if (result.Still[i] == 0) throw Bad("a still-water level falls on the none code");
                }
            return result;
        }

        /// <summary>
        /// The ground the game sails on: the issued base, dredged. Refuses unless the committed live
        /// rasters are exactly that, so a builder never runs on ground the data does not explain.
        /// </summary>
        public static RicePointRaster ReadRicePointGround(string folder)
        {
            var dredged = RicePointDredged(ReadRicePointFolder(folder),
                ReadHarbourWorks(File.ReadAllText(Path.Combine(folder, RicePointWorksFile))));
            RequireSameCodes(Path.Combine(folder, "RicePointSeabed_HeightTex_plan.png"), dredged, dredged.Heights);
            RequireSameCodes(Path.Combine(folder, "RicePointSeabed_StillTex_plan.png"), dredged, dredged.Still);
            return dredged;
        }

        static void RequireSameCodes(string path, RicePointRaster frame, ushort[] expected)
        {
            var image = TerrainPlanPng.ReadFile(path);
            if (image.BitDepth != 16 || image.ColourType != 0 || image.Channels != 1 ||
                image.Width != frame.Width || image.Height != frame.Height)
                throw Bad("Rice Point requires matching 16-bit grey rasters");
            if (!image.Channel(0).SequenceEqual(expected)) throw Bad("Rice Point's live raster is not the base, dredged: " + path);
        }
    }
}
