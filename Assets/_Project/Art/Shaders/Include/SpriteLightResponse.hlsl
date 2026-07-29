#ifndef HIDDEN_HARBOURS_SPRITE_LIGHT_RESPONSE_INCLUDED
#define HIDDEN_HARBOURS_SPRITE_LIGHT_RESPONSE_INCLUDED

// SpriteLightResponse.hlsl — the SHARED light response for rig-baked sprites.
//
// A flat sprite that carries the rig's baked lighting channels can CATCH and RIM with the colour of a
// light (docs/design/sprite-light-response.md). Tree.mat is the FIRST adopter; the shoreline plants and
// the rocks consume this same contract next, which is why none of it is tree-specific — a consumer
// supplies the sheets, the light, and the strengths, and gets back an additive RGB term.
//
// ⚠️ EVERY function here has a LINE-FOR-LINE C# TWIN in HiddenHarbours.Art.SpriteLightMath, tested
// headless (Assets/Tests/EditMode/Art/SpriteLightResponseTests.cs) with measured sabotages. That
// discipline is what the design doc §4.2 finding 2 demands "from the first commit" — the answer to the
// reference author's own honest caveat about duplicated maths across shaders. If you change a curve
// here, change it there in the same commit or the twin tests fail with the drift measured.
//
// ================================================================================================
// 🔴 THE MASK CHANNEL ORDER IS OURS, NOT THE REFERENCE TECHNIQUE'S
// ================================================================================================
// Every public description of this technique says "green = front, blue = rim". Our rigs bake:
//
//        R = KEY LIGHT   (the rig's max(0, N.K)^1.35 against its own fixed key)
//        G = BACK RIM    (the silhouette fringe, already gated on local mass thickness)
//        B = DEPTH       (view-axis depth, normalised per sprite — 1 = NEAR the camera, 0 = FAR)
//        A = COVERAGE    (the albedo's alpha, the 1 px keyline ring INCLUDED)
//
// A ported snippet swaps two channels and looks SUBTLY wrong rather than broken. The order is pinned
// at the bake (TreeRigBakeTests.MaskChannels_AreKeyRimDepthCoverage_NotTheReferenceTechniquesOrder)
// and again HERE, at the point of consumption, by the accessor macros below and their twin tests.
#define SPRITE_LIGHT_MASK_KEY(m)      ((m).r)
#define SPRITE_LIGHT_MASK_RIM(m)      ((m).g)
#define SPRITE_LIGHT_MASK_DEPTH(m)    ((m).b)
#define SPRITE_LIGHT_MASK_COVERAGE(m) ((m).a)
//
// ================================================================================================
// 🔴 SAMPLE ALL THREE SHEETS THROUGH THE ALBEDO'S MESH AND UV
// ================================================================================================
// The rig composites a 1 px keyline ring OUTSIDE the volume. Those pixels are opaque in the albedo
// (and so in the mask's A) but have NO surface normal to write: measured 611 px of Red Spruce's 7601
// (TreeRigBakeTests.ChannelCoverage_AlbedoAndMaskAgree_ButTheNormalOmitsTheKeyline). Under a Tight
// import the normal sheet's own MESH is smaller too, so giving the normal its own sprite loses the
// ring and the outline goes flat.
//
// Therefore: the ALBEDO's sprite is the mesh; the mask and the normal are TEXTURE LOOKUPS at the
// albedo's uv (secondary-texture style), never their own sprites. The sheets are baked at identical
// dimensions with an identical cell layout, so one atlas uv addresses the same texel in all three —
// pinned by SpriteLightResponseTests.TheThreeSheets_ShareOneUvSpace_SoOneAtlasUvHitsAllThree. And
// where the normal has no coverage, every term falls back to the BAKED MASK (see SpriteLightKey):
// light the keyline from the MASK, never the normal.
//
// ⚠️ UV SPACE (multi-cell): the kit sheets are multi-cell, so raw atlas uv is NOT a per-sprite 0..1
// fraction. Nothing in this file needs one — the light terms are per-TEXEL lookups, which is exactly
// what raw atlas uv is for. Anything added here that needs a per-sprite fraction must derive it from
// the sprite's rect, the way the wind bend's own test does.
//
// ⚠️ sRGB: the mask and normal sheets import with sRGB OFF and NO lossy compression (asserted by
// TreeSheetImportTests). They are data. Never "fix" an import setting to make a channel look right in
// the shader — if a channel reads wrong, the bug is the sampling or the channel order.
//
// SHADER CAUTIONS honoured (the magenta class, which CI cannot catch — no GPU): NO operator
// characters in any string a consumer will put in [Header(...)] or a Property label, helpers declared
// BEFORE use, NO loops (so no [unroll] over a runtime bound).

