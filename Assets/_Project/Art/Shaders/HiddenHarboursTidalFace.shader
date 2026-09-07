// HiddenHarboursTidalFace.shader — a DRAWN VERTICAL FACE THAT THE SEA COVERS AND UNCOVERS.
//
// THE DEFECT (owner playtest 2026-09-06, Nine Mile Creek north wall, 06:11): "boats arent touching
// but not sitting in water". #751 pulled the berth line off the timber and #753 taught a hull's
// picture to ride the tide — and the fleet still read as models set on a shelf. Measured on the
// owner's own scene and the committed sheet: the quay face is a static sprite whose ink runs 2.625
// units BELOW its pivot, so the wall's picture covers the sea from world y 82.00 up to its lip at
// 87.00 — five units of solid timber standing over the water, AT EVERY STATE OF THE TIDE. The five
// wall hulls lie at y 84.00-84.50. So the sea's drawn edge was 2.0-2.5 units below every hull at
// mean water and 3.7-4.2 below her at spring high, and what she was drawn against was wall.
//
// THE FIX IS ONE LINE: the face does not draw where the sea is over it. What shows through is the
// sea that is already drawn there and was being hidden — the water quad covers the whole basin and
// sorts below this (SortingBands.Sea = -5 against the face's -3), so this shader ADDS no water. It
// stops covering it up. The wall's exposed height then falls straight out of the arithmetic: 0.61
// units at spring high (this wharf's authored 0.80 m of freeboard) and 3.98 at spring low (its 5.2 m
// of bared face), and the growth bands the pack re-baked at #478 — barnacle 1.76-3.52 m, rockweed
// 0.26-1.76 m above chart datum — are covered and uncovered by the water they were painted for.
//
// ⭐ IT DOES NOT GET ITS OWN SEA. The level arrives on _HHSeaLevelWorld, the ONE global WaterSurface
// publishes for "every surface that meets the sea without being it" — the same one the cliff faces
// ride (CliffWaterlineMath's opening rule, and the reason it is written down there). A second read
// of the tide here is exactly how a cliff, a quay and a hull start disagreeing about where the water
// is by a few centimetres per metre of range.
//
// ⭐ AND THE ARITHMETIC IS Core TidalRide's, PUSHED IN RATHER THAN SPELLED HERE. _HHFaceTide carries
// (lip world y, lip elevation, screen units per metre of height, on) and the waterline row is
//     lipWorldY + (seaLevel - lipElevation) * heightScale
// which IS TidalRide.ScreenRise(seaLevel, lipElevation) added to the one line the placement
// guarantees. The scale is PUSHED, not written here as 0.766, so IsoGround.HeightScale stays the
// single definition and no shader constant can drift from the camera (rule 6).
//
// ⚠️ THE TIDE, NOT THE SURGE. The cliff face adds the shared wave field's surge to its own waterline;
// this one does not, and Nine Mile Creek is the reason it is defensible — a creek behind a
// breakwater, where the drawn crests inside the basin are centimetres. Adding it later is one more
// term on the same line and the same published freqScale/exaggeration in _HHSeaLevelWorld.yz; adding
// it by transcribing the train loop a second time is what CliffWaterlineMath forbids.
//
// ⚠️ EVERYTHING ELSE IS URP'S OWN Sprite-Unlit-Default, structurally verbatim — the same Core2D /
// 2DCommon includes, the same UnityFlipSprite and SetUpSpriteInstanceProperties, the same
// `input.color * _Color * unity_SpriteColor`, the same blend, the same legacy fallback properties.
// That is deliberate and load-bearing, exactly as it is in HiddenHarboursDeckOccludedSprite: this
// material replaces the one the quay already draws through, so any difference in tint, flip,
// instancing or premultiplication would show up as the wall changing colour. Re-derive from URP's
// shader, never from memory, if this ever needs to move. (The project's sprites are unlit and night
// is a full-screen multiply overlay — ADR 0016, SceneLight's opening note — so an unlit clone is the
// like-for-like swap here and not a downgrade.)
//
// _HHFaceTide.w == 0 is a PIXEL-IDENTICAL passthrough: no publisher, no clip, and the face draws
// exactly the picture it draws today. So is _HHSeaLevelWorld.w == 0, the "there is no sea here"
// state WaterSurface publishes on disable — a wall must not stay cut by a sea that has stopped.
//
// Visual only: drives no sim, saves nothing (rule 5). SHADER CAUTIONS honoured (this project has
// lost hours to magenta shaders): no operator characters in Property display strings; no loops and
// no [unroll] over a runtime bound; globals declared OUTSIDE the per-material CBUFFER and the
// per-renderer values outside it too; force-compiled headless by TidalFaceShaderCompileGuardTests.
Shader "HiddenHarbours/TidalFace"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Written per renderer by TidalFaceWaterline. See the header for the packing; w is 0 until a
        // publisher has configured this face, and at 0 nothing is clipped.
        [HideInInspector] _HHFaceTide ("Face lip y and elevation and height scale and on", Vector) = (0,0,0,0)

        // Legacy properties, kept exactly as URP's own sprite shader keeps them, so a material using
        // this can gracefully fall back to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                // The fragment's WORLD Y, which is the axis the whole rule is stated on: this camera
                // draws the ground unforeshortened in y (a flat quad's world y IS its screen y), so a
                // world-y threshold is a screen row. TEXCOORD4 because COMMON_2D_OUTPUTS itself takes
                // 2 and 3 under DEBUG_DISPLAY.
                float faceWorldY : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // THE DRAWN WATERLINE, published by WaterSurface for everything that meets the sea without
            // being it: x = the eased sea level in metres above the game's datum, w = 1 once published.
            // All-zero is the unset state and means there is no sea here.
            float4 _HHSeaLevelWorld;

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            // Per RENDERER (MaterialPropertyBlock), deliberately outside UnityPerMaterial: one wharf
            // draws six runs of face off one material and each run stands on its own lip, so folding
            // this into the batched block would cut every wall at one wall's waterline.
            float4 _HHFaceTide;

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                o.faceWorldY = TransformObjectToWorld(input.positionOS).y;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // THE WATERLINE, and the only lines that are not URP's. Guarded on BOTH flags: an
                // unconfigured face and a scene with no published sea must each draw the shipped
                // picture rather than a wall cut at zero.
                if (_HHFaceTide.w > 0.5 && _HHSeaLevelWorld.w > 0.5)
                {
                    float waterlineWorldY =
                        _HHFaceTide.x + (_HHSeaLevelWorld.x - _HHFaceTide.y) * _HHFaceTide.z;
                    if (input.faceWorldY < waterlineWorldY) discard;
                }

                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
