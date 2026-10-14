using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static partial class GroundFileIntake
    {
        public const string RicePointSchema = "hidden-harbours/rpt-ground-plan@1";

        /// <summary>Pure, file-order raster intake. No assets, scene, engine calls or writes.</summary>
        public sealed class RicePointRaster
        {
            public string Id, RegionId, Name;
            public int Width, Height;
            public double[] Centre, Size, Range;
            public double PerMetre, Mean, Amplitude, PhaseHours;
            public ushort[] Heights, Still;
            public Dictionary<string, object> Document;
            public double Decode(ushort code) => Range[0] + (Range[1] - Range[0]) * code / ushort.MaxValue;
            public double StillLevel(ushort code) => code == 0 ? double.NegativeInfinity : Decode(code);
        }

        public static RicePointRaster ReadRicePoint(string json, byte[] heightPng, byte[] stillPng)
        {
            var doc = TerrainPlanSourcesJson.ParseDocument(json) as Dictionary<string, object>;
            if (doc == null || Str(doc, "schema") != RicePointSchema || Str(doc, "id") != "terrain_plan.rpt")
                throw Bad("not the Rice Point ground profile");
            if (Num(Obj(doc, "issue"), "n") != 2) throw Bad("Rice Point needs the second issue");
            var region = Obj(doc, "region");
            var tide = Obj(doc, "tide");
            var texels = Nums(region, "texels", 2);
            var result = new RicePointRaster {
                Id = Str(doc, "id"), RegionId = Str(region, "id"), Name = Str(region, "name"),
                Centre = Nums(region, "centre", 2), Size = Nums(region, "size", 2),
                Range = Nums(region, "range", 2), PerMetre = Num(region, "perMetre"),
                Mean = Num(tide, "mean"), Amplitude = Num(tide, "amplitude"), PhaseHours = Num(tide, "phaseHours"),
                Document = doc
            };
            if (result.RegionId != "region.rice_point" || result.Centre.Any(n => n != 0) ||
                result.Size.Any(n => !Finite(n) || n <= 0) || !Finite(result.PerMetre) || result.PerMetre <= 0 ||
                result.Range.Any(n => !Finite(n)) || result.Range[0] >= result.Range[1] ||
                !Finite(result.Mean) || !Finite(result.Amplitude) || result.Amplitude <= 0 || !Finite(result.PhaseHours))
                throw Bad("invalid Rice Point frame, range or tide");
            for (int i = 0; i < texels.Length; i++)
                if (!Finite(texels[i]) || texels[i] <= 0 || texels[i] != Math.Floor(texels[i]) ||
                    texels[i] != result.Size[i] * result.PerMetre || texels[i] > int.MaxValue)
                    throw Bad("Rice Point texels disagree with its frame");
            result.Width = (int)texels[0]; result.Height = (int)texels[1];
            var textures = Obj(doc, "textures");
            result.Heights = RiceCodes(heightPng, result, Str(Obj(textures, "height"), "pixelsSha256"));
            result.Still = RiceCodes(stillPng, result, Str(Obj(textures, "still"), "pixelsSha256"));
            return result;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        static ushort[] RiceCodes(byte[] png, RicePointRaster frame, string expectedHash)
        {
            var image = TerrainPlanPng.Read(png);
            if (image.BitDepth != 16 || image.ColourType != 0 || image.Channels != 1 ||
                image.Width != frame.Width || image.Height != frame.Height)
                throw Bad("Rice Point requires matching 16-bit grey rasters");
            var codes = image.Channel(0);
            if (RasterCodeHash(codes) != expectedHash) throw Bad("Rice Point raster hash differs");
            return codes;
        }

        public static string RasterCodeHash(ushort[] codes)
        {
            var bytes = new byte[checked(codes.Length * 2)];
            for (int i = 0; i < codes.Length; i++) { bytes[i * 2] = (byte)(codes[i] >> 8); bytes[i * 2 + 1] = (byte)codes[i]; }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Validate committed inputs before any CREATE or REFRESH can mutate the scene.</summary>
        public static RicePointRaster ReadRicePointFolder(string folder) => ReadRicePoint(
            File.ReadAllText(Path.Combine(folder, "plan.json")),
            File.ReadAllBytes(Path.Combine(folder, "RicePointSeabed_HeightTex_plan.png")),
            File.ReadAllBytes(Path.Combine(folder, "RicePointSeabed_StillTex_plan.png")));
    }
}