// ---- the bake camera (ADR 0006/0022) -------------------------------------------------------------
// ¾ from the south at 40°, orthographic — the same camera the tree, boat, rock and shoreline rigs all
// render at, which is why ONE light-direction transform serves every one of them. cos/sin are the kit
// README's own published numbers: "Height x0.766, ground depth x0.643".
#define SPRITE_LIGHT_HEIGHT_SCALE       0.76604444   // cos 40 — a metre of HEIGHT draws this tall
#define SPRITE_LIGHT_GROUND_DEPTH_SCALE 0.64278761   // sin 40 — a NORTHWARD metre draws this far up-screen

// ---- the rig's own fixed key ---------------------------------------------------------------------
// treeIsoRig.js: LIGHT.key = nrm([-0.55, -0.66, 0.52]) in the RIG's frame, where ny is screen-DOWN.
// normalView() encodes -ny into green, so a decoded normal is y-UP and the key's y flips sign with it:
// upper-LEFT, slightly toward the camera (art bible §1). Pre-normalised (|(-0.55, 0.66, 0.52)| = 1.0042).
#define SPRITE_LIGHT_RIG_KEY  float3(-0.54768, 0.65722, 0.51780)

// The exponent the rig raises its Lambert to before writing mask R. SpriteLightKeyTerm is that SAME
// function evaluated for a different light — which is why feeding it SPRITE_LIGHT_RIG_KEY must
// reproduce the baked R channel, and why the twin tests can use the bake itself as the oracle.
#define SPRITE_LIGHT_KEY_EXPONENT 1.35

// =================================================================================================
// helpers (declared BEFORE use)
// =================================================================================================

// Decode one NORMAL-sheet texel to a unit VIEW-space normal (+x right, +y UP, +z toward the camera).
// Returns 0 where the texel has no coverage — the keyline ring — so callers test the length instead of
// consuming a normalize(-1,-1,-1) that points nowhere in particular but points somewhere.
float3 SpriteLightDecodeNormal(float4 texel)
{
    float3 n = texel.rgb * 2.0 - 1.0;
    float len = length(n);
    return (texel.a > 0.003 && len > 1e-5) ? n / len : float3(0.0, 0.0, 0.0);
}

// A light's VIEW-space direction from its GROUND-PLANE direction and its elevation — the one place the
// bake camera's geometry is applied, so the sun and every local lamp agree about what "in front" means.
//   groundDir  unit ground direction TOWARD the light (+x east, +y north) — what _SunDir publishes.
//   elevation  its height above the horizon as a SINE: 1 zenith, 0 horizon, negative below — _SunElevation.
// Under the 40° camera looking north, world +y (north) maps to view (0, sin40, -cos40) and world +z (up)
// to (0, cos40, sin40). So a light in the NORTH has a NEGATIVE view z: it is BEHIND the sprite. That is
// the entire basis of SpriteLightFrontness.
//
// ⚠️ THE 0.766/0.643 DO NOT APPLY TO WORLD POSITIONS. This project's world xy IS the ground plane — a 2D
// game drawn ¾, where a northward metre is a full metre of world y. Those two factors describe the RIG
// CAMERA's projection, and the only reason they appear here is that the baked NORMALS live in that
// camera's space, so a light has to be carried into it before it can be dotted against them. Distances,
// ranges and lamp positions stay in plain world units (see SpriteLightLampShape).
float3 SpriteLightViewDirection(float2 groundDir, float elevation)
{
    float sa = clamp(elevation, -1.0, 1.0);            // sin(altitude)
    float ca = sqrt(max(0.0, 1.0 - sa * sa));          // cos(altitude)

    float gl = length(groundDir);
    float2 g = gl > 1e-5 ? groundDir / gl : float2(0.0, 0.0);
    float3 w = float3(g.x * ca, g.y * ca, sa);         // world direction toward the light

    float3 v = float3(
        w.x,
        w.y * SPRITE_LIGHT_GROUND_DEPTH_SCALE + w.z * SPRITE_LIGHT_HEIGHT_SCALE,
       -w.y * SPRITE_LIGHT_HEIGHT_SCALE       + w.z * SPRITE_LIGHT_GROUND_DEPTH_SCALE);
    float vl = length(v);
    return vl > 1e-5 ? v / vl : float3(0.0, 0.0, 1.0);
}

