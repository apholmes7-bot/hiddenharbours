using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Route 1 of the figure pixel look</b> (<c>docs/design/figure-pixel-target.md</c> §3, behind
    /// <see cref="GameConfig.FigurePixelTarget"/>, ship default OFF): the mesh figures drawn into a
    /// target of their own at one texel per art pixel, then laid back over the screen with hard edges.
    ///
    /// <para><b>Which renderers are figures.</b> A figure's own facet material is moved to
    /// <see cref="FigureRenderQueue"/> (<see cref="Sync"/>, every LateUpdate), and the hull feature's
    /// full-size facet list then filters <see cref="SceneQueue"/> — so the figures leave the full-size
    /// picture without their renderer, layer or scene changing, and the guard and reflection passes
    /// (which never filter by queue) keep them exactly as before. OFF puts the queue back.</para>
    ///
    /// <para><b>The five passes</b>, recorded by the hull feature between the water and the facets,
    /// all under its "HH IsoFacet Hulls" sampler:</para>
    /// <list type="number">
    ///   <item><b>Depth</b> — the water's hardware depth (the shared private z-buffer, written by the
    ///   water before any hull) read at the screen pixel under each texel's centre, so a figure below
    ///   the lifted sea is still hidden by it. Only while the displaced water draws.</item>
    ///   <item><b>Facets</b> — through the camera's projection moved onto the texels
    ///   (<see cref="FigurePixelGrid.TargetProjection"/>, depth rows unchanged): the figures first,
    ///   writing stencil 1; then every other facet renderer and the deck list as CONTEXT, writing
    ///   stencil bit 2. Context is what keeps the figure honest — her contour and ink against her own
    ///   deck, and the house that stands in front of her.</item>
    ///   <item><b>Resolve</b> — the hulls' own resolve shader, unchanged, on its own material; the
    ///   legacy flood held off (it would ring the context hulls), the figure ink as the frame has it.</item>
    ///   <item><b>Coverage</b> — 1 on each texel where a figure shows (stencil 1): her visible art
    ///   pixels, at texel size.</item>
    ///   <item><b>Export</b> — a stencil-tested copy into a persistent per-camera target bound as
    ///   <c>_HHFigureScreenTex</c>. Stencil 0 (empty, or her ink ring) and 1 (her) are exported; 3
    ///   (her, covered by context) only on a texel that shares an edge with one where she shows — the
    ///   house's own texel squares off the edge beside her visible pixels, one art pixel deep (the
    ///   owner's D4 of 10-15), and deeper under the house nothing is exported, so the full-size house
    ///   draws over her hidden body exactly as OFF does (the owner's ruling of 10-16); 2 (context
    ///   alone) never is, so <b>no hull pixel away from a figure is replaced</b>.</item>
    /// </list>
    ///
    /// <para><b>Re-entry.</b> Each hull overlay, and each ashore figure's cell overlay, reads
    /// <c>_HHFigureScreenTex</c> through <c>_HHFigurePixelMap</c> <c>(offset x, offset y, 1/m, m)</c>
    /// and takes the texel in place of its own pixel wherever the texel is not empty; its existing id
    /// test then keeps or drops it. So the art-pixel image sorts exactly where the full-size one did.
    /// The map is ZERO for every camera this did not run for (<see cref="Unpublish"/> first thing in
    /// every <c>AddRenderPasses</c>), and zero is the overlay's untouched path.</para>
    /// </summary>
    public static class IsoFacetFigureTarget
    {
        /// <summary>The queue a figure's facet material sits at while the look is ON: the top of the
        /// range, which no hull, deck or prop material uses.</summary>
        public const int FigureRenderQueue = 5000;

        /// <summary>The figures, alone.</summary>
        public static readonly RenderQueueRange FigureQueue = new RenderQueueRange(FigureRenderQueue, FigureRenderQueue);

        /// <summary>Everything else: the full-size facet list's filter while the figures draw small.</summary>
        public static readonly RenderQueueRange SceneQueue = new RenderQueueRange(0, FigureRenderQueue - 1);

        /// <summary>The depth-transfer and export shader (found by name, as the foam transport is).</summary>
        public const string ShaderName = "Hidden/HiddenHarbours/FigurePixelTarget";

        /// <summary><see cref="ShaderName"/>'s passes.</summary>
        public const int DepthPass = 0, ExportPass = 1, CoveragePass = 2, EdgePass = 3;

        /// <summary>The stencil a figure writes, and the bit context writes.</summary>
        public const int FigureStencil = 1, ContextStencil = 2;

        public static readonly int ScreenTexId = Shader.PropertyToID("_HHFigureScreenTex");
        public static readonly int PixelMapId = Shader.PropertyToID("_HHFigurePixelMap");
        internal static readonly int SceneDepthId = Shader.PropertyToID("_HHFigureSceneDepth");
        internal static readonly int LowTexId = Shader.PropertyToID("_HHFigureLowTex");
        internal static readonly int CoverageTexId = Shader.PropertyToID("_HHFigureCoverageTex");
        private static readonly int s_ScreenParamsId = Shader.PropertyToID("_ScreenParams");

        private static int s_LiveFrame = -2;
        private static bool s_MapClean;
        private static Texture2D s_Clear;

        /// <summary>True when a visible figure synced this frame or the last while the look was ON. An
        /// ashore figure can hold an id while hidden, so the registry's count is not enough.</summary>
        public static bool FiguresLive => Time.frameCount - s_LiveFrame <= 1;

        /// <summary>Forget the last live frame, so <see cref="FiguresLive"/> reads false until a visible
        /// figure syncs again (tests start from it).</summary>
        internal static void ResetLive() => s_LiveFrame = -2;

        /// <summary>
        /// A figure's per-frame hand-in: ON moves its facet material to <see cref="FigureRenderQueue"/>
        /// and, when <paramref name="visible"/>, marks the frame live; OFF moves it back to the shader's
        /// own queue (and leaves any other explicit queue alone). Allocation-free.
        /// </summary>
        public static void Sync(Material facetMaterial, bool visible)
        {
            if (facetMaterial == null) return;
            if (GameServices.FigurePixelTarget)
            {
                if (facetMaterial.renderQueue != FigureRenderQueue) facetMaterial.renderQueue = FigureRenderQueue;
                if (visible) s_LiveFrame = Time.frameCount;
            }
            else if (facetMaterial.renderQueue == FigureRenderQueue)
            {
                facetMaterial.renderQueue = -1;
            }
        }

        /// <summary>Zero <c>_HHFigurePixelMap</c>: the overlays' untouched path. A no-op when already
        /// zero, so it is free to call per camera per frame (the first call also clears a map a domain
        /// reload left on the GPU).</summary>
        public static void Unpublish()
        {
            if (s_MapClean) return;
            Shader.SetGlobalVector(PixelMapId, Vector4.zero);
            s_MapClean = true;
        }

        internal static void Publish(Vector4 map)
        {
            Shader.SetGlobalVector(PixelMapId, map);
            s_MapClean = false;
        }

        /// <summary>Bind a 1×1 CLEAR texture as <c>_HHFigureScreenTex</c>. Done whenever a target is
        /// made or resized: until this frame's export binds the new one, the reflection pass would
        /// otherwise read a released texture, whose grey placeholder (alpha 128/255) IS a hull id.</summary>
        internal static void BindClearFallback()
        {
            if (s_Clear == null)
            {
                s_Clear = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
                {
                    name = "HH Figure Target Clear",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                };
                s_Clear.SetPixel(0, 0, Color.clear);
                s_Clear.Apply(false, true);
            }
            Shader.SetGlobalTexture(ScreenTexId, s_Clear);
        }

        /// <summary>The per-camera state and recording. Owned by the hull feature's pass.</summary>
        internal sealed class Recorder : IDisposable
        {
            private static readonly ShaderTagId s_FacetTag = new ShaderTagId("HHHullFacet");
            private static readonly ShaderTagId s_DeckTag = new ShaderTagId("HHHullDeck");
            private static readonly ShaderTagId[] s_TagValues = { ShaderTagId.none };
            private static readonly RenderStateBlock[] s_Blocks = new RenderStateBlock[1];

            private readonly IsoFacetHullFeature.HullPass.TargetAllocator _allocate;
            private readonly Dictionary<EntityId, RTHandle> _targets = new Dictionary<EntityId, RTHandle>();
            private Material _resolve;
            private Material _target;
            private bool _shaderMissing;

            internal Recorder(IsoFacetHullFeature.HullPass.TargetAllocator allocate) => _allocate = allocate;

            private sealed class DepthData
            {
                public Material Material;
                public TextureHandle SceneDepth;
            }

            private sealed class FacetData
            {
                public RendererListHandle Figures, Context, Decks;
                public Matrix4x4 View, LowProjection, CameraProjection;
                public Vector4 LowParams, ScreenParams;
            }

            private sealed class ResolveData
            {
                public Material Material;
                public TextureHandle Facet, Dark, Key, Depth;
                public Vector4 FigureInk;
            }

            private sealed class CoverageData
            {
                public Material Material;
            }

            private sealed class ExportData
            {
                public Material Material;
                public TextureHandle Low, Coverage;
            }

            /// <summary>
            /// Record the five passes for this camera, or nothing. True only when they were recorded:
            /// the caller then filters its own facet list to <see cref="SceneQueue"/>. False leaves the
            /// caller's recording exactly as it was before this class existed.
            /// </summary>
            internal bool Record(RenderGraph graph, UniversalCameraData cameraData, UniversalRenderingData renderingData,
                                 TextureHandle sceneDepth, bool sceneDepthLive, int screenWidth, int screenHeight,
                                 Material sharedResolve, ProfilingSampler sampler)
            {
                if (!GameServices.FigurePixelTarget || !FiguresLive || sharedResolve == null || _shaderMissing)
                    return false;
                Camera camera = cameraData.camera;
                if (camera == null || !camera.orthographic || cameraData.renderType != CameraRenderType.Base ||
                    cameraData.xr.enabled)
                    return false;
                // A pixel perfect camera that draws through its own offscreen target publishes THAT size as
                // _ScreenParams, which the facet pass below could not put back. Not this project's set-up.
                if (camera.TryGetComponent(out PixelPerfectCamera ppc) && ppc.enabled &&
                    (ppc.cropFrame != PixelPerfectCamera.CropFrame.None ||
                     ppc.gridSnapping == PixelPerfectCamera.GridSnapping.UpscaleRenderTexture))
                    return false;

                Matrix4x4 view = cameraData.GetViewMatrix();
                Matrix4x4 projection = cameraData.GetProjectionMatrix();
                if (!FigurePixelGrid.TryPlanCamera(screenWidth, screenHeight, view, projection, out FigurePixelPlan plan))
                    return false;
                if (!EnsureMaterials(sharedResolve)) return false;
                Publish(plan.PixelMap);

                float cameraWidth = camera.pixelWidth, cameraHeight = camera.pixelHeight;
                TextureHandle lowDepth = graph.CreateTexture(new TextureDesc(plan.Width, plan.Height)
                {
                    name = "_HHFigureZ",
                    format = GraphicsFormatUtility.GetDepthStencilFormat(32, 8),
                    clearBuffer = true,
                    msaaSamples = MSAASamples.None,
                });

                // ---- the water's depth, at each texel's sample ------------------------------------
                if (sceneDepthLive && sceneDepth.IsValid())
                {
                    using var builder = graph.AddRasterRenderPass<DepthData>(
                        "HH Figure Target Depth", out DepthData passData, sampler);
                    passData.Material = _target;
                    passData.SceneDepth = sceneDepth;
                    builder.UseTexture(sceneDepth, AccessFlags.Read);
                    builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.Write);
                    builder.SetRenderFunc(static (DepthData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetTexture(SceneDepthId, (RTHandle)data.SceneDepth);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, DepthPass);
                    });
                }

                // ---- the figures, then their context, at one texel per art pixel -------------------
                TextureHandle facet = MakeColorTarget(graph, plan.Width, plan.Height, "_HHFigureFacetTex");
                TextureHandle dark = MakeColorTarget(graph, plan.Width, plan.Height, "_HHFigureDarkTex");
                TextureHandle key = MakeColorTarget(graph, plan.Width, plan.Height, "_HHFigureKeyTex");
                TextureHandle trueDepth = graph.CreateTexture(new TextureDesc(plan.Width, plan.Height)
                {
                    name = "_HHFigureDepthTex",
                    format = GraphicsFormat.R32_SFloat,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    msaaSamples = MSAASamples.None,
                });
                using (var builder = graph.AddRasterRenderPass<FacetData>(
                           "HH Figure Target Facets", out FacetData passData, sampler))
                {
                    var sorting = new SortingSettings(camera) { criteria = SortingCriteria.None };
                    var facets = new DrawingSettings(s_FacetTag, sorting) { perObjectData = PerObjectData.None };
                    var decks = new DrawingSettings(s_DeckTag, sorting) { perObjectData = PerObjectData.None };
                    passData.Figures = StencilledList(graph, renderingData.cullResults, facets, FigureQueue,
                                                      FigureStencil, 0xFF);
                    passData.Context = StencilledList(graph, renderingData.cullResults, facets, SceneQueue,
                                                      ContextStencil, ContextStencil);
                    passData.Decks = StencilledList(graph, renderingData.cullResults, decks, SceneQueue,
                                                    ContextStencil, ContextStencil);
                    passData.View = view;
                    passData.LowProjection = FigurePixelGrid.TargetProjection(projection, view, in plan);
                    passData.CameraProjection = projection;
                    passData.LowParams = new Vector4(plan.Width, plan.Height, 1f + 1f / plan.Width, 1f + 1f / plan.Height);
                    passData.ScreenParams = new Vector4(cameraWidth, cameraHeight, 1f + 1f / cameraWidth, 1f + 1f / cameraHeight);

                    builder.UseRendererList(passData.Figures);
                    builder.UseRendererList(passData.Context);
                    builder.UseRendererList(passData.Decks);
                    builder.SetRenderAttachment(facet, 0);
                    builder.SetRenderAttachment(dark, 1);
                    builder.SetRenderAttachment(key, 2);
                    builder.SetRenderAttachment(trueDepth, 3);
                    builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.ReadWrite);
                    // The camera's matrices and _ScreenParams (the figure head snap reads it) are
                    // swapped for the target's, and put back as URP set them, inside this pass alone.
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (FacetData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetViewProjectionMatrices(data.View, data.LowProjection);
                        ctx.cmd.SetGlobalVector(s_ScreenParamsId, data.LowParams);
                        ctx.cmd.DrawRendererList(data.Figures);
                        ctx.cmd.DrawRendererList(data.Context);
                        ctx.cmd.DrawRendererList(data.Decks);
                        ctx.cmd.SetViewProjectionMatrices(data.View, data.CameraProjection);
                        ctx.cmd.SetGlobalVector(s_ScreenParamsId, data.ScreenParams);
                    });
                }

                // ---- the hulls' own resolve, at that size --------------------------------------------
                TextureHandle low = MakeColorTarget(graph, plan.Width, plan.Height, "_HHFigureLowTex");
                using (var builder = graph.AddRasterRenderPass<ResolveData>(
                           "HH Figure Target Resolve", out ResolveData passData, sampler))
                {
                    passData.Material = _resolve;
                    passData.Facet = facet;
                    passData.Dark = dark;
                    passData.Key = key;
                    passData.Depth = trueDepth;
                    passData.FigureInk = IsoFacetFigureInk.Value;
                    builder.UseTexture(facet, AccessFlags.Read);
                    builder.UseTexture(dark, AccessFlags.Read);
                    builder.UseTexture(key, AccessFlags.Read);
                    builder.UseTexture(trueDepth, AccessFlags.Read);
                    builder.SetRenderAttachment(low, 0);
                    builder.SetRenderFunc(static (ResolveData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetTexture(IsoFacetShaderIds.FacetTex, (RTHandle)data.Facet);
                        data.Material.SetTexture(IsoFacetShaderIds.DarkTex, (RTHandle)data.Dark);
                        data.Material.SetTexture(IsoFacetShaderIds.KeyTex, (RTHandle)data.Key);
                        data.Material.SetTexture(IsoFacetShaderIds.DepthTex, (RTHandle)data.Depth);
                        IsoFacetKeylineGate.Apply(data.Material, false);
                        IsoFacetFigureInk.Apply(data.Material, data.FigureInk);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                    });
                }

                // ---- where a figure shows (stencil 1), for the edge below ------------------------------
                TextureHandle coverage = graph.CreateTexture(new TextureDesc(plan.Width, plan.Height)
                {
                    name = "_HHFigureCoverageTex",
                    format = GraphicsFormat.R8_UNorm,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    msaaSamples = MSAASamples.None,
                });
                using (var builder = graph.AddRasterRenderPass<CoverageData>(
                           "HH Figure Target Coverage", out CoverageData passData, sampler))
                {
                    passData.Material = _target;
                    builder.SetRenderAttachment(coverage, 0);
                    builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.Read);
                    builder.SetRenderFunc(static (CoverageData data, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, CoveragePass));
                }

                // ---- the figures (and the edge beside them) out to the overlays ------------------------
                RTHandle target = GetTarget(camera.GetEntityId(), plan.Width, plan.Height);
                TextureHandle exported = graph.ImportTexture(target, new ImportResourceParams
                {
                    clearOnFirstUse = true,
                    clearColor = Color.clear,
                    discardOnLastUse = false,
                });
                using (var builder = graph.AddRasterRenderPass<ExportData>(
                           "HH Figure Target Export", out ExportData passData, sampler))
                {
                    passData.Material = _target;
                    passData.Low = low;
                    passData.Coverage = coverage;
                    builder.UseTexture(low, AccessFlags.Read);
                    builder.UseTexture(coverage, AccessFlags.Read);
                    builder.SetRenderAttachment(exported, 0);
                    builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.Read);
                    // The readers are the in-scene overlays, which the graph cannot see.
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(exported, ScreenTexId);
                    builder.SetRenderFunc(static (ExportData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetTexture(LowTexId, (RTHandle)data.Low);
                        data.Material.SetTexture(CoverageTexId, (RTHandle)data.Coverage);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, ExportPass);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.Material, EdgePass);
                    });
                }
                return true;
            }

            public void Dispose()
            {
                foreach (var kv in _targets)
                    kv.Value?.Release();
                _targets.Clear();
                CoreUtils.Destroy(_resolve);
                _resolve = null;
                CoreUtils.Destroy(_target);
                _target = null;
                Unpublish();
            }

            private bool EnsureMaterials(Material sharedResolve)
            {
                // Its OWN resolve material: the hulls' resolve sets its textures in its render func, and
                // two passes sharing one material would both draw with whichever set them last.
                if (_resolve == null) _resolve = CoreUtils.CreateEngineMaterial(sharedResolve.shader);
                if (_target != null) return true;
                Shader shader = Shader.Find(ShaderName);
                if (shader == null || !shader.isSupported)
                {
                    _shaderMissing = true;
                    Debug.LogError($"[IsoFacetFigureTarget] {ShaderName} is missing or unsupported; " +
                                   "the figures keep drawing at screen size.");
                    return false;
                }
                _target = CoreUtils.CreateEngineMaterial(shader);
                return true;
            }

            private RTHandle GetTarget(EntityId cameraId, int w, int h)
            {
                _targets.TryGetValue(cameraId, out RTHandle handle);
                bool fresh = handle == null || handle.rt == null || handle.rt.width != w || handle.rt.height != h;
                var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
                {
                    sRGB = true,
                    msaaSamples = 1,
                };
                _allocate(ref handle, desc, "_HHFigureScreenTex");
                _targets[cameraId] = handle;
                if (fresh) BindClearFallback();
                return handle;
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

            // URP's own pattern for a renderer list with a render-state override
            // (RenderingUtils.CreateRendererListWithRenderStateBlock): one block, every tag. The graph
            // keeps these params and builds the list when it compiles, later this frame, so the Temp
            // arrays are left to the frame exactly as URP leaves them (native memory, no GC).
            private static RendererListHandle StencilledList(RenderGraph graph, CullingResults cull,
                                                             DrawingSettings drawing, RenderQueueRange queue,
                                                             int reference, int writeMask)
            {
                s_Blocks[0] = new RenderStateBlock(RenderStateMask.Stencil)
                {
                    stencilReference = reference,
                    stencilState = new StencilState(true, 0xFF, (byte)writeMask, CompareFunction.Always,
                                                    StencilOp.Replace, StencilOp.Keep, StencilOp.Keep),
                };
                var param = new RendererListParams(cull, drawing, new FilteringSettings(queue))
                {
                    tagValues = new NativeArray<ShaderTagId>(s_TagValues, Allocator.Temp),
                    stateBlocks = new NativeArray<RenderStateBlock>(s_Blocks, Allocator.Temp),
                    isPassTagName = false,
                };
                return graph.CreateRendererList(param);
            }
        }
    }
}
