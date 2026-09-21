using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WakeCentrePhotographPlayTests
    {
        const float AttachmentToleranceMetres = 2f / 32f;
        float _v24PreviousCaptureDelta;
        bool _v24LoadedIntro;
        readonly HashSet<string> _v24RendererPaths = new HashSet<string>();

        [UnitySetUp]
        public IEnumerator V24FixedFrameSetup()
        {
            _v24PreviousCaptureDelta = Time.captureDeltaTime;
            if (TestContext.CurrentContext.Test.Name.StartsWith("V24_")) Time.captureDeltaTime = 1f/30f;
            yield return null;
        }

        byte[] ShootNamed(Transform wakeRoot, FoamInjector injector, string family)
        {
            if (_emitter == null) _emitter = Object.FindFirstObjectByType<BoatWakeEmitter>();
            if (_emitter != null) _emitter.enabled = false;
            foreach (Transform child in wakeRoot)
            {
                var r = child.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    string path = $"RENDERER family={child.name} shader={r.sharedMaterial.shader.name}";
                    if (_v24RendererPaths.Add(path)) _rows.Add(path);
                }
                if (r != null) r.enabled = child.gameObject.activeSelf && (family == "ALL" || child.name == family);
            }
            if (family != "ALL")
            {
                if (injector != null) injector.enabled = false;
                FoamInjectionRegistry.PublishLookStrength(0f);
            }
            return Capture();
        }

        [UnityTest]
        public IEnumerator V24_MeshPoseOracle_FleetHeadingsPitchTideAndSwaps()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion();
            yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(1, out var anchors, out _), "No verified deep-water anchor");
            var root = new GameObject("v24-oracle-fleet");
            _spawned.Add(root);
            root.transform.position = anchors[0];
            var boat = root.AddComponent<BoatController>();
            boat.SetLocalSeabedDepth(40f);
            var rb = root.GetComponent<Rigidbody2D>();
            rb.simulated = false;
            boat.enabled = false;
            var picker = root.AddComponent<DevBoatPicker>();
            picker.Configure(System.Array.Empty<BoatHullDef>(), boat, null, null);
            yield return FrameOn(root.transform, new Bounds(root.transform.position, new Vector3(40f,40f,0f)));
            var failures = new StringBuilder();
            int checkedHulls = 0, samples = 0, rejectedOldHeading = 0;
            float maxPose = 0f, maxBirth = 0f;
#if UNITY_EDITOR
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[]{"Assets/_Project/Data/Boats"}))
            {
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (hull.Visual == null || !hull.Visual.HasHullMesh()) continue;
                picker.Show(hull);
                yield return null;
                var driver = root.GetComponent<MeshHullDriver>();
                var facet = root.GetComponentInChildren<IsoFacetHullRenderer>();
                var injector = root.GetComponentInChildren<FoamInjector>();
                Assert.IsNotNull(driver, hull.Id); Assert.IsNotNull(facet, hull.Id); Assert.IsNotNull(injector, hull.Id);
                var motion = root.GetComponent<BoatWaveMotion>();
                if (motion != null) motion.enabled = false;
                var def = hull.Visual.HullMesh;
                // The stern station is verified against the original rig's loft by WakeSternOffsetRigTests.
                // The oracle transforms the authored point through the ACTUAL GPU mesh transform,
                // never through the wake/presenter projection helper being tested.
                var local = new Vector3(0f, -def.WakeSternOffsetMeters, def.RestingDraftMeters);
                var sternMethod = typeof(FoamInjector).GetMethod("SternWorld", BindingFlags.Instance|BindingFlags.NonPublic);
                float hullMax = 0f;
                foreach (float heading in new[]{0f,45f,90f,135f,180f,225f,270f,315f,259.8f,195f})
                foreach (float pitch in new[]{-6f,0f,6f})
                foreach (float tide in new[]{-0.7f,0.9f})
                {
                    root.transform.rotation = Quaternion.Euler(0f,0f,-heading);
                    driver.Visual.position = root.transform.position + Vector3.up * tide;
                    driver.RockFrame = -1;
                    driver.SetStormRock(1f, 2f, pitch);
                    driver.SetDisplacedHeaveMeters(0.25f);
                    driver.Drive(); facet.ApplyPose();
                    Vector2 oracle = facet.PosedMesh.TransformPoint(local);
                    Assert.IsTrue(driver.TryGetWakePose(out HullWakePose pose));
                    float poseError = Vector2.Distance(oracle, pose.DrawnStern);
                    Vector2 birth = (Vector2)sternMethod.Invoke(injector, null);
                    Vector2 drawnBirth = birth + Vector2.up * (tide + V24SurfaceLift(birth));
                    float birthError = Vector2.Distance(oracle, drawnBirth);
                    Vector2 sabotaged = WakeRootMath.SternWorld(driver.Visual.position, driver.Visual.up,
                        def.WakeSternOffsetMeters, def.ElevationDeg);
                    if (heading == 270f && Vector2.Distance(sabotaged, oracle) > AttachmentToleranceMetres)
                        rejectedOldHeading++;
                    hullMax = Mathf.Max(hullMax, birthError); maxPose = Mathf.Max(maxPose,poseError);
                    maxBirth = Mathf.Max(maxBirth,birthError); samples++;
                    if (poseError > AttachmentToleranceMetres || birthError > AttachmentToleranceMetres)
                        failures.AppendLine($"{hull.Id} heading={heading} pitch={pitch} tide={tide} pose={poseError:F5}m birth={birthError:F5}m");
                }
                checkedHulls++;
                _rows.Add($"ORACLE {hull.Id} 60 poses; max birth={hullMax:F5}m; pixels={hullMax*_h/(2f*_cam.orthographicSize):F4}; mesh={def.name}");
            }
