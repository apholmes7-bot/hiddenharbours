using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiddenHarbours.App.Editor
{
    public static partial class GroundFileIntake
    {
        /// <summary>Deterministic starter substrate paint, not roads, faces, buildings or scatter.</summary>
        public static byte[][] RicePointStarterPaint(RicePointRaster ground, string rulesJson)
        {
            var rules = HiddenHarbours.World.TerrainPlanSourcesJson.ParseDocument(rulesJson) as Dictionary<string, object>;
            if (rules == null || Str(rules, "schema") != "hidden-harbours/land-starter-paint@1") throw Bad("unknown starter paint rules");
            var bands = Rows(rules, "bands").Select(b => new[] { Num(b, "below"), Num(b, "channel") }).ToArray();
            var marsh = Rows(rules, "marsh").Select(m => new {
                Centre = Nums(m, "centre", 2), Radii = Nums(m, "radii", 2),
                Angle = Num(m, "rotation") * Math.PI / 180, Channel = Num(m, "channel")
            }).ToArray();
            int stillChannel = PaintChannel(Num(rules, "stillChannel"));
            foreach (var band in bands) PaintChannel(band[1]);
            foreach (var m in marsh) { PaintChannel(m.Channel); if (m.Radii.Any(r => r <= 0)) throw Bad("invalid paint ellipse"); }
            if (bands.Length == 0 || bands.Any(b => !Finite(b[0])) || bands.Last()[0] <= ground.Range[1])
                throw Bad("paint bands do not cover the ground range");
            for (int i = 1; i < bands.Length; i++) if (bands[i][0] <= bands[i - 1][0]) throw Bad("paint bands are not ordered");
            var maps = Enumerable.Range(0, TerrainSplatBrush.TextureCount).Select(_ => new byte[ground.Heights.Length * 4]).ToArray();
            for (int row = 0; row < ground.Height; row++)
                for (int col = 0; col < ground.Width; col++)
                {
                    int i = row * ground.Width + col;
                    double height = ground.Decode(ground.Heights[i]);
                    int channel = (int)bands.First(b => height < b[0])[1];
                    double x = -ground.Size[0] / 2 + (col + 0.5) / ground.PerMetre;
                    double y = ground.Size[1] / 2 - (row + 0.5) / ground.PerMetre;
                    foreach (var m in marsh)
                    {
                        double dx = x - m.Centre[0], dy = y - m.Centre[1];
                        double u = (dx * Math.Cos(m.Angle) + dy * Math.Sin(m.Angle)) / m.Radii[0];
                        double v = (-dx * Math.Sin(m.Angle) + dy * Math.Cos(m.Angle)) / m.Radii[1];
                        if (u * u + v * v <= 1) channel = (int)m.Channel;
                    }
                    if (ground.Still[i] != 0) channel = stillChannel;
                    maps[channel / 4][i * 4 + channel % 4] = byte.MaxValue;
                }
            return maps;
        }

        static int PaintChannel(double value)
        {
            if (!Finite(value) || value != Math.Floor(value) || value < 0 || value >= TerrainSplatBrush.MaterialNames.Length)
                throw Bad("invalid substrate paint channel");
            return (int)value;
        }
    }
}
