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
        // The weed under the water (the HHUnderWater pass below). PlantField writes all six per chunk from
        // PlantFieldDefinition and PlantSpeciesDef; these defaults only show on a bare material.
        _UnderWaterSway ("Under water sway metres", Float) = 0.12
        _UnderWaterSwayPeriod ("Under water sway period seconds", Float) = 7
        _UnderWaterSwayWavelength ("Under water sway wavelength metres", Float) = 9
        _UnderWaterTint ("Under water tint", Color) = (0.4, 0.38, 0.16, 1)
        _UnderWaterTintAmount ("Under water tint amount", Range(0, 1)) = 0
        _UnderWaterShade ("Under water shade", Range(0, 1)) = 1
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
            #include "Assets/_Project/Art/Shaders/Include/PlantWind.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); TEXTURE2D(_LightTex); TEXTURE2D(_DetailTex);
            CBUFFER_START(UnityPerMaterial)
            float _Sway, _FootRadius, _FootStrength, _FootDirSoftness, _SkyAmount, _SunAmount, _Bounce;
            float _UnderWaterSway, _UnderWaterSwayPeriod, _UnderWaterSwayWavelength, _UnderWaterTintAmount, _UnderWaterShade;
            float4 _UnderWaterTint;
            CBUFFER_END
            float4 _WindWorld, _SunDir;
            float _FoliageSnow, _ShadowStrength, _SunElevation;
            #define WALKERS_N 8
            #define POINTS_N 24
            float4 _GrassTrail[WALKERS_N * POINTS_N];
            float4 _GrassWalkers[WALKERS_N];
            // The clump law's tone steps (PlantClumpDef.ToneSteps), set per chunk by PlantField. Entry 0 is unused.
            #define TONES_N 8
            float4 _PlantTone[TONES_N];
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; float4 root : TEXCOORD1; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; nointerpolation half3 tone : TEXCOORD1; };
            Output Vert(Input v)
            {
                Output o;
                float3 wp = TransformObjectToWorld(v.vertex);
                // root.z is height above this cell's pivot, supplied by the mesh, independent of atlas UV.
                float bend = v.root.z * v.root.z;
                // root.w carries the foot-bend flag in its unit and the tone step in twos above it.
                // Step 0 is "as drawn": the tint is exactly 1 and the table is never read.
                float toneStep = floor(v.root.w * 0.5);
                float foot = v.root.w - toneStep * 2;
                float2 offset = PlantWindOffset(v.root.xy, _Time.y, _WindWorld.xy, _Sway);
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
                wp.xy += (offset + direction * best * _FootStrength * foot) * bend * (1 - saturate(_FoliageSnow) * 0.8);
                o.tone = toneStep < 0.5 ? half3(1, 1, 1) : _PlantTone[(int)min(toneStep, TONES_N - 1)].rgb;
                o.vertex = TransformWorldToHClip(wp); o.uv = v.uv; return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); clip(albedo.a - 0.01);
                // One tone step for the whole plant: a discrete tint, never a gradient across it.
                albedo.rgb *= i.tone;
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
        // ---- THE WEED UNDER THE WATER (the sea's HHUnderWater list) -----------------------------------
        // Drawn only for a chunk carrying UnderWaterRegistry.RenderingLayer (PlantField.JoinsUnderWater: a
        // tidal species under the sea plane, both switches on), into the target the sea lays over its floor
        // (IsoFacetHullFeature). ALBEDO ONLY, premultiplied, as HiddenHarboursLitSprite's pass draws a placed
        // shore plant: the sea's column darkens and blues it with depth, bends it with the floor, adds the
        // caustics and grades it, the one law the floor beside it takes, so nothing of that is copied here.
        // No walker push and no wind bend: under the surface the weed rocks with the swell's surge instead.
        // The rows are the pass above's, in the same order, so both passes share one UnityPerMaterial.
        Pass
        {
            Name "HHPlantUnderWater"
            Tags { "LightMode"="HHUnderWater" }
            Blend One OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex UnderWaterVert
            #pragma fragment UnderWaterFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Sway, _FootRadius, _FootStrength, _FootDirSoftness, _SkyAmount, _SunAmount, _Bounce;
            float _UnderWaterSway, _UnderWaterSwayPeriod, _UnderWaterSwayWavelength, _UnderWaterTintAmount, _UnderWaterShade;
            float4 _UnderWaterTint;
            CBUFFER_END
            float4 _WindWorld;
            #define TONES_N 8
            float4 _PlantTone[TONES_N];
            // The HLSL twin of PlantField.UnderWaterSurge: along the wind (east when it is calm), a sine of
            // the swell's period travelling one wavelength across the bed. 0.1 is
            // PlantFieldDefinition.MinUnderWaterSwaySpan.
            float2 PlantUnderWaterSurge(float2 root, float time, float2 windWorld, float sway, float period, float wavelength)
            {
                float2 dir = dot(windWorld, windWorld) > 1e-8 ? normalize(windWorld) : float2(1.0, 0.0);
                float phase = 6.28318530718 * (time / max(period, 0.1) - dot(root, dir) / max(wavelength, 0.1));
                return dir * (sway * sin(phase));
            }
            // The HLSL twin of PlantField.UnderWaterTint: the species' hue at the pixel's own brightness, then its
            // shade (1 as bright as the sheet, less toward a dark silhouette over the lighter floor).
            float3 PlantUnderWaterTint(float3 albedo, float3 tint, float amount, float shade)
            {
                const float3 lumaWeights = float3(0.299, 0.587, 0.114);
                float luma = dot(albedo, lumaWeights);
                float3 hue = tint * (luma / max(dot(tint, lumaWeights), 1e-3));
                return lerp(albedo, saturate(hue), saturate(amount)) * saturate(shade);
            }
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; float4 root : TEXCOORD1; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; nointerpolation half3 tone : TEXCOORD1; };
            Output UnderWaterVert(Input v)
            {
                Output o;
                float3 wp = TransformObjectToWorld(v.vertex);
                // The pass above's bend: 0 at the holdfast, the full sway at the frond tips.
                float bend = v.root.z * v.root.z;
                float toneStep = floor(v.root.w * 0.5);
                wp.xy += PlantUnderWaterSurge(v.root.xy, _Time.y, _WindWorld.xy, _UnderWaterSway,
                                              _UnderWaterSwayPeriod, _UnderWaterSwayWavelength) * bend;
                o.tone = toneStep < 0.5 ? half3(1, 1, 1) : _PlantTone[(int)min(toneStep, TONES_N - 1)].rgb;
                o.vertex = TransformWorldToHClip(wp); o.uv = v.uv; return o;
            }
            half4 UnderWaterFrag(Output i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); clip(albedo.a - 0.01);
                albedo.rgb *= i.tone;
                albedo.rgb = PlantUnderWaterTint(albedo.rgb, _UnderWaterTint.rgb, _UnderWaterTintAmount, _UnderWaterShade);
                // Premultiplied: overlapping fronds composite in ONE target, as the sea's read expects.
                return half4(albedo.rgb * albedo.a, albedo.a);
            }
            ENDHLSL
        }
    }
}
