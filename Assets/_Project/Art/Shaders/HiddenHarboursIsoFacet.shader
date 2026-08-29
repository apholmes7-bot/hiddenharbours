// HiddenHarboursIsoFacet.shader — the mesh-hull facet pass (ADR 0022 phase 3).
//
// Reproduces the art director's rig rasteriser on the GPU: flat per-FACE normal, fixed
// screen-space key light, palette-RAMP lookup (not continuous lighting), ordered Bayer dither
// between adjacent ramp indices, no AA, no filtering. Descends from the measured spike shader
// (spike/3d-boats, ADR 0022: 1.3–4.4% px vs the rig's own render, dither crawl 0.00%).
//
// The hull mesh is placed in world space ALREADY iso-rotated (IsoFacetMath.RigToWorld — the
// rig's projection baked into the object transform), so the game's straight-down orthographic
// 2D camera reproduces the rig's exact projection AND z-buffer. That collapses the rig's
// shadeOf() to a plain dot(worldNormal, LN): the key light is fixed in SCREEN space, which is
// what makes the shading read as pixel art rather than as lit 3D. _LN arrives with its z
// NEGATED (IsoFacetMath.ShaderLightVector) because the object matrix is a REFLECTION of the
// rig's right-handed frame — measured in the spike; do not "fix" the sign.
//
// ⚠️ The ONLY pass has LightMode "HHHullFacet", which the 2D renderer's own draw does NOT pick
// up (deliberately: a mesh writing the scene's shared depth buffer punches holes in every later
// sprite that z-tests). IsoFacetHullFeature draws it off-screen into a 4-target MRT with a
// private depth buffer; IsoFacetOverlay re-composes the resolved image in-scene.
//
// Dither is indexed in the HULL-CELL frame derived from world position — NOT SV_Position — so
// it cannot crawl when the hull translates (the 13–16% class ADR 0022 measured for
// screen-pinned dither) and needs no per-render-target phase calibration (the spike's
// _DitherPhase probe becomes unnecessary: world-derived cell coordinates are y-flip-proof).
//
// THE DECK-OCCUPANT SPLIT (owner playtest 2026-08-07: "rider/player sprites visible THROUGH
// closed cabins"). A figure standing on deck is an ordinary sprite drawn ABOVE the hull's
// whole-object sorting slot, so a wheelhouse in front of them can never cover them: sorting is per
// OBJECT and the question is per PIXEL. This pass already holds the answer — it runs against a
// private z-buffer, so every fragment knows its own view depth, and a figure standing on the deck
// has ONE depth, their feet. So a hull carrying an occupant writes a FORE id into the facet alpha
// wherever her geometry is NEARER the camera than that figure, and _HullId everywhere else. It is a
// PARTITION, never a duplication: every solid pixel still carries exactly one id, the overlay
// re-composes them all (so the hull's own picture is unchanged), and the FIGURE's shader discards
// where it reads a fore id — covered exactly where the boat is genuinely in front of them.
//
// ⚠️ N OCCUPANTS, NOT ONE — AND THE ENCODING IS THE WHOLE TRICK. The stern-deck loop puts a
// skipper, a sternman, four pieces of working furniture, two trap stacks and a carried tray on one
// deck at once, all at DIFFERENT depths, and one split plane cannot be right for two of them: hull
// geometry between two occupants is "in front" of the far one and "behind" the near one, and a
// single id cannot say both. So the plane becomes a fixed ARRAY of planes, and the id becomes a
// BAND INDEX.
//
// Per pixel: band = HOW MANY occupants this fragment is in front of. Since the occupants a pixel is
// in front of are always the deepest ones (z < d is a prefix of the depth-sorted list), that COUNT
// is all the information anyone needs, and counting is order-free — no sort on the GPU, no sort on
// the CPU to feed it. Band 0 (behind everybody) writes _HullId exactly as before; band m > 0 writes
// _HullIdFore + (m-1), the hull's fore ids being a CONTIGUOUS block reserved at registration. An
// occupant of rank r (r = how many occupants stand at or deeper than they do) is then hidden
// exactly where band >= r — a plain RANGE over that block, which is what the sprite shader tests.
//
// The nesting is why this is exact rather than approximate: the deeper occupant's fore region
// strictly contains the nearer one's, and consecutive band ids reproduce that containment in a
// single 8-bit channel.
//
// _DeckOccupantCount = 0 — nobody aboard, and every hull most of the time — means the loop never
// runs and the alpha is byte-identical to before this existed. With exactly ONE occupant the band
// can only be 0 or 1, so the pass writes _HullId / _HullIdFore, which is byte-for-byte the
// single-plane split this grew out of.
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters
// in Property display strings; no [unroll] over runtime bounds; force-compiled headless by
// IsoFacetShaderCompileGuardTests so a break fails CI red.
Shader "HiddenHarbours/IsoFacet"
{
    Properties
    {
        [NoScaleOffset] _RampTex ("Palette ramps by material", 2D) = "white" {}
        [NoScaleOffset] _DarkRampTex ("RINDEX darkened ramps by material", 2D) = "white" {}
        _KeyColor ("Keyline colour, pre linearised", Color) = (0.05, 0.08, 0.09, 1)
        _Gain ("Rig GAIN", Float) = 0
        _Bias ("Rig BIAS", Float) = 0
        _PivotPx ("Cell pivot px from top left", Vector) = (0, 0, 0, 0)
        _PixelsPerMetre ("Pixels per metre", Float) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // Both passes below share ONE vertex program, deliberately. The guard pass must land on
        // exactly the pixels the facet pass will land on — including the per-face `db` depth bias
        // — so the two cannot be allowed to drift apart as separately-maintained copies. (The
        // water shader uses this same SubShader-scope include pattern for the same reason.)
        HLSLINCLUDE
        #pragma target 3.5

        // THE CUTAWAY LEVEL GATE (owner ruling 2026-08-26), behind a keyword that is OFF by
        // default. Everything it adds lives inside #ifdef HH_LEVEL_GATE: the TEXCOORD1 vertex
        // input, the extra varying, the uniform and the two discards. With the keyword off the
        // compiled program is LITERALLY the pre-gate one — not "the same picture for a discard per
        // fragment on every hull every frame", which is a cost (rule 7) and is what the spike's
        // first version actually did. The interior-mesh spike measured the boundary both ways:
        // shipped program vs the gated variant at 0 = 0 differing px, and vs hull-plus-rooms
        // through the gate at 0 = 0 differing px (the second is what says the gate HIDES the
        // geometry rather than the geometry happening to be invisible).
        //
        // ⚠️ multi_compile_local, NOT shader_feature_local, and the difference shows up in a BUILD
        // only. shader_feature keeps only the variants some MATERIAL ASSET enables — and this
        // hull's material is never an asset: IsoFacetHullRenderer builds it at runtime with
        // `new Material(Shader.Find(...))`, so nothing in the project would carry the keyword and
        // the stripper would drop the gated variant. The cutaway would then work in the editor and
        // quietly never happen in the player: the exact shape of bug this project keeps paying for.
        // multi_compile always compiles both. Cost: one extra variant of this shader; the OFF
        // variant is unchanged, so no hull pays anything at run time. _local, so the keyword lives
        // per MATERIAL — Shader.EnableKeyword does not reach it (measured on the spike fixture);
        // the renderer writes its own instance material.
        #pragma multi_compile_local _ HH_LEVEL_GATE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ⚠️ THE SLOT COUNT, AND IT IS MEASURED, NOT GUESSED. The stern-deck loop's worst beat
            // (deck-loop-kit README, the twelve-beat shift; TrapIso.CAPS) puts TEN things on a
            // lobster boat's deck at once: two hands, four pieces of furniture, two trap stacks, the
            // pot in play and the fish tray. Twelve is that plus two. The cost is the ID BUDGET —
            // each hull reserves one base id plus this many contiguous fore ids out of 255, so
            // twelve leaves 19 simultaneous mesh hulls against a roadmap whose largest scene is a
            // harbour of a dozen. Raising it costs hulls; lowering it costs occupants, loudly (the
            // registry refuses the surplus claim rather than dropping it silently).
            //
            // C# HOLDS THE SAME NUMBER (IsoFacetHullRenderer.DeckOccupantSlots) and the two are
            // asserted equal by DeckOccupantSlotTests — this literal is the one a compiler sees, so
            // it cannot be an expression.
            #define HH_DECK_OCCUPANT_SLOTS 12

            // Palette lookups are integer Loads — never filtered, never mipped.
            Texture2D<float4> _RampTex;
            Texture2D<float4> _DarkRampTex;

            // Set once per hull material (IsoFacetHullRenderer.Configure). Arrays cannot be
            // Properties; they are plain uniforms via SetVectorArray. Not SRP-batcher packed —
            // hulls are few and each is one draw in a private pass.
            float4 _LN;                 // rig LN, z pre-negated for the reflected frame
            float  _Gain, _Bias;
            float4 _Bayer[4];           // BAYER[x&3][y&3], values already (v+0.5)/16, row = x
            float4 _RampMeta[16];       // per material: x = ramp length, y = index offset
            float4 _KeyColor;           // pre-linearised keyline colour
            float4 _PivotPx;            // xy = cell pivot in px from the cell's top-left
            float  _PixelsPerMetre;

            // Per draw via MaterialPropertyBlock (IsoFacetHullRenderer.ApplyPose).
            float4 _HullOrigin;         // xy = world position of the rig origin (unheaved root)
            float  _HullId;             // hull id already divided by 255; the facet alpha
            // THE DECK-OCCUPANT SPLIT (see the header). _HullIdFore is the BASE of this hull's
            // contiguous fore-id block, already divided by 255 — band m writes _HullIdFore + (m-1).
            // _DeckOccupant[k].x is slot k's occupant's view depth in the same world z the fragment
            // carries, and .w is 1 only while that slot is actually claimed and standing.
            // _DeckOccupantCount is how many of them are live: 0 skips the whole thing. All default
            // to 0, so a material nobody wrote to never splits.
            float  _HullIdFore;
            float4 _DeckOccupant[HH_DECK_OCCUPANT_SLOTS];
            float  _DeckOccupantCount;
            // ADR 0033 — ONE DEPTH UNIT. x = the y→z shear g = cos(elev)(1−sin(elev))/sin(elev),
            // y = the world y it is referenced to (the water's own _HeightWorldMin.y). C# reference:
            // DisplacedWaterMath.ShearedDepth, of which the vert() line below is a transcription.
            // Defaults to 0, so a material nobody wrote to — and every hull while no displaced sea
            // is live — is byte-identical to before ADR 0033 (the A/B contract).
            float4 _HullShear;

#ifdef HH_LEVEL_GATE
            // WHICH LEVEL THE OCCUPANT IS INSIDE. 0 = show the exterior, which is the shipped
            // behaviour and the state every hull starts and ends in.
            //
            // ⚠️ PER DRAW, via the MaterialPropertyBlock — NOT Shader.SetGlobalFloat, which is what
            // the spike used and what a fixture with one hull on screen cannot tell apart. Eighteen
            // lobster boats can be afloat in one creek and only ONE of them has anybody below; a
            // global would cut open every sister ship in the harbour at the same instant, and the
            // sisters mostly share a def so the id in the tag would match on all of them. Same class
            // as the deck-occupant properties beside it, and set from the same ApplyPose write.
            float  _HHLevelShown;
            // THE LID (coordinator ruling 2026-08-27): the level whose faces are the ceiling of the
            // one the occupant is inside, and which therefore comes off with it. 0 = none.
            //
            // ONE HOP IS ENFORCED BY THE SHAPE OF THIS DATA, not by a rule anybody has to remember:
            // there is one lid uniform, so a chain simply cannot be expressed here. The bake refuses
            // a lid that has a lid, and a level carries one lid field, so the same law holds in all
            // three places it could be broken.
            float  _HHLevelLid;

            // ⭐ THE INTERIOR'S OWN PALETTE, AND WHY IT IS A SECOND TABLE RATHER THAN A WIDER ONE.
            //
            // A full-mesh room needs 20–21 ramps of its own (measured over every hull, level and
            // facing of boatInteriorRig.js, counting distinct COLOUR ARRAYS rather than names).
            // The hull's own faces already spend 10–13. One table would therefore have to hold
            // THIRTY-THREE on the tanker — so the fleet's float4[16] cannot simply be widened to
            // 24 or 32, and widening it to 48 would have re-opened a cap that is guarded in three
            // places and that the road fleet's night-lamp slot-reuse ruling (#668) rests on.
            //
            // Measured cost of the two, on the shipped target: widening is byte-identical in the
            // compiled program (it costs 512 B of constant buffer, not instructions) but every
            // hull pays it in every frame. This table costs 384 B and ~148 B of extra fragment
            // code, and ONLY while a cut is live — because it lives in here, and
            // IsoFacetHullRenderer.ApplyCutawayKeyword enables HH_LEVEL_GATE only then. On a
            // harbour of boats nobody is aboard, this design costs nothing at all (rule 7).
            //
            // The two tables are separate INDEX SPACES: an interior face's matId counts from 0 in
            // here, so the hull's 16 stay exactly the hull's and neither side can starve the other.
            float4 _RampMetaInterior[24];
            Texture2D<float4> _RampTexInterior;
            Texture2D<float4> _DarkRampTexInterior;
#endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // x = matId  y = faceBias b  z = depthBias db  w = INTERIOR side code (0 =
                // exterior both sides — the value every mesh baked before the interior mask
                // existed carries, so an un-rebaked hull behaves exactly as before; 1 = interior
                // both sides; 2/3 = interior on the front/back side only — see vertGuard, which
                // decodes the rendered side).
                float4 attrs      : TEXCOORD0;
#ifdef HH_LEVEL_GATE
                // x = the face's LEVEL id, from her rig's own geometry().ids (0 = hull, the
                // exterior silhouette, never cut). y = 1 on emitted INTERIOR geometry, 0 on
                // the hull's own faces. Absent on every hull baked before the cutaway kit.
                float2 levelTag   : TEXCOORD1;
                // THE ROOM'S SURFACE. xy = generator id + period, flat per face; zw = the rig's
                // own per-vertex uv, which paint() interpolates before calling the generator.
                // All zero on a hull with no room, and on every hull face of a hull that has one.
                float4 texAttr    : TEXCOORD2;
#endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // Face-flat by construction (every vertex of a face carries the face's values);
                // nointerpolation keeps them exact across the fan.
                nointerpolation float fidx : TEXCOORD0;
                nointerpolation float mat  : TEXCOORD1;
                float3 wpos : TEXCOORD2;         // xy = dither frame  z = TRUE unbiased depth
#ifdef HH_LEVEL_GATE
                // xy = the level tag, z = 1 when the camera is rendering this
                // face's FRONT (decoded from the stored normal exactly as vertGuard does).
                nointerpolation float3 lvl : TEXCOORD3;
                // xy flat (generator + period), zw INTERPOLATED — the uv has to vary across the
                // face or the pattern would be one flat value per facet.
                nointerpolation float2 texKp : TEXCOORD4;
                float2 texUv : TEXCOORD5;
#endif
            };

            struct FragOut
            {
                float4 facet : SV_Target0;       // rgb = facet colour, a = hull id
                float4 dark  : SV_Target1;       // rgb = RINDEX darkened colour
                float4 key   : SV_Target2;       // rgb = keyline colour
                float  depth : SV_Target3;       // true unbiased view depth (world z, metres)
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1.0)).xyz;
                // The object matrix is orthogonal (rotation times mirror), so inverse-transpose
                // equals the matrix itself — the plain mul is exact, as in the spike.
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normalOS));

                // The rig: sh = shadeOf(n, se, ce). In iso-rotated world space that is exactly a
                // dot with LN — see the header comment.
                float sh = dot(wn, _LN.xyz);
                // "if(sh<0 && f.b<=-1) sh = shadeOf(-n)*0.9" — the rig's interior/backface rescue.
                if (sh < 0 && v.attrs.y <= -1) sh = -sh * 0.9;

                o.fidx = sh * _Gain + _Bias + v.attrs.y;
                o.mat  = v.attrs.x;
