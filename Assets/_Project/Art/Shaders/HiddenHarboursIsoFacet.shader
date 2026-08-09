// HiddenHarboursIsoFacet.shader — the mesh-hull facet pass (ADR 0022 phase 3).
//
// Reproduces the art director's rig rasteriser on the GPU: flat per-FACE normal, fixed
// screen-space key light, palette-RAMP lookup (not continuous lighting), ordered Bayer dither
// between adjacent ramp indices, no AA, no filtering. Descends from the measured spike shader
// (spike/3d-boats, ADR 0022: 1.3–4.4% px vs the rig's own render, dither crawl 0.00%).
//
// The hull mesh is placed in world space ALREADY iso-rotated (IsoFacetMath.RigToWorld — the
// rig's projection baked into the object transform), so the game's straight-down orthographic
// 2D camera reproduces the rig's exact projection AND z-buffer. That collapses the rig's
// shadeOf() to a plain dot(worldNormal, LN): the key light is fixed in SCREEN space, which is
// what makes the shading read as pixel art rather than as lit 3D. _LN arrives with its z
// NEGATED (IsoFacetMath.ShaderLightVector) because the object matrix is a REFLECTION of the
// rig's right-handed frame — measured in the spike; do not "fix" the sign.
//
// ⚠️ The ONLY pass has LightMode "HHHullFacet", which the 2D renderer's own draw does NOT pick
// up (deliberately: a mesh writing the scene's shared depth buffer punches holes in every later
// sprite that z-tests). IsoFacetHullFeature draws it off-screen into a 4-target MRT with a
// private depth buffer; IsoFacetOverlay re-composes the resolved image in-scene.
//
// Dither is indexed in the HULL-CELL frame derived from world position — NOT SV_Position — so
// it cannot crawl when the hull translates (the 13–16% class ADR 0022 measured for
// screen-pinned dither) and needs no per-render-target phase calibration (the spike's
// _DitherPhase probe becomes unnecessary: world-derived cell coordinates are y-flip-proof).
//
// THE DECK-OCCUPANT SPLIT (owner playtest 2026-08-07: "rider/player sprites visible THROUGH
// closed cabins"). A figure standing on deck is an ordinary sprite drawn ABOVE the hull's
// whole-object sorting slot, so a wheelhouse in front of them can never cover them: sorting is per
// OBJECT and the question is per PIXEL. This pass already holds the answer — it runs against a
// private z-buffer, so every fragment knows its own view depth, and a figure standing on the deck
// has ONE depth, their feet. So a hull carrying an occupant writes a FORE id into the facet alpha
// wherever her geometry is NEARER the camera than that figure, and _HullId everywhere else. It is a
// PARTITION, never a duplication: every solid pixel still carries exactly one id, the overlay
// re-composes them all (so the hull's own picture is unchanged), and the FIGURE's shader discards
// where it reads a fore id — covered exactly where the boat is genuinely in front of them.
//
// ⚠️ N OCCUPANTS, NOT ONE — AND THE ENCODING IS THE WHOLE TRICK. The stern-deck loop puts a
// skipper, a sternman, four pieces of working furniture, two trap stacks and a carried tray on one
// deck at once, all at DIFFERENT depths, and one split plane cannot be right for two of them: hull
// geometry between two occupants is "in front" of the far one and "behind" the near one, and a
// single id cannot say both. So the plane becomes a fixed ARRAY of planes, and the id becomes a
// BAND INDEX.
//
// Per pixel: band = HOW MANY occupants this fragment is in front of. Since the occupants a pixel is
// in front of are always the deepest ones (z < d is a prefix of the depth-sorted list), that COUNT
// is all the information anyone needs, and counting is order-free — no sort on the GPU, no sort on
// the CPU to feed it. Band 0 (behind everybody) writes _HullId exactly as before; band m > 0 writes
// _HullIdFore + (m-1), the hull's fore ids being a CONTIGUOUS block reserved at registration. An
// occupant of rank r (r = how many occupants stand at or deeper than they do) is then hidden
// exactly where band >= r — a plain RANGE over that block, which is what the sprite shader tests.
//
// The nesting is why this is exact rather than approximate: the deeper occupant's fore region
// strictly contains the nearer one's, and consecutive band ids reproduce that containment in a
// single 8-bit channel.
//
// _DeckOccupantCount = 0 — nobody aboard, and every hull most of the time — means the loop never
// runs and the alpha is byte-identical to before this existed. With exactly ONE occupant the band
// can only be 0 or 1, so the pass writes _HullId / _HullIdFore, which is byte-for-byte the
// single-plane split this grew out of.
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters
// in Property display strings; no [unroll] over runtime bounds; force-compiled headless by
// IsoFacetShaderCompileGuardTests so a break fails CI red.
Shader "HiddenHarbours/IsoFacet"
{
    Properties
    {
        [NoScaleOffset] _RampTex ("Palette ramps by material", 2D) = "white" {}
        [NoScaleOffset] _DarkRampTex ("RINDEX darkened ramps by material", 2D) = "white" {}
        _KeyColor ("Keyline colour, pre linearised", Color) = (0.05, 0.08, 0.09, 1)
        _Gain ("Rig GAIN", Float) = 0
        _Bias ("Rig BIAS", Float) = 0
        _PivotPx ("Cell pivot px from top left", Vector) = (0, 0, 0, 0)
        _PixelsPerMetre ("Pixels per metre", Float) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // Both passes below share ONE vertex program, deliberately. The guard pass must land on
        // exactly the pixels the facet pass will land on — including the per-face `db` depth bias
        // — so the two cannot be allowed to drift apart as separately-maintained copies. (The
        // water shader uses this same SubShader-scope include pattern for the same reason.)
        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ⚠️ THE SLOT COUNT, AND IT IS MEASURED, NOT GUESSED. The stern-deck loop's worst beat
            // (deck-loop-kit README, the twelve-beat shift; TrapIso.CAPS) puts TEN things on a
            // lobster boat's deck at once: two hands, four pieces of furniture, two trap stacks, the
            // pot in play and the fish tray. Twelve is that plus two. The cost is the ID BUDGET —
            // each hull reserves one base id plus this many contiguous fore ids out of 255, so
            // twelve leaves 19 simultaneous mesh hulls against a roadmap whose largest scene is a
            // harbour of a dozen. Raising it costs hulls; lowering it costs occupants, loudly (the
            // registry refuses the surplus claim rather than dropping it silently).
            //
            // C# HOLDS THE SAME NUMBER (IsoFacetHullRenderer.DeckOccupantSlots) and the two are
            // asserted equal by DeckOccupantSlotTests — this literal is the one a compiler sees, so
            // it cannot be an expression.
            #define HH_DECK_OCCUPANT_SLOTS 12

            // Palette lookups are integer Loads — never filtered, never mipped.
            Texture2D<float4> _RampTex;
            Texture2D<float4> _DarkRampTex;

            // Set once per hull material (IsoFacetHullRenderer.Configure). Arrays cannot be
            // Properties; they are plain uniforms via SetVectorArray. Not SRP-batcher packed —
            // hulls are few and each is one draw in a private pass.
            float4 _LN;                 // rig LN, z pre-negated for the reflected frame
            float  _Gain, _Bias;
            float4 _Bayer[4];           // BAYER[x&3][y&3], values already (v+0.5)/16, row = x
            float4 _RampMeta[16];       // per material: x = ramp length, y = index offset
            float4 _KeyColor;           // pre-linearised keyline colour
            float4 _PivotPx;            // xy = cell pivot in px from the cell's top-left
            float  _PixelsPerMetre;

            // Per draw via MaterialPropertyBlock (IsoFacetHullRenderer.ApplyPose).
            float4 _HullOrigin;         // xy = world position of the rig origin (unheaved root)
            float  _HullId;             // hull id already divided by 255; the facet alpha
            // THE DECK-OCCUPANT SPLIT (see the header). _HullIdFore is the BASE of this hull's
            // contiguous fore-id block, already divided by 255 — band m writes _HullIdFore + (m-1).
            // _DeckOccupant[k].x is slot k's occupant's view depth in the same world z the fragment
            // carries, and .w is 1 only while that slot is actually claimed and standing.
            // _DeckOccupantCount is how many of them are live: 0 skips the whole thing. All default
            // to 0, so a material nobody wrote to never splits.
            float  _HullIdFore;
            float4 _DeckOccupant[HH_DECK_OCCUPANT_SLOTS];
            float  _DeckOccupantCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // x = matId  y = faceBias b  z = depthBias db  w = INTERIOR side code (0 =
                // exterior both sides — the value every mesh baked before the interior mask
                // existed carries, so an un-rebaked hull behaves exactly as before; 1 = interior
                // both sides; 2/3 = interior on the front/back side only — see vertGuard, which
                // decodes the rendered side).
                float4 attrs      : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // Face-flat by construction (every vertex of a face carries the face's values);
                // nointerpolation keeps them exact across the fan.
                nointerpolation float fidx : TEXCOORD0;
                nointerpolation float mat  : TEXCOORD1;
                float3 wpos : TEXCOORD2;         // xy = dither frame  z = TRUE unbiased depth
            };

            struct FragOut
            {
                float4 facet : SV_Target0;       // rgb = facet colour, a = hull id
                float4 dark  : SV_Target1;       // rgb = RINDEX darkened colour
                float4 key   : SV_Target2;       // rgb = keyline colour
                float  depth : SV_Target3;       // true unbiased view depth (world z, metres)
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1.0)).xyz;
                // The object matrix is orthogonal (rotation times mirror), so inverse-transpose
                // equals the matrix itself — the plain mul is exact, as in the spike.
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normalOS));

                // The rig: sh = shadeOf(n, se, ce). In iso-rotated world space that is exactly a
                // dot with LN — see the header comment.
                float sh = dot(wn, _LN.xyz);
                // "if(sh<0 && f.b<=-1) sh = shadeOf(-n)*0.9" — the rig's interior/backface rescue.
                if (sh < 0 && v.attrs.y <= -1) sh = -sh * 0.9;

                o.fidx = sh * _Gain + _Bias + v.attrs.y;
                o.mat  = v.attrs.x;
                o.wpos = wp;                     // camera looks along +Z; larger z = further
                wp.z  -= v.attrs.z;              // f.db pulls the face toward the camera
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }

            FragOut frag (Varyings i)
            {
                // The hull-cell pixel this fragment lands on, derived from WORLD position: the
                // rig's screen grid is just world metres times PPU with y down and the pivot as
                // origin. Locked to the hull, immune to render-target conventions.
                float2 cellF = float2(
                    (i.wpos.x - _HullOrigin.x) * _PixelsPerMetre + _PivotPx.x,
                    _PivotPx.y - (i.wpos.y - _HullOrigin.y) * _PixelsPerMetre);
                int2 cell = int2(floor(cellF));
                float bay = _Bayer[cell.x & 3][cell.y & 3];

                int m    = (int)round(i.mat);
                int len  = (int)_RampMeta[m].x;
                int off  = (int)_RampMeta[m].y;
                float fbase = floor(i.fidx);
                int idx = (int)fbase + ((i.fidx - fbase) > bay ? 1 : 0) + off;
                idx = clamp(idx, 0, len - 1);

                // WHICH OF THIS HULL'S IDS THIS PIXEL CARRIES. Camera looks along +Z, so a SMALLER
                // depth is nearer: hull geometry in front of a figure standing on the deck takes a
                // FORE id and is composed over them. Strictly less-than, so the very planking under
                // their feet (same depth) stays behind them.
                //
                // The band is a COUNT of the occupants this pixel is in front of (see the header),
                // so the slots need no ordering and the loop is order-free. The whole thing hangs
                // off one uniform that is 0 for every hull with nobody aboard, which is nearly all
                // of them nearly all of the time (rule 7); an unclaimed slot inside the loop costs
                // exactly one compare that fails. Bound is a compile-time constant, so this unrolls
                // without the [unroll] this file's cautions forbid over runtime bounds.
                float hullId = _HullId;
                if (_DeckOccupantCount > 0.5)
                {
                    int band = 0;
                    for (int k = 0; k < HH_DECK_OCCUPANT_SLOTS; k++)
                    {
                        if (_DeckOccupant[k].w > 0.5 && i.wpos.z < _DeckOccupant[k].x) band++;
                    }
                    // Band m takes the m-th id of the reserved block. 1/255 per step because every
                    // id in this channel is already divided by 255.
                    if (band > 0) hullId = _HullIdFore + (band - 1) * (1.0 / 255.0);
                }

                FragOut o;
                o.facet = float4(_RampTex.Load(int3(idx, m, 0)).rgb, hullId);
                o.dark  = float4(_DarkRampTex.Load(int3(idx, m, 0)).rgb, 1.0);
                o.key   = float4(_KeyColor.rgb, 1.0);
                o.depth = i.wpos.z;
                return o;
            }

            // ---- the INTERIOR GUARD (ADR 0023: the per-face interior mask) -------------------
            // Rides the SAME vert() above, so it occupies the same pixels with the same depth.
            // Its one job is to answer, per pixel, "is the nearest hull surface here an open
            // interior?" — resolved by the ordinary z-test into the guard pass's OWN depth
            // buffer, which is why no stencil is needed and why the facet pass is untouched.
            struct GuardVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float interior : TEXCOORD0;
            };

            GuardVaryings vertGuard (Attributes v)
            {
                GuardVaryings o;
                o.positionCS = vert(v).positionCS;

                // attrs.w is the PER-SIDE interior code (RigMeshInteriorClassifier.ClassifySides):
                //   0 = exterior both sides   1 = interior both sides
                //   2 = interior when the camera renders the FRONT (the side the face normal
                //       points toward)        3 = interior when it renders the BACK
                // Which side is being rendered comes from the SAME stored normal the bake labelled
                // the sides with — never from SV_IsFrontFace, whose winding convention would have
                // to survive the object matrix's deliberate reflection (det −1) and the rigs'
                // shared-winding mirror twins. Orthogonal maps preserve dot products, so
                // sign(dot(worldNormal, towardCamera)) here equals sign(dot(normalOS, eyeOS)),
                // the exact quantity the classifier sorted sides by. The third row of the view
                // matrix is the world-space toward-camera axis. A face edge-on to the camera
                // (dot 0) rasterises no pixels, so its tie-break never shows.
                float3 wn = mul((float3x3)unity_ObjectToWorld, v.normalOS);
                bool front = dot(wn, UNITY_MATRIX_V[2].xyz) >= 0.0;
                int code = (int)round(v.attrs.w);
                bool interior = code == 1 || code == (front ? 2 : 3);
                o.interior = interior ? 1.0 : 0.0;
                return o;
            }

            float fragGuard (GuardVaryings i) : SV_Target
            {
                return i.interior;
            }
        ENDHLSL

        Pass
        {
            Name "HHHullFacet"
            // Drawn ONLY by IsoFacetHullFeature's renderer list — see the header comment.
            Tags { "LightMode" = "HHHullFacet" }

            Cull Off            // the rig z-buffers everything; it never backface-culls
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // The interior guard. Recorded by IsoFacetHullFeature ONLY while a displaced sea is live
        // and the mask is enabled; its single-channel output is published as _HHHullGuardTex and
        // the displaced water's fragment discards against it. Same state as the facet pass (so the
        // same fragments survive), ColorMask R because the target is R8.
        Pass
        {
            Name "HHHullGuard"
            Tags { "LightMode" = "HHHullGuard" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertGuard
            #pragma fragment fragGuard
            ENDHLSL
        }
    }
}
