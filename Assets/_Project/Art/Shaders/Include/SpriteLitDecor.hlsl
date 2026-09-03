#ifndef HIDDEN_HARBOURS_SPRITE_LIT_DECOR_INCLUDED
#define HIDDEN_HARBOURS_SPRITE_LIT_DECOR_INCLUDED

// SpriteLitDecor.hlsl — the SHARED CONSUMER of the rig-baked light channels.
//
// SpriteLightResponse.hlsl already held the shared MATHS (key, rim, frontness, lamp shape, day/night
// compensation). What was NOT shared was everything around it: the property declarations, the three
// texture lookups, and the assembly of sun + lamp into one additive term. That assembly lived inside
// HiddenHarboursTreeWind.shader, so the ONLY way to light a new sprite family was to copy a fragment
// stage. This file is that assembly, lifted out unchanged — the tree now calls it, and so does every
// other rig family. A rig that bakes a light channel gets lit by ATTACHING, not by getting its own
// shader.
//
// ⚠️ THE TREE IS THE REGRESSION PIN. SpriteLitDecorResponse is the tree's own fragment block moved
// verbatim; the tree passes the same arguments it always computed inline. The ONE added operation on
// the tree path is a multiply by the rim gate, which is exactly 1.0 when no gate sheet is bound
// (_LightRimGateChannel defaults to (0,0,0,0), so the dot is 0 and the allow term is 1). Multiplication
// by 1.0 is the IEEE 754 multiplicative identity, so the tree's rendered output is BIT-identical, not
// merely close. Pinned by SpriteLitDecorTests + TreeWindShaderCompileGuardTests.
//
// ================================================================================================
// 🔴 WHAT A CONSUMER MUST BRING (and what it may leave out)
// ================================================================================================
// REQUIRED  a light sheet   — R key · G back rim · B depth · A coverage. OUR order, not the reference
//                             technique's (see SpriteLightResponse.hlsl's header). The tree bakes it as
//                             `<species>_mask.png`; the shoreline plants and the shrubs bake the same
//                             four channels as `<key>_light.png`. Same contract, different file name.
// OPTIONAL  a normal sheet  — view-space, y UP. ONLY the tree rig ships one: the shoreline-plant and
//                             shrub rigs consume their normals AT BAKE (the lit spine) and what survives
//                             to runtime is the light channel alone. A consumer that binds none is not
//                             degraded into a special case — SpriteLightKey/SpriteLightRim already
//                             collapse to the BAKED mask wherever a texel has no normal (that is how the
//                             tree's keyline ring is lit), so "no normal sheet anywhere" is just that
//                             same path taken by every texel. This is why the generalization is cheap.
// OPTIONAL  a rim gate      — one channel of a STATE sheet that says "this pixel may not rim", 255 where
//                             forbidden. The rigs disagree about WHERE it lives and that is deliberate:
//                             the shoreline plants put it in `_tide.png` BLUE (strap pixels: blades,
//                             culms, fronds) and the shrubs in `_calendar.png` RED (veil pixels). Both
//                             contracts say the same thing in the same words — "This is the branch. Read
//                             it, do not infer it." So the channel is a SELECTOR the consumer publishes
//                             (_LightRimGateChannel), never a guess made here. Bind nothing and the
//                             selector is (0,0,0,0): no gate, every pixel may rim, which is the tree.
//
// A note on why the gate is belt-and-braces rather than load-bearing: both contracts also state that
// mask G is *already* identically 0 on those pixels. An honest bake therefore needs no gate at all. The
// gate costs one dot and one multiply and makes a DIShonest bake (a re-bake that regresses, a channel
// swapped) fail safe instead of rimming a blade of eelgrass like a tree trunk.
//
// ================================================================================================
// 🔴 SAMPLE EVERYTHING THROUGH THE ALBEDO'S MESH AND UV
// ================================================================================================
// Inherited unchanged from the tree and non-negotiable for every consumer: the ALBEDO's sprite is the
// mesh; the light, normal and gate sheets are TEXTURE LOOKUPS at the albedo's uv, never their own
// sprites. The rig composites a 1 px keyline ring that is opaque in the albedo but has no surface
// normal, so a normal sheet imported Tight has a SMALLER mesh — sampling through it loses the ring and
// flattens the outline. All sheets are baked at identical dimensions with an identical cell layout, so
// one atlas uv addresses the same texel in every one of them.
//
// SHADER CAUTIONS honoured: NO operator characters in any string a consumer puts in [Header(...)] or a
// Property label (ShaderLab parse error -> magenta); helpers declared BEFORE use; NO loops, so no
// [unroll] over a runtime bound; globals OUTSIDE the per-material CBUFFER, tunables INSIDE it.

