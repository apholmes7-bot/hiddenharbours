using System.Collections.Generic;
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
    /// empty pixels touching a hull. Output is a persistent screen-size texture bound globally as
    /// <c>_HHHullScreenTex</c>; each hull's in-scene overlay quad re-composes its own pixels from
    /// it, sorted against sprites like any other renderer.</item>
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
    /// z-direction is wrong).</para>
    /// </summary>
    public sealed class IsoFacetHullFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("Hidden/HiddenHarbours/IsoFacetResolve (auto-found when null).")]
        private Shader _resolveShader;

        private HullPass _pass;
        private Material _resolveMaterial;

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
            if (!hulls && !water)
                return;   // the zero-cost guarantee — scenes without mesh hulls or displaced water pay nothing
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

            _pass.renderPassSortingLayerID = LowestSortingLayerId();
            _pass.ResolveMaterial = _resolveMaterial;
            _pass.DrawHulls = hulls;
            _pass.DrawWater = water;
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
        }

        private sealed class HullPass : ScriptableRenderPass2D
        {
            private static readonly ShaderTagId s_FacetTag = new ShaderTagId("HHHullFacet");
            private static readonly ShaderTagId s_DeckTag = new ShaderTagId("HHHullDeck");
            // ADR 0023 phase 2: the displaced water surface's off-screen pass (the water shader's
            // "HHWaterDisplaced" pass). Drawn against the SAME private depth buffer as the hulls.
            private static readonly ShaderTagId s_WaterTag = new ShaderTagId("HHWater");

            public Material ResolveMaterial;
            /// <summary>Record the hull MRT + keyline resolve this frame (any live mesh hull)?</summary>
            public bool DrawHulls;
            /// <summary>Record the displaced-water pass this frame (any active DisplacedWaterSurface)?</summary>
            public bool DrawWater;

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

            private class ResolvePassData
            {
                public Material Material;
                public TextureHandle Facet, Dark, Key, Depth;
                public Vector4 TexSize;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var desc = cameraData.cameraTargetDescriptor;
                int w = desc.width, h = desc.height;
                if (w <= 0 || h <= 0) return;
                bool drawHulls = DrawHulls && ResolveMaterial != null;
                if (!drawHulls && !DrawWater) return;

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

                // ---- the displaced water surface (ADR 0023 phase 2) ----------------------
                // Its OWN colour target + the SHARED private depth buffer. Deliberately NOT the
                // hull MRT: the facet buffers' alpha channel is the hull-id contract (the overlay
                // and keyline resolve key on it), while the water's alpha is its translucency —
                // and water pixels inside the facet targets would starve the keyline flood of the
                // empty neighbours it floods into (hull outlines would vanish over water). The
                // keyline resolve therefore stays byte-identical; the water simply shares the
                // z-buffer, which is the part phase 3's waterline-on-the-hull needs.
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

            public void Dispose()
            {
                foreach (var kv in _resolveTargets)
                    kv.Value?.Release();
                _resolveTargets.Clear();
                foreach (var kv in _waterTargets)
                    kv.Value?.Release();
                _waterTargets.Clear();
            }
        }
    }
}