// THE SUN, with the cycle-off fallback the additive lights already use. DayNightController publishes
// _SunDir/_SunElevation at RUNTIME only, so in EDIT MODE (or a bare art scene, or before the controller
// installs) they are unset — and a response that reads as dead in the scene view is a response the owner
// cannot tune. When nothing has published a direction, fall back to a plausible mid-morning sun.
// `sunAmount` is 0..1: it takes the term to nothing once the sun is under the horizon, which is correct
// because the SUN's catch is added PRE-grade and must go with the daylight.
float3 SpriteLightSunDirection(float2 sunDirGround, float sunElevation, out float sunAmount)
{
    bool unset = dot(sunDirGround, sunDirGround) < 1e-6;
    float2 g = unset ? float2(-0.45, -0.89) : sunDirGround;    // south and a little west
    float  e = unset ? 0.72 : sunElevation;
    sunAmount = saturate(e);
    return SpriteLightViewDirection(g, e);
}

// The KEY term for a live light: the RIG'S OWN Lambert function, evaluated for a different direction.
// Pass SPRITE_LIGHT_RIG_KEY and this reproduces the baked mask R.
float SpriteLightKeyTerm(float3 n, float3 l)
{
    return pow(max(0.0, dot(n, l)), SPRITE_LIGHT_KEY_EXPONENT);
}

// FRONTNESS: how much a light is IN FRONT of the sprite (between it and the camera) rather than BEHIND
// it, 0..1, from the light's view-space z. The reference technique makes this decision with a
// screen-space lookup against each light's own green-over-blue gradient sprite (design doc §4.1); we
// compute it directly because SpriteLightViewDirection already knows the camera.
//
// ⚠️ The shipped SUN never goes fully behind — DayNightMath.SunDirection keeps it in the south (toward
// the camera) all day, so its view z is positive from dawn to dusk: MEASURED 0.215 at the horizon to
// 0.724 at noon. `band` is therefore not a smoothing detail, it is the LOOK decision that buys the
// grazing dawn/dusk rim. Near 0 the sun is pure key forever. At the shipped 0.6 a horizon sun reads 68%
// front / 32% rim while a noon sun still saturates to pure key — golden hour rims, midday does not.
// Mirrored by SpriteLightMath.DefaultFrontBand, which Tree.mat is asserted against.
float SpriteLightFrontness(float lightViewZ, float band)
{
    float b = max(band, 1e-4);
    return saturate((lightViewZ + b) / (2.0 * b));
}

// RIM STEER: how much a rim pixel faces the light LATERALLY (in the screen plane), 0..1. A rim lights
// the side of the silhouette facing the light, and on a silhouette the normal lies in the screen plane
// — the same quantity the rig's own rim term uses (back = max(0, n.xy_hat . RS)). Depth is deliberately
// ignored; the front/behind half of the decision is SpriteLightFrontness's job. Returns 1 when either
// lateral direction is degenerate (a normal facing straight at the camera, a light pointing straight
// away from it) so the baked band passes through unsteered rather than snapping black. An OVERHEAD
// light is NOT one of those: the 40 degree camera projects world up onto screen up, so it has a real
// lateral direction and rims the TOP of the silhouette.
float SpriteLightRimSteer(float3 n, float3 l)
{
    float2 nl = n.xy;
    float2 ll = l.xy;
    float na = length(nl), la = length(ll);
    if (na < 1e-4 || la < 1e-4) return 1.0;
    return saturate(dot(nl / na, ll / la));
}

