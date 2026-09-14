/* px cliff face — CONTINUOUS RELIGHT.  The cheap live path: three samples, no LUT, no index bake.
   Hidden Harbours · contract: Art/gameplay/pxCliffFaceRig.gameplay.json (LIGHTING)

   Use this when you want a traversing sun today and can live with the banding softening. It is the
   v10 photoreal shader with ONE addition that matters: the light term is QUANTISED before it
   touches the pixel. Multiply a pixel-art albedo by a smooth N·L and you get mud — the six tiers
   the rig cut become a gradient and the form stops reading at a low sun.

   If the banding matters, use relight_palette.glsl instead. It is exact and costs less.

   TEXTURES
     _Unlit    <stem>_unlit.png   sRGB ON  · Point   — base colour, no directional light in it
     _Normal   <stem>_normal.png  sRGB OFF · Point   — RGB tangent normal, A = cavity AO
     _Mask     <stem>_mask.png    sRGB OFF · Point   — R baked N·L × cast · G sky occ · B height · A cover

   SUN / ASPECT: identical to relight_palette.glsl — see its header. Build _SunTangent per wall
   segment with Nw tipped back by the batter, and leave the aspect tier_shift out of it. */

uniform sampler2D _Unlit;
uniform sampler2D _Normal;
uniform sampler2D _Mask;

uniform vec3  _SunTangent;      // world sun in face tangent space
uniform vec3  _BakedKey;        // BATTERS[b].key_in_face_frame — the key the mask was baked at
uniform vec3  _SunColour;
uniform vec3  _SkyColour;
uniform vec3  _BounceColour;
uniform float _CastStrength;    // 0..1 — how much of the baked cast shadow to borrow
uniform float _Steps;           // light quantisation. 4.0 matches the rig's tier count. Do not raise it.

vec3 pxCliffRelightContinuous(vec2 uv)
{
    vec3  base = texture(_Unlit, uv).rgb;
    vec4  nn   = texture(_Normal, uv);
    vec3  N    = nn.xyz * 2.0 - 1.0;
    float ao   = nn.w;                          // cavity
    vec4  mk   = texture(_Mask, uv);

    float ndl = max(dot(N, _SunTangent), 0.0);

    /* Borrow the baked cast shadow. mask.R is (baked N·L) × cast, so divide the N·L back out to
       recover cast alone. Where the baked N·L is near zero this is noise — the clamp keeps it
       bounded but the texel is a guess. A live sun over displaced PROFILE geometry is the honest
       answer; see _confirm.cast_shadow_reuse in the sidecar. */
    float bakedNdl = max(dot(N, _BakedKey), 0.02);
    float cast     = mix(1.0, clamp(mk.r / bakedNdl, 0.0, 1.0), _CastStrength);

    float sky    = mk.g;                                            // sky dome occlusion, as baked
    float bounce = clamp(-N.y * 0.85 + 0.15, 0.0, 1.0) * (0.35 + ao * 0.5);

    /* THE ADDITION. Quantise the direct term on the way in, so the wall still steps.
       Ideally quantise on the rig's 8×6 form cell (0.50 × 0.375 m) rather than per texel, or the
       tier boundaries fizz — LIGHTING.quantisation says so, and it is untested in engine. */
    float direct = floor(ndl * cast * _Steps + 0.5) / _Steps;

    return base * (sky * _SkyColour + bounce * _BounceColour + direct * _SunColour);
}

/* WHAT THIS PATH CANNOT DO
   - Brow and toe decals. Their darks are cast shadow and occlusion, not N·L. Leave them alone.
   - The iso ledge tiles. Fixed-sun pixel art, 8 steps on a 32 px face — palette-swap, never this.
   - Night. Luminance in the lit channels sits inside 16–244 so a grade has headroom, but a night
     set is a re-bake with a different key, not a grade. */
