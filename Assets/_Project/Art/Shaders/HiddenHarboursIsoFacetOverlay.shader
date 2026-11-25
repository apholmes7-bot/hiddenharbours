// HiddenHarboursIsoFacetOverlay.shader — the in-scene face of a mesh hull (ADR 0022 phase 3).
//
// A cell-sized quad drawn by the 2D renderer's ordinary Universal2D pass. It re-composes THIS
// hull's pixels (keyline included) from the feature's resolved screen texture
// (_HHHullScreenTex, alpha = hull id/255) at its own SV_Position — a 1:1 texel fetch that is
// convention-proof, because the offscreen passes and this quad render through the same camera
// projection and viewport transform.
//
// The quad is the thing that SORTS: it sits in the hull's SortingGroup like a sprite would, so
// a crew sprite above the boat covers hull AND keyline, and the water below is covered by both
// — whole-object sorting, exactly as good as the baked-sprite path (ADR 0022 "Unchanged").
//
// The hull-id filter is what keeps two OVERLAPPING mesh hulls honest: each quad re-composes
// only its own hull's pixels, so hull A's image never rides along at hull B's sorting position.
//
// ⚠️ A HULL HAS A BLOCK OF IDS, and this quad composes ALL of them (see the facet shader's
// deck-occupant note). While anybody stands on her deck, the geometry nearer the camera than each of
// them is written with one of her FORE ids — one per occupancy band — so those figures' own shaders
// can discard behind her. It is still HER image and still belongs in HER sorting slot, so nothing
// about what this quad draws changes. Accepting only _HullId here would delete the wheelhouse from
// the boat the moment anyone climbed aboard.
Shader "HiddenHarbours/IsoFacetOverlay"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHHullOverlay"
            Tags { "LightMode" = "Universal2D" }

            Blend One Zero   // the same colour as SrcAlpha OneMinusSrcAlpha at alpha 1; the alpha carries the calm mirror's mesh tag
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Global, bound by IsoFacetHullFeature after the keyline resolve. Before the feature
            // has ever run, IsoFacetHullRegistry binds a 1x1 CLEAR fallback so this discards
            // everywhere instead of sampling the grey unbound placeholder.
            Texture2D<float4> _HHHullScreenTex;
            Texture2D<float4> _HHHullCausticTex;
            float4 _HHHullCausticSettings;
            float _HHHullCausticGain;
            float4 _CausticColor;

            // Per draw via MaterialPropertyBlock: this hull's ids, already divided by 255.
            // _HullIdFore is the BASE of her reserved FORE BLOCK — the deck-occupant split (see the
            // header) — and _HullIdForeSpan is how many ids that block holds. 0 when she is not
            // registered for one, which no id ever is, so the block test never matches.
            float _HullId;
            float _HullIdFore;
            float _HullIdForeSpan;
            float _HHCalmMirrorMeshTag;   // global: 1 while the calm mirror runs (CalmMirror), else unset = 0

            // The figure pixel look (route 1, GameConfig.FigurePixelTarget; IsoFacetFigureTarget): the
            // figures at one texel per art pixel, and the map from a screen pixel to its texel,
            // (offset x, offset y, 1/m, m). Both GLOBAL, never material properties. The map is zero for
            // every camera the look did not run for, and zero returns the pixel untouched.
            Texture2D<float4> _HHFigureScreenTex;
            float4 _HHFigurePixelMap;

            // A non-empty texel takes the place of the resolved pixel BEFORE the id test, so the test
            // keeps it for the overlay whose id it carries and drops it from every other: the
            // art-pixel image sorts exactly where the full-size one did.
            float4 HHFigureOver(float4 c, float2 pos)
            {
                if (_HHFigurePixelMap.w < 0.5) return c;
                int2 t = int2(floor((pos - _HHFigurePixelMap.xy) * _HHFigurePixelMap.z));
                uint w, h;
                _HHFigureScreenTex.GetDimensions(w, h);
                if (any(t < 0) || t.x >= (int)w || t.y >= (int)h) return c;
                float4 f = _HHFigureScreenTex.Load(int3(t, 0));
                return f.a >= 0.5 / 255.0 ? f : c;
            }

            // True when a resolved alpha is one of THIS hull's ids. Ids are small integers over
            // 255, so the comparison is done in id space with a half-step tolerance — 8-bit alpha
            // rounding must not be able to drop a pixel of the boat.
            //
            // The fore ids are a CONTIGUOUS BLOCK (one per occupancy band), so membership in them is
            // one range rather than one compare per band — cheaper than the two-id test it replaces
            // once the span is above two, and identical to it at span 1.
            bool HHIsThisHull(float a)
            {
                if (abs(a - _HullId) * 255.0 < 0.5) return true;
                if (_HullIdFore <= 0.0) return false;
                float d = (a - _HullIdFore) * 255.0;
                return d > -0.5 && d < _HullIdForeSpan - 0.5;
            }

            // ---- A LAMP LIGHTS THE FACES OF HER IT CAN SEE (ADR 0016, "lamps light what they can see") -----
            // The ceiling pool (HiddenHarboursLampPool.shader) multiplies EVERY pixel as ground. A mesh pixel is
            // not ground: it has its own height and its own face. So this overlay, which draws her at her own
            // place in the sort, hands the frame her colour times faceTerm / groundTerm for each pool; the
            // pool's ground multiply then leaves her lit by her face, and anything drawn over her afterwards
            // (the dock in front of a moored dory) is lit as ground, because it never carried the ratio.
            // Done in the pool from the hull pass's texture, a dory behind the St Peters dock showed her lit
            // silhouette on the planks (the owner, 2026-11-25).
            // CPU twins: LightMath.HullHeightPlane / HullPoint / HullViewDirection / HullFaceNormal /
            // HullFaceGain. Globals from LampPoolSystem and IsoFacetHullFeature, outside the CBUFFER.
            #define HH_LAMP_FACE_MAX 32
            Texture2D<float>  _HHHullDepthTex;
            float  _HHHullDepthLive;
            float4 _HHLampHullPlane;     // h = y*P.y + z*P.z + P.w (LightMath.HullHeightPlane); all 0 = no sea frame
            float  _HHLampFaceCount;     // pools this frame whose faces are lit here (0 = none)
            float  _HHLampFaceScale;     // how far up the screen one metre of height draws (IsoGround.HeightScale)
            float4 _HHLampFaceColor[HH_LAMP_FACE_MAX];   // rgb colour, a gain ceiling (the pool's _PoolColor)
            float4 _HHLampFaceLamp[HH_LAMP_FACE_MAX];    // xy foot, z height, w reach (the pool's _PoolLamp)
            float4 _HHLampFaceCone[HH_LAMP_FACE_MAX];    // the pool's _PoolCone
            float4 _HHLampFaceSea[HH_LAMP_FACE_MAX];     // x the fitting's height above the still sea
            float4 _HHLampFaceWall[HH_LAMP_FACE_MAX];    // the pool's _PoolWall

            // The pool's own term, RESTATED LINE FOR LINE from HiddenHarboursLampPool.shader (LampPoolTests
            // holds the two copies equal): the edge, the cone and the wall, given the incidence.
            float HHLampPoolGain(float incidence, float2 toFrag, float d, float reach, float gainCeiling,
                                 float4 coneV, float4 wallV)
            {
                // 2 · THE EDGE: 1 out to reach*(1-soft), smoothly to 0 at reach. LightMath.PoolFalloff.
                float soft  = saturate(coneV.x);
                float inner = reach * (1.0 - soft);
                float edge  = 1.0 - smoothstep(inner, reach, d);
                // 3 · THE CONE, for a lamp that is aimed (the searchlight sweeping a dock). LightMath.PoolCone.
                // ⚠️ A RADIAL lamp (cosHalf <= -1) is a BRANCH to 1: run through the feather below, cosHalf -1
                // dimmed every direction within ~45 deg of straight away from the axis, and the axis is up the
                // screen, so every radial lamp cut a dark wedge out of the ground directly SOUTH of it.
                float cosHalf = coneV.y;
                float2 axis   = coneV.zw;
                float  aligned = d > 1e-4 ? dot(toFrag / d, axis) : 1.0;
                float coneBand = max(1.0 - cosHalf, 1e-3) * soft;
                float cone = cosHalf <= -0.9999 ? 1.0 : smoothstep(cosHalf, cosHalf + coneBand, aligned);
                // 4 · THE WALL, for a lamp on one: light falls OUT and DOWN, so nothing behind the wall line is
                // lit. LightMath.PoolWallGate.
                float wall = 1.0;
                if (dot(wallV.xy, wallV.xy) > 1e-6)
                {
                    float ahead = dot(toFrag, normalize(wallV.xy)) + wallV.z;
                    float ws = max(wallV.w, 1e-3);
                    wall = smoothstep(-ws, ws, ahead);
                }
                float gain = gainCeiling * incidence * edge * cone * wall;
                return gain <= 1e-4 ? 0.0 : gain;
            }

            // One neighbour tap: in the target, on the same mesh (its id within half a step), and its depth.
            bool HullTap(int2 q, int2 size, float id, out float z)
            {
                z = 0.0;
                if (any(q < 0) || any(q >= size)) return false;
                if (abs(_HHHullScreenTex.Load(int3(q, 0)).a - id) * 255.0 >= 0.5) return false;
                z = _HHHullDepthTex.Load(int3(q, 0));
                return true;
            }

            float3 HullPoint(float2 world, float z)   // LightMath.HullPoint
            {
                float h = _HHLampHullPlane.y * world.y + _HHLampHullPlane.z * z + _HHLampHullPlane.w;
                return float3(world.x, world.y - h * _HHLampFaceScale, h);
            }

            // On one axis: the step to the neighbour with the smaller depth jump, so an edge between two
            // faces does not bend the normal. False when neither neighbour is on this mesh.
            bool HullStep(int2 p, int2 axisPx, float2 axisWorld, int2 size, float id, float2 world, float z,
                          out float3 step)
            {
                float zp, zm;
                bool hp = HullTap(p + axisPx, size, id, zp);
                bool hm = HullTap(p - axisPx, size, id, zm);
                float3 c = HullPoint(world, z);
                step = float3(0, 0, 0);
                if (!hp && !hm) return false;
                bool usePlus = hp && (!hm || abs(zp - z) <= abs(z - zm));
                step = usePlus ? HullPoint(world + axisWorld, zp) - c : c - HullPoint(world - axisWorld, zm);
                return true;
            }

            // What this pixel of her must carry so that the ceiling's GROUND multiply leaves her lit by her
            // FACE: the product over the pools of (1 + colour·faceGain) / (1 + colour·groundGain). Exactly 1
            // where no pool reaches her or her face cannot be read, so she is then lit as ground, as before.
            float3 HHLampFaceRatio(float2 world, float2 stepX, float2 stepY, int2 p, float id)
            {
                float3 ratio = float3(1, 1, 1);
                if (_HHLampFaceCount < 0.5 || _HHHullDepthLive < 0.5 || _HHLampHullPlane.z == 0.0) return ratio;
                uint tw, th;
                _HHHullDepthTex.GetDimensions(tw, th);
                int2 size = int2(tw, th);
                if (any(p < 0) || any(p >= size) || id < 0.5 / 255.0) return ratio;

                float z = _HHHullDepthTex.Load(int3(p, 0));
                float3 sx, sy;
                bool okX = HullStep(p, int2(1, 0), stepX, size, id, world, z, sx);
                bool okY = HullStep(p, int2(0, 1), stepY, size, id, world, z, sy);
                if (!(okX && okY)) return ratio;
                float3 view = float3(0.0, -_HHLampHullPlane.z * _HHLampFaceScale, _HHLampHullPlane.z);
                float3 n = cross(sx, sy);
                float nl = length(n);
                n = nl > 1e-8 ? n / nl : float3(0, 0, 1);
                if (dot(n, view) > 0.0) n = -n;
                float3 pt = HullPoint(world, z);

                int count = (int)min(_HHLampFaceCount, (float)HH_LAMP_FACE_MAX);
                for (int k = 0; k < HH_LAMP_FACE_MAX; k++)
                {
                    if (k >= count) break;
                    float4 L = _HHLampFaceLamp[k];
                    float2 lamp = L.xy;
                    float  h = L.z;
                    float  reach = max(L.w, 1e-4);
                    if (h <= 0.0) continue;
                    // The pool's quad spans its foot +- reach: outside it the pool never drew, so neither term.
                    float2 toG = world - lamp;
                    if (max(abs(toG.x), abs(toG.y)) > reach) continue;
                    float dG = length(toG);
                    float g = HHLampPoolGain(h * rsqrt(h * h + dG * dG), toG, dG, reach,
                                             _HHLampFaceColor[k].a, _HHLampFaceCone[k], _HHLampFaceWall[k]);
                    float3 toLamp = float3(lamp - pt.xy, _HHLampFaceSea[k].x - pt.z);
                    float dl = length(toLamp);
                    float incF = dl > 1e-5 ? saturate(dot(n, toLamp) / dl) : 1.0;
                    float2 toF = pt.xy - lamp;
                    float dF = length(toF);
                    float f = HHLampPoolGain(incF, toF, dF, reach,
                                             _HHLampFaceColor[k].a, _HHLampFaceCone[k], _HHLampFaceWall[k]);
                    float3 col = _HHLampFaceColor[k].rgb;
                    ratio *= (1.0 + col * f) / (1.0 + col * g);
                }
                return ratio;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;   // the ground point under the pixel (ortho, looking down +Z)
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                o.worldXY = wp.xy;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // The world step of one pixel across and one down, before any branch (a derivative inside
                // divergent flow is undefined). The quad is flat, so these are exact.
                float2 stepX = ddx(i.worldXY);
                float2 stepY = ddy(i.worldXY);
                int2 p = int2(i.positionCS.xy);
                float4 hull = _HHHullScreenTex.Load(int3(p, 0));
                float hullIdAtPixel = hull.a;
                if (_HHHullCausticSettings.z > 0.5 && HHIsThisHull(hull.a))
                {
                    float encoded = _HHHullCausticTex.Load(int3(p, 0)).a;
                    if (encoded >= 0.5 && encoded < 1.0)
                        hull.rgb += _CausticColor.rgb * ((1.0 - encoded) * 2.0 * _HHHullCausticGain);
                }
                float4 c = HHFigureOver(hull, i.positionCS.xy);
                // Only THIS hull's pixels — either of her ids.
                clip(HHIsThisHull(c.a) ? 1.0 : -1.0);
                clip(c.a - 0.5 / 255.0);
                // Her faces, lit by the lamps that reach her (see HHLampFaceRatio). 1 by day and with no pool.
                c.rgb *= HHLampFaceRatio(i.worldXY, stepX, stepY, p, hullIdAtPixel);
                // While the calm mirror runs, a mesh pixel carries alpha 0.875 so the mirror leaves it to the mesh mirror pass.
                return float4(c.rgb, _HHCalmMirrorMeshTag > 0.5 ? 0.875 : 1.0);
            }
            ENDHLSL
        }

        // ================================================================================================
        // HHReflect — this hull's REFLECTION in the water (ADR 0027 #8)
        // ================================================================================================
        // Recorded by IsoFacetHullFeature into _HHReflectTex and sampled by the water shader, which warps
        // the lookup by the SAME wave field the hull rides.
        //
        // ⚠️ THE REFLECTION IS THE OVERLAY'S JOB, NOT THE FACET PASS'S. A mesh hull's visible image is not
        // what the facet MRT holds — it is what the KEYLINE RESOLVE made of it, which this quad re-composes
        // from _HHHullScreenTex. Mirroring the facet pass would reflect raw facet colour with no keyline,
        // no darkening and no hull-id filter: recognisably the wrong boat. So the reflection mirrors THIS
        // quad and re-composes from the SAME resolved texture.
        //
        // ⚠️ …which means the quad rasterises MIRRORED but must SAMPLE UNMIRRORED. _HHHullScreenTex holds
        // the hull where the hull actually is; there is nothing at the reflection's screen position. The
        // mirror is a vertical flip about the pivot's screen row, so the fetch flips back about that same
        // row: sampleY = 2*pivotRow - fragmentY. The pivot's row is computed ONCE in the vertex stage
        // through ComputeScreenPos (which carries the render target's Y-flip convention) and is constant
        // across the quad under this project's unrotated orthographic camera.
        //
        // ⚠️ MEMBERSHIP IS THE RENDERING-LAYER BIT, NOT THIS TAG — every mesh hull shares this shader.
        // Only hulls carrying ReflectionRegistry.RenderingLayer (i.e. those a ReflectiveObject was put on)
        // are in the filtered list.
        Pass
        {
            Name "HHHullReflect"
            Tags { "LightMode" = "HHReflect" }

            Blend One OneMinusSrcAlpha   // premultiplied: overlapping reflectors composite in ONE target
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex reflectVert
            #pragma fragment reflectFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/ReflectMirror.hlsl"

            Texture2D<float4> _HHHullScreenTex;

            float  _HullId;
            float  _HullIdFore;        // her fore BLOCK base (the deck-occupant split) — see the header
            float  _HullIdForeSpan;    // how many ids that block holds
            float4 _HHReflectOrigin;   // xy = the published ground-contact pivot, w = 1 when published
            float  _HHReflectLit;      // 1 = night light content (rides the post-grade bucket, §11.6)
            float4 _DayNightTint;      // GLOBAL (DayNightController) — the overlay this pass compensates for

            // The figure pixel look's texture and map, restated for this pass (see the in-scene pass).
            Texture2D<float4> _HHFigureScreenTex;
            float4 _HHFigurePixelMap;

            // The in-scene pass's figure substitution, restated in this pass's own HLSL block. Same
            // rule; zero map, untouched pixel.
            float4 HHFigureOverReflect(float4 c, float2 pos)
            {
                if (_HHFigurePixelMap.w < 0.5) return c;
                int2 t = int2(floor((pos - _HHFigurePixelMap.xy) * _HHFigurePixelMap.z));
                uint w, h;
                _HHFigureScreenTex.GetDimensions(w, h);
                if (any(t < 0) || t.x >= (int)w || t.y >= (int)h) return c;
                float4 f = _HHFigureScreenTex.Load(int3(t, 0));
                return f.a >= 0.5 / 255.0 ? f : c;
            }

            // The in-scene pass's membership test, restated in this pass's own HLSL block (the two
            // passes do not share one). Same rule, same tolerance.
            bool HHIsThisHullReflect(float a)
            {
                if (abs(a - _HullId) * 255.0 < 0.5) return true;
                if (_HullIdFore <= 0.0) return false;
                float d = (a - _HullIdFore) * 255.0;
                return d > -0.5 && d < _HullIdForeSpan - 0.5;
            }

            struct ReflectAttributes { float4 positionOS : POSITION; };

            struct ReflectVaryings
            {
                float4 positionCS : SV_POSITION;
                // The screen ROW of the mirror axis, in the same pixel space as SV_POSITION. One scalar,
                // constant across the quad — the fetch flips back about it.
                float  pivotRow   : TEXCOORD0;
            };

            ReflectVaryings reflectVert (ReflectAttributes v)
            {
                ReflectVaryings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = HHReflectPositionCS(wp, _HHReflectOrigin);

                // The axis, projected. ComputeScreenPos carries the render-target Y flip, so this stays
                // the same row on every graphics API.
                float4 axisCS = TransformWorldToHClip(float3(_HHReflectOrigin.xy, wp.z));
                float4 axisSP = ComputeScreenPos(axisCS);
                o.pivotRow = (axisSP.y / max(axisSP.w, 1e-6)) * _ScreenParams.y;
                return o;
            }

            float4 reflectFrag (ReflectVaryings i) : SV_Target
            {
                // Flip the fetch back about the axis row: this fragment is drawing the reflection of the
                // hull pixel that sits as far ABOVE the waterline as this one is below it.
                int2 px = int2(i.positionCS.x, 2.0 * i.pivotRow - i.positionCS.y);
                if (any(px < 0) || any(px >= int2(_ScreenParams.xy))) discard;

                float4 c = HHFigureOverReflect(_HHHullScreenTex.Load(int3(px, 0)), float2(px) + 0.5);
                // Only THIS hull's pixels — the same either-id filter the in-scene pass uses, for
                // the same reason: two overlapping hulls must not reflect each other's image, and a
                // boat must not lose her wheelhouse out of the water the moment anyone boards her.
                clip(HHIsThisHullReflect(c.a) ? 1.0 : -1.0);
                clip(c.a - 0.5 / 255.0);

                // The resolved texture stores the hull OPAQUE (the in-scene pass returns alpha 1), so the
                // reflection's coverage is 1 wherever the id matched.
                return HHReflectPremultiply(c.rgb, 1.0, _HHReflectLit, _DayNightTint.rgb);
            }
            ENDHLSL
        }
    }
}
