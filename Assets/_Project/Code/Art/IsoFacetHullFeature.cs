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
    /// <para><b>Cost when idle: zero.</b> No live <see cref="IsoFacetHullRenderer"/> ⇒
    /// <see cref="AddRenderPasses"/> enqueues nothing.</para>
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
            if (IsoFacetHullRegistry.Count == 0)
                return;   // the zero-cost guarantee — scenes without mesh hulls pay nothing
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (_resolveMaterial == null)
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

            public Material ResolveMaterial;

            // One persistent resolve target per camera: game view, scene view and any inset view
            // differ in size, and thrashing a single handle between sizes would reallocate every
            // frame. Bounded by the number of live cameras; released in Dispose.
            private readonly Dictionary<EntityId, RTHandle> _resolveTargets = new Dictionary<EntityId, RTHandle>();

            public HullPass()
            {
                profilingSampler = new ProfilingSampler("HH IsoFacet Hulls");
            }

            private class FacetPassData
            {
                public RendererListHandle Renderers;
                public RendererListHandle DeckRenderers;
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
                if (w <= 0 || h <= 0 || ResolveMaterial == null) return;

                // ---- transient MRT for the facet draw ------------------------------------
                TextureHandle facet = MakeColorTarget(renderGraph, w, h, "_HHFacetTex");
                TextureHandle dark = MakeColorTarget(renderGraph, w, h, "_HHDarkTex");
                TextureHandle key = MakeColorTarget(renderGraph, w, h, "_HHKeyTex");
                TextureHandle depthVal = renderGraph.CreateTexture(new TextureDesc(w, h)
                {
                    name = "_HHDepthTex",
                    format = GraphicsFormat.R32_SFloat,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    msaaSamples = MSAASamples.None,
                });
                TextureHandle depthBuf = renderGraph.CreateTexture(new TextureDesc(w, h)
                {
                    name = "_HHHullZ",
                    format = GraphicsFormat.None,
                    depthBufferBits = DepthBits.Depth32,
                    clearBuffer = true,
                    msaaSamples = MSAASamples.None,
                });

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

            public void Dispose()
            {
                foreach (var kv in _resolveTargets)
                    kv.Value?.Release();
                _resolveTargets.Clear();
            }
        }
    }
}
