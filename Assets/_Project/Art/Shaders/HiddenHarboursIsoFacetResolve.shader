// HiddenHarboursIsoFacetResolve.shader — the rig's keyline post-pass as a FULLSCREEN shader
// (ADR 0022 phase 3; the spike ran this on the CPU for exactness, production wants it here).
//
// Two rules, verbatim from the rigs' shared rasteriser:
//   1. DEPTH-EDGE DARKENING (doEdge): where two adjacent solid pixels differ in TRUE view depth
//      by more than 0.30 m, the FAR one is replaced by its colour darkened two RINDEX ramp
//      steps. The darkened colour was precomputed per (material, ramp index) by
//      IsoFacetMath.BuildDarkenedRamps and drawn into _HHDarkTex by the facet pass, so here it
//      is a plain load — RINDEX's aliased-ramp resolution included.
//   2. 1 PX KEYLINE: an empty pixel with any solid 4-neighbour becomes the neighbour's keyline
//      colour (and carries that neighbour's hull id, so the right overlay quad re-composes it).
//
// ⚠️ ADR 0031 (the keyline retirement): rule 2 is production-GATED by _HHKeylineFlood, ship
// default OFF — the outline is retired from the world-art style, and a hull's edge is carried by
// its own shaded turning faces. Rule 1 ALWAYS runs (it is the separate interior rule that keeps
// overlapping parts of one object readable). With the gate ON the pass is verbatim the rigs'
// shared rasteriser, which is what the GPU oracle fixtures (IsoFacetUrpPassTests and kin) force
// and pin. The gate lives INSIDE this one pass on purpose — never a forked variant that can
// drift from what ships.
//
// ⭐ RIG 9'S INK (character PR 2a, ADR 0044 §8). A v9 character's own renderer (characterIsoRig9.js
// paint) inks the figure by two rules of its own, and a figure whose ink is live
// (GameConfig.MeshFigureKeyline, IsoCharacterFigureRenderer.SyncInk) is drawn by them here:
//   1'. ITS EDGE: the farther of two adjacent solid pixels drops ONE ramp step when their depths
//       differ by more than SHADING.edge (0.12 m, not 0.30). The facet pass drew the step-down
//       colour into _HHDarkTex for the figure's pixels and flagged them with dark alpha 0; a hull
//       writes 1 there, as it always has. Each pixel keeps ITS OWNER'S rule against every solid
//       neighbour, so no hull pixel reads a figure's threshold.
//   2'. ITS KEYLINE, on EMPTY pixels only: an empty pixel beside the figure takes the keyline
//       colour mixed SHADING.keylineMix of the way toward its NEAREST figure neighbour's colour
//       after 1' (ties to the one whose bytes sum lower), scanned up, right, left, down. Never
//       over a hull or deck pixel, and never a hull's own neighbour: the ring is the figure's.
// _HHFigureInk.z = 0 — no figure inked, which is every frame with the dial off — and the pass is
// exactly the program above, byte for byte. With the flood on, a pixel the flood gives to a hull
// keeps the hull's keyline; only a pixel it gives to an inked figure takes the figure's ink.
//
// Rules 1, 1' and 2 are neighbour-symmetric, so render-target y-orientation cannot change them;
// 2' reads its scan order only to break exact ties (see kInkOffs).
// Output alpha carries the hull id (id/255); 0 = nothing here. Runs once per camera into the
// persistent _HHHullScreenTex that the in-scene overlay quads sample.
Shader "Hidden/HiddenHarbours/IsoFacetResolve"
{
    Properties
    {
        // ADR 0031: the keyline gate — 1 floods the legacy 1 px outline (rule 2), 0 leaves empty
        // pixels empty. Written per frame by IsoFacetKeylineGate.Apply from the owner's GameConfig
        // dial. Default 0 ON PURPOSE: a material the feature never touched fails to the SHIPPED
        // (outline-free) style, never back to outlines.
        _HHKeylineFlood ("Keyline Flood (ADR 0031 production gate)", Float) = 0
        // Rig 9's ink: (edge, keylineMix, live, 0), written per frame by IsoFacetFigureInk.Apply.
        // Default zero ON PURPOSE, for the same reason: an untouched material inks no figure.
        _HHFigureInk ("Figure ink (character PR 2a)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "HHHullKeylineResolve"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            // The canonical URP blit include stack (see e.g. URP's own Bloom.shader): URP Core
            // must precede Blit.hlsl or its TEXTURE2D_X macros are undefined.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            Texture2D<float4> _HHFacetTex;   // rgb = facet colour, a = hull id
            Texture2D<float4> _HHDarkTex;    // rgb = RINDEX darkened colour; a = 0 on an inked figure
            Texture2D<float4> _HHKeyTex;     // rgb = keyline colour
            Texture2D<float>  _HHDepthTex;   // true unbiased view depth, metres

            // ADR 0031: the keyline gate (see the Properties block). Gates rule 2 ONLY.
            float _HHKeylineFlood;

            // Rig 9's ink (see the header): x = SHADING.edge, y = SHADING.keylineMix, z = 1 while live.
            float4 _HHFigureInk;

            // The rig's doEdge threshold — a property of the art director's renderer being
            // transcribed (like GAIN and BIAS), not a game tunable.
            #define HH_EDGE_DEPTH_MIN 0.30

            // 4-neighbourhood offsets. Fixed bound — never an [unroll] over a runtime count.
            static const int2 kOffs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

            // Rule 2''s scan order, rig 9's [0,−1], [1,0], [−1,0], [0,1] (up, right, left, down) in its
            // y-down frame, written in THIS pass's pixel rows, which run UP the picture: Unity renders
            // every render texture bottom-up on every graphics API (the projection flip), the same
            // fact the reflection pass's mirror row leans on. It only ever breaks an exact tie of
            // depth AND byte sum.
            static const int2 kInkOffs[4] = { int2(0, 1), int2(1, 0), int2(-1, 0), int2(0, -1) };

            bool HHInBounds(int2 q, uint w, uint h)
            {
                return q.x >= 0 && q.y >= 0 && q.x < (int)w && q.y < (int)h;
            }

            // Rules 1 and 1' for ONE solid pixel: its colour after the depth-edge darkening. Solid:
            // darken if ANY in-bounds solid 4-neighbour is nearer by more than the threshold
            // (equivalent to the rig's pairwise right/down sweep, which darkens the far side of each
            // qualifying pair). The threshold is 0.30 m unless the pixel is an inked figure's.
            float3 HHSolidRgb(int2 p, float4 c, uint w, uint h)
            {
                float edge = HH_EDGE_DEPTH_MIN;
                if (_HHFigureInk.z > 0.5 && _HHDarkTex.Load(int3(p, 0)).a < 0.5) edge = _HHFigureInk.x;
                float d = _HHDepthTex.Load(int3(p, 0));
                bool darken = false;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 q = p + kOffs[k];
                    if (!HHInBounds(q, w, h)) continue;
                    if (_HHFacetTex.Load(int3(q, 0)).a <= 0) continue;
                    float dn = _HHDepthTex.Load(int3(q, 0));
                    if (abs(d - dn) > edge && d > dn) darken = true;
                }
                return darken ? _HHDarkTex.Load(int3(p, 0)).rgb : c.rgb;
            }

            // The bytes an sRGB target stored for a colour it hands back linear, and back: the
            // three targets are R8G8B8A8_SRGB, the rig mixes BYTES, and _HHHullScreenTex re-encodes
            // the exact byte from the exact linear value.
            float3 HHBytesOf(float3 lin)
            {
                float3 s = lin <= 0.0031308 ? lin * 12.92 : 1.055 * pow(max(lin, 0.0031308), 1.0 / 2.4) - 0.055;
                return floor(s * 255.0 + 0.5);
            }

            float3 HHLinearOf(float3 bytes)
            {
                float3 s = bytes / 255.0;
                return s <= 0.04045 ? s / 12.92 : pow((max(s, 0.04045) + 0.055) / 1.055, 2.4);
            }

            // Rule 2', for one EMPTY pixel: false when no inked figure's pixel borders it. The
            // candidate test is paint's "dep[j] < sd − 1e-9 || (|dep[j] − sd| <= 1e-9 && lum < sl)";
            // a float depth has no two values within 1e-9 at a figure's range, so equal is the tie.
            // The mix is mixHex(keyline, src, mix) as the resolve can do it: per byte,
            // floor(k + (s − k)·mix + 0.5) in single precision, `precise` so no compiler fuses it
            // into one rounding (RigPaint9.MixHexSingle; the guard proves it equals the rig's
            // double-precision mix on every byte pair at the def's mix).
            bool HHFigureKeyline(int2 p, uint w, uint h, out float4 ink)
            {
                bool found = false;
                float sd = 0, sl = 0, sa = 0;
                float3 sb = 0;
                int2 sq = p;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 q = p + kInkOffs[k];
                    if (!HHInBounds(q, w, h)) continue;
                    float4 cq = _HHFacetTex.Load(int3(q, 0));
                    if (cq.a <= 0) continue;
                    if (_HHDarkTex.Load(int3(q, 0)).a >= 0.5) continue;   // a hull's pixel
                    float dn = _HHDepthTex.Load(int3(q, 0));
                    float3 bytes = HHBytesOf(HHSolidRgb(q, cq, w, h));
                    float lum = bytes.r + bytes.g + bytes.b;
                    if (!found || dn < sd || (dn == sd && lum < sl))
                    {
                        found = true;
                        sd = dn;
                        sl = lum;
                        sb = bytes;
                        sa = cq.a;
                        sq = q;
                    }
                }
                ink = float4(0, 0, 0, 0);
                if (!found) return false;
                float3 kb = HHBytesOf(_HHKeyTex.Load(int3(sq, 0)).rgb);
                precise float3 mixed = floor(kb + (sb - kb) * _HHFigureInk.y + 0.5);
                ink = float4(HHLinearOf(mixed), sa);
                return true;
            }

            float4 frag (Varyings input) : SV_Target
            {
                uint w, h;
                _HHFacetTex.GetDimensions(w, h);
                int2 p = int2(input.positionCS.xy);

                float4 c = _HHFacetTex.Load(int3(p, 0));
                if (c.a > 0)
                    return float4(HHSolidRgb(p, c, w, h), c.a);

                float4 ink;
                // Empty: rule 2, the 1 px keyline flood — RETIRED from the world style by ADR 0031
                // and gated here, inside the same pass rule 1 runs in. Gate OFF: an empty pixel
                // returns exactly what an empty pixel with no solid neighbour always returned —
                // float4(0,0,0,0) — so every solid pixel (darkening included) and the water share
                // are byte-identical whichever way the gate sits; only rig 9's own ring (rule 2')
                // lands on an empty pixel beside an inked figure. Gate ON (the GPU oracle fixtures
                // force this): the flood below runs verbatim from the rigs' shared rasteriser.
                if (_HHKeylineFlood < 0.5)
                {
                    if (_HHFigureInk.z > 0.5 && HHFigureKeyline(p, w, h, ink)) return ink;
                    return float4(0, 0, 0, 0);
                }
                int2 q0 = p;
                float a0 = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 q = p + kOffs[k];
                    if (a0 > 0 || !HHInBounds(q, w, h)) continue;
                    float na = _HHFacetTex.Load(int3(q, 0)).a;
                    if (na > 0) { q0 = q; a0 = na; }
                }
                if (a0 <= 0) return float4(0, 0, 0, 0);
                // The flood gave this pixel to an inked figure: that figure's own ring instead.
                if (_HHFigureInk.z > 0.5 && _HHDarkTex.Load(int3(q0, 0)).a < 0.5 && HHFigureKeyline(p, w, h, ink))
                    return ink;
                return float4(_HHKeyTex.Load(int3(q0, 0)).rgb, a0);
            }
            ENDHLSL
        }
    }
}
