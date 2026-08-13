using System;
using System.IO;
using System.Text;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Renders the ROAD KIT's <b>adjacency proof sheets</b> — the artifacts that can catch a wrong
    /// blob index, which nothing else can.
    ///
    /// <para><b>⭐ WHY A PER-TILE CHECK IS WORTHLESS HERE.</b> Blob-47 correctness is combinatoric: the
    /// 47 cases exist to MEET EACH OTHER. A tile drawn from the wrong atlas cell is a perfectly good
    /// tile — correct palette, correct wear, correct shadow — so it passes every check that looks at
    /// one tile. It only fails where it touches its neighbours, as a carriageway that stops dead at a
    /// junction or a kerb that runs into grass. So the proof has to be a rendered NETWORK, graded by
    /// eye.</para>
    ///
    /// <para>Two sheets, because they fail differently:</para>
    /// <list type="number">
    /// <item><b>The mask grid</b> (<see cref="RenderMaskGrid"/>) — each of the 47 tiles drawn with
    /// exactly the neighbours its own contract entry DECLARES. Rigorous and exhaustive: if index and
    /// mask have drifted apart, that tile's road points at bare ground while a neighbour it should not
    /// have touches it. Every one of the 47 is on the sheet, so nothing hides.</item>
    /// <item><b>The village network</b> (<see cref="RenderNetwork"/>) — a hand-drawn layout with the
    /// junctions a real region actually asks for: crossroads, tees off a main street, a loop, dead
    /// ends, a 3-wide yard. Catches what the grid cannot, namely whether the set reads as a road at
    /// all when laid continuously.</item>
    /// </list>
    /// </summary>
    public static class RoadKitProofSheet
    {
        const string ResultGlobal = "__hhRoadProofCell";

        /// <summary>Where the proof sheets land. Not shipped art — a review artifact.</summary>
        public const string OutputFolder = "docs/art/rigs/road-path-kit-v3/proof";

        /// <summary>
        /// The village network, as ASCII so the layout is reviewable in the diff rather than being a
        /// wall of coordinates. <c>#</c> is road, <c>.</c> is not.
        ///
        /// <para>Chosen to contain a 4-way crossroads, tees on all four bearings, all four bends,
        /// horizontal and vertical straights, dead-end caps, an isolated tile, and several solid
        /// blocks and courtyards whose concave corners are what exercise the diagonal fillet bits.</para>
        ///
        /// <para><b>⚠ It exercises 29 of the 47, not all of them, and that is deliberate.</b>
        /// <see cref="CoverageReport"/> names the ones it misses rather than letting the sheet imply
        /// full coverage. Pushing this map to 47/47 would mean drawing shapes no village has, which
        /// would turn it into a second, worse copy of the mask grid — and the mask grid already
        /// covers all 47 exhaustively and by construction. The two sheets have different jobs: the
        /// grid proves every index, this one proves a realistic layout reads as a road.</para>
        /// </summary>
        public static readonly string[] VillageMap =
        {
            "..........................................",
            "....#.............#.......................",
            "....#.............#....####...............",
            "..#####.......#########.####..............",
            "....#.............#....####...............",
            "....#.............#....####...............",
            "....#####.........#.......................",
            "........#.........#....#####..............",
            "........#.........#....#...#..............",
            "....#####.........#....#.###..............",
            "....#.............#....#.#................",
            "....#.............#....###................",
            "....#######################...............",
            "..........#.......#.......................",
            "..........#####...#...####................",
            "..............#...#...#####...............",
            "..............#####...######..............",
            "......................######..............",
            "..#...................######..............",
            "..........................................",
        };

        /// <summary>
        /// Sheet 1: every one of the 47 tiles, each surrounded by exactly the neighbours its contract
        /// entry declares. Laid out 8 across.
        /// </summary>
        public static byte[] RenderMaskGrid(IRigScriptHost host, string surface,
                                            out int width, out int height)
        {
            const int across = 8, pad = 1;          // pad in cells, around each 3×3 neighbourhood
            const int block = 3 + pad * 2;          // cells per tile's little plot
            int down = (RoadKitV3.BlobCount + across - 1) / across;

            int cols = across * block, rows = down * block;
            var map = new bool[cols, rows];

            var entries = RoadKitContract.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                int bx = (i % across) * block + pad + 1;   // centre of this tile's plot
                int by = (i / across) * block + pad + 1;
                int m = entries[i].Mask;

                map[bx, by] = true;
                // Screen rows increase DOWNWARD, so north is by-1.
                if ((m & RoadKitV3.BitN) != 0) map[bx, by - 1] = true;
                if ((m & RoadKitV3.BitS) != 0) map[bx, by + 1] = true;
                if ((m & RoadKitV3.BitE) != 0) map[bx + 1, by] = true;
                if ((m & RoadKitV3.BitW) != 0) map[bx - 1, by] = true;
                if ((m & RoadKitV3.BitNE) != 0) map[bx + 1, by - 1] = true;
                if ((m & RoadKitV3.BitNW) != 0) map[bx - 1, by - 1] = true;
                if ((m & RoadKitV3.BitSE) != 0) map[bx + 1, by + 1] = true;
                if ((m & RoadKitV3.BitSW) != 0) map[bx - 1, by + 1] = true;
            }

            return Composite(host, surface, map, out width, out height);
        }

        /// <summary>Sheet 2: the village network.</summary>
        public static byte[] RenderNetwork(IRigScriptHost host, string surface,
                                           out int width, out int height)
        {
            int rows = VillageMap.Length, cols = VillageMap[0].Length;
            var map = new bool[cols, rows];
            for (int y = 0; y < rows; y++)
            {
                if (VillageMap[y].Length != cols)
                    throw new InvalidOperationException(
                        $"[RoadKitProofSheet] VillageMap row {y} is {VillageMap[y].Length} chars, " +
                        $"row 0 is {cols}. A ragged map would silently shift every cell below it.");
                for (int x = 0; x < cols; x++) map[x, y] = VillageMap[y][x] == '#';
            }
            return Composite(host, surface, map, out width, out height);
        }

        /// <summary>
        /// Lay a boolean road map down as pixels, following the kit's own composite rule.
        ///
        /// <para><b>Two passes, painter N→S.</b> Pass 1 blits rows 0..41 of every cell; pass 2 blits
        /// every cell's skirt in the same order. This is not decoration — a single pass over whole
        /// cells lets each cell's ground square erase the kerb and drop faces of the cell north of it,
        /// which is the exact failure the split into two tilemaps exists to prevent. The proof sheet
        /// composites the way the GAME will, or it is not a proof.</para>
        ///
        /// <para>Shadow pixels carry real alpha, so both passes blend rather than copy.</para>
        /// </summary>
        static byte[] Composite(IRigScriptHost host, string surface, bool[,] map,
                                out int width, out int height)
        {
            int cols = map.GetLength(0), rows = map.GetLength(1);
            width = cols * RoadKitV3.Tile;
            height = rows * RoadKitV3.Tile + (RoadKitV3.CellH - RoadKitV3.Tile);
            var canvas = new byte[width * height * 4];

            for (int pass = 0; pass < 2; pass++)
            {
                int rowFrom = pass == 0 ? 0 : RoadKitV3.Skirt;
                int rowTo = pass == 0 ? RoadKitV3.Skirt : RoadKitV3.CellH;

                for (int y = 0; y < rows; y++)          // painter N→S: screen row 0 is northmost
                for (int x = 0; x < cols; x++)
                {
                    if (!map[x, y]) continue;

                    int mask = MaskAt(map, x, y);
                    int index = RoadKitContract.IndexForMask(mask);

                    // Pattern space is world-oriented: gy increases NORTHWARD.
                    byte[] cell = RenderCell(host, surface, index, x, rows - 1 - y);

                    int ox = x * RoadKitV3.Tile;
                    int oy = y * RoadKitV3.Tile - RoadKitV3.Head;
                    for (int r = rowFrom; r < rowTo; r++)
                    {
                        int cy = oy + r;
                        if (cy < 0 || cy >= height) continue;
                        for (int c = 0; c < RoadKitV3.Tile; c++)
                        {
                            int si = (r * RoadKitV3.Tile + c) * 4;
                            int a = cell[si + 3];
                            if (a == 0) continue;
                            int di = (cy * width + ox + c) * 4;
                            if (a == 255)
                            {
                                canvas[di] = cell[si]; canvas[di + 1] = cell[si + 1];
                                canvas[di + 2] = cell[si + 2]; canvas[di + 3] = 255;
                                continue;
                            }
                            // Normal (non-premultiplied) source-over.
                            float sa = a / 255f, da = canvas[di + 3] / 255f;
                            float outA = sa + da * (1f - sa);
                            for (int ch = 0; ch < 3; ch++)
                            {
                                float sc = cell[si + ch] / 255f, dc = canvas[di + ch] / 255f;
                                float o = outA <= 0f ? 0f : (sc * sa + dc * da * (1f - sa)) / outA;
                                canvas[di + ch] = (byte)Mathf.Clamp(Mathf.RoundToInt(o * 255f), 0, 255);
                            }
                            canvas[di + 3] = (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255);
                        }
                    }
                }
            }

            return canvas;
        }

        /// <summary>The raw 8-bit neighbourhood of a cell. Off-map is "not road".</summary>
        public static int MaskAt(bool[,] map, int x, int y)
        {
            int cols = map.GetLength(0), rows = map.GetLength(1);
            bool At(int px, int py) =>
                px >= 0 && py >= 0 && px < cols && py < rows && map[px, py];

            // Screen rows increase downward, so NORTH is y-1.
            return RoadKitV3.Mask(
                n: At(x, y - 1), e: At(x + 1, y), s: At(x, y + 1), w: At(x - 1, y),
                ne: At(x + 1, y - 1), se: At(x + 1, y + 1),
                sw: At(x - 1, y + 1), nw: At(x - 1, y - 1));
        }

        static byte[] RenderCell(IRigScriptHost host, string surface, int index, int gx, int gy)
        {
            host.Execute($"globalThis.{ResultGlobal} = {ProofCellExpression(surface, index, gx, gy)};");
            return host.EvaluateBytes($"{ResultGlobal}.data");
        }

        /// <summary>
        /// Same shape as the shipped bake, but at the cell's REAL pattern position rather than the
        /// atlas's 2 m sampling step — a proof sheet has to show the pattern flowing across cells the
        /// way a placed road will, which is the whole point of global-metre patterns.
        /// </summary>
        static string ProofCellExpression(string surface, int index, int gx, int gy)
        {
            const string G = RoadKitV3.RigGlobalName;
            return $@"(function(){{
                var b = {G}.BLOB47[{index}];
                var o = {{ con:b.con, diag:b.diag, axis:b.axis,
                          wear:'{RoadKitV3.BakedWear}', profile:'{RoadKitV3.BakedProfile}',
                          piece:'{RoadKitV3.BakedPiece}', markings:[],
                          ground:'{RoadKitV3.BakedGround}',
                          gx:{gx}, gy:{gy}, seed:{RoadKitV3.BakedSeed} }};
                return {G}.render('{surface}', o);
            }})()";
        }

        /// <summary>
        /// Which of the 47 the village network actually exercises — reported so the PR body can say
        /// what the eyeball grade covered instead of implying it covered everything.
        /// </summary>
        public static string CoverageReport()
        {
            int rows = VillageMap.Length, cols = VillageMap[0].Length;
            var map = new bool[cols, rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++) map[x, y] = VillageMap[y][x] == '#';

            var seen = new bool[RoadKitV3.BlobCount];
            int cells = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if (map[x, y]) { seen[RoadKitContract.IndexForMask(MaskAt(map, x, y))] = true; cells++; }

            int n = 0;
            var missing = new StringBuilder();
            for (int i = 0; i < seen.Length; i++)
                if (seen[i]) n++;
                else missing.Append(missing.Length == 0 ? "" : ", ")
                            .Append($"{i}({RoadKitContract.Entries[i].Label})");

            return $"village network: {cells} road cells exercising {n}/{RoadKitV3.BlobCount} tiles. " +
                   (n == RoadKitV3.BlobCount
                       ? "All 47 covered."
                       : $"NOT exercised by the network (the mask grid covers all 47): {missing}");
        }
    }
}