#ifdef HH_LEVEL_GATE
                o.lvl  = float3(v.levelTag, dot(wn, UNITY_MATRIX_V[2].xyz) >= 0.0 ? 1.0 : 0.0);
                o.texKp = v.texAttr.xy;
                o.texUv = v.texAttr.zw;
#endif
                // ⚠️ TWO DEPTHS, AND THE SPLIT IS THE DESIGN (ADR 0033).
                //
                // o.wpos.z stays the RIG's own depth (ry·cos − rz·sin, unsheared) because the two
                // things that read it are INTRA-HULL questions asked in the rig's frame: the
                // deck-occupant band (is this planking in front of the figure standing on it?) and
                // the keyline resolve's adjacent-pixel edge test, which is a transcription of the
                // art director's own post-pass over the rig's own z-buffer. Shearing either would
                // answer a different question with a worse number — the shear's gradient is a pure
                // function of screen row, so it would charge a flat surface 0.013 m of false depth
                // per pixel against the resolve's 0.30 m threshold, and would ask the occupant test
                // to compare depths taken about two different world y. Both are byte-identical
                // through this change, which is what keeps the #481 occupant suites and the golden
                // masters untouched.
                //
                // positionCS DOES take the shear, because the private z-buffer is the ONE place
                // where the hull meets water that was recorded in a different unit. Referenced to
                // the water's own ReferenceY (never anything per-hull), so any two fragments
                // sharing a pixel — same hull, another hull, or a bolted-on fitting — take the
                // identical shift and every ordering among them is preserved.
                o.wpos = wp;                     // camera looks along +Z; larger z = further
                wp.z  -= (wp.y - _HullShear.y) * _HullShear.x;   // ADR 0033: the y→z shear
                wp.z  -= v.attrs.z;              // f.db pulls the face toward the camera
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }

