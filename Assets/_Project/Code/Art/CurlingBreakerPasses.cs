using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace HiddenHarbours.Art
{
    /// <summary>What one camera records for the curl: nothing, or a handoff mask of this size and the sheet.</summary>
    public readonly struct CurlPassPlan
    {
        public static readonly CurlPassPlan None = default;

        public readonly bool Record;
        public readonly int MaskWidth;
        public readonly int MaskHeight;

        public CurlPassPlan(bool record, int maskWidth, int maskHeight)
        {
            Record = record;
            MaskWidth = maskWidth;
            MaskHeight = maskHeight;
        }
    }

    /// <summary>
    /// <b>ADR 0040 B2 — the curl's two passes, recorded by <see cref="IsoFacetHullFeature"/> around its
    /// displaced water pass.</b> The MASK pass (before the water) draws each live curl's handoff band
    /// into a reduced R8 target and raises <c>_HHCurlEnabled</c>; the water fades its flat lip and
    /// barrel under it, and only there. The SHEET pass (after the water) draws the curl into the
    /// water's own colour and depth targets, so a hull in front of it hides it and a hull behind it
    /// is hidden, then lowers the flag. With the master at 0, or no live curl, neither pass is
    /// recorded and the flag stays at the 0 the feature lowers it to for every camera.
    /// </summary>
    public static class CurlingBreakerPasses
    {
        public static readonly int EnabledId = Shader.PropertyToID("_HHCurlEnabled");
        public static readonly int HandoffTexId = Shader.PropertyToID("_HHCurlHandoffTex");
        public static readonly int HandoffScaleBiasId = Shader.PropertyToID("_HHCurlHandoffScaleBias");

        /// <summary>The curl shader's pass indices (its pass order).</summary>
        public const int MaskPass = 0;
        public const int SheetPass = 1;

        /// <summary>The curl tunables in force: the loaded config's, else the shipped default.</summary>
        public static CurlingBreakerSettings Settings
            => GameServices.Config != null ? GameServices.Config.CurlingBreakers : CurlingBreakerSettings.Default;

        /// <summary>What a camera <paramref name="width"/> × <paramref name="height"/> records. Pure,
        /// so fixtures can pin the zero dial without a pipeline.</summary>
        public static CurlPassPlan Plan(in CurlingBreakerSettings settings, int liveCurls, int width, int height)
        {
            if (!settings.Enabled || !settings.IsValid || liveCurls <= 0 || width <= 0 || height <= 0)
                return CurlPassPlan.None;
            return new CurlPassPlan(true,
                Mathf.Max(1, Mathf.RoundToInt(width * settings.MaskScale)),
                Mathf.Max(1, Mathf.RoundToInt(height * settings.MaskScale)));
        }

        /// <summary>Lowers the water's handoff flag (every camera, before anything records).</summary>
        public static void Lower() => Shader.SetGlobalFloat(EnabledId, 0f);

        /// <summary>Records the handoff mask for a water target <paramref name="width"/> ×
        /// <paramref name="height"/>; an invalid handle when the plan records nothing.</summary>
        internal static TextureHandle RecordMask(RenderGraph renderGraph, int width, int height,
                                                 ProfilingSampler sampler)
        {
            CurlPassPlan plan = Plan(Settings, CurlingBreakerRegistry.LiveCurls, width, height);
            if (!plan.Record) return TextureHandle.nullHandle;

            TextureHandle mask = renderGraph.CreateTexture(new TextureDesc(plan.MaskWidth, plan.MaskHeight)
            {
                name = "_HHCurlHandoffTex",
                format = GraphicsFormat.R8_UNorm,
                clearBuffer = true,
                clearColor = Color.black,   // 0 = the flat lip draws as before
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
            });

            using var builder = renderGraph.AddRasterRenderPass<CurlPassData>(
                "HH Curl Handoff Mask", out CurlPassData passData, sampler);
            passData.Pass = MaskPass;
            // The water reads the mask by its own pixel position: one water pixel is 1/size of it.
            passData.ScaleBias = new Vector4(1f / width, 1f / height, 0f, 0f);
            builder.SetRenderAttachment(mask, 0);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);
            builder.SetGlobalTextureAfterPass(mask, HandoffTexId);
            builder.SetRenderFunc((CurlPassData data, RasterGraphContext ctx) =>
            {
                DrawLive(ctx.cmd, data.Pass);
                ctx.cmd.SetGlobalVector(HandoffScaleBiasId, data.ScaleBias);
                ctx.cmd.SetGlobalFloat(EnabledId, 1f);
            });
            return mask;
        }

        /// <summary>Records the sheet into the water's colour and depth, after the water pass. Does
        /// nothing when <paramref name="mask"/> was not recorded.</summary>
        internal static void RecordSheet(RenderGraph renderGraph, TextureHandle mask, TextureHandle colour,
                                         TextureHandle depth, TextureHandle guard, ProfilingSampler sampler)
        {
            if (!mask.IsValid()) return;

            using var builder = renderGraph.AddRasterRenderPass<CurlPassData>(
                "HH Curl Sheet", out CurlPassData passData, sampler);
            passData.Pass = SheetPass;
            // The sheet stays out of a hull's interior exactly as the water does.
            if (guard.IsValid()) builder.UseTexture(guard, AccessFlags.Read);
            builder.SetRenderAttachment(colour, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc((CurlPassData data, RasterGraphContext ctx) =>
            {
                DrawLive(ctx.cmd, data.Pass);
                ctx.cmd.SetGlobalFloat(EnabledId, 0f);
            });
        }

        private static void DrawLive(RasterCommandBuffer cmd, int pass)
        {
            for (int h = 0; h < CurlingBreakerRegistry.Count; h++)
            {
                CurlingBreakerRegion host = CurlingBreakerRegistry.Get(h);
                if (host == null || host.Pool == null || host.Material == null) continue;
                for (int s = 0; s < CurlingBreakerPool.Capacity; s++)
                {
                    CurlingBreakerPool.Slot slot = host.Pool[s];
                    if (!slot.Live) continue;
                    Mesh mesh = pass == MaskPass ? slot.Mask : slot.Sheet;
                    if (mesh != null) cmd.DrawMesh(mesh, Matrix4x4.identity, host.Material, 0, pass);
                }
            }
        }

        private sealed class CurlPassData
        {
            public int Pass;
            public Vector4 ScaleBias;
        }
    }
}
