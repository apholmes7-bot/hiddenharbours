Shader "HiddenHarbours/Plant"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Albedo", 2D) = "white" {}
        [NoScaleOffset] _NormalTex ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _LightTex ("Sky transmission height", 2D) = "white" {}
        [NoScaleOffset] _DetailTex ("Part (r)", 2D) = "black" {}
        _Sway ("Wind bend metres", Float) = 0.16
        _FootRadius ("Foot radius metres", Float) = 0.7
        _FootStrength ("Foot bend metres", Float) = 0.3
        _FootDirSoftness ("Foot direction softness", Float) = 0.2
        _SkyAmount ("Sky amount", Float) = 0.8
        _SunAmount ("Sun amount", Float) = 1.1
        _Bounce ("Ground bounce", Float) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); TEXTURE2D(_LightTex); TEXTURE2D(_DetailTex);
            CBUFFER_START(UnityPerMaterial)
            float _Sway, _FootRadius, _FootStrength, _FootDirSoftness, _SkyAmount, _SunAmount, _Bounce;
            CBUFFER_END
            float4 _WindWorld, _SunDir;
            float _FoliageSnow, _ShadowStrength, _SunElevation;
            #define WALKERS_N 8
            #define POINTS_N 24
            float4 _GrassTrail[WALKERS_N * POINTS_N];
            float4 _GrassWalkers[WALKERS_N];
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; float4 root : TEXCOORD1; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input v)
            {
                Output o;
                float3 wp = TransformObjectToWorld(v.vertex);
                // root.z is height above this cell's pivot, supplied by the mesh, independent of atlas UV.
                float bend = v.root.z * v.root.z;
                float gust = sin(dot(v.root.xy, float2(0.37, 0.21)) - _Time.y * 1.7);
                float2 offset = _WindWorld.xy * _Sway * (0.65 + 0.35 * gust);
                float best = 0; float2 direction = 0;
                [loop] for (int w = 0; w < WALKERS_N; w++)
                {
                    float4 walker = _GrassWalkers[w];
                    if (walker.z < 0 || distance(v.root.xy, walker.xy) > walker.z + _FootRadius) continue;
                    [loop] for (int p = 0; p < POINTS_N; p++)
                    {
                        float4 trail = _GrassTrail[w * POINTS_N + p];
                        float2 delta = v.root.xy - trail.xy; float dist = length(delta);
                        float behind = 1 - smoothstep(0, max(_FootDirSoftness, 0.0001), dot(delta, float2(cos(trail.w), sin(trail.w))));
                        float push = (1 - smoothstep(0, max(_FootRadius, 0.0001), dist)) * saturate(trail.z) * lerp(1, behind, saturate(walker.w));
                        if (push > best) { best = push; direction = dist > 0.0001 ? delta / dist : float2(0, 1); }
                    }
                }
                wp.xy += (offset + direction * best * _FootStrength * v.root.w) * bend * (1 - saturate(_FoliageSnow) * 0.8);
                o.vertex = TransformWorldToHClip(wp); o.uv = v.uv; return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); clip(albedo.a - 0.01);
                float3 n = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_MainTex, i.uv).rgb * 2 - 1);
                float3 light = SAMPLE_TEXTURE2D(_LightTex, sampler_MainTex, i.uv).rgb;
                // The import moves part from the detail map's b to r (PlantRig5Baker.Load), so Standalone keeps it as R8.
                float part = SAMPLE_TEXTURE2D(_DetailTex, sampler_MainTex, i.uv).r;
                float sunUp;
                // Plant map depth runs opposite the shared sprite normal axis.
                float3 sun = SpriteLightSunDirection(float2(_SunDir.x, -_SunDir.y), _SunElevation, sunUp);
                float sunAmount = SpriteLightSunAmount(_SunDir.xy, sunUp, _ShadowStrength);
                float up = saturate(dot(n, float3(0, 0.76604444, 0.64278761)));
                float nl = dot(n, sun);
                float skyTerm = _SkyAmount * (0.3 + 0.7 * light.r) * (0.45 + 0.55 * up);
                float direct = _SunAmount * sunAmount * pow(max(0, nl), 1.25);
                float through = light.g * sunAmount * pow(max(0, -nl), 0.7) * 0.24;
                float level = skyTerm + direct + through + _Bounce * (0.5 - 0.5 * up);
                // Four-map approximation: the voxel sun transmission and palette ramps stay in the rig.
                albedo.rgb *= clamp(level, 0.2, 1.6);
                float snow = step(1.02 - saturate(_FoliageSnow) * 0.95, up) * step(0.2, light.r) * step(0.4, part);
                albedo.rgb = lerp(albedo.rgb, half3(0.81, 0.87, 0.89), snow * saturate(_FoliageSnow));
                // The scene overlay applies the day and night grade after this pass.
                return albedo;
            }
            ENDHLSL
        }
    }
}
