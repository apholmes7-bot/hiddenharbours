// ⚠️ THROWAWAY SPIKE SHADER — spike/3d-boats. Reproduces the art director's rig rasteriser on the
// GPU: flat per-FACE normal, fixed screen-space key light, palette-RAMP lookup (not continuous
// lighting), ordered Bayer dither between adjacent ramp indices, no AA, no filtering.
//
// The mesh is placed in world space ALREADY iso-rotated (Rx(elev-90) * Rz(heading)), so the game's
// own straight-down orthographic 2D camera produces the rig's exact projection. That collapses the
// rig's shadeOf() to a plain dot(worldNormal, LN) — the key light is fixed in SCREEN space, which
// is what makes the shading read as pixel art rather than as lit 3D.
Shader "Hidden/HH/Spike3dBoats/IsoFacet"
{
    Properties { _RampTex ("Ramps", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off            // the rig z-buffers everything; it never backface-culls
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            Texture2D<float4> _RampTex;
            float4 _LN;                 // light normal (rig LN), screen-space fixed
            float  _Gain, _Bias;
            float4 _Bayer[4];           // BAYER[x&3][y&3], values already (v+0.5)/16
            float4 _RampMeta[16];       // per material: x = ramp length, y = index offset

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 attrs  : TEXCOORD0;   // x=matId  y=faceBias b  z=depthBias db
            };
            struct v2f {
                float4 pos   : SV_POSITION;
                nointerpolation float  fidx : TEXCOORD0;
                nointerpolation float  mat  : TEXCOORD1;
                float  depth : TEXCOORD2;    // TRUE (unbiased) view depth, for the keyline pass
            };
            struct frag_out { float4 col : SV_Target0; float d : SV_Target1; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // The rig: sh = shadeOf(n, se, ce). In iso-rotated world space that is exactly a
                // dot with LN — see the header comment.
                float sh = dot(wn, _LN.xyz);
                // "if(sh<0 && f.b<=-1) sh = shadeOf(-n)*0.9" — the rig's interior/backface rescue.
                if (sh < 0 && v.attrs.y <= -1) sh = -sh * 0.9;

                o.fidx  = sh * _Gain + _Bias + v.attrs.y;
                o.mat   = v.attrs.x;
                o.depth = wp.z;                      // camera looks along +Z; larger = further
                wp.z   -= v.attrs.z;                 // f.db pulls the face toward the camera
                o.pos   = mul(UNITY_MATRIX_VP, float4(wp, 1));
                return o;
            }

            frag_out frag (v2f i)
            {
                uint2 p = uint2(i.pos.xy);           // integer PIXEL coords, from the top-left
                float bay = _Bayer[p.x & 3][p.y & 3];

                int m    = (int)round(i.mat);
                int len  = (int)_RampMeta[m].x;
                int off  = (int)_RampMeta[m].y;
                float base = floor(i.fidx);
                int idx = (int)base + ((i.fidx - base) > bay ? 1 : 0) + off;
                idx = clamp(idx, 0, len - 1);

                frag_out o;
                o.col = _RampTex.Load(int3(idx, m, 0));
                o.d   = i.depth;
                return o;
            }
            ENDCG
        }
    }
}
