/* px cliff face — PALETTE-SHIFT RELIGHT.  The exact path: keeps the pixel look under any sun.
   Hidden Harbours · contract: Art/gameplay/pxCliffFaceRig.gameplay.json (LIGHTING)

   Two samples and one LUT tap. It does not multiply the pixel by a light value — it STEPS THE
   INDEX the texel was cut from and looks the new colour up in the rock's own palette. That is why
   it survives a low sun: every result is a colour a pixel artist put in the ramp.

   TEXTURES
     _Index    <stem>_index.png       RAW.  sRGB OFF · Point · no mips · no compression
     _Normal   <stem>_normal.png      RAW.  sRGB OFF · Point
     _Palette  CliffPx_palette.png    8×32, sRGB ON · Point · Clamp

   _index is OPT-IN in the rig: PxCliffFace.channels(b, {index:true}). Bake it with
   PXCF_BAKE({..., index:true}) before you ship this path.

   SUN
     _SunTangent is the world sun in this face's tangent basis. Build it per wall segment:
       Ts = normalize(along-cliff, horizontal)
       Nw = normalize(N_plan * sin(batter) + worldUp * cos(batter))   // TIPPED BY THE BATTER
       Tt = cross(Nw, Ts)                                            // up the face
       L  = vec3(dot(S, Ts), dot(S, Tt), dot(S, Nw))
     Feed BATTERS[b].key_in_face_frame instead and you reproduce the shipped albedo at noon.

   ASPECT
     _AspectShift = 0 when you build L per segment as above — the incidence is already in L, and
     adding ASPECTS[a].tier_shift on top darkens every E face twice. Set it only if you light the
     whole coast with one shared sun vector. */

uniform sampler2D _Index;
uniform sampler2D _Normal;
uniform sampler2D _Palette;

uniform vec3  _SunTangent;    // normalised sun in face tangent space
uniform float _SunShadow;     // 0..1 from the engine's own shadow map. 0 = fully lit
uniform int   _AspectShift;   // -1 | 0 | +1 — read the ASPECT note above before using it

const float BAND_UP   = 0.74;   // LIGHTING.thresholds, verbatim from the rig
const float BAND_DOWN = 0.40;
const float SHADOW_1  = 0.45;
const float SHADOW_2  = 0.85;
const vec2  LUT_SIZE  = vec2(8.0, 32.0);

vec3 pxCliffRelight(vec2 uv)
{
    vec4  idx  = texture(_Index, uv);
    float row  = floor(idx.r * 255.0 + 0.5);   // 0..24
    float band = floor(idx.g * 255.0 + 0.5);   // 0..4
    bool  rock = idx.b > 0.5;                  // accessory palettes never take a tier step

    vec3  N   = texture(_Normal, uv).xyz * 2.0 - 1.0;
    float ndl = dot(N, _SunTangent);

    /* the detail light: ONE band step, never more. This is the law that keeps it pixel art. */
    if      (ndl > BAND_UP)   band += 1.0;
    else if (ndl < BAND_DOWN) band -= 1.0;

    /* form, incidence and cast shadow move the TIER — and only the rock rows may move.
       Lichen does not go the colour of shadowed rock, so rows 18..24 stay where they are. */
    if (rock) {
        float tier = mod(row, 6.0);
        float base = row - tier;               // 0 sandstone · 6 till · 12 basalt
        tier += float(_AspectShift);
        if      (_SunShadow > SHADOW_2) tier -= 2.0;
        else if (_SunShadow > SHADOW_1) tier -= 1.0;
        row = base + clamp(tier, 0.0, 5.0);
    }

    band = clamp(band, 0.0, 4.0);
    return texture(_Palette, (vec2(band, row) + 0.5) / LUT_SIZE).rgb;
}

/* NIGHT, LAMPLIGHT AND GRADES
   Do not tint the result. A grade over a six-tier ramp crushes the two shadow tiers together and
   the form stops reading. Swap the LUT instead: re-bake CliffPx_palette.png with a night key
   (PxLang.setKey) and the same 25 rows, then cross-fade between two LUT taps across dusk. The
   index maps do not change — only the table they point into. One extra sample, no loss of banding.

   A point light (a lamp on the wharf, a lighthouse sweep) adds a SECOND tier step, not a glow:
     tier += (lampNdl * lampAtten > 0.55) ? 1.0 : 0.0;
   Two lights are two steps. There is no third — the ramp runs out, and that is the ramp's job. */
