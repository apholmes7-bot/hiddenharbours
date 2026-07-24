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

        /// <summary>The hull footprint half-width the clamp scans: half the rig cell's width in
        /// world metres. The cell is authored to contain the whole hull at every heading with
        /// margin, so this bounds the planking's true x-reach (slightly conservative — a farther
        /// crest can only raise the bound, i.e. dry the hull, never flood it).</summary>
        public static float FootprintRadiusMeters(int cellW, int pxPerMetre)
            => 0.5f * cellW / Mathf.Max(1, pxPerMetre);

        /// <summary>
        /// The scan's y half-height (metres) — deliberately MUCH tighter than the x half-width.
        /// The pixel-share water that can cover a hull point sits at
        /// <c>Δy = r·cos(elev) − lift</c> from that point's ground line: at the flooding
        /// threshold that is ≈ −0.38·r (a metre or so in front), and even the deepest useful
        /// cover in the fleet's gale (lift ≈ 5.5 m over a low interior point) reaches only ≈ 5 m
        /// in front — while the hull's ground lines themselves span ±(half-beam·sin(elev)) ≈
        /// ±1.2 m. Crests farther abeam than this CANNOT paint the hull, and scanning them
        /// (the first cut scanned a full 14 m disc on the dragger) inflates the bound and dries
        /// the crest-at-root waterline band a big hull should keep. 6 m covers the fleet's
        /// worst case with margin; the storm acceptance adjudicates the residue in pixels.
        /// </summary>
        public const float FootprintScanHalfHeightMeters = 6f;

        /// <summary>Scan step (metres) along x. 2 m against the fleet's shortest meaningful
        /// trains (λ ≥ ~10 m) bounds the worst between-station crest miss at ≈ 2% of amplitude
        /// — inside the committed deck heights' residual shave.</summary>
        public const float FootprintScanStepMeters = 2f;

        /// <summary>Scan step (metres) along y — much DENSER than x: the fought hull height
        /// moves at 1/cos(elev) ≈ 1.3 rig-m per metre of y offset AND the demand field peaks
        /// sharply where a fight spans the exact half-beam, so the y gap is what bounds the
        /// clamp's blind spot between rows (0.5 m measured the between-rows residue down to
        /// noise; 1 m left a ~600 px far-washboard streak at an off-root storm crest).</summary>
        public const float FootprintScanRowStepMeters = 0.5f;

        /// <summary>Safety (z-heave metres) added to a BINDING demand — the budget for what a
        /// discrete scan of a continuous demand field cannot see (between-station crests, float
        /// edges at the exact deck plane). RAMPED with engagement — the applied safety is
        /// <c>min(this, SafetyRampSlope·(demand − heave))</c> — so it is EXACTLY ZERO at the
        /// no-clamp boundary (daily seas, whose demands sit at or below the honest heave, stay
        /// bit-untouched), reaches full size by 0.1 m of engagement (a slope-1 ramp measured a
        /// 16 px leak at a barely-binding trough instant), and costs ≈ 0.21 rig-m ≈ 5 px of
        /// waterline band only where protection genuinely binds. Sized from the measured
        /// residue class (16–53 px single-instant leaks at 0 safety, 2026-07-23).</summary>
        public const float WatertightDemandSafetyMeters = 0.4f;

        /// <summary>The safety ramp's slope (see <see cref="WatertightDemandSafetyMeters"/>):
        /// full safety by engagement = safety/slope = 0.1 m.</summary>
        public const float WatertightSafetyRampSlope = 4f;

        /// <summary>
        /// The clamped heave (metres) the hull's Z BIAS rides (never the visual — the screen
        /// lift stays the honest shared heave): at least the true heave, raised exactly enough
        /// that NO interior face — any hull height ≥ <paramref name="deckHeightMeters"/> above
        /// the keel — can lose the shared z-test to the CURRENT displaced surface.
        ///
        /// <para><b>The per-point law (measured into shape in pixels, 2026-07-23).</b> Solve the
        /// shared z-buffer's pixel-share fight between a hull face at rig height r on ground
        /// line ry (screen y rises at cos(elev) per metre of height and sin(elev) per metre of
        /// ground; depth falls at sin(elev) / rises at cos(elev)) and the displaced water
        /// (screen y rises at 1 per metre of lift — the vertex stage's <c>ws.y += lift</c>):
        /// a water sample at ground offset Δ from the hull's ROOT line with lift L fights, on
        /// EACH ground line ry, exactly the height
        /// <c>r(ry) = r_f − tan(elev)·ry</c> where <c>r_f = (Δ + L)/cos</c>, and wins iff
        /// <c>r(ry)·(cos²+sin) &lt; L·(cos+sin) − zHeave·sin + ry·cos·(1−sin)</c> (the last
        /// term is §24's beam residual, now EXACT instead of a data shave). Keeping every
        /// interior face (r ≥ deckHeight, |ry| ≤ halfBeam) dry therefore demands, per sample,
        ///
        /// <code>
        /// ry* = min(halfBeam, (r_f − deckHeight)/tan(elev))   // the worst far-side line fought at/above the deck
        /// zHeave ≥ (L·(cos+sin) − (r_f − tan·ry*)·(cos²+sin) + ry*·cos·(1−sin)) / sin
        /// </code>
        ///
        /// gated on <c>r_f ≥ deckHeight</c> — samples fighting only the open planking BELOW the
        /// deck line demand NOTHING, so the exterior waterline keeps every centimetre of
        /// truthful climb the interior allows. (The measured lineage: a 1:1 differential clamp
        /// flooded the cockpit; a blanket footprint-max bound dry-docked the dragger; a
        /// root-line-only per-point law re-flooded the far rail — each adjudicated by the
        /// acceptance suite before this complete law replaced it.)</para>
        ///
        /// <para>The scan is an anisotropic grid: x spanning ±<paramref name="halfWidthMeters"/>
        /// (the hull's real reach — <see cref="FootprintRadiusMeters"/>), y spanning
        /// ±<see cref="FootprintScanHalfHeightMeters"/> (all the water that can share a pixel
        /// with the hull — see that constant), stepped <see cref="FootprintScanStepMeters"/> in
        /// x and <see cref="FootprintScanRowStepMeters"/> in y (denser: the fought height moves
        /// at 1/cos per metre of Δ, so y sampling is what bounds the residue). Heights come from
        /// <see cref="WaveFieldBridge.ShaderTwinSample"/> over the PUBLISHED globals — the exact
        /// field the water shader lifts its vertices with (the ONE-SEA rule closed at the
        /// globals) — times the frame's effective exaggeration; shore fade is deliberately taken
        /// as 1 (an offshore bound: near the coast the true lift is smaller, so the clamp only
        /// ever over-dries, never floods).</para>
        ///
        /// <para><paramref name="deckHeightMeters"/> ≤ 0 disables the clamp entirely (the
        /// pre-fix render, byte-identical — the safety of an unset def). A silent field (no
        /// bridge — every height 0) demands nothing. Allocation-free (rule 7): ≤ ~15×13 ≈ 200
        /// four-train evaluations per hull per pose push — microseconds on the desktop
        /// baseline.</para>
        /// </summary>
        public static float WatertightZHeaveMeters(float heaveMeters, float deckHeightMeters,
                                                   float halfBeamMeters,
                                                   Vector2 centerWorld, float halfWidthMeters,
                                                   in Vector4 train0, in Vector4 train1,
                                                   in Vector4 train2, in Vector4 train3,
                                                   in Vector4 phases, in Vector4 fieldParams,
                                                   in WaterIsoDepthFrame frame)
        {
            if (deckHeightMeters <= 0f) return heaveMeters;
            float c = frame.CosElev;
            float s = Mathf.Max(frame.SinElev, 1e-4f);
            float cInv = 1f / Mathf.Max(c, 1e-4f);
            float tanE = s * cInv;
            float exaggeration = Mathf.Max(0f, frame.Exaggeration);
            halfBeamMeters = Mathf.Max(0f, halfBeamMeters);

            halfWidthMeters = Mathf.Max(0f, halfWidthMeters);
            int nx = Mathf.Max(1, Mathf.CeilToInt(halfWidthMeters / FootprintScanStepMeters));
            int ny = Mathf.Max(1, Mathf.CeilToInt(
                FootprintScanHalfHeightMeters / FootprintScanRowStepMeters));

            float demand = float.MinValue;
            for (int ix = -nx; ix <= nx; ix++)
            {
                float x = centerWorld.x + halfWidthMeters * ix / (float)nx;
                for (int iy = -ny; iy <= ny; iy++)
                {
                    float dy = FootprintScanHalfHeightMeters * iy / (float)ny;
                    float lift = exaggeration * WaveFieldBridge.ShaderTwinSample(
                        new Vector2(x, centerWorld.y + dy), train0, train1, train2, train3,
                        phases, fieldParams).Height;
                    float foughtR = (dy + lift) * cInv;
                    if (foughtR < deckHeightMeters) continue;   // fights the open planking: allowed
                    float ryStar = Mathf.Min(halfBeamMeters,
                                             (foughtR - deckHeightMeters) / tanE);
                    float protectedR = foughtR - tanE * ryStar;
                    float need = (lift * (c + s) - protectedR * (c * c + s)
                                  + ryStar * c * (1f - s)) / s;
                    if (need > demand) demand = need;
                }
            }

            // The engagement-ramped safety (see WatertightDemandSafetyMeters): zero at the
            // no-clamp boundary (daily seas bit-untouched), full where protection binds.
            if (demand <= heaveMeters) return heaveMeters;
            return demand + Mathf.Min(WatertightDemandSafetyMeters,
                                      WatertightSafetyRampSlope * (demand - heaveMeters));
        }
    }
}
