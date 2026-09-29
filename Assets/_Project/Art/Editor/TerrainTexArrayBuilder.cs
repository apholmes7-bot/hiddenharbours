using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Terrain packing contract and CPU packer used by TerrainArrayImporter (ADR 0047).
    /// Order256 and LadderSteps are the shader's append-only slice ABI. Outputs belong to the
    /// import context in Library; this class never writes assets or changes source import settings.
    /// </summary>
    public static class TerrainTexArrayBuilder
    {
        public const string TexDir = "Assets/_Project/Art/Terrain";
        public const string RelightJsonPath = TexDir + "/TerrainRelight.json";

        public static readonly string[] Order256 =
        {
            "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed",
            "Eelgrass", "Irishmoss", "Lawn",
            "Shingle", "Ripple", "Silt", "Foreshore", "Talus", "Musselbed", "Oysterreef",
            "Mud", "Path",
        };
        public static readonly string[] LadderSteps = { "_Lo", "", "_Hi" };
        public static readonly string[] RelightMapSuffixes = { "_normal", "_light", "_detail" };
        public const int RelightPalettes = 16, RelightBands = 5;
        public static int Depth => Order256.Length * LadderSteps.Length;

        internal static string SourcePath(string directory, string name) => directory + "/" + name;

        internal static Texture2DArray PackDetail(string directory, Action<string> dependsOnSource)
        {
            var paths = new List<string>();
            foreach (string material in Order256)
            foreach (string step in LadderSteps)
                paths.Add(SourcePath(directory, material + step + ".png"));
            // Register all paths, including absent ones, before any validation can refuse the pack.
            foreach (string path in paths) dependsOnSource(path);
            Texture2DArray result = NewArray(TerrainArrayAssets.DetailName, true, false);
            try
            {
                for (int slice = 0; slice < paths.Count; slice++)
                    result.SetPixels32(Decode(paths[slice], false), slice);
                result.Apply(updateMipmaps: true, makeNoLongerReadable: false);
                return result;
            }
            catch
            {
                Object.DestroyImmediate(result);
                throw;
            }
        }

        internal static Object[] PackRelight(string directory, Action<string> dependsOnSource)
        {
            string manifestPath = SourcePath(directory, "TerrainRelight.json");
            dependsOnSource(manifestPath);
            RelightManifest manifest;
            try { manifest = JsonUtility.FromJson<RelightManifest>(File.ReadAllText(manifestPath)); }
            catch (Exception e) { throw Fault(manifestPath, "cannot read the relight manifest: " + e.Message); }
            if (manifest?.tiles == null || manifest.size != TerrainSplatSurface.RelightTileTexels)
                throw Fault(manifestPath, "missing tiles or wrong tile size.");

            var byName = new Dictionary<string, RelightTile>(StringComparer.Ordinal);
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string material in Order256)
            foreach (string step in LadderSteps) allowed.Add(material + step);
            // The existing bake includes cliff Bank tiles; they do not occupy ground slices.
            foreach (string step in LadderSteps) allowed.Add("Bank" + step);
            foreach (RelightTile tile in manifest.tiles)
            {
                if (tile?.name == null || !allowed.Contains(tile.name))
                    throw Fault(manifestPath, "unknown tile '" + tile?.name + "'.");
                if (byName.ContainsKey(tile.name))
                    throw Fault(manifestPath, "duplicate tile '" + tile.name + "'.");
                byName.Add(tile.name, tile);
            }

            var tiles = new RelightTile[Depth];
            var paths = new List<string>();
            for (int m = 0; m < Order256.Length; m++)
            for (int s = 0; s < LadderSteps.Length; s++)
            {
                if (!byName.TryGetValue(Order256[m] + LadderSteps[s], out RelightTile tile)) continue;
                tiles[m * LadderSteps.Length + s] = tile;
                foreach (string suffix in RelightMapSuffixes)
                    paths.Add(SourcePath(directory, tile.name + suffix + ".png"));
            }
            foreach (string path in paths) dependsOnSource(path);

            var rampPixels = new Color[TerrainSplatSurface.RelightRampWidth * Depth];
            for (int m = 0; m < Order256.Length; m++)
            {
                int baked = 0;
                for (int s = 0; s < LadderSteps.Length; s++)
                {
                    int slice = m * LadderSteps.Length + s;
                    RelightTile tile = tiles[slice];
                    if (tile == null) continue;
                    baked++;
                    if (tile.step != s)
                        throw Fault(manifestPath, "'" + tile.name + "' has a step inconsistent with its name.");
                    if (!Finite(tile.heightMin) || !Finite(tile.heightRange) || !Finite(tile.pondMax))
                        throw Fault(manifestPath, "'" + tile.name + "' has non-finite ramp parameters.");
                    string fault = PackRampRow(tile.palettes, tile.heightMin, tile.heightRange, tile.pondMax,
                        rampPixels, slice * TerrainSplatSurface.RelightRampWidth);
                    if (fault != null) throw Fault(manifestPath, "'" + tile.name + "': " + fault);
                }
                if (baked != 0 && baked != LadderSteps.Length)
                    throw Fault(manifestPath, "'" + Order256[m] + "' has a partial ladder.");
            }

            var output = new Object[4];
            try
            {
                for (int a = 0; a < RelightMapSuffixes.Length; a++)
                    output[a] = NewArray(TerrainArrayAssets.RelightNames[a], false, true);
                var ramp = new Texture2D(TerrainSplatSurface.RelightRampWidth, Depth, TextureFormat.RGBAFloat,
                    mipChain: false, linear: true)
                {
                    name = TerrainArrayAssets.RampName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    anisoLevel = 0,
                };
                output[3] = ramp;
                var blank = new Color32[TerrainSplatSurface.RelightTileTexels * TerrainSplatSurface.RelightTileTexels];
                for (int slice = 0; slice < Depth; slice++)
                for (int a = 0; a < RelightMapSuffixes.Length; a++)
                {
                    RelightTile tile = tiles[slice];
                    Color32[] pixels = tile == null ? blank :
                        Decode(SourcePath(directory, tile.name + RelightMapSuffixes[a] + ".png"), true);
                    ((Texture2DArray)output[a]).SetPixels32(pixels, slice);
                }
                for (int a = 0; a < RelightMapSuffixes.Length; a++)
                    ((Texture2DArray)output[a]).Apply(updateMipmaps: false, makeNoLongerReadable: false);
                ramp.SetPixels(rampPixels);
                ramp.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                return output;
            }
            catch
            {
                foreach (Object obj in output) if (obj != null) Object.DestroyImmediate(obj);
                throw;
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static Texture2DArray NewArray(string name, bool mipChain, bool linear) =>
            new Texture2DArray(TerrainSplatSurface.RelightTileTexels, TerrainSplatSurface.RelightTileTexels,
                Depth, TextureFormat.RGBA32, mipChain, linear)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, anisoLevel = 0,
            };

        static Color32[] Decode(string path, bool linear)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception e) { throw Fault(path, "cannot read source PNG: " + e.Message); }
            ValidatePng(bytes, path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: linear);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false))
                    throw Fault(path, "PNG decode failed.");
                int size = TerrainSplatSurface.RelightTileTexels;
                if (texture.width != size || texture.height != size)
                    throw Fault(path, "decoded PNG dimensions differ from its header.");
                // Both APIs use bottom-up rows. Only the independent bake-hash test flips rows.
                return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static InvalidDataException Fault(string path, string message) =>
            new InvalidDataException("'" + path + "': " + message);

        static uint BigEndian(byte[] data, int at) =>
            ((uint)data[at] << 24) | ((uint)data[at + 1] << 16) | ((uint)data[at + 2] << 8) | data[at + 3];

        static void ValidatePng(byte[] data, string path)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (data.Length < 33) throw Fault(path, "truncated PNG.");
            for (int i = 0; i < signature.Length; i++)
                if (data[i] != signature[i])
                    throw Fault(path, "not a PNG (check for an unmaterialized Git LFS pointer).");
            int size = TerrainSplatSurface.RelightTileTexels;
            bool imageData = false, end = false;
            for (int at = 8; at < data.Length;)
            {
                if (data.Length - at < 12) throw Fault(path, "truncated PNG chunk.");
                uint length = BigEndian(data, at);
                if (length > data.Length - at - 12) throw Fault(path, "truncated PNG chunk.");
                int count = (int)length;
                string kind = System.Text.Encoding.ASCII.GetString(data, at + 4, 4);
                if (at == 8 && (kind != "IHDR" || count != 13)) throw Fault(path, "invalid PNG header.");
                if (kind == "IHDR")
                {
                    if (at != 8 || count != 13) throw Fault(path, "duplicate or invalid PNG header.");
                    if (BigEndian(data, at + 8) != size || BigEndian(data, at + 12) != size)
                        throw Fault(path, "expected a " + size + " x " + size + " PNG.");
                    if (data[at + 16] != 8 || (data[at + 17] != 2 && data[at + 17] != 6)
                        || data[at + 18] != 0 || data[at + 19] != 0 || data[at + 20] != 0)
                        throw Fault(path, "export non-interlaced 8-bit RGB or RGBA PNG; indexed/16-bit inputs are unsupported.");
                }
                if (kind == "gAMA" || kind == "sRGB" || kind == "iCCP" || kind == "cHRM" || kind == "tRNS")
                    throw Fault(path, "unsupported PNG colour/transparency chunk " + kind + "; export raw RGB/RGBA.");
                uint crc = 0xffffffff;
                for (int i = at + 4; i < at + 8 + count; i++)
                {
                    crc ^= data[i];
                    for (int bit = 0; bit < 8; bit++)
                        crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
                }
                if ((crc ^ 0xffffffff) != BigEndian(data, at + 8 + count))
                    throw Fault(path, "PNG " + kind + " checksum failed.");
                imageData |= kind == "IDAT";
                at += count + 12;
                if (kind != "IEND") continue;
                if (count != 0 || at != data.Length) throw Fault(path, "invalid PNG end.");
                end = true;
                break;
            }
            if (!imageData || !end) throw Fault(path, "PNG has no complete image data.");
        }

        /// <summary>Pack the existing shader layout: palette bytes as float values, then
        /// (heightMin, heightRange, pondMax, 1) at column 80. Returns the refusal reason or null.</summary>
        public static string PackRampRow(string[] palettes, float heightMin, float heightRange, float pondMax,
                                         Color[] ramp, int rowStart)
        {
            if (palettes == null || palettes.Length == 0 || palettes.Length % RelightBands != 0)
                return $"{palettes?.Length ?? 0} palette colours, not whole palettes of {RelightBands}.";
            if (palettes.Length > RelightPalettes * RelightBands)
                return $"{palettes.Length / RelightBands} palettes; the ramp holds {RelightPalettes}.";
            for (int x = 0; x < TerrainSplatSurface.RelightRampWidth; x++) ramp[rowStart + x] = default;
            for (int i = 0; i < palettes.Length; i++)
            {
                string h = palettes[i];
                if (h == null || h.Length != 7 || h[0] != '#'
                    || !int.TryParse(h.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                                     System.Globalization.CultureInfo.InvariantCulture, out int rgb))
                    return $"palette colour '{h}' is not #rrggbb.";
                ramp[rowStart + i] = new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255, 255);
            }
            ramp[rowStart + RelightPalettes * RelightBands] = new Color(heightMin, heightRange, pondMax, 1f);
            return null;
        }

        // Keep JsonUtility's float conversion unchanged; external JSON parsers differ by an ULP.
        [Serializable] class RelightManifest { public int size; public RelightTile[] tiles; }
        [Serializable] class RelightTile
        {
            public string name;
            public int step;
            public string[] palettes;
            public float heightMin, heightRange, pondMax;
        }
    }
}