// DEPTH attenuation from mask B (1 = NEAR, 0 = FAR): at bias 0 the channel is unused; at 1 the far side
// of the canopy catches nothing, so a light wraps the form instead of flooding it flat.
float SpriteLightDepthAtten(float maskDepth, float bias)
{
    return lerp(1.0, saturate(maskDepth), saturate(bias));
}

// =================================================================================================
// the two assembled terms — what a consumer's fragment stage adds
// =================================================================================================

// The KEY response at one texel for one light, before colour and strength.
//
// `relight` blends the BAKED catch (0) toward the LIVE recompute (1): at 0 the sprite brightens where
// the rig's fixed key already put light; at 1 the catch swings with the real light and the crown's far
// side goes dark at dawn.
//
// 🔴 Where the texel has NO normal it collapses to the baked mask regardless. That is the keyline ring
// — coverage but no surface — and it is the whole of "light the keyline from the MASK, never the
// normal" in one line. Mask R is 0 on the ring (the rig writes the masks only inside the volume), so
// the keyline stays the deliberate dark outline it was drawn as.
float SpriteLightKey(float4 mask, float3 n, float3 l, float relight, float frontBand, float depthBias)
{
    float hasN = (dot(n, n) > 1e-8) ? 1.0 : 0.0;
    float live = hasN * SpriteLightKeyTerm(n, l);
    float catchTerm = lerp(saturate(SPRITE_LIGHT_MASK_KEY(mask)), live, saturate(relight) * hasN);

    return catchTerm
         * SpriteLightFrontness(l.z, frontBand)
         * SpriteLightDepthAtten(SPRITE_LIGHT_MASK_DEPTH(mask), depthBias)
         * saturate(SPRITE_LIGHT_MASK_COVERAGE(mask));
}

// The BACK-RIM response at one texel for one light: the baked rim band, gated on the light being
// BEHIND, steered to the side of the silhouette that faces it.
//
// ⚠️ MEASURED LIMITATION, stated rather than hidden: mask G is ALREADY directional — the rig bakes it
// against a fixed back light at nrm([0.48, -0.28, -0.83]) (behind and upper-RIGHT), so pixels the rig's
// rim never touched are 0 and no amount of live steering can rim them. A light behind and to the LEFT
// therefore DIMS the rim rather than moving it to the left edge. Fixing that needs an undirected rim
// band out of the rig — an art-director change, flagged not made.
float SpriteLightRim(float4 mask, float3 n, float3 l, float steer, float frontBand, float depthBias)
{
    float hasN = (dot(n, n) > 1e-8) ? 1.0 : 0.0;
    float lateral = lerp(1.0, SpriteLightRimSteer(n, l), hasN);
    float steered = lerp(1.0, lateral, saturate(steer));

    return saturate(SPRITE_LIGHT_MASK_RIM(mask))
         * steered
         * (1.0 - SpriteLightFrontness(l.z, frontBand))    // BEHIND, not in front
         * SpriteLightDepthAtten(SPRITE_LIGHT_MASK_DEPTH(mask), depthBias)
         * saturate(SPRITE_LIGHT_MASK_COVERAGE(mask));
}

// =================================================================================================
// the LOCAL lamp's shape — a transcription of LightMath's already-twinned cone
// =================================================================================================
//
// The boat spotlight is the one local light this project publishes as shader GLOBALS (ADR 0016), so it
// is the one a sprite can read without any per-light coupling. Its shape and its night gate are ALREADY
// pure C# in HiddenHarbours.Art.LightMath (RadialFalloff / ConeFalloffCos / WaterConeTerm / NightGate),
// tested there; these are line-for-line transcriptions of those, NOT a second definition — the twin
// tests call LightMath itself, so any drift here shows up as a numeric disagreement, not a rewrite.

