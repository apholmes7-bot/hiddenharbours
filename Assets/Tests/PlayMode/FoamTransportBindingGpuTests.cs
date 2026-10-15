using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Executes the production graph recorder and shader. Null CI skips BEFORE allocating or
    // changing pipelines. These numerical controls do not claim Phase C visual/performance proof.
    public sealed class FoamTransportBindingGpuTests
    {
        const int N=16;
        Material _material;
        UniversalRenderPipelineAsset _pipeline;
        UniversalRendererData _renderer;
        HistoryUrpTestFeature _feature;
        RenderPipelineAsset _oldDefault, _oldQuality;
        Camera _camera;
        RenderTexture _target;
        bool _changed;
        readonly List<Camera> _disabled=new List<Camera>();
        readonly List<RTHandle> _handles=new List<RTHandle>();
        readonly List<Texture2D> _textures=new List<Texture2D>();
        Color[] _read;
        Camera _secondCamera;
        sealed class FenceData { internal FenceResult Result; }
        sealed class FenceResult { internal bool Recorded; internal GraphicsFence Fence; }

        [UnitySetUp] public IEnumerator Setup()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: F2 transport requires a GPU; expected on Null CI. Phase C remains pending.");
            Assert.IsTrue(SystemInfo.supportsGraphicsFence);
            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback);
            Assert.IsTrue(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RG32),"RG32 integer-code transport is required.");
            foreach(var camera in Object.FindObjectsByType<Camera>())
                if(camera.enabled) { _disabled.Add(camera); camera.enabled=false; }
            _oldDefault=GraphicsSettings.defaultRenderPipeline; _oldQuality=QualitySettings.renderPipeline;
            _renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            _renderer.renderingMode=RenderingMode.Forward;
            _feature=ScriptableObject.CreateInstance<HistoryUrpTestFeature>(); _feature.Create();
            _renderer.rendererFeatures.Add(_feature);
            _pipeline=UniversalRenderPipelineAsset.Create(_renderer);
            _target=new RenderTexture(32,32,24); _target.Create();
            _camera=new GameObject("F2 numerical test camera").AddComponent<Camera>();
            _camera.enabled=false; _camera.cullingMask=0; _camera.orthographic=true;
            _camera.clearFlags=CameraClearFlags.SolidColor; _camera.targetTexture=_target;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            bool listener=false;
            foreach(var item in Object.FindObjectsByType<AudioListener>()) if(item.isActiveAndEnabled) listener=true;
            if(!listener) _camera.gameObject.AddComponent<AudioListener>();
            _feature.Owner=_camera;
            _changed=true; GraphicsSettings.defaultRenderPipeline=_pipeline; QualitySettings.renderPipeline=_pipeline;
            _camera.enabled=true;
            yield return Bounded(() => RenderPipelineManager.currentPipeline is UniversalRenderPipeline,"URP startup");
            _camera.enabled=false;
            var shader=Shader.Find("Hidden/HiddenHarbours/FoamTransport");
            Assert.IsNotNull(shader); Assert.IsTrue(shader.isSupported);
            _material=CoreUtils.CreateEngineMaterial(shader);
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            if(_feature!=null) _feature.Job=null;
            if(_camera!=null) Object.DestroyImmediate(_camera.gameObject);
            if(_secondCamera!=null) Object.DestroyImmediate(_secondCamera.gameObject);
            if(_changed) { QualitySettings.renderPipeline=_oldQuality; GraphicsSettings.defaultRenderPipeline=_oldDefault; }
            foreach(var camera in _disabled) if(camera!=null) camera.enabled=true;
            yield return null;
            foreach(var handle in _handles) handle.Release(); _handles.Clear();
            foreach(var texture in _textures) Object.DestroyImmediate(texture); _textures.Clear();
            CoreUtils.Destroy(_material); CoreUtils.Destroy(_pipeline); CoreUtils.Destroy(_renderer); CoreUtils.Destroy(_feature);
            if(_target!=null) { _target.Release(); Object.DestroyImmediate(_target); }
            if(_changed)
            {
                Assert.AreSame(_oldDefault,GraphicsSettings.defaultRenderPipeline);
                Assert.AreSame(_oldQuality,QualitySettings.renderPipeline);
            }
            _changed=false; _disabled.Clear(); _read=null;
        }

        [UnityTest] public IEnumerator Repair_BindingsOneCameraOneStep() { yield return Matrix(1,1); }
        [UnityTest] public IEnumerator Repair_BindingsOneCameraFourSteps() { yield return Matrix(1,4); }
        [UnityTest] public IEnumerator Repair_BindingsTwoCamerasOneStep() { yield return Matrix(2,1); }
        [UnityTest] public IEnumerator Repair_BindingsTwoCamerasFourSteps() { yield return Matrix(2,4); }

        IEnumerator Matrix(int cameras,int steps)
        {
            // Assertion-bearing heir to F2E's diagnostic production arm. Keep the SAME material
            // for every draw; distinct per-camera seed codes catch cross-camera contamination.
            var record=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic)
                .GetMethod("RecordTransport",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsNotNull(record);
            var outputs=new RTHandle[cameras]; var fences=new FenceResult[cameras];
            if(cameras==2)
            {
                _secondCamera=new GameObject("F2 binding second camera").AddComponent<Camera>();
                _secondCamera.CopyFrom(_camera); _secondCamera.enabled=false;
                _secondCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            }
            for(int c=0;c<cameras;c++)
            {
                var seed=Texture(N,N,TextureFormat.RG32);
                var raw=seed.GetRawTextureData<ushort>();
                for(int i=0;i<N*N;i++) { raw[2*i]=Coverage(c,i); raw[2*i+1]=Freshness(c,i); }
                seed.Apply(false,false);
                var flow=Texture(5,5,TextureFormat.RGFloat);
                var vectors=flow.GetRawTextureData<Vector2>();
                for(int i=0;i<vectors.Length;i++) vectors[i]=Vector2.zero;
                flow.Apply(false,false);
                var mask=Texture(N,N,TextureFormat.R8);
                var bits=mask.GetRawTextureData<byte>(); for(int i=0;i<bits.Length;i++) bits[i]=0;
                mask.Apply(false,false);
                RTHandle seedHandle=Wrap(seed),flowHandle=Wrap(flow),maskHandle=Wrap(mask);
                RTHandle a=History(),b=History(); outputs[c]=b;
                var tail=new FenceResult(); fences[c]=tail;
                _feature.Owner=c==0?_camera:_secondCamera;
                _feature.Job=graph =>
                {
                    TextureHandle current=graph.ImportTexture(seedHandle),next=graph.ImportTexture(a),spare=graph.ImportTexture(b);
                    TextureHandle velocity=graph.ImportTexture(flowHandle),solids=graph.ImportTexture(maskHandle);
                    for(int step=0;step<steps;step++) for(int matching=0;matching<4;matching++)
                    {
                        record.Invoke(null,new object[]{graph,_material,current,next,velocity,solids,
                            new Vector4(N,N,.125f,.03125f),new Vector4(0,0,-3,2),new Vector4(-1,-1,1,5),matching});
                        current=next; next=spare; spare=current;
                    }
                    using var builder=graph.AddUnsafePass<FenceData>("F2 binding completion",out var data);
                    data.Result=tail; builder.UseTexture(current,AccessFlags.Read); builder.AllowPassCulling(false);
                    builder.SetRenderFunc((FenceData pass,UnsafeGraphContext context) =>
                    {
                        pass.Result.Fence=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd).CreateGraphicsFence(
                            GraphicsFenceType.CPUSynchronisation,SynchronisationStageFlags.AllGPUOperations);
                        pass.Result.Recorded=true;
                    });
                };
                int jobs=_feature.ExecutedJobs;
                var request=new UniversalRenderPipeline.SingleCameraRequest {destination=_target};
                Assert.IsTrue(RenderPipeline.SupportsRenderRequest(_feature.Owner,request));
                RenderPipeline.SubmitRenderRequest(_feature.Owner,request);
                Assert.AreEqual(jobs+1,_feature.ExecutedJobs);
            }
            // Submit every camera before waiting/reading any result.
            for(int c=0;c<cameras;c++)
            {
                var tail=fences[c];
                yield return Bounded(() => tail.Recorded && tail.Fence.passed,"binding matrix submission");
                bool done=false,error=false;
                AsyncGPUReadback.Request(outputs[c].rt,0,TextureFormat.RGBAFloat,result =>
                { error=result.hasError; if(!error) _read=result.GetData<Color>().ToArray(); done=true; });
                yield return Bounded(() => done,"binding matrix readback");
                Assert.IsFalse(error); Assert.AreEqual(N*N,_read.Length);
                for(int i=0;i<N*N;i++)
                {
                    Assert.AreEqual(Coverage(c,i),Mathf.RoundToInt(_read[i].r*65535),"coverage camera "+c+" cell "+i);
                    Assert.AreEqual(Freshness(c,i),Mathf.RoundToInt(_read[i].g*65535),"freshness camera "+c+" cell "+i);
                }
            }
        }

        static ushort Coverage(int camera,int cell) => (ushort)((cell*3571+71+camera*101)%65536);
        static ushort Freshness(int camera,int cell) => (ushort)((cell*919+311+camera*197)%65536);

        Texture2D Texture(int w,int h,TextureFormat format)
        {
            var texture=new Texture2D(w,h,format,false,true) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            _textures.Add(texture); return texture;
        }
        RTHandle Wrap(Texture2D texture) { var handle=RTHandles.Alloc(texture); _handles.Add(handle); return handle; }
        RTHandle History()
        {
            var handle=RTHandles.Alloc(N,N,colorFormat:GraphicsFormat.R16G16_UNorm,filterMode:FilterMode.Point);
            _handles.Add(handle); return handle;
        }
        static IEnumerator Bounded(Func<bool> ready,string phase)
        {
            float start=Time.realtimeSinceStartup;
            for(int i=0;i<120 && Time.realtimeSinceStartup-start<5;i++)
            { yield return new WaitForSecondsRealtime(.025f); if(ready()) yield break; }
            Assert.Fail("F2 GPU timed out: "+phase);
        }
    }
}
