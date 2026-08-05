// HiddenHarboursTreeWind.shader — gentle canopy wind-sway for the trees (sibling of HiddenHarboursGrass).
//
// A custom URP 2D UNLIT sprite shader (text HLSL, builds headless). A tree sprite is planted at its TRUNK
// (bottom-centre pivot); this shader keeps the trunk rooted and sways only the CANOPY above _TrunkAnchor, off the
// SAME deterministic wind the grass and water read — GrassWindBridge publishes EnvironmentSample.WindVector into
// the GLOBAL vector _WindWorld (direction * 0..1 strength), so a gust leans the trees, ruffles the grass, and
// ripples the water TOGETHER (one world).
//
// Trees are stiffer and far taller than grass, so vs the grass shader this: (1) anchors the bend ABOVE a trunk
// fraction (smoothstep(_TrunkAnchor,1,uv.y)) instead of from the very base; (2) uses gentler, slower defaults;
// and (3) decorrelates per-tree with a SMOOTH value-noise phase (NOT grass's floor-cell phase) so a 5.5 m sprite
// never shows a hard phase seam down its trunk — the canopy flexes naturally and neighbouring trees differ.
// There is NO footstep interaction (trees don't bend underfoot) and NO loops (so no [unroll] magenta trap).
//
// ⚠️ THE BEND CURVE REQUIRES A TESSELLATED SPRITE. bendW is shaped in the VERTEX stage, so on a sprite imported
// as FullRect (a FOUR-VERTEX quad) it is only ever evaluated at uv.y 0 and uv.y 1 and the rasteriser interpolates
// LINEARLY between them: the squaring collapses, _TrunkAnchor becomes inert for every value, and the whole sprite
// shears from its bottom row instead of keeping a planted trunk. Measured 2026-07-25 on the shipped trees, which
// were all FullRect: worst deviation 0.362 of full sway near mid-canopy. Sprites driven by this shader MUST import
// with Mesh Type Tight (the grass tufts always did). Pinned by Assets/Tests/EditMode/Art/WindBendTessellationTests.
//
// ⚠️ IT ALSO LIGHTS, BUT IT NO LONGER OWNS THE LIGHTING. The fragment stage consumes the rig's baked MASK and
// NORMAL sheets so a tree CATCHES and RIMS with the colour of a light (docs/design/sprite-light-response.md) —
// the sun swinging its catch across the crown through the day, and the boat lamp picking a bankside tree out of
// the dark at night. That block used to live HERE, which meant the only way to light a new sprite family was to
// copy a fragment stage; it now lives in the SHARED Include/SpriteLitDecor.hlsl, which the shoreline plants, the
// shrubs and (next) the cliffs call too. The tree is the REGRESSION PIN for that move: it binds no rim gate, so
// the one added operation is a multiply by exactly 1.0 and its output is bit-identical. Two rules that cost a
// day if found late: the mask order is OURS (R key, G rim, B depth, A coverage — NOT the reference technique's
// green/blue), and every sheet is sampled through the ALBEDO's mesh and uv because the 1 px keyline ring has no
// normal. Both are documented at length in the include headers; read them before touching frag().
//
// SHADER CAUTIONS honoured: NO operator characters in any [Header(...)] label or Property string (ShaderLab parse
// error -> magenta); helpers declared BEFORE use; globals OUTSIDE the per-material CBUFFER, tunables INSIDE it;
// pixel-snapped + point-sampled, PPU 32. Visual-only: drives no sim, saves nothing (rule 5). The Tree material's
// shipped variant is force-compiled headless by Assets/Tests/EditMode/Art/TreeWindShaderCompileGuardTests.cs.
Shader "HiddenHarbours/TreeWind"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Tree sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip threshold", Range(0, 1)) = 0.01

        [Header(Pixelization)]
        _PixelsPerUnit ("Pixels per unit", Float) = 32

        [Header(Trunk anchor (below this the tree stays planted))]
        // uv.y below which there is NO sway (the trunk and lowest boughs stay rooted). 0 = sway from the base
        // like grass, higher = a taller planted trunk. The canopy sway ramps up from here to the crown.
        _TrunkAnchor ("Trunk anchor (uv.y, planted below)", Range(0, 0.8)) = 0.14

        [Header(Canopy wind sway (driven by the shared wind via _WindWorld))]
        _SwayAmount ("Sway amount at full wind (m)", Float) = 0.12
        // A small wind-INDEPENDENT baseline so the canopy always has a hint of life (and the look is sane before
        // the wind sim feeds it). Set to 0 for dead-still trees with no wind.
        _IdleSway ("Idle baseline sway (m)", Float) = 0.02
        // How much a STEADY wind holds the crown leaned over (0 = only gust ripple, 1 = a strong lean).
        _WindLean ("Steady lean from wind (0..1)", Range(0, 1)) = 0.35
        _SwaySpeed ("Gust temporal speed (slow for trees)", Float) = 1.1
        // Spatial frequency (per metre) of the travelling gust wave — it rolls downwind across the stand.
        _GustScale ("Gust travel scale (per m)", Float) = 0.2
        _GustStrength ("Gust ripple strength (0..1)", Range(0, 1)) = 0.6
        // Per-tree decorrelation: the spatial frequency of the SMOOTH phase noise. Small = broad, slow variation
        // (one tree flexes coherently, neighbours differ). No hard cell seams, unlike the grass.
        _PhaseScale ("Phase noise scale (per m)", Float) = 0.12
        // How much the crown DIPS in Y as it leans (foreshorten) so a big lean reads as the canopy folding over.
        _BendY ("Bend foreshorten (0..1)", Range(0, 1)) = 0.2

        // ---- LIGHT RESPONSE (the baked mask and normal sheets; Include/SpriteLightResponse.hlsl) --------
        // SHIPS AT 0 = OFF. Tree.mat's flat look is byte-for-byte what it was before this feature, and the
        // response is one slider away. Everything below is already tuned for _LightResponse 1, so turning it
        // on is a single move, not ten. See the PR that added this for the noon/dusk/night comparison.
        [Header(Light response master (0 is off and the tree draws exactly as before))]
        _LightResponse ("Light response", Range(0, 2)) = 0
        [Header(Baked channels (bound per tree by TreeTrunkAnchor))]
        // R key light, G back rim, B depth, A coverage. OURS, not the reference technique's order.
        [NoScaleOffset] _LightMask ("Light mask sheet", 2D) = "black" {}
        // View space, y UP (the rig writes minus ny into green). No coverage on the 1 px keyline ring.
        [NoScaleOffset] _LightNormal ("Light normal sheet", 2D) = "black" {}
        // The shared path's optional no-rim gate. THE TREE BINDS NEITHER: no tree pixel is forbidden a
        // rim, so the selector stays (0,0,0,0), the dot is 0 and the rim passes through untouched. Both
        // are declared here only because one UnityPerMaterial layout is shared with the plants and the
        // shrubs, which do bind them (SpriteLitDecor.hlsl's header says which channel each rig uses).
        [NoScaleOffset] _LightRimGate ("Rim gate sheet (unused by trees)", 2D) = "black" {}
        [HideInInspector] _LightRimGateChannel ("Rim gate channel selector", Vector) = (0, 0, 0, 0)
        // 1 only when a renderer has bound the LIGHT sheet. The fallback texture is opaque black, which
        // would read as "coverage everywhere" — a flag is honest where sniffing is not. (The normal is
        // optional on the shared path; the tree binds one and every term uses it.)
        _LightChannels ("Baked channels bound", Range(0, 1)) = 0
        // PUBLISHED, not tuned — hidden because there is nothing here for the owner to set. xy is where
        // this tree stands in world space and w is 1 once a renderer has said so. See the rootWS varying:
        // a SpriteRenderer's unity_ObjectToWorld is IDENTITY, so the shader cannot work this out itself.
        [HideInInspector] _SpriteRootWS ("Sprite root world xy (published per renderer)", Vector) = (0, 0, 0, 0)
        // ADR 0027 num 8 — the HHReflect pass's per-renderer MIRROR AXIS and light flag. PUBLISHED by
        // ReflectiveObject, never tuned, and hidden for the same reason _SpriteRootWS is: a
        // SpriteRenderer's unity_ObjectToWorld is IDENTITY, so the shader cannot derive the pivot and
        // the owner has nothing to set here. w of 0 means nobody published one and the mirror REFUSES
        // to run (a reflection pinned to the world origin is worse than no reflection).
        [HideInInspector] _HHReflectOrigin ("Reflection mirror axis (published per renderer)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HHReflectLit ("Reflection is night light content", Range(0, 1)) = 0

        [Header(Response shape)]
        // 0 = brighten where the rig's fixed key already put light. 1 = recompute the catch for the LIVE
        // light, so the crown's far side genuinely goes dark at dawn.
        _KeyRelight ("Key relight (0 baked .. 1 live)", Range(0, 1)) = 0.75
        _RimSteerAmount ("Rim steer toward the light", Range(0, 1)) = 0.8
        // How softly a light crosses from lighting the front to rimming the back. The shipped sun stays in
        // the south all day, so this width is what buys the grazing dawn and dusk rim.
        _LightFrontBand ("Front to back changeover width", Range(0.02, 1)) = 0.6
        _LightDepthBias ("Depth bias (near masses catch more)", Range(0, 1)) = 0.45

        [Header(Sun and moon key (added PRE grade so it dims with the world))]
        _SunKeyColor ("Sun catch colour", Color) = (0.91, 0.69, 0.42, 1)
        _SunKeyStrength ("Sun key strength", Range(0, 2)) = 0.16
        _SunRimStrength ("Sun rim strength", Range(0, 2)) = 0.28

        [Header(Boat lamp (pre compensated so it survives the night grade))]
        _LampKeyStrength ("Lamp key strength", Range(0, 3)) = 0.18
        _LampRimStrength ("Lamp rim strength", Range(0, 3)) = 0.9
        // The lamp sits above the water, the tree stands on the bank: a small positive elevation keeps the
        // beam from reading as a light buried in the ground. Metres are not needed, only the sine.
        _LampElevation ("Lamp elevation (sine)", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The SHARED lit-decor consumer: the sheet declarations, the sun/lamp globals, the per-material
            // rows and the assembled response — which is THIS shader's own former fragment block, lifted out
            // so the shoreline plants, the shrubs and (next) the cliffs light through the same code rather
            // than each copying a fragment stage. It pulls in SpriteLightResponse.hlsl for the maths, whose
            // every function has a line-for-line C# twin in HiddenHarbours.Art.SpriteLightMath.
            #include "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                // Where this sprite STANDS, in world space, for the local-lamp direction and distance. The
                // lamp is taken from the ROOT rather than per fragment because a sprite's fragment world xy
                // conflates height with ground depth — a 5.5 m crown would read as 5.5 m further north.
                //
                // 🔴 unity_ObjectToWorld IS IDENTITY FOR A SpriteRenderer. Unity submits sprite meshes with
                // their vertices ALREADY IN WORLD SPACE, so _m03/_m13 is (0,0) for every tree and reading the
                // object origin there hands all of them the same position. MEASURED 2026-07-29: a 3 m-range
                // lamp parked on tree 1 of a three-tree lineup lit all three equally. That is why the root
                // arrives as a PUBLISHED per-renderer property (TreeTrunkAnchor writes it into the same
                // property block as the anchor), the same lesson the facet renderer learned with _HullOrigin.
                // The fallback when nothing published one is the fragment's own world xy — off by the tree's
                // drawn height, but never off by the whole scene.
                float2 rootWS     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // ⚠️ The light sheets are NOT declared here. SpriteLitDecor.hlsl owns _LightMask, _LightNormal
            // and _LightRimGate (and the sun / day-night / boat-lamp globals) so that every consumer binds
            // the same names — a second declaration here would be a redefinition, and a DIFFERENT name here
            // would be a tree that quietly stopped sharing the path. They are sampled at the ALBEDO's uv
            // through the ALBEDO's mesh; see that file's header for why the keyline ring makes that
            // mandatory rather than merely tidy.

            // GLOBAL shared wind (Shader.SetGlobalVector from GrassWindBridge; NOT per-material, so OUTSIDE the
            // CBUFFER). _WindWorld = wind dir * normalized strength (0..1). Default (0,0,0,0) = no wind.
            // Tree-only: the shared include carries the lighting globals, this one is the wind's.
            float4 _WindWorld;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _PixelsPerUnit;
                float  _TrunkAnchor;
                float  _SwayAmount;
                float  _IdleSway;
                float  _WindLean;
                float  _SwaySpeed;
                float  _GustScale;
                float  _GustStrength;
                float  _PhaseScale;
                float  _BendY;
                // Every lighting row, from the SHARED include — the response master, the shape tunables, the
                // sun and lamp strengths, the rim-gate selector and _SpriteRootWS (xy = where this sprite
                // stands in world space, w = 1 when a renderer published it; it cannot be read from
                // unity_ObjectToWorld, which is IDENTITY for every SpriteRenderer). Pasted as a macro because
                // a CBUFFER must be one contiguous block, and pasted IDENTICALLY into the HHReflect pass
                // below because the SRP Batcher requires one layout across a shader's passes.
                SPRITE_LIT_DECOR_MATERIAL_ROWS
                // ADR 0027 #8 — the reflection pass's per-renderer mirror axis + light flag.
                // Declared HERE too so both passes share ONE UnityPerMaterial layout (the SRP
                // Batcher requires it to be identical across a shader's passes); unused by this pass.
                float4 _HHReflectOrigin;
                float  _HHReflectLit;
            CBUFFER_END

            // ---- helpers (declared BEFORE use) ----------------------------------------------------------------
            // Snap a world offset to the PPU grid so the sway moves in WHOLE pixels (crisp pixel art).
            float2 PixelSnap(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // Smooth value noise (smoothstep interpolation) — NO hard jumps, so a tall tree's phase varies
            // gently across its body (a natural flex) instead of seaming at a cell boundary.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vpos = GetVertexPositionInputs(IN.positionOS);
                float3 wp = vpos.positionWS;

                // Read the stand position BEFORE the wind offset — a tree sways, it does not relocate.
                OUT.rootWS = _SpriteRootWS.w > 0.5 ? _SpriteRootWS.xy : wp.xy;

                // Trunk-anchored canopy weight: 0 below _TrunkAnchor (trunk planted), easing up to 1 at the crown.
                // Squared so the motion accelerates into the upper canopy rather than hinging at the anchor.
                float bendW = smoothstep(_TrunkAnchor, 1.0, saturate(IN.uv.y));
                bendW = bendW * bendW;

                // ---- shared wind ----
                float2 windVec = _WindWorld.xy;
                float  windStr = length(windVec);
                float2 wdir = windStr > 1e-4 ? windVec / windStr : float2(1.0, 0.0);

                // smooth per-tree phase (value noise; no hard seam down a tall sprite)
                float phase = ValueNoise(wp.xy * max(_PhaseScale, 1e-3)) * 6.2831853;

                // travelling gust rolls downwind; two beats slightly out of phase read organic.
                float travel = dot(wp.xy, wdir) * _GustScale;
                float gust = sin(_Time.y * _SwaySpeed - travel + phase) * 0.6
                           + sin(_Time.y * _SwaySpeed * 1.7 - travel * 1.3 + phase) * 0.4;

                // steady lean holds the crown over in a real wind; the gust ripples around it.
                float  swayMag = _IdleSway + windStr * _SwayAmount;
                float  lean    = windStr * _WindLean;
                float2 windOffset = wdir * ((lean + gust * _GustStrength) * swayMag);

                float2 offset = windOffset * bendW;
                float  yDip   = -length(offset) * _BendY;
                offset = PixelSnap(offset);
                wp.xy += offset;
                wp.y  += PixelSnap(float2(0.0, yDip)).y;

                OUT.positionCS = TransformWorldToHClip(wp);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;
                clip(col.a - _AlphaClip);

                // ---- the light response ------------------------------------------------------------------
                // Skipped entirely unless a renderer bound the sheets AND the master is up, so the shipped
                // default (_LightResponse 0) costs two scalar compares and nothing else.
                if (_LightResponse > 1e-4 && _LightChannels > 0.5)
                {
                    // 🔴 THE SHARED PATH. Every line this used to hold now lives in SpriteLitDecor.hlsl and
                    // is run, unchanged, by the shoreline plants and the shrubs as well. The tree binds no
                    // rim gate, so its selector is (0,0,0,0), the gate resolves to exactly 1.0 and the only
                    // difference from the inline version is a multiply by the IEEE 754 multiplicative
                    // identity — the tree's output is BIT-identical, which is the point of doing it this way
                    // rather than leaving the original behind as a special case.
                    SPRITE_LIT_DECOR_PARAMS(litParams)
                    col.rgb += _LightResponse * SpriteLitDecorResponse(IN.uv, IN.rootWS, litParams);
                }

                return col;
            }
            ENDHLSL
        }

        // ================================================================================================
        // HHReflect — this tree's REFLECTION in the water (ADR 0027 #8)
        // ================================================================================================
        // Recorded by IsoFacetHullFeature into _HHReflectTex and sampled by the water shader, which warps
        // the lookup by the SAME wave field the hull rides. This pass draws the CHEAPEST correct thing —
        // a flat mirrored silhouette, no interior mask, no self-occlusion — which is what the ADR's
        // fidelity probe found sufficient at PPU 32 (see the PR).
        //
        // 🔴 The mirror axis is PUBLISHED per renderer (_HHReflectOrigin), never derived from
        // unity_ObjectToWorld, which is IDENTITY for a SpriteRenderer. See Include/ReflectMirror.hlsl.
        //
        // ⚠️ MEMBERSHIP IS THE RENDERING-LAYER BIT, NOT THIS TAG. Every tree in the scene shares this
        // shader; only those carrying ReflectionRegistry.RenderingLayer (i.e. those someone put a
        // ReflectiveObject on) are in the filtered list. Same discipline as the displaced water's layer.
        //
        // The tree deliberately does NOT sway in its reflection: the wind offset is a per-vertex screen
        // displacement that would have to be mirrored too, and at PPU 32 a reflection is a few dozen
        // pixels of broken-up colour under a wave warp. Cheapest correct answer wins.
        Pass
        {
            Name "HHTreeReflect"
            Tags { "LightMode" = "HHReflect" }
            Blend One OneMinusSrcAlpha      // premultiplied: overlapping reflectors composite in ONE target
            Cull Off
            ZWrite Off
            ZTest Always                    // no depth buffer here; the water sorts the result, not this pass

            HLSLPROGRAM
            #pragma vertex reflectVert
            #pragma fragment reflectFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/ReflectMirror.hlsl"
            // Included for SPRITE_LIT_DECOR_MATERIAL_ROWS alone — this pass lights nothing. Taking the rows
            // from the SAME macro as the pass above is what makes the two UnityPerMaterial layouts identical
            // by construction rather than by two lists someone has to keep in step (the SRP Batcher silently
            // drops the shader when they drift). It also supplies _DayNightTint, which this pass DOES read.
            #include "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl"

            struct ReflectAttributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ReflectVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // _DayNightTint — the overlay this pass compensates for — comes from the shared include above,
            // along with the rest of the lighting globals this pass does not read.

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _PixelsPerUnit;
                float  _TrunkAnchor;
                float  _SwayAmount;
                float  _IdleSway;
                float  _WindLean;
                float  _SwaySpeed;
                float  _GustScale;
                float  _GustStrength;
                float  _PhaseScale;
                float  _BendY;
                // The SAME macro the lit pass uses. Unused here — but the layouts must match exactly.
                SPRITE_LIT_DECOR_MATERIAL_ROWS
                // ADR 0027 #8 — this pass's per-renderer mirror axis + light flag, the two rows it
                // actually reads.
                float4 _HHReflectOrigin;
                float  _HHReflectLit;
            CBUFFER_END

            ReflectVaryings reflectVert(ReflectAttributes IN)
            {
                ReflectVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 wp = GetVertexPositionInputs(IN.positionOS).positionWS;
                OUT.positionCS = HHReflectPositionCS(wp, _HHReflectOrigin);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 reflectFrag(ReflectVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                clip(col.a - _AlphaClip);
                return HHReflectPremultiply(col.rgb, col.a, _HHReflectLit, _DayNightTint.rgb);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
