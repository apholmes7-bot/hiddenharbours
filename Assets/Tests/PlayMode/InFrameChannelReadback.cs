using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>A plate's eye INSIDE the render: it copies RenderGraph globals out while they are still
    /// live.</b> TESTS ONLY; no production file knows it exists.
    ///
    /// <para><b>Why it must be inside.</b> RenderGraph ends every graph by re-binding each global it
    /// registered to its default black texture (<c>RenderGraph.ClearGlobalBindings</c>, Core RP
    /// 17.5.0), and a transient texture such as <c>_HHHullGuardTex</c> is returned to the pool by then
    /// anyway. So <see cref="Shader.GetGlobalTexture(int)"/> read after <c>Camera.Render()</c> returns,
    /// or at <c>endCameraRendering</c>, sees a 4x4 black texture on every render. That is why
    /// <c>4ade16c7</c>'s instrument read "unknown" on every numeric claim.</para>
    ///
    /// <para><b>Where it sits.</b> A plain <see cref="ScriptableRenderPass"/> at
    /// <see cref="RenderPassEvent.AfterRendering"/>, which the 2D renderer records at its own
    /// <c>AfterRendering</c> point: after the facet block (<c>BeforeRenderingSprites</c>) has published
    /// its globals, and before the graph ends. It is enqueued on ONE camera from
    /// <see cref="RenderPipelineManager.beginCameraRendering"/>, the documented way to inject a pass
    /// from a script. For each watched global it adds one unsafe pass. That pass declares a read of
    /// the global's handle, which keeps a transient texture alive until the copy. It copies the texture
    /// raw with <c>CommandBuffer.CopyTexture</c> into a RenderTexture the test owns, and the copy
    /// outlives the graph.</para>
    ///
    /// <para><b>What it costs.</b> One same-size, same-format copy per watched global per render of the
    /// one armed camera, only while a test has it armed. No shader runs and no render target changes.
    /// Nothing in a build records it.</para>
    ///
    /// <para><b>Its one brittle joint.</b> The graph's handle for a global comes from the INTERNAL
    /// <c>RenderGraph.GetGlobal(int)</c>, read by reflection. If a package update renames it, every
    /// read says so and the plate writes "unknown"; it never guesses.</para>
    /// </summary>
    internal sealed class InFrameChannelReadback : ScriptableRenderPass, IDisposable
    {
        const string PassNamePrefix = "HH Test In-Frame Readback ";

        /// <summary>One watched global, and the test-owned copy of it from the latest render that
        /// copied it.</summary>
        sealed class Channel
        {
            public int PropertyId;
            public string Name;
            public RenderTexture Copy;
            public int CopiedOnRender;
            public string Why;
            public string How;

            public void Release()
            {
                if (Copy == null) return;
                Copy.Release();
                Object.DestroyImmediate(Copy);
                Copy = null;
            }
        }

        sealed class CopyData
        {
            public TextureHandle Source;
            public Channel Into;
            public int Render;
        }

        static readonly MethodInfo s_getGlobal = typeof(RenderGraph).GetMethod(
            "GetGlobal", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);

        readonly List<Channel> _channels = new List<Channel>();
        Camera _camera;
        bool _subscribed;
        string _whyNotEnqueued;

        /// <summary>How many renders of the armed camera have recorded this pass.</summary>
        public int Renders { get; private set; }

        public InFrameChannelReadback()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

        /// <summary>Copy this global out of every render of the armed camera.</summary>
        public void Watch(int propertyId, string name)
        {
            if (_channels.Exists(c => c.PropertyId == propertyId)) return;
            _channels.Add(new Channel { PropertyId = propertyId, Name = name });
        }

        /// <summary>Record the copies in every render of <paramref name="camera"/>, until
        /// <see cref="Dispose"/>.</summary>
        public void Arm(Camera camera)
        {
            _camera = camera;
            if (_subscribed) return;
            RenderPipelineManager.beginCameraRendering += Enqueue;
            _subscribed = true;
        }

        /// <summary>
        /// The test-owned copy of <paramref name="propertyId"/> made during the LATEST render of the armed
        /// camera, with how it was copied in <paramref name="note"/>. Null when that render made no copy,
        /// with the reason in <paramref name="note"/>.
        /// </summary>
        public RenderTexture Latest(int propertyId, out string note)
        {
            Channel ch = _channels.Find(c => c.PropertyId == propertyId);
            if (ch == null) { note = "the in-frame readback does not watch it"; return null; }
            if (!_subscribed || _camera == null) { note = "the in-frame readback is not armed on a camera"; return null; }
            if (Renders == 0)
            {
                note = _whyNotEnqueued ??
                       "no render of the armed camera recorded the in-frame readback (is the pipeline URP on RenderGraph?)";
                return null;
            }
            if (ch.CopiedOnRender != Renders)
            {
                note = $"render {Renders} made no copy: {ch.Why ?? "the copy was not recorded"}";
                return null;
            }
            note = ch.How;
            return ch.Copy;
        }

        public void Dispose()
        {
            if (_subscribed) RenderPipelineManager.beginCameraRendering -= Enqueue;
            _subscribed = false;
            _camera = null;
            foreach (Channel ch in _channels) ch.Release();
        }

        void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera != _camera) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) || data.scriptableRenderer == null)
            {
                _whyNotEnqueued = $"the camera '{camera.name}' has no URP renderer to enqueue the in-frame readback on";
                return;
            }
            data.scriptableRenderer.EnqueuePass(this);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            Renders++;
            foreach (Channel ch in _channels)
            {
                if (s_getGlobal == null)
                {
                    ch.Why = "RenderGraph.GetGlobal(int) is not in this Core RP, so the graph's handle cannot be had";
                    continue;
                }
                var handle = (TextureHandle)s_getGlobal.Invoke(renderGraph, new object[] { ch.PropertyId });
                if (!handle.IsValid())
                {
                    ch.Why = "no pass in this render published it (the facet block did not record it)";
                    continue;
                }

                using (IUnsafeRenderGraphBuilder builder =
                           renderGraph.AddUnsafePass(PassNamePrefix + ch.Name, out CopyData data))
                {
                    data.Source = handle;
                    data.Into = ch;
                    data.Render = Renders;
                    builder.UseTexture(handle, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((CopyData d, UnsafeGraphContext ctx) => CopyOut(d, ctx));
                }
                ch.Why = "its copy was recorded, but the pass never ran";
            }
        }

        static void CopyOut(CopyData d, UnsafeGraphContext context)
        {
            Channel ch = d.Into;
            RTHandle source = d.Source;
            RenderTexture rt = source != null ? source.rt : null;
            if (rt == null)
            {
                ch.Why = "the graph's handle had no RenderTexture behind it when the pass ran";
                return;
            }
            if (rt.antiAliasing > 1)
            {
                ch.Why = $"'{rt.name}' is MSAA x{rt.antiAliasing}, and a raw copy does not resolve it";
                return;
            }
            if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
            {
                ch.Why = "this graphics device cannot CopyTexture";
                return;
            }

            if (ch.Copy == null || ch.Copy.width != rt.width || ch.Copy.height != rt.height ||
                ch.Copy.graphicsFormat != rt.graphicsFormat)
            {
                ch.Release();
                ch.Copy = new RenderTexture(rt.width, rt.height, rt.graphicsFormat, GraphicsFormat.None)
                {
                    name = PassNamePrefix + ch.Name,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                ch.Copy.Create();
            }

            CommandBufferHelpers.GetNativeCommandBuffer(context.cmd).CopyTexture(rt, ch.Copy);
            ch.CopiedOnRender = d.Render;
            ch.Why = null;
            ch.How = $"copied inside render {d.Render} from '{rt.name}' {rt.width}x{rt.height} {rt.graphicsFormat}";
        }
    }
}
