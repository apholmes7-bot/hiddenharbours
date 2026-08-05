using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The URP render passes behind mesh hulls (ADR 0022 phase 3) — the answer to the ADR's
    /// "largest remaining unknown" (URP integration). Two RenderGraph raster passes per camera,
    /// recorded through the 2D renderer's own injection system:
    ///
    /// <list type="number">
    /// <item><b>Facet pass</b> — a renderer list of every LightMode <c>HHHullFacet</c> renderer
    /// (the hull meshes), drawn off-screen into a 4-target MRT (facet colour + hull id, RINDEX
    /// darkened colour, keyline colour, true unbiased view depth) with a PRIVATE depth buffer.
    /// Private because the hull genuinely needs a z-buffer (the rig z-buffers everything) but the
    /// scene's shared depth buffer belongs to painter's-algorithm sprites — a mesh writing it
    /// punches holes in every later sprite that z-tests.</item>
    /// <item><b>Keyline resolve pass</b> — the rig's CPU post-pass as a FULLSCREEN shader: darken
    /// the far side of depth discontinuities by two RINDEX ramp steps, flood a 1 px keyline into
    /// empty pixels touching a hull. ⚠️ ADR 0031 (the keyline retirement): the FLOOD half is
    /// production-gated by <see cref="IsoFacetKeylineGate"/> (GameConfig data, ship default OFF —
    /// the outline is retired from the world style); the depth-edge darkening half always runs —
    /// it is what keeps overlapping parts of one boat readable. Output is a persistent screen-size
    /// texture bound globally as <c>_HHHullScreenTex</c>; each hull's in-scene overlay quad
    /// re-composes its own pixels from it, sorted against sprites like any other renderer.</item>
    /// </list>
    ///
    /// <para><b>Injection point.</b> <see cref="ScriptableRenderPass2D"/> with
    /// <c>renderPassEvent2D = BeforeRenderingSprites</c> on the LOWEST sorting layer: that is
    /// inside the 2D renderer's main record (camera matrices ARE set up — the plain
    /// <c>BeforeRendering</c> event is before <c>SetupRenderGraphCameraProperties</c> and is not
    /// usable for drawing with camera matrices) and before any sprite of any layer draws, so the
    /// resolved texture is ready for the first overlay quad. This was probed against URP 17.5's
    /// <c>Renderer2DRendergraph</c> source, not assumed.</para>
    ///
    /// <para><b>Cost when idle: zero.</b> No live <see cref="IsoFacetHullRenderer"/> and no
    /// active <see cref="DisplacedWaterSurface"/> ⇒ <see cref="AddRenderPasses"/> enqueues
    /// nothing.</para>
    ///
    /// <para><b>The displaced water surface (ADR 0023 phase 2)</b> joins this recording as a third
    /// renderer list (LightMode <c>HHWater</c>, filtered to
    /// <see cref="DisplacedWaterRegistry.RenderingLayer"/>): its OWN colour target
    /// (<c>_HHWaterScreenTex</c>, ARGBHalf, alpha = the water's translucency) drawn against the
    /// SAME private depth buffer as the hulls — the shared z-buffer ADR 0023 §(5)/(6) requires for
    /// the phase-3 waterline-on-the-hull. Deliberately NOT a fifth MRT attachment: the facet
    /// buffers' alpha is the hull-id contract, and water pixels inside them would starve the
    /// keyline flood of the empty neighbours it floods into. The keyline resolve is therefore
    /// byte-identical with or without water.</para>
    ///
    /// <para><b>The waterline on the hull (ADR 0023 phase 3, step 1).</b> When the displaced
    /// surface is active the WATER records FIRST and the hulls z-test against it: a hull fragment
    /// below the lifted surface fails the shared depth test, never enters the facet MRT, and the
    /// sea (composed under every boat by the WaterOverlay's sorting slot) shows where the lower
    /// planking used to be — the waterline climbing the planking as swell passes. This only
    /// compares truthfully because both sides share ONE calibrated iso-depth convention,
    /// <c>z = waterPlaneZ + (groundY − _HeightWorldMin.y)·cos(elev) − heightAboveStillWater·sin(elev)</c>:
    /// the water's vertex stage computes it per vertex (HHWaterDisplaced), and
    /// <see cref="IsoFacetHullRenderer"/> translates each hull's whole frame onto it per frame via
    /// <see cref="DisplacedWaterRegistry.TryGetIsoDepthFrame"/> — a per-hull CONSTANT z offset, so
    /// every intra-hull depth relation (facet self-occlusion, the deck contract, the keyline's
    /// depth-difference darkening) is preserved exactly. With no displaced surface there is no
    /// frame and no offset: the water-off recording is byte-identical to before phase 3.</para>
    ///
    /// <para><b>Deck occupants — the depth buffer is a CONTRACT, not an implementation detail
    /// (owner decision, 2026-07-21).</b> The facet pass's private z-buffer stays attached for a
    /// second renderer list drawn between facet and resolve: any renderer whose shader has a
    /// LightMode <c>HHHullDeck</c> pass is depth-tested PER-PIXEL against the hull geometry (and
    /// writes depth back), so a future character-on-deck billboard is occluded by the wheelhouse
    /// in front of it and occludes the gunwale behind it — no sorting-order hacks. A deck pass
    /// must honour the facet MRT contract: SV_Target0 = (colour, carrying hull's id/255),
    /// SV_Target1 = darkened colour, SV_Target2 = keyline colour, SV_Target3 = TRUE unbiased
    /// view depth (world z, metres), clip-space depth biased however the pass likes.
    /// ⚠️ Convention note for phase 4: write plain <c>ZTest LEqual</c> and clear-depth-1
    /// semantics — this is the STANDARD camera-matrices RenderGraph path, where Unity handles
    /// reversed-Z automatically. The spike's hand-built command buffer needed explicit
    /// <c>ZTest GEqual</c> + depth-clear-0; that convention does NOT carry over here (proved by
    /// the deck-occlusion probe in IsoFacetUrpPassTests, which fails loudly both ways if the
    /// z-direction is wrong). ⚠️ Phase-3 corollary: while a displaced surface is active every
    /// hull's frame is translated into the calibrated iso-depth convention (see the waterline
    /// paragraph below), so a deck occupant that wants to interleave with ITS hull must ride the
    /// hull's frame (parent under the hull renderer, or apply the same
    /// <see cref="DisplacedWaterRegistry.TryGetIsoDepthFrame"/> offset) — a raw world-z≈0 deck
    /// renderer would sit far NEARER than a calibrated hull and win everywhere while the sea is
    /// displaced.</para>
    /// </summary>
    public sealed class IsoFacetHullFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("Hidden/HiddenHarbours/IsoFacetResolve (auto-found when null).")]
        private Shader _resolveShader;

        [SerializeField, Tooltip("Hidden/HiddenHarbours/FoamBufferAdvect (auto-found when null) — " +
                                 "ADR 0027 #6's advected foam buffer.")]
        private Shader _foamAdvectShader;

        [SerializeField, Tooltip("ADR 0027 #6: how much sea the foam buffer covers, metres square, " +
                                 "centred on the camera. Must comfortably exceed the widest framing " +
                                 "(33.75 m down-screen, ~60 m across) or wake scrolls out of the " +
                                 "window while still on screen. The texel resolution is DERIVED from " +
                                 "this (extent x FoamBuffer.CellsPerUnit), so one texel is always " +
                                 "exactly one world cell: 96 m -> 768^2 x R8 x 2 (ping-pong) = 1.1 MB.")]
        [Range(32f, 192f)]
        private float _foamWindowMeters = 96f;

        [SerializeField, Tooltip("ADR 0027 #6: seconds for wake foam to fade to half. The trail's " +
                                 "whole visible lifetime is roughly 4x this. Exponential, so it is " +
                                 "frame-rate independent.")]
        [Range(0.25f, 30f)]
        private float _foamHalfLifeSeconds = 6f;

        [SerializeField, Tooltip("The per-face INTERIOR MASK (ADR 0023). On: a guard pass marks " +
                                 "each pixel whose nearest hull surface is an open interior, and " +
                                 "the displaced sea discards there, so water can never draw inside " +
                                 "a boat. Off: the shipped whole-hull watertight clamp instead. " +
                                 "This is the one-boolean rollback — not a balance knob, so it " +
                                 "lives here and not in GameConfig.")]
        private bool _interiorMask = true;

        /// <summary>
        /// Mirror of <c>_interiorMask</c> for the code that cannot see the feature asset — chiefly
        /// <see cref="IsoFacetHullRenderer.ApplyPose"/>, which must BYPASS the whole-hull watertight
        /// clamp when the mask is doing the job instead (running both would re-introduce exactly the
        /// over-drying the mask exists to cure). Written every AddRenderPasses; defaults to false so
        /// a project with no feature instance keeps the shipped clamp behaviour.
        /// </summary>
        public static bool InteriorMaskEnabled { get; private set; }

        private HullPass _pass;
        private Material _resolveMaterial;
        private Material _foamMaterial;

        public override void Create()
        {
            _pass ??= new HullPass();
            _pass.renderPassEvent2D = RenderPassEvent2D.BeforeRenderingSprites;
            // Also give the classic event a sane value so a non-2D renderer maps it before
            // transparents rather than at the default (AfterRenderingOpaques would be fine too —
            // the pass draws only to its own targets).
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool hulls = IsoFacetHullRegistry.Count > 0;
            // ADR 0023 phase 2: the displaced water surface joins this feature's off-screen
            // recording (its own colour target + the SAME private depth buffer). Active only when
            // a DisplacedWaterSurface is toggled on — the A/B's OFF side records nothing extra.
            bool water = DisplacedWaterRegistry.Count > 0;
            // ADR 0027 #8: object REFLECTIONS join as a fourth filtered renderer list, on the same
            // zero-cost-when-idle contract: Count 0 enqueues nothing, so there is no "reflections
            // off" branch to keep byte-identical — only an absent pass. ⚠️ That idle case is what a
            // scene WITHOUT reflectors relies on; it is not a description of the shipped ones.
            // §26.10's activation opted the fleet in (IsoFacetHullPresentationService.Install) and
            // the trees in (DecorPrefabBuilder / AcadianTreeCatalog), so a live harbour records this
            // pass deliberately, and Water.mat ships _ObjectReflectStrength 0.5 to show it.
            bool reflect = ReflectionRegistry.Count > 0;
            // ADR 0027 #6: the advected foam buffer joins as one persistent ping-ponged target, on
            // the same zero-cost-when-idle contract and with BOTH halves of it enforced — no hull is
            // churning water (every FoamInjector unregisters the moment it is off the water) OR the
            // owner has not dialled the look in (_WakeFoamStrength 0), and nothing is recorded at
            // all. So there is no "foam buffer off" branch to keep byte-identical — only an absent
            // pass.
            //
            // ⚠️ That idle case is what a scene with no churning hull (or the dial at 0) relies on;
            // it is NOT a description of the shipped state, and must not be rewritten into one. The
            // reflections comment above was corrected for exactly that drift (#380): it claimed
            // "nothing in the shipped scenes carries a ReflectiveObject", which was true the day it
            // was written and false the day §26.10 opted the fleet in. AS OF THIS COMMIT no scene
            // carries a FoamInjector and Water.mat ships _WakeFoamStrength 0 — but that is a fact
            // about today's content, not about this gate, and the owner's dial-in will end it.
            bool foam = FoamInjectionRegistry.ShouldRun;
            if (!foam)
                FoamInjectionRegistry.BindIdle();   // never leave a frozen wake bound on the sea
            if (!hulls && !water && !reflect && !foam)
                return;   // the zero-cost guarantee — scenes without mesh hulls, displaced water, reflectors or churn pay nothing
            // CI runs Unity with NO graphics device ("Null Device"), where recording a raster pass
            // can crash the editor outright (exit 1, no results XML) — and phase 4's PlayMode tests
            // legitimately keep a live mesh hull while other fixtures own cameras. Never enqueue
            // there: a null device has no pixels to be right or wrong about.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (hulls && _resolveMaterial == null)
            {
                var shader = _resolveShader != null
                    ? _resolveShader
                    : Shader.Find("Hidden/HiddenHarbours/IsoFacetResolve");
                if (shader == null)
                {
                    Debug.LogError("[IsoFacetHullFeature] Resolve shader missing; mesh hulls will not draw.");
                    return;
                }
                _resolveMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            if (foam && _foamMaterial == null)
            {
                var foamShader = _foamAdvectShader != null
                    ? _foamAdvectShader
                    : Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect");
                if (foamShader == null)
                {
                    Debug.LogError("[IsoFacetHullFeature] Foam advect shader missing; the ADR 0027 #6 " +
                                   "wake buffer will not fill.");
                    foam = false;
                }
                else
                {
                    _foamMaterial = CoreUtils.CreateEngineMaterial(foamShader);
                }
            }

            InteriorMaskEnabled = _interiorMask;
            _pass.renderPassSortingLayerID = LowestSortingLayerId();
            _pass.ResolveMaterial = _resolveMaterial;
            // ADR 0031: the owner's keyline dial, re-read every camera every frame so flipping it in
            // the GameConfig inspector during play converts the whole mesh fleet within a frame.
            _pass.KeylineFlood = IsoFacetKeylineGate.Enabled;
            _pass.DrawHulls = hulls;
            _pass.DrawWater = water;
            _pass.DrawGuard = _interiorMask;
            _pass.DrawReflections = reflect;
            _pass.DrawFoam = foam;
            _pass.FoamMaterial = _foamMaterial;
            _pass.FoamWindowMeters = _foamWindowMeters;
            _pass.FoamHalfLifeSeconds = _foamHalfLifeSeconds;
            renderer.EnqueuePass(_pass);
        }

        // SortingLayer.layers allocates an array per call and AddRenderPasses runs per camera per
        // frame, so the lowest id is cached (rule 7). Layers are edit-time data; in the editor the
        // cache refreshes when the set changes, in a player it cannot change at all.
        private static int s_LowestLayerId;
        private static bool s_LowestLayerCached;

        private static int LowestSortingLayerId()
        {
            if (!s_LowestLayerCached)
            {
                var layers = SortingLayer.layers;
                s_LowestLayerId = layers.Length > 0 ? layers[0].id : 0;
                s_LowestLayerCached = true;
            }
            return s_LowestLayerId;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            CoreUtils.Destroy(_resolveMaterial);
            _resolveMaterial = null;
            CoreUtils.Destroy(_foamMaterial);
            _foamMaterial = null;
        }

        private sealed class HullPass : ScriptableRenderPass2D
        {
            private static readonly ShaderTagId s_FacetTag = new ShaderTagId("HHHullFacet");
            private static readonly ShaderTagId s_DeckTag = new ShaderTagId("HHHullDeck");
            // ADR 0023 phase 2: the displaced water surface's off-screen pass (the water shader's
            // "HHWaterDisplaced" pass). Drawn against the SAME private depth buffer as the hulls.
            private static readonly ShaderTagId s_WaterTag = new ShaderTagId("HHWater");
            // The per-face interior mask's guard pass (ADR 0023) — the facet shader's second pass.
            private static readonly ShaderTagId s_GuardTag = new ShaderTagId("HHHullGuard");
            // ADR 0027 #8: object reflections. The tag says "this renderer KNOWS how to draw a
            // reflection"; ReflectionRegistry.RenderingLayer says "…and it should" (see below).
            private static readonly ShaderTagId s_ReflectTag = new ShaderTagId("HHReflect");

            public Material ResolveMaterial;
            /// <summary>ADR 0027 #6's advect/decay/inject blit material.</summary>
            public Material FoamMaterial;
            /// <summary>Record the advected foam buffer this frame (a hull churning water AND the
            /// owner's look dial above 0)?</summary>
            public bool DrawFoam;
            /// <summary>Metres of sea the foam buffer window covers (square, camera-centred).</summary>
            public float FoamWindowMeters;
            /// <summary>Half-life of buffered foam, seconds.</summary>
            public float FoamHalfLifeSeconds;
            /// <summary>Record the interior-guard pass (the per-face mask)? Only meaningful with
            /// both hulls and water live — with no sea there is nothing to keep out.</summary>
            public bool DrawGuard;
            /// <summary>Record the hull MRT + keyline resolve this frame (any live mesh hull)?</summary>
            public bool DrawHulls;
            /// <summary>ADR 0031: flood the legacy 1 px keyline this frame? From the owner's
            /// GameConfig dial via <see cref="IsoFacetKeylineGate.Enabled"/> — ship default OFF.
            /// Gates only the resolve shader's flood rule; depth-edge darkening always runs.</summary>
            public bool KeylineFlood;
            /// <summary>Record the displaced-water pass this frame (any active DisplacedWaterSurface)?</summary>
            public bool DrawWater;
            /// <summary>Record the object-reflection pass this frame (any live ReflectiveObject)?</summary>
            public bool DrawReflections;

            // One persistent resolve target per camera: game view, scene view and any inset view
            // differ in size, and thrashing a single handle between sizes would reallocate every
            // frame. Bounded by the number of live cameras; released in Dispose.
            private readonly Dictionary<EntityId, RTHandle> _resolveTargets = new Dictionary<EntityId, RTHandle>();
            // The displaced water's persistent per-camera target (same lifetime rules). HDR half
            // float on purpose: the water fragment's night light content is PRE-COMPENSATED far
            // above 1 for the day/night multiply overlay and must survive the round trip through
            // this texture (an 8-bit sRGB target would clamp it and the moon/beam would dim on
            // displaced water only). Alpha carries the water's own translucency.
            private readonly Dictionary<EntityId, RTHandle> _waterTargets = new Dictionary<EntityId, RTHandle>();
            // ADR 0027 #8's persistent per-camera reflection target (same lifetime rules). ARGBHalf
            // for the same reason the water target is: a NIGHT-LIT source writes its colour already
            // divided by the day/night tint (far above 1) so the §11.6 post-grade compensation can
            // recover it, and an 8-bit target would clamp that to nothing.
            private readonly Dictionary<EntityId, RTHandle> _reflectTargets = new Dictionary<EntityId, RTHandle>();
            // ADR 0027 #6's advected foam buffer: a PING-PONG PAIR plus the world window it is
            // anchored to, per camera. Per camera rather than per world because the game view and the
            // scene view frame different places and pan independently — one shared buffer would have
            // them fighting over the window origin every frame.
            private readonly Dictionary<EntityId, FoamState> _foamStates = new Dictionary<EntityId, FoamState>();
            // Staging for the registry's collect call, reused every frame so packing the injection
            // slots allocates nothing (rule 7). The GPU-bound copies live per camera on FoamState.
            private readonly FoamInjection[] _foamInjections = new FoamInjection[FoamBuffer.MaxInjectors];

            /// <summary>
            /// One camera's foam buffer: the two targets it ping-pongs between, and — the part that
            /// matters — the WORLD-CELL-SNAPPED window origin and the sub-cell drift remainder that
            /// together make the whole thing a mark on the sea rather than a screen filter.
            /// </summary>
            private sealed class FoamState
            {
                public RTHandle A, B;
                public bool ReadIsA = true;
                public Vector2 Origin;          // cell-snapped window corner (world m)
                public Vector2 DriftResidual;   // sub-cell drift banked for a later whole-cell step
                public bool Primed;
                public bool NeedsClear = true;  // only true for the first frame after a (re)allocation
                public int LastFrame = -1;
                public int Resolution;
                // This camera's OWN slot arrays — never shared, so a second camera recording between
                // this pass's record and its execute cannot overwrite the deposits.
                public readonly Vector4[] Segments = new Vector4[FoamBuffer.MaxInjectors];
                public readonly Vector4[] Shapes = new Vector4[FoamBuffer.MaxInjectors];

                public RTHandle Read => ReadIsA ? A : B;
                public RTHandle Write => ReadIsA ? B : A;

                public void Release()
                {
                    A?.Release();
                    B?.Release();
                    A = null;
                    B = null;
                }
            }

            public HullPass()
            {
                profilingSampler = new ProfilingSampler("HH IsoFacet Hulls");
            }

            private class FacetPassData
            {
                public RendererListHandle Renderers;
                public RendererListHandle DeckRenderers;
            }

            private class WaterPassData
            {
                public RendererListHandle Renderers;
            }

            private class GuardPassData
            {
                public RendererListHandle Renderers;
            }

            private class ReflectPassData
            {
                public RendererListHandle Renderers;
            }

            private class FoamPassData
            {
                public Material Material;
                public TextureHandle Prev;
                public Vector4 World, Resolution, Shift;
                public float DecayFactor;
                public Vector4[] Segments, Shapes;
            }

            private class ResolvePassData
            {
                public Material Material;
                public TextureHandle Facet, Dark, Key, Depth;
                public Vector4 TexSize;
                /// <summary>ADR 0031: this frame's keyline-gate value, applied to the material in
                /// the render func through <see cref="IsoFacetKeylineGate.Apply"/>.</summary>
                public bool KeylineFlood;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var desc = cameraData.cameraTargetDescriptor;
                int w = desc.width, h = desc.height;
                if (w <= 0 || h <= 0) return;
                bool drawHulls = DrawHulls && ResolveMaterial != null;
                bool drawFoam = DrawFoam && FoamMaterial != null;
                if (!drawHulls && !DrawWater && !DrawReflections && !drawFoam) return;

                // ---- OBJECT REFLECTIONS (ADR 0027 #8) ------------------------------------------
                // A fourth filtered renderer list: every renderer that BOTH has an HHReflect pass
                // AND carries ReflectionRegistry.RenderingLayer, drawn mirrored about its own
                // published ground-contact pivot (ADR 0026) into one ARGBHalf target. The water
                // shader then samples it with the lookup warped by the SAME WaveFieldSample() the
                // hull rides — one place to do the warp, which is the whole reason this beats
                // per-object mirrored duplicates.
                //
                // ⚠️ MEMBERSHIP IS THE LAYER BIT, NOT THE TAG ALONE. The tree shader carries an
                // HHReflect pass, so filtering on the tag alone would sweep EVERY tree in the scene
                // into this list — the exact trap the displaced water hit when the flat Sea sprite
                // shared the water shader. The layer is what keeps the set bounded and deliberate.
                //
                // NO depth buffer and no interior mask: this is the flat mirrored silhouette the
                // ADR's fidelity probe found sufficient at PPU 32. Premultiplied blending inside the
                // pass means overlapping reflectors composite correctly with no sorting contract.
                if (DrawReflections)
                {
                    RTHandle reflectTarget = GetReflectTarget(cameraData.camera.GetEntityId(), w, h);
                    var reflectImport = new ImportResourceParams
                    {
                        clearOnFirstUse = true,
                        clearColor = Color.clear,
                        discardOnLastUse = false,
                    };
                    TextureHandle reflectTex = renderGraph.ImportTexture(reflectTarget, reflectImport);

                    using var builder = renderGraph.AddRasterRenderPass<ReflectPassData>(
                        "HH Object Reflections", out ReflectPassData passData, profilingSampler);
                    var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.None };
                    var drawing = new DrawingSettings(s_ReflectTag, sorting) { perObjectData = PerObjectData.None };
                    var filtering = new FilteringSettings(RenderQueueRange.all, -1,
                                                          ReflectionRegistry.RenderingLayer);
                    passData.Renderers = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, drawing, filtering));

                    builder.UseRendererList(passData.Renderers);
                    builder.SetRenderAttachment(reflectTex, 0);
                    // The consumer is the in-scene water quad, whose read the graph cannot see —
                    // never cull, and publish the result.
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(reflectTex, ReflectionShaderIds.ReflectTex);
                    builder.SetRenderFunc((ReflectPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(data.Renderers);
                    });
                }
                // ---- THE ADVECTED FOAM BUFFER (ADR 0027 #6) -------------------------------------
                // One persistent single-channel target, ping-ponged: scroll the previous frame by a
                // WHOLE number of world cells, decay it exponentially, and inject a capsule of foam
                // along every churning hull's swept segment. The water shader samples the result as a
                // mask that ADDS to the existing foam — so a wake is a mark left on a PLACE IN THE
                // SEA, still drifting downwind after the boat has gone, and a hull merely BOBBING at
                // her mooring churns the water she is working against (the owner's 2026-08-01 ruling).
                //
                // 🔴 THE CELL LAW, and it is the whole item. A render target is screen-space by
                // nature; if these cells were camera-relative the entire trail would crawl under
                // every pan — the one artefact that would make this read as a screen filter rather
                // than as foam on water. So the window origin is snapped to a WORLD cell lattice
                // (FoamBuffer.WorldCellOrigin), and BOTH things that move the content this frame —
                // the camera's window move and the wind/current drift — are reduced to WHOLE world
                // cells before the blit sees them. Whole-cell moves are also exact integer copies, so
                // the wake never blurs into a smudge however long it lives. Twin: FoamBuffer.
                //
                // It records BEFORE the displaced-water pass on purpose: that pass's fragment samples
                // the buffer, so the publish has to have happened.
                if (drawFoam)
                {
                    float extent = Mathf.Clamp(FoamWindowMeters, FoamBuffer.MinExtentMeters, 512f);
                    int res = FoamBuffer.ResolutionForExtent(extent);
                    FoamState state = GetFoamState(cameraData.camera.GetEntityId(), res);

                    // dt ONCE per camera per frame. Two cameras each decay their own buffer once; the
                    // same camera recorded twice in a frame must not decay twice (that would make the
                    // trail's lifetime depend on how many times Unity happened to render it).
                    int frame = Time.frameCount;
                    float dt = state.LastFrame == frame ? 0f : Mathf.Max(0f, Time.deltaTime);
                    state.LastFrame = frame;

                    Vector3 camPos = cameraData.camera.transform.position;
                    Vector2 newOrigin = FoamBuffer.WorldCellOrigin(new Vector2(camPos.x, camPos.y), extent);
                    Vector2Int driftCells = FoamBuffer.AdvectCells(
                        ref state.DriftResidual, FoamInjectionRegistry.DriftVelocity * dt);
                    // Before the first frame there is no previous window to move content between.
                    Vector2Int sourceOffset = state.Primed
                        ? FoamBuffer.SourceOffsetCells(state.Origin, newOrigin, driftCells)
                        : Vector2Int.zero;
                    state.Origin = newOrigin;
                    state.Primed = true;

                    // Pack this frame's deposits into the camera's OWN slot arrays (never a shared
                    // one — the render func runs after recording, and a second camera recording in
                    // between would otherwise overwrite the first camera's slots).
                    int injected = FoamInjectionRegistry.CollectInjections(_foamInjections);
                    for (int i = 0; i < FoamBuffer.MaxInjectors; i++)
                    {
                        if (i < injected)
                        {
                            FoamInjection inj = _foamInjections[i];
                            state.Segments[i] = new Vector4(inj.From.x, inj.From.y, inj.To.x, inj.To.y);
                            state.Shapes[i] = new Vector4(inj.Radius, inj.Amount, 0f, 0f);
                        }
                        else
                        {
                            state.Segments[i] = Vector4.zero;
                            state.Shapes[i] = Vector4.zero;   // amount 0 = the slot adds exactly nothing
                        }
                    }

                    var foamWorld = new Vector4(newOrigin.x, newOrigin.y, extent, 1f / extent);
                    // Only the FIRST frame after a (re)allocation clears: every later frame the read
                    // side IS last frame's foam, and clearing it would be the buffer's whole point
                    // thrown away. The write side never needs a clear — the blit covers every texel.
                    TextureHandle prevTex = renderGraph.ImportTexture(state.Read, new ImportResourceParams
                    {
                        clearOnFirstUse = state.NeedsClear,
                        clearColor = Color.black,
                        discardOnLastUse = false,
                    });
                    TextureHandle nextTex = renderGraph.ImportTexture(state.Write, new ImportResourceParams
                    {
                        clearOnFirstUse = false,
                        discardOnLastUse = false,
                    });
                    state.NeedsClear = false;

                    using (var builder = renderGraph.AddRasterRenderPass<FoamPassData>(
                               "HH Advected Foam Buffer", out FoamPassData passData, profilingSampler))
                    {
                        passData.Material = FoamMaterial;
                        passData.Prev = prevTex;
                        passData.World = foamWorld;
                        passData.Resolution = new Vector4(res, res, 1f / res, 1f / res);
                        passData.Shift = new Vector4(sourceOffset.x, sourceOffset.y, 0f, 0f);
                        passData.DecayFactor = FoamBuffer.DecayFactor(FoamHalfLifeSeconds, dt);
                        passData.Segments = state.Segments;
                        passData.Shapes = state.Shapes;

                        builder.UseTexture(prevTex, AccessFlags.Read);
                        builder.SetRenderAttachment(nextTex, 0);
                        // The consumers are the water passes / the in-scene water quad, whose reads
                        // the graph cannot see — never cull, and publish the result.
                        builder.AllowPassCulling(false);
                        builder.SetGlobalTextureAfterPass(nextTex, FoamShaderIds.BufferTex);
                        builder.SetRenderFunc((FoamPassData data, RasterGraphContext ctx) =>
                        {
                            data.Material.SetTexture(FoamShaderIds.Prev, (RTHandle)data.Prev);
                            data.Material.SetVector(FoamShaderIds.BufferWorld, data.World);
                            data.Material.SetVector(FoamShaderIds.Resolution, data.Resolution);
                            data.Material.SetVector(FoamShaderIds.Shift, data.Shift);
                            data.Material.SetFloat(FoamShaderIds.Decay, data.DecayFactor);
                            data.Material.SetVectorArray(FoamShaderIds.InjectSeg, data.Segments);
                            data.Material.SetVectorArray(FoamShaderIds.InjectShape, data.Shapes);
                            Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                        });
                    }

                    // Where the buffer sits in the world, for every shader that reads it. A plain
                    // global (the _SeaFramingHeight idiom) rather than a per-renderer property: the
                    // window belongs to the camera, not to any one water renderer. z = extent > 0 is
                    // also the "published at all" flag the water shader tests before sampling.
                    Shader.SetGlobalVector(FoamShaderIds.BufferWorld, foamWorld);
                    FoamInjectionRegistry.NotePublished();
                    state.ReadIsA = !state.ReadIsA;   // ping-pong for the next frame
                }

                if (!drawHulls && !DrawWater) return;   // reflection/foam-only frames need nothing below

                // ---- the PRIVATE depth buffer (shared by hulls, deck occupants AND the displaced
                // water — ADR 0023 §(6): one z-buffer in the iso frame is what makes the phase-3
                // waterline-on-the-hull free) --------------------------------------------------
                TextureHandle depthBuf = renderGraph.CreateTexture(new TextureDesc(w, h)
                {
                    name = "_HHHullZ",
                    format = GraphicsFormat.None,
                    depthBufferBits = DepthBits.Depth32,
                    clearBuffer = true,
                    msaaSamples = MSAASamples.None,
                });

                // ---- the displaced water surface (ADR 0023 phase 2) ----------------------
                // Its OWN colour target + the SHARED private depth buffer. Deliberately NOT the
                // hull MRT: the facet buffers' alpha channel is the hull-id contract (the overlay
                // and keyline resolve key on it), while the water's alpha is its translucency —
                // and water pixels inside the facet targets would starve the keyline flood of the
                // empty neighbours it floods into (hull outlines would vanish over water). The
                // keyline resolve therefore stays byte-identical; the water simply shares the
                // z-buffer.
                //
                // ⚠️ ORDER IS THE WATERLINE (ADR 0023 phase 3): the water records BEFORE the
                // hulls, so a hull fragment BELOW the lifted surface loses the shared z-test and
                // never enters the facet MRT. The hull overlay then composes only the EMERGENT
                // hull, the keyline resolve floods the emergent silhouette (the outline rides the
                // waterline, over the water — the sprite fleet's ink-over-water convention), and
                // the water overlay (sorted at the flat sprite's slot, under every boat) shows
                // the sea where planking used to be. Water pixels the hull is nearer than stay in
                // the water target and are covered in-scene by the hull overlay's sort. Hulls can
                // only lose this z-test when they are CALIBRATED into the water's iso-depth
                // convention — see WaterIsoDepthFrame; with no displaced
                // surface active there is no water pass, no frame, and the recording below is
                // byte-identical to the water-off path.
                // ---- the INTERIOR GUARD (ADR 0023: the per-face interior mask) ----------------
                // The cure for the whole-hull watertight clamp's two failure modes at once (owner
                // playtest 2026-07-25: "when the bow faces south you see water at the stern" +
                // "boats ride very high with props not submerged"). The clamp guessed, from a
                // footprint scan and a half-BEAM radius, how far to shove a whole hull toward the
                // camera; a bow-on hull reaches her half-LENGTH, so the stern leaked, and closing
                // that gap would have cost another 0.4–2.5 m of shove on every boat.
                //
                // This asks the GPU instead. One extra rasterisation of the hull geometry writes
                // the per-face INTERIOR flag (UV0.w) into a single-channel target, with ZWrite +
                // LEqual against its OWN depth buffer — so the surviving value at each pixel is the
                // flag of the NEAREST hull surface there. The displaced water's fragment then
                // discards where that reads interior. Per-pixel exact, orientation-free, and it
                // duplicates no wave maths anywhere (a duplicated copy of the lift is precisely
                // what caused the frequency-scale defect this same week).
                //
                // ⚠️ ITS OWN DEPTH BUFFER, never `depthBuf`. Sharing would (a) make the facet pass's
                // depth semantics depend on a second vertex program agreeing bit-for-bit, a silent
                // GPU-only failure CI can never see, and (b) let the water z-test against hull
                // geometry, which would turn the fittings' authored cell overhang (an outboard's
                // cell is ~28 px wider than its hull's) into transparent holes to the seabed rather
                // than the harmless cosmetic clip it is today.
                TextureHandle guardTex = default;
                bool drawGuard = drawHulls && DrawWater && DrawGuard;
                if (drawGuard)
                {
                    guardTex = renderGraph.CreateTexture(new TextureDesc(w, h)
                    {
                        name = "_HHHullGuardTex",
                        format = GraphicsFormat.R8_UNorm,
                        clearBuffer = true,
                        clearColor = Color.black,   // 0 = exterior; see the fallback's doc
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        msaaSamples = MSAASamples.None,
                    });
                    TextureHandle guardDepth = renderGraph.CreateTexture(new TextureDesc(w, h)
                    {
                        name = "_HHHullGuardZ",
                        format = GraphicsFormat.None,
                        depthBufferBits = DepthBits.Depth32,
                        clearBuffer = true,
                        msaaSamples = MSAASamples.None,
                    });

                    using var builder = renderGraph.AddRasterRenderPass<GuardPassData>(
                        "HH Hull Interior Guard", out GuardPassData passData, profilingSampler);
                    var guardSorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.None };
                    var guardDrawing = new DrawingSettings(s_GuardTag, guardSorting) { perObjectData = PerObjectData.None };
                    var guardFiltering = new FilteringSettings(RenderQueueRange.all);
                    passData.Renderers = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, guardDrawing, guardFiltering));

                    builder.UseRendererList(passData.Renderers);
                    builder.SetRenderAttachment(guardTex, 0);
                    builder.SetRenderAttachmentDepth(guardDepth);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(guardTex, IsoFacetShaderIds.GuardTex);
                    builder.SetRenderFunc((GuardPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(data.Renderers);
                    });
                }

                if (DrawWater)
                {
                    RTHandle waterTarget = GetWaterTarget(cameraData.camera.GetEntityId(), w, h);
                    var waterImport = new ImportResourceParams
                    {
                        clearOnFirstUse = true,
                        clearColor = Color.clear,
                        discardOnLastUse = false,
                    };
                    TextureHandle waterTex = renderGraph.ImportTexture(waterTarget, waterImport);

                    using (var builder = renderGraph.AddRasterRenderPass<WaterPassData>(
                               "HH Displaced Water", out WaterPassData passData, profilingSampler))
                    {
                        var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.None };
                        var drawing = new DrawingSettings(s_WaterTag, sorting) { perObjectData = PerObjectData.None };
                        // Membership is the EXPLICIT rendering-layer bit, not the shader tag alone:
                        // the flat Sea sprite (and any preset-derived material) carries the same
                        // shader, and must never ride into the off-screen pass by accident.
                        var filtering = new FilteringSettings(RenderQueueRange.all, -1,
                                                              DisplacedWaterRegistry.RenderingLayer);
                        passData.Renderers = renderGraph.CreateRendererList(
                            new RendererListParams(renderingData.cullResults, drawing, filtering));

                        builder.UseRendererList(passData.Renderers);
                        // The water fragment SAMPLES the guard. Declaring the read makes
                        // guard-before-water a real dependency edge in the graph rather than an
                        // accident of the order these using-blocks happen to be written in.
                        if (drawGuard)
                            builder.UseTexture(guardTex, AccessFlags.Read);
                        builder.SetRenderAttachment(waterTex, 0);
                        builder.SetRenderAttachmentDepth(depthBuf);
                        // The consumer is the in-scene WaterOverlay quad, whose read the graph
                        // cannot see — never cull, and publish the result.
                        builder.AllowPassCulling(false);
                        builder.SetGlobalTextureAfterPass(waterTex, IsoFacetShaderIds.WaterScreenTex);
                        builder.SetRenderFunc((WaterPassData data, RasterGraphContext ctx) =>
                        {
                            ctx.cmd.DrawRendererList(data.Renderers);
                        });
                    }
                }

                // ---- transient MRT for the facet draw ------------------------------------
                TextureHandle facet = default, dark = default, key = default, depthVal = default;
                if (drawHulls)
                {
                    facet = MakeColorTarget(renderGraph, w, h, "_HHFacetTex");
                    dark = MakeColorTarget(renderGraph, w, h, "_HHDarkTex");
                    key = MakeColorTarget(renderGraph, w, h, "_HHKeyTex");
                    depthVal = renderGraph.CreateTexture(new TextureDesc(w, h)
                    {
                        name = "_HHDepthTex",
                        format = GraphicsFormat.R32_SFloat,
                        clearBuffer = true,
                        clearColor = Color.clear,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        msaaSamples = MSAASamples.None,
                    });
                }

                if (drawHulls)
                using (var builder = renderGraph.AddRasterRenderPass<FacetPassData>(
                           "HH Hull Facets", out FacetPassData passData, profilingSampler))
                {
                    var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.None };
                    var drawing = new DrawingSettings(s_FacetTag, sorting) { perObjectData = PerObjectData.None };
                    var filtering = new FilteringSettings(RenderQueueRange.all);
                    passData.Renderers = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, drawing, filtering));
                    // Deck occupants: drawn AFTER the hulls against the SAME private z-buffer, so
                    // they are per-pixel occluded by nearer hull geometry (and occlude farther
                    // geometry). See the feature doc — this is the owner's deck-walking contract.
                    var deckDrawing = new DrawingSettings(s_DeckTag, sorting) { perObjectData = PerObjectData.None };
                    passData.DeckRenderers = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, deckDrawing, filtering));

                    builder.UseRendererList(passData.Renderers);
                    builder.UseRendererList(passData.DeckRenderers);
                    builder.SetRenderAttachment(facet, 0);
                    builder.SetRenderAttachment(dark, 1);
                    builder.SetRenderAttachment(key, 2);
                    builder.SetRenderAttachment(depthVal, 3);
                    builder.SetRenderAttachmentDepth(depthBuf);
                    builder.SetRenderFunc((FacetPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(data.Renderers);
                        ctx.cmd.DrawRendererList(data.DeckRenderers);
                    });
                }

                if (!drawHulls) return;   // water-only frames need no keyline resolve

                // ---- persistent resolve target, imported ---------------------------------
                RTHandle target = GetResolveTarget(cameraData.camera.GetEntityId(), w, h);
                var importParams = new ImportResourceParams
                {
                    clearOnFirstUse = true,
                    clearColor = Color.clear,
                    discardOnLastUse = false,
                };
                TextureHandle resolved = renderGraph.ImportTexture(target, importParams);

                using (var builder = renderGraph.AddRasterRenderPass<ResolvePassData>(
                           "HH Hull Keyline Resolve", out ResolvePassData passData, profilingSampler))
                {
                    passData.Material = ResolveMaterial;
                    passData.Facet = facet;
                    passData.Dark = dark;
                    passData.Key = key;
                    passData.Depth = depthVal;
                    passData.TexSize = new Vector4(w, h, 0, 0);
                    passData.KeylineFlood = KeylineFlood;

                    builder.UseTexture(facet, AccessFlags.Read);
                    builder.UseTexture(dark, AccessFlags.Read);
                    builder.UseTexture(key, AccessFlags.Read);
                    builder.UseTexture(depthVal, AccessFlags.Read);
                    builder.SetRenderAttachment(resolved, 0);
                    // The consumers are ordinary scene renderers (the overlay quads) whose reads
                    // the graph cannot see — never cull this pass, and publish the result.
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(resolved, IsoFacetShaderIds.HullScreenTex);
                    builder.SetRenderFunc((ResolvePassData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetTexture(IsoFacetShaderIds.FacetTex, (RTHandle)data.Facet);
                        data.Material.SetTexture(IsoFacetShaderIds.DarkTex, (RTHandle)data.Dark);
                        data.Material.SetTexture(IsoFacetShaderIds.KeyTex, (RTHandle)data.Key);
                        data.Material.SetTexture(IsoFacetShaderIds.DepthTex, (RTHandle)data.Depth);
                        IsoFacetKeylineGate.Apply(data.Material, data.KeylineFlood);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                    });
                }
            }

            private static TextureHandle MakeColorTarget(RenderGraph graph, int w, int h, string name) =>
                graph.CreateTexture(new TextureDesc(w, h)
                {
                    name = name,
                    format = GraphicsFormat.R8G8B8A8_SRGB,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    msaaSamples = MSAASamples.None,
                });

            private RTHandle GetResolveTarget(EntityId cameraId, int w, int h)
            {
                _resolveTargets.TryGetValue(cameraId, out RTHandle handle);
                var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
                {
                    sRGB = true,
                    msaaSamples = 1,
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref handle, desc,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHHullScreenTex");
                _resolveTargets[cameraId] = handle;
                return handle;
            }

            private RTHandle GetWaterTarget(EntityId cameraId, int w, int h)
            {
                _waterTargets.TryGetValue(cameraId, out RTHandle handle);
                // ARGBHalf (not ARGB32): the water fragment's pre-compensated night light content
                // exceeds 1 and must survive to the overlay's in-scene composite (see the field doc).
                var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 0)
                {
                    sRGB = false,
                    msaaSamples = 1,
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref handle, desc,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHWaterScreenTex");
                _waterTargets[cameraId] = handle;
                return handle;
            }

            private RTHandle GetReflectTarget(EntityId cameraId, int w, int h)
            {
                _reflectTargets.TryGetValue(cameraId, out RTHandle handle);
                // ARGBHalf: a night-lit reflector writes PRE-COMPENSATED colour (divided by the
                // day/night tint) far above 1, exactly as the moon glitter does, and an 8-bit target
                // would clamp it — the lit wheelhouse would go out on the way to the water shader.
                // Camera render resolution + POINT filtering: the RT inherits the pixel grid, and the
                // WORLD-space snap in the water's lookup is what stops it crawling on a pan.
                var descriptor = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 0)
                {
                    sRGB = false,
                    msaaSamples = 1,
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref handle, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHReflectTex");
                _reflectTargets[cameraId] = handle;
                return handle;
            }

            /// <summary>
            /// The camera's foam ping-pong pair, allocated (or re-allocated when the window
            /// resolution changes) to <paramref name="resolution"/> square.
            ///
            /// <para><b>R8, and the resolution is DERIVED, not chosen.</b> One texel is exactly one
            /// world cell by construction (<see cref="FoamBuffer.ResolutionForExtent"/>), so the
            /// pixel grid cannot drift away from the cell law by someone typing a different number.
            /// Point-filtered and clamped: an integer texel move must stay an exact copy. Budget at
            /// the 96 m default — 768² × R8 × 2 = <b>1.1 MB per camera</b>, against the seabed bake's
            /// 1.0 MB and the reflection target's ARGBHalf at camera resolution. A single channel is
            /// all a coverage mask needs, and 256 levels are more than a posterized foam edge can
            /// show (rule 7 — and it keeps the later mobile port affordable).</para>
            /// </summary>
            private FoamState GetFoamState(EntityId cameraId, int resolution)
            {
                if (!_foamStates.TryGetValue(cameraId, out FoamState state))
                {
                    state = new FoamState();
                    _foamStates[cameraId] = state;
                }
                if (state.A != null && state.B != null && state.Resolution == resolution)
                    return state;

                state.Release();
                var descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.R8, 0)
                {
                    sRGB = false,
                    msaaSamples = 1,
                };
                RTHandle a = null, b = null;
                RenderingUtils.ReAllocateHandleIfNeeded(ref a, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHFoamBufferA");
                RenderingUtils.ReAllocateHandleIfNeeded(ref b, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHFoamBufferB");
                state.A = a;
                state.B = b;
                state.Resolution = resolution;
                // A fresh target's contents are undefined, and the window it was anchored to is gone:
                // start from clean water rather than from whatever was in memory.
                state.NeedsClear = true;
                state.Primed = false;
                state.DriftResidual = Vector2.zero;
                return state;
            }

            public void Dispose()
            {
                foreach (var kv in _resolveTargets)
                    kv.Value?.Release();
                _resolveTargets.Clear();
                foreach (var kv in _waterTargets)
                    kv.Value?.Release();
                _waterTargets.Clear();
                foreach (var kv in _reflectTargets)
                    kv.Value?.Release();
                _reflectTargets.Clear();
                foreach (var kv in _foamStates)
                    kv.Value?.Release();
                _foamStates.Clear();
            }
        }
    }

    /// <summary>
    /// ADR 0031 (the keyline retirement): the ONE code path that turns the owner's style decision
    /// (<see cref="GameServices.HullKeylineFlood"/>, ship default OFF) into the resolve shader's
    /// <c>_HHKeylineFlood</c> uniform. The production render func calls <see cref="Apply"/> every
    /// frame; the EditMode gate suite (IsoFacetKeylineGateTests) pins THIS method against a material
    /// built from the REAL resolve shader, with sabotage arms — so the gate cannot silently fork from
    /// what ships. That is the guard-rot lesson applied up front: zero a layer's output through the
    /// mechanism the production path runs (the same shader, the same pass), never a parallel path
    /// that can drift.
    ///
    /// <para>The gate switches ONLY the resolve's rule 2 (the 1 px flood into empty pixels). Rule 1
    /// — the depth-edge darkening that keeps overlapping parts of one object readable — never reads
    /// it, and because the flood was the sole consumer of EMPTY facet pixels, the water share and
    /// every solid pixel are byte-identical whichever way the gate sits. With the gate ON the pass
    /// is verbatim the rigs' shared post-pass, which is what the GPU oracle fixtures force and pin.</para>
    /// </summary>
    public static class IsoFacetKeylineGate
    {
        /// <summary>This frame's gate value — the owner's <c>GameConfig.HullKeylineFlood</c> resolved
        /// through <see cref="GameServices"/> (falls back to the retired-outline default, OFF, when no
        /// config is wired). Read per camera per AddRenderPasses, never cached.</summary>
        public static bool Enabled => GameServices.HullKeylineFlood;

        /// <summary>Write <paramref name="floodEnabled"/> onto <paramref name="resolveMaterial"/> as
        /// <c>_HHKeylineFlood</c> (1 = flood the legacy outline, 0 = retired). The shader property
        /// defaults to 0, so a material this method never touched fails to the SHIPPED style.</summary>
        public static void Apply(Material resolveMaterial, bool floodEnabled) =>
            resolveMaterial.SetFloat(IsoFacetShaderIds.KeylineFlood, floodEnabled ? 1f : 0f);
    }
}
