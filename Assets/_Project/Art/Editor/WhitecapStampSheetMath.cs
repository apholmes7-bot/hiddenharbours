using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Composes the open-water whitecap STAMP SHEET (<c>Whitecaps.png</c>) out of the owner's single
    /// hand-painted mark. Pure and deterministic — no RNG state, no time, no assets: the same seed
    /// always lays the same sheet, so <c>WhitecapStampSheetTests</c> can re-bake in memory and prove
    /// the committed PNG really is this function's output (they cannot drift).
    ///
    /// <para><b>The defect this exists to kill (owner, 2026-08-05: "some of the whitecaps have a
    /// square pattern that I recognize from earlier builds").</b> The painted whitecap slot has been
    /// ON since #87 at <c>_WhitecapTexStrength</c> 0.865, so the painted mark — not the procedural
    /// evolving field — is what actually PLACES open-water caps. And the art in that slot was ONE
    /// mark, alone, in a 64 px tile sampled at <c>_PaintScale</c> 0.25 (one tile per 4 m). One mark
    /// per repeat cell IS a lattice: it laid an identical whitecap every 4 metres, in rows, forever.
    ///
    /// <para><b>Why the untiler could not save it.</b> <c>UntileSampleW</c> hides a repeat by
    /// TRANSLATING whole repeat cells by a per-cell hash. Translation re-positions a lattice; it
    /// cannot make one non-periodic. That trick works on <c>Foam.png</c> (58% coverage, many varied
    /// marks — you stop recognising WHICH cell you are in) and is powerless against a tile whose
    /// entire content is a single dot. The fix therefore has to be the ART, not more untiling.</para>
    ///
    /// <para><b>What the sheet does instead.</b> <see cref="MarkCount"/> copies of the owner's exact
    /// mark — his pixels, never resampled, only mirrored — scattered by toroidal dart-throwing at a
    /// minimum spacing, on a sheet <see cref="SheetSize"/>/<see cref="MarkCellSize"/> = 4x larger in
    /// each axis. Sampled at <c>_WhitecapTexScale</c> the sheet holds the SAME texel density
    /// (<see cref="TexelsPerMetre"/>), so every mark keeps the world size, coverage and pixel grid it
    /// had — 16 marks over 16 m is the same one-per-4-m DENSITY, at irregular positions instead of on
    /// a grid. The repeat period goes 4 m -> 16 m, and inside it nothing is periodic at all; with a
    /// busy sheet the existing untiler finally has something it can hide.</para>
    ///
    /// <para>Marks are placed with <b>MAX-alpha</b> compositing and a minimum spacing wider than the
    /// mark, so a stamp is never half-drawn over another and the coverage is exactly
    /// <see cref="MarkCount"/> x the mark's own area — the tuned foam density is preserved by
    /// construction rather than by eye.</para>
    /// </summary>
    public static class WhitecapStampSheetMath
    {
        /// <summary>The baked sheet's edge, px. 4x the owner's cell in each axis = 16x the marks.</summary>
        public const int SheetSize = 256;

        /// <summary>The owner's source cell edge, px (<c>WhitecapSource~/WhitecapMark.png</c>).</summary>
        public const int MarkCellSize = 64;

        /// <summary>How many stamps the sheet carries. Chosen as (SheetSize/MarkCellSize)^2 so the
        /// marks-per-square-metre — and therefore the foam coverage — is unchanged from the single-mark
        /// tile it replaces. Anything above 1 breaks the lattice; 16 keeps the density honest.</summary>
        public const int MarkCount = 16;

        /// <summary>Minimum toroidal centre-to-centre spacing, px. Wider than the mark (19x37), so
        /// stamps never overlap and coverage stays exactly MarkCount x the mark area; small enough
        /// that dart-throwing converges long before <see cref="MaxAttempts"/>.</summary>
        public const int MinSpacingPx = 40;

        /// <summary>The one seed. Re-rolling it re-scatters the sheet; the tests pin the PROPERTIES
        /// (count, spacing, irregularity), never the exact positions, so a re-roll is allowed.</summary>
        public const uint Seed = 0x5EA0F0A1u;

        /// <summary>Dart-throwing attempt ceiling — a runaway guard, not a tuning knob.</summary>
        public const int MaxAttempts = 4096;

        /// <summary>The shared painted-slot grid the single-mark tile was sampled at, tiles/unit
        /// (<c>_PaintScale</c> on every water material).</summary>
        public const float LegacyPaintScale = 0.25f;

        /// <summary>The whitecap slot's OWN grid, tiles/unit (<c>_WhitecapTexScale</c>). Derived, not
        /// picked: it is the value at which the bigger sheet lands the owner's mark at the size it
        /// already had — see <see cref="TexelsPerMetre"/>.</summary>
        public const float SheetScale = 0.0625f;

        /// <summary>A painted slot's resolution ON THE WATER: how many of its texels a world metre
        /// spans. The whole point of <see cref="SheetScale"/> is that
        /// <c>TexelsPerMetre(SheetSize, SheetScale)</c> equals
        /// <c>TexelsPerMetre(MarkCellSize, LegacyPaintScale)</c> — a bigger sheet at a proportionally
        /// coarser scale draws the identical mark, not a shrunken one.</summary>
        public static float TexelsPerMetre(int pixels, float tilesPerUnit) => pixels * tilesPerUnit;

        /// <summary>One stamp: a centre in TEXTURE space (y up, Unity's Texture2D convention) plus the
        /// mirror applied to the mark. Mirrors only — the owner's pixels are never resampled.</summary>
        public readonly struct Placement
        {
            public readonly int X, Y;
            public readonly bool FlipX, FlipY;

            public Placement(int x, int y, bool flipX, bool flipY)
            {
                X = x; Y = y; FlipX = flipX; FlipY = flipY;
            }
        }

        /// <summary>xorshift32. Chosen because it is one line of integer ops with no library or
        /// float behind it, so the bake is reproducible anywhere — including the Python that first
        /// laid the committed sheet.</summary>
        public static uint NextRandom(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>Squared distance on the TORUS — the sheet wraps (Repeat), so two stamps near
        /// opposite edges are neighbours and must respect the spacing, or the seam grows a clump.</summary>
        public static int ToroidalDistanceSq(int ax, int ay, int bx, int by, int size)
        {
            int dx = Mathf.Abs(ax - bx);
            if (dx > size / 2) dx = size - dx;
            int dy = Mathf.Abs(ay - by);
            if (dy > size / 2) dy = size - dy;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// The scatter: dart-throwing with a toroidal minimum spacing. Every attempt draws all four
        /// values whether or not it is accepted, so the stream — and the sheet — depend on nothing but
        /// the seed.
        /// </summary>
        public static IReadOnlyList<Placement> Placements()
        {
            var placed = new List<Placement>(MarkCount);
            uint s = Seed;
            for (int attempt = 0; attempt < MaxAttempts && placed.Count < MarkCount; attempt++)
            {
                s = NextRandom(s); int x = (int)(s % SheetSize);
                s = NextRandom(s); int y = (int)(s % SheetSize);
                s = NextRandom(s); bool flipX = (s & 1u) != 0u;
                s = NextRandom(s); bool flipY = (s & 1u) != 0u;

                bool clear = true;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (ToroidalDistanceSq(x, y, placed[i].X, placed[i].Y, SheetSize)
                        < MinSpacingPx * MinSpacingPx)
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear) placed.Add(new Placement(x, y, flipX, flipY));
            }
            return placed;
        }

        /// <summary>
        /// Where the largest circular run of EMPTY lines ends — i.e. the line to rotate to index 0 so
        /// the mark stops straddling the wrap. The owner's mark does straddle it (his tile is seamless,
        /// and the mark sits across the y edge), so cropping without this would cut it in half.
        /// </summary>
        public static int UnwrapOffset(bool[] used)
        {
            int n = used.Length;
            int bestStart = 0, bestRun = -1, run = 0, runStart = 0;
            // walk twice so a run that wraps the end is seen whole
            for (int k = 0; k < n * 2; k++)
            {
                if (!used[k % n])
                {
                    if (run == 0) runStart = k % n;
                    run++;
                    if (run > bestRun && run <= n) { bestRun = run; bestStart = runStart; }
                }
                else run = 0;
            }
            return bestRun <= 0 ? 0 : bestStart;
        }

        /// <summary>
        /// Compose the sheet from the owner's mark cell. <paramref name="markCell"/> is
        /// <see cref="MarkCellSize"/> square, row-major, TEXTURE space (row 0 = bottom) — exactly what
        /// <c>Texture2D.GetPixels32()</c> returns. The result is <see cref="SheetSize"/> square in the
        /// same space, ready for <c>SetPixels32</c>.
        /// </summary>
        public static Color32[] Compose(Color32[] markCell)
        {
            int n = MarkCellSize;
            if (markCell == null || markCell.Length != n * n)
                throw new System.ArgumentException(
                    $"the whitecap source must be {n}x{n} ({n * n} px); got " +
                    $"{(markCell == null ? 0 : markCell.Length)}");

            // 1. un-wrap: rotate the cell so the mark sits whole instead of across the seam.
            var rowUsed = new bool[n];
            var colUsed = new bool[n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (markCell[y * n + x].a > 0) { rowUsed[y] = true; colUsed[x] = true; }

            int rollY = UnwrapOffset(rowUsed);
            int rollX = UnwrapOffset(colUsed);

            // 2. crop to the mark's own bounds in the rotated cell.
            int minX = n, minY = n, maxX = -1, maxY = -1;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (markCell[((y + rollY) % n) * n + ((x + rollX) % n)].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
                throw new System.ArgumentException("the whitecap source is fully transparent");

            int mw = maxX - minX + 1, mh = maxY - minY + 1;
            var mark = new Color32[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                    mark[y * mw + x] = markCell[((y + minY + rollY) % n) * n + ((x + minX + rollX) % n)];

            // 3. stamp. MAX-alpha compositing, wrapped — the sheet is sampled on Repeat.
            var sheet = new Color32[SheetSize * SheetSize];
            foreach (var p in Placements())
            {
                int x0 = p.X - mw / 2, y0 = p.Y - mh / 2;
                for (int j = 0; j < mh; j++)
                {
                    int sy = Wrap(y0 + j, SheetSize);
                    int srcY = p.FlipY ? mh - 1 - j : j;
                    for (int i = 0; i < mw; i++)
                    {
                        int srcX = p.FlipX ? mw - 1 - i : i;
                        Color32 c = mark[srcY * mw + srcX];
                        int sx = Wrap(x0 + i, SheetSize);
                        if (c.a >= sheet[sy * SheetSize + sx].a) sheet[sy * SheetSize + sx] = c;
                    }
                }
            }
            return sheet;
        }

        /// <summary>Floored modulo — C#'s <c>%</c> keeps the sign, which would index off the sheet.</summary>
        public static int Wrap(int v, int n) => ((v % n) + n) % n;
    }
}