#ifdef HH_LEVEL_GATE
            // ONE tag, both halves of ADR 0038's swap (question B) — and the owner's cutaway.
            //   _HHLevelShown == 0 : the shipped picture. Interior faces (if any exist at
            //                        all) are off; nothing else is touched.
            //   _HHLevelShown == k : you are inside level k. The hull's own faces that
            //                        belong to level k are culled (the house you are inside
            //                        of), level k's room is drawn, every other level's room
            //                        is off. Exactly one of the two, per pixel, by
            //                        construction rather than by sorting.
            // The BACK-facing test on interior geometry is the rig's hand-written "THE CUT"
            // (near walls culled, far walls kept) falling out of the geometry for free, at
            // every heading instead of at eight.
            //
            // TWO LEVELS THIS CAN NEVER BE ASKED FOR, and both are structural rather than
            // policed here. `hull` (the exterior silhouette) is id 0 on every rig in the kit,
            // and 0 is the same value as "gate off" — so the shell can never be cut away and
            // the room always shows INSIDE her own outline, which is the whole of what
            // "cutaway" means. `rigging` (arch, aerials, gantry, masts, derricks) is id 5, and
            // 5 is outside the 1..4 band a walkable level ever occupies — so a cut can never
            // take a spar with the room it happens to stand over. Neither fact is transcribed
            // into C#: the rigs publish the ids and the bake carries them.
            //
            // ⚠️ THE SWAP ALONE IS NOT ENOUGH, once room geometry exists. Culling the house
            // does not cull the hull's own near TOPSIDES, which in a ¾ view stand between the
            // camera and a cabin sole; the spike measured a revealed room surviving at only
            // 20.3% because of them. The lever is already in the mesh — UV0.z, the rig's own
            // per-face `db`, subtracted from clip depth in vert() above — and setting it on
            // the room to the hull's bounding-sphere diameter took the same room to 97.6%.
            // Recorded here because the next person to add the shell will meet it.
            // ⭐ THE ROOM'S PROCEDURAL SURFACE — boatInteriorRig.js's plankTex / boardTex /
            // quiltTex, transcribed. paint() does `if (tex && uv) fi += tex(uu, vv)`, shifting the
            // ramp index by a small INTEGER; because the shift is an integer, adding it to `idx`
            // after the dither is exactly equivalent to adding it to `fidx` before, and leaves the
            // Bayer term untouched. Measured coverage: 28.6% of the lobster's wheelhouse faces and
            // 63.4% of her cuddy carry one, so this is most of a berth space's surface, not a
            // garnish.
            //
            // ⚠️⚠️ THE RIG'S PER-PLANK HASH IS DEAD CODE, AND THIS TRANSCRIBES WHAT IT DOES, NOT
            // WHAT IT MEANT. plankTex and quiltTex both branch on `hash2(...) < 0.5`, intending a
            // per-plank / per-cell coin flip. hash2 ends with `((h ^ (h >> 16)) >>> 0) / 4294967296`
            // — and in JS `>>` coerces to int32 and sign-extends, so bit 31 of `h ^ (h >> 16)` is
            // ALWAYS the sign bit xored with itself, i.e. always 0. The value can therefore never
            // reach 0.5. Measured in the repo's own V8 over a in [-40,40] x b in [-20,20]: 3321 of
            // 3321 samples below 0.5, max 0.49996. So the hash branch never fires, plankTex never
            // returns -1 and quiltTex never returns +1 — in the SPRITE path too.
            //
            // Transcribing the intent instead would make the mesh disagree with the shipped sheets
            // on every plank, which is the opposite of parity. If the art director fixes hash2, both
            // paths move together and this comment is the note that says where to look. Reported
            // upstream rather than fixed here: the sprite art is shipped and this is not our file.
            float HHInteriorTex(float2 kp, float2 uv)
            {
                int kind = (int)round(kp.x);
                if (kind == 0) return 0.0;
                float p = kp.y;
                if (p <= 0.0) return 0.0;

                // JS `((x % p) + p) % p` — a true positive modulo, since JS % keeps the dividend's
                // sign and a room's uv crosses zero.
                if (kind == 1)              // plankTex(p): a groove every p in V
                    return (uv.y - p * floor(uv.y / p)) < 0.022 ? -2.0 : 0.0;
                if (kind == 2)              // boardTex(p): a groove every p in U
                    return (uv.x - p * floor(uv.x / p)) < 0.026 ? -1.0 : 0.0;
                                            // quiltTex(): a 0.20 grid, grooves on both axes
                float fu = uv.x - p * floor(uv.x / p);
                float fv = uv.y - p * floor(uv.y / p);
                return (fu < 0.030 || fv < 0.030) ? -1.0 : 0.0;
            }

            bool HHLevelDiscards(float3 lvl)
            {
                bool isInterior = lvl.y > 0.5;
                if (_HHLevelShown < 0.5) return isInterior;
                bool isShown = abs(lvl.x - _HHLevelShown) < 0.5;

                // INTERIOR geometry tests the SHOWN level only. A lid is a thing that comes OFF; it
                // is not a second room you are also standing in, and drawing its fit-out because you
                // are under it would put two rooms in one hull.
                if (isInterior) return !isShown || lvl.z < 0.5;

                // HULL geometry loses the level you are inside AND its declared lid. Two compares,
                // no loop, no chain.
                bool isLid = _HHLevelLid >= 0.5 && abs(lvl.x - _HHLevelLid) < 0.5;
                return isShown || isLid;
            }
