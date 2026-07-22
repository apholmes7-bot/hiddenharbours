// SHORE-SEAM PROOF HARNESS (ADR 0023) — editor-only evidence tooling, never shipped in a build.
//
// A displaced water surface in the game's pixel-art language (the 3D-water spike's IsoWater
// conventions: vertex lift by the ONE shared wave field, quantised palette bands, world-locked
// Bayer cells) EXTENDED with the shore seam under proof:
//
//   elevation(p) = _ProfileBase + _ProfileGrad * p.y      (an analytic shore transect, mirrored
//                                                          exactly in ShoreSeamRenderer's C#)
//   depth(p)     = _WaterLevel - elevation(p)              (the game's ONE depth rule)
//   fade         = ShoreFade01(depth, _Band)               (the HLSL TWIN of Core's
//                                                          ShoreFadeMath.Fade01 — keep in lockstep)
//   lift         = height * _HeightScale * fade            (ShoreFadeMath.DisplacedHeight)
//
// clip() uses the REAL depth exactly like the production water shader — the walkable waterline
// contour — so the rendered boundary can be measured against the analytic contour. _SeamEnabled 0
// is the CONTROL: full displacement with no fade (the naive port), which is what tears the coast.
// _MaskMode 1 renders water as pure red and land as pure green so the boundary is measurable per
// screen column with byte tests instead of eyeballs.
//
// SHADER CAUTIONS honoured (the project's magenta-trap list): no operator characters in Property
// display strings; no [unroll] over runtime bounds; pow bases floored (pow(0,0) is NaN on some
// GPUs); fixed loop bound of 4 with a count mask, like the production twin.
// RAW-COMMAND-BUFFER TRAP (spike-measured, ADR 0023): outside URP's frame there is NO reversed-Z
// translation — a hardcoded ZTest LEqual can silently kill everything against a raw cleared depth
// buffer. ZTest is [_ZTestOp], calibrated at runtime by ShoreSeamRenderer.CalibrateDepth().
Shader "Hidden/HH/ShoreSeamProof/Water"
{
    Properties
    {
        _ColDeep     ("Trough colour", Color)  = (0.02, 0.08, 0.26, 1)
        _ColMid      ("Mean colour", Color)    = (0.08, 0.26, 0.50, 1)
        _ColShallow  ("Crest colour", Color)   = (0.16, 0.50, 0.72, 1)
        _ColFoam     ("Foam colour", Color)    = (0.92, 0.97, 1.00, 1)
        _ColSandDry  ("Dry sand colour", Color) = (0.76, 0.68, 0.50, 1)
        _ColSandWet  ("Wet sand colour", Color) = (0.52, 0.46, 0.34, 1)
        _Bands       ("Value bands", Float)    = 7
        _FaceShade   ("Face shading strength", Float) = 0.45
        _SunDir2D    ("Implied light dir xy", Vector) = (-0.6, 0.8, 0, 0)
        _CapThreshold ("Whitecap crest threshold", Float) = 0.62
        _CapSolid    ("Whitecap solid core margin", Float) = 0.3
        _CapDither   ("Whitecap dither band width", Float) = 0.25
        _DitherWin   ("Band edge dither window", Float) = 0.4
        _PPU         ("Pixels per unit", Float) = 32
        _HeightScale ("Height exaggeration", Float) = 1.5
        _IsoScreen   ("Screen factors se ce", Vector) = (1, 1, 0, 0)
        _IsoDepth    ("Depth factors ce se", Vector) = (0.766, 0.643, 0, 0)
        _RefXY       ("Reference origin xy", Vector) = (0, 0, 0, 0)
        _ProfileBase ("Shore elevation at y zero", Float) = 0
        _ProfileGrad ("Shore elevation gradient per y", Float) = 0.15
        _WaterLevel  ("Water level m", Float) = 0
        _Band        ("Shore fade band m", Float) = 0.5
        _SeamEnabled ("Seam enabled", Float) = 1
        _MaskMode    ("Mask output mode", Float) = 0
        _ZTestOp     ("Z test op", Float) = 4
        _CalibColor  ("Calibration colour", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // The ONE shared wave field, exactly as WaveFieldBridge packs it (globals, outside any
        // CBUFFER). The harness C# publishes these itself for the chosen (scenario, time).
        float4 _WaveTrain0;      // xy = unit dir, z = wave number k, w = amplitude (m)
        float4 _WaveTrain1;
        float4 _WaveTrain2;
        float4 _WaveTrain3;
        float4 _WavePhases;      // per-train phase (rad), wrapped in C# double
        float4 _WaveFieldParams; // x = count, y = crest sharpening p, z = total amplitude

        CBUFFER_START(UnityPerMaterial)
            float4 _ColDeep, _ColMid, _ColShallow, _ColFoam, _ColSandDry, _ColSandWet;
            float  _Bands, _FaceShade;
            float4 _SunDir2D;
            float  _CapThreshold, _CapSolid, _CapDither, _DitherWin;
            float  _PPU, _HeightScale;
            float4 _IsoScreen, _IsoDepth, _RefXY;
            float  _ProfileBase, _ProfileGrad, _WaterLevel, _Band, _SeamEnabled, _MaskMode;
            float  _ZTestOp;
            float4 _CalibColor;
        CBUFFER_END

        // Line-for-line the production water shader's WaveFieldSample / the C# twin
        // (WaveFieldBridge.ShaderTwinSample): theta = k*(dir*pos) + phi, sharpened sine,
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

        // The analytic shore transect — mirrored EXACTLY in ShoreSeamRenderer (C#), so the
        // measured boundary can be compared against the analytic contour.
        float SeabedElevation(float2 p) { return _ProfileBase + _ProfileGrad * p.y; }
        float StillDepth(float2 p)      { return _WaterLevel - SeabedElevation(p); }

        // The HLSL TWIN of HiddenHarbours.Core.ShoreFadeMath.Fade01 (ADR 0023). Any change to the
        // C# must change this in the same PR (the WaveMath twin discipline).
        float ShoreFade01(float depth, float band)
        {
            if (depth <= 0.0) return 0.0;
            float t = saturate(depth / max(band, 1e-4));
            return t * t * (3.0 - 2.0 * t);
        }

        // Displacement fade: the seam under proof, or 1 for the naive control (_SeamEnabled 0).
        float DisplacementFade(float depth)
        {
            return _SeamEnabled > 0.5 ? ShoreFade01(depth, _Band) : 1.0;
        }

        // The rig's own 4x4 ordered-dither thresholds, already (v+0.5)/16 — indexed [x&3][y&3]
        // (the ADR 0022 dither discipline: world-locked cells cannot crawl).
        static const float BAYER[4][4] =
        {
            {  0.5/16.0,  8.5/16.0,  2.5/16.0, 10.5/16.0 },
            { 12.5/16.0,  4.5/16.0, 14.5/16.0,  6.5/16.0 },
            {  3.5/16.0, 11.5/16.0,  1.5/16.0,  9.5/16.0 },
            { 15.5/16.0,  7.5/16.0, 13.5/16.0,  5.5/16.0 },
        };
        ENDHLSL

        // ---- Pass 0: the displaced water surface, shore seam applied -------------------------
        Pass
        {
            Name "SeamWater"
            Cull Off
            ZWrite On
            ZTest [_ZTestOp]
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

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

                // THE SEAM: displacement dies to zero at the walkable waterline (depth 0) over
                // _Band metres of depth — ShoreFadeMath.DisplacedHeight, verbatim.
                float hs = h * _HeightScale * DisplacementFade(StillDepth(w));

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
                // Pixelize: quantise the surface's own coords to the PPU grid so shading sits on
                // world-locked pixel-art cells, and index Bayer by that cell.
                float2 cellF = i.groundXY * _PPU;
                int2 cell = int2(floor(cellF));
                float2 wq = (floor(cellF) + 0.5) / _PPU;
                float bay = BAYER[cell.x & 3][cell.y & 3];

                // The REAL waterline contour — the production water shader's exact clip contract
                // (depth from the still-water rule; displacement NEVER moves the contour).
                float depth = StillDepth(wq);
                clip(depth + 1e-4);

                if (_MaskMode > 0.5)
                    return half4(1.0, 0.0, 0.0, 1.0);   // water = pure red for boundary measurement

                float h, crest, totalAmp;
                float2 slope;
                WaveEval(wq, h, slope, crest, totalAmp);
                float fade = DisplacementFade(depth);

                // Value axis: normalised DISPLAYED height (faded height — what is actually lifted)
                // plus the lit-face/shaded-back term, then hard posterize with band-edge-only
                // dither (the spike's style law: solid bands, dithered EDGES only).
                float tN = clamp(h * fade / max(totalAmp, 1e-4), -1.0, 1.0) * 0.5 + 0.5;
                float2 sun = normalize(_SunDir2D.xy);
                float face = dot(-slope * fade, sun);
                float v = saturate(tN + face * _FaceShade);

                float bands = max(_Bands, 2.0);
                float x = v * (bands - 1.0);
                float fb = floor(x);
                float win = clamp(_DitherWin, 1e-3, 1.0);
                float e = saturate(((x - fb) - (0.5 - 0.5 * win)) / win);
                float q = (fb + (e > bay ? 1.0 : 0.0)) / (bands - 1.0);

                float3 col = q < 0.5
                    ? lerp(_ColDeep.rgb, _ColMid.rgb, q * 2.0)
                    : lerp(_ColMid.rgb, _ColShallow.rgb, (q - 0.5) * 2.0);

                // Whitecaps on the real sharpened crest tips, faded with the displacement so the
                // dying edge does not wear open-sea caps.
                float capSig = crest * fade - _CapThreshold;
                if (capSig > _CapSolid)
                    col = _ColFoam.rgb;
                else if (capSig > 0.0 && (capSig / max(_CapDither, 1e-4)) > bay)
                    col = _ColFoam.rgb;

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ---- Pass 1: the land layer (flat, undisplaced — land tiles do not move) -------------
        Pass
        {
            Name "SeamLand"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 groundXY   : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(v.positionOS.xyz);
                o.groundXY = v.positionOS.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_MaskMode > 0.5)
                    return half4(0.0, 1.0, 0.0, 1.0);   // land = pure green for boundary measurement

                // Pixel-quantised ground, dry sand above the waterline, wet sand below (visible
                // only where the water clipped or tore away — which is the point of the proof).
                float2 wq = (floor(i.groundXY * _PPU) + 0.5) / _PPU;
                float depth = StillDepth(wq);
                float3 col = depth > 0.0 ? _ColSandWet.rgb : _ColSandDry.rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ---- Pass 2: flat calibration quad (depth-convention probe — the measured trap) -------
        Pass
        {
            Name "SeamCalib"
            Cull Off
            ZWrite On
            ZTest [_ZTestOp]
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

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
