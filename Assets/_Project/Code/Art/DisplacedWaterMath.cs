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

        /// <summary>
        /// The per-hull z translation that places a mesh hull's PIVOT into the calibrated
        /// iso-depth convention of the shared private z-buffer (ADR 0023 phase 3, step 1) —
        /// the C# reference of the water's own vertex-stage depth
        /// (<c>ws.z += (ground.y − _HeightWorldMin.y)·cosElev − lift·sinElev</c> in
        /// HHWaterDisplaced, applied to the hull's ground anchor and heave):
        ///
        /// <code>z = BaseZ + (hullWorldY − ReferenceY) · CosElev − heaveMeters · SinElev</code>
        ///
        /// Applied as ONE constant translation of the whole hull frame (never per vertex), so the
        /// rig's intra-hull depth convention (<c>ry·cos − rz·sin</c>, the golden-master truth) is
        /// bit-preserved; only the hull-vs-water comparison changes. At the contact line the
        /// ground terms of hull and adjacent water cancel and the z-test reduces to
        /// <c>heightAboveStillWater vs surfaceLift</c> — water truthfully covers exactly the
        /// planking below the lifted surface, and a rising surface climbs the planking
        /// ≈(cos+sin)/(cos²+sin) ≈ 1.15 rig-metres per metre of lift at the fleet's 40°.
        /// </summary>
        public static float HullDepthBias(float hullWorldY, float heaveMeters,
                                          in WaterIsoDepthFrame frame)
            => frame.BaseZ
               + (hullWorldY - frame.ReferenceY) * frame.CosElev
               - heaveMeters * frame.SinElev;

        // ==== The WATERTIGHT clamp (owner playtest 2026-07-23: "water enters hull on the mesh
        // models") ====================================================================================
        //
        // The calibrated z-test covers ANY hull point whose height above still water is below the
        // lift of the surface point sharing its pixel — including the LOW interior surfaces a real
        // boat keeps dry (cockpit sole, hold floor, inner bulwarks): in a storm the differential
        // between the hull's single-point ride and the local surface (wave slope across the hull,
        // plus the baked-iso beam residual) exceeds the interior's freeboard and the boat reads as
        // flooding. The fix stays inside the #263 discipline (per-hull CONSTANT transforms, never a
        // per-vertex touch of the rig's own convention): the heave term of the hull's z bias — and
        // ONLY the z bias; the visual ride stays the honest shared heave — is clamped so the
        // highest surface the hull can currently meet sits at most WatertightDeckHeightMeters
        // above its keel. Water still climbs the exterior planking with every wave; it can never
        // climb past the line where it would board the boat.

        /// <summary>Ring samples per radius when bounding the surface over a hull's footprint —
        /// 8 (the 45° compass), on two radii plus the centre: 17 field evaluations, enough to
        /// catch a crest anywhere on a hull-sized footprint against wave trains that are
        /// metres-scale or longer (the storm acceptance adjudicates the residue in pixels).</summary>
        public const int FootprintRingSamples = 8;

        /// <summary>The hull footprint radius the clamp scans: half the rig cell's width in world
        /// metres. The cell is authored to contain the whole hull at every heading with margin, so
        /// this bounds the planking's true reach (slightly conservative — a farther crest can only
        /// raise the bound, i.e. dry the hull, never flood it).</summary>
        public static float FootprintRadiusMeters(int cellW, int pxPerMetre)
            => 0.5f * cellW / Mathf.Max(1, pxPerMetre);

        static readonly Vector2[] s_RingDirs =
        {
            new Vector2(1f, 0f), new Vector2(0.70710678f, 0.70710678f),
            new Vector2(0f, 1f), new Vector2(-0.70710678f, 0.70710678f),
            new Vector2(-1f, 0f), new Vector2(-0.70710678f, -0.70710678f),
            new Vector2(0f, -1f), new Vector2(0.70710678f, -0.70710678f),
        };

        /// <summary>
        /// The highest DISPLACED surface lift (metres) a hull at <paramref name="centerWorld"/>
        /// can currently meet: max of the shader-twin field height over the centre and two
        /// concentric 8-point rings (<paramref name="radiusMeters"/> and half of it), times the
        /// active surface's effective exaggeration (<see cref="WaterIsoDepthFrame.Exaggeration"/>).
        /// Evaluates <see cref="WaveFieldBridge.ShaderTwinSample"/> over the PUBLISHED globals —
        /// the exact field the water shader lifts its vertices with, so the clamp and the drawn
        /// sea cannot disagree (the ONE-SEA rule). Shore fade is deliberately taken as 1: an
        /// offshore upper bound — near the coast the true lift is smaller, so the clamp only ever
        /// over-dries, never floods. Allocation-free (rule 7).
        /// </summary>
        public static float MaxSurfaceLiftMeters(Vector2 centerWorld, float radiusMeters,
                                                 in Vector4 train0, in Vector4 train1,
                                                 in Vector4 train2, in Vector4 train3,
                                                 in Vector4 phases, in Vector4 fieldParams,
                                                 float exaggeration)
        {
            float max = WaveFieldBridge.ShaderTwinSample(
                centerWorld, train0, train1, train2, train3, phases, fieldParams).Height;
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? 0.5f * radiusMeters : radiusMeters;
                for (int i = 0; i < FootprintRingSamples; i++)
                {
                    Vector2 p = centerWorld + s_RingDirs[i] * r;
                    float h = WaveFieldBridge.ShaderTwinSample(
                        p, train0, train1, train2, train3, phases, fieldParams).Height;
                    if (h > max) max = h;
                }
            }
            return max * Mathf.Max(0f, exaggeration);
        }

        /// <summary>
        /// The clamped heave (metres) the hull's Z BIAS rides (never the visual — the screen lift
        /// stays the honest shared heave): at least the true heave, raised so that
        /// <c>maxLift − zHeave ≤ deckHeight</c> — the surface can wet the planking up to the
        /// deck/sole line and no further. <paramref name="deckHeightMeters"/> ≤ 0 disables the
        /// clamp entirely (the pre-fix render, byte-identical — the safety of an unset def).
        /// </summary>
        public static float WatertightZHeaveMeters(float heaveMeters, float maxLiftMeters,
                                                   float deckHeightMeters)
            => deckHeightMeters > 0f
               ? Mathf.Max(heaveMeters, maxLiftMeters - deckHeightMeters)
               : heaveMeters;
    }
}
