using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static partial class GroundFileIntake
    {
        public const string CumberlandSchema = "hidden-harbours/cmb-ground-plan@1";

        /// <summary>Pure file-order intake. The shared bay's phase comes from the RegionDef, not this source.</summary>
        public sealed class CumberlandRaster
        {
            public string Id, RegionId, Name;
            public int Width, Height;
            public double[] Centre, Size, Range;
            public double PerMetre, Mean, Amplitude;
            public ushort[] Heights, Still;
            public Dictionary<string, object> Document;
            public double Decode(ushort code) => Range[0] + (Range[1] - Range[0]) * code / ushort.MaxValue;
            public double StillLevel(ushort code) => code == 0 ? double.NegativeInfinity : Decode(code);
        }

        public static CumberlandRaster ReadCumberland(string json, byte[] heightPng, byte[] stillPng)
        {
            var doc = TerrainPlanSourcesJson.ParseDocument(json) as Dictionary<string, object>;
            if (doc == null || Str(doc, "schema") != CumberlandSchema || Str(doc, "id") != "terrain_plan.cmb")
                throw Bad("not the Cumberland ground profile");
            var region = Obj(doc, "region");
            var tide = Obj(doc, "tide");
            var texels = Nums(region, "texels", 2);
            var result = new CumberlandRaster {
                Id = Str(doc, "id"), RegionId = Str(region, "id"), Name = Str(region, "name"),
                Centre = Nums(region, "centre", 2), Size = Nums(region, "size", 2),
                Range = Nums(region, "range", 2), PerMetre = Num(region, "perMetre"),
                Mean = Num(tide, "mean"), Amplitude = Num(tide, "amplitude"), Document = doc
            };
            if (result.RegionId != "region.cumberland" || result.Name != "Cumberland" ||
                result.Centre.Any(n => n != 0) || result.Size.Any(n => !Finite(n) || n <= 0) ||
                !Finite(result.PerMetre) || result.PerMetre <= 0 || result.Range.Any(n => !Finite(n)) ||
                result.Range[0] >= result.Range[1] || !Finite(result.Mean) ||
                !Finite(result.Amplitude) || result.Amplitude <= 0)
                throw Bad("invalid Cumberland frame, range or tide");
            for (int i = 0; i < texels.Length; i++)
                if (!Finite(texels[i]) || texels[i] <= 0 || texels[i] != Math.Floor(texels[i]) ||
                    texels[i] != result.Size[i] * result.PerMetre || texels[i] > int.MaxValue)
                    throw Bad("Cumberland texels disagree with its frame");
            result.Width = (int)texels[0]; result.Height = (int)texels[1];
            var textures = Obj(doc, "textures");
            result.Heights = CumberlandCodes(heightPng, result, Obj(textures, "height"));
            result.Still = CumberlandCodes(stillPng, result, Obj(textures, "still"));
            return result;
        }

        static ushort[] CumberlandCodes(byte[] png, CumberlandRaster frame, Dictionary<string, object> texture)
        {
            if (Sha(png) != Str(texture, "fileSha256")) throw Bad("Cumberland PNG file hash differs");
            var image = TerrainPlanPng.Read(png);
            if (image.BitDepth != 16 || image.ColourType != 0 || image.Channels != 1 ||
                image.Width != frame.Width || image.Height != frame.Height)
                throw Bad("Cumberland requires matching 16-bit grey rasters");
            var codes = image.Channel(0);
            if (RasterCodeHash(codes) != Str(texture, "pixelsSha256")) throw Bad("Cumberland raster hash differs");
            return codes;
        }

        /// <summary>Validate all committed source bytes before CREATE or REFRESH can write.</summary>
        public static CumberlandRaster ReadCumberlandFolder(string folder) => ReadCumberland(
            File.ReadAllText(Path.Combine(folder, "plan.json")),
            File.ReadAllBytes(Path.Combine(folder, "CumberlandSeabed_HeightTex_plan.png")),
            File.ReadAllBytes(Path.Combine(folder, "CumberlandSeabed_StillTex_plan.png")));
    }
}
