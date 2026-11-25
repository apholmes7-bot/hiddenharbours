// HiddenHarboursAdditiveLight.shader â€” the reusable ADDITIVE 2D LIGHT (ADR 0016): a soft glow with a
// CONE/RADIAL shape that brightens (cuts through) the darkened night frame.
//
// WHY ADDITIVE-ABOVE-THE-OVERLAY (ADR 0016, building on ADR 0013). Sprites are Sprite-UNLIT and night is a
// full-screen MULTIPLY darkening overlay drawn at sortingOrder ~32760 â€” so a URP Light2D would do nothing
// (the unlit sprites sample no 2D light). A light here is instead an ADDITIVE quad drawn ABOVE that overlay
// (sortingOrder ~32770), blended One-One, so it ADDS brightness back into the crushed-dark frame â€” a lantern
// "punching a hole in the dark". The component (SceneLight) positions/orients/colors it; this shader does the
// soft shape + the night-gate.
//
// THE IN-SHADER NIGHT-GATE (zero per-light C# coupling to the cycle). The shader reads the global _DayNightTint
// the DayNightController publishes and scales its output by the frame DARKNESS (~ 1 - luminance(tint)): a light
// is ~invisible at a bright noon (so it can't wash daytime out) and full in a dark night. Mirrors LightMath.
// NightGate exactly (that C# copy is unit-tested headless). When the cycle is OFF the tint global is unset /
// near-black; we then DEFAULT TO SHOWING the light (tunable via _GateFallback) so the demo + edit-mode preview
// work â€” exactly how the water shader treats an unset tint.
//
// SHAPE. We work in the light's NORMALIZED local space: the quad spans [-1,1]; the light origin is the bottom-
// centre (uv.y=0 at the lamp, uv.y=1 at the far throw) so a CONE points "up" the local quad (the component
// orients the quad along the boat heading). Radial distance + the angle off the axis give the soft cone/round
// glow. A half-angle >= 180 is a full radial (round) glow; a small half-angle is a tight beam.
//
// Pixel-art friendly: smooth additive falloff with no texture (resolution-independent, crisp at any zoom), tiny
// cost (one quad). Visual-only: drives no sim, saves nothing (rule 5). Force-compiled by the magenta guard via
// the shipped Resources/AdditiveLight.mat â€” a break fails CI RED, not magenta-in-build.
//
// NOTE: no plus / operator characters inside any [Header] / [Tooltip] label (ShaderLab's lexer rejects them);
// no [unroll] over a runtime loop count; every symbol is defined before it is used.
Shader "HiddenHarbours/AdditiveLight"
{
    Properties
    {
        [Header(Light colour and strength)]
        [HDR] _LightColor   ("Light colour", Color) = (1, 0.86, 0.6, 1)
        _Intensity          ("Intensity (master)", Float) = 1.2

        [Header(Shape)]
        _ConeHalfAngle      ("Cone half-angle (deg, 180 is radial)", Range(0, 180)) = 30
        _AngularSoftness    ("Angular edge softness (0..1)", Range(0, 1)) = 0.4
        _EdgeSoftness       ("Radial edge softness (0..1)", Range(0, 1)) = 0.6
        _CoreBoost          ("Bright core boost", Range(0, 4)) = 1

        [Header(Lamp look. All zero is the smooth glow)]
        _CoreRadius         ("Hard core radius (fraction of throw, 0 is smooth)", Range(0, 1)) = 0
        _HaloStrength       ("Halo strength beside the core (0..1)", Range(0, 1)) = 0
        _BandSteps          ("Falloff bands (0 is smooth)", Float) = 0
        _CoreWhiten         ("Core pull toward white (0..1)", Range(0, 1)) = 0
        _PixelGrid          ("One art pixel in quad space (xy, 0 is no snap)", Vector) = (0, 0, 0, 0)

        [Header(Glowing glass. Core zero is off)]
        _GlassCore          ("Glass hot core radius (fraction of throw, 0 is off)", Range(0, 1)) = 0
        _GlassRim           ("Glass coloured rim width (fraction of throw)", Range(0, 1)) = 0
        _GlassWhiten        ("Glass core pull toward white (0..1)", Range(0, 1)) = 0
        _GlassRimLevel      ("Glass rim brightness (0..1)", Range(0, 1)) = 1
        _GlassTail          ("Glow tail brightness beside the glass (0..1)", Range(0, 1)) = 0
        _GlassTailScale     ("Glow tail half distance (fraction of throw)", Range(0.01, 1)) = 0.25
        _GlassBands         ("Glow tail bands (0 is smooth)", Float) = 0
        _GlassHot           ("Share of the core that is white hot (1 is all of it)", Range(0, 1)) = 1
        _GlassBody          ("Coloured body brightness round the hot centre (0..1)", Range(0, 1)) = 1
        _GlassBodyWhiten    ("Share of the core's whiten the body keeps (0..1)", Range(0, 1)) = 1
        _GlassRimFade       ("How far the rim fades across its width (0 is flat)", Range(0, 1)) = 0

        [Header(Lit window pane. Mode zero is off)]
        _PaneMode           ("Window pane mode (0 off, 1 on)", Float) = 0
        _PaneLook           ("Pane x head level, y sill level, z centre lift, w bands", Vector) = (0.62, 1, 0.22, 3)
        _PaneFrame          ("Pane x frame px, y mullion px, z mullion min px, w ledge depth m (below 0 off)", Vector) = (0, 0, 7, -1)
        _PaneLook2          ("Pane x across falloff, y unevenness, z tint variation", Vector) = (0, 0, 0, 0)
        _PaneTint           ("Pane second warm interior colour", Color) = (1, 0.72, 0.42, 1)

        [Header(Reflection streak. Mode zero is off)]
        _StreakMode         ("Reflection streak mode (0 off, 1 on)", Float) = 0
        _StreakLook         ("Streak x rows, y breakup, z wobble, w speed", Vector) = (12, 0.4, 0.35, 0.9)
        _StreakTime         ("Streak game time (seconds, set by the component)", Float) = 0

        [Header(Sorted among the world. Zero is over everything)]
        _InWorld            ("Sorted under the day/night tint (1 = divide the tint back out)", Float) = 0

        [Header(Night gate)]
        _GateThreshold      ("Gate darkness threshold (0..1)", Range(0, 1)) = 0.12
        _GateSoftness       ("Gate fade band (0..1)", Range(0, 1)) = 0.35
        _GateFallback       ("Show when no cycle (0..1)", Range(0, 1)) = 1

        [Header(Geometry set by the component)]
        _LampPos            ("Lamp position in quad space (xy in minus1..1)", Vector) = (0, -1, 0, 0)
        _Throw              ("Throw distance in quad-space units", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // ADDITIVE: ADD the glow into the (darkened) frame, brightening it. Premultiplied-style:
        // the fragment already multiplies colour by its scalar, so One One adds exactly that.
        Blend One One
        ColorMask RGB   // The calm mirror: ground cover leaves the alpha class under it alone
        Cull Off
        ZWrite Off
        ZTest Always   // draw on top regardless of depth (it is a screen-space-ish overlay element)

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv1        : TEXCOORD1;   // window panes only: the pane's drawn size in art pixels
                float2 uv2        : TEXCOORD2;   // window panes only: x = this window's own 0..1 (stable)
                float2 uv3        : TEXCOORD3;   // window panes only: x = the glass's posed world z (hull depth units)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 positionWS : TEXCOORD2;   // the streak's water test
                float2 uv2        : TEXCOORD3;
                float  paneZ      : TEXCOORD4;
            };

            // Global published by DayNightController (read-only here): the whole-frame multiply tint.
            float4 _DayNightTint;
            #ifndef HH_LAMP_GATE_DECLARED
            #define HH_LAMP_GATE_DECLARED
            float4 _HHLampGate;   // the shared lamp band (x off-darkness, y on-darkness); global
            #endif

            // The streak's masks, all published globals (read-only here). WATER = the published seabed
            // below the published sea level (SeabedGlobals, WaterSurface), the same decode the foam
            // buffer uses; with either unpublished there is no water to put a streak on, so none is
            // drawn. HULLS = IsoFacetHullRegistry's screen texture, alpha non-zero wherever a hull is
            // drawn (a 1x1 clear fallback is bound before it first runs), so a reflection never paints
            // over a boat.
            TEXTURE2D(_HHSeabedTex); SAMPLER(sampler_HHSeabedTex);
            float4 _HHSeabedRect;     // xy = world min (m), zw = world size (m)
            float4 _HHSeabedRange;    // x = min elevation, y = max, w = 1 when bound
            float4 _HHSeaLevelWorld;  // x = the drawn sea level (m), w > 0.5 when published
            Texture2D<float4> _HHHullScreenTex;
            // The hull pass's depth (world z, unsheared, larger = farther) and whether it is this camera's,
            // this frame (IsoFacetHullFeature). A lit pane reads it to hide under a nearer ledge of her mesh.
            Texture2D<float>  _HHHullDepthTex;
            float  _HHHullDepthLive;

            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor;
                float  _Intensity;
                float  _ConeHalfAngle;
                float  _AngularSoftness;
                float  _EdgeSoftness;
                float  _CoreBoost;
                float  _GateThreshold;
                float  _GateSoftness;
                float  _GateFallback;
                float4 _LampPos;
                float  _Throw;
                float  _CoreRadius;
                float  _HaloStrength;
                float  _BandSteps;
                float  _CoreWhiten;
                float4 _PixelGrid;
                float  _GlassCore;
                float  _GlassRim;
                float  _GlassWhiten;
                float  _GlassRimLevel;
                float  _GlassTail;
                float  _GlassTailScale;
                float  _GlassBands;
                float  _GlassHot;
                float  _GlassBody;
                float  _GlassBodyWhiten;
                float  _GlassRimFade;
                float  _PaneMode;
                float4 _PaneLook;
                float4 _PaneFrame;
                float4 _PaneLook2;
                float4 _PaneTint;
                float  _StreakMode;
                float4 _StreakLook;
                float  _StreakTime;
                float  _InWorld;
            CBUFFER_END

            // --- pure helpers (mirror LightMath; defined BEFORE use) ---

            // A light SORTED AMONG THE WORLD (SceneLight.SetWorldSort) draws under the day/night tint,
            // which then multiplies it dark. Dividing the tint out first makes what lands on the frame
            // exactly what the same light added over the tint: (base + L / t) * t = base * t + L. An
            // unset tint compensates nothing. Off (0) = the light sorts over the tint, untouched.
            // t is the colour the OVERLAY multiplies by: in a linear project that is the published tint
            // made linear (measured 2026-11-25: dividing by the raw global left a night lamp at ~0.12x,
            // the sRGB-to-linear ratio of the tint). The floor only guards a zero channel; the overlay
            // multiplies the same t back, so a deep-night channel (~0.013 linear) must not be clamped.
            #define HH_LIGHT_INWORLD_MIN_CHANNEL 0.002
            float3 HHSrgbToLinear(float3 c)
            {
                float3 lo = c / 12.92;
                float3 hi = pow(max((c + 0.055) / 1.055, 0.0), 2.4);
                return float3(c.r <= 0.04045 ? lo.r : hi.r, c.g <= 0.04045 ? lo.g : hi.g, c.b <= 0.04045 ? lo.b : hi.b);
            }
            float3 InWorld(float3 rgb)
            {
                if (_InWorld < 0.5) return rgb;
                float3 tint = _DayNightTint.rgb;
                if (tint.r + tint.g + tint.b <= 1e-3) return rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                tint = HHSrgbToLinear(tint);
                #endif
                return rgb / max(tint, HH_LIGHT_INWORLD_MIN_CHANNEL);
            }

            float Luma(float3 c)
            {
                return max(0.0, dot(c, float3(0.299, 0.587, 0.114)));
            }

            // The night-gate ramp on frame darkness, with the cycle-off fallback. Mirrors LightMath.NightGate /
            // NightGateWithFallback. _DayNightTint near-black (unset) => no cycle => show (the demo/edit case).
            float NightGate()
            {
                float lum = Luma(_DayNightTint.rgb);
                // "Cycle active" when the tint is meaningfully non-black (the controller is darkening the frame).
                float cycleActive = step(0.02, lum);
                float darkness = saturate(1.0 - lum);
                // The SHARED lamp band (LightMath.NightGate's offDarkness/onDarkness, published by
                // DayNightController as _HHLampGate; unset 0,0 = the old gate): off by day whatever the threshold.
                float lo = max(saturate(_GateThreshold), saturate(_HHLampGate.x));
                float hi = max(saturate(_GateThreshold + max(_GateSoftness, 1e-4)), saturate(_HHLampGate.y));
                hi = max(hi, lo + 1e-4);
                float gated = smoothstep(lo, hi, darkness);
                // No cycle => fall back to _GateFallback (default 1 = show).
                return lerp(saturate(_GateFallback), gated, cycleActive);
            }

            // A cheap stable hash, 0..1 (visual only: drives no sim).
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Is this world point open water? The seabed under the published sea level.
            float IsWater(float2 worldXY)
            {
                if (_HHSeabedRange.w < 0.5 || _HHSeaLevelWorld.w < 0.5) return 0.0;
                float2 suv = (worldXY - _HHSeabedRect.xy) / max(_HHSeabedRect.zw, float2(1e-3, 1e-3));
                if (any(suv < 0.0) || any(suv > 1.0)) return 0.0;
                float r = SAMPLE_TEXTURE2D_LOD(_HHSeabedTex, sampler_HHSeabedTex, suv, 0).r;
                float elevation = lerp(_HHSeabedRange.x, _HHSeabedRange.y, r);
                return step(elevation, _HHSeaLevelWorld.x);
            }

            // THE LIT PANE: brightness at one point of a window, 0 on its frame and mullions. 'uv' is the
            // pane's own 0..1 (v = 0 at the sill), 'sizePx' its drawn size in art pixels. Brighter low and
            // in the middle, because the lamp inside stands on a table, not on the ceiling. Dimmer toward
            // the sides (_PaneLook2.x), and a little uneven pixel to pixel (_PaneLook2.y), as old glass is;
            // 'id' is this window's own stable 0..1, so two windows of one cabin are not one stamp.
            float PaneShape(float2 uv, float2 sizePx, float id)
            {
                float2 sz = max(sizePx, float2(1.0, 1.0));
                float2 cell = floor(saturate(uv) * sz - 1e-3);    // which art pixel of the pane
                float2 p = cell + 0.5;
                float fr = max(_PaneFrame.x, 0.0);
                float inside = step(fr, cell.x) * step(fr, cell.y) * step(cell.x, sz.x - 1.0 - fr) * step(cell.y, sz.y - 1.0 - fr);
                float mw = max(_PaneFrame.y, 0.0);
                float mull = 1.0;
                if (mw >= 1.0)
                {
                    float2 lo = floor(sz * 0.5) - floor((mw - 1.0) * 0.5);
                    float2 hi = lo + mw - 1.0;
                    if (sz.x >= _PaneFrame.z) mull *= 1.0 - step(lo.x, cell.x) * step(cell.x, hi.x);
                    if (sz.y >= _PaneFrame.z) mull *= 1.0 - step(lo.y, cell.y) * step(cell.y, hi.y);
                }
                float v = saturate(p.y / sz.y);
                float level = lerp(_PaneLook.y, _PaneLook.x, v);
                float2 c = (p / sz - float2(0.5, 0.4)) * float2(2.0, 2.0);
                level += _PaneLook.z * saturate(1.0 - dot(c, c));
                float across = abs(saturate(p.x / sz.x) - 0.5) * 2.0;     // 0 mid-pane, 1 at a side
                level *= 1.0 - saturate(_PaneLook2.x) * across * across;
                level *= 1.0 + saturate(_PaneLook2.y) * (Hash21(cell + id * 97.0) * 2.0 - 1.0);
                float bands = floor(max(_PaneLook.w, 0.0));
                if (bands >= 1.0) level = ceil(level * bands) / bands;
                return level * inside * mull;
            }

            // THE STREAK: a lamp's broken reflection, a column of short rows hanging under it. 'q' is the
            // streak quad's -1..1 space (y = 1 at the lamp end). Rows re-break and sway with GAME time, handed in by the component (_StreakTime), so a frozen clock is a frozen streak.
            float StreakShape(float2 q)
            {
                float t = saturate((1.0 - q.y) * 0.5);            // 0 at the lamp end, 1 at the far end
                float rows = max(_StreakLook.x, 1.0);
                float row = floor(t * rows);
                float tm = _StreakTime * max(_StreakLook.w, 0.0);
                float h1 = Hash21(float2(row, floor(tm)));
                float h2 = Hash21(float2(row + 17.0, floor(tm * 0.7 + 0.5)));
                float sway = sin(row * 1.93 + tm * 2.1) * saturate(_StreakLook.z);
                float halfW = lerp(1.0, 0.4, t) * lerp(0.45, 1.0, h1);
                float bar = step(abs(q.x - sway * halfW), halfW);
                float kept = step(saturate(_StreakLook.y), h2);
                float body = pow(saturate(4.0 * t * (1.0 - t)), 0.7);   // brightest mid-way, fading both ends
                return bar * kept * body;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS.xy;
                OUT.uv = IN.uv;
                OUT.uv1 = IN.uv1;
                OUT.uv2 = IN.uv2;
                OUT.paneZ = IN.uv3.x;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ---- the lit window pane (BoatWindowGlow) ----------------------------------------------
                if (_PaneMode > 0.5)
                {
                    float id = IN.uv2.x;
                    // UNDER A LEDGE: where her mesh at this pixel stands nearer the camera than the glass by more
                    // than the wall's own relief (a roof overhang, a post), the glass is behind it and is not seen.
                    // From the hull pass's own depth, so it follows every rig with no number per boat. GlassLook.
                    if (_PaneFrame.w >= 0.0 && _HHHullDepthLive > 0.5)
                    {
                        int2 hp = int2(IN.positionCS.xy);
                        if (_HHHullScreenTex.Load(int3(hp, 0)).a >= 0.5 / 255.0 &&
                            _HHHullDepthTex.Load(int3(hp, 0)) < IN.paneZ - _PaneFrame.w)
                            discard;
                    }
                    float pk = max(_Intensity, 0.0) * PaneShape(IN.uv, IN.uv1, id) * NightGate();
                    if (pk <= 0.0) discard;
                    // This window's own warm colour (the room behind it is not one stamp), then deep
                    // amber at the head, toward a warm white where the pane is hottest.
                    float3 room = lerp(_LightColor.rgb, _PaneTint.rgb, saturate(_PaneLook2.z) * id);
                    float3 prgb = lerp(room, lerp(room, float3(1.0, 1.0, 1.0), 0.5), saturate(pk - 0.6));
                    return half4(InWorld(prgb * pk), pk);
                }

                // ---- the reflection streak (SceneLight's second quad) --------------------------------
                if (_StreakMode > 0.5)
                {
                    float2 sq = IN.uv * 2.0 - 1.0;
                    if (_PixelGrid.x > 0.0 && _PixelGrid.y > 0.0)
                        sq = (floor(sq / _PixelGrid.xy) + 0.5) * _PixelGrid.xy;
                    float hull = _HHHullScreenTex.Load(int3(int2(IN.positionCS.xy), 0)).a;
                    float sk = max(_Intensity, 0.0) * StreakShape(sq) * IsWater(IN.positionWS) * step(hull, 0.0) * NightGate();
                    if (sk <= 0.0) discard;
                    return half4(InWorld(_LightColor.rgb * sk), sk);
                }

                // Quad space: map uv [0,1]^2 to q in [-1,1]^2 (the quad centre is 0,0). The LAMP sits at
                // _LampPos in this space (bottom-centre (0,-1) for a cone so the beam throws "up"; centre (0,0)
                // for a radial so the halo is a full disc). 'rel' is the point relative to the lamp; the beam
                // axis is local +y. The component scales the quad so _Throw quad-space units == the world range.
                float2 q = IN.uv * 2.0 - 1.0;

                // PIXEL SNAP (the lamp look). _PixelGrid is one art pixel in quad space; snapping q to
                // the centre of its cell makes every band below a run of whole pixels instead of a
                // curve through them. Zero = no snap, the resolution-free glow.
                if (_PixelGrid.x > 0.0 && _PixelGrid.y > 0.0)
                    q = (floor(q / _PixelGrid.xy) + 0.5) * _PixelGrid.xy;

                float2 rel = q - _LampPos.xy;

                // RADIAL falloff: distance from the lamp normalized by the throw (1 at the lamp, 0 at the edge).
                float dist = saturate(length(rel) / max(_Throw, 1e-4));
                float linearR = saturate(1.0 - dist);
                float power = lerp(2.0, 0.6, saturate(_EdgeSoftness));   // soft halo vs harder disc
                float radial = pow(linearR, power);
                float bands = floor(max(_BandSteps, 0.0));

                // ANGULAR (cone) falloff: angle of 'rel' off the +y axis. halfAng >= 180 => full radial (no cut).
                float cone;
                float halfAng = max(_ConeHalfAngle, 0.0);
                if (halfAng >= 180.0)
                {
                    cone = 1.0;
                }
                else
                {
                    float ang = degrees(atan2(abs(rel.x), max(rel.y, 1e-4)));   // 0 on axis, grows off-axis
                    float soft = saturate(_AngularSoftness);
                    float inner = halfAng * (1.0 - soft);
                    cone = 1.0 - smoothstep(inner, halfAng, ang);
                    cone *= step(0.0, rel.y);   // forward-only: nothing behind the lamp
                }

                float shape;
                float whiten = 0.0;
                if (_GlassCore > 0.0)
                {
                    // GLOWING GLASS (GlassLook): a white-hot centre, a body of the lamp's own colour
                    // round it, a soft rim (the glass), then a glow that falls off the way light does,
                    // an inverse-square tail windowed to reach zero at the bloom's edge, cut into bands.
                    float d = length(rel) / max(_Throw, 1e-4);
                    float coreR = _GlassCore;
                    float rimR = coreR + max(_GlassRim, 0.0);
                    float coreMask = step(d, coreR);
                    float hotMask = step(d, coreR * saturate(_GlassHot));
                    float bodyMask = coreMask * (1.0 - hotMask);
                    float rimMask = step(d, rimR) * (1.0 - coreMask);
                    float rimT = saturate((d - coreR) / max(rimR - coreR, 1e-4));
                    float rimLevel = saturate(_GlassRimLevel) * (1.0 - saturate(_GlassRimFade) * rimT);
                    float x = max(d - rimR, 0.0) / max(_GlassTailScale, 1e-3);
                    float fall = 1.0 / (1.0 + x * x);
                    float edge = saturate(1.0 - (d - rimR) / max(1.0 - rimR, 1e-4));
                    float glow = saturate(_GlassTail) * fall * edge * edge;
                    float gb = floor(max(_GlassBands, 0.0));
                    if (gb >= 1.0) glow = floor(glow / max(saturate(_GlassTail), 1e-4) * gb + 0.5) / gb * saturate(_GlassTail);
                    float core = hotMask + bodyMask * saturate(_GlassBody);
                    shape = max(core, max(rimMask * rimLevel, glow)) * cone;
                    whiten = (hotMask + bodyMask * saturate(_GlassBodyWhiten)) * saturate(_GlassWhiten);
                }
                else if (_CoreRadius > 0.0)
                {
                    // THE LAMP LOOK (mirrors LightMath.LampShape): a flat hot core out to _CoreRadius,
                    // then a faint halo from the core's edge to the throw, cut into flat bands. A lamp
                    // is a small hard thing with a little glow round it; one smooth gradient the size
                    // of the whole quad is a ball of light hanging in the air.
                    float d = length(rel) / max(_Throw, 1e-4);
                    float coreMask = step(d, _CoreRadius);
                    float haloLin = saturate(1.0 - (d - _CoreRadius) / max(1.0 - _CoreRadius, 1e-4));
                    float halo = bands >= 1.0 ? ceil(haloLin * bands) / bands : haloLin;
                    shape = max(coreMask, halo * saturate(_HaloStrength)) * cone;
                    whiten = coreMask * saturate(_CoreWhiten);
                }
                else
                {
                    if (bands >= 1.0) radial = ceil(radial * bands) / bands;
                    shape = radial * cone;

                    // Bright core: a small extra punch near the lamp so the source reads as a hot point.
                    float core = pow(linearR, 6.0) * _CoreBoost;
                    shape = saturate(shape + core * cone);
                }

                float gate = NightGate();
                float k = max(_Intensity, 0.0) * shape * gate;

                if (k <= 0.0) discard;   // outside the cone / washed out by day -> contribute nothing

                // Premultiplied additive: colour already scaled by k; Blend One One adds it to the frame.
                float3 rgb = lerp(_LightColor.rgb, float3(1.0, 1.0, 1.0), whiten) * k;
                return half4(InWorld(rgb), k);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
