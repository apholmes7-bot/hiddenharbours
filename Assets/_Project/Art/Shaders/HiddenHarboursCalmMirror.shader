// THE CALM MIRROR (the owner, 2026-11-23: calm water shows a TRUE reflection, at the prototype's strength).
// One full-screen pass after the 2D world and the day/night multiply, recorded by CalmMirror.cs only while
// GameConfig.WaterMirrorStrength is above 0. At 0 it is never recorded, so it costs nothing.
//
// Each pixel's CLASS rides the colour target's alpha (CalmMirror.cs sets it up):
//   a < 0.25        water: the water shader and its overlay blend the alpha toward 0
//   0.25 .. 0.75    ground: the camera clears to 0.5; ground cover, shadows and light pools leave it alone
//   > 0.75          standing: ordinary sprites blend it toward 1 (wharf faces, piles, sheds, cliffs)
//   0.80 .. 0.95    a mesh: the hull/figure overlay writes 0.875 while the mirror runs
//
// Two layers, laid on WATER pixels only:
//   1. THE TRUE LAYER: the mesh mirror pass's picture (_HHReflectTex) at this pixel. Hulls, their fittings,
//      the crew aboard and figures ashore are redrawn there mirrored about the water plane, so the water
//      shows them as seen from below. The water's own object reflection hands this share over
//      (_HHCalmMirrorHandover), so nothing reflects twice.
//   2. THE FLIP, for sprites: walk up the screen to the first standing pixel (the base, where it meets the
//      water) and show the pixel as far above the base as this one is below it. Vertical faces (wharf faces,
//      piles, cliffs) match a true mirror this way. Meshes are never flipped: a base or a sample that is a
//      mesh pixel is skipped, which also keeps a wharf deck behind a moored hull out of the water under her.
//   3. DECKS (WaterMirrorSettings.DeckAware): every standable deck's DRAWN top arrives as convex quads with the
//      height on screen of the face under its front edge (freeboard x HeightScale). A deck top faces the sky, so
//      it is never mirrored: under a deck the flip is replaced by a true mirror about the deck's waterline (the
//      art's own, when it sits within FaceSnap of the data's), only as tall as the face, art where the art draws
//      a face and a face band where it draws none at all (a float drawn as a top only). Away
//      from a deck's face, anything that stands up through a deck (a pile, a post) stops reflecting where the
//      deck begins.
// Foam is drawn by the water itself (water class), so neither layer ever picks it up.
Shader "Hidden/HiddenHarbours/CalmMirror"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "CalmMirror"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CalmMirrorParams;   // x strength (dial x sea fade), y ripple metres, z break share, w time s
            float4 _CalmMirrorParams2;  // x max search px, y search step px, z darken, w debug (1 classes, 2 true layer)
            float4 _CalmMirrorRect;     // xy world min, zw world size
            float4 _CalmMirrorTexel;    // xy 1/size, z art px per metre, w roughness 0..1
            float4 _CalmMirrorReach;    // x thin start px, y gone px, z min stand px, w ripple band (art px)
            float4 _DayNightTint;
            // THE AXIS (owner ruling 2026-11-26, "no mixels"): the flip mirrors about the base pixel's lower EDGE,
            // so screen row y below the base shows row y + 2 * basePx - 1 and both screen rows of one art pixel
            // read one art pixel. About the base pixel's CENTRE (the old 2 * basePx) the two rows of an art pixel
            // came from two different art pixels at every even scale: a half-art-pixel mixel under every face.
            // A global left unset reads 0, the edge axis; 1 is the old centre axis, kept only as the A/B.
            float _HHCalmMirrorCentreAxis;
            TEXTURE2D(_HHReflectTex);

            // Decks (CalmMirror.MaxTops must match).
            #define HH_MAX_TOPS 24
            float4 _CalmMirrorTopA[HH_MAX_TOPS];   // corners 0, 1 of a deck top as drawn (world xy)
            float4 _CalmMirrorTopB[HH_MAX_TOPS];   // corners 2, 3
            float4 _CalmMirrorTopH[HH_MAX_TOPS];   // x: the face under the front edge, height on screen (world units); y fascia rows, z 1 = has an underside
            float4 _CalmMirrorTops;                // x count, y snap (world), z feather (world), w face band share
            float4 _CalmMirrorBand;                // rgb: the face band colour, before the day/night tint
            // Undersides (UndersideDef): one sheet, each quad's cell in texels (x, y, w, h); _CalmMirrorUnder.x the share laid.
            float4 _CalmMirrorTopU[HH_MAX_TOPS];
            float4 _CalmMirrorUnder;
            TEXTURE2D(_CalmMirrorUnderTex);
            // UndersideMath.Texel: columns wrap across the cell, the fascia rows are laid once, the rows above repeat.
            int2 UnderTexel(int i, float artX, float rowFromFar)
            {
                float4 cell = _CalmMirrorTopU[i];
                float f = _CalmMirrorTopH[i].y, x = artX - floor(artX / max(cell.z, 1.0)) * max(cell.z, 1.0);
                float r = max(rowFromFar, 0.0), p = max(cell.w - f, 1.0);
                if (r >= f) r = f + (r - f) - floor((r - f) / p) * p;
                return int2((int)(cell.x + x), (int)(cell.y + r));
            }

            // The span [lo, hi] of deck quad i down the column at world x; false where the column misses it.
            bool TopColumn(int i, float x, out float lo, out float hi)
            {
                float2 p[4] = { _CalmMirrorTopA[i].xy, _CalmMirrorTopA[i].zw, _CalmMirrorTopB[i].xy, _CalmMirrorTopB[i].zw };
                lo = 1e9; hi = -1e9;
                [unroll] for (int e = 0; e < 4; e++)
                {
                    float2 a = p[e], b = p[(e + 1) & 3];
                    // Padded by one art pixel (_CalmMirrorBand.w, world units): the drawn art snaps to the art
                    // grid, so its last column can stand just outside the data's outline; that column then took
                    // the plain flip and mirrored the whole structure as a 1-pixel seam down the water (the Nine
                    // Mile Creek ramp's right edge).
                    float mn = min(a.x, b.x) - _CalmMirrorBand.w, mx = max(a.x, b.x) + _CalmMirrorBand.w;
                    if (x < mn || x > mx) continue;
                    float ya = a.y, yb = b.y;
                    if (mx - mn > 1e-5) { ya = lerp(a.y, b.y, saturate((x - a.x) / (b.x - a.x))); yb = ya; }
                    lo = min(lo, min(ya, yb)); hi = max(hi, max(ya, yb));
                }
                return hi >= lo;
            }

            float4 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0); }
            float WaterShare(float a) { return saturate(1.0 - a * 4.0); }
            float StandShare(float a) { return saturate((a - 0.75) * 8.0 + 0.5); }
            bool IsStanding(float a) { return a > 0.75; }
            bool IsMesh(float a) { return a > 0.80 && a < 0.95; }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 c = Tap(uv);
                float dbg = _CalmMirrorParams2.w;
                if (dbg > 0.5 && dbg < 1.5)
                {
                    float3 k = WaterShare(c.a) > 0.5 ? float3(1, 0, 0) : (IsMesh(c.a) ? float3(1, 0, 1)
                             : (StandShare(c.a) > 0.5 ? float3(0, 0, 1) : float3(0, 1, 0)));
                    return float4(lerp(c.rgb, k, 0.6), c.a);
                }
                if (dbg > 2.5)
                {
                    // Looks only: deck tops as the mirror sees them (cyan) and each one's data waterline (yellow).
                    float2 dw = _CalmMirrorRect.xy + uv * _CalmMirrorRect.zw;
                    float dpx = _CalmMirrorRect.w * _CalmMirrorTexel.y;
                    float3 k3 = c.rgb;
                    [loop] for (int di = 0; di < (int)_CalmMirrorTops.x; di++)
                    {
                        float dlo, dhi;
                        if (!TopColumn(di, dw.x, dlo, dhi)) continue;
                        if (dw.y >= dlo && dw.y <= dhi) k3 = lerp(k3, float3(0, 1, 1), 0.45);
                        if (abs(dw.y - (dlo - _CalmMirrorTopH[di].x)) <= dpx) k3 = float3(1, 1, 0);
                    }
                    return float4(k3, c.a);
                }
                float strength = _CalmMirrorParams.x;
                float water = WaterShare(c.a);
                if (strength <= 0.001 || water <= 0.0) return c;

                // The ripple, locked to the world on the art pixel grid and driven by the game clock. Rows of
                // RippleBandPixels art pixels move together and every shift is whole art pixels, so tall
                // striped faces (cliffs) break into steady bands instead of zig-zags.
                float ppm = max(_CalmMirrorTexel.z, 1.0);
                float2 world = _CalmMirrorRect.xy + uv * _CalmMirrorRect.zw;
                float2 pw = floor(world * ppm);                       // art pixel
                float band = floor(pw.y / max(_CalmMirrorReach.w, 1.0));
                float t = _CalmMirrorParams.w;
                float rough = _CalmMirrorTexel.w;
                // Both shifts are a function of the band and the clock only, never of x: a shift that changed
                // along a row tore tall striped faces into vertical streaks (the cliff fault).
                float wob = sin(band * 1.7 + t * 1.3 + VNoise(float2(band * 0.37, 0.0)) * 6.0) * 0.6
                          + (VNoise(float2(band * 0.5 - t * 0.4, 3.0)) - 0.5) * 0.8;
                float2 pxPerArt = 1.0 / (_CalmMirrorRect.zw * ppm * _CalmMirrorTexel.xy);   // screen px per art px
                float dxArt = round(wob * _CalmMirrorParams.y * ppm);
                float dyArt = round((VNoise(float2(17.0, band * 0.8 + t * 0.7)) - 0.5)
                                    * _CalmMirrorParams.y * ppm * (0.5 + 2.0 * rough));
                float crest = VNoise(float2(pw.x / (ppm * 1.1) + t * 0.15, pw.y * (2.6 / ppm) + t * 0.35));
                float brk = smoothstep(1.0 - _CalmMirrorParams.z * 0.6, 1.0, crest) * saturate(_CalmMirrorParams.z * 1.5);

                // 1) THE TRUE LAYER: the mesh mirror pass at this pixel, shifted by the same ripple.
                uint tw0, th0;
                _HHReflectTex.GetDimensions(tw0, th0);   // the frame's size, or half it (HullReflectionResolutionDivisor 2)
                float2 toTex = float2(tw0, th0) * _CalmMirrorTexel.xy;
                int2 tp = int2((input.positionCS.xy + float2(round(dxArt * pxPerArt.x), 0.0)) * toTex);
                float4 tr = (tp.x >= 0 && tp.x < (int)tw0) ? LOAD_TEXTURE2D(_HHReflectTex, tp) : 0.0;
                float tcov = saturate(tr.a);
                float tintSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                float3 tint = tintSum > 1e-3 ? _DayNightTint.rgb : float3(1, 1, 1);
                float3 trueCol = (tcov > 1e-3 ? min(tr.rgb, tcov.xxx) / tcov : 0.0) * tint * _CalmMirrorParams2.z;
                float3 trueLit = max(tr.rgb - tcov.xxx, 0.0) * tint * _CalmMirrorParams2.z;   // a night-lit source
                float tw = saturate(strength * water * tcov);
                float tl = saturate(strength * water);
                if (dbg > 1.5 && dbg < 2.5) return float4(lerp(c.rgb, float3(1, 0, 1), tcov), c.a);

                // 2) THE FLIP: up the screen to the first standing pixel, past water and ground.
                float texY = _CalmMirrorTexel.y;
                float maxPx = _CalmMirrorParams2.x, stepPx = max(_CalmMirrorParams2.y, 1.0);
                float basePx = -1.0;
                [loop] for (float d = stepPx; d <= maxPx; d += stepPx)
                {
                    float2 q = float2(uv.x, uv.y + d * texY);
                    if (q.y >= 1.0) break;
                    if (IsStanding(Tap(q).a))
                    {
                        basePx = d;
                        [loop] for (float r = 1.0; r < stepPx; r += 1.0)   // refine to the exact first pixel
                            if (IsStanding(Tap(float2(uv.x, uv.y + (d - stepPx + r) * texY)).a)) { basePx = d - stepPx + r; break; }
                        break;
                    }
                }
                float3 o = c.rgb;
                float mPerPx = _CalmMirrorRect.w * texY;   // world units per screen pixel, up the screen
                if (basePx >= 0.0)
                {
                    float baseA = Tap(float2(uv.x, uv.y + basePx * texY)).a;
                    // A base must stand MinStandPixels art pixels tall: thin rings, splashes and lines on the
                    // water are not mirrored, and neither is anything above them.
                    bool tall = true;
                    [loop] for (float k = 1.0; k <= _CalmMirrorReach.z; k += 1.0)
                        if (!IsStanding(Tap(float2(uv.x, uv.y + (basePx + k * pxPerArt.y) * texY)).a)) { tall = false; break; }
                    float axisEdge = 1.0 - saturate(_HHCalmMirrorCentreAxis);   // 1 = mirror about the base's lower edge
                    float2 s = float2(uv.x + dxArt * pxPerArt.x * _CalmMirrorTexel.x,
                                      uv.y + (2.0 * basePx - axisEdge + dyArt * pxPerArt.y) * texY);
                    if (tall && !IsMesh(baseA))
                    {
                        // 3) DECKS: which deck's face is this base (the nearest front edge above it), and does the
                        //    plain flip's sample reach into or past any deck top above the base?
                        float baseY = world.y + (basePx - 0.5 * axisEdge) * mPerPx;   // the base's lower edge
                        float flipY = _CalmMirrorRect.y + s.y * _CalmMirrorRect.w;
                        float snap = _CalmMirrorTops.y;
                        int capI = -1; float capLo = 0.0, capWl = 0.0;
                        bool pastTop = false;
                        [loop] for (int ti = 0; ti < (int)_CalmMirrorTops.x; ti++)
                        {
                            float lo, hi;
                            if (!TopColumn(ti, world.x, lo, hi)) continue;
                            float wl = lo - _CalmMirrorTopH[ti].x;
                            // A base below the waterline inside the face's own band is a hanger cut by the water (a
                            // tyre string, a ladder): mirror the face about the real waterline, never about its tip.
                            bool hung = world.y < wl - snap && baseY >= wl - _CalmMirrorTopH[ti].x;
                            if ((baseY >= wl - snap || hung) && baseY <= hi && (capI < 0 || lo < capLo)) { capI = ti; capLo = lo; capWl = wl; }
                            if (lo <= flipY && hi >= baseY) pastTop = true;
                        }
                        float reachD = basePx;
                        if (capI >= 0)
                        {
                            // A TRUE mirror about the deck's waterline, the face only: the top is never shown.
                            float mirrorY = abs(baseY - capWl) <= snap ? baseY : capWl;
                            float below = mirrorY - world.y;      // how far below the waterline this pixel lies
                            float faceTop = capLo - mirrorY;      // the face's height above that line
                            if (below >= 0.0 && below < faceTop)
                            {
                                float2 fs = float2(s.x, (mirrorY + below - _CalmMirrorRect.y) / _CalmMirrorRect.w + dyArt * pxPerArt.y * texY);
                                float4 m = (fs.y < 1.0 && fs.x > 0.0 && fs.x < 1.0) ? Tap(fs) : float4(0, 0, 0, 0);
                                bool art = IsStanding(m.a) && !IsMesh(m.a);
                                // The band stands in for a face only where the art draws none: the first thing
                                // above the water is the deck's own front edge. Where the art draws its face (a
                                // pier on piles) the gaps in it stay open water, as they are in the art.
                                bool topOnly = baseY >= capLo - snap;
                                float3 col = art ? m.rgb : _CalmMirrorBand.rgb * tint;
                                float share = art ? StandShare(m.a) : (topOnly ? _CalmMirrorTops.w : 0.0);
                                if (!art && _CalmMirrorTopH[capI].z > 0.5 && _CalmMirrorUnder.x > 0.0)
                                {
                                    // The underside seen from below, on the world's art grid: its far rows the edge beam.
                                    int2 ut = UnderTexel(capI, floor((_CalmMirrorRect.x + fs.x * _CalmMirrorRect.z) * ppm), floor((faceTop - below) * ppm));
                                    col = LOAD_TEXTURE2D(_CalmMirrorUnderTex, ut).rgb * tint;
                                    share = _CalmMirrorUnder.x;
                                }
                                float feather = saturate((faceTop - below) / max(_CalmMirrorTops.z, 1e-4));
                                float reach = 1.0 - smoothstep(_CalmMirrorReach.x, max(_CalmMirrorReach.y, _CalmMirrorReach.x + 1.0), below / mPerPx);
                                float w = saturate(strength * water * share * (1.0 - brk) * reach * feather);
                                o = lerp(c.rgb, col * _CalmMirrorParams2.z, w);
                            }
                        }
                        else if (!pastTop && s.y < 1.0 && s.x > 0.0 && s.x < 1.0)
                        {
                            float4 m = Tap(s);
                            float stand = IsMesh(m.a) ? 0.0 : StandShare(m.a);
                            float reach = 1.0 - smoothstep(_CalmMirrorReach.x, max(_CalmMirrorReach.y, _CalmMirrorReach.x + 1.0), reachD);
                            float w = saturate(strength * water * stand * (1.0 - brk) * reach);
                            o = lerp(c.rgb, m.rgb * _CalmMirrorParams2.z, w);
                        }
                    }
                }
                o = lerp(o, trueCol, tw) + trueLit * tl;
                return float4(o, c.a);
            }
            ENDHLSL
        }
    }
}
