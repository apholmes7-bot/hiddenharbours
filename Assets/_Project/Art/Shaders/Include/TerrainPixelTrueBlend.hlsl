#ifndef HH_TERRAIN_PIXEL_TRUE_BLEND_INCLUDED
#define HH_TERRAIN_PIXEL_TRUE_BLEND_INCLUDED

// TerrainPixelTrueBlend.hlsl — THE GROUND'S BLENDS, DRAWN (terrain blends, after the splat warp).
//
// Where two ground materials meet, the weights TSA_GroundWeights returns are fractions: a splat's bilinear
// ramp across its half-metre texel, a height band's metre-scale gate. Summed by weight they are a soft
// gradient sixteen art texels wide or wider, under sprites whose every edge is one hard pixel. This file
// turns those fractions into ONE material per art texel: the two heaviest materials at the texel, the
// lighter one's share compared with a threshold that belongs to the texel, and the winner drawn whole. The
// edge the splat warp made wander becomes a stepped line with a broken rim instead of a fade.
//
// THE TEXEL, NOT THE PIXEL. The threshold is read from the texel's index on the ground's own grid
// (floor(world * pixels per metre)) and from the meander's noise on the same grid: world space, never the
// screen. The caller reads the weights it hands in at the texel's centre, so every screen pixel inside one
// art texel reaches one answer at any zoom, and the pattern is a picture laid on the ground that the
// camera moves over. It cannot crawl.
//
// THE THRESHOLD is two patterns mixed: an ordered four by four matrix (the regular stipple a pixel artist
// lays on a gradient) and a two-octave clump noise in metres (so a wide band breaks into tongues and
// islands, not a screen of dots). The fringe scales it about one half: fringe 0 is a clean line where the
// two materials weigh the same; fringe 1 spreads the break across the whole blend.
//
// THE LADDER IS NOT TOUCHED. A material's intensity (its step on the kit ladder) is left exactly as the
// weights returned it: a lawn's mower stripes are two high channel values read as two tones, and a
// stippled ladder would speckle every lawn. Only WHICH material a texel shows is decided here.
//
// Include AFTER TerrainSplatAlbedo.hlsl: this reads HH_MAT_COUNT and TSA_VNoise. It reads no uniform and
// no texture; the includer passes its tunables in, so any shader that draws the ground can take the same
// law at its own point without declaring anything for this file.
//
// Visual only: drives no sim, saves nothing (rule 5).

// The ordered matrix, row by row; an entry n stands for the threshold (n + 0.5) / 16.
static const float TPB_ORDERED[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

// A texel whose heaviest material weighs this much or more is that material: nothing to decide.
static const float TPB_PURE = 0.999;

// The clump noise: the coarse octave's share, the fine octave's scale against the coarse one, and the
// gain that spreads value noise (which huddles about one half) across the threshold's range.
static const float TPB_CLUMP_COARSE = 0.6;
static const float TPB_CLUMP_FINE_SCALE = 0.37;
static const float TPB_CLUMP_GAIN = 1.8;

// The threshold never reaches 0 or 1: it stays inside the ordered matrix's own extremes, so a texel one
// material holds is that material whatever the noise says, and the pick agrees with TPB_Blended's gate.
static const float TPB_THRESHOLD_MARGIN = 0.03125;

// Is anything blended here? False where one material already holds the texel.
bool TPB_Blended(float w[HH_MAT_COUNT])
{
    float top = 0.0;
    for (int i = 0; i < HH_MAT_COUNT; i++) top = max(top, w[i]);
    return top < TPB_PURE;
}

// The texel's threshold, inside (0, 1) by TPB_THRESHOLD_MARGIN. texel is floor(world * pixels per metre), wpx the same in metres (the
// noise domain the meander uses), ragged the clump noise's share over the ordered matrix.
float TPB_Threshold(float2 texel, float2 wpx, float ragged, float clumpMetres)
{
    int2 c = int2((int)texel.x & 3, (int)texel.y & 3);
    float ordered = (TPB_ORDERED[c.y * 4 + c.x] + 0.5) / 16.0;
    float scale = max(clumpMetres, 1e-3);
    float clump = TSA_VNoise(wpx / scale, 53.0) * TPB_CLUMP_COARSE
                + TSA_VNoise(wpx / (scale * TPB_CLUMP_FINE_SCALE), 59.0) * (1.0 - TPB_CLUMP_COARSE);
    clump = saturate(0.5 + (clump - 0.5) * TPB_CLUMP_GAIN);
    return clamp(lerp(ordered, clump, saturate(ragged)), TPB_THRESHOLD_MARGIN, 1.0 - TPB_THRESHOLD_MARGIN);
}

// The fringe about one half: 0 is the line of equal weight, 1 the whole blend.
float TPB_Fringe(float threshold, float fringe)
{
    return 0.5 + (threshold - 0.5) * saturate(fringe);
}

// One material per texel. The two heaviest are found (a tie goes to the lower index); the one with the
// lower INDEX is drawn where its share of the pair is over the threshold, the other elsewhere. Asking by
// index, not by weight, is what keeps the pattern whole across the line where the two swap rank. Every
// entry of w is written: the winner 1, the rest 0.
void TPB_Pick(float threshold, inout float w[HH_MAT_COUNT])
{
    float a = -1.0, b = -1.0;
    float ia = 0.0, ib = 0.0;
    for (int i = 0; i < HH_MAT_COUNT; i++)
    {
        float wi = w[i];
        if (wi > a) { b = a; ib = ia; a = wi; ia = (float)i; }
        else if (wi > b) { b = wi; ib = (float)i; }
    }
    float lowIsTop = step(ia, ib);                    // 1 when the heaviest has the lower index
    float low = min(ia, ib), high = max(ia, ib);
    float lowWeight = lerp(b, a, lowIsTop);
    float share = lowWeight / max(a + max(b, 0.0), 1e-4);
    float pick = share > threshold ? low : high;
    for (int k = 0; k < HH_MAT_COUNT; k++) w[k] = abs((float)k - pick) < 0.5 ? 1.0 : 0.0;
}

#endif // HH_TERRAIN_PIXEL_TRUE_BLEND_INCLUDED
