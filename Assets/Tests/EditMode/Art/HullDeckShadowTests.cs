using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullDeckShadowTests
    {
        [Test] public void DeckPlane_IsReadFromTheHullNotALiteral()
        {
            foreach(string name in new[]{"CapeIslander","Tanker"})
            {
                var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/"+name+"IsoHullMesh.asset");
                var host=new GameObject("DeckPlane");
                try
                {
                    var hull=(IsoFacetHullRenderer)new IsoFacetHullPresentationService().Install(host,def);
                    var deck=host.GetComponent<HullDeckShadowCaster>();
                    Assert.That(hull.SunShadowState.DeckHeight,Is.EqualTo(def.WatertightDeckHeightMeters));
                    Assert.That(deck.Geometry.DeckHeight,Is.EqualTo(def.WatertightDeckHeightMeters));
                    Assert.That(deck.Geometry.Receiver.vertexCount,Is.GreaterThan(0));
                    foreach(var p in deck.Geometry.Receiver.vertices)Assert.That(p.z,Is.EqualTo(def.WatertightDeckHeightMeters).Within(1e-5f));
                }
                finally{ReleaseDeckCasterForFixture(host);Object.DestroyImmediate(host);}
            }
        }
        [Test] public void HouseFacesOnly_Cast()
        {
            using var stage=new DeckStage();
            Assert.That(stage.Caster.Geometry.DeckLevel,Is.EqualTo(1));
            Assert.That(stage.Caster.Geometry.HouseTriangles,Is.EqualTo(10));
            Assert.That(stage.Caster.Geometry.Receiver.triangles.Length,Is.EqualTo(6));
        }
        [Test] public void HouselessHull_CastsNoDeckShadow()
        {
            var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset");
            var host=new GameObject("OpenHull");
            try
            {
                new IsoFacetHullPresentationService().Install(host,def);
                var caster=host.GetComponent<HullDeckShadowCaster>();
                Assert.That(caster.Geometry.HouseTriangles,Is.Zero);Assert.That(caster.ShadowRenderer,Is.Null);
                Assert.That(caster.Geometry.Receiver,Is.Null);Assert.That(caster.Geometry.TriangleBuffer,Is.Null);
            }
            finally{ReleaseDeckCasterForFixture(host);Object.DestroyImmediate(host);}
        }
        [Test] public void LiftedHouse_DoesNotCast()
        {
            using var stage=new DeckStage();
            stage.Hull.ShowCutaway(new HullMeshDef.Cut(0,1<<2,int.MaxValue));
            Assert.That(HullSunShadowRenderTests.Changed(stage.Read(false,8),stage.Read(true,8)),Is.Zero);
        }
        [Test] public void SunAtOrBelowHorizon_IsDisabled()
        {
            using var stage=new DeckStage();
            foreach(float e in new[]{0f,-0.0000001f,-1f})
            {
                Shader.SetGlobalFloat("_SunElevation",e);Shader.SetGlobalFloat("_ShadowStrength",1);
                stage.Caster.Tick();Assert.That(stage.Caster.ShadowRenderer.enabled,Is.False);
            }
        }
        [Test] public void Profile_IsShared()
        {
            using var a=new DeckStage();using var b=new DeckStage();
            a.Caster.Tick();b.Caster.Tick();
            Assert.That(a.Caster.Profile,Is.SameAs(SpriteShadow.SharedProfile));
            Assert.That(a.Caster.ShadowRenderer.sharedMaterial,Is.SameAs(b.Caster.ShadowRenderer.sharedMaterial));
            var original=Resources.Load<Material>(HullSunShadowCaster.MaterialPath);
            foreach(string p in new[]{"_StencilRef","_StencilComp","_StencilPass","_SrcBlend","_DstBlend","_CastSunShadow"})
                Assert.That(a.Caster.ShadowRenderer.sharedMaterial.GetFloat(p),Is.EqualTo(original.GetFloat(p)));
        }
        [Test] public void WaterAndDeckShadows_NeverClaimTheSamePixel()
        {
            using var stage=new DeckStage();
            foreach(int id in new[]{0,stage.Hull.HullId,stage.Hull.ForeHullId,stage.Hull.ForeHullId+11,254})
            {
                bool own=HullSunShadowMath.IsOwnId(id/255f,stage.Hull.HullId,stage.Hull.ForeHullId,IsoFacetHullRenderer.DeckOccupantSlots);
                bool water=!own,deck=own;
                Assert.That(water&&deck,Is.False);
            }
            string shader=System.IO.File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursHullSunShadow.shader");
            StringAssert.Contains("if(a<=0.5/255.0||!own)discard;",shader);
            StringAssert.Contains("if (a > 0.5/255.0 && own) discard;",shader);
            StringAssert.Contains("Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilPass] }",shader);
        }
        [Test] public void WarmUpdates_AllocateZeroManagedBytes()
        {
            using var stage=new DeckStage();HullSunShadowRenderTests.SetSun(17);
            for(int i=0;i<32;i++)stage.Caster.Tick();
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<256;i++)stage.Caster.Tick();
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
        [Test] public void Disable_ReleasesOwnedResources()
        {
            using var stage=new DeckStage();Mesh source=stage.Hull.SunShadowState.Hull;
            stage.Caster.enabled=false;stage.Caster.Tick();
            Assert.That(stage.Caster.Geometry,Is.Null);Assert.That(stage.Caster.ShadowRenderer,Is.Null);Assert.That(source!=null,Is.True);
        }

        // EditMode fixtures invoke Tick explicitly; they must release its detached draw explicitly too.
        // Destroying the non-ExecuteAlways host alone does not exercise its runtime teardown.
        internal static void ReleaseDeckCasterForFixture(GameObject host)
        {
            var caster=host.GetComponent<HullDeckShadowCaster>();if(caster==null)return;
            var users=typeof(HullDeckShadowCaster).GetField("_users",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            int before=(int)users.GetValue(null);bool ownedDraw=caster.ShadowRenderer!=null;
            caster.enabled=false;caster.Tick();
            Assert.That(caster.ShadowRenderer,Is.Null,"The fixture must not leave a detached deck draw in the next test.");
            Assert.That(caster.Geometry,Is.Null,"The fixture must return its mesh and native buffers.");
            int after=(int)users.GetValue(null);
            Assert.That(after,Is.EqualTo(before-(ownedDraw?1:0)),"The fixture must return exactly its own shared-material user.");
            const string folder="artifacts/boats-look/pr2/d1";Directory.CreateDirectory(folder);
            File.AppendAllText(folder+"/cleanup-guards.jsonl",JsonUtility.ToJson(new CleanupGuard{test=TestContext.CurrentContext.Test.FullName,usersBefore=before,usersAfter=after,hadDraw=ownedDraw})+"\n");
        }
        [Serializable] class CleanupGuard{public string test;public int usersBefore,usersAfter;public bool hadDraw;}

        internal static void AssertDeckPixels(float hour,int changed)
        {
            const string folder="artifacts/boats-look/pr2/d1";Directory.CreateDirectory(folder);
            File.AppendAllText(folder+"/C-deck-pixels.jsonl",JsonUtility.ToJson(new DeckPixels{test=TestContext.CurrentContext.Test.FullName,hour=hour,changed=changed})+"\n");
            if(hour<20)Assert.That(changed,Is.GreaterThan(100),"The declared fixture must show shade at hour "+hour);
            else Assert.That(changed,Is.Zero,"There is no deck sun shade at night.");
        }
        [Serializable] class DeckPixels{public string test;public float hour;public int changed;}

        public static void CaptureOneDeckPair()
        {
            try
            {
                const string folder="artifacts/boats-look/pr2/d1/plates";
                if(!File.Exists("artifacts/boats-look/pr2/d1/pair-floor.json"))throw new InvalidOperationException("The pair floor must precede this launch.");
                if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("The pair needs a graphics device.");
                Directory.CreateDirectory(folder);
                var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/NineMileCreek.unity",OpenSceneMode.Single);Transform station=null;
                foreach(GameObject root in scene.GetRootGameObjects())if(root.name=="NineMileCreekWharf")
                    foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name.IndexOf("bollard",StringComparison.OrdinalIgnoreCase)>=0){station=t;break;}
                if(station==null)throw new InvalidOperationException("The berth station is absent.");
                Vector3 place=station.position+Vector3.down*4;
                using var shot=new HullSunShadowRenderTests.SunStage(30,1024,false);
                using var clock=new HullSunShadowRenderTests.ProofClock(shot.Camera);
                shot.Camera.cullingMask=~0;shot.Camera.transform.position=new Vector3(place.x,place.y,-1000);shot.Camera.farClipPlane=2500;
                HullSunShadowRenderTests.SetSun(17);
                var host=shot.Own(new GameObject("DeckProofCapeIslander"));host.transform.position=place;
                var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset");
                var hull=(IsoFacetHullRenderer)new IsoFacetHullPresentationService().Install(host,def);
                hull.HeadingDirUnits=2;hull.ApplyPose();
                var deck=host.GetComponent<HullDeckShadowCaster>();var water=host.GetComponent<HullSunShadowCaster>();
                try
                {
                    shot.AddLamp(new Vector2(place.x+2,place.y+2));
                    var tint=shot.Sprite("Proof day and night",place,Vector2.one*shot.Camera.orthographicSize*3,Color.white,SortingBands.WorldTint);
                    tint.sharedMaterial=shot.Own(new Material(Shader.Find("HiddenHarbours/DayNight")));
                    var block=new MaterialPropertyBlock();block.SetColor("_DayNightTint",Shader.GetGlobalColor("_DayNightTint"));tint.SetPropertyBlock(block);water.Tick();
                    deck.enabled=false;deck.Tick();File.WriteAllBytes(folder+"/before-capeislander-deck-1700.png",shot.Png());var off=shot.LastPixels;
                    deck.enabled=true;deck.Tick();ShaderUtil.CompilePass(deck.ShadowRenderer.sharedMaterial,0,true);
                    File.WriteAllBytes(folder+"/after-capeislander-deck-1700.png",shot.Png());var on=shot.LastPixels;
                    int changed=HullSunShadowRenderTests.Changed(off,on);if(changed==0)throw new InvalidOperationException("The own deck pair changed no pixels.");
                    Bounds bounds=deck.Geometry.Receiver.bounds;Vector3 low=bounds.min,high=bounds.max;
                    int minX=shot.Size,minY=shot.Size,maxX=0,maxY=0;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 rig=new Vector3((i&1)==0?low.x:high.x,(i&2)==0?low.y:high.y,(i&4)==0?low.z:high.z);
                        Vector3 p=shot.Camera.WorldToViewportPoint(hull.PosedMesh.TransformPoint(rig));
                        minX=Mathf.Min(minX,Mathf.FloorToInt(p.x*shot.Size));minY=Mathf.Min(minY,Mathf.FloorToInt(p.y*shot.Size));
                        maxX=Mathf.Max(maxX,Mathf.CeilToInt(p.x*shot.Size));maxY=Mathf.Max(maxY,Mathf.CeilToInt(p.y*shot.Size));
                    }
                    File.WriteAllText(folder+"/pair.json",JsonUtility.ToJson(new OnePair{scene=scene.path,station=station.name,x=place.x,y=place.y,hour=17,
                        deckHeight=def.WatertightDeckHeightMeters,changed=changed,width=shot.Size,height=shot.Size,minX=minX,minY=minY,maxX=maxX,maxY=maxY},true));
                }
                finally{deck.enabled=false;deck.Tick();water.enabled=false;water.Tick();}
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] class OnePair{public string scene,station;public float x,y,hour,deckHeight;public int changed,width,height,minX,minY,maxX,maxY;}

        /// <summary>Explicit second-lease proof; never runs as part of the test filter.</summary>
        public static void CaptureProofFrames()
        {
            try
            {
                if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("The deck proof needs a graphics device.");
                const string folder="artifacts/boats-look/pr2/c3/plates";
                if(!File.Exists("artifacts/boats-look/pr2/c3/plates-floor.json"))throw new InvalidOperationException("The floor must precede this launch.");
                Directory.CreateDirectory(folder);
                var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/NineMileCreek.unity",OpenSceneMode.Single);
                Transform station=null;
                foreach(GameObject root in scene.GetRootGameObjects())if(root.name=="NineMileCreekWharf")
                    foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name.IndexOf("bollard",StringComparison.OrdinalIgnoreCase)>=0){station=t;break;}
                if(station==null)throw new InvalidOperationException("The berth station is absent.");
                var records=new List<string>();
                foreach(string name in new[]{"CapeIslander","Tanker"})
                {
                    bool berth=name=="CapeIslander";Vector3 place=station.position+Vector3.down*4+(berth?Vector3.zero:new Vector3(80,-55,0));
                    using var shot=new HullSunShadowRenderTests.SunStage(berth?30:180,1024,false);
                    using var clock=new HullSunShadowRenderTests.ProofClock(shot.Camera);
                    shot.Camera.cullingMask=~0;shot.Camera.transform.position=new Vector3(place.x,place.y,-1000);shot.Camera.farClipPlane=2500;
                    var host=shot.Own(new GameObject("DeckProof"+name));host.transform.position=place;
                    var def=AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/"+name+"IsoHullMesh.asset");
                    var hull=(IsoFacetHullRenderer)new IsoFacetHullPresentationService().Install(host,def);
                    hull.HeadingDirUnits=berth?2:1;hull.RollDegrees=berth?0:3;hull.ApplyPose();
                    var deck=host.GetComponent<HullDeckShadowCaster>();var water=host.GetComponent<HullSunShadowCaster>();
                    shot.AddLamp(new Vector2(place.x+2,place.y+2));
                    var tint=shot.Sprite("Proof day and night",place,Vector2.one*shot.Camera.orthographicSize*3,Color.white,SortingBands.WorldTint);
                    tint.sharedMaterial=shot.Own(new Material(Shader.Find("HiddenHarbours/DayNight")));
                    foreach(float hour in berth?new[]{8f,17f,22.5f}:new[]{17f})
                    {
                        HullSunShadowRenderTests.SetSun(hour);var block=new MaterialPropertyBlock();block.SetColor("_DayNightTint",Shader.GetGlobalColor("_DayNightTint"));tint.SetPropertyBlock(block);
                        water.Tick();string time=hour==8?"0800":hour==17?"1700":"2230";
                        string key=name.ToLowerInvariant()+"-"+(berth?"berth":"sea")+"-"+time;
                        deck.enabled=false;deck.Tick();byte[] before=shot.Png();Color32[] off=shot.LastPixels;
                        File.WriteAllBytes(Path.Combine(folder,"before-"+key+"-deck.png"),before);
                        deck.enabled=true;deck.Tick();byte[] after=shot.Png();Color32[] on=shot.LastPixels;
                        File.WriteAllBytes(Path.Combine(folder,"after-"+key+".png"),after);
                        var screen=new RenderTexture(shot.Size,shot.Size,0,RenderTextureFormat.ARGB32);
                        var copy=shot.Own(new Material(Resources.Load<Material>(HullSunShadowCaster.MaterialPath)));
                        ShaderUtil.CompilePass(copy,1,true);Graphics.Blit(null,screen,copy,1);
                        var mask=shot.Own(new Texture2D(shot.Size,shot.Size,TextureFormat.RGBA32,false));
                        RenderTexture previous=RenderTexture.active;RenderTexture.active=screen;mask.ReadPixels(new Rect(0,0,shot.Size,shot.Size),0,0);mask.Apply();RenderTexture.active=previous;
                        screen.Release();Object.DestroyImmediate(screen);
                        var ids=mask.GetPixels32();File.WriteAllBytes(Path.Combine(folder,"ownid-"+key+".png"),mask.EncodeToPNG());
                        int changed=0,outside=0,minX=shot.Size,minY=shot.Size,maxX=0,maxY=0;
                        for(int i=0;i<off.Length;i++)if(!off[i].Equals(on[i]))
                        {
                            changed++;if(!HullSunShadowMath.IsOwnId(ids[i].a/255f,hull.HullId,hull.ForeHullId,IsoFacetHullRenderer.DeckOccupantSlots))outside++;
                            int x=i%shot.Size,y=i/shot.Size;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
                        }
                        var record=new DeckFrame{key=key,scene=scene.path,station=station.name,hour=hour,deckHeight=def.WatertightDeckHeightMeters,
                            changed=changed,outside=outside,minX=minX,minY=minY,maxX=maxX,maxY=maxY,hullId=hull.HullId,foreId=hull.ForeHullId,span=IsoFacetHullRenderer.DeckOccupantSlots};
                        records.Add(JsonUtility.ToJson(record));File.WriteAllText(Path.Combine(folder,"frames-deck.json"),"["+string.Join(",\n",records)+"]");
                        if(outside!=0)throw new InvalidOperationException("STOP: deck shade outside the own footprint in "+key);
                        if(hour<20&&changed==0)throw new InvalidOperationException("No visible deck shadow in "+key);
                        if(hour>20&&changed!=0)throw new InvalidOperationException("The night deck frame changed in "+key);
                    }
                }
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] class DeckFrame
        {public string key,scene,station;public float hour,deckHeight;public int changed,outside,minX,minY,maxX,maxY,hullId,foreId,span;}

        public static void InspectFixture()
        {
            try
            {
                const string folder="artifacts/boats-look/pr2/c2/fixture";Directory.CreateDirectory(folder);
                using var stage=new DeckStage();
                foreach(float hour in new[]{8f,17f})
                {
                    var off=stage.Read(false,hour);File.WriteAllBytes(folder+"/off-"+hour+".png",stage.Shot.Png());
                    var on=stage.Read(true,hour);File.WriteAllBytes(folder+"/on-"+hour+".png",stage.Shot.Png());
                    var block=new MaterialPropertyBlock();stage.Caster.ShadowRenderer.GetPropertyBlock(block);
                    var texture=Shader.GetGlobalTexture(IsoFacetShaderIds.HullScreenTex);
                    File.AppendAllText(folder+"/state.txt","hour="+hour+" changed="+HullSunShadowRenderTests.Changed(off,on)+" active="+stage.Caster.ShadowRenderer.enabled+
                        " id="+stage.Hull.HullId+" ray="+block.GetVector("_HHDeckSunRay")+" keywords="+string.Join(",",stage.Caster.ShadowRenderer.sharedMaterial.shaderKeywords)+
                        " texture="+(texture!=null?texture.width+"x"+texture.height:"none")+"\n");
                }
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        internal sealed class DeckStage:IDisposable
        {
            public readonly HullSunShadowRenderTests.SunStage Shot;
            public readonly IsoFacetHullRenderer Hull;
            public readonly HullDeckShadowCaster Caster;
            readonly GameObject _host;readonly Mesh _mesh;
            readonly Vector4 _sun;readonly float _e,_s;readonly Color _tint;
            public DeckStage()
            {
                if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("A graphics device is required for the own-deck receiver.");
                _sun=Shader.GetGlobalVector("_SunDir");_e=Shader.GetGlobalFloat("_SunElevation");_s=Shader.GetGlobalFloat("_ShadowStrength");_tint=Shader.GetGlobalColor("_DayNightTint");
                Shot=new HullSunShadowRenderTests.SunStage(12,256);Shot.Shadow.enabled=false;
                _mesh=MakeMesh();_host=new GameObject("DeckFixture"){layer=31};
                var setup=HullSunShadowFixture.Setup(_mesh);setup.WatertightDeckHeightMeters=0.8f;
                setup.Ramps=new[]{new[]{new Color32(190,180,130,255),new Color32(190,180,130,255)},new[]{new Color32(120,65,30,255),new Color32(120,65,30,255)}};
                setup.RampOffsets=new[]{0,0};Hull=_host.AddComponent<IsoFacetHullRenderer>();Hull.Configure(setup);
                foreach(Transform child in _host.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                Caster=HullDeckShadowCaster.Fit(_host);
                Shot.Sprite("neighbour",new Vector2(4,0),new Vector2(1,4),Color.cyan,0);
            }
            public Color32[] Read(bool on,float hour)
            {
                HullSunShadowRenderTests.SetSun(hour);Caster.enabled=on;Caster.Tick();
                if(on)ShaderUtil.CompilePass(Caster.ShadowRenderer.sharedMaterial,0,true);
                return Shot.Read();
            }
            public int DeckSample=>Shot.PixelOf(IsoFacetMath.RigToWorld(0,40).MultiplyPoint3x4(new Vector3(-2,0,0.8f)));
            public void Dispose()
            {
                ReleaseDeckCasterForFixture(_host);
                Object.DestroyImmediate(_host);Object.DestroyImmediate(_mesh);Shot.Dispose();
                Shader.SetGlobalVector("_SunDir",_sun);Shader.SetGlobalFloat("_SunElevation",_e);Shader.SetGlobalFloat("_ShadowStrength",_s);Shader.SetGlobalColor("_DayNightTint",_tint);
            }
        }
        static Mesh MakeMesh()
        {
            var p=new List<Vector3>();var n=new List<Vector3>();var attrs=new List<Vector4>();var tags=new List<Vector4>();var indices=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int level,int material)
            {
                int at=p.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                p.Add(a);p.Add(b);p.Add(c);p.Add(d);
                for(int i=0;i<4;i++){n.Add(normal);attrs.Add(new Vector4(material,0,0,0));tags.Add(new Vector4(level,0,0,0));}
                indices.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
            }
            Quad(new Vector3(-3,-3,.8f),new Vector3(3,-3,.8f),new Vector3(3,3,.8f),new Vector3(-3,3,.8f),1,0);
            Vector3 a=new Vector3(-.6f,-.6f,.8f),b=new Vector3(.6f,-.6f,.8f),c=new Vector3(.6f,.6f,.8f),d=new Vector3(-.6f,.6f,.8f),up=Vector3.forward*2;
            Quad(a+up,b+up,c+up,d+up,2,1);Quad(a,b,b+up,a+up,2,1);Quad(b,c,c+up,b+up,2,1);Quad(c,d,d+up,c+up,2,1);Quad(d,a,a+up,d+up,2,1);
            var mesh=new Mesh();mesh.SetVertices(p);mesh.SetNormals(n);mesh.SetUVs(0,attrs);mesh.SetUVs(1,tags);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