#include "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl"

// =================================================================================================
// the sheets — declared HERE so every consumer binds the same names
// =================================================================================================
// Bound per RENDERER through a MaterialPropertyBlock (SpriteLightBinder), never per material: every
// species is its own sheet, and ten materials that drift apart is the cost the tree already refused.
TEXTURE2D(_LightMask);
SAMPLER(sampler_LightMask);
TEXTURE2D(_LightNormal);
SAMPLER(sampler_LightNormal);
TEXTURE2D(_LightRimGate);
SAMPLER(sampler_LightRimGate);

// =================================================================================================
// the globals — the SAME inputs the tree always took (one lighting model, more consumers)
// =================================================================================================
// _SunDir.xy is the ground-plane direction TOWARD the sun; _SunElevation is 1 at the zenith, 0 at the
// horizon, negative at night; _DayNightTint is the whole-frame multiply the overlay applies AFTER this
// shader (ADR 0013). All three published by DayNightController.
float4 _SunDir;
float  _SunElevation;
float4 _DayNightTint;

// How firmly a CAST SHADOW reads right now, 0..1 — saturate(sun elevation) folded with the live
// weather (DayNightMath.ShadowStrength, published by the same controller on the same tick). SpriteShadow
// already spends it on its alpha; the sun catch below spends it too, so a tree's lit side and the shadow
// it throws fade together under cloud instead of disagreeing. See SpriteLightSunAmount for why taking
// this number is not the same as reading the weather twice.
float  _ShadowStrength;

// The boat spotlight (BoatSpotlight, ADR 0016) — the one LOCAL light this project publishes as globals,
// so a sprite reads it with no per-light coupling. Params: x intensity, y range, z cos(half angle),
// w cos(inner angle). Params2: x edge softness, y gate threshold, z gate softness, w gate fallback when
// the cycle is not running.
float4 _BoatLightPos;
float4 _BoatLightDir;
float4 _BoatLightColor;
float4 _BoatLightParams;
float4 _BoatLightParams2;

// =================================================================================================
// the per-material rows — a MACRO, because a CBUFFER must be ONE contiguous block
// =================================================================================================
// A consumer pastes this inside its own CBUFFER_START(UnityPerMaterial) alongside whatever else it
// owns (the tree's wind tunables, a cliff's bedding). Sharing the ROWS rather than the whole buffer is
// what lets one lighting model serve shaders that otherwise have nothing in common.
//
// ⚠️ SRP BATCHER: a shader's UnityPerMaterial layout must be IDENTICAL across all of its passes. A
// consumer with a second pass (the tree's HHReflect mirror) must paste this macro there too, even where
// the pass reads none of it.
#define SPRITE_LIT_DECOR_MATERIAL_ROWS \
    float  _LightResponse; \
    float  _LightChannels; \
    float  _KeyRelight; \
    float  _RimSteerAmount; \
    float  _LightFrontBand; \
    float  _LightDepthBias; \
    float4 _SunKeyColor; \
    float  _SunKeyStrength; \
    float  _SunRimStrength; \
    float  _LampKeyStrength; \
    float  _LampRimStrength; \
    float  _LampElevation; \
    float4 _LightRimGateChannel; \
    float4 _SpriteRootWS;

// The tunables, gathered so the response is one call instead of a fifteen-argument one. Filled from the
// CBUFFER by SPRITE_LIT_DECOR_PARAMS below, which is the only place the property NAMES appear.
struct SpriteLitDecorParams
{
    float  keyRelight;
    float  rimSteer;
    float  frontBand;
    float  depthBias;
    float3 sunKeyColor;
    float  sunKeyStrength;
    float  sunRimStrength;
    float  lampKeyStrength;
    float  lampRimStrength;
    float  lampElevation;
    float4 rimGateChannel;
};

#define SPRITE_LIT_DECOR_PARAMS(p) \
    SpriteLitDecorParams p; \
    p.keyRelight     = _KeyRelight; \
    p.rimSteer       = _RimSteerAmount; \
    p.frontBand      = _LightFrontBand; \
    p.depthBias      = _LightDepthBias; \
    p.sunKeyColor    = _SunKeyColor.rgb; \
    p.sunKeyStrength = _SunKeyStrength; \
    p.sunRimStrength = _SunRimStrength; \
    p.lampKeyStrength = _LampKeyStrength; \
    p.lampRimStrength = _LampRimStrength; \
    p.lampElevation  = _LampElevation; \
    p.rimGateChannel = _LightRimGateChannel;