#endif

            FragOut frag (Varyings i)
            {
#ifdef HH_LEVEL_GATE
                if (HHLevelDiscards(i.lvl)) discard;
#endif
                // The hull-cell pixel this fragment lands on, derived from WORLD position: the
                // rig's screen grid is just world metres times PPU with y down and the pivot as
                // origin. Locked to the hull, immune to render-target conventions.
                float2 cellF = float2(
                    (i.wpos.x - _HullOrigin.x) * _PixelsPerMetre + _PivotPx.x,
                    _PivotPx.y - (i.wpos.y - _HullOrigin.y) * _PixelsPerMetre);
                int2 cell = int2(floor(cellF));
                float bay = _Bayer[cell.x & 3][cell.y & 3];

                int m    = (int)round(i.mat);
#ifdef HH_LEVEL_GATE
                // Which table this face reads from. lvl.y is the bake's own per-face interior
                // flag and the fragment has already used it once, in HHLevelDiscards — this is
                // the same bit asked a second time, not a new mechanism and not a new channel.
                bool hhInterior = i.lvl.y > 0.5;
                int len  = (int)(hhInterior ? _RampMetaInterior[m].x : _RampMeta[m].x);
                int off  = (int)(hhInterior ? _RampMetaInterior[m].y : _RampMeta[m].y);
#else
                int len  = (int)_RampMeta[m].x;
                int off  = (int)_RampMeta[m].y;
#endif
                float fbase = floor(i.fidx);
                int idx = (int)fbase + ((i.fidx - fbase) > bay ? 1 : 0) + off;
#ifdef HH_LEVEL_GATE
                // Integer shift, so this is the rig's `fi += tex(u,v)` moved to the other side of
                // the dither compare without changing it: floor(f + k) == floor(f) + k and the
                // fractional part is untouched. Zero on every hull face — the attribute is zero
                // there — so no hull pixel changes.
                if (hhInterior) idx += (int)HHInteriorTex(i.texKp, i.texUv);
#endif
                idx = clamp(idx, 0, len - 1);

                // WHICH OF THIS HULL'S IDS THIS PIXEL CARRIES. Camera looks along +Z, so a SMALLER
                // depth is nearer: hull geometry in front of a figure standing on the deck takes a
                // FORE id and is composed over them. Strictly less-than, so the very planking under
                // their feet (same depth) stays behind them.
                //
                // The band is a COUNT of the occupants this pixel is in front of (see the header),
                // so the slots need no ordering and the loop is order-free. The whole thing hangs
                // off one uniform that is 0 for every hull with nobody aboard, which is nearly all
                // of them nearly all of the time (rule 7); an unclaimed slot inside the loop costs
                // exactly one compare that fails. Bound is a compile-time constant, so this unrolls
                // without the [unroll] this file's cautions forbid over runtime bounds.
                float hullId = _HullId;
                if (_DeckOccupantCount > 0.5)
                {
                    int band = 0;
                    for (int k = 0; k < HH_DECK_OCCUPANT_SLOTS; k++)
                    {
                        if (_DeckOccupant[k].w > 0.5 && i.wpos.z < _DeckOccupant[k].x) band++;
                    }
                    // Band m takes the m-th id of the reserved block. 1/255 per step because every
                    // id in this channel is already divided by 255.
                    if (band > 0) hullId = _HullIdFore + (band - 1) * (1.0 / 255.0);
                }

                FragOut o;
#ifdef HH_LEVEL_GATE
                o.facet = float4(hhInterior ? _RampTexInterior.Load(int3(idx, m, 0)).rgb
                                            : _RampTex.Load(int3(idx, m, 0)).rgb, hullId);
                o.dark  = float4(hhInterior ? _DarkRampTexInterior.Load(int3(idx, m, 0)).rgb
                                            : _DarkRampTex.Load(int3(idx, m, 0)).rgb, 1.0);
#else
                o.facet = float4(_RampTex.Load(int3(idx, m, 0)).rgb, hullId);
                o.dark  = float4(_DarkRampTex.Load(int3(idx, m, 0)).rgb, 1.0);
#endif
                o.key   = float4(_KeyColor.rgb, 1.0);
                o.depth = i.wpos.z;
                return o;
            }

            // ---- the INTERIOR GUARD (ADR 0023: the per-face interior mask) -------------------
            // Rides the SAME vert() above, so it occupies the same pixels with the same depth.
            // Its one job is to answer, per pixel, "is the nearest hull surface here an open
            // interior?" — resolved by the ordinary z-test into the guard pass's OWN depth
            // buffer, which is why no stencil is needed and why the facet pass is untouched.
            struct GuardVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float interior : TEXCOORD0;
