using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullSunShadowRenderTests
    {
        static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to verify drawn sun shadows.");
        }

        [Test] public void DeckShadow_LandsOnTheOwnDeckOnly()
        {
            using var stage=new HullDeckShadowTests.DeckStage();
            Color32[] off=stage.Read(false,8),on=stage.Read(true,8);Color32 deck=off[stage.DeckSample];int changed=0;
            for(int i=0;i<off.Length;i++)if(!off[i].Equals(on[i])){changed++;Assert.That(off[i],Is.EqualTo(deck),"A changed pixel must be visible deck, never a wall, water or neighbour.");}
            Assert.That(changed,Is.GreaterThan(100));
        }
        [Test] public void DeckShadow_FollowsTheSun()
        {
            using var stage=new HullDeckShadowTests.DeckStage();
            double Centre(float hour)
            {
                Color32[] off=stage.Read(false,hour),on=stage.Read(true,hour);long x=0,count=0;
                for(int i=0;i<off.Length;i++)if(!off[i].Equals(on[i])){x+=i%stage.Shot.Size;count++;}
                Assert.That(count,Is.GreaterThan(100));return (double)x/count-stage.Shot.Size/2.0;
            }
            Assert.That(Centre(8)*Centre(17),Is.LessThan(0),"The morning and evening shade must lie on opposite sides of the house.");
        }

        [Test]
        public void Neighbour_Receives()
        {
            RequireGpu();
            using var stage = new SunStage();
            stage.Mask(24);
            Color32[] off = stage.Shoot(false), on = stage.Shoot(true);
            Assert.That(Changed(off,on), Is.GreaterThan(100), "A neighbouring id must remain a shadow receiver.");
            stage.Mask(23);
            Assert.That(Changed(stage.Shoot(false),stage.Shoot(true)), Is.Zero, "The casting hull excludes itself before the stencil.");
        }

        [Test]
        public void Overlap_MultipliesOnce()
        {
            RequireGpu();
            using var stage = new SunStage();
            Color32[] once = stage.Shoot(true);
            int[] original = stage.Mesh.triangles;
            var twice = new int[original.Length * 2];
            Array.Copy(original,twice,original.Length); Array.Copy(original,0,twice,original.Length,original.Length);
            stage.Mesh.triangles = twice;
            Assert.That(Changed(once,stage.Shoot(true)), Is.Zero, "Overlapping proxy faces share the existing one-shade stencil.");
        }

        [Test]
        public void CutawayAndDoor_MatchTheDisplayedMask()
        {
            RequireGpu();
            using var stage = new SunStage();
            Color32[] absent = stage.Shoot(false);
            Assert.That(Changed(absent,stage.Shoot(true)), Is.GreaterThan(100));
            var levels = new List<Vector4> {new Vector4(1,0,1,0),new Vector4(1,0,1,0),new Vector4(1,0,1,0),new Vector4(1,0,1,0)};
            stage.Mesh.SetUVs(0,levels);
            stage.Material.SetFloat("_HHSunLevelGate",1);
            stage.Material.SetFloat("_HHLevelShown",1);
            stage.Material.SetInteger("_HHShownRoles",int.MaxValue);
            Assert.That(Changed(absent,stage.Shoot(true)), Is.Zero, "The visible facet predicate removes the same house faces.");
            for(int i=0;i<levels.Count;i++)levels[i]=new Vector4(1,0,2,0);
            stage.Mesh.SetUVs(0,levels);stage.Material.SetInteger("_HHShownRoles",1);
            Assert.That(Changed(absent,stage.Shoot(true)),Is.GreaterThan(100),"A role not selected for lifting stays visible.");
            stage.Material.SetInteger("_HHShownRoles",2);
            Assert.That(Changed(absent,stage.Shoot(true)),Is.Zero,"The raw third tag component is the role, not the front flag.");
            stage.Material.SetFloat("_HHSunLevelGate",0);
            stage.Mesh.SetUVs(1,new List<Vector2> {Vector2.right,Vector2.right,Vector2.right,Vector2.right});
            stage.Material.SetFloat("_HHSunDoorOpen",1);
            Assert.That(Changed(absent,stage.Shoot(true)), Is.Zero, "Closed-leaf geometry is absent when the door is open.");
            stage.Material.SetFloat("_HHSunDoorOpen",0);
            Assert.That(Changed(absent,stage.Shoot(true)), Is.GreaterThan(100));
        }

        [Test]
        public void ReceiverOrder_PreservesLampsAndFigures()
        {
            RequireGpu();
            using var stage = new SunStage();
            stage.Sprite("figure",new Vector2(1,0),new Vector2(0.7f,0.8f),new Color32(180,100,60,255),10);
            Color32[] off = stage.Shoot(false), on = stage.Shoot(true);
            int figure = stage.PixelOf(new Vector2(1,0));
            Assert.That(on[figure].r, Is.LessThan(off[figure].r), "A figure's final visible colour receives the shade.");
            stage.Sprite("late light",new Vector2(1,0),new Vector2(0.5f,0.5f),Color.white,SceneLight.MaxSortingOrder);
            off = stage.Shoot(false); on = stage.Shoot(true);
            Assert.That(on[figure], Is.EqualTo(off[figure]), "The later light remains over the sun shade.");
        }

        [Test]
        public void Night_LampsAreUnchanged()
        {
            RequireGpu();
            using var stage = new SunStage();
            var host = stage.Own(new GameObject("NightHull") {layer=31});
            var mesh = stage.Own(HullSunShadowFixture.Box());
            host.AddComponent<IsoFacetHullRenderer>().Configure(HullSunShadowFixture.Setup(mesh));
            var caster = HullSunShadowCaster.Fit(host);
            stage.Shadow.enabled=false;
            stage.AddLamp(new Vector2(1,0));
            Vector4 oldSun=Shader.GetGlobalVector("_SunDir");
            float oldE=Shader.GetGlobalFloat("_SunElevation"), oldS=Shader.GetGlobalFloat("_ShadowStrength");
            Color oldTint=Shader.GetGlobalColor("_DayNightTint");
            try
            {
                SetSun(22.5f);
                caster.Tick();
                Assert.That(caster.ShadowRenderer.enabled,Is.False);
                Color32[] on=stage.Read();
                caster.enabled=false; caster.Tick();
                Assert.That(Changed(on,stage.Read()),Is.Zero,"Turning the sun caster off at night changes no lamp pixel.");
            }
            finally {Shader.SetGlobalVector("_SunDir",oldSun);Shader.SetGlobalFloat("_SunElevation",oldE);Shader.SetGlobalFloat("_ShadowStrength",oldS);Shader.SetGlobalColor("_DayNightTint",oldTint);}
        }

        internal static int Changed(Color32[] before,Color32[] after)
        {
            int count=0; for(int i=0;i<before.Length;i++)if(!before[i].Equals(after[i]))count++; return count;
        }

        internal static void SetSun(float hour)
        {
            var day=Resources.Load<DayNightProfile>("DayNightProfile");
            Vector2 d=DayNightMath.SunDirection(hour,day.SunriseHour,day.SunsetHour,day.ShadowSouthBias,day.ShadowNoonLift);
            Shader.SetGlobalVector("_SunDir",new Vector4(d.x,d.y,0,0));
            Shader.SetGlobalFloat("_SunElevation",DayNightMath.SunElevation(hour,day.SunriseHour,day.SunsetHour));
            Shader.SetGlobalFloat("_ShadowStrength",DayNightMath.ShadowStrength(hour,day.SunriseHour,day.SunsetHour,0,day.OvercastFadesShadow));
            Shader.SetGlobalColor("_DayNightTint",DayNightMath.DayNightTint(hour,day,1,0));
        }

        /// <summary>The six requested game-scene pairs; called explicitly under the keeper, never by CI.</summary>
        public static void CaptureProofFrames()
        {
            try
            {
                RequireGpu();
                string folder=Path.Combine("artifacts","boats-look","pr2","c","plates");
                if(!File.Exists(Path.Combine("artifacts","boats-look","pr2","c","plates-floor.json")))throw new InvalidOperationException("The plate floor must precede this launch.");
                Directory.CreateDirectory(folder);
                var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/NineMileCreek.unity",OpenSceneMode.Single);
                Transform station=null;
                foreach(GameObject root in scene.GetRootGameObjects())
                    if(root.name=="NineMileCreekWharf")
                        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                            if(t.name.IndexOf("bollard",StringComparison.OrdinalIgnoreCase)>=0){station=t;break;}
                if(station==null)throw new InvalidOperationException("The named wharf bollard station is absent.");
                Vector3 berth=station.position+Vector3.down*4;
                var records=new List<string>();
                foreach(string hullName in new[]{"CapeIslander","Dory","Tanker"})
                {
                    var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/"+hullName+"IsoHullMesh.asset");
                    if(def==null)throw new InvalidOperationException("The proof hull def is absent: "+hullName);
                    bool atBerth=hullName=="CapeIslander";
                    Vector3 place=atBerth?berth:berth+new Vector3(hullName=="Tanker"?80:0,-55,0);
                    using var shot=new SunStage(hullName=="Tanker"?180:atBerth?30:16,1024,false);
                    using var clock=new ProofClock(shot.Camera);
                    shot.Camera.cullingMask=~0;
                    shot.Camera.transform.position=new Vector3(place.x,place.y,-1000);
                    shot.Camera.farClipPlane=2500;
                    var host=shot.Own(new GameObject("SunProof"+hullName));host.transform.position=place;
                    var hull=(IsoFacetHullRenderer)new IsoFacetHullPresentationService().Install(host,def);
                    hull.HeadingDirUnits=atBerth?2:1;hull.RollDegrees=atBerth?0:3;hull.ApplyPose();
                    var caster=host.GetComponent<HullSunShadowCaster>();
                    shot.AddLamp(new Vector2(place.x+2,place.y+2));
                    var tintRenderers=new List<SpriteRenderer>();
                    foreach(var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                        if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="HiddenHarbours/DayNight")tintRenderers.Add(renderer);
                    if(tintRenderers.Count==0)
                    {
                        var overlay=shot.Sprite("Proof day and night",new Vector2(place.x,place.y),Vector2.one*shot.Camera.orthographicSize*3,
                            Color.white,SortingBands.WorldTint);
                        overlay.sharedMaterial=shot.Own(new Material(Shader.Find("HiddenHarbours/DayNight")));
                        tintRenderers.Add(overlay);
                    }
                    foreach(float hour in hullName=="Tanker"?new[]{17f}:new[]{8f,17f,22.5f})
                    {
                        SetSun(hour);
                        var tintBlock=new MaterialPropertyBlock();
                        tintBlock.SetColor("_DayNightTint",Shader.GetGlobalColor("_DayNightTint"));
                        foreach(var renderer in tintRenderers)renderer.SetPropertyBlock(tintBlock);
                        shot.Camera.backgroundColor=hour>20?new Color(0.015f,0.025f,0.05f):new Color(0.18f,0.3f,0.35f);
                        string time=hour==8?"0800":hour==17?"1700":"2230";
                        string key=hullName.ToLowerInvariant()+"-"+(atBerth?"berth":"sea")+"-"+time;
                        caster.enabled=false;caster.Tick();
                        byte[] before=shot.Png();File.WriteAllBytes(Path.Combine(folder,"before-"+key+".png"),before);
                        Color32[] off=shot.LastPixels;
                        caster.enabled=true;caster.Tick();
                        byte[] after=shot.Png();File.WriteAllBytes(Path.Combine(folder,"after-"+key+".png"),after);
                        int changed=Changed(off,shot.LastPixels);
                        if(hour<20&&changed==0)throw new InvalidOperationException("The daytime shadow changed no pixels: "+key);
                        if(hour>20&&changed!=0)throw new InvalidOperationException("The sun caster changed the night frame: "+key);
                        records.Add(JsonUtility.ToJson(new FrameRecord{key=key,scene=scene.path,station=atBerth?station.name:"south of "+station.name,
                            x=place.x,y=place.y,hour=hour,heading=hull.HeadingDirUnits,roll=hull.RollDegrees,heave=hull.HeavePixels,ride=hull.RidePixels,
                            ortho=shot.Camera.orthographicSize,width=shot.Size,height=shot.Size,changed=changed,frame=Time.frameCount,
                            format=shot.TargetFormat,graphics=SystemInfo.graphicsDeviceType.ToString()}));
                    }
                }
                File.WriteAllText(Path.Combine(folder,"frames.json"),"["+string.Join(",\n",records)+"]");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        [Serializable] class FrameRecord
        { public string key,scene,station,format,graphics;public float x,y,hour,heading,roll,heave,ride,ortho;public int width,height,changed,frame; }

        // Editor URP resets shader time from real elapsed time for every Camera.Render.
        // Freeze it after camera setup and before sprites so only the caster arm differs.
        internal sealed class ProofClock:IDisposable
        {
            readonly Camera _camera;
            readonly ClockPass _pass=new ClockPass();
            public ProofClock(Camera camera){_camera=camera;RenderPipelineManager.beginCameraRendering+=Begin;}
            void Begin(ScriptableRenderContext context,Camera camera)
            {if(camera==_camera)camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);}
            public void Dispose(){RenderPipelineManager.beginCameraRendering-=Begin;}
            sealed class ClockPass:ScriptableRenderPass2D
            {
                sealed class Data{}
                public ClockPass(){renderPassEvent2D=RenderPassEvent2D.BeforeRenderingSprites;renderPassSortingLayerID=0;}
                public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
                {
                    using var builder=graph.AddUnsafePass<Data>("Proof frozen shader clock",out _);
                    builder.AllowPassCulling(false);builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data data,UnsafeGraphContext context)=>
                    {
                        const float t=42f;
                        context.cmd.SetGlobalVector("_Time",new Vector4(t/20,t,t*2,t*3));
                        context.cmd.SetGlobalVector("_SinTime",new Vector4(Mathf.Sin(t/8),Mathf.Sin(t/4),Mathf.Sin(t/2),Mathf.Sin(t)));
                        context.cmd.SetGlobalVector("_CosTime",new Vector4(Mathf.Cos(t/8),Mathf.Cos(t/4),Mathf.Cos(t/2),Mathf.Cos(t)));
                        context.cmd.SetGlobalVector("unity_DeltaTime",new Vector4(1f/60,60,1f/60,60));
                        context.cmd.SetGlobalVector("_TimeParameters",new Vector4(t,Mathf.Sin(t),Mathf.Cos(t),0));
                        context.cmd.SetGlobalVector("_LastTimeParameters",new Vector4(t,Mathf.Sin(t),Mathf.Cos(t),0));
                    });
                }
            }
        }

        static SunStage _counterStage;
        static HullSunShadowCaster _counterCaster;
        static ProfilerRecorder _draws,_batches;
        static int _counterFrame,_lastCounterFrame;
        static long _offDraws,_offBatches;
        const string CostPending="HullSunShadowCostPending";
        public static void ReadCostOnce()
        {
            RequireGpu();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(CostPending,true);
            AwaitCostMode();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void AwaitCostMode()
        {
            if(!SessionState.GetBool(CostPending,false))return;
            EditorApplication.playModeStateChanged-=CostModeChanged;
            EditorApplication.playModeStateChanged+=CostModeChanged;
        }

        static void CostModeChanged(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode)return;
            EditorApplication.playModeStateChanged-=CostModeChanged;
            SessionState.SetBool(CostPending,false);
            EditorApplication.delayCall+=StartCostCounter;
        }

        static void StartCostCounter()
        {
            try
            {
                RequireGpu();
                _counterStage=new SunStage();
                _counterStage.Shadow.enabled=false;
                var host=_counterStage.Own(new GameObject("CostHull"){layer=31});
                var mesh=_counterStage.Own(HullSunShadowFixture.Box());
                host.AddComponent<IsoFacetHullRenderer>().Configure(HullSunShadowFixture.Setup(mesh));
                SetSun(17);
                _counterCaster=HullSunShadowCaster.Fit(host);
                for(int i=0;i<32;i++)_counterCaster.Tick();
                _counterCaster.ShadowRenderer.forceRenderingOff=true;
                Profiler.enabled=true;
                _counterFrame=0;
                _lastCounterFrame=Time.frameCount;
                EditorApplication.update+=CounterUpdate;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void CounterUpdate()
        {
            try
            {
                if(Time.frameCount==_lastCounterFrame)return;
                _lastCounterFrame=Time.frameCount;
                _counterFrame++;
                if(_counterFrame==5)
                {
                    var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
                    var offered=new List<string>();
                    foreach(var handle in handles)
                    {
                        var d=ProfilerRecorderHandle.GetDescription(handle);
                        if(d.Category==ProfilerCategory.Render||d.Name=="Draw Calls Count"||d.Name=="Batches Count")offered.Add(d.Category.Name+": "+d.Name);
                        if(d.Name=="Draw Calls Count")_draws=ProfilerRecorder.StartNew(d.Category,d.Name);
                        if(d.Name=="Batches Count")_batches=ProfilerRecorder.StartNew(d.Category,d.Name);
                    }
                    File.WriteAllLines(Path.Combine("artifacts","boats-look","pr2","c","offered-render-counters.txt"),offered);
                }
                if(_counterFrame==10){_offDraws=_draws.LastValue;_offBatches=_batches.LastValue;_counterCaster.ShadowRenderer.forceRenderingOff=false;}
                if(_counterFrame<15)return;
                if(!_draws.Valid||!_batches.Valid)throw new InvalidOperationException("The rendering counters are unavailable in PlayMode.");
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<256;i++)_counterCaster.Tick();
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                var result=new CostRecord{drawsOff=_offDraws,drawsOn=_draws.LastValue,batchesOff=_offBatches,batchesOn=_batches.LastValue,
                    drawDelta=_draws.LastValue-_offDraws,batchDelta=_batches.LastValue-_offBatches,managedBytes=allocated,
                    proxyBytes=Profiler.GetRuntimeMemorySizeLong(_counterCaster.Proxy),submeshes=_counterCaster.Proxy.subMeshCount,
                    drawsValid=_draws.Valid,batchesValid=_batches.Valid};
                File.WriteAllText(Path.Combine("artifacts","boats-look","pr2","c","cost.json"),JsonUtility.ToJson(result,true));
                EditorApplication.update-=CounterUpdate;_draws.Dispose();_batches.Dispose();_counterStage.Dispose();
                EditorApplication.Exit(allocated==0?0:1);
            }
            catch(Exception e){EditorApplication.update-=CounterUpdate;Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] class CostRecord
        { public long drawsOff,drawsOn,batchesOff,batchesOn,drawDelta,batchDelta,managedBytes,proxyBytes;public int submeshes;public bool drawsValid,batchesValid; }

        internal sealed class SunStage:IDisposable
        {
            readonly List<Object> _objects=new List<Object>();
            readonly RenderTexture _target;
            readonly Texture2D _read;
            Texture2D _mask;
            public readonly Camera Camera;
            public readonly int Size;
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly MeshRenderer Shadow;
            public Color32[] LastPixels {get;private set;}
            public string TargetFormat => _target.graphicsFormat.ToString();
            public T Own<T>(T value) where T:Object {_objects.Add(value);return value;}
            public SunStage(float metres=10,int size=256,bool ground=true)
            {
                Size=size;
                Camera=Own(new GameObject("SunShadowCamera")).AddComponent<Camera>();
                Camera.orthographic=true;Camera.orthographicSize=metres/2;Camera.transform.position=new Vector3(0,0,-20);
                Camera.nearClipPlane=0.1f;Camera.farClipPlane=100;Camera.clearFlags=CameraClearFlags.SolidColor;
                Camera.backgroundColor=Color.gray;Camera.allowHDR=false;Camera.allowMSAA=false;Camera.cullingMask=1<<31;
                _target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};Camera.targetTexture=_target;
                _read=new Texture2D(size,size,TextureFormat.RGBA32,false);
                Mesh=Own(new Mesh());Mesh.vertices=new[]{new Vector3(-2,-1,1),new Vector3(2,-1,1),new Vector3(2,1,1),new Vector3(-2,1,1)};
                Mesh.triangles=new[]{0,1,2,0,2,3};Mesh.SetUVs(0,new List<Vector4>{Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
                Mesh.SetUVs(1,new List<Vector2>{Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero});Mesh.RecalculateBounds();
                Material=Own(new Material(Resources.Load<Material>(HullSunShadowCaster.MaterialPath)));
                Material.SetMatrix("_HHSunPose",Matrix4x4.identity);
                Material.SetMatrix("_HHSunViewPose",Matrix4x4.identity);
                Material.SetVector("_HHSunProjection",HullSunShadowMath.Projection(Vector2.right,1.5f,40,0,0,32));
                Material.SetVector("_HullIds",new Vector4(23/255f,100/255f,12,0));
                Material.SetColor("_ShadowColor",new Color(0.04f,0.05f,0.1f,0.3f));
                Material.SetVectorArray("_HHRoleLiftMasks",new Vector4[31]);
                var draw=Own(new GameObject("ShadeFixture"){layer=31});draw.AddComponent<MeshFilter>().sharedMesh=Mesh;
                Shadow=draw.AddComponent<MeshRenderer>();Shadow.sharedMaterial=Material;Shadow.sortingOrder=SortingBands.SunShade;
                draw.AddComponent<SortingGroup>().sortingOrder=SortingBands.SunShade;
                Shadow.localBounds=new Bounds(Vector3.zero,Vector3.one*50);
                Mask(0);
                if(ground)Sprite("ground",Vector2.zero,Vector2.one*metres,Color.gray,-10);
                else Shadow.enabled=false;
            }
            public void Mask(byte id)
            {
                if(_mask!=null)Object.DestroyImmediate(_mask);
                _mask=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true);
                var pixels=new Color32[Size*Size];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,0,0,id);
                _mask.SetPixels32(pixels);_mask.Apply();Material.SetTexture("_HHHullScreenTex",_mask);
            }
            public SpriteRenderer Sprite(string name,Vector2 at,Vector2 size,Color color,int order)
            {
                var texture=Own(new Texture2D(1,1));texture.SetPixel(0,0,Color.white);texture.Apply();
                var sprite=Own(UnityEngine.Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(0.5f,0.5f),1));
                var go=Own(new GameObject(name){layer=31});go.transform.position=at;go.transform.localScale=new Vector3(size.x,size.y,1);
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=order;
                renderer.sharedMaterial=Own(new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")));
                return renderer;
            }
            public void AddLamp(Vector2 position)
            {
                var go=Own(new GameObject("ProofLamp"){layer=31});go.transform.position=position;
                var lamp=go.AddComponent<SceneLight>();lamp.Range=6;lamp.Intensity=1.5f;lamp.FlickerAmount=0;
                typeof(SceneLight).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lamp,null);
                typeof(SceneLight).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lamp,null);
                foreach(Transform child in go.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
            }
            public int PixelOf(Vector2 world)
            {Vector3 p=Camera.WorldToViewportPoint(world);return Mathf.Clamp((int)(p.y*Size),0,Size-1)*Size+Mathf.Clamp((int)(p.x*Size),0,Size-1);}
            public Color32[] Shoot(bool on){Shadow.enabled=on;return Read();}
            public Color32[] Read()
            {
                Camera.Render();Camera.Render();
                RenderTexture old=RenderTexture.active;RenderTexture.active=_target;
                _read.ReadPixels(new Rect(0,0,Size,Size),0,0);_read.Apply();RenderTexture.active=old;
                return LastPixels=_read.GetPixels32();
            }
            public byte[] Png(){Read();return _read.EncodeToPNG();}
            public void Dispose()
            {
                Camera.targetTexture=null;_target.Release();Object.DestroyImmediate(_target);Object.DestroyImmediate(_read);
                if(_mask!=null)Object.DestroyImmediate(_mask);
                for(int i=_objects.Count-1;i>=0;i--)if(_objects[i]!=null)Object.DestroyImmediate(_objects[i]);
            }
        }
    }
}