// =================================================================================================
// helpers (declared BEFORE use)
// =================================================================================================

// How much this texel is FORBIDDEN to rim, 0..1, from the consumer's own channel selector. An unbound
// gate publishes a (0,0,0,0) selector, so the dot is 0 whatever the fallback texture holds — which is
// what makes "no gate" cost nothing and read as "every pixel may rim".
//
// ⚠️ The selector is dotted, not indexed: a per-pixel branch on a channel INDEX is the same decision
// made the expensive way, and both shipped gates (plants tide.B, shrubs calendar.R) are a single
// channel, so a dot with a one-hot selector is exact. Twinned by SpriteLightMath.RimGateFlag.
float SpriteLitDecorNoRim(float4 gateTexel, float4 selector)
{
    return saturate(dot(gateTexel, selector));
}

// =================================================================================================
// THE RESPONSE — the tree's own fragment block, lifted verbatim and given a rim gate
// =================================================================================================
//
// Returns the ADDITIVE rgb a consumer adds to its albedo, already scaled by nothing: the caller still
// multiplies by its own _LightResponse master, exactly as the tree always did.
//
// `uv`     the ALBEDO's uv — every sheet is sampled at it (see the header).
// `rootWS` where this sprite STANDS in world space. It CANNOT be derived here: Unity submits
//          SpriteRenderer meshes with vertices already in WORLD space and an IDENTITY object-to-world
//          matrix, so unity_ObjectToWorld reads (0,0) for every sprite in the scene and a 3 m lamp
//          parked on one plant would light the whole shore equally. The renderer publishes it
//          (_SpriteRootWS, w = 1 when real); the caller passes the fragment's own world xy as the
//          fallback so an unpublished sprite is off by its drawn height, never by the whole scene.
float3 SpriteLitDecorResponse(float2 uv, float2 rootWS, SpriteLitDecorParams p)
{
    // 🔴 ALL sampled at the ALBEDO's uv. Same sheet dimensions, same cell layout, so one atlas uv
    // addresses the same texel in every sheet (pinned by SpriteLightResponseTests).
    float4 mask = SAMPLE_TEXTURE2D(_LightMask, sampler_LightMask, uv);
    float4 nTex = SAMPLE_TEXTURE2D(_LightNormal, sampler_LightNormal, uv);
    float3 n = SpriteLightDecodeNormal(nTex);

    // The rig families that bake no normal sheet arrive here with nTex opaque black -> n = 0, and every
    // term below already falls back to the BAKED mask for exactly that case. No branch, no variant.
    float4 gate = SAMPLE_TEXTURE2D(_LightRimGate, sampler_LightRimGate, uv);
    float rimAllow = 1.0 - SpriteLitDecorNoRim(gate, p.rimGateChannel);

    // ---- the sun: PRE grade, so it dims with the world -------------------------------------------
    // Deliberate (ADR 0013): a sunlit sprite MUST go dark when the sun goes down. Gated on elevation so
    // the catch fades out at dusk rather than switching off, AND on the live weather, so a lit side
    // never outlives the shadow under it — both off the one published _ShadowStrength (see
    // SpriteLightSunAmount; clear weather is bit-identical to the elevation-only reading it replaces).
    // At night the MOON reaches these sprites through the day/night tint's own moonlight lift
    // (DayNightController), not as a third term here.
    float sunUp;
    float3 sunL = SpriteLightSunDirection(_SunDir.xy, _SunElevation, sunUp);
    float sunAmount = SpriteLightSunAmount(_SunDir.xy, sunUp, _ShadowStrength);
    float sunKey = SpriteLightKey(mask, n, sunL, p.keyRelight, p.frontBand, p.depthBias);
    float sunRim = SpriteLightRim(mask, n, sunL, p.rimSteer, p.frontBand, p.depthBias) * rimAllow;
    float3 sunAdd = p.sunKeyColor * sunAmount
                  * (sunKey * p.sunKeyStrength + sunRim * p.sunRimStrength);

    // ---- the boat lamp: POST grade compensated, so it SURVIVES the night --------------------------
    // The opposite call, and the whole point of the technique: a lamp rimming a sprite in the dark. An
    // uncompensated add would survive the overlay's multiply at ~3 to 6 percent. Dividing by the tint
    // cancels it — the same pattern the water's moon glitter rides. HDR must stay ON.
    float lampShape = SpriteLightLampShape(
        _BoatLightPos.xy, _BoatLightDir.xy, rootWS,
        _BoatLightParams.y, _BoatLightParams.z, _BoatLightParams.w, _BoatLightParams2.x);
    float lampGate = SpriteLightNightGate(
        _DayNightTint.rgb, _BoatLightParams2.y, _BoatLightParams2.z, _BoatLightParams2.w);
    float lampPower = max(0.0, _BoatLightParams.x) * lampShape * lampGate;

    float3 lampAdd = float3(0.0, 0.0, 0.0);
    if (lampPower > 1e-4)
    {
        float3 lampL = SpriteLightViewDirection(_BoatLightPos.xy - rootWS, p.lampElevation);
        float lampKey = SpriteLightKey(mask, n, lampL, p.keyRelight, p.frontBand, p.depthBias);
        float lampRim = SpriteLightRim(mask, n, lampL, p.rimSteer, p.frontBand, p.depthBias) * rimAllow;
        lampAdd = _BoatLightColor.rgb * lampPower
                * (lampKey * p.lampKeyStrength + lampRim * p.lampRimStrength);
        lampAdd = SpriteLightCompensateForDayNight(lampAdd, _DayNightTint.rgb);
    }

    return sunAdd + lampAdd;
}