float SpriteLightSmoothStep01(float a, float b, float x)
{
    if (b <= a) return x >= b ? 1.0 : 0.0;            // degenerate band -> a hard step
    float t = saturate((x - a) / (b - a));
    return t * t * (3.0 - 2.0 * t);
}

// LightMath.WaterConeTerm: the lamp's 0..1 intensity at a point, before colour, intensity and the gate.
// dist/range radial falloff shaped by edgeSoftness, times a COSINE cone (a single dot, no per-pixel
// trig — the publisher sends both cosines). 0 beyond the range or outside the cone.
float SpriteLightLampShape(float2 lampXY, float2 beamDir, float2 pointXY,
                           float range, float cosHalf, float cosInner, float edgeSoftness)
{
    float r = max(range, 1e-4);
    float2 to = pointXY - lampXY;
    float dist = length(to);
    if (dist >= r) return 0.0;                        // beyond the throw -> dark

    float nd = dist / r;
    float radial = pow(saturate(1.0 - nd), lerp(2.0, 0.6, saturate(edgeSoftness)));

    float invD = dist > 1e-5 ? 1.0 / dist : 0.0;
    float2 nDir = to * invD;
    float bl = length(beamDir);
    float2 bDir = bl > 1e-5 ? beamDir / bl : float2(0.0, 1.0);
    float cosAngle = dist > 1e-5 ? dot(nDir, bDir) : 1.0;   // 1 = on the beam axis
    float cone = SpriteLightSmoothStep01(cosHalf, max(cosInner, cosHalf + 1e-4), cosAngle);

    return saturate(radial * cone);
}

// LightMath.NightGate / NightGateWithFallback: how much an additive light shows right now, 0 (full
// daylight, so it cannot wash the scene out) .. 1 (deep dark). A near-black/unset tint means the cycle
// is not running at all (EditMode, a bare art scene) -> show the light, so tuning is possible.
float SpriteLightNightGate(float3 tint, float threshold, float softness, float fallbackWhenNoCycle)
{
    if (tint.r + tint.g + tint.b <= 1e-3) return saturate(fallbackWhenNoCycle);
    float lum = max(0.0, 0.299 * tint.r + 0.587 * tint.g + 0.114 * tint.b);
    float darkness = saturate(1.0 - lum);
    float lo = saturate(threshold);
    float hi = saturate(threshold + max(softness, 1e-4));
    return SpriteLightSmoothStep01(lo, hi, darkness);
}

// =================================================================================================
// day/night integration (ADR 0013) — decide per term, deliberately
// =================================================================================================
//
// The day/night overlay MULTIPLIES the whole composited frame by _DayNightTint AFTER every world
// sprite draws; at the shipped deepest night that is ~(0.022, 0.029, 0.061). So:
//
//   * A term added PRE-GRADE dims with the world. Correct for the SUN's catch: a sunlit tree must go
//     dark when the sun goes down. Add it and do nothing else.
//
//   * A term that must SURVIVE the night — a lamp rimming a tree in the dark, which is the entire
//     point of the technique — has to ride the pre-compensation the water's moon glitter already uses:
//     divide by max(_DayNightTint.rgb, DN floor) so the overlay's multiply cancels. That is what this
//     helper is. HDR DEPENDENCY: the compensated values are far above 1 and must survive the
//     framebuffer to reach the overlay (UniversalRP.asset m_SupportsHDR: 1).
//
// Mirrors LightMath.CompensateForDayNightTint / DayNightCompensationMinChannel exactly — including the
// "cycle off/unset (near-black tint) means there is no overlay to compensate for" branch, which is what
// keeps a bare art scene and EditMode from blowing out.
#define SPRITE_LIGHT_DN_COMP_MIN_CHANNEL 0.02

float3 SpriteLightCompensateForDayNight(float3 additive, float3 tint)
{
    float sum = tint.r + tint.g + tint.b;
    if (sum <= 1e-3) return additive;                    // cycle off/unset -> nothing to compensate
    return additive / max(tint, SPRITE_LIGHT_DN_COMP_MIN_CHANNEL);
}

#endif // HIDDEN_HARBOURS_SPRITE_LIGHT_RESPONSE_INCLUDED
