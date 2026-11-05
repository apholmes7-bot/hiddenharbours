using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;
namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        [Serializable] sealed class LandingPropsHeight { public string key,path; public float minimumLocalZ,maximumLocalZ; }
        [Serializable] sealed class LandingPropsHeightContract { public LandingPropsHeight[] maps; }
        static string LandingPropsPlates=>Path.GetFullPath("Evidence~/stp-props/phase-b2/plates");
        void LandingPropsCamera(Vector2 centre,float height=14)
        {
            _follow.enabled=false;
            foreach(var c in _cam.GetComponents<Behaviour>().Where(x=>x.GetType().Name=="PixelPerfectCamera"))c.enabled=false;
            if(_rt==null||_rt.width!=796||_rt.height!=448)
            {
                if(_rt!=null){_rt.Release();Object.DestroyImmediate(_rt);}
                _w=796;_h=448;_rt=new RenderTexture(_w,_h,24,RenderTextureFormat.ARGBHalf);_rt.Create();
            }
            _cam.targetTexture=_rt;_cam.transform.position=new Vector3(centre.x,centre.y,_cam.transform.position.z);
            _cam.orthographicSize=height/2;_cam.aspect=24.89f/14;
        }
        void LandingPropsSave(string name)
        {
            Directory.CreateDirectory(LandingPropsPlates);
            var texture=new Texture2D(_w,_h,TextureFormat.RGBA32,false);
            try{texture.LoadRawTextureData(Capture());texture.Apply();File.WriteAllBytes(Path.Combine(LandingPropsPlates,name+".png"),texture.EncodeToPNG());}
            finally{Object.DestroyImmediate(texture);}
        }
        IEnumerator LandingPropsTide(IEnvironmentService inner,float level)
        {
            GameServices.Environment=new LandingBoardTide(inner,level);Time.timeScale=1;
            float until=Time.realtimeSinceStartup+15;
            do{yield return null;}while(Time.realtimeSinceStartup<until&&
                (Shader.GetGlobalVector("_HHSeaLevelWorld").w<.5f||Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x-level)>.01f));
            Assert.That(Shader.GetGlobalVector("_HHSeaLevelWorld").x,Is.EqualTo(level).Within(.01f));
            for(int i=0;i<5;i++)yield return null;
            Time.timeScale=0;
        }
        void LandingPropsReleaseCamera()
        {
            _follow.enabled=true;foreach(var c in _cam.GetComponents<Behaviour>().Where(x=>x.GetType().Name=="PixelPerfectCamera"))c.enabled=true;
            _cam.targetTexture=null;_cam.ResetAspect();Time.timeScale=1;
        }
        [UnityTest] public IEnumerator StPeters_LandingProps_AtBothBoardFrames()
        {
            RequireAGraphicsDevice();yield return LoadRegion(StPeters);EnsureTheFollow();HoldTheMood();
            var held=GameServices.Environment;Directory.CreateDirectory(LandingPropsPlates);
            string numbers=Path.Combine(LandingPropsPlates,"board-numbers.txt");File.WriteAllText(numbers,"");
            var ids=new[]{"prop.stp_landing_cart","prop.stp_landing_traps","prop.stp_landing_totes"};
            var keys=new[]{"landingCart","landingTraps","landingTotes"};
            for(int i=0;i<ids.Length;i++)
            {
                var go=GameObject.Find(ids[i]);Assert.IsNotNull(go,ids[i]);
                var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>("Assets/_Project/Data/SetPieces/SetPiece_"+keys[i]+".asset");
                Assert.AreSame(d.SpriteFor(SetPieceDef.FrameForRigDir(4)),go.GetComponent<SpriteRenderer>().sprite,ids[i]);
                File.AppendAllText(numbers,$"{ids[i]} pos={go.transform.position:R} sprite={go.GetComponent<SpriteRenderer>().sprite.name}\n");
            }
            try
            {
                foreach(var board in new[]{new{Id="landing.wide_approach.1431",Hour=14+31f/60,Tide=2.18f,At=new Vector2(203,2.649f)},
                    new{Id="cannery.wide_landing.0600",Hour=6f,Tide=-.85f,At=new Vector2(181,12.596f)}})
                {
                    yield return PinTheHour(board.Hour,ClearRunSeconds);yield return LandingPropsTide(held,board.Tide);yield return ShadersReady();
                    LandingPropsCamera(board.At);yield return null;LandingPropsSave(board.Id);
                    Assert.That(GameServices.Clock.DayIndex,Is.EqualTo(1));
                    File.AppendAllText(numbers,$"{board.Id} day=1 hour={GameServices.Clock.HourOfDay:R} tide={board.Tide:R} camera={_cam.transform.position:R} worldHeight=14 aspect={_cam.aspect:R} size=796x448\n");
                    using var draws=OfferedCounter("Draw Calls Count");for(int n=0;n<5;n++)yield return null;
                    long count=draws.Valid?draws.LastValue:-1;if(count<=0)count=UnityEditor.UnityStats.drawCalls;
                    Assert.Greater(count,0);File.AppendAllText(numbers,$"drawCalls={count}\n");
                }
                yield return PinTheHour(12,ClearRunSeconds);yield return LandingPropsTide(held,0);
                LandingPropsCamera(new Vector2(190,5.5f),8);yield return null;LandingPropsSave("landing-props-detail");
            }
            finally{GameServices.Environment=held;LandingPropsReleaseCamera();}
        }
        [UnityTest] public IEnumerator StPeters_LadderArt_AtLowAndHighWater()
        {
            RequireAGraphicsDevice();yield return LoadRegion(StPeters);EnsureTheFollow();HoldTheMood();
            var held=GameServices.Environment;yield return PinTheHour(12,ClearRunSeconds);
            var original=GameObject.Find("Fitting_ladder");Assert.IsNotNull(original);
            var oldRenderer=original.GetComponent<SpriteRenderer>();Assert.IsNotNull(oldRenderer);
            var def=AssetDatabase.LoadAssetAtPath<SetPieceDef>("Assets/_Project/Data/SetPieces/SetPiece_landingLadder.asset");Assert.IsNotNull(def);
            var maps=JsonUtility.FromJson<LandingPropsHeightContract>(File.ReadAllText("Assets/_Project/Art/Sprites/StPeters/LandingProps/stpLandingProps.height-contract.json"));
            var map=maps.maps.Single(x=>x.key=="landingLadder");
            var preview=new GameObject("LandingLadderArtPreview");_spawned.Add(preview);preview.transform.position=original.transform.position;
            var sr=preview.AddComponent<SpriteRenderer>();sr.sprite=def.SpriteFor(4);sr.sortingOrder=oldRenderer.sortingOrder;
            var water=preview.AddComponent<TidalFaceWaterline>();water.Configure(preview.transform.position.y,5.354004f);
            water.ConfigureHeight(AssetDatabase.LoadAssetAtPath<Texture2D>(map.path),map.minimumLocalZ,map.maximumLocalZ-map.minimumLocalZ,-5.354004f);
            var figureObject=new GameObject("LandingRig10ScaleFigure");_spawned.Add(figureObject);figureObject.transform.position=preview.transform.position+new Vector3(-1.3f,.4f,0);
            var sort=figureObject.AddComponent<SpriteRenderer>();sort.sortingOrder=sr.sortingOrder+1;
            var skin=AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");
            Assert.AreEqual("10.3",skin.SourceRigRevision);
            var figure=figureObject.AddComponent<IsoCharacterFigureRenderer>();figure.Configure(skin);Assert.IsTrue(figure.EnterAshore(sort));
            Assert.IsTrue(figure.SetPose("idle",0));figure.SetAshoreYaw(180);
            var dory=GameObject.Find("Dory");Assert.IsNotNull(dory);Vector3 doryBefore=dory.transform.position;
            Quaternion doryRotationBefore=dory.transform.rotation;
            var body=dory.GetComponent<Rigidbody2D>();Assert.IsNotNull(body);
            Vector2 bodyBefore=body.position,velocityBefore=body.linearVelocity;
            float bodyRotationBefore=body.rotation,angularVelocityBefore=body.angularVelocity;
            var boat=dory.GetComponent<HiddenHarbours.Boats.BoatController>();Assert.IsNotNull(boat.Hull);
            var ride=dory.GetComponent<HiddenHarbours.Boats.HullTideRide>();Assert.IsNotNull(ride);
            var proofBerth=new GameObject("LandingLadderTemporaryProofBerth");proofBerth.SetActive(false);_spawned.Add(proofBerth);
            var anchor=proofBerth.AddComponent<HiddenHarbours.App.RegionAnchor>();
            anchor.Configure(GameServices.CurrentRegionId,proofBerth.transform,null,null);
            float halfBeam=boat.Hull.Visual.HullMesh.WatertightHalfBeamMeters;
            anchor.ConfigureAlongsideBerth(halfBeam);
            bool oldEnabled=oldRenderer.enabled;oldRenderer.enabled=false;
            string numbers=Path.Combine(LandingPropsPlates,"ladder-numbers.txt");Directory.CreateDirectory(LandingPropsPlates);File.WriteAllText(numbers,"");
            string RendererPath(Transform at)=>at==dory.transform?dory.name:RendererPath(at.parent)+"/"+at.name;
            void WriteRenderers(string stage)
            {
                foreach(var renderer in dory.GetComponentsInChildren<Renderer>(true))
                    File.AppendAllText(numbers,$"{stage} renderer={RendererPath(renderer.transform)} type={renderer.GetType().Name} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} follows={renderer.transform.parent?.name} position={renderer.transform.position:R} boundsMin={renderer.bounds.min:R} boundsMax={renderer.bounds.max:R}\n");
                File.AppendAllText(numbers,$"{stage} doryRoot={dory.transform.position:R} body={body.position:R} bakedWaterline={ride.BakedWaterlineElevation:R} tideScreenRise={ride.ScreenRiseNow():R}\n");
            }
            try
            {
                WriteRenderers("loaded-before-proof");
                var visual=dory.transform.Find(HiddenHarbours.Boats.BoatHullSkinner.VisualChildName);Assert.IsNotNull(visual);
                var facet=visual.GetComponent<IsoFacetHullRenderer>();
                Renderer[] hullRenderers;
                if(facet!=null&&facet.IsConfigured)
                    hullRenderers=new Renderer[]{visual.Find("FacetMesh").GetComponent<MeshRenderer>(),visual.Find("HullOverlay").GetComponent<MeshRenderer>()};
                else
                    hullRenderers=new Renderer[]{visual.GetComponent<SpriteRenderer>()};
                foreach(var hull in hullRenderers)Assert.IsNotNull(hull,"The game's Dory must carry its own detailed hull renderer.");
                var figureRenderers=figureObject.GetComponentsInChildren<Renderer>().Where(x=>x.enabled&&x.gameObject.activeInHierarchy).ToArray();
                Assert.IsNotEmpty(figureRenderers);
                foreach(float level in new[]{-2.2f,2.2f})
                {
                    yield return LandingPropsTide(held,level);
                    // Temporary proof berth only. Production placement belongs to the traversal lane.
                    proofBerth.transform.SetPositionAndRotation(new Vector3(preview.transform.position.x,
                        preview.transform.position.y-HiddenHarbours.App.Editor.StPetersBuilder.AlongsideFenderGapMetres-halfBeam,doryBefore.z),doryRotationBefore);
                    HiddenHarbours.App.RegionTravelCoordinator.LayAlongside(dory.transform,anchor);
                    body.position=dory.transform.position;body.rotation=dory.transform.eulerAngles.z;
                    body.linearVelocity=Vector2.zero;body.angularVelocity=0;Physics2D.SyncTransforms();
                    for(int n=0;n<5;n++)yield return null;
                    string id=level<0?"ladder-low":"ladder-high";WriteRenderers(id);
                    Bounds frame=sr.bounds,figureBounds=figureRenderers[0].bounds;
                    foreach(var renderer in figureRenderers)figureBounds.Encapsulate(renderer.bounds);
                    frame.Encapsulate(figureBounds);
                    foreach(var hull in hullRenderers)
                    {
                        string name=RendererPath(hull.transform);
                        Assert.IsTrue(hull.enabled,name+" must be enabled as play draws it.");
                        Assert.IsTrue(hull.gameObject.activeInHierarchy,name+" must be active as play draws it.");
                        frame.Encapsulate(hull.bounds);
                    }
                    frame.Expand(figureBounds.size.y);
                    LandingPropsCamera(frame.center,Mathf.Max(frame.size.y,frame.size.x/(24.89f/14)));
                    yield return ShadersReady();yield return null;
                    foreach(var hull in hullRenderers)
                    {
                        string name=RendererPath(hull.transform);Bounds bounds=hull.bounds;
                        Assert.IsTrue(hull.enabled,name+" must remain enabled at capture.");
                        File.AppendAllText(numbers,$"{id} assertedHull={name} boundsMin={bounds.min:R} boundsMax={bounds.max:R}\n");
                        for(int x=0;x<=1;x++)for(int y=0;y<=1;y++)for(int z=0;z<=1;z++)
                        {
                            var corner=new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z);
                            Vector3 viewport=_cam.WorldToViewportPoint(corner);
                            File.AppendAllText(numbers,$"{id} renderer={name} corner={corner:R} viewport={viewport:R}\n");
                            Assert.That(viewport.x,Is.InRange(0f,1f),name+" corner x");
                            Assert.That(viewport.y,Is.InRange(0f,1f),name+" corner y");
                        }
                    }
                    LandingPropsSave(id);
                    Assert.That(5.354004f-8.1f,Is.LessThan(-2.2f));
                    File.AppendAllText(numbers,$"{id} top=5.354004 foot=-2.745996 rung=.30 standoff=.275 tide={level:R} hanger={preview.transform.position:R} doryProofRoot={dory.transform.position:R} body={body.position:R} camera={_cam.transform.position:R} worldHeight={_cam.orthographicSize*2:R} figureRig={skin.SourceRigRevision} source={skin.SourceRigPath} decodeMin={map.minimumLocalZ:R} decodeRange={map.maximumLocalZ-map.minimumLocalZ:R} datumShift=-5.354004 static art proof; no boarding motion\n");
                }
            }
            finally
            {
                dory.transform.SetPositionAndRotation(doryBefore,doryRotationBefore);body.position=bodyBefore;body.rotation=bodyRotationBefore;
                body.linearVelocity=velocityBefore;body.angularVelocity=angularVelocityBefore;Physics2D.SyncTransforms();
                oldRenderer.enabled=oldEnabled;figure.LeaveAshore();GameServices.Environment=held;LandingPropsReleaseCamera();
            }
        }
#endif
    }
}
