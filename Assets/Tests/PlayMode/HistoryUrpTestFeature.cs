using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Tests.PlayMode
{
    // Test-owned in-memory renderer feature. No scene, renderer asset or production code edit.
    public sealed class HistoryUrpTestFeature : ScriptableRendererFeature
    {
        internal Camera Owner;
        internal Action<RenderGraph> Job;
        internal int ExecutedJobs;
        Pass _pass;
        public override void Create() => _pass = new Pass(this)
            { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (Job != null && data.cameraData.camera == Owner) renderer.EnqueuePass(_pass);
        }
        sealed class Pass : ScriptableRenderPass
        {
            readonly HistoryUrpTestFeature _owner;
            internal Pass(HistoryUrpTestFeature owner) => _owner = owner;
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var job = _owner.Job;
                _owner.Job = null;
                job(graph);
                _owner.ExecutedJobs++;
            }
        }
    }
}
