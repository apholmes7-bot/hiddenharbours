// HiddenHarboursAdditiveLight.shader — the reusable ADDITIVE 2D LIGHT (ADR 0016): a soft glow with a
// CONE/RADIAL shape that brightens (cuts through) the darkened night frame.
//
// WHY ADDITIVE-ABOVE-THE-OVERLAY (ADR 0016, building on ADR 0013). Sprites are Sprite-UNLIT and night is a
// full-screen MULTIPLY darkening overlay drawn at sortingOrder ~32760 — so a URP Light2D would do nothing
// (the unlit sprites sample no 2D light). A light here is instead an ADDITIVE quad drawn ABOVE that overlay
// (sortingOrder ~32770), blended One-One, so it ADDS brightness back into the crushed-dark frame — a lantern
// "punching a hole in the dark". The component (SceneLight) positions/orients/colors it; this shader does the
// soft shape + the night-gate.
//
// THE IN-SHADER NIGHT-GATE (zero per-light C# coupling to the cycle). The shader reads the global _DayNightTint
// the DayNightController publishes and scales its output by the frame DARKNESS (~ 1 - luminance(tint)): a light
// is ~invisible at a bright noon (so it can't wash daytime out) and full in a dark night. Mirrors LightMath.
// NightGate exactly (that C# copy is unit-tested headless). When the cycle is OFF the tint global is unset /
// near-black; we then DEFAULT TO SHOWING the light (tunable via _GateFallback) so the demo + edit-mode preview
// work — exactly how the water shader treats an unset tint.
//
// SHAPE. We work in the light's NORMALIZED local space: the quad spans [-1,1]; the light origin is the bottom-
// centre (uv.y=0 at the lamp, uv.y=1 at the far throw) so a CONE points "up" the local quad (the component
// orients the quad along the boat heading). Radial distance + the angle off the axis give the soft cone/round
// glow. A half-angle >= 180 is a full radial (round) glow; a small half-angle is a tight beam.
//
// Pixel-art friendly: smooth additive falloff with no texture (resolution-independent, crisp at any zoom), tiny
// cost (one quad). Visual-only: drives no sim, saves nothing (rule 5). Force-compiled by the magenta guard via
// the shipped Resources/AdditiveLight.mat — a break fails CI RED, not magenta-in-build.
//
// NOTE: no plus / operator characters inside any [Header] / [Tooltip] label (ShaderLab's lexer rejects them);
// no [unroll] over a runtime loop count; every symbol is defined before it is used.
Shader "HiddenHarbours/AdditiveLight"
{
    Properties
    {
        [Header(Light colour and strength)]
        [HDR] _LightColor   ("Light colour", Color) = (1, 0.86, 0.6, 1)
        _Intensity          ("Intensity (master)", Float) = 1.2

        [Header(Shape)]
        _ConeHalfAngle      ("Cone half-angle (deg, 180 is radial)", Range(0, 180)) = 30
        _AngularSoftness    ("Angular edge softness (0..1)", Range(0, 1)) = 0.4
        _EdgeSoftness       ("Radial edge softness (0..1)", Range(0, 1)) = 0.6
        _CoreBoost          ("Bright core boost", Range(0, 4)) = 1

        [Header(Night gate)]
        _GateThreshold      ("Gate darkness threshold (0..1)", Range(0, 1)) = 0.12
        _GateSoftness       ("Gate fade band (0..1)", Range(0, 1)) = 0.35
        _GateFallback       ("Show when no cycle (0..1)", Range(0, 1)) = 1

        [Header(Geometry set by the component)]
        _LampPos            ("Lamp position in quad space (xy in minus1..1)", Vector) = (0, -1, 0, 0)
        _Throw              ("Throw distance in quad-space units", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // ADDITIVE: ADD the glow into the (darkened) frame, brightening it. Premultiplied-style:
        // the fragment already multiplies colour by its scalar, so One One adds exactly that.
        Blend One One
        Cull Off
        ZWrite Off
        ZTest Always   // draw on top regardless of depth (it is a screen-space-ish overlay element)

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Global published by DayNightController (read-only here): the whole-frame multiply tint.
            float4 _DayNightTint;

            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor;
                float  _Intensity;
                float  _ConeHalfAngle;
                float  _AngularSoftness;
                float  _EdgeSoftness;
                float  _CoreBoost;
                float  _GateThreshold;
                float  _GateSoftness;
                float  _GateFallback;
                float4 _LampPos;
                float  _Throw;
            CBUFFER_END

            // --- pure helpers (mirror LightMath; defined BEFORE use) ---

            float Luma(float3 c)
            {
                return max(0.0, dot(c, float3(0.299, 0.587, 0.114)));
            }

            // The night-gate ramp on frame darkness, with the cycle-off fallback. Mirrors LightMath.NightGate /
            // NightGateWithFallback. _DayNightTint near-black (unset) => no cycle => show (the demo/edit case).
            float NightGate()
            {
                float lum = Luma(_DayNightTint.rgb);
                // "Cycle active" when the tint is meaningfully non-black (the controller is darkening the frame).
                float cycleActive = step(0.02, lum);
                float darkness = saturate(1.0 - lum);
                float lo = saturate(_GateThreshold);
                float hi = saturate(_GateThreshold + max(_GateSoftness, 1e-4));
                float gated = smoothstep(lo, hi, darkness);
                // No cycle => fall back to _GateFallback (default 1 = show).
                return lerp(saturate(_GateFallback), gated, cycleActive);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetVertexPositionInputs(IN.positionOS).positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Quad space: map uv [0,1]^2 to q in [-1,1]^2 (the quad centre is 0,0). The LAMP sits at
                // _LampPos in this space (bottom-centre (0,-1) for a cone so the beam throws "up"; centre (0,0)
                // for a radial so the halo is a full disc). 'rel' is the point relative to the lamp; the beam
                // axis is local +y. The component scales the quad so _Throw quad-space units == the world range.
                float2 q = IN.uv * 2.0 - 1.0;
                float2 rel = q - _LampPos.xy;

                // RADIAL falloff: distance from the lamp normalized by the throw (1 at the lamp, 0 at the edge).
                float dist = saturate(length(rel) / max(_Throw, 1e-4));
                float linearR = saturate(1.0 - dist);
                float power = lerp(2.0, 0.6, saturate(_EdgeSoftness));   // soft halo vs harder disc
                float radial = pow(linearR, power);

                // ANGULAR (cone) falloff: angle of 'rel' off the +y axis. halfAng >= 180 => full radial (no cut).
                float cone;
                float halfAng = max(_ConeHalfAngle, 0.0);
                if (halfAng >= 180.0)
                {
                    cone = 1.0;
                }
                else
                {
                    float ang = degrees(atan2(abs(rel.x), max(rel.y, 1e-4)));   // 0 on axis, grows off-axis
                    float soft = saturate(_AngularSoftness);
                    float inner = halfAng * (1.0 - soft);
                    cone = 1.0 - smoothstep(inner, halfAng, ang);
                    cone *= step(0.0, rel.y);   // forward-only: nothing behind the lamp
                }

                float shape = radial * cone;

                // Bright core: a small extra punch near the lamp so the source reads as a hot point.
                float core = pow(linearR, 6.0) * _CoreBoost;
                shape = saturate(shape + core * cone);

                float gate = NightGate();
                float k = max(_Intensity, 0.0) * shape * gate;

                if (k <= 0.0) discard;   // outside the cone / washed out by day -> contribute nothing

                // Premultiplied additive: colour already scaled by k; Blend One One adds it to the frame.
                float3 rgb = _LightColor.rgb * k;
                return half4(rgb, k);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