// ================================================================================================
// 🔴 THE SILHOUETTE THROUGH THE LEAVES (owner ruling, 2026-08-16)
// ================================================================================================
// "Slight occlusion — you always know where you are, you never lose your character." Foliage stays
// OPAQUE and draws in front where it genuinely is in front; the fisher reads through it as a tinted
// silhouette. SilhouetteMaskFeature stamps her sprite coverage into _HHSilhouetteMaskTex before any
// sprite draws; this is the consumer half, and every foliage family calls it from the END of its
// Universal2D fragment, after its own colour is final.
//
// ⚠️ THIS IS NOT A THIRD LIGHTING PATH, and it must never become one. SpriteLitDecorResponse above is
// the shared LIGHT assembly — one include, two lighting paths (sun and lamp), by design. What follows
// is a COMPOSITING operation on an already-lit colour: it reads no normal, no mask channel, no light
// direction, and runs after the light has been added. It lives in this file only because every foliage
// consumer already includes it, so the read exists in exactly ONE place for the tree, the shrubs and
// the shoreline plants alike. Adding a lighting term here would be the fork; adding this is not.
//
// ⚠️ SORTING, NOT DEPTH, DECIDES WHO IS IN FRONT — and nothing here checks. It does not need to:
// foliage BEHIND the fisher draws before her and is painted over by her own sprite, so the tint it
// received this frame is never seen; foliage in FRONT draws after her and keeps it. That is the entire
// "is it in front?" test, paid for by the sprite queue we were already running. A tree that tints and
// is then overdrawn has cost one Load and nothing else.
//
// ⚠️ THE HHReflect PASS MUST NOT CALL THIS. A tree's reflection in the water is a mirrored silhouette
// of the TREE; stamping the fisher into it would put her in the sea, upside down, wherever she happens
// to stand on the shore.

// Declared at file scope, deliberately OUTSIDE UnityPerMaterial: both are globals written once per
// camera per frame (SetGlobalTexture / SetGlobalVector), never per-material values, and folding them
// into the batched block would break the SRP batcher's layout contract for every decor material.
Texture2D<float4> _HHSilhouetteMaskTex;
float4 _HHSilhouetteParams;   // rgb = tint, a = strength AND gate

/// Blend an already-final foliage colour toward the silhouette tint where the fisher is behind it.
/// positionCS is the fragment's SV_Position.xy — the same integer screen pixel the mask was
/// rasterised at, so the lookup is an exact Load and never a filtered sample. Point-for-point: no
/// half-texel offset can slide the silhouette off the body.
float3 SilhouetteThrough(float3 col, float2 positionCS)
{
    // The gate is the strength, and it is a UNIFORM branch — every fragment in the draw takes the
    // same side, so the GPU pays a scalar compare and nothing else. At 0 (dial off, no live body, no
    // config wired) the texture read never happens, which is what makes "off" free rather than merely
    // invisible. Never write a zero as a cleared BUFFER and leave the strength up: a buffer needs a
    // source and a zero must be a strength.
    float strength = _HHSilhouetteParams.a;
    if (strength <= 1e-4) return col;

    float cover = _HHSilhouetteMaskTex.Load(int3(int2(positionCS), 0)).r;
    return lerp(col, _HHSilhouetteParams.rgb, saturate(cover * strength));
}

#endif // HIDDEN_HARBOURS_SPRITE_LIT_DECOR_INCLUDED
