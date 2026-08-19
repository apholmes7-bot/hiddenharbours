#ifndef HIDDEN_HARBOURS_CHARACTER_PALETTE_INCLUDED
#define HIDDEN_HARBOURS_CHARACTER_PALETTE_INCLUDED

// CharacterPalette.hlsl — THE CHARACTER RECOLOUR, as a fragment-stage colour substitution.
//
// ADR 0021 keeps the character rig's JavaScript out of shipped builds, so nothing at runtime can draw
// a person; ADR 0029 answers that by splitting the character's parameter space — STRUCTURE is baked
// into sheets, COLOUR is a ramp swap. This file is the colour half, and it is the only place in the
// project that recolours a character. A second family that wants it (a deck rider, a mirror preview,
// an NPC) INCLUDES this, exactly as SpriteLitDecor.hlsl is attached rather than copied.
//
// ================================================================================================
// WHY A COLOUR-KEYED SUBSTITUTION IS EXACT HERE, AND WOULD NOT BE ON ARBITRARY ART
// ================================================================================================
// Substituting colours in a fragment shader is normally a bad idea: on anti-aliased or shaded art the
// source colours are a continuum, so any match is a guess. The character sheets are not that art, and
// this was measured rather than assumed. Across all 37 baked Fisher_* sheets — 1,034,817 opaque
// pixels — there are exactly 69 distinct colours, and every one of them is either a literal entry of
// one of the rig's ramps or that entry run through the rig's tinted-outline pass
// (keyTint(c) = mix(KEY, c, 0.22)). Alpha is strictly 0 or 255: the sheets carry no anti-aliasing at
// all. So the set of colours that can arrive here is small, known, and authored — and a substitution
// is not an approximation of the art, it is the same arithmetic the rig did against a different ramp.
// CharacterPaletteContentTests re-runs that classification over the shipped PNGs, so a future drop
// that shades off-palette fails loudly rather than leaving a fisher three-quarters recoloured.
//
// ================================================================================================
// WHY A LIST AND NOT A LOOKUP TEXTURE
// ================================================================================================
// The obvious mechanism is a 3D LUT: one texture fetch, no loop. It was measured and rejected. The
// character kit's 403 possible colours (and even the fisher's own 69) still COLLIDE when quantised to
// a LUT grid — 5-bit collides on 7 cells, 6-bit still collides on the near-blacks, and a 6-bit LUT is
// 1 MB per wearer to carry a table with 22 live entries in it. A short list compared exactly is both
// smaller and correct, and it has no quantisation to reason about when the palette changes.
//
// ================================================================================================
// THE COMPARISON IS IN LINEAR SPACE, AND THAT IS NOT INCIDENTAL
// ================================================================================================
// The project renders in Linear colour space (ProjectSettings m_ActiveColorSpace: 1), so an sRGB
// sprite arrives here already linearised by the sampler — the bytes in the PNG are NOT what this
// function sees. The FROM colours are therefore linearised on the C# side by the same standard
// conversion (Color.linear) before they are uploaded, and the comparison happens in that space.
//
// The epsilon is measured, not chosen. In linear space the closest any swappable source colour comes
// to any OTHER colour present in the fisher's sheets is 0.00353. The default below is a squared
// distance of 3.1e-06 — a radius of 0.00176, half that gap — so no colour the wardrobe does not own
// can fall inside a match, and float error (order 1e-7) cannot fall outside one.
// CharacterPaletteContentTests re-derives that gap from the shipped ramps and fails if a widened
// palette closes it.
//
// ================================================================================================
// CONTRACT
// ================================================================================================
// _CharPaletteCount == 0 is a PIXEL-IDENTICAL passthrough — the fisher in the outfit she was baked
// in costs one compare against zero and nothing else, which is what lets this sit unconditionally in
// a shader that must keep looking exactly as it does today. Unused slots are still filled by the
// uploader with a colour no sprite can hold, so a stale array can never match either.
//
// Budget (rule 7): no texture fetch, no dependent read, a bounded loop over a COMPILE-TIME constant
// (never a runtime count — see HiddenHarboursPlayerSubmerge.shader's HLSL-trap note), and an early-out
// at the live count. Twenty-two entries over a ~44x48 px figure is a few hundred thousand compares a
// frame at worst, which is noise beside one sprite's overdraw.

// The most swaps a wearer can carry. MUST equal CharacterPalette.MaxSwaps on the C# side; the shader
// compile guard test asserts the pair rather than trusting the comment.
#define CHARACTER_PALETTE_MAX 24

// Deliberately OUTSIDE UnityPerMaterial: arrays are not SRP-Batcher-compatible, and these arrive by
// MaterialPropertyBlock anyway (which opts a renderer out of batching regardless). The wearer is one
// sprite, so the batch was never the win here — being able to dress two characters differently
// without two materials is.
float4 _CharPaletteFrom[CHARACTER_PALETTE_MAX];
float4 _CharPaletteTo[CHARACTER_PALETTE_MAX];
float  _CharPaletteCount;
float  _CharPaletteEpsSq;

// Replace one baked colour with what the wearer chose. Returns the albedo untouched when the wearer
// has no palette, when the colour is not one the wardrobe owns, or when this shader is drawing
// something that is not a character.
half3 ApplyCharacterPalette(half3 albedo)
{
    int count = (int)_CharPaletteCount;
    if (count <= 0) return albedo;

    float epsSq = (_CharPaletteEpsSq > 0.0) ? _CharPaletteEpsSq : 3.1e-06;

    for (int i = 0; i < CHARACTER_PALETTE_MAX; i++)
    {
        if (i >= count) break;

        float3 d = (float3)albedo - _CharPaletteFrom[i].rgb;
        if (dot(d, d) < epsSq) return (half3)_CharPaletteTo[i].rgb;
    }

    return albedo;
}

#endif // HIDDEN_HARBOURS_CHARACTER_PALETTE_INCLUDED
