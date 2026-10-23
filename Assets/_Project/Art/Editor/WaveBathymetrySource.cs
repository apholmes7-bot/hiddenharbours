using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>Engine-free source intake. Raw greyscale PNG codes, bottom-up, at 8 or 16 bits.</summary>
    public static class WaveBathymetrySource
    {
        [Serializable]
        public sealed class Recipe
        {
            public int schemaVersion;
            public string id, seabedPath;
            public int cellSizeMm, offshoreDepthMm, minimumDepthMm, dryDepthMm;
            public int heightCapMilli, transportFloorMilli, openEdges;
            public int[] directionX, directionY, waterLevelsMm;
            public WaveBathymetry.Settings Settings() => new WaveBathymetry.Settings
            {
                CellSizeMm = cellSizeMm, OffshoreDepthMm = offshoreDepthMm,
                MinimumDepthMm = minimumDepthMm, DryDepthMm = dryDepthMm,
                HeightCapMilli = heightCapMilli, TransportFloorMilli = transportFloorMilli, OpenEdges = openEdges,
                DirectionX = directionX, DirectionY = directionY, WaterLevelsMm = waterLevelsMm,
            };
            public void Validate(string path)
            {
                if (schemaVersion != 1 || id == null || !Regex.IsMatch(id, @"\Abathymetry\.[a-z][a-z0-9_]*\z"))
                    throw Fault(path, "expected schema 1 and a stable bathymetry id.");
                AssetPath(seabedPath);
                Settings().Validate();
            }
        }

        public sealed class Input
        {
            public WaveBathymetry.Grid Grid;
            public string HeightPath, StillPath;
            public int PngBits, SizeXmm, SizeYmm;
        }

        public static readonly string[] CodePaths =
        {
            "Assets/_Project/Code/Core/Environment/WaveBathymetry.cs",
            "Assets/_Project/Code/Core/Environment/WaveBathymetryData.cs",
            "Assets/_Project/Art/Editor/WaveBathymetrySource.cs",
            "Assets/_Project/Art/Editor/WaveBathymetryImporter.cs",
            "Assets/_Project/Art/Editor/WaveBathymetryAssets.cs",
            "Assets/_Project/Code/World/PaintedHeightMap.cs",
            "Assets/_Project/Code/World/PaintedHeightField.cs",
            "Assets/_Project/Code/Core/Environment/SeabedBakeMath.cs",
            "Assets/_Project/Code/Core/Environment/IStillWater.cs",
        };

        public static Input Read(Recipe recipe, string recipePath, Func<string, string> resolveGuid, Action<string> depends)
        {
            recipe.Validate(recipePath);
            foreach (string path in CodePaths) depends(path);
            depends(recipe.seabedPath);
            string yaml = File.ReadAllText(recipe.seabedPath);
            if (!yaml.Contains("HiddenHarbours.World::HiddenHarbours.World.PaintedHeightMap"))
                throw Fault(recipe.seabedPath, "expected a PaintedHeightMap.");
            string height = TexturePath("_heightTexture", true);
            string still = TexturePath("_stillLevelTexture", false);
            int min = Mm(Scalar("_minElevation")), max = Mm(Scalar("_maxElevation"));
            if (min >= max) throw Fault(recipe.seabedPath, "elevation range must increase.");
            var center = Pair("_worldCenter"); var size = Pair("_worldSize");
            int sx = Mm(size[0]), sy = Mm(size[1]);
            if (sx < 2 * recipe.cellSizeMm || sy < 2 * recipe.cellSizeMm || sx % recipe.cellSizeMm != 0 || sy % recipe.cellSizeMm != 0)
                throw Fault(recipe.seabedPath, "world size must contain whole cells, at least two per axis.");
            int w = sx / recipe.cellSizeMm, h = sy / recipe.cellSizeMm;
            Png ground = Decode(File.ReadAllBytes(height), height);
            int[] elevations = Sample(ground, w, h, min, max, false);
            int[] levels = null;
            if (still != null)
            {
                Png pond = Decode(File.ReadAllBytes(still), still);
                if (pond.Width != ground.Width || pond.Height != ground.Height)
                    throw Fault(still, "still and height dimensions must agree.");
                levels = Sample(pond, w, h, min, max, true);
            }
            return new Input
            {
                Grid = new WaveBathymetry.Grid(w, h, Mm(center[0]) - sx / 2, Mm(center[1]) - sy / 2, elevations, levels),
                HeightPath = height, StillPath = still, PngBits = ground.Bits, SizeXmm = sx, SizeYmm = sy,
            };

            string TexturePath(string field, bool required)
            {
                MatchCollection fields = Regex.Matches(yaml, @"(?m)^  " + field + @": \{([^\r\n]+)\}\r?$");
                if (fields.Count == 0 && !required) return null;
                if (fields.Count != 1) throw Fault(recipe.seabedPath, "missing or duplicate " + field + ".");
                string value = fields[0].Groups[1].Value;
                if (!required && value == "fileID: 0") return null;
                Match guid = Regex.Match(value, @"\AfileID: 2800000, guid: ([a-f0-9]{32}), type: 3\z");
                if (!guid.Success) throw Fault(recipe.seabedPath, "unsupported texture field " + field + ".");
                string path = resolveGuid(guid.Groups[1].Value);
                AssetPath(path); depends(path);
                if (!path.EndsWith(".png", StringComparison.Ordinal)) throw Fault(path, "expected a PNG.");
                return path;
            }
            decimal Scalar(string field)
            {
                MatchCollection matches = Regex.Matches(yaml, @"(?m)^  " + field + @": (-?[0-9]+(?:\.[0-9]+)?)\r?$");
                if (matches.Count != 1) throw Fault(recipe.seabedPath, "unsupported " + field + ".");
                return decimal.Parse(matches[0].Groups[1].Value, CultureInfo.InvariantCulture);
            }
            decimal[] Pair(string field)
            {
                MatchCollection matches = Regex.Matches(yaml, @"(?m)^  " + field + @": \{x: (-?[0-9]+(?:\.[0-9]+)?), y: (-?[0-9]+(?:\.[0-9]+)?)\}\r?$");
                if (matches.Count != 1) throw Fault(recipe.seabedPath, "unsupported " + field + ".");
                return new[] { decimal.Parse(matches[0].Groups[1].Value, CultureInfo.InvariantCulture),
                    decimal.Parse(matches[0].Groups[2].Value, CultureInfo.InvariantCulture) };
            }
        }

        public static int Mm(decimal meters) => checked((int)decimal.Round(meters * 1000, 0, MidpointRounding.AwayFromZero));

        static void AssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.Contains("\\") || path.Contains("..") || path.Contains(":"))
                throw Fault(path ?? "<empty>", "expected a project source path.");
        }

        public sealed class Png
        {
            public int Width, Height, Bits;
            public ushort[] Codes;
        }

        public static Png Decode(byte[] data, string path)
        {
            try { return DecodeChecked(data, path); }
            catch (Exception e) when (!(e is InvalidDataException && e.Message.StartsWith("'", StringComparison.Ordinal)))
            { throw Fault(path, "PNG decode failed: " + e.Message); }
        }

        static Png DecodeChecked(byte[] data, string path)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (data == null || data.Length < 33) throw Fault(path, "truncated PNG.");
            for (int i = 0; i < 8; i++) if (data[i] != signature[i]) throw Fault(path, "expected materialized PNG bytes.");
            var png = new Png();
            using var compressed = new MemoryStream();
            bool end = false, started = false, closed = false;
            for (int at = 8; at < data.Length;)
            {
                if (data.Length - at < 12) throw Fault(path, "truncated PNG chunk.");
                uint length = Big(data, at);
                if (length > data.Length - at - 12) throw Fault(path, "truncated PNG chunk.");
                int n = (int)length; string kind = Encoding.ASCII.GetString(data, at + 4, 4);
                uint crc = 0xffffffff;
                for (int j = at + 4; j < at + 8 + n; j++)
                {
                    crc ^= data[j];
                    for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
                }
                if ((crc ^ 0xffffffff) != Big(data, at + 8 + n)) throw Fault(path, kind + " checksum failed.");
                if (at == 8 && kind != "IHDR") throw Fault(path, "expected IHDR first.");
                if (kind == "IHDR")
                {
                    if (at != 8 || n != 13) throw Fault(path, "invalid IHDR.");
                    png.Width = checked((int)Big(data, at + 8)); png.Height = checked((int)Big(data, at + 12));
                    png.Bits = data[at + 16];
                    if (png.Width < 2 || png.Height < 2 || png.Width > 8192 || png.Height > 8192
                        || (png.Bits != 8 && png.Bits != 16) || data[at + 17] != 0
                        || data[at + 18] != 0 || data[at + 19] != 0 || data[at + 20] != 0)
                        throw Fault(path, "expected non-interlaced raw greyscale 8/16-bit PNG, 2..8192 texels per axis.");
                }
                else if (kind == "IDAT")
                {
                    if (closed) throw Fault(path, "nonconsecutive IDAT.");
                    compressed.Write(data, at + 8, n); started = true;
                }
                else if (kind == "IEND")
                {
                    if (n != 0 || at + n + 12 != data.Length) throw Fault(path, "invalid IEND.");
                    end = true;
                }
                else
                {
                    if (started) closed = true;
                    // No colour conversions, alpha, palette, text or animation silently accepted.
                    throw Fault(path, "unsupported PNG chunk " + kind + ".");
                }
                at += n + 12;
            }
            if (!end || !started) throw Fault(path, "incomplete PNG.");
            byte[] packed = compressed.ToArray();
            if (packed.Length < 6 || (packed[0] & 15) != 8 || (packed[0] >> 4) > 7
                || ((packed[0] << 8) + packed[1]) % 31 != 0 || (packed[1] & 32) != 0)
                throw Fault(path, "unsupported zlib header.");
            int bpp = png.Bits / 8, stride = checked(png.Width * bpp);
            var filtered = new byte[checked((stride + 1) * png.Height)];
            using (var stream = new MemoryStream(packed, 2, packed.Length - 6))
            using (var inflate = new DeflateStream(stream, CompressionMode.Decompress))
            {
                int read = 0, part;
                while (read < filtered.Length && (part = inflate.Read(filtered, read, filtered.Length - read)) > 0) read += part;
                if (read != filtered.Length || inflate.ReadByte() != -1) throw Fault(path, "invalid inflated byte count.");
            }
            uint sumA = 1, sumB = 0;
            foreach (byte value in filtered) { sumA = (sumA + value) % 65521; sumB = (sumB + sumA) % 65521; }
            if ((sumB << 16 | sumA) != Big(packed, packed.Length - 4)) throw Fault(path, "zlib checksum failed.");
            var raw = new byte[stride * png.Height];
            for (int y = 0; y < png.Height; y++)
            {
                int filter = filtered[y * (stride + 1)];
                if (filter > 4) throw Fault(path, "unsupported PNG filter.");
                for (int x = 0; x < stride; x++)
                {
                    int i = y * stride + x, left = x >= bpp ? raw[i - bpp] : 0;
                    int up = y > 0 ? raw[i - stride] : 0, corner = y > 0 && x >= bpp ? raw[i - stride - bpp] : 0;
                    int prediction = filter == 0 ? 0 : filter == 1 ? left : filter == 2 ? up
                        : filter == 3 ? (left + up) / 2 : Paeth(left, up, corner);
                    raw[i] = unchecked((byte)(filtered[y * (stride + 1) + x + 1] + prediction));
                }
            }
            png.Codes = new ushort[png.Width * png.Height];
            for (int y = 0; y < png.Height; y++)
                for (int x = 0; x < png.Width; x++)
                {
                    int i = y * stride + x * bpp;
                    png.Codes[(png.Height - 1 - y) * png.Width + x] = bpp == 1 ? raw[i] : (ushort)((raw[i] << 8) | raw[i + 1]);
                }
            return png;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c);
            return da <= db && da <= dc ? a : db <= dc ? b : c;
        }
        static uint Big(byte[] d, int i) => ((uint)d[i] << 24) | ((uint)d[i + 1] << 16) | ((uint)d[i + 2] << 8) | d[i + 3];

        /// <summary>Same texel-centre bilinear law as PaintedHeightField, rounded to millimetres once.</summary>
        public static int[] Sample(Png png, int width, int height, int minMm, int maxMm, bool still)
        {
            var output = new int[width * height];
            int codes = png.Bits == 16 ? 65535 : 255;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    decimal fx = (x + 0.5m) * png.Width / width - 0.5m;
                    decimal fy = (y + 0.5m) * png.Height / height - 0.5m;
                    int x0 = (int)decimal.Floor(fx), y0 = (int)decimal.Floor(fy);
                    decimal tx = fx - x0, ty = fy - y0;
                    decimal a = Code(x0, y0) * (1 - tx) + Code(x0 + 1, y0) * tx;
                    decimal b = Code(x0, y0 + 1) * (1 - tx) + Code(x0 + 1, y0 + 1) * tx;
                    decimal r = a * (1 - ty) + b * ty;
                    output[y * width + x] = still && r == 0 ? int.MinValue
                        : checked((int)decimal.Round(minMm + (maxMm - minMm) * r / codes, 0, MidpointRounding.AwayFromZero));
                }
            return output;
            int Code(int x, int y) => png.Codes[Math.Max(0, Math.Min(png.Height - 1, y)) * png.Width
                + Math.Max(0, Math.Min(png.Width - 1, x))];
        }
        static InvalidDataException Fault(string path, string why) => new InvalidDataException("'" + path + "': " + why);
    }
}
