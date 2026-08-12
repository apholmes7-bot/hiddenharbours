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
        /// Applied as ONE constant translation of the whole hull frame, so the hull's ROOT LINE
        /// lands exactly in the sea. ⚠️ A constant cancels a GRADIENT at one line only, which is
        /// the whole of ADR 0033: see <see cref="HullDepthShear"/> for the y→z shear that carries
        /// the rest of the hull, and <see cref="HullShearCompensation"/> for the term that keeps
        /// this calibration exact once the shear is live.
        /// </summary>
        public static float HullDepthBias(float hullWorldY, float heaveMeters,
                                          in WaterIsoDepthFrame frame)
            => frame.BaseZ
               + (hullWorldY - frame.ReferenceY) * frame.CosElev
               - heaveMeters * frame.SinElev;

        // ==== ADR 0033: ONE DEPTH UNIT — the hull frame's y→z shear ==================================
        //
        // The defect this cures, measured in #491: hull and water were reading "ground y" in two
        // different units. The hull's vertices carry the RIG projection
        // (IsoFacetMath.RigToWorld: screen y = ry·sin + rz·cos, depth = ry·cos − rz·sin), so one rig
        // ground metre aft is sin(elev) of screen travel but cos(elev) of depth. The water's depth
        // (HullDepthBias above, the C# reference of the shader's vertex stage) advances at cos(elev)
        // per WORLD y — and a flat water quad's world y IS its screen y. So along its own fore-aft
        // axis the hull's depth ramp ran 1/sin(elev) = 1.556× too steep, and HullDepthBias's single
        // constant could only cancel that at the root line. Everywhere else the two drifted by
        // Δy_rigGround·cos·(1−sin): −1.64 m of false depth at a 12 m lobster boat's stern sailing
        // north (no wave could ever reach that planking), sign-flipped sailing south (the sea drew
        // over a dry stern), exactly zero east/west. Two owner reports ten months apart, one
        // mechanism.

        /// <summary>
        /// <b>The y→z shear that puts the hull frame in the water's depth unit</b> (ADR 0033).
        /// <c>g = cos(elev)·(1 − sin(elev)) / sin(elev)</c> — 0.42571 at the fleet's 40° bake.
        ///
        /// <para>Applied by the facet shader's vertex stage as
        /// <c>z −= (worldY − referenceY)·g</c> (see <see cref="ShearedDepth"/>, of which the HLSL is
        /// a one-line transcription). It is EXACT, not a correction factor: it zeroes the ground
        /// term at every facing and lands the height term on the true iso relation
        /// <c>−h/sin(elev)</c>, because <c>g + cos = cos/sin = cot(elev)</c> and
        /// <c>sin + cos²/sin = 1/sin</c>. After it, the shared z-test asks only the question the
        /// composite exists to ask — <i>is this bit of hull above or below the water?</i> — with no
        /// heading term left in it.</para>
        ///
        /// <para><b>Why a shear is free where "ONE constant per hull" was thought necessary.</b>
        /// Under the ortho camera two fragments sharing a pixel share a world y, so they take the
        /// IDENTICAL shift — and because the reference is the water's own
        /// <see cref="WaterIsoDepthFrame.ReferenceY"/> rather than anything per-hull, that holds
        /// across hulls and fittings too, not merely within one hull. Hull self-occlusion, the
        /// deck-occupant band encoding (#481), fitting-vs-hull occlusion and the golden masters'
        /// intra-hull ordering are therefore invariant by construction.</para>
        ///
        /// <para><b>Elevation comes from the hull's own bake</b> (<c>HullMeshDef.ElevationDeg</c> via
        /// the setup), never a hard-coded 40 — a hull re-baked at another elevation is right for
        /// free (rule 6). ⚠️ It presumes hull and water share the one iso convention, which they do:
        /// the frame's cos/sin are the water's <c>_WaterIsoDepth</c>, the same 40°. Were they ever
        /// to diverge the exact form is <c>cot(elevHull) − cos(elevWater)</c>, which this reduces to
        /// when they agree. Degenerate elevations return 0 (no shear) rather than a division by a
        /// vanishing sine.</para>
        /// </summary>
        public static float HullDepthShear(float bakeElevationDegrees)
        {
            if (bakeElevationDegrees <= 0f || bakeElevationDegrees > 90f) return 0f;
            float e = bakeElevationDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(e);
            if (s <= 1e-4f) return 0f;
            return Mathf.Cos(e) * (1f - s) / s;
        }

        /// <summary>
        /// The sheared depth of one hull vertex — <c>z − (worldY − referenceY)·shear</c>, the C#
        /// reference of the facet shader's vertex-stage line (<c>HiddenHarboursIsoFacet.shader</c>,
        /// <c>vert()</c>). A WORLD-space function of world y alone, which is exactly what makes the
        /// shift identical for any two fragments sharing a pixel (see <see cref="HullDepthShear"/>).
        /// <paramref name="shear"/> 0 returns <paramref name="depthZ"/> unchanged — the no-displaced-sea
        /// A/B contract, byte-identical.
        /// </summary>
        public static float ShearedDepth(float worldY, float depthZ, float referenceY, float shear)
            => depthZ - (worldY - referenceY) * shear;

        /// <summary>
        /// <b>What the shear will subtract at this hull's own drawn root</b>, added back into her
        /// per-hull constant so <see cref="HullDepthBias"/>'s calibration survives the shear
        /// untouched: <c>(rootWorldY + heave − referenceY)·shear</c>.
        ///
        /// <para>The reference is the hull's <b>drawn</b> (heaved) root, not her unheaved one, and
        /// that is load-bearing rather than tidy. The heave/lift channel is already exact — the
        /// hull's <c>−heave·sin</c> and the water's <c>−lift·sin</c> cancel term for term when she
        /// floats on the sea she is riding — so the shear must not touch it. Referencing the
        /// unheaved root instead leaves a residual of <c>−heave·g</c>: 0.6 m of false depth on a
        /// 1.4 m crest, which would rebuild the very defect ADR 0033 exists to close, this time
        /// modulated by the wave rather than by the heading.</para>
        /// </summary>
        public static float HullShearCompensation(float hullWorldY, float heaveMeters, float shear,
                                                  in WaterIsoDepthFrame frame)
            => (hullWorldY + heaveMeters - frame.ReferenceY) * shear;

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
        /// <para><b>The per-point law (measured into shape in pixels, 2026-07-23; re-derived under
        /// the shear, ADR 0033).</b> Solve the shared z-buffer's pixel-share fight between a hull
        /// face at rig height r on ground line ry (screen y rises at cos(elev) per metre of height
        /// and sin(elev) per metre of ground; depth falls at sin(elev) / rises at cos(elev), and
        /// ADR 0033's shear then takes <c>g·(screen y travelled)</c> back off it) and the displaced
        /// water (screen y rises at 1 per metre of lift — the vertex stage's <c>ws.y += lift</c>).
        /// A water sample at ground offset Δ from the hull's ROOT line with lift L fights, on EACH
        /// ground line ry, exactly the height <c>r(ry) = r_f − tan(elev)·ry</c> where
        /// <c>r_f = (Δ + L − H)/cos</c> — the shear moves no vertex on screen, so WHICH face is
        /// fought is unchanged — and wins iff</para>
        ///
        /// <code>
        /// r·(sin + cos·g) + r_f·cos²  &lt;  L·(cos+sin) − zHeave·sin − H·cos + ry·(cos − sin·g)
        /// </code>
        ///
        /// <para>and <b>that is where the two coefficients this law was built on come apart.</b>
        /// Substituting <c>g = cos·(1−sin)/sin</c> and <c>r_f = r + tan·ry</c>:</para>
        ///
        /// <code>
        /// sin + cos·g + cos²  =  (sin² + cos²·sin + cos² − cos²·sin)/sin  =  1/sin
        /// cos − sin·g − sin·cos  =  cos − cos·(1−sin) − sin·cos  =  0
        /// </code>
        ///
        /// <para>The protected height's coefficient <c>(cos²+sin) = 1.2296</c> becomes
        /// <c>1/sin = 1.5557</c> — the true iso relation — and <b>§24's beam residual
        /// <c>ry·cos·(1−sin)</c> cancels to EXACTLY ZERO</b>. It was never a shave to be tightened;
        /// it was the unit error, and the shear is what pays it off. (Which is why ADR 0033 forbids
        /// shipping the shear without this file: delete the term blind and the clamp keeps its old
        /// over-demand, shoving hulls toward the camera to cure a residual that no longer exists.)
        /// So the law collapses to <c>r/sin &lt; L·(cos+sin) − zHeave·sin − H·cos</c> — no ry at
        /// all — and keeping every interior face (r ≥ deckHeight, |ry| ≤ halfBeam) dry demands,
        /// per sample,</para>
        ///
        /// <code>
        /// ry* = min(halfBeam, (r_f − deckHeight)/tan(elev))   // the worst far-side line fought at/above the deck
        /// zHeave ≥ (L·(cos+sin) − (r_f − tan·ry*)/sin − H·cos) / sin
        /// </code>
        ///
        /// <para>⚠️ <b>ry* still matters, though it no longer appears as its own term.</b> It picks
        /// WHICH height on the fought line is the worst one at or above the deck; only the residual
        /// that used to be charged for standing on that line has gone. Net effect: the clamp demands
        /// strictly LESS heave than it did (the protected height buys 1.5557 instead of 1.2296, and
        /// the added residual is zero), so a hull is shoved toward the camera less — never more, so
        /// this cannot newly flood one.</para>
        ///
        /// <para>⚠️ <b>The clamp governs hulls the shear has NOT re-baked out from under, and it is
        /// the fallback wherever the per-face interior mask does not apply</b> — but every hull the
        /// clamp still runs for is drawn through <c>IsoFacetHullRenderer</c> and therefore IS
        /// sheared, which is why this re-derivation ships in the same PR as the shear and not later.
        /// Legacy SPRITE hulls never enter the facet z-buffer and never reach this function at all.</para>
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
        /// field evaluations per hull per pose push — microseconds on the desktop
        /// baseline.</para>
        /// </summary>
        public static float WatertightZHeaveMeters(float heaveMeters, float deckHeightMeters,
                                                   float halfBeamMeters,
                                                   Vector2 centerWorld, float halfWidthMeters,
                                                   in PackedWaveField field,
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
            // ⚠️ THE STEPS MUST SHORTEN WITH THE WAVELENGTHS. Both scan steps were sized against
            // "the fleet's shortest meaningful trains (λ ≥ ~10 m)" — but freqScale divides every
            // effective wavelength by exactly that factor. At the owner's 2.8 the shortest trains
            // land at ~3.6 m, and the 2 m x-step is then BELOW NYQUIST (1.8 samples per wave): the
            // scan can straddle a crest with two trough samples and miss it almost entirely — a
            // near-full-amplitude miss, not the ~2 % of amplitude the fixed step was chosen for.
            // That is the second half of why the clamp under-protected every hull. Refining both
            // axes keeps samples-per-wavelength invariant; cost stays trivial (the dragger's worst
            // case goes ~375 → ~2.8 k field evaluations per pose push (each now up to
            // WaveTrains.MaxTrains trains rather than four — ADR 0027 P2), i.e. tens of
            // microseconds on the desktop baseline — rule 7 is comfortable).
            float scan = Mathf.Max(1f, frame.FreqScale);
            int nx = Mathf.Max(1, Mathf.CeilToInt(halfWidthMeters * scan / FootprintScanStepMeters));
            int ny = Mathf.Max(1, Mathf.CeilToInt(
                FootprintScanHalfHeightMeters * scan / FootprintScanRowStepMeters));

            float demand = float.MinValue;
            for (int ix = -nx; ix <= nx; ix++)
            {
                float x = centerWorld.x + halfWidthMeters * ix / (float)nx;
                for (int iy = -ny; iy <= ny; iy++)
                {
                    float dy = FootprintScanHalfHeightMeters * iy / (float)ny;
                    // ⚠️ frame.FreqScale, NOT the implicit 1: the displaced vertex stage samples the
                    // field through _OceanSwellScale/0.025, so at the owner's 0.07 the DRAWN sea runs
                    // at 2.8x frequency. Scanning at 1 hunted crests 2.8x too far apart, under-
                    // demanded, and let the real ones board every hull (owner playtest 2026-07-25).
                    float lift = exaggeration * WaveFieldBridge.ShaderTwinSample(
                        new Vector2(x, centerWorld.y + dy), in field, frame.FreqScale).Height;
                    // ⚠️ THE HULL'S OWN SCREEN HEAVE IS PART OF THE FIGHT (owner playtest
                    // 2026-07-25, "water is in the hulls still" — on EVERY hull, including the two
                    // whose clamp data was already proven green).
                    //
                    // The hull's image is translated in world/screen Y by `heaveMeters` while its
                    // DEPTH is anchored at the root line (HullDepthBias takes root.y with no heave
                    // term of its own). So which hull face shares a pixel with a water sample
                    // depends on that lift: equating screen y for a water sample at ground offset
                    // dy with lift L against a face at rig height r on ground line ry gives
                    //     r(ry) = (dy + L − H)/cos − tan·ry,
                    // and the water wins iff
                    //     r(c²+s) < L(c+s) − zHeave·s + ry·c(1−s) − H·c.
                    // Both H terms were absent: the law was derived when a mesh hull's heave was
                    // the rig's ~0.04 m rock bob, where dropping them was worth nothing. ADR 0023
                    // phase 3 step 2 made the channel metre-scale — and H is NEGATIVE most of the
                    // time (the ride always subtracts the resting draft, and the sharpened field
                    // sits below still water for most of its period), which makes the demand come
                    // out `H·cot(elev)` ≈ 1.19·|H| TOO LOW exactly when the boat is down in a
                    // trough with the sea standing over her. The 0.4 m ramped safety only ever
                    // covered |H| ≤ 0.34 m.
                    //
                    // At H = 0 both terms vanish and this is bit-identical to the shipped law —
                    // which is precisely the pose HullWaterlineAcceptanceTests pins
                    // (`_hull.HeavePixels = 0f`), and precisely why the suite could never see this.
                    float foughtR = (dy + lift - heaveMeters) * cInv;
                    if (foughtR < deckHeightMeters) continue;   // fights the open planking: allowed
                    float ryStar = Mathf.Min(halfBeamMeters,
                                             (foughtR - deckHeightMeters) / tanE);
                    float protectedR = foughtR - tanE * ryStar;
                    // ADR 0033, the re-derived law (see the doc above): the protected height buys
                    // 1/sin instead of (cos²+sin), and §24's beam residual ry*·cos·(1−sin) is gone
                    // — it cancelled to exactly zero against the shear. The frame's cos/sin are the
                    // water's, and the collapse presumes hull and water share the one iso
                    // convention — the same presumption HullDepthShear documents, and the same 40°.
                    float need = (lift * (c + s) - protectedR / s - heaveMeters * c) / s;
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
