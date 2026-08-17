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
    /// <b>The fisher's shape, stamped into a screen texture before any sprite draws.</b> One
    /// RenderGraph raster pass per camera: a renderer list of every <c>HHSilhouette</c> renderer
    /// carrying <see cref="SilhouetteRegistry.RenderingLayer"/> (in the shipped game, exactly one — the
    /// hidden child <see cref="SilhouetteMaskSource"/> keeps on the player), drawn into a persistent
    /// single-channel target published globally as <c>_HHSilhouetteMaskTex</c>.
    ///
    /// <para>The foliage shaders then read that texture at their own screen pixel and lerp toward the
    /// owner's tint where it is set (<c>SilhouetteThrough</c> in Include/SpriteLitDecor.hlsl). That is
    /// the whole mechanism: dense woods draw in FRONT of the fisher, opaque and correctly sorted, and
    /// she reads through them as a tinted silhouette — the classic top-down RPG treatment the owner
    /// ruled for on 2026-08-16.</para>
    ///
    /// <para><b>⚠️ ONE DRAW CALL, AT ANY DENSITY — and that is the whole design.</b> The same question
    /// could be answered by masking the FOLIAGE instead, which is how it is usually written up. That
    /// would mean rasterising every tree and shrub a second time, and at forest density the foliage IS
    /// the expensive thing on screen; the cost of the effect would grow with exactly the number this
    /// arc exists to raise. Masking the BODY costs one extra draw of one sprite, forever. Sorting
    /// answers the "in front" half for free — foliage behind the fisher is painted over by her own
    /// sprite, so the tint it received is never seen — and the per-fragment price on the foliage side
    /// is one point-sampled Load, skipped entirely when the dial is 0 (rule 7).</para>
    ///
    /// <para><b>Injection point.</b> <see cref="ScriptableRenderPass2D"/> with
    /// <c>renderPassEvent2D = BeforeRenderingSprites</c> on the LOWEST sorting layer — inside the 2D
    /// renderer's main record (camera matrices ARE set up; the plain <c>BeforeRendering</c> event is
    /// before <c>SetupRenderGraphCameraProperties</c> and cannot draw with camera matrices) and before
    /// any sprite of any layer draws, so the mask is ready for the first tree. This is the same
    /// injection <see cref="IsoFacetHullFeature"/> probed against URP 17.5's
    /// <c>Renderer2DRendergraph</c> source, reused rather than re-derived.</para>
    ///
    /// <para><b>Cost when idle: zero.</b> No live <see cref="SilhouetteMaskSource"/> — the menu, a bare
    /// art scene, every EditMode fixture, any scene with no player — and
    /// <see cref="AddRenderPasses"/> enqueues nothing AND
    /// <see cref="SilhouetteRegistry.BindIdle"/> zeroes the foliage's read strength, so the consumers
    /// stop paying too. Both halves, because a buffer needs a SOURCE and a zero must be a STRENGTH:
    /// the rendering-guards-rot lesson.</para>
    ///
    /// <para><b>Why this is its own feature and not another branch of
    /// <see cref="IsoFacetHullFeature"/>.</b> That feature already carries five unrelated concerns
    /// (hull facets, displaced water, the interior guard, object reflections, the advected foam
    /// buffer) and every one of them is gated on something to do with BOATS. A fisher walking through
    /// woods a mile from the water would have to be threaded through all of it, and the zero-cost
    /// gate — the part most worth keeping honest — would become a six-way disjunction. This pass
    /// shares nothing with those but the injection point.</para>
    /// </summary>
    public sealed class SilhouetteMaskFeature : ScriptableRendererFeature
    {
        private MaskPass _pass;

        public override void Create()
        {
            _pass ??= new MaskPass();
            _pass.renderPassEvent2D = RenderPassEvent2D.BeforeRenderingSprites;
            // Give the classic event a sane value too, so a non-2D renderer maps it before
            // transparents rather than at the default. The pass draws only to its own target.
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // The owner's dial, re-read every camera every frame so tuning the tint in the GameConfig
            // inspector during play converts every tree within a frame — the same live-dial contract
            // IsoFacetKeylineGate has. Written even when the pass is skipped: that is what makes the
            // idle case a zeroed STRENGTH rather than a stale one.
            bool live = SilhouetteRegistry.Count > 0;
            FoliageSilhouetteGate.Apply(live);

            if (!live)
            {
                SilhouetteRegistry.BindIdle();   // never leave a frozen silhouette bound on the trees
                return;                          // the zero-cost guarantee
            }

            // CI runs Unity with NO graphics device ("Null Device"), where recording a raster pass can
            // crash the editor outright (exit 1, no results XML). Never enqueue there: a null device
            // has no pixels to be right or wrong about, and the GPU adjudication happens on the 4060.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            _pass.renderPassSortingLayerID = LowestSortingLayerId();
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
        }

        private sealed class MaskPass : ScriptableRenderPass2D
        {
            private static readonly ShaderTagId s_MaskTag = new ShaderTagId("HHSilhouette");

            // One persistent target per camera: game view, scene view and any inset view differ in
            // size, and thrashing a single handle between sizes would reallocate every frame. Bounded
            // by the number of live cameras; released in Dispose. Persistent rather than transient
            // because the CONSUMERS are ordinary in-scene sprite renderers, whose reads the render
            // graph cannot see — the same reason IsoFacetHullFeature's resolve target is persistent.
            private readonly Dictionary<EntityId, RTHandle> _targets = new Dictionary<EntityId, RTHandle>();

            public MaskPass()
            {
                profilingSampler = new ProfilingSampler("HH Silhouette Mask");
            }

            private class MaskPassData
            {
                public RendererListHandle Renderers;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var desc = cameraData.cameraTargetDescriptor;
                int w = desc.width, h = desc.height;
                if (w <= 0 || h <= 0) return;

                RTHandle target = GetTarget(cameraData.camera.GetEntityId(), w, h);
                var import = new ImportResourceParams
                {
                    clearOnFirstUse = true,
                    clearColor = Color.clear,   // 0 = no body here; see the fallback's doc
                    discardOnLastUse = false,
                };
                TextureHandle maskTex = renderGraph.ImportTexture(target, import);

                using var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                    "HH Silhouette Mask", out MaskPassData passData, profilingSampler);

                var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.None };
                var drawing = new DrawingSettings(s_MaskTag, sorting) { perObjectData = PerObjectData.None };
                // Membership is the EXPLICIT rendering-layer bit, not the shader tag alone. Nothing
                // but the mask material carries HHSilhouette today — and "nothing but" is exactly what
                // the displaced water believed about the water shader before the flat Sea sprite
                // shared it, and what the reflection pass believed before every tree turned out to
                // carry an HHReflect pass. See SilhouetteRegistry.RenderingLayer.
                var filtering = new FilteringSettings(RenderQueueRange.all, -1,
                                                      SilhouetteRegistry.RenderingLayer);
                passData.Renderers = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawing, filtering));

                builder.UseRendererList(passData.Renderers);
                builder.SetRenderAttachment(maskTex, 0);
                // No depth attachment on purpose: the mask is a flat stamp of one sprite, and there is
                // nothing for it to z-test against. Sorting — not depth — is what decides which
                // foliage is in front, and that happens later, in the ordinary sprite queue.
                //
                // The consumers are ordinary scene renderers (every tree and shrub) whose reads the
                // graph cannot see — never cull this pass, and publish the result.
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(maskTex, SilhouetteShaderIds.MaskTex);
                builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.Renderers);
                });
            }

            private RTHandle GetTarget(EntityId cameraId, int w, int h)
            {
                _targets.TryGetValue(cameraId, out RTHandle handle);
                // R8 at camera resolution, point-filtered and clamped. A single channel is all a
                // coverage mask needs; the read is a Load at an integer screen pixel, so filtering
                // never runs and any smoothing would only smear the silhouette off the body. Budget:
                // 1920x1080 x R8 = 2.0 MB per camera, against the hull resolve's ARGB32 at 8.3 MB and
                // the foam buffer's 1.1 MB (rule 7 — and it keeps the later mobile port affordable).
                var descriptor = new RenderTextureDescriptor(w, h, RenderTextureFormat.R8, 0)
                {
                    sRGB = false,
                    msaaSamples = 1,
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref handle, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_HHSilhouetteMaskTex");
                _targets[cameraId] = handle;
                return handle;
            }

            public void Dispose()
            {
                foreach (var kv in _targets)
                    kv.Value?.Release();
                _targets.Clear();
            }
        }
    }

    /// <summary>
    /// The ONE code path that turns the owner's silhouette dial
    /// (<see cref="GameServices.FoliageSilhouette"/>) into the <c>_HHSilhouetteParams</c> global every
    /// foliage fragment reads. Twin of <see cref="IsoFacetKeylineGate"/>, and pinned the same way: the
    /// production render func calls <see cref="Apply"/> every camera every frame, and the EditMode gate
    /// suite drives THIS method rather than a re-statement of it — the guard-rot lesson, applied up
    /// front. Zero a layer's output through the mechanism that ships, never a parallel copy.
    ///
    /// <para><b>The packing is the gate.</b> <c>rgb</c> is the tint, <c>a</c> is the strength — and a
    /// is what the shader branches on. Off (or idle, or unwired) writes <c>a = 0</c>, and the foliage
    /// skips the texture read entirely. So "off" is genuinely free, not merely invisible, and the
    /// shipped default lives in <see cref="GameConfig"/> as a code const so an asset that lags the
    /// code still resolves the intended look.</para>
    /// </summary>
    public static class FoliageSilhouetteGate
    {
        /// <summary>Write this frame's dial onto the global. <paramref name="live"/> false forces the
        /// strength to 0 whatever the config says — there is no body to read, so a non-zero strength
        /// would buy every foliage fragment a texture read that can only return 0.</summary>
        public static void Apply(bool live)
        {
            bool on = live && GameServices.FoliageSilhouetteEnabled;
            if (!on)
            {
                Shader.SetGlobalVector(SilhouetteShaderIds.Params, Vector4.zero);
                return;
            }

            Color tint = GameServices.FoliageSilhouetteTint;
            float strength = Mathf.Clamp01(GameServices.FoliageSilhouetteStrength);
            Shader.SetGlobalVector(SilhouetteShaderIds.Params,
                                   new Vector4(tint.r, tint.g, tint.b, strength));
        }
    }
}