#ifdef HH_LEVEL_GATE
                nointerpolation float3 lvl : TEXCOORD1;   // see HHLevelDiscards
#endif
            };

            GuardVaryings vertGuard (Attributes v)
            {
                GuardVaryings o;
#ifdef HH_LEVEL_GATE
                Varyings f = vert(v);
                o.positionCS = f.positionCS;
                o.lvl = f.lvl;
#else
                o.positionCS = vert(v).positionCS;
#endif

                // attrs.w is the PER-SIDE interior code (RigMeshInteriorClassifier.ClassifySides):
                //   0 = exterior both sides   1 = interior both sides
                //   2 = interior when the camera renders the FRONT (the side the face normal
                //       points toward)        3 = interior when it renders the BACK
                // Which side is being rendered comes from the SAME stored normal the bake labelled
                // the sides with — never from SV_IsFrontFace, whose winding convention would have
                // to survive the object matrix's deliberate reflection (det −1) and the rigs'
                // shared-winding mirror twins. Orthogonal maps preserve dot products, so
                // sign(dot(worldNormal, towardCamera)) here equals sign(dot(normalOS, eyeOS)),
                // the exact quantity the classifier sorted sides by. The third row of the view
                // matrix is the world-space toward-camera axis. A face edge-on to the camera
                // (dot 0) rasterises no pixels, so its tie-break never shows.
                float3 wn = mul((float3x3)unity_ObjectToWorld, v.normalOS);
                bool front = dot(wn, UNITY_MATRIX_V[2].xyz) >= 0.0;
                int code = (int)round(v.attrs.w);
                bool interior = code == 1 || code == (front ? 2 : 3);
                o.interior = interior ? 1.0 : 0.0;
                return o;
            }

            float fragGuard (GuardVaryings i) : SV_Target
            {
#ifdef HH_LEVEL_GATE
                if (HHLevelDiscards(i.lvl)) discard;
#endif
                return i.interior;
            }
        ENDHLSL

        Pass
        {
            Name "HHHullFacet"
            // Drawn ONLY by IsoFacetHullFeature's renderer list — see the header comment.
            Tags { "LightMode" = "HHHullFacet" }

            Cull Off            // the rig z-buffers everything; it never backface-culls
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // The interior guard. Recorded by IsoFacetHullFeature ONLY while a displaced sea is live
        // and the mask is enabled; its single-channel output is published as _HHHullGuardTex and
        // the displaced water's fragment discards against it. Same state as the facet pass (so the
        // same fragments survive), ColorMask R because the target is R8.
        Pass
        {
            Name "HHHullGuard"
            Tags { "LightMode" = "HHHullGuard" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertGuard
            #pragma fragment fragGuard
            ENDHLSL
        }
    }
}
