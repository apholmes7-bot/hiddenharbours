using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Lifecycle regressions, NOT natural-frame visual acceptance. The real URP graph owns
    // execution and submission. All assertion readbacks are asynchronous, after a tail fence.
    public sealed class FoamHistoryInitializationGpuTests
    {
        const float TimeoutSeconds = 5;
        const int MaxPolls = 120;
        const float PollInterval = .025f;
        static string _fatalSanity;
        enum Mutation { None, SkipInitialization, InitializeAfterDeposit, ClearEveryGraph }
        sealed class Pair : IDisposable
        {
            internal RTHandle A, B;
            internal readonly FoamHistoryInitialization Init = new FoamHistoryInitialization();
            internal bool ReadA = true, NeedsPoison = true;
            internal int Size, InitializerCallbacks;
            internal string LastAuditedTicket;
            readonly bool _owns = true;
            internal Pair(int size) { Resize(size); }
            internal Pair(RTHandle a, RTHandle b, FoamHistoryInitialization init, int size)
            { A=a; B=b; Init=init; Size=size; _owns=false; }
            internal void Resize(int size)
            {
                A?.Release(); B?.Release(); Size=size;
                A=RTHandles.Alloc(size,size,colorFormat:UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat,
                    filterMode:FilterMode.Point,name:"HistoryTestA");
                B=RTHandles.Alloc(size,size,colorFormat:UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat,
                    filterMode:FilterMode.Point,name:"HistoryTestB");
                Init.ResetAllocation(); ReadA=true; NeedsPoison=true;
            }
            internal RTHandle Read => ReadA ? A : B;
            internal RTHandle Write => ReadA ? B : A;
            public void Dispose() { if(_owns) { A?.Release(); B?.Release(); } }
        }
        sealed class DrawData { internal TextureHandle Previous; internal Material Material; internal bool Deposit; internal int Size; }
        sealed class FenceResult { internal GraphicsFence Fence; internal bool Recorded; }
        sealed class FenceData { internal FenceResult Result; }
        sealed class ClearData { internal TextureHandle A,B; internal Color Color; }
        Material _material;
        UniversalRenderPipelineAsset _pipeline;
        UniversalRendererData _rendererData;
        HistoryUrpTestFeature _feature;
        RenderPipelineAsset _oldDefault, _oldQuality;
        Camera _cameraA, _cameraB;
        RenderTexture _destination;
        bool _settingsAssigned;
        int _endedCameraRenders;
        readonly Dictionary<RTHandle,Color[]> _pixels = new Dictionary<RTHandle,Color[]>();
        readonly List<Camera> _disabledCameras = new List<Camera>();
        readonly Dictionary<Camera,bool> _originalCameraStates = new Dictionary<Camera,bool>();
        readonly HistorySyntheticWorld _world = new HistorySyntheticWorld();
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Setup()
        {
            if (_fatalSanity != null) Assert.Ignore("STOPPED after failed submission sanity: " + _fatalSanity);
            // No GPU (CI's Null device) or not D3D12: an honest skip BEFORE the sanity latch, the isolation and any
            // pipeline setup. On a D3D12 device neither branch is taken and the hard asserts below stand as validated.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered and nothing was proved. These history lifecycle tests need a D3D12 GPU. Expected on CI.");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                Assert.Ignore($"SKIPPED, NOT VERIFIED: the graphics API is {SystemInfo.graphicsDeviceType}; these history lifecycle tests were validated on D3D12 only.");
            _fatalSanity="setup/sanity has not completed"; // Latches failures; subsequent tests do not repeat timeouts.
            _endedCameraRenders=0;
            TestContext.WriteLine($"HISTORY_BOOT api={SystemInfo.graphicsDeviceType} unity={Application.unityVersion}");
            Assert.AreEqual("Direct3D12",SystemInfo.graphicsDeviceType.ToString(),"Granted slot is D3D12 only");
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType,"GPU slot required");
            Assert.IsTrue(SystemInfo.supportsGraphicsFence,"Fence support required; do not bypass readiness");
            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback,"Nonblocking assertion readbacks required");
            foreach(var camera in Object.FindObjectsByType<Camera>())
            {
                _originalCameraStates.Add(camera,camera.enabled);
                if(camera.enabled) { _disabledCameras.Add(camera); camera.enabled=false; }
            }
            _oldDefault=GraphicsSettings.defaultRenderPipeline; _oldQuality=QualitySettings.renderPipeline;
            _world.Begin();
            _rendererData=ScriptableObject.CreateInstance<UniversalRendererData>();
            _rendererData.hideFlags=HideFlags.HideAndDontSave;
            _rendererData.renderingMode=RenderingMode.Forward;
            _feature=ScriptableObject.CreateInstance<HistoryUrpTestFeature>();
            _feature.hideFlags=HideFlags.HideAndDontSave; _feature.Create();
            _rendererData.rendererFeatures.Add(_feature);
            _pipeline=UniversalRenderPipelineAsset.Create(_rendererData);
            _pipeline.hideFlags=HideFlags.HideAndDontSave;
            _destination=new RenderTexture(64,64,24) {name="History test camera target",hideFlags=HideFlags.HideAndDontSave};
            _destination.Create();
            _cameraA=MakeCamera("History owner A"); _cameraB=MakeCamera("History owner B");
            bool hasListener=false;
            foreach(var listener in Object.FindObjectsByType<AudioListener>())
                if(listener.isActiveAndEnabled) hasListener=true;
            if(!hasListener) _cameraA.gameObject.AddComponent<AudioListener>();
            RenderPipelineManager.endCameraRendering+=CameraEnded;
            _settingsAssigned=true;
            GraphicsSettings.defaultRenderPipeline=_pipeline; QualitySettings.renderPipeline=_pipeline;
            _cameraA.enabled=true;
            yield return Bounded(() => RenderPipelineManager.currentPipeline is UniversalRenderPipeline && _endedCameraRenders>0,
                "real camera/URP bootstrap");
            _cameraA.enabled=false;
            Assert.IsNotNull(typeof(UniversalRenderPipeline).GetField("s_RTHandlePool",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),
                "URP constructor must own the resource pool; never fabricate one");
            _material=CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect"));
            Assert.IsNotNull(_material);
            yield return Sanity();
            _fatalSanity=null;
        }

        Camera MakeCamera(string name)
        {
            var go=new GameObject(name) {hideFlags=HideFlags.HideAndDontSave};
            var camera=go.AddComponent<Camera>(); camera.enabled=false; camera.cullingMask=0;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.orthographic=true; camera.targetTexture=_destination;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            return camera;
        }
        void CameraEnded(ScriptableRenderContext context,Camera camera)
        { if(camera==_cameraA || camera==_cameraB) _endedCameraRenders++; }

        [UnityTearDown] public IEnumerator TearDown()
        {
            bool verifySettings=_settingsAssigned;
            RenderPipelineManager.endCameraRendering-=CameraEnded;
            if(_feature!=null) _feature.Job=null;
            if(_cameraA!=null) Object.DestroyImmediate(_cameraA.gameObject);
            if(_cameraB!=null) Object.DestroyImmediate(_cameraB.gameObject);
            if(_settingsAssigned) { QualitySettings.renderPipeline=_oldQuality; GraphicsSettings.defaultRenderPipeline=_oldDefault; _settingsAssigned=false; }
            foreach(var camera in _disabledCameras) if(camera!=null) camera.enabled=true;
            // Allow the engine to dispose/switch pipelines before destroying owned data.
            yield return new WaitForSecondsRealtime(PollInterval);
            CoreUtils.Destroy(_material); CoreUtils.Destroy(_pipeline);
            CoreUtils.Destroy(_rendererData); CoreUtils.Destroy(_feature);
            if(_destination!=null) { _destination.Release(); Object.DestroyImmediate(_destination); }
            yield return _world.Restore();
            if(verifySettings)
            {
                Assert.AreSame(_oldQuality,QualitySettings.renderPipeline,"Quality pipeline reference not restored");
                Assert.AreSame(_oldDefault,GraphicsSettings.defaultRenderPipeline,"Default pipeline reference not restored");
            }
            foreach(var entry in _originalCameraStates)
            {
                Assert.IsNotNull(entry.Key,"Original scene camera disappeared during test");
                Assert.AreEqual(entry.Value,entry.Key.enabled,"Original scene-camera enabled state not restored");
            }
            TestContext.WriteLine($"HISTORY_TEARDOWN pipelineReferencesChecked={verifySettings} pipelineReferencesRestored=True sceneCameraCount={_originalCameraStates.Count} sceneCameraStatesRestored=True");
            _originalCameraStates.Clear();
            _disabledCameras.Clear(); _pixels.Clear();
        }

        static IEnumerator Bounded(Func<bool> ready,string phase)
        {
            float start=Time.realtimeSinceStartup; int polls=0,first=Time.frameCount,last=-1;
            // Wall throttling AND a count bound: fast batchmode cannot spin for 20,000 frames.
            while(Time.realtimeSinceStartup-start<TimeoutSeconds && polls<MaxPolls)
            {
                yield return new WaitForSecondsRealtime(PollInterval);
                if(Time.frameCount==last) continue;
                last=Time.frameCount; polls++;
                if(ready())
                {
                    TestContext.WriteLine($"HISTORY_WAIT phase={phase} passed=True polls={polls} frameDelta={Time.frameCount-first} seconds={Time.realtimeSinceStartup-start:F6}");
                    yield break;
                }
            }
            Assert.Fail($"HISTORY_WAIT phase={phase} timed out after {polls} bounded polls / {Time.realtimeSinceStartup-start:F6}s");
        }
        void Submit(Action<RenderGraph> job,Camera camera=null)
        {
            _world.AssertSuspended();
            camera=camera ?? _cameraA;
            Assert.IsNull(_feature.Job,"Previous camera job was not consumed");
            int before=_feature.ExecutedJobs,ended=_endedCameraRenders;
            _feature.Owner=camera;
            _feature.Job=graph =>
            {
                Assert.IsTrue((bool)typeof(RenderGraph).GetField("m_EnableCompilationCaching",Private).GetValue(graph),
                    "Must use the production graph with caching enabled");
                job(graph);
            };
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=_destination};
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera,request));
            RenderPipeline.SubmitRenderRequest(camera,request);
            Assert.IsNull(_feature.Job,"URP did not consume the test job");
            Assert.AreEqual(before+1,_feature.ExecutedJobs);
            Assert.Greater(_endedCameraRenders,ended,"Real URP camera end callback missing");
        }
        static TextureHandle Import(RenderGraph graph,RTHandle texture) => graph.ImportTexture(texture,
            new ImportResourceParams {clearOnFirstUse=false,discardOnLastUse=false});
        static void Clear(RenderGraph graph,TextureHandle a,TextureHandle b,Color color)
        {
            using var builder=graph.AddUnsafePass<ClearData>("TEST poison or sanity clear",out var data);
            data.A=a; data.B=b; data.Color=color;
            builder.UseTexture(a,AccessFlags.WriteAll); builder.UseTexture(b,AccessFlags.WriteAll);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((ClearData d,UnsafeGraphContext context) =>
            {
                var cmd=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                CoreUtils.SetRenderTarget(cmd,(RTHandle)d.A,ClearFlag.Color,d.Color);
                CoreUtils.SetRenderTarget(cmd,(RTHandle)d.B,ClearFlag.Color,d.Color);
            });
        }
        static FenceResult Tail(RenderGraph graph,TextureHandle a,TextureHandle b)
        {
            var result=new FenceResult();
            using var builder=graph.AddUnsafePass<FenceData>("TEST all history work complete",out var data);
            data.Result=result; builder.UseTexture(a,AccessFlags.Read); builder.UseTexture(b,AccessFlags.Read);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((FenceData d,UnsafeGraphContext context) =>
            {
                d.Result.Fence=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd).CreateGraphicsFence(
                    GraphicsFenceType.CPUSynchronisation,SynchronisationStageFlags.AllGPUOperations);
                d.Result.Recorded=true;
            });
            return result;
        }
        IEnumerator Capture(RTHandle target)
        {
            bool done=false,error=false; Color[] pixels=null;
            AsyncGPUReadback.Request(target.rt,0,TextureFormat.RGBAFloat,request =>
            {
                error=request.hasError;
                // Request data survives only one frame. Copy in the completion callback, never
                // after the throttled coroutine wakes up several fast batchmode frames later.
                if(!error) pixels=request.GetData<Color>().ToArray();
                done=true;
            });
            yield return Bounded(() => done,"async assertion readback");
            Assert.IsFalse(error); Assert.IsNotNull(pixels);
            _pixels[target]=pixels;
        }
        Color[] Read(RTHandle target) => _pixels[target];
        IEnumerator Sanity()
        {
            using var pair=new Pair(16);
            FenceResult tail=null;
            Submit(graph => {var a=Import(graph,pair.A); var b=Import(graph,pair.B);
                Clear(graph,a,b,new Color(.25f,.5f,0,0)); tail=Tail(graph,a,b);});
            Assert.IsNotNull(tail); Assert.IsTrue(tail.Recorded,"Fence callback missing");
            yield return Bounded(() => tail.Fence.passed,"submission sanity GPU fence");
            yield return Capture(pair.A); yield return Capture(pair.B);
            // Optional harness-only negative oracle: deliberate wrong expectation must fail
            // sanity, latch the suite shut, and prevent any initialization regression work.
            bool negative=System.Environment.GetEnvironmentVariable("HH_WAKE_SANITY_NEGATIVE")=="1";
            float expectedR=negative ? .75f : .25f;
            TestContext.WriteLine($"HISTORY_SANITY_ORACLE negative={negative}");
            foreach(var target in new[]{pair.A,pair.B}) foreach(var pixel in Read(target))
            { Assert.That(pixel.r,Is.EqualTo(expectedR).Within(.001f)); Assert.That(pixel.g,Is.EqualTo(.5f).Within(.001f)); }
            LogAssert.NoUnexpectedReceived();
            TestContext.WriteLine($"HISTORY_SANITY passed=True api={SystemInfo.graphicsDeviceType} pipeline={RenderPipelineManager.currentPipeline.GetType().Name} pool=URP-owned caching=True bothHistories=True");
        }
        [UnityTest,Order(0)] public IEnumerator SubmissionFenceSanity()
        { TestContext.WriteLine("HISTORY_SANITY_GATE passed before regressions"); yield break; }

        static void Audit(Pair pair,string phase)
        {
            string ticket=pair.Init.State.Generation+"/"+pair.Init.State.Attempt;
            string status=pair.Init.State.Status.ToString();
            if((status=="WaitingForFence"||status=="Ready") && ticket!=pair.LastAuditedTicket)
            { pair.InitializerCallbacks++; pair.LastAuditedTicket=ticket; }
            TestContext.WriteLine($"HISTORY_TEST phase={phase} size={pair.Size} ticket={ticket} status={status} successfulInitializerCallbacks={pair.InitializerCallbacks} ready={pair.Init.Ready}");
        }
        IEnumerator Run(Pair pair,bool deposit,Mutation mutation=Mutation.None,Camera camera=null)
        {
            if(pair.NeedsPoison)
            {
                FenceResult poison=null;
                Submit(graph => {var a=Import(graph,pair.A);var b=Import(graph,pair.B);
                    Clear(graph,a,b,new Color(.875f,.875f,0,0)); poison=Tail(graph,a,b);},camera);
                yield return Bounded(() => poison.Recorded && poison.Fence.passed,"poison submitted");
                yield return Capture(pair.A); yield return Capture(pair.B);
                foreach(var target in new[]{pair.A,pair.B}) foreach(var pixel in Read(target))
                { Assert.That(pixel.r,Is.EqualTo(.875f).Within(.001f)); Assert.That(pixel.g,Is.EqualTo(.875f).Within(.001f)); }
                pair.NeedsPoison=false;
            }
            FenceResult tail=null;
            Submit(graph => { tail=Record(graph,pair,deposit,mutation); },camera);
            Audit(pair,"URP-submitted");
            if(mutation!=Mutation.SkipInitialization)
                yield return Bounded(() => {pair.Init.CanRecord();return pair.Init.Ready;},"initialization GPU readiness");
            // An initializer fence precedes the deposit. Never use it as deposit completion proof.
            yield return Bounded(() => tail.Recorded && tail.Fence.passed,"deposit tail GPU completion");
            Audit(pair,"GPU-confirmed");
            yield return Capture(pair.A); yield return Capture(pair.B);
            LogAssert.NoUnexpectedReceived();
        }

        FenceResult Record(RenderGraph graph,Pair pair,bool deposit,Mutation mutation)
        {
            var previous=Import(graph,pair.Read); var next=Import(graph,pair.Write);
            if(mutation==Mutation.ClearEveryGraph) pair.Init.ResetAllocation();
            if(mutation!=Mutation.SkipInitialization && mutation!=Mutation.InitializeAfterDeposit)
                pair.Init.Record(graph,previous,next);
            using (var builder = graph.AddRasterRenderPass<DrawData>("History test actual foam shader", out var data))
            {
                data.Previous = previous; data.Material = _material; data.Deposit = deposit; data.Size = pair.Size;
                builder.UseTexture(previous, AccessFlags.Read);
                builder.SetRenderAttachment(next, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                {
                    var segments = new Vector4[FoamBuffer.MaxInjectors];
                    var shapes = new Vector4[FoamBuffer.MaxInjectors];
                    var empty = new Vector4[FoamBuffer.MaxInjectors];
                    segments[0] = new Vector4(4,4,4,4);
                    if (d.Deposit) shapes[0] = new Vector4(1,.6f,1,0);
                    d.Material.SetTexture("_HHFoamPrev", (RTHandle)d.Previous);
                    d.Material.SetVector("_HHFoamBufferWorld", new Vector4(0,0,8,.125f));
                    d.Material.SetVector("_HHFoamResolution", new Vector4(d.Size,d.Size,1f/d.Size,1f/d.Size));
                    d.Material.SetVector("_HHFoamShift", Vector4.zero);
                    d.Material.SetFloat("_HHFoamDecay",1);
                    d.Material.SetFloat("_HHFoamAgeDecay",1);
                    d.Material.SetVectorArray("_HHFoamInjectSeg",segments);
                    d.Material.SetVectorArray("_HHFoamInjectShape",shapes);
                    d.Material.SetVectorArray("_HHFoamDispTrackA",empty);
                    d.Material.SetVectorArray("_HHFoamDispTrackB",empty);
                    d.Material.SetVectorArray("_HHFoamDispTrackC",empty);
                    d.Material.SetVectorArray("_HHFoamDispShape",empty);
                    d.Material.SetVector("_HHSurfDeposit",Vector4.zero);
                    Blitter.BlitTexture(ctx.cmd,new Vector4(1,1,0,0),d.Material,0);
                });
            }
            if(mutation==Mutation.InitializeAfterDeposit) pair.Init.Record(graph,previous,next);
            pair.ReadA=!pair.ReadA;
            return Tail(graph,previous,next);
        }
        static float Peak(Color[] pixels) { float peak=0; foreach(var c in pixels) { Assert.IsFalse(float.IsNaN(c.r)||float.IsNaN(c.g)); peak=Mathf.Max(peak,c.r); } return peak; }
        static bool CleanDeposit(Color[] pixels) => pixels[0].r < .001f && pixels[0].g < .001f && Peak(pixels) > .1f;

        [UnityTest] public IEnumerator FirstDeposit_BothHistories_Recurrence_Reallocation()
        {
            using var pair = new Pair(64);
            yield return Run(pair,true);
            var first = Read(pair.Read);
            Assert.IsTrue(CleanDeposit(first),"Initialization must preserve the very first deposit and remove poisoned history.");
            foreach(var c in Read(pair.Write)) { Assert.Less(c.r,.001f); Assert.Less(c.g,.001f); }
            int initialized=pair.InitializerCallbacks;
            Assert.AreEqual(1,initialized);
            for(int i=0;i<12;i++)
            {
                yield return Run(pair,false);
                CollectionAssert.AreEqual(first,Read(pair.Read),"Zero-delta copy or steady graph re-cleared/changed history at recurrence "+i);
            }
            Assert.AreEqual(initialized,pair.InitializerCallbacks,"No recurring initializer callback after Ready");
            pair.Resize(128);
            Assert.IsFalse(pair.Init.Ready);
            yield return Run(pair,true);
            Assert.AreEqual(initialized+1,pair.InitializerCallbacks,"Exactly one initializer for resized allocation");
            Assert.IsTrue(CleanDeposit(Read(pair.Read)),"Reallocated history was not initialized before its first deposit.");
            foreach(var c in Read(pair.Write)) { Assert.Less(c.r,.001f); Assert.Less(c.g,.001f); }
        }

        [UnityTest] public IEnumerator TwoCameras_InitializeIndependently()
        {
            using var a=new Pair(64); using var b=new Pair(128);
            yield return Run(a,true);
            var retained=Read(a.Read); Assert.IsFalse(b.Init.Ready);
            yield return Run(b,true,camera:_cameraB);
            yield return Capture(a.Read);
            CollectionAssert.AreEqual(retained,Read(a.Read));
            b.Resize(64); Assert.IsTrue(a.Init.Ready); Assert.IsFalse(b.Init.Ready);
            yield return Run(b,false,camera:_cameraB);
            yield return Capture(a.Read);
            CollectionAssert.AreEqual(retained,Read(a.Read)); Assert.Less(Peak(Read(b.Read)),.001f);
        }

        [UnityTest] public IEnumerator ProductionAllocation_Resize_TextureLoss_CameraIsolation()
        {
            var passType=typeof(IsoFacetHullFeature).GetNestedType("HullPass",BindingFlags.NonPublic);
            var pass=Activator.CreateInstance(passType,true);
            var get=passType.GetMethod("GetFoamState",Private);
            var cameraA=_cameraA;
            var cameraB=_cameraB;
            cameraA.enabled=false; cameraB.enabled=false;
            object Fetch(Camera camera,int size) => get.Invoke(pass,new object[]{camera.GetEntityId(),size});
            object Field(object state,string name) => state.GetType().GetField(name).GetValue(state);
            Pair Wrap(object state,int size) => new Pair((RTHandle)Field(state,"A"),(RTHandle)Field(state,"B"),
                (FoamHistoryInitialization)Field(state,"Initialization"),size);
            try
            {
                var stateA=Fetch(cameraA,64);
                using var initial=Wrap(stateA,64);
                yield return Run(initial,true);
                Assert.IsTrue(initial.Init.Ready);
                Assert.AreSame(initial.A,Field(Fetch(cameraA,64),"A"),"Stable allocation must retain its histories.");
                var b=Fetch(cameraB,64);
                Assert.AreNotSame(Field(stateA,"Initialization"),Field(b,"Initialization"));
                Assert.IsFalse(((FoamHistoryInitialization)Field(b,"Initialization")).Ready);
                int generation=initial.Init.State.Generation;
                var resized=Fetch(cameraA,128);
                Assert.Greater(initial.Init.State.Generation,generation);
                Assert.IsFalse(initial.Init.Ready); Assert.AreEqual(-1,Field(resized,"LastFrame"));
                Assert.IsTrue((bool)Field(resized,"ReadIsA"));
                using var replacement=Wrap(resized,128);
                yield return Run(replacement,true);
                Assert.IsTrue(CleanDeposit(Read(replacement.Read)),"Production resize lost the new first deposit.");
                generation=replacement.Init.State.Generation;
                replacement.A.rt.Release(); // actual native texture loss, unchanged requested size/format
                var recovered=Fetch(cameraA,128);
                Assert.Greater(replacement.Init.State.Generation,generation);
                Assert.IsFalse(replacement.Init.Ready);
                using var restored=Wrap(recovered,128);
                yield return Run(restored,true);
                Assert.IsTrue(CleanDeposit(Read(restored.Read)));
                Assert.IsFalse(((FoamHistoryInitialization)Field(b,"Initialization")).Ready);
            }
            finally
            {
                passType.GetMethod("Dispose").Invoke(pass,null);
                // Cameras belong to the fixture and are restored by teardown.
            }
        }

        [UnityTest] public IEnumerator PoisonAndOrder_NegativeControls()
        {
            using(var skipped=new Pair(64))
            {
                yield return Run(skipped,true,Mutation.SkipInitialization);
                Assert.IsFalse(CleanDeposit(Read(skipped.Read)),"Negative control failed: poisoned uncleared history escaped detection.");
            }
            using(var late=new Pair(64))
            {
                yield return Run(late,true,Mutation.InitializeAfterDeposit);
                Assert.IsFalse(CleanDeposit(Read(late.Read)),"Negative control failed: late clear did not erase the first deposit.");
            }
            using(var recurring=new Pair(64))
            {
                yield return Run(recurring,true);
                Assert.IsTrue(CleanDeposit(Read(recurring.Read)));
                yield return Run(recurring,false,Mutation.ClearEveryGraph);
                Assert.Less(Peak(Read(recurring.Read)),.001f,"Negative control failed: recurring initialization did not destroy history.");
            }
        }

        [UnityTest] public IEnumerator AbortedBeforeCallback_Retries_AbortedSubmissionNeverReady()
        {
            using(var retry=new Pair(64))
            {
                // Supported engine-light state seam: stop BEFORE giving any graph to URP.
                // No live render callback throws and no native context is abandoned.
                var abandoned=retry.Init.State.Begin();
                Assert.IsFalse(retry.Init.Ready); Assert.IsTrue(retry.Init.RequiresRecording);
                Assert.AreEqual(FoamHistoryInitializationState.Stage.Recording,retry.Init.State.Status);
                yield return Run(retry,true);
                Assert.Greater(retry.Init.State.Attempt,abandoned.Attempt);
                Assert.IsFalse(retry.Init.State.Confirm(abandoned,true),"Abandoned attempt must not confirm a later attempt");
                Assert.IsTrue(CleanDeposit(Read(retry.Read)));
            }
            // No supported API safely abandons an already recorded native camera stream. Test
            // the CPU ownership contract explicitly; do NOT manufacture an invalid GraphicsFence.
            // This does not cover real GPU submission interruption or driver-induced long pending.
            var interrupted=new FoamHistoryInitializationState();
            var ticket=interrupted.Begin(); interrupted.FenceRecorded(ticket);
            Assert.IsFalse(interrupted.Ready);
            Assert.AreEqual(FoamHistoryInitializationState.Stage.WaitingForFence,interrupted.Status);
            int eligible=0,skipped=0,lastFrame=Time.frameCount; float skippedAmount=0;
            for(int i=0;i<8;i++)
            {
                yield return new WaitForSecondsRealtime(PollInterval);
                Assert.Greater(Time.frameCount,lastFrame,"Pending opportunity must be on a new yielded frame");
                lastFrame=Time.frameCount;
                // Explicit modeled producer opportunity: one positive deposit per yielded frame.
                eligible++;
                Assert.IsFalse(interrupted.Confirm(ticket,false));
                if(!interrupted.Ready && !interrupted.RequiresRecording) {skipped++;skippedAmount+=1;}
                Assert.IsFalse(interrupted.Ready,"No GPU confirmation means no Ready, regardless of elapsed time");
                Assert.IsFalse(interrupted.RequiresRecording,"Pending must not re-record initialization");
            }
            Assert.AreEqual(8,eligible); Assert.AreEqual(eligible,skipped); Assert.AreEqual(8,skippedAmount);
            interrupted.ResetAllocation();
            Assert.IsFalse(interrupted.Confirm(ticket,true),"A late completion after reset is stale");
            Assert.IsFalse(interrupted.Ready); Assert.IsTrue(interrupted.RequiresRecording);
            TestContext.WriteLine($"HISTORY_INTERRUPTION coverage=CPU-state-only noNativeContextAbandoned=True eligible={eligible} skipped={skipped} skippedAmount={skippedAmount} realGpuInterruption=UNVERIFIED realProlongedPendingDeposits=UNVERIFIED");
        }
    }
}
