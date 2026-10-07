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
    public sealed class FoamTransportGpuTests
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

        [UnityTest] public IEnumerator FoamTransportShader_PairAgreesOnBothEndpoints()
        {
            // Negative/positive velocities, both absolute parities, high coverage and unequal clocks.
            yield return Check(new Vector2(.5f,-.25f),false,1,new Vector2Int(-3,2));
            yield return Check(new Vector2(-.5f,.25f),false,1,new Vector2Int(2,-3));
        }

        [UnityTest] public IEnumerator FoamTransportShader_Rg32IdentityPreservesBothCodes()
        { yield return Check(Vector2.zero,false,4,new Vector2Int(-3,-5)); }

        [UnityTest] public IEnumerator FoamTransportShader_WallBlocksUniformIncidentFlow()
        { yield return Check(new Vector2(.5f,.125f),true,8,Vector2Int.zero); }

        IEnumerator Check(Vector2 speed,bool wall,int steps,Vector2Int lattice)
        {
            // PlayMode does not reference URP's 2D assembly. Reach the production recorder without
            // expanding the approved asmdef scope (or reproducing the graph in this fixture).
            MethodInfo record=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic)
                .GetMethod("RecordTransport",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsNotNull(record);
            var mass=new uint[N*N]; var fresh=new uint[N*N]; var blocked=new bool[N*N];
            var seed=Texture(N,N,TextureFormat.RG32);
            var seedBytes=seed.GetRawTextureData<ushort>();
            ulong initial=0;
            for(int y=0;y<N;y++) for(int x=0;x<N;x++)
            {
                int i=y*N+x;
                mass[i]=wall?(x==6?40000u:0u):(uint)((i*3571+71)%65536);
                fresh[i]=(uint)((i*919+311)%65536);
                seedBytes[2*i]=(ushort)mass[i]; seedBytes[2*i+1]=(ushort)fresh[i]; initial+=mass[i];
                blocked[i]=wall && x==8;
            }
            seed.Apply(false,false);
            var flow=Texture(5,5,TextureFormat.RGFloat);
            var vectors=flow.GetRawTextureData<Vector2>(); for(int i=0;i<vectors.Length;i++) vectors[i]=speed;
            flow.Apply(false,false);
            var mask=Texture(N,N,TextureFormat.R8);
            var bits=mask.GetRawTextureData<byte>(); for(int i=0;i<bits.Length;i++) bits[i]=blocked[i]?(byte)255:(byte)0;
            mask.Apply(false,false);
            RTHandle seedHandle=Wrap(seed), flowHandle=Wrap(flow), maskHandle=Wrap(mask);
            RTHandle a=History(), b=History();
            FenceResult tail=new FenceResult();
            _feature.Job=graph =>
            {
                TextureHandle current=graph.ImportTexture(seedHandle), next=graph.ImportTexture(a), spare=graph.ImportTexture(b);
                TextureHandle flowTex=graph.ImportTexture(flowHandle), maskTex=graph.ImportTexture(maskHandle);
                for(int s=0;s<steps;s++) for(int matching=0;matching<4;matching++)
                {
                    record.Invoke(null,new object[]{graph,_material,current,next,flowTex,maskTex,
                        new Vector4(N,N,.125f,.03125f),new Vector4(0,0,lattice.x,lattice.y),new Vector4(-1,-1,1,5),matching});
                    current=next; next=spare; spare=current;
                }
                using var builder=graph.AddUnsafePass<FenceData>("F2 test completion fence",out var data);
                data.Result=tail; builder.UseTexture(current,AccessFlags.Read); builder.AllowPassCulling(false);
                builder.SetRenderFunc((FenceData d,UnsafeGraphContext context) =>
                {
                    d.Result.Fence=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd).CreateGraphicsFence(
                        GraphicsFenceType.CPUSynchronisation,SynchronisationStageFlags.AllGPUOperations);
                    d.Result.Recorded=true;
                });
            };
            int jobs=_feature.ExecutedJobs;
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=_target};
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(_camera,request));
            RenderPipeline.SubmitRenderRequest(_camera,request);
            Assert.IsNull(_feature.Job); Assert.AreEqual(jobs+1,_feature.ExecutedJobs);
            yield return Bounded(() => tail.Recorded && tail.Fence.passed,"exchange submission");
            bool done=false,error=false;
            AsyncGPUReadback.Request(b.rt,0,TextureFormat.RGBAFloat,result =>
            { error=result.hasError; if(!error) _read=result.GetData<Color>().ToArray(); done=true; });
            yield return Bounded(() => done,"RG32 readback");
            Assert.IsFalse(error); Assert.AreEqual(N*N,_read.Length);
            for(int s=0;s<steps;s++) for(int matching=0;matching<4;matching++)
            {
                int axis=matching/2, parity=matching%2;
                for(int y=0;y<N;y++) for(int x=0;x<N;x++)
                {
                    int coordinate=axis==0?x+lattice.x:y+lattice.y;
                    if((coordinate&1)!=parity) continue;
                    int bx=x+(axis==0?1:0), by=y+(axis==0?0:1);
                    if(bx>=N || by>=N) continue;
                    int i=y*N+x,j=by*N+bx; if(blocked[i] || blocked[j]) continue;
                    FoamTransport.Exchange(mass[i],fresh[i],mass[j],fresh[j],(axis==0?speed.x:speed.y)*.25f,
                        out var ma,out var fa,out var mb,out var fb);
                    mass[i]=ma; fresh[i]=fa; mass[j]=mb; fresh[j]=fb;
                }
            }
            ulong actual=0;
            for(int i=0;i<_read.Length;i++)
            {
                uint m=(uint)Mathf.RoundToInt(_read[i].r*65535), f=(uint)Mathf.RoundToInt(_read[i].g*65535);
                Assert.AreEqual(mass[i],m,"coverage cell "+i); Assert.AreEqual(fresh[i],f,"freshness cell "+i);
                actual+=m; if(wall && i%N>=8) Assert.AreEqual(0,m,"wall transmission");
            }
            Assert.AreEqual(initial,actual,"Closed window conserves coverage codes exactly.");
        }

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
