#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>G3'S FOUR MASKS</b> (NMC H2): where Nine Mile Creek's third-issue ground takes the splat, the shore,
    /// the fields and the woods, with the town and the campground left out. Each mask is a pure function of G3's
    /// committed codes (<see cref="NineMileCreekGround"/>) and of G3's own plan: the town's key-scene areas, every
    /// building lot, the campground, the wood lots and the roads below, transcribed from the plan by their ids.
    ///
    /// <para><b>⚠ DERIVED DATA, NEVER GRADING.</b> The masks read the ground; nothing here writes a height. Nothing
    /// in the game reads them yet either: the painters (<see cref="NineMileCreekStarterSplat"/>,
    /// <see cref="NineMileCreekShorePainter"/>, <see cref="NineMileCreekFields"/>, <see cref="NineMileCreekWoodLots"/>)
    /// keep their analytic premise until the wave that moves each onto G3, and these are the masks it starts from.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Splat:</b> ground at or above <see cref="NineMileCreekShoreMap.PaintFloorElevation"/>, the band
    /// ladder's footprint. Below it the splat quad clips itself away and the sea is what shows.</item>
    /// <item><b>Shore:</b> the spring tide's reach, <see cref="NineMileCreekShoreMap.SpringLowWater"/> to
    /// <see cref="NineMileCreekShoreMap.SpringHighWater"/>, off still water and off every carriageway.</item>
    /// <item><b>Field:</b> the fields proper, at or above <see cref="NineMileCreekFields.FieldFloorElevation"/>, off
    /// still water and off every carriageway. A farmed layer takes Field and not Wood: the lots give it nothing.</item>
    /// <item><b>Wood:</b> field ground inside a wood lot and clear of every carriageway's verge
    /// (<see cref="NineMileCreekFields.RoadVergeMetres"/>). The river wood is in, as the overhaul's H2 adds it; the
    /// south and middle lots lie in the campground, which keeps its own woods.</item>
    /// </list>
    ///
    /// <para><b>The town</b> is its five key-scene areas and every building lot the plan lays (the town's 25, the
    /// waterfront's 7, the south shore's 9 houses, the truck park and the laydown), each with the
    /// <see cref="NineMileCreekFields.TownLotBreathingMetres"/> the region keeps clear round a lot. <b>The camp</b> is
    /// <c>lot.nmc_campground</c>'s outline. Neither takes any mask.</para>
    ///
    /// <para><b>⭐ THE SAME BYTES ON EVERY RUNTIME.</b> A height gate compares a texel's 16-bit code with the first
    /// code at or past its threshold, and every shape test runs in doubles from the texel's centre, so a headless
    /// run and the editor derive the same masks and a test can demand equality. Each file is an 8-bit greyscale
    /// PNG on G3's grid, rows top-down as G3's own files hold them: 255 in, 0 out.</para>
    /// </summary>
    public static class NineMileCreekGroundMasks
    {
        public enum Kind { Splat, Shore, Field, Wood }

        public static readonly Kind[] Kinds = { Kind.Splat, Kind.Shore, Kind.Field, Kind.Wood };

        /// <summary>A texel the mask takes.</summary>
        public const byte On = 255;

        /// <summary>G3's grid: half-metre texels from its west edge and its north edge (row 0 is the north).</summary>
        public const double TexelMetres = 0.5, WestEdge = -380, NorthEdge = 280;

        public static string PathOf(Kind kind) => NineMileCreekGround.Folder + "/NineMileCreekGround_" + kind + ".png";

        // =========================================================================================================
        //  THE GROUND, AS ITS FILES HOLD IT
        // =========================================================================================================

        /// <summary>G3's height and still codes, rows top-down as the files hold them.</summary>
        public sealed class Ground
        {
            public readonly ushort[] Height, Still;
            public Ground(ushort[] height, ushort[] still)
            {
                Height = height ?? throw new ArgumentNullException(nameof(height));
                Still = still ?? throw new ArgumentNullException(nameof(still));
            }
        }

        public static Ground ReadGround(string projectRoot = ".") =>
            new Ground(ReadCodes(projectRoot, NineMileCreekGround.HeightPath),
                       ReadCodes(projectRoot, NineMileCreekGround.StillPath));

        static ushort[] ReadCodes(string projectRoot, string path)
        {
            var image = TerrainPlanPng.ReadFile(Path.Combine(projectRoot, path));
            if (image.Width != NineMileCreekGround.Width || image.Height != NineMileCreekGround.Height ||
                image.BitDepth != 16 || image.ColourType != 0)
                throw new InvalidDataException("[NineMileCreekGroundMasks] " + path +
                                               " must be G3's 1520 by 1120 R16 greyscale.");
            return image.Samples;
        }

        /// <summary>A code's height, in doubles: <c>min + (max − min) · code / 65535</c>.</summary>
        public static double HeightOfCode(int code) =>
            NineMileCreekGround.Minimum +
            ((double)NineMileCreekGround.Maximum - NineMileCreekGround.Minimum) * (code / 65535.0);

        /// <summary>The first code whose height is at or above <paramref name="metres"/> (65536 if none).</summary>
        public static int FirstCodeAtOrAbove(double metres) => FirstCode(metres, orAbove: true);

        /// <summary>The first code whose height is above <paramref name="metres"/> (65536 if none).</summary>
        public static int FirstCodeAbove(double metres) => FirstCode(metres, orAbove: false);

        static int FirstCode(double metres, bool orAbove)
        {
            bool Past(int code) => orAbove ? HeightOfCode(code) >= metres : HeightOfCode(code) > metres;
            double range = (double)NineMileCreekGround.Maximum - NineMileCreekGround.Minimum;
            int c = (int)Math.Max(0, Math.Min(65536, Math.Ceiling((metres - NineMileCreekGround.Minimum) / range * 65535.0)));
            while (c > 0 && Past(c - 1)) c--;
            while (c <= 65535 && !Past(c)) c++;
            return c;
        }

        public static double CentreX(int column) => WestEdge + (column + 0.5) * TexelMetres;
        public static double CentreY(int row) => NorthEdge - (row + 0.5) * TexelMetres;

        /// <summary>The index (row × width + column) of the texel holding a world point.</summary>
        public static int TexelOf(double x, double y) =>
            (int)Math.Floor((NorthEdge - y) / TexelMetres) * NineMileCreekGround.Width +
            (int)Math.Floor((x - WestEdge) / TexelMetres);

        // =========================================================================================================
        //  THE FOUR MASKS
        // =========================================================================================================

        /// <summary>All four masks in <see cref="Kinds"/> order, one byte a texel, file order.</summary>
        public static byte[][] DeriveAll(Ground ground)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            int width = NineMileCreekGround.Width, height = NineMileCreekGround.Height;
            if (ground.Height.Length != width * height || ground.Still.Length != width * height)
                throw new InvalidDataException("[NineMileCreekGroundMasks] G3's grids are 1520 by 1120.");

            var masks = new byte[Kinds.Length][];
            for (int k = 0; k < masks.Length; k++) masks[k] = new byte[width * height];
            int splatFloor = FirstCodeAtOrAbove(NineMileCreekShoreMap.PaintFloorElevation);
            int shoreFloor = FirstCodeAtOrAbove(NineMileCreekShoreMap.SpringLowWater);
            int shoreAbove = FirstCodeAbove(NineMileCreekShoreMap.SpringHighWater);
            int fieldFloor = FirstCodeAtOrAbove(NineMileCreekFields.FieldFloorElevation);
            double verge = NineMileCreekFields.RoadVergeMetres;

            for (int row = 0; row < height; row++)
            {
                double y = CentreY(row);
                for (int column = 0; column < width; column++)
                {
                    int i = row * width + column, code = ground.Height[i];
                    bool still = ground.Still[i] != 0;
                    bool splat = code >= splatFloor;
                    bool shore = code >= shoreFloor && code < shoreAbove && !still;
                    bool field = code >= fieldFloor && !still;
                    if (!splat && !shore && !field) continue;

                    double x = CentreX(column);
                    if (InTown(x, y) || InCamp(x, y)) continue;
                    if (splat) masks[(int)Kind.Splat][i] = On;
                    if (!shore && !field) continue;

                    double clear = RoadClearance(x, y);
                    if (clear <= 0) continue;
                    if (shore) masks[(int)Kind.Shore][i] = On;
                    if (!field) continue;
                    masks[(int)Kind.Field][i] = On;
                    if (clear > verge && InAWoodLot(x, y)) masks[(int)Kind.Wood][i] = On;
                }
            }
            return masks;
        }

        public static byte[] Derive(Kind kind, Ground ground) => DeriveAll(ground)[(int)kind];

        public static int Count(byte[] mask)
        {
            int n = 0;
            foreach (byte b in mask) if (b == On) n++;
            return n;
        }

        /// <summary>The town: its key-scene areas, and every building lot with the region's breathing room.</summary>
        public static bool InTown(double x, double y)
        {
            foreach (Box area in TownAreas)
                if (area.Contains(x, y)) return true;
            double margin = NineMileCreekFields.TownLotBreathingMetres;
            foreach (Lot lot in BuildingLots)
                if (lot.Contains(x, y, margin)) return true;
            return false;
        }

        /// <summary>Inside <c>lot.nmc_campground</c>'s outline (even-odd).</summary>
        public static bool InCamp(double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = CampOutline.Length - 1; i < CampOutline.Length; j = i++)
            {
                Point a = CampOutline[i], b = CampOutline[j];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        public static bool InAWoodLot(double x, double y)
        {
            foreach (Disc lot in WoodLots)
                if (lot.Contains(x, y)) return true;
            return false;
        }

        /// <summary>How far a point stands off the nearest carriageway's edge (negative on a carriageway).</summary>
        public static double RoadClearance(double x, double y)
        {
            double best = double.PositiveInfinity;
            foreach (Road road in Roads)
                for (int s = 1; s < road.Points.Length; s++)
                    best = Math.Min(best, DistanceToSegment(x, y, road.Points[s - 1], road.Points[s]) - road.Width / 2);
            return best;
        }

        static double DistanceToSegment(double x, double y, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, length2 = dx * dx + dy * dy;
            double t = length2 > 0 ? ((x - a.X) * dx + (y - a.Y) * dy) / length2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double px = a.X + t * dx - x, py = a.Y + t * dy - y;
            return Math.Sqrt(px * px + py * py);
        }

        // =========================================================================================================
        //  THE FILES
        // =========================================================================================================

        /// <summary>
        /// Derive the four masks and write each one whose decoded content differs from its file (an unchanged
        /// mask keeps its bytes). Returns how many it wrote. The menu and the slot's apply both come here.
        /// </summary>
        public static int Write(string projectRoot = ".")
        {
            byte[][] masks = DeriveAll(ReadGround(projectRoot));
            int written = 0;
            foreach (Kind kind in Kinds)
            {
                string path = Path.Combine(projectRoot, PathOf(kind));
                if (File.Exists(path) && SameAs(path, masks[(int)kind])) continue;
                File.WriteAllBytes(path, EncodeGreyPng(masks[(int)kind], NineMileCreekGround.Width, NineMileCreekGround.Height));
                written++;
            }
            return written;
        }

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Write G3 masks")]
        public static void WriteMenu()
        {
            int written = Write();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[NineMileCreekGroundMasks] wrote " + written + " of " + Kinds.Length + " masks.");
        }

        /// <summary>A committed mask's bytes, file order; refuses anything but G3's grid at 8-bit greyscale.</summary>
        public static byte[] ReadMask(string path)
        {
            var image = TerrainPlanPng.ReadFile(path);
            if (image.Width != NineMileCreekGround.Width || image.Height != NineMileCreekGround.Height ||
                image.BitDepth != 8 || image.ColourType != 0)
                throw new InvalidDataException("[NineMileCreekGroundMasks] " + path + " must be 1520 by 1120, 8-bit greyscale.");
            return image.Channel8(0);
        }

        static bool SameAs(string path, byte[] mask)
        {
            byte[] old;
            try { old = ReadMask(path); }
            catch (InvalidDataException) { return false; }
            if (old.Length != mask.Length) return false;
            for (int i = 0; i < old.Length; i++)
                if (old[i] != mask[i]) return false;
            return true;
        }

        /// <summary>One byte a texel, file order, as an 8-bit greyscale PNG (filter None, one IDAT).</summary>
        public static byte[] EncodeGreyPng(byte[] grey, int width, int height)
        {
            if (grey == null || width <= 0 || height <= 0 || grey.Length != width * height)
                throw new ArgumentException("[NineMileCreekGroundMasks] a mask PNG needs exactly width × height bytes.");
            var raw = new byte[(width + 1) * height];
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(grey, row * width, raw, row * (width + 1) + 1, width);
            byte[] deflated;
            using (var stream = new MemoryStream())
            {
                using (var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
                    deflate.Write(raw, 0, raw.Length);
                deflated = stream.ToArray();
            }
            var zlib = new byte[deflated.Length + 6];
            zlib[0] = 0x78;
            zlib[1] = 0x9C;
            Buffer.BlockCopy(deflated, 0, zlib, 2, deflated.Length);
            PutU32(zlib, zlib.Length - 4, Adler32(raw));

            var header = new byte[13];
            PutU32(header, 0, (uint)width);
            PutU32(header, 4, (uint)height);
            header[8] = 8;   // bit depth; colour type 0 (grey), deflate, filter 0, not interlaced
            using (var png = new MemoryStream())
            {
                png.Write(Signature, 0, Signature.Length);
                WriteChunk(png, "IHDR", header);
                WriteChunk(png, "IDAT", zlib);
                WriteChunk(png, "IEND", Array.Empty<byte>());
                return png.ToArray();
            }
        }

        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var head = new byte[8];
            PutU32(head, 0, (uint)data.Length);
            for (int i = 0; i < 4; i++) head[4 + i] = (byte)type[i];
            stream.Write(head, 0, head.Length);
            stream.Write(data, 0, data.Length);
            var tail = new byte[4];
            PutU32(tail, 0, Crc32(data, Crc32(head, 0xFFFFFFFFu, 4)) ^ 0xFFFFFFFFu);
            stream.Write(tail, 0, tail.Length);
        }

        static uint Crc32(byte[] bytes, uint crc, int from = 0)
        {
            for (int i = from; i < bytes.Length; i++)
            {
                crc ^= bytes[i];
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
            return crc;
        }

        static uint Adler32(byte[] bytes)
        {
            uint a = 1, b = 0;
            foreach (byte x in bytes)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        static void PutU32(byte[] bytes, int at, uint value)
        {
            bytes[at] = (byte)(value >> 24);
            bytes[at + 1] = (byte)(value >> 16);
            bytes[at + 2] = (byte)(value >> 8);
            bytes[at + 3] = (byte)value;
        }

        // =========================================================================================================
        //  G3'S PLAN, BY ITS IDS (areas[], lots.town/waterfront/south_shore_houses/working, lots.campground,
        //  lots.wood_lots, roads[]). A lot is its footprint about its point: w along x when it faces N or S (or
        //  nothing), along y when it faces E or W.
        // =========================================================================================================

        public readonly struct Point
        {
            public readonly double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
        }

        public readonly struct Box
        {
            public readonly string Id;
            public readonly double X0, X1, Y0, Y1;
            public Box(string id, double x0, double x1, double y0, double y1) { Id = id; X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; }
            public bool Contains(double x, double y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
        }

        public readonly struct Lot
        {
            public readonly string Id;
            public readonly double X, Y, SizeX, SizeY;
            public Lot(string id, double x, double y, double sizeX, double sizeY) { Id = id; X = x; Y = y; SizeX = sizeX; SizeY = sizeY; }
            public bool Contains(double x, double y, double margin) =>
                Math.Abs(x - X) <= SizeX / 2 + margin && Math.Abs(y - Y) <= SizeY / 2 + margin;
        }

        /// <summary>G3's building lots by id, read only: the tests that hold the lots to the roads (NMC H4) read the
        /// same boxes the masks are cut round.</summary>
        public static IReadOnlyList<Lot> Lots => BuildingLots;

        public readonly struct Disc
        {
            public readonly string Id;
            public readonly double X, Y, Radius;
            public Disc(string id, double x, double y, double radius) { Id = id; X = x; Y = y; Radius = radius; }
            public bool Contains(double x, double y) => (x - X) * (x - X) + (y - Y) * (y - Y) <= Radius * Radius;
        }

        public sealed class Road
        {
            public readonly string Id;
            public readonly double Width;
            public readonly Point[] Points;
            public Road(string id, double width, params Point[] points) { Id = id; Width = width; Points = points; }
        }

        static readonly Box[] TownAreas =
        {
            new Box("area.nmc_water_street_block", -276, -171, 216, 281),
            new Box("area.nmc_east_lane", -188, -93, 217, 277),
            new Box("area.nmc_plaza_docks_yard", -288, -186, 78, 170),
            new Box("area.nmc_plaza_lot_south", -282, -190, 55, 78),
            new Box("area.nmc_plaza_flats", -292, -183, 1, 66),
        };

        static readonly Lot[] BuildingLots =
        {
            new Lot("lot.nmc_town_plaza", -227, 120.5, 58, 24),
            new Lot("lot.nmc_restaurant_bar", -226, 253.26, 13.25, 17.5),
            new Lot("lot.nmc_chandlery", -160, 237.26, 9, 8),
            new Lot("lot.nmc_parish_hall", -152, 62, 12, 18),
            new Lot("lot.nmc_house_south", -200, -8, 8.7, 7),
            new Lot("lot.nmc_house_mill", -264.5, 152, 7, 8.7),
            new Lot("lot.nmc_boat_shed", -94, 158, 10, 8),
            new Lot("lot.nmc_plaza_cannery", -271.45, 37.55, 30.3, 20.9),
            new Lot("lot.nmc_plaza_walkup", -240.9, 38.55, 22.6, 18.9),
            new Lot("lot.nmc_plaza_terrace", -201.6, 40.2, 28, 11.6),
            new Lot("lot.nmc_main_street_outfitter", -262, 246.61, 8, 11),
            new Lot("lot.nmc_main_street_flowers", -255, 246.63, 6, 10.5),
            new Lot("lot.nmc_main_street_antiques", -249, 247.11, 6, 11),
            new Lot("lot.nmc_main_street_west_block", -239.5, 248.2, 13, 12.5),
            new Lot("lot.nmc_main_street_corner_block", -194.3, 247.63, 11.4, 13.5),
            new Lot("lot.nmc_main_street_gallery", -240, 275, 12, 9.6),
            new Lot("lot.nmc_main_street_pharmacy", -228, 275, 10, 9.6),
            new Lot("lot.nmc_main_street_wine", -218.5, 275, 8, 9.6),
            new Lot("lot.nmc_main_street_visitor", -207.75, 275, 12, 9.6),
            new Lot("lot.nmc_main_street_post_office", -196.37, 275, 8.75, 9.6),
            new Lot("lot.nmc_main_street_east_corner", -168.9, 240.04, 8.2, 12.6),
            new Lot("lot.nmc_main_street_east_block", -148.9, 238.36, 12.6, 12.4),
            new Lot("lot.nmc_main_street_east_decker", -134.8, 237.2, 8.4, 12),
            new Lot("lot.nmc_main_street_east_captains", -123.6, 236.5, 12, 11),
            new Lot("lot.nmc_main_street_east_row", -161.2, 262.75, 22.4, 10.5),
            new Lot("lot.nmc_waterfront_1", -72, 264.1, 20.5, 10.3),
            new Lot("lot.nmc_waterfront_2", -46, 263.15, 16.2, 8.5),
            new Lot("lot.nmc_waterfront_3", -20, 260.85, 22.2, 10.75),
            new Lot("lot.nmc_waterfront_4", 6, 258.25, 21.1, 10.15),
            new Lot("lot.nmc_waterfront_5", -58, 220.75, 20.3, 9.5),
            new Lot("lot.nmc_waterfront_6", -32, 218.5, 11, 9),
            new Lot("lot.nmc_waterfront_7", -6, 216.75, 17.6, 9.5),
            new Lot("lot.nmc_south_shore_estate_1", -345, -148.5, 21, 10.5),
            new Lot("lot.nmc_south_shore_estate_2", -305, -143, 29, 10),
            new Lot("lot.nmc_south_shore_estate_3", -262, -142, 34, 10.5),
            new Lot("lot.nmc_south_shore_estate_4", -356, -100, 35, 10),
            new Lot("lot.nmc_south_shore_estate_5", -316, -101, 19.6, 10),
            new Lot("lot.nmc_south_shore_estate_6", -273, -95, 20.8, 9.5),
            new Lot("lot.nmc_south_shore_6", -196, -160, 10, 7.2),
            new Lot("lot.nmc_south_shore_7", -172, -161, 8, 9),
            new Lot("lot.nmc_south_shore_8", -150, -161, 9.6, 7.6),
            new Lot("lot.nmc_truck_park", -131, 78, 20.1, 13.4),
            new Lot("lot.nmc_laydown", -95, 62, 44.8, 35.9),
        };

        static readonly Point[] CampOutline =
        {
            new Point(-128, 38), new Point(4, 38), new Point(14, 30), new Point(16, 0), new Point(20, -40),
            new Point(30, -90), new Point(40, -120), new Point(52, -140), new Point(20, -156), new Point(-40, -154),
            new Point(-128, -150),
        };

        static readonly Disc[] WoodLots =
        {
            new Disc("woodlot.nmc_south", -61.1, -96, 30),
            new Disc("woodlot.nmc_middle", -77.3, 0, 30),
            new Disc("woodlot.nmc_north", -73.4, 192, 30),
            new Disc("woodlot.nmc_river_wood", -332, 236, 26),
        };

        static readonly Road[] Roads =
        {
            new Road("road.nmc.route_91", 5.8, new Point(-186, 280), new Point(-181, 242), new Point(-177.6, 222),
                new Point(-176.8, 205), new Point(-176.6, 197), new Point(-177.1, 147), new Point(-177.45, 92),
                new Point(-177.6, 57), new Point(-181, 10), new Point(-190, -40), new Point(-206, -82),
                new Point(-229.4, -109), new Point(-240, -114.6), new Point(-252, -117.6), new Point(-266, -119),
                new Point(-284, -119.6), new Point(-302, -119.8), new Point(-316, -120.4), new Point(-328, -122),
                new Point(-339, -124.8), new Point(-349, -128.6), new Point(-358, -133), new Point(-366, -137.4),
                new Point(-373, -140.6), new Point(-380, -142)),
            new Road("road.nmc.wharf_road", 5.8, new Point(-177.45, 92), new Point(-100, 92), new Point(-40, 92),
                new Point(14, 92), new Point(22, 95), new Point(27, 101), new Point(29, 108), new Point(30.5, 114),
                new Point(34, 118), new Point(50, 118)),
            new Road("road.nmc.bar_road", 5, new Point(58, -146), new Point(36, -112), new Point(20, -56),
                new Point(14, 0), new Point(20, 78), new Point(-16, 92)),
            new Road("road.nmc.water_street", 5.4, new Point(-271, 228.81), new Point(-266, 229), new Point(-240, 230),
                new Point(-205, 229.5), new Point(-177.4, 228), new Point(-166, 227.17), new Point(-150, 226),
                new Point(-139, 224), new Point(-128, 222)),
            new Road("road.nmc.mill_road", 5, new Point(-177.3, 158), new Point(-205, 160), new Point(-236, 163),
                new Point(-262, 166), new Point(-282, 170)),
            new Road("road.nmc.north_creek_lane", 4.2, new Point(-184.6, 273.6), new Point(-170, 274),
                new Point(-150, 274.3), new Point(-130, 274.4), new Point(-110, 274.5), new Point(-70, 274),
                new Point(-30, 272), new Point(10, 268), new Point(30, 264)),
            new Road("road.nmc.creek_lane_south", 4.2, new Point(-128, 222), new Point(-98, 214), new Point(-60, 208),
                new Point(-24, 205), new Point(10, 203), new Point(28, 202)),
            new Road("road.nmc.stream_lane", 5, new Point(-116.5, 94), new Point(-116.5, 145.5), new Point(-104, 154),
                new Point(-96, 156)),
            new Road("road.nmc.shore_lane", 3.1, new Point(-226, -110.5), new Point(-212, -134), new Point(-190, -146),
                new Point(-156, -149.5), new Point(-140, -150)),
            new Road("road.nmc.camp_road", 3.6, new Point(17.8, 30), new Point(4, 30), new Point(-10, 28)),
            new Road("road.nmc.camp_loop_a", 3.6, new Point(-10, 28), new Point(-4, 8), new Point(2, -20),
                new Point(8, -48), new Point(14, -76), new Point(20, -104), new Point(16, -124), new Point(2, -128),
                new Point(-12, -118), new Point(-20, -92), new Point(-24, -64), new Point(-26, -36), new Point(-24, -8),
                new Point(-16, 14), new Point(-10, 28)),
            new Road("road.nmc.camp_loop_b_stem", 3.6, new Point(-25, -64), new Point(-44, -66), new Point(-62, -66),
                new Point(-74, -68)),
            new Road("road.nmc.camp_loop_b", 3.6, new Point(-74, -68), new Point(-94, -76), new Point(-110, -96),
                new Point(-114, -120), new Point(-100, -138), new Point(-82, -137), new Point(-74, -120),
                new Point(-76, -96), new Point(-74, -68)),
            new Road("road.nmc.camp_loop_c_stem", 3.6, new Point(-10, 28), new Point(-30, 31), new Point(-50, 32),
                new Point(-62, 30)),
            new Road("road.nmc.camp_loop_c", 3.6, new Point(-62, 30), new Point(-80, 42), new Point(-104, 40),
                new Point(-120, 27), new Point(-119, 8), new Point(-106, -1), new Point(-90, 2), new Point(-76, 14),
                new Point(-62, 30)),
            new Road("road.nmc_main_street_lane", 6.5, new Point(-185.5, 265.45), new Point(-268, 265.45)),
            new Road("road.nmc_main_street_east_lane", 7, new Point(-182.3, 251.6), new Point(-147, 251.6)),
            new Road("road.nmc_plaza_flats_street", 6, new Point(-285.75, 12.4), new Point(-191.25, 12.4)),
            new Road("road.nmc_plaza_flats_link", 6, new Point(-221, 64), new Point(-221, 12.4)),
        };
    }
}
#endif
