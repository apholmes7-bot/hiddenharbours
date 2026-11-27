using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The faulted bedding's tunables for one region's cliffs, read off its <see cref="CliffFaceMixDef"/>. The default
    /// is off: a region with no mix, or a mix with no block size, draws every face as it is mapped.
    /// </summary>
    public readonly struct CliffFault
    {
        /// <summary>x: the mean width of a block, y: the mean height of a tier of them. Ground texels.</summary>
        public readonly Vector2 BlockTexels;
        /// <summary>The most a block's bedding is thrown up or down, whole ground texels.</summary>
        public readonly int ThrowTexels;
        /// <summary>The most a block's rock is slid along its bedding, whole ground texels.</summary>
        public readonly int SlideTexels;
        /// <summary>How many rows a crack runs before it may step a texel left or right.</summary>
        public readonly int CrackRunTexels;
        /// <summary>The share of block edges that carry a crack, 0 to 1.</summary>
        public readonly float CrackShare;
        /// <summary>The step a block's facing snaps to for its shade, degrees. 0 leaves the chunk's own.</summary>
        public readonly float FacetDegrees;

        public CliffFault(Vector2 blockTexels, int throwTexels, int slideTexels, int crackRunTexels, float crackShare,
                          float facetDegrees)
        {
            BlockTexels = blockTexels;
            ThrowTexels = Mathf.Max(0, throwTexels);
            SlideTexels = Mathf.Max(0, slideTexels);
            CrackRunTexels = Mathf.Max(1, crackRunTexels);
            CrackShare = Mathf.Clamp01(crackShare);
            FacetDegrees = Mathf.Max(0f, facetDegrees);
        }

        public bool On => BlockTexels.x > 0f && BlockTexels.y > 0f;

        public static CliffFault From(CliffFaceMixDef mix) => mix == null
            ? default
            : new CliffFault(mix.FaultBlockTexels, mix.FaultThrowTexels, mix.FaultSlideTexels, mix.FaultCrackRunTexels,
                             mix.FaultCrackShare, mix.FaultFacetDegrees);

        /// <summary>The shader's _FaultA: block width, tier height, throw, slide.</summary>
        public Vector4 ShaderA => new Vector4(BlockTexels.x, BlockTexels.y, ThrowTexels, SlideTexels);
        /// <summary>The shader's _FaultB: crack run, crack share, facet degrees, 1 when on.</summary>
        public Vector4 ShaderB(bool on) => new Vector4(CrackRunTexels, CrackShare, FacetDegrees, on && On ? 1f : 0f);
    }

    /// <summary>
    /// ⭐ The bedding of a wall laid on the screen grid, FAULTED IN BLOCKS (owner 11 26: rough and jagged, and no
    /// mixels). Level bedding mapped by world position runs the whole wall in ruler-straight courses of one height,
    /// which reads as plank siding. So the wall is cut into blocks on the ground's texel grid: master columns of
    /// uneven width (a pair of cells split at a hashed point, as <see cref="CliffJagMath"/> breaks the brow), each cut
    /// into tiers of uneven height at its own phase, each tier cut once more across. A block's rock is thrown up or
    /// down and slid along its bedding by WHOLE texels, so its courses step against its neighbours' and are a
    /// different stretch of the painted rock, of different heights. Block edges carry a one-texel crack that steps
    /// a texel left or right every few rows. Nothing is rotated, sheared or moved by part of a texel.
    ///
    /// <para>A function of the fragment's WORLD CELL and nothing else, so it does not know chunks, stations or
    /// bands: a block that spans two chunks is one block. The shader's CliffFaultBlock and the fault lines of its
    /// fragment stage are the same arithmetic; this is their twin, for the tests. Deterministic: hashes of the cell,
    /// no global random.</para>
    /// </summary>
    public static class CliffFaultMath
    {
        const float FirstSplit = 0.25f, SplitSpan = 0.5f;     // where a pair of cells is split, as a share of the pair
        const float FirstCut = 0.3f, CutSpan = 0.4f;          // where a tier is cut across, as a share of its column

        /// <summary>The block a world cell is in: x names the column piece, y the tier. Twin: CliffFaultBlock.</summary>
        public static Vector2 BlockOf(in CliffFault f, Vector2 cell)
        {
            float bw = Mathf.Max(f.BlockTexels.x, 2f), th = Mathf.Max(f.BlockTexels.y, 2f);
            float pair = bw * 4f;
            float k = Mathf.Floor(cell.x / pair);
            float split = Mathf.Floor((k + FirstSplit + SplitSpan * CliffWaterlineMath.Hash01(new Vector2(k, 0f), 20f)) * pair);
            bool right = cell.x >= split;
            float m = 2f * k + (right ? 1f : 0f);
            float lo = right ? split : k * pair;
            float hi = right ? (k + 1f) * pair : split;

            float tpair = th * 2f;
            float yk = cell.y + Mathf.Floor(CliffWaterlineMath.Hash01(new Vector2(m, 0f), 21f) * tpair);
            float tk = Mathf.Floor(yk / tpair);
            float tsplit = Mathf.Floor((tk + FirstSplit + SplitSpan * CliffWaterlineMath.Hash01(new Vector2(tk, m), 22f)) * tpair);
            float tier = 2f * tk + (yk >= tsplit ? 1f : 0f);

            float cut = Mathf.Floor(lo + (FirstCut + CutSpan * CliffWaterlineMath.Hash01(new Vector2(m, tier), 23f)) * (hi - lo));
            return new Vector2(m * 2f + (cell.x >= cut ? 1f : 0f), tier);
        }

        /// <summary>The cell a fragment's block is read at: its world cell, stepped a texel left or right every
        /// <see cref="CliffFault.CrackRunTexels"/> rows, which is what stair-steps every block edge.</summary>
        public static Vector2 Stepped(in CliffFault f, Vector2 cell)
        {
            float run = Mathf.Floor(cell.y / Mathf.Max(f.CrackRunTexels, 1));
            float zone = Mathf.Floor(cell.x / (Mathf.Max(f.BlockTexels.x, 2f) * 4f));
            float stair = Mathf.Floor(CliffWaterlineMath.Hash01(new Vector2(zone, run), 25f) * 3f) - 1f;
            return new Vector2(cell.x + stair, cell.y);
        }

        /// <summary>
        /// What the fault does at a world cell: the <paramref name="block"/> it is in, how many whole cells that
        /// block's rock is moved (<paramref name="move"/>: x along the bedding, y up or down), and whether the cell
        /// is a <paramref name="crack"/>. Pure.
        /// </summary>
        public static void At(in CliffFault f, Vector2 cell, out Vector2 block, out Vector2 move, out bool crack)
        {
            Vector2 stepped = Stepped(in f, cell);
            block = BlockOf(in f, stepped);
            Vector2 left = BlockOf(in f, stepped - Vector2.right);
            // A crack is carried run by run of rows, not block by block: it breaks off and picks up again.
            float run = Mathf.Floor(cell.y / Mathf.Max(f.CrackRunTexels, 1));
            crack = left.x != block.x
                 && CliffWaterlineMath.Hash01(new Vector2(block.x + run, block.y - run), 26f) < f.CrackShare;
            move = new Vector2(
                Mathf.Floor(CliffWaterlineMath.Hash01(block, 27f) * (f.SlideTexels + 1f)),
                Mathf.Round((CliffWaterlineMath.Hash01(block, 28f) * 2f - 1f) * f.ThrowTexels));
        }

        /// <summary>
        /// ⭐ The facing a block is SHADED at. Every chunk has its own facing and the px look shades in steps, so two
        /// chunks a few degrees apart meet on a straight edge of tone. Each block instead takes one of the two
        /// nearest multiples of <see cref="CliffFault.FacetDegrees"/>, the nearer one more often (an ordered dither
        /// over the blocks), so the tone changes block by block along the wall and a chunk boundary changes the
        /// answer only in the few blocks whose threshold lies between the two chunks' facings. Pure.
        /// </summary>
        public static float FacetAzimuth(in CliffFault f, float azimuthDegrees, Vector2 block)
        {
            if (f.FacetDegrees <= 0f) return azimuthDegrees;
            float a = azimuthDegrees / f.FacetDegrees;
            float whole = Mathf.Floor(a);
            return f.FacetDegrees * (whole + (CliffWaterlineMath.Hash01(block, 29f) < a - whole ? 1f : 0f));
        }
    }
}
