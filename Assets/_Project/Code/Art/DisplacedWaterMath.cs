using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure math behind the DISPLACED water surface (ADR 0023 phase 2): grid/chunk sizing for the
    /// vertex mesh, and the parameter plumbing that keeps the production surface in LOCKSTEP with
    /// the Core seam (<see cref="ShoreFadeMath"/>). Engine-light, stateless, allocation-free —
    /// the headless twin the EditMode tests pin.
    ///
    /// <para><b>Grid sizing (rule 7).</b> The ADR's perf envelope: the spike measured a 4 px grid
    /// at 43 k verts ≈ 0.6–3.9 ms on the desktop baseline; production starts at
    /// <see cref="DefaultGridPixels"/> = 8 px and lets crest-silhouette tolerance argue it down.
    /// The mesh is built in CHUNKS so ordinary frustum culling drops off-screen water (the
    /// mobile-portability discipline) — chunk size is capped so every chunk stays comfortably
    /// under the 16-bit index limit.</para>
    ///
    /// <para><b>Band plumbing (rule 6).</b> The shore-fade band is DERIVED, never a free number:
    /// <see cref="BandMeters"/> is <c>coefficient × envelope × exaggeration × gradient</c> — the
    /// exact formula of <see cref="ShoreFadeMath.RecommendedBandMeters"/> with the coefficient
    /// lifted to a parameter (the ADR asks both exaggeration and coefficient to be plumbed
    /// end-to-end; GameConfig exposure is arc step 3). <c>DisplacedWaterMathTests</c> pins this
    /// equal to the Core derivation at the canonical coefficient, so the plumbing cannot drift
    /// from the tear-safety proof.</para>
    /// </summary>
    public static class DisplacedWaterMath
    {
        /// <summary>Production start density (ADR 0023 § Performance envelope): one vertex every
        /// 8 screen pixels. The spike proved 4 px is affordable; 8 px is the comfortable start.</summary>
        public const int DefaultGridPixels = 8;

        /// <summary>Max grid CELLS per chunk axis: 64 cells = 65×65 = 4,225 verts per chunk —
        /// far under the 16-bit mesh index limit, and small enough that frustum culling pays.</summary>
        public const int MaxChunkCells = 64;

        /// <summary>World metres per grid cell: <paramref name="gridPixels"/> at the project's
        /// pixels-per-unit (8 px at PPU 32 = 0.25 m).</summary>
        public static float CellMeters(int gridPixels, float pixelsPerUnit)
            => Mathf.Max(1, gridPixels) / Mathf.Max(1f, pixelsPerUnit);

        /// <summary>How many whole cells cover <paramref name="sizeMeters"/> (ceil — the mesh may
        /// overhang the rect by a fraction of a cell rather than undershoot the coast).</summary>
        public static int CellCount(float sizeMeters, float cellMeters)
            => Mathf.Max(1, Mathf.CeilToInt(sizeMeters / Mathf.Max(cellMeters, 1e-4f) - 1e-4f));

        /// <summary>How many chunks cover <paramref name="cells"/> at <paramref name="maxChunkCells"/> per chunk.</summary>
        public static int ChunkCount(int cells, int maxChunkCells)
            => (Mathf.Max(1, cells) + Mathf.Max(1, maxChunkCells) - 1) / Mathf.Max(1, maxChunkCells);

        /// <summary>Cell count of chunk <paramref name="chunkIndex"/>: full chunks first, the
        /// remainder in the last — so the chunks tile <paramref name="cells"/> EXACTLY (no crack,
        /// no overlap; the tests pin the sum).</summary>
        public static int ChunkCells(int cells, int maxChunkCells, int chunkIndex)
        {
            cells = Mathf.Max(1, cells);
            maxChunkCells = Mathf.Max(1, maxChunkCells);
            int count = ChunkCount(cells, maxChunkCells);
            if (chunkIndex < 0 || chunkIndex >= count) return 0;
            if (chunkIndex < count - 1) return maxChunkCells;
            int rem = cells - (count - 1) * maxChunkCells;
            return rem;
        }

        /// <summary>Vertices in a grid chunk of <paramref name="cellsX"/> × <paramref name="cellsY"/> cells.</summary>
        public static int ChunkVertexCount(int cellsX, int cellsY) => (cellsX + 1) * (cellsY + 1);

        /// <summary>Triangle INDICES in a grid chunk (2 tris per cell).</summary>
        public static int ChunkIndexCount(int cellsX, int cellsY) => cellsX * cellsY * 6;

        /// <summary>
        /// The tear-safe shore-fade band with the safety coefficient as a PARAMETER —
        /// <c>coefficient × envelope × exaggeration × gradient</c>, the exact
        /// <see cref="ShoreFadeMath.RecommendedBandMeters"/> derivation (which fixes the
        /// coefficient at <see cref="ShoreFadeMath.RecommendedBandCoefficient"/>). Kept in
        /// lockstep by <c>DisplacedWaterMathTests</c>: at the canonical coefficient the two are
        /// bit-equal, so this plumbing can never drift from the proven Core rule.
        /// </summary>
        public static float BandMeters(float envelopeMeters, float exaggeration,
                                       float maxShoreGradient, float coefficient)
            => Mathf.Max(0f, coefficient)
               * Mathf.Max(0f, envelopeMeters)
               * Mathf.Max(0f, exaggeration)
               * Mathf.Max(0f, maxShoreGradient);

        /// <summary>
        /// The vertex lift the production shader computes per vertex — delegated STRAIGHT to
        /// <see cref="ShoreFadeMath.DisplacedHeight"/> (the ONE shared rule every displaced-water
        /// consumer reads; ADR 0023 §(2)). This is the C# reference of the HLSL vertex stage
        /// (<c>vertDisplaced</c> in HiddenHarboursWater.shader): height × exaggeration ×
        /// ShoreFade01(depth, band). The tests drive the reference sea's 100%-envelope event
        /// through it to prove the production parameter path preserves the seam contract.
        /// </summary>
        public static float VertexLift(float waveHeightMeters, float stillDepthMeters,
                                       float bandMeters, float exaggeration)
            => ShoreFadeMath.DisplacedHeight(waveHeightMeters, stillDepthMeters, bandMeters, exaggeration);
    }
}
