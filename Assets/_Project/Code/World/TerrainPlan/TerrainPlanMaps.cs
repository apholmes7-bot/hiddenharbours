using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PLAN'S MAPS, AS THE FILES HOLD THEM.</b> A derivation's result encoded to the committed maps' values: the
    /// height map's 16-bit codes at the plan's range, the splat's six RGBA maps (A to F) and the still-water map's
    /// 16-bit codes, each in Unity's pixel order (row 0 south), and the manifest's text. The editor writes these values
    /// through the project's own PNG paths; the determinism test compares a fresh derivation's values with the files'.
    ///
    /// <para>Pure, so a headless run can check the encoding: the height code is the paint tool's rule
    /// (<see cref="PaintedHeightField.EncodeElevation"/>, then round(r × 65535)), the still code is
    /// <see cref="StillWaterLevels.EncodeCode"/> on the height map's range (the range the still map is decoded on), and
    /// a splat cell is one-hot at 255 in its zone's slot, with a light path (weight 0 to 1) laid over it in the path
    /// slot, F.r, at round(weight × 255) and the zone keeping the rest, so a cell's channels never sum past 255.</para>
    /// </summary>
    public static class TerrainPlanMaps
    {
        /// <summary>The splat maps, A to F: 24 channels, the 20 kit slots and the path slot (F.r).</summary>
        public const int SplatMaps = 6;

        /// <summary>A 16-bit map's largest code (PaintedHeightPng.CodeCount, StillWaterLevels.CodeCount).</summary>
        public const int CodeCount = 65535;

        /// <summary>The Unity pixel (row 0 south) that holds a plan cell (row 0 north).</summary>
        public static int Pixel(TerrainPlanGrid g, int cell) => (g.H - 1 - cell / g.W) * g.W + cell % g.W;

        /// <summary>The height map's normalized R per pixel: EncodeElevation at the range, Unity order.</summary>
        public static float[] HeightR01(TerrainPlanGrid g, double[] e, float min, float max)
        {
            Fill(g, e, "the height");
            var o = new float[g.Count];
            for (int i = 0; i < g.Count; i++) o[Pixel(g, i)] = PaintedHeightField.EncodeElevation((float)e[i], min, max);
            return o;
        }

        /// <summary>The 16-bit codes a normalized R buffer encodes to: PaintedHeightPng.EncodeCodes' rule, exactly.</summary>
        public static ushort[] Codes(float[] r01)
        {
            var c = new ushort[r01.Length];
            for (int i = 0; i < c.Length; i++)
            {
                float r = r01[i] < 0f ? 0f : r01[i] > 1f ? 1f : r01[i];
                // Mathf.RoundToInt: ties to even, on the product rounded to a float as its parameter rounds it. The cast is
                // the rule: without it Unity's Mono keeps the product at double precision (5,088 of St Peters' codes a step off).
                c[i] = (ushort)(int)Math.Round((double)(float)(r * CodeCount));
            }
            return c;
        }

        /// <summary>The metres a height code decodes to (the sim's lerp(min, max, code / 65535)).</summary>
        public static float Decode(ushort code, float min, float max) =>
            PaintedHeightField.DecodeElevation(code / (float)CodeCount, min, max);

        /// <summary>A ground file's import, its R16 decoded as the sim decodes it, in plan order (row 0 north).</summary>
        public static double[] GroundOf(GroundFileImport.Result r, float min, float max)
        {
            var g = r.Grid;
            var e = new double[g.Count];
            for (int i = 0; i < g.Count; i++) e[i] = Decode(r.Codes[Pixel(g, i)], min, max);
            return e;
        }

        /// <summary>The still map's codes per pixel, Unity order: EncodeCode on the height map's range; none (NaN) is 0.</summary>
        public static ushort[] StillCodes(TerrainPlanGrid g, double[] still, float min, float max)
        {
            Fill(g, still, "the still water");
            var o = new ushort[g.Count];
            for (int i = 0; i < g.Count; i++) o[Pixel(g, i)] = StillWaterLevels.EncodeCode((float)still[i], min, max);
            return o;
        }

        /// <summary>The splat's six maps as RGBA32 bytes, [map][pixel × 4 + channel], Unity order.</summary>
        public static byte[][] Splat(TerrainPlanGrid g, byte[] zone, double[] pathWeight)
        {
            if (zone == null || zone.Length != g.Count) throw new ArgumentException("[TerrainPlan] the zones do not fill the grid.");
            if (pathWeight != null && pathWeight.Length != g.Count) throw new ArgumentException("[TerrainPlan] the path weights do not fill the grid.");
            var maps = new byte[SplatMaps][];
            for (int m = 0; m < SplatMaps; m++) maps[m] = new byte[g.Count * 4];
            int pathMap = TerrainPlanZones.Path / 4, pathCh = TerrainPlanZones.Path % 4;
            for (int i = 0; i < g.Count; i++)
            {
                int px = Pixel(g, i) * 4;
                byte z = zone[i];
                double w = pathWeight != null ? pathWeight[i] : 0;
                if (z != TerrainPlanZones.Unpainted && z > TerrainPlanZones.Path)
                    throw new ArgumentException("[TerrainPlan] zone " + z + " at cell " + i + " is no splat slot.");
                if (z == TerrainPlanZones.Path || w >= 1) { maps[pathMap][px + pathCh] = 255; continue; }
                byte path = w > 0 ? (byte)Math.Round(w * 255.0, MidpointRounding.AwayFromZero) : (byte)0;
                if (z != TerrainPlanZones.Unpainted) maps[z / 4][px + z % 4] = (byte)(255 - path);
                maps[pathMap][px + pathCh] = path;
            }
            return maps;
        }

        /// <summary>SHA-256 of a code buffer as little-endian bytes (lower-case hex).</summary>
        public static string Sha256(ushort[] codes)
        {
            var b = new byte[codes.Length * 2];
            for (int i = 0; i < codes.Length; i++) { b[2 * i] = (byte)codes[i]; b[2 * i + 1] = (byte)(codes[i] >> 8); }
            return Sha256(b);
        }

        /// <summary>SHA-256 of bytes (lower-case hex).</summary>
        public static string Sha256(byte[] bytes)
        {
            using (var h = SHA256.Create()) return TerrainPlanResult.Hex(h.ComputeHash(bytes));
        }

        /// <summary>SHA-256 of doubles as little-endian bytes (NaN as −9999, the result's rule).</summary>
        public static string Sha256(double[] values)
        {
            var b = new byte[values.Length * 8];
            for (int i = 0; i < values.Length; i++)
            {
                long v = BitConverter.DoubleToInt64Bits(double.IsNaN(values[i]) ? -9999.0 : values[i]);
                for (int k = 0; k < 8; k++) b[8 * i + k] = (byte)(v >> (8 * k));
            }
            return Sha256(b);
        }

        static void Fill(TerrainPlanGrid g, double[] a, string what)
        {
            if (a == null || a.Length != g.Count) throw new ArgumentException("[TerrainPlan] " + what + " does not fill the grid.");
        }
    }

    /// <summary>
    /// <b>THE MANIFEST.</b> What a derivation read and wrote, as canonical text: the plan and its seeds, the range, the
    /// grid, the frozen sources' and today's ground's SHA-256, every Def by id with its file's SHA-256 and CD's id, the
    /// derivation's hash, its order log and numbers, the pools and frozen pieces it laid, and each map's path, code
    /// SHA-256 and file SHA-256; and with a ground file (PR 5 B), the file it was imported from. The editor fills it when
    /// it writes the maps; the determinism test reads the codes back.
    /// </summary>
    public sealed class TerrainPlanManifest
    {
        public const string Format = "hidden-harbours.terrain-plan-manifest/1";

        public string PlanId = "";
        public int Seed, Part2Seed;
        public float HeightMin, HeightMax;
        public string SourcesPath = "", SourcesSha256 = "";
        public string BaseFrom = "", BaseSha256 = "";

        /// <summary>
        /// The ground file the height map was imported from (amendment 1 §4.1 item 8; "" when the plan has none): its id,
        /// the file's sha256 (the package's JSON), its base's pixel hash, and the import's codes' SHA-256, which is the
        /// height map's values.
        /// </summary>
        public string GroundId = "", GroundFileSha256 = "", GroundBasePixelsSha256 = "", GroundCodesSha256 = "";

        /// <summary>Each live patch ask of the ground file, in the file's order, with its samples' sha256.</summary>
        public readonly List<(string Id, string Sha256)> GroundPatches = new List<(string, string)>();

        /// <summary>One Def: its id, its file, the file's SHA-256 and CD's id ("" when it has none).</summary>
        public readonly List<(string Id, string File, string Sha256, string SourceId)> Defs = new List<(string, string, string, string)>();

        /// <summary>One map: its role (height, splatA … splatF, still), its path, its values' SHA-256 and its file's.</summary>
        public readonly List<(string Role, string Path, string ValuesSha256, string FileSha256)> Maps = new List<(string, string, string, string)>();

        /// <summary>The canonical text. Defs by id, maps in the order given, numbers in ordinal order.</summary>
        public string Write(TerrainPlanResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(1 << 15);
            b.Append("{\n");
            Line(b, "format", Q(Format));
            Line(b, "plan", Q(PlanId));
            Line(b, "seed", Seed.ToString(inv));
            Line(b, "part2Seed", Part2Seed.ToString(inv));
            Line(b, "heightRange", "[" + TerrainPlanSourcesJson.Num(HeightMin) + ", " + TerrainPlanSourcesJson.Num(HeightMax) + "]");
            Line(b, "grid", "{\"w\": " + r.Grid.W.ToString(inv) + ", \"h\": " + r.Grid.H.ToString(inv) + ", \"mpp\": " + TerrainPlanSourcesJson.Num(r.Grid.Mpp) +
                            ", \"x0\": " + TerrainPlanSourcesJson.Num(r.Grid.X0) + ", \"y1\": " + TerrainPlanSourcesJson.Num(r.Grid.Y1) + "}");
            Line(b, "sources", "{\"path\": " + Q(SourcesPath) + ", \"sha256\": " + Q(SourcesSha256) + "}");
            Line(b, "base", "{\"from\": " + Q(BaseFrom) + ", \"sha256\": " + Q(BaseSha256) + "}");
            b.Append(GroundLine());
            b.Append("  \"defs\": [\n");
            var defs = Defs.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
            for (int k = 0; k < defs.Count; k++)
                b.Append("    [").Append(Q(defs[k].Id)).Append(", ").Append(Q(defs[k].File)).Append(", ").Append(Q(defs[k].Sha256)).Append(", ")
                 .Append(Q(defs[k].SourceId ?? "")).Append(k + 1 < defs.Count ? "],\n" : "]\n");
            b.Append("  ],\n");
            Line(b, "derivation", Q(r.Sha256()));
            b.Append("  \"order\": [\n");
            for (int k = 0; k < r.Order.Count; k++) b.Append("    ").Append(Q(r.Order[k])).Append(k + 1 < r.Order.Count ? ",\n" : "\n");
            b.Append("  ],\n  \"numbers\": {\n");
            var nums = r.Numbers.ToList();
            for (int k = 0; k < nums.Count; k++) b.Append("    ").Append(Q(nums[k].Key)).Append(": ").Append(Q(nums[k].Value)).Append(k + 1 < nums.Count ? ",\n" : "\n");
            b.Append("  },\n  \"pools\": [\n");
            for (int k = 0; k < r.Pools.Count; k++)
            {
                var p = r.Pools[k];
                b.Append("    {\"id\": ").Append(Q(p.Id)).Append(", \"source\": ").Append(Q(p.Source)).Append(", \"centre\": [")
                 .Append(N(p.CentreX)).Append(", ").Append(N(p.CentreY)).Append("], \"radii\": [").Append(N(p.RadiusA)).Append(", ").Append(N(p.RadiusB))
                 .Append("], \"rotationDeg\": ").Append(N(p.RotationDeg)).Append(", \"depth\": ").Append(N(p.Depth)).Append(", \"spill\": ").Append(N(p.Spill))
                 .Append(", \"bed\": ").Append(N(p.Bed)).Append(", \"areaM2\": ").Append(N(p.AreaM2)).Append(", \"maxWater\": ").Append(N(p.MaxWater))
                 .Append(k + 1 < r.Pools.Count ? "},\n" : "}\n");
            }
            b.Append("  ],\n  \"pieces\": [\n");
            for (int k = 0; k < r.Pieces.Count; k++)
            {
                var p = r.Pieces[k];
                b.Append("    {\"id\": ").Append(Q(p.Id)).Append(", \"cells\": ").Append(p.Cells.ToString(inv)).Append(", \"alreadyHeld\": ")
                 .Append(p.AlreadyHeld.ToString(inv)).Append(", \"centreHeld\": ").Append(p.CentreHeld ? "true" : "false")
                 .Append(", \"maxMove\": ").Append(N(p.MaxMove)).Append(k + 1 < r.Pieces.Count ? "},\n" : "}\n");
            }
            b.Append("  ],\n  \"maps\": [\n");
            for (int k = 0; k < Maps.Count; k++)
                b.Append("    {\"role\": ").Append(Q(Maps[k].Role)).Append(", \"path\": ").Append(Q(Maps[k].Path)).Append(", \"values\": ")
                 .Append(Q(Maps[k].ValuesSha256)).Append(", \"file\": ").Append(Q(Maps[k].FileSha256)).Append(k + 1 < Maps.Count ? "},\n" : "}\n");
            b.Append("  ]\n}\n");
            return b.ToString();
        }

        /// <summary>
        /// The ground file's line, as <see cref="Write"/> writes it: <c>"ground": {"id", "file", "basePixels", "codes",
        /// "patches": [[id, sha256], …]}</c>; empty when the plan has no ground file.
        /// </summary>
        public string GroundLine()
        {
            if (string.IsNullOrEmpty(GroundId)) return "";
            var b = new StringBuilder();
            b.Append("  \"ground\": {\"id\": ").Append(Q(GroundId)).Append(", \"file\": ").Append(Q(GroundFileSha256)).Append(", \"basePixels\": ")
             .Append(Q(GroundBasePixelsSha256)).Append(", \"codes\": ").Append(Q(GroundCodesSha256)).Append(", \"patches\": [");
            for (int k = 0; k < GroundPatches.Count; k++)
                b.Append(k > 0 ? ", [" : "[").Append(Q(GroundPatches[k].Id)).Append(", ").Append(Q(GroundPatches[k].Sha256)).Append("]");
            return b.Append("]},\n").ToString();
        }

        /// <summary>The values SHA-256 the manifest text records for a map role, or null.</summary>
        public static string ValuesOf(string manifestText, string role)
        {
            if (manifestText == null) return null;
            string key = "{\"role\": " + Q(role) + ", \"path\": ";
            int at = manifestText.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return null;
            int v = manifestText.IndexOf("\"values\": \"", at, StringComparison.Ordinal);
            if (v < 0) return null;
            v += "\"values\": \"".Length;
            int end = manifestText.IndexOf('"', v);
            return end < 0 ? null : manifestText.Substring(v, end - v);
        }

        static string N(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : TerrainPlanSourcesJson.Num(v);

        static void Line(StringBuilder b, string key, string value) => b.Append("  ").Append(Q(key)).Append(": ").Append(value).Append(",\n");

        static string Q(string s)
        {
            var b = new StringBuilder();
            TerrainPlanSourcesJson.Str(b, s ?? "");
            return b.ToString();
        }
    }
}
