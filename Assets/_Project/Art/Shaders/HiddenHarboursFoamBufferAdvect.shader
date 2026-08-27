// ADR 0027 #6 — THE ADVECTED FOAM BUFFER: one blit that scrolls, decays and injects.
//
// The buffer holds TWO things about each patch of sea, ONE TEXEL PER WORLD CELL:
//   R = COVERAGE  — how much churned foam is on it (accumulates, decays, saturates)
//   G = FRESHNESS — how recently it was churned (a CLOCK: reset by churn, decays, never accumulates)
// Every frame IsoFacetHullFeature runs this pass into the ping-pong's write side:
//
//   1. SCROLL   — copy the previous buffer shifted by a WHOLE number of texels. That shift is the
//                 camera window's move PLUS the wind/current drift, both already reduced to whole
//                 world cells on the C# side (FoamBuffer.OriginShiftCells / FoamBuffer.AdvectCells).
//   2. DECAY    — multiply by an exponential half-life factor (FoamBuffer.DecayFactor), so a wake
//                 fades over seconds and the buffer's total never runs away. The two channels decay
//                 on their OWN half-lives: the colour walk has to finish while the foam is still
//                 visible, so freshness dies faster than coverage.
//   3. INJECT   — add a capsule of foam along each hull's swept segment this frame (a segment, not a
//                 point, so a fast boat or a frame hitch cannot lay a dashed trail), and MAX the
//                 freshness clock back to fully-fresh wherever that hull churned.
//
// 🔴 WHY G EXISTS (owner eyeball 2026-08-27: the wake "stays white — never disperses"). #665 read a
// texel's age off its coverage. Measured, that cannot work: coverage saturates at 1.0 within ~0.4 s
// of deposit and the water shader then thresholds and posterizes it to three values, so 72–81% of
// the visible band drew at age exactly 0 whatever the threshold was set to. Freshness is bounded by
// a MAX rather than an ADD, so it never clamps and is monotone in time-since-churn by construction.
// Twin: FoamBuffer.Freshness.
//
// 🔴 THE CELL LAW. A render target is screen-space by nature, and that is exactly the trap ADR 0027
// names for this item: with CameraFollow panning continuously behind the boat, a buffer whose cells
// are camera-relative makes the WHOLE WAKE CRAWL under every pan — the one artefact that would make
// this read as a screen filter instead of a mark left on a place in the sea. So the window origin is
// snapped onto a WORLD cell lattice (FoamBuffer.WorldCellOrigin) and every scroll here is an integer
// texel Load. Integer moves are also EXACT COPIES: no bilinear resample, so a wake never blurs into a
// smudge however long it lives. Twin: FoamBuffer — and its deliberately wrong sibling
// FoamBuffer.CameraRelativeOrigin, which exists only so the tests can MEASURE the crawl.
//
// ⚠️ FOAM_MAX_INJECTORS is a COMPILE-TIME loop bound, never a runtime one: ADR 0027 flags [unroll]
// over a runtime bound as a known magenta trap and WaterShaderCompileGuardTests guards the class.
// Unused slots carry amount 0, so they add exactly nothing and the fixed loop costs nothing while
// one boat is sailing.
//
// Visual only: this target is read by the water shader's foam compose and by NOTHING else. It feeds
// no sim and enters no save (rule 5 / ADR 0008) — it is accumulated visual state and is allowed to
// differ run-to-run, exactly as particles are.
Shader "Hidden/HiddenHarbours/FoamBufferAdvect"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "HHFoamBufferAdvect"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            // The canonical URP blit include stack (the IsoFacetResolve pass's, verbatim).
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ==== the seam constants — mirrored in C#, and FoamBufferTests reads THIS FILE to pin them ====
            // Cells per world metre. ⚠️ DELIBERATELY NOT the water material's _PixelsPerUnit: that is
            // an ART knob the owner may drag, and the C# twin cannot read a material, so quantizing
            // through it would let the two halves of this seam drift silently — the precedent (and the
            // exact wording of the ruling) is FETCH_MARCH_PPU. It is also COARSER than the pixel grid
            // on purpose: 8 cells/m = 4 screen px at PPU 32, the ADR's "foam coarser than caustics"
            // scale hierarchy. Twin: FoamBuffer.CellsPerUnit.
            #define FOAM_CELLS_PER_UNIT 8.0
            // The injection slot count — a COMPILE-TIME constant (see the header note).
            // Twin: FoamBuffer.MaxInjectors.
            #define FOAM_MAX_INJECTORS 8

            Texture2D<float4> _HHFoamPrev;   // r = previous COVERAGE, g = previous FRESHNESS

            float4 _HHFoamBufferWorld;   // xy = DRAW origin (lattice + banked drift residual, m), z = extent, w = 1/extent
            float4 _HHFoamResolution;    // xy = texels, zw = 1/texels
            float4 _HHFoamShift;         // xy = WHOLE-texel content shift this frame (camera window + drift)
            float  _HHFoamDecay;         // exponential decay multiplier for COVERAGE this frame
            float  _HHFoamAgeDecay;      // exponential decay multiplier for FRESHNESS this frame

            float4 _HHFoamInjectSeg[FOAM_MAX_INJECTORS];     // xy = from, zw = to (world m)
            // x = radius (m), y = amount (0 = unused slot), z = vigour 0..1 (the freshness GATE)
            float4 _HHFoamInjectShape[FOAM_MAX_INJECTORS];

            // Distance from a point to a segment — the capsule that keeps a fast boat's trail
            // continuous instead of a row of dots.
            float DistanceToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float denom = max(dot(ab, ab), 1e-8);
                float t = saturate(dot(p - a, ab) / denom);
                return length(p - (a + ab * t));
            }

            float4 frag (Varyings input) : SV_Target
            {
                int2 texel = int2(input.positionCS.xy);
                int2 res   = int2(_HHFoamResolution.xy);

                // ---- 1. SCROLL + 2. DECAY -------------------------------------------------------
                // Load(), not a uv sample: an integer texel fetch has no filtering, and it shares
                // SV_POSITION's coordinate convention — which removes the render-target Y-flip
                // ambiguity a uv fetch would smuggle in (the ObjectReflection precedent, same reason).
                int2 src = texel + int2(_HHFoamShift.xy);
                float2 previous = float2(0.0, 0.0);   // x = coverage, y = freshness
                // Off-buffer reads would wrap, or clamp a stale edge column across the sea, which
                // reads as a smear pinned to the window edge. Foam that has scrolled out of the
                // window simply is not there any more.
                if (all(src >= int2(0, 0)) && all(src < res))
                    previous = _HHFoamPrev.Load(int3(src, 0)).rg;

                float foam = previous.x * _HHFoamDecay;
                // TWIN of FoamBuffer.Freshness (the decay half; the max half is the injection loop).
                float fresh = saturate(previous.y) * saturate(_HHFoamAgeDecay);

                // ---- 3. INJECT --------------------------------------------------------------------
                // This texel's world position: the CENTRE of its world cell, so the deposit is
                // evaluated on the same lattice the buffer stores (no half-cell bias).
                //
                // ⚠️ Through the DRAW origin (lattice + banked drift residual), the SAME origin the
                // water shader reads through — so a mark is stamped exactly under the hull that made
                // it and thereafter drifts with the residual. Two different origins here would tear
                // the injection off the read by up to a cell. Twin: FoamBuffer.DrawOrigin.
                float2 worldXY = _HHFoamBufferWorld.xy
                               + (float2(texel) + 0.5) * (1.0 / FOAM_CELLS_PER_UNIT);

                [unroll]
                for (int i = 0; i < FOAM_MAX_INJECTORS; i++)   // CONSTANT bound — never a runtime one
                {
                    float4 seg   = _HHFoamInjectSeg[i];
                    float3 shape = _HHFoamInjectShape[i].xyz;  // x = radius, y = amount, z = vigour
                    float radius = max(shape.x, 1e-4);
                    // A soft-edged band: full along the swept line, gone by the radius. An unused
                    // slot's amount is 0, so it contributes nothing and needs no branch.
                    float d = DistanceToSegment(worldXY, seg.xy, seg.zw);
                    float falloff = 1.0 - smoothstep(radius * 0.5, radius, d);
                    foam += shape.y * falloff;
                    // The freshness CLOCK: a MAX, never an add — that is what stops this channel
                    // saturating the way coverage does, and it is the whole reason the wake can
                    // change colour at all.
                    //
                    // The mark is 1 wherever this hull churned this frame and 0 everywhere else. It is
                    // deliberately NOT scaled by how hard she is working: a clock scaled by vigour
                    // conflates "how hard" with "how long ago", so a dory at half her knee speed would
                    // be born half-aged and could never make white foam at all — the same category
                    // error round 1 made with the coverage channel, one level down. How MUCH foam is
                    // on this water is the R channel's job, and it already does it.
                    //
                    // Two branchless gates: shape.z is the injector's dt-INDEPENDENT vigour (0 in an
                    // unused slot, and 0 for a hull that is not working the water), and the falloff
                    // keeps the mark inside the band this hull actually swept.
                    float churned = step(1e-6, shape.z) * step(1e-3, falloff);
                    fresh = max(fresh, churned);
                }

                return float4(saturate(foam), saturate(fresh), 0, 1);
            }
            ENDHLSL
        }
    }
}