#endif
            _rows.Add($"ORACLE SUMMARY hulls={checkedHulls} samples={samples} maxPose={maxPose:F6}m maxBirth={maxBirth:F6}m tolerance={AttachmentToleranceMetres}m pixelsPerMetre={_h/(2f*_cam.orthographicSize):F5} oldHeadingRejected={rejectedOldHeading}");
            WriteMeasurements("v24-pose-oracle");
            Assert.GreaterOrEqual(checkedHulls,4);
            Assert.GreaterOrEqual(rejectedOldHeading,checkedHulls);
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        [UnityTest]
        public IEnumerator V24_Cape_ContinuousAccelerationDecelerationAndRest()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion();
            yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(1,out var anchors,out _));
            var hull=LoadAsset<BoatHullDef>(CapeHullPath);
            var visual=LoadAsset<BoatVisualDef>(CapeVisualPath);
            var start=anchors[0]-Vector2.right*36f;
            var she=Spawn("v24-ramp",hull,visual,false,new Leg("ramp",90f,0f),start);
            she.Boat.enabled=false; she.Rb.simulated=false;
            var probe=she.Go.AddComponent<V24RenderedSternProbe>(); probe.Configure(she.Boat);
            yield return FrameOn(she.Go.transform,new Bounds(anchors[0],new Vector3(115f,80f,0f)));
            Time.timeScale=1f;
            for(int frame=1;frame<=720;frame++)
            {
                float speed=frame<=180 ? frame/15f : Mathf.Max(0f,24f-frame/15f);
                Vector2 current=GameServices.Environment.Sample().CurrentVector;
                she.Rb.linearVelocity=Vector2.right*speed+current;
                she.Go.transform.position+=(Vector3)(she.Rb.linearVelocity/30f);
                yield return null;
                if(frame==90 || frame==180 || frame==270 || frame==360 || frame==720)
                {
                    Time.timeScale=0f;
                    Transform rig=FindWakeRoot(she.Go.name); Assert.IsNotNull(rig);
                    byte[] complete=ShootNamed(rig,she.Injector,"ALL");
                    SavePlate($"v24-ramp-frame-{frame}-speed-{speed}-complete.png",complete);
                    int live=0;
                    foreach(Transform child in rig)
                        if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll")) live++;
                    _rows.Add($"RAMP frame={frame} speed={speed} liveCrests={live} poseError={probe.MaxPoseError:F6}m birthError={probe.MaxBirthError:F6}m metresPerPixel={2f*_cam.orthographicSize/_h:F6}");
                    if(frame==720) Assert.AreEqual(0,live,"Twelve seconds at rest must exhaust the wake");
                    RestoreRig(rig,she.Injector); Time.timeScale=1f;
                }
            }
            Assert.LessOrEqual(probe.MaxBirthError,AttachmentToleranceMetres);
            WriteMeasurements("v24-ramp");
        }

        internal static float V24SurfaceLift(Vector2 at)
        {
            if (!DisplacedSea.TryGet(out DisplacedSeaState sea)) return 0f;
            var packed = WaveFieldBridge.ReadPublishedField();
            float wave = WaveFieldBridge.ShaderTwinSample(at,in packed,sea.FreqScale,GameServices.FetchEnvelopeAt(at)).Height;
            float level = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            return ShoreFadeMath.DisplacedHeight(wave,level-GameServices.TidalTerrain.ElevationAt(at),sea.ShoreFadeBandMeters,sea.Exaggeration);
        }

        [UnityTest]
        public IEnumerator V24_Cape_CardinalsAndBothTurns()
        {
            RequireAGraphicsDevice();
            yield return Photograph("v24-cape", CapeHullPath, CapeVisualPath, false);
        }

        [UnityTest]
        public IEnumerator V24_Dory_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-dory",DoryHullPath,DoryVisualPath,false); }

        [UnityTest]
        public IEnumerator V24_Punt_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-punt","Assets/_Project/Data/Boats/Punt.asset","Assets/_Project/Data/Boats/Visuals/PuntIsoBasic.asset",false); }

        [UnityTest]
        public IEnumerator V24_AuthoredIntroRoute_RenderedSternThroughScriptedTurns()
        {
            RequireAGraphicsDevice();
            LogAssert.ignoreFailingMessages=true;
            _v24LoadedIntro=true;
            yield return SceneManager.LoadSceneAsync("StPeters",LoadSceneMode.Single);
            for(int i=0;i<8;i++) yield return null;
            yield return SetHour(6f);
            Time.timeScale=1f;
            var opening=Object.FindAnyObjectByType<ArrivalOpening>();
            Assert.IsNotNull(opening,"Authored StPeters opening missing");
            if(opening.Boat==null)
            {
                typeof(ArrivalOpening).GetField("_alwaysRunInEditor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(opening,true);
                Assert.IsTrue(opening.TryBegin(),"Authored intro could not start");
            }
            var boat=opening.Boat;
            var probe=boat.gameObject.AddComponent<V24RenderedSternProbe>();
            probe.Configure(boat);
            float start=Time.realtimeSinceStartup;
            int lastLeg=-1, plates=0;
            var route=opening.Route;
            while(Time.realtimeSinceStartup-start<180f && opening.Current!=ArrivalOpening.Phase.Moored)
            {
                yield return null;
                int nearest=0;float distance=float.MaxValue;
                for(int i=0;i<route.Length;i++)
                { float d=Vector2.Distance(boat.transform.position,route[i]); if(d<distance){distance=d;nearest=i;} }
                if(nearest!=lastLeg)
                {
                    lastLeg=nearest;
                    Time.timeScale=0f;
                    yield return FrameOn(boat.transform,new Bounds(boat.transform.position,new Vector3(55f,42f,0f)));
                    SavePlate($"v24-intro-mark-{nearest}.png",Capture());
                    _rows.Add($"INTRO mark={nearest} heading={ArrivalPilot.CompassOf(boat.transform.up):F3} speed={boat.Velocity.magnitude:F3} phase={opening.Current} clock={GameServices.Clock.TotalSeconds} frame={Time.frameCount} ortho={_cam.orthographicSize}");
                    plates++;Time.timeScale=1f;
                }
                if(plates>=route.Length && probe.HeadingSweep>30f) break;
            }
            _rows.Add($"INTRO samples={probe.Samples} headingSweep={probe.HeadingSweep:F3} maxPose={probe.MaxPoseError:F6}m maxBirth={probe.MaxBirthError:F6}m plates={plates} phase={opening.Current}");
            WriteMeasurements("v24-authored-intro");
            Assert.Greater(probe.Samples,30);
            Assert.Greater(probe.HeadingSweep,30f,"Intro did not execute its turns");
            Assert.GreaterOrEqual(plates,3,"Intro did not reach the approach marks");
            Assert.Less(probe.MaxPoseError,AttachmentToleranceMetres);
            Assert.Less(probe.MaxBirthError,AttachmentToleranceMetres);
        }

        [UnityTest]
        public IEnumerator V24_Lobster_CardinalsAndBothTurns()
        { RequireAGraphicsDevice(); yield return Photograph("v24-lobster","Assets/_Project/Data/Boats/LobsterBoat.asset","Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset",false); }

        [UnityTest]
        public IEnumerator V24_Cape_SpeedAndAgeResponse()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(); yield return SetHour(ShotHour);
            Assert.IsTrue(TryLegAnchors(4,out var starts,out _));
            var hull=LoadAsset<BoatHullDef>(CapeHullPath);
            var visual=LoadAsset<BoatVisualDef>(CapeVisualPath);
            float previousAlpha=-1f, previousScale=-1f;
            var errors=new StringBuilder();
            int index=0;
            foreach(float speed in new[]{0.5f,2f,6f,12f})
            {
                var leg=new Leg("speed-"+speed,90f,0f);
                var she=Spawn("v24-response",hull,visual,false,leg,starts[index++]);
                she.Boat.enabled=false; she.Rb.simulated=false;
                yield return FrameOn(she.Go.transform,new Bounds(starts[index-1]+Vector2.right*12f,new Vector3(95f,65f,0f)));
                Time.timeScale=1f;
                var current=GameServices.Environment.Sample().CurrentVector;
                for(int frame=0;frame<90;frame++)
                {
                    she.Rb.linearVelocity=Vector2.right*speed+current;
                    she.Go.transform.position+=(Vector3)(she.Rb.linearVelocity/30f);
                    yield return null;
                }
                Time.timeScale=0f;
                Transform rig=FindWakeRoot(she.Go.name);
                Assert.IsNotNull(rig);
                float alpha=0f, scale=0f;
                foreach(Transform child in rig)
                    if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll"))
                    {
                        var sr=child.GetComponent<SpriteRenderer>();
                        alpha=Mathf.Max(alpha,sr.color.a);scale=Mathf.Max(scale,child.localScale.x);
                    }
                byte[] complete=ShootNamed(rig,she.Injector,"ALL");
                byte[] bare=ShootNamed(rig,she.Injector,"NONE");
                SavePlate($"v24-response-speed-{speed}-complete.png",complete);
                SavePlate($"v24-response-speed-{speed}-bare.png",bare);
                WorstChannelDelta(complete,bare,out int pixels);
                _rows.Add($"RESPONSE speed={speed} frame=90 time={GameServices.Clock.TotalSeconds} crestPeakAlpha={alpha:F6} crestLength={scale:F6} visiblePixels={pixels} metresPerPixel={2f*_cam.orthographicSize/_h:F6}");
                if(alpha<=previousAlpha || scale<=previousScale) errors.AppendLine($"speed {speed}: alpha {alpha} <= {previousAlpha} or scale {scale} <= {previousScale}");
                previousAlpha=alpha;previousScale=scale;
                if(speed==12f)
                {
                    RestoreRig(rig,she.Injector); Time.timeScale=1f;
                    she.Rb.linearVelocity=current;
                    for(int frame=0;frame<360;frame++)
                    {
                        she.Go.transform.position+=(Vector3)(current/30f);
                        yield return null;
                        if(frame==89 || frame==359)
                        {
                            Time.timeScale=0f;
                            byte[] age=ShootNamed(rig,she.Injector,"ALL");
                            SavePlate($"v24-response-release-age-{(frame+1)/30}-complete.png",age);
                            int live=0;
                            foreach(Transform child in rig)
                                if(child.gameObject.activeSelf && (child.name=="crest" || child.name=="sternRoll")) live++;
                            _rows.Add($"RELEASE age={(frame+1)/30} liveCrests={live}");
                            if(frame==359 && live!=0) errors.AppendLine($"Crests remained after 12 s at zero through-water speed: {live}");
                            RestoreRig(rig,she.Injector);Time.timeScale=1f;
                        }
                    }
                }
                RestoreRig(rig,she.Injector); Time.timeScale=1f;
                _spawned.Remove(she.Go);Object.Destroy(she.Go);yield return null;
            }
            WriteMeasurements("v24-speed-age");
            Assert.IsEmpty(errors.ToString(),errors.ToString());
        }
    }

    [DefaultExecutionOrder(40)]
    public sealed class V24RenderedSternProbe : MonoBehaviour
    {
        MeshHullDriver _driver;
        IsoFacetHullRenderer _facet;
        FoamInjector _injector;
        Vector3 _local;
        float _firstHeading;
        public int Samples { get; private set; }
        public float MaxPoseError { get; private set; }
        public float MaxBirthError { get; private set; }
        public float HeadingSweep { get; private set; }
        public void Configure(BoatController boat)
        {
            _driver=boat.GetComponent<MeshHullDriver>();
            _facet=boat.GetComponentInChildren<IsoFacetHullRenderer>();
            _injector=boat.GetComponentInChildren<FoamInjector>();
            var def=boat.Hull.Visual.HullMesh;
            _local=new Vector3(0f,-def.WakeSternOffsetMeters,def.RestingDraftMeters);
            _firstHeading=ArrivalPilot.CompassOf(boat.transform.up);
        }
        void LateUpdate()
        {
            if(_driver==null || _facet==null || _injector==null || !_driver.TryGetWakePose(out var pose)) return;
            Vector2 oracle=_facet.PosedMesh.TransformPoint(_local);
            MaxPoseError=Mathf.Max(MaxPoseError,Vector2.Distance(oracle,pose.DrawnStern));
            Vector2 born=_injector.EmissionStern;
            Vector2 drawn=born+Vector2.up*(pose.TideRise+WakeCentrePhotographPlayTests.V24SurfaceLift(born));
            MaxBirthError=Mathf.Max(MaxBirthError,Vector2.Distance(oracle,drawn));
            HeadingSweep=Mathf.Max(HeadingSweep,Mathf.Abs(Mathf.DeltaAngle(_firstHeading,ArrivalPilot.CompassOf(transform.up))));
            Samples++;
        }
    }
}
