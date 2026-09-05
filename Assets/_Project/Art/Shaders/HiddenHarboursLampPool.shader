// ================================================================================================
// HIDDEN HARBOURS — THE LAMP'S GROUND POOL (ADR 0016 amendment, world-lighting PR 2c)
// ================================================================================================
// The owner, 2026-09-04: "dock lights are just a round glow, it should glow from within the lamp
// reasilitcally." #733 answered the first half — the additive quad came down to the size of the lit
// fitting, so a lamp reads as a lamp. This is the second half: the PATCH OF GROUND the lamp makes
// brighter, which nothing in the game has ever drawn.
//
// ⭐⭐ IT MULTIPLIES UP. `Blend DstColor One` computes dst + src*dst = dst * (1 + src), so this pass
// SCALES what the frame already returned instead of adding a sheet of its own over it. That single
// choice is the whole difference from the disc the owner refused:
//
//   * A plank stays a plank and a dark gap stays dark. Relative contrast is a ratio, and a uniform
//     scale multiplies both of its terms — so the deck's texture survives BY CONSTRUCTION, not by
//     tuning. An additive quad cannot make that promise at any strength that reads.
//   * ⚠️⚠️ AND THE GAIN ARRIVES ALREADY DIVIDED BY THE NIGHT'S OWN LUMINANCE. A multiply is bounded
//     by what it multiplies, and ADR 0013's tint has crushed the frame long before this pass runs —
//     dst × 1.6 on a pier at 0.04 lifts a plank by six values in 255, and the first measured run of
//     this shader reported ZERO pixels changed. LampPoolSystem divides by the tint on the CPU (once
//     per lamp per frame, not per pixel), which is what reconstructs albedo × (ambient + lamp) from
//     a frame holding albedo × ambient.
//   * It is exactly the mirror of the sun's shade arm (#727), which multiplies DOWN with
//     `Blend Zero SrcColor`. Shade darkens what is there; a lamp brightens it. One ladder, two
//     directions, and the two now compose: a bollard's lamp shadow (#698) draws AFTER this pass and
//     multiplies it back down, so the shadow is the ABSENCE of this term rather than a separate
//     picture laid beside it.
//
// ⚠️ THE COST, STATED RATHER THAN BOUNDED AWAY. A screen-space multiply brightens whatever occupies
// the pixel — including something that is ABOVE the ground rather than standing on it: a boat's
// upper works passing through, a gull, the top of a stack of traps. The lamp SHADOW system already
// accepts precisely this cost, and so does the shade arm. It is the price of lighting a wharf whose
// planks, bollards and hulls are plain unlit sprites and a mesh, none of which is on any lit path.
//
// ⚠️ IT NEEDS A LAMP HEIGHT AND HAS NO OPINION WITHOUT ONE. The shape is h/sqrt(h²+d²) — the cosine
// between the lamp's ray and the ground's normal — so a lamp that publishes no height draws nothing
// at all rather than falling back to a flat disc. That fallback IS the bug this replaces.
Shader "HiddenHarbours/LampPool"
{
    Properties
    {
        // Colour × intensity of the lamp, and the gain ceiling.
        _PoolColor      ("Pool colour (rgb) x gain (a)", Color) = (1, 0.88, 0.62, 1)
        // xy = lamp world position on the ground plane, z = lamp height (m), w = reach (m).
        _PoolLamp       ("Lamp (xy world, z height m, w reach m)", Vector) = (0, 0, 2.5, 3.6)
        // x = edge softness, y = cos(cone half-angle) (<= -1 for a radial), z/w = cone axis xy.
        _PoolCone       ("x soft, y cosHalf, zw axis", Vector) = (0.35, -1, 0, 1)
        // The night gate, resolved on the CPU and handed in: x = gate 0..1. A pool is a night thing.
        _PoolGate       ("x = night gate", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // dst * (1 + src): a MULTIPLICATIVE brighten. See the header — this is the whole design.
        Blend DstColor One
        Cull Off
        ZWrite Off
        ZTest Always   // a compositing element above the world, like the quads it brightens under

        Pass
        {
            Name "LampPool"
            // ⚠️⚠️ WITHOUT THIS TAG URP DRAWS NOTHING, AND EVERYTHING ELSE LOOKS RIGHT. The 2D renderer
            // dispatches passes by LightMode; a pass that declares none is not in any pass list, so it is
            // silently skipped. The symptom is a renderer that is enabled, correctly posed, at the right
            // sorting order, carrying a material whose shader has the name you expect — and contributing
            // exactly zero pixels. There is no error, no magenta and no warning anywhere.
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            // No keywords at all, deliberately: nothing for a variant stripper to drop out of a player
            // build, which is the trap a runtime-built material otherwise walks into.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 worldXY : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _PoolColor;
                float4 _PoolLamp;
                float4 _PoolCone;
                float4 _PoolGate;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                // ⚠️ THE QUAD IS PINNED IN FRONT OF THE CAMERA (its Z is a compositing rung, not a
                // place in the world), so its own world Z is meaningless — but its XY is the honest
                // ground position under each fragment, because the camera is orthographic and looks
                // down +Z. That is what lets a screen-space pass shade by WORLD distance.
                o.worldXY = wp.xy;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 lamp = _PoolLamp.xy;
                float  h    = _PoolLamp.z;
                float  reach = max(_PoolLamp.w, 1e-4);

                // No published height ⇒ no opinion about the ground. Never a flat disc.
                if (h <= 0.0) return half4(0, 0, 0, 0);

                float2 toFrag = i.worldXY - lamp;
                float  d = length(toFrag);

                // 1 · THE SHAPE: how squarely the lamp's ray meets the ground here. LightMath.GroundIncidence.
                float incidence = h * rsqrt(h * h + d * d);

                // 2 · THE EDGE: 1 out to reach*(1-soft), smoothly to 0 at reach. LightMath.PoolFalloff.
                float soft  = saturate(_PoolCone.x);
                float inner = reach * (1.0 - soft);
                float edge  = 1.0 - smoothstep(inner, reach, d);

                // 3 · THE CONE, for a lamp that is aimed (the searchlight sweeping a dock). A radial
                // lamp ships cosHalf <= -1, which makes this exactly 1 for every direction — one code
                // path, no branch, and the radial case is the cone case with the gate wide open.
                float cosHalf = _PoolCone.y;
                float2 axis   = _PoolCone.zw;
                float  aligned = d > 1e-4 ? dot(toFrag / d, axis) : 1.0;
                // Feather the angular edge over the same softness the radial edge uses, so a beam's
                // rim on the ground is not a stamped wedge.
                float coneBand = max(1.0 - cosHalf, 1e-3) * soft;
                float cone = smoothstep(cosHalf, cosHalf + coneBand, aligned);

                float gain = _PoolColor.a * incidence * edge * cone * saturate(_PoolGate.x);
                if (gain <= 1e-4) return half4(0, 0, 0, 0);

                // The colour rides the gain: a sodium lamp warms the planks it lifts, and the multiply
                // means a channel the ground does not return cannot be invented here.
                return half4(_PoolColor.rgb * gain, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
