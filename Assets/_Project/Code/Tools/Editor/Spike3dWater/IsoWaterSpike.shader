// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-water. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
//
// The DISPLACED water surface, spoken in the game's pixel-art language: the vertex stage lifts a
// flat grid by the ONE shared deterministic wave field (the SAME packed globals WaveFieldBridge
// publishes — _WaveTrain0..3/_WavePhases/_WaveFieldParams, evaluated exactly like the production
// water shader's WaveFieldSample twin), and the fragment stage shades it as QUANTISED palette
// bands with ordered Bayer dither on world-locked pixel cells — NOT smooth lit 3D. The palette
// anchors are read off the owner's Water.mat at run time, so the surface wears his colours.
//
// Iso conventions follow ADR 0022 (the facet pass): the "3D" lives in the object placement /
// vertex math and the game's ordinary straight-down 2D ortho camera does the rest. Two framing
// modes via _IsoScreen:
//   A/B mode  (_IsoScreen = (1, 1)):        the game-faithful framing — the ground plane is the
//             unforeshortened world XY exactly as production draws it, and wave height lifts
//             screen-y by heightScale metres, the SAME mapping production boat heave uses
//             (IsoFacetMath.HeaveOffset: straight world +Y, metres = px/PPU).
//   probe mode (_IsoScreen = (sin e, cos e)): the rig's true iso frame (ADR 0022 projection rows:
//             screen-y = y·se + z·ce, depth = y·ce − z·se) so the surface depth-tests EXACTLY
//             against a hull mesh placed by IsoFacetMath.RigToWorld.
// Depth always uses the rig rows (_IsoDepth = (cos e, sin e)) so a nearer crest occludes the
// water behind it — the occlusion cue flat water cannot give.
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters
// in Property display strings; no [unroll] over runtime bounds; pow bases floored (pow(0,0) is
// NaN on some GPUs); fixed loop bound of 4 with a count mask, like the production twin.
Shader "Hidden/HH/Spike3dWater/IsoWater"
{
    Properties
    {
        _ColDeep    ("Trough colour", Color)  = (0.05, 0.135, 0.205, 1)
        _ColMid     ("Mean colour", Color)    = (0.14, 0.30, 0.38, 1)
        _ColShallow ("Crest colour", Color)   = (0.34, 0.60, 0.62, 1)
        _ColFoam    ("Foam colour", Color)    = (0.92, 0.96, 0.98, 1)
        _Bands      ("Value bands", Float)    = 6
        _FaceShade  ("Face shading strength", Float) = 0.35
        _SunDir2D   ("Implied light dir xy", Vector) = (-0.6, 0.8, 0, 0)
        _CapThreshold ("Whitecap crest threshold", Float) = 0.55
        _CapSolid   ("Whitecap solid core margin", Float) = 0.2
        _CapDither  ("Whitecap dither band width", Float) = 0.25
        _PPU        ("Pixels per unit", Float) = 32
        _HeightScale ("Height exaggeration", Float) = 1
        _IsoScreen  ("Screen factors se ce", Vector) = (1, 1, 0, 0)
        _IsoDepth   ("Depth factors ce se", Vector) = (0.766, 0.643, 0, 0)
        _RefXY      ("Reference origin xy", Vector) = (0, 0, 0, 0)
        _ZTestOp    ("Z test op", Float) = 4
        _CalibColor ("Calibration colour", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // ---- Pass 0: the displaced, band-shaded water surface --------------------------------
        Pass
        {
            Name "SpikeIsoWater"
            Cull Off
            ZWrite On
            ZTest [_ZTestOp]
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // The ONE shared wave field, exactly as WaveFieldBridge packs it (globals, outside
            // any CBUFFER). The spike C# publishes these itself for a chosen (scenario, time).
            float4 _WaveTrain0;      // xy = unit dir, z = wave number k, w = amplitude (m)
            float4 _WaveTrain1;
            float4 _WaveTrain2;
            float4 _WaveTrain3;
            float4 _WavePhases;      // per-train phase (rad), wrapped in C# double
            float4 _WaveFieldParams; // x = count, y = crest sharpening p, z = total amplitude

            CBUFFER_START(UnityPerMaterial)
                float4 _ColDeep, _ColMid, _ColShallow, _ColFoam;
                float  _Bands, _FaceShade;
                float4 _SunDir2D;
                float  _CapThreshold, _CapSolid, _CapDither;
                float  _PPU, _HeightScale;
                float4 _IsoScreen, _IsoDepth, _RefXY;
                float  _ZTestOp;
                float4 _CalibColor;
            CBUFFER_END

            // The rig's own 4x4 ordered-dither thresholds, already (v+0.5)/16 — indexed [x&3][y&3]
            // with row = x, exactly as the boat rigs hold it (ADR 0022 dither discipline).
            static const float BAYER[4][4] =
            {
                {  0.5/16.0,  8.5/16.0,  2.5/16.0, 10.5/16.0 },
                { 12.5/16.0,  4.5/16.0, 14.5/16.0,  6.5/16.0 },
                {  3.5/16.0, 11.5/16.0,  1.5/16.0,  9.5/16.0 },
                { 15.5/16.0,  7.5/16.0, 13.5/16.0,  5.5/16.0 },
            };

            // Line-for-line the production water shader's WaveFieldSample / the C# twin
            // (WaveFieldBridge.ShaderTwinSample): theta = k·(dir·pos) + phi, sharpened sine,
            // ANALYTIC slope, crest factor normalised by the amplitude envelope.
            void WaveEval(float2 p, out float height, out float2 slope, out float crest, out float totalAmp)
            {
                height = 0.0;
                slope = float2(0.0, 0.0);
                int count = (int)(_WaveFieldParams.x + 0.5);
                float sharp = max(_WaveFieldParams.y, 1.0);
                totalAmp = _WaveFieldParams.z;

                float4 trains[4] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3 };
                float  phases[4] = { _WavePhases.x, _WavePhases.y, _WavePhases.z, _WavePhases.w };

                for (int i = 0; i < 4; i++)   // fixed bound, count-masked (the twin's own shape)
                {
                    float4 tr = trains[i];
                    float amp = tr.w;
                    if (i >= count || amp <= 0.0) continue;

                    float theta = tr.z * (tr.x * p.x + tr.y * p.y) + phases[i];
                    float sn = sin(theta);
                    float cs = cos(theta);
                    float s = (sn + 1.0) * 0.5;
                    float shaped = pow(max(s, 1e-6), sharp);
                    height += amp * (2.0 * shaped - 1.0);
                    float sm = amp * sharp * pow(max(s, 1e-6), sharp - 1.0) * cs * tr.z;
                    slope += sm * tr.xy;
                }

                crest = 0.0;
                if (totalAmp > 1e-6)
                    crest = pow(saturate(height / totalAmp), sharp);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;   // xy = WORLD ground coords (grid built in world space)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 groundXY   : TEXCOORD0;  // the surface's own material coords (pre-displacement)
            };

            Varyings vert(Attributes v)
            {
                float2 w = v.positionOS.xy;
                float h, crest, totalAmp;
                float2 slope;
                WaveEval(w, h, slope, crest, totalAmp);
                float hs = h * _HeightScale;

                float3 pos;
                pos.x = w.x;
                pos.y = _RefXY.y + (w.y - _RefXY.y) * _IsoScreen.x + hs * _IsoScreen.y;
                pos.z = (w.y - _RefXY.y) * _IsoDepth.x - hs * _IsoDepth.y;

                Varyings o;
                o.positionCS = TransformWorldToHClip(pos);
                o.groundXY = w;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Pixelize: quantise the surface's own coords to the PPU grid so the shading sits
                // on pixel-art cells, and index the Bayer matrix by that WORLD-locked cell (the
                // production facet lesson: world-derived dither cannot crawl under translation).
                float2 cellF = i.groundXY * _PPU;
                int2 cell = int2(floor(cellF));
                float2 wq = (floor(cellF) + 0.5) / _PPU;
                float bay = BAYER[cell.x & 3][cell.y & 3];

                float h, crest, totalAmp;
                float2 slope;
                WaveEval(wq, h, slope, crest, totalAmp);

                // Value axis: normalised height (trough 0 .. crest 1) plus the antisymmetric
                // lit-face/shaded-back term (the production _SwellFaceShade idea, quantised here).
                float tN = clamp(h / max(totalAmp, 1e-4), -1.0, 1.0) * 0.5 + 0.5;
                float2 sun = normalize(_SunDir2D.xy);
                float face = dot(-slope, sun);
                float v = saturate(tN + face * _FaceShade);

                // Posterize into _Bands discrete steps, Bayer-dithered at the band boundary.
                float bands = max(_Bands, 2.0);
                float x = v * (bands - 1.0);
                float fb = floor(x);
                float q = (fb + ((x - fb) > bay ? 1.0 : 0.0)) / (bands - 1.0);

                float3 col = q < 0.5
                    ? lerp(_ColDeep.rgb, _ColMid.rgb, q * 2.0)
                    : lerp(_ColMid.rgb, _ColShallow.rgb, (q - 0.5) * 2.0);

                // Whitecaps on the REAL sharpened crest tips: a solid core near the tip, a
                // Bayer-dithered fringe below it — the rig's two-tone foam language.
                float capSig = crest - _CapThreshold;
                if (capSig > _CapSolid)
                    col = _ColFoam.rgb;
                else if (capSig > 0.0 && (capSig / max(_CapDither, 1e-4)) > bay)
                    col = _ColFoam.rgb;

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ---- Pass 1: flat calibration quad (depth-convention probe, like the boats spike) ----
        Pass
        {
            Name "SpikeCalib"
            Cull Off
            ZWrite On
            ZTest [_ZTestOp]
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColDeep, _ColMid, _ColShallow, _ColFoam;
                float  _Bands, _FaceShade;
                float4 _SunDir2D;
                float  _CapThreshold, _CapSolid, _CapDither;
                float  _PPU, _HeightScale;
                float4 _IsoScreen, _IsoDepth, _RefXY;
                float  _ZTestOp;
                float4 _CalibColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_CalibColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
